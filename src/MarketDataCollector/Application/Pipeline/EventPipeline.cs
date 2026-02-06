using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using System.Threading;
using MarketDataCollector.Application.Monitoring;
using MarketDataCollector.Application.Services;
using MarketDataCollector.Domain.Events;
using MarketDataCollector.Infrastructure.Performance;
using MarketDataCollector.Storage.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace MarketDataCollector.Application.Pipeline;

/// <summary>
/// High-throughput, backpressured pipeline that decouples producers from storage sinks.
/// Includes periodic flushing, capacity monitoring, and performance metrics.
/// </summary>
public sealed class EventPipeline : IMarketEventPublisher, IAsyncDisposable, IFlushable
{
    private readonly Channel<MarketEvent> _channel;
    private readonly IStorageSink _sink;
    private readonly ILogger<EventPipeline> _logger;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _consumer;
    private readonly Task? _flusher;
    private readonly int _capacity;
    private readonly bool _metricsEnabled;
    private readonly DroppedEventAuditTrail? _auditTrail;

    // Performance metrics
    private long _publishedCount;
    private long _droppedCount;
    private long _consumedCount;
    private long _peakQueueSize;
    private long _totalProcessingTimeNs;
    private long _lastFlushTimestamp;
    private bool _highWaterMarkWarned;

    // Configuration
    private readonly TimeSpan _flushInterval;
    private readonly int _batchSize;
    private readonly bool _enablePeriodicFlush;

    /// <summary>
    /// Maximum time to wait for the final flush during shutdown before giving up.
    /// Prevents the consumer task from hanging indefinitely if the sink is unresponsive.
    /// </summary>
    private static readonly TimeSpan FinalFlushTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Maximum time to wait for the consumer/flusher tasks to complete during disposal.
    /// Should be slightly longer than FinalFlushTimeout to allow the flush to timeout first.
    /// </summary>
    private static readonly TimeSpan DisposeTaskTimeout = TimeSpan.FromSeconds(35);

    /// <summary>
    /// Creates a new EventPipeline with configurable capacity and flush behavior.
    /// </summary>
    /// <param name="sink">The storage sink for persisting events.</param>
    /// <param name="capacity">Maximum number of events the queue can hold. Default is 100,000.</param>
    /// <param name="fullMode">Behavior when the queue is full. Default is DropOldest.</param>
    /// <param name="flushInterval">Interval between periodic flushes. Default is 5 seconds.</param>
    /// <param name="batchSize">Number of events to batch before writing. Default is 100.</param>
    /// <param name="enablePeriodicFlush">Whether to enable periodic flushing. Default is true.</param>
    /// <param name="logger">Optional logger for error reporting. When provided, enables logging for flush failures and disposal errors.</param>
    public EventPipeline(
        IStorageSink sink,
        int capacity = 100_000,
        BoundedChannelFullMode fullMode = BoundedChannelFullMode.DropOldest,
        TimeSpan? flushInterval = null,
        int batchSize = 100,
        bool enablePeriodicFlush = true,
        ILogger<EventPipeline>? logger = null,
        DroppedEventAuditTrail? auditTrail = null)
        : this(
            sink,
            new EventPipelinePolicy(capacity, fullMode),
            flushInterval,
            batchSize,
            enablePeriodicFlush,
            logger,
            auditTrail)
    {
    }

    /// <summary>
    /// Creates a new EventPipeline with a shared policy for capacity and backpressure.
    /// </summary>
    public EventPipeline(
        IStorageSink sink,
        EventPipelinePolicy policy,
        TimeSpan? flushInterval = null,
        int batchSize = 100,
        bool enablePeriodicFlush = true,
        ILogger<EventPipeline>? logger = null,
        DroppedEventAuditTrail? auditTrail = null)
    {
        _sink = sink ?? throw new ArgumentNullException(nameof(sink));
        _logger = logger ?? NullLogger<EventPipeline>.Instance;
        _auditTrail = auditTrail;
        if (policy is null)
            throw new ArgumentNullException(nameof(policy));
        _capacity = policy.Capacity;
        _metricsEnabled = policy.EnableMetrics;
        _flushInterval = flushInterval ?? TimeSpan.FromSeconds(5);
        _batchSize = Math.Max(1, batchSize);
        _enablePeriodicFlush = enablePeriodicFlush;

        _channel = policy.CreateChannel<MarketEvent>(singleReader: true, singleWriter: false);

        // Start consumer with long-running task
        _consumer = Task.Factory.StartNew(
            ConsumeAsync,
            _cts.Token,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default).Unwrap();

        // Start periodic flusher if enabled
        if (_enablePeriodicFlush)
        {
            _flusher = Task.Run(PeriodicFlushAsync);
        }

        Interlocked.Exchange(ref _lastFlushTimestamp, Stopwatch.GetTimestamp());
    }

    #region Public Properties - Pipeline Statistics

