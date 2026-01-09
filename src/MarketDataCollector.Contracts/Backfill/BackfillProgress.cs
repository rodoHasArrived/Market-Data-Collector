using System.Text.Json.Serialization;

namespace MarketDataCollector.Contracts.Backfill;

/// <summary>
/// Backfill job status constants.
/// </summary>
public static class BackfillJobStatus
{
    public const string Pending = "Pending";
    public const string Running = "Running";
    public const string Paused = "Paused";
    public const string Completed = "Completed";
    public const string Failed = "Failed";
    public const string Cancelled = "Cancelled";
}

/// <summary>
/// Symbol backfill status constants.
/// </summary>
public static class SymbolBackfillStatus
{
    public const string Pending = "Pending";
    public const string Downloading = "Downloading";
    public const string Completed = "Completed";
    public const string Failed = "Failed";
    public const string Skipped = "Skipped";
}

/// <summary>
/// Backfill progress tracking.
/// </summary>
public sealed class BackfillProgress
{
    [JsonPropertyName("jobId")]
    public string JobId { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("status")]
    public string Status { get; set; } = BackfillJobStatus.Pending;

    [JsonPropertyName("totalSymbols")]
    public int TotalSymbols { get; set; }

    [JsonPropertyName("completedSymbols")]
    public int CompletedSymbols { get; set; }

    [JsonPropertyName("failedSymbols")]
    public int FailedSymbols { get; set; }

    [JsonPropertyName("totalBars")]
    public long TotalBars { get; set; }

    [JsonPropertyName("downloadedBars")]
    public long DownloadedBars { get; set; }

    [JsonPropertyName("barsPerSecond")]
    public double BarsPerSecond { get; set; }

    [JsonPropertyName("estimatedSecondsRemaining")]
    public int? EstimatedSecondsRemaining { get; set; }

    [JsonPropertyName("startedAt")]
    public DateTime StartedAt { get; set; }

    [JsonPropertyName("completedAt")]
    public DateTime? CompletedAt { get; set; }

    [JsonPropertyName("symbolProgress")]
    public SymbolBackfillProgress[]? SymbolProgress { get; set; }

    [JsonPropertyName("currentProvider")]
    public string? CurrentProvider { get; set; }

    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets the overall progress percentage.
    /// </summary>
    [JsonIgnore]
    public double ProgressPercent => TotalSymbols > 0 ? (double)CompletedSymbols / TotalSymbols * 100 : 0;
}

/// <summary>
/// Per-symbol backfill progress.
/// </summary>
public sealed class SymbolBackfillProgress
{
    [JsonPropertyName("symbol")]
    public string Symbol { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = SymbolBackfillStatus.Pending;

    [JsonPropertyName("progress")]
    public double Progress { get; set; }

    [JsonPropertyName("barsDownloaded")]
    public int BarsDownloaded { get; set; }

    [JsonPropertyName("expectedBars")]
    public int ExpectedBars { get; set; }

    [JsonPropertyName("startedAt")]
    public DateTime? StartedAt { get; set; }

    [JsonPropertyName("completedAt")]
    public DateTime? CompletedAt { get; set; }

    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }

    [JsonPropertyName("provider")]
    public string? Provider { get; set; }

    [JsonPropertyName("retryCount")]
    public int RetryCount { get; set; }
}
