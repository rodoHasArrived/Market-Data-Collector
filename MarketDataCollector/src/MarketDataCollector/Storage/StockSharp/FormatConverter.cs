using MarketDataCollector.Application.Logging;
using System.Threading;
using MarketDataCollector.Domain.Events;
using MarketDataCollector.Domain.Models;
using MarketDataCollector.Storage.Policies;
using MarketDataCollector.Storage.Replay;
using MarketDataCollector.Storage.Sinks;
using Serilog;

namespace MarketDataCollector.Storage.StockSharp;

/// <summary>
/// Converts between JSONL and StockSharp binary storage formats.
/// Enables migration between storage backends and format optimization.
///
/// Conversion ratios:
/// - JSONL → Binary: ~25x compression for trades, ~28x for order books
/// - Binary → JSONL: Enables human-readable inspection and third-party tool integration
/// </summary>
public sealed class FormatConverter
{
    private readonly ILogger _log = LoggingSetup.ForContext<FormatConverter>();

    /// <summary>
    /// Convert JSONL files to StockSharp binary format.
    /// </summary>
    /// <param name="jsonlPath">Path to JSONL storage directory.</param>
    /// <param name="stockSharpPath">Path for StockSharp binary output.</param>
    /// <param name="progress">Optional progress reporter (0.0 - 1.0).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Conversion result with statistics.</returns>
    public async Task<ConversionResult> ConvertJsonlToStockSharpAsync(
        string jsonlPath,
        string stockSharpPath,
        IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        var result = new ConversionResult { SourcePath = jsonlPath, TargetPath = stockSharpPath };
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        _log.Information("Starting JSONL to StockSharp conversion: {Source} → {Target}",
            jsonlPath, stockSharpPath);

        try
        {
            await using var sink = new StockSharpStorageSink(stockSharpPath, useBinaryFormat: true);
            var replayer = new JsonlReplayer(jsonlPath);

            long totalEvents = 0;
            long progressInterval = 10000;

            await foreach (var evt in replayer.ReadEventsAsync(ct).ConfigureAwait(false))
            {
                await sink.AppendAsync(evt, ct).ConfigureAwait(false);
                result.EventsConverted++;
                totalEvents++;

                if (totalEvents % progressInterval == 0)
                {
                    _log.Debug("Converted {Count:N0} events", totalEvents);
                    // Progress is approximate since we don't know total ahead of time
                    progress?.Report(Math.Min(0.99, totalEvents / 1000000.0));
                }
            }

            await sink.FlushAsync(ct).ConfigureAwait(false);

            stopwatch.Stop();
            result.Success = true;
            result.Duration = stopwatch.Elapsed;

            _log.Information("JSONL to StockSharp conversion complete: {Count:N0} events in {Duration}",
                result.EventsConverted, result.Duration);
        }
        catch (OperationCanceledException)
        {
            result.Error = "Conversion cancelled";
            _log.Warning("JSONL to StockSharp conversion cancelled at {Count:N0} events",
                result.EventsConverted);
        }
        catch (Exception ex)
        {
            result.Error = ex.Message;
            _log.Error(ex, "JSONL to StockSharp conversion failed at {Count:N0} events",
                result.EventsConverted);
        }

        progress?.Report(1.0);
        return result;
    }

