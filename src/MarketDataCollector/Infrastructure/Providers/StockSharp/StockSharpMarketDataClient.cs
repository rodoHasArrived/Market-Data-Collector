#if STOCKSHARP
using StockSharp.Algo;
using System.Threading;
using StockSharp.BusinessEntities;
using StockSharp.Messages;
#endif
using MarketDataCollector.Application.Config;
using MarketDataCollector.Application.Logging;
using MarketDataCollector.Domain.Collectors;
using MarketDataCollector.Domain.Models;
using MarketDataCollector.Infrastructure.Contracts;
using MarketDataCollector.Infrastructure.Providers.StockSharp.Converters;
using Serilog;

namespace MarketDataCollector.Infrastructure.Providers.StockSharp;

/// <summary>
/// IMarketDataClient implementation that wraps StockSharp connectors.
/// Provides unified access to 90+ data sources through the S# adapter pattern.
///
/// Features (inspired by Hydra best practices):
/// - Automatic reconnection with exponential backoff
/// - Subscription recovery after reconnection
/// - Connection health monitoring with heartbeats
/// - Message buffering for high-frequency data
///
/// Supported data types:
/// - Trades (tick-by-tick)
/// - Market Depth (Level 2 order books)
/// - Quotes (BBO)
///
/// See StockSharpConnectorFactory for available connector types.
/// </summary>
[ImplementsAdr("ADR-001", "StockSharp streaming data provider implementation")]
[ImplementsAdr("ADR-004", "All async methods support CancellationToken")]
public sealed class StockSharpMarketDataClient : IMarketDataClient
{
    private readonly ILogger _log = LoggingSetup.ForContext<StockSharpMarketDataClient>();
    private readonly TradeDataCollector _tradeCollector;
    private readonly MarketDepthCollector _depthCollector;
    private readonly QuoteCollector _quoteCollector;
    private readonly StockSharpConfig _config;

#if STOCKSHARP
    private Connector? _connector;
    private readonly Dictionary<int, (Security Security, string Symbol, SubscriptionType Type)> _subscriptions = new();
    private readonly Dictionary<string, Security> _securities = new();

    // Reconnection support (Hydra-inspired pattern)
    private CancellationTokenSource? _reconnectCts;
    private Task? _reconnectTask;
    private int _reconnectAttempt;
    private const int MaxReconnectAttempts = 10;
    private static readonly TimeSpan[] ReconnectDelays = new[]
    {
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
    };

    // Heartbeat monitoring
    private DateTimeOffset _lastDataReceived = DateTimeOffset.UtcNow;
    private Timer? _heartbeatTimer;
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan HeartbeatTimeout = TimeSpan.FromMinutes(2);

    // Message buffering for high-frequency data
    private readonly System.Threading.Channels.Channel<Action> _messageChannel;
    private Task? _messageProcessorTask;
    private CancellationTokenSource? _processorCts;
#endif

    private int _nextSubId = 200_000; // Keep away from other provider IDs
    private bool _disposed;
    private readonly object _gate = new();

    /// <summary>
    /// Event raised when connection state changes.
    /// </summary>
    public event Action<ConnectionState>? ConnectionStateChanged;

    /// <summary>
    /// Current connection state.
    /// </summary>
    public ConnectionState CurrentState { get; private set; } = ConnectionState.Disconnected;

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

#if STOCKSHARP
        // Initialize bounded message channel for high-frequency data buffering
        // This prevents event handler blocking during bursts (Hydra pattern)
        _messageChannel = System.Threading.Channels.Channel.CreateBounded<Action>(
            new System.Threading.Channels.BoundedChannelOptions(50_000)
            {
                FullMode = System.Threading.Channels.BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false
            });
#endif
    }

    /// <summary>
    /// Whether this client is enabled based on configuration.
    /// </summary>
    public bool IsEnabled => _config.Enabled;

