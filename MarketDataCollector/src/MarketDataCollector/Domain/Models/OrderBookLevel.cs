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
/// <b>Price type:</b> Uses decimal for financial precision. This ensures accurate representation
/// of prices without floating-point rounding errors, which is critical for financial calculations.
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
public sealed record OrderBookLevel
{
    public OrderBookSide Side { get; init; }
    public int Level { get; init; }
    public decimal Price { get; init; }
    public decimal Size { get; init; }
    public string? MarketMaker { get; init; }

    /// <summary>
    /// Validates order book level data at construction time to prevent corrupt datasets.
    /// </summary>
    public OrderBookLevel(
        OrderBookSide Side,
        int Level,
        decimal Price,
        decimal Size,
        string? MarketMaker = null)
    {
        if (Price <= 0)
            throw new ArgumentOutOfRangeException(nameof(Price), Price, "Price must be greater than 0");

        if (Size < 0)
            throw new ArgumentOutOfRangeException(nameof(Size), Size, "Size must be greater than or equal to 0");

        if (Level < 0)
            throw new ArgumentOutOfRangeException(nameof(Level), Level, "Level must be greater than or equal to 0");

        this.Side = Side;
        this.Level = Level;
        this.Price = Price;
        this.Size = Size;
        this.MarketMaker = MarketMaker;
    }
}
