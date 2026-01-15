namespace MarketDataCollector.Contracts.Domain.Enums;

/// <summary>
/// Type of market event.
/// </summary>
public enum MarketEventType
{
    /// <summary>
    /// Unknown event type.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Level 2 order book snapshot.
    /// </summary>
    L2Snapshot = 1,

    /// <summary>
    /// Best bid/offer quote.
    /// </summary>
    BboQuote = 2,

    /// <summary>
    /// Trade execution.
    /// </summary>
    Trade = 3,

    /// <summary>
    /// Order flow statistics.
    /// </summary>
    OrderFlow = 4,

    /// <summary>
    /// Heartbeat message.
    /// </summary>
    Heartbeat = 5,

    /// <summary>
    /// Connection status change.
    /// </summary>
    ConnectionStatus = 6,

    /// <summary>
    /// Data integrity event.
    /// </summary>
    Integrity = 7,

    /// <summary>
    /// Historical bar data.
    /// </summary>
    HistoricalBar = 8,

    /// <summary>
    /// Historical quote data.
    /// </summary>
    HistoricalQuote = 9,

    /// <summary>
    /// Historical trade data.
    /// </summary>
    HistoricalTrade = 10,

    /// <summary>
    /// Historical auction data.
    /// </summary>
    HistoricalAuction = 11,

    /// <summary>
    /// Real-time aggregate bar (OHLCV) from streaming providers.
    /// </summary>
    AggregateBar = 12
}
