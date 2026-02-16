using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UiServices = MarketDataCollector.Ui.Services;
using ApiClientService = MarketDataCollector.Ui.Services.ApiClientService;

namespace MarketDataCollector.Wpf.Services;

/// <summary>
/// Service for advanced data analysis and export functionality.
/// Supports analysis-focused exports, aggregations, and research-friendly formats.
/// </summary>
public sealed class WpfAnalysisExportService
{
    private static WpfAnalysisExportService? _instance;
    private static readonly object _lock = new();
    private readonly ApiClientService _apiClient;

    public static WpfAnalysisExportService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new WpfAnalysisExportService();
                }
            }
            return _instance;
        }
    }

    private WpfAnalysisExportService()
    {
        _apiClient = ApiClientService.Instance;
    }

    /// <summary>
    /// Raised when export progress changes.
    /// </summary>
    public event EventHandler<ExportProgressEventArgs>? ProgressChanged;

    /// <summary>
    /// Exports data with analysis-focused options.
    /// </summary>
    public async Task<AnalysisExportResult> ExportAsync(
        AnalysisExportOptions options,
        CancellationToken ct = default)
    {
        var response = await _apiClient.PostWithResponseAsync<AnalysisExportResponse>(
            "/api/export/analysis",
            new
            {
                symbols = options.Symbols,
                fromDate = options.FromDate?.ToString("yyyy-MM-dd"),
                toDate = options.ToDate?.ToString("yyyy-MM-dd"),
                format = options.Format.ToString(),
                aggregation = options.Aggregation?.ToString(),
                includeFields = options.IncludeFields,
                excludeFields = options.ExcludeFields,
                filters = options.Filters,
                outputPath = options.OutputPath,
                fileName = options.FileName,
                compression = options.Compression?.ToString(),
                includeMetadata = options.IncludeMetadata,
                splitBySymbol = options.SplitBySymbol,
                timezone = options.Timezone
            },
            ct);

        if (response.Success && response.Data != null)
        {
            return new AnalysisExportResult
            {
                Success = response.Data.Success,
                OutputPath = response.Data.OutputPath,
                FilesCreated = response.Data.FilesCreated?.ToList() ?? new List<string>(),
                RowsExported = response.Data.RowsExported,
                BytesWritten = response.Data.BytesWritten,
                Duration = TimeSpan.FromSeconds(response.Data.DurationSeconds),
                Warnings = response.Data.Warnings?.ToList() ?? new List<string>()
            };
        }

        return new AnalysisExportResult
        {
            Success = false,
            Error = response.ErrorMessage ?? "Export failed"
        };
    }

    /// <summary>
    /// Gets available export formats.
    /// </summary>
    public async Task<ExportFormatsResult> GetAvailableFormatsAsync(CancellationToken ct = default)
    {
        var response = await _apiClient.GetWithResponseAsync<ExportFormatsResponse>(
            "/api/export/formats",
            ct);

        if (response.Success && response.Data != null)
        {
            return new ExportFormatsResult
            {
                Success = true,
                Formats = response.Data.Formats?.ToList() ?? new List<ExportFormatInfo>()
            };
        }

        // Return default formats
        return new ExportFormatsResult
        {
            Success = true,
            Formats = new List<ExportFormatInfo>
            {
                new() { Name = "CSV", Extension = ".csv", Description = "Comma-separated values", SupportsCompression = true },
                new() { Name = "Parquet", Extension = ".parquet", Description = "Apache Parquet columnar format", SupportsCompression = true },
                new() { Name = "JSON", Extension = ".json", Description = "JSON format", SupportsCompression = true },
                new() { Name = "JSONL", Extension = ".jsonl", Description = "JSON Lines format (one JSON per line)", SupportsCompression = true },
                new() { Name = "Excel", Extension = ".xlsx", Description = "Microsoft Excel format", SupportsCompression = false },
                new() { Name = "HDF5", Extension = ".h5", Description = "HDF5 hierarchical data format", SupportsCompression = true },
                new() { Name = "Feather", Extension = ".feather", Description = "Apache Arrow Feather format", SupportsCompression = true }
            }
        };
    }

    /// <summary>
    /// Gets available aggregation options.
    /// </summary>
    public Task<List<AggregationOption>> GetAggregationOptionsAsync(CancellationToken ct = default)
    {
        return Task.FromResult(new List<AggregationOption>
        {
            new() { Value = "Tick", DisplayName = "Tick (No Aggregation)", Description = "Raw tick data" },
            new() { Value = "Second", DisplayName = "1 Second", Description = "Aggregate to 1-second bars" },
            new() { Value = "Minute", DisplayName = "1 Minute", Description = "Aggregate to 1-minute bars" },
            new() { Value = "FiveMinute", DisplayName = "5 Minutes", Description = "Aggregate to 5-minute bars" },
            new() { Value = "FifteenMinute", DisplayName = "15 Minutes", Description = "Aggregate to 15-minute bars" },
            new() { Value = "ThirtyMinute", DisplayName = "30 Minutes", Description = "Aggregate to 30-minute bars" },
            new() { Value = "Hour", DisplayName = "1 Hour", Description = "Aggregate to hourly bars" },
            new() { Value = "Daily", DisplayName = "Daily", Description = "Aggregate to daily bars" },
            new() { Value = "Weekly", DisplayName = "Weekly", Description = "Aggregate to weekly bars" },
            new() { Value = "Monthly", DisplayName = "Monthly", Description = "Aggregate to monthly bars" }
        });
    }

    /// <summary>
    /// Generates a quality analysis report.
    /// </summary>
    public async Task<QualityReportResult> GenerateQualityReportAsync(
        QualityReportOptions options,
        CancellationToken ct = default)
    {
        var response = await _apiClient.PostWithResponseAsync<QualityReportResponse>(
            "/api/export/quality-report",
            new
            {
                symbols = options.Symbols,
                fromDate = options.FromDate?.ToString("yyyy-MM-dd"),
                toDate = options.ToDate?.ToString("yyyy-MM-dd"),
                includeCharts = options.IncludeCharts,
                format = options.Format
            },
            ct);

        if (response.Success && response.Data != null)
        {
            return new QualityReportResult
            {
                Success = true,
                ReportPath = response.Data.ReportPath,
                Summary = response.Data.Summary
            };
        }

        return new QualityReportResult
        {
            Success = false,
            Error = response.ErrorMessage ?? "Failed to generate report"
        };
    }

    /// <summary>
    /// Exports order flow statistics.
    /// </summary>
    public async Task<AnalysisExportResult> ExportOrderFlowAsync(
        OrderFlowExportOptions options,
        CancellationToken ct = default)
    {
        var response = await _apiClient.PostWithResponseAsync<AnalysisExportResponse>(
            "/api/export/orderflow",
            new
            {
                symbols = options.Symbols,
                fromDate = options.FromDate?.ToString("yyyy-MM-dd"),
                toDate = options.ToDate?.ToString("yyyy-MM-dd"),
                metrics = options.Metrics,
                aggregation = options.Aggregation,
                format = options.Format,
                outputPath = options.OutputPath
            },
            ct);

        if (response.Success && response.Data != null)
        {
            return new AnalysisExportResult
            {
                Success = response.Data.Success,
                OutputPath = response.Data.OutputPath,
                FilesCreated = response.Data.FilesCreated?.ToList() ?? new List<string>(),
                RowsExported = response.Data.RowsExported,
                BytesWritten = response.Data.BytesWritten
            };
        }

        return new AnalysisExportResult
        {
            Success = false,
            Error = response.ErrorMessage ?? "Export failed"
        };
    }

    /// <summary>
    /// Exports integrity events for analysis.
    /// </summary>
    public async Task<AnalysisExportResult> ExportIntegrityEventsAsync(
        IntegrityExportOptions options,
        CancellationToken ct = default)
    {
        var response = await _apiClient.PostWithResponseAsync<AnalysisExportResponse>(
            "/api/export/integrity",
            new
            {
                symbols = options.Symbols,
                fromDate = options.FromDate?.ToString("yyyy-MM-dd"),
                toDate = options.ToDate?.ToString("yyyy-MM-dd"),
                eventTypes = options.EventTypes,
                format = options.Format,
                outputPath = options.OutputPath
            },
            ct);

        if (response.Success && response.Data != null)
        {
            return new AnalysisExportResult
            {
                Success = response.Data.Success,
                OutputPath = response.Data.OutputPath,
                FilesCreated = response.Data.FilesCreated?.ToList() ?? new List<string>(),
                RowsExported = response.Data.RowsExported
            };
        }

        return new AnalysisExportResult
        {
            Success = false,
            Error = response.ErrorMessage ?? "Export failed"
        };
    }

    /// <summary>
    /// Creates a research dataset package.
    /// </summary>
    public async Task<ResearchPackageResult> CreateResearchPackageAsync(
        ResearchPackageOptions options,
        CancellationToken ct = default)
    {
        var response = await _apiClient.PostWithResponseAsync<ResearchPackageResponse>(
            "/api/export/research-package",
            new
            {
                name = options.Name,
                description = options.Description,
                symbols = options.Symbols,
                fromDate = options.FromDate?.ToString("yyyy-MM-dd"),
                toDate = options.ToDate?.ToString("yyyy-MM-dd"),
                includeData = options.IncludeData,
                includeMetadata = options.IncludeMetadata,
                includeQualityReport = options.IncludeQualityReport,
                format = options.Format,
                outputPath = options.OutputPath
            },
            ct);

        if (response.Success && response.Data != null)
        {
            return new ResearchPackageResult
            {
                Success = true,
                PackagePath = response.Data.PackagePath,
                ManifestPath = response.Data.ManifestPath,
                SizeBytes = response.Data.SizeBytes
            };
        }

        return new ResearchPackageResult
        {
            Success = false,
            Error = response.ErrorMessage ?? "Failed to create package"
        };
    }

    /// <summary>
    /// Gets export templates for common research use cases.
    /// </summary>
    public Task<List<ExportTemplate>> GetExportTemplatesAsync(CancellationToken ct = default)
    {
        return Task.FromResult(new List<ExportTemplate>
        {
            new()
            {
                Name = "Academic Research",
                Description = "Clean dataset suitable for academic research papers",
                Format = AnalysisExportFormat.Parquet,
                Aggregation = DataAggregation.Daily,
                IncludeFields = new[] { "Symbol", "Date", "Open", "High", "Low", "Close", "Volume", "AdjClose" },
                IncludeMetadata = true
            },
            new()
            {
                Name = "Machine Learning",
                Description = "Format optimized for ML pipelines",
                Format = AnalysisExportFormat.Parquet,
                Aggregation = DataAggregation.Minute,
                IncludeFields = new[] { "Timestamp", "Symbol", "Price", "Volume", "BidPrice", "AskPrice", "Spread" },
                IncludeMetadata = false
            },
            new()
            {
                Name = "Backtesting",
                Description = "Data format compatible with backtesting engines",
                Format = AnalysisExportFormat.CSV,
                Aggregation = DataAggregation.Minute,
                IncludeFields = new[] { "DateTime", "Symbol", "Open", "High", "Low", "Close", "Volume" },
                IncludeMetadata = false
            },
            new()
            {
                Name = "Order Flow Analysis",
                Description = "Tick data with trade direction and size",
                Format = AnalysisExportFormat.Parquet,
                Aggregation = DataAggregation.Tick,
                IncludeFields = new[] { "Timestamp", "Symbol", "Price", "Size", "Side", "Exchange", "Sequence" },
                IncludeMetadata = true
            },
            new()
            {
                Name = "Market Microstructure",
                Description = "Full LOB snapshots and trades for microstructure research",
                Format = AnalysisExportFormat.HDF5,
                Aggregation = DataAggregation.Tick,
                IncludeFields = new[] { "Timestamp", "Symbol", "EventType", "Price", "Size", "BidPrices", "AskPrices", "BidSizes", "AskSizes" },
                IncludeMetadata = true
            }
        });
    }
}

