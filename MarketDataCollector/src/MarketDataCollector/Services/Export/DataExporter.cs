using System.Globalization;
using System.Text;
using System.Xml;
using MarketDataCollector.Application.Logging;
using MarketDataCollector.Domain.Events;
using MarketDataCollector.Domain.Models;
using MarketDataCollector.Services.CandleBuilding;
using Serilog;

namespace MarketDataCollector.Services.Export;

/// <summary>
/// Exports market data to various formats.
/// Inspired by StockSharp Hydra's export capabilities.
///
/// Supported formats:
/// - CSV (comma/tab/semicolon delimited)
/// - Excel-compatible XML
/// - Generic XML
/// - SQL INSERT statements
/// - JSON Lines
/// </summary>
public sealed class DataExporter
{
    private readonly ILogger _log = LoggingSetup.ForContext<DataExporter>();

    /// <summary>
    /// Export trades to CSV format.
    /// </summary>
    public async Task ExportTradesToCsvAsync(
        IAsyncEnumerable<Trade> trades,
        string outputPath,
        CsvExportOptions? options = null,
        CancellationToken ct = default)
    {
        options ??= new CsvExportOptions();
        var count = 0L;

        await using var writer = new StreamWriter(outputPath, false, Encoding.UTF8);

        // Write header
        if (options.IncludeHeader)
        {
            var headers = new[] { "Timestamp", "Symbol", "Price", "Size", "Side", "Venue", "SequenceNumber" };
            await writer.WriteLineAsync(string.Join(options.Delimiter, headers));
        }

        await foreach (var trade in trades.WithCancellation(ct))
        {
            var line = string.Join(options.Delimiter,
                FormatTimestamp(trade.Timestamp, options.TimestampFormat),
                trade.Symbol,
                trade.Price.ToString(CultureInfo.InvariantCulture),
                trade.Size.ToString(),
                trade.Aggressor.ToString(),
                trade.Venue ?? "",
                trade.SequenceNumber.ToString()
            );

            await writer.WriteLineAsync(line);
            count++;

            if (count % 100000 == 0)
                _log.Debug("Exported {Count:N0} trades to CSV", count);
        }

        _log.Information("Exported {Count:N0} trades to {Path}", count, outputPath);
    }

    /// <summary>
    /// Export candles to CSV format.
    /// </summary>
    public async Task ExportCandlesToCsvAsync(
        IAsyncEnumerable<Candle> candles,
        string outputPath,
        CsvExportOptions? options = null,
        CancellationToken ct = default)
    {
        options ??= new CsvExportOptions();
        var count = 0L;

        await using var writer = new StreamWriter(outputPath, false, Encoding.UTF8);

        if (options.IncludeHeader)
        {
            var headers = new[] { "OpenTime", "CloseTime", "Symbol", "Open", "High", "Low", "Close", "Volume", "TradeCount", "VWAP" };
            await writer.WriteLineAsync(string.Join(options.Delimiter, headers));
        }

        await foreach (var candle in candles.WithCancellation(ct))
        {
            var line = string.Join(options.Delimiter,
                FormatTimestamp(candle.OpenTime, options.TimestampFormat),
                FormatTimestamp(candle.CloseTime, options.TimestampFormat),
                candle.Symbol,
                candle.Open.ToString(CultureInfo.InvariantCulture),
                candle.High.ToString(CultureInfo.InvariantCulture),
                candle.Low.ToString(CultureInfo.InvariantCulture),
                candle.Close.ToString(CultureInfo.InvariantCulture),
                candle.Volume.ToString(),
                candle.TradeCount.ToString(),
                candle.Vwap?.ToString(CultureInfo.InvariantCulture) ?? ""
            );

            await writer.WriteLineAsync(line);
            count++;
        }

        _log.Information("Exported {Count:N0} candles to {Path}", count, outputPath);
    }

