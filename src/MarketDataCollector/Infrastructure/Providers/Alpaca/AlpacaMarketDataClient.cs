using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using MarketDataCollector.Application.Config;
using MarketDataCollector.Application.Logging;
using MarketDataCollector.Domain.Collectors;
using MarketDataCollector.Contracts.Domain.Models;
using MarketDataCollector.Domain.Models;
using MarketDataCollector.Infrastructure.Contracts;
using MarketDataCollector.Infrastructure.Providers.Core;
using MarketDataCollector.Infrastructure.Resilience;
using MarketDataCollector.Infrastructure.Shared;
using Polly;
using Serilog;

namespace MarketDataCollector.Infrastructure.Providers.Alpaca;

/// <summary>
/// Alpaca Market Data client (WebSocket) that implements the IMarketDataClient abstraction.
///
/// Current support:
/// - Trades: YES (streams "t" messages and forwards to TradeDataCollector)
/// - Depth (L2): NO (Alpaca stock stream provides quotes/BBO, not full L2 updates; method returns -1)
///
/// Notes:
/// - Alpaca typically limits to 1 active stream connection per user per endpoint.
/// - Authentication is performed by sending an "auth" message immediately after connect.
/// </summary>
[ImplementsAdr("ADR-001", "Alpaca streaming data provider implementation")]
[ImplementsAdr("ADR-004", "All async methods support CancellationToken")]
public sealed class AlpacaMarketDataClient : IMarketDataClient
{
    private readonly ILogger _log = LoggingSetup.ForContext<AlpacaMarketDataClient>();
    private readonly TradeDataCollector _tradeCollector;
    private readonly QuoteCollector _quoteCollector;
    private readonly AlpacaOptions _opt;

    private ClientWebSocket? _ws;
    private Task? _recvLoop;
    private CancellationTokenSource? _cts;
    private WebSocketHeartbeat? _heartbeat;

    // Centralized subscription management
    private readonly SubscriptionManager _subscriptionManager = new(startingId: 100_000);

    // Resilience pipeline for connection retry with exponential backoff
    private readonly ResiliencePipeline _connectionPipeline;

    // Cached serializer options to avoid allocations in hot path
    private static readonly JsonSerializerOptions s_serializerOptions = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    // Reconnection state
    private volatile bool _isReconnecting;
    private readonly SemaphoreSlim _reconnectGate = new(1, 1);

    public AlpacaMarketDataClient(TradeDataCollector tradeCollector, QuoteCollector quoteCollector, AlpacaOptions opt)
    {
        _tradeCollector = tradeCollector;
        _quoteCollector = quoteCollector ?? throw new ArgumentNullException(nameof(quoteCollector));
        _opt = opt ?? throw new ArgumentNullException(nameof(opt));
        if (string.IsNullOrWhiteSpace(_opt.KeyId) || string.IsNullOrWhiteSpace(_opt.SecretKey))
            throw new ArgumentException("Alpaca KeyId/SecretKey required.");

        // Initialize resilience pipeline with exponential backoff
        // Default: 5 retries with 2s base delay, max 30s between retries
        _connectionPipeline = WebSocketResiliencePolicy.CreateComprehensivePipeline(
            maxRetries: 5,
            retryBaseDelay: TimeSpan.FromSeconds(2),
            circuitBreakerFailureThreshold: 5,
            circuitBreakerDuration: TimeSpan.FromSeconds(30),
            operationTimeout: TimeSpan.FromSeconds(30));
    }

    public bool IsEnabled => true;

    #region IProviderMetadata

    /// <inheritdoc/>
    public string ProviderId => "alpaca";

    /// <inheritdoc/>
    public string ProviderDisplayName => "Alpaca Markets Streaming";

    /// <inheritdoc/>
    public string ProviderDescription => "Real-time trades and quotes via Alpaca WebSocket API";

    /// <inheritdoc/>
    public int ProviderPriority => 10;

