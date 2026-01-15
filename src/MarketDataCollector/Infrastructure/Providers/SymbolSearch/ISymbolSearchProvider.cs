using MarketDataCollector.Application.Subscriptions.Models;

namespace MarketDataCollector.Infrastructure.Providers.SymbolSearch;

/// <summary>
/// Interface for symbol search and autocomplete providers.
/// </summary>
public interface ISymbolSearchProvider
{
    /// <summary>
    /// Provider identifier.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Human-readable provider name.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Priority for this provider (lower = higher priority).
    /// </summary>
    int Priority { get; }

    /// <summary>
    /// Check if the provider is available/configured.
    /// </summary>
    Task<bool> IsAvailableAsync(CancellationToken ct = default);

    /// <summary>
    /// Search for symbols matching the query.
    /// </summary>
    /// <param name="query">Search query (partial symbol or company name).</param>
    /// <param name="limit">Maximum number of results.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of matching symbols.</returns>
    Task<IReadOnlyList<SymbolSearchResult>> SearchAsync(
        string query,
        int limit = 10,
        CancellationToken ct = default);

    /// <summary>
    /// Get detailed information about a specific symbol.
    /// </summary>
    /// <param name="symbol">Symbol ticker.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Symbol details or null if not found.</returns>
    Task<SymbolDetails?> GetDetailsAsync(string symbol, CancellationToken ct = default);
}

/// <summary>
/// Interface for providers that support filtering in symbol search.
/// </summary>
public interface IFilterableSymbolSearchProvider : ISymbolSearchProvider
{
    /// <summary>
    /// Supported asset types for filtering.
    /// </summary>
    IReadOnlyList<string> SupportedAssetTypes { get; }

    /// <summary>
    /// Supported exchanges for filtering.
    /// </summary>
    IReadOnlyList<string> SupportedExchanges { get; }

    /// <summary>
    /// Search for symbols with filtering options.
    /// </summary>
    Task<IReadOnlyList<SymbolSearchResult>> SearchAsync(
        string query,
        int limit = 10,
        string? assetType = null,
        string? exchange = null,
        CancellationToken ct = default);
}
