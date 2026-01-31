using System.Threading;
using System.Threading.Tasks;

namespace MarketDataCollector.Wpf.Services;

public interface IOfflineTrackingPersistenceService
{
    Task SaveOfflineDataAsync<T>(string key, T data, CancellationToken cancellationToken = default);
    Task<T?> LoadOfflineDataAsync<T>(string key, CancellationToken cancellationToken = default);
    Task DeleteOfflineDataAsync(string key, CancellationToken cancellationToken = default);
    Task<bool> HasOfflineDataAsync(string key, CancellationToken cancellationToken = default);
}
