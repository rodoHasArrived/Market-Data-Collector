using MarketDataCollector.Application.Backfill;
using MarketDataCollector.Application.Logging;
using MarketDataCollector.Application.Pipeline;
using MarketDataCollector.Infrastructure.Providers.Backfill;
using MarketDataCollector.Storage;
using MarketDataCollector.Storage.Policies;
using MarketDataCollector.Storage.Sinks;
using Serilog;

namespace MarketDataCollector.Ui.Services;

/// <summary>
/// Result of a backfill preview operation.
/// </summary>
public sealed record BackfillPreviewResult(
    string Provider,
    string ProviderDisplayName,
    DateOnly From,
    DateOnly To,
    int TotalDays,
    int EstimatedTradingDays,
    SymbolPreview[] Symbols,
    int EstimatedDurationSeconds,
    string[] Notes
);

/// <summary>
/// Preview information for a single symbol.
/// </summary>
public sealed record SymbolPreview(
    string Symbol,
    string DateRange,
    int EstimatedBars,
    ExistingDataInfo ExistingData,
    bool WouldOverwrite
);

/// <summary>
/// Information about existing data for a symbol.
/// </summary>
public sealed record ExistingDataInfo(
    bool HasData,
    bool IsComplete,
    DateOnly? ExistingFrom,
    DateOnly? ExistingTo,
    int FileCount,
    long TotalSizeBytes
);

/// <summary>
/// Coordinates backfill operations for historical data retrieval.
/// Provides thread-safe execution with status tracking.
/// </summary>
public sealed class BackfillCoordinator
{
    private readonly ConfigStore _store;
    private readonly Serilog.ILogger _log = LoggingSetup.ForContext<BackfillCoordinator>();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private BackfillResult? _lastRun;

    public BackfillCoordinator(ConfigStore store)
    {
        _store = store;
        _lastRun = store.TryLoadBackfillStatus();
    }

    /// <summary>
    /// Gets descriptions of all available backfill providers.
    /// </summary>
    public IEnumerable<object> DescribeProviders()
    {
        var service = CreateService();
        return service.Providers
            .Select(p => new { p.Name, p.DisplayName, p.Description });
    }

    /// <summary>
    /// Tries to read the last backfill result.
    /// </summary>
    public BackfillResult? TryReadLast() => _lastRun ?? _store.TryLoadBackfillStatus();

    /// <summary>
    /// Previews a backfill operation without actually fetching data.
    /// Returns information about what would be backfilled.
    /// </summary>
    public async Task<BackfillPreviewResult> PreviewAsync(Application.Backfill.BackfillRequest request, CancellationToken ct = default)
    {
        var service = CreateService();
        var cfg = _store.Load();
        var dataRoot = _store.GetDataRoot(cfg);

        var symbolPreviews = new List<SymbolPreview>();
        var providerInfo = service.Providers
            .FirstOrDefault(p => p.Name.Equals(request.Provider, StringComparison.OrdinalIgnoreCase));

        var from = request.From ?? DateOnly.FromDateTime(DateTime.Today.AddYears(-1));
        var to = request.To ?? DateOnly.FromDateTime(DateTime.Today);
        var totalDays = to.DayNumber - from.DayNumber + 1;
        var tradingDays = EstimateTradingDays(from, to);

        foreach (var symbol in request.Symbols)
        {
            // Check if data already exists for this symbol
            var existingDataInfo = GetExistingDataInfo(dataRoot, symbol, from, to);

            symbolPreviews.Add(new SymbolPreview(
                Symbol: symbol.ToUpperInvariant(),
                DateRange: $"{from:yyyy-MM-dd} to {to:yyyy-MM-dd}",
                EstimatedBars: tradingDays,
                ExistingData: existingDataInfo,
                WouldOverwrite: existingDataInfo.HasData && !existingDataInfo.IsComplete
            ));
        }

        return new BackfillPreviewResult(
            Provider: providerInfo?.Name ?? request.Provider,
            ProviderDisplayName: providerInfo?.DisplayName ?? request.Provider,
            From: from,
            To: to,
            TotalDays: totalDays,
            EstimatedTradingDays: tradingDays,
            Symbols: symbolPreviews.ToArray(),
            EstimatedDurationSeconds: EstimateBackfillDuration(request.Symbols.Length, tradingDays),
            Notes: GetProviderNotes(providerInfo)
        );
    }

