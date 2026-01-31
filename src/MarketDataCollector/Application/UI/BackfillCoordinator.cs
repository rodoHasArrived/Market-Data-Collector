using MarketDataCollector.Application.Backfill;
using System.Threading;
using MarketDataCollector.Application.Config;
using MarketDataCollector.Application.Logging;
using MarketDataCollector.Application.Monitoring;
using MarketDataCollector.Application.Pipeline;
using MarketDataCollector.Infrastructure.Providers.Backfill;
using MarketDataCollector.Infrastructure.Providers.Backfill.SymbolResolution;
using MarketDataCollector.Infrastructure.Providers.Core;
using BackfillRequest = MarketDataCollector.Application.Backfill.BackfillRequest;
using MarketDataCollector.Storage;
using MarketDataCollector.Storage.Policies;
using MarketDataCollector.Storage.Sinks;
using Serilog;

namespace MarketDataCollector.Application.UI;

/// <summary>
/// Coordinates backfill operations using providers from the unified <see cref="ProviderRegistry"/>.
/// </summary>
/// <remarks>
/// All provider discovery is routed through <see cref="ProviderRegistry.GetProviders{T}"/>
/// rather than creating providers directly, ensuring consistent provider registration.
/// </remarks>
[ImplementsAdr("ADR-001", "Uses unified ProviderRegistry for provider discovery")]
public sealed class BackfillCoordinator : IDisposable
{
    private readonly ConfigStore _store;
    private readonly ProviderRegistry _registry;
    private readonly ILogger _log = LoggingSetup.ForContext<BackfillCoordinator>();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly OpenFigiSymbolResolver? _symbolResolver;
    private BackfillResult? _lastRun;
    private bool _disposed;

    /// <summary>
    /// Creates a new BackfillCoordinator that uses the unified ProviderRegistry for provider discovery.
    /// </summary>
    /// <param name="store">Configuration store.</param>
    /// <param name="registry">Provider registry containing all registered backfill providers.</param>
    public BackfillCoordinator(ConfigStore store, ProviderRegistry registry)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _lastRun = store.TryLoadBackfillStatus();

        // Initialize symbol resolver
        var cfg = store.Load();
        var openFigiConfig = cfg.Backfill?.Providers?.OpenFigi;
        if (openFigiConfig?.Enabled ?? true)
        {
            _symbolResolver = new OpenFigiSymbolResolver(openFigiConfig?.ApiKey, log: _log);
        }
    }

    /// <summary>
    /// Describes all registered backfill providers from the unified registry.
    /// </summary>
    public IEnumerable<object> DescribeProviders()
    {
        var providers = _registry.GetProviders<IHistoricalDataProvider>();
        return providers
            .Select(p => new
            {
                p.Name,
                p.DisplayName,
                p.Description,
                p.Priority,
                p.SupportsAdjustedPrices,
                p.SupportsDividends
            });
    }

    public BackfillResult? TryReadLast() => _lastRun ?? _store.TryLoadBackfillStatus();

    /// <summary>
    /// Get health status of all backfill providers from the unified registry.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, ProviderHealthStatus>> CheckProviderHealthAsync(CancellationToken ct = default)
    {
        var providers = _registry.GetProviders<IHistoricalDataProvider>();
        var results = new Dictionary<string, ProviderHealthStatus>();

        foreach (var provider in providers)
        {
            var startTime = DateTimeOffset.UtcNow;
            try
            {
                var isAvailable = await provider.IsAvailableAsync(ct).ConfigureAwait(false);
                var elapsed = DateTimeOffset.UtcNow - startTime;
                results[provider.Name] = new ProviderHealthStatus(
                    provider.Name,
                    isAvailable,
                    isAvailable ? "Healthy" : "Unavailable",
                    DateTimeOffset.UtcNow,
                    elapsed
                );
            }
            catch (Exception ex)
            {
                results[provider.Name] = new ProviderHealthStatus(
                    provider.Name,
                    false,
                    ex.Message,
                    DateTimeOffset.UtcNow
                );
            }
        }

        return results;
    }

    /// <summary>
    /// Resolve a symbol using OpenFIGI.
    /// </summary>
    public async Task<SymbolResolution?> ResolveSymbolAsync(string symbol, CancellationToken ct = default)
    {
        if (_symbolResolver is null)
        {
            _log.Warning("Symbol resolver not configured");
            return null;
        }

        return await _symbolResolver.ResolveAsync(symbol, ct: ct).ConfigureAwait(false);
    }

    public async Task<BackfillResult> RunAsync(BackfillRequest request, CancellationToken ct = default)
    {
        if (!await _gate.WaitAsync(TimeSpan.Zero, ct).ConfigureAwait(false))
            throw new InvalidOperationException("A backfill is already running. Please try again after it completes.");

        try
        {
            var cfg = _store.Load();
            var compressionEnabled = cfg.Compress ?? false;
            var storageOpt = cfg.Storage?.ToStorageOptions(cfg.DataRoot, compressionEnabled)
                ?? StorageProfilePresets.CreateFromProfile(null, cfg.DataRoot, compressionEnabled);

            var policy = new JsonlStoragePolicy(storageOpt);
            await using var sink = new JsonlStorageSink(storageOpt, policy);
            await using var pipeline = new EventPipeline(sink, capacity: 20_000, enablePeriodicFlush: false);

            // Keep pipeline counters scoped per run
            Metrics.Reset();

            var service = CreateService();
            var result = await service.RunAsync(request, pipeline, ct).ConfigureAwait(false);

            var statusStore = new BackfillStatusStore(_store.GetDataRoot(cfg));
            await statusStore.WriteAsync(result).ConfigureAwait(false);
            _lastRun = result;
            return result;
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Backfill failed");
            throw;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Creates a backfill service using providers from the unified registry.
    /// </summary>
    private HistoricalBackfillService CreateService()
    {
        var cfg = _store.Load();
        var backfillCfg = cfg.Backfill;

        // Get all backfill providers from the unified registry
        var registeredProviders = _registry.GetProviders<IHistoricalDataProvider>();
        var providers = new List<IHistoricalDataProvider>(registeredProviders);

        // If composite provider requested, wrap all providers
        if (string.Equals(backfillCfg?.Provider, "composite", StringComparison.OrdinalIgnoreCase)
            || (backfillCfg?.EnableFallback ?? true))
        {
            // Check if composite is already registered
            var existingComposite = _registry.GetProvider<CompositeHistoricalDataProvider>("composite-backfill");
            if (existingComposite != null)
            {
                // Use existing composite, add individual providers for direct selection
                providers = new List<IHistoricalDataProvider> { existingComposite };
                providers.AddRange(registeredProviders.Where(p => p is not CompositeHistoricalDataProvider));
            }
            else
            {
                // Create new composite from registered providers
                var composite = new CompositeHistoricalDataProvider(
                    registeredProviders.Where(p => p is not CompositeHistoricalDataProvider).ToList(),
                    backfillCfg?.EnableSymbolResolution ?? true ? _symbolResolver : null,
                    enableCrossValidation: false,
                    log: _log
                );

                providers = new List<IHistoricalDataProvider> { composite };
                // Add individual providers for direct selection
                providers.AddRange(registeredProviders.Where(p => p is not CompositeHistoricalDataProvider));
            }
        }

        return new HistoricalBackfillService(providers, _log);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _symbolResolver?.Dispose();
        _gate.Dispose();
    }
}
