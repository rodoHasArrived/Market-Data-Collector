using System.IO.Compression;
using System.Text.Json;
using MarketDataCollector.Application.Logging;
using MarketDataCollector.Storage.Interfaces;
using Parquet;
using Parquet.Data;
using Parquet.Schema;
using Serilog;

namespace MarketDataCollector.Storage.Services;

/// <summary>
/// Background service that detects completed trading days' JSONL files
/// and converts them to Parquet format for optimized analytics queries.
/// Only converts files from prior trading days (never the current day's live data).
/// </summary>
public sealed class ParquetConversionService
{
    private readonly ILogger _log = LoggingSetup.ForContext<ParquetConversionService>();
    private readonly StorageOptions _options;
    private readonly string _parquetOutputDir;

    // Trade event schema for Parquet output
    private static readonly ParquetSchema TradeSchema = new(
        new DataField<DateTimeOffset>("Timestamp"),
        new DataField<string>("Symbol"),
        new DataField<decimal>("Price"),
        new DataField<long>("Size"),
        new DataField<string>("AggressorSide"),
        new DataField<string>("Exchange"),
        new DataField<string>("Source")
    );

    // Quote event schema for Parquet output
    private static readonly ParquetSchema QuoteSchema = new(
        new DataField<DateTimeOffset>("Timestamp"),
        new DataField<string>("Symbol"),
        new DataField<decimal>("BidPrice"),
        new DataField<long>("BidSize"),
        new DataField<decimal>("AskPrice"),
        new DataField<long>("AskSize"),
        new DataField<string>("Exchange")
    );

    public ParquetConversionService(StorageOptions options)
    {
        _options = options;
        _parquetOutputDir = Path.Combine(options.RootPath, "_parquet");
    }

