using MarketDataCollector.Application.Config;
using System.Threading;
using MarketDataCollector.Application.Logging;
using MarketDataCollector.Domain.Collectors;
using MarketDataCollector.Domain.Events;
using MarketDataCollector.Infrastructure.Providers;
using MarketDataCollector.Infrastructure.Resilience;
using Polly;
using Serilog;

namespace MarketDataCollector.Infrastructure.Providers.Polygon;

/// <summary>
/// Minimal stub for a Polygon market data adapter. This validates the provider abstraction and can be
/// evolved to a full WebSocket client later. For now it exercises the pipelines with a synthetic heartbeat.
///
/// Connection Resilience (when fully implemented):
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
///
/// <para><b>Current Status:</b></para>
/// <list type="bullet">
/// <item><description>Subscription tracking: IMPLEMENTED - generates unique IDs and tracks subscribed symbols</description></item>
/// <item><description>WebSocket connection: STUB - uses synthetic heartbeat, awaiting full implementation</description></item>
/// </list>
/// </summary>
// TODO: Implement full Polygon WebSocket client with real data streaming
// TODO: Replace synthetic heartbeat with actual Polygon API connection
// TODO: Handle Polygon message parsing and route to collectors
// TODO: Use _connectionPipeline for resilience (retry/circuit breaker) in real WebSocket code
// NOTE: Subscription tracking is now implemented - SubscribeTrades and SubscribeMarketDepth
//       generate unique IDs and track symbols. When WebSocket is implemented, use
//       _tradeSymbols and _quoteSymbols to send subscription messages to Polygon.
public sealed class PolygonMarketDataClient : IMarketDataClient
{
    private readonly ILogger _log = LoggingSetup.ForContext<PolygonMarketDataClient>();
    private readonly IMarketEventPublisher _publisher;
    private readonly TradeDataCollector _tradeCollector;
    private readonly QuoteCollector _quoteCollector;
    private readonly PolygonOptions _opt;
    private readonly string? _apiKey;
    private readonly bool _hasValidCredentials;

    // Resilience pipeline for connection retry with exponential backoff
    // Pre-configured for when full WebSocket implementation is added
    private readonly ResiliencePipeline _connectionPipeline;

    // Subscription tracking - ready for full WebSocket implementation
    private readonly object _gate = new();
    private readonly HashSet<string> _tradeSymbols = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _quoteSymbols = new(StringComparer.OrdinalIgnoreCase);
    private int _nextSubId = 200_000; // keep away from IB (0-99999) and Alpaca (100000-199999) ids
    private readonly Dictionary<int, (string Symbol, string Kind)> _subs = new();

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
        // Ready to use when full WebSocket implementation is added
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
    /// Connects to Polygon WebSocket stream.
    /// Currently a stub - will use _connectionPipeline for retry logic when fully implemented.
    /// </summary>
    public Task ConnectAsync(CancellationToken ct = default)
    {
        if (!_hasValidCredentials)
        {
            _log.Warning("Polygon client connect called without valid API key - running in stub mode. " +
                "Configure POLYGON_API_KEY environment variable for real data streaming.");
        }
        else
        {
            _log.Information("Polygon client connect called (stub mode - credentials configured, awaiting full implementation)");
        }

        // Emit a synthetic heartbeat so downstream consumers can verify connectivity without real credentials.
        _publisher.TryPublish(MarketEvent.Heartbeat(DateTimeOffset.UtcNow, source: "PolygonStub"));

        // When implementing full WebSocket connection, use:
        // await _connectionPipeline.ExecuteAsync(async token =>
        // {
        //     var uri = new Uri($"wss://socket.polygon.io/{_opt.Feed}");
        //     // WebSocket connection logic here
        //     // var apiKey = GetApiKey();
        //     // Connect to wss://socket.polygon.io/stocks with API key authentication
        // }, ct).ConfigureAwait(false);

        return Task.CompletedTask;
    }

    public Task DisconnectAsync(CancellationToken ct = default)
    {
        _log.Information("Polygon client disconnect called (stub mode)");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Subscribes to market depth (quotes) for a symbol.
    /// Polygon provides L2 quotes that map to BBO updates via QuoteCollector.
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

        _log.Information("Subscribed to quotes for {Symbol} with ID {SubscriptionId} (stub mode - awaiting WebSocket implementation)", symbol, id);
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

        // Emit a lightweight synthetic trade for testing cross-provider reconciliation
        _tradeCollector.OnTrade(new Domain.Models.MarketTradeUpdate(
            DateTimeOffset.UtcNow, symbol, 0m, 0, Domain.Models.AggressorSide.Unknown, 0, "POLY", "STUB"));

        _log.Information("Subscribed to trades for {Symbol} with ID {SubscriptionId} (stub mode - awaiting WebSocket implementation)", symbol, id);
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

        _log.Debug("Unsubscribed from trades for {Symbol} (subscription ID {SubscriptionId})", sub.Symbol, subscriptionId);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
