namespace MarketDataCollector.Domain.Models;

// TODO: Consider using decimal instead of double for Price to avoid floating-point precision issues
// TODO: Validate that bid prices are < ask prices when both sides are present in order book

/// <summary>
/// One price level in an order book side. Level is 0-based (0 = best).
/// </summary>
public sealed record OrderBookLevel(
    OrderBookSide Side,
    int Level,
    double Price,
    double Size,
    string? MarketMaker = null)
{
    /// <summary>
    /// Validates order book level data at construction time to prevent corrupt datasets.
    /// </summary>
    public OrderBookLevel
    {
        if (Price <= 0)
            throw new ArgumentOutOfRangeException(nameof(Price), Price, "Price must be greater than 0");

        if (Size < 0)
            throw new ArgumentOutOfRangeException(nameof(Size), Size, "Size must be greater than or equal to 0");

        if (Level < 0)
            throw new ArgumentOutOfRangeException(nameof(Level), Level, "Level must be greater than or equal to 0");
    }
}