#region Event Args

public class ExportProgressEventArgs : EventArgs
{
    public double Progress { get; set; }
    public string? CurrentSymbol { get; set; }
    public int RowsProcessed { get; set; }
    public TimeSpan Elapsed { get; set; }
}

#endregion

#region Options Classes

public class AnalysisExportOptions
{
    public List<string>? Symbols { get; set; }
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
    public AnalysisExportFormat Format { get; set; } = AnalysisExportFormat.Parquet;
    public DataAggregation? Aggregation { get; set; }
    public string[]? IncludeFields { get; set; }
    public string[]? ExcludeFields { get; set; }
    public Dictionary<string, string>? Filters { get; set; }
    public string? OutputPath { get; set; }
    public string? FileName { get; set; }
    public CompressionType? Compression { get; set; }
    public bool IncludeMetadata { get; set; } = true;
    public bool SplitBySymbol { get; set; }
    public string? Timezone { get; set; }
}

public class QualityReportOptions
{
    public List<string>? Symbols { get; set; }
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
    public bool IncludeCharts { get; set; } = true;
    public string Format { get; set; } = "HTML";
}

public class OrderFlowExportOptions
{
    public List<string>? Symbols { get; set; }
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
    public string[]? Metrics { get; set; }
    public string Aggregation { get; set; } = "Minute";
    public string Format { get; set; } = "Parquet";
    public string? OutputPath { get; set; }
}

