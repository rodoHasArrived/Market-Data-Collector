using System.Text.Json.Serialization;

namespace MarketDataCollector.Infrastructure.Plugins.Core;

/// <summary>
/// Unified market data event that flows through the pipeline.
/// All plugins emit this type, regardless of whether they're real-time or historical.
/// </summary>
/// <remarks>
/// Design note: This is a discriminated union implemented via inheritance.
/// The EventType property indicates the concrete type for serialization.
/// Use pattern matching to handle specific event types.
///
/// Example:
/// <code>
/// await foreach (var evt in plugin.StreamAsync(request, ct))
/// {
///     var result = evt switch
///     {
///         TradeEvent trade => ProcessTrade(trade),
///         QuoteEvent quote => ProcessQuote(quote),
///         BarEvent bar => ProcessBar(bar),
///         _ => LogUnhandled(evt)
///     };
/// }
/// </code>
/// </remarks>
[JsonDerivedType(typeof(TradeEvent), typeDiscriminator: "trade")]
[JsonDerivedType(typeof(QuoteEvent), typeDiscriminator: "quote")]
[JsonDerivedType(typeof(DepthEvent), typeDiscriminator: "depth")]
[JsonDerivedType(typeof(BarEvent), typeDiscriminator: "bar")]
[JsonDerivedType(typeof(DividendEvent), typeDiscriminator: "dividend")]
[JsonDerivedType(typeof(SplitEvent), typeDiscriminator: "split")]
[JsonDerivedType(typeof(HeartbeatEvent), typeDiscriminator: "heartbeat")]
[JsonDerivedType(typeof(ErrorEvent), typeDiscriminator: "error")]
public abstract record MarketDataEvent
{
    /// <summary>
    /// Symbol this event applies to.
    /// </summary>
    public required string Symbol { get; init; }

    /// <summary>
    /// Timestamp of the event (from the source, not receipt time).
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Source plugin that emitted this event.
    /// </summary>
    public required string Source { get; init; }

    /// <summary>
    /// Event type discriminator for routing and filtering.
    /// </summary>
    public abstract DataType EventType { get; }

    /// <summary>
    /// Optional sequence number for ordering (provider-specific).
    /// </summary>
    public long? SequenceNumber { get; init; }
}

/// <summary>
/// Trade execution event (tick-by-tick).
/// </summary>
public sealed record TradeEvent : MarketDataEvent
{
    public override DataType EventType => DataType.Trade;

    /// <summary>
    /// Trade price.
    /// </summary>
    public required decimal Price { get; init; }

    /// <summary>
    /// Trade size/volume.
    /// </summary>
    public required decimal Size { get; init; }

    /// <summary>
    /// Exchange where the trade occurred.
    /// </summary>
    public string? Exchange { get; init; }

    /// <summary>
    /// Trade conditions (e.g., "regular", "odd_lot", "intermarket_sweep").
    /// </summary>
    public IReadOnlyList<string>? Conditions { get; init; }

    /// <summary>
    /// Trade ID from the exchange (if available).
    /// </summary>
    public string? TradeId { get; init; }
}

/// <summary>
/// Best bid/offer quote event.
/// </summary>
public sealed record QuoteEvent : MarketDataEvent
{
    public override DataType EventType => DataType.Quote;

    /// <summary>
    /// Best bid price.
    /// </summary>
    public required decimal BidPrice { get; init; }

    /// <summary>
    /// Best bid size.
    /// </summary>
    public required decimal BidSize { get; init; }

    /// <summary>
    /// Best ask price.
    /// </summary>
    public required decimal AskPrice { get; init; }

    /// <summary>
    /// Best ask size.
    /// </summary>
    public required decimal AskSize { get; init; }

    /// <summary>
    /// Bid exchange.
    /// </summary>
    public string? BidExchange { get; init; }

    /// <summary>
    /// Ask exchange.
    /// </summary>
    public string? AskExchange { get; init; }

    /// <summary>
    /// Mid price (convenience).
    /// </summary>
    public decimal MidPrice => (BidPrice + AskPrice) / 2;

    /// <summary>
    /// Spread in price terms.
    /// </summary>
    public decimal Spread => AskPrice - BidPrice;

    /// <summary>
    /// Spread in basis points.
    /// </summary>
    public decimal SpreadBps => MidPrice > 0 ? Spread / MidPrice * 10000 : 0;
}

/// <summary>
/// Order book depth event (L2/L3).
/// </summary>
public sealed record DepthEvent : MarketDataEvent
{
    public override DataType EventType => DataType.Depth;