    /// <summary>Gets the total number of events successfully published to the pipeline.</summary>
    public long PublishedCount => Interlocked.Read(ref _publishedCount);

    /// <summary>Gets the total number of events dropped due to backpressure.</summary>
    public long DroppedCount => Interlocked.Read(ref _droppedCount);

    /// <summary>Gets the total number of events consumed and written to storage.</summary>
    public long ConsumedCount => Interlocked.Read(ref _consumedCount);

    /// <summary>Gets the peak queue size observed during operation.</summary>
    public long PeakQueueSize => Interlocked.Read(ref _peakQueueSize);

    /// <summary>Gets the current number of events in the queue.</summary>
    public int CurrentQueueSize => _channel.Reader.Count;

    /// <summary>Gets the queue capacity utilization as a percentage (0-100).</summary>
    public double QueueUtilization => (double)CurrentQueueSize / _capacity * 100;

    /// <summary>Gets the average processing time per event in microseconds.</summary>
    public double AverageProcessingTimeUs
    {
        get
        {
            var consumed = Interlocked.Read(ref _consumedCount);
            if (consumed == 0) return 0;
            var totalNs = Interlocked.Read(ref _totalProcessingTimeNs);
            return totalNs / 1000.0 / consumed;
        }
    }

    /// <summary>Gets the time since the last flush operation.</summary>
    public TimeSpan TimeSinceLastFlush
    {
        get
        {
            var lastTs = Interlocked.Read(ref _lastFlushTimestamp);
            return TimeSpan.FromTicks((long)((Stopwatch.GetTimestamp() - lastTs) *
                (TimeSpan.TicksPerSecond / (double)Stopwatch.Frequency)));
        }
    }

    #endregion

    /// <summary>
    /// Attempts to publish an event to the pipeline without blocking.
    /// Returns false if the queue is full (event will be dropped based on FullMode).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryPublish(in MarketEvent evt)
    {
        var written = _channel.Writer.TryWrite(evt);

        if (written)
        {
            Interlocked.Increment(ref _publishedCount);
            if (_metricsEnabled)
            {
                Metrics.IncPublished();
            }

            // Track peak queue size and warn on high utilization
            var currentSize = _channel.Reader.Count;
            var peak = Interlocked.Read(ref _peakQueueSize);
            if (currentSize > peak)
            {
                Interlocked.CompareExchange(ref _peakQueueSize, currentSize, peak);
            }

            var utilization = (double)currentSize / _capacity;
            if (utilization >= 0.8 && !_highWaterMarkWarned)
            {
                _highWaterMarkWarned = true;
                _logger.LogWarning(
                    "Pipeline queue utilization at {Utilization:P0} ({CurrentSize}/{Capacity}). Events may be dropped if queue fills. Consider increasing capacity or reducing event rate",
                    utilization, currentSize, _capacity);
            }
            else if (utilization < 0.5 && _highWaterMarkWarned)
            {
                _highWaterMarkWarned = false;
                _logger.LogInformation("Pipeline queue utilization recovered to {Utilization:P0}", utilization);
            }
        }
        else
        {
            Interlocked.Increment(ref _droppedCount);
            if (_metricsEnabled)
            {
                Metrics.IncDropped();
            }

            // Record dropped event to audit trail for gap-aware consumers
            if (_auditTrail != null)
            {
                _ = _auditTrail.RecordDroppedEventAsync(evt, "backpressure_queue_full");
            }
        }

        return written;
    }

    /// <summary>
    /// Publishes an event to the pipeline, waiting if necessary.
    /// </summary>
    public async ValueTask PublishAsync(MarketEvent evt, CancellationToken ct = default)
    {
        await _channel.Writer.WriteAsync(evt, ct).ConfigureAwait(false);
        Interlocked.Increment(ref _publishedCount);
        if (_metricsEnabled)
        {
            Metrics.IncPublished();
        }
    }

    /// <summary>
    /// Signals that no more events will be published.
    /// </summary>
    public void Complete() => _channel.Writer.TryComplete();

    /// <summary>
    /// Forces an immediate flush of buffered data to storage.
    /// </summary>
    public async Task FlushAsync(CancellationToken ct = default)
    {
        await _sink.FlushAsync(ct).ConfigureAwait(false);
        Interlocked.Exchange(ref _lastFlushTimestamp, Stopwatch.GetTimestamp());
    }

    /// <summary>
    /// Gets a snapshot of current pipeline statistics.
    /// </summary>
    public PipelineStatistics GetStatistics()
    {
        return new PipelineStatistics(
            PublishedCount: PublishedCount,
            DroppedCount: DroppedCount,
            ConsumedCount: ConsumedCount,
            CurrentQueueSize: CurrentQueueSize,
            PeakQueueSize: PeakQueueSize,
            QueueCapacity: _capacity,
            QueueUtilization: QueueUtilization,
            AverageProcessingTimeUs: AverageProcessingTimeUs,
            TimeSinceLastFlush: TimeSinceLastFlush,
            Timestamp: DateTimeOffset.UtcNow,
            HighWaterMarkWarned: _highWaterMarkWarned
        );
    }

