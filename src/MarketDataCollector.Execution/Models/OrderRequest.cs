namespace MarketDataCollector.Execution.Models;

/// <summary>
/// Describes a request to submit an order to a broker or simulator.
/// Use the static factory methods to construct common order types.
/// </summary>
public sealed record OrderRequest
{
    /// <summary>Client-assigned unique identifier for this request.</summary>
    public string ClientOrderId { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>Ticker symbol (e.g., "AAPL", "SPY").</summary>
    public required string Symbol { get; init; }

    /// <summary>Signed quantity: positive = buy, negative = sell.</summary>
    public required long Quantity { get; init; }

    /// <summary>The order type (Market, Limit, etc.).</summary>
    public required OrderType Type { get; init; }

    /// <summary>Limit price; required when <see cref="Type"/> is <see cref="OrderType.Limit"/>.</summary>
    public decimal? LimitPrice { get; init; }

    /// <summary>When the order was created.</summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Creates a market order for the given symbol and quantity.</summary>
    public static OrderRequest Market(string symbol, long quantity) =>
        new() { Symbol = symbol, Quantity = quantity, Type = OrderType.Market };

    /// <summary>Creates a limit order for the given symbol, quantity, and limit price.</summary>
    public static OrderRequest Limit(string symbol, long quantity, decimal limitPrice) =>
        new() { Symbol = symbol, Quantity = quantity, Type = OrderType.Limit, LimitPrice = limitPrice };
}
