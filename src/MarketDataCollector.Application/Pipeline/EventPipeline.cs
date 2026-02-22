using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using System.Threading;
using MarketDataCollector.Application.Monitoring;
using MarketDataCollector.Application.Services;
using MarketDataCollector.Core.Performance;
using MarketDataCollector.Domain.Events;
using MarketDataCollector.Infrastructure.Shared;
using MarketDataCollector.Storage.Archival;
using MarketDataCollector.Storage.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace MarketDataCollector.Application.Pipeline;

/// <summary>
/// High-throughput, backpressured pipeline that decouples producers from storage sinks.
/// Includes periodic flushing, capacity monitoring, performance metrics, and optional
/// Write-Ahead Log (WAL) integration for crash-safe durability.
/// </summary>
/// <remarks>
/// When a <see cref="WriteAheadLog"/> is provided, the pipeline ensures events are
/// persisted to the WAL before being written to the primary storage sink. On startup,
/// <see cref="RecoverAsync"/> replays any uncommitted WAL records to the sink, preventing
/// data loss from crashes. The consumer writes each event to the WAL, then to the sink,
/// and commits the WAL after each batch is flushed. For callers using
/// <see cref="PublishAsync"/>, the WAL write occurs at publish time for full durability.
/// </remarks>
public sealed class EventPipeline : IMarketEventPublisher, IAsyncDisposable, IFlushable
{
    private readonly Channel<MarketEvent> _channel;
    private readonly IStorageSink _sink;
    private readonly WriteAheadLog? _wal;
    private readonly ILogger<EventPipeline> _logger;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _consumer;
    private readonly Task? _flusher;
    private readonly int _capacity;
    private readonly bool _metricsEnabled;
    private readonly DroppedEventAuditTrail? _auditTrail;
    private readonly IEventMetrics _metrics;

    // Performance metrics
    private long _publishedCount;
    private long _droppedCount;
    private long _consumedCount;
    private long _recoveredCount;
    private long _peakQueueSize;
    private long _totalProcessingTimeNs;
    private long _lastFlushTimestamp;
    private bool _highWaterMarkWarned;

    // WAL tracking: last sequence committed to primary storage
    private long _lastCommittedWalSequence;

    // Configuration
    private readonly TimeSpan _flushInterval;
    private readonly int _batchSize;
    private readonly bool _enablePeriodicFlush;

    /// <summary>
    /// Default maximum time to wait for the final flush during shutdown before giving up.
    /// Prevents the consumer task from hanging indefinitely if the sink is unresponsive.
    /// </summary>
    private static readonly TimeSpan DefaultFinalFlushTimeout = TimeSpan.FromSeconds(30);

