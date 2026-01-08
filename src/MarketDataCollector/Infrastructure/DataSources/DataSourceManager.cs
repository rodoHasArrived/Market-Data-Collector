using System.Collections.Concurrent;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using MarketDataCollector.Application.Logging;
using Serilog;

namespace MarketDataCollector.Infrastructure.DataSources;

/// <summary>
/// Central orchestrator for all data sources with health monitoring,
/// failover, and unified access patterns.
/// </summary>
public interface IDataSourceManager
{
    #region Discovery

    /// <summary>
    /// All registered data sources.
    /// </summary>
    IReadOnlyList<IDataSource> AllSources { get; }

    /// <summary>
    /// All registered real-time data sources.
    /// </summary>
    IReadOnlyList<IRealtimeDataSource> RealtimeSources { get; }

    /// <summary>
    /// All registered historical data sources.
    /// </summary>
    IReadOnlyList<IHistoricalDataSource> HistoricalSources { get; }

    #endregion

    #region Retrieval

    /// <summary>
    /// Gets a data source by ID.
    /// </summary>
    IDataSource? GetSource(string id);

    /// <summary>
    /// Gets a data source by ID and type.
    /// </summary>
    T? GetSource<T>(string id) where T : class, IDataSource;

    #endregion

    #region Capability Queries

    /// <summary>
    /// Gets all sources with the specified capability.
    /// </summary>
    IEnumerable<IDataSource> GetSourcesWithCapability(DataSourceCapabilities capability);

    /// <summary>
    /// Gets all sources that support the specified market.
    /// </summary>
    IEnumerable<IDataSource> GetSourcesForMarket(string market);

    /// <summary>
    /// Gets all sources that support the specified asset class.
    /// </summary>
    IEnumerable<IDataSource> GetSourcesForAssetClass(AssetClass assetClass);

    /// <summary>
    /// Gets enabled sources only.
    /// </summary>
    IEnumerable<IDataSource> GetEnabledSources();

    #endregion

    #region Smart Selection

    /// <summary>
    /// Gets the best available real-time source for a symbol.
    /// </summary>
    IRealtimeDataSource? GetBestRealtimeSource(string symbol, DataSourceCapabilities required = DataSourceCapabilities.None);

    /// <summary>
    /// Gets the best available historical source for a symbol and date range.
    /// </summary>
    IHistoricalDataSource? GetBestHistoricalSource(string symbol, DateOnly? from = null, DateOnly? to = null);

    /// <summary>
    /// Gets all available sources for a symbol, ordered by priority.
    /// </summary>
    IEnumerable<IDataSource> GetAvailableSourcesForSymbol(string symbol);

    #endregion

    #region Health & Status

    /// <summary>
    /// Gets aggregated health across all data sources.
    /// </summary>
    DataSourceManagerHealth GetAggregatedHealth();

    /// <summary>
    /// Observable stream of health changes from any source.
    /// </summary>
    IObservable<DataSourceHealthChanged> HealthChanges { get; }

    /// <summary>
    /// Gets status summary for all sources.
    /// </summary>
    IReadOnlyDictionary<string, DataSourceStatusSummary> GetAllSourceStatus();

    #endregion

    #region Lifecycle

    /// <summary>
    /// Initializes all enabled data sources.
    /// </summary>
    Task InitializeAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Validates credentials for all sources.
    /// </summary>
    Task<DataSourceValidationResult> ValidateAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Tests connectivity for all enabled sources.
    /// </summary>
    Task<DataSourceConnectivityResult> TestConnectivityAsync(CancellationToken ct = default);

    /// <summary>
    /// Disposes all data sources.
    /// </summary>
    Task DisposeAllAsync();

    #endregion
}

/// <summary>
/// Default implementation of IDataSourceManager.
/// </summary>
public sealed class DataSourceManager : IDataSourceManager, IAsyncDisposable
{
    private readonly IReadOnlyList<IDataSource> _sources;
    private readonly IReadOnlyList<IRealtimeDataSource> _realtimeSources;
    private readonly IReadOnlyList<IHistoricalDataSource> _historicalSources;
    private readonly ConcurrentDictionary<string, IDataSource> _sourceById;
    private readonly Subject<DataSourceHealthChanged> _healthChanges = new();
    private readonly ILogger _log;
    private readonly IDisposable _healthSubscription;
    private bool _disposed;

