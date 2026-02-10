using System.Text.Json.Serialization;

namespace MarketDataCollector.Contracts.Api;

/// <summary>
/// Status response from the core service.
/// </summary>
public class StatusResponse
{
    [JsonPropertyName("isConnected")]
    public bool IsConnected { get; set; }

    [JsonPropertyName("timestampUtc")]
    public DateTimeOffset TimestampUtc { get; set; }

    [JsonPropertyName("metrics")]
    public MetricsData? Metrics { get; set; }

    [JsonPropertyName("pipeline")]
    public PipelineData? Pipeline { get; set; }
}

/// <summary>
/// Metrics data snapshot.
/// </summary>
public class MetricsData
{
    [JsonPropertyName("published")]
    public long Published { get; set; }

    [JsonPropertyName("dropped")]
    public long Dropped { get; set; }

    [JsonPropertyName("integrity")]
    public long Integrity { get; set; }

    [JsonPropertyName("historicalBars")]
    public long HistoricalBars { get; set; }

    [JsonPropertyName("eventsPerSecond")]
    public double EventsPerSecond { get; set; }

    [JsonPropertyName("dropRate")]
    public double DropRate { get; set; }

    [JsonPropertyName("trades")]
    public long Trades { get; set; }

    [JsonPropertyName("depthUpdates")]
    public long DepthUpdates { get; set; }

    [JsonPropertyName("quotes")]
    public long Quotes { get; set; }
}

/// <summary>
/// Pipeline statistics.
/// </summary>
public class PipelineData
{
    [JsonPropertyName("publishedCount")]
    public long PublishedCount { get; set; }

    [JsonPropertyName("droppedCount")]
    public long DroppedCount { get; set; }

    [JsonPropertyName("consumedCount")]
    public long ConsumedCount { get; set; }

    [JsonPropertyName("currentQueueSize")]
    public int CurrentQueueSize { get; set; }

    [JsonPropertyName("peakQueueSize")]
    public long PeakQueueSize { get; set; }

    [JsonPropertyName("queueCapacity")]
    public int QueueCapacity { get; set; }

    [JsonPropertyName("queueUtilization")]
    public double QueueUtilization { get; set; }

    [JsonPropertyName("averageProcessingTimeUs")]
    public double AverageProcessingTimeUs { get; set; }
}

/// <summary>
/// Health check response.
/// </summary>
public class HealthCheckResponse
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = "unknown";

    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; set; }

    [JsonPropertyName("uptime")]
    public TimeSpan Uptime { get; set; }

    [JsonPropertyName("checks")]
    public HealthCheckItem[]? Checks { get; set; }
}

/// <summary>
/// Individual health check item.
/// </summary>
public class HealthCheckItem
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = "unknown";

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}

/// <summary>
/// Backfill provider information.
/// </summary>
public class BackfillProviderInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("isAvailable")]
    public bool IsAvailable { get; set; } = true;

    [JsonPropertyName("requiresApiKey")]
    public bool RequiresApiKey { get; set; }
}

/// <summary>
/// Backfill operation result.
/// </summary>
public class BackfillResult
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("provider")]
    public string? Provider { get; set; }

    [JsonPropertyName("symbols")]
    public string[]? Symbols { get; set; }

    [JsonPropertyName("barsWritten")]
    public int BarsWritten { get; set; }

    [JsonPropertyName("startedUtc")]
    public DateTimeOffset? StartedUtc { get; set; }

    [JsonPropertyName("completedUtc")]
    public DateTimeOffset? CompletedUtc { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("symbolResults")]
    public SymbolBackfillResult[]? SymbolResults { get; set; }
}

/// <summary>
/// Per-symbol backfill result.
/// </summary>
public class SymbolBackfillResult
{
    [JsonPropertyName("symbol")]
    public string Symbol { get; set; } = string.Empty;

    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("barsDownloaded")]
    public int BarsDownloaded { get; set; }

    [JsonPropertyName("provider")]
    public string? Provider { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}

/// <summary>
/// Backfill request.
/// </summary>
public class BackfillRequest
{
    [JsonPropertyName("provider")]
    public string? Provider { get; set; }

    [JsonPropertyName("symbols")]
    public string[] Symbols { get; set; } = Array.Empty<string>();

    [JsonPropertyName("from")]
    public string? From { get; set; }

    [JsonPropertyName("to")]
    public string? To { get; set; }

    [JsonPropertyName("granularity")]
    public string Granularity { get; set; } = "Daily";
}

/// <summary>
/// Storage analytics data.
/// </summary>
public class StorageAnalytics
{
    [JsonPropertyName("totalSizeBytes")]
    public long TotalSizeBytes { get; set; }

    [JsonPropertyName("tradeSizeBytes")]
    public long TradeSizeBytes { get; set; }

    [JsonPropertyName("depthSizeBytes")]
    public long DepthSizeBytes { get; set; }

    [JsonPropertyName("historicalSizeBytes")]
    public long HistoricalSizeBytes { get; set; }

    [JsonPropertyName("totalFileCount")]
    public int TotalFileCount { get; set; }

    [JsonPropertyName("tradeFileCount")]
    public int TradeFileCount { get; set; }

    [JsonPropertyName("depthFileCount")]
    public int DepthFileCount { get; set; }

    [JsonPropertyName("historicalFileCount")]
    public int HistoricalFileCount { get; set; }

    [JsonPropertyName("lastUpdated")]
    public DateTimeOffset LastUpdated { get; set; }

    [JsonPropertyName("symbolBreakdown")]
    public SymbolStorageInfo[]? SymbolBreakdown { get; set; }

    [JsonPropertyName("dailyGrowthBytes")]
    public long DailyGrowthBytes { get; set; }

    [JsonPropertyName("projectedDaysUntilFull")]
    public int? ProjectedDaysUntilFull { get; set; }
}

/// <summary>
/// Per-symbol storage information.
/// </summary>
public class SymbolStorageInfo
{
    [JsonPropertyName("symbol")]
    public string Symbol { get; set; } = string.Empty;

    [JsonPropertyName("sizeBytes")]
    public long SizeBytes { get; set; }

    [JsonPropertyName("fileCount")]
    public int FileCount { get; set; }

    [JsonPropertyName("percentOfTotal")]
    public double PercentOfTotal { get; set; }

    [JsonPropertyName("oldestData")]
    public DateTimeOffset? OldestData { get; set; }

    [JsonPropertyName("newestData")]
    public DateTimeOffset? NewestData { get; set; }
}
