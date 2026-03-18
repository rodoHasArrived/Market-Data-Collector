using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MarketDataCollector.Ui.Services.Contracts;

namespace MarketDataCollector.Ui.Services;

/// <summary>
/// Default watchlist service for the shared UI services layer.
/// Platform-specific projects (WPF) can supply their own <see cref="IWatchlistService"/>
/// implementation by assigning it to <see cref="Instance"/> during app startup.
/// </summary>
public sealed class WatchlistService : IWatchlistService
{
    private static IWatchlistService _instance = new WatchlistService();

    /// <summary>
    /// Gets or sets the active <see cref="IWatchlistService"/> implementation.
    /// Replace with a platform-specific implementation during app startup.
    /// </summary>
    public static IWatchlistService Instance
    {
        get => _instance;
        set => _instance = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <inheritdoc/>
    public Task<WatchlistData> LoadWatchlistAsync()
        => Task.FromResult(new WatchlistData());

    /// <inheritdoc/>
    public Task<bool> CreateOrUpdateWatchlistAsync(string name, IEnumerable<string> symbols, CancellationToken ct = default)
    {
        return Task.FromResult(false);
    }
}

/// <summary>
/// Watchlist data containing watched symbols.
/// </summary>
public sealed class WatchlistData
{
    public List<WatchlistItem> Symbols { get; set; } = new();
    public List<WatchlistGroup> Groups { get; set; } = new();
}

/// <summary>
/// A single item in a watchlist.
/// </summary>
public sealed class WatchlistItem
{
    public string Symbol { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public List<string> Tags { get; set; } = new();
}

/// <summary>
/// A group of symbols in a watchlist.
/// </summary>
public sealed class WatchlistGroup
{
    public string Name { get; set; } = string.Empty;
    public List<string> Symbols { get; set; } = new();
}