    #region Properties

    /// <inheritdoc />
    public IReadOnlyList<IDataSource> AllSources => _sources;

    /// <inheritdoc />
    public IReadOnlyList<IRealtimeDataSource> RealtimeSources => _realtimeSources;

    /// <inheritdoc />
    public IReadOnlyList<IHistoricalDataSource> HistoricalSources => _historicalSources;

    /// <inheritdoc />
    public IObservable<DataSourceHealthChanged> HealthChanges => _healthChanges;

    #endregion

    #region Constructor

    /// <summary>
    /// Creates a new DataSourceManager with the provided sources.
    /// </summary>
    public DataSourceManager(
        IEnumerable<IDataSource> sources,
        ILogger? logger = null)
    {
        _log = logger ?? LoggingSetup.ForContext<DataSourceManager>();

        // Sort by priority and store
        _sources = sources
            .OrderBy(GetPriority)
            .ToList();

        _sourceById = new ConcurrentDictionary<string, IDataSource>(
            _sources.ToDictionary(s => s.Id, s => s, StringComparer.OrdinalIgnoreCase));

        _realtimeSources = _sources
            .OfType<IRealtimeDataSource>()
            .ToList();

        _historicalSources = _sources
            .OfType<IHistoricalDataSource>()
            .ToList();

        // Subscribe to health changes from all sources
        _healthSubscription = _sources
            .Select(s => s.HealthChanges)
            .Merge()
            .Subscribe(change => _healthChanges.OnNext(change));

        _log.Information("DataSourceManager initialized with {Total} sources ({Realtime} realtime, {Historical} historical)",
            _sources.Count, _realtimeSources.Count, _historicalSources.Count);
    }

    #endregion

    #region Retrieval

    /// <inheritdoc />
    public IDataSource? GetSource(string id)
    {
        _sourceById.TryGetValue(id, out var source);
        return source;
    }

    /// <inheritdoc />
    public T? GetSource<T>(string id) where T : class, IDataSource
    {
        return GetSource(id) as T;
    }

    #endregion

    #region Capability Queries

    /// <inheritdoc />
    public IEnumerable<IDataSource> GetSourcesWithCapability(DataSourceCapabilities capability)
    {
        return _sources.Where(s => s.Capabilities.HasFlag(capability));
    }

    /// <inheritdoc />
    public IEnumerable<IDataSource> GetSourcesForMarket(string market)
    {
        return _sources.Where(s => s.SupportedMarkets.Contains(market, StringComparer.OrdinalIgnoreCase));
    }

    /// <inheritdoc />
    public IEnumerable<IDataSource> GetSourcesForAssetClass(AssetClass assetClass)
    {
        return _sources.Where(s => s.SupportedAssetClasses.Contains(assetClass));
    }

    /// <inheritdoc />
    public IEnumerable<IDataSource> GetEnabledSources()
    {
        return _sources.Where(s => s.Status != DataSourceStatus.Disabled);
    }

    #endregion

    #region Smart Selection

    /// <inheritdoc />
    public IRealtimeDataSource? GetBestRealtimeSource(
        string symbol,
        DataSourceCapabilities required = DataSourceCapabilities.None)
    {
        return _realtimeSources
            .Where(s => s.Status == DataSourceStatus.Connected)
            .Where(s => s.Health.IsHealthy)
            .Where(s => s.RateLimitState.CanMakeRequest)
            .Where(s => required == DataSourceCapabilities.None || s.Capabilities.HasFlag(required))
            .Where(s => SupportsSymbol(s, symbol))
            .OrderBy(GetPriority)
            .ThenByDescending(s => s.Health.Score)
            .FirstOrDefault();
    }