    /// <inheritdoc/>
    public ProviderCapabilities ProviderCapabilities { get; } = ProviderCapabilities.Streaming(
        trades: true,
        quotes: true,
        depth: false) with
    {
        SupportedMarkets = new[] { "US" },
        MaxRequestsPerWindow = 200,
        RateLimitWindow = TimeSpan.FromMinutes(1),
        MinRequestDelay = TimeSpan.FromMilliseconds(300)
    };

    /// <inheritdoc/>
    public ProviderCredentialField[] ProviderCredentialFields => new[]
    {
        new ProviderCredentialField("KeyId", "ALPACA__KEYID", "API Key ID", true),
        new ProviderCredentialField("SecretKey", "ALPACA__SECRETKEY", "API Secret Key", true)
    };

    /// <inheritdoc/>
    public string[] ProviderNotes => new[]
    {
        "Alpaca requires API credentials (free account available).",
        "Rate limit: 200 requests/minute.",
        "IEX feed is free; SIP feed requires subscription."
    };

    #endregion

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        if (_ws != null) return;

        var host = _opt.UseSandbox ? "stream.data.sandbox.alpaca.markets" : "stream.data.alpaca.markets";
        var uri = new Uri($"wss://{host}/v2/{_opt.Feed}");

        _log.Information("Connecting to Alpaca WebSocket at {Uri} (Sandbox: {UseSandbox}) with retry policy", uri, _opt.UseSandbox);

        await _connectionPipeline.ExecuteAsync(async token =>
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            _ws = new ClientWebSocket();

            try
            {
                await _ws.ConnectAsync(uri, token).ConfigureAwait(false);
                _log.Information("Successfully connected to Alpaca WebSocket");

                // Authenticate via message (must be within ~10 seconds of connection)
                var authMsg = JsonSerializer.Serialize(new { action = "auth", key = _opt.KeyId, secret = _opt.SecretKey });
                await SendTextAsync(authMsg, token).ConfigureAwait(false);
                _log.Debug("Authentication message sent to Alpaca");
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "Connection attempt to Alpaca WebSocket failed at {Uri}. Will retry per policy.", uri);
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
            _log.Warning("WebSocket connection lost, initiating automatic reconnection");

            // Clean up existing connection
            await CleanupConnectionAsync();

            // Attempt to reconnect using the resilience pipeline
            await ConnectAsync(CancellationToken.None);

            // Resubscribe to all active subscriptions
            if (_ws?.State == WebSocketState.Open)
            {
                await TrySendSubscribeAsync();
                _log.Information("Successfully reconnected and resubscribed to Alpaca WebSocket");
            }
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Failed to reconnect to Alpaca WebSocket after connection loss. " +
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

    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        _log.Information("Disconnecting from Alpaca WebSocket");

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

        _log.Information("Disconnected from Alpaca WebSocket");
    }

    public int SubscribeTrades(SymbolConfig cfg)
    {
        if (cfg is null) throw new ArgumentNullException(nameof(cfg));
        var id = _subscriptionManager.Subscribe(cfg.Symbol, "trades");
        if (id == -1) return -1;

        _ = TrySendSubscribeAsync();
        return id;
    }

    public void UnsubscribeTrades(int subscriptionId)
    {
        var subscription = _subscriptionManager.Unsubscribe(subscriptionId);
        if (subscription != null)
        {
            _ = TrySendSubscribeAsync(); // send updated subscription set
        }
    }

    public int SubscribeMarketDepth(SymbolConfig cfg)
    {
        // Not supported for stocks: Alpaca provides quotes, not full L2 depth updates.
        // If you later add QuoteCollector -> L2Snapshot mapping, wire it here.
        if (!_opt.SubscribeQuotes) return -1;

        if (cfg is null) throw new ArgumentNullException(nameof(cfg));
        var id = _subscriptionManager.Subscribe(cfg.Symbol, "quotes");
        if (id == -1) return -1;

        _ = TrySendSubscribeAsync();
        return id;
    }

