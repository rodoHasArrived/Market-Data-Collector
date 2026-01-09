using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading;
using Serilog;

namespace DataIngestion.Contracts.Services.DeadLetter;

/// <summary>
/// Generic in-memory dead letter queue with optional disk persistence.
/// Standardized implementation for use across all microservices.
/// </summary>
/// <typeparam name="TMessage">The type of messages this queue handles.</typeparam>
public sealed class DeadLetterQueue<TMessage> : IDeadLetterQueue<TMessage>, IAsyncDisposable
{
    private readonly ConcurrentQueue<DeadLetteredMessage<TMessage>> _queue = new();
    private readonly DeadLetterQueueConfig _config;
    private readonly ILogger _log;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly SemaphoreSlim _persistLock = new(1, 1);
    private readonly string _serviceName;

    private long _totalEnqueued;
    private long _totalRetried;
    private long _totalPersisted;
    private long _totalExpired;
    private DateTimeOffset? _oldestEntry;
    private DateTimeOffset? _newestEntry;

    public int Count => _queue.Count;
    public string QueueName => _config.QueueName;

    /// <summary>
    /// Creates a new dead letter queue instance.
    /// </summary>
    /// <param name="config">Configuration for the queue.</param>
    /// <param name="serviceName">Name of the service using this queue.</param>
    /// <param name="log">Optional logger instance.</param>
    public DeadLetterQueue(
        DeadLetterQueueConfig config,
        string serviceName,
        ILogger? log = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _serviceName = serviceName ?? throw new ArgumentNullException(nameof(serviceName));
        _log = log?.ForContext<DeadLetterQueue<TMessage>>()
            ?? Log.ForContext<DeadLetterQueue<TMessage>>()
                  .ForContext("QueueName", config.QueueName)
                  .ForContext("ServiceName", serviceName);

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

    public async Task<bool> EnqueueAsync(
        TMessage message,
        string reason,
        Exception? exception = null,
        string? correlationId = null,
        IReadOnlyDictionary<string, string>? context = null,
        CancellationToken ct = default)
    {
        if (!_config.Enabled)
        {
            _log.Debug("Dead letter queue is disabled, discarding message");
            return false;
        }

        // Check queue capacity
        if (_queue.Count >= _config.MaxQueueSize)
        {
            _log.Warning(
                "Dead letter queue {QueueName} at capacity ({Capacity}), persisting oldest entries",
                _config.QueueName,
                _config.MaxQueueSize);

            await PersistOldestEntriesAsync(_config.PersistBatchSize, ct).ConfigureAwait(false);
        }

        var now = DateTimeOffset.UtcNow;
        var entry = new DeadLetteredMessage<TMessage>
        {
            Id = Guid.NewGuid().ToString("N"),
            Message = message,
            Reason = reason,
            ExceptionMessage = exception?.Message,
            ExceptionType = exception?.GetType().FullName,
            StackTrace = exception?.StackTrace,
            DeadLetteredAt = now,
            SourceService = _serviceName,
            CorrelationId = correlationId,
            Context = context
        };

        _queue.Enqueue(entry);
        Interlocked.Increment(ref _totalEnqueued);

        // Update timestamps
        UpdateTimestamps(now);

        _log.Debug(
            "Message dead lettered in {QueueName}: {Reason} (Id={Id}, CorrelationId={CorrelationId})",
            _config.QueueName,
            reason,
            entry.Id,
            correlationId ?? "N/A");

        return true;
    }

    public async Task<int> EnqueueBatchAsync(
        IEnumerable<TMessage> messages,
        string reason,
        Exception? exception = null,
        CancellationToken ct = default)
    {
        var count = 0;
        foreach (var message in messages)
        {
            ct.ThrowIfCancellationRequested();
            if (await EnqueueAsync(message, reason, exception, ct: ct).ConfigureAwait(false))
            {
                count++;
            }
        }
        return count;
    }

    public bool TryDequeue(out DeadLetteredMessage<TMessage>? entry)
    {
        if (_queue.TryDequeue(out entry))
        {
            Interlocked.Increment(ref _totalRetried);
            return true;
        }

        entry = null;
        return false;
    }

    public IReadOnlyList<DeadLetteredMessage<TMessage>> GetAll()
    {
        return _queue.ToArray();
    }

    public DeadLetterStatistics GetStatistics()
    {
        return new DeadLetterStatistics(
            QueueName: _config.QueueName,
            QueueDepth: _queue.Count,
            TotalEnqueued: Interlocked.Read(ref _totalEnqueued),
            TotalRetried: Interlocked.Read(ref _totalRetried),
            TotalPersisted: Interlocked.Read(ref _totalPersisted),
            TotalExpired: Interlocked.Read(ref _totalExpired),
            OldestEntry: _oldestEntry,
            NewestEntry: _newestEntry
        );
    }

    public async Task ClearAsync(CancellationToken ct = default)
    {
        await _persistLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var count = _queue.Count;
            while (_queue.TryDequeue(out _))
            {
                // Clear queue
            }

            _oldestEntry = null;
            _newestEntry = null;

            _log.Information("Cleared {Count} entries from dead letter queue {QueueName}", count, _config.QueueName);
        }
        finally
        {
            _persistLock.Release();
        }
    }

    private void UpdateTimestamps(DateTimeOffset now)
    {
        if (_oldestEntry is null)
            _oldestEntry = now;
        _newestEntry = now;
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

        await _persistLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var entries = new List<DeadLetteredMessage<TMessage>>();
            for (var i = 0; i < count && _queue.TryDequeue(out var entry); i++)
            {
                entries.Add(entry!);
            }

            if (entries.Count == 0) return;

            var fileName = $"{_config.QueueName}_{_serviceName}_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}.jsonl";
            var filePath = Path.Combine(_config.PersistenceDirectory, fileName);

            await using var writer = new StreamWriter(filePath);
            foreach (var entry in entries)
            {
                var json = JsonSerializer.Serialize(entry, _jsonOptions);
                await writer.WriteLineAsync(json).ConfigureAwait(false);
                Interlocked.Increment(ref _totalPersisted);
            }

            _log.Information(
                "Persisted {Count} dead letter entries to {Path} for queue {QueueName}",
                entries.Count,
                filePath,
                _config.QueueName);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Failed to persist dead letter entries for queue {QueueName}", _config.QueueName);
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
            _log.Information(
                "Persisting {Count} remaining dead letter entries on shutdown for queue {QueueName}",
                _queue.Count,
                _config.QueueName);

            await PersistOldestEntriesAsync(_queue.Count, CancellationToken.None).ConfigureAwait(false);
        }

        _persistLock.Dispose();
    }
}
