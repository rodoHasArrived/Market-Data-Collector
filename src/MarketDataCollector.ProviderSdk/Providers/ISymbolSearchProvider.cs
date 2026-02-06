namespace MarketDataCollector.ProviderSdk.Providers;

/// <summary>
/// Contract for symbol search and autocomplete providers in the plugin system.
/// </summary>
public interface ISymbolSearchProvider : IProviderIdentity
{
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
    Task<SymbolDetails?> GetDetailsAsync(string symbol, CancellationToken ct = default);
}

/// <summary>
/// Extended interface for providers that support filtering in symbol search.
/// </summary>
public interface IFilterableSymbolSearchProvider : ISymbolSearchProvider
{
    /// <summary>
    /// Supported asset types for filtering (e.g., "stock", "etf", "crypto").
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

/// <summary>
/// A symbol search result returned by a provider.
/// </summary>
public sealed record SymbolSearchResult(
    string Symbol,
    string Name,
    string? Exchange = null,
    string? AssetType = null,
    string? Currency = null,
    string? Region = null,
    string? Isin = null,
    string? Figi = null,
    double? MatchScore = null);

/// <summary>
/// Detailed symbol information.
/// </summary>
public sealed record SymbolDetails(
    string Symbol,
    string Name,
    string? Exchange = null,
    string? AssetType = null,
    string? Currency = null,
    string? Description = null,
    string? Sector = null,
    string? Industry = null,
    string? Country = null,
    string? Url = null,
    decimal? MarketCap = null,
    IReadOnlyDictionary<string, string>? AdditionalData = null);
