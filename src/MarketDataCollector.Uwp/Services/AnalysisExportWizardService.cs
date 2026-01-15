using System.Text;
using System.Text.Json;

namespace MarketDataCollector.Uwp.Services;

/// <summary>
/// Service for guided analysis export wizard.
/// Provides tool-specific export profiles and generates ready-to-use exports.
/// </summary>
public sealed class AnalysisExportWizardService
{
    private readonly DataCompletenessService _completenessService;
    private readonly StorageAnalyticsService _storageService;
    private readonly ConfigService _configService;

    public AnalysisExportWizardService()
    {
        _completenessService = new DataCompletenessService();
        _storageService = new StorageAnalyticsService();
        _configService = new ConfigService();
    }

    /// <summary>
    /// Gets available export profiles for different analysis tools.
    /// </summary>
    public IReadOnlyList<ExportProfile> GetExportProfiles()
    {
        return new List<ExportProfile>
        {
            new()
            {
                Id = "python-pandas",
                Name = "Python / Pandas",
                Description = "Parquet files with appropriate dtypes for pandas DataFrame",
                Icon = "\uE943",
                OutputFormat = "Parquet",
                Compression = "snappy",
                IncludeLoaderCode = true,
                LoaderLanguage = "python",
                FileExtension = ".parquet",
                Features = new[] { "Efficient columnar storage", "Native pandas support", "Type preservation" }
            },
            new()
            {
                Id = "python-pytorch",
                Name = "Python / PyTorch",
                Description = "HDF5 files optimized for ML training pipelines",
                Icon = "\uE945",
                OutputFormat = "HDF5",
                Compression = "gzip",
                IncludeLoaderCode = true,
                LoaderLanguage = "python",
                FileExtension = ".h5",
                Features = new[] { "ML-ready format", "Chunked storage", "Feature scaling metadata" }
            },
            new()
            {
                Id = "r-dataframe",
                Name = "R / data.frame",
                Description = "CSV with proper formatting for R analysis",
                Icon = "\uE8A1",
                OutputFormat = "CSV",
                Compression = "none",
                IncludeLoaderCode = true,
                LoaderLanguage = "r",
                FileExtension = ".csv",
                Features = new[] { "R-compatible dates", "NA handling", "Factor encoding" }
            },
            new()
            {
                Id = "quantconnect-lean",
                Name = "QuantConnect Lean",
                Description = "Native Lean data format for backtesting",
                Icon = "\uE9D9",
                OutputFormat = "Lean",
                Compression = "zip",
                IncludeLoaderCode = false,
                LoaderLanguage = "csharp",
                FileExtension = ".zip",
                Features = new[] { "Direct Lean compatibility", "Multiple resolutions", "Corporate actions" }
            },
            new()
            {
                Id = "excel",
                Name = "Microsoft Excel",
                Description = "XLSX with multiple sheets and formatting",
                Icon = "\uE8D5",
                OutputFormat = "Excel",
                Compression = "none",
                IncludeLoaderCode = false,
                LoaderLanguage = "none",
                FileExtension = ".xlsx",
                Features = new[] { "Pivot-ready", "Charts included", "Summary statistics" }
            },
            new()
            {
                Id = "sql-postgres",
                Name = "PostgreSQL / TimescaleDB",
                Description = "SQL COPY format optimized for time-series databases",
                Icon = "\uE8F1",
                OutputFormat = "SQL",
                Compression = "gzip",
                IncludeLoaderCode = true,
                LoaderLanguage = "sql",
                FileExtension = ".sql.gz",
                Features = new[] { "COPY format", "Schema included", "TimescaleDB hypertables" }
            },
            new()
            {
                Id = "clickhouse",
                Name = "ClickHouse",
                Description = "Native ClickHouse format for analytics",
                Icon = "\uE8F1",
                OutputFormat = "ClickHouse",
                Compression = "lz4",
                IncludeLoaderCode = true,
                LoaderLanguage = "sql",
                FileExtension = ".clickhouse",
                Features = new[] { "Columnar format", "High compression", "Fast analytics" }
            },
            new()
            {
                Id = "jupyter",
                Name = "Jupyter Notebook",
                Description = "Ready-to-run notebook with data loading and exploration",
                Icon = "\uE8A1",
                OutputFormat = "Notebook",
                Compression = "none",
                IncludeLoaderCode = true,
                LoaderLanguage = "python",
                FileExtension = ".ipynb",
                Features = new[] { "Interactive exploration", "Sample visualizations", "Documentation" }
            }
        };
    }

