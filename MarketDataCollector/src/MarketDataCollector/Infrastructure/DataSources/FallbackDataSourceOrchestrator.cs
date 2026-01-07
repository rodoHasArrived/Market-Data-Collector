using System.Collections.Concurrent;
using MarketDataCollector.Application.Logging;
using MarketDataCollector.Domain.Models;
using MarketDataCollector.Infrastructure.Providers.Backfill;
using Serilog;

namespace MarketDataCollector.Infrastructure.DataSources;

/// <summary>
/// Orchestrates requests across multiple data sources with automatic failover,
/// cooldown periods, and intelligent source selection.
/// </summary>
public interface IFallbackDataSourceOrchestrator
{
    /// <summary>
    /// Gets historical bars with automatic fallback across providers.
    /// </summary>
    Task<IReadOnlyList<HistoricalBar>> GetHistoricalBarsWithFallbackAsync(
        string symbol,
        DateOnly? from = null,
        DateOnly? to = null,
        CancellationToken ct = default);

    /// <summary>
    /// Gets adjusted historical bars with automatic fallback.
    /// </summary>
    Task<IReadOnlyList<AdjustedHistoricalBar>> GetAdjustedBarsWithFallbackAsync(
        string symbol,
        DateOnly? from = null,
        DateOnly? to = null,
        CancellationToken ct = default);

    /// <summary>
    /// Gets intraday bars with automatic fallback.
    /// </summary>
    Task<IReadOnlyList<IntradayBar>> GetIntradayBarsWithFallbackAsync(
        string symbol,
        string interval,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        CancellationToken ct = default);

    /// <summary>
    /// Gets an available real-time source with automatic failover monitoring.
    /// </summary>
    Task<IRealtimeDataSource?> GetRealtimeSourceWithFailoverAsync(
        string symbol,
        DataSourceCapabilities required = DataSourceCapabilities.None,
        CancellationToken ct = default);

    /// <summary>
    /// Gets the current failover status.
    /// </summary>
    FallbackOrchestratorStatus GetStatus();
}

/// <summary>
/// Default implementation of IFallbackDataSourceOrchestrator.
/// </summary>
public sealed class FallbackDataSourceOrchestrator : IFallbackDataSourceOrchestrator
{
    private readonly IDataSourceManager _manager;
    private readonly FallbackOptions _options;
    private readonly ILogger _log;
    private readonly ConcurrentDictionary<string, DateTimeOffset> _sourceCooldowns = new();
    private readonly ConcurrentDictionary<string, int> _failureCounts = new();
    private readonly ConcurrentDictionary<string, FallbackAttempt> _recentAttempts = new();

    public FallbackDataSourceOrchestrator(
        IDataSourceManager manager,
        FallbackOptions? options = null,
        ILogger? logger = null)
    {
        _manager = manager ?? throw new ArgumentNullException(nameof(manager));
        _options = options ?? new FallbackOptions();
        _log = logger ?? LoggingSetup.ForContext<FallbackDataSourceOrchestrator>();
    }

    #region Historical Data with Fallback

