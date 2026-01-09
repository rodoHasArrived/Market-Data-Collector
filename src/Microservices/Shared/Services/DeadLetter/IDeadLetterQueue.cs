using System.Threading;

namespace DataIngestion.Contracts.Services.DeadLetter;

/// <summary>
/// Represents a message that failed processing and was moved to dead letter queue.
/// </summary>
/// <typeparam name="TMessage">The type of the failed message.</typeparam>
public sealed record DeadLetteredMessage<TMessage>
{
    /// <summary>Unique identifier for this dead letter entry.</summary>
    public required string Id { get; init; }

    /// <summary>The original message that failed.</summary>
    public required TMessage Message { get; init; }

    /// <summary>Reason for failure.</summary>
    public required string Reason { get; init; }

    /// <summary>Exception message if applicable.</summary>
    public string? ExceptionMessage { get; init; }

    /// <summary>Full exception type name if applicable.</summary>
    public string? ExceptionType { get; init; }

    /// <summary>Stack trace if available.</summary>
    public string? StackTrace { get; init; }

    /// <summary>Timestamp when the message was dead lettered.</summary>
    public DateTimeOffset DeadLetteredAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Number of retry attempts made.</summary>
    public int RetryCount { get; init; }

    /// <summary>Timestamp of last retry attempt.</summary>
    public DateTimeOffset? LastRetryAt { get; init; }

    /// <summary>Source service that dead lettered this message.</summary>
    public string? SourceService { get; init; }

    /// <summary>Correlation ID for tracing.</summary>
    public string? CorrelationId { get; init; }

    /// <summary>Additional context about the failure.</summary>
    public IReadOnlyDictionary<string, string>? Context { get; init; }
}

/// <summary>
/// Generic interface for dead letter queue operations.
/// </summary>
/// <typeparam name="TMessage">The type of messages this queue handles.</typeparam>
public interface IDeadLetterQueue<TMessage>
{
    /// <summary>Enqueue a failed message to the dead letter queue.</summary>
    Task<bool> EnqueueAsync(
        TMessage message,
        string reason,
        Exception? exception = null,
        string? correlationId = null,
        IReadOnlyDictionary<string, string>? context = null,
        CancellationToken ct = default);

    /// <summary>Enqueue a batch of failed messages.</summary>
    Task<int> EnqueueBatchAsync(
        IEnumerable<TMessage> messages,
        string reason,
        Exception? exception = null,
        CancellationToken ct = default);

    /// <summary>Try to dequeue a message for retry.</summary>
    bool TryDequeue(out DeadLetteredMessage<TMessage>? entry);

    /// <summary>Get all dead lettered messages (for inspection).</summary>
    IReadOnlyList<DeadLetteredMessage<TMessage>> GetAll();

    /// <summary>Get dead letter queue statistics.</summary>
    DeadLetterStatistics GetStatistics();

    /// <summary>Clear all entries from the queue.</summary>
    Task ClearAsync(CancellationToken ct = default);

    /// <summary>Current queue depth.</summary>
    int Count { get; }

    /// <summary>Name of the dead letter queue.</summary>
    string QueueName { get; }
}

/// <summary>
/// Statistics for the dead letter queue.
/// </summary>
public sealed record DeadLetterStatistics(
    string QueueName,
    int QueueDepth,
    long TotalEnqueued,
    long TotalRetried,
    long TotalPersisted,
    long TotalExpired,
    DateTimeOffset? OldestEntry = null,
    DateTimeOffset? NewestEntry = null
);

/// <summary>
/// Configuration for dead letter queue behavior.
/// </summary>
public sealed record DeadLetterQueueConfig
{
    /// <summary>Whether the dead letter queue is enabled.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Maximum number of entries in the in-memory queue before persistence kicks in.</summary>
    public int MaxQueueSize { get; init; } = 10000;

    /// <summary>Whether to persist entries to disk when queue overflows.</summary>
    public bool EnablePersistence { get; init; } = true;

    /// <summary>Directory for persisted dead letter entries.</summary>
    public string PersistenceDirectory { get; init; } = "./dead_letters";

    /// <summary>Maximum age of entries before they expire.</summary>
    public TimeSpan? MaxEntryAge { get; init; } = TimeSpan.FromDays(7);

    /// <summary>How many entries to persist when queue reaches capacity.</summary>
    public int PersistBatchSize { get; init; } = 100;

    /// <summary>Name of this dead letter queue (used in logging and metrics).</summary>
    public string QueueName { get; init; } = "default";
}
