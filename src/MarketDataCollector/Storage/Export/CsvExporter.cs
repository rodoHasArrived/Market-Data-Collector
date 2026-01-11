using System.Globalization;
using System.Text;
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

    public CrystallizedCsvExporter(
        CrystallizedStorageFormat format,
        CsvExportOptions? csvOptions = null)
    {
        _format = format ?? throw new ArgumentNullException(nameof(format));
        _csvExporter = new CsvExporter(csvOptions);
    }

    /// <summary>
    /// Exports all bar data for a symbol to a single CSV file.
    /// Useful for Excel users who want one file per symbol.
    /// </summary>
    // TODO: Implement actual crystallized storage reading - currently a placeholder
    public async Task ExportSymbolBarsAsync(
        string provider,
        string symbol,
        TimeGranularity granularity,
        string outputPath,
        DateOnly? fromDate = null,
        DateOnly? toDate = null,
        CancellationToken ct = default)
    {
        // Implementation would read from crystallized storage and export to CSV
        // This is a placeholder showing the intended API
        await Task.CompletedTask;
    }

    /// <summary>
    /// Creates a combined export of multiple symbols into a single CSV.
    /// Each row includes the symbol column.
    /// </summary>
    // TODO: Implement multi-symbol export from crystallized storage - currently a placeholder
    public async Task ExportMultipleSymbolsAsync(
        string provider,
        IEnumerable<string> symbols,
        TimeGranularity granularity,
        string outputPath,
        DateOnly? fromDate = null,
        DateOnly? toDate = null,
        CancellationToken ct = default)
    {
        // Implementation would combine multiple symbols into one file
        await Task.CompletedTask;
    }
}
