using System.Diagnostics;
using System.Runtime.CompilerServices;
using MarketDataCollector.Application.Logging;
using MarketDataCollector.Domain.Events;
using MarketDataCollector.Storage.Interfaces;
using Serilog;

namespace MarketDataCollector.Storage.Replay;

/// <summary>
/// Options for configuring an event replay session.
/// </summary>
public sealed class ReplayPipelineOptions
{
    /// <summary>
    /// Symbols to include in replay. Null or empty means all symbols.
    /// </summary>
    public IReadOnlySet<string>? Symbols { get; init; }

    /// <summary>
    /// Event types to include in replay (matched against <see cref="MarketEvent.Type"/>).
    /// Null or empty means all types.
    /// </summary>
    public IReadOnlySet<string>? EventTypes { get; init; }

    /// <summary>
    /// Start of the replay time range (inclusive). Null means from the beginning.
    /// </summary>
    public DateTimeOffset? From { get; init; }

    /// <summary>
    /// End of the replay time range (inclusive). Null means until the end.
    /// </summary>
    public DateTimeOffset? To { get; init; }

    /// <summary>
    /// Speed multiplier for replay. 1.0 = real-time, 2.0 = 2x speed, 0 = maximum speed (no delay).
    /// </summary>
    public double SpeedMultiplier { get; init; } = 0;

    /// <summary>
    /// Whether to publish replayed events to a storage sink.
    /// </summary>
    public bool PublishToSink { get; init; } = false;

    /// <summary>
    /// Maximum number of events to replay. 0 = unlimited.
    /// </summary>
    public long MaxEvents { get; init; } = 0;

    /// <summary>
    /// Default options: replay everything at max speed, no sink publishing.
    /// </summary>
    public static ReplayPipelineOptions Default => new();
}

/// <summary>
/// Statistics tracked during a replay session.
/// </summary>
public sealed class ReplaySessionStatistics
{
    private long _eventsReplayed;
    private long _eventsSkipped;
    private long _eventsErrored;
    private long _bytesRead;
    private readonly Stopwatch _stopwatch = new();
    private DateTimeOffset? _firstEventTimestamp;
    private DateTimeOffset? _lastEventTimestamp;

    /// <summary>Total events successfully replayed.</summary>
    public long EventsReplayed => Interlocked.Read(ref _eventsReplayed);

    /// <summary>Total events skipped by filters.</summary>
    public long EventsSkipped => Interlocked.Read(ref _eventsSkipped);

    /// <summary>Total events that failed deserialization.</summary>
    public long EventsErrored => Interlocked.Read(ref _eventsErrored);

    /// <summary>Total bytes read from source files.</summary>
    public long BytesRead => Interlocked.Read(ref _bytesRead);

    /// <summary>Total wall-clock time of replay.</summary>
    public TimeSpan Elapsed => _stopwatch.Elapsed;

    /// <summary>Timestamp of the first event in the replay.</summary>
    public DateTimeOffset? FirstEventTimestamp => _firstEventTimestamp;

    /// <summary>Timestamp of the last event replayed.</summary>
    public DateTimeOffset? LastEventTimestamp => _lastEventTimestamp;

    /// <summary>Effective data time span covered by the replay.</summary>
    public TimeSpan? DataTimeSpan =>
        _firstEventTimestamp.HasValue && _lastEventTimestamp.HasValue
            ? _lastEventTimestamp.Value - _firstEventTimestamp.Value
            : null;

    /// <summary>Replay throughput in events per second.</summary>
    public double EventsPerSecond =>
        _stopwatch.Elapsed.TotalSeconds > 0
            ? EventsReplayed / _stopwatch.Elapsed.TotalSeconds
            : 0;

    internal void Start() => _stopwatch.Start();
    internal void Stop() => _stopwatch.Stop();