    /// <summary>
    /// Bid levels (price, size, count).
    /// </summary>
    public required IReadOnlyList<DepthLevel> Bids { get; init; }

    /// <summary>
    /// Ask levels (price, size, count).
    /// </summary>
    public required IReadOnlyList<DepthLevel> Asks { get; init; }

    /// <summary>
    /// Whether this is a full snapshot or incremental update.
    /// </summary>
    public bool IsSnapshot { get; init; } = true;
}

/// <summary>
/// Single level in the order book.
/// </summary>
public sealed record DepthLevel(
    decimal Price,
    decimal Size,
    int? OrderCount = null,
    string? Exchange = null);

/// <summary>
/// OHLCV bar event (aggregate).
/// </summary>
public sealed record BarEvent : MarketDataEvent
{
    public override DataType EventType => DataType.Bar;

    /// <summary>
    /// Opening price.
    /// </summary>
    public required decimal Open { get; init; }

    /// <summary>
    /// High price.
    /// </summary>
    public required decimal High { get; init; }

    /// <summary>
    /// Low price.
    /// </summary>
    public required decimal Low { get; init; }

    /// <summary>
    /// Closing price.
    /// </summary>
    public required decimal Close { get; init; }

    /// <summary>
    /// Volume for the period.
    /// </summary>
    public required decimal Volume { get; init; }

    /// <summary>
    /// Volume-weighted average price (if available).
    /// </summary>
    public decimal? Vwap { get; init; }

    /// <summary>
    /// Number of trades in the period.
    /// </summary>
    public int? TradeCount { get; init; }

    /// <summary>
    /// Bar interval (e.g., "1min", "1day").
    /// </summary>
    public required string Interval { get; init; }

    /// <summary>
    /// Whether prices are adjusted for splits/dividends.
    /// </summary>
    public bool IsAdjusted { get; init; }
}

/// <summary>
/// Dividend event.
/// </summary>
public sealed record DividendEvent : MarketDataEvent
{
    public override DataType EventType => DataType.Dividend;

    /// <summary>
    /// Ex-dividend date.
    /// </summary>
    public required DateOnly ExDate { get; init; }

    /// <summary>
    /// Dividend amount per share.
    /// </summary>
    public required decimal Amount { get; init; }

    /// <summary>
    /// Payment date.
    /// </summary>
    public DateOnly? PayDate { get; init; }

    /// <summary>
    /// Record date.
    /// </summary>
    public DateOnly? RecordDate { get; init; }

    /// <summary>
    /// Declaration date.
    /// </summary>
    public DateOnly? DeclaredDate { get; init; }

    /// <summary>
    /// Dividend type (e.g., "cash", "stock", "special").
    /// </summary>
    public string? DividendType { get; init; }
}

/// <summary>
/// Stock split event.
/// </summary>
public sealed record SplitEvent : MarketDataEvent
{
    public override DataType EventType => DataType.Split;

    /// <summary>
    /// Effective date of the split.
    /// </summary>
    public required DateOnly EffectiveDate { get; init; }

    /// <summary>
    /// Split ratio (e.g., 4.0 for a 4:1 split, 0.5 for a 1:2 reverse split).
    /// </summary>
    public required decimal Ratio { get; init; }

    /// <summary>
    /// Whether this is a reverse split.
    /// </summary>
    public bool IsReverse => Ratio < 1;
}

/// <summary>
/// Heartbeat event for connection keep-alive.
/// </summary>
public sealed record HeartbeatEvent : MarketDataEvent
{
    public override DataType EventType => DataType.Trade; // Using Trade as placeholder

    /// <summary>
    /// Creates a heartbeat event.
    /// </summary>
    public static HeartbeatEvent Create(string source) => new()
    {
        Symbol = "_heartbeat_",
        Timestamp = DateTimeOffset.UtcNow,
        Source = source
    };
}

/// <summary>
/// Error event indicating a problem with a specific symbol or the connection.
/// </summary>
public sealed record ErrorEvent : MarketDataEvent
{
    public override DataType EventType => DataType.Trade; // Using Trade as placeholder

    /// <summary>
    /// Error code (provider-specific).
    /// </summary>
    public string? ErrorCode { get; init; }

    /// <summary>
    /// Error message.
    /// </summary>
    public required string ErrorMessage { get; init; }

    /// <summary>
    /// Whether this error is recoverable.
    /// </summary>
    public bool IsRecoverable { get; init; } = true;

    /// <summary>
    /// Suggested retry delay (if recoverable).
    /// </summary>
    public TimeSpan? RetryAfter { get; init; }
}
