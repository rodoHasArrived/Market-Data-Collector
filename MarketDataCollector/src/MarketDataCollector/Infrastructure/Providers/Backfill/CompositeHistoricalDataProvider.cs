using System.Collections.Concurrent;
using MarketDataCollector.Application.Logging;
using MarketDataCollector.Domain.Models;
using MarketDataCollector.Infrastructure.Providers.Backfill.SymbolResolution;
using Serilog;

namespace MarketDataCollector.Infrastructure.Providers.Backfill;

/// <summary>
/// Composite provider that chains multiple data providers with automatic failover.
/// Supports symbol resolution, provider health tracking, and cross-provider validation.
/// </summary>
public sealed class CompositeHistoricalDataProvider : IHistoricalDataProviderV2, IDisposable
{
    private readonly List<IHistoricalDataProvider> _providers;
    private readonly ISymbolResolver? _symbolResolver;
    private readonly ConcurrentDictionary<string, ProviderHealthStatus> _healthStatus = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _providerFailures = new();
    private readonly TimeSpan _failureBackoffDuration;
    private readonly bool _enableCrossValidation;
    private readonly ILogger _log;
    private bool _disposed;

    /// <summary>
    /// Event raised when progress is updated during backfill.
    /// </summary>
    public event Action<BackfillProgress>? OnProgressUpdate;

    public string Name => "composite";
    public string DisplayName => "Multi-Source (Auto-Failover)";
    public string Description => $"Automatically tries multiple providers ({string.Join(", ", _providers.Select(p => p.Name))}) with failover support.";

    public int Priority => 0;
    public TimeSpan RateLimitDelay => TimeSpan.Zero;
    public int MaxRequestsPerWindow => int.MaxValue;
    public TimeSpan RateLimitWindow => TimeSpan.FromHours(1);

    public bool SupportsAdjustedPrices => _providers.OfType<IHistoricalDataProviderV2>().Any(p => p.SupportsAdjustedPrices);
    public bool SupportsIntraday => _providers.OfType<IHistoricalDataProviderV2>().Any(p => p.SupportsIntraday);
    public bool SupportsDividends => _providers.OfType<IHistoricalDataProviderV2>().Any(p => p.SupportsDividends);
    public bool SupportsSplits => _providers.OfType<IHistoricalDataProviderV2>().Any(p => p.SupportsSplits);

    public IReadOnlyList<string> SupportedMarkets => _providers
        .OfType<IHistoricalDataProviderV2>()
        .SelectMany(p => p.SupportedMarkets)
        .Distinct()
        .ToList();

    /// <summary>
    /// Get current health status of all providers.
    /// </summary>
    public IReadOnlyDictionary<string, ProviderHealthStatus> ProviderHealth => _healthStatus;

