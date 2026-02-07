using System.Threading.Tasks;

namespace MarketDataCollector.Ui.Services.Contracts;

/// <summary>
/// Interface for watchlist services used by shared UI services.
/// Implemented by platform-specific watchlist services (WPF, UWP).
/// </summary>
public interface IWatchlistService
{
    Task<WatchlistData> LoadWatchlistAsync();
}