    /// <inheritdoc />
    public IHistoricalDataSource? GetBestHistoricalSource(
        string symbol,
        DateOnly? from = null,
        DateOnly? to = null)
    {
        return _historicalSources
            .Where(s => s.Health.IsHealthy)
            .Where(s => s.RateLimitState.CanMakeRequest)
            .Where(s => SupportsSymbol(s, symbol))
            .Where(s => CoversDateRange(s, from, to))
            .OrderBy(GetPriority)
            .ThenByDescending(s => s.Health.Score)
            .FirstOrDefault();
    }

    /// <inheritdoc />
    public IEnumerable<IDataSource> GetAvailableSourcesForSymbol(string symbol)
    {
        return _sources
            .Where(s => s.Status is DataSourceStatus.Connected or DataSourceStatus.Disconnected)
            .Where(s => s.Health.IsHealthy)
            .Where(s => SupportsSymbol(s, symbol))
            .OrderBy(GetPriority)
            .ThenByDescending(s => s.Health.Score);
    }

    #endregion

    #region Health & Status

    /// <inheritdoc />
    public DataSourceManagerHealth GetAggregatedHealth()
    {
        var enabledSources = GetEnabledSources().ToList();
        if (enabledSources.Count == 0)
        {
            return new DataSourceManagerHealth(
                IsHealthy: false,
                OverallScore: 0,
                HealthySources: 0,
                UnhealthySources: 0,
                TotalSources: 0,
                Message: "No data sources configured"
            );
        }

        var healthySources = enabledSources.Count(s => s.Health.IsHealthy);
        var unhealthySources = enabledSources.Count - healthySources;
        var overallScore = enabledSources.Average(s => s.Health.Score);
        var isHealthy = healthySources > 0;

        string? message = null;
        if (unhealthySources > 0)
        {
            var unhealthyIds = enabledSources
                .Where(s => !s.Health.IsHealthy)
                .Select(s => s.Id);
            message = $"Unhealthy sources: {string.Join(", ", unhealthyIds)}";
        }

        return new DataSourceManagerHealth(
            IsHealthy: isHealthy,
            OverallScore: overallScore,
            HealthySources: healthySources,
            UnhealthySources: unhealthySources,
            TotalSources: enabledSources.Count,
            Message: message
        );
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, DataSourceStatusSummary> GetAllSourceStatus()
    {
        return _sources.ToDictionary(
            s => s.Id,
            s => new DataSourceStatusSummary(
                s.Id,
                s.DisplayName,
                s.Type,
                s.Category,
                s.Status,
                s.Health,
                s.RateLimitState,
                s.Priority
            )
        );
    }

    #endregion

    #region Lifecycle

    /// <inheritdoc />
    public async Task InitializeAllAsync(CancellationToken ct = default)
    {
        _log.Information("Initializing {Count} data sources...", _sources.Count);

        var initTasks = _sources
            .Where(s => s.Status != DataSourceStatus.Disabled)
            .Select(async source =>
            {
                try
                {
                    await source.InitializeAsync(ct).ConfigureAwait(false);
                    return (source.Id, Success: true, Error: (Exception?)null);
                }
                catch (Exception ex)
                {
                    _log.Warning(ex, "Failed to initialize {Source}", source.Id);
                    return (source.Id, Success: false, Error: ex);
                }
            });

        var results = await Task.WhenAll(initTasks).ConfigureAwait(false);

        var successCount = results.Count(r => r.Success);
        var failCount = results.Count(r => !r.Success);

        _log.Information("Data source initialization complete: {Success} succeeded, {Failed} failed",
            successCount, failCount);
    }

    /// <inheritdoc />
    public async Task<DataSourceValidationResult> ValidateAllAsync(CancellationToken ct = default)
    {
        _log.Debug("Validating credentials for all data sources...");

        var validationTasks = _sources.Select(async source =>
        {
            try
            {
                var isValid = await source.ValidateCredentialsAsync(ct).ConfigureAwait(false);
                return new DataSourceValidationEntry(source.Id, isValid, null);
            }
            catch (Exception ex)
            {
                return new DataSourceValidationEntry(source.Id, false, ex.Message);
            }
        });

        var entries = await Task.WhenAll(validationTasks).ConfigureAwait(false);

        return new DataSourceValidationResult(
            entries.All(e => e.IsValid),
            entries.ToList()
        );
    }

    /// <inheritdoc />
    public async Task<DataSourceConnectivityResult> TestConnectivityAsync(CancellationToken ct = default)
    {
        _log.Debug("Testing connectivity for all data sources...");

        var testTasks = _sources
            .Where(s => s.Status != DataSourceStatus.Disabled)
            .Select(async source =>
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                try
                {
                    var isConnected = await source.TestConnectivityAsync(ct).ConfigureAwait(false);
                    sw.Stop();
                    return new DataSourceConnectivityEntry(source.Id, isConnected, sw.Elapsed, null);
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    return new DataSourceConnectivityEntry(source.Id, false, sw.Elapsed, ex.Message);
                }
            });

        var entries = await Task.WhenAll(testTasks).ConfigureAwait(false);

        return new DataSourceConnectivityResult(
            entries.All(e => e.IsConnected),
            entries.ToList()
        );
    }

