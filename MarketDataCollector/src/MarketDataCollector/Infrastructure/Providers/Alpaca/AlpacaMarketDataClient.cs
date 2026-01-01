using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using MarketDataCollector.Application.Config;
using MarketDataCollector.Application.Logging;
using MarketDataCollector.Domain.Collectors;
using MarketDataCollector.Domain.Models;
using MarketDataCollector.Infrastructure.Resilience;
using Serilog;

namespace MarketDataCollector.Infrastructure.Providers.Alpaca;

/// <summary>
/// Alpaca Market Data client (WebSocket) that implements the IMarketDataClient abstraction.
///
/// Features:
/// - Resilient WebSocket connection with automatic retry and reconnection
/// - Heartbeat monitoring to detect stale connections
/// - Circuit breaker protection against cascading failures
/// - Thread-safe subscription management
///
/// Current data support:
/// - Trades: YES (streams "t" messages and forwards to TradeDataCollector)
/// - Depth (L2): Partial (Alpaca provides quotes/BBO, not full L2 updates)
///
/// Notes:
/// - Alpaca typically limits to 1 active stream connection per user per endpoint.
/// - Authentication is performed by sending an "auth" message immediately after connect.
/// </summary>
public sealed class AlpacaMarketDataClient : IMarketDataClient
{
    private readonly ILogger _log = LoggingSetup.ForContext<AlpacaMarketDataClient>();
    private readonly TradeDataCollector _tradeCollector;
    private readonly QuoteCollector _quoteCollector;
    private readonly AlpacaOptions _opt;

    private ResilientWebSocketClient? _client;
    private CancellationTokenSource? _cts;

    private readonly object _gate = new();
    private readonly HashSet<string> _tradeSymbols = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _quoteSymbols = new(StringComparer.OrdinalIgnoreCase);
    private bool _isAuthenticated;
    private bool _subscriptionsPending;

    // Cached serializer options to avoid allocations in hot path
    private static readonly JsonSerializerOptions s_serializerOptions = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private int _nextSubId = 100_000; // keep away from IB ids
    private readonly Dictionary<int, (string Symbol, string Kind)> _subs = new();

    public AlpacaMarketDataClient(TradeDataCollector tradeCollector, QuoteCollector quoteCollector, AlpacaOptions opt)
    {
        _tradeCollector = tradeCollector;
        _quoteCollector = quoteCollector ?? throw new ArgumentNullException(nameof(quoteCollector));
        _opt = opt ?? throw new ArgumentNullException(nameof(opt));
        if (string.IsNullOrWhiteSpace(_opt.KeyId) || string.IsNullOrWhiteSpace(_opt.SecretKey))
            throw new ArgumentException("Alpaca KeyId/SecretKey required.");
    }

    public bool IsEnabled => true;

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        if (_client != null) return;

        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        var host = _opt.UseSandbox ? "stream.data.sandbox.alpaca.markets" : "stream.data.alpaca.markets";
        var uri = new Uri($"wss://{host}/v2/{_opt.Feed}");

        _log.Information("Connecting to Alpaca WebSocket at {Uri} (Sandbox: {UseSandbox})", uri, _opt.UseSandbox);

        // Configure resilient connection options
        var options = new ResilientWebSocketOptions
        {
            MaxConnectionRetries = _opt.MaxConnectionRetries,
            InitialRetryDelay = TimeSpan.FromSeconds(_opt.InitialRetryDelaySeconds),
            MaxRetryDelay = TimeSpan.FromSeconds(_opt.MaxRetryDelaySeconds),
            ConnectionTimeout = TimeSpan.FromSeconds(_opt.ConnectionTimeoutSeconds),
            HeartbeatInterval = TimeSpan.FromSeconds(_opt.HeartbeatIntervalSeconds),
            HeartbeatTimeout = TimeSpan.FromSeconds(_opt.HeartbeatTimeoutSeconds),
            EnableAutoReconnect = _opt.EnableAutoReconnect,
            EnableHeartbeat = _opt.EnableHeartbeat,
            ConsecutiveFailuresBeforeReconnect = _opt.ConsecutiveFailuresBeforeReconnect
        };

        _client = new ResilientWebSocketClient(
            uri,
            options,
            onConnected: OnConnectedAsync,
            onMessage: OnMessageAsync);

        // Subscribe to connection events
        _client.StateChanged += OnConnectionStateChanged;
        _client.ConnectionLost += OnConnectionLost;
        _client.ConnectionRestored += OnConnectionRestored;

