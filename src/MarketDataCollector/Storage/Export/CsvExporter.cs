using System.Globalization;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using MarketDataCollector.Domain.Events;
using MarketDataCollector.Domain.Models;
using MarketDataCollector.Storage.Crystallized;

namespace MarketDataCollector.Storage.Export;

/// <summary>
/// Exports market data to CSV format optimized for Excel and data analysis tools.
/// Provides consistent column schemas across all data types.
/// </summary>
public sealed class CsvExporter
{
    private readonly CsvExportOptions _options;

    public CsvExporter(CsvExportOptions? options = null)
    {
        _options = options ?? new CsvExportOptions();
    }

    /// <summary>
    /// Exports historical bars to CSV format.
    /// </summary>
    public async Task ExportBarsAsync(
        IAsyncEnumerable<HistoricalBar> bars,
        string outputPath,
        CancellationToken ct = default)
    {
        await using var writer = new StreamWriter(outputPath, false, Encoding.UTF8);

        if (_options.IncludeHeader)
        {
            await writer.WriteLineAsync(GetBarHeader());
        }

        await foreach (var bar in bars.WithCancellation(ct))
        {
            await writer.WriteLineAsync(FormatBar(bar));
        }
    }

    /// <summary>
    /// Exports historical bars to CSV format.
    /// </summary>
    public async Task ExportBarsAsync(
        IEnumerable<HistoricalBar> bars,
        string outputPath,
        CancellationToken ct = default)
    {
        await using var writer = new StreamWriter(outputPath, false, Encoding.UTF8);

        if (_options.IncludeHeader)
        {
            await writer.WriteLineAsync(GetBarHeader());
        }

        foreach (var bar in bars)
        {
            ct.ThrowIfCancellationRequested();
            await writer.WriteLineAsync(FormatBar(bar));
        }
    }

    /// <summary>
    /// Exports trades to CSV format.
    /// </summary>
    public async Task ExportTradesAsync(
        IAsyncEnumerable<Trade> trades,
        string outputPath,
        CancellationToken ct = default)
    {
        await using var writer = new StreamWriter(outputPath, false, Encoding.UTF8);

        if (_options.IncludeHeader)
        {
            await writer.WriteLineAsync(GetTradeHeader());
        }

        await foreach (var trade in trades.WithCancellation(ct))
        {
            await writer.WriteLineAsync(FormatTrade(trade));
        }
    }

    /// <summary>
    /// Exports BBO quotes to CSV format.
    /// </summary>
    public async Task ExportQuotesAsync(
        IAsyncEnumerable<BboQuotePayload> quotes,
        string outputPath,
        CancellationToken ct = default)
    {
        await using var writer = new StreamWriter(outputPath, false, Encoding.UTF8);

        if (_options.IncludeHeader)
        {
            await writer.WriteLineAsync(GetQuoteHeader());
        }

        await foreach (var quote in quotes.WithCancellation(ct))
        {
            await writer.WriteLineAsync(FormatQuote(quote));
        }
    }

    /// <summary>
    /// Exports market events to CSV format (generic).
    /// </summary>
    public async Task ExportEventsAsync(
        IAsyncEnumerable<MarketEvent> events,
        string outputPath,
        CancellationToken ct = default)
    {
        await using var writer = new StreamWriter(outputPath, false, Encoding.UTF8);

        if (_options.IncludeHeader)
        {
            await writer.WriteLineAsync(GetEventHeader());
        }

        await foreach (var evt in events.WithCancellation(ct))
        {
            await writer.WriteLineAsync(FormatEvent(evt));
        }
    }

    /// <summary>
    /// Gets the CSV header for bar data.
    /// </summary>
    public string GetBarHeader()
    {
        var cols = new List<string> { "date", "open", "high", "low", "close", "volume" };

        if (_options.IncludeSource)
            cols.Add("source");

        if (_options.IncludeSymbol)
            cols.Insert(0, "symbol");

        return string.Join(_options.Delimiter, cols);
    }

