using System.Collections.Concurrent;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using MarketDataCollector.Domain.Events;
using MarketDataCollector.Storage.Interfaces;

namespace MarketDataCollector.Storage.Sinks;

/// <summary>
/// Buffered JSONL writer with per-path writers. Minimal compile-ready implementation.
/// </summary>
public sealed class JsonlStorageSink : IStorageSink
{
    private readonly StorageOptions _options;
    private readonly IStoragePolicy _policy;

    private readonly ConcurrentDictionary<string, WriterState> _writers = new(StringComparer.OrdinalIgnoreCase);
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public JsonlStorageSink(StorageOptions options, IStoragePolicy policy)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
    }

    public async ValueTask AppendAsync(MarketEvent evt, CancellationToken ct = default)
    {
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
}
