using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Apache.Arrow;
using Apache.Arrow.Ipc;
using Apache.Arrow.Types;
using Parquet;
using Parquet.Data;
using Parquet.Schema;

namespace MarketDataCollector.Storage.Export;

/// <summary>
/// Format-specific export methods (CSV, Parquet, JSONL, Lean, SQL, XLSX).
/// </summary>
public sealed partial class AnalysisExportService
{
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

    private async Task<List<ExportedFile>> ExportToArrowAsync(
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
                $"{symbol}_{DateTime.UtcNow:yyyyMMdd}.arrow");

            // Collect all records to determine schema and build columnar data
            var records = new List<Dictionary<string, object?>>();

            foreach (var sourceFile in group)
            {
                await foreach (var record in ReadJsonlRecordsAsync(sourceFile.Path, ct))
                {
                    records.Add(record);
                }
            }

            if (records.Count > 0)
            {
                await WriteArrowFileAsync(outputPath, records, ct);
            }
            else
            {
                await WriteEmptyArrowFileAsync(outputPath, ct);
            }

            var fileInfo = new FileInfo(outputPath);
            exportedFiles.Add(new ExportedFile
            {
                Path = outputPath,
                RelativePath = Path.GetFileName(outputPath),
                Symbol = symbol,
                Format = "arrow",
                SizeBytes = fileInfo.Length,
                RecordCount = records.Count,
                ChecksumSha256 = await ComputeChecksumAsync(outputPath, ct)
            });
        }

        return exportedFiles;
    }

    /// <summary>
    /// Writes records to an Apache Arrow IPC (Feather v2) file.
    /// Uses columnar layout for zero-copy reads in PyArrow, R arrow, Julia, and Spark.
    /// </summary>
    private async Task WriteArrowFileAsync(
        string path,
        List<Dictionary<string, object?>> records,
        CancellationToken ct)
    {
        if (records.Count == 0) return;

        var firstRecord = records[0];
        var columns = firstRecord.Keys.ToList();

        // Build Arrow schema
        var schemaBuilder = new Apache.Arrow.Schema.Builder();
        var arrowFields = new List<Apache.Arrow.Field>();
        foreach (var column in columns)
        {
            var value = firstRecord[column];
            var field = InferArrowField(column, value);
            arrowFields.Add(field);
            schemaBuilder.Field(field);
        }

        var schema = schemaBuilder.Build();

        // Build arrays for each column
        var arrays = new List<IArrowArray>();
        for (int colIdx = 0; colIdx < columns.Count; colIdx++)
        {
            var column = columns[colIdx];
            var fieldType = arrowFields[colIdx].DataType;
            var values = records.Select(r => r.TryGetValue(column, out var v) ? v : null).ToList();
            arrays.Add(BuildArrowArray(fieldType, values));
        }

        var batch = new RecordBatch(schema, arrays.ToArray(), records.Count);

        await using var stream = File.Create(path);
        using var writer = new ArrowFileWriter(stream, schema);
        await writer.WriteRecordBatchAsync(batch, ct);
        await writer.WriteEndAsync(ct);

        _log.Debug("Wrote {RecordCount} records to Arrow file: {Path}", records.Count, path);
    }

    /// <summary>
    /// Creates an empty Arrow IPC file with a minimal schema.
    /// </summary>
    private static async Task WriteEmptyArrowFileAsync(string path, CancellationToken ct)
    {
        var schema = new Apache.Arrow.Schema.Builder()
            .Field(new Apache.Arrow.Field("_empty", StringType.Default, nullable: true))
            .Build();

        await using var stream = File.Create(path);
        using var writer = new ArrowFileWriter(stream, schema);
        await writer.WriteRecordBatchAsync(
            new RecordBatch(schema, new IArrowArray[] { new StringArray.Builder().Build() }, 0), ct);
        await writer.WriteEndAsync(ct);
    }

    /// <summary>
    /// Infers the Arrow field type from a sample value.
    /// </summary>
    private static Apache.Arrow.Field InferArrowField(string name, object? value)
    {
        var dataType = value switch
        {
            int => Int32Type.Default as IArrowType,
            long => Int64Type.Default,
            float => FloatType.Default,
            double => DoubleType.Default,
            decimal => DoubleType.Default, // Arrow has no native decimal; use double
            bool => BooleanType.Default,
            DateTime => TimestampType.Default,
            DateTimeOffset => TimestampType.Default,
            _ => StringType.Default
        };

        return new Apache.Arrow.Field(name, dataType, nullable: true);
    }

    /// <summary>
    /// Builds an Arrow array from a list of values based on the target data type.
    /// </summary>
    private static IArrowArray BuildArrowArray(IArrowType dataType, List<object?> values)
    {
        switch (dataType)
        {
            case Int32Type:
            {
                var builder = new Int32Array.Builder();
                foreach (var v in values)
                {
                    if (v is null) builder.AppendNull();
                    else builder.Append(Convert.ToInt32(v));
                }
                return builder.Build();
            }
            case Int64Type:
            {
                var builder = new Int64Array.Builder();
                foreach (var v in values)
                {
                    if (v is null) builder.AppendNull();
                    else builder.Append(Convert.ToInt64(v));
                }
                return builder.Build();
            }
            case FloatType:
            {
                var builder = new FloatArray.Builder();
                foreach (var v in values)
                {
                    if (v is null) builder.AppendNull();
                    else builder.Append(Convert.ToSingle(v));
                }
                return builder.Build();
            }
            case DoubleType:
            {
                var builder = new DoubleArray.Builder();
                foreach (var v in values)
                {
                    if (v is null) builder.AppendNull();
                    else builder.Append(Convert.ToDouble(v));
                }
                return builder.Build();
            }
            case BooleanType:
            {
                var builder = new BooleanArray.Builder();
                foreach (var v in values)
                {
                    if (v is null) builder.AppendNull();
                    else builder.Append(Convert.ToBoolean(v));
                }
                return builder.Build();
            }
            case TimestampType:
            {
                var builder = new TimestampArray.Builder();
                foreach (var v in values)
                {
                    if (v is null)
                        builder.AppendNull();
                    else if (v is DateTimeOffset dto)
                        builder.Append(dto);
                    else if (v is DateTime dt)
                        builder.Append(new DateTimeOffset(dt, TimeSpan.Zero));
                    else if (v is string s && DateTimeOffset.TryParse(s, out var parsed))
                        builder.Append(parsed);
                    else
                        builder.AppendNull();
                }
                return builder.Build();
            }
            default: // StringType
            {
                var builder = new StringArray.Builder();
                foreach (var v in values)
                {
                    if (v is null) builder.AppendNull();
                    else builder.Append(v.ToString() ?? string.Empty);
                }
                return builder.Build();
            }
        }
    }

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
}
