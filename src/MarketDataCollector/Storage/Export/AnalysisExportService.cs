using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using MarketDataCollector.Application.Logging;
using Parquet;
using Parquet.Data;
using Parquet.Schema;
using Serilog;

namespace MarketDataCollector.Storage.Export;

/// <summary>
/// Service for exporting collected market data in analysis-ready formats.
/// Supports multiple output formats optimized for external analysis tools.
/// </summary>
public sealed class AnalysisExportService
{
    private readonly ILogger _log = LoggingSetup.ForContext<AnalysisExportService>();
    private readonly string _dataRoot;
    private readonly Dictionary<string, ExportProfile> _profiles;

    public AnalysisExportService(string dataRoot)
    {
        _dataRoot = dataRoot;
        _profiles = ExportProfile.GetBuiltInProfiles()
            .ToDictionary(p => p.Id, p => p);
    }

    /// <summary>
    /// Get all available export profiles.
    /// </summary>
    public IReadOnlyList<ExportProfile> GetProfiles() => _profiles.Values.ToList();

    /// <summary>
    /// Get a specific profile by ID.
    /// </summary>
    public ExportProfile? GetProfile(string profileId) =>
        _profiles.TryGetValue(profileId, out var profile) ? profile : null;

    /// <summary>
    /// Register a custom export profile.
    /// </summary>
    public void RegisterProfile(ExportProfile profile)
    {
        _profiles[profile.Id] = profile;
        _log.Information("Registered export profile: {ProfileId} ({Name})", profile.Id, profile.Name);
    }

    /// <summary>
    /// Export data according to the request.
    /// </summary>
    public async Task<ExportResult> ExportAsync(ExportRequest request, CancellationToken ct = default)
    {
        var profile = request.CustomProfile ?? GetProfile(request.ProfileId);
        if (profile is null)
            return ExportResult.CreateFailure(request.ProfileId, $"Unknown profile: {request.ProfileId}");

        var result = ExportResult.CreateSuccess(profile.Id, request.OutputDirectory);

        try
        {
            _log.Information("Starting export with profile {ProfileId} to {OutputDir}",
                profile.Id, request.OutputDirectory);

            // Ensure output directory exists
            Directory.CreateDirectory(request.OutputDirectory);

            // Find source files to export
            var sourceFiles = FindSourceFiles(request);
            if (sourceFiles.Count is 0)
            {
                result.Warnings = [.. result.Warnings, "No source data found for the specified criteria"];
                result.CompletedAt = DateTime.UtcNow;
                return result;
            }

            _log.Information("Found {FileCount} source files to export", sourceFiles.Count);

            // Export based on format
            var exportedFiles = new List<ExportedFile>();

            switch (profile.Format)
            {
                case ExportFormat.Csv:
                    exportedFiles = await ExportToCsvAsync(sourceFiles, request, profile, ct);
                    break;
                case ExportFormat.Parquet:
                    exportedFiles = await ExportToParquetAsync(sourceFiles, request, profile, ct);
                    break;
                case ExportFormat.Jsonl:
                    exportedFiles = await ExportToJsonlAsync(sourceFiles, request, profile, ct);
                    break;
                case ExportFormat.Lean:
                    exportedFiles = await ExportToLeanAsync(sourceFiles, request, profile, ct);
                    break;
                case ExportFormat.Sql:
                    exportedFiles = await ExportToSqlAsync(sourceFiles, request, profile, ct);
                    break;
                case ExportFormat.Xlsx:
                    exportedFiles = await ExportToXlsxAsync(sourceFiles, request, profile, ct);
                    break;
                default:
                    throw new NotSupportedException($"Format {profile.Format} is not supported");
            }

            result.Files = exportedFiles.ToArray();
            result.FilesGenerated = exportedFiles.Count;
            result.TotalRecords = exportedFiles.Sum(f => f.RecordCount);
            result.TotalBytes = exportedFiles.Sum(f => f.SizeBytes);
            result.Symbols = exportedFiles
                .Where(f => f.Symbol != null)
                .Select(f => f.Symbol!)
                .Distinct()
                .ToArray();

            result.DateRange = new ExportDateRange
            {
                Start = request.StartDate,
                End = request.EndDate,
                TradingDays = CountTradingDays(request.StartDate, request.EndDate)
            };

            // Generate supporting files
            if (profile.IncludeDataDictionary)
            {
                var dictPath = await GenerateDataDictionaryAsync(
                    request.OutputDirectory, request.EventTypes, profile, ct);
                result.DataDictionaryPath = dictPath;
            }

            if (profile.IncludeLoaderScript)
            {
                var scriptPath = await GenerateLoaderScriptAsync(
                    request.OutputDirectory, profile, exportedFiles, ct);
                result.LoaderScriptPath = scriptPath;
            }

            result.CompletedAt = DateTime.UtcNow;
            result.Success = true;

            _log.Information("Export completed: {FileCount} files, {RecordCount:N0} records, {Bytes:N0} bytes",
                result.FilesGenerated, result.TotalRecords, result.TotalBytes);

            return result;
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Export failed for profile {ProfileId}", profile.Id);
            result.Success = false;
            result.Error = ex.Message;
            result.CompletedAt = DateTime.UtcNow;
            return result;
        }
    }

