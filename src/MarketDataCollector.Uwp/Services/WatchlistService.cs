using System.Text.Json;
using System.Threading;

namespace MarketDataCollector.Uwp.Services;

/// <summary>
/// Service for managing user's watchlist of favorite symbols.
/// Provides quick access to frequently monitored securities with real-time status.
/// </summary>
public sealed class WatchlistService : IWatchlistService
{
    private const string WatchlistFileName = "watchlist.json";
    // Use centralized JSON options to avoid duplication across services
    private static JsonSerializerOptions JsonOptions => DesktopJsonOptions.PrettyPrint;

    private static WatchlistService? _instance;
    private static readonly object _lock = new();
    private readonly string _watchlistPath;
    private WatchlistData? _cachedData;

    /// <summary>
    /// Gets the singleton instance of the WatchlistService.
    /// </summary>
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
    }

    /// <summary>
    /// Event raised when the watchlist changes.
    /// </summary>
    public event EventHandler<WatchlistChangedEventArgs>? WatchlistChanged;

    private WatchlistService()
    {
        var appDir = AppContext.BaseDirectory;
        _watchlistPath = Path.Combine(appDir, "data", WatchlistFileName);
    }

    /// <summary>
    /// Loads the watchlist from disk.
    /// </summary>
    public async Task<WatchlistData> LoadWatchlistAsync()
    {
        if (_cachedData != null) return _cachedData;

        try
        {
            if (File.Exists(_watchlistPath))
            {
                var json = await File.ReadAllTextAsync(_watchlistPath);
                _cachedData = JsonSerializer.Deserialize<WatchlistData>(json, JsonOptions) ?? CreateDefaultWatchlist();
            }
            else
            {
                _cachedData = CreateDefaultWatchlist();
                await SaveWatchlistAsync(_cachedData);
            }
        }
        catch (Exception ex)
        {
            // Log the error but gracefully degrade to defaults
            LoggingService.Instance.LogWarning("Failed to load watchlist, using defaults", ex);
            _cachedData = CreateDefaultWatchlist();
        }

        return _cachedData;
    }

    /// <summary>
    /// Saves the watchlist to disk.
    /// </summary>
    public async Task SaveWatchlistAsync(WatchlistData data, CancellationToken cancellationToken = default)
    {
        try
        {
            var directory = Path.GetDirectoryName(_watchlistPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(data, JsonOptions);
            await File.WriteAllTextAsync(_watchlistPath, json, cancellationToken);
            _cachedData = data;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Log the error but don't crash - watchlist is non-critical
            LoggingService.Instance.LogWarning("Failed to save watchlist", ex);
        }
    }

    /// <summary>
    /// Adds a symbol to the watchlist.
    /// </summary>
    public async Task<bool> AddSymbolAsync(string symbol, string? notes = null, CancellationToken cancellationToken = default)
    {
        var data = await LoadWatchlistAsync();
        var normalizedSymbol = symbol.ToUpperInvariant().Trim();

        if (data.Symbols.Any(s => s.Symbol == normalizedSymbol))
        {
            return false; // Already exists
        }

        var newItem = new WatchlistItem
        {
            Symbol = normalizedSymbol,
            AddedAt = DateTime.UtcNow,
            Notes = notes,
            SortOrder = data.Symbols.Count
        };

        data.Symbols.Add(newItem);
        data.UpdatedAt = DateTime.UtcNow;

        await SaveWatchlistAsync(data, cancellationToken);
        OnWatchlistChanged(WatchlistChangeType.Added, newItem);

        return true;
    }

    /// <summary>
    /// Removes a symbol from the watchlist.
    /// </summary>
    public async Task<bool> RemoveSymbolAsync(string symbol, CancellationToken cancellationToken = default)
    {
        var data = await LoadWatchlistAsync();
        var normalizedSymbol = symbol.ToUpperInvariant().Trim();
        var item = data.Symbols.FirstOrDefault(s => s.Symbol == normalizedSymbol);

        if (item == null)
        {
            return false;
        }

        data.Symbols.Remove(item);
        data.UpdatedAt = DateTime.UtcNow;

        await SaveWatchlistAsync(data, cancellationToken);
        OnWatchlistChanged(WatchlistChangeType.Removed, item);

        return true;
    }

    /// <summary>
    /// Updates a watchlist item.
    /// </summary>
    public async Task<bool> UpdateItemAsync(WatchlistItem item, CancellationToken cancellationToken = default)
    {
        var data = await LoadWatchlistAsync();
        var existingIndex = data.Symbols.FindIndex(s => s.Symbol == item.Symbol);

        if (existingIndex < 0)
        {
            return false;
        }

        item.UpdatedAt = DateTime.UtcNow;
        data.Symbols[existingIndex] = item;
        data.UpdatedAt = DateTime.UtcNow;

        await SaveWatchlistAsync(data, cancellationToken);
        OnWatchlistChanged(WatchlistChangeType.Updated, item);

        return true;
    }

    /// <summary>
    /// Toggles favorite status for a symbol.
    /// </summary>
    public async Task<bool> ToggleFavoriteAsync(string symbol, CancellationToken cancellationToken = default)
    {
        var data = await LoadWatchlistAsync();
        var normalizedSymbol = symbol.ToUpperInvariant().Trim();
        var item = data.Symbols.FirstOrDefault(s => s.Symbol == normalizedSymbol);

        if (item == null)
        {
            return false;
        }

        item.IsFavorite = !item.IsFavorite;
        item.UpdatedAt = DateTime.UtcNow;
        data.UpdatedAt = DateTime.UtcNow;

        await SaveWatchlistAsync(data, cancellationToken);
        OnWatchlistChanged(WatchlistChangeType.Updated, item);

        return true;
    }

    /// <summary>
    /// Reorders the watchlist.
    /// </summary>
    public async Task ReorderAsync(List<string> symbolOrder, CancellationToken cancellationToken = default)
    {
        var data = await LoadWatchlistAsync();

        for (int i = 0; i < symbolOrder.Count; i++)
        {
            var item = data.Symbols.FirstOrDefault(s => s.Symbol == symbolOrder[i]);
            if (item != null)
            {
                item.SortOrder = i;
            }
        }

        data.Symbols = data.Symbols.OrderBy(s => s.SortOrder).ToList();
        data.UpdatedAt = DateTime.UtcNow;

        await SaveWatchlistAsync(data, cancellationToken);
        OnWatchlistChanged(WatchlistChangeType.Reordered, null);
    }

    /// <summary>
    /// Gets symbols sorted by favorites first, then by sort order.
    /// </summary>
    public async Task<List<WatchlistItem>> GetSortedSymbolsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var data = await LoadWatchlistAsync();
        return data.Symbols
            .OrderByDescending(s => s.IsFavorite)
            .ThenBy(s => s.SortOrder)
            .ToList();
    }

    /// <summary>
    /// Checks if a symbol is in the watchlist.
    /// </summary>
    public async Task<bool> ContainsSymbolAsync(string symbol, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var data = await LoadWatchlistAsync();
        return data.Symbols.Any(s => s.Symbol == symbol.ToUpperInvariant().Trim());
    }

    /// <summary>
    /// Updates real-time status for a symbol.
    /// </summary>
    public async Task UpdateSymbolStatusAsync(string symbol, WatchlistItemStatus status, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var data = await LoadWatchlistAsync();
        var item = data.Symbols.FirstOrDefault(s => s.Symbol == symbol.ToUpperInvariant().Trim());

        if (item != null)
        {
            item.Status = status;
            item.LastStatusUpdate = DateTime.UtcNow;
            _cachedData = data; // Update cache without saving to disk (for real-time updates)
        }
    }

    /// <summary>
    /// Creates a new watchlist or updates an existing one.
    /// Since UWP uses a single watchlist model, this adds symbols to the main watchlist.
    /// </summary>
    /// <param name="name">The watchlist name (unused in UWP single-list model).</param>
    /// <param name="symbols">The symbols to add.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if successful, false otherwise.</returns>
    public async Task<bool> CreateOrUpdateWatchlistAsync(string name, IEnumerable<string> symbols, CancellationToken cancellationToken = default)
    {
        try
        {
            // Add all symbols to the watchlist
            foreach (var symbol in symbols)
            {
                await AddSymbolAsync(symbol, null, cancellationToken);
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static WatchlistData CreateDefaultWatchlist()
    {
        return new WatchlistData
        {
            Version = "1.0",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Symbols = new List<WatchlistItem>
            {
                new() { Symbol = "SPY", IsFavorite = true, SortOrder = 0, AddedAt = DateTime.UtcNow },
                new() { Symbol = "QQQ", SortOrder = 1, AddedAt = DateTime.UtcNow },
                new() { Symbol = "AAPL", SortOrder = 2, AddedAt = DateTime.UtcNow }
            }
        };
    }

    private void OnWatchlistChanged(WatchlistChangeType changeType, WatchlistItem? item)
    {
        WatchlistChanged?.Invoke(this, new WatchlistChangedEventArgs(changeType, item));
    }
}

/// <summary>
/// Watchlist data container.
/// </summary>
public class WatchlistData
{
    public string Version { get; set; } = "1.0";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public List<WatchlistItem> Symbols { get; set; } = new();
}

/// <summary>
/// Individual watchlist item.
/// </summary>
public class WatchlistItem
{
    public string Symbol { get; set; } = string.Empty;
    public bool IsFavorite { get; set; }
    public int SortOrder { get; set; }
    public string? Notes { get; set; }
    public string? AlertPrice { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public DateTime? LastStatusUpdate { get; set; }
    public WatchlistItemStatus? Status { get; set; }
    public string[]? Tags { get; set; }
}

/// <summary>
/// Real-time status for a watchlist item.
/// </summary>
public class WatchlistItemStatus
{
    public bool IsStreaming { get; set; }
    public long EventCount { get; set; }
    public double EventRate { get; set; }
    public DateTime? LastEventTime { get; set; }
    public string? DataHealth { get; set; }
    public double? HealthScore { get; set; }
}

/// <summary>
/// Types of watchlist changes.
/// </summary>
public enum WatchlistChangeType
{
    Added,
    Removed,
    Updated,
    Reordered
}

/// <summary>
/// Event args for watchlist changes.
/// </summary>
public class WatchlistChangedEventArgs : EventArgs
{
    public WatchlistChangeType ChangeType { get; }
    public WatchlistItem? Item { get; }

    public WatchlistChangedEventArgs(WatchlistChangeType changeType, WatchlistItem? item)
    {
        ChangeType = changeType;
        Item = item;
    }
}
