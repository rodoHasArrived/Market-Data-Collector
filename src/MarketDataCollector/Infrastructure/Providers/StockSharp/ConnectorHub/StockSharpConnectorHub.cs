#if STOCKSHARP
using StockSharp.Algo;
using StockSharp.BusinessEntities;
using StockSharp.Messages;
#endif
using MarketDataCollector.Application.Config;
using MarketDataCollector.Application.Logging;
using MarketDataCollector.Application.Pipeline;
using MarketDataCollector.Contracts.Domain.Models;
using MarketDataCollector.Domain.Collectors;
using MarketDataCollector.Domain.Models;
using MarketDataCollector.Infrastructure.Contracts;
using MarketDataCollector.Infrastructure.Providers.Core;
using Serilog;

namespace MarketDataCollector.Infrastructure.Providers.StockSharp.ConnectorHub;

/// <summary>
/// Unified connector hub that routes market data requests through StockSharp's adapter framework.
/// Acts as a single entry point for 90+ exchanges and data feeds, reducing per-provider code
/// by leveraging StockSharp's battle-tested adapter model.
/// </summary>
/// <remarks>
/// <para>
/// This hub implements a provider-agnostic routing layer that maps provider identifiers
/// (e.g., "alpaca", "polygon", "ib") to their corresponding StockSharp adapters.
/// Benefits include:
/// </para>
/// <list type="bullet">
///   <item>Unified reconnection logic with exponential backoff</item>
///   <item>Consistent heartbeat monitoring across all adapters</item>
///   <item>Automatic subscription recovery after disconnections</item>
///   <item>Message buffering for high-frequency data bursts</item>
///   <item>Single codebase for adapter lifecycle management</item>
/// </list>
/// <para>
/// Supported adapter types include: InteractiveBrokers, Rithmic, IQFeed, CQG, Alpaca, Polygon,
/// Binance, LMAX, FTX, and 80+ more through StockSharp's adapter ecosystem.
/// </para>
/// </remarks>
[ImplementsAdr("ADR-001", "Unified StockSharp connector hub for provider routing")]
[ImplementsAdr("ADR-004", "All async methods support CancellationToken")]
public sealed class StockSharpConnectorHub : IMarketDataClient
{
    private readonly ILogger _log = LoggingSetup.ForContext<StockSharpConnectorHub>();
    private readonly ConnectorHubOptions _options;
    private readonly IStockSharpAdapterRegistry _adapterRegistry;
    private readonly TradeDataCollector _tradeCollector;
    private readonly MarketDepthCollector _depthCollector;
    private readonly QuoteCollector _quoteCollector;

#if STOCKSHARP
    private readonly Dictionary<string, Connector> _connectors = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<int, (string ProviderId, Security Security, string Symbol, HubSubscriptionType Type)> _subscriptions = new();
    private readonly Dictionary<string, Dictionary<string, Security>> _securities = new(StringComparer.OrdinalIgnoreCase);

    // Reconnection support per connector
    private readonly Dictionary<string, ReconnectionState> _reconnectionStates = new(StringComparer.OrdinalIgnoreCase);
    private const int MaxReconnectAttempts = 10;
    private static readonly TimeSpan[] ReconnectDelays =
    [
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromSeconds(60),
        TimeSpan.FromMinutes(2),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(10),
        TimeSpan.FromMinutes(15)
    ];

    // Heartbeat monitoring
    private readonly Dictionary<string, DateTimeOffset> _lastDataReceived = new(StringComparer.OrdinalIgnoreCase);
    private Timer? _heartbeatTimer;
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan HeartbeatTimeout = TimeSpan.FromMinutes(2);

    // Message buffering for high-frequency data
    private readonly System.Threading.Channels.Channel<Action> _messageChannel;
    private Task? _messageProcessorTask;
    private CancellationTokenSource? _processorCts;
#endif

    private int _nextSubId = 300_000; // Connector hub uses higher ID range
    private bool _disposed;
    private readonly object _gate = new();

    /// <summary>
    /// Event raised when connection state changes for any adapter.
    /// </summary>
    public event Action<string, ConnectionState>? AdapterConnectionStateChanged;

