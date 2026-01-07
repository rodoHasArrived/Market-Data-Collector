using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using MarketDataCollector.Application.Monitoring;
using MarketDataCollector.Application.Services;
using MarketDataCollector.Domain.Events;
using MarketDataCollector.Infrastructure.Performance;
using MarketDataCollector.Storage.Interfaces;

namespace MarketDataCollector.Application.Pipeline;

/// <summary>
/// High-throughput, backpressured pipeline that decouples producers from storage sinks.
/// Includes periodic flushing, capacity monitoring, and performance metrics.
/// </summary>
public sealed class EventPipeline : IMarketEventPublisher, IAsyncDisposable, IFlushable
{
    private readonly Channel<MarketEvent> _channel;
    private readonly IStorageSink _sink;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _consumer;
    private readonly Task? _flusher;
    private readonly int _capacity;

    // Performance metrics
    private long _publishedCount;
    private long _droppedCount;
    private long _consumedCount;
    private long _peakQueueSize;
    private long _totalProcessingTimeNs;
    private long _lastFlushTimestamp;

    // Configuration
    private readonly TimeSpan _flushInterval;
    private readonly int _batchSize;
    private readonly bool _enablePeriodicFlush;

    public EventPipeline(
        IStorageSink sink,
        int capacity = 100_000,
        BoundedChannelFullMode fullMode = BoundedChannelFullMode.DropOldest,
        TimeSpan? flushInterval = null,
        int batchSize = 100,
        bool enablePeriodicFlush = true)
    {
        _sink = sink ?? throw new ArgumentNullException(nameof(sink));
        _capacity = capacity;
        _flushInterval = flushInterval ?? TimeSpan.FromSeconds(5);
        _batchSize = Math.Max(1, batchSize);
        _enablePeriodicFlush = enablePeriodicFlush;

        _channel = Channel.CreateBounded<MarketEvent>(new BoundedChannelOptions(capacity)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = fullMode,
            AllowSynchronousContinuations = false // Avoid blocking producers
        });

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
            Metrics.IncPublished();

            // Track peak queue size
            var currentSize = _channel.Reader.Count;
            var peak = Interlocked.Read(ref _peakQueueSize);
            if (currentSize > peak)
            {
                Interlocked.CompareExchange(ref _peakQueueSize, currentSize, peak);
            }
        }
        else
        {
            Interlocked.Increment(ref _droppedCount);
            Metrics.IncDropped();
        }

        return written;
    }

    /// <summary>
    /// Implements IMarketEventPublisher.TryPublish (non-ref overload).
    /// </summary>
    public bool TryPublish(MarketEvent evt) => TryPublish(in evt);

    /// <summary>
    /// Publishes an event to the pipeline, waiting if necessary.
    /// </summary>
    public async ValueTask PublishAsync(MarketEvent evt, CancellationToken ct = default)
    {
        await _channel.Writer.WriteAsync(evt, ct).ConfigureAwait(false);
        Interlocked.Increment(ref _publishedCount);
        Metrics.IncPublished();
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
            Timestamp: DateTimeOffset.UtcNow
        );
    }

    private async Task ConsumeAsync()
    {
        // Set thread priority for consistent throughput
        ThreadingUtilities.SetAboveNormalPriority();

        try
        {
            var batchBuffer = new List<MarketEvent>(_batchSize);

            await foreach (var evt in _channel.Reader.ReadAllAsync(_cts.Token).ConfigureAwait(false))
            {
                var startTs = Stopwatch.GetTimestamp();

                // Process single event
                await _sink.AppendAsync(evt, _cts.Token).ConfigureAwait(false);
                Interlocked.Increment(ref _consumedCount);

                // Track processing time
                var elapsedNs = (long)(HighResolutionTimestamp.GetElapsedNanoseconds(startTs));
                Interlocked.Add(ref _totalProcessingTimeNs, elapsedNs);
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            // Final flush on shutdown
            try
            {
                await _sink.FlushAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch
            {
                // Log in production - flush failure could mean data loss
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
                catch
                {
                    // Log in production - flush failure needs attention
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

        try
        {
            await _consumer.ConfigureAwait(false);
        }
        catch { }

        if (_flusher is not null)
        {
            try
            {
                await _flusher.ConfigureAwait(false);
            }
            catch { }
        }

        _cts.Dispose();
        await _sink.DisposeAsync().ConfigureAwait(false);
    }
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
    DateTimeOffset Timestamp
);
