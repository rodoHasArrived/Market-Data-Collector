using System.Collections.Concurrent;
using DataIngestion.Contracts.Messages;
using DataIngestion.Contracts.Services;
using Serilog;

namespace DataIngestion.HistoricalService.Services;

public interface IBackfillJobManager
{
    BackfillJobStatus CreateJob(BackfillRequest request);
    BackfillJobStatus? GetJob(Guid jobId);
    IEnumerable<BackfillJobStatus> GetActiveJobs();
    int GetActiveJobCount();
    void UpdateProgress(Guid jobId, long records, double percent);
    void CompleteJob(Guid jobId, bool success, string? error = null);
    void CancelJob(Guid jobId);
}

public sealed class BackfillJobManager : IBackfillJobManager
{
    private readonly ConcurrentDictionary<Guid, BackfillJob> _jobs = new();
    private readonly ILogger _log = Log.ForContext<BackfillJobManager>();

    public BackfillJobStatus CreateJob(BackfillRequest request)
    {
        var job = new BackfillJob
        {
            JobId = Guid.NewGuid(),
            Symbol = request.Symbol,
            DataType = request.DataType,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Source = request.Source,
            Status = "Queued",
            CreatedAt = DateTimeOffset.UtcNow,
            Priority = request.Priority
        };

        _jobs[job.JobId] = job;
        _log.Information("Created backfill job {JobId} for {Symbol} ({DataType})",
            job.JobId, job.Symbol, job.DataType);

        return ToStatus(job);
    }

    public BackfillJobStatus? GetJob(Guid jobId)
    {
        return _jobs.TryGetValue(jobId, out var job) ? ToStatus(job) : null;
    }

    public IEnumerable<BackfillJobStatus> GetActiveJobs()
    {
        return _jobs.Values
            .Where(j => j.Status is "Queued" or "Running")
            .Select(ToStatus);
    }

    public int GetActiveJobCount()
    {
        return _jobs.Values.Count(j => j.Status is "Queued" or "Running");
    }

    public void UpdateProgress(Guid jobId, long records, double percent)
    {
        if (_jobs.TryGetValue(jobId, out var job))
        {
            job.RecordsProcessed = records;
            job.ProgressPercent = percent;
            job.Status = "Running";
            job.StartedAt ??= DateTimeOffset.UtcNow;
        }
    }

    public void CompleteJob(Guid jobId, bool success, string? error = null)
    {
        if (_jobs.TryGetValue(jobId, out var job))
        {
            job.Status = success ? "Completed" : "Failed";
            job.CompletedAt = DateTimeOffset.UtcNow;
            job.ErrorMessage = error;
            job.ProgressPercent = success ? 100 : job.ProgressPercent;
            _log.Information("Job {JobId} {Status}: {Records} records",
                jobId, job.Status, job.RecordsProcessed);
        }
    }

    public void CancelJob(Guid jobId)
    {
        if (_jobs.TryGetValue(jobId, out var job))
        {
            job.Status = "Cancelled";
            job.CompletedAt = DateTimeOffset.UtcNow;
            job.CancellationToken?.Cancel();
        }
    }

    private static BackfillJobStatus ToStatus(BackfillJob job) => new(
        job.JobId, job.Symbol, job.Status, job.StartDate, job.EndDate,
        job.RecordsProcessed, job.TotalRecords, job.ProgressPercent,
        job.StartedAt, job.CompletedAt, job.ErrorMessage);

    internal class BackfillJob
    {
        public Guid JobId { get; init; }
        public required string Symbol { get; init; }
        public required string DataType { get; init; }
        public DateTimeOffset StartDate { get; init; }
        public DateTimeOffset EndDate { get; init; }
        public required string Source { get; init; }
        public string Status { get; set; } = "Queued";
        public DateTimeOffset CreatedAt { get; init; }
        public DateTimeOffset? StartedAt { get; set; }
        public DateTimeOffset? CompletedAt { get; set; }
        public long RecordsProcessed { get; set; }
        public long? TotalRecords { get; set; }
        public double ProgressPercent { get; set; }
        public string? ErrorMessage { get; set; }
        public int Priority { get; init; }
        public CancellationTokenSource? CancellationToken { get; set; }
    }
}