    /// <summary>
    /// Gets the CSV header for trade data.
    /// </summary>
    public string GetTradeHeader()
    {
        var cols = new List<string> { "timestamp", "price", "size", "side", "sequence" };

        if (_options.IncludeVenue)
            cols.Add("venue");

        if (_options.IncludeSource)
            cols.Add("source");

        if (_options.IncludeSymbol)
            cols.Insert(0, "symbol");

        return string.Join(_options.Delimiter, cols);
    }

    /// <summary>
    /// Gets the CSV header for quote data.
    /// </summary>
    public string GetQuoteHeader()
    {
        var cols = new List<string>
        {
            "timestamp", "bid_price", "bid_size", "ask_price", "ask_size", "spread", "mid_price", "sequence"
        };

        if (_options.IncludeSource)
            cols.Add("source");

        if (_options.IncludeSymbol)
            cols.Insert(0, "symbol");

        return string.Join(_options.Delimiter, cols);
    }

    /// <summary>
    /// Gets the CSV header for generic market events.
    /// </summary>
    public string GetEventHeader()
    {
        return string.Join(_options.Delimiter, new[]
        {
            "timestamp", "symbol", "type", "source", "sequence", "data"
        });
    }

    private string FormatBar(HistoricalBar bar)
    {
        var cols = new List<string>
        {
            bar.SessionDate.ToString(_options.DateFormat, CultureInfo.InvariantCulture),
            FormatDecimal(bar.Open),
            FormatDecimal(bar.High),
            FormatDecimal(bar.Low),
            FormatDecimal(bar.Close),
            bar.Volume.ToString(CultureInfo.InvariantCulture)
        };

        if (_options.IncludeSource)
            cols.Add(Escape(bar.Source));

        if (_options.IncludeSymbol)
            cols.Insert(0, Escape(bar.Symbol));

        return string.Join(_options.Delimiter, cols);
    }

    private string FormatTrade(Trade trade)
    {
        var cols = new List<string>
        {
            FormatTimestamp(trade.Timestamp),
            FormatDecimal(trade.Price),
            trade.Size.ToString(CultureInfo.InvariantCulture),
            trade.AggressorSide.ToString().ToLowerInvariant(),
            trade.SequenceNumber.ToString(CultureInfo.InvariantCulture)
        };

        if (_options.IncludeVenue)
            cols.Add(Escape(trade.Venue ?? ""));

        if (_options.IncludeSource)
            cols.Add(""); // Trade doesn't have source directly

        if (_options.IncludeSymbol)
            cols.Insert(0, Escape(trade.Symbol));

        return string.Join(_options.Delimiter, cols);
    }

    private string FormatQuote(BboQuotePayload quote)
    {
        var cols = new List<string>
        {
            FormatTimestamp(quote.Timestamp),
            FormatDecimal(quote.BidPrice),
            quote.BidSize.ToString(CultureInfo.InvariantCulture),
            FormatDecimal(quote.AskPrice),
            quote.AskSize.ToString(CultureInfo.InvariantCulture),
            FormatDecimal(quote.Spread),
            FormatDecimal(quote.MidPrice),
            quote.SequenceNumber.ToString(CultureInfo.InvariantCulture)
        };

        if (_options.IncludeSource)
            cols.Add("");

        if (_options.IncludeSymbol)
            cols.Insert(0, Escape(quote.Symbol));

        return string.Join(_options.Delimiter, cols);
    }

