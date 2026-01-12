using MarketDataCollector.Application.Config;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
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
/// Polygon.io market data client (WebSocket) that implements the IMarketDataClient abstraction.
///
/// Supports:
/// - Trades: YES (streams "T" messages and forwards to TradeDataCollector)
/// - Quotes: YES (streams "Q" messages and forwards to QuoteCollector)
/// - Aggregates: Planned for future implementation
///
/// Connection Resilience:
/// - Uses Polly-based WebSocketResiliencePolicy for connection retry with exponential backoff
/// - Implements circuit breaker pattern to prevent cascading failures
/// - Automatic reconnection on connection loss with jitter
/// - Configurable retry attempts (default: 5) with 2s base delay, max 30s between retries
///
/// <para><b>Authentication:</b> Requires Polygon.io API key for WebSocket connection.</para>
/// <para><b>Security Best Practices:</b></para>
/// <list type="bullet">
/// <item><description>Use environment variable: <c>POLYGON__APIKEY</c></description></item>
/// <item><description>Use a secure vault service for production deployments</description></item>
/// <item><description>Ensure configuration files with real credentials are in <c>.gitignore</c></description></item>
/// </list>
/// <para>See <see href="https://polygon.io/docs/stocks/ws_getting-started">Polygon WebSocket Docs</see></para>
/// </summary>
public sealed class PolygonMarketDataClient : IMarketDataClient
{
    private readonly ILogger _log = LoggingSetup.ForContext<PolygonMarketDataClient>();
    private readonly IMarketEventPublisher _publisher;
    private readonly TradeDataCollector _tradeCollector;
    private readonly QuoteCollector _quoteCollector;
    private readonly PolygonOptions _opt;
    private readonly bool _hasValidCredentials;

    // WebSocket connection management
    private ClientWebSocket? _ws;
    private Task? _recvLoop;
    private CancellationTokenSource? _cts;
    private WebSocketHeartbeat? _heartbeat;

    // Resilience pipeline for connection retry with exponential backoff
    private readonly ResiliencePipeline _connectionPipeline;

    // Subscription tracking
    private readonly object _gate = new();
    private readonly HashSet<string> _tradeSymbols = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _quoteSymbols = new(StringComparer.OrdinalIgnoreCase);
    private int _nextSubId = 200_000; // keep away from IB (0-99999) and Alpaca (100000-199999) ids
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
        // Check environment variable first (preferred for security)
        var envApiKey = Environment.GetEnvironmentVariable("POLYGON_API_KEY")
                     ?? Environment.GetEnvironmentVariable("POLYGON__APIKEY");

        var apiKey = !string.IsNullOrWhiteSpace(envApiKey) ? envApiKey : options.ApiKey;

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
    /// Connects to Polygon WebSocket stream with resilience.
    /// Uses exponential backoff retry policy for connection establishment.
    /// </summary>
    public async Task ConnectAsync(CancellationToken ct = default)
    {
        if (_ws != null) return;

        if (!_hasValidCredentials)
        {
            _log.Warning("Polygon client connect called without valid API key. " +
                "Configure POLYGON_API_KEY environment variable for real data streaming. " +
                "Running in stub mode with synthetic heartbeat.");
            _publisher.TryPublish(MarketEvent.Heartbeat(DateTimeOffset.UtcNow, source: "PolygonStub"));
            return;
        }

        // Determine WebSocket endpoint based on delayed/real-time setting
        var host = _opt.UseDelayed ? "delayed.polygon.io" : "socket.polygon.io";
        var uri = new Uri($"wss://{host}/{_opt.Feed}");

        _log.Information("Connecting to Polygon WebSocket at {Uri} (Delayed: {UseDelayed}) with retry policy",
            uri, _opt.UseDelayed);

        await _connectionPipeline.ExecuteAsync(async token =>
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            _ws = new ClientWebSocket();

            try
            {
                await _ws.ConnectAsync(uri, token).ConfigureAwait(false);
                _log.Information("Successfully connected to Polygon WebSocket");

                // Authenticate using Polygon's auth message format
                var apiKey = GetApiKey();
                var authMsg = JsonSerializer.Serialize(new { action = "auth", @params = apiKey }, s_serializerOptions);
                await SendTextAsync(authMsg, token).ConfigureAwait(false);
                _log.Debug("Authentication message sent to Polygon");
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "Connection attempt to Polygon WebSocket failed at {Uri}. Will retry per policy.", uri);
                // Clean up failed connection attempt
                try { _ws?.Dispose(); }
                catch (Exception disposeEx)
                {
                    _log.Debug(disposeEx, "WebSocket disposal failed during connection cleanup");
                }
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
            try { cts.Cancel(); }
            catch (Exception ex)
            {
                _log.Debug(ex, "CancellationTokenSource.Cancel failed during connection cleanup");
            }
            try { cts.Dispose(); }
            catch (Exception ex)
            {
                _log.Debug(ex, "CancellationTokenSource.Dispose failed during connection cleanup");
            }
        }

        if (ws != null)
        {
            try { ws.Dispose(); }
            catch (Exception ex)
            {
                _log.Debug(ex, "WebSocket disposal failed during connection cleanup");
            }
        }

        if (_recvLoop != null)
        {
            try { await _recvLoop.ConfigureAwait(false); }
            catch (Exception ex)
            {
                _log.Debug(ex, "Receive loop task completion error during connection cleanup");
            }
            _recvLoop = null;
        }
    }