    /// <summary>
    /// Export trades to Excel-compatible XML (SpreadsheetML).
    /// </summary>
    public async Task ExportTradesToExcelXmlAsync(
        IAsyncEnumerable<Trade> trades,
        string outputPath,
        CancellationToken ct = default)
    {
        var settings = new XmlWriterSettings
        {
            Async = true,
            Indent = true,
            Encoding = Encoding.UTF8
        };

        await using var writer = XmlWriter.Create(outputPath, settings);
        var count = 0L;

        await writer.WriteStartDocumentAsync();
        await writer.WriteProcessingInstructionAsync("mso-application", "progid=\"Excel.Sheet\"");

        await writer.WriteStartElementAsync(null, "Workbook", "urn:schemas-microsoft-com:office:spreadsheet");
        await writer.WriteAttributeStringAsync("xmlns", "ss", null, "urn:schemas-microsoft-com:office:spreadsheet");

        // Styles
        await writer.WriteStartElementAsync(null, "Styles", null);
        await writer.WriteStartElementAsync(null, "Style", null);
        await writer.WriteAttributeStringAsync("ss", "ID", null, "Header");
        await writer.WriteStartElementAsync(null, "Font", null);
        await writer.WriteAttributeStringAsync("ss", "Bold", null, "1");
        await writer.WriteEndElementAsync(); // Font
        await writer.WriteEndElementAsync(); // Style
        await writer.WriteEndElementAsync(); // Styles

        // Worksheet
        await writer.WriteStartElementAsync(null, "Worksheet", null);
        await writer.WriteAttributeStringAsync("ss", "Name", null, "Trades");
        await writer.WriteStartElementAsync(null, "Table", null);

        // Header row
        await writer.WriteStartElementAsync(null, "Row", null);
        foreach (var header in new[] { "Timestamp", "Symbol", "Price", "Size", "Side", "Venue" })
        {
            await writer.WriteStartElementAsync(null, "Cell", null);
            await writer.WriteAttributeStringAsync("ss", "StyleID", null, "Header");
            await writer.WriteStartElementAsync(null, "Data", null);
            await writer.WriteAttributeStringAsync("ss", "Type", null, "String");
            await writer.WriteStringAsync(header);
            await writer.WriteEndElementAsync(); // Data
            await writer.WriteEndElementAsync(); // Cell
        }
        await writer.WriteEndElementAsync(); // Row

        // Data rows
        await foreach (var trade in trades.WithCancellation(ct))
        {
            await writer.WriteStartElementAsync(null, "Row", null);

            await WriteExcelCellAsync(writer, trade.Timestamp.ToString("o"), "String");
            await WriteExcelCellAsync(writer, trade.Symbol, "String");
            await WriteExcelCellAsync(writer, trade.Price.ToString(CultureInfo.InvariantCulture), "Number");
            await WriteExcelCellAsync(writer, trade.Size.ToString(), "Number");
            await WriteExcelCellAsync(writer, trade.Aggressor.ToString(), "String");
            await WriteExcelCellAsync(writer, trade.Venue ?? "", "String");

            await writer.WriteEndElementAsync(); // Row
            count++;
        }

        await writer.WriteEndElementAsync(); // Table
        await writer.WriteEndElementAsync(); // Worksheet
        await writer.WriteEndElementAsync(); // Workbook
        await writer.WriteEndDocumentAsync();

        _log.Information("Exported {Count:N0} trades to Excel XML {Path}", count, outputPath);
    }

