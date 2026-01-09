namespace MarketDataCollector.Application.Subscriptions.Models;

/// <summary>
/// Represents a single symbol search result from autocomplete.
/// </summary>
public sealed record SymbolSearchResult(
    /// <summary>Symbol ticker (e.g., AAPL, MSFT).</summary>
    string Symbol,

    /// <summary>Company or instrument name.</summary>
    string Name,

    /// <summary>Primary exchange where the symbol trades.</summary>
    string? Exchange = null,

    /// <summary>Asset type: Stock, ETF, ADR, Crypto, etc.</summary>
    string? AssetType = null,

    /// <summary>Country or region of the instrument.</summary>
    string? Country = null,

    /// <summary>Currency in which the instrument trades.</summary>
    string? Currency = null,

    /// <summary>Provider that returned this result.</summary>
    string? Source = null,

    /// <summary>Match score/relevance (0-100, higher is better).</summary>
    int MatchScore = 0,

    /// <summary>FIGI identifier if available.</summary>
    string? Figi = null,

    /// <summary>Composite FIGI if available.</summary>
    string? CompositeFigi = null
);

/// <summary>
/// Detailed symbol information including market data.
/// </summary>
public sealed record SymbolDetails(
    /// <summary>Symbol ticker.</summary>
    string Symbol,

    /// <summary>Full company or instrument name.</summary>
    string Name,

    /// <summary>Short description of the company/instrument.</summary>
    string? Description = null,

    /// <summary>Primary exchange.</summary>
    string? Exchange = null,

    /// <summary>Asset type: Stock, ETF, ADR, Crypto, etc.</summary>
    string? AssetType = null,

    /// <summary>Industry sector.</summary>
    string? Sector = null,

    /// <summary>Industry sub-sector.</summary>
    string? Industry = null,

    /// <summary>Country of domicile.</summary>
    string? Country = null,

    /// <summary>Trading currency.</summary>
    string? Currency = null,

    /// <summary>Market capitalization in USD.</summary>
    decimal? MarketCap = null,

    /// <summary>Average daily trading volume.</summary>
    long? AverageVolume = null,

    /// <summary>52-week high price.</summary>
    decimal? Week52High = null,

    /// <summary>52-week low price.</summary>
    decimal? Week52Low = null,

    /// <summary>Current/last price.</summary>
    decimal? LastPrice = null,

    /// <summary>Company website URL.</summary>
    string? WebUrl = null,

    /// <summary>Company logo URL.</summary>
    string? LogoUrl = null,

    /// <summary>IPO date.</summary>
    DateOnly? IpoDate = null,

    /// <summary>Whether the company pays dividends.</summary>
    bool? PaysDividend = null,

    /// <summary>Dividend yield percentage.</summary>
    decimal? DividendYield = null,

    /// <summary>Price to earnings ratio.</summary>
    decimal? PeRatio = null,

    /// <summary>Shares outstanding.</summary>
    long? SharesOutstanding = null,

    /// <summary>FIGI identifier.</summary>
    string? Figi = null,

    /// <summary>Composite FIGI.</summary>
    string? CompositeFigi = null,

    /// <summary>ISIN code.</summary>
    string? Isin = null,

    /// <summary>CUSIP code.</summary>
    string? Cusip = null,

    /// <summary>Provider that returned this information.</summary>
    string? Source = null,

    /// <summary>When the details were last updated.</summary>
    DateTimeOffset? LastUpdated = null
);

/// <summary>
/// Request for symbol search/autocomplete.
/// </summary>
public sealed record SymbolSearchRequest(
    /// <summary>Search query (partial symbol or company name).</summary>
    string Query,

    /// <summary>Maximum number of results to return.</summary>
    int Limit = 10,

    /// <summary>Filter by asset type (Stock, ETF, ADR, etc.).</summary>
    string? AssetType = null,

    /// <summary>Filter by exchange.</summary>
    string? Exchange = null,

    /// <summary>Filter by country/region.</summary>
    string? Country = null,

    /// <summary>Specific provider to search (null = all available).</summary>
    string? Provider = null,

    /// <summary>Include OpenFIGI lookup in results.</summary>
    bool IncludeFigi = true
);

/// <summary>
/// Response from symbol search.
/// </summary>
public sealed record SymbolSearchResponse(
    /// <summary>Search results ordered by relevance.</summary>
    IReadOnlyList<SymbolSearchResult> Results,

    /// <summary>Total count of results found.</summary>
    int TotalCount,

    /// <summary>Providers that contributed results.</summary>
    IReadOnlyList<string> Sources,

    /// <summary>Time taken to complete search in milliseconds.</summary>
    long ElapsedMs,

    /// <summary>Original query.</summary>
    string Query
);

/// <summary>
/// OpenFIGI mapping result.
/// </summary>
public sealed record FigiMapping(
    /// <summary>FIGI identifier.</summary>
    string Figi,

    /// <summary>Composite FIGI for the security.</summary>
    string? CompositeFigi = null,

    /// <summary>Security type (Common Stock, ADR, etc.).</summary>
    string? SecurityType = null,

    /// <summary>Market sector.</summary>
    string? MarketSector = null,

    /// <summary>Ticker symbol.</summary>
    string? Ticker = null,

    /// <summary>Security name.</summary>
    string? Name = null,

    /// <summary>Exchange code.</summary>
    string? ExchangeCode = null,

    /// <summary>Share class FIGI.</summary>
    string? ShareClassFigi = null,

    /// <summary>Security description.</summary>
    string? SecurityDescription = null
);

/// <summary>
/// Bulk OpenFIGI lookup request item.
/// </summary>
public sealed record FigiLookupRequest(
    /// <summary>Identifier type: ID_TICKER, ID_ISIN, ID_CUSIP, ID_SEDOL, etc.</summary>
    string IdType,

    /// <summary>Identifier value.</summary>
    string IdValue,

    /// <summary>Optional exchange code to narrow results.</summary>
    string? ExchCode = null,

    /// <summary>Optional market sector (Equity, Govt, etc.).</summary>
    string? MarketSecDes = null
);