    /// <summary>
    /// Disconnects from Polygon WebSocket stream.
    /// </summary>
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
            try { cts.Cancel(); }
            catch (Exception ex)
            {
                _log.Debug(ex, "CancellationTokenSource.Cancel failed during disconnect");
            }
            try { cts.Dispose(); }
            catch (Exception ex)
            {
                _log.Debug(ex, "CancellationTokenSource.Dispose failed during disconnect");
            }
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
            try { ws.Dispose(); }
            catch (Exception ex)
            {
                _log.Debug(ex, "WebSocket disposal failed during disconnect");
            }
        }

        if (_recvLoop != null)
        {
            try { await _recvLoop.ConfigureAwait(false); }
            catch (Exception ex)
            {
                _log.Debug(ex, "Receive loop task completion error during disconnect");
            }
        }
        _recvLoop = null;

        _log.Information("Disconnected from Polygon WebSocket");
    }

    /// <summary>
    /// Subscribes to market depth (quotes) for a symbol.
    /// Polygon provides NBBO quotes that map to BBO updates via QuoteCollector.
    /// </summary>
    /// <param name="cfg">Symbol configuration.</param>
    /// <returns>Subscription ID, or -1 if quotes are disabled or symbol is invalid.</returns>
    public int SubscribeMarketDepth(SymbolConfig cfg)
    {
        if (cfg is null) throw new ArgumentNullException(nameof(cfg));
        var symbol = cfg.Symbol.Trim();
        if (symbol.Length == 0) return -1;

        // Only subscribe if quotes are enabled in options
        if (!_opt.SubscribeQuotes)
        {
            _log.Debug("Quote subscription for {Symbol} skipped - SubscribeQuotes is disabled", symbol);
            return -1;
        }

        var id = Interlocked.Increment(ref _nextSubId);
        lock (_gate)
        {
            _quoteSymbols.Add(symbol);
            _subs[id] = (symbol, "quotes");
        }

        _ = TrySendSubscribeAsync();
        _log.Information("Subscribed to quotes for {Symbol} with ID {SubscriptionId}", symbol, id);
        return id;
    }

    /// <summary>
    /// Unsubscribes from market depth (quotes) for a subscription.
    /// </summary>
    /// <param name="subscriptionId">The subscription ID to unsubscribe.</param>
    public void UnsubscribeMarketDepth(int subscriptionId)
    {
        (string Symbol, string Kind) sub;
        lock (_gate)
        {
            if (!_subs.TryGetValue(subscriptionId, out sub)) return;
            _subs.Remove(subscriptionId);
            if (sub.Kind == "quotes")
            {
                // Remove symbol only if no remaining quote subs for this symbol
                if (!_subs.Values.Any(v => v.Kind == "quotes" && v.Symbol.Equals(sub.Symbol, StringComparison.OrdinalIgnoreCase)))
                    _quoteSymbols.Remove(sub.Symbol);
            }
        }

        _ = TrySendSubscribeAsync();
        _log.Debug("Unsubscribed from quotes for {Symbol} (subscription ID {SubscriptionId})", sub.Symbol, subscriptionId);
    }

    /// <summary>
    /// Subscribes to trades for a symbol.
    /// </summary>
    /// <param name="cfg">Symbol configuration.</param>
    /// <returns>Subscription ID, or -1 if trades are disabled or symbol is invalid.</returns>
    public int SubscribeTrades(SymbolConfig cfg)
    {
        if (cfg is null) throw new ArgumentNullException(nameof(cfg));
        var symbol = cfg.Symbol.Trim();
        if (symbol.Length == 0) return -1;

        // Only subscribe if trades are enabled in options
        if (!_opt.SubscribeTrades)
        {
            _log.Debug("Trade subscription for {Symbol} skipped - SubscribeTrades is disabled", symbol);
            return -1;
        }

        var id = Interlocked.Increment(ref _nextSubId);
        lock (_gate)
        {
            _tradeSymbols.Add(symbol);
            _subs[id] = (symbol, "trades");
        }

        _ = TrySendSubscribeAsync();
        _log.Information("Subscribed to trades for {Symbol} with ID {SubscriptionId}", symbol, id);
        return id;
    }

    /// <summary>
    /// Unsubscribes from trades for a subscription.
    /// </summary>
    /// <param name="subscriptionId">The subscription ID to unsubscribe.</param>
    public void UnsubscribeTrades(int subscriptionId)
    {
        (string Symbol, string Kind) sub;
        lock (_gate)
        {
            if (!_subs.TryGetValue(subscriptionId, out sub)) return;
            _subs.Remove(subscriptionId);
            if (sub.Kind == "trades")
            {
                // Remove symbol only if no remaining trade subs for this symbol
                if (!_subs.Values.Any(v => v.Kind == "trades" && v.Symbol.Equals(sub.Symbol, StringComparison.OrdinalIgnoreCase)))
                    _tradeSymbols.Remove(sub.Symbol);
            }
        }

        _ = TrySendSubscribeAsync();
        _log.Debug("Unsubscribed from trades for {Symbol} (subscription ID {SubscriptionId})", sub.Symbol, subscriptionId);
    }

    /// <summary>
    /// Sends subscription message to Polygon for current symbol sets.
    /// </summary>
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

            // Polygon uses T.SYMBOL for trades and Q.SYMBOL for quotes
            var subscriptions = new List<string>();

            foreach (var symbol in trades)
                subscriptions.Add($"T.{symbol}");

            if (_opt.SubscribeQuotes)
            {
                foreach (var symbol in quotes)
                    subscriptions.Add($"Q.{symbol}");
            }

            if (subscriptions.Count == 0) return;

            var msg = JsonSerializer.Serialize(new
            {
                action = "subscribe",
                @params = string.Join(",", subscriptions)
            }, s_serializerOptions);

            await SendTextAsync(msg, CancellationToken.None).ConfigureAwait(false);
            _log.Debug("Sent subscription for {Count} channels: {Subscriptions}", subscriptions.Count, string.Join(",", subscriptions));
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Failed to send subscription update to Polygon WebSocket. " +
                "This may indicate a connection issue. Check network connectivity and Polygon service status.");
        }
    }

    /// <summary>
    /// Sends text message over WebSocket.
    /// </summary>
    private async Task SendTextAsync(string text, CancellationToken ct)
    {
        var ws = _ws ?? throw new InvalidOperationException("Not connected.");
        var bytes = Encoding.UTF8.GetBytes(text);
        await ws.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Main receive loop that processes incoming WebSocket messages.
    /// </summary>
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

            // Polygon sends arrays of objects: [{"ev":"T","sym":"SPY",...}, {"ev":"status",...}]
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

    /// <summary>
    /// Handles a single message from Polygon WebSocket.
    /// Polygon message types:
    /// - "T" = Trade
    /// - "Q" = Quote
    /// - "A" = Aggregate (per second)
    /// - "AM" = Aggregate (per minute)
    /// - "status" = Status message
    /// </summary>
    private void HandleMessage(JsonElement el)
    {
        // Get event type
        if (!el.TryGetProperty("ev", out var evProp))
        {
            // Also check for status messages without ev property
            if (el.TryGetProperty("status", out var statusProp))
            {
                HandleStatusMessage(el);
            }
            return;
        }

        var ev = evProp.GetString();

        switch (ev)
        {
            case "T":
                HandleTradeMessage(el);
                break;
            case "Q":
                HandleQuoteMessage(el);
                break;
            case "status":
                HandleStatusMessage(el);
                break;
            case "A":
            case "AM":
                // Aggregates - could be implemented in the future
                _log.Debug("Received aggregate message (not currently processed)");
                break;
            default:
                _log.Debug("Unknown Polygon event type: {EventType}", ev);
                break;
        }

        // Record activity for heartbeat monitoring
        _heartbeat?.RecordPongReceived();
    }

    /// <summary>
    /// Handles trade messages from Polygon.
    /// Polygon trade format: {"ev":"T","sym":"SPY","x":4,"i":"123","p":450.12,"s":100,"t":1699999999999,"c":[...],"z":3}
    /// </summary>
    private void HandleTradeMessage(JsonElement el)
    {
        var sym = el.TryGetProperty("sym", out var symProp) ? symProp.GetString() : null;
        if (string.IsNullOrWhiteSpace(sym)) return;

        var price = el.TryGetProperty("p", out var pProp) ? (decimal)pProp.GetDouble() : 0m;
        var size = el.TryGetProperty("s", out var sProp) ? sProp.GetInt64() : 0L;
        var timestamp = el.TryGetProperty("t", out var tProp) ? tProp.GetInt64() : 0L;
        var exchange = el.TryGetProperty("x", out var xProp) ? xProp.GetInt32() : 0;
        var tradeId = el.TryGetProperty("i", out var iProp) ? iProp.GetString() : null;

        // Convert Unix milliseconds timestamp to DateTimeOffset
        var dto = timestamp > 0
            ? DateTimeOffset.FromUnixTimeMilliseconds(timestamp)
            : DateTimeOffset.UtcNow;

        // Map exchange code to venue name (simplified - could use full mapping)
        var venue = MapExchangeCode(exchange);

        // Parse trade ID as sequence number if numeric
        long seqNum = 0;
        if (!string.IsNullOrEmpty(tradeId) && long.TryParse(tradeId, out var parsedId))
            seqNum = parsedId;

        var update = new MarketTradeUpdate(
            Timestamp: dto,
            Symbol: sym!,
            Price: price,
            Size: size,
            Aggressor: AggressorSide.Unknown, // Polygon doesn't provide aggressor side in basic stream
            SequenceNumber: seqNum,
            StreamId: "POLYGON",
            Venue: venue
        );

        _tradeCollector.OnTrade(update);
    }

    /// <summary>
    /// Handles quote (NBBO) messages from Polygon.
    /// Polygon quote format: {"ev":"Q","sym":"SPY","bx":4,"bp":450.10,"bs":100,"ax":7,"ap":450.12,"as":200,"t":1699999999999,"c":1}
    /// </summary>
    private void HandleQuoteMessage(JsonElement el)
    {
        var sym = el.TryGetProperty("sym", out var symProp) ? symProp.GetString() : null;
        if (string.IsNullOrWhiteSpace(sym)) return;

        var bidPrice = el.TryGetProperty("bp", out var bpProp) ? (decimal)bpProp.GetDouble() : 0m;
        var bidSize = el.TryGetProperty("bs", out var bsProp) ? bsProp.GetInt64() : 0L;
        var askPrice = el.TryGetProperty("ap", out var apProp) ? (decimal)apProp.GetDouble() : 0m;
        var askSize = el.TryGetProperty("as", out var asProp) ? asProp.GetInt64() : 0L;
        var timestamp = el.TryGetProperty("t", out var tProp) ? tProp.GetInt64() : 0L;

        // Convert Unix milliseconds timestamp to DateTimeOffset
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

    /// <summary>
    /// Handles status messages from Polygon.
    /// </summary>
    private void HandleStatusMessage(JsonElement el)
    {
        var status = el.TryGetProperty("status", out var statusProp) ? statusProp.GetString() : null;
        var message = el.TryGetProperty("message", out var msgProp) ? msgProp.GetString() : null;

        if (status == "connected")
        {
            _log.Information("Polygon WebSocket connected: {Message}", message);
        }
        else if (status == "auth_success")
        {
            _log.Information("Polygon authentication successful");
            _publisher.TryPublish(MarketEvent.Heartbeat(DateTimeOffset.UtcNow, source: "Polygon"));
        }
        else if (status == "auth_failed")
        {
            _log.Error("Polygon authentication failed: {Message}. Check your API key configuration.", message);
        }
        else if (status == "success")
        {
            _log.Debug("Polygon subscription confirmed: {Message}", message);
        }
        else
        {
            _log.Information("Polygon status: {Status} - {Message}", status, message);
        }
    }

    /// <summary>
    /// Maps Polygon exchange code to venue name.
    /// See: https://polygon.io/docs/stocks/get_v3_reference_exchanges
    /// </summary>
    private static string MapExchangeCode(int code)
    {
        return code switch
        {
            1 => "NYSE",
            2 => "AMEX",
            3 => "ARCA",
            4 => "NASDAQ",
            5 => "NASDAQ",
            6 => "NASDAQ",
            7 => "NYSE",
            8 => "ARCA",
            9 => "BATS",
            10 => "BATS",
            11 => "IEX",
            12 => "EDGX",
            13 => "EDGA",
            14 => "NSDQ",
            15 => "CQS",
            16 => "CTS",
            17 => "LTSE",
            19 => "MEMX",
            _ => $"EX{code}"
        };
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync().ConfigureAwait(false);
        _reconnectGate.Dispose();
    }
}