    private List<SourceFile> FindSourceFiles(ExportRequest request)
    {
        if (!Directory.Exists(_dataRoot)) return [];

        return ["*.jsonl", "*.jsonl.gz"]
            .SelectMany(pattern => Directory.GetFiles(_dataRoot, pattern, SearchOption.AllDirectories))
            .Select(ParseFileName)
            .Where(f => f is not null)
            .Where(f => request.Symbols is not { Length: > 0 } ||
                        request.Symbols.Contains(f!.Symbol, StringComparer.OrdinalIgnoreCase))
            .Where(f => request.EventTypes is not { Length: > 0 } ||
                        request.EventTypes.Contains(f!.EventType, StringComparer.OrdinalIgnoreCase))
            .Where(f => !f!.Date.HasValue ||
                        (f.Date.Value >= request.StartDate.Date && f.Date.Value <= request.EndDate.Date))
            .OrderBy(f => f!.Symbol)
            .ThenBy(f => f!.Date)
            .ToList()!;
    }

    private SourceFile? ParseFileName(string path)
    {
        var fileName = Path.GetFileName(path);
        var parts = fileName.Split('.');

        if (parts.Length < 2) return null;

        // Handle patterns like: AAPL.Trade.jsonl, SPY.BboQuote.2026-01-03.jsonl.gz
        var result = new SourceFile
        {
            Path = path,
            IsCompressed = fileName.EndsWith(".gz", StringComparison.OrdinalIgnoreCase)
        };

        // Try to extract symbol and event type
        result.Symbol = parts[0];

        if (parts.Length >= 3)
        {
            result.EventType = parts[1];

            // Check if there's a date component
            if (parts.Length >= 4 && DateTime.TryParse(parts[2], out var date))
            {
                result.Date = date;
            }
        }

        return result;
    }

    private async Task<List<ExportedFile>> ExportToCsvAsync(
        List<SourceFile> sourceFiles,
        ExportRequest request,
        ExportProfile profile,
        CancellationToken ct)
    {
        var exportedFiles = new List<ExportedFile>();

        foreach (var group in GroupBySymbolIfRequired(sourceFiles, profile.SplitBySymbol))
        {
            var symbol = group.Key ?? "combined";
            var outputPath = Path.Combine(
                request.OutputDirectory,
                $"{symbol}_{DateTime.UtcNow:yyyyMMdd}.csv");

            var recordCount = 0L;
            var isFirst = true;

            await using var writer = new StreamWriter(outputPath, false, Encoding.UTF8);

            foreach (var sourceFile in group)
            {
                await foreach (var record in ReadJsonlRecordsAsync(sourceFile.Path, ct))
                {
                    if (isFirst)
                    {
                        // Write header
                        await writer.WriteLineAsync(string.Join(",", record.Keys));
                        isFirst = false;
                    }

                    // Write values
                    var values = record.Values.Select(v => EscapeCsvValue(v?.ToString() ?? ""));
                    await writer.WriteLineAsync(string.Join(",", values));
                    recordCount++;
                }
            }

            var fileInfo = new FileInfo(outputPath);
            exportedFiles.Add(new ExportedFile
            {
                Path = outputPath,
                RelativePath = Path.GetFileName(outputPath),
                Symbol = symbol,
                Format = "csv",
                SizeBytes = fileInfo.Length,
                RecordCount = recordCount,
                ChecksumSha256 = await ComputeChecksumAsync(outputPath, ct)
            });
        }

        return exportedFiles;
    }

    private async Task<List<ExportedFile>> ExportToParquetAsync(
        List<SourceFile> sourceFiles,
        ExportRequest request,
        ExportProfile profile,
        CancellationToken ct)
    {
        var exportedFiles = new List<ExportedFile>();

        foreach (var group in GroupBySymbolIfRequired(sourceFiles, profile.SplitBySymbol))
        {
            var symbol = group.Key ?? "combined";
            var outputPath = Path.Combine(
                request.OutputDirectory,
                $"{symbol}_{DateTime.UtcNow:yyyyMMdd}.parquet");

            // Collect all records first to determine schema
            var records = new List<Dictionary<string, object?>>();
            long recordCount = 0;

            foreach (var sourceFile in group)
            {
                await foreach (var record in ReadJsonlRecordsAsync(sourceFile.Path, ct))
                {
                    records.Add(record);
                    recordCount++;
                }
            }

            if (records.Count is > 0)
                await WriteParquetFileAsync(outputPath, records, ct);
            else
                await WriteEmptyParquetFileAsync(outputPath, ct);

            var fileInfo = new FileInfo(outputPath);
            exportedFiles.Add(new ExportedFile
            {
                Path = outputPath,
                RelativePath = Path.GetFileName(outputPath),
                Symbol = symbol,
                Format = "parquet",
                SizeBytes = fileInfo.Length,
                RecordCount = recordCount,
                ChecksumSha256 = await ComputeChecksumAsync(outputPath, ct)
            });
        }

        return exportedFiles;
    }

    /// <summary>
    /// Writes records to a Parquet file using columnar storage format.
    /// Dynamically builds schema from record keys and writes data in columnar format
    /// for optimal compression and analytics performance.
    /// </summary>
    private async Task WriteParquetFileAsync(
        string path,
        List<Dictionary<string, object?>> records,
        CancellationToken ct)
    {
        if (records.Count == 0) return;

        // Build schema from the first record's keys
        var firstRecord = records[0];
        var columns = firstRecord.Keys.ToList();
        var dataFields = new List<DataField>();

        // Infer schema from first record's values
        foreach (var column in columns)
        {
            var value = firstRecord[column];
            var dataField = InferDataField(column, value);
            dataFields.Add(dataField);
        }

        var schema = new ParquetSchema(dataFields);

        // Prepare columnar data
        var columnData = new Dictionary<string, List<object?>>();
        foreach (var column in columns)
        {
            columnData[column] = new List<object?>(records.Count);
        }

        // Extract data into columns
        foreach (var record in records)
        {
            foreach (var column in columns)
            {
                record.TryGetValue(column, out var value);
                columnData[column].Add(value);
            }
        }

        // Write to Parquet file
        await using var fileStream = File.Create(path);
        using var parquetWriter = await ParquetWriter.CreateAsync(schema, fileStream);
        using var rowGroupWriter = parquetWriter.CreateRowGroup();

        for (int i = 0; i < columns.Count; i++)
        {
            var column = columns[i];
            var dataField = dataFields[i];
            var values = columnData[column];
            var dataColumn = CreateDataColumn(dataField, values);
            await rowGroupWriter.WriteColumnAsync(dataColumn);
        }

        _log.Debug("Wrote {RecordCount} records to Parquet file: {Path}", records.Count, path);
    }

