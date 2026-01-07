using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using MarketDataCollector.Application.Logging;
using Serilog;

namespace MarketDataCollector.Storage.Export;

/// <summary>
/// Service for exporting collected market data in analysis-ready formats.
/// Supports multiple output formats optimized for external analysis tools.
/// </summary>
public class AnalysisExportService
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
        if (profile == null)
        {
            return ExportResult.CreateFailure(request.ProfileId, $"Unknown profile: {request.ProfileId}");
        }

        var result = ExportResult.CreateSuccess(profile.Id, request.OutputDirectory);

        try
        {
            _log.Information("Starting export with profile {ProfileId} to {OutputDir}",
                profile.Id, request.OutputDirectory);

            // Ensure output directory exists
            Directory.CreateDirectory(request.OutputDirectory);

            // Find source files to export
            var sourceFiles = FindSourceFiles(request);
            if (sourceFiles.Count == 0)
            {
                result.Warnings = result.Warnings.Append("No source data found for the specified criteria").ToArray();
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
                default:
                    throw new NotSupportedException($"Format {profile.Format} is not yet implemented");
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
        var files = new List<SourceFile>();

        // Search for JSONL files in the data root
        if (!Directory.Exists(_dataRoot)) return files;

        var patterns = new[] { "*.jsonl", "*.jsonl.gz" };
        foreach (var pattern in patterns)
        {
            foreach (var file in Directory.GetFiles(_dataRoot, pattern, SearchOption.AllDirectories))
            {
                var parsed = ParseFileName(file);
                if (parsed == null) continue;

                // Filter by symbol
                if (request.Symbols != null && request.Symbols.Length > 0)
                {
                    if (!request.Symbols.Contains(parsed.Symbol, StringComparer.OrdinalIgnoreCase))
                        continue;
                }

                // Filter by event type
                if (request.EventTypes != null && request.EventTypes.Length > 0)
                {
                    if (!request.EventTypes.Contains(parsed.EventType, StringComparer.OrdinalIgnoreCase))
                        continue;
                }

                // Filter by date (if available)
                if (parsed.Date.HasValue)
                {
                    if (parsed.Date.Value < request.StartDate.Date || parsed.Date.Value > request.EndDate.Date)
                        continue;
                }

                files.Add(parsed);
            }
        }

        return files.OrderBy(f => f.Symbol).ThenBy(f => f.Date).ToList();
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

        var grouped = profile.SplitBySymbol
            ? sourceFiles.GroupBy(f => f.Symbol)
            : new[] { sourceFiles.AsEnumerable() }.Select(g => g);

        foreach (var group in grouped)
        {
            var symbol = profile.SplitBySymbol ? group.First().Symbol : "combined";
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

        // For Parquet, we'll create a simple CSV as a placeholder
        // In a real implementation, you'd use Parquet.Net or PyArrow
        var grouped = profile.SplitBySymbol
            ? sourceFiles.GroupBy(f => f.Symbol)
            : new[] { sourceFiles.AsEnumerable() }.Select(g => g);

        foreach (var group in grouped)
        {
            var symbol = profile.SplitBySymbol ? group.First().Symbol : "combined";
            var outputPath = Path.Combine(
                request.OutputDirectory,
                $"{symbol}_{DateTime.UtcNow:yyyyMMdd}.parquet");

            // For now, create a JSON manifest that describes what would be in the Parquet file
            var records = new List<Dictionary<string, object?>>();
            long recordCount = 0;

            foreach (var sourceFile in group)
            {
                await foreach (var record in ReadJsonlRecordsAsync(sourceFile.Path, ct))
                {
                    records.Add(record);
                    recordCount++;

                    // Limit records in memory
                    if (records.Count >= 10000)
                    {
                        await WriteParquetPlaceholderAsync(outputPath, records, recordCount > 10000);
                        records.Clear();
                    }
                }
            }

            if (records.Count > 0)
            {
                await WriteParquetPlaceholderAsync(outputPath, records, recordCount > records.Count);
            }

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

    private async Task WriteParquetPlaceholderAsync(
        string path,
        List<Dictionary<string, object?>> records,
        bool append)
    {
        // Placeholder implementation - writes JSON instead of Parquet
        // Real implementation would use Parquet.Net
        var mode = append ? FileMode.Append : FileMode.Create;
        await using var fs = new FileStream(path, mode, FileAccess.Write);
        await using var writer = new StreamWriter(fs);

        foreach (var record in records)
        {
            await writer.WriteLineAsync(JsonSerializer.Serialize(record));
        }
    }

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
            if (date.DayOfWeek != DayOfWeek.Saturday && date.DayOfWeek != DayOfWeek.Sunday)
                count++;
        }
        return count;
    }

    private class SourceFile
    {
        public string Path { get; set; } = string.Empty;
        public string? Symbol { get; set; }
        public string? EventType { get; set; }
        public DateTime? Date { get; set; }
        public bool IsCompressed { get; set; }
    }
}
