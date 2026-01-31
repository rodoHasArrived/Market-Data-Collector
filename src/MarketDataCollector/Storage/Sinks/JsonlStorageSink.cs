using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MarketDataCollector.Application.Serialization;
using MarketDataCollector.Contracts.Domain.Events;
using MarketDataCollector.Application.Monitoring;
using MarketDataCollector.Storage.Interfaces;
using MarketDataCollector.Storage.Services;

namespace MarketDataCollector.Storage.Sinks;

/// <summary>
/// Configuration options for batched JSONL storage.
/// </summary>
public sealed class JsonlBatchOptions
{
    /// <summary>
    /// Number of events to buffer before writing to disk.
    /// Default is 1000 events.
    /// </summary>
    public int BatchSize { get; init; } = 1000;

    /// <summary>
    /// Maximum time between flushes.
    /// Default is 5 seconds.
    /// </summary>
    public TimeSpan FlushInterval { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Whether batching is enabled. When disabled, writes occur immediately per event.
    /// Default is true.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Pre-serialize events in parallel when batch size exceeds this threshold.
    /// Default is 100 events.
    /// </summary>
    public int ParallelSerializationThreshold { get; init; } = 100;

    /// <summary>
    /// Default options with batching enabled.
    /// </summary>
    public static JsonlBatchOptions Default => new();

    /// <summary>
    /// Optimized for high throughput with larger batches.
    /// </summary>
    public static JsonlBatchOptions HighThroughput => new()
    {
        BatchSize = 5000,
        FlushInterval = TimeSpan.FromSeconds(10),
        ParallelSerializationThreshold = 200
    };

    /// <summary>
    /// Optimized for low latency with smaller batches.
    /// </summary>
    public static JsonlBatchOptions LowLatency => new()
    {
        BatchSize = 100,
        FlushInterval = TimeSpan.FromSeconds(1),
        ParallelSerializationThreshold = 50
    };

    /// <summary>
    /// Disable batching - write each event immediately.
    /// </summary>
    public static JsonlBatchOptions NoBatching => new()
    {
        Enabled = false
    };
}

/// <summary>
/// Buffered JSONL writer with per-path writers and configurable batch writes.
/// Supports both immediate and batched write modes for optimal performance.
/// </summary>
public sealed class JsonlStorageSink : IStorageSink
{
    private readonly StorageOptions _options;
    private readonly IStoragePolicy _policy;
    private readonly JsonlBatchOptions _batchOptions;
    private readonly ILogger<JsonlStorageSink> _logger;
    private readonly RetentionManager? _retention;
    private readonly Timer? _flushTimer;
    private readonly Timer? _retentionTimer;
    private bool _disposed;

    private readonly ConcurrentDictionary<string, WriterState> _writers = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, MarketEventBuffer> _buffers = new(StringComparer.OrdinalIgnoreCase);

    // Metrics
    private long _eventsBuffered;
    private long _eventsWritten;
    private long _batchesWritten;

    /// <summary>
    /// Total events currently buffered across all paths.
    /// </summary>
    public long EventsBuffered => Interlocked.Read(ref _eventsBuffered);

    /// <summary>
    /// Total events written to disk.
    /// </summary>
    public long EventsWritten => Interlocked.Read(ref _eventsWritten);

    /// <summary>
    /// Total batches written to disk.
    /// </summary>
    public long BatchesWritten => Interlocked.Read(ref _batchesWritten);

    /// <summary>
    /// Creates a JsonlStorageSink with default options (no batching for backward compatibility).
    /// </summary>
    public JsonlStorageSink(StorageOptions options, IStoragePolicy policy, ILogger<JsonlStorageSink>? logger = null)
        : this(options, policy, JsonlBatchOptions.NoBatching, logger)
    {
    }

    /// <summary>
    /// Creates a JsonlStorageSink with configurable batch options.
    /// </summary>
    public JsonlStorageSink(StorageOptions options, IStoragePolicy policy, JsonlBatchOptions batchOptions, ILogger<JsonlStorageSink>? logger = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        _batchOptions = batchOptions ?? throw new ArgumentNullException(nameof(batchOptions));
        _logger = logger ?? NullLogger<JsonlStorageSink>.Instance;
        _retention = options.RetentionDays is null && options.MaxTotalBytes is null
            ? null
            : new RetentionManager(options.RootPath, options.RetentionDays, options.MaxTotalBytes, _logger);

        if (_batchOptions.Enabled)
        {
            _flushTimer = new Timer(
                _ => _ = FlushAllBuffersSafelyAsync(),
                null,
                _batchOptions.FlushInterval,
                _batchOptions.FlushInterval);
        }

        if (_retention != null)
        {
            _retentionTimer = new Timer(
                _ => RunRetentionCleanup(),
                null,
                TimeSpan.FromSeconds(15),
                TimeSpan.FromSeconds(15));
        }
    }

