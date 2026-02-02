using System.Threading;
using System.Threading.Tasks;

namespace MarketDataCollector.Wpf.Services;

public interface IPendingOperationsQueueService
{
    Task EnqueueAsync<T>(T operation, CancellationToken cancellationToken = default);
    Task<T?> DequeueAsync<T>(CancellationToken cancellationToken = default);
    Task<int> GetQueueLengthAsync(CancellationToken cancellationToken = default);
    Task ClearQueueAsync(CancellationToken cancellationToken = default);
}
