using MarketDataCollector.Domain.Models;

namespace MarketDataCollector.Infrastructure.Providers.Backfill;

/// <summary>
/// Extended historical data provider interface with support for tick-level data:
/// quotes (NBBO), trades, and auction prices.
/// </summary>
public interface IHistoricalDataProviderExtended : IHistoricalDataProviderV2
{
    /// <summary>
    /// Whether this provider supports historical quote (NBBO) data.
    /// </summary>
    bool SupportsQuotes { get; }

    /// <summary>
    /// Whether this provider supports historical trade data.
    /// </summary>
    bool SupportsTrades { get; }

    /// <summary>
    /// Whether this provider supports historical auction data.
    /// </summary>
    bool SupportsAuctions { get; }

    /// <summary>
    /// Fetch historical NBBO quotes for a symbol within the specified date range.
    /// </summary>
    /// <param name="symbol">The stock symbol (e.g., "AAPL").</param>
    /// <param name="start">Start timestamp (inclusive).</param>
    /// <param name="end">End timestamp (exclusive).</param>
    /// <param name="limit">Maximum number of quotes to return per request (for pagination).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A collection of historical quotes sorted by timestamp.</returns>
    Task<HistoricalQuotesResult> GetHistoricalQuotesAsync(
        string symbol,
        DateTimeOffset start,
        DateTimeOffset end,
        int? limit = null,
        CancellationToken ct = default);

    /// <summary>
    /// Fetch historical quotes for multiple symbols within the specified date range.
    /// </summary>
    /// <param name="symbols">List of stock symbols.</param>
    /// <param name="start">Start timestamp (inclusive).</param>
    /// <param name="end">End timestamp (exclusive).</param>
    /// <param name="limit">Maximum number of quotes to return per request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A collection of historical quotes for all symbols.</returns>
    Task<HistoricalQuotesResult> GetHistoricalQuotesAsync(
        IEnumerable<string> symbols,
        DateTimeOffset start,
        DateTimeOffset end,
        int? limit = null,
        CancellationToken ct = default);

    /// <summary>
    /// Fetch historical trades for a symbol within the specified date range.
    /// </summary>
    /// <param name="symbol">The stock symbol (e.g., "AAPL").</param>
    /// <param name="start">Start timestamp (inclusive).</param>
    /// <param name="end">End timestamp (exclusive).</param>
    /// <param name="limit">Maximum number of trades to return per request (for pagination).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A collection of historical trades sorted by timestamp.</returns>
    Task<HistoricalTradesResult> GetHistoricalTradesAsync(
        string symbol,
        DateTimeOffset start,
        DateTimeOffset end,
        int? limit = null,
        CancellationToken ct = default);

    /// <summary>
    /// Fetch historical trades for multiple symbols within the specified date range.
    /// </summary>
    /// <param name="symbols">List of stock symbols.</param>
    /// <param name="start">Start timestamp (inclusive).</param>
    /// <param name="end">End timestamp (exclusive).</param>
    /// <param name="limit">Maximum number of trades to return per request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A collection of historical trades for all symbols.</returns>
    Task<HistoricalTradesResult> GetHistoricalTradesAsync(
        IEnumerable<string> symbols,
        DateTimeOffset start,
        DateTimeOffset end,
        int? limit = null,
        CancellationToken ct = default);

    /// <summary>
    /// Fetch historical auction data for a symbol within the specified date range.
    /// </summary>
    /// <param name="symbol">The stock symbol (e.g., "AAPL").</param>
    /// <param name="start">Start date (inclusive).</param>
    /// <param name="end">End date (inclusive).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A collection of historical auctions sorted by session date.</returns>
    Task<HistoricalAuctionsResult> GetHistoricalAuctionsAsync(
        string symbol,
        DateOnly start,
        DateOnly end,
        CancellationToken ct = default);

    /// <summary>
    /// Fetch historical auction data for multiple symbols within the specified date range.
    /// </summary>
    /// <param name="symbols">List of stock symbols.</param>
    /// <param name="start">Start date (inclusive).</param>
    /// <param name="end">End date (inclusive).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A collection of historical auctions for all symbols.</returns>
    Task<HistoricalAuctionsResult> GetHistoricalAuctionsAsync(
        IEnumerable<string> symbols,
        DateOnly start,
        DateOnly end,
        CancellationToken ct = default);
}

/// <summary>
/// Result container for historical quotes with pagination support.
/// </summary>
public sealed record HistoricalQuotesResult(
    IReadOnlyList<HistoricalQuote> Quotes,
    string? NextPageToken = null,
    int TotalCount = 0
)
{
    /// <summary>
    /// Whether there are more results available.
    /// </summary>
    public bool HasMore => !string.IsNullOrEmpty(NextPageToken);
}

/// <summary>
/// Result container for historical trades with pagination support.
/// </summary>
public sealed record HistoricalTradesResult(
    IReadOnlyList<HistoricalTrade> Trades,
    string? NextPageToken = null,
    int TotalCount = 0
)
{
    /// <summary>
    /// Whether there are more results available.
    /// </summary>
    public bool HasMore => !string.IsNullOrEmpty(NextPageToken);
}

/// <summary>
/// Result container for historical auctions with pagination support.
/// </summary>
public sealed record HistoricalAuctionsResult(
    IReadOnlyList<HistoricalAuction> Auctions,
    string? NextPageToken = null,
    int TotalCount = 0
)
{
    /// <summary>
    /// Whether there are more results available.
    /// </summary>
    public bool HasMore => !string.IsNullOrEmpty(NextPageToken);
}
