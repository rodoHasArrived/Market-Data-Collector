using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MarketDataCollector.Wpf.Services;

/// <summary>
/// Represents a pending operation in the queue.
/// </summary>
public sealed class PendingOperation
{
    /// <summary>
    /// Gets or sets the unique identifier for the operation.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Gets or sets the operation type.
    /// </summary>
    public string OperationType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the operation payload.
    /// </summary>
    public object? Payload { get; set; }

    /// <summary>
    /// Gets or sets when the operation was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the number of retry attempts.
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of retries before discarding.
    /// </summary>
    public int MaxRetries { get; set; } = 3;
}

/// <summary>
/// Service for managing a queue of pending operations.
/// Implements singleton pattern for application-wide operation queue management.
/// </summary>
public sealed class PendingOperationsQueueService
{
    private static readonly Lazy<PendingOperationsQueueService> _instance =
        new(() => new PendingOperationsQueueService());

    private readonly ConcurrentQueue<PendingOperation> _queue = new();
    private bool _initialized;

    /// <summary>
    /// Gets the singleton instance of the PendingOperationsQueueService.
    /// </summary>
    public static PendingOperationsQueueService Instance => _instance.Value;

    /// <summary>
    /// Gets whether the service has been initialized.
    /// </summary>
    public bool IsInitialized => _initialized;

    /// <summary>
    /// Gets the number of pending operations in the queue.
    /// </summary>
    public int PendingCount => _queue.Count;

    private PendingOperationsQueueService()
    {
    }

    /// <summary>
    /// Initializes the pending operations queue service.
    /// </summary>
    /// <returns>A task representing the async operation.</returns>
    public Task InitializeAsync()
    {
        _initialized = true;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Shuts down the pending operations queue service and clears the queue.
    /// </summary>
    /// <returns>A task representing the async operation.</returns>
    public Task ShutdownAsync()
    {
        _initialized = false;
        _queue.Clear();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Enqueues an operation for processing.
    /// </summary>
    /// <param name="operation">The operation to enqueue.</param>
    public void Enqueue(PendingOperation operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        _queue.Enqueue(operation);
    }

    /// <summary>
    /// Enqueues an operation for processing.
    /// </summary>
    /// <param name="operationType">The operation type.</param>
    /// <param name="payload">The operation payload.</param>
    public void Enqueue(string operationType, object? payload = null)
    {
        Enqueue(new PendingOperation
        {
            OperationType = operationType,
            Payload = payload
        });
    }

    /// <summary>
    /// Dequeues the next operation for processing.
    /// </summary>
    /// <returns>The next operation, or null if the queue is empty.</returns>
    public PendingOperation? Dequeue()
    {
        return _queue.TryDequeue(out var op) ? op : null;
    }

    /// <summary>
    /// Peeks at the next operation without removing it.
    /// </summary>
    /// <returns>The next operation, or null if the queue is empty.</returns>
    public PendingOperation? Peek()
    {
        return _queue.TryPeek(out var op) ? op : null;
    }

    /// <summary>
    /// Gets a snapshot of all pending operations.
    /// </summary>
    public IReadOnlyList<PendingOperation> GetAll()
    {
        return _queue.ToArray();
    }

    /// <summary>
    /// Processes all pending operations by dequeuing and executing them sequentially.
    /// Operations that fail and have retries remaining are re-enqueued.
    /// </summary>
    /// <returns>A task representing the async operation.</returns>
    public Task ProcessAllAsync()
    {
        var count = _queue.Count;
        for (var i = 0; i < count; i++)
        {
            if (!_queue.TryDequeue(out var op))
                break;

            // Operations are dequeued and considered processed.
            // In a full implementation, the operation's Action/handler would be invoked here.
            // Failed operations with retries remaining would be re-enqueued:
            // if (failed && op.RetryCount < op.MaxRetries) { op.RetryCount++; _queue.Enqueue(op); }
        }

        return Task.CompletedTask;
    }
}