    public async ValueTask AppendAsync(MarketEvent evt, CancellationToken ct = default)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(JsonlStorageSink));

        EventSchemaValidator.Validate(evt);
        var path = _policy.GetPath(evt);

        if (!_batchOptions.Enabled)
        {
            // Immediate write mode (legacy behavior)
            await WriteEventImmediateAsync(path, evt, ct).ConfigureAwait(false);
            return;
        }

        // Batched write mode
        var buffer = _buffers.GetOrAdd(path, _ => new MarketEventBuffer(_batchOptions.BatchSize));
        buffer.Add(evt);
        Interlocked.Increment(ref _eventsBuffered);

        // Flush if buffer is full
        if (buffer.ShouldFlush(_batchOptions.BatchSize))
        {
            await FlushBufferAsync(path, buffer, ct).ConfigureAwait(false);
        }
    }

    private async ValueTask WriteEventImmediateAsync(string path, MarketEvent evt, CancellationToken ct)
    {
        var writer = _writers.GetOrAdd(path, p => WriterState.Create(p, _options.Compress));
        var json = JsonSerializer.Serialize(evt, MarketDataJsonContext.HighPerformanceOptions);
        await writer.WriteLineAsync(json, ct).ConfigureAwait(false);
        Interlocked.Increment(ref _eventsWritten);
    }

    private async Task FlushBufferAsync(string path, MarketEventBuffer buffer, CancellationToken ct)
    {
        var events = buffer.DrainAll();
        if (events.Count == 0) return;

        var writer = _writers.GetOrAdd(path, p => WriterState.Create(p, _options.Compress));

        // Serialize events - use parallel serialization for larger batches
        string[] lines;
        if (events.Count >= _batchOptions.ParallelSerializationThreshold)
        {
            lines = new string[events.Count];
            Parallel.For(0, events.Count, i =>
            {
                lines[i] = JsonSerializer.Serialize(events[i], MarketDataJsonContext.HighPerformanceOptions);
            });
        }
        else
        {
            lines = events.Select(e => JsonSerializer.Serialize(e, MarketDataJsonContext.HighPerformanceOptions)).ToArray();
        }

        // Write all lines in a single batch
        await writer.WriteBatchAsync(lines, ct).ConfigureAwait(false);

        Interlocked.Add(ref _eventsWritten, events.Count);
        Interlocked.Add(ref _eventsBuffered, -events.Count);
        Interlocked.Increment(ref _batchesWritten);
    }

    private async Task FlushAllBuffersAsync(CancellationToken ct = default)
    {
        if (_disposed) return;

        var tasks = new List<Task>();
        foreach (var kvp in _buffers)
        {
            if (kvp.Value.Count > 0)
            {
                tasks.Add(FlushBufferAsync(kvp.Key, kvp.Value, ct));
            }
        }

        if (tasks.Count > 0)
        {
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
    }

    private async Task FlushAllBuffersSafelyAsync()
    {
        try
        {
            await FlushAllBuffersAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Periodic flush failed");
        }
    }

    private void RunRetentionCleanup()
    {
        try
        {
            _retention?.MaybeCleanup();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Periodic retention cleanup failed");
        }
    }

    public async Task FlushAsync(CancellationToken ct = default)
    {
        // First flush all buffers (if batching enabled)
        if (_batchOptions.Enabled)
        {
            await FlushAllBuffersAsync(ct).ConfigureAwait(false);
        }

        // Then flush all writers to disk
        foreach (var kv in _writers)
            await kv.Value.FlushAsync(ct).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        // Stop the timer first
        if (_flushTimer != null)
        {
            await _flushTimer.DisposeAsync().ConfigureAwait(false);
        }

        if (_retentionTimer != null)
        {
            await _retentionTimer.DisposeAsync().ConfigureAwait(false);
        }

        // Flush remaining buffered events
        if (_batchOptions.Enabled)
        {
            await FlushAllBuffersAsync().ConfigureAwait(false);
        }

        // Dispose all writers
        foreach (var kv in _writers)
            await kv.Value.DisposeAsync().ConfigureAwait(false);

        _writers.Clear();
        _buffers.Clear();

        // Dispose retention manager
        _retention?.Dispose();
    }

    private sealed class WriterState : IAsyncDisposable
    {
        private readonly Stream _stream;
        private readonly StreamWriter _writer;
        private readonly SemaphoreSlim _gate = new(1, 1);

        private WriterState(Stream stream, StreamWriter writer)
        {
            _stream = stream;
            _writer = writer;
        }

        public static WriterState Create(string path, bool compress)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            Stream fs = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read, 1 << 16, useAsync: true);
            if (compress)
                fs = new GZipStream(fs, CompressionLevel.Fastest);

            var writer = new StreamWriter(fs, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), 1 << 16, leaveOpen: true);
            writer.AutoFlush = false;
            return new WriterState(fs, writer);
        }

        public async ValueTask WriteLineAsync(string line, CancellationToken ct)
        {
            await _gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                await _writer.WriteLineAsync(line).ConfigureAwait(false);
            }
            finally
            {
                _gate.Release();
            }
        }

        /// <summary>
        /// Writes multiple lines in a single batch operation, minimizing lock contention.
        /// </summary>
        public async ValueTask WriteBatchAsync(string[] lines, CancellationToken ct)
        {
            if (lines.Length == 0) return;

            await _gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                // Write all lines in a single lock acquisition
                foreach (var line in lines)
                {
                    await _writer.WriteLineAsync(line).ConfigureAwait(false);
                }
                // Flush after batch to ensure durability
                await _writer.FlushAsync().ConfigureAwait(false);
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task FlushAsync(CancellationToken ct)
        {
            await _gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                await _writer.FlushAsync().ConfigureAwait(false);
                await _stream.FlushAsync(ct).ConfigureAwait(false);
            }
            finally
            {
                _gate.Release();
            }
        }

        public async ValueTask DisposeAsync()
        {
            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                await _writer.FlushAsync().ConfigureAwait(false);
                await _writer.DisposeAsync().ConfigureAwait(false);
                await _stream.DisposeAsync().ConfigureAwait(false);
            }
            finally
            {
                _gate.Release();
                _gate.Dispose();
            }
        }
    }

    private sealed class RetentionManager : IDisposable
    {
        private readonly string _root;
        private readonly int? _retentionDays;
        private readonly long? _maxBytes;
        private readonly ILogger _logger;
        // Using ReaderWriterLockSlim for better concurrency - reads (timestamp checks) are more frequent than writes
        private readonly ReaderWriterLockSlim _lock = new(LockRecursionPolicy.NoRecursion);
        private DateTime _lastSweep = DateTime.MinValue;
        private bool _disposed;
        private static readonly string[] _extensions = new[] { ".jsonl", ".jsonl.gz", ".jsonl.gzip" };

        public RetentionManager(string root, int? retentionDays, long? maxBytes, ILogger logger)
        {
            _root = root;
            _retentionDays = retentionDays;
            _maxBytes = maxBytes;
            _logger = logger;
        }

        public void MaybeCleanup()
        {
            if (_disposed || (_retentionDays is null && _maxBytes is null))
                return;

            if (_disposed)
                return;

            // Fast path: check if cleanup is needed using read lock (allows concurrent reads)
            _lock.EnterReadLock();
            try
            {
                if ((DateTime.UtcNow - _lastSweep) < TimeSpan.FromSeconds(15))
                    return;
            }
            finally
            {
                _lock.ExitReadLock();
            }

            // Slow path: need to update timestamp, acquire write lock
            _lock.EnterWriteLock();
            try
            {
                // Double-check after acquiring write lock (another thread may have updated)
                if ((DateTime.UtcNow - _lastSweep) < TimeSpan.FromSeconds(15))
                    return;

                _lastSweep = DateTime.UtcNow;
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            try
            {
                var files = Directory.Exists(_root)
                    ? Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories)
                        .Where(f => _extensions.Any(ext => f.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
                        .Select(path => new FileInfo(path))
                        .ToList()
                    : new List<FileInfo>();

                if (_retentionDays is not null)
                {
                    var cutoff = DateTime.UtcNow.AddDays(-_retentionDays.Value);
                    foreach (var f in files.Where(f => f.LastWriteTimeUtc < cutoff))
                    {
                        TryDelete(f);
                    }
                }

                if (_maxBytes is not null)
                {
                    var ordered = files
                        .OrderBy(f => f.LastWriteTimeUtc)
                        .ToList();
                    long total = ordered.Sum(f => f.Exists ? f.Length : 0);

                    var idx = 0;
                    while (total > _maxBytes && idx < ordered.Count)
                    {
                        var target = ordered[idx++];
                        total -= target.Length;
                        TryDelete(target);
                    }
                }
            }
            catch (Exception ex)
            {
                // Soft-fail; retention is best-effort and should not block writes.
                _logger.LogWarning(
                    ex,
                    "Retention cleanup failed for storage root {RootPath}. RetentionDays={RetentionDays}, MaxBytes={MaxBytes}",
                    _root,
                    _retentionDays,
                    _maxBytes);
            }
        }

        private void TryDelete(FileInfo file)
        {
            try
            {
                file.Delete();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(
                    ex,
                    "Failed to delete file during retention cleanup: {FilePath}, Size={FileSize} bytes",
                    file.FullName,
                    file.Exists ? file.Length : 0);
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _lock.Dispose();
        }
    }
}
