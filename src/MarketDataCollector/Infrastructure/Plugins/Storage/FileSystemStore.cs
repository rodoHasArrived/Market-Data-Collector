using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Channels;
using MarketDataCollector.Infrastructure.Plugins.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace MarketDataCollector.Infrastructure.Plugins.Storage;

/// <summary>
/// Simple file system storage for market data using JSONL format.
/// One file per symbol per day.
/// </summary>
/// <remarks>
/// File layout:
///   {DataPath}/{symbol}/{date:yyyy-MM-dd}.jsonl[.gz]
///
/// Example:
///   ./data/AAPL/2024-01-15.jsonl
///   ./data/AAPL/2024-01-16.jsonl.gz (compressed)
///
/// This is the "simplest thing that works" storage implementation.
/// For more complex needs, consider:
/// - ParquetStore: Columnar format for analytics
/// - SqliteStore: Indexed queries
/// - CloudStore: S3/Azure Blob
/// </remarks>
public sealed class FileSystemStore : IMarketDataStore
{
    private readonly StoreOptions _options;
    private readonly ILogger _logger;
    private readonly Channel<MarketDataEvent> _buffer;
    private readonly CancellationTokenSource _flushCts = new();
    private readonly Task _flushTask;
    private readonly Dictionary<string, StreamWriter> _writers = new();
    private readonly SemaphoreSlim _writerLock = new(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public FileSystemStore(StoreOptions? options = null, ILogger<FileSystemStore>? logger = null)
    {
        _options = options ?? new StoreOptions();
        _logger = logger ?? NullLogger<FileSystemStore>.Instance;

        // Ensure data directory exists
        Directory.CreateDirectory(_options.DataPath);

        // Set up buffered writes
        _buffer = Channel.CreateBounded<MarketDataEvent>(
            new BoundedChannelOptions(_options.BufferSize)
            {
                FullMode = BoundedChannelFullMode.Wait
            });

        // Start background flush task
        _flushTask = FlushLoopAsync(_flushCts.Token);
    }

    #region IMarketDataStore Implementation

    public async ValueTask AppendAsync(MarketDataEvent data, CancellationToken ct = default)
    {
        await _buffer.Writer.WriteAsync(data, ct).ConfigureAwait(false);
    }

    public async ValueTask AppendManyAsync(IEnumerable<MarketDataEvent> data, CancellationToken ct = default)
    {
        foreach (var evt in data)
        {
            await _buffer.Writer.WriteAsync(evt, ct).ConfigureAwait(false);
        }
    }

    public async IAsyncEnumerable<MarketDataEvent> QueryAsync(
        string symbol,
        DateOnly from,
        DateOnly to,
        DataType? dataType = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var symbolDir = Path.Combine(_options.DataPath, SanitizeSymbol(symbol));
        if (!Directory.Exists(symbolDir))
        {
            yield break;
        }

        for (var date = from; date <= to; date = date.AddDays(1))
        {
            ct.ThrowIfCancellationRequested();

            var filePath = GetFilePath(symbol, date);
            var gzipPath = filePath + ".gz";

            string? actualPath = null;
            if (File.Exists(gzipPath))
                actualPath = gzipPath;
            else if (File.Exists(filePath))
                actualPath = filePath;

            if (actualPath == null)
                continue;

            await foreach (var evt in ReadFileAsync(actualPath, ct).ConfigureAwait(false))
            {
                if (dataType == null || evt.EventType == dataType)
                {
                    yield return evt;
                }
            }
        }
    }

    public async Task<(DateOnly? First, DateOnly? Last)> GetDateRangeAsync(
        string symbol,
        CancellationToken ct = default)
    {
        var symbolDir = Path.Combine(_options.DataPath, SanitizeSymbol(symbol));
        if (!Directory.Exists(symbolDir))
        {
            return (null, null);
        }

        var files = Directory.GetFiles(symbolDir, "*.jsonl*")
            .Select(Path.GetFileName)
            .Where(f => f != null)
            .Select(f => f!.Replace(".jsonl.gz", "").Replace(".jsonl", ""))
            .Where(f => DateOnly.TryParse(f, out _))
            .Select(f => DateOnly.Parse(f))
            .OrderBy(d => d)
            .ToList();

        if (files.Count == 0)
        {
            return (null, null);
        }

        return await Task.FromResult((files.First(), files.Last()));
    }

    public async Task FlushAsync(CancellationToken ct = default)
    {
        await _writerLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            foreach (var writer in _writers.Values)
            {
                await writer.FlushAsync(ct).ConfigureAwait(false);
            }
        }
        finally
        {
            _writerLock.Release();
        }
    }