    /// <summary>
    /// Creates an empty Parquet file with a minimal schema.
    /// </summary>
    private async Task WriteEmptyParquetFileAsync(string path, CancellationToken ct)
    {
        var schema = new ParquetSchema(
            new DataField<string>("_empty")
        );

        await using var fileStream = File.Create(path);
        using var parquetWriter = await ParquetWriter.CreateAsync(schema, fileStream);
        using var rowGroupWriter = parquetWriter.CreateRowGroup();
        await rowGroupWriter.WriteColumnAsync(new DataColumn(schema.DataFields[0], Array.Empty<string>()));
    }

    /// <summary>
    /// Infers the appropriate Parquet DataField type from a sample value.
    /// </summary>
    private static DataField InferDataField(string columnName, object? sampleValue)
    {
        return sampleValue switch
        {
            int => new DataField<int?>(columnName),
            long => new DataField<long?>(columnName),
            float => new DataField<float?>(columnName),
            double => new DataField<double?>(columnName),
            decimal => new DataField<decimal?>(columnName),
            bool => new DataField<bool?>(columnName),
            DateTime => new DataField<DateTimeOffset?>(columnName),
            DateTimeOffset => new DataField<DateTimeOffset?>(columnName),
            _ => new DataField<string>(columnName) // Default to string for unknown types
        };
    }

    /// <summary>
    /// Creates a DataColumn from a list of values, converting them to the appropriate type.
    /// </summary>
    private static DataColumn CreateDataColumn(DataField dataField, List<object?> values) =>
        dataField.ClrType switch
        {
            var t when t == typeof(int?) => new DataColumn(dataField, values.Select(ConvertToInt).ToArray()),
            var t when t == typeof(long?) => new DataColumn(dataField, values.Select(ConvertToLong).ToArray()),
            var t when t == typeof(float?) => new DataColumn(dataField, values.Select(ConvertToFloat).ToArray()),
            var t when t == typeof(double?) => new DataColumn(dataField, values.Select(ConvertToDouble).ToArray()),
            var t when t == typeof(decimal?) => new DataColumn(dataField, values.Select(ConvertToDecimal).ToArray()),
            var t when t == typeof(bool?) => new DataColumn(dataField, values.Select(ConvertToBool).ToArray()),
            var t when t == typeof(DateTimeOffset?) => new DataColumn(dataField, values.Select(ConvertToDateTimeOffset).ToArray()),
            _ => new DataColumn(dataField, values.Select(v => v?.ToString() ?? string.Empty).ToArray())
        };

    private static int? ConvertToInt(object? v) => v switch
    {
        int i => i,
        long l => (int)l,
        double d => (int)d,
        null => null,
        _ => Convert.ToInt32(v)
    };

    private static long? ConvertToLong(object? v) => v switch
    {
        long l => l,
        int i => i,
        double d => (long)d,
        null => null,
        _ => Convert.ToInt64(v)
    };

    private static float? ConvertToFloat(object? v) => v switch
    {
        float f => f,
        double d => (float)d,
        int i => i,
        null => null,
        _ => Convert.ToSingle(v)
    };

    private static double? ConvertToDouble(object? v) => v switch
    {
        double d => d,
        float f => f,
        int i => i,
        long l => l,
        null => null,
        _ => Convert.ToDouble(v)
    };

    private static decimal? ConvertToDecimal(object? v) => v switch
    {
        decimal dec => dec,
        double d => Convert.ToDecimal(d),
        float f => Convert.ToDecimal(f),
        int i => i,
        long l => l,
        null => null,
        _ => Convert.ToDecimal(v)
    };

    private static bool? ConvertToBool(object? v) => v switch
    {
        bool b => b,
        null => null,
        _ => Convert.ToBoolean(v)
    };

    private static DateTimeOffset? ConvertToDateTimeOffset(object? v) => v switch
    {
        DateTimeOffset dto => dto,
        DateTime dt => new DateTimeOffset(dt),
        string s when DateTimeOffset.TryParse(s, out var parsed) => parsed,
        _ => null
    };

    private async Task<List<ExportedFile>> ExportToJsonlAsync(
        List<SourceFile> sourceFiles,
        ExportRequest request,
        ExportProfile profile,
        CancellationToken ct)
    {
        var exportedFiles = new List<ExportedFile>();

        foreach (var sourceFile in sourceFiles)
        {
            var outputFileName = Path.GetFileName(sourceFile.Path);
            if (sourceFile.IsCompressed)
            {
                outputFileName = outputFileName[..^3]; // Remove .gz
            }

            var outputPath = Path.Combine(request.OutputDirectory, outputFileName);

            // Copy and optionally decompress
            if (sourceFile.IsCompressed && profile.Compression.Type == CompressionType.None)
            {
                await using var input = new GZipStream(
                    File.OpenRead(sourceFile.Path), CompressionMode.Decompress);
                await using var output = File.Create(outputPath);
                await input.CopyToAsync(output, ct);
            }
            else
            {
                File.Copy(sourceFile.Path, outputPath, request.OverwriteExisting);
            }

            var recordCount = await CountRecordsAsync(outputPath, ct);
            var fileInfo = new FileInfo(outputPath);

            exportedFiles.Add(new ExportedFile
            {
                Path = outputPath,
                RelativePath = Path.GetFileName(outputPath),
                Symbol = sourceFile.Symbol,
                EventType = sourceFile.EventType,
                Format = "jsonl",
                SizeBytes = fileInfo.Length,
                RecordCount = recordCount,
                ChecksumSha256 = await ComputeChecksumAsync(outputPath, ct)
            });
        }

        return exportedFiles;
    }

