using Prometheus;

namespace DataIngestion.HistoricalService.Services;

public sealed class HistoricalMetrics
{
    private readonly Counter _recordsIngested = Metrics.CreateCounter("historical_records_ingested_total", "Records ingested");
    private readonly Counter _jobsCompleted = Metrics.CreateCounter("historical_jobs_completed_total", "Jobs completed");
    private readonly Counter _jobsFailed = Metrics.CreateCounter("historical_jobs_failed_total", "Jobs failed");

    private long _totalRecords;
    private long _completed;

    public long TotalRecordsIngested => _totalRecords;
    public long CompletedJobs => _completed;

    public void RecordIngested()
    {
        Interlocked.Increment(ref _totalRecords);
        _recordsIngested.Inc();
    }

    public void RecordJobCompleted()
    {
        Interlocked.Increment(ref _completed);
        _jobsCompleted.Inc();
    }

    public void RecordJobFailed() => _jobsFailed.Inc();
}
