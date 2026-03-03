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
/// and commits the WAL after each batch is flushed. Both <see cref="TryPublish"/> and
/// <see cref="PublishAsync"/> defer WAL writes to the consumer to ensure each event is
/// recorded exactly once, preventing duplicate records during recovery.
/// </remarks>
public sealed class EventPipeline : IMarketEventPublisher, IBackpressureSignal, IAsyncDisposable, IFlushable
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
    private readonly IEventValidator? _validator;
    private readonly DeadLetterSink? _deadLetterSink;
    private int _disposed;
    private volatile bool _consumerBusy;

    // Performance metrics
    private long _publishedCount;
    private long _droppedCount;
    private long _consumedCount;
    private long _recoveredCount;
    private long _rejectedCount;
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
    /// <param name="validator">Optional event validator for pre-persistence validation.</param>
    /// <param name="deadLetterSink">Optional dead-letter sink for rejected events.</param>
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
        TimeSpan? finalFlushTimeout = null,
        IEventValidator? validator = null,
        DeadLetterSink? deadLetterSink = null)
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
            finalFlushTimeout,
            validator,
            deadLetterSink)
    {
    }

    /// <summary>
    /// Creates a new EventPipeline with a shared policy for capacity and backpressure.
    /// </summary>
    /// <param name="validator">Optional event validator. When provided, events that fail validation
    /// are routed to the <paramref name="deadLetterSink"/> and excluded from primary storage.</param>
    /// <param name="deadLetterSink">Optional dead-letter sink for events rejected by the validator.</param>
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
        TimeSpan? finalFlushTimeout = null,
        IEventValidator? validator = null,
        DeadLetterSink? deadLetterSink = null)
    {
        _sink = sink ?? throw new ArgumentNullException(nameof(sink));
        _logger = logger ?? NullLogger<EventPipeline>.Instance;
        _auditTrail = auditTrail;
        _wal = wal;
        _metrics = metrics ?? new DefaultEventMetrics();
        _validator = validator;
        _deadLetterSink = deadLetterSink;
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

    /// <summary>Gets the total number of events rejected by the validator and sent to the dead-letter sink.</summary>
    public long RejectedCount => Interlocked.Read(ref _rejectedCount);

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

    /// <summary>Gets whether event validation is enabled for this pipeline.</summary>
    public bool IsValidationEnabled => _validator != null;

    /// <summary>
    /// Returns <see langword="true"/> when the queue utilization has reached or exceeded 80 %.
    /// Upstream producers should observe this signal and slow down publishing to avoid data loss.
    /// </summary>
    public bool IsUnderPressure => _highWaterMarkWarned;

    // IBackpressureSignal: return a 0–1 fraction while the public property keeps 0–100 for
    // backwards compatibility with callers that already use it for display purposes.
    double IBackpressureSignal.QueueUtilization => QueueUtilization / 100.0;

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

        // Emit WAL recovery metrics to Prometheus
        PrometheusMetrics.RecordWalRecovery(
            _wal.LastRecoveryEventCount,
            _wal.LastRecoveryDurationMs / 1000.0);
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
    /// When WAL is enabled, the WAL write is performed by the consumer task
    /// to avoid duplicate records during recovery.
    /// </summary>
    public async ValueTask PublishAsync(MarketEvent evt, CancellationToken ct = default)
    {
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
    /// Waits for the consumer to process all currently-queued events, then forces
    /// an immediate flush of buffered data to storage.
    /// </summary>
    /// <remarks>
    /// If events were dropped due to backpressure during the flush window, a warning
    /// is logged. The flush still writes all events that <em>were</em> consumed to
    /// storage — it does not suppress the flush because of drops — but callers should
    /// treat the warning as an indication that the result set is incomplete.
    /// </remarks>
    public async Task FlushAsync(CancellationToken ct = default)
    {
        // Capture the drop baseline so we can report new drops that occurred
        // during this flush window (indicates data loss the caller may not expect).
        var droppedAtStart = Interlocked.Read(ref _droppedCount);

        // Wait for the consumer to process all currently-queued events.
        // IMPORTANT: We only wait for _consumed_ events to reach the target,
        // NOT consumed + dropped. Dropped events were never consumed and are
        // NOT in storage, so counting them as "accounted for" would let
        // FlushAsync return success while data is silently missing.
        // The secondary check (channel empty + consumer idle) handles the
        // DropOldest case where published events are silently discarded by
        // the channel — those events will never be consumed, but we should
        // wait until the channel is drained and the consumer is quiescent.
        var targetPublished = Interlocked.Read(ref _publishedCount);

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            var consumed = Interlocked.Read(ref _consumedCount);

            // All published events have been consumed (accounting for rejected events
            // which were read from the channel but not persisted to the primary sink)
            if (consumed + Interlocked.Read(ref _rejectedCount) >= targetPublished)
                break;

            // Channel is empty — check if the consumer has finished its batch.
            // This handles the DropOldest case where events were silently discarded
            // by the channel before reaching the consumer.
            if (_channel.Reader.Count == 0 && !_consumerBusy)
            {
                await Task.Delay(10, ct).ConfigureAwait(false);
                var newConsumed = Interlocked.Read(ref _consumedCount);
                if (_channel.Reader.Count == 0 && !_consumerBusy && newConsumed == consumed)
                    break; // Consumer is idle, nothing left to process
            }
            else
            {
                await Task.Delay(1, ct).ConfigureAwait(false);
            }
        }

        await _sink.FlushAsync(ct).ConfigureAwait(false);
        Interlocked.Exchange(ref _lastFlushTimestamp, Stopwatch.GetTimestamp());

        // Warn callers if events were dropped during this flush window so they
        // understand that the returned flush is not a full-fidelity confirmation.
        var newDrops = Interlocked.Read(ref _droppedCount) - droppedAtStart;
        if (newDrops > 0)
        {
            _logger.LogWarning(
                "FlushAsync completed but {DroppedCount} event(s) were dropped due to backpressure during this flush window and are NOT in storage. " +
                "Consider increasing pipeline capacity or reducing event rate.",
                newDrops);
        }
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
                _consumerBusy = true;
                var startTs = Stopwatch.GetTimestamp();

                try
                {
                    // Drain up to _batchSize events from the channel
                    batchBuffer.Clear();
                    while (batchBuffer.Count < _batchSize && _channel.Reader.TryRead(out var evt))
                    {
                        batchBuffer.Add(evt);
                    }

                    long maxWalSequence = _lastCommittedWalSequence;

                    // Write each event: validate → WAL (if enabled) → sink
                    for (var i = 0; i < batchBuffer.Count; i++)
                    {
                        var evt = batchBuffer[i];

                        // Validate event before persistence (when a validator is configured)
                        if (_validator != null)
                        {
                            var validationResult = _validator.Validate(in evt);
                            if (!validationResult.IsValid)
                            {
                                Interlocked.Increment(ref _rejectedCount);
                                if (_deadLetterSink != null)
                                {
                                    await _deadLetterSink.RecordAsync(evt, validationResult.Errors, _cts.Token).ConfigureAwait(false);
                                }
                                continue; // Skip persisting invalid events
                            }
                        }

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
                }
                finally
                {
                    _consumerBusy = false;
                }

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
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return; // Already disposed

        // Signal no more events will be published so the consumer can drain
        // remaining items from the channel and exit naturally.
        _channel.Writer.TryComplete();

        // Wait for consumer to drain the channel. Only force-cancel as a
        // timeout fallback to prevent indefinite hang.
        try
        {
            var completed = await Task.WhenAny(
                _consumer,
                Task.Delay(_disposeTaskTimeout)).ConfigureAwait(false);

            if (completed != _consumer)
            {
                _logger.LogWarning(
                    "Consumer task did not complete within {TimeoutSeconds}s during disposal. " +
                    "Published: {PublishedCount}, consumed: {ConsumedCount}. Force-cancelling",
                    _disposeTaskTimeout.TotalSeconds, _publishedCount, _consumedCount);

                await _cts.CancelAsync().ConfigureAwait(false);

                // Give a short grace period after force-cancel
                await Task.WhenAny(_consumer, Task.Delay(TimeSpan.FromSeconds(5))).ConfigureAwait(false);
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

        // Cancel the CTS to stop the periodic flusher
        if (!_cts.IsCancellationRequested)
            await _cts.CancelAsync().ConfigureAwait(false);

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

        if (_deadLetterSink != null)
        {
            await _deadLetterSink.DisposeAsync().ConfigureAwait(false);
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