    private async Task<List<ExportedFile>> ExportToLeanAsync(
        List<SourceFile> sourceFiles,
        ExportRequest request,
        ExportProfile profile,
        CancellationToken ct)
    {
        // Lean format: data/{security_type}/{market}/{resolution}/{symbol}/{date}_{type}.zip
        var exportedFiles = new List<ExportedFile>();

        var grouped = sourceFiles.GroupBy(f => f.Symbol);

        foreach (var symbolGroup in grouped)
        {
            var symbol = symbolGroup.Key?.ToLowerInvariant() ?? "unknown";
            var symbolDir = Path.Combine(request.OutputDirectory, "equity", "usa", "tick", symbol);
            Directory.CreateDirectory(symbolDir);

            foreach (var sourceFile in symbolGroup)
            {
                var date = sourceFile.Date ?? DateTime.UtcNow;
                var eventType = sourceFile.EventType?.ToLowerInvariant() ?? "trade";
                var zipFileName = $"{date:yyyyMMdd}_{eventType}.zip";
                var zipPath = Path.Combine(symbolDir, zipFileName);

                await using var zipStream = File.Create(zipPath);
                using var archive = new ZipArchive(zipStream, ZipArchiveMode.Create);

                var entry = archive.CreateEntry($"{date:yyyyMMdd}_{eventType}.csv");
                await using var entryStream = entry.Open();
                await using var writer = new StreamWriter(entryStream);

                var recordCount = 0L;
                await foreach (var record in ReadJsonlRecordsAsync(sourceFile.Path, ct))
                {
                    // Convert to Lean format: Timestamp,Price,Size
                    if (record.TryGetValue("Timestamp", out var ts) &&
                        record.TryGetValue("Price", out var price) &&
                        record.TryGetValue("Size", out var size))
                    {
                        await writer.WriteLineAsync($"{ts},{price},{size}");
                        recordCount++;
                    }
                }

                exportedFiles.Add(new ExportedFile
                {
                    Path = zipPath,
                    RelativePath = Path.GetRelativePath(request.OutputDirectory, zipPath),
                    Symbol = symbol,
                    EventType = eventType,
                    Format = "lean",
                    SizeBytes = new FileInfo(zipPath).Length,
                    RecordCount = recordCount
                });
            }
        }

        return exportedFiles;
    }

    private async Task<List<ExportedFile>> ExportToSqlAsync(
        List<SourceFile> sourceFiles,
        ExportRequest request,
        ExportProfile profile,
        CancellationToken ct)
    {
        var exportedFiles = new List<ExportedFile>();

        // Generate DDL
        var ddlPath = Path.Combine(request.OutputDirectory, "create_tables.sql");
        await File.WriteAllTextAsync(ddlPath, GenerateDdl(request.EventTypes), ct);

        // Generate INSERT statements
        foreach (var sourceFile in sourceFiles)
        {
            var tableName = $"market_{sourceFile.EventType?.ToLowerInvariant() ?? "data"}";
            var sqlPath = Path.Combine(
                request.OutputDirectory,
                $"{sourceFile.Symbol}_{sourceFile.EventType}.sql");

            await using var writer = new StreamWriter(sqlPath);
            var recordCount = 0L;

            await foreach (var record in ReadJsonlRecordsAsync(sourceFile.Path, ct))
            {
                var columns = string.Join(", ", record.Keys);
                var values = string.Join(", ", record.Values.Select(v => SqlEscape(v)));
                await writer.WriteLineAsync($"INSERT INTO {tableName} ({columns}) VALUES ({values});");
                recordCount++;
            }

            exportedFiles.Add(new ExportedFile
            {
                Path = sqlPath,
                RelativePath = Path.GetFileName(sqlPath),
                Symbol = sourceFile.Symbol,
                EventType = sourceFile.EventType,
                Format = "sql",
                SizeBytes = new FileInfo(sqlPath).Length,
                RecordCount = recordCount
            });
        }

        return exportedFiles;
    }

    private async Task<List<ExportedFile>> ExportToXlsxAsync(
        List<SourceFile> sourceFiles,
        ExportRequest request,
        ExportProfile profile,
        CancellationToken ct)
    {
        var exportedFiles = new List<ExportedFile>();

        // Group by symbol if requested
        foreach (var group in GroupBySymbolIfRequired(sourceFiles, profile.SplitBySymbol))
        {
            var symbol = group.Key ?? "combined";
            var outputPath = Path.Combine(
                request.OutputDirectory,
                $"{symbol}_{DateTime.UtcNow:yyyyMMdd}.xlsx");

            var recordCount = await CreateXlsxFileAsync(outputPath, group.ToList(), profile, ct);

            var fileInfo = new FileInfo(outputPath);
            exportedFiles.Add(new ExportedFile
            {
                Path = outputPath,
                RelativePath = Path.GetFileName(outputPath),
                Symbol = symbol,
                Format = "xlsx",
                SizeBytes = fileInfo.Length,
                RecordCount = recordCount,
                ChecksumSha256 = await ComputeChecksumAsync(outputPath, ct)
            });
        }

        return exportedFiles;
    }

