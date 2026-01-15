using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using MarketDataCollector.Application.Config;
using MarketDataCollector.Application.Logging;
using MarketDataCollector.Domain.Collectors;
using MarketDataCollector.Domain.Events;
using MarketDataCollector.Domain.Models;
using MarketDataCollector.Infrastructure.Providers;
using MarketDataCollector.Infrastructure.Resilience;
using Polly;
using Serilog;

namespace MarketDataCollector.Infrastructure.Providers.Polygon;

/// <summary>
/// Polygon.io WebSocket market data client that implements the IMarketDataClient abstraction.
///
/// Current support:
/// - Trades: YES (streams "T" messages and forwards to TradeDataCollector)
/// - Quotes: YES (streams "Q" messages and forwards to QuoteCollector)
/// - Aggregates: Not yet implemented
///
/// Connection Resilience:
/// - Uses Polly-based WebSocketResiliencePolicy for connection retry with exponential backoff
/// - Implements circuit breaker pattern to prevent cascading failures
/// - Automatic reconnection on connection loss with jitter
/// - Configurable retry attempts (default: 5) with 2s base delay, max 30s between retries
///
/// Authentication:
/// - Polygon uses API key authentication via WebSocket message
/// - API key is read from PolygonOptions or POLYGON_API_KEY environment variable
/// </summary>
public sealed class PolygonMarketDataClient : IMarketDataClient
{
    private readonly ILogger _log = LoggingSetup.ForContext<PolygonMarketDataClient>();
    private readonly IMarketEventPublisher _publisher;
    private readonly TradeDataCollector _tradeCollector;
    private readonly QuoteCollector _quoteCollector;
    private readonly PolygonOptions _opt;
    private readonly string? _apiKey;

    private ClientWebSocket? _ws;
    private Task? _recvLoop;
    private CancellationTokenSource? _cts;
    private WebSocketHeartbeat? _heartbeat;

    private readonly object _gate = new();
    private readonly HashSet<string> _tradeSymbols = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _quoteSymbols = new(StringComparer.OrdinalIgnoreCase);

    // Resilience pipeline for connection retry with exponential backoff
    private readonly ResiliencePipeline _connectionPipeline;

    // Subscription ID management
    private int _nextSubId = 200_000; // Keep away from IB and Alpaca IDs
    private readonly Dictionary<int, (string Symbol, string Kind)> _subs = new();

    // Reconnection state
    private volatile bool _isReconnecting;
    private readonly SemaphoreSlim _reconnectGate = new(1, 1);

    // Cached serializer options to avoid allocations in hot path
    private static readonly JsonSerializerOptions s_serializerOptions = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public PolygonMarketDataClient(
        IMarketEventPublisher publisher,
        TradeDataCollector tradeCollector,
        QuoteCollector quoteCollector,
        PolygonOptions? options = null)
    {
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        _tradeCollector = tradeCollector ?? throw new ArgumentNullException(nameof(tradeCollector));
        _quoteCollector = quoteCollector ?? throw new ArgumentNullException(nameof(quoteCollector));
        _opt = options ?? new PolygonOptions();

        // Get API key from options or environment variable
        _apiKey = _opt.ApiKey ?? Environment.GetEnvironmentVariable("POLYGON_API_KEY");

        // Initialize resilience pipeline with exponential backoff
        _connectionPipeline = WebSocketResiliencePolicy.CreateComprehensivePipeline(
            maxRetries: 5,
            retryBaseDelay: TimeSpan.FromSeconds(2),
            circuitBreakerFailureThreshold: 5,
            circuitBreakerDuration: TimeSpan.FromSeconds(30),
            operationTimeout: TimeSpan.FromSeconds(30));
    }

    public bool IsEnabled => !string.IsNullOrWhiteSpace(_apiKey);