    private string FormatEvent(MarketEvent evt)
    {
        var data = evt.Payload switch
        {
            HistoricalBar bar => $"O={bar.Open}|H={bar.High}|L={bar.Low}|C={bar.Close}|V={bar.Volume}",
            Trade trade => $"P={trade.Price}|S={trade.Size}|Side={trade.AggressorSide}",
            BboQuotePayload quote => $"Bid={quote.BidPrice}x{quote.BidSize}|Ask={quote.AskPrice}x{quote.AskSize}",
            _ => evt.Payload?.ToString() ?? ""
        };

        return string.Join(_options.Delimiter, new[]
        {
            FormatTimestamp(evt.Timestamp),
            Escape(evt.Symbol),
            evt.Type.ToString(),
            Escape(evt.Source),
            evt.Sequence.ToString(CultureInfo.InvariantCulture),
            Escape(data)
        });
    }

    private string FormatTimestamp(DateTimeOffset ts)
    {
        return ts.ToString(_options.TimestampFormat, CultureInfo.InvariantCulture);
    }

    private string FormatDecimal(decimal value)
    {
        return value.ToString($"F{_options.DecimalPrecision}", CultureInfo.InvariantCulture);
    }

    private string Escape(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        // If the value contains the delimiter, quotes, or newlines, wrap in quotes
        if (value.Contains(_options.Delimiter) || value.Contains('"') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }
}

/// <summary>
/// Options for CSV export.
/// </summary>
public sealed class CsvExportOptions
{
    /// <summary>
    /// Column delimiter character.
    /// Default: ","
    /// </summary>
    public string Delimiter { get; init; } = ",";

    /// <summary>
    /// Whether to include a header row.
    /// Default: true
    /// </summary>
    public bool IncludeHeader { get; init; } = true;

    /// <summary>
    /// Whether to include the symbol column (useful when exporting single symbol).
    /// Default: false
    /// </summary>
    public bool IncludeSymbol { get; init; } = false;

    /// <summary>
    /// Whether to include the data source/provider column.
    /// Default: false
    /// </summary>
    public bool IncludeSource { get; init; } = false;

    /// <summary>
    /// Whether to include the venue column for trades.
    /// Default: false
    /// </summary>
    public bool IncludeVenue { get; init; } = false;

    /// <summary>
    /// Date format for daily bars.
    /// Default: "yyyy-MM-dd"
    /// </summary>
    public string DateFormat { get; init; } = "yyyy-MM-dd";

    /// <summary>
    /// Timestamp format for intraday data.
    /// Default: "yyyy-MM-dd HH:mm:ss.fff"
    /// </summary>
    public string TimestampFormat { get; init; } = "yyyy-MM-dd HH:mm:ss.fff";

    /// <summary>
    /// Decimal precision for price values.
    /// Default: 4
    /// </summary>
    public int DecimalPrecision { get; init; } = 4;

    /// <summary>
    /// Creates options optimized for Excel.
    /// </summary>
    public static CsvExportOptions ForExcel() => new()
    {
        Delimiter = ",",
        IncludeHeader = true,
        DateFormat = "yyyy-MM-dd",
        TimestampFormat = "yyyy-MM-dd HH:mm:ss",
        DecimalPrecision = 4
    };

    /// <summary>
    /// Creates options optimized for pandas/Python data analysis.
    /// </summary>
    public static CsvExportOptions ForPandas() => new()
    {
        Delimiter = ",",
        IncludeHeader = true,
        DateFormat = "yyyy-MM-dd",
        TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ", // ISO 8601
        DecimalPrecision = 6
    };

    /// <summary>
    /// Creates options for tab-separated values.
    /// </summary>
    public static CsvExportOptions TabSeparated() => new()
    {
        Delimiter = "\t",
        IncludeHeader = true,
        DateFormat = "yyyy-MM-dd",
        TimestampFormat = "yyyy-MM-dd HH:mm:ss.fff",
        DecimalPrecision = 4
    };
}

/// <summary>
/// Utility class for batch exporting data from the crystallized storage format.
/// </summary>
public sealed class CrystallizedCsvExporter
{
    private readonly CrystallizedStorageFormat _format;
    private readonly CsvExporter _csvExporter;
    private readonly CsvExportOptions _csvOptions;
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public CrystallizedCsvExporter(
        CrystallizedStorageFormat format,
        CsvExportOptions? csvOptions = null)
    {
        _format = format ?? throw new ArgumentNullException(nameof(format));
        _csvOptions = csvOptions ?? new CsvExportOptions();
        _csvExporter = new CsvExporter(_csvOptions);
    }