        try
        {
            await _client.ConnectAsync(_cts.Token).ConfigureAwait(false);
            _log.Information("Successfully connected to Alpaca WebSocket with resilience enabled");
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Failed to connect to Alpaca WebSocket at {Uri}. " +
                "Troubleshooting: 1) Check your internet connection. 2) Verify Alpaca service status at status.alpaca.markets. " +
                "3) Ensure your API keys are valid and not expired.", uri);
            throw;
        }
    }

    private async Task OnConnectedAsync(ClientWebSocket ws, CancellationToken ct)
    {
        _isAuthenticated = false;

        // Authenticate via message (must be within ~10 seconds of connection)
        var authMsg = JsonSerializer.Serialize(new { action = "auth", key = _opt.KeyId, secret = _opt.SecretKey });
        var bytes = Encoding.UTF8.GetBytes(authMsg);
        await ws.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, ct).ConfigureAwait(false);
        _log.Debug("Authentication message sent to Alpaca");

        // Mark subscriptions as pending - they'll be sent after auth confirmation
        lock (_gate)
        {
            if (_tradeSymbols.Count > 0 || _quoteSymbols.Count > 0)
            {
                _subscriptionsPending = true;
            }
        }
    }

    private async Task OnMessageAsync(ReadOnlyMemory<byte> data, CancellationToken ct)
    {
        var json = Encoding.UTF8.GetString(data.Span);
        if (string.IsNullOrWhiteSpace(json)) return;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in doc.RootElement.EnumerateArray())
                    await HandleMessageAsync(el).ConfigureAwait(false);
            }
            else if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                await HandleMessageAsync(doc.RootElement).ConfigureAwait(false);
            }
        }
        catch (JsonException ex)
        {
            _log.Warning(ex, "Failed to parse Alpaca WebSocket message. Raw JSON: {RawJson}. " +
                "This may indicate a protocol change or malformed message.",
                json.Length > 500 ? json[..500] + "..." : json);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Unexpected error processing Alpaca WebSocket message");
        }
    }

    private void OnConnectionStateChanged(object? sender, ConnectionStateChangedEventArgs e)
    {
        _log.Information("Alpaca connection state changed: {OldState} -> {NewState} (Attempt: {Attempt})",
            e.OldState, e.NewState, e.ReconnectAttempt);

        if (e.Exception != null)
        {
            _log.Warning(e.Exception, "Connection state change due to error");
        }
    }

    private void OnConnectionLost(object? sender, Exception? ex)
    {
        _log.Warning(ex, "Alpaca WebSocket connection lost. Auto-reconnect will attempt recovery.");
        _isAuthenticated = false;
    }

    private void OnConnectionRestored(object? sender, EventArgs e)
    {
        _log.Information("Alpaca WebSocket connection restored");
        // Note: OnConnectedAsync will handle re-authentication and subscriptions
    }

    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        _log.Information("Disconnecting from Alpaca WebSocket");

        var client = _client;
        var cts = _cts;

        _client = null;
        _cts = null;

        if (cts != null)
        {
            try { cts.Cancel(); } catch { }
            try { cts.Dispose(); } catch { }
        }

        if (client != null)
        {
            client.StateChanged -= OnConnectionStateChanged;
            client.ConnectionLost -= OnConnectionLost;
            client.ConnectionRestored -= OnConnectionRestored;

            try
            {
                await client.DisconnectAsync(ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "Error during WebSocket disconnect");
            }
            finally
            {
                await client.DisposeAsync().ConfigureAwait(false);
            }
        }

        _isAuthenticated = false;
        _log.Information("Disconnected from Alpaca WebSocket");
    }

    public int SubscribeTrades(SymbolConfig cfg)
    {
        if (cfg is null) throw new ArgumentNullException(nameof(cfg));
        var symbol = cfg.Symbol.Trim();
        if (symbol.Length == 0) return -1;

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

        _ = TrySendSubscribeAsync(); // send updated subscription set
    }

    public int SubscribeMarketDepth(SymbolConfig cfg)
    {
        // Alpaca provides quotes, not full L2 depth updates.
        if (_opt.SubscribeQuotes)
        {
            var symbol = cfg.Symbol.Trim();
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

        return -1;
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
    }

    private async Task TrySendSubscribeAsync()
    {
        try
        {
            var client = _client;
            if (client == null || !client.IsConnected || !_isAuthenticated) return;

            string[] trades;
            string[] quotes;
            lock (_gate)
            {
                trades = _tradeSymbols.ToArray();
                quotes = _quoteSymbols.ToArray();
                _subscriptionsPending = false;
            }

            var msg = new Dictionary<string, object?>
            {
                ["action"] = "subscribe",
                ["trades"] = trades.Length == 0 ? null : trades
            };

            if (_opt.SubscribeQuotes && quotes.Length > 0)
                msg["quotes"] = quotes;

            var json = JsonSerializer.Serialize(msg, s_serializerOptions);
            await client.SendTextAsync(json, CancellationToken.None).ConfigureAwait(false);

            _log.Debug("Sent subscription update: {TradeCount} trades, {QuoteCount} quotes",
                trades.Length, quotes.Length);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Failed to send subscription update to Alpaca WebSocket. " +
                "This may indicate a connection issue. Check network connectivity and Alpaca service status.");
        }
    }

    private async Task HandleMessageAsync(JsonElement el)
    {
        if (!el.TryGetProperty("T", out var tProp)) return;
        var t = tProp.GetString();

        // Handle authentication responses
        if (t == "success")
        {
            var msg = el.TryGetProperty("msg", out var msgProp) ? msgProp.GetString() : null;
            if (msg == "authenticated")
            {
                _log.Information("Alpaca authentication successful");
                _isAuthenticated = true;

                // Send any pending subscriptions
                if (_subscriptionsPending)
                {
                    await TrySendSubscribeAsync().ConfigureAwait(false);
                }
            }
            else if (msg == "connected")
            {
                _log.Debug("Alpaca WebSocket connected confirmation received");
            }
        }
        else if (t == "error")
        {
            var code = el.TryGetProperty("code", out var codeProp) ? codeProp.GetInt32() : 0;
            var msg = el.TryGetProperty("msg", out var msgProp) ? msgProp.GetString() : "Unknown error";
            _log.Error("Alpaca error received: [{Code}] {Message}", code, msg);

            if (code == 401 || code == 402 || code == 403)
            {
                _log.Error("Authentication error. Check your API keys and permissions.");
                _isAuthenticated = false;
            }
        }
        else if (t == "subscription")
        {
            // Subscription confirmation
            var trades = el.TryGetProperty("trades", out var tradesProp) && tradesProp.ValueKind == JsonValueKind.Array
                ? tradesProp.EnumerateArray().Select(x => x.GetString()).Where(x => x != null).ToArray()
                : Array.Empty<string?>();
            var quotes = el.TryGetProperty("quotes", out var quotesProp) && quotesProp.ValueKind == JsonValueKind.Array
                ? quotesProp.EnumerateArray().Select(x => x.GetString()).Where(x => x != null).ToArray()
                : Array.Empty<string?>();

            _log.Information("Alpaca subscription confirmed: {TradeCount} trades, {QuoteCount} quotes",
                trades.Length, quotes.Length);
        }
        else if (t == "t")
        {
            // Trade message
            HandleTradeMessage(el);
        }
        else if (t == "q")
        {
            // Quote message - now fully wired to QuoteCollector
            HandleQuoteMessage(el);
        }
    }

    private void HandleTradeMessage(JsonElement el)
    {
        var sym = el.TryGetProperty("S", out var sProp) ? sProp.GetString() : null;
        if (string.IsNullOrWhiteSpace(sym)) return;

        var price = el.TryGetProperty("p", out var pProp) ? (decimal)pProp.GetDouble() : 0m;
        var size = el.TryGetProperty("s", out var szProp) ? szProp.GetInt32() : 0;
        var ts = el.TryGetProperty("t", out var tsProp) ? tsProp.GetString() : null;
        var venue = el.TryGetProperty("x", out var xProp) ? xProp.GetString() : null;
        var tradeId = el.TryGetProperty("i", out var iProp) ? iProp.GetInt64() : 0;

        DateTimeOffset dto;
        if (!DateTimeOffset.TryParse(ts, out dto))
            dto = DateTimeOffset.UtcNow;

        var update = new MarketTradeUpdate(
            Timestamp: dto,
            Symbol: sym!,
            Price: price,
            Size: size,
            Aggressor: AggressorSide.Unknown,
            SequenceNumber: tradeId <= 0 ? null : tradeId,
            StreamId: "ALPACA",
            Venue: venue ?? "ALPACA"
        );

        _tradeCollector.OnTrade(update);
    }

    private void HandleQuoteMessage(JsonElement el)
    {
        var sym = el.TryGetProperty("S", out var sProp) ? sProp.GetString() : null;
        if (string.IsNullOrWhiteSpace(sym)) return;

        var bidPrice = el.TryGetProperty("bp", out var bpProp) ? (decimal)bpProp.GetDouble() : 0m;
        var bidSize = el.TryGetProperty("bs", out var bsProp) ? bsProp.GetInt32() : 0;
        var askPrice = el.TryGetProperty("ap", out var apProp) ? (decimal)apProp.GetDouble() : 0m;
        var askSize = el.TryGetProperty("as", out var asProp) ? asProp.GetInt32() : 0;
        var ts = el.TryGetProperty("t", out var tsProp) ? tsProp.GetString() : null;

        DateTimeOffset dto;
        if (!DateTimeOffset.TryParse(ts, out dto))
            dto = DateTimeOffset.UtcNow;

        var update = new MarketQuoteUpdate(
            Timestamp: dto,
            Symbol: sym!,
            BidPrice: bidPrice,
            BidSize: bidSize,
            AskPrice: askPrice,
            AskSize: askSize,
            StreamId: "ALPACA",
            Venue: "ALPACA"
        );

        _quoteCollector.OnQuote(update);
    }
}
