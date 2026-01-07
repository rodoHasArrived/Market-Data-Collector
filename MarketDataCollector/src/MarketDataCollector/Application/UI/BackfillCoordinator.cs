using MarketDataCollector.Application.Backfill;
using System.Threading;
using MarketDataCollector.Application.Config;
using MarketDataCollector.Application.Logging;
using MarketDataCollector.Application.Monitoring;
using MarketDataCollector.Application.Pipeline;
using MarketDataCollector.Infrastructure.Providers.Backfill;
using MarketDataCollector.Infrastructure.Providers.Backfill.SymbolResolution;
using MarketDataCollector.Storage;
using MarketDataCollector.Storage.Policies;
using MarketDataCollector.Storage.Sinks;
using Serilog;

namespace MarketDataCollector.Application.UI;

public sealed class BackfillCoordinator : IDisposable
{
    private readonly ConfigStore _store;
    private readonly ILogger _log = LoggingSetup.ForContext<BackfillCoordinator>();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly OpenFigiSymbolResolver? _symbolResolver;
    private BackfillResult? _lastRun;
    private bool _disposed;

    public BackfillCoordinator(ConfigStore store)
    {
        _store = store;
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
        var providers = CreateProviders();
        return providers
            .Select(p => new
            {
                p.Name,
                p.DisplayName,
                p.Description,
                Priority = p is IHistoricalDataProviderV2 v2 ? v2.Priority : 100,
                SupportsAdjustedPrices = p is IHistoricalDataProviderV2 v2a && v2a.SupportsAdjustedPrices,
                SupportsDividends = p is IHistoricalDataProviderV2 v2d && v2d.SupportsDividends
            });
    }

    public BackfillResult? TryReadLast() => _lastRun ?? _store.TryLoadBackfillStatus();

    /// <summary>
    /// Get health status of all providers.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, ProviderHealthStatus>> CheckProviderHealthAsync(CancellationToken ct = default)
    {
        var providers = CreateProviders();
        var results = new Dictionary<string, ProviderHealthStatus>();

        foreach (var provider in providers)
        {
            var startTime = DateTimeOffset.UtcNow;
            try
            {
                bool isAvailable;
                if (provider is IHistoricalDataProviderV2 v2)
                {
                    isAvailable = await v2.IsAvailableAsync(ct).ConfigureAwait(false);
                }
                else
                {
                    // Basic providers assumed available
                    isAvailable = true;
                }

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
            var storageOpt = cfg.Storage?.ToStorageOptions(cfg.DataRoot, cfg.Compress)
                ?? new StorageOptions
                {
                    RootPath = cfg.DataRoot,
                    Compress = cfg.Compress,
                    NamingConvention = FileNamingConvention.BySymbol,
                    DatePartition = DatePartition.Daily
                };

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

    private List<IHistoricalDataProvider> CreateProviders()
    {
        var cfg = _store.Load();
        var backfillCfg = cfg.Backfill;
        var providersCfg = backfillCfg?.Providers;

        var providers = new List<IHistoricalDataProvider>();

        // Stooq (always available, free)
        var stooqCfg = providersCfg?.Stooq;
        if (stooqCfg?.Enabled ?? true)
        {
            providers.Add(new StooqHistoricalDataProvider(log: _log));
        }

        // Yahoo Finance
        var yahooCfg = providersCfg?.Yahoo;
        if (yahooCfg?.Enabled ?? true)
        {
            providers.Add(new YahooFinanceHistoricalDataProvider(log: _log));
        }

        // Nasdaq Data Link (Quandl)
        var nasdaqCfg = providersCfg?.Nasdaq;
        if (nasdaqCfg?.Enabled ?? true)
        {
            providers.Add(new NasdaqDataLinkHistoricalDataProvider(
                apiKey: nasdaqCfg?.ApiKey,
                database: nasdaqCfg?.Database ?? "WIKI",
                log: _log
            ));
        }

        // Sort by priority
        return providers
            .OrderBy(p => p is IHistoricalDataProviderV2 v2 ? v2.Priority : 100)
            .ToList();
    }

    private HistoricalBackfillService CreateService()
    {
        var cfg = _store.Load();
        var backfillCfg = cfg.Backfill;

        var providers = CreateProviders();

        // If composite provider requested, wrap all providers
        if (string.Equals(backfillCfg?.Provider, "composite", StringComparison.OrdinalIgnoreCase)
            || (backfillCfg?.EnableFallback ?? true))
        {
            var composite = new CompositeHistoricalDataProvider(
                providers,
                backfillCfg?.EnableSymbolResolution ?? true ? _symbolResolver : null,
                enableCrossValidation: false,
                log: _log
            );

            providers = new List<IHistoricalDataProvider> { composite };
        }

        // Add individual providers as well for direct selection
        providers.AddRange(CreateProviders());

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
