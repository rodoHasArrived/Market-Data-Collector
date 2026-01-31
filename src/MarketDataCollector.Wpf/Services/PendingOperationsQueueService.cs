using System.Threading;
using System.Threading.Tasks;

namespace MarketDataCollector.Wpf.Services;

public sealed class PendingOperationsQueueService : IPendingOperationsQueueService
{
    private readonly ILoggingService _logger;

    public PendingOperationsQueueService(ILoggingService logger)
    {
        _logger = logger;
        _logger.Log("PendingOperationsQueueService initialized (stub implementation)");
    }

    public Task EnqueueAsync<T>(T operation, CancellationToken cancellationToken = default)
    {
        _logger.Log($"EnqueueAsync called for operation type: {typeof(T).Name} (not implemented)");
        return Task.CompletedTask;
    }

    public Task<T?> DequeueAsync<T>(CancellationToken cancellationToken = default)
    {
        _logger.Log($"DequeueAsync called for operation type: {typeof(T).Name} (not implemented)");
        return Task.FromResult<T?>(default);
    }

    public Task<int> GetQueueLengthAsync(CancellationToken cancellationToken = default)
    {
        _logger.Log("GetQueueLengthAsync called (not implemented)");
        return Task.FromResult(0);
    }

    public Task ClearQueueAsync(CancellationToken cancellationToken = default)
    {
        _logger.Log("ClearQueueAsync called (not implemented)");
        return Task.CompletedTask;
    }
}