    private async Task ConsumeAsync()
    {
        // Set thread priority for consistent throughput
        ThreadingUtilities.SetAboveNormalPriority();

        try
        {
            var batchBuffer = new List<MarketEvent>(_batchSize);

            while (await _channel.Reader.WaitToReadAsync(_cts.Token).ConfigureAwait(false))
            {
                var startTs = Stopwatch.GetTimestamp();

                // Drain up to _batchSize events from the channel
                batchBuffer.Clear();
                while (batchBuffer.Count < _batchSize && _channel.Reader.TryRead(out var evt))
                {
                    batchBuffer.Add(evt);
                }

                // Write the batch to the sink
                for (var i = 0; i < batchBuffer.Count; i++)
                {
                    await _sink.AppendAsync(batchBuffer[i], _cts.Token).ConfigureAwait(false);
                }

                Interlocked.Add(ref _consumedCount, batchBuffer.Count);

                // Track processing time amortized across the batch
                var elapsedNs = (long)(HighResolutionTimestamp.GetElapsedNanoseconds(startTs));
                Interlocked.Add(ref _totalProcessingTimeNs, elapsedNs);
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            // Final flush on shutdown with timeout to prevent indefinite hang
            try
            {
                using var flushTimeoutCts = new CancellationTokenSource(FinalFlushTimeout);
                await _sink.FlushAsync(flushTimeoutCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning(
                    "Final flush timed out after {TimeoutSeconds}s during pipeline shutdown. Consumed {ConsumedCount} events before timeout - some buffered data may be lost",
                    FinalFlushTimeout.TotalSeconds, _consumedCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Final flush failed during pipeline shutdown. Consumed {ConsumedCount} events before failure - potential data loss", _consumedCount);
            }
        }
    }

    private async Task PeriodicFlushAsync()
    {
        try
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                await Task.Delay(_flushInterval, _cts.Token).ConfigureAwait(false);

                try
                {
                    await _sink.FlushAsync(_cts.Token).ConfigureAwait(false);
                    Interlocked.Exchange(ref _lastFlushTimestamp, Stopwatch.GetTimestamp());
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Periodic flush failed. Queue size: {QueueSize}, consumed: {ConsumedCount}. May indicate storage issues", CurrentQueueSize, _consumedCount);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        _channel.Writer.TryComplete();

        // Wait for consumer with timeout to prevent indefinite hang
        // (the consumer's finally block has its own FinalFlushTimeout, but this
        // acts as a defense-in-depth safeguard)
        try
        {
            var completed = await Task.WhenAny(
                _consumer,
                Task.Delay(DisposeTaskTimeout)).ConfigureAwait(false);

            if (completed != _consumer)
            {
                _logger.LogWarning(
                    "Consumer task did not complete within {TimeoutSeconds}s during disposal. " +
                    "Published: {PublishedCount}, consumed: {ConsumedCount}. Proceeding with disposal",
                    DisposeTaskTimeout.TotalSeconds, _publishedCount, _consumedCount);
            }
            else
            {
                await _consumer.ConfigureAwait(false); // Observe any exception
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Consumer task failed during disposal. Published: {PublishedCount}, consumed: {ConsumedCount}", _publishedCount, _consumedCount);
        }

        if (_flusher is not null)
        {
            try
            {
                var completed = await Task.WhenAny(
                    _flusher,
                    Task.Delay(TimeSpan.FromSeconds(5))).ConfigureAwait(false);

                if (completed != _flusher)
                {
                    _logger.LogWarning("Flusher task did not complete within 5s during disposal. Proceeding with disposal");
                }
                else
                {
                    await _flusher.ConfigureAwait(false); // Observe any exception
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Flusher task failed during disposal. Last flush was {TimeSinceLastFlush} ago", TimeSinceLastFlush);
            }
        }

        _cts.Dispose();
        await _sink.DisposeAsync().ConfigureAwait(false);

        if (_auditTrail != null)
        {
            await _auditTrail.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Gets the dropped event audit trail, if configured.
    /// </summary>
    public DroppedEventAuditTrail? AuditTrail => _auditTrail;
}

/// <summary>
/// Snapshot of pipeline performance statistics.
/// </summary>
public readonly record struct PipelineStatistics(
    long PublishedCount,
    long DroppedCount,
    long ConsumedCount,
    int CurrentQueueSize,
    long PeakQueueSize,
    int QueueCapacity,
    double QueueUtilization,
    double AverageProcessingTimeUs,
    TimeSpan TimeSinceLastFlush,
    DateTimeOffset Timestamp,
    bool HighWaterMarkWarned = false
);
