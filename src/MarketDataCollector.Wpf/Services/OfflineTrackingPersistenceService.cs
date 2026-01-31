using System.Threading;
using System.Threading.Tasks;

namespace MarketDataCollector.Wpf.Services;

public sealed class OfflineTrackingPersistenceService : IOfflineTrackingPersistenceService
{
    private readonly ILoggingService _logger;

    public OfflineTrackingPersistenceService(ILoggingService logger)
    {
        _logger = logger;
        _logger.Log("OfflineTrackingPersistenceService initialized (stub implementation)");
    }

    public Task SaveOfflineDataAsync<T>(string key, T data, CancellationToken cancellationToken = default)
    {
        _logger.Log($"SaveOfflineDataAsync called for key: {key} (not implemented)");
        return Task.CompletedTask;
    }

    public Task<T?> LoadOfflineDataAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        _logger.Log($"LoadOfflineDataAsync called for key: {key} (not implemented)");
        return Task.FromResult<T?>(default);
    }

    public Task DeleteOfflineDataAsync(string key, CancellationToken cancellationToken = default)
    {
        _logger.Log($"DeleteOfflineDataAsync called for key: {key} (not implemented)");
        return Task.CompletedTask;
    }

    public Task<bool> HasOfflineDataAsync(string key, CancellationToken cancellationToken = default)
    {
        _logger.Log($"HasOfflineDataAsync called for key: {key} (not implemented)");
        return Task.FromResult(false);
    }
}
