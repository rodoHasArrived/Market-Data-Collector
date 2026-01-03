using MarketDataCollector.Domain.Events;

namespace MarketDataCollector.Domain.Models;

/// <summary>
/// Historical auction data from Alpaca Markets API.
/// Contains opening and closing auction information for a trading session.
/// Useful for VWAP calculations and understanding market open/close dynamics.
/// </summary>
public sealed record HistoricalAuction(
    string Symbol,
    DateOnly SessionDate,
    IReadOnlyList<AuctionPrice> OpeningAuctions,
    IReadOnlyList<AuctionPrice> ClosingAuctions,
    string Source = "alpaca",
    long SequenceNumber = 0
) : MarketEventPayload
{
    public HistoricalAuction
    {
        if (string.IsNullOrWhiteSpace(Symbol))
            throw new ArgumentException("Symbol is required", nameof(Symbol));

        OpeningAuctions ??= Array.Empty<AuctionPrice>();
        ClosingAuctions ??= Array.Empty<AuctionPrice>();
    }

    /// <summary>
    /// Gets the primary opening auction price (first valid opening auction).
    /// </summary>
    public decimal? PrimaryOpenPrice => OpeningAuctions.FirstOrDefault()?.Price;

    /// <summary>
    /// Gets the primary opening auction volume.
    /// </summary>
    public long? PrimaryOpenVolume => OpeningAuctions.FirstOrDefault()?.Size;

    /// <summary>
    /// Gets the primary closing auction price (first valid closing auction).
    /// </summary>
    public decimal? PrimaryClosePrice => ClosingAuctions.FirstOrDefault()?.Price;

    /// <summary>
    /// Gets the primary closing auction volume.
    /// </summary>
    public long? PrimaryCloseVolume => ClosingAuctions.FirstOrDefault()?.Size;

    /// <summary>
    /// Total volume from all opening auctions.
    /// </summary>
    public long TotalOpeningVolume => OpeningAuctions.Sum(a => a.Size);

    /// <summary>
    /// Total volume from all closing auctions.
    /// </summary>
    public long TotalClosingVolume => ClosingAuctions.Sum(a => a.Size);
}

/// <summary>
/// Individual auction price record within an opening or closing auction.
/// </summary>
public sealed record AuctionPrice(
    DateTimeOffset Timestamp,
    decimal Price,
    long Size,
    string? Exchange = null,
    string? Condition = null
)
{
    public AuctionPrice
    {
        if (Price <= 0)
            throw new ArgumentOutOfRangeException(nameof(Price), "Price must be greater than zero.");

        if (Size < 0)
            throw new ArgumentOutOfRangeException(nameof(Size), "Size cannot be negative.");
    }

    /// <summary>
    /// Notional value of the auction (Price * Size).
    /// </summary>
    public decimal NotionalValue => Price * Size;
}