public class IntegrityExportOptions
{
    public List<string>? Symbols { get; set; }
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
    public string[]? EventTypes { get; set; }
    public string Format { get; set; } = "CSV";
    public string? OutputPath { get; set; }
}

public class ResearchPackageOptions
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<string>? Symbols { get; set; }
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
    public DataTypeInclusion IncludeData { get; set; } = new();
    public bool IncludeMetadata { get; set; } = true;
    public bool IncludeQualityReport { get; set; } = true;
    public string Format { get; set; } = "Parquet";
    public string? OutputPath { get; set; }
}

public class DataTypeInclusion
{
    public bool Trades { get; set; } = true;
    public bool Quotes { get; set; } = true;
    public bool Bars { get; set; } = true;
    public bool OrderBook { get; set; }
    public bool OrderFlow { get; set; }
}

#endregion

#region Result Classes

public class AnalysisExportResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? OutputPath { get; set; }
    public List<string> FilesCreated { get; set; } = new();
    public long RowsExported { get; set; }
    public long BytesWritten { get; set; }
    public TimeSpan Duration { get; set; }
    public List<string> Warnings { get; set; } = new();
}

public class ExportFormatsResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public List<ExportFormatInfo> Formats { get; set; } = new();
}

public class ExportFormatInfo
{
    public string Name { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool SupportsCompression { get; set; }
}

public class AggregationOption
{
    public string Value { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class QualityReportResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? ReportPath { get; set; }
    public QualityReportSummary? Summary { get; set; }
}

public class QualityReportSummary
{
    public int TotalSymbols { get; set; }
    public int TotalDays { get; set; }
    public double OverallScore { get; set; }
    public int GapsFound { get; set; }
    public int AnomaliesFound { get; set; }
}

public class ResearchPackageResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? PackagePath { get; set; }
    public string? ManifestPath { get; set; }
    public long SizeBytes { get; set; }
}

public class ExportTemplate
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public AnalysisExportFormat Format { get; set; }
    public DataAggregation Aggregation { get; set; }
    public string[]? IncludeFields { get; set; }
    public bool IncludeMetadata { get; set; }
}

