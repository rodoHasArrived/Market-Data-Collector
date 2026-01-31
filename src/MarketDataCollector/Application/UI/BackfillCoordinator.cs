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
/// All providers are retrieved via <see cref="ProviderRegistry.GetProviders{T}"/> rather than
/// being instantiated ad-hoc, ensuring consistent credential resolution and priority ordering.
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

    public IEnumerable<object> DescribeProviders()
    {
        var providers = GetProviders();
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
    /// Get health status of all providers.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, ProviderHealthStatus>> CheckProviderHealthAsync(CancellationToken ct = default)
    {
        var providers = GetProviders();
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
    /// Gets backfill providers from the unified registry.
    /// </summary>
    /// <remarks>
    /// Providers are retrieved via <see cref="ProviderRegistry.GetProviders{T}"/> which returns
    /// all enabled providers of the specified type, already sorted by priority.
    /// This replaces ad-hoc provider creation with centralized registry discovery.
    /// </remarks>
    private IReadOnlyList<IHistoricalDataProvider> GetProviders()
    {
        var providers = _registry.GetProviders<IHistoricalDataProvider>();

        if (providers.Count == 0)
        {
            _log.Warning("No backfill providers registered in ProviderRegistry. " +
                "Ensure providers are initialized via ProviderFactory.CreateAndRegisterAllAsync()");
        }

        return providers;
    }

    private HistoricalBackfillService CreateService()
    {
        var cfg = _store.Load();
        var backfillCfg = cfg.Backfill;

        var registryProviders = GetProviders();
        var providers = new List<IHistoricalDataProvider>(registryProviders);

        // If composite provider requested, wrap all providers with fallback support
        if (string.Equals(backfillCfg?.Provider, "composite", StringComparison.OrdinalIgnoreCase)
            || (backfillCfg?.EnableFallback ?? true))
        {
            var composite = new CompositeHistoricalDataProvider(
                registryProviders,
                backfillCfg?.EnableSymbolResolution ?? true ? _symbolResolver : null,
                enableCrossValidation: false,
                log: _log
            );

            // Use composite as primary, with individual providers for direct selection
            providers = new List<IHistoricalDataProvider> { composite };
            providers.AddRange(registryProviders);
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
