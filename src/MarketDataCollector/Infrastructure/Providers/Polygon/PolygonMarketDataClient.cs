using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using MarketDataCollector.Application.Config;
using MarketDataCollector.Application.Logging;
using MarketDataCollector.Domain.Collectors;
using MarketDataCollector.Domain.Events;
using MarketDataCollector.Domain.Models;
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
/// - Depth (L2): NO (Polygon provides BBO quotes, not full L2; SubscribeMarketDepth subscribes to quotes)
///
/// Connection Resilience:
/// - Uses Polly-based WebSocketResiliencePolicy for connection retry with exponential backoff
/// - Implements circuit breaker pattern to prevent cascading failures
/// - Automatic reconnection on connection loss with jitter
/// - Configurable retry attempts (default: 5) with 2s base delay, max 30s between retries
///
/// Notes:
/// - Authentication is performed by sending an "auth" message immediately after connect.
/// - Polygon WebSocket URL: wss://socket.polygon.io/{feed}
/// - Delayed data available at: wss://delayed.polygon.io/{feed}
/// </summary>
public sealed class PolygonMarketDataClient : IMarketDataClient
{
    private readonly ILogger _log = LoggingSetup.ForContext<PolygonMarketDataClient>();
    private readonly IMarketEventPublisher _publisher;
    private readonly TradeDataCollector _tradeCollector;
    private readonly QuoteCollector _quoteCollector;
    private readonly PolygonOptions _opt;
    private readonly bool _hasValidCredentials;

    private ClientWebSocket? _ws;
    private Task? _recvLoop;
    private CancellationTokenSource? _cts;
    private WebSocketHeartbeat? _heartbeat;

    private readonly object _gate = new();
    private readonly HashSet<string> _tradeSymbols = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _quoteSymbols = new(StringComparer.OrdinalIgnoreCase);

    // Resilience pipeline for connection retry with exponential backoff
    private readonly ResiliencePipeline _connectionPipeline;

    // Cached serializer options to avoid allocations in hot path
    private static readonly JsonSerializerOptions s_serializerOptions = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private int _nextSubId = 200_000; // keep away from IB and Alpaca ids
    private readonly Dictionary<int, (string Symbol, string Kind)> _subs = new();

    // Reconnection state
    private volatile bool _isReconnecting;
    private readonly SemaphoreSlim _reconnectGate = new(1, 1);

    public PolygonMarketDataClient(
        IMarketEventPublisher publisher,
        TradeDataCollector tradeCollector,
        QuoteCollector quoteCollector,
        PolygonOptions? opt = null)
    {
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        _tradeCollector = tradeCollector ?? throw new ArgumentNullException(nameof(tradeCollector));
        _quoteCollector = quoteCollector ?? throw new ArgumentNullException(nameof(quoteCollector));
        _opt = opt ?? new PolygonOptions();

        // Validate Polygon API key credentials
        _hasValidCredentials = ValidateCredentials(_opt);

        // Initialize resilience pipeline with exponential backoff
        _connectionPipeline = WebSocketResiliencePolicy.CreateComprehensivePipeline(
            maxRetries: 5,
            retryBaseDelay: TimeSpan.FromSeconds(2),
            circuitBreakerFailureThreshold: 5,
            circuitBreakerDuration: TimeSpan.FromSeconds(30),
            operationTimeout: TimeSpan.FromSeconds(30));
    }

    /// <summary>
    /// Validates the Polygon API key configuration.
    /// </summary>
    /// <param name="options">The Polygon options to validate.</param>
    /// <returns>True if credentials are valid, false otherwise.</returns>
    private bool ValidateCredentials(PolygonOptions options)
    {
        var apiKey = GetApiKey();

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _log.Warning("Polygon API key is not configured. Set POLYGON_API_KEY environment variable " +
                "or configure Polygon.ApiKey in appsettings.json. Client will run in stub mode.");
            return false;
        }

        // Basic format validation for Polygon API keys (typically alphanumeric, 32+ chars)
        if (apiKey.Length < 20)
        {
            _log.Warning("Polygon API key appears to be invalid (too short). " +
                "Please verify your API key at https://polygon.io/dashboard/api-keys");
            return false;
        }

