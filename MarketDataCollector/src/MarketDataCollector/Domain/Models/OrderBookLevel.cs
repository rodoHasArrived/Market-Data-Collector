namespace MarketDataCollector.Domain.Models;

// TODO: Add validation for Price (must be > 0) and Size (must be >= 0)
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
    string? MarketMaker = null);