    /// <summary>
    /// Export trades to SQL INSERT statements.
    /// </summary>
    public async Task ExportTradesToSqlAsync(
        IAsyncEnumerable<Trade> trades,
        string outputPath,
        SqlExportOptions? options = null,
        CancellationToken ct = default)
    {
        options ??= new SqlExportOptions();
        var count = 0L;
        var batchCount = 0;

        await using var writer = new StreamWriter(outputPath, false, Encoding.UTF8);

        // Write table creation if requested
        if (options.IncludeCreateTable)
        {
            await writer.WriteLineAsync($@"
CREATE TABLE IF NOT EXISTS {options.TableName} (
    timestamp DATETIME NOT NULL,
    symbol VARCHAR(50) NOT NULL,
    price DECIMAL(18,8) NOT NULL,
    size BIGINT NOT NULL,
    aggressor VARCHAR(10),
    venue VARCHAR(50),
    sequence_number BIGINT,
    PRIMARY KEY (symbol, timestamp, sequence_number)
);
");
        }

        var insertPrefix = $"INSERT INTO {options.TableName} (timestamp, symbol, price, size, aggressor, venue, sequence_number) VALUES";
        var values = new List<string>();

        await foreach (var trade in trades.WithCancellation(ct))
        {
            var valueStr = $"('{trade.Timestamp:yyyy-MM-dd HH:mm:ss.ffffff}', " +
                          $"'{EscapeSql(trade.Symbol)}', " +
                          $"{trade.Price.ToString(CultureInfo.InvariantCulture)}, " +
                          $"{trade.Size}, " +
                          $"'{trade.Aggressor}', " +
                          $"'{EscapeSql(trade.Venue ?? "")}', " +
                          $"{trade.SequenceNumber})";

            values.Add(valueStr);
            count++;

            if (values.Count >= options.BatchSize)
            {
                await writer.WriteLineAsync($"{insertPrefix}\n{string.Join(",\n", values)};");
                values.Clear();
                batchCount++;
            }
        }

        // Write remaining values
        if (values.Count > 0)
        {
            await writer.WriteLineAsync($"{insertPrefix}\n{string.Join(",\n", values)};");
        }

        _log.Information("Exported {Count:N0} trades as SQL to {Path} ({Batches} batches)",
            count, outputPath, batchCount + 1);
    }

    /// <summary>
    /// Export to generic XML format.
    /// </summary>
    public async Task ExportTradesToXmlAsync(
        IAsyncEnumerable<Trade> trades,
        string outputPath,
        CancellationToken ct = default)
    {
        var settings = new XmlWriterSettings
        {
            Async = true,
            Indent = true,
            Encoding = Encoding.UTF8
        };

        await using var writer = XmlWriter.Create(outputPath, settings);
        var count = 0L;

        await writer.WriteStartDocumentAsync();
        await writer.WriteStartElementAsync(null, "trades", null);
        await writer.WriteAttributeStringAsync(null, "exportedAt", null, DateTimeOffset.UtcNow.ToString("o"));

        await foreach (var trade in trades.WithCancellation(ct))
        {
            await writer.WriteStartElementAsync(null, "trade", null);
            await writer.WriteElementStringAsync(null, "timestamp", null, trade.Timestamp.ToString("o"));
            await writer.WriteElementStringAsync(null, "symbol", null, trade.Symbol);
            await writer.WriteElementStringAsync(null, "price", null, trade.Price.ToString(CultureInfo.InvariantCulture));
            await writer.WriteElementStringAsync(null, "size", null, trade.Size.ToString());
            await writer.WriteElementStringAsync(null, "aggressor", null, trade.Aggressor.ToString());
            if (trade.Venue != null)
                await writer.WriteElementStringAsync(null, "venue", null, trade.Venue);
            await writer.WriteElementStringAsync(null, "sequence", null, trade.SequenceNumber.ToString());
            await writer.WriteEndElementAsync();
            count++;
        }

        await writer.WriteEndElementAsync();
        await writer.WriteEndDocumentAsync();

        _log.Information("Exported {Count:N0} trades to XML {Path}", count, outputPath);
    }

    /// <summary>
    /// Export order book snapshots to CSV.
    /// </summary>
    public async Task ExportDepthToCsvAsync(
        IAsyncEnumerable<LOBSnapshot> snapshots,
        string outputPath,
        int maxLevels = 10,
        CsvExportOptions? options = null,
        CancellationToken ct = default)
    {
        options ??= new CsvExportOptions();
        var count = 0L;

        await using var writer = new StreamWriter(outputPath, false, Encoding.UTF8);

        // Build header
        if (options.IncludeHeader)
        {
            var headers = new List<string> { "Timestamp", "Symbol" };
            for (int i = 0; i < maxLevels; i++)
            {
                headers.Add($"BidPrice{i}");
                headers.Add($"BidSize{i}");
                headers.Add($"AskPrice{i}");
                headers.Add($"AskSize{i}");
            }
            await writer.WriteLineAsync(string.Join(options.Delimiter, headers));
        }

        await foreach (var snapshot in snapshots.WithCancellation(ct))
        {
            var values = new List<string>
            {
                FormatTimestamp(snapshot.Timestamp, options.TimestampFormat),
                snapshot.Symbol
            };

            for (int i = 0; i < maxLevels; i++)
            {
                var bid = i < snapshot.Bids.Count ? snapshot.Bids[i] : null;
                var ask = i < snapshot.Asks.Count ? snapshot.Asks[i] : null;

                values.Add(bid?.Price.ToString(CultureInfo.InvariantCulture) ?? "");
                values.Add(bid?.Size.ToString(CultureInfo.InvariantCulture) ?? "");
                values.Add(ask?.Price.ToString(CultureInfo.InvariantCulture) ?? "");
                values.Add(ask?.Size.ToString(CultureInfo.InvariantCulture) ?? "");
            }

            await writer.WriteLineAsync(string.Join(options.Delimiter, values));
            count++;
        }

        _log.Information("Exported {Count:N0} depth snapshots to {Path}", count, outputPath);
    }

    #region Private Helpers

    private static async Task WriteExcelCellAsync(XmlWriter writer, string value, string type)
    {
        await writer.WriteStartElementAsync(null, "Cell", null);
        await writer.WriteStartElementAsync(null, "Data", null);
        await writer.WriteAttributeStringAsync("ss", "Type", null, type);
        await writer.WriteStringAsync(value);
        await writer.WriteEndElementAsync(); // Data
        await writer.WriteEndElementAsync(); // Cell
    }

    private static string FormatTimestamp(DateTimeOffset ts, string format)
    {
        return format switch
        {
            "ISO8601" => ts.ToString("o"),
            "Unix" => ts.ToUnixTimeMilliseconds().ToString(),
            "DateTime" => ts.ToString("yyyy-MM-dd HH:mm:ss.ffffff"),
            _ => ts.ToString(format)
        };
    }

    private static string EscapeSql(string value)
    {
        return value.Replace("'", "''");
    }

    #endregion
}

/// <summary>
/// Options for CSV export.
/// </summary>
public sealed record CsvExportOptions
{
    /// <summary>Field delimiter.</summary>
    public string Delimiter { get; init; } = ",";

    /// <summary>Whether to include header row.</summary>
    public bool IncludeHeader { get; init; } = true;

    /// <summary>Timestamp format: ISO8601, Unix, DateTime, or custom format string.</summary>
    public string TimestampFormat { get; init; } = "ISO8601";

    /// <summary>Decimal places for prices.</summary>
    public int PriceDecimals { get; init; } = 8;
}

/// <summary>
/// Options for SQL export.
/// </summary>
public sealed record SqlExportOptions
{
    /// <summary>Target table name.</summary>
    public string TableName { get; init; } = "trades";

    /// <summary>Whether to include CREATE TABLE statement.</summary>
    public bool IncludeCreateTable { get; init; } = true;

    /// <summary>Number of rows per INSERT statement.</summary>
    public int BatchSize { get; init; } = 1000;

    /// <summary>SQL dialect: MySQL, PostgreSQL, SQLite, MSSQL.</summary>
    public string Dialect { get; init; } = "PostgreSQL";
}
