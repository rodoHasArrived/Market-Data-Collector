namespace MarketDataCollector.Ui.Services.Contracts;

/// <summary>
/// Interface for persisting offline tracking data.
/// Shared between WPF and UWP desktop applications.
/// Part of C1 improvement (WPF/UWP service deduplication).
/// </summary>
public interface IOfflineTrackingPersistenceService
{
    Task SaveOfflineDataAsync<T>(string key, T data, CancellationToken cancellationToken = default);
    Task<T?> LoadOfflineDataAsync<T>(string key, CancellationToken cancellationToken = default);
    Task DeleteOfflineDataAsync(string key, CancellationToken cancellationToken = default);
    Task<bool> HasOfflineDataAsync(string key, CancellationToken cancellationToken = default);
}
