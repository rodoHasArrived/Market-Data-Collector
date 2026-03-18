using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MarketDataCollector.Ui.Services.Contracts;

/// <summary>
/// Interface for watchlist services used by shared UI services.
/// Implemented by platform-specific watchlist services (WPF).
/// </summary>
public interface IWatchlistService
{
    Task<WatchlistData> LoadWatchlistAsync();

    /// <summary>
    /// Creates a new watchlist or updates an existing one.
    /// </summary>
    /// <param name="name">The watchlist name.</param>
    /// <param name="symbols">The symbols to add.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if successful, false otherwise.</returns>
    Task<bool> CreateOrUpdateWatchlistAsync(string name, IEnumerable<string> symbols, CancellationToken ct = default);
}