    /// <summary>
    /// Exports all bar data for a symbol to a single CSV file.
    /// Useful for Excel users who want one file per symbol.
    /// </summary>
    public async Task ExportSymbolBarsAsync(
        string provider,
        string symbol,
        TimeGranularity granularity,
        string outputPath,
        DateOnly? fromDate = null,
        DateOnly? toDate = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(provider))
            throw new ArgumentException("Provider is required", nameof(provider));
        if (string.IsNullOrWhiteSpace(symbol))
            throw new ArgumentException("Symbol is required", nameof(symbol));
        if (string.IsNullOrWhiteSpace(outputPath))
            throw new ArgumentException("Output path is required", nameof(outputPath));

        var bars = ReadBarsFromStorageAsync(provider, symbol, granularity, fromDate, toDate, ct);
        await _csvExporter.ExportBarsAsync(bars, outputPath, ct);
    }

    /// <summary>
    /// Creates a combined export of multiple symbols into a single CSV.
    /// Each row includes the symbol column.
    /// </summary>
    public async Task ExportMultipleSymbolsAsync(
        string provider,
        IEnumerable<string> symbols,
        TimeGranularity granularity,
        string outputPath,
        DateOnly? fromDate = null,
        DateOnly? toDate = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(provider))
            throw new ArgumentException("Provider is required", nameof(provider));
        if (symbols is null)
            throw new ArgumentNullException(nameof(symbols));
        if (string.IsNullOrWhiteSpace(outputPath))
            throw new ArgumentException("Output path is required", nameof(outputPath));

        var symbolList = symbols.ToList();
        if (symbolList.Count == 0)
            throw new ArgumentException("At least one symbol is required", nameof(symbols));

