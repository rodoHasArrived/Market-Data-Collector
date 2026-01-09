using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using MarketDataCollector.Application.Logging;
using MarketDataCollector.Application.Pipeline;
using Serilog;

namespace MarketDataCollector.Storage.OfflineQueue;

/// <summary>
/// Implements robust offline queueing for market events with local persistence.
/// Provides seamless resume after connectivity restoration and clock drift detection.
/// </summary>
public sealed class OfflineEventQueue : IAsyncDisposable
{
    private readonly ILogger _log = LoggingSetup.ForContext<OfflineEventQueue>();
    private readonly OfflineQueueConfig _config;
    private readonly string _queueDirectory;
    private readonly Channel<QueuedEvent> _memoryQueue;
    private readonly ConcurrentDictionary<string, ClockSyncInfo> _clockSyncStates = new();
    private readonly SemaphoreSlim _flushLock = new(1, 1);
    private readonly Timer _clockSyncTimer;
    private readonly Timer _flushTimer;

    private volatile bool _isOnline = true;
    private volatile bool _isDisposed;
    private long _queuedCount;
    private long _flushedCount;
    private long _droppedCount;
    private long _currentQueueBytes;
    private DateTimeOffset _offlineSince = DateTimeOffset.MinValue;

    /// <summary>
    /// Event raised when an event is queued while offline.
    /// </summary>
    public event Action<QueuedEventInfo>? OnEventQueued;

    /// <summary>
    /// Event raised when events are flushed after coming online.
    /// </summary>
    public event Action<FlushCompletedInfo>? OnFlushCompleted;

    /// <summary>
    /// Event raised when clock drift is detected.
    /// </summary>
    public event Action<ClockDriftEvent>? OnClockDriftDetected;

    /// <summary>
    /// Event raised when online/offline status changes.
    /// </summary>
    public event Action<bool>? OnConnectivityChanged;

    /// <summary>
    /// Handler for flushing events to primary storage.
    /// </summary>
    public Func<IReadOnlyList<QueuedEvent>, CancellationToken, Task<int>>? FlushHandler { get; set; }

    public OfflineEventQueue(string queueDirectory, OfflineQueueConfig? config = null)
    {
        _queueDirectory = queueDirectory;
        _config = config ?? new OfflineQueueConfig();

        Directory.CreateDirectory(_queueDirectory);

        _memoryQueue = Channel.CreateBounded<QueuedEvent>(new BoundedChannelOptions(_config.MaxMemoryQueueSize)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });

        _clockSyncTimer = new Timer(CheckClockSync, null,
            TimeSpan.FromMinutes(_config.ClockSyncCheckIntervalMinutes),
            TimeSpan.FromMinutes(_config.ClockSyncCheckIntervalMinutes));

