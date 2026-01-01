using MarketDataCollector.Application.Backfill;
using MarketDataCollector.Application.Logging;
using MarketDataCollector.Application.Monitoring;
using MarketDataCollector.Application.Pipeline;
using MarketDataCollector.Infrastructure.Providers.Backfill;
using MarketDataCollector.Storage;
using MarketDataCollector.Storage.Policies;
using MarketDataCollector.Storage.Sinks;
using Serilog;

namespace MarketDataCollector.Application.UI;

public sealed class BackfillCoordinator
{
    private readonly ConfigStore _store;
    private readonly ILogger _log = LoggingSetup.ForContext<BackfillCoordinator>();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private BackfillResult? _lastRun;

    public BackfillCoordinator(ConfigStore store)
    {
        _store = store;
        _lastRun = store.TryLoadBackfillStatus();
    }

    public IEnumerable<object> DescribeProviders()
    {
        var service = CreateService();
        return service.Providers
            .Select(p => new { p.Name, p.DisplayName, p.Description });
    }

    public BackfillResult? TryReadLast() => _lastRun ?? _store.TryLoadBackfillStatus();

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

    private HistoricalBackfillService CreateService()
    {
        var providers = new IHistoricalDataProvider[]
        {
            new StooqHistoricalDataProvider()
        };
        return new HistoricalBackfillService(providers, _log);
    }
}
