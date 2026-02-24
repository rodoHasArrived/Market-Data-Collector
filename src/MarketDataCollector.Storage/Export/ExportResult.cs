using System.Text.Json.Serialization;

namespace MarketDataCollector.Storage.Export;

/// <summary>
/// Result of an export operation.
/// </summary>
public sealed class ExportResult
{
    /// <summary>
    /// Unique ID for this export job.
    /// </summary>
    [JsonPropertyName("jobId")]
    public string JobId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Whether the export completed successfully.
    /// </summary>
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    /// <summary>
    /// Export profile used.
    /// </summary>
    [JsonPropertyName("profileId")]
    public string ProfileId { get; set; } = string.Empty;

    /// <summary>
    /// Start time of export.
    /// </summary>
    [JsonPropertyName("startedAt")]
    public DateTime StartedAt { get; set; }

    /// <summary>
    /// Completion time of export.
    /// </summary>
    [JsonPropertyName("completedAt")]
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Duration in seconds.
    /// </summary>
    [JsonPropertyName("durationSeconds")]
    public double DurationSeconds => CompletedAt.HasValue
        ? (CompletedAt.Value - StartedAt).TotalSeconds
        : (DateTime.UtcNow - StartedAt).TotalSeconds;

    /// <summary>
    /// Number of files generated.
    /// </summary>
    [JsonPropertyName("filesGenerated")]
    public int FilesGenerated { get; set; }

    /// <summary>
    /// Total records exported.
    /// </summary>
    [JsonPropertyName("totalRecords")]
    public long TotalRecords { get; set; }

    /// <summary>
    /// Total bytes written.
    /// </summary>
    [JsonPropertyName("totalBytes")]
    public long TotalBytes { get; set; }

    /// <summary>
    /// Symbols exported.
    /// </summary>
    [JsonPropertyName("symbols")]
    public string[] Symbols { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Date range exported.
    /// </summary>
    [JsonPropertyName("dateRange")]
    public ExportDateRange? DateRange { get; set; }

    /// <summary>
    /// Output directory.
    /// </summary>
    [JsonPropertyName("outputDirectory")]
    public string OutputDirectory { get; set; } = string.Empty;

    /// <summary>
    /// Generated files.
    /// </summary>
    [JsonPropertyName("files")]
    public ExportedFile[] Files { get; set; } = Array.Empty<ExportedFile>();

    /// <summary>
    /// Path to generated data dictionary.
    /// </summary>
    [JsonPropertyName("dataDictionaryPath")]
    public string? DataDictionaryPath { get; set; }

    /// <summary>
    /// Path to generated loader script.
    /// </summary>
    [JsonPropertyName("loaderScriptPath")]
    public string? LoaderScriptPath { get; set; }

    /// <summary>
    /// Path to the data lineage manifest (improvement 11.1).
    /// </summary>
    [JsonPropertyName("lineageManifestPath")]
    public string? LineageManifestPath { get; set; }

    /// <summary>
    /// Data quality summary.
    /// </summary>
    [JsonPropertyName("qualitySummary")]
    public ExportQualitySummary? QualitySummary { get; set; }

    /// <summary>
    /// Warnings encountered during export.
    /// </summary>
    [JsonPropertyName("warnings")]
    public string[] Warnings { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Error message if failed.
    /// </summary>
    [JsonPropertyName("error")]
    public string? Error { get; set; }

    /// <summary>
    /// Create a success result.
    /// </summary>
    public static ExportResult CreateSuccess(string profileId, string outputDir) => new()
    {
        Success = true,
        ProfileId = profileId,
        OutputDirectory = outputDir,
        StartedAt = DateTime.UtcNow
    };

    /// <summary>
    /// Create a failure result.
    /// </summary>
    public static ExportResult CreateFailure(string profileId, string error) => new()
    {
        Success = false,
        ProfileId = profileId,
        Error = error,
        StartedAt = DateTime.UtcNow,
        CompletedAt = DateTime.UtcNow
    };
}

/// <summary>
/// Date range for export.
/// </summary>
public sealed class ExportDateRange
{
    [JsonPropertyName("start")]
    public DateTime Start { get; set; }

    [JsonPropertyName("end")]
    public DateTime End { get; set; }

    [JsonPropertyName("tradingDays")]
    public int TradingDays { get; set; }
}

/// <summary>
/// Information about an exported file.
/// </summary>
public sealed class ExportedFile
{
    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("relativePath")]
    public string RelativePath { get; set; } = string.Empty;

    [JsonPropertyName("symbol")]
    public string? Symbol { get; set; }

    [JsonPropertyName("eventType")]
    public string? EventType { get; set; }

    [JsonPropertyName("format")]
    public string Format { get; set; } = string.Empty;

    [JsonPropertyName("sizeBytes")]
    public long SizeBytes { get; set; }

    [JsonPropertyName("recordCount")]
    public long RecordCount { get; set; }

    [JsonPropertyName("checksumSha256")]
    public string? ChecksumSha256 { get; set; }

    [JsonPropertyName("firstTimestamp")]
    public DateTime? FirstTimestamp { get; set; }

    [JsonPropertyName("lastTimestamp")]
    public DateTime? LastTimestamp { get; set; }
}

/// <summary>
/// Preview of an export operation with estimates and sample data (improvement 10.5).
/// </summary>
public sealed class ExportPreview
{
    [JsonPropertyName("profileId")]
    public string ProfileId { get; set; } = string.Empty;