    public CompositeHistoricalDataProvider(
        IEnumerable<IHistoricalDataProvider> providers,
        ISymbolResolver? symbolResolver = null,
        TimeSpan? failureBackoffDuration = null,
        bool enableCrossValidation = false,
        ILogger? log = null)
    {
        _providers = providers
            .OrderBy(p => p is IHistoricalDataProviderV2 v2 ? v2.Priority : 100)
            .ToList();

        if (_providers.Count == 0)
            throw new ArgumentException("At least one provider is required", nameof(providers));

        _symbolResolver = symbolResolver;
        _failureBackoffDuration = failureBackoffDuration ?? TimeSpan.FromMinutes(5);
        _enableCrossValidation = enableCrossValidation;
        _log = log ?? LoggingSetup.ForContext<CompositeHistoricalDataProvider>();

        // Initialize health status
        foreach (var provider in _providers)
        {
            _healthStatus[provider.Name] = new ProviderHealthStatus(provider.Name, true, "Not checked");
        }
    }

    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        // Available if any provider is available
        foreach (var provider in _providers)
        {
            if (provider is IHistoricalDataProviderV2 v2)
            {
                if (await v2.IsAvailableAsync(ct).ConfigureAwait(false))
                    return true;
            }
            else
            {
                // Assume basic providers are always available
                return true;
            }
        }
        return false;
    }

    public async Task<IReadOnlyList<HistoricalBar>> GetDailyBarsAsync(string symbol, DateOnly? from, DateOnly? to, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (string.IsNullOrWhiteSpace(symbol))
            throw new ArgumentException("Symbol is required", nameof(symbol));

        var errors = new List<(string Provider, Exception Error)>();

        foreach (var provider in _providers)
        {
            // Skip providers in backoff period
            if (IsInBackoffPeriod(provider.Name))
            {
                _log.Debug("Skipping {Provider} - in backoff period", provider.Name);
                continue;
            }

            try
            {
                // Resolve symbol for this provider if resolver is available
                var resolvedSymbol = await ResolveSymbolForProviderAsync(symbol, provider.Name, ct).ConfigureAwait(false);

                _log.Information("Trying {Provider} for {Symbol} (resolved: {Resolved})",
                    provider.Name, symbol, resolvedSymbol);

                var startTime = DateTimeOffset.UtcNow;
                var bars = await provider.GetDailyBarsAsync(resolvedSymbol, from, to, ct).ConfigureAwait(false);
                var elapsed = DateTimeOffset.UtcNow - startTime;

                if (bars.Count > 0)
                {
                    // Update health status
                    UpdateHealthStatus(provider.Name, true, $"Retrieved {bars.Count} bars", elapsed);
                    ClearFailure(provider.Name);

                    _log.Information("Successfully retrieved {Count} bars from {Provider} for {Symbol}",
                        bars.Count, provider.Name, symbol);

                    // Optionally validate against other providers
                    if (_enableCrossValidation && bars.Count > 0)
                    {
                        await ValidateBarsAsync(bars, symbol, from, to, provider.Name, ct).ConfigureAwait(false);
                    }

                    return bars;
                }

                _log.Debug("No bars returned from {Provider} for {Symbol}, trying next", provider.Name, symbol);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "Provider {Provider} failed for {Symbol}", provider.Name, symbol);
                errors.Add((provider.Name, ex));
                RecordFailure(provider.Name, ex.Message);
            }
        }

        // All providers failed
        if (errors.Count > 0)
        {
            var errorSummary = string.Join("; ", errors.Select(e => $"{e.Provider}: {e.Error.Message}"));
            throw new AggregateException($"All providers failed for {symbol}: {errorSummary}",
                errors.Select(e => e.Error));
        }

        _log.Warning("No data found from any provider for {Symbol}", symbol);
        return Array.Empty<HistoricalBar>();
    }

    public async Task<IReadOnlyList<AdjustedHistoricalBar>> GetAdjustedDailyBarsAsync(string symbol, DateOnly? from, DateOnly? to, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Try V2 providers first for adjusted data
        foreach (var provider in _providers.OfType<IHistoricalDataProviderV2>().Where(p => p.SupportsAdjustedPrices))
        {
            if (IsInBackoffPeriod(provider.Name))
                continue;

            try
            {
                var resolvedSymbol = await ResolveSymbolForProviderAsync(symbol, provider.Name, ct).ConfigureAwait(false);
                var bars = await provider.GetAdjustedDailyBarsAsync(resolvedSymbol, from, to, ct).ConfigureAwait(false);

                if (bars.Count > 0)
                {
                    ClearFailure(provider.Name);
                    return bars;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "Provider {Provider} failed for adjusted bars", provider.Name);
                RecordFailure(provider.Name, ex.Message);
            }
        }

        // Fallback to standard bars
        var standardBars = await GetDailyBarsAsync(symbol, from, to, ct).ConfigureAwait(false);
        return standardBars.Select(b => new AdjustedHistoricalBar(
            b.Symbol, b.SessionDate, b.Open, b.High, b.Low, b.Close, b.Volume, b.Source, b.SequenceNumber
        )).ToList();
    }

    /// <summary>
    /// Check health of all providers.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, ProviderHealthStatus>> CheckAllProvidersHealthAsync(CancellationToken ct = default)
    {
        var tasks = _providers
            .OfType<IHistoricalDataProviderV2>()
            .Select(async p =>
            {
                var startTime = DateTimeOffset.UtcNow;
                try
                {
                    var available = await p.IsAvailableAsync(ct).ConfigureAwait(false);
                    var elapsed = DateTimeOffset.UtcNow - startTime;
                    UpdateHealthStatus(p.Name, available, available ? "Healthy" : "Unavailable", elapsed);
                }
                catch (Exception ex)
                {
                    UpdateHealthStatus(p.Name, false, ex.Message);
                }
            });

        await Task.WhenAll(tasks).ConfigureAwait(false);
        return _healthStatus;
    }

    private async Task<string> ResolveSymbolForProviderAsync(string symbol, string providerName, CancellationToken ct)
    {
        if (_symbolResolver is null)
            return symbol;

        try
        {
            var mapped = await _symbolResolver.MapSymbolAsync(symbol, "input", providerName, ct).ConfigureAwait(false);
            return mapped ?? symbol;
        }
        catch (Exception ex)
        {
            _log.Debug(ex, "Symbol resolution failed for {Symbol} -> {Provider}", symbol, providerName);
            return symbol;
        }
    }

    private async Task ValidateBarsAsync(IReadOnlyList<HistoricalBar> bars, string symbol, DateOnly? from, DateOnly? to, string sourceProvider, CancellationToken ct)
    {
        // Try to validate with a different provider
        var validationProvider = _providers.FirstOrDefault(p => p.Name != sourceProvider);
        if (validationProvider is null) return;

        try
        {
            var validationBars = await validationProvider.GetDailyBarsAsync(symbol, from, to, ct).ConfigureAwait(false);

            if (validationBars.Count > 0)
            {
                var discrepancies = 0;
                foreach (var bar in bars.Take(5)) // Check first 5 bars
                {
                    var matchingBar = validationBars.FirstOrDefault(b => b.SessionDate == bar.SessionDate);
                    if (matchingBar is not null)
                    {
                        var closeDiff = Math.Abs(bar.Close - matchingBar.Close) / bar.Close;
                        if (closeDiff > 0.01m) // More than 1% difference
                        {
                            discrepancies++;
                            _log.Debug("Price discrepancy on {Date}: {Provider1}={Price1}, {Provider2}={Price2}",
                                bar.SessionDate, sourceProvider, bar.Close, validationProvider.Name, matchingBar.Close);
                        }
                    }
                }

                if (discrepancies > 0)
                {
                    _log.Warning("Found {Count} price discrepancies between {Provider1} and {Provider2} for {Symbol}",
                        discrepancies, sourceProvider, validationProvider.Name, symbol);
                }
            }
        }
        catch (Exception ex)
        {
            _log.Debug(ex, "Cross-validation failed for {Symbol}", symbol);
        }
    }

    private bool IsInBackoffPeriod(string providerName)
    {
        if (_providerFailures.TryGetValue(providerName, out var failedAt))
        {
            return DateTimeOffset.UtcNow - failedAt < _failureBackoffDuration;
        }
        return false;
    }

    private void RecordFailure(string providerName, string message)
    {
        _providerFailures[providerName] = DateTimeOffset.UtcNow;
        UpdateHealthStatus(providerName, false, message);
    }

    private void ClearFailure(string providerName)
    {
        _providerFailures.TryRemove(providerName, out _);
    }

    private void UpdateHealthStatus(string providerName, bool isAvailable, string? message = null, TimeSpan? responseTime = null)
    {
        _healthStatus[providerName] = new ProviderHealthStatus(
            providerName,
            isAvailable,
            message,
            DateTimeOffset.UtcNow,
            responseTime
        );
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var provider in _providers.OfType<IDisposable>())
        {
            provider.Dispose();
        }
    }
}

/// <summary>
/// Configuration for composite provider behavior.
/// </summary>
public sealed record CompositeProviderOptions
{
    /// <summary>
    /// Duration to skip a provider after failure.
    /// </summary>
    public TimeSpan FailureBackoffDuration { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Enable cross-validation of data between providers.
    /// </summary>
    public bool EnableCrossValidation { get; init; } = false;

    /// <summary>
    /// Maximum number of retries per provider.
    /// </summary>
    public int MaxRetriesPerProvider { get; init; } = 2;

    /// <summary>
    /// Prefer providers that support adjusted prices.
    /// </summary>
    public bool PreferAdjustedPrices { get; init; } = true;
}