    private async Task<long> CreateXlsxFileAsync(
        string outputPath,
        List<SourceFile> sourceFiles,
        ExportProfile profile,
        CancellationToken ct)
    {
        var recordCount = 0L;

        await using var zipStream = new FileStream(outputPath, FileMode.Create);
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Create);

        // Create [Content_Types].xml
        var contentTypesEntry = archive.CreateEntry("[Content_Types].xml");
        await using (var stream = contentTypesEntry.Open())
        await using (var writer = new StreamWriter(stream, Encoding.UTF8))
        {
            await writer.WriteAsync(GetContentTypesXml());
        }

        // Create _rels/.rels
        var relsEntry = archive.CreateEntry("_rels/.rels");
        await using (var stream = relsEntry.Open())
        await using (var writer = new StreamWriter(stream, Encoding.UTF8))
        {
            await writer.WriteAsync(GetRelsXml());
        }

        // Create xl/workbook.xml
        var workbookEntry = archive.CreateEntry("xl/workbook.xml");
        await using (var stream = workbookEntry.Open())
        await using (var writer = new StreamWriter(stream, Encoding.UTF8))
        {
            await writer.WriteAsync(GetWorkbookXml());
        }

        // Create xl/_rels/workbook.xml.rels
        var workbookRelsEntry = archive.CreateEntry("xl/_rels/workbook.xml.rels");
        await using (var stream = workbookRelsEntry.Open())
        await using (var writer = new StreamWriter(stream, Encoding.UTF8))
        {
            await writer.WriteAsync(GetWorkbookRelsXml());
        }

        // Create xl/styles.xml (minimal styles)
        var stylesEntry = archive.CreateEntry("xl/styles.xml");
        await using (var stream = stylesEntry.Open())
        await using (var writer = new StreamWriter(stream, Encoding.UTF8))
        {
            await writer.WriteAsync(GetStylesXml());
        }

        // Create shared strings for text values
        var sharedStrings = new List<string>();
        var sharedStringIndex = new Dictionary<string, int>();

        // Collect all records first
        var allRecords = new List<Dictionary<string, object?>>();
        foreach (var sourceFile in sourceFiles)
        {
            await foreach (var record in ReadJsonlRecordsAsync(sourceFile.Path, ct))
            {
                allRecords.Add(record);
                recordCount++;

                // Respect Excel row limit (profile.MaxRecordsPerFile)
                if (profile.MaxRecordsPerFile.HasValue && recordCount >= profile.MaxRecordsPerFile.Value)
                {
                    _log.Warning("Export truncated at {MaxRecords} records due to Excel row limit",
                        profile.MaxRecordsPerFile.Value);
                    break;
                }
            }
            if (profile.MaxRecordsPerFile.HasValue && recordCount >= profile.MaxRecordsPerFile.Value)
                break;
        }

        if (allRecords.Count == 0)
        {
            // Create empty worksheet
            var emptySheetEntry = archive.CreateEntry("xl/worksheets/sheet1.xml");
            await using (var stream = emptySheetEntry.Open())
            await using (var writer = new StreamWriter(stream, Encoding.UTF8))
            {
                await writer.WriteAsync(GetEmptySheetXml());
            }

            // Create empty shared strings
            var emptyStringsEntry = archive.CreateEntry("xl/sharedStrings.xml");
            await using (var stream = emptyStringsEntry.Open())
            await using (var writer = new StreamWriter(stream, Encoding.UTF8))
            {
                await writer.WriteAsync(GetSharedStringsXml(new List<string>()));
            }

            return 0;
        }

        // Build shared strings and sheet content
        var headers = allRecords[0].Keys.ToList();
        foreach (var header in headers)
        {
            if (!sharedStringIndex.ContainsKey(header))
            {
                sharedStringIndex[header] = sharedStrings.Count;
                sharedStrings.Add(header);
            }
        }

        var sheetXml = new StringBuilder();
        sheetXml.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        sheetXml.AppendLine("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">");
        sheetXml.AppendLine("<sheetData>");

        // Header row
        sheetXml.AppendLine("<row r=\"1\">");
        for (int col = 0; col < headers.Count; col++)
        {
            var cellRef = GetCellReference(col, 0);
            var stringIndex = sharedStringIndex[headers[col]];
            sheetXml.AppendLine($"<c r=\"{cellRef}\" t=\"s\"><v>{stringIndex}</v></c>");
        }
        sheetXml.AppendLine("</row>");

        // Data rows
        for (int rowIndex = 0; rowIndex < allRecords.Count; rowIndex++)
        {
            var record = allRecords[rowIndex];
            var rowNum = rowIndex + 2; // 1-indexed, after header
            sheetXml.AppendLine($"<row r=\"{rowNum}\">");

            for (int col = 0; col < headers.Count; col++)
            {
                var header = headers[col];
                var cellRef = GetCellReference(col, rowIndex + 1);

                if (record.TryGetValue(header, out var value) && value != null)
                {
                    var cellXml = GetCellXml(cellRef, value, sharedStrings, sharedStringIndex);
                    sheetXml.Append(cellXml);
                }
            }
            sheetXml.AppendLine("</row>");
        }

        sheetXml.AppendLine("</sheetData>");
        sheetXml.AppendLine("</worksheet>");

        // Write worksheet
        var sheetEntry = archive.CreateEntry("xl/worksheets/sheet1.xml");
        await using (var stream = sheetEntry.Open())
        await using (var writer = new StreamWriter(stream, Encoding.UTF8))
        {
            await writer.WriteAsync(sheetXml.ToString());
        }

        // Write shared strings
        var sharedStringsEntry = archive.CreateEntry("xl/sharedStrings.xml");
        await using (var stream = sharedStringsEntry.Open())
        await using (var writer = new StreamWriter(stream, Encoding.UTF8))
        {
            await writer.WriteAsync(GetSharedStringsXml(sharedStrings));
        }

        return recordCount;
    }

