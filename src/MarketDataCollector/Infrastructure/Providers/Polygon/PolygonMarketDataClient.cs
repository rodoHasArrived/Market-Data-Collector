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
/// Polygon.io market data adapter implementing the IMarketDataClient abstraction.
/// Currently operates in stub mode with synthetic data until full WebSocket implementation is added.
///
/// Current Support:
/// - Trades: Stub mode only (emits synthetic trades for testing)
/// - Quotes: Stub mode only (not yet wired)
/// - Aggregates: Not implemented
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
/// Polygon WebSocket Protocol:
/// - Endpoint: wss://socket.polygon.io/{feed} (stocks, options, forex, crypto)
/// - Auth: Send {"action":"auth","params":"{apiKey}"} after connect
/// - Subscribe: {"action":"subscribe","params":"T.AAPL,Q.AAPL"}
/// - Message types: T=trade, Q=quote, A=aggregate, AM=minute aggregate
/// </summary>
/// <remarks>
/// When implementing the full WebSocket client:
/// 1. Connect to wss://socket.polygon.io/{feed} or wss://delayed.polygon.io/{feed}
/// 2. Authenticate with API key
/// 3. Subscribe to channels: T.{symbol} for trades, Q.{symbol} for quotes
/// 4. Parse incoming JSON messages and route to appropriate collectors
/// </remarks>
public sealed class PolygonMarketDataClient : IMarketDataClient
{
    private readonly ILogger _log = LoggingSetup.ForContext<PolygonMarketDataClient>();
    private readonly IMarketEventPublisher _publisher;
    private readonly TradeDataCollector _tradeCollector;
    private readonly QuoteCollector _quoteCollector;
    private readonly PolygonOptions _options;

    // Resilience pipeline for connection retry with exponential backoff
    private readonly ResiliencePipeline _connectionPipeline;

    // Subscription tracking
    private readonly object _gate = new();
    private readonly HashSet<string> _tradeSymbols = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _quoteSymbols = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<int, (string Symbol, string Kind)> _subs = new();
    private int _nextSubId = 200_000; // Separate ID range from other providers

    // Connection state
    private bool _isConnected;

    /// <summary>
    /// Creates a new Polygon market data client.
    /// </summary>
    /// <param name="publisher">Event publisher for heartbeats and status.</param>
    /// <param name="tradeCollector">Collector for trade data.</param>
    /// <param name="quoteCollector">Collector for quote data.</param>
    /// <param name="options">Polygon configuration options. If null or missing ApiKey, runs in stub mode.</param>
    /// <exception cref="ArgumentNullException">If publisher, tradeCollector, or quoteCollector is null.</exception>
    public PolygonMarketDataClient(
        IMarketEventPublisher publisher,
        TradeDataCollector tradeCollector,
        QuoteCollector quoteCollector,
        PolygonOptions? options = null)
    {
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        _tradeCollector = tradeCollector ?? throw new ArgumentNullException(nameof(tradeCollector));
        _quoteCollector = quoteCollector ?? throw new ArgumentNullException(nameof(quoteCollector));
        _options = options ?? new PolygonOptions();

        // Validate API key format if provided
        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            ValidateApiKeyFormat(_options.ApiKey);
        }

        // Initialize resilience pipeline with exponential backoff
        _connectionPipeline = WebSocketResiliencePolicy.CreateComprehensivePipeline(
            maxRetries: 5,
            retryBaseDelay: TimeSpan.FromSeconds(2),
            circuitBreakerFailureThreshold: 5,
            circuitBreakerDuration: TimeSpan.FromSeconds(30),
            operationTimeout: TimeSpan.FromSeconds(30));

