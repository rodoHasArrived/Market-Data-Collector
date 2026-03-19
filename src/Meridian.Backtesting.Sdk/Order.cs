namespace Meridian.Backtesting.Sdk;

/// <summary>Order type classification.</summary>
public enum OrderType { Market, Limit, StopMarket }

/// <summary>Lifecycle state of a simulated order.</summary>
public enum OrderStatus { Pending, PartiallyFilled, Filled, Cancelled, Rejected }

/// <summary>Immutable order record submitted to the backtest context.</summary>
public sealed record Order(
    Guid OrderId,
    string Symbol,
    OrderType Type,
    long Quantity,          // positive = buy; negative = sell / short
    decimal? LimitPrice,
    decimal? StopPrice,
    DateTimeOffset SubmittedAt,
    OrderStatus Status = OrderStatus.Pending);