    /// <summary>
    /// Gets available data types for export.
    /// </summary>
    public IReadOnlyList<ExportDataType> GetDataTypes()
    {
        return new List<ExportDataType>
        {
            new() { Id = "trades", Name = "Trades", Description = "Tick-by-tick trade data", Icon = "\uE8AB" },
            new() { Id = "quotes", Name = "Quotes", Description = "Best bid/offer quotes", Icon = "\uE8D4" },
            new() { Id = "depth", Name = "Order Book", Description = "L2 market depth snapshots", Icon = "\uE8A1" },
            new() { Id = "bars_1m", Name = "1-Minute Bars", Description = "OHLCV aggregated to 1 minute", Icon = "\uE9D9" },
            new() { Id = "bars_5m", Name = "5-Minute Bars", Description = "OHLCV aggregated to 5 minutes", Icon = "\uE9D9" },
            new() { Id = "bars_1h", Name = "Hourly Bars", Description = "OHLCV aggregated to 1 hour", Icon = "\uE9D9" },
            new() { Id = "bars_1d", Name = "Daily Bars", Description = "OHLCV aggregated to 1 day", Icon = "\uE9D9" }
        };
    }

    /// <summary>
    /// Estimates export size and duration.
    /// </summary>
    public async Task<ExportEstimate> EstimateExportAsync(
        ExportConfiguration config,
        CancellationToken ct = default)
    {
        var estimate = new ExportEstimate();

        // Get data availability info
        var completeness = await _completenessService.GetCompletenessReportAsync(
            config.Symbols,
            config.FromDate,
            config.ToDate,
            ct);

        estimate.TotalRecords = completeness.TotalExpectedEvents;
        estimate.AvailableRecords = completeness.TotalActualEvents;
        estimate.CompletenessPercent = completeness.OverallCompleteness;

        // Estimate file size based on format
        var bytesPerRecord = config.Profile.OutputFormat switch
        {
            "Parquet" => 50,
            "CSV" => 120,
            "HDF5" => 60,
            "Excel" => 150,
            "SQL" => 100,
            _ => 80
        };

        estimate.EstimatedSizeBytes = estimate.AvailableRecords * bytesPerRecord;

        // Apply compression factor
        var compressionFactor = config.Profile.Compression switch
        {
            "snappy" => 0.4,
            "gzip" => 0.3,
            "lz4" => 0.5,
            "zstd" => 0.25,
            _ => 1.0
        };

        estimate.EstimatedSizeBytes = (long)(estimate.EstimatedSizeBytes * compressionFactor);

        // Estimate duration (rough estimate: 100MB/minute)
        estimate.EstimatedDurationSeconds = (int)(estimate.EstimatedSizeBytes / (100.0 * 1024 * 1024) * 60);
        estimate.EstimatedDurationSeconds = Math.Max(5, estimate.EstimatedDurationSeconds);

        // Check for potential issues
        if (completeness.OverallCompleteness < 95)
        {
            estimate.Warnings.Add($"Data completeness is {completeness.OverallCompleteness:F1}% - some gaps may exist");
        }

        if (completeness.GapCount > 0)
        {
            estimate.Warnings.Add($"{completeness.GapCount} data gaps detected in selected range");
        }

        if (estimate.EstimatedSizeBytes > 1024L * 1024 * 1024)
        {
            estimate.Warnings.Add("Large export - consider splitting by date range");
        }

        return estimate;
    }