        _log.Information("Polygon API key configured successfully (key length: {KeyLength})", apiKey.Length);
        return true;
    }

    /// <summary>
    /// Gets the configured API key, preferring environment variables over config.
    /// </summary>
    private string? GetApiKey()
    {
        var envApiKey = Environment.GetEnvironmentVariable("POLYGON_API_KEY")
                     ?? Environment.GetEnvironmentVariable("POLYGON__APIKEY");
        return !string.IsNullOrWhiteSpace(envApiKey) ? envApiKey : _opt.ApiKey;
    }

    public bool IsEnabled => _hasValidCredentials;

    /// <summary>
    /// Connects to Polygon WebSocket stream.
    /// Uses resilience pipeline for retry logic with exponential backoff.
    /// </summary>
    public async Task ConnectAsync(CancellationToken ct = default)
    {
        if (_ws != null) return;

        if (!_hasValidCredentials)
        {
            _log.Warning("Polygon client connect called without valid API key - running in stub mode. " +
                "Configure POLYGON_API_KEY environment variable for real data streaming.");
            // Emit a synthetic heartbeat so downstream consumers can verify connectivity
            _publisher.TryPublish(MarketEvent.Heartbeat(DateTimeOffset.UtcNow, source: "PolygonStub"));
            return;
        }

        var host = _opt.UseDelayed ? "delayed.polygon.io" : "socket.polygon.io";
        var uri = new Uri($"wss://{host}/{_opt.Feed}");

        _log.Information("Connecting to Polygon WebSocket at {Uri} (Delayed: {UseDelayed}) with retry policy", uri, _opt.UseDelayed);

        await _connectionPipeline.ExecuteAsync(async token =>
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            _ws = new ClientWebSocket();

            try
            {
                await _ws.ConnectAsync(uri, token).ConfigureAwait(false);
                _log.Information("Successfully connected to Polygon WebSocket");

                // Authenticate via message (required immediately after connection)
                var apiKey = GetApiKey();
                var authMsg = JsonSerializer.Serialize(new { action = "auth", @params = apiKey });
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
    /// Uses rate limiting to prevent reconnection storms.
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

            // Clean up existing connection
            await CleanupConnectionAsync();

            // Attempt to reconnect using the resilience pipeline
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
                _log.Warning(ex, "Error during WebSocket close, connection may have been lost");
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
        var symbol = cfg.Symbol.Trim();
        if (symbol.Length == 0) return -1;

        if (!_hasValidCredentials)
        {
            // Emit a lightweight synthetic trade for testing when not connected
            _tradeCollector.OnTrade(new MarketTradeUpdate(
                DateTimeOffset.UtcNow, symbol, 0m, 0,
                AggressorSide.Unknown, 0, "POLYGON", "STUB"));
            return -1;
        }

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
                // remove only if no remaining trade subs for this symbol
                if (!_subs.Values.Any(v => v.Kind == "trades" && v.Symbol.Equals(sub.Symbol, StringComparison.OrdinalIgnoreCase)))
                    _tradeSymbols.Remove(sub.Symbol);
            }
        }

        _ = TrySendUnsubscribeAsync(sub.Symbol, "T");
    }

    public int SubscribeMarketDepth(SymbolConfig cfg)
    {
        // Polygon provides BBO quotes, not full L2 depth updates.
        // We subscribe to quotes as a proxy for depth data.
        if (cfg is null) throw new ArgumentNullException(nameof(cfg));
        var symbol = cfg.Symbol.Trim();
        if (symbol.Length == 0) return -1;

        if (!_hasValidCredentials)
        {
            return -1;
        }

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

        _ = TrySendUnsubscribeAsync(sub.Symbol, "Q");
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

            string[] tradeParams;
            string[] quoteParams;
            lock (_gate)
            {
                // Polygon format: T.AAPL for trades, Q.AAPL for quotes
                tradeParams = _tradeSymbols.Select(s => $"T.{s}").ToArray();
                quoteParams = _quoteSymbols.Select(s => $"Q.{s}").ToArray();
            }

            var allParams = tradeParams.Concat(quoteParams).ToArray();
            if (allParams.Length == 0) return;

            var msg = JsonSerializer.Serialize(new
            {
                action = "subscribe",
                @params = string.Join(",", allParams)
            }, s_serializerOptions);

            await SendTextAsync(msg, CancellationToken.None).ConfigureAwait(false);
            _log.Debug("Sent subscription for {Symbols}", string.Join(",", allParams));
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Failed to send subscription update to Polygon WebSocket. " +
                "This may indicate a connection issue. Check network connectivity and Polygon service status.");
        }
    }

    private async Task TrySendUnsubscribeAsync(string symbol, string prefix)
    {
        try
        {
            var ws = _ws;
            if (ws == null || ws.State != WebSocketState.Open) return;

            var msg = JsonSerializer.Serialize(new
            {
                action = "unsubscribe",
                @params = $"{prefix}.{symbol}"
            }, s_serializerOptions);

            await SendTextAsync(msg, CancellationToken.None).ConfigureAwait(false);
            _log.Debug("Sent unsubscription for {Prefix}.{Symbol}", prefix, symbol);
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "Failed to send unsubscription to Polygon WebSocket");
        }
    }

    private async Task SendTextAsync(string text, CancellationToken ct)
    {
        var ws = _ws ?? throw new InvalidOperationException("Not connected.");
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
            try
            {
                do
                {
                    res = await ws.ReceiveAsync(buf, ct).ConfigureAwait(false);
                    if (res.MessageType == WebSocketMessageType.Close) return;
                    sb.Append(Encoding.UTF8.GetString(buf, 0, res.Count));
                }
                while (!res.EndOfMessage);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (WebSocketException ex)
            {
                _log.Warning(ex, "WebSocket receive error");
                return;
            }

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
        // Get event type: "ev" field
        if (!el.TryGetProperty("ev", out var evProp)) return;
        var ev = evProp.GetString();

        switch (ev)
        {
            case "T": // Trade
                HandleTrade(el);
                break;

            case "Q": // Quote
                HandleQuote(el);
                break;

            case "status":
                HandleStatus(el);
                break;

            case "AM": // Aggregate minute bar (optional)
            case "A": // Aggregate second bar (optional)
                // Could be handled if SubscribeAggregates is implemented
                break;
        }
    }

    private void HandleTrade(JsonElement el)
    {
        // Polygon trade fields:
        // sym = symbol, p = price, s = size, t = timestamp (Unix ms), i = trade ID
        // x = exchange ID, c = conditions, z = tape
        var sym = el.TryGetProperty("sym", out var symProp) ? symProp.GetString() : null;
        if (string.IsNullOrWhiteSpace(sym)) return;

        var price = el.TryGetProperty("p", out var pProp) ? (decimal)pProp.GetDouble() : 0m;
        var size = el.TryGetProperty("s", out var sProp) ? sProp.GetInt32() : 0;
        var timestamp = el.TryGetProperty("t", out var tProp) ? tProp.GetInt64() : 0L;
        var tradeId = el.TryGetProperty("i", out var iProp) ? iProp.GetString() : null;
        var exchange = el.TryGetProperty("x", out var xProp) ? xProp.GetInt32() : 0;

        // Convert Unix milliseconds to DateTimeOffset
        DateTimeOffset dto = timestamp > 0
            ? DateTimeOffset.FromUnixTimeMilliseconds(timestamp)
            : DateTimeOffset.UtcNow;

        // Parse trade ID to long if possible
        long seqNum = 0;
        if (!string.IsNullOrEmpty(tradeId) && long.TryParse(tradeId, out var parsedId))
            seqNum = parsedId;

        var update = new MarketTradeUpdate(
            Timestamp: dto,
            Symbol: sym!,
            Price: price,
            Size: size,
            Aggressor: AggressorSide.Unknown,
            SequenceNumber: seqNum,
            StreamId: "POLYGON",
            Venue: GetExchangeName(exchange)
        );

        _tradeCollector.OnTrade(update);
    }

    private void HandleQuote(JsonElement el)
    {
        // Polygon quote fields:
        // sym = symbol, bp = bid price, bs = bid size, ap = ask price, as = ask size
        // t = timestamp (Unix ms), bx = bid exchange, ax = ask exchange
        var sym = el.TryGetProperty("sym", out var symProp) ? symProp.GetString() : null;
        if (string.IsNullOrWhiteSpace(sym)) return;

        var bidPrice = el.TryGetProperty("bp", out var bpProp) ? (decimal)bpProp.GetDouble() : 0m;
        var bidSize = el.TryGetProperty("bs", out var bsProp) ? bsProp.GetInt64() : 0L;
        var askPrice = el.TryGetProperty("ap", out var apProp) ? (decimal)apProp.GetDouble() : 0m;
        var askSize = el.TryGetProperty("as", out var asProp) ? asProp.GetInt64() : 0L;
        var timestamp = el.TryGetProperty("t", out var tProp) ? tProp.GetInt64() : 0L;

        DateTimeOffset dto = timestamp > 0
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
        // Status messages: {"ev":"status","status":"connected"} or {"ev":"status","status":"auth_success"}
        var status = el.TryGetProperty("status", out var statusProp) ? statusProp.GetString() : null;
        var message = el.TryGetProperty("message", out var msgProp) ? msgProp.GetString() : null;

        if (status == "connected")
        {
            _log.Information("Polygon WebSocket status: connected");
        }
        else if (status == "auth_success")
        {
            _log.Information("Polygon WebSocket authentication successful");
            // Publish heartbeat to signal connection is ready
            _publisher.TryPublish(MarketEvent.Heartbeat(DateTimeOffset.UtcNow, source: "Polygon"));
        }
        else if (status == "auth_failed")
        {
            _log.Error("Polygon WebSocket authentication failed: {Message}", message);
        }
        else if (status == "success")
        {
            _log.Debug("Polygon WebSocket subscription success: {Message}", message);
        }
        else
        {
            _log.Debug("Polygon WebSocket status: {Status} - {Message}", status, message);
        }
    }

    /// <summary>
    /// Converts Polygon exchange ID to exchange name.
    /// See: https://polygon.io/docs/stocks/get_v3_reference_exchanges
    /// </summary>
    private static string GetExchangeName(int exchangeId) => exchangeId switch
    {
        1 => "NYSE",
        2 => "AMEX",
        3 => "NYSE ARCA",
        4 => "NASDAQ",
        5 => "NASDAQ BX",
        6 => "NASDAQ PSX",
        7 => "NYSE NATIONAL",
        8 => "IEX",
        9 => "CBOE",
        10 => "BATS BZX",
        11 => "BATS BYX",
        12 => "BATS EDGA",
        13 => "BATS EDGX",
        14 => "CHX",
        15 => "MIAX",
        16 => "ISE",
        17 => "BOX",
        18 => "MEMX",
        19 => "LTSE",
        21 => "OTC",
        _ => $"EXCHANGE_{exchangeId}"
    };
}