#if STOCKSHARP
    /// <summary>
    /// Connect to the configured StockSharp data source.
    /// Includes automatic reconnection and subscription recovery (Hydra pattern).
    /// </summary>
    public async Task ConnectAsync(CancellationToken ct = default)
    {
        if (_connector != null && CurrentState == ConnectionState.Connected)
        {
            _log.Debug("StockSharp connector already connected, skipping connection");
            return;
        }

        _log.Information("Initializing StockSharp connector: {Type}", _config.ConnectorType);
        SetConnectionState(ConnectionState.Connecting);

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

            // Reset reconnection attempt counter on successful connect
            _reconnectAttempt = 0;

            // Start message processor for buffered high-frequency data
            StartMessageProcessor();

            // Start heartbeat monitoring (Hydra pattern)
            StartHeartbeatMonitoring();

            SetConnectionState(ConnectionState.Connected);
            _log.Information("StockSharp connector connected successfully to {Type}", _config.ConnectorType);
        }
        catch (OperationCanceledException)
        {
            SetConnectionState(ConnectionState.Disconnected);
            _log.Warning("StockSharp connection cancelled");
            throw;
        }
        catch (Exception ex)
        {
            SetConnectionState(ConnectionState.Error);
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
    /// Start the message processor task for buffered events.
    /// </summary>
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
                        _log.Warning(ex, "Error processing buffered message");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown
            }
        }, _processorCts.Token);
    }

    /// <summary>
    /// Start heartbeat monitoring to detect stale connections (Hydra pattern).
    /// </summary>
    private void StartHeartbeatMonitoring()
    {
        _lastDataReceived = DateTimeOffset.UtcNow;
        _heartbeatTimer = new Timer(
            CheckHeartbeat,
            null,
            HeartbeatInterval,
            HeartbeatInterval);
    }

    /// <summary>
    /// Check if connection is still alive based on last data received.
    /// </summary>
    private void CheckHeartbeat(object? state)
    {
        if (_disposed || CurrentState != ConnectionState.Connected)
            return;

        var timeSinceLastData = DateTimeOffset.UtcNow - _lastDataReceived;
        if (timeSinceLastData > HeartbeatTimeout)
        {
            _log.Warning("No data received for {Duration}s, connection may be stale. Triggering reconnection.",
                timeSinceLastData.TotalSeconds);
            TriggerReconnection();
        }
    }

    /// <summary>
    /// Trigger automatic reconnection with subscription recovery.
    /// </summary>
    private void TriggerReconnection()
    {
        if (_disposed || _reconnectTask != null)
            return;

        _reconnectCts?.Cancel();
        _reconnectCts = new CancellationTokenSource();

        _reconnectTask = Task.Run(async () =>
        {
            try
            {
                await ReconnectWithRecoveryAsync(_reconnectCts.Token).ConfigureAwait(false);
            }
            finally
            {
                _reconnectTask = null;
            }
        });
    }

    /// <summary>
    /// Reconnect with exponential backoff and subscription recovery (Hydra pattern).
    /// </summary>
    private async Task ReconnectWithRecoveryAsync(CancellationToken ct)
    {
        // Save current subscriptions for recovery
        List<(int SubId, Security Security, string Symbol, SubscriptionType Type)> savedSubscriptions;
        lock (_gate)
        {
            savedSubscriptions = _subscriptions
                .Select(kvp => (kvp.Key, kvp.Value.Security, kvp.Value.Symbol, kvp.Value.Type))
                .ToList();
        }

        _log.Information("Starting reconnection with {Count} subscriptions to recover", savedSubscriptions.Count);
        SetConnectionState(ConnectionState.Reconnecting);

        while (!ct.IsCancellationRequested && _reconnectAttempt < MaxReconnectAttempts)
        {
            var delay = ReconnectDelays[Math.Min(_reconnectAttempt, ReconnectDelays.Length - 1)];
            _reconnectAttempt++;

            _log.Information("Reconnection attempt {Attempt}/{Max} in {Delay}s",
                _reconnectAttempt, MaxReconnectAttempts, delay.TotalSeconds);

            try
            {
                await Task.Delay(delay, ct).ConfigureAwait(false);

                // Disconnect existing connector
                if (_connector != null)
                {
                    try { _connector.Disconnect(); }
                    catch (Exception disconnectEx)
                    {
                        _log.Debug(disconnectEx, "Error disconnecting StockSharp connector during reconnection");
                    }
                    _connector.Dispose();
                    _connector = null;
                }

                // Create new connector and connect
                _connector = StockSharpConnectorFactory.Create(_config);
                _connector.Connected += OnConnected;
                _connector.Disconnected += OnDisconnected;
                _connector.ConnectionError += OnConnectionError;
                _connector.NewTrade += OnNewTrade;
                _connector.MarketDepthChanged += OnMarketDepthChanged;
                _connector.ValuesChanged += OnValuesChanged;
                _connector.Error += OnError;

                var tcs = new TaskCompletionSource<bool>();
                using var reg = ct.Register(() => tcs.TrySetCanceled());

                void Handler(object? sender, EventArgs e) => tcs.TrySetResult(true);
                _connector.Connected += Handler;

                _connector.Connect();
                await tcs.Task.WaitAsync(TimeSpan.FromSeconds(30), ct).ConfigureAwait(false);

                _connector.Connected -= Handler;
                _reconnectAttempt = 0;

                // Recover subscriptions
                await RecoverSubscriptionsAsync(savedSubscriptions, ct).ConfigureAwait(false);

                _lastDataReceived = DateTimeOffset.UtcNow;
                SetConnectionState(ConnectionState.Connected);
                _log.Information("Reconnection successful. {Count} subscriptions recovered.", savedSubscriptions.Count);
                return;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                _log.Debug("Reconnection cancelled");
                return;
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "Reconnection attempt {Attempt} failed", _reconnectAttempt);
            }
        }

        _log.Error("Failed to reconnect after {Attempts} attempts", _reconnectAttempt);
        SetConnectionState(ConnectionState.Error);
    }

    /// <summary>
    /// Recover subscriptions after successful reconnection (Hydra pattern).
    /// </summary>
    private async Task RecoverSubscriptionsAsync(
        List<(int SubId, Security Security, string Symbol, SubscriptionType Type)> subscriptions,
        CancellationToken ct)
    {
        foreach (var (subId, security, symbol, type) in subscriptions)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                switch (type)
                {
                    case SubscriptionType.Trades:
                        _connector?.SubscribeTrades(security);
                        _log.Debug("Recovered trade subscription for {Symbol}", symbol);
                        break;
                    case SubscriptionType.Depth:
                        _connector?.SubscribeMarketDepth(security);
                        _log.Debug("Recovered depth subscription for {Symbol}", symbol);
                        break;
                }

                // Small delay between subscriptions to avoid overwhelming the connector
                await Task.Delay(50, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "Failed to recover subscription for {Symbol}", symbol);
            }
        }
    }

    /// <summary>
    /// Update connection state and raise event.
    /// </summary>
    private void SetConnectionState(ConnectionState newState)
    {
        if (CurrentState != newState)
        {
            var oldState = CurrentState;
            CurrentState = newState;
            _log.Debug("Connection state changed: {OldState} → {NewState}", oldState, newState);
            ConnectionStateChanged?.Invoke(newState);
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
            _subscriptions[subId] = (security, cfg.Symbol, SubscriptionType.Depth);
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
        (Security Security, string Symbol, SubscriptionType Type) sub;
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
            _subscriptions[subId] = (security, cfg.Symbol, SubscriptionType.Trades);
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
        (Security Security, string Symbol, SubscriptionType Type) sub;
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

        // Cancel reconnection if in progress
        _reconnectCts?.Cancel();
        if (_reconnectTask != null)
        {
            try { await _reconnectTask.ConfigureAwait(false); }
            catch (Exception reconnectEx) when (reconnectEx is not OperationCanceledException)
            {
                _log.Debug(reconnectEx, "Error completing reconnection task during disposal");
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested during disposal
            }
        }

        // Stop heartbeat monitoring
        if (_heartbeatTimer != null)
        {
            await _heartbeatTimer.DisposeAsync().ConfigureAwait(false);
            _heartbeatTimer = null;
        }

        // Stop message processor
        _processorCts?.Cancel();
        if (_messageProcessorTask != null)
        {
            try { await _messageProcessorTask.ConfigureAwait(false); }
            catch (Exception processorEx) when (processorEx is not OperationCanceledException)
            {
                _log.Debug(processorEx, "Error completing message processor task during disposal");
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested during disposal
            }
        }
        _messageChannel.Writer.Complete();

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

        _reconnectCts?.Dispose();
        _processorCts?.Dispose();
        SetConnectionState(ConnectionState.Disconnected);
    }

    #region Event Handlers

    private void OnConnected(object? sender, EventArgs e)
    {
        _log.Information("StockSharp connector connected");
    }

    private void OnDisconnected(object? sender, EventArgs e)
    {
        _log.Information("StockSharp connector disconnected unexpectedly");

        // Trigger automatic reconnection if not disposed (Hydra pattern)
        if (!_disposed && CurrentState == ConnectionState.Connected)
        {
            TriggerReconnection();
        }
    }

    private void OnConnectionError(Exception ex)
    {
        _log.Error(ex, "StockSharp connection error");
        SetConnectionState(ConnectionState.Error);

        // Trigger automatic reconnection
        if (!_disposed)
        {
            TriggerReconnection();
        }
    }

    private void OnError(Exception ex)
    {
        _log.Error(ex, "StockSharp error");
    }

    /// <summary>
    /// Handle incoming trade ticks from StockSharp.
    /// Uses message buffering for high-frequency data (Hydra pattern).
    /// </summary>
    private void OnNewTrade(Trade trade)
    {
        if (trade == null) return;

        // Update heartbeat timestamp
        _lastDataReceived = DateTimeOffset.UtcNow;

        var symbol = trade.Security?.Code ?? trade.Security?.Id ?? "UNKNOWN";

        // Buffer the message for processing (Hydra pattern)
        // This prevents blocking the connector's callback thread during bursts
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
                    StreamId: "STOCKSHARP",
                    Venue: trade.Security?.Board?.Code ?? _config.ConnectorType
                );

                _tradeCollector.OnTrade(update);
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "Error processing StockSharp trade for {Symbol}", symbol);
            }
        });
    }

    /// <summary>
    /// Handle incoming market depth changes from StockSharp.
    /// Uses message buffering for high-frequency data (Hydra pattern).
    /// </summary>
    private void OnMarketDepthChanged(MarketDepth depth)
    {
        if (depth?.Security == null) return;

        // Update heartbeat timestamp
        _lastDataReceived = DateTimeOffset.UtcNow;

        var symbol = depth.Security.Code ?? depth.Security.Id ?? "UNKNOWN";
        var timestamp = depth.LastChangeTime;
        var venue = depth.Security.Board?.Code ?? _config.ConnectorType;

        // Capture values for the lambda closure
        var bids = depth.Bids.ToArray();
        var asks = depth.Asks.ToArray();

        // Buffer the message for processing (Hydra pattern)
        _messageChannel.Writer.TryWrite(() =>
        {
            try
            {
                // Process bids
                for (int i = 0; i < bids.Length; i++)
                {
                    var quote = bids[i];
                    var update = new MarketDepthUpdate(
                        Timestamp: timestamp,
                        Symbol: symbol,
                        Position: i,
                        Operation: DepthOperation.Update,
                        Side: OrderBookSide.Bid,
                        Price: quote.Price,
                        Size: quote.Volume,
                        MarketMaker: null,
                        SequenceNumber: 0,
                        StreamId: "STOCKSHARP",
                        Venue: venue
                    );
                    _depthCollector.OnDepth(update);
                }

                // Process asks
                for (int i = 0; i < asks.Length; i++)
                {
                    var quote = asks[i];
                    var update = new MarketDepthUpdate(
                        Timestamp: timestamp,
                        Symbol: symbol,
                        Position: i,
                        Operation: DepthOperation.Update,
                        Side: OrderBookSide.Ask,
                        Price: quote.Price,
                        Size: quote.Volume,
                        MarketMaker: null,
                        SequenceNumber: 0,
                        StreamId: "STOCKSHARP",
                        Venue: venue
                    );
                    _depthCollector.OnDepth(update);
                }
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "Error processing StockSharp depth for {Symbol}", symbol);
            }
        });
    }

    /// <summary>
    /// Handle Level1 value changes (BBO quotes).
    /// Uses message buffering for high-frequency data (Hydra pattern).
    /// </summary>
    private void OnValuesChanged(Security security, IEnumerable<KeyValuePair<Level1Fields, object>> changes, DateTimeOffset serverTime, DateTimeOffset localTime)
    {
        if (security == null) return;

        // Update heartbeat timestamp
        _lastDataReceived = DateTimeOffset.UtcNow;

        var symbol = security.Code ?? security.Id ?? "UNKNOWN";
        var venue = security.Board?.Code ?? _config.ConnectorType;

        // Pre-process values before buffering
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
        if (bidPrice <= 0 && askPrice <= 0) return;

        // Buffer the message for processing (Hydra pattern)
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
                    StreamId: "STOCKSHARP",
                    Venue: venue
                );

                _quoteCollector.OnQuote(quoteUpdate);
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "Error processing StockSharp Level1 for {Symbol}", symbol);
            }
        });
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

/// <summary>
/// Connection state for the StockSharp client (Hydra-inspired pattern).
/// </summary>
public enum ConnectionState
{
    /// <summary>Client is disconnected.</summary>
    Disconnected,

    /// <summary>Client is connecting.</summary>
    Connecting,

    /// <summary>Client is connected and receiving data.</summary>
    Connected,

    /// <summary>Client is reconnecting after a connection loss.</summary>
    Reconnecting,

    /// <summary>Client encountered an error.</summary>
    Error
}

/// <summary>
/// Type of subscription for recovery purposes.
/// </summary>
public enum SubscriptionType
{
    /// <summary>Trade tick subscription.</summary>
    Trades,

    /// <summary>Market depth (Level 2) subscription.</summary>
    Depth,

    /// <summary>BBO/Level 1 quote subscription.</summary>
    Quotes
}
