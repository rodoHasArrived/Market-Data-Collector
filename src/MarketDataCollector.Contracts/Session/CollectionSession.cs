using System.Text.Json.Serialization;

namespace MarketDataCollector.Contracts.Session;

/// <summary>
/// Represents a discrete data collection session with comprehensive tracking.
/// </summary>
public sealed class CollectionSession
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = SessionStatus.Pending;

    [JsonPropertyName("startedAt")]
    public DateTime? StartedAt { get; set; }

    [JsonPropertyName("endedAt")]
    public DateTime? EndedAt { get; set; }

    [JsonPropertyName("symbols")]
    public string[] Symbols { get; set; } = Array.Empty<string>();

    [JsonPropertyName("eventTypes")]
    public string[] EventTypes { get; set; } = Array.Empty<string>();

    [JsonPropertyName("provider")]
    public string? Provider { get; set; }

    [JsonPropertyName("tags")]
    public string[]? Tags { get; set; }

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }

    [JsonPropertyName("statistics")]
    public CollectionSessionStatistics? Statistics { get; set; }

    [JsonPropertyName("qualityScore")]
    public double QualityScore { get; set; }

    [JsonPropertyName("manifestPath")]
    public string? ManifestPath { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Session status constants.
/// </summary>
public static class SessionStatus
{
    public const string Pending = "Pending";
    public const string Active = "Active";
    public const string Paused = "Paused";
    public const string Completed = "Completed";
    public const string Failed = "Failed";
}

/// <summary>
/// Statistics for a collection session.
/// </summary>
public sealed class CollectionSessionStatistics
{
    [JsonPropertyName("totalEvents")]
    public long TotalEvents { get; set; }

    [JsonPropertyName("tradeEvents")]
    public long TradeEvents { get; set; }

    [JsonPropertyName("quoteEvents")]
    public long QuoteEvents { get; set; }

    [JsonPropertyName("depthEvents")]
    public long DepthEvents { get; set; }

    [JsonPropertyName("barEvents")]
    public long BarEvents { get; set; }

    [JsonPropertyName("totalBytes")]
    public long TotalBytes { get; set; }

    [JsonPropertyName("compressedBytes")]
    public long CompressedBytes { get; set; }

    [JsonPropertyName("fileCount")]
    public int FileCount { get; set; }

    [JsonPropertyName("gapsDetected")]
    public int GapsDetected { get; set; }

    [JsonPropertyName("gapsFilled")]
    public int GapsFilled { get; set; }

    [JsonPropertyName("sequenceErrors")]
    public int SequenceErrors { get; set; }

    [JsonPropertyName("eventsPerSecond")]
    public double EventsPerSecond { get; set; }

    [JsonPropertyName("compressionRatio")]
    public double CompressionRatio { get; set; }
}

/// <summary>
/// Configuration for collection sessions.
/// </summary>
public sealed class CollectionSessionsConfig
{
    [JsonPropertyName("sessions")]
    public CollectionSession[]? Sessions { get; set; }

    [JsonPropertyName("activeSessionId")]
    public string? ActiveSessionId { get; set; }

    [JsonPropertyName("autoCreateDailySessions")]
    public bool AutoCreateDailySessions { get; set; } = true;

    [JsonPropertyName("sessionNamingPattern")]
    public string SessionNamingPattern { get; set; } = "{date}-{mode}";

    [JsonPropertyName("generateManifestOnComplete")]
    public bool GenerateManifestOnComplete { get; set; } = true;

    [JsonPropertyName("retainSessionHistory")]
    public int RetainSessionHistory { get; set; } = 365;
}
