using System.Text.Json.Serialization;

namespace MarketDataCollector.Contracts.Manifest;

/// <summary>
/// Comprehensive manifest for a collection session or archive package.
/// </summary>
public sealed class DataManifest
{
    [JsonPropertyName("manifestVersion")]
    public string ManifestVersion { get; set; } = "1.0";

    [JsonPropertyName("generatedAt")]
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("sessionId")]
    public string? SessionId { get; set; }

    [JsonPropertyName("sessionName")]
    public string? SessionName { get; set; }

    [JsonPropertyName("dateRange")]
    public DateRangeInfo? DateRange { get; set; }

    [JsonPropertyName("symbols")]
    public string[] Symbols { get; set; } = Array.Empty<string>();

    [JsonPropertyName("totalFiles")]
    public int TotalFiles { get; set; }

    [JsonPropertyName("totalEvents")]
    public long TotalEvents { get; set; }

    [JsonPropertyName("totalBytesRaw")]
    public long TotalBytesRaw { get; set; }

    [JsonPropertyName("totalBytesCompressed")]
    public long TotalBytesCompressed { get; set; }

    [JsonPropertyName("files")]
    public ManifestFileEntry[] Files { get; set; } = Array.Empty<ManifestFileEntry>();

    [JsonPropertyName("schemas")]
    public Dictionary<string, string>? Schemas { get; set; }

    [JsonPropertyName("qualityMetrics")]
    public DataQualityMetrics? QualityMetrics { get; set; }

    [JsonPropertyName("verificationStatus")]
    public string VerificationStatus { get; set; } = VerificationStatusValues.Pending;

    [JsonPropertyName("lastVerifiedAt")]
    public DateTime? LastVerifiedAt { get; set; }
}

/// <summary>
/// Verification status constants.
/// </summary>
public static class VerificationStatusValues
{
    public const string Pending = "Pending";
    public const string Verified = "Verified";
    public const string Failed = "Failed";
}

/// <summary>
/// Date range information.
/// </summary>
public sealed class DateRangeInfo
{
    [JsonPropertyName("start")]
    public DateTime Start { get; set; }

    [JsonPropertyName("end")]
    public DateTime End { get; set; }

    [JsonPropertyName("tradingDays")]
    public int TradingDays { get; set; }
}

/// <summary>
/// Individual file entry in a manifest.
/// </summary>
public sealed class ManifestFileEntry
{
    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("relativePath")]
    public string RelativePath { get; set; } = string.Empty;

    [JsonPropertyName("symbol")]
    public string? Symbol { get; set; }

    [JsonPropertyName("eventType")]
    public string? EventType { get; set; }

    [JsonPropertyName("date")]
    public DateTime? Date { get; set; }

    [JsonPropertyName("checksumSha256")]
    public string ChecksumSha256 { get; set; } = string.Empty;

    [JsonPropertyName("sizeBytes")]
    public long SizeBytes { get; set; }

    [JsonPropertyName("compressedSizeBytes")]
    public long? CompressedSizeBytes { get; set; }

    [JsonPropertyName("eventCount")]
    public long EventCount { get; set; }

    [JsonPropertyName("firstTimestamp")]
    public DateTime? FirstTimestamp { get; set; }

    [JsonPropertyName("lastTimestamp")]
    public DateTime? LastTimestamp { get; set; }

    [JsonPropertyName("schemaVersion")]
    public string? SchemaVersion { get; set; }

    [JsonPropertyName("isCompressed")]
    public bool IsCompressed { get; set; }

    [JsonPropertyName("compressionType")]
    public string? CompressionType { get; set; }

    [JsonPropertyName("verificationStatus")]
    public string VerificationStatus { get; set; } = VerificationStatusValues.Pending;

    [JsonPropertyName("lastVerifiedAt")]
    public DateTime? LastVerifiedAt { get; set; }
}

/// <summary>
/// Data quality metrics for manifests and sessions.
/// </summary>
public sealed class DataQualityMetrics
{
    [JsonPropertyName("completenessScore")]
    public double CompletenessScore { get; set; }

    [JsonPropertyName("integrityScore")]
    public double IntegrityScore { get; set; }

    [JsonPropertyName("overallScore")]
    public double OverallScore { get; set; }

    [JsonPropertyName("gapsDetected")]
    public int GapsDetected { get; set; }

    [JsonPropertyName("sequenceErrors")]
    public int SequenceErrors { get; set; }

    [JsonPropertyName("duplicatesFound")]
    public int DuplicatesFound { get; set; }

    [JsonPropertyName("expectedEvents")]
    public long ExpectedEvents { get; set; }

    [JsonPropertyName("actualEvents")]
    public long ActualEvents { get; set; }

    [JsonPropertyName("missingTradingDays")]
    public string[]? MissingTradingDays { get; set; }

    [JsonPropertyName("outliersDetected")]
    public int OutliersDetected { get; set; }
}
