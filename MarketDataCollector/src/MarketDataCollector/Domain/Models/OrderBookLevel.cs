namespace MarketDataCollector.Domain.Models;

/// <summary>
/// One price level in an order book side. Level is 0-based (0 = best).
/// </summary>
/// <remarks>
/// <para>
/// <b>Design Notes:</b>
/// </para>
/// <list type="bullet">
/// <item>
/// <description>
/// <b>Price type (double vs decimal):</b> Uses double for performance in high-frequency scenarios.
/// For sub-cent precision requirements (e.g., forex), consider migrating to decimal.
/// </description>
/// </item>
/// <item>
/// <description>
/// <b>Cross-side validation:</b> Bid/ask price validation (bid &lt; ask) is performed at the
/// order book level in <see cref="MarketDataCollector.Infrastructure.OrderBook.OrderBookMatchingEngine"/>
/// rather than at the individual level, since single levels don't have visibility into the opposite side.
/// </description>
/// </item>
/// </list>
/// </remarks>
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