    /// <summary>
    /// Generates a data quality pre-export report.
    /// </summary>
    public async Task<PreExportQualityReport> GenerateQualityReportAsync(
        ExportConfiguration config,
        CancellationToken ct = default)
    {
        var report = new PreExportQualityReport
        {
            GeneratedAt = DateTime.UtcNow,
            Symbols = config.Symbols,
            FromDate = config.FromDate,
            ToDate = config.ToDate
        };

        // Get completeness data
        var completeness = await _completenessService.GetCompletenessReportAsync(
            config.Symbols,
            config.FromDate,
            config.ToDate,
            ct);

        report.OverallCompleteness = completeness.OverallCompleteness;
        report.TotalTradingDays = completeness.TotalTradingDays;
        report.DaysWithData = completeness.DaysWithData;
        report.GapCount = completeness.GapCount;

        // Per-symbol quality
        foreach (var symbol in config.Symbols)
        {
            var symbolReport = await _completenessService.GetSymbolCompletenessAsync(
                symbol, config.FromDate, config.ToDate, ct);

            report.SymbolQuality.Add(new SymbolQualityInfo
            {
                Symbol = symbol,
                Completeness = symbolReport.Completeness,
                RecordCount = symbolReport.RecordCount,
                HasGaps = symbolReport.GapCount > 0,
                QualityGrade = GetQualityGrade(symbolReport.Completeness)
            });
        }

        // Analysis suitability assessment
        report.SuitabilityAssessment = AssessSuitability(report);

        return report;
    }

    private string GetQualityGrade(double completeness)
    {
        return completeness switch
        {
            >= 99 => "A+",
            >= 95 => "A",
            >= 90 => "B",
            >= 80 => "C",
            >= 70 => "D",
            _ => "F"
        };
    }

    private string AssessSuitability(PreExportQualityReport report)
    {
        if (report.OverallCompleteness >= 99)
            return "Excellent - Data is highly suitable for all analysis types including ML training";
        if (report.OverallCompleteness >= 95)
            return "Good - Data is suitable for most analysis with minor gap handling";
        if (report.OverallCompleteness >= 90)
            return "Fair - Consider gap filling before use in production backtests";
        if (report.OverallCompleteness >= 80)
            return "Limited - Significant gaps may affect analysis accuracy";
        return "Poor - Data quality issues may severely impact analysis results";
    }

    /// <summary>
    /// Generates loader code for the selected profile.
    /// </summary>
    public string GenerateLoaderCode(ExportProfile profile, string exportPath, string[] symbols)
    {
        return profile.LoaderLanguage switch
        {
            "python" => GeneratePythonLoader(profile, exportPath, symbols),
            "r" => GenerateRLoader(profile, exportPath, symbols),
            "sql" => GenerateSqlLoader(profile, exportPath),
            _ => string.Empty
        };
    }

