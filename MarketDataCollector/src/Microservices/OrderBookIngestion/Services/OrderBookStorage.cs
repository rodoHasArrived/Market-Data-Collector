using System.Text.Json;
using DataIngestion.OrderBookService.Configuration;
using DataIngestion.OrderBookService.Models;
using Serilog;

namespace DataIngestion.OrderBookService.Services;

/// <summary>
/// Interface for order book snapshot storage.
/// </summary>
public interface IOrderBookStorage
{
    Task WriteSnapshotAsync(ManagedOrderBook book);
    Task WriteBatchAsync(IEnumerable<ManagedOrderBook> books);
    Task FlushAsync();
}

/// <summary>
/// JSONL-based order book snapshot storage.
/// </summary>
public sealed class JsonlOrderBookStorage : IOrderBookStorage, IAsyncDisposable
{
    private readonly OrderBookServiceConfig _config;
    private readonly ILogger _log = Log.ForContext<JsonlOrderBookStorage>();
    private readonly Dictionary<string, StreamWriter> _writers = new();
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions;

    public JsonlOrderBookStorage(OrderBookServiceConfig config)
    {
        _config = config;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        var dataDir = _config.Storage.DataDirectory;
        if (!Directory.Exists(dataDir))
        {
            Directory.CreateDirectory(dataDir);
        }
    }

    public async Task WriteSnapshotAsync(ManagedOrderBook book)
    {
        await WriteBatchAsync([book]);
    }

    public async Task WriteBatchAsync(IEnumerable<ManagedOrderBook> books)
    {
        await _writeLock.WaitAsync();
        try
        {
            foreach (var book in books)
            {
                var writer = GetOrCreateWriter(book.Symbol);
                var snapshot = CreateStorageSnapshot(book);
                var json = JsonSerializer.Serialize(snapshot, _jsonOptions);
                await writer.WriteLineAsync(json);
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
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private StreamWriter GetOrCreateWriter(string symbol)
    {
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var key = $"{symbol}_{today}";

        if (!_writers.TryGetValue(key, out var writer))
        {
            var safeName = symbol.Replace("/", "_").Replace("\\", "_");
            var fileName = $"orderbook_{safeName}_{today}.jsonl";
            var filePath = Path.Combine(_config.Storage.DataDirectory, fileName);

            var stream = new FileStream(filePath, FileMode.Append, FileAccess.Write,
                FileShare.Read, bufferSize: 65536, useAsync: true);
            writer = new StreamWriter(stream) { AutoFlush = false };
            _writers[key] = writer;

            _log.Debug("Created writer for {Symbol} -> {Path}", symbol, filePath);
        }

        return writer;
    }

    private static OrderBookStorageSnapshot CreateStorageSnapshot(ManagedOrderBook book)
    {
        lock (book.SyncLock)
        {
            return new OrderBookStorageSnapshot
            {
                Symbol = book.Symbol,
                Timestamp = book.LastUpdateTime,
                Sequence = book.LastSequence,
                Bids = book.Bids.Values
                    .OrderByDescending(l => l.Price)
                    .Select(l => new LevelData(l.Price, l.Size, l.MarketMaker))
                    .ToList(),
                Asks = book.Asks.Values
                    .OrderBy(l => l.Price)
                    .Select(l => new LevelData(l.Price, l.Size, l.MarketMaker))
                    .ToList(),
                Spread = book.Spread,
                MidPrice = book.MidPrice
            };
        }
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

internal record OrderBookStorageSnapshot
{
    public required string Symbol { get; init; }
    public DateTimeOffset Timestamp { get; init; }
    public long Sequence { get; init; }
    public required List<LevelData> Bids { get; init; }
    public required List<LevelData> Asks { get; init; }
    public decimal? Spread { get; init; }
    public decimal? MidPrice { get; init; }
}

internal record LevelData(decimal Price, long Size, string? MarketMaker);