    private static string GetCellReference(int col, int row)
    {
        // Convert column index to Excel column letter (0=A, 1=B, ..., 26=AA, etc.)
        var colName = new StringBuilder();
        var colNum = col;
        while (colNum >= 0)
        {
            colName.Insert(0, (char)('A' + (colNum % 26)));
            colNum = colNum / 26 - 1;
        }
        return $"{colName}{row + 1}";
    }

    private static string GetCellXml(
        string cellRef,
        object value,
        List<string> sharedStrings,
        Dictionary<string, int> sharedStringIndex)
    {
        // Handle different value types
        return value switch
        {
            // Numbers - inline value
            int or long or float or double or decimal =>
                $"<c r=\"{cellRef}\"><v>{value}</v></c>",

            // Booleans - Excel uses 0/1
            bool b =>
                $"<c r=\"{cellRef}\" t=\"b\"><v>{(b ? "1" : "0")}</v></c>",

            // DateTime - Excel serial date
            DateTime dt =>
                $"<c r=\"{cellRef}\" s=\"1\"><v>{dt.ToOADate()}</v></c>",

            DateTimeOffset dto =>
                $"<c r=\"{cellRef}\" s=\"1\"><v>{dto.DateTime.ToOADate()}</v></c>",

            // Strings - use shared strings table
            string s => GetStringCellXml(cellRef, s, sharedStrings, sharedStringIndex),

            // Everything else - convert to string
            _ => GetStringCellXml(cellRef, value.ToString() ?? "", sharedStrings, sharedStringIndex)
        };
    }

    private static string GetStringCellXml(
        string cellRef,
        string value,
        List<string> sharedStrings,
        Dictionary<string, int> sharedStringIndex)
    {
        var escapedValue = EscapeXml(value);
        if (!sharedStringIndex.TryGetValue(escapedValue, out var index))
        {
            index = sharedStrings.Count;
            sharedStringIndex[escapedValue] = index;
            sharedStrings.Add(escapedValue);
        }
        return $"<c r=\"{cellRef}\" t=\"s\"><v>{index}</v></c>";
    }

    private static string EscapeXml(string value)
    {
        return value
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&apos;");
    }

    private static string GetContentTypesXml() => """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
            <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
            <Default Extension="xml" ContentType="application/xml"/>
            <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
            <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
            <Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>
            <Override PartName="/xl/sharedStrings.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sharedStrings+xml"/>
        </Types>
        """;

    private static string GetRelsXml() => """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
            <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
        </Relationships>
        """;

    private static string GetWorkbookXml() => """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
            <sheets>
                <sheet name="Data" sheetId="1" r:id="rId1"/>
            </sheets>
        </workbook>
        """;

    private static string GetWorkbookRelsXml() => """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
            <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
            <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>
            <Relationship Id="rId3" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/sharedStrings" Target="sharedStrings.xml"/>
        </Relationships>
        """;

    private static string GetStylesXml() => """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
            <numFmts count="1">
                <numFmt numFmtId="164" formatCode="yyyy-mm-dd hh:mm:ss"/>
            </numFmts>
            <fonts count="1">
                <font><sz val="11"/><name val="Calibri"/></font>
            </fonts>
            <fills count="1">
                <fill><patternFill patternType="none"/></fill>
            </fills>
            <borders count="1">
                <border><left/><right/><top/><bottom/></border>
            </borders>
            <cellXfs count="2">
                <xf numFmtId="0" fontId="0" fillId="0" borderId="0"/>
                <xf numFmtId="164" fontId="0" fillId="0" borderId="0" applyNumberFormat="1"/>
            </cellXfs>
        </styleSheet>
        """;

    private static string GetEmptySheetXml() => """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
            <sheetData/>
        </worksheet>
        """;

    private static string GetSharedStringsXml(List<string> strings) =>
        $"""
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <sst xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" count="{strings.Count}" uniqueCount="{strings.Count}">
        {string.Join(Environment.NewLine, strings.Select(s => $"<si><t>{s}</t></si>"))}
        </sst>
        """;