    private string GeneratePythonLoader(ExportProfile profile, string exportPath, string[] symbols)
    {
        var sb = new StringBuilder();
        sb.AppendLine("\"\"\"");
        sb.AppendLine("Auto-generated data loader for Market Data Collector export");
        sb.AppendLine($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"Profile: {profile.Name}");
        sb.AppendLine("\"\"\"");
        sb.AppendLine();
        sb.AppendLine("import pandas as pd");
        sb.AppendLine("from pathlib import Path");

        if (profile.OutputFormat == "Parquet")
        {
            sb.AppendLine("import pyarrow.parquet as pq");
            sb.AppendLine();
            sb.AppendLine($"DATA_PATH = Path(r\"{exportPath}\")");
            sb.AppendLine();
            sb.AppendLine("def load_data(symbol: str = None) -> pd.DataFrame:");
            sb.AppendLine("    \"\"\"Load market data from parquet files.\"\"\"");
            sb.AppendLine("    if symbol:");
            sb.AppendLine("        file_path = DATA_PATH / f\"{symbol}.parquet\"");
            sb.AppendLine("        return pd.read_parquet(file_path)");
            sb.AppendLine("    ");
            sb.AppendLine("    # Load all symbols");
            sb.AppendLine("    dfs = []");
            sb.AppendLine("    for file in DATA_PATH.glob(\"*.parquet\"):");
            sb.AppendLine("        df = pd.read_parquet(file)");
            sb.AppendLine("        df['symbol'] = file.stem");
            sb.AppendLine("        dfs.append(df)");
            sb.AppendLine("    return pd.concat(dfs, ignore_index=True)");
        }
        else if (profile.OutputFormat == "HDF5")
        {
            sb.AppendLine("import h5py");
            sb.AppendLine("import numpy as np");
            sb.AppendLine();
            sb.AppendLine($"DATA_PATH = Path(r\"{exportPath}\")");
            sb.AppendLine();
            sb.AppendLine("def load_data(symbol: str) -> dict:");
            sb.AppendLine("    \"\"\"Load market data from HDF5 file.\"\"\"");
            sb.AppendLine("    with h5py.File(DATA_PATH / f\"{symbol}.h5\", 'r') as f:");
            sb.AppendLine("        return {");
            sb.AppendLine("            'timestamps': f['timestamps'][:],");
            sb.AppendLine("            'prices': f['prices'][:],");
            sb.AppendLine("            'volumes': f['volumes'][:],");
            sb.AppendLine("        }");
        }

        sb.AppendLine();
        sb.AppendLine("# Example usage:");
        sb.AppendLine($"# df = load_data(\"{symbols.FirstOrDefault() ?? "SPY"}\")");
        sb.AppendLine("# print(df.head())");

        return sb.ToString();
    }

    private string GenerateRLoader(ExportProfile profile, string exportPath, string[] symbols)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Auto-generated data loader for Market Data Collector export");
        sb.AppendLine($"# Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"# Profile: {profile.Name}");
        sb.AppendLine();
        sb.AppendLine("library(tidyverse)");
        sb.AppendLine("library(lubridate)");
        sb.AppendLine();
        sb.AppendLine($"DATA_PATH <- \"{exportPath.Replace("\\", "/")}\"");
        sb.AppendLine();
        sb.AppendLine("load_data <- function(symbol = NULL) {");
        sb.AppendLine("  if (!is.null(symbol)) {");
        sb.AppendLine("    file_path <- file.path(DATA_PATH, paste0(symbol, \".csv\"))");
        sb.AppendLine("    return(read_csv(file_path, col_types = cols(");
        sb.AppendLine("      timestamp = col_datetime(),");
        sb.AppendLine("      price = col_double(),");
        sb.AppendLine("      volume = col_integer()");
        sb.AppendLine("    )))");
        sb.AppendLine("  }");
        sb.AppendLine("  ");
        sb.AppendLine("  # Load all symbols");
        sb.AppendLine("  files <- list.files(DATA_PATH, pattern = \"\\\\.csv$\", full.names = TRUE)");
        sb.AppendLine("  map_dfr(files, ~read_csv(.x) %>% mutate(symbol = tools::file_path_sans_ext(basename(.x))))");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("# Example usage:");
        sb.AppendLine($"# df <- load_data(\"{symbols.FirstOrDefault() ?? "SPY"}\")");
        sb.AppendLine("# head(df)");

        return sb.ToString();
    }