    private static int EstimateTradingDays(DateOnly from, DateOnly to)
    {
        // Rough estimate: ~252 trading days per year, exclude weekends
        var days = 0;
        var current = from;
        while (current <= to)
        {
            if (current.DayOfWeek != DayOfWeek.Saturday && current.DayOfWeek != DayOfWeek.Sunday)
            {
                days++;
            }
            current = current.AddDays(1);
        }
        return days;
    }

    private ExistingDataInfo GetExistingDataInfo(string dataRoot, string symbol, DateOnly from, DateOnly to)
    {
        // Check for existing data files
        var symbolDir = Path.Combine(dataRoot, "historical", symbol.ToUpperInvariant());
        if (!Directory.Exists(symbolDir))
        {
            return new ExistingDataInfo(
                HasData: false,
                IsComplete: false,
                ExistingFrom: null,
                ExistingTo: null,
                FileCount: 0,
                TotalSizeBytes: 0
            );
        }

        var files = Directory.GetFiles(symbolDir, "*.jsonl*", SearchOption.AllDirectories);
        if (files.Length == 0)
        {
            return new ExistingDataInfo(
                HasData: false,
                IsComplete: false,
                ExistingFrom: null,
                ExistingTo: null,
                FileCount: 0,
                TotalSizeBytes: 0
            );
        }

        var totalSize = files.Sum(f => new FileInfo(f).Length);

        // Try to determine date range from file names
        DateOnly? existingFrom = null;
        DateOnly? existingTo = null;
        foreach (var file in files)
        {
            var name = Path.GetFileNameWithoutExtension(file);
            // Try to extract date from filename (common patterns: YYYY-MM-DD, YYYYMMDD)
            if (TryExtractDateFromFileName(name, out var date))
            {
                if (existingFrom is null || date < existingFrom.Value)
                    existingFrom = date;
                if (existingTo is null || date > existingTo.Value)
                    existingTo = date;
            }
        }

        var isComplete = existingFrom <= from && existingTo >= to;

        return new ExistingDataInfo(
            HasData: true,
            IsComplete: isComplete,
            ExistingFrom: existingFrom,
            ExistingTo: existingTo,
            FileCount: files.Length,
            TotalSizeBytes: totalSize
        );
    }

    private static bool TryExtractDateFromFileName(string name, out DateOnly date)
    {
        date = default;

        // Try YYYY-MM-DD pattern
        if (name.Length >= 10)
        {
            for (var i = 0; i <= name.Length - 10; i++)
            {
                var segment = name.Substring(i, 10);
                if (DateOnly.TryParseExact(segment, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out date))
                    return true;
            }
        }

        // Try YYYYMMDD pattern
        if (name.Length >= 8)
        {
            for (var i = 0; i <= name.Length - 8; i++)
            {
                var segment = name.Substring(i, 8);
                if (DateOnly.TryParseExact(segment, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out date))
                    return true;
            }
        }

        return false;
    }

    private static int EstimateBackfillDuration(int symbolCount, int tradingDays)
    {
        // Rough estimate based on typical API rates and processing time
        // Most free APIs allow 5-60 requests/minute
        var requestsPerSecond = 0.5; // Conservative estimate
        var estimatedRequests = symbolCount * (tradingDays / 252 + 1); // One request per symbol per year
        return (int)(estimatedRequests / requestsPerSecond) + symbolCount; // Add processing overhead
    }

    private static string[] GetProviderNotes(IHistoricalDataProvider? provider)
    {
        var notes = new List<string>();

        if (provider is null)
        {
            notes.Add("Provider not found. Backfill may fail.");
            return notes.ToArray();
        }

        if (provider.Name.Equals("stooq", StringComparison.OrdinalIgnoreCase))
        {
            notes.Add("Stooq provides daily OHLCV data for free.");
            notes.Add("Rate limits apply. Large date ranges may take several minutes.");
        }
        else if (provider.Name.Equals("yahoo", StringComparison.OrdinalIgnoreCase))
        {
            notes.Add("Yahoo Finance data is unofficial and may have gaps.");
        }
        else if (provider.Name.Equals("alpaca", StringComparison.OrdinalIgnoreCase))
        {
            notes.Add("Alpaca requires API credentials.");
            notes.Add("Rate limit: 200 requests/minute.");
        }

        return notes.ToArray();
    }

    /// <summary>
    /// Runs a backfill operation for the specified request.
    /// Thread-safe - only one backfill can run at a time.
    /// </summary>
    public async Task<BackfillResult> RunAsync(Application.Backfill.BackfillRequest request, CancellationToken ct = default)
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