    [JsonPropertyName("profileName")]
    public string ProfileName { get; set; } = string.Empty;

    [JsonPropertyName("format")]
    public string Format { get; set; } = string.Empty;

    [JsonPropertyName("symbols")]
    public string[] Symbols { get; set; } = Array.Empty<string>();

    [JsonPropertyName("eventTypes")]
    public string[] EventTypes { get; set; } = Array.Empty<string>();

    [JsonPropertyName("sourceFileCount")]
    public int SourceFileCount { get; set; }

    [JsonPropertyName("estimatedRecords")]
    public long EstimatedRecords { get; set; }

    [JsonPropertyName("sourceSizeBytes")]
    public long SourceSizeBytes { get; set; }

    [JsonPropertyName("estimatedOutputSizeBytes")]
    public long EstimatedOutputSizeBytes { get; set; }

    [JsonPropertyName("estimatedOutputSizeMb")]
    public double EstimatedOutputSizeMb { get; set; }

    [JsonPropertyName("sampleData")]
    public List<Dictionary<string, object?>> SampleData { get; set; } = new();

    [JsonPropertyName("dateRange")]
    public ExportDateRange? DateRange { get; set; }

    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}

/// <summary>
/// Data lineage manifest embedded in exports (improvement 11.1).
/// Tracks the provenance of exported data through the pipeline.
/// </summary>
public sealed class ExportLineageManifest
{
    [JsonPropertyName("exportJobId")]
    public string ExportJobId { get; set; } = string.Empty;

    [JsonPropertyName("generatedAt")]
    public DateTimeOffset GeneratedAt { get; set; } = DateTimeOffset.UtcNow;

    [JsonPropertyName("collectorVersion")]
    public string CollectorVersion { get; set; } = "1.6.2";

    [JsonPropertyName("sourceProviders")]
    public List<LineageProvider> SourceProviders { get; set; } = new();

    [JsonPropertyName("pipeline")]
    public LineagePipeline Pipeline { get; set; } = new();

    [JsonPropertyName("transformations")]
    public List<string> Transformations { get; set; } = new();

    [JsonPropertyName("qualityChecks")]
    public List<LineageQualityCheck> QualityChecks { get; set; } = new();

    [JsonPropertyName("symbols")]
    public string[] Symbols { get; set; } = Array.Empty<string>();

    [JsonPropertyName("dateRange")]
    public ExportDateRange? DateRange { get; set; }

    [JsonPropertyName("recordCount")]
    public long RecordCount { get; set; }

    [JsonPropertyName("checksumSha256")]
    public string? ChecksumSha256 { get; set; }
}

/// <summary>
/// Provider lineage information.
/// </summary>
public sealed class LineageProvider
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty; // "streaming" or "historical"

    [JsonPropertyName("recordCount")]
    public long RecordCount { get; set; }

    [JsonPropertyName("firstRecord")]
    public DateTimeOffset? FirstRecord { get; set; }

    [JsonPropertyName("lastRecord")]
    public DateTimeOffset? LastRecord { get; set; }
}

/// <summary>
/// Pipeline lineage information.
/// </summary>
public sealed class LineagePipeline
{
    [JsonPropertyName("walEnabled")]
    public bool WalEnabled { get; set; }

    [JsonPropertyName("deduplicationEnabled")]
    public bool DeduplicationEnabled { get; set; }

    [JsonPropertyName("storageFormat")]
    public string StorageFormat { get; set; } = "jsonl";

    [JsonPropertyName("compressionProfile")]
    public string CompressionProfile { get; set; } = "standard";
}

/// <summary>
/// Quality check performed during export.
/// </summary>
public sealed class LineageQualityCheck
{
    [JsonPropertyName("check")]
    public string Check { get; set; } = string.Empty;

    [JsonPropertyName("passed")]
    public bool Passed { get; set; }

    [JsonPropertyName("details")]
    public string? Details { get; set; }
}

/// <summary>
/// Quality summary for exported data.
/// </summary>
public sealed class ExportQualitySummary
{
    [JsonPropertyName("overallScore")]
    public double OverallScore { get; set; }

    [JsonPropertyName("completenessScore")]
    public double CompletenessScore { get; set; }

    [JsonPropertyName("totalExpectedRecords")]
    public long TotalExpectedRecords { get; set; }

    [JsonPropertyName("totalActualRecords")]
    public long TotalActualRecords { get; set; }

    [JsonPropertyName("gapsDetected")]
    public int GapsDetected { get; set; }

    [JsonPropertyName("gapsFilled")]
    public int GapsFilled { get; set; }

    [JsonPropertyName("outliersDetected")]
    public int OutliersDetected { get; set; }

    [JsonPropertyName("outlierHandling")]
    public string OutlierHandling { get; set; } = "None";

    [JsonPropertyName("missingDates")]
    public string[]? MissingDates { get; set; }

    [JsonPropertyName("issueDetails")]
    public string[]? IssueDetails { get; set; }
}
