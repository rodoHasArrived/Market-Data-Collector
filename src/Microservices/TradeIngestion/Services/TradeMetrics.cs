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
    private readonly Gauge _queueDepth;
    private readonly Histogram _processingLatency;

    private long _totalProcessed;
    private long _errorCount;
    private long _startTime;
    private double _latencySum;

    public long TradesProcessed => _totalProcessed;
    public long ErrorCount => _errorCount;

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

        _queueDepth = Metrics.CreateGauge(
            "trade_service_queue_depth",
            "Current processing queue depth");

        _processingLatency = Metrics.CreateHistogram(
            "trade_service_processing_latency_ms",
            "Trade processing latency in milliseconds",
            new HistogramConfiguration
            {
                Buckets = [0.01, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5, 10, 25, 50, 100]
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
}
