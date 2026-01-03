#if STOCKSHARP
using StockSharp.Algo;
using StockSharp.BusinessEntities;
using StockSharp.Messages;
#endif
using MarketDataCollector.Application.Config;
using MarketDataCollector.Application.Logging;
using MarketDataCollector.Domain.Collectors;
using MarketDataCollector.Domain.Models;
using MarketDataCollector.Infrastructure.Providers.StockSharp.Converters;
using Serilog;

namespace MarketDataCollector.Infrastructure.Providers.StockSharp;

/// <summary>
/// IMarketDataClient implementation that wraps StockSharp connectors.
/// Provides unified access to 90+ data sources through the S# adapter pattern.
///
/// Supported data types:
/// - Trades (tick-by-tick)
/// - Market Depth (Level 2 order books)
/// - Quotes (BBO)
///
/// See StockSharpConnectorFactory for available connector types.
/// </summary>
public sealed class StockSharpMarketDataClient : IMarketDataClient
{
    private readonly ILogger _log = LoggingSetup.ForContext<StockSharpMarketDataClient>();
    private readonly TradeDataCollector _tradeCollector;
    private readonly MarketDepthCollector _depthCollector;
    private readonly QuoteCollector _quoteCollector;
    private readonly StockSharpConfig _config;

#if STOCKSHARP
    private Connector? _connector;
    private readonly Dictionary<int, (Security Security, string Symbol)> _subscriptions = new();
    private readonly Dictionary<string, Security> _securities = new();
#endif

    private int _nextSubId = 200_000; // Keep away from other provider IDs
    private bool _disposed;
    private readonly object _gate = new();