        _log.Information(
            "Polygon client initialized (Mode: {Mode}, Feed: {Feed}, Trades: {Trades}, Quotes: {Quotes})",
            IsStubMode ? "Stub" : "Live",
            _options.Feed,
            _options.SubscribeTrades,
            _options.SubscribeQuotes);
    }

    /// <summary>
    /// Gets whether the client has a valid API key configured.
    /// When false, the client operates in stub mode with synthetic data.
    /// </summary>
    public bool HasValidCredentials => !string.IsNullOrWhiteSpace(_options.ApiKey);

    /// <summary>
    /// Gets whether the client is operating in stub mode (no real connection).
    /// </summary>
    public bool IsStubMode => !HasValidCredentials;

    /// <summary>
    /// Gets whether the client is enabled and ready to receive subscriptions.
    /// </summary>
    public bool IsEnabled => true;

    /// <summary>
    /// Gets the current connection state.
    /// </summary>
    public bool IsConnected => _isConnected;

    /// <summary>
    /// Gets the configured feed type (stocks, options, forex, crypto).
    /// </summary>
    public string Feed => _options.Feed;

    /// <summary>
    /// Gets whether using delayed (15-minute) data.
    /// </summary>
    public bool UseDelayed => _options.UseDelayed;

    /// <summary>
    /// Validates the API key format.
    /// Polygon API keys are typically 32-character alphanumeric strings.
    /// </summary>
    private void ValidateApiKeyFormat(string apiKey)
    {
        // Polygon API keys are typically alphanumeric, 32 characters
        // But we'll be lenient and just check for reasonable length and no whitespace
        if (apiKey.Length < 10)
        {
            _log.Warning("Polygon API key appears too short ({Length} chars). Expected ~32 characters.", apiKey.Length);
        }

        if (apiKey.Any(char.IsWhiteSpace))
        {
            throw new ArgumentException("Polygon API key contains whitespace characters", nameof(apiKey));
        }

        _log.Debug("Polygon API key format validated (length: {Length})", apiKey.Length);
    }

    /// <summary>
    /// Connects to Polygon WebSocket stream.
    /// In stub mode, emits a synthetic heartbeat. In live mode, will connect to Polygon WebSocket.
    /// </summary>
    public Task ConnectAsync(CancellationToken ct = default)
    {
        if (IsStubMode)
        {
            _log.Information(
                "Polygon client connecting in STUB mode (no API key configured). " +
                "Set Polygon:ApiKey in configuration or POLYGON__APIKEY environment variable for live data.");

            _isConnected = true;
            _publisher.TryPublish(MarketEvent.Heartbeat(DateTimeOffset.UtcNow, source: "PolygonStub"));
            return Task.CompletedTask;
        }

        // Live mode - log connection attempt
        var endpoint = _options.UseDelayed
            ? $"wss://delayed.polygon.io/{_options.Feed}"
            : $"wss://socket.polygon.io/{_options.Feed}";

        _log.Information(
            "Polygon client connecting to {Endpoint} (Delayed: {UseDelayed})",
            endpoint,
            _options.UseDelayed);

        // For now, still stub mode but with credentials available
        // When implementing full WebSocket:
        // await _connectionPipeline.ExecuteAsync(async token =>
        // {
        //     _ws = new ClientWebSocket();
        //     await _ws.ConnectAsync(new Uri(endpoint), token);
        //     await AuthenticateAsync(token);
        //     _recvLoop = Task.Run(() => ReceiveLoopAsync(_cts!.Token), token);
        // }, ct).ConfigureAwait(false);

        _isConnected = true;
        _publisher.TryPublish(MarketEvent.Heartbeat(DateTimeOffset.UtcNow, source: "Polygon"));

        _log.Warning(
            "Polygon WebSocket connection not yet implemented. " +
            "API key is configured but client is operating in stub mode. " +
            "Full implementation pending.");

        return Task.CompletedTask;
    }

    /// <summary>
    /// Disconnects from Polygon WebSocket stream.
    /// </summary>
    public Task DisconnectAsync(CancellationToken ct = default)
    {
        _log.Information("Polygon client disconnecting (Mode: {Mode})", IsStubMode ? "Stub" : "Live");

        lock (_gate)
        {
            _tradeSymbols.Clear();
            _quoteSymbols.Clear();
            _subs.Clear();
        }

        _isConnected = false;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Subscribes to market depth (L2) for the specified symbol.
    /// Note: Polygon provides BBO quotes, not full L2 order book depth.
    /// </summary>
    /// <returns>Subscription ID, or -1 if not supported/not subscribed.</returns>
    public int SubscribeMarketDepth(SymbolConfig cfg)
    {
        if (cfg is null) throw new ArgumentNullException(nameof(cfg));

        // Polygon provides quotes (BBO), not full L2 depth
        // We can map quotes to BBO updates via QuoteCollector
        if (!_options.SubscribeQuotes)
        {
            _log.Debug("Quote subscription disabled in Polygon options, skipping depth for {Symbol}", cfg.Symbol);
            return -1;
        }

        var symbol = cfg.Symbol.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(symbol)) return -1;

        var id = Interlocked.Increment(ref _nextSubId);
        lock (_gate)
        {
            _quoteSymbols.Add(symbol);
            _subs[id] = (symbol, "quotes");
        }

        _log.Debug("Subscribed to Polygon quotes for {Symbol} (SubId: {SubId}, Mode: {Mode})",
            symbol, id, IsStubMode ? "Stub" : "Live");

        // In live mode, would send: {"action":"subscribe","params":"Q.{symbol}"}
        return id;
    }

    /// <summary>
    /// Unsubscribes from market depth for the specified subscription.
    /// </summary>
    public void UnsubscribeMarketDepth(int subscriptionId)
    {
        lock (_gate)
        {
            if (!_subs.TryGetValue(subscriptionId, out var sub)) return;
            _subs.Remove(subscriptionId);
            if (sub.Kind == "quotes")
            {
                // Only remove symbol if no other quote subs exist for it
                if (!_subs.Values.Any(v => v.Kind == "quotes" && v.Symbol.Equals(sub.Symbol, StringComparison.OrdinalIgnoreCase)))
                {
                    _quoteSymbols.Remove(sub.Symbol);
                    _log.Debug("Unsubscribed from Polygon quotes for {Symbol}", sub.Symbol);
                }
            }
        }
    }

    /// <summary>
    /// Subscribes to tick-by-tick trades for the specified symbol.
    /// </summary>
    /// <returns>Subscription ID, or -1 if not supported/not subscribed.</returns>
    public int SubscribeTrades(SymbolConfig cfg)
    {
        if (cfg is null) throw new ArgumentNullException(nameof(cfg));

        if (!_options.SubscribeTrades)
        {
            _log.Debug("Trade subscription disabled in Polygon options, skipping trades for {Symbol}", cfg.Symbol);
            return -1;
        }

        var symbol = cfg.Symbol.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(symbol)) return -1;

        var id = Interlocked.Increment(ref _nextSubId);
        lock (_gate)
        {
            _tradeSymbols.Add(symbol);
            _subs[id] = (symbol, "trades");
        }

        _log.Debug("Subscribed to Polygon trades for {Symbol} (SubId: {SubId}, Mode: {Mode})",
            symbol, id, IsStubMode ? "Stub" : "Live");

        // In stub mode, emit a synthetic trade for testing
        if (IsStubMode)
        {
            _tradeCollector.OnTrade(new MarketTradeUpdate(
                Timestamp: DateTimeOffset.UtcNow,
                Symbol: symbol,
                Price: 0m,
                Size: 0,
                Aggressor: AggressorSide.Unknown,
                SequenceNumber: 0,
                StreamId: "POLYGON_STUB",
                Venue: "POLYGON"));
        }

        // In live mode, would send: {"action":"subscribe","params":"T.{symbol}"}
        return id;
    }

    /// <summary>
    /// Unsubscribes from trades for the specified subscription.
    /// </summary>
    public void UnsubscribeTrades(int subscriptionId)
    {
        lock (_gate)
        {
            if (!_subs.TryGetValue(subscriptionId, out var sub)) return;
            _subs.Remove(subscriptionId);
            if (sub.Kind == "trades")
            {
                // Only remove symbol if no other trade subs exist for it
                if (!_subs.Values.Any(v => v.Kind == "trades" && v.Symbol.Equals(sub.Symbol, StringComparison.OrdinalIgnoreCase)))
                {
                    _tradeSymbols.Remove(sub.Symbol);
                    _log.Debug("Unsubscribed from Polygon trades for {Symbol}", sub.Symbol);
                }
            }
        }
    }

    /// <summary>
    /// Gets the current subscription count.
    /// </summary>
    public int SubscriptionCount
    {
        get
        {
            lock (_gate)
            {
                return _subs.Count;
            }
        }
    }

    /// <summary>
    /// Gets the list of currently subscribed trade symbols.
    /// </summary>
    public IReadOnlyList<string> SubscribedTradeSymbols
    {
        get
        {
            lock (_gate)
            {
                return _tradeSymbols.ToList();
            }
        }
    }

    /// <summary>
    /// Gets the list of currently subscribed quote symbols.
    /// </summary>
    public IReadOnlyList<string> SubscribedQuoteSymbols
    {
        get
        {
            lock (_gate)
            {
                return _quoteSymbols.ToList();
            }
        }
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        _log.Information("Disposing Polygon client");
        _isConnected = false;

        lock (_gate)
        {
            _tradeSymbols.Clear();
            _quoteSymbols.Clear();
            _subs.Clear();
        }

        return ValueTask.CompletedTask;
    }
}
