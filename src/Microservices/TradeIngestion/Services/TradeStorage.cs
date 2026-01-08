using System.Collections.Concurrent;
using System.IO.Compression;
using System.Text.Json;
using System.Threading;
using DataIngestion.TradeService.Configuration;
using DataIngestion.TradeService.Models;
using Serilog;

namespace DataIngestion.TradeService.Services;

/// <summary>
/// Interface for trade storage operations.
/// </summary>
public interface ITradeStorage
{
    /// <summary>Write a batch of trades.</summary>
    Task WriteBatchAsync(IEnumerable<ProcessedTrade> trades);

    /// <summary>Flush pending writes.</summary>
    Task FlushAsync();

    /// <summary>Get storage statistics.</summary>
    StorageStatistics GetStatistics();
}

/// <summary>
/// JSONL-based trade storage with optional compression.
/// </summary>
public sealed class JsonlTradeStorage : ITradeStorage, IAsyncDisposable
{
    private readonly TradeServiceConfig _config;
    private readonly Serilog.ILogger _log = Log.ForContext<JsonlTradeStorage>();
    private readonly ConcurrentDictionary<string, StreamWriter> _writers = new();
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    private long _tradesWritten;
    private long _bytesWritten;
    private long _filesCreated;

    public JsonlTradeStorage(TradeServiceConfig config)
    {
        _config = config;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        // Ensure data directory exists
        var dataDir = _config.Storage.DataDirectory;
        if (!Directory.Exists(dataDir))
        {
            Directory.CreateDirectory(dataDir);
            _log.Information("Created data directory: {Path}", dataDir);
        }
    }

    public async Task WriteBatchAsync(IEnumerable<ProcessedTrade> trades)
    {
        var tradesBySymbol = trades.GroupBy(t => t.Symbol);

        await _writeLock.WaitAsync();
        try
        {
            foreach (var group in tradesBySymbol)
            {
                var writer = GetOrCreateWriter(group.Key);

                foreach (var trade in group)
                {
                    var json = JsonSerializer.Serialize(trade, _jsonOptions);
                    await writer.WriteLineAsync(json);
                    Interlocked.Increment(ref _tradesWritten);
                    Interlocked.Add(ref _bytesWritten, json.Length + 1);
                }
            }
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task FlushAsync()
    {
        await _writeLock.WaitAsync();
        try
        {
            foreach (var writer in _writers.Values)
            {
                await writer.FlushAsync();
            }
            _log.Debug("Flushed all trade writers");
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public StorageStatistics GetStatistics()
    {
        return new StorageStatistics(
            TradesWritten: Interlocked.Read(ref _tradesWritten),
            BytesWritten: Interlocked.Read(ref _bytesWritten),
            FilesCreated: Interlocked.Read(ref _filesCreated),
            OpenWriters: _writers.Count
        );
    }

    private StreamWriter GetOrCreateWriter(string symbol)
    {
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var key = $"{symbol}_{today}";

        return _writers.GetOrAdd(key, _ =>
        {
            var safeName = symbol.Replace("/", "_").Replace("\\", "_");
            var fileName = $"trades_{safeName}_{today}.jsonl";
            if (_config.Storage.EnableCompression)
            {
                fileName += ".gz";
            }

            var filePath = Path.Combine(_config.Storage.DataDirectory, fileName);
            var fileExists = File.Exists(filePath);

            Stream stream = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.Read,
                bufferSize: 65536, useAsync: true);

            if (_config.Storage.EnableCompression)
            {
                stream = new GZipStream(stream, CompressionLevel.Fastest);
            }

            var writer = new StreamWriter(stream) { AutoFlush = false };

            if (!fileExists)
            {
                Interlocked.Increment(ref _filesCreated);
            }

            _log.Debug("Created writer for {Symbol} -> {Path}", symbol, filePath);
            return writer;
        });
    }

    public async ValueTask DisposeAsync()
    {
        await FlushAsync();

        foreach (var writer in _writers.Values)
        {
            await writer.DisposeAsync();
        }
        _writers.Clear();

        _writeLock.Dispose();
    }
}

/// <summary>
/// Storage statistics.
/// </summary>
public record StorageStatistics(
    long TradesWritten,
    long BytesWritten,
    long FilesCreated,
    int OpenWriters
);
