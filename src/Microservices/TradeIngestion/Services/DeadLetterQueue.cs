using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading;
using DataIngestion.TradeService.Configuration;
using DataIngestion.TradeService.Models;
using Serilog;

namespace DataIngestion.TradeService.Services;

/// <summary>
/// Represents a trade that failed processing and was moved to dead letter queue.
/// </summary>
public sealed record DeadLetteredTrade
{
    /// <summary>Unique identifier for this dead letter entry.</summary>
    public required string Id { get; init; }

    /// <summary>The original trade that failed.</summary>
    public required ProcessedTrade Trade { get; init; }

    /// <summary>Reason for failure.</summary>
    public required string Reason { get; init; }

    /// <summary>Exception message if applicable.</summary>
    public string? ExceptionMessage { get; init; }

    /// <summary>Timestamp when the trade was dead lettered.</summary>
    public DateTimeOffset DeadLetteredAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Number of retry attempts made.</summary>
    public int RetryCount { get; init; }

    /// <summary>Timestamp of last retry attempt.</summary>
    public DateTimeOffset? LastRetryAt { get; init; }
}

/// <summary>
/// Interface for dead letter queue operations.
/// </summary>
public interface IDeadLetterQueue
{
    /// <summary>Enqueue a failed trade to the dead letter queue.</summary>
    Task<bool> EnqueueAsync(ProcessedTrade trade, string reason, Exception? exception = null, CancellationToken ct = default);

    /// <summary>Enqueue a batch of failed trades.</summary>
    Task<int> EnqueueBatchAsync(IEnumerable<ProcessedTrade> trades, string reason, Exception? exception = null, CancellationToken ct = default);

    /// <summary>Try to dequeue a trade for retry.</summary>
    bool TryDequeue(out DeadLetteredTrade? entry);

    /// <summary>Get all dead lettered trades (for inspection).</summary>
    IReadOnlyList<DeadLetteredTrade> GetAll();

    /// <summary>Get dead letter queue statistics.</summary>
    DeadLetterStatistics GetStatistics();

    /// <summary>Current queue depth.</summary>
    int Count { get; }
}

/// <summary>
/// Statistics for the dead letter queue.
/// </summary>
public record DeadLetterStatistics(
    int QueueDepth,
    long TotalEnqueued,
    long TotalRetried,
    long TotalPersisted,
    long TotalExpired
);

/// <summary>
/// In-memory dead letter queue with optional disk persistence.
/// </summary>
public sealed class DeadLetterQueue : IDeadLetterQueue, IAsyncDisposable
{
    private readonly ConcurrentQueue<DeadLetteredTrade> _queue = new();
    private readonly DeadLetterConfig _config;
    private readonly TradeMetrics _metrics;
    private readonly Serilog.ILogger _log = Log.ForContext<DeadLetterQueue>();
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly SemaphoreSlim _persistLock = new(1, 1);

    private long _totalEnqueued;
    private long _totalRetried;
    private long _totalPersisted;
    private long _totalExpired;

    public int Count => _queue.Count;

    public DeadLetterQueue(DeadLetterConfig config, TradeMetrics metrics)
    {
        _config = config;
        _metrics = metrics;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        if (_config.EnablePersistence && !Directory.Exists(_config.PersistenceDirectory))
        {
            Directory.CreateDirectory(_config.PersistenceDirectory);
            _log.Information("Created dead letter persistence directory: {Path}", _config.PersistenceDirectory);
        }
    }

    public async Task<bool> EnqueueAsync(ProcessedTrade trade, string reason, Exception? exception = null, CancellationToken ct = default)
    {
        if (!_config.Enabled)
        {
            return false;
        }

        // Check queue capacity
        if (_queue.Count >= _config.MaxQueueSize)
        {
            _log.Warning(
                "Dead letter queue at capacity ({Capacity}), persisting oldest entries",
                _config.MaxQueueSize);

            await PersistOldestEntriesAsync(100, ct);
        }

        var entry = new DeadLetteredTrade
        {
            Id = Guid.NewGuid().ToString("N"),
            Trade = trade,
            Reason = reason,
            ExceptionMessage = exception?.Message,
            DeadLetteredAt = DateTimeOffset.UtcNow
        };

        _queue.Enqueue(entry);
        Interlocked.Increment(ref _totalEnqueued);
        _metrics.RecordDeadLetterEnqueued(reason);
        _metrics.SetDeadLetterQueueDepth(_queue.Count);

        _log.Debug(
            "Trade dead lettered for {Symbol}: {Reason} (TradeId={TradeId})",
            trade.Symbol,
            reason,
            trade.TradeId ?? "N/A");

        return true;
    }

    public async Task<int> EnqueueBatchAsync(IEnumerable<ProcessedTrade> trades, string reason, Exception? exception = null, CancellationToken ct = default)
    {
        var count = 0;
        foreach (var trade in trades)
        {
            if (await EnqueueAsync(trade, reason, exception, ct))
            {
                count++;
            }
        }
        return count;
    }

    public bool TryDequeue(out DeadLetteredTrade? entry)
    {
        if (_queue.TryDequeue(out entry))
        {
            Interlocked.Increment(ref _totalRetried);
            _metrics.SetDeadLetterQueueDepth(_queue.Count);
            return true;
        }

        entry = null;
        return false;
    }

    public IReadOnlyList<DeadLetteredTrade> GetAll()
    {
        return _queue.ToArray();
    }

    public DeadLetterStatistics GetStatistics()
    {
        return new DeadLetterStatistics(
            QueueDepth: _queue.Count,
            TotalEnqueued: Interlocked.Read(ref _totalEnqueued),
            TotalRetried: Interlocked.Read(ref _totalRetried),
            TotalPersisted: Interlocked.Read(ref _totalPersisted),
            TotalExpired: Interlocked.Read(ref _totalExpired)
        );
    }

    private async Task PersistOldestEntriesAsync(int count, CancellationToken ct)
    {
        if (!_config.EnablePersistence)
        {
            // Just discard if persistence is disabled
            for (var i = 0; i < count && _queue.TryDequeue(out _); i++)
            {
                Interlocked.Increment(ref _totalExpired);
            }
            return;
        }

        await _persistLock.WaitAsync(ct);
        try
        {
            var entries = new List<DeadLetteredTrade>();
            for (var i = 0; i < count && _queue.TryDequeue(out var entry); i++)
            {
                entries.Add(entry!);
            }

            if (entries.Count == 0) return;

            var fileName = $"dead_letter_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}.jsonl";
            var filePath = Path.Combine(_config.PersistenceDirectory, fileName);

            await using var writer = new StreamWriter(filePath);
            foreach (var entry in entries)
            {
                var json = JsonSerializer.Serialize(entry, _jsonOptions);
                await writer.WriteLineAsync(json);
                Interlocked.Increment(ref _totalPersisted);
                _metrics.RecordDeadLetterPersisted();
            }

            _log.Information(
                "Persisted {Count} dead letter entries to {Path}",
                entries.Count,
                filePath);
        }
        finally
        {
            _persistLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        // Persist remaining entries on shutdown
        if (_config.EnablePersistence && _queue.Count > 0)
        {
            _log.Information("Persisting {Count} remaining dead letter entries on shutdown", _queue.Count);
            await PersistOldestEntriesAsync(_queue.Count, CancellationToken.None);
        }

        _persistLock.Dispose();
    }
}