    public void UnsubscribeMarketDepth(int subscriptionId)
    {
        var subscription = _subscriptionManager.Unsubscribe(subscriptionId);
        if (subscription != null)
        {
            _ = TrySendSubscribeAsync();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync().ConfigureAwait(false);
        _reconnectGate.Dispose();
        _subscriptionManager.Dispose();
    }

    private async Task TrySendSubscribeAsync()
    {
        try
        {
            var ws = _ws;
            if (ws == null || ws.State != WebSocketState.Open) return;

            var trades = _subscriptionManager.GetSymbolsByKind("trades");
            var quotes = _subscriptionManager.GetSymbolsByKind("quotes");

            var msg = new Dictionary<string, object?>
            {
                ["action"] = "subscribe",
                ["trades"] = trades.Length == 0 ? null : trades
            };

            if (_opt.SubscribeQuotes && quotes.Length > 0)
                msg["quotes"] = quotes;

            var json = JsonSerializer.Serialize(msg, s_serializerOptions);
            await SendTextAsync(json, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Failed to send subscription update to Alpaca WebSocket. " +
                "This may indicate a connection issue. Check network connectivity and Alpaca service status.");
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
            do
            {
                res = await ws.ReceiveAsync(buf, ct).ConfigureAwait(false);
                if (res.MessageType == WebSocketMessageType.Close) return;
                sb.Append(Encoding.UTF8.GetString(buf, 0, res.Count));
            }
            while (!res.EndOfMessage);

            var json = sb.ToString();
            if (string.IsNullOrWhiteSpace(json)) continue;

            // Alpaca sends arrays of objects: [{"T":"success",...}, {"T":"t",...}]
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
                _log.Warning(ex, "Failed to parse Alpaca WebSocket message. Raw JSON: {RawJson}. " +
                    "This may indicate a protocol change or malformed message.",
                    json.Length > 500 ? json[..500] + "..." : json);
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Unexpected error processing Alpaca WebSocket message");
            }
        }
    }

    private void HandleMessage(JsonElement el)
    {
        // Trades: "T":"t" (per Alpaca docs)
        if (!el.TryGetProperty("T", out var tProp)) return;
        var t = tProp.GetString();
        if (t == "t")
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
                SequenceNumber: tradeId <= 0 ? 0L : tradeId,
                StreamId: "ALPACA",
                Venue: venue ?? "ALPACA"
            );

            _tradeCollector.OnTrade(update);
        }

        // Handle Alpaca quotes ("T":"q") - BBO updates
        // Alpaca quote fields: S=symbol, bp=bidPrice, bs=bidSize, ap=askPrice, as=askSize, t=timestamp
        if (t == "q")
        {
            var sym = el.TryGetProperty("S", out var sProp) ? sProp.GetString() : null;
            if (string.IsNullOrWhiteSpace(sym)) return;

            var bidPrice = el.TryGetProperty("bp", out var bpProp) ? (decimal)bpProp.GetDouble() : 0m;
            var bidSize = el.TryGetProperty("bs", out var bsProp) ? bsProp.GetInt64() : 0L;
            var askPrice = el.TryGetProperty("ap", out var apProp) ? (decimal)apProp.GetDouble() : 0m;
            var askSize = el.TryGetProperty("as", out var asProp) ? asProp.GetInt64() : 0L;
            var ts = el.TryGetProperty("t", out var tsProp) ? tsProp.GetString() : null;

            DateTimeOffset dto;
            if (!DateTimeOffset.TryParse(ts, out dto))
                dto = DateTimeOffset.UtcNow;

            var quoteUpdate = new MarketQuoteUpdate(
                Timestamp: dto,
                Symbol: sym!,
                BidPrice: bidPrice,
                BidSize: bidSize,
                AskPrice: askPrice,
                AskSize: askSize,
                SequenceNumber: null,
                StreamId: "ALPACA",
                Venue: "ALPACA"
            );

            _quoteCollector.OnQuote(quoteUpdate);
        }
    }
}