    /// <summary>
    /// Convert StockSharp binary files to JSONL format.
    /// </summary>
    /// <param name="stockSharpPath">Path to StockSharp storage directory.</param>
    /// <param name="jsonlPath">Path for JSONL output.</param>
    /// <param name="symbols">Symbols to convert.</param>
    /// <param name="from">Start of date range.</param>
    /// <param name="to">End of date range.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Conversion result with statistics.</returns>
    public async Task<ConversionResult> ConvertStockSharpToJsonlAsync(
        string stockSharpPath,
        string jsonlPath,
        string[] symbols,
        DateTimeOffset from,
        DateTimeOffset to,
        IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        var result = new ConversionResult { SourcePath = stockSharpPath, TargetPath = jsonlPath };
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        _log.Information("Starting StockSharp to JSONL conversion: {Source} → {Target} ({SymbolCount} symbols)",
            stockSharpPath, jsonlPath, symbols.Length);

        try
        {
            var reader = new StockSharpStorageReader(stockSharpPath);

            var storageOpt = new StorageOptions
            {
                RootPath = jsonlPath,
                NamingConvention = FileNamingConvention.BySymbol,
                DatePartition = DatePartition.Daily
            };

            var policy = new JsonlStoragePolicy(storageOpt);
            await using var sink = new JsonlStorageSink(storageOpt, policy);

            int symbolIndex = 0;
            foreach (var symbol in symbols)
            {
                ct.ThrowIfCancellationRequested();

                var symbolProgress = (double)symbolIndex / symbols.Length;
                progress?.Report(symbolProgress);

                _log.Debug("Converting symbol {Symbol} ({Index}/{Total})",
                    symbol, symbolIndex + 1, symbols.Length);

                // Convert trades
                await foreach (var trade in reader.ReadTradesAsync(symbol, from, to, ct).ConfigureAwait(false))
                {
                    var evt = MarketEvent.Trade(trade.Timestamp, symbol, trade, source: "stocksharp");
                    await sink.AppendAsync(evt, ct).ConfigureAwait(false);
                    result.EventsConverted++;
                    result.TradesConverted++;
                }

                // Convert depth snapshots
                await foreach (var lob in reader.ReadDepthAsync(symbol, from, to, ct).ConfigureAwait(false))
                {
                    var evt = MarketEvent.L2Snapshot(lob.Timestamp, symbol, lob, source: "stocksharp");
                    await sink.AppendAsync(evt, ct).ConfigureAwait(false);
                    result.EventsConverted++;
                    result.DepthSnapshotsConverted++;
                }

                // Convert quotes
                await foreach (var quote in reader.ReadQuotesAsync(symbol, from, to, ct).ConfigureAwait(false))
                {
                    var evt = MarketEvent.BboQuote(quote.Timestamp, symbol, quote, source: "stocksharp");
                    await sink.AppendAsync(evt, ct).ConfigureAwait(false);
                    result.EventsConverted++;
                    result.QuotesConverted++;
                }

                // Convert candles
                await foreach (var bar in reader.ReadCandlesAsync(symbol, from, to, ct).ConfigureAwait(false))
                {
                    var evt = MarketEvent.HistoricalBar(bar.ToTimestampUtc(), symbol, bar, source: "stocksharp");
                    await sink.AppendAsync(evt, ct).ConfigureAwait(false);
                    result.EventsConverted++;
                    result.CandlesConverted++;
                }

                symbolIndex++;
                _log.Debug("Completed symbol {Symbol}: {Count:N0} total events so far",
                    symbol, result.EventsConverted);
            }

            await sink.FlushAsync(ct).ConfigureAwait(false);

            stopwatch.Stop();
            result.Success = true;
            result.Duration = stopwatch.Elapsed;

            _log.Information("StockSharp to JSONL conversion complete: {Count:N0} events " +
                "(trades: {Trades:N0}, depth: {Depth:N0}, quotes: {Quotes:N0}, candles: {Candles:N0}) in {Duration}",
                result.EventsConverted, result.TradesConverted, result.DepthSnapshotsConverted,
                result.QuotesConverted, result.CandlesConverted, result.Duration);
        }
        catch (OperationCanceledException)
        {
            result.Error = "Conversion cancelled";
            _log.Warning("StockSharp to JSONL conversion cancelled at {Count:N0} events",
                result.EventsConverted);
        }
        catch (Exception ex)
        {
            result.Error = ex.Message;
            _log.Error(ex, "StockSharp to JSONL conversion failed at {Count:N0} events",
                result.EventsConverted);
        }

        progress?.Report(1.0);
        return result;
    }