    /// <summary>
    /// Creates a new StockSharp connector hub.
    /// </summary>
    public StockSharpConnectorHub(
        ConnectorHubOptions options,
        IStockSharpAdapterRegistry adapterRegistry,
        TradeDataCollector tradeCollector,
        MarketDepthCollector depthCollector,
        QuoteCollector quoteCollector)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _adapterRegistry = adapterRegistry ?? throw new ArgumentNullException(nameof(adapterRegistry));
        _tradeCollector = tradeCollector ?? throw new ArgumentNullException(nameof(tradeCollector));
        _depthCollector = depthCollector ?? throw new ArgumentNullException(nameof(depthCollector));
        _quoteCollector = quoteCollector ?? throw new ArgumentNullException(nameof(quoteCollector));

#if STOCKSHARP
        _messageChannel = System.Threading.Channels.Channel.CreateBounded<Action>(
            EventPipelinePolicy.MessageBuffer.ToBoundedOptions(singleReader: true, singleWriter: false));
#endif
    }

    /// <inheritdoc/>
    public bool IsEnabled => _options.Enabled;

    #region IProviderMetadata

    /// <inheritdoc/>
    public string ProviderId => "stocksharp-hub";

    /// <inheritdoc/>
    public string ProviderDisplayName => "StockSharp Connector Hub";

    /// <inheritdoc/>
    public string ProviderDescription =>
        "Unified connector hub providing access to 90+ exchanges through StockSharp's adapter framework";

    /// <inheritdoc/>
    public int ProviderPriority => 5; // High priority as the hub routes to multiple providers

    /// <inheritdoc/>
    public ProviderCapabilities ProviderCapabilities => ProviderCapabilities.Streaming(
        trades: true,
        quotes: true,
        depth: true,
        maxDepthLevels: 50);

    #endregion