    internal void RecordReplayed(MarketEvent evt)
    {
        Interlocked.Increment(ref _eventsReplayed);
        var ts = evt.Timestamp;
        if (_firstEventTimestamp is null || ts < _firstEventTimestamp)
            _firstEventTimestamp = ts;
        if (_lastEventTimestamp is null || ts > _lastEventTimestamp)
            _lastEventTimestamp = ts;
    }

    internal void RecordSkipped() => Interlocked.Increment(ref _eventsSkipped);
    internal void RecordErrored() => Interlocked.Increment(ref _eventsErrored);
    internal void AddBytesRead(long bytes) => Interlocked.Add(ref _bytesRead, bytes);
}

/// <summary>
/// Replays stored JSONL market events through the pipeline for debugging, QA, and
/// backfill verification. Supports filtering, speed control, and optional publishing
/// to a storage sink for re-processing.
/// </summary>
/// <remarks>
/// This is the core H3 (Event Replay Infrastructure) component from the project roadmap.
/// It reads events using <see cref="MemoryMappedJsonlReader"/> for high-performance I/O
/// and supports IAsyncEnumerable streaming to the caller.
/// </remarks>
public sealed class EventReplayPipeline : IAsyncDisposable
{
    private static readonly ILogger Log = LoggingSetup.ForContext<EventReplayPipeline>();
    private readonly MemoryMappedJsonlReader _reader;
    private readonly IStorageSink? _sink;
    private readonly ReplayPipelineOptions _options;
    private readonly ReplaySessionStatistics _statistics = new();
    private CancellationTokenSource? _pauseCts;
    private volatile bool _isPaused;
    private volatile bool _disposed;
    private int _state; // 0 = idle, 1 = running, 2 = completed

    /// <summary>
    /// Creates a new replay pipeline for the specified data root.
    /// </summary>
    /// <param name="dataRoot">Root directory containing JSONL files to replay.</param>
    /// <param name="options">Replay configuration options.</param>
    /// <param name="sink">Optional storage sink for re-publishing replayed events.</param>
    /// <param name="readerOptions">Optional memory-mapped reader configuration.</param>
    public EventReplayPipeline(
        string dataRoot,
        ReplayPipelineOptions? options = null,
        IStorageSink? sink = null,
        MemoryMappedReaderOptions? readerOptions = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataRoot);

