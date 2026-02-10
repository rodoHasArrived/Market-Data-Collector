using System.IO.Compression;
using System.Text;
using Serilog;

namespace MarketDataCollector.Storage.Export;

/// <summary>
/// Handles XLSX (Excel) file generation for the analysis export pipeline.
/// All methods are static to allow extraction from <see cref="AnalysisExportService"/>
/// without requiring instance state.
/// </summary>
internal static class XlsxExportFormatter
{
    /// <summary>
    /// Delegate that reads JSONL records from a file path, yielding one dictionary per record.
    /// </summary>
    internal delegate IAsyncEnumerable<Dictionary<string, object?>> RecordReaderDelegate(
        string path, CancellationToken ct);

    /// <summary>
    /// Minimal representation of a source file for XLSX export.
    /// Decouples the formatter from the private SourceFile class in AnalysisExportService.
    /// </summary>
    internal sealed class XlsxSourceFile
    {
        public string Path { get; init; } = string.Empty;
    }

    /// <summary>
    /// Creates an XLSX file from the given source files, using the provided record reader
    /// to stream JSONL records from each source file.
    /// </summary>
    /// <param name="outputPath">Full path for the output .xlsx file.</param>
    /// <param name="sourceFiles">Source files to read records from.</param>
    /// <param name="maxRecordsPerFile">Optional maximum record count (Excel row limit).</param>
    /// <param name="readRecords">Delegate that reads JSONL records from a file path.</param>
    /// <param name="log">Optional logger for warnings.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The total number of records written to the file.</returns>
    internal static async Task<long> CreateXlsxFileAsync(
        string outputPath,
        IReadOnlyList<XlsxSourceFile> sourceFiles,
        long? maxRecordsPerFile,
        RecordReaderDelegate readRecords,
        ILogger? log,
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
            await foreach (var record in readRecords(sourceFile.Path, ct))
            {
                allRecords.Add(record);
                recordCount++;

                // Respect Excel row limit (maxRecordsPerFile)
                if (maxRecordsPerFile.HasValue && recordCount >= maxRecordsPerFile.Value)
                {
                    log?.Warning("Export truncated at {MaxRecords} records due to Excel row limit",
                        maxRecordsPerFile.Value);
                    break;
                }
            }
            if (maxRecordsPerFile.HasValue && recordCount >= maxRecordsPerFile.Value)
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

    /// <summary>
    /// Converts a zero-based column index and zero-based row index to an Excel cell reference
    /// (e.g., column 0, row 0 becomes "A1").
    /// </summary>
    internal static string GetCellReference(int col, int row)
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

    /// <summary>
    /// Produces the XML fragment for a single cell, choosing the appropriate type encoding
    /// (number, boolean, datetime serial, or shared string).
    /// </summary>
    internal static string GetCellXml(
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

    /// <summary>
    /// Produces the XML fragment for a string-typed cell, registering the value in the shared
    /// strings table if it is not already present.
    /// </summary>
    internal static string GetStringCellXml(
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

    /// <summary>
    /// Escapes XML special characters in a string value.
    /// </summary>
    internal static string EscapeXml(string value)
    {
        return value
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&apos;");
    }

    /// <summary>
    /// Returns the [Content_Types].xml document required by the Open Packaging Convention.
    /// </summary>
    internal static string GetContentTypesXml() => """
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

    /// <summary>
    /// Returns the top-level _rels/.rels relationship file.
    /// </summary>
    internal static string GetRelsXml() => """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
            <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
        </Relationships>
        """;

    /// <summary>
    /// Returns the xl/workbook.xml document with a single "Data" sheet.
    /// </summary>
    internal static string GetWorkbookXml() => """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
            <sheets>
                <sheet name="Data" sheetId="1" r:id="rId1"/>
            </sheets>
        </workbook>
        """;

    /// <summary>
    /// Returns the xl/_rels/workbook.xml.rels relationship file linking the worksheet,
    /// styles, and shared strings parts.
    /// </summary>
    internal static string GetWorkbookRelsXml() => """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
            <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
            <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>
            <Relationship Id="rId3" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/sharedStrings" Target="sharedStrings.xml"/>
        </Relationships>
        """;

    /// <summary>
    /// Returns a minimal xl/styles.xml with a default cell format and a date/time
    /// number format (numFmtId 164).
    /// </summary>
    internal static string GetStylesXml() => """
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

    /// <summary>
    /// Returns a minimal empty worksheet XML document.
    /// </summary>
    internal static string GetEmptySheetXml() => """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
            <sheetData/>
        </worksheet>
        """;

    /// <summary>
    /// Returns the xl/sharedStrings.xml document containing all string values used in cells.
    /// </summary>
    internal static string GetSharedStringsXml(List<string> strings) =>
        $"""
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <sst xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" count="{strings.Count}" uniqueCount="{strings.Count}">
        {string.Join(Environment.NewLine, strings.Select(s => $"<si><t>{s}</t></si>"))}
        </sst>
        """;
}
