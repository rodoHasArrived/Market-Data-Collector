using System.Collections.Concurrent;
using System.Text.Json.Serialization;

namespace MarketDataCollector.Application.Backfill;

/// <summary>
/// Tracks backfill progress in real-time with ETA estimation and per-symbol breakdowns.
/// Implements improvement 9.2 from the high-value improvements brainstorm.
/// </summary>
public sealed class BackfillProgressTracker
{
    private readonly ConcurrentDictionary<string, BackfillJobProgress> _activeJobs = new();

    /// <summary>
    /// Starts tracking a new backfill job.
    /// </summary>
    public string StartJob(string provider, string[] symbols, DateOnly? from, DateOnly? to)
    {
        var jobId = $"bf_{DateTimeOffset.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid().ToString("N")[..6]}";

        var progress = new BackfillJobProgress
        {
            JobId = jobId,
            Provider = provider,
            TotalSymbols = symbols.Length,
            Symbols = symbols,
            From = from,
            To = to,
            StartedAt = DateTimeOffset.UtcNow,
            Status = BackfillJobStatus.Running
        };

        foreach (var symbol in symbols)
        {
            progress.SymbolProgress[symbol] = new SymbolBackfillProgress
            {
                Symbol = symbol,
                Status = SymbolBackfillStatus.Pending
            };
        }

        _activeJobs[jobId] = progress;
        return jobId;
    }

    /// <summary>
    /// Marks a symbol as currently being processed.
    /// </summary>
    public void StartSymbol(string jobId, string symbol)
    {
        if (!_activeJobs.TryGetValue(jobId, out var job)) return;
        if (!job.SymbolProgress.TryGetValue(symbol, out var sp)) return;

        sp.Status = SymbolBackfillStatus.InProgress;
        sp.StartedAt = DateTimeOffset.UtcNow;
        job.CurrentSymbol = symbol;
    }

    /// <summary>
    /// Records bars written for a symbol during backfill.
    /// </summary>
    public void RecordBars(string jobId, string symbol, long barsWritten)
    {
        if (!_activeJobs.TryGetValue(jobId, out var job)) return;
        if (!job.SymbolProgress.TryGetValue(symbol, out var sp)) return;

        sp.BarsWritten += barsWritten;
        job.TotalBarsWritten += barsWritten;
    }

    /// <summary>
    /// Marks a symbol as completed.
    /// </summary>
    public void CompleteSymbol(string jobId, string symbol, long totalBars)
    {
        if (!_activeJobs.TryGetValue(jobId, out var job)) return;
        if (!job.SymbolProgress.TryGetValue(symbol, out var sp)) return;

        sp.Status = SymbolBackfillStatus.Completed;
        sp.CompletedAt = DateTimeOffset.UtcNow;
        sp.BarsWritten = totalBars;
        job.CompletedSymbols++;
    }

    /// <summary>
    /// Marks a symbol as failed.
    /// </summary>
    public void FailSymbol(string jobId, string symbol, string error)
    {
        if (!_activeJobs.TryGetValue(jobId, out var job)) return;
        if (!job.SymbolProgress.TryGetValue(symbol, out var sp)) return;

        sp.Status = SymbolBackfillStatus.Failed;
        sp.CompletedAt = DateTimeOffset.UtcNow;
        sp.Error = error;
        job.FailedSymbols++;
    }