    /// <inheritdoc />
    public async Task DisposeAllAsync()
    {
        _log.Information("Disposing all data sources...");

        var disposeTasks = _sources.Select(async source =>
        {
            try
            {
                await source.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "Error disposing {Source}", source.Id);
            }
        });

        await Task.WhenAll(disposeTasks).ConfigureAwait(false);
    }

    #endregion

    #region Helpers

    private static int GetPriority(IDataSource source)
    {
        // Get priority from attribute if available
        var attr = source.GetType().GetDataSourceAttribute();
        if (attr != null)
            return attr.Priority;

        return source.Priority;
    }

    private static bool SupportsSymbol(IDataSource source, string symbol)
    {
        // For now, assume all symbols are supported if markets overlap
        // In a real implementation, this would check symbol format and exchange
        return true;
    }

    private static bool CoversDateRange(IHistoricalDataSource source, DateOnly? from, DateOnly? to)
    {
        var capInfo = source.CapabilityInfo;

        // Check if the source covers the requested date range
        if (from.HasValue && capInfo.MinHistoricalDate.HasValue)
        {
            if (from.Value < capInfo.MinHistoricalDate.Value)
                return false;
        }

        if (from.HasValue && capInfo.MaxHistoricalLookback.HasValue)
        {
            var earliestSupported = DateOnly.FromDateTime(DateTime.UtcNow.Subtract(capInfo.MaxHistoricalLookback.Value));
            if (from.Value < earliestSupported)
                return false;
        }

        return true;
    }

    #endregion

    #region IAsyncDisposable

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        _healthSubscription.Dispose();
        _healthChanges.OnCompleted();
        _healthChanges.Dispose();

        await DisposeAllAsync().ConfigureAwait(false);
    }

    #endregion
}

#region Supporting Types

/// <summary>
/// Aggregated health status for the data source manager.
/// </summary>
public sealed record DataSourceManagerHealth(
    bool IsHealthy,
    double OverallScore,
    int HealthySources,
    int UnhealthySources,
    int TotalSources,
    string? Message = null
);

/// <summary>
/// Status summary for a data source.
/// </summary>
public sealed record DataSourceStatusSummary(
    string Id,
    string DisplayName,
    DataSourceType Type,
    DataSourceCategory Category,
    DataSourceStatus Status,
    DataSourceHealth Health,
    RateLimitState RateLimitState,
    int Priority
);

/// <summary>
/// Result of validating all data sources.
/// </summary>
public sealed record DataSourceValidationResult(
    bool AllValid,
    IReadOnlyList<DataSourceValidationEntry> Entries
);

/// <summary>
/// Validation result for a single data source.
/// </summary>
public sealed record DataSourceValidationEntry(
    string SourceId,
    bool IsValid,
    string? ErrorMessage
);

/// <summary>
/// Result of connectivity testing for all data sources.
/// </summary>
public sealed record DataSourceConnectivityResult(
    bool AllConnected,
    IReadOnlyList<DataSourceConnectivityEntry> Entries
);

/// <summary>
/// Connectivity test result for a single data source.
/// </summary>
public sealed record DataSourceConnectivityEntry(
    string SourceId,
    bool IsConnected,
    TimeSpan ResponseTime,
    string? ErrorMessage
);

#endregion
