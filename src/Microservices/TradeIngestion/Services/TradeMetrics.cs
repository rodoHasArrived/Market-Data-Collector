using System.Diagnostics;
using Prometheus;

namespace DataIngestion.TradeService.Services;

/// <summary>
/// Prometheus metrics for trade ingestion service.
/// </summary>
public sealed class TradeMetrics
{
    private readonly Counter _tradesSubmitted;
    private readonly Counter _tradesProcessed;
    private readonly Counter _tradesDropped;
    private readonly Counter _duplicatesDetected;
    private readonly Counter _validationErrors;
    private readonly Counter _processorErrors;
    private readonly Counter _deadLetterEnqueued;
    private readonly Counter _deadLetterRetried;
    private readonly Counter _deadLetterPersisted;
    private readonly Counter _storageRetries;
    private readonly Counter _deduplicationFiltered;
    private readonly Gauge _queueDepth;
    private readonly Gauge _deadLetterQueueDepth;
    private readonly Histogram _processingLatency;
    private readonly Histogram _retryLatency;

    private long _totalProcessed;
    private long _errorCount;
    private long _deadLetterCount;
    private long _startTime;
    private double _latencySum;

    public long TradesProcessed => _totalProcessed;
    public long ErrorCount => _errorCount;
    public long DeadLetterCount => _deadLetterCount;

    public double TradesPerSecond
    {
        get
        {
            var elapsed = Stopwatch.GetElapsedTime(_startTime).TotalSeconds;
            return elapsed > 0 ? _totalProcessed / elapsed : 0;
        }
    }

    public double AverageLatencyMs => _totalProcessed > 0 ? _latencySum / _totalProcessed : 0;

    public TradeMetrics()
    {
        _startTime = Stopwatch.GetTimestamp();

        _tradesSubmitted = Metrics.CreateCounter(
            "trade_service_submitted_total",
            "Total trades submitted for processing");

        _tradesProcessed = Metrics.CreateCounter(
            "trade_service_processed_total",
            "Total trades successfully processed",
            new CounterConfiguration { LabelNames = ["symbol"] });

        _tradesDropped = Metrics.CreateCounter(
            "trade_service_dropped_total",
            "Total trades dropped due to queue overflow");

        _duplicatesDetected = Metrics.CreateCounter(
            "trade_service_duplicates_total",
            "Total duplicate trades detected");

        _validationErrors = Metrics.CreateCounter(
            "trade_service_validation_errors_total",
            "Total validation errors");

        _processorErrors = Metrics.CreateCounter(
            "trade_service_processor_errors_total",
            "Total processor errors during trade processing");

        _deadLetterEnqueued = Metrics.CreateCounter(
            "trade_service_dead_letter_enqueued_total",
            "Total trades moved to dead letter queue",
            new CounterConfiguration { LabelNames = ["reason"] });

        _deadLetterRetried = Metrics.CreateCounter(
            "trade_service_dead_letter_retried_total",
            "Total trades retried from dead letter queue",
            new CounterConfiguration { LabelNames = ["success"] });

        _deadLetterPersisted = Metrics.CreateCounter(
            "trade_service_dead_letter_persisted_total",
            "Total trades persisted to dead letter storage");

        _storageRetries = Metrics.CreateCounter(
            "trade_service_storage_retries_total",
            "Total storage retry attempts",
            new CounterConfiguration { LabelNames = ["attempt"] });

        _deduplicationFiltered = Metrics.CreateCounter(
            "trade_service_deduplication_filtered_total",
            "Total trades filtered by deduplication",
            new CounterConfiguration { LabelNames = ["reason"] });

        _queueDepth = Metrics.CreateGauge(
            "trade_service_queue_depth",
            "Current processing queue depth");

        _deadLetterQueueDepth = Metrics.CreateGauge(
            "trade_service_dead_letter_queue_depth",
            "Current dead letter queue depth");

        _processingLatency = Metrics.CreateHistogram(
            "trade_service_processing_latency_ms",
            "Trade processing latency in milliseconds",
            new HistogramConfiguration
            {
                Buckets = [0.01, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5, 10, 25, 50, 100]
            });

        _retryLatency = Metrics.CreateHistogram(
            "trade_service_retry_latency_ms",
            "Storage retry latency in milliseconds",
            new HistogramConfiguration
            {
                Buckets = [1, 5, 10, 25, 50, 100, 250, 500, 1000, 2500, 5000]
            });
    }

    public void RecordSubmission()
    {
        _tradesSubmitted.Inc();
    }

    public void RecordProcessed(int count, string? symbol = null)
    {
        Interlocked.Add(ref _totalProcessed, count);
        _tradesProcessed.WithLabels(symbol ?? "unknown").Inc(count);
    }

    public void RecordDropped()
    {
        _tradesDropped.Inc();
    }

    public void RecordDuplicate()
    {
        _duplicatesDetected.Inc();
    }

    public void RecordValidationError()
    {
        Interlocked.Increment(ref _errorCount);
        _validationErrors.Inc();
    }

    public void RecordProcessorError()
    {
        Interlocked.Increment(ref _errorCount);
        _processorErrors.Inc();
    }

    public void SetQueueDepth(int depth)
    {
        _queueDepth.Set(depth);
    }

    public void RecordLatency(double latencyMs)
    {
        _processingLatency.Observe(latencyMs);
        Interlocked.Exchange(ref _latencySum, _latencySum + latencyMs);
    }

    /// <summary>Records a trade being moved to dead letter queue.</summary>
    public void RecordDeadLetterEnqueued(string reason)
    {
        Interlocked.Increment(ref _deadLetterCount);
        _deadLetterEnqueued.WithLabels(reason).Inc();
    }

    /// <summary>Records a dead letter retry attempt.</summary>
    public void RecordDeadLetterRetried(bool success)
    {
        _deadLetterRetried.WithLabels(success ? "true" : "false").Inc();
        if (success)
        {
            Interlocked.Decrement(ref _deadLetterCount);
        }
    }

    /// <summary>Records a dead letter persisted to disk.</summary>
    public void RecordDeadLetterPersisted()
    {
        _deadLetterPersisted.Inc();
    }

    /// <summary>Records a storage retry attempt.</summary>
    public void RecordStorageRetry(int attempt)
    {
        _storageRetries.WithLabels(attempt.ToString()).Inc();
    }

    /// <summary>Records a trade filtered by deduplication.</summary>
    public void RecordDeduplicationFiltered(string reason)
    {
        _deduplicationFiltered.WithLabels(reason).Inc();
    }

    /// <summary>Sets the current dead letter queue depth.</summary>
    public void SetDeadLetterQueueDepth(int depth)
    {
        _deadLetterQueueDepth.Set(depth);
    }

    /// <summary>Records retry latency.</summary>
    public void RecordRetryLatency(double latencyMs)
    {
        _retryLatency.Observe(latencyMs);
    }
}