    /// <summary>
    /// Connects to Polygon WebSocket stream and authenticates.
    /// </summary>
    public async Task ConnectAsync(CancellationToken ct = default)
    {
        if (_ws != null) return;

        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            _log.Warning("Polygon API key not configured. Set POLYGON_API_KEY environment variable or configure in settings. Running in stub mode.");
            _publisher.TryPublish(MarketEvent.Heartbeat(DateTimeOffset.UtcNow, source: "PolygonStub"));
            return;
        }

        // Polygon WebSocket endpoint based on feed type
        var cluster = _opt.Feed?.ToLowerInvariant() switch
        {
            "forex" => "forex",
            "crypto" => "crypto",
            "options" => "options",
            _ => "stocks"
        };

        var uri = _opt.UseDelayed
            ? new Uri($"wss://delayed.polygon.io/{cluster}")
            : new Uri($"wss://socket.polygon.io/{cluster}");

        _log.Information("Connecting to Polygon WebSocket at {Uri} (Feed: {Feed}, Delayed: {UseDelayed}) with retry policy",
            uri, cluster, _opt.UseDelayed);

        await _connectionPipeline.ExecuteAsync(async token =>
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            _ws = new ClientWebSocket();

            try
            {
                await _ws.ConnectAsync(uri, token).ConfigureAwait(false);
                _log.Information("Successfully connected to Polygon WebSocket");

                // Authenticate via message (required immediately after connection)
                var authMsg = JsonSerializer.Serialize(new { action = "auth", @params = _apiKey });
                await SendTextAsync(authMsg, token).ConfigureAwait(false);
                _log.Debug("Authentication message sent to Polygon");
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "Connection attempt to Polygon WebSocket failed at {Uri}. Will retry per policy.", uri);
                // Clean up failed connection attempt
                try { _ws?.Dispose(); } catch { }
                _ws = null;
                _cts?.Dispose();
                _cts = null;
                throw;
            }
        }, ct).ConfigureAwait(false);

        // Start heartbeat monitoring for stale connection detection
        if (_ws != null)
        {
            _heartbeat = new WebSocketHeartbeat(_ws, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(10));
            _heartbeat.ConnectionLost += OnConnectionLostAsync;
        }

        _recvLoop = Task.Run(() => ReceiveLoopAsync(_cts!.Token), _cts!.Token);
    }

    /// <summary>
    /// Handles automatic reconnection when connection is lost.
    /// </summary>
    private async Task OnConnectionLostAsync()
    {
        if (_isReconnecting) return;

        if (!await _reconnectGate.WaitAsync(0))
        {
            _log.Debug("Reconnection already in progress, skipping duplicate attempt");
            return;
        }

        try
        {
            _isReconnecting = true;
            _log.Warning("Polygon WebSocket connection lost, initiating automatic reconnection");

            await CleanupConnectionAsync();
            await ConnectAsync(CancellationToken.None);

            // Resubscribe to all active subscriptions
            if (_ws?.State == WebSocketState.Open)
            {
                await TrySendSubscribeAsync();
                _log.Information("Successfully reconnected and resubscribed to Polygon WebSocket");
            }
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Failed to reconnect to Polygon WebSocket after connection loss. " +
                "Manual intervention may be required.");
        }
        finally
        {
            _isReconnecting = false;
            _reconnectGate.Release();
        }
    }

    /// <summary>
    /// Cleans up the current connection without triggering reconnection.
    /// </summary>
    private async Task CleanupConnectionAsync()
    {
        var ws = _ws;
        var cts = _cts;
        var heartbeat = _heartbeat;

        _ws = null;
        _cts = null;
        _heartbeat = null;

        if (heartbeat != null)
        {
            heartbeat.ConnectionLost -= OnConnectionLostAsync;
            await heartbeat.DisposeAsync();
        }

        if (cts != null)
        {
            try { cts.Cancel(); } catch { }
            try { cts.Dispose(); } catch { }
        }

        if (ws != null)
        {
            try { ws.Dispose(); } catch { }
        }

        if (_recvLoop != null)
        {
            try { await _recvLoop.ConfigureAwait(false); } catch { }
            _recvLoop = null;
        }
    }

    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        _log.Information("Disconnecting from Polygon WebSocket");

        var ws = _ws;
        var cts = _cts;
        var heartbeat = _heartbeat;

        _ws = null;
        _cts = null;
        _heartbeat = null;

        // Dispose heartbeat first to prevent reconnection attempts
        if (heartbeat != null)
        {
            heartbeat.ConnectionLost -= OnConnectionLostAsync;
            await heartbeat.DisposeAsync();
        }

        if (cts != null)
        {
            try { cts.Cancel(); } catch { }
            try { cts.Dispose(); } catch { }
        }

        if (ws != null)
        {
            try
            {
                if (ws.State is WebSocketState.Open or WebSocketState.CloseReceived)
                    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "shutdown", ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "Error during Polygon WebSocket close, connection may have been lost");
            }
            try { ws.Dispose(); } catch { }
        }

        if (_recvLoop != null)
        {
            try { await _recvLoop.ConfigureAwait(false); } catch { }
        }
        _recvLoop = null;

        _log.Information("Disconnected from Polygon WebSocket");
    }

    public int SubscribeTrades(SymbolConfig cfg)
    {
        if (cfg is null) throw new ArgumentNullException(nameof(cfg));
        var symbol = cfg.Symbol.Trim().ToUpperInvariant();
        if (symbol.Length == 0) return -1;

        if (!_opt.SubscribeTrades) return -1;

        var id = Interlocked.Increment(ref _nextSubId);
        lock (_gate)
        {
            _tradeSymbols.Add(symbol);
            _subs[id] = (symbol, "trades");
        }

        _ = TrySendSubscribeAsync();
        return id;
    }

    public void UnsubscribeTrades(int subscriptionId)
    {
        (string Symbol, string Kind) sub;
        lock (_gate)
        {
            if (!_subs.TryGetValue(subscriptionId, out sub)) return;
            _subs.Remove(subscriptionId);
            if (sub.Kind == "trades")
            {
                if (!_subs.Values.Any(v => v.Kind == "trades" && v.Symbol.Equals(sub.Symbol, StringComparison.OrdinalIgnoreCase)))
                    _tradeSymbols.Remove(sub.Symbol);
            }
        }

        _ = TrySendSubscribeAsync();
    }

    public int SubscribeMarketDepth(SymbolConfig cfg)
    {
        // Polygon provides quotes (BBO) rather than full L2 depth
        // Subscribe to quotes if SubscribeQuotes is enabled
        if (!_opt.SubscribeQuotes) return -1;

        if (cfg is null) throw new ArgumentNullException(nameof(cfg));
        var symbol = cfg.Symbol.Trim().ToUpperInvariant();
        if (symbol.Length == 0) return -1;

        var id = Interlocked.Increment(ref _nextSubId);
        lock (_gate)
        {
            _quoteSymbols.Add(symbol);
            _subs[id] = (symbol, "quotes");
        }

        _ = TrySendSubscribeAsync();
        return id;
    }

    public void UnsubscribeMarketDepth(int subscriptionId)
    {
        (string Symbol, string Kind) sub;
        lock (_gate)
        {
            if (!_subs.TryGetValue(subscriptionId, out sub)) return;
            _subs.Remove(subscriptionId);
            if (sub.Kind == "quotes")
            {
                if (!_subs.Values.Any(v => v.Kind == "quotes" && v.Symbol.Equals(sub.Symbol, StringComparison.OrdinalIgnoreCase)))
                    _quoteSymbols.Remove(sub.Symbol);
            }
        }

        _ = TrySendSubscribeAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync().ConfigureAwait(false);
        _reconnectGate.Dispose();
    }

    private async Task TrySendSubscribeAsync()
    {
        try
        {
            var ws = _ws;
            if (ws == null || ws.State != WebSocketState.Open) return;

            string[] trades;
            string[] quotes;
            lock (_gate)
            {
                trades = _tradeSymbols.ToArray();
                quotes = _quoteSymbols.ToArray();
            }

            // Build Polygon subscription params: T.AAPL for trades, Q.AAPL for quotes
            var subscriptions = new List<string>();

            foreach (var sym in trades)
                subscriptions.Add($"T.{sym}");

            foreach (var sym in quotes)
                subscriptions.Add($"Q.{sym}");

            if (subscriptions.Count == 0) return;

            var msg = JsonSerializer.Serialize(new
            {
                action = "subscribe",
                @params = string.Join(",", subscriptions)
            }, s_serializerOptions);

            await SendTextAsync(msg, CancellationToken.None).ConfigureAwait(false);
            _log.Debug("Sent subscription request for {Count} channels to Polygon", subscriptions.Count);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Failed to send subscription update to Polygon WebSocket. " +
                "This may indicate a connection issue. Check network connectivity and Polygon service status.");
        }
    }

    private async Task SendTextAsync(string text, CancellationToken ct)
    {
        var ws = _ws ?? throw new InvalidOperationException("Not connected to Polygon.");
        var bytes = Encoding.UTF8.GetBytes(text);
        await ws.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, ct).ConfigureAwait(false);
    }

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        if (_ws == null) return;

        var ws = _ws;
        var buf = new byte[64 * 1024];
        var sb = new StringBuilder(128 * 1024);

        while (!ct.IsCancellationRequested && ws.State == WebSocketState.Open)
        {
            sb.Clear();

            WebSocketReceiveResult? res;
            do
            {
                res = await ws.ReceiveAsync(buf, ct).ConfigureAwait(false);
                if (res.MessageType == WebSocketMessageType.Close) return;
                sb.Append(Encoding.UTF8.GetString(buf, 0, res.Count));
            }
            while (!res.EndOfMessage);

            var json = sb.ToString();
            if (string.IsNullOrWhiteSpace(json)) continue;

            // Polygon sends arrays of objects: [{"ev":"T",...}, {"ev":"Q",...}]
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var el in doc.RootElement.EnumerateArray())
                        HandleMessage(el);
                }
                else if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    HandleMessage(doc.RootElement);
                }
            }
            catch (JsonException ex)
            {
                _log.Warning(ex, "Failed to parse Polygon WebSocket message. Raw JSON: {RawJson}. " +
                    "This may indicate a protocol change or malformed message.",
                    json.Length > 500 ? json[..500] + "..." : json);
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Unexpected error processing Polygon WebSocket message");
            }
        }
    }

    private void HandleMessage(JsonElement el)
    {
        // Get event type: "ev" field in Polygon messages
        if (!el.TryGetProperty("ev", out var evProp)) return;
        var ev = evProp.GetString();

        switch (ev)
        {
            case "T": // Trade
                HandleTrade(el);
                break;

            case "Q": // Quote (NBBO)
                HandleQuote(el);
                break;

            case "status": // Connection status message
                HandleStatus(el);
                break;

            case "AM": // Per-minute aggregate (not yet implemented)
            case "A": // Per-second aggregate (not yet implemented)
                // Could be implemented in future
                break;
        }
    }

    private void HandleTrade(JsonElement el)
    {
        // Polygon trade fields: sym=symbol, p=price, s=size, t=timestamp (Unix ms), x=exchange, i=trade ID
        var sym = el.TryGetProperty("sym", out var symProp) ? symProp.GetString() : null;
        if (string.IsNullOrWhiteSpace(sym)) return;

        var price = el.TryGetProperty("p", out var pProp) ? (decimal)pProp.GetDouble() : 0m;
        var size = el.TryGetProperty("s", out var sProp) ? sProp.GetInt32() : 0;
        var timestamp = el.TryGetProperty("t", out var tProp) ? tProp.GetInt64() : 0L;
        var exchange = el.TryGetProperty("x", out var xProp) ? xProp.GetInt32() : 0;
        var tradeId = el.TryGetProperty("i", out var iProp) ? iProp.GetString() : null;

        // Convert Unix milliseconds to DateTimeOffset
        var dto = timestamp > 0
            ? DateTimeOffset.FromUnixTimeMilliseconds(timestamp)
            : DateTimeOffset.UtcNow;

        // Convert exchange ID to name (simplified mapping)
        var venue = MapExchangeId(exchange);

        var update = new MarketTradeUpdate(
            Timestamp: dto,
            Symbol: sym!,
            Price: price,
            Size: size,
            Aggressor: AggressorSide.Unknown,
            SequenceNumber: long.TryParse(tradeId, out var seqNum) ? seqNum : 0L,
            StreamId: "POLYGON",
            Venue: venue
        );

        _tradeCollector.OnTrade(update);
    }

    private void HandleQuote(JsonElement el)
    {
        // Polygon quote fields: sym=symbol, bp=bid price, bs=bid size, ap=ask price, as=ask size, t=timestamp
        var sym = el.TryGetProperty("sym", out var symProp) ? symProp.GetString() : null;
        if (string.IsNullOrWhiteSpace(sym)) return;

        var bidPrice = el.TryGetProperty("bp", out var bpProp) ? (decimal)bpProp.GetDouble() : 0m;
        var bidSize = el.TryGetProperty("bs", out var bsProp) ? bsProp.GetInt64() : 0L;
        var askPrice = el.TryGetProperty("ap", out var apProp) ? (decimal)apProp.GetDouble() : 0m;
        var askSize = el.TryGetProperty("as", out var asProp) ? asProp.GetInt64() : 0L;
        var timestamp = el.TryGetProperty("t", out var tProp) ? tProp.GetInt64() : 0L;

        var dto = timestamp > 0
            ? DateTimeOffset.FromUnixTimeMilliseconds(timestamp)
            : DateTimeOffset.UtcNow;

        var quoteUpdate = new MarketQuoteUpdate(
            Timestamp: dto,
            Symbol: sym!,
            BidPrice: bidPrice,
            BidSize: bidSize,
            AskPrice: askPrice,
            AskSize: askSize,
            SequenceNumber: null,
            StreamId: "POLYGON",
            Venue: "POLYGON"
        );

        _quoteCollector.OnQuote(quoteUpdate);
    }

    private void HandleStatus(JsonElement el)
    {
        var status = el.TryGetProperty("status", out var statusProp) ? statusProp.GetString() : null;
        var message = el.TryGetProperty("message", out var msgProp) ? msgProp.GetString() : null;

        switch (status)
        {
            case "connected":
                _log.Information("Polygon WebSocket connected: {Message}", message);
                break;

            case "auth_success":
                _log.Information("Polygon authentication successful");
                _publisher.TryPublish(MarketEvent.Heartbeat(DateTimeOffset.UtcNow, source: "Polygon"));
                break;

            case "auth_failed":
                _log.Error("Polygon authentication failed: {Message}. Check your API key.", message);
                break;

            case "success":
                _log.Debug("Polygon subscription successful: {Message}", message);
                break;

            default:
                _log.Debug("Polygon status message: {Status} - {Message}", status, message);
                break;
        }
    }

    /// <summary>
    /// Maps Polygon exchange ID to exchange name.
    /// See: https://polygon.io/docs/stocks/get_v3_reference_exchanges
    /// </summary>
    private static string MapExchangeId(int exchangeId)
    {
        return exchangeId switch
        {
            1 => "NYSE",
            2 => "AMEX",
            3 => "ARCA",
            4 => "BATS",
            5 => "NASDAQ",
            6 => "NASDAQ_OMX",
            7 => "NYSE_ARCA",
            8 => "NYSE_NATIONAL",
            9 => "FINRA",
            10 => "ISE",
            11 => "EDGA",
            12 => "EDGX",
            13 => "CHX",
            14 => "NYSE_CHICAGO",
            15 => "DIRECT_EDGE_A",
            16 => "DIRECT_EDGE_X",
            17 => "IEX",
            19 => "NASDAQ_BX",
            20 => "NASDAQ_PSX",
            21 => "CBOE_BYX",
            22 => "CBOE_BZX",
            23 => "MEMX",
            24 => "MIAX",
            _ => $"EXCH_{exchangeId}"
        };
    }
}