    private string GenerateDdl(string[] eventTypes)
    {
        var sb = new StringBuilder();
        sb.AppendLine("-- Market Data Tables");
        sb.AppendLine("-- Generated by MarketDataCollector AnalysisExportService");
        sb.AppendLine();

        foreach (var eventType in eventTypes)
        {
            switch (eventType.ToLowerInvariant())
            {
                case "trade":
                    sb.AppendLine(@"
CREATE TABLE IF NOT EXISTS market_trade (
    id SERIAL PRIMARY KEY,
    timestamp TIMESTAMPTZ NOT NULL,
    symbol VARCHAR(20) NOT NULL,
    price DECIMAL(18,8) NOT NULL,
    size BIGINT NOT NULL,
    side VARCHAR(10),
    exchange VARCHAR(20),
    trade_id VARCHAR(50),
    conditions TEXT[],
    created_at TIMESTAMPTZ DEFAULT NOW()
);
CREATE INDEX IF NOT EXISTS idx_trade_symbol_time ON market_trade(symbol, timestamp);
");
                    break;
                case "bbo":
                case "bboquote":
                case "quote":
                    sb.AppendLine(@"
CREATE TABLE IF NOT EXISTS market_quote (
    id SERIAL PRIMARY KEY,
    timestamp TIMESTAMPTZ NOT NULL,
    symbol VARCHAR(20) NOT NULL,
    bid_price DECIMAL(18,8),
    bid_size BIGINT,
    ask_price DECIMAL(18,8),
    ask_size BIGINT,
    exchange VARCHAR(20),
    created_at TIMESTAMPTZ DEFAULT NOW()
);
CREATE INDEX IF NOT EXISTS idx_quote_symbol_time ON market_quote(symbol, timestamp);
");
                    break;
            }
        }

        return sb.ToString();
    }

    private async Task<string> GenerateDataDictionaryAsync(
        string outputDir,
        string[] eventTypes,
        ExportProfile profile,
        CancellationToken ct)
    {
        var dictPath = Path.Combine(outputDir, "data_dictionary.md");

        var sb = new StringBuilder();
        sb.AppendLine("# Data Dictionary");
        sb.AppendLine();
        sb.AppendLine($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"Export Profile: {profile.Name}");
        sb.AppendLine($"Format: {profile.Format}");
        sb.AppendLine();

        foreach (var eventType in eventTypes)
        {
            sb.AppendLine($"## {eventType} Event");
            sb.AppendLine();
            sb.AppendLine("| Field | Type | Description | Example |");
            sb.AppendLine("|-------|------|-------------|---------|");

            switch (eventType.ToLowerInvariant())
            {
                case "trade":
                    sb.AppendLine("| Timestamp | datetime64[ns] | Event timestamp in UTC | 2026-01-03T14:30:00.123456789Z |");
                    sb.AppendLine("| Symbol | string | Ticker symbol | AAPL |");
                    sb.AppendLine("| Price | decimal(18,8) | Trade price | 185.2500 |");
                    sb.AppendLine("| Size | int64 | Trade size in shares | 100 |");
                    sb.AppendLine("| Side | enum | Aggressor side (Buy/Sell/Unknown) | Buy |");
                    sb.AppendLine("| Exchange | string | Exchange code | XNAS |");
                    sb.AppendLine("| TradeId | string | Unique trade identifier | T123456789 |");
                    sb.AppendLine("| Conditions | string[] | Trade condition codes | [\"@\", \"F\"] |");
                    break;

                case "bboquote":
                case "quote":
                    sb.AppendLine("| Timestamp | datetime64[ns] | Event timestamp in UTC | 2026-01-03T14:30:00.123456789Z |");
                    sb.AppendLine("| Symbol | string | Ticker symbol | AAPL |");
                    sb.AppendLine("| BidPrice | decimal(18,8) | Best bid price | 185.2400 |");
                    sb.AppendLine("| BidSize | int64 | Bid size in shares | 500 |");
                    sb.AppendLine("| AskPrice | decimal(18,8) | Best ask price | 185.2600 |");
                    sb.AppendLine("| AskSize | int64 | Ask size in shares | 300 |");
                    sb.AppendLine("| Exchange | string | Exchange code | XNAS |");
                    break;
            }

            sb.AppendLine();
        }

        await File.WriteAllTextAsync(dictPath, sb.ToString(), ct);
        return dictPath;
    }

    private async Task<string> GenerateLoaderScriptAsync(
        string outputDir,
        ExportProfile profile,
        List<ExportedFile> files,
        CancellationToken ct)
    {
        string scriptPath;
        string script;

        switch (profile.TargetTool.ToLowerInvariant())
        {
            case "python":
                scriptPath = Path.Combine(outputDir, "load_data.py");
                script = GeneratePythonLoader(files, profile);
                break;
            case "r":
                scriptPath = Path.Combine(outputDir, "load_data.R");
                script = GenerateRLoader(files, profile);
                break;
            case "postgresql":
                scriptPath = Path.Combine(outputDir, "load_data.sh");
                script = GeneratePostgresLoader(files, profile);
                break;
            default:
                return string.Empty;
        }

        await File.WriteAllTextAsync(scriptPath, script, ct);
        return scriptPath;
    }

    private string GeneratePythonLoader(List<ExportedFile> files, ExportProfile profile)
    {
        var sb = new StringBuilder();
        sb.AppendLine("#!/usr/bin/env python3");
        sb.AppendLine("\"\"\"");
        sb.AppendLine("Market Data Loader");
        sb.AppendLine($"Generated by MarketDataCollector - {profile.Name}");
        sb.AppendLine("\"\"\"");
        sb.AppendLine();
        sb.AppendLine("import pandas as pd");
        sb.AppendLine("from pathlib import Path");
        sb.AppendLine();
        sb.AppendLine("DATA_DIR = Path(__file__).parent");
        sb.AppendLine();

        if (profile.Format == ExportFormat.Parquet)
        {
            sb.AppendLine("""
def load_trades(symbol: str = None) -> pd.DataFrame:
    \"\"\"Load trade data into a pandas DataFrame.\"\"\"
    pattern = f"{symbol}_*.parquet" if symbol else "*.parquet"
    files = list(DATA_DIR.glob(pattern))
    if not files:
        raise FileNotFoundError(f"No parquet files found matching {pattern}")
    return pd.concat([pd.read_parquet(f) for f in files], ignore_index=True)


def load_quotes(symbol: str = None) -> pd.DataFrame:
    \"\"\"Load quote data into a pandas DataFrame.\"\"\"
    pattern = f"{symbol}_*.parquet" if symbol else "*quote*.parquet"
    files = list(DATA_DIR.glob(pattern))
    if not files:
        raise FileNotFoundError(f"No quote files found matching {pattern}")
    return pd.concat([pd.read_parquet(f) for f in files], ignore_index=True)

""");
        }
        else if (profile.Format == ExportFormat.Csv)
        {
            sb.AppendLine("""
def load_trades(symbol: str = None) -> pd.DataFrame:
    \"\"\"Load trade data into a pandas DataFrame.\"\"\"
    pattern = f"{symbol}_*.csv" if symbol else "*.csv"
    files = list(DATA_DIR.glob(pattern))
    if not files:
        raise FileNotFoundError(f"No CSV files found matching {pattern}")
    return pd.concat([pd.read_csv(f, parse_dates=['Timestamp']) for f in files], ignore_index=True)

""");
        }

        sb.AppendLine("""
if __name__ == "__main__":
    # Example usage
    df = load_trades()
    print(f"Loaded {len(df):,} records")
    print(df.head())
""");

        return sb.ToString();
    }

    private string GenerateRLoader(List<ExportedFile> files, ExportProfile profile)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Market Data Loader");
        sb.AppendLine($"# Generated by MarketDataCollector - {profile.Name}");
        sb.AppendLine();
        sb.AppendLine("library(tidyverse)");
        sb.AppendLine("library(lubridate)");
        sb.AppendLine();
        sb.AppendLine("data_dir <- dirname(rstudioapi::getActiveDocumentContext()$path)");
        sb.AppendLine();
        sb.AppendLine("""
load_trades <- function(symbol = NULL) {
  pattern <- if (!is.null(symbol)) paste0(symbol, "_.*\\.csv$") else ".*\\.csv$"
  files <- list.files(data_dir, pattern = pattern, full.names = TRUE)

  if (length(files) == 0) {
    stop("No CSV files found")
  }

  df <- files %>%
    map_dfr(read_csv) %>%
    mutate(Timestamp = ymd_hms(Timestamp))

  return(df)
}

# Example usage
# trades <- load_trades("AAPL")
# head(trades)
""");

        return sb.ToString();
    }

    private string GeneratePostgresLoader(List<ExportedFile> files, ExportProfile profile)
    {
        var sb = new StringBuilder();
        sb.AppendLine("#!/bin/bash");
        sb.AppendLine("# Market Data PostgreSQL Loader");
        sb.AppendLine($"# Generated by MarketDataCollector - {profile.Name}");
        sb.AppendLine();
        sb.AppendLine("DB_NAME=${1:-marketdata}");
        sb.AppendLine("DB_USER=${2:-postgres}");
        sb.AppendLine("SCRIPT_DIR=$(dirname \"$0\")");
        sb.AppendLine();
        sb.AppendLine("# Create tables");
        sb.AppendLine("psql -U $DB_USER -d $DB_NAME -f \"$SCRIPT_DIR/create_tables.sql\"");
        sb.AppendLine();
        sb.AppendLine("# Load data");

        foreach (var file in files.Where(f => f.Format == "csv"))
        {
            var tableName = $"market_{file.EventType?.ToLowerInvariant() ?? "data"}";
            sb.AppendLine($"psql -U $DB_USER -d $DB_NAME -c \"\\copy {tableName} FROM '$SCRIPT_DIR/{file.RelativePath}' WITH CSV HEADER\"");
        }

        sb.AppendLine();
        sb.AppendLine("echo \"Data loaded successfully\"");

        return sb.ToString();
    }

    private async IAsyncEnumerable<Dictionary<string, object?>> ReadJsonlRecordsAsync(
        string path,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        Stream stream = File.OpenRead(path);
        if (path.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
        {
            stream = new GZipStream(stream, CompressionMode.Decompress);
        }

        await using (stream)
        using (var reader = new StreamReader(stream))
        {
            while (!reader.EndOfStream && !ct.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(line)) continue;

                var doc = JsonDocument.Parse(line);
                var dict = new Dictionary<string, object?>();

                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    dict[prop.Name] = prop.Value.ValueKind switch
                    {
                        JsonValueKind.String => prop.Value.GetString(),
                        JsonValueKind.Number => prop.Value.GetDouble(),
                        JsonValueKind.True => true,
                        JsonValueKind.False => false,
                        JsonValueKind.Null => null,
                        _ => prop.Value.GetRawText()
                    };
                }

                yield return dict;
            }
        }
    }