    /// <summary>
    /// Creates a new StockSharp market data client.
    /// </summary>
    /// <param name="tradeCollector">Collector for trade events.</param>
    /// <param name="depthCollector">Collector for market depth events.</param>
    /// <param name="quoteCollector">Collector for quote/BBO events.</param>
    /// <param name="config">StockSharp configuration.</param>
    public StockSharpMarketDataClient(
        TradeDataCollector tradeCollector,
        MarketDepthCollector depthCollector,
        QuoteCollector quoteCollector,
        StockSharpConfig config)
    {
        _tradeCollector = tradeCollector ?? throw new ArgumentNullException(nameof(tradeCollector));
        _depthCollector = depthCollector ?? throw new ArgumentNullException(nameof(depthCollector));
        _quoteCollector = quoteCollector ?? throw new ArgumentNullException(nameof(quoteCollector));
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    /// <summary>
    /// Whether this client is enabled based on configuration.
    /// </summary>
    public bool IsEnabled => _config.Enabled;

#if STOCKSHARP
    /// <summary>
    /// Connect to the configured StockSharp data source.
    /// </summary>
    public async Task ConnectAsync(CancellationToken ct = default)
    {
        if (_connector != null)
        {
            _log.Debug("StockSharp connector already initialized, skipping connection");
            return;
        }

        _log.Information("Initializing StockSharp connector: {Type}", _config.ConnectorType);

        _connector = StockSharpConnectorFactory.Create(_config);

        // Wire up event handlers
        _connector.Connected += OnConnected;
        _connector.Disconnected += OnDisconnected;
        _connector.ConnectionError += OnConnectionError;
        _connector.NewTrade += OnNewTrade;
        _connector.MarketDepthChanged += OnMarketDepthChanged;
        _connector.ValuesChanged += OnValuesChanged;
        _connector.Error += OnError;

        var tcs = new TaskCompletionSource<bool>();
        using var registration = ct.Register(() => tcs.TrySetCanceled());

        void ConnectedHandler(object? sender, EventArgs e) => tcs.TrySetResult(true);
        void ErrorHandler(Exception ex) => tcs.TrySetException(ex);

        _connector.Connected += ConnectedHandler;
        _connector.ConnectionError += ErrorHandler;

        try
        {
            _connector.Connect();
            await tcs.Task.ConfigureAwait(false);
            _log.Information("StockSharp connector connected successfully to {Type}", _config.ConnectorType);
        }
        catch (OperationCanceledException)
        {
            _log.Warning("StockSharp connection cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Failed to connect to StockSharp {Type}", _config.ConnectorType);
            throw;
        }
        finally
        {
            _connector.Connected -= ConnectedHandler;
            _connector.ConnectionError -= ErrorHandler;
        }
    }

    /// <summary>
    /// Disconnect from the StockSharp data source.
    /// </summary>
    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        if (_connector == null)
        {
            _log.Debug("StockSharp connector not initialized, skipping disconnect");
            return;
        }

        _log.Information("Disconnecting from StockSharp {Type}", _config.ConnectorType);

        var tcs = new TaskCompletionSource<bool>();
        void Handler(object? sender, EventArgs e) => tcs.TrySetResult(true);

        _connector.Disconnected += Handler;

        try
        {
            _connector.Disconnect();
            await tcs.Task.WaitAsync(TimeSpan.FromSeconds(10), ct).ConfigureAwait(false);
            _log.Information("StockSharp connector disconnected");
        }
        catch (TimeoutException)
        {
            _log.Warning("StockSharp disconnect timed out");
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "Error during StockSharp disconnect");
        }
        finally
        {
            _connector.Disconnected -= Handler;
        }
    }

    /// <summary>
    /// Subscribe to market depth (Level 2) for a symbol.
    /// </summary>
    public int SubscribeMarketDepth(SymbolConfig cfg)
    {
        if (_connector == null)
            throw new InvalidOperationException("StockSharp connector not initialized. Call ConnectAsync first.");

        if (cfg == null)
            throw new ArgumentNullException(nameof(cfg));

        var security = GetOrCreateSecurity(cfg);
        var subId = Interlocked.Increment(ref _nextSubId);

        lock (_gate)
        {
            _subscriptions[subId] = (security, cfg.Symbol);
        }

        _connector.SubscribeMarketDepth(security);
        _log.Debug("Subscribed to depth: {Symbol} (subId={SubId})", cfg.Symbol, subId);

        return subId;
    }

    /// <summary>
    /// Unsubscribe from market depth.
    /// </summary>
    public void UnsubscribeMarketDepth(int subscriptionId)
    {
        (Security Security, string Symbol) sub;
        lock (_gate)
        {
            if (!_subscriptions.TryGetValue(subscriptionId, out sub))
                return;
            _subscriptions.Remove(subscriptionId);
        }

        _connector?.UnSubscribeMarketDepth(sub.Security);
        _log.Debug("Unsubscribed from depth: {Symbol} (subId={SubId})", sub.Symbol, subscriptionId);
    }

    /// <summary>
    /// Subscribe to trades for a symbol.
    /// </summary>
    public int SubscribeTrades(SymbolConfig cfg)
    {
        if (_connector == null)
            throw new InvalidOperationException("StockSharp connector not initialized. Call ConnectAsync first.");

        if (cfg == null)
            throw new ArgumentNullException(nameof(cfg));

        var security = GetOrCreateSecurity(cfg);
        var subId = Interlocked.Increment(ref _nextSubId);

        lock (_gate)
        {
            _subscriptions[subId] = (security, cfg.Symbol);
        }

        _connector.SubscribeTrades(security);
        _log.Debug("Subscribed to trades: {Symbol} (subId={SubId})", cfg.Symbol, subId);

        return subId;
    }

    /// <summary>
    /// Unsubscribe from trades.
    /// </summary>
    public void UnsubscribeTrades(int subscriptionId)
    {
        (Security Security, string Symbol) sub;
        lock (_gate)
        {
            if (!_subscriptions.TryGetValue(subscriptionId, out sub))
                return;
            _subscriptions.Remove(subscriptionId);
        }

        _connector?.UnSubscribeTrades(sub.Security);
        _log.Debug("Unsubscribed from trades: {Symbol} (subId={SubId})", sub.Symbol, subscriptionId);
    }

    /// <summary>
    /// Dispose of the client and release resources.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        await DisconnectAsync().ConfigureAwait(false);

        if (_connector != null)
        {
            _connector.Connected -= OnConnected;
            _connector.Disconnected -= OnDisconnected;
            _connector.ConnectionError -= OnConnectionError;
            _connector.NewTrade -= OnNewTrade;
            _connector.MarketDepthChanged -= OnMarketDepthChanged;
            _connector.ValuesChanged -= OnValuesChanged;
            _connector.Error -= OnError;

            _connector.Dispose();
            _connector = null;
        }

        lock (_gate)
        {
            _subscriptions.Clear();
            _securities.Clear();
        }
    }

    #region Event Handlers

    private void OnConnected(object? sender, EventArgs e)
    {
        _log.Information("StockSharp connector connected");
    }

    private void OnDisconnected(object? sender, EventArgs e)
    {
        _log.Information("StockSharp connector disconnected");
    }

    private void OnConnectionError(Exception ex)
    {
        _log.Error(ex, "StockSharp connection error");
    }

    private void OnError(Exception ex)
    {
        _log.Error(ex, "StockSharp error");
    }

    /// <summary>
    /// Handle incoming trade ticks from StockSharp.
    /// </summary>
    private void OnNewTrade(Trade trade)
    {
        if (trade == null) return;

        var symbol = trade.Security?.Code ?? trade.Security?.Id ?? "UNKNOWN";

        try
        {
            var update = new MarketTradeUpdate(
                Timestamp: trade.Time,
                Symbol: symbol,
                Price: trade.Price,
                Size: (long)trade.Volume,
                Aggressor: trade.OrderDirection switch
                {
                    Sides.Buy => AggressorSide.Buy,
                    Sides.Sell => AggressorSide.Sell,
                    _ => AggressorSide.Unknown
                },
                SequenceNumber: trade.Id,
                StreamId: "STOCKSHARP",
                Venue: trade.Security?.Board?.Code ?? _config.ConnectorType
            );

            _tradeCollector.OnTrade(update);
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "Error processing StockSharp trade for {Symbol}", symbol);
        }
    }

    /// <summary>
    /// Handle incoming market depth changes from StockSharp.
    /// </summary>
    private void OnMarketDepthChanged(MarketDepth depth)
    {
        if (depth?.Security == null) return;

        var symbol = depth.Security.Code ?? depth.Security.Id ?? "UNKNOWN";

        try
        {
            // Convert S# depth to MDC depth updates
            // Process bids
            for (int i = 0; i < depth.Bids.Length; i++)
            {
                var quote = depth.Bids[i];
                var update = new MarketDepthUpdate(
                    Timestamp: depth.LastChangeTime,
                    Symbol: symbol,
                    Position: i,
                    Operation: DepthOperation.Update,
                    Side: OrderBookSide.Bid,
                    Price: quote.Price,
                    Size: quote.Volume,
                    MarketMaker: null,
                    SequenceNumber: 0,
                    StreamId: "STOCKSHARP",
                    Venue: depth.Security.Board?.Code ?? _config.ConnectorType
                );
                _depthCollector.OnDepth(update);
            }

            // Process asks
            for (int i = 0; i < depth.Asks.Length; i++)
            {
                var quote = depth.Asks[i];
                var update = new MarketDepthUpdate(
                    Timestamp: depth.LastChangeTime,
                    Symbol: symbol,
                    Position: i,
                    Operation: DepthOperation.Update,
                    Side: OrderBookSide.Ask,
                    Price: quote.Price,
                    Size: quote.Volume,
                    MarketMaker: null,
                    SequenceNumber: 0,
                    StreamId: "STOCKSHARP",
                    Venue: depth.Security.Board?.Code ?? _config.ConnectorType
                );
                _depthCollector.OnDepth(update);
            }
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "Error processing StockSharp depth for {Symbol}", symbol);
        }
    }

    /// <summary>
    /// Handle Level1 value changes (BBO quotes).
    /// </summary>
    private void OnValuesChanged(Security security, IEnumerable<KeyValuePair<Level1Fields, object>> changes, DateTimeOffset serverTime, DateTimeOffset localTime)
    {
        if (security == null) return;

        var symbol = security.Code ?? security.Id ?? "UNKNOWN";

        try
        {
            decimal bidPrice = 0, askPrice = 0;
            long bidSize = 0, askSize = 0;

            foreach (var change in changes)
            {
                switch (change.Key)
                {
                    case Level1Fields.BestBidPrice when change.Value is decimal d:
                        bidPrice = d;
                        break;
                    case Level1Fields.BestBidVolume when change.Value is decimal d:
                        bidSize = (long)d;
                        break;
                    case Level1Fields.BestAskPrice when change.Value is decimal d:
                        askPrice = d;
                        break;
                    case Level1Fields.BestAskVolume when change.Value is decimal d:
                        askSize = (long)d;
                        break;
                }
            }

            // Only emit quote if we have valid bid/ask prices
            if (bidPrice > 0 || askPrice > 0)
            {
                var quoteUpdate = new MarketQuoteUpdate(
                    Timestamp: serverTime,
                    Symbol: symbol,
                    BidPrice: bidPrice,
                    BidSize: bidSize,
                    AskPrice: askPrice,
                    AskSize: askSize,
                    SequenceNumber: null,
                    StreamId: "STOCKSHARP",
                    Venue: security.Board?.Code ?? _config.ConnectorType
                );

                _quoteCollector.OnQuote(quoteUpdate);
            }
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "Error processing StockSharp Level1 for {Symbol}", symbol);
        }
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Get or create a StockSharp Security from MDC SymbolConfig.
    /// </summary>
    private Security GetOrCreateSecurity(SymbolConfig cfg)
    {
        var key = cfg.LocalSymbol ?? cfg.Symbol;

        lock (_gate)
        {
            if (!_securities.TryGetValue(key, out var security))
            {
                security = SecurityConverter.ToSecurity(cfg);
                _securities[key] = security;
            }
            return security;
        }
    }

    #endregion

