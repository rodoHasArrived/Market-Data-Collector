using System.Collections.Concurrent;
using System.Threading;
using MarketDataCollector.Application.Config;
using MarketDataCollector.Application.Logging;
using MarketDataCollector.Domain.Collectors;
using MarketDataCollector.Infrastructure.Providers.Alpaca;
using Serilog;

namespace MarketDataCollector.Infrastructure.Providers.MultiProvider;

/// <summary>
/// Manages simultaneous connections to multiple market data providers.
/// Enables parallel data collection from IB, Alpaca, Polygon, etc.
/// </summary>
public sealed class MultiProviderConnectionManager : IAsyncDisposable
{
    private readonly ILogger _log = LoggingSetup.ForContext<MultiProviderConnectionManager>();
    private readonly ConcurrentDictionary<string, ProviderConnection> _connections = new();
    private readonly ConcurrentDictionary<string, ProviderMetrics> _providerMetrics = new();
    private readonly TradeDataCollector _tradeCollector;
    private readonly MarketDepthCollector _depthCollector;
    private readonly QuoteCollector _quoteCollector;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);

    public MultiProviderConnectionManager(
        TradeDataCollector tradeCollector,
        MarketDepthCollector depthCollector,
        QuoteCollector quoteCollector)
    {
        _tradeCollector = tradeCollector ?? throw new ArgumentNullException(nameof(tradeCollector));
        _depthCollector = depthCollector ?? throw new ArgumentNullException(nameof(depthCollector));
        _quoteCollector = quoteCollector ?? throw new ArgumentNullException(nameof(quoteCollector));
    }

    /// <summary>
    /// Gets all active provider connections.
    /// </summary>
    public IReadOnlyDictionary<string, ProviderConnection> ActiveConnections => _connections;

    /// <summary>
    /// Gets metrics for all providers (including disconnected ones).
    /// </summary>
    public IReadOnlyDictionary<string, ProviderMetrics> AllMetrics => _providerMetrics;

    /// <summary>
    /// Adds and connects a new provider.
    /// </summary>
    public async Task<bool> AddProviderAsync(DataSourceConfig config, CancellationToken ct = default)
    {
        if (config is null) throw new ArgumentNullException(nameof(config));
        if (string.IsNullOrWhiteSpace(config.Id))
            throw new ArgumentException("Provider config must have an Id", nameof(config));

        await _connectionLock.WaitAsync(ct);
        try
        {
            if (_connections.ContainsKey(config.Id))
            {
                _log.Warning("Provider {ProviderId} is already connected", config.Id);
                return false;
            }

            var client = CreateClient(config);
            if (client == null)
            {
                _log.Error("Failed to create client for provider {ProviderId} of type {ProviderType}",
                    config.Id, config.Provider);
                return false;
            }

            var connection = new ProviderConnection(config, client);
            _providerMetrics.TryAdd(config.Id, new ProviderMetrics(config.Id, config.Provider));

            try
            {
                _log.Information("Connecting to provider {ProviderId} ({ProviderType})", config.Id, config.Provider);
                await client.ConnectAsync(ct);
                connection.MarkConnected();
                _connections.TryAdd(config.Id, connection);
                UpdateMetrics(config.Id, m => m.RecordConnectionSuccess());
                _log.Information("Successfully connected to provider {ProviderId}", config.Id);
                return true;
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Failed to connect to provider {ProviderId}", config.Id);
                UpdateMetrics(config.Id, m => m.RecordConnectionFailure());
                await client.DisposeAsync();
                return false;
            }
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    /// <summary>
    /// Connects multiple providers simultaneously.
    /// </summary>
    public async Task<Dictionary<string, bool>> AddProvidersAsync(
        IEnumerable<DataSourceConfig> configs,
        CancellationToken ct = default)
    {
        var results = new Dictionary<string, bool>();
        var tasks = configs.Select(async config =>
        {
            var success = await AddProviderAsync(config, ct);
            return (config.Id, success);
        });

        foreach (var result in await Task.WhenAll(tasks))
        {
            results[result.Id] = result.success;
        }

        return results;
    }

    /// <summary>
    /// Removes and disconnects a provider.
    /// </summary>
    public async Task<bool> RemoveProviderAsync(string providerId, CancellationToken ct = default)
    {
        await _connectionLock.WaitAsync(ct);
        try
        {
            if (!_connections.TryRemove(providerId, out var connection))
            {
                _log.Warning("Provider {ProviderId} is not connected", providerId);
                return false;
            }

            try
            {
                await connection.Client.DisconnectAsync(ct);
                await connection.Client.DisposeAsync();
                connection.MarkDisconnected();
                _log.Information("Disconnected from provider {ProviderId}", providerId);
                return true;
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Error disconnecting from provider {ProviderId}", providerId);
                return false;
            }
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    /// <summary>
    /// Subscribes a symbol across all connected providers or specific ones.
    /// </summary>
    public Dictionary<string, int> SubscribeSymbol(
        SymbolConfig symbol,
        string[]? providerIds = null,
        bool subscribeTrades = true,
        bool subscribeDepth = true)
    {
        var results = new Dictionary<string, int>();
        var providers = providerIds?.Length > 0
            ? _connections.Where(c => providerIds.Contains(c.Key, StringComparer.OrdinalIgnoreCase))
            : _connections;

        foreach (var (id, connection) in providers)
        {
            try
            {
                if (subscribeTrades && symbol.SubscribeTrades)
                {
                    var tradeSubId = connection.Client.SubscribeTrades(symbol);
                    if (tradeSubId >= 0)
                    {
                        connection.AddSubscription(symbol.Symbol, tradeSubId, "trades", symbol);
                        results[$"{id}:trades"] = tradeSubId;
                        UpdateMetrics(id, m => m.IncrementActiveSubscriptions());
                    }
                }

                if (subscribeDepth && symbol.SubscribeDepth)
                {
                    var depthSubId = connection.Client.SubscribeMarketDepth(symbol);
                    if (depthSubId >= 0)
                    {
                        connection.AddSubscription(symbol.Symbol, depthSubId, "depth", symbol);
                        results[$"{id}:depth"] = depthSubId;
                        UpdateMetrics(id, m => m.IncrementActiveSubscriptions());
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Failed to subscribe {Symbol} on provider {ProviderId}", symbol.Symbol, id);
            }
        }

        return results;
    }

    /// <summary>
    /// Unsubscribes a symbol from specific providers.
    /// </summary>
    public void UnsubscribeSymbol(string symbol, string[]? providerIds = null)
    {
        var providers = providerIds?.Length > 0
            ? _connections.Where(c => providerIds.Contains(c.Key, StringComparer.OrdinalIgnoreCase))
            : _connections;

        foreach (var (id, connection) in providers)
        {
            try
            {
                foreach (var sub in connection.GetSubscriptions(symbol))
                {
                    if (sub.Kind == "trades")
                        connection.Client.UnsubscribeTrades(sub.SubscriptionId);
                    else if (sub.Kind == "depth")
                        connection.Client.UnsubscribeMarketDepth(sub.SubscriptionId);

                    connection.RemoveSubscription(sub.SubscriptionId);
                    UpdateMetrics(id, m => m.DecrementActiveSubscriptions());
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Failed to unsubscribe {Symbol} from provider {ProviderId}", symbol, id);
            }
        }
    }

    /// <summary>
    /// Transfers all subscriptions from one provider to another.
    /// Used during failover and recovery operations.
    /// </summary>
    /// <param name="fromProviderId">Source provider ID</param>
    /// <param name="toProviderId">Target provider ID</param>
    /// <param name="unsubscribeFromSource">Whether to unsubscribe from the source provider</param>
    /// <returns>Transfer result containing success status and counts</returns>
    public SubscriptionTransferResult TransferSubscriptions(
        string fromProviderId,
        string toProviderId,
        bool unsubscribeFromSource = false)
    {
        if (!_connections.TryGetValue(fromProviderId, out var sourceConnection))
        {
            _log.Warning("Cannot transfer subscriptions: source provider {ProviderId} not found", fromProviderId);
            return new SubscriptionTransferResult(false, 0, 0, "Source provider not found");
        }

        if (!_connections.TryGetValue(toProviderId, out var targetConnection))
        {
            _log.Warning("Cannot transfer subscriptions: target provider {ProviderId} not found", toProviderId);
            return new SubscriptionTransferResult(false, 0, 0, "Target provider not found");
        }

        var subscriptions = sourceConnection.GetAllSubscriptions().ToList();
        var transferred = 0;
        var failed = 0;

        // Group subscriptions by symbol config to avoid duplicate subscriptions
        var symbolGroups = subscriptions
            .GroupBy(s => (s.Config.Symbol, s.Config))
            .ToList();

        foreach (var group in symbolGroups)
        {
            var config = group.Key.Config;
            var hasTradesSub = group.Any(s => s.Kind == "trades");
            var hasDepthSub = group.Any(s => s.Kind == "depth");

            try
            {
                // Subscribe on target provider
                if (hasTradesSub && config.SubscribeTrades)
                {
                    var tradeSubId = targetConnection.Client.SubscribeTrades(config);
                    if (tradeSubId >= 0)
                    {
                        targetConnection.AddSubscription(config.Symbol, tradeSubId, "trades", config);
                        UpdateMetrics(toProviderId, m => m.IncrementActiveSubscriptions());
                        transferred++;
                    }
                    else
                    {
                        failed++;
                    }
                }

                if (hasDepthSub && config.SubscribeDepth)
                {
                    var depthSubId = targetConnection.Client.SubscribeMarketDepth(config);
                    if (depthSubId >= 0)
                    {
                        targetConnection.AddSubscription(config.Symbol, depthSubId, "depth", config);
                        UpdateMetrics(toProviderId, m => m.IncrementActiveSubscriptions());
                        transferred++;
                    }
                    else
                    {
                        failed++;
                    }
                }

                // Optionally unsubscribe from source
                if (unsubscribeFromSource)
                {
                    foreach (var sub in group)
                    {
                        try
                        {
                            if (sub.Kind == "trades")
                                sourceConnection.Client.UnsubscribeTrades(sub.SubscriptionId);
                            else if (sub.Kind == "depth")
                                sourceConnection.Client.UnsubscribeMarketDepth(sub.SubscriptionId);

                            sourceConnection.RemoveSubscription(sub.SubscriptionId);
                            UpdateMetrics(fromProviderId, m => m.DecrementActiveSubscriptions());
                        }
                        catch (Exception ex)
                        {
                            _log.Warning(ex, "Failed to unsubscribe {Symbol} ({Kind}) from source provider {ProviderId}",
                                sub.Symbol, sub.Kind, fromProviderId);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Failed to transfer subscription for {Symbol} from {From} to {To}",
                    config.Symbol, fromProviderId, toProviderId);
                failed++;
            }
        }

        _log.Information("Subscription transfer complete: {Transferred} transferred, {Failed} failed ({From} -> {To})",
            transferred, failed, fromProviderId, toProviderId);

        return new SubscriptionTransferResult(
            Success: failed == 0,
            TransferredCount: transferred,
            FailedCount: failed,
            Message: failed == 0
                ? "All subscriptions transferred successfully"
                : $"{failed} subscription(s) failed to transfer");
    }

    /// <summary>
    /// Gets the connection status for all providers.
    /// </summary>
    public Dictionary<string, ProviderConnectionStatus> GetConnectionStatus()
    {
        var status = new Dictionary<string, ProviderConnectionStatus>();

        foreach (var (id, connection) in _connections)
        {
            status[id] = new ProviderConnectionStatus(
                ProviderId: id,
                ProviderType: connection.Config.Provider,
                IsConnected: connection.IsConnected,
                ConnectedAt: connection.ConnectedAt,
                ActiveSubscriptions: connection.ActiveSubscriptionCount,
                LastHeartbeat: connection.LastHeartbeat
            );
        }

        return status;
    }

    /// <summary>
    /// Gets comparison metrics for all providers.
    /// </summary>
    public ProviderComparisonResult GetComparisonMetrics()
    {
        var metrics = _providerMetrics.Values
            .Select(m => m.GetSnapshot())
            .ToList();

        return new ProviderComparisonResult(
            Timestamp: DateTimeOffset.UtcNow,
            Providers: metrics,
            TotalProviders: _connections.Count,
            HealthyProviders: _connections.Count(c => c.Value.IsConnected)
        );
    }

    private IMarketDataClient? CreateClient(DataSourceConfig config)
    {
        return config.Provider switch
        {
            DataSourceKind.Alpaca when config.Alpaca != null =>
                new AlpacaMarketDataClient(_tradeCollector, _quoteCollector, config.Alpaca),
            // IB and Polygon clients would be instantiated similarly
            // For now, return null for unsupported providers
            _ => null
        };
    }

    private void UpdateMetrics(string providerId, Action<ProviderMetrics> action)
    {
        if (_providerMetrics.TryGetValue(providerId, out var metrics))
        {
            action(metrics);
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var (id, connection) in _connections)
        {
            try
            {
                await connection.Client.DisposeAsync();
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Error disposing provider {ProviderId}", id);
            }
        }

        _connections.Clear();
        _connectionLock.Dispose();
    }
}

/// <summary>
/// Represents a connection to a single provider.
/// </summary>
public sealed class ProviderConnection
{
    private readonly ConcurrentDictionary<int, SymbolSubscription> _subscriptions = new();
    private volatile bool _isConnected;
    private DateTimeOffset? _connectedAt;
    private DateTimeOffset? _lastHeartbeat;

    public ProviderConnection(DataSourceConfig config, IMarketDataClient client)
    {
        Config = config;
        Client = client;
    }

    public DataSourceConfig Config { get; }
    public IMarketDataClient Client { get; }
    public bool IsConnected => _isConnected;
    public DateTimeOffset? ConnectedAt => _connectedAt;
    public DateTimeOffset? LastHeartbeat => _lastHeartbeat;
    public int ActiveSubscriptionCount => _subscriptions.Count;

    public void MarkConnected()
    {
        _isConnected = true;
        _connectedAt = DateTimeOffset.UtcNow;
        _lastHeartbeat = DateTimeOffset.UtcNow;
    }

    public void MarkDisconnected()
    {
        _isConnected = false;
    }

    public void UpdateHeartbeat()
    {
        _lastHeartbeat = DateTimeOffset.UtcNow;
    }

    public void AddSubscription(string symbol, int subscriptionId, string kind, SymbolConfig config)
    {
        _subscriptions.TryAdd(subscriptionId, new SymbolSubscription(symbol, subscriptionId, kind, config));
    }

    public void RemoveSubscription(int subscriptionId)
    {
        _subscriptions.TryRemove(subscriptionId, out _);
    }

    public IEnumerable<SymbolSubscription> GetSubscriptions(string symbol)
    {
        return _subscriptions.Values.Where(s =>
            s.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Gets all active subscriptions for this connection.
    /// </summary>
    public IEnumerable<SymbolSubscription> GetAllSubscriptions()
    {
        return _subscriptions.Values.ToList();
    }
}

/// <summary>
/// Represents a symbol subscription on a provider.
/// </summary>
public readonly record struct SymbolSubscription(string Symbol, int SubscriptionId, string Kind, SymbolConfig Config);

/// <summary>
/// Connection status for a provider.
/// </summary>
public readonly record struct ProviderConnectionStatus(
    string ProviderId,
    DataSourceKind ProviderType,
    bool IsConnected,
    DateTimeOffset? ConnectedAt,
    int ActiveSubscriptions,
    DateTimeOffset? LastHeartbeat
);

/// <summary>
/// Metrics tracked for each provider.
/// </summary>
public sealed class ProviderMetrics
{
    private long _tradesReceived;
    private long _depthUpdatesReceived;
    private long _quotesReceived;
    private long _connectionAttempts;
    private long _connectionFailures;
    private long _messagesDropped;
    private long _activeSubscriptions;
    private long _latencySampleCount;
    private double _totalLatencyMs;
    private double _minLatencyMs = double.MaxValue;
    private double _maxLatencyMs;

    public ProviderMetrics(string providerId, DataSourceKind providerType)
    {
        ProviderId = providerId;
        ProviderType = providerType;
    }

    public string ProviderId { get; }
    public DataSourceKind ProviderType { get; }

    public void RecordTrade() => Interlocked.Increment(ref _tradesReceived);
    public void RecordDepthUpdate() => Interlocked.Increment(ref _depthUpdatesReceived);
    public void RecordQuote() => Interlocked.Increment(ref _quotesReceived);
    public void RecordConnectionSuccess() => Interlocked.Increment(ref _connectionAttempts);
    public void RecordConnectionFailure()
    {
        Interlocked.Increment(ref _connectionAttempts);
        Interlocked.Increment(ref _connectionFailures);
    }
    public void RecordDroppedMessage() => Interlocked.Increment(ref _messagesDropped);
    public void IncrementActiveSubscriptions() => Interlocked.Increment(ref _activeSubscriptions);
    public void DecrementActiveSubscriptions() => Interlocked.Decrement(ref _activeSubscriptions);

    public void RecordLatency(double latencyMs)
    {
        Interlocked.Increment(ref _latencySampleCount);
        // Use lock-free approach for double accumulation
        double initial, newValue;
        do
        {
            initial = _totalLatencyMs;
            newValue = initial + latencyMs;
        } while (Interlocked.CompareExchange(ref _totalLatencyMs, newValue, initial) != initial);

        // Update min/max
        SpinWait.SpinUntil(() =>
        {
            var currentMin = _minLatencyMs;
            if (latencyMs >= currentMin) return true;
            return Interlocked.CompareExchange(ref _minLatencyMs, latencyMs, currentMin) == currentMin;
        });

        SpinWait.SpinUntil(() =>
        {
            var currentMax = _maxLatencyMs;
            if (latencyMs <= currentMax) return true;
            return Interlocked.CompareExchange(ref _maxLatencyMs, latencyMs, currentMax) == currentMax;
        });
    }

    public ProviderMetricsSnapshot GetSnapshot()
    {
        var samples = Interlocked.Read(ref _latencySampleCount);
        var avgLatency = samples > 0 ? _totalLatencyMs / samples : 0;

        return new ProviderMetricsSnapshot(
            ProviderId: ProviderId,
            ProviderType: ProviderType,
            TradesReceived: Interlocked.Read(ref _tradesReceived),
            DepthUpdatesReceived: Interlocked.Read(ref _depthUpdatesReceived),
            QuotesReceived: Interlocked.Read(ref _quotesReceived),
            ConnectionAttempts: Interlocked.Read(ref _connectionAttempts),
            ConnectionFailures: Interlocked.Read(ref _connectionFailures),
            MessagesDropped: Interlocked.Read(ref _messagesDropped),
            ActiveSubscriptions: Interlocked.Read(ref _activeSubscriptions),
            AverageLatencyMs: avgLatency,
            MinLatencyMs: _minLatencyMs == double.MaxValue ? 0 : _minLatencyMs,
            MaxLatencyMs: _maxLatencyMs,
            LatencySampleCount: samples,
            Timestamp: DateTimeOffset.UtcNow
        );
    }
}

/// <summary>
/// Immutable snapshot of provider metrics.
/// </summary>
public readonly record struct ProviderMetricsSnapshot(
    string ProviderId,
    DataSourceKind ProviderType,
    long TradesReceived,
    long DepthUpdatesReceived,
    long QuotesReceived,
    long ConnectionAttempts,
    long ConnectionFailures,
    long MessagesDropped,
    long ActiveSubscriptions,
    double AverageLatencyMs,
    double MinLatencyMs,
    double MaxLatencyMs,
    long LatencySampleCount,
    DateTimeOffset Timestamp
)
{
    /// <summary>
    /// Calculates the connection success rate.
    /// </summary>
    public double ConnectionSuccessRate =>
        ConnectionAttempts > 0
            ? (double)(ConnectionAttempts - ConnectionFailures) / ConnectionAttempts * 100
            : 100;

    /// <summary>
    /// Calculates the data quality score (0-100).
    /// Based on connection stability, latency, and drop rate.
    /// </summary>
    public double DataQualityScore
    {
        get
        {
            var connectionScore = ConnectionSuccessRate;
            var totalMessages = TradesReceived + DepthUpdatesReceived + QuotesReceived;
            var dropScore = totalMessages > 0
                ? Math.Max(0, 100 - (double)MessagesDropped / totalMessages * 100)
                : 100;
            var latencyScore = AverageLatencyMs > 0
                ? Math.Max(0, 100 - Math.Min(AverageLatencyMs, 100))
                : 100;

            return (connectionScore * 0.4 + dropScore * 0.4 + latencyScore * 0.2);
        }
    }
}

/// <summary>
/// Result of comparing metrics across providers.
/// </summary>
public readonly record struct ProviderComparisonResult(
    DateTimeOffset Timestamp,
    IReadOnlyList<ProviderMetricsSnapshot> Providers,
    int TotalProviders,
    int HealthyProviders
);

/// <summary>
/// Result of a subscription transfer operation.
/// </summary>
public readonly record struct SubscriptionTransferResult(
    bool Success,
    int TransferredCount,
    int FailedCount,
    string Message
);