        _flushTimer = new Timer(TryFlushAsync, null,
            TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));

        _log.Information("OfflineEventQueue initialized: MaxBufferMB={MaxMB}, Directory={Dir}",
            _config.MaxBufferSizeMB, _queueDirectory);
    }

    /// <summary>
    /// Initializes the queue and recovers any pending events from disk.
    /// </summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        _log.Information("Initializing offline queue...");

        // Recover pending events from disk
        var pendingFiles = Directory.GetFiles(_queueDirectory, "*.pending.json")
            .OrderBy(f => f)
            .ToList();

        if (pendingFiles.Count > 0)
        {
            _log.Information("Found {Count} pending queue files to recover", pendingFiles.Count);

            foreach (var file in pendingFiles)
            {
                try
                {
                    var json = await File.ReadAllTextAsync(file, ct);
                    var events = JsonSerializer.Deserialize<List<QueuedEvent>>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (events != null)
                    {
                        foreach (var evt in events)
                        {
                            if (_memoryQueue.Writer.TryWrite(evt))
                            {
                                Interlocked.Increment(ref _queuedCount);
                            }
                        }
                    }

                    // Rename to .recovered
                    File.Move(file, file.Replace(".pending.json", ".recovered.json"));
                    _log.Information("Recovered {Count} events from {File}", events?.Count ?? 0, file);
                }
                catch (Exception ex)
                {
                    _log.Error(ex, "Failed to recover queue file: {File}", file);
                }
            }
        }

        _log.Information("Offline queue initialized with {Count} pending events", _queuedCount);
    }

    /// <summary>
    /// Queues an event for storage.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryEnqueue(MarketEvent evt)
    {
        if (_isDisposed) return false;

        // Check buffer size limit
        var estimatedBytes = EstimateEventSize(evt);
        if (Interlocked.Read(ref _currentQueueBytes) + estimatedBytes > _config.MaxBufferSizeMB * 1024 * 1024)
        {
            Interlocked.Increment(ref _droppedCount);
            return false;
        }

        var queuedEvent = new QueuedEvent
        {
            EventId = Guid.NewGuid().ToString(),
            QueuedAt = DateTimeOffset.UtcNow,
            Event = evt,
            EstimatedBytes = estimatedBytes
        };

        if (_memoryQueue.Writer.TryWrite(queuedEvent))
        {
            Interlocked.Increment(ref _queuedCount);
            Interlocked.Add(ref _currentQueueBytes, estimatedBytes);

            if (!_isOnline)
            {
                try
                {
                    OnEventQueued?.Invoke(new QueuedEventInfo
                    {
                        EventId = queuedEvent.EventId,
                        Symbol = evt.Symbol,
                        EventType = evt.Type.ToString(),
                        QueuedAt = queuedEvent.QueuedAt,
                        QueueDepth = (int)Interlocked.Read(ref _queuedCount) - (int)Interlocked.Read(ref _flushedCount)
                    });
                }
                catch { }
            }

            return true;
        }

        Interlocked.Increment(ref _droppedCount);
        return false;
    }

    /// <summary>
    /// Marks the queue as offline (no connectivity).
    /// </summary>
    public void GoOffline(string reason = "Unknown")
    {
        if (!_isOnline) return;

        _isOnline = false;
        _offlineSince = DateTimeOffset.UtcNow;

        _log.Warning("OfflineEventQueue: Going offline. Reason: {Reason}", reason);

        // Persist current memory queue to disk
        _ = PersistMemoryQueueToDiskAsync();

        try
        {
            OnConnectivityChanged?.Invoke(false);
        }
        catch { }
    }

    /// <summary>
    /// Marks the queue as online and triggers flush.
    /// </summary>
    public async Task GoOnlineAsync(CancellationToken ct = default)
    {
        if (_isOnline) return;

        _isOnline = true;
        var offlineDuration = DateTimeOffset.UtcNow - _offlineSince;

        _log.Information("OfflineEventQueue: Coming online after {Duration}", offlineDuration);

        try
        {
            OnConnectivityChanged?.Invoke(true);
        }
        catch { }

        if (_config.FlushOnReconnect)
        {
            await FlushAsync(ct);
        }
    }

    /// <summary>
    /// Flushes all queued events to primary storage.
    /// </summary>
    public async Task FlushAsync(CancellationToken ct = default)
    {
        if (!_isOnline || FlushHandler == null) return;
        if (!await _flushLock.WaitAsync(0, ct)) return;

        try
        {
            var startTime = Stopwatch.GetTimestamp();
            var eventsFlushed = 0;
            var eventsToFlush = new List<QueuedEvent>();

            // Collect events from memory queue
            while (_memoryQueue.Reader.TryRead(out var evt))
            {
                eventsToFlush.Add(evt);
                if (eventsToFlush.Count >= _config.FlushBatchSize)
                {
                    break;
                }
            }

            // Also check for any pending files on disk
            var pendingFiles = Directory.GetFiles(_queueDirectory, "*.pending.json");
            foreach (var file in pendingFiles)
            {
                try
                {
                    var json = await File.ReadAllTextAsync(file, ct);
                    var events = JsonSerializer.Deserialize<List<QueuedEvent>>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (events != null)
                    {
                        eventsToFlush.AddRange(events);
                    }

                    File.Delete(file);
                }
                catch { }
            }

            if (eventsToFlush.Count == 0) return;

            // Sort by timestamp to preserve order
            if (_config.PreserveOrder)
            {
                eventsToFlush = eventsToFlush.OrderBy(e => e.Event.Timestamp).ToList();
            }

            // Flush to primary storage
            try
            {
                eventsFlushed = await FlushHandler(eventsToFlush, ct);

                Interlocked.Add(ref _flushedCount, eventsFlushed);
                Interlocked.Add(ref _currentQueueBytes, -eventsToFlush.Sum(e => e.EstimatedBytes));

                var duration = Stopwatch.GetElapsedTime(startTime);
                _log.Information("Flushed {Count} events in {Duration:F2}ms", eventsFlushed, duration.TotalMilliseconds);

                try
                {
                    OnFlushCompleted?.Invoke(new FlushCompletedInfo
                    {
                        EventsFlushed = eventsFlushed,
                        Duration = duration,
                        RemainingInQueue = (int)(Interlocked.Read(ref _queuedCount) - Interlocked.Read(ref _flushedCount))
                    });
                }
                catch { }
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Failed to flush events, re-queueing {Count} events", eventsToFlush.Count);

                // Re-queue failed events
                foreach (var evt in eventsToFlush)
                {
                    _memoryQueue.Writer.TryWrite(evt);
                }
            }
        }
        finally
        {
            _flushLock.Release();
        }
    }

    /// <summary>
    /// Gets the current queue status.
    /// </summary>
    public OfflineQueueStatus GetStatus()
    {
        var queued = Interlocked.Read(ref _queuedCount);
        var flushed = Interlocked.Read(ref _flushedCount);

        return new OfflineQueueStatus
        {
            IsOnline = _isOnline,
            OfflineSince = _isOnline ? null : _offlineSince,
            TotalQueued = queued,
            TotalFlushed = flushed,
            TotalDropped = Interlocked.Read(ref _droppedCount),
            PendingCount = (int)(queued - flushed),
            CurrentBufferBytes = Interlocked.Read(ref _currentQueueBytes),
            MaxBufferBytes = _config.MaxBufferSizeMB * 1024 * 1024,
            BufferUtilization = (double)Interlocked.Read(ref _currentQueueBytes) / (_config.MaxBufferSizeMB * 1024 * 1024) * 100,
            ClockSyncStates = _clockSyncStates.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
        };
    }

    /// <summary>
    /// Records a clock sync point for drift detection.
    /// </summary>
    public void RecordClockSync(string providerName, DateTimeOffset serverTime)
    {
        var localTime = DateTimeOffset.UtcNow;
        var drift = (serverTime - localTime).TotalMilliseconds;

        var state = _clockSyncStates.GetOrAdd(providerName, _ => new ClockSyncInfo { ProviderName = providerName });
        state.LastSyncTime = localTime;
        state.LastServerTime = serverTime;
        state.DriftMs = drift;
        state.SyncCount++;

        // Track drift history
        state.DriftHistory.Add(drift);
        while (state.DriftHistory.Count > 100)
        {
            state.DriftHistory.RemoveAt(0);
        }

        state.AverageDriftMs = state.DriftHistory.Average();
        state.MaxDriftMs = state.DriftHistory.Max(Math.Abs);

        // Check for significant drift
        if (Math.Abs(drift) > _config.ClockDriftToleranceMs)
        {
            _log.Warning("Clock drift detected for {Provider}: {Drift:F2}ms", providerName, drift);

            try
            {
                OnClockDriftDetected?.Invoke(new ClockDriftEvent
                {
                    ProviderName = providerName,
                    DriftMs = drift,
                    LocalTime = localTime,
                    ServerTime = serverTime,
                    Severity = Math.Abs(drift) > _config.ClockDriftToleranceMs * 2 ? "Critical" : "Warning"
                });
            }
            catch { }
        }
    }

    private async Task PersistMemoryQueueToDiskAsync()
    {
        var events = new List<QueuedEvent>();

        while (_memoryQueue.Reader.TryRead(out var evt))
        {
            events.Add(evt);
        }

        if (events.Count == 0) return;

        var fileName = $"queue_{DateTimeOffset.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}.pending.json";
        var filePath = Path.Combine(_queueDirectory, fileName);

        var json = JsonSerializer.Serialize(events, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        });

        await File.WriteAllTextAsync(filePath, json);
        _log.Information("Persisted {Count} events to {File}", events.Count, filePath);
    }

    private void CheckClockSync(object? state)
    {
        if (_isDisposed) return;

        foreach (var kvp in _clockSyncStates)
        {
            var syncState = kvp.Value;
            var timeSinceSync = DateTimeOffset.UtcNow - syncState.LastSyncTime;

            if (timeSinceSync.TotalMinutes > _config.ClockSyncCheckIntervalMinutes * 2)
            {
                _log.Warning("Clock sync stale for {Provider}: last sync {Elapsed:F1} minutes ago",
                    kvp.Key, timeSinceSync.TotalMinutes);
            }
        }
    }

    private async void TryFlushAsync(object? state)
    {
        if (_isDisposed || !_isOnline) return;

        try
        {
            await FlushAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Error during periodic flush");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int EstimateEventSize(MarketEvent evt)
    {
        // Rough estimation based on event type
        return evt.Type switch
        {
            MarketEventType.Trade => 200,
            MarketEventType.BboQuote => 250,
            MarketEventType.L2Snapshot => 1000,
            MarketEventType.OrderFlow => 500,
            _ => 200
        };
    }

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        _clockSyncTimer.Dispose();
        _flushTimer.Dispose();

        // Final flush
        if (_isOnline)
        {
            try
            {
                await FlushAsync(CancellationToken.None);
            }
            catch { }
        }
        else
        {
            // Persist to disk if offline
            await PersistMemoryQueueToDiskAsync();
        }

        _memoryQueue.Writer.Complete();
        _flushLock.Dispose();

        _log.Information("OfflineEventQueue disposed. Final stats: Queued={Queued}, Flushed={Flushed}, Dropped={Dropped}",
            _queuedCount, _flushedCount, _droppedCount);
    }
}

