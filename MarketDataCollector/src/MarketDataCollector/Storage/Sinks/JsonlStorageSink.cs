using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Linq;
using MarketDataCollector.Domain.Events;
using MarketDataCollector.Application.Monitoring;
using MarketDataCollector.Storage.Interfaces;

namespace MarketDataCollector.Storage.Sinks;

/// <summary>
/// Buffered JSONL writer with per-path writers. Minimal compile-ready implementation.
/// </summary>
public sealed class JsonlStorageSink : IStorageSink
{
    private readonly StorageOptions _options;
    private readonly IStoragePolicy _policy;
    private readonly RetentionManager? _retention;

    private readonly ConcurrentDictionary<string, WriterState> _writers = new(StringComparer.OrdinalIgnoreCase);
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public JsonlStorageSink(StorageOptions options, IStoragePolicy policy)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        _retention = options.RetentionDays is null && options.MaxTotalBytes is null
            ? null
            : new RetentionManager(options.RootPath, options.RetentionDays, options.MaxTotalBytes);
    }

    public async ValueTask AppendAsync(MarketEvent evt, CancellationToken ct = default)
    {
        EventSchemaValidator.Validate(evt);

        _retention?.MaybeCleanup();

        var path = _policy.GetPath(evt);
        var writer = _writers.GetOrAdd(path, p => WriterState.Create(p, _options.Compress));

        // Serialize event as one JSON line
        var json = JsonSerializer.Serialize(evt, JsonOpts);
        await writer.WriteLineAsync(json, ct).ConfigureAwait(false);
    }

    public async Task FlushAsync(CancellationToken ct = default)
    {
        foreach (var kv in _writers)
            await kv.Value.FlushAsync(ct).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var kv in _writers)
            await kv.Value.DisposeAsync().ConfigureAwait(false);
        _writers.Clear();
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

    private sealed class RetentionManager
    {
        private readonly string _root;
        private readonly int? _retentionDays;
        private readonly long? _maxBytes;
        private readonly object _sync = new();
        private DateTime _lastSweep = DateTime.MinValue;
        private static readonly string[] _extensions = new[] { ".jsonl", ".jsonl.gz", ".jsonl.gzip" };

        public RetentionManager(string root, int? retentionDays, long? maxBytes)
        {
            _root = root;
            _retentionDays = retentionDays;
            _maxBytes = maxBytes;
        }

        public void MaybeCleanup()
        {
            if (_retentionDays is null && _maxBytes is null)
                return;

            lock (_sync)
            {
                if ((DateTime.UtcNow - _lastSweep) < TimeSpan.FromSeconds(15))
                    return;

                _lastSweep = DateTime.UtcNow;
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
            catch
            {
                // Soft-fail; retention is best-effort and should not block writes.
            }
        }

        private static void TryDelete(FileInfo file)
        {
            try { file.Delete(); }
            catch { }
        }
    }
}