    private async Task<long> CountRecordsAsync(string path, CancellationToken ct)
    {
        long count = 0;
        await foreach (var _ in ReadJsonlRecordsAsync(path, ct))
        {
            count++;
        }
        return count;
    }

    private async Task<string> ComputeChecksumAsync(string path, CancellationToken ct)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, ct);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string EscapeCsvValue(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
        return value;
    }

    private static string SqlEscape(object? value)
    {
        if (value == null) return "NULL";
        if (value is string s) return $"'{s.Replace("'", "''")}'";
        if (value is bool b) return b ? "TRUE" : "FALSE";
        if (value is DateTime dt) return $"'{dt:yyyy-MM-dd HH:mm:ss.ffffff}'";
        return value.ToString() ?? "NULL";
    }

    private static int CountTradingDays(DateTime start, DateTime end)
    {
        var count = 0;
        for (var date = start.Date; date <= end.Date; date = date.AddDays(1))
        {
            if (date.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday)
                count++;
        }
        return count;
    }

    /// <summary>
    /// Groups source files by symbol if requested, otherwise returns a single group.
    /// </summary>
    private static IEnumerable<IGrouping<string?, SourceFile>> GroupBySymbolIfRequired(
        List<SourceFile> files, bool splitBySymbol) =>
        splitBySymbol
            ? files.GroupBy(f => f.Symbol)
            : files.GroupBy(_ => (string?)"combined");

    private class SourceFile
    {
        public string Path { get; set; } = string.Empty;
        public string? Symbol { get; set; }
        public string? EventType { get; set; }
        public DateTime? Date { get; set; }
        public bool IsCompressed { get; set; }
    }
}