    private string GenerateSqlLoader(ExportProfile profile, string exportPath)
    {
        var sb = new StringBuilder();
        sb.AppendLine("-- Auto-generated SQL loader for Market Data Collector export");
        sb.AppendLine($"-- Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"-- Profile: {profile.Name}");
        sb.AppendLine();

        if (profile.Id == "sql-postgres")
        {
            sb.AppendLine("-- Create table");
            sb.AppendLine("CREATE TABLE IF NOT EXISTS market_data (");
            sb.AppendLine("    id BIGSERIAL PRIMARY KEY,");
            sb.AppendLine("    symbol VARCHAR(20) NOT NULL,");
            sb.AppendLine("    timestamp TIMESTAMPTZ NOT NULL,");
            sb.AppendLine("    price DECIMAL(18,8) NOT NULL,");
            sb.AppendLine("    volume BIGINT NOT NULL,");
            sb.AppendLine("    data_type VARCHAR(20) NOT NULL");
            sb.AppendLine(");");
            sb.AppendLine();
            sb.AppendLine("-- Create TimescaleDB hypertable (if using TimescaleDB)");
            sb.AppendLine("-- SELECT create_hypertable('market_data', 'timestamp', if_not_exists => TRUE);");
            sb.AppendLine();
            sb.AppendLine("-- Create index");
            sb.AppendLine("CREATE INDEX IF NOT EXISTS idx_market_data_symbol_time ON market_data (symbol, timestamp DESC);");
            sb.AppendLine();
            sb.AppendLine("-- Load data (run from psql)");
            sb.AppendLine($"-- \\COPY market_data FROM '{exportPath}/data.csv' WITH CSV HEADER;");
        }
        else if (profile.Id == "clickhouse")
        {
            sb.AppendLine("-- Create table");
            sb.AppendLine("CREATE TABLE IF NOT EXISTS market_data (");
            sb.AppendLine("    symbol String,");
            sb.AppendLine("    timestamp DateTime64(3),");
            sb.AppendLine("    price Decimal64(8),");
            sb.AppendLine("    volume UInt64,");
            sb.AppendLine("    data_type String");
            sb.AppendLine(") ENGINE = MergeTree()");
            sb.AppendLine("ORDER BY (symbol, timestamp);");
            sb.AppendLine();
            sb.AppendLine("-- Load data");
            sb.AppendLine($"-- INSERT INTO market_data FROM INFILE '{exportPath}/data.csv' FORMAT CSV;");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Executes the export with the given configuration.
    /// </summary>
    public async Task<ExportResult> ExecuteExportAsync(
        ExportConfiguration config,
        IProgress<ExportProgress>? progress = null,
        CancellationToken ct = default)
    {
        var result = new ExportResult
        {
            StartTime = DateTime.UtcNow,
            Configuration = config
        };

        try
        {
            // Create output directory
            Directory.CreateDirectory(config.OutputPath);

            var totalSymbols = config.Symbols.Length;
            var processedSymbols = 0;

            foreach (var symbol in config.Symbols)
            {
                ct.ThrowIfCancellationRequested();

                progress?.Report(new ExportProgress
                {
                    CurrentSymbol = symbol,
                    ProcessedSymbols = processedSymbols,
                    TotalSymbols = totalSymbols,
                    PercentComplete = (double)processedSymbols / totalSymbols * 100
                });

                // Export logic would go here - this is a placeholder
                await Task.Delay(100, ct); // Simulate work

                processedSymbols++;
                result.ProcessedSymbols++;
            }

            // Generate loader code if requested
            if (config.Profile.IncludeLoaderCode)
            {
                var loaderCode = GenerateLoaderCode(config.Profile, config.OutputPath, config.Symbols);
                var loaderFile = Path.Combine(config.OutputPath, $"loader{GetLoaderExtension(config.Profile.LoaderLanguage)}");
                await File.WriteAllTextAsync(loaderFile, loaderCode, ct);
                result.GeneratedFiles.Add(loaderFile);
            }

            // Generate quality report if requested
            if (config.IncludeQualityReport)
            {
                var qualityReport = await GenerateQualityReportAsync(config, ct);
                var reportPath = Path.Combine(config.OutputPath, "quality_report.json");
                await File.WriteAllTextAsync(reportPath,
                    JsonSerializer.Serialize(qualityReport, new JsonSerializerOptions { WriteIndented = true }), ct);
                result.GeneratedFiles.Add(reportPath);
            }

            result.Success = true;
            result.EndTime = DateTime.UtcNow;
        }
        catch (OperationCanceledException)
        {
            result.Success = false;
            result.ErrorMessage = "Export cancelled";
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }

        result.EndTime = DateTime.UtcNow;
        return result;
    }

    private string GetLoaderExtension(string language)
    {
        return language switch
        {
            "python" => ".py",
            "r" => ".R",
            "sql" => ".sql",
            "csharp" => ".cs",
            _ => ".txt"
        };
    }
}

/// <summary>
/// Export profile for a specific analysis tool.
/// </summary>
public class ExportProfile
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string OutputFormat { get; set; } = string.Empty;
    public string Compression { get; set; } = string.Empty;
    public bool IncludeLoaderCode { get; set; }
    public string LoaderLanguage { get; set; } = string.Empty;
    public string FileExtension { get; set; } = string.Empty;
    public string[] Features { get; set; } = Array.Empty<string>();
}

/// <summary>
/// Data type available for export.
/// </summary>
public class ExportDataType
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
}

/// <summary>
/// Export configuration.
/// </summary>
public class ExportConfiguration
{
    public ExportProfile Profile { get; set; } = new();
    public string[] Symbols { get; set; } = Array.Empty<string>();
    public string[] DataTypes { get; set; } = Array.Empty<string>();
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
    public string OutputPath { get; set; } = string.Empty;
    public bool IncludeQualityReport { get; set; }
    public bool IncludeSchema { get; set; }
    public Dictionary<string, string> AdditionalOptions { get; set; } = new();
}

/// <summary>
/// Export size and duration estimate.
/// </summary>
public class ExportEstimate
{
    public long TotalRecords { get; set; }
    public long AvailableRecords { get; set; }
    public double CompletenessPercent { get; set; }
    public long EstimatedSizeBytes { get; set; }
    public int EstimatedDurationSeconds { get; set; }
    public List<string> Warnings { get; set; } = new();