        // Ensure output directory exists
        var outputDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDir))
            Directory.CreateDirectory(outputDir);

        // Create exporter with symbol column enabled
        var multiSymbolOptions = _csvOptions with { IncludeSymbol = true };
        var multiExporter = new CsvExporter(multiSymbolOptions);

        await using var writer = new StreamWriter(outputPath, false, Encoding.UTF8);

        if (multiSymbolOptions.IncludeHeader)
        {
            await writer.WriteLineAsync(multiExporter.GetBarHeader());
        }

        // Stream bars from all symbols
        foreach (var symbol in symbolList)
        {
            ct.ThrowIfCancellationRequested();

            await foreach (var bar in ReadBarsFromStorageAsync(provider, symbol, granularity, fromDate, toDate, ct))
            {
                var line = FormatBarWithSymbol(bar, multiSymbolOptions);
                await writer.WriteLineAsync(line);
            }
        }
    }

    /// <summary>
    /// Reads historical bars from crystallized storage for a specific symbol.
    /// </summary>
    private async IAsyncEnumerable<HistoricalBar> ReadBarsFromStorageAsync(
        string provider,
        string symbol,
        TimeGranularity granularity,
        DateOnly? fromDate,
        DateOnly? toDate,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var barFiles = EnumerateBarFiles(provider, symbol, granularity);

        foreach (var filePath in barFiles)
        {
            ct.ThrowIfCancellationRequested();

            await foreach (var bar in ReadBarsFromFileAsync(filePath, ct))
            {
                // Filter by date range if specified
                if (fromDate.HasValue && bar.SessionDate < fromDate.Value)
                    continue;
                if (toDate.HasValue && bar.SessionDate > toDate.Value)
                    continue;

                yield return bar;
            }
        }
    }

    /// <summary>
    /// Enumerates all bar files for a symbol from crystallized storage.
    /// </summary>
    private IEnumerable<string> EnumerateBarFiles(string provider, string symbol, TimeGranularity granularity)
    {
        // Get the symbol manifest path to determine the base directory
        var manifestPath = _format.GetSymbolManifestPath(provider, symbol);
        var symbolDir = Path.GetDirectoryName(manifestPath);

        if (string.IsNullOrEmpty(symbolDir) || !Directory.Exists(symbolDir))
            yield break;

        // Build the bars directory path based on granularity
        var barsDir = Path.Combine(symbolDir, "bars", granularity.ToFileSuffix());

        if (!Directory.Exists(barsDir))
            yield break;

        // Find all data files (JSONL or CSV, optionally compressed)
        var patterns = new[] { "*.jsonl", "*.jsonl.gz", "*.jsonl.zst", "*.jsonl.lz4", "*.csv", "*.csv.gz" };

        var files = new List<string>();
        foreach (var pattern in patterns)
        {
            files.AddRange(Directory.EnumerateFiles(barsDir, pattern, SearchOption.TopDirectoryOnly));
        }

        // Sort files by name to ensure chronological order
        foreach (var file in files.OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            yield return file;
        }
    }

    /// <summary>
    /// Reads historical bars from a single JSONL or CSV file.
    /// </summary>
    private async IAsyncEnumerable<HistoricalBar> ReadBarsFromFileAsync(
        string filePath,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var isCompressed = filePath.EndsWith(".gz", StringComparison.OrdinalIgnoreCase);
        var isCsv = filePath.Contains(".csv", StringComparison.OrdinalIgnoreCase);

        await using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, useAsync: true);
        Stream stream = fs;

        if (isCompressed)
        {
            stream = new GZipStream(fs, CompressionMode.Decompress);
        }

        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 64 * 1024);

        var isFirstLine = true;
        string[]? csvHeaders = null;

        while (!reader.EndOfStream)
        {
            ct.ThrowIfCancellationRequested();

            var line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(line))
                continue;

            // Handle CSV header row
            if (isCsv && isFirstLine)
            {
                isFirstLine = false;
                // Check if this looks like a header row
                if (line.StartsWith("date", StringComparison.OrdinalIgnoreCase) ||
                    line.StartsWith("symbol", StringComparison.OrdinalIgnoreCase) ||
                    line.StartsWith("timestamp", StringComparison.OrdinalIgnoreCase))
                {
                    csvHeaders = line.Split(',');
                    continue;
                }
            }
            isFirstLine = false;

            HistoricalBar? bar = null;

            if (isCsv)
            {
                bar = ParseCsvBar(line, csvHeaders);
            }
            else
            {
                bar = ParseJsonlBar(line);
            }

            if (bar != null)
            {
                yield return bar;
            }
        }
    }

    /// <summary>
    /// Parses a HistoricalBar from a JSONL line.
    /// </summary>
    private static HistoricalBar? ParseJsonlBar(string line)
    {
        try
        {
            var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;

            var symbol = root.TryGetProperty("symbol", out var symProp) ? symProp.GetString() ?? "" : "";
            var source = root.TryGetProperty("source", out var srcProp) ? srcProp.GetString() ?? "unknown" : "unknown";

            DateOnly sessionDate;
            if (root.TryGetProperty("sessionDate", out var dateProp))
            {
                sessionDate = DateOnly.Parse(dateProp.GetString() ?? "");
            }
            else if (root.TryGetProperty("date", out var date2Prop))
            {
                sessionDate = DateOnly.Parse(date2Prop.GetString() ?? "");
            }
            else
            {
                return null;
            }

            var open = root.TryGetProperty("open", out var openProp) ? openProp.GetDecimal() : 0m;
            var high = root.TryGetProperty("high", out var highProp) ? highProp.GetDecimal() : 0m;
            var low = root.TryGetProperty("low", out var lowProp) ? lowProp.GetDecimal() : 0m;
            var close = root.TryGetProperty("close", out var closeProp) ? closeProp.GetDecimal() : 0m;
            var volume = root.TryGetProperty("volume", out var volProp) ? volProp.GetInt64() : 0L;
            var sequence = root.TryGetProperty("sequenceNumber", out var seqProp) ? seqProp.GetInt64() : 0L;

            if (open <= 0 || high <= 0 || low <= 0 || close <= 0)
                return null;

            return new HistoricalBar(symbol, sessionDate, open, high, low, close, volume, source, sequence);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Parses a HistoricalBar from a CSV line.
    /// </summary>
    private static HistoricalBar? ParseCsvBar(string line, string[]? headers)
    {
        try
        {
            var parts = line.Split(',');
            if (parts.Length < 6)
                return null;

            // Determine column indices based on headers or default order
            int dateIdx = 0, openIdx = 1, highIdx = 2, lowIdx = 3, closeIdx = 4, volumeIdx = 5;
            int symbolIdx = -1, sourceIdx = -1;

            if (headers != null)
            {
                for (int i = 0; i < headers.Length; i++)
                {
                    var h = headers[i].Trim().ToLowerInvariant();
                    switch (h)
                    {
                        case "date" or "sessiondate" or "timestamp": dateIdx = i; break;
                        case "open": openIdx = i; break;
                        case "high": highIdx = i; break;
                        case "low": lowIdx = i; break;
                        case "close": closeIdx = i; break;
                        case "volume": volumeIdx = i; break;
                        case "symbol": symbolIdx = i; break;
                        case "source": sourceIdx = i; break;
                    }
                }
            }

            var dateStr = parts[dateIdx].Trim();
            if (!DateOnly.TryParse(dateStr, out var sessionDate))
                return null;

            if (!decimal.TryParse(parts[openIdx].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var open) || open <= 0)
                return null;
            if (!decimal.TryParse(parts[highIdx].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var high) || high <= 0)
                return null;
            if (!decimal.TryParse(parts[lowIdx].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var low) || low <= 0)
                return null;
            if (!decimal.TryParse(parts[closeIdx].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var close) || close <= 0)
                return null;
            if (!long.TryParse(parts[volumeIdx].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var volume))
                volume = 0;

            var symbol = symbolIdx >= 0 && symbolIdx < parts.Length ? parts[symbolIdx].Trim() : "";
            var source = sourceIdx >= 0 && sourceIdx < parts.Length ? parts[sourceIdx].Trim() : "csv";

            return new HistoricalBar(symbol.Length > 0 ? symbol : "UNKNOWN", sessionDate, open, high, low, close, volume, source);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Formats a bar as a CSV line including the symbol column.
    /// </summary>
    private static string FormatBarWithSymbol(HistoricalBar bar, CsvExportOptions options)
    {
        var cols = new List<string>();

        if (options.IncludeSymbol)
            cols.Add(EscapeCsv(bar.Symbol, options.Delimiter));

        cols.Add(bar.SessionDate.ToString(options.DateFormat, CultureInfo.InvariantCulture));
        cols.Add(bar.Open.ToString($"F{options.DecimalPrecision}", CultureInfo.InvariantCulture));
        cols.Add(bar.High.ToString($"F{options.DecimalPrecision}", CultureInfo.InvariantCulture));
        cols.Add(bar.Low.ToString($"F{options.DecimalPrecision}", CultureInfo.InvariantCulture));
        cols.Add(bar.Close.ToString($"F{options.DecimalPrecision}", CultureInfo.InvariantCulture));
        cols.Add(bar.Volume.ToString(CultureInfo.InvariantCulture));

        if (options.IncludeSource)
            cols.Add(EscapeCsv(bar.Source, options.Delimiter));

        return string.Join(options.Delimiter, cols);
    }

    private static string EscapeCsv(string value, string delimiter)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        if (value.Contains(delimiter) || value.Contains('"') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }
}