/// <summary>
/// Configuration for offline event queue.
/// </summary>
public class OfflineQueueConfig
{
    /// <summary>
    /// Maximum buffer size in megabytes.
    /// </summary>
    public int MaxBufferSizeMB { get; set; } = 1024;

    /// <summary>
    /// Maximum number of events in memory queue.
    /// </summary>
    public int MaxMemoryQueueSize { get; set; } = 100000;

    /// <summary>
    /// Whether to flush immediately on reconnect.
    /// </summary>
    public bool FlushOnReconnect { get; set; } = true;

    /// <summary>
    /// Whether to preserve event order during flush.
    /// </summary>
    public bool PreserveOrder { get; set; } = true;

    /// <summary>
    /// Clock drift tolerance in milliseconds.
    /// </summary>
    public double ClockDriftToleranceMs { get; set; } = 100;

    /// <summary>
    /// Interval for clock sync checks in minutes.
    /// </summary>
    public int ClockSyncCheckIntervalMinutes { get; set; } = 15;

    /// <summary>
    /// Number of events to flush in a single batch.
    /// </summary>
    public int FlushBatchSize { get; set; } = 10000;
}

/// <summary>
/// A queued market event.
/// </summary>
public class QueuedEvent
{
    public string EventId { get; set; } = string.Empty;
    public DateTimeOffset QueuedAt { get; set; }
    public MarketEvent Event { get; set; } = default!;
    public int EstimatedBytes { get; set; }
}