    #endregion

    #region Private Methods

    private async Task FlushLoopAsync(CancellationToken ct)
    {
        var batch = new List<MarketDataEvent>(_options.BufferSize);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                // Wait for data or timeout
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeoutCts.CancelAfter(_options.FlushInterval);

                while (batch.Count < _options.BufferSize)
                {
                    try
                    {
                        var evt = await _buffer.Reader.ReadAsync(timeoutCts.Token).ConfigureAwait(false);
                        batch.Add(evt);
                    }
                    catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested)
                    {
                        // Timeout, flush what we have
                        break;
                    }
                }

                if (batch.Count > 0)
                {
                    await WriteBatchAsync(batch, ct).ConfigureAwait(false);
                    batch.Clear();
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // Shutting down
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in flush loop");
            }
        }

        // Drain remaining items on shutdown
        while (_buffer.Reader.TryRead(out var evt))
        {
            batch.Add(evt);
        }

        if (batch.Count > 0)
        {
            await WriteBatchAsync(batch, CancellationToken.None).ConfigureAwait(false);
        }
    }

    private async Task WriteBatchAsync(List<MarketDataEvent> batch, CancellationToken ct)
    {
        // Group by symbol and date
        var groups = batch
            .GroupBy(e => (Symbol: e.Symbol, Date: DateOnly.FromDateTime(e.Timestamp.DateTime)));

        await _writerLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            foreach (var group in groups)
            {
                var writer = GetOrCreateWriter(group.Key.Symbol, group.Key.Date);

                foreach (var evt in group)
                {
                    var json = JsonSerializer.Serialize<MarketDataEvent>(evt, JsonOptions);
                    await writer.WriteLineAsync(json).ConfigureAwait(false);
                }
            }
        }
        finally
        {
            _writerLock.Release();
        }
    }

    private StreamWriter GetOrCreateWriter(string symbol, DateOnly date)
    {
        var key = $"{symbol}_{date:yyyy-MM-dd}";

        if (_writers.TryGetValue(key, out var existing))
        {
            return existing;
        }

        var filePath = GetFilePath(symbol, date);
        var dir = Path.GetDirectoryName(filePath)!;
        Directory.CreateDirectory(dir);

        Stream stream = new FileStream(
            filePath,
            FileMode.Append,
            FileAccess.Write,
            FileShare.Read,
            bufferSize: 65536,
            useAsync: true);

        if (_options.Compress)
        {
            // For compressed files, we use a different extension
            filePath += ".gz";
            stream = new GZipStream(stream, CompressionLevel.Fastest);
        }

        var writer = new StreamWriter(stream) { AutoFlush = false };
        _writers[key] = writer;

        _logger.LogDebug("Created writer for {Symbol} on {Date}", symbol, date);

        return writer;
    }

    private static async IAsyncEnumerable<MarketDataEvent> ReadFileAsync(
        string filePath,
        [EnumeratorCancellation] CancellationToken ct)
    {
        Stream stream = File.OpenRead(filePath);

        if (filePath.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
        {
            stream = new GZipStream(stream, CompressionMode.Decompress);
        }

        await using (stream)
        using (var reader = new StreamReader(stream))
        {
            while (!reader.EndOfStream)
            {
                ct.ThrowIfCancellationRequested();

                var line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                MarketDataEvent? evt = null;
                try
                {
                    evt = JsonSerializer.Deserialize<MarketDataEvent>(line, JsonOptions);
                }
                catch (JsonException)
                {
                    // Skip malformed lines
                    continue;
                }

                if (evt != null)
                {
                    yield return evt;
                }
            }
        }
    }

    private string GetFilePath(string symbol, DateOnly date)
    {
        var sanitized = SanitizeSymbol(symbol);
        return Path.Combine(_options.DataPath, sanitized, $"{date:yyyy-MM-dd}.jsonl");
    }

    private static string SanitizeSymbol(string symbol)
    {
        // Remove invalid path characters
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(symbol.Where(c => !invalid.Contains(c)));
    }

    #endregion

    #region IAsyncDisposable

    public async ValueTask DisposeAsync()
    {
        _flushCts.Cancel();
        _buffer.Writer.Complete();

        try
        {
            await _flushTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        await _writerLock.WaitAsync().ConfigureAwait(false);
        try
        {
            foreach (var writer in _writers.Values)
            {
                await writer.DisposeAsync().ConfigureAwait(false);
            }
            _writers.Clear();
        }
        finally
        {
            _writerLock.Release();
        }

        _writerLock.Dispose();
        _flushCts.Dispose();
    }

    #endregion
}
