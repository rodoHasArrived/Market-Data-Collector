namespace IBDataCollector.Domain.Models;

/// <summary>
/// One price level in an order book side. Level is 0-based (0 = best).
/// </summary>
public sealed record OrderBookLevel(
    OrderBookSide Side,
    int Level,
    double Price,
    double Size,
    string? MarketMaker = null);