    /// <summary>
    /// Estimate storage savings from converting JSONL to StockSharp binary.
    /// </summary>
    /// <param name="jsonlPath">Path to JSONL storage directory.</param>
    /// <returns>Estimated savings information.</returns>
    public StorageSavingsEstimate EstimateSavings(string jsonlPath)
    {
        if (!Directory.Exists(jsonlPath))
            return new StorageSavingsEstimate { Error = "Directory not found" };

        var files = Directory.GetFiles(jsonlPath, "*.jsonl*", SearchOption.AllDirectories);
        var totalBytes = files.Sum(f => new FileInfo(f).Length);

        // Estimate based on typical compression ratios:
        // - Trades: ~150 bytes JSONL → ~2 bytes binary (75x)
        // - With gzip: ~50 bytes → ~2 bytes (25x)
        // - Order books: ~500 bytes JSONL → ~7 bytes binary (71x)
        // - With gzip: ~200 bytes → ~7 bytes (28x)

        // Use conservative estimate of 20x average compression
        var estimatedBinaryBytes = totalBytes / 20;
        var savings = totalBytes - estimatedBinaryBytes;
        var savingsPercent = totalBytes > 0 ? (double)savings / totalBytes * 100 : 0;

        return new StorageSavingsEstimate
        {
            CurrentSizeBytes = totalBytes,
            EstimatedBinarySizeBytes = estimatedBinaryBytes,
            EstimatedSavingsBytes = savings,
            EstimatedSavingsPercent = savingsPercent,
            FileCount = files.Length
        };
    }
}

/// <summary>
/// Result of a format conversion operation.
/// </summary>
public sealed record ConversionResult
{
    /// <summary>Whether the conversion completed successfully.</summary>
    public bool Success { get; set; }

    /// <summary>Total events converted.</summary>
    public long EventsConverted { get; set; }

    /// <summary>Trade events converted.</summary>
    public long TradesConverted { get; set; }

    /// <summary>Depth snapshot events converted.</summary>
    public long DepthSnapshotsConverted { get; set; }

    /// <summary>Quote events converted.</summary>
    public long QuotesConverted { get; set; }

    /// <summary>Candle events converted.</summary>
    public long CandlesConverted { get; set; }

    /// <summary>Error message if conversion failed.</summary>
    public string? Error { get; set; }

    /// <summary>Source path.</summary>
    public string? SourcePath { get; set; }

    /// <summary>Target path.</summary>
    public string? TargetPath { get; set; }

    /// <summary>Duration of the conversion.</summary>
    public TimeSpan Duration { get; set; }

    /// <summary>Events per second throughput.</summary>
    public double EventsPerSecond => Duration.TotalSeconds > 0
        ? EventsConverted / Duration.TotalSeconds
        : 0;
}

/// <summary>
/// Estimate of storage savings from format conversion.
/// </summary>
public sealed record StorageSavingsEstimate
{
    /// <summary>Current storage size in bytes.</summary>
    public long CurrentSizeBytes { get; init; }

    /// <summary>Estimated binary storage size in bytes.</summary>
    public long EstimatedBinarySizeBytes { get; init; }

    /// <summary>Estimated savings in bytes.</summary>
    public long EstimatedSavingsBytes { get; init; }

    /// <summary>Estimated savings as percentage.</summary>
    public double EstimatedSavingsPercent { get; init; }

    /// <summary>Number of files analyzed.</summary>
    public int FileCount { get; init; }

    /// <summary>Error if estimation failed.</summary>
    public string? Error { get; init; }

    /// <summary>Format current size as human-readable string.</summary>
    public string CurrentSizeFormatted => FormatBytes(CurrentSizeBytes);

    /// <summary>Format estimated binary size as human-readable string.</summary>
    public string EstimatedBinarySizeFormatted => FormatBytes(EstimatedBinarySizeBytes);

    /// <summary>Format estimated savings as human-readable string.</summary>
    public string EstimatedSavingsFormatted => FormatBytes(EstimatedSavingsBytes);

    private static string FormatBytes(long bytes)
    {
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        int i = 0;
        double dblBytes = bytes;
        while (dblBytes >= 1024 && i < suffixes.Length - 1)
        {
            dblBytes /= 1024;
            i++;
        }
        return $"{dblBytes:0.##} {suffixes[i]}";
    }
}
