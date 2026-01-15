using System.Text.Json;
using System.Threading.Channels;
using DataIngestion.Contracts.Messages;
using DataIngestion.Contracts.Services;
using DataIngestion.HistoricalService.Configuration;
using MassTransit;
using Serilog;

namespace DataIngestion.HistoricalService.Services;

/// <summary>
/// Background service that processes historical data backfill jobs.
/// Implements graceful shutdown to properly cancel in-flight jobs.
/// </summary>
public sealed class BackfillWorkerService : BackgroundService
{
    private readonly IBackfillJobManager _jobManager;
    private readonly IHistoricalDataProvider _provider;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly HistoricalServiceConfig _config;
    private readonly HistoricalMetrics _metrics;
    private readonly Serilog.ILogger _log = Log.ForContext<BackfillWorkerService>();
    private readonly Channel<BackfillRequest> _requestChannel;
    private readonly TimeSpan _shutdownTimeout = TimeSpan.FromSeconds(30);

    public BackfillWorkerService(
        IBackfillJobManager jobManager,
        IHistoricalDataProvider provider,
        IPublishEndpoint publishEndpoint,
        HistoricalServiceConfig config,
        HistoricalMetrics metrics)
    {
        _jobManager = jobManager;
        _provider = provider;
        _publishEndpoint = publishEndpoint;
        _config = config;
        _metrics = metrics;
        _requestChannel = Channel.CreateBounded<BackfillRequest>(100);
    }

    public bool SubmitRequest(BackfillRequest request)
    {
        return _requestChannel.Writer.TryWrite(request);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.Information("Backfill worker starting with max {Max} concurrent jobs",
            _config.Backfill.MaxConcurrentJobs);

        var workers = Enumerable.Range(0, _config.Backfill.MaxConcurrentJobs)
            .Select(_ => ProcessJobsAsync(stoppingToken))
            .ToList();

        await Task.WhenAll(workers);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _log.Information("Backfill worker service stopping - cancelling in-flight jobs...");

        // Complete the request channel so no new requests are accepted
        _requestChannel.Writer.TryComplete();

        using var timeoutCts = new CancellationTokenSource(_shutdownTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, timeoutCts.Token);

        try
        {
            // Mark active jobs as cancelled
            var activeJobs = _jobManager.GetActiveJobs()
                .Where(j => j.Status == "Running" || j.Status == "Queued")
                .ToList();

            foreach (var job in activeJobs)
            {
                _jobManager.CompleteJob(job.JobId, false, "Service shutdown");
                _log.Debug("Cancelled job {JobId} due to shutdown", job.JobId);
            }

            if (activeJobs.Count > 0)
            {
                _log.Information("Cancelled {Count} active backfill jobs due to shutdown", activeJobs.Count);
            }

            _log.Information("Backfill worker service stopped gracefully");
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Error during backfill worker shutdown");
        }

        await base.StopAsync(cancellationToken);
    }

    private async Task ProcessJobsAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var activeJobs = _jobManager.GetActiveJobs().ToList();
                var queuedJob = activeJobs.FirstOrDefault(j => j.Status == "Queued");

                if (queuedJob == null)
                {
                    await Task.Delay(1000, ct);
                    continue;
                }

                await ProcessJobAsync(queuedJob.JobId, ct);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _log.Error(ex, "Worker error");
                await Task.Delay(5000, ct);
            }
        }
    }

    private async Task ProcessJobAsync(Guid jobId, CancellationToken ct)
    {
        var job = _jobManager.GetJob(jobId);
        if (job == null) return;

        _log.Information("Processing backfill job {JobId} for {Symbol}", jobId, job.Symbol);

        try
        {
            var dataType = Enum.TryParse<HistoricalDataType>(job.DataType, true, out var dt)
                ? dt : HistoricalDataType.Trades;

            long recordCount = 0;
            var lastProgress = DateTimeOffset.UtcNow;

            await foreach (var record in _provider.FetchDataAsync(
                job.Symbol, dataType, job.StartDate, job.EndDate, BarTimeframe.Minute, ct))
            {
                // Publish to downstream services
                await _publishEndpoint.Publish(new HistoricalRecordMessage
                {
                    MessageId = Guid.NewGuid(),
                    CorrelationId = jobId,
                    Timestamp = record.Timestamp,
                    Source = "HistoricalService",
                    SchemaVersion = 1,
                    Symbol = record.Symbol,
                    RecordType = record.RecordType,
                    Data = JsonSerializer.Serialize(record.Data)
                }, ct);

                recordCount++;
                _metrics.RecordIngested();

                // Update progress periodically
                if ((DateTimeOffset.UtcNow - lastProgress).TotalSeconds >= _config.Backfill.ProgressReportIntervalSeconds)
                {
                    var totalDays = (job.EndDate - job.StartDate).TotalDays;
                    var elapsed = (record.Timestamp - job.StartDate).TotalDays;
                    var percent = totalDays > 0 ? elapsed / totalDays * 100 : 100;

                    _jobManager.UpdateProgress(jobId, recordCount, percent);
                    lastProgress = DateTimeOffset.UtcNow;
                }
            }

            _jobManager.CompleteJob(jobId, true);
            _metrics.RecordJobCompleted();
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Failed to process backfill job {JobId}", jobId);
            _jobManager.CompleteJob(jobId, false, ex.Message);
            _metrics.RecordJobFailed();
        }
    }
}

internal class HistoricalRecordMessage
{
    public Guid MessageId { get; init; }
    public Guid CorrelationId { get; init; }
    public DateTimeOffset Timestamp { get; init; }
    public required string Source { get; init; }
    public int SchemaVersion { get; init; }
    public required string Symbol { get; init; }
    public required string RecordType { get; init; }
    public required string Data { get; init; }
}
