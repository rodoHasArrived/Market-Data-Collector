using System.Text.Json.Serialization;

namespace MarketDataCollector.Contracts.Api;

/// <summary>
/// Backpressure status response.
/// </summary>
public sealed record BackpressureStatusDto
{
    /// <summary>
    /// Gets a value indicating whether backpressure is currently active.
    /// </summary>
    [JsonPropertyName("isActive")]
    public bool IsActive { get; init; }

    /// <summary>
    /// Gets the current backpressure level (none, low, medium, high, critical).
    /// </summary>
    [JsonPropertyName("level")]
    public string Level { get; init; } = "none";

    /// <summary>
    /// Gets the queue utilization percentage (0.0 to 1.0).
    /// </summary>
    [JsonPropertyName("queueUtilization")]
    public double QueueUtilization { get; init; }

    /// <summary>
    /// Gets the total number of dropped events due to backpressure.
    /// </summary>
    [JsonPropertyName("droppedEvents")]
    public long DroppedEvents { get; init; }

    /// <summary>
    /// Gets the rate of dropped events per second.
    /// </summary>
    [JsonPropertyName("dropRate")]
    public double DropRate { get; init; }

    /// <summary>
    /// Gets the duration in seconds that backpressure has been active.
    /// </summary>
    [JsonPropertyName("durationSeconds")]
    public double DurationSeconds { get; init; }

    /// <summary>
    /// Gets an optional human-readable message about the backpressure condition.
    /// </summary>
    [JsonPropertyName("message")]
    public string? Message { get; init; }

    /// <summary>
    /// Gets a value indicating whether the queue depth has reached a warning threshold.
    /// </summary>
    [JsonPropertyName("queueDepthWarning")]
    public bool QueueDepthWarning { get; init; }
}

/// <summary>
/// Provider latency statistics.
/// </summary>
public sealed record ProviderLatencyStatsDto
{
    /// <summary>
    /// Gets the name of the provider.
    /// </summary>
    [JsonPropertyName("provider")]
    public string Provider { get; init; } = string.Empty;

    /// <summary>
    /// Gets the average latency in milliseconds.
    /// </summary>
    [JsonPropertyName("averageMs")]
    public double AverageMs { get; init; }

    [JsonPropertyName("minMs")]
    public double MinMs { get; init; }

    [JsonPropertyName("maxMs")]
    public double MaxMs { get; init; }

    [JsonPropertyName("p50Ms")]
    public double P50Ms { get; init; }

    [JsonPropertyName("p95Ms")]
    public double P95Ms { get; init; }

    [JsonPropertyName("p99Ms")]
    public double P99Ms { get; init; }

    [JsonPropertyName("sampleCount")]
    public long SampleCount { get; init; }

    [JsonPropertyName("isHealthy")]
    public bool IsHealthy { get; init; }
}

/// <summary>
/// Provider latency summary response.
/// </summary>
public sealed record ProviderLatencySummaryDto
{
    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; init; }

    [JsonPropertyName("providers")]
    public IReadOnlyList<ProviderLatencyStatsDto> Providers { get; init; } = Array.Empty<ProviderLatencyStatsDto>();

    [JsonPropertyName("globalAverageMs")]
    public double GlobalAverageMs { get; init; }

    [JsonPropertyName("globalP99Ms")]
    public double GlobalP99Ms { get; init; }
}

/// <summary>
/// Individual connection health information.
/// </summary>
public sealed record ConnectionHealthDto
{
    [JsonPropertyName("connectionId")]
    public string ConnectionId { get; init; } = string.Empty;

    [JsonPropertyName("providerName")]
    public string ProviderName { get; init; } = string.Empty;

    [JsonPropertyName("isConnected")]
    public bool IsConnected { get; init; }

    [JsonPropertyName("isHealthy")]
    public bool IsHealthy { get; init; }

    [JsonPropertyName("lastHeartbeatTime")]
    public DateTimeOffset? LastHeartbeatTime { get; init; }

    [JsonPropertyName("missedHeartbeats")]
    public int MissedHeartbeats { get; init; }

    [JsonPropertyName("uptimeSeconds")]
    public double UptimeSeconds { get; init; }

    [JsonPropertyName("averageLatencyMs")]
    public double AverageLatencyMs { get; init; }
}

/// <summary>
/// Connection health snapshot response.
/// </summary>
public sealed record ConnectionHealthSnapshotDto
{
    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; init; }

    [JsonPropertyName("totalConnections")]
    public int TotalConnections { get; init; }

    [JsonPropertyName("healthyConnections")]
    public int HealthyConnections { get; init; }

    [JsonPropertyName("unhealthyConnections")]
    public int UnhealthyConnections { get; init; }

    [JsonPropertyName("globalAverageLatencyMs")]
    public double GlobalAverageLatencyMs { get; init; }

    [JsonPropertyName("globalMinLatencyMs")]
    public double GlobalMinLatencyMs { get; init; }

    [JsonPropertyName("globalMaxLatencyMs")]
    public double GlobalMaxLatencyMs { get; init; }

    [JsonPropertyName("connections")]
    public IReadOnlyList<ConnectionHealthDto> Connections { get; init; } = Array.Empty<ConnectionHealthDto>();
}

/// <summary>
/// Error entry in the error log.
/// </summary>
public sealed record ErrorEntryDto
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; init; }

    [JsonPropertyName("level")]
    public string Level { get; init; } = "error";

    [JsonPropertyName("source")]
    public string? Source { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }

    [JsonPropertyName("exceptionType")]
    public string? ExceptionType { get; init; }

    [JsonPropertyName("context")]
    public string? Context { get; init; }

    [JsonPropertyName("symbol")]
    public string? Symbol { get; init; }

    [JsonPropertyName("provider")]
    public string? Provider { get; init; }
}

/// <summary>
/// Error statistics.
/// </summary>
public sealed record ErrorStatsDto
{
    [JsonPropertyName("totalErrors")]
    public int TotalErrors { get; init; }

    [JsonPropertyName("errorsInLastMinute")]
    public int ErrorsInLastMinute { get; init; }

    [JsonPropertyName("errorsInLastHour")]
    public int ErrorsInLastHour { get; init; }

    [JsonPropertyName("warningCount")]
    public int WarningCount { get; init; }

    [JsonPropertyName("errorCount")]
    public int ErrorCount { get; init; }

    [JsonPropertyName("criticalCount")]
    public int CriticalCount { get; init; }

    [JsonPropertyName("lastErrorTime")]
    public DateTimeOffset? LastErrorTime { get; init; }
}

/// <summary>
/// Errors response with entries and statistics.
/// </summary>
public sealed record ErrorsResponseDto
{
    [JsonPropertyName("errors")]
    public IReadOnlyList<ErrorEntryDto> Errors { get; init; } = Array.Empty<ErrorEntryDto>();

    [JsonPropertyName("stats")]
    public ErrorStatsDto? Stats { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }
}

/// <summary>
/// Prometheus metrics response (plain text format).
/// </summary>
public sealed record PrometheusMetricsDto
{
    public string Content { get; init; } = string.Empty;
}
