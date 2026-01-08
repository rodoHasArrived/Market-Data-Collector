namespace MarketDataCollector.Application.Subscriptions.Models;

/// <summary>
/// Enriched metadata for a symbol including industry, sector, and market data.
/// </summary>
public sealed record SymbolMetadata(
    /// <summary>Symbol ticker.</summary>
    string Symbol,

    /// <summary>Company or instrument name.</summary>
    string Name,

    /// <summary>Industry sector (e.g., Technology, Healthcare).</summary>
    string? Sector = null,

    /// <summary>Industry sub-sector.</summary>
    string? Industry = null,

    /// <summary>Market capitalization in USD.</summary>
    decimal? MarketCap = null,

    /// <summary>Market cap category.</summary>
    MarketCapCategory? MarketCapCategory = null,

    /// <summary>Primary exchange where the symbol trades.</summary>
    string? Exchange = null,

    /// <summary>Country of domicile.</summary>
    string? Country = null,

    /// <summary>Asset type: Stock, ETF, ADR, etc.</summary>
    string? AssetType = null,

    /// <summary>Whether the company pays dividends.</summary>
    bool? PaysDividend = null,

    /// <summary>Indices that include this symbol.</summary>
    string[]? IndexMemberships = null,

    /// <summary>When the metadata was last updated.</summary>
    DateTimeOffset? LastUpdated = null
);

/// <summary>
/// Market capitalization categories.
/// </summary>
public enum MarketCapCategory
{
    /// <summary>Market cap under $300M.</summary>
    Nano,

    /// <summary>Market cap $300M - $2B.</summary>
    Micro,

    /// <summary>Market cap $2B - $10B.</summary>
    Small,

    /// <summary>Market cap $10B - $200B.</summary>
    Mid,

    /// <summary>Market cap over $200B.</summary>
    Large,

    /// <summary>Market cap over $200B (mega cap).</summary>
    Mega
}

/// <summary>
/// Filter criteria for symbol metadata queries.
/// </summary>
public sealed record SymbolMetadataFilter(
    /// <summary>Filter by sector (exact match).</summary>
    string? Sector = null,

    /// <summary>Filter by industry (exact match).</summary>
    string? Industry = null,

    /// <summary>Minimum market cap in USD.</summary>
    decimal? MinMarketCap = null,

    /// <summary>Maximum market cap in USD.</summary>
    decimal? MaxMarketCap = null,

    /// <summary>Filter by market cap category.</summary>
    MarketCapCategory? MarketCapCategory = null,

    /// <summary>Filter by exchange.</summary>
    string? Exchange = null,

    /// <summary>Filter by country.</summary>
    string? Country = null,

    /// <summary>Filter by asset type.</summary>
    string? AssetType = null,

    /// <summary>Filter by index membership.</summary>
    string? IndexMembership = null,

    /// <summary>Only include dividend-paying symbols.</summary>
    bool? PaysDividend = null
);

/// <summary>
/// Result of a metadata filter operation.
/// </summary>
public sealed record MetadataFilterResult(
    SymbolMetadata[] Symbols,
    int TotalCount,
    SymbolMetadataFilter AppliedFilter
);