    /// <inheritdoc />
    public async Task<IReadOnlyList<HistoricalBar>> GetHistoricalBarsWithFallbackAsync(
        string symbol,
        DateOnly? from = null,
        DateOnly? to = null,
        CancellationToken ct = default)
    {
        var sources = GetAvailableHistoricalSources(symbol, from, to);
        var errors = new List<(string Source, Exception Error)>();

        foreach (var source in sources)
        {
            try
            {
                _log.Debug("Attempting {Source} for {Symbol} historical data", source.Id, symbol);

                var bars = await source.GetDailyBarsAsync(symbol, from, to, ct).ConfigureAwait(false);

                if (bars.Count > 0)
                {
                    _log.Information("Retrieved {Count} bars from {Source} for {Symbol}",
                        bars.Count, source.Id, symbol);
                    RecordSuccess(source.Id);
                    RecordAttempt(symbol, source.Id, true);
                    return bars;
                }

                _log.Warning("{Source} returned no data for {Symbol}", source.Id, symbol);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                errors.Add((source.Id, ex));
                _log.Warning(ex, "{Source} failed for {Symbol}, trying next source", source.Id, symbol);
                RecordFailure(source.Id);
                RecordAttempt(symbol, source.Id, false, ex.Message);

                if (_options.CooldownOnError)
                    SetCooldown(source.Id);
            }
        }

        // All sources failed
        if (errors.Count > 0)
        {
            var aggregate = new AggregateException(
                $"All {errors.Count} data sources failed for {symbol}",
                errors.Select(e => e.Error));

            _log.Error(aggregate, "Historical data request failed for {Symbol} across all sources", symbol);
            throw aggregate;
        }

        _log.Warning("No available data sources for {Symbol}", symbol);
        return Array.Empty<HistoricalBar>();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AdjustedHistoricalBar>> GetAdjustedBarsWithFallbackAsync(
        string symbol,
        DateOnly? from = null,
        DateOnly? to = null,
        CancellationToken ct = default)
    {
        var sources = GetAvailableHistoricalSources(symbol, from, to)
            .Where(s => s.Capabilities.HasFlag(DataSourceCapabilities.HistoricalAdjustedPrices));

        var errors = new List<(string Source, Exception Error)>();

        foreach (var source in sources)
        {
            try
            {
                _log.Debug("Attempting {Source} for {Symbol} adjusted bars", source.Id, symbol);

                var bars = await source.GetAdjustedDailyBarsAsync(symbol, from, to, ct).ConfigureAwait(false);

                if (bars.Count > 0)
                {
                    _log.Information("Retrieved {Count} adjusted bars from {Source} for {Symbol}",
                        bars.Count, source.Id, symbol);
                    RecordSuccess(source.Id);
                    return bars;
                }

                _log.Warning("{Source} returned no adjusted data for {Symbol}", source.Id, symbol);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                errors.Add((source.Id, ex));
                _log.Warning(ex, "{Source} failed for adjusted bars {Symbol}", source.Id, symbol);
                RecordFailure(source.Id);

                if (_options.CooldownOnError)
                    SetCooldown(source.Id);
            }
        }

        if (errors.Count > 0)
        {
            throw new AggregateException(
                $"All data sources failed for adjusted bars {symbol}",
                errors.Select(e => e.Error));
        }

        return Array.Empty<AdjustedHistoricalBar>();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<IntradayBar>> GetIntradayBarsWithFallbackAsync(
        string symbol,
        string interval,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        CancellationToken ct = default)
    {
        var sources = _manager.HistoricalSources
            .Where(s => s.SupportsIntraday)
            .Where(s => s.SupportedBarIntervals.Contains(interval, StringComparer.OrdinalIgnoreCase))
            .Where(s => s.Health.IsHealthy)
            .Where(s => !IsInCooldown(s.Id))
            .OrderBy(s => s.Priority);

        var errors = new List<(string Source, Exception Error)>();

        foreach (var source in sources)
        {
            try
            {
                _log.Debug("Attempting {Source} for {Symbol} intraday bars ({Interval})",
                    source.Id, symbol, interval);

                var bars = await source.GetIntradayBarsAsync(symbol, interval, from, to, ct).ConfigureAwait(false);

                if (bars.Count > 0)
                {
                    _log.Information("Retrieved {Count} intraday bars from {Source} for {Symbol}",
                        bars.Count, source.Id, symbol);
                    RecordSuccess(source.Id);
                    return bars;
                }

                _log.Warning("{Source} returned no intraday data for {Symbol}", source.Id, symbol);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                errors.Add((source.Id, ex));
                _log.Warning(ex, "{Source} failed for intraday bars {Symbol}", source.Id, symbol);
                RecordFailure(source.Id);

                if (_options.CooldownOnError)
                    SetCooldown(source.Id);
            }
        }

        if (errors.Count > 0)
        {
            throw new AggregateException(
                $"All data sources failed for intraday bars {symbol}",
                errors.Select(e => e.Error));
        }

        return Array.Empty<IntradayBar>();
    }

    #endregion

    #region Real-time with Failover

    /// <inheritdoc />
    public Task<IRealtimeDataSource?> GetRealtimeSourceWithFailoverAsync(
        string symbol,
        DataSourceCapabilities required = DataSourceCapabilities.None,
        CancellationToken ct = default)
    {
        var source = _manager.RealtimeSources
            .Where(s => s.Status == DataSourceStatus.Connected)
            .Where(s => s.Health.IsHealthy)
            .Where(s => s.RateLimitState.CanMakeRequest)
            .Where(s => !IsInCooldown(s.Id))
            .Where(s => required == DataSourceCapabilities.None || s.Capabilities.HasFlag(required))
            .OrderBy(s => s.Priority)
            .ThenByDescending(s => s.Health.Score)
            .FirstOrDefault();

        if (source != null)
        {
            // Monitor for failover
            source.HealthChanges
                .Where(h => !h.CurrentHealth.IsHealthy)
                .Take(1)
                .Subscribe(_ =>
                {
                    _log.Warning("{Source} became unhealthy, failover may be needed for {Symbol}",
                        source.Id, symbol);
                    SetCooldown(source.Id);
                });
        }

        return Task.FromResult(source);
    }

    #endregion

    #region Status

    /// <inheritdoc />
    public FallbackOrchestratorStatus GetStatus()
    {
        var cooldownSources = _sourceCooldowns
            .Where(kvp => kvp.Value > DateTimeOffset.UtcNow)
            .Select(kvp => new SourceCooldownInfo(
                kvp.Key,
                kvp.Value,
                kvp.Value - DateTimeOffset.UtcNow
            ))
            .ToList();

        var failureSummary = _failureCounts.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value
        );

        var recentAttempts = _recentAttempts.Values
            .OrderByDescending(a => a.Timestamp)
            .Take(20)
            .ToList();

        return new FallbackOrchestratorStatus(
            cooldownSources,
            failureSummary,
            recentAttempts,
            _options
        );
    }

    #endregion

    #region Helpers

    private IEnumerable<IHistoricalDataSource> GetAvailableHistoricalSources(
        string symbol,
        DateOnly? from,
        DateOnly? to)
    {
        return _manager.HistoricalSources
            .Where(s => s.Health.IsHealthy)
            .Where(s => s.RateLimitState.CanMakeRequest)
            .Where(s => !IsInCooldown(s.Id))
            .OrderBy(s => s.Priority)
            .ThenByDescending(s => s.Health.Score);
    }

    private bool IsInCooldown(string sourceId)
    {
        if (_sourceCooldowns.TryGetValue(sourceId, out var cooldownUntil))
        {
            if (cooldownUntil > DateTimeOffset.UtcNow)
                return true;

            // Cooldown expired, remove it
            _sourceCooldowns.TryRemove(sourceId, out _);
        }
        return false;
    }

    private void SetCooldown(string sourceId)
    {
        var cooldownUntil = DateTimeOffset.UtcNow.Add(_options.EffectiveCooldownDuration);
        _sourceCooldowns[sourceId] = cooldownUntil;
        _log.Debug("Set cooldown for {Source} until {Until}", sourceId, cooldownUntil);
    }

    private void RecordSuccess(string sourceId)
    {
        // Reset failure count on success
        _failureCounts.TryRemove(sourceId, out _);

        // Remove from cooldown early on success
        _sourceCooldowns.TryRemove(sourceId, out _);
    }

    private void RecordFailure(string sourceId)
    {
        _failureCounts.AddOrUpdate(sourceId, 1, (_, count) => count + 1);
    }

    private void RecordAttempt(string symbol, string sourceId, bool success, string? error = null)
    {
        var key = $"{symbol}_{sourceId}_{DateTimeOffset.UtcNow.Ticks}";
        _recentAttempts[key] = new FallbackAttempt(
            symbol,
            sourceId,
            DateTimeOffset.UtcNow,
            success,
            error
        );

        // Keep only recent attempts
        if (_recentAttempts.Count > 100)
        {
            var oldestKey = _recentAttempts
                .OrderBy(kvp => kvp.Value.Timestamp)
                .First().Key;
            _recentAttempts.TryRemove(oldestKey, out _);
        }
    }

    #endregion
}

#region Options and Status Types

/// <summary>
/// Configuration options for fallback orchestration.
/// </summary>
public sealed record FallbackOptions(
    bool Enabled = true,
    FallbackStrategy Strategy = FallbackStrategy.Priority,
    int MaxFailoverAttempts = 3,
    TimeSpan? CooldownDuration = null,
    bool CooldownOnError = true,
    bool CooldownOnEmptyResult = false
)
{
    public TimeSpan EffectiveCooldownDuration { get; } = CooldownDuration ?? TimeSpan.FromMinutes(1);
}

/// <summary>
/// Fallback strategy for source selection.
/// </summary>
public enum FallbackStrategy
{
    /// <summary>Try sources in priority order.</summary>
    Priority,

    /// <summary>Try sources with best health scores first.</summary>
    HealthScore,

    /// <summary>Round-robin across healthy sources.</summary>
    RoundRobin,

    /// <summary>Random selection from healthy sources.</summary>
    Random
}

/// <summary>
/// Current status of the fallback orchestrator.
/// </summary>
public sealed record FallbackOrchestratorStatus(
    IReadOnlyList<SourceCooldownInfo> SourcesInCooldown,
    IReadOnlyDictionary<string, int> FailureCounts,
    IReadOnlyList<FallbackAttempt> RecentAttempts,
    FallbackOptions Options
);

/// <summary>
/// Information about a source currently in cooldown.
/// </summary>
public sealed record SourceCooldownInfo(
    string SourceId,
    DateTimeOffset CooldownUntil,
    TimeSpan RemainingTime
);

/// <summary>
/// Record of a fallback attempt.
/// </summary>
public sealed record FallbackAttempt(
    string Symbol,
    string SourceId,
    DateTimeOffset Timestamp,
    bool Success,
    string? ErrorMessage = null
);

#endregion