#else
    // Stub implementations when StockSharp packages are not installed

    /// <summary>
    /// Stub: Connect not available without StockSharp packages.
    /// </summary>
    public Task ConnectAsync(CancellationToken ct = default)
    {
        throw new NotSupportedException(
            "StockSharp integration requires StockSharp.Algo NuGet package. " +
            "Install with: dotnet add package StockSharp.Algo");
    }

    /// <summary>
    /// Stub: Disconnect not available without StockSharp packages.
    /// </summary>
    public Task DisconnectAsync(CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Stub: SubscribeMarketDepth not available without StockSharp packages.
    /// </summary>
    public int SubscribeMarketDepth(SymbolConfig cfg)
    {
        throw new NotSupportedException(
            "StockSharp integration requires StockSharp.Algo NuGet package.");
    }

    /// <summary>
    /// Stub: UnsubscribeMarketDepth not available without StockSharp packages.
    /// </summary>
    public void UnsubscribeMarketDepth(int subscriptionId)
    {
    }

    /// <summary>
    /// Stub: SubscribeTrades not available without StockSharp packages.
    /// </summary>
    public int SubscribeTrades(SymbolConfig cfg)
    {
        throw new NotSupportedException(
            "StockSharp integration requires StockSharp.Algo NuGet package.");
    }

    /// <summary>
    /// Stub: UnsubscribeTrades not available without StockSharp packages.
    /// </summary>
    public void UnsubscribeTrades(int subscriptionId)
    {
    }

    /// <summary>
    /// Stub: DisposeAsync.
    /// </summary>
    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
#endif
}