    public string EstimatedSizeFormatted => FormatBytes(EstimatedSizeBytes);
    public string EstimatedDurationFormatted => FormatDuration(EstimatedDurationSeconds);

    private static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        int order = 0;
        double size = bytes;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        return $"{size:F1} {sizes[order]}";
    }

    private static string FormatDuration(int seconds)
    {
        if (seconds < 60) return $"{seconds}s";
        if (seconds < 3600) return $"{seconds / 60}m {seconds % 60}s";
        return $"{seconds / 3600}h {seconds % 3600 / 60}m";
    }
}

/// <summary>
/// Pre-export data quality report.
/// </summary>
public class PreExportQualityReport
{
    public DateTime GeneratedAt { get; set; }
    public string[] Symbols { get; set; } = Array.Empty<string>();
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
    public double OverallCompleteness { get; set; }
    public int TotalTradingDays { get; set; }
    public int DaysWithData { get; set; }
    public int GapCount { get; set; }
    public List<SymbolQualityInfo> SymbolQuality { get; set; } = new();
    public string SuitabilityAssessment { get; set; } = string.Empty;
}

/// <summary>
/// Quality info for a single symbol.
/// </summary>
public class SymbolQualityInfo
{
    public string Symbol { get; set; } = string.Empty;
    public double Completeness { get; set; }
    public long RecordCount { get; set; }
    public bool HasGaps { get; set; }
    public string QualityGrade { get; set; } = string.Empty;
}

/// <summary>
/// Export progress information.
/// </summary>
public class ExportProgress
{
    public string CurrentSymbol { get; set; } = string.Empty;
    public int ProcessedSymbols { get; set; }
    public int TotalSymbols { get; set; }
    public double PercentComplete { get; set; }
    public string StatusMessage { get; set; } = string.Empty;
}

/// <summary>
/// Export result.
/// </summary>
public class ExportResult
{
    public bool Success { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public ExportConfiguration Configuration { get; set; } = new();
    public int ProcessedSymbols { get; set; }
    public long TotalRecordsExported { get; set; }
    public long OutputSizeBytes { get; set; }
    public List<string> GeneratedFiles { get; set; } = new();

    public TimeSpan Duration => EndTime - StartTime;
}