        _reader = new MemoryMappedJsonlReader(dataRoot, readerOptions ?? MemoryMappedReaderOptions.Default);
        _options = options ?? ReplayPipelineOptions.Default;
        _sink = sink;
    }

    /// <summary>
    /// Gets the current replay statistics.
    /// </summary>
    public ReplaySessionStatistics Statistics => _statistics;

    /// <summary>
    /// Gets whether the replay is currently paused.
    /// </summary>
    public bool IsPaused => _isPaused;

    /// <summary>
    /// Gets whether the replay is currently running.
    /// </summary>
    public bool IsRunning => Interlocked.CompareExchange(ref _state, 0, 0) == 1;

    /// <summary>
    /// Gets whether the replay has completed.
    /// </summary>
    public bool IsCompleted => Interlocked.CompareExchange(ref _state, 0, 0) == 2;

    /// <summary>
    /// Replays events as an async enumerable, applying configured filters and speed control.
    /// </summary>
    public async IAsyncEnumerable<MarketEvent> ReplayAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (Interlocked.CompareExchange(ref _state, 1, 0) != 0)
            throw new InvalidOperationException("Replay is already running or has completed.");

        _pauseCts = new CancellationTokenSource();
        _statistics.Start();
        DateTimeOffset? previousEventTimestamp = null;

        try
        {
            var source = GetFilteredSource(ct);

            await foreach (var evt in source)
            {
                ct.ThrowIfCancellationRequested();

                // Pause support
                while (_isPaused && !ct.IsCancellationRequested)
                {
                    await Task.Delay(50, ct).ConfigureAwait(false);
                }

                // Speed control: delay based on inter-event timing
                if (_options.SpeedMultiplier > 0 && previousEventTimestamp.HasValue)
                {
                    var interEventDelay = evt.Timestamp - previousEventTimestamp.Value;
                    if (interEventDelay > TimeSpan.Zero)
                    {
                        var scaledDelay = TimeSpan.FromTicks(
                            (long)(interEventDelay.Ticks / _options.SpeedMultiplier));
                        if (scaledDelay > TimeSpan.FromMilliseconds(1))
                        {
                            await Task.Delay(scaledDelay, ct).ConfigureAwait(false);
                        }
                    }
                }

                // Publish to sink if configured
                if (_options.PublishToSink && _sink is not null)
                {
                    await _sink.AppendAsync(evt, ct).ConfigureAwait(false);
                }

                _statistics.RecordReplayed(evt);
                previousEventTimestamp = evt.Timestamp;

                yield return evt;

                // Check max events limit
                if (_options.MaxEvents > 0 && _statistics.EventsReplayed >= _options.MaxEvents)
                {
                    break;
                }
            }

            // Flush sink if we published events
            if (_options.PublishToSink && _sink is not null && _statistics.EventsReplayed > 0)
            {
                await _sink.FlushAsync(ct).ConfigureAwait(false);
            }
        }
        finally
        {
            _statistics.Stop();
            _statistics.AddBytesRead(_reader.BytesRead);
            Interlocked.Exchange(ref _state, 2);

            Log.Information(
                "Replay completed: {EventsReplayed} replayed, {EventsSkipped} skipped, " +
                "{EventsErrored} errored in {Elapsed:g} ({EventsPerSecond:F0} evt/s)",
                _statistics.EventsReplayed,
                _statistics.EventsSkipped,
                _statistics.EventsErrored,
                _statistics.Elapsed,
                _statistics.EventsPerSecond);
        }
    }

    /// <summary>
    /// Pauses the replay. Events in flight will complete, but no new events will be yielded.
    /// </summary>
    public void Pause()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _isPaused = true;
        Log.Debug("Replay paused at {EventsReplayed} events", _statistics.EventsReplayed);
    }

    /// <summary>
    /// Resumes a paused replay.
    /// </summary>
    public void Resume()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _isPaused = false;
        Log.Debug("Replay resumed at {EventsReplayed} events", _statistics.EventsReplayed);
    }

    /// <summary>
    /// Gets file statistics for the replay source directory.
    /// </summary>
    public FileStatistics GetSourceStatistics() => _reader.GetFileStatistics();

    private async IAsyncEnumerable<MarketEvent> GetFilteredSource(
        [EnumeratorCancellation] CancellationToken ct)
    {
        IAsyncEnumerable<MarketEvent> source;

        // Apply time range filter at the reader level if both bounds are specified
        if (_options.From.HasValue && _options.To.HasValue)
        {
            source = _reader.ReadEventsInRangeAsync(_options.From.Value, _options.To.Value, ct);
        }
        else
        {
            source = _reader.ReadEventsAsync(ct);
        }

        await foreach (var evt in source)
        {
            // Time range filter (for single-bound cases)
            if (_options.From.HasValue && evt.Timestamp < _options.From.Value)
            {
                _statistics.RecordSkipped();
                continue;
            }
            if (_options.To.HasValue && evt.Timestamp > _options.To.Value)
            {
                _statistics.RecordSkipped();
                continue;
            }

            // Symbol filter
            if (_options.Symbols is { Count: > 0 } && !_options.Symbols.Contains(evt.Symbol))
            {
                _statistics.RecordSkipped();
                continue;
            }

            // Event type filter
            if (_options.EventTypes is { Count: > 0 } && !_options.EventTypes.Contains(evt.Type.ToString()))
            {
                _statistics.RecordSkipped();
                continue;
            }

            yield return evt;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        _pauseCts?.Cancel();
        _pauseCts?.Dispose();

        if (_sink is not null)
        {
            try
            {
                await _sink.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error disposing replay sink");
            }
        }
    }
}