#endregion

#region Enums

public enum AnalysisExportFormat
{
    CSV,
    Parquet,
    JSON,
    JSONL,
    Excel,
    HDF5,
    Feather
}

public enum DataAggregation
{
    Tick,
    Second,
    Minute,
    FiveMinute,
    FifteenMinute,
    ThirtyMinute,
    Hour,
    Daily,
    Weekly,
    Monthly
}

public enum CompressionType
{
    None,
    Gzip,
    LZ4,
    Snappy,
    ZSTD
}

#endregion

#region API Response Classes

public class AnalysisExportResponse
{
    public bool Success { get; set; }
    public string? OutputPath { get; set; }
    public string[]? FilesCreated { get; set; }
    public long RowsExported { get; set; }
    public long BytesWritten { get; set; }
    public double DurationSeconds { get; set; }
    public string[]? Warnings { get; set; }
}

public class ExportFormatsResponse
{
    public List<ExportFormatInfo>? Formats { get; set; }
}

public class QualityReportResponse
{
    public string? ReportPath { get; set; }
    public QualityReportSummary? Summary { get; set; }
}

public class ResearchPackageResponse
{
    public string? PackagePath { get; set; }
    public string? ManifestPath { get; set; }
    public long SizeBytes { get; set; }
}

#endregion