    /// <summary>
    /// Scan for JSONL files from completed trading days and convert them to Parquet.
    /// </summary>
    /// <param name="maxAgeDays">Only consider files from the last N days (default: 30).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Summary of conversion results.</returns>
    public async Task<ConversionSummary> ConvertCompletedDaysAsync(int maxAgeDays = 30, CancellationToken ct = default)
    {
        var summary = new ConversionSummary();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var cutoff = today.AddDays(-maxAgeDays);

        if (!Directory.Exists(_options.RootPath))
        {
            _log.Warning("Data root {RootPath} does not exist; skipping Parquet conversion", _options.RootPath);
            return summary;
        }

        Directory.CreateDirectory(_parquetOutputDir);

        // Find all JSONL files (excluding today's live data)
        var jsonlFiles = Directory.GetFiles(_options.RootPath, "*.jsonl", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(_options.RootPath, "*.jsonl.gz", SearchOption.AllDirectories))
            .Where(f => !f.Contains("_wal") && !f.Contains("_archive") && !f.Contains("_parquet"))
            .ToList();

        foreach (var jsonlPath in jsonlFiles)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                var fileDate = ExtractDateFromPath(jsonlPath);
                if (fileDate == null || fileDate >= today || fileDate < cutoff)
                    continue;

                // Check if Parquet version already exists
                var parquetPath = GetParquetOutputPath(jsonlPath);
                if (File.Exists(parquetPath))
                {
                    summary.SkippedAlreadyConverted++;
                    continue;
                }

                // Read and convert
                var records = await ReadJsonlFileAsync(jsonlPath, ct);
                if (records.Count == 0)
                {
                    summary.SkippedEmpty++;
                    continue;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(parquetPath)!);
                await WriteParquetAsync(parquetPath, records, ct);

                summary.FilesConverted++;
                summary.RecordsConverted += records.Count;
                summary.BytesSaved += new FileInfo(jsonlPath).Length - new FileInfo(parquetPath).Length;

                _log.Information("Converted {Source} to Parquet ({RecordCount} records)",
                    Path.GetFileName(jsonlPath), records.Count);
            }
            catch (Exception ex)
            {
                summary.Errors++;
                _log.Warning(ex, "Failed to convert {File} to Parquet", jsonlPath);
            }
        }

        _log.Information(
            "Parquet conversion complete: {Converted} files, {Records} records, {Skipped} skipped, {Errors} errors",
            summary.FilesConverted, summary.RecordsConverted, summary.SkippedAlreadyConverted, summary.Errors);

        return summary;
    }

    private DateOnly? ExtractDateFromPath(string path)
    {
        // Try to extract a date from the file path or name
        // Supports patterns: 2026-01-15, 2026/01/15, filename containing date
        var fileName = Path.GetFileNameWithoutExtension(path);
        if (fileName.EndsWith(".jsonl", StringComparison.OrdinalIgnoreCase))
            fileName = Path.GetFileNameWithoutExtension(fileName);

        // Try parsing each segment of the path
        var segments = path.Replace('\\', '/').Split('/');
        foreach (var segment in segments.Reverse())
        {
            if (DateOnly.TryParse(segment, out var date))
                return date;
        }

        // Try extracting date from filename (e.g., AAPL.Trade.2026-01-15)
        var parts = fileName.Split('.');
        foreach (var part in parts)
        {
            if (DateOnly.TryParse(part, out var date))
                return date;
        }

        // Fall back to file modification time
        var fileInfo = new FileInfo(path);
        if (fileInfo.Exists)
        {
            var modDate = DateOnly.FromDateTime(fileInfo.LastWriteTimeUtc);
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            if (modDate < today)
                return modDate;
        }

        return null;
    }

    private string GetParquetOutputPath(string jsonlPath)
    {
        var relativePath = Path.GetRelativePath(_options.RootPath, jsonlPath);
        var baseName = relativePath
            .Replace(".jsonl.gz", ".parquet")
            .Replace(".jsonl", ".parquet");
        return Path.Combine(_parquetOutputDir, baseName);
    }

    private static async Task<List<Dictionary<string, JsonElement>>> ReadJsonlFileAsync(
        string path, CancellationToken ct)
    {
        var records = new List<Dictionary<string, JsonElement>>();

        Stream stream = File.OpenRead(path);
        if (path.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
            stream = new GZipStream(stream, CompressionMode.Decompress);

        await using (stream)
        using (var reader = new StreamReader(stream))
        {
            while (!reader.EndOfStream && !ct.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(ct);
                if (string.IsNullOrWhiteSpace(line)) continue;

                try
                {
                    var doc = JsonDocument.Parse(line);
                    var dict = new Dictionary<string, JsonElement>();
                    foreach (var prop in doc.RootElement.EnumerateObject())
                    {
                        dict[prop.Name] = prop.Value.Clone();
                    }
                    records.Add(dict);
                }
                catch
                {
                    // Skip malformed lines
                }
            }
        }

        return records;
    }

    private static async Task WriteParquetAsync(
        string path,
        List<Dictionary<string, JsonElement>> records,
        CancellationToken ct)
    {
        // Detect schema from first record
        if (records.Count == 0) return;

        var fields = records[0].Keys.Select(key =>
        {
            // Infer type from first non-null value
            foreach (var record in records)
            {
                if (record.TryGetValue(key, out var val))
                {
                    return val.ValueKind switch
                    {
                        JsonValueKind.Number when val.TryGetInt64(out _) => (DataField)new DataField<long>(key),
                        JsonValueKind.Number => new DataField<double>(key),
                        JsonValueKind.True or JsonValueKind.False => new DataField<bool>(key),
                        _ => new DataField<string>(key)
                    };
                }
            }
            return (DataField)new DataField<string>(key);
        }).ToArray();

        var schema = new ParquetSchema(fields);

        // Build column arrays
        var columns = new List<DataColumn>();
        foreach (var field in fields)
        {
            if (field.ClrType == typeof(long))
            {
                var data = records.Select(r =>
                    r.TryGetValue(field.Name, out var v) && v.ValueKind == JsonValueKind.Number
                        ? v.GetInt64() : 0L).ToArray();
                columns.Add(new DataColumn(field, data));
            }
            else if (field.ClrType == typeof(double))
            {
                var data = records.Select(r =>
                    r.TryGetValue(field.Name, out var v) && v.ValueKind == JsonValueKind.Number
                        ? v.GetDouble() : 0.0).ToArray();
                columns.Add(new DataColumn(field, data));
            }
            else if (field.ClrType == typeof(bool))
            {
                var data = records.Select(r =>
                    r.TryGetValue(field.Name, out var v) &&
                    (v.ValueKind == JsonValueKind.True || v.ValueKind == JsonValueKind.False)
                        ? v.GetBoolean() : false).ToArray();
                columns.Add(new DataColumn(field, data));
            }
            else
            {
                var data = records.Select(r =>
                    r.TryGetValue(field.Name, out var v) ? v.ToString() : "").ToArray();
                columns.Add(new DataColumn(field, data));
            }
        }

        await using var fileStream = File.Create(path);
        using var writer = await ParquetWriter.CreateAsync(schema, fileStream, cancellationToken: ct);
        using var groupWriter = writer.CreateRowGroup();
        foreach (var column in columns)
        {
            await groupWriter.WriteColumnAsync(column, ct);
        }
    }
}

/// <summary>
/// Summary of a Parquet conversion batch run.
/// </summary>
public sealed class ConversionSummary
{
    public int FilesConverted { get; set; }
    public long RecordsConverted { get; set; }
    public long BytesSaved { get; set; }
    public int SkippedAlreadyConverted { get; set; }
    public int SkippedEmpty { get; set; }
    public int Errors { get; set; }
}