    /// <summary>
    /// Marks the entire job as completed.
    /// </summary>
    public void CompleteJob(string jobId, bool success)
    {
        if (!_activeJobs.TryGetValue(jobId, out var job)) return;

        job.Status = success ? BackfillJobStatus.Completed : BackfillJobStatus.Failed;
        job.CompletedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Gets progress for a specific job, including ETA calculation.
    /// </summary>
    public BackfillProgressSnapshot? GetProgress(string jobId)
    {
        if (!_activeJobs.TryGetValue(jobId, out var job)) return null;
        return BuildSnapshot(job);
    }

    /// <summary>
    /// Gets progress for all active and recently completed jobs.
    /// </summary>
    public IReadOnlyList<BackfillProgressSnapshot> GetAllProgress()
    {
        // Clean up old completed jobs (keep last hour)
        var cutoff = DateTimeOffset.UtcNow.AddHours(-1);
        foreach (var kvp in _activeJobs.Where(j => j.Value.CompletedAt.HasValue && j.Value.CompletedAt < cutoff))
        {
            _activeJobs.TryRemove(kvp.Key, out _);
        }

        return _activeJobs.Values.Select(BuildSnapshot).ToList();
    }

    private static BackfillProgressSnapshot BuildSnapshot(BackfillJobProgress job)
    {
        var elapsed = DateTimeOffset.UtcNow - job.StartedAt;
        var completedCount = job.CompletedSymbols + job.FailedSymbols;
        var progressPercent = job.TotalSymbols > 0
            ? (double)completedCount / job.TotalSymbols * 100.0
            : 0;

        // ETA calculation based on average time per symbol
        TimeSpan? estimatedRemaining = null;
        DateTimeOffset? estimatedCompletionTime = null;

        if (completedCount > 0 && completedCount < job.TotalSymbols)
        {
            var avgTimePerSymbol = elapsed / completedCount;
            var remainingSymbols = job.TotalSymbols - completedCount;
            estimatedRemaining = avgTimePerSymbol * remainingSymbols;
            estimatedCompletionTime = DateTimeOffset.UtcNow + estimatedRemaining;
        }
        else if (completedCount >= job.TotalSymbols)
        {
            estimatedRemaining = TimeSpan.Zero;
        }

        return new BackfillProgressSnapshot
        {
            JobId = job.JobId,
            Provider = job.Provider,
            Status = job.Status.ToString().ToLowerInvariant(),
            TotalSymbols = job.TotalSymbols,
            CompletedSymbols = job.CompletedSymbols,
            FailedSymbols = job.FailedSymbols,
            CurrentSymbol = job.CurrentSymbol,
            TotalBarsWritten = job.TotalBarsWritten,
            ProgressPercent = Math.Round(progressPercent, 1),
            ElapsedSeconds = Math.Round(elapsed.TotalSeconds, 1),
            EstimatedRemainingSeconds = estimatedRemaining.HasValue
                ? Math.Round(estimatedRemaining.Value.TotalSeconds, 1)
                : null,
            EstimatedCompletionTime = estimatedCompletionTime,
            StartedAt = job.StartedAt,
            CompletedAt = job.CompletedAt,
            SymbolDetails = job.SymbolProgress.Values.Select(sp => new SymbolProgressSnapshot
            {
                Symbol = sp.Symbol,
                Status = sp.Status.ToString().ToLowerInvariant(),
                BarsWritten = sp.BarsWritten,
                Error = sp.Error,
                DurationSeconds = sp.CompletedAt.HasValue && sp.StartedAt.HasValue
                    ? Math.Round((sp.CompletedAt.Value - sp.StartedAt.Value).TotalSeconds, 1)
                    : null
            }).ToList()
        };
    }
}

internal sealed class BackfillJobProgress
{
    public string JobId { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public int TotalSymbols { get; set; }
    public string[] Symbols { get; set; } = Array.Empty<string>();
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public BackfillJobStatus Status { get; set; }
    public string? CurrentSymbol { get; set; }
    public int CompletedSymbols { get; set; }
    public int FailedSymbols { get; set; }
    public long TotalBarsWritten { get; set; }
    public ConcurrentDictionary<string, SymbolBackfillProgress> SymbolProgress { get; } = new();
}

internal sealed class SymbolBackfillProgress
{
    public string Symbol { get; set; } = string.Empty;
    public SymbolBackfillStatus Status { get; set; }
    public long BarsWritten { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? Error { get; set; }
}

internal enum BackfillJobStatus { Pending, Running, Completed, Failed }
internal enum SymbolBackfillStatus { Pending, InProgress, Completed, Failed }

/// <summary>
/// Snapshot of backfill progress for API consumption.
/// </summary>
public sealed class BackfillProgressSnapshot
{
    [JsonPropertyName("jobId")]
    public string JobId { get; set; } = string.Empty;

    [JsonPropertyName("provider")]
    public string Provider { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("totalSymbols")]
    public int TotalSymbols { get; set; }

    [JsonPropertyName("completedSymbols")]
    public int CompletedSymbols { get; set; }

    [JsonPropertyName("failedSymbols")]
    public int FailedSymbols { get; set; }

    [JsonPropertyName("currentSymbol")]
    public string? CurrentSymbol { get; set; }

    [JsonPropertyName("totalBarsWritten")]
    public long TotalBarsWritten { get; set; }

    [JsonPropertyName("progressPercent")]
    public double ProgressPercent { get; set; }

    [JsonPropertyName("elapsedSeconds")]
    public double ElapsedSeconds { get; set; }

    [JsonPropertyName("estimatedRemainingSeconds")]
    public double? EstimatedRemainingSeconds { get; set; }

    [JsonPropertyName("estimatedCompletionTime")]
    public DateTimeOffset? EstimatedCompletionTime { get; set; }

    [JsonPropertyName("startedAt")]
    public DateTimeOffset StartedAt { get; set; }

    [JsonPropertyName("completedAt")]
    public DateTimeOffset? CompletedAt { get; set; }

    [JsonPropertyName("symbolDetails")]
    public List<SymbolProgressSnapshot> SymbolDetails { get; set; } = new();
}

/// <summary>
/// Per-symbol progress within a backfill job.
/// </summary>
public sealed class SymbolProgressSnapshot
{
    [JsonPropertyName("symbol")]
    public string Symbol { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("barsWritten")]
    public long BarsWritten { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("durationSeconds")]
    public double? DurationSeconds { get; set; }
}