/// <summary>
/// Status of the offline queue.
/// </summary>
public class OfflineQueueStatus
{
    public bool IsOnline { get; set; }
    public DateTimeOffset? OfflineSince { get; set; }
    public long TotalQueued { get; set; }
    public long TotalFlushed { get; set; }
    public long TotalDropped { get; set; }
    public int PendingCount { get; set; }
    public long CurrentBufferBytes { get; set; }
    public long MaxBufferBytes { get; set; }
    public double BufferUtilization { get; set; }
    public Dictionary<string, ClockSyncInfo> ClockSyncStates { get; set; } = new();
}

/// <summary>
/// Information about a queued event.
/// </summary>
public class QueuedEventInfo
{
    public string EventId { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public DateTimeOffset QueuedAt { get; set; }
    public int QueueDepth { get; set; }
}

/// <summary>
/// Information about a completed flush.
/// </summary>
public class FlushCompletedInfo
{
    public int EventsFlushed { get; set; }
    public TimeSpan Duration { get; set; }
    public int RemainingInQueue { get; set; }
}

/// <summary>
/// Clock sync tracking information.
/// </summary>
public class ClockSyncInfo
{
    public string ProviderName { get; set; } = string.Empty;
    public DateTimeOffset LastSyncTime { get; set; }
    public DateTimeOffset LastServerTime { get; set; }
    public double DriftMs { get; set; }
    public double AverageDriftMs { get; set; }
    public double MaxDriftMs { get; set; }
    public int SyncCount { get; set; }
    public List<double> DriftHistory { get; set; } = new();
}

/// <summary>
/// Event raised when clock drift is detected.
/// </summary>
public class ClockDriftEvent
{
    public string ProviderName { get; set; } = string.Empty;
    public double DriftMs { get; set; }
    public DateTimeOffset LocalTime { get; set; }
    public DateTimeOffset ServerTime { get; set; }
    public string Severity { get; set; } = "Warning";
}
