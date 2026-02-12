using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MarketDataCollector.Ui.Services;

/// <summary>
/// Default watchlist service for the shared UI services layer.
/// Platform-specific projects (WPF, UWP) override this with their own implementations
/// by setting the Instance property during app startup.
/// </summary>
public class WatchlistService
{
    private static WatchlistService? _instance;
    private static readonly object _lock = new();

    public static WatchlistService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new WatchlistService();
                }
            }
            return _instance;
        }
        set
        {
            lock (_lock)
            {
                _instance = value;
            }
        }
    }

    public virtual Task<WatchlistData> LoadWatchlistAsync()
        => Task.FromResult(new WatchlistData());

    /// <summary>
    /// Creates a new watchlist or updates an existing one.
    /// Platform-specific implementations should override this method.
    /// </summary>
    /// <param name="name">The watchlist name.</param>
    /// <param name="symbols">The symbols to add.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if successful, false otherwise.</returns>
    public virtual Task<bool> CreateOrUpdateWatchlistAsync(string name, IEnumerable<string> symbols, CancellationToken ct = default)
    {
        // Default implementation - platform-specific implementations should override
        return Task.FromResult(false);
    }
}

/// <summary>
/// Watchlist data containing watched symbols.
/// </summary>
public class WatchlistData
{
    public List<WatchlistItem> Symbols { get; set; } = new();
    public List<WatchlistGroup> Groups { get; set; } = new();
}

/// <summary>
/// A single item in a watchlist.
/// </summary>
public class WatchlistItem
{
    public string Symbol { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

/// <summary>
/// A group of symbols in a watchlist.
/// </summary>
public class WatchlistGroup
{
    public string Name { get; set; } = string.Empty;
    public List<string> Symbols { get; set; } = new();
}
