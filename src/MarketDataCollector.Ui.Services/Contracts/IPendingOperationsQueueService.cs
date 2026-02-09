namespace MarketDataCollector.Ui.Services.Contracts;

/// <summary>
/// Interface for queuing and managing pending operations.
/// Shared between WPF and UWP desktop applications.
/// Part of C1 improvement (WPF/UWP service deduplication).
/// </summary>
public interface IPendingOperationsQueueService
{
    Task EnqueueAsync<T>(T operation, CancellationToken cancellationToken = default);
    Task<T?> DequeueAsync<T>(CancellationToken cancellationToken = default);
    Task<int> GetQueueLengthAsync(CancellationToken cancellationToken = default);
    Task ClearQueueAsync(CancellationToken cancellationToken = default);
}