    private readonly TimeSpan _finalFlushTimeout;
    private readonly TimeSpan _disposeTaskTimeout;

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
    /// <param name="auditTrail">Optional audit trail for tracking dropped events.</param>
    /// <param name="wal">Optional Write-Ahead Log for crash-safe durability. When provided, events
    /// are written to the WAL before the primary sink. Call <see cref="RecoverAsync"/> on startup
    /// to replay any uncommitted records from a prior crash.</param>
    /// <param name="metrics">Optional event metrics for tracking pipeline throughput.</param>
    /// <param name="finalFlushTimeout">Optional timeout for the final flush during shutdown. Defaults to 30 seconds.</param>
    public EventPipeline(
        IStorageSink sink,
        int capacity = 100_000,
        BoundedChannelFullMode fullMode = BoundedChannelFullMode.DropOldest,
        TimeSpan? flushInterval = null,
        int batchSize = 100,
        bool enablePeriodicFlush = true,
        ILogger<EventPipeline>? logger = null,
        DroppedEventAuditTrail? auditTrail = null,
        WriteAheadLog? wal = null,
        IEventMetrics? metrics = null,
        TimeSpan? finalFlushTimeout = null)
        : this(
            sink,
            new EventPipelinePolicy(capacity, fullMode),
            flushInterval,
            batchSize,
            enablePeriodicFlush,
            logger,
            auditTrail,
            wal,
            metrics,
            finalFlushTimeout)
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
        DroppedEventAuditTrail? auditTrail = null,
        WriteAheadLog? wal = null,
        IEventMetrics? metrics = null,
        TimeSpan? finalFlushTimeout = null)
    {
        _sink = sink ?? throw new ArgumentNullException(nameof(sink));
        _logger = logger ?? NullLogger<EventPipeline>.Instance;
        _auditTrail = auditTrail;
        _wal = wal;
        _metrics = metrics ?? new DefaultEventMetrics();
        _finalFlushTimeout = finalFlushTimeout ?? DefaultFinalFlushTimeout;
        _disposeTaskTimeout = _finalFlushTimeout + TimeSpan.FromSeconds(5);
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
            _flusher = PeriodicFlushAsync();
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

    /// <summary>Gets the total number of events recovered from WAL on startup.</summary>
    public long RecoveredCount => Interlocked.Read(ref _recoveredCount);

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

    /// <summary>Gets whether a WAL is configured for this pipeline.</summary>
    public bool IsWalEnabled => _wal != null;

    #endregion

    /// <summary>
    /// Recovers uncommitted events from the WAL and replays them to the storage sink.
    /// Call this method once on startup, before publishing new events, to ensure
    /// data from a prior crash is not lost.
    /// </summary>
    /// <remarks>
    /// This method initializes the WAL and reads any records that were written
    /// but not committed (i.e., not yet confirmed persisted to the primary sink).
    /// Each recovered event is written to the sink and then the WAL is committed.
    /// If no WAL is configured, this method is a no-op.
    /// </remarks>
    public async Task RecoverAsync(CancellationToken ct = default)
    {
        if (_wal == null) return;

        _logger.LogInformation("Initializing WAL for pipeline recovery");
        await _wal.InitializeAsync(ct).ConfigureAwait(false);

        var recovered = 0;
        long maxRecoveredSequence = 0;

        await foreach (var walRecord in _wal.GetUncommittedRecordsAsync(ct).ConfigureAwait(false))
        {
            if (walRecord.RecordType == "COMMIT") continue;

            try
            {
                var evt = walRecord.DeserializePayload<MarketEvent>();
                if (evt != null)
                {
                    await _sink.AppendAsync(evt, ct).ConfigureAwait(false);
                    maxRecoveredSequence = Math.Max(maxRecoveredSequence, walRecord.Sequence);
                    recovered++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize WAL record {Sequence} during recovery", walRecord.Sequence);
            }
        }

        if (recovered > 0)
        {
            await _sink.FlushAsync(ct).ConfigureAwait(false);
            await _wal.CommitAsync(maxRecoveredSequence, ct).ConfigureAwait(false);
            await _wal.TruncateAsync(maxRecoveredSequence, ct).ConfigureAwait(false);

            Interlocked.Add(ref _recoveredCount, recovered);

            _logger.LogInformation(
                "Recovered {RecoveredCount} uncommitted events from WAL through sequence {MaxSequence}",
                recovered, maxRecoveredSequence);
        }
        else
        {
            _logger.LogInformation("WAL recovery complete, no uncommitted events found");
        }
    }

    /// <summary>
    /// Attempts to publish an event to the pipeline without blocking.
    /// Returns false if the queue is full (event will be dropped based on FullMode).
    /// </summary>
    /// <remarks>
    /// When WAL is enabled, the WAL write occurs in the consumer task (not at publish time)
    /// to preserve the non-blocking contract of this method. Events are WAL-protected
    /// once they reach the consumer. For full publish-time WAL protection, use
    /// <see cref="PublishAsync"/> instead.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryPublish(in MarketEvent evt)
    {
        var written = _channel.Writer.TryWrite(evt);

        if (written)
        {
            Interlocked.Increment(ref _publishedCount);
            if (_metricsEnabled)
            {
                _metrics.IncPublished();
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
                _metrics.IncDropped();
            }

            // Record dropped event to audit trail for gap-aware consumers
            if (_auditTrail != null)
            {
                _auditTrail.RecordDroppedEventAsync(evt, "backpressure_queue_full")
                    .ObserveException(operation: "audit trail recording dropped event");
            }
        }

        return written;
    }

    /// <summary>
    /// Publishes an event to the pipeline, waiting if necessary.
    /// When WAL is enabled, the event is written to the WAL before being queued,
    /// providing full durability for the async publish path.
    /// </summary>
    public async ValueTask PublishAsync(MarketEvent evt, CancellationToken ct = default)
    {
        if (_wal != null)
        {
            await _wal.AppendAsync(evt, evt.Type.ToString(), ct).ConfigureAwait(false);
        }

        await _channel.Writer.WriteAsync(evt, ct).ConfigureAwait(false);
        Interlocked.Increment(ref _publishedCount);
        if (_metricsEnabled)
        {
            _metrics.IncPublished();
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

                long maxWalSequence = _lastCommittedWalSequence;

                // Write each event: WAL first (if enabled), then sink
                for (var i = 0; i < batchBuffer.Count; i++)
                {
                    var evt = batchBuffer[i];

                    if (_wal != null)
                    {
                        var walRecord = await _wal.AppendAsync(evt, evt.Type.ToString(), _cts.Token).ConfigureAwait(false);
                        maxWalSequence = Math.Max(maxWalSequence, walRecord.Sequence);
                    }

                    await _sink.AppendAsync(evt, _cts.Token).ConfigureAwait(false);
                }

                // Commit the WAL batch after all events are written to the sink
                if (_wal != null && maxWalSequence > _lastCommittedWalSequence)
                {
                    await _sink.FlushAsync(_cts.Token).ConfigureAwait(false);
                    await _wal.CommitAsync(maxWalSequence, _cts.Token).ConfigureAwait(false);
                    _lastCommittedWalSequence = maxWalSequence;
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
                using var flushTimeoutCts = new CancellationTokenSource(_finalFlushTimeout);
                await _sink.FlushAsync(flushTimeoutCts.Token).ConfigureAwait(false);

                // Final WAL commit for any remaining uncommitted records
                if (_wal != null && _lastCommittedWalSequence > 0)
                {
                    await _wal.CommitAsync(_lastCommittedWalSequence, flushTimeoutCts.Token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning(
                    "Final flush timed out after {TimeoutSeconds}s during pipeline shutdown. Consumed {ConsumedCount} events before timeout - some buffered data may be lost",
                    _finalFlushTimeout.TotalSeconds, _consumedCount);
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

                    // Periodically truncate committed WAL files to reclaim disk space
                    if (_wal != null && _lastCommittedWalSequence > 0)
                    {
                        await _wal.TruncateAsync(_lastCommittedWalSequence, _cts.Token).ConfigureAwait(false);
                    }
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
        // (the consumer's finally block has its own _finalFlushTimeout, but this
        // acts as a defense-in-depth safeguard)
        try
        {
            var completed = await Task.WhenAny(
                _consumer,
                Task.Delay(_disposeTaskTimeout)).ConfigureAwait(false);

            if (completed != _consumer)
            {
                _logger.LogWarning(
                    "Consumer task did not complete within {TimeoutSeconds}s during disposal. " +
                    "Published: {PublishedCount}, consumed: {ConsumedCount}. Proceeding with disposal",
                    _disposeTaskTimeout.TotalSeconds, _publishedCount, _consumedCount);
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

        if (_wal != null)
        {
            await _wal.DisposeAsync().ConfigureAwait(false);
        }

        if (_auditTrail != null)
        {
            await _auditTrail.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Gets the dropped event audit trail, if configured.
    /// </summary>
    public DroppedEventAuditTrail? AuditTrail => _auditTrail;

    /// <summary>
    /// Gets the queue capacity.
    /// </summary>
    public int QueueCapacity => _capacity;

    /// <summary>
    /// Gets the injected event metrics instance.
    /// </summary>
    public IEventMetrics EventMetrics => _metrics;
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