#if STOCKSHARP
    /// <summary>
    /// Connect to all configured adapters in the hub.
    /// </summary>
    public async Task ConnectAsync(CancellationToken ct = default)
    {
        _log.Information("Initializing StockSharp Connector Hub with {Count} adapters",
            _options.EnabledAdapters.Count);

        // Start message processor
        StartMessageProcessor();

        // Connect to each enabled adapter
        var connectTasks = new List<Task>();
        foreach (var adapterId in _options.EnabledAdapters)
        {
            connectTasks.Add(ConnectAdapterAsync(adapterId, ct));
        }

        await Task.WhenAll(connectTasks).ConfigureAwait(false);

        // Start heartbeat monitoring
        StartHeartbeatMonitoring();

        _log.Information("StockSharp Connector Hub initialized with {Connected}/{Total} adapters connected",
            _connectors.Count, _options.EnabledAdapters.Count);
    }

    /// <summary>
    /// Connect to a specific adapter by ID.
    /// </summary>
    public async Task ConnectAdapterAsync(string adapterId, CancellationToken ct = default)
    {
        var adapterConfig = _adapterRegistry.GetAdapterConfig(adapterId);
        if (adapterConfig == null)
        {
            _log.Warning("Adapter {AdapterId} not found in registry, skipping", adapterId);
            return;
        }

        _log.Information("Connecting to adapter: {AdapterId} ({DisplayName})",
            adapterId, adapterConfig.DisplayName);

        try
        {
            var connector = CreateConnector(adapterConfig);
            WireConnectorEvents(connector, adapterId);

            var tcs = new TaskCompletionSource<bool>();
            using var registration = ct.Register(() => tcs.TrySetCanceled());

            void ConnectedHandler(object? sender, EventArgs e) => tcs.TrySetResult(true);
            void ErrorHandler(Exception ex) => tcs.TrySetException(ex);

            connector.Connected += ConnectedHandler;
            connector.ConnectionError += ErrorHandler;

            try
            {
                connector.Connect();
                await tcs.Task.WaitAsync(TimeSpan.FromSeconds(30), ct).ConfigureAwait(false);

                lock (_gate)
                {
                    _connectors[adapterId] = connector;
                    _lastDataReceived[adapterId] = DateTimeOffset.UtcNow;
                    _reconnectionStates[adapterId] = new ReconnectionState();
                }

                _log.Information("Adapter {AdapterId} connected successfully", adapterId);
                OnAdapterConnectionStateChanged(adapterId, ConnectionState.Connected);
            }
            finally
            {
                connector.Connected -= ConnectedHandler;
                connector.ConnectionError -= ErrorHandler;
            }
        }
        catch (OperationCanceledException)
        {
            _log.Warning("Connection to adapter {AdapterId} cancelled", adapterId);
            throw;
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Failed to connect to adapter {AdapterId}", adapterId);
            OnAdapterConnectionStateChanged(adapterId, ConnectionState.Error);
        }
    }

    /// <summary>
    /// Create a connector instance from adapter configuration.
    /// </summary>
    private Connector CreateConnector(IStockSharpAdapterConfig config)
    {
        var connector = new Connector();

        // Use the adapter factory to configure the connector
        var adapter = config.CreateAdapter(connector.TransactionIdGenerator);
        connector.Adapter.InnerAdapters.Add(adapter);

        _log.Debug("Created connector for adapter {AdapterId} with type {AdapterType}",
            config.AdapterId, adapter.GetType().Name);

        return connector;
    }

    /// <summary>
    /// Wire up event handlers for a connector.
    /// </summary>
    private void WireConnectorEvents(Connector connector, string adapterId)
    {
        connector.Connected += (s, e) => OnConnectorConnected(adapterId);
        connector.Disconnected += (s, e) => OnConnectorDisconnected(adapterId);
        connector.ConnectionError += ex => OnConnectorError(adapterId, ex);
        connector.NewTrade += trade => OnNewTrade(adapterId, trade);
        connector.MarketDepthChanged += depth => OnMarketDepthChanged(adapterId, depth);
        connector.ValuesChanged += (security, changes, serverTime, localTime) =>
            OnValuesChanged(adapterId, security, changes, serverTime, localTime);
        connector.Error += ex => _log.Warning(ex, "Connector {AdapterId} error", adapterId);
    }

    /// <summary>
    /// Disconnect from the hub and all adapters.
    /// </summary>
    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        _log.Information("Disconnecting StockSharp Connector Hub");

        var disconnectTasks = new List<Task>();

        lock (_gate)
        {
            foreach (var (adapterId, connector) in _connectors)
            {
                disconnectTasks.Add(DisconnectAdapterAsync(adapterId, connector, ct));
            }
        }

        await Task.WhenAll(disconnectTasks).ConfigureAwait(false);
    }

    private async Task DisconnectAdapterAsync(string adapterId, Connector connector, CancellationToken ct)
    {
        try
        {
            var tcs = new TaskCompletionSource<bool>();
            void Handler(object? sender, EventArgs e) => tcs.TrySetResult(true);

            connector.Disconnected += Handler;
            connector.Disconnect();

            await tcs.Task.WaitAsync(TimeSpan.FromSeconds(10), ct).ConfigureAwait(false);
            connector.Disconnected -= Handler;

            _log.Information("Adapter {AdapterId} disconnected", adapterId);
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "Error disconnecting adapter {AdapterId}", adapterId);
        }
    }

    /// <inheritdoc/>
    public int SubscribeMarketDepth(SymbolConfig cfg)
    {
        if (cfg == null)
            throw new ArgumentNullException(nameof(cfg));

        var adapterId = ResolveAdapterId(cfg);
        var connector = GetConnectorOrThrow(adapterId);
        var security = GetOrCreateSecurity(adapterId, cfg);
        var subId = Interlocked.Increment(ref _nextSubId);

        lock (_gate)
        {
            _subscriptions[subId] = (adapterId, security, cfg.Symbol, HubSubscriptionType.Depth);
        }

        connector.SubscribeMarketDepth(security);
        _log.Debug("Hub subscribed to depth: {Symbol} via {Adapter} (subId={SubId})",
            cfg.Symbol, adapterId, subId);

        return subId;
    }

    /// <inheritdoc/>
    public void UnsubscribeMarketDepth(int subscriptionId)
    {
        (string ProviderId, Security Security, string Symbol, HubSubscriptionType Type) sub;
        Connector? connector;

        lock (_gate)
        {
            if (!_subscriptions.TryGetValue(subscriptionId, out sub))
                return;
            _subscriptions.Remove(subscriptionId);
            _connectors.TryGetValue(sub.ProviderId, out connector);
        }

        connector?.UnSubscribeMarketDepth(sub.Security);
        _log.Debug("Hub unsubscribed from depth: {Symbol} (subId={SubId})", sub.Symbol, subscriptionId);
    }

    /// <inheritdoc/>
    public int SubscribeTrades(SymbolConfig cfg)
    {
        if (cfg == null)
            throw new ArgumentNullException(nameof(cfg));

        var adapterId = ResolveAdapterId(cfg);
        var connector = GetConnectorOrThrow(adapterId);
        var security = GetOrCreateSecurity(adapterId, cfg);
        var subId = Interlocked.Increment(ref _nextSubId);

        lock (_gate)
        {
            _subscriptions[subId] = (adapterId, security, cfg.Symbol, HubSubscriptionType.Trades);
        }

        connector.SubscribeTrades(security);
        _log.Debug("Hub subscribed to trades: {Symbol} via {Adapter} (subId={SubId})",
            cfg.Symbol, adapterId, subId);

        return subId;
    }

    /// <inheritdoc/>
    public void UnsubscribeTrades(int subscriptionId)
    {
        (string ProviderId, Security Security, string Symbol, HubSubscriptionType Type) sub;
        Connector? connector;

        lock (_gate)
        {
            if (!_subscriptions.TryGetValue(subscriptionId, out sub))
                return;
            _subscriptions.Remove(subscriptionId);
            _connectors.TryGetValue(sub.ProviderId, out connector);
        }

        connector?.UnSubscribeTrades(sub.Security);
        _log.Debug("Hub unsubscribed from trades: {Symbol} (subId={SubId})", sub.Symbol, subscriptionId);
    }

    #region Adapter Resolution

    /// <summary>
    /// Resolve the adapter ID for a symbol based on configuration rules.
    /// </summary>
    private string ResolveAdapterId(SymbolConfig cfg)
    {
        // Check explicit provider mapping in symbol config
        if (!string.IsNullOrEmpty(cfg.Provider))
        {
            var mappedAdapter = _adapterRegistry.GetAdapterForProvider(cfg.Provider);
            if (mappedAdapter != null)
                return mappedAdapter;
        }

        // Check exchange-based routing
        if (!string.IsNullOrEmpty(cfg.Exchange))
        {
            var exchangeAdapter = _adapterRegistry.GetAdapterForExchange(cfg.Exchange);
            if (exchangeAdapter != null)
                return exchangeAdapter;
        }

        // Check asset class routing
        if (!string.IsNullOrEmpty(cfg.AssetClass))
        {
            var assetAdapter = _adapterRegistry.GetAdapterForAssetClass(cfg.AssetClass);
            if (assetAdapter != null)
                return assetAdapter;
        }

        // Fall back to default adapter
        if (_options.DefaultAdapterId != null && _connectors.ContainsKey(_options.DefaultAdapterId))
        {
            return _options.DefaultAdapterId;
        }

        // Use first connected adapter
        lock (_gate)
        {
            var firstConnected = _connectors.Keys.FirstOrDefault();
            if (firstConnected != null)
                return firstConnected;
        }

        throw new InvalidOperationException(
            $"No adapter available for symbol {cfg.Symbol}. Ensure at least one adapter is connected.");
    }

    private Connector GetConnectorOrThrow(string adapterId)
    {
        lock (_gate)
        {
            if (_connectors.TryGetValue(adapterId, out var connector))
                return connector;
        }

        throw new InvalidOperationException(
            $"Adapter {adapterId} is not connected. Call ConnectAsync first.");
    }

    private Security GetOrCreateSecurity(string adapterId, SymbolConfig cfg)
    {
        var key = cfg.LocalSymbol ?? cfg.Symbol;

        lock (_gate)
        {
            if (!_securities.TryGetValue(adapterId, out var adapterSecurities))
            {
                adapterSecurities = new Dictionary<string, Security>(StringComparer.OrdinalIgnoreCase);
                _securities[adapterId] = adapterSecurities;
            }

            if (!adapterSecurities.TryGetValue(key, out var security))
            {
                security = Converters.SecurityConverter.ToSecurity(cfg);
                adapterSecurities[key] = security;
            }

            return security;
        }
    }

    #endregion

    #region Event Handlers

    private void OnConnectorConnected(string adapterId)
    {
        _log.Information("Connector {AdapterId} connected", adapterId);
        lock (_gate)
        {
            _lastDataReceived[adapterId] = DateTimeOffset.UtcNow;
        }
        OnAdapterConnectionStateChanged(adapterId, ConnectionState.Connected);
    }

    private void OnConnectorDisconnected(string adapterId)
    {
        _log.Warning("Connector {AdapterId} disconnected unexpectedly", adapterId);
        OnAdapterConnectionStateChanged(adapterId, ConnectionState.Disconnected);

        if (!_disposed)
        {
            TriggerReconnection(adapterId);
        }
    }

    private void OnConnectorError(string adapterId, Exception ex)
    {
        _log.Error(ex, "Connector {AdapterId} connection error", adapterId);
        OnAdapterConnectionStateChanged(adapterId, ConnectionState.Error);

        if (!_disposed)
        {
            TriggerReconnection(adapterId);
        }
    }

    private void OnNewTrade(string adapterId, Trade trade)
    {
        if (trade == null) return;

        lock (_gate)
        {
            _lastDataReceived[adapterId] = DateTimeOffset.UtcNow;
        }

        var symbol = trade.Security?.Code ?? trade.Security?.Id ?? "UNKNOWN";

        _messageChannel.Writer.TryWrite(() =>
        {
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
                    StreamId: $"STOCKSHARP-{adapterId.ToUpperInvariant()}",
                    Venue: trade.Security?.Board?.Code ?? adapterId
                );

                _tradeCollector.OnTrade(update);
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "Error processing trade for {Symbol} from {Adapter}", symbol, adapterId);
            }
        });
    }

    private void OnMarketDepthChanged(string adapterId, MarketDepth depth)
    {
        if (depth?.Security == null) return;

        lock (_gate)
        {
            _lastDataReceived[adapterId] = DateTimeOffset.UtcNow;
        }

        var symbol = depth.Security.Code ?? depth.Security.Id ?? "UNKNOWN";
        var timestamp = depth.LastChangeTime;
        var venue = depth.Security.Board?.Code ?? adapterId;
        var bids = depth.Bids.ToArray();
        var asks = depth.Asks.ToArray();

        _messageChannel.Writer.TryWrite(() =>
        {
            try
            {
                ProcessDepthSide(symbol, timestamp, venue, adapterId, bids, OrderBookSide.Bid);
                ProcessDepthSide(symbol, timestamp, venue, adapterId, asks, OrderBookSide.Ask);
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "Error processing depth for {Symbol} from {Adapter}", symbol, adapterId);
            }
        });
    }

    private void ProcessDepthSide(string symbol, DateTimeOffset timestamp, string venue,
        string adapterId, Quote[] quotes, OrderBookSide side)
    {
        for (int i = 0; i < quotes.Length; i++)
        {
            var quote = quotes[i];
            var update = new MarketDepthUpdate(
                Timestamp: timestamp,
                Symbol: symbol,
                Position: i,
                Operation: DepthOperation.Update,
                Side: side,
                Price: quote.Price,
                Size: quote.Volume,
                MarketMaker: null,
                SequenceNumber: 0,
                StreamId: $"STOCKSHARP-{adapterId.ToUpperInvariant()}",
                Venue: venue
            );
            _depthCollector.OnDepth(update);
        }
    }

    private void OnValuesChanged(string adapterId, Security security,
        IEnumerable<KeyValuePair<Level1Fields, object>> changes,
        DateTimeOffset serverTime, DateTimeOffset localTime)
    {
        if (security == null) return;

        lock (_gate)
        {
            _lastDataReceived[adapterId] = DateTimeOffset.UtcNow;
        }

        var symbol = security.Code ?? security.Id ?? "UNKNOWN";
        var venue = security.Board?.Code ?? adapterId;

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

        if (bidPrice <= 0 && askPrice <= 0) return;

        _messageChannel.Writer.TryWrite(() =>
        {
            try
            {
                var quoteUpdate = new MarketQuoteUpdate(
                    Timestamp: serverTime,
                    Symbol: symbol,
                    BidPrice: bidPrice,
                    BidSize: bidSize,
                    AskPrice: askPrice,
                    AskSize: askSize,
                    SequenceNumber: null,
                    StreamId: $"STOCKSHARP-{adapterId.ToUpperInvariant()}",
                    Venue: venue
                );

                _quoteCollector.OnQuote(quoteUpdate);
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "Error processing Level1 for {Symbol} from {Adapter}", symbol, adapterId);
            }
        });
    }

    private void OnAdapterConnectionStateChanged(string adapterId, ConnectionState state)
    {
        AdapterConnectionStateChanged?.Invoke(adapterId, state);
    }

    #endregion

    #region Reconnection

    private void TriggerReconnection(string adapterId)
    {
        ReconnectionState? state;
        lock (_gate)
        {
            if (!_reconnectionStates.TryGetValue(adapterId, out state))
            {
                state = new ReconnectionState();
                _reconnectionStates[adapterId] = state;
            }

            if (state.IsReconnecting)
                return;

            state.IsReconnecting = true;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await ReconnectWithRecoveryAsync(adapterId, state).ConfigureAwait(false);
            }
            finally
            {
                lock (_gate)
                {
                    state.IsReconnecting = false;
                }
            }
        });
    }

    private async Task ReconnectWithRecoveryAsync(string adapterId, ReconnectionState state)
    {
        // Save subscriptions for recovery
        List<(int SubId, Security Security, string Symbol, HubSubscriptionType Type)> savedSubs;
        lock (_gate)
        {
            savedSubs = _subscriptions
                .Where(kvp => kvp.Value.ProviderId == adapterId)
                .Select(kvp => (kvp.Key, kvp.Value.Security, kvp.Value.Symbol, kvp.Value.Type))
                .ToList();
        }

        _log.Information("Starting reconnection for {AdapterId} with {Count} subscriptions to recover",
            adapterId, savedSubs.Count);
        OnAdapterConnectionStateChanged(adapterId, ConnectionState.Reconnecting);

        while (!_disposed && state.Attempt < MaxReconnectAttempts)
        {
            var delay = ReconnectDelays[Math.Min(state.Attempt, ReconnectDelays.Length - 1)];
            state.Attempt++;

            _log.Information("Reconnection attempt {Attempt}/{Max} for {AdapterId} in {Delay}s",
                state.Attempt, MaxReconnectAttempts, adapterId, delay.TotalSeconds);

            try
            {
                await Task.Delay(delay).ConfigureAwait(false);

                // Dispose old connector
                Connector? oldConnector;
                lock (_gate)
                {
                    _connectors.TryGetValue(adapterId, out oldConnector);
                }

                if (oldConnector != null)
                {
                    try { oldConnector.Disconnect(); }
                    catch { /* ignore */ }
                    oldConnector.Dispose();
                }

                // Reconnect
                await ConnectAdapterAsync(adapterId, CancellationToken.None).ConfigureAwait(false);

                // Recover subscriptions
                await RecoverSubscriptionsAsync(adapterId, savedSubs).ConfigureAwait(false);

                state.Attempt = 0;
                _log.Information("Reconnection successful for {AdapterId}. {Count} subscriptions recovered.",
                    adapterId, savedSubs.Count);
                return;
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "Reconnection attempt {Attempt} failed for {AdapterId}", state.Attempt, adapterId);
            }
        }

        _log.Error("Failed to reconnect {AdapterId} after {Attempts} attempts", adapterId, state.Attempt);
        OnAdapterConnectionStateChanged(adapterId, ConnectionState.Error);
    }

    private async Task RecoverSubscriptionsAsync(string adapterId,
        List<(int SubId, Security Security, string Symbol, HubSubscriptionType Type)> subscriptions)
    {
        Connector? connector;
        lock (_gate)
        {
            if (!_connectors.TryGetValue(adapterId, out connector))
                return;
        }

        foreach (var (subId, security, symbol, type) in subscriptions)
        {
            try
            {
                switch (type)
                {
                    case HubSubscriptionType.Trades:
                        connector.SubscribeTrades(security);
                        _log.Debug("Recovered trade subscription for {Symbol}", symbol);
                        break;
                    case HubSubscriptionType.Depth:
                        connector.SubscribeMarketDepth(security);
                        _log.Debug("Recovered depth subscription for {Symbol}", symbol);
                        break;
                }

                await Task.Delay(50).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "Failed to recover subscription for {Symbol}", symbol);
            }
        }
    }

    #endregion

    #region Heartbeat Monitoring

    private void StartHeartbeatMonitoring()
    {
        _heartbeatTimer = new Timer(CheckHeartbeats, null, HeartbeatInterval, HeartbeatInterval);
    }

    private void CheckHeartbeats(object? state)
    {
        if (_disposed) return;

        List<string> staleAdapters;
        lock (_gate)
        {
            var now = DateTimeOffset.UtcNow;
            staleAdapters = _lastDataReceived
                .Where(kvp => now - kvp.Value > HeartbeatTimeout)
                .Select(kvp => kvp.Key)
                .ToList();
        }

        foreach (var adapterId in staleAdapters)
        {
            _log.Warning("No data received from {AdapterId} for timeout period, triggering reconnection", adapterId);
            TriggerReconnection(adapterId);
        }
    }

    #endregion

    #region Message Processor

    private void StartMessageProcessor()
    {
        _processorCts = new CancellationTokenSource();
        _messageProcessorTask = Task.Run(async () =>
        {
            try
            {
                await foreach (var action in _messageChannel.Reader.ReadAllAsync(_processorCts.Token))
                {
                    try
                    {
                        action();
                    }
                    catch (Exception ex)
                    {
                        _log.Warning(ex, "Error processing buffered message in hub");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown
            }
        }, _processorCts.Token);
    }

    #endregion

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        // Stop heartbeat
        if (_heartbeatTimer != null)
        {
            await _heartbeatTimer.DisposeAsync().ConfigureAwait(false);
        }

        // Stop message processor
        _processorCts?.Cancel();
        if (_messageProcessorTask != null)
        {
            try { await _messageProcessorTask.ConfigureAwait(false); }
            catch (OperationCanceledException) { /* expected */ }
        }
        _messageChannel.Writer.Complete();

        // Disconnect all adapters
        await DisconnectAsync().ConfigureAwait(false);

        // Dispose connectors
        lock (_gate)
        {
            foreach (var connector in _connectors.Values)
            {
                connector.Dispose();
            }
            _connectors.Clear();
            _subscriptions.Clear();
            _securities.Clear();
            _reconnectionStates.Clear();
        }

        _processorCts?.Dispose();
    }

#else
    // Stub implementations when StockSharp is not available

    /// <inheritdoc/>
    public Task ConnectAsync(CancellationToken ct = default)
    {
        throw new NotSupportedException(
            "StockSharp Connector Hub requires StockSharp.Algo NuGet package. " +
            "Install with: dotnet add package StockSharp.Algo");
    }

    /// <inheritdoc/>
    public Task DisconnectAsync(CancellationToken ct = default) => Task.CompletedTask;

    /// <inheritdoc/>
    public int SubscribeMarketDepth(SymbolConfig cfg)
    {
        throw new NotSupportedException("StockSharp Connector Hub requires StockSharp.Algo package.");
    }

    /// <inheritdoc/>
    public void UnsubscribeMarketDepth(int subscriptionId) { }

    /// <inheritdoc/>
    public int SubscribeTrades(SymbolConfig cfg)
    {
        throw new NotSupportedException("StockSharp Connector Hub requires StockSharp.Algo package.");
    }

    /// <inheritdoc/>
    public void UnsubscribeTrades(int subscriptionId) { }

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
#endif
}

#if STOCKSHARP
/// <summary>
/// Tracks reconnection state for an adapter.
/// </summary>
internal sealed class ReconnectionState
{
    public int Attempt { get; set; }
    public bool IsReconnecting { get; set; }
}
#endif

/// <summary>
/// Subscription type for the connector hub.
/// </summary>
public enum HubSubscriptionType
{
    /// <summary>Trade tick subscription.</summary>
    Trades,

    /// <summary>Market depth subscription.</summary>
    Depth,

    /// <summary>Quote (BBO) subscription.</summary>
    Quotes
}
