namespace MarketDataCollector.Domain.Events;

public enum MarketEventType
{
    Unknown = 0,
    L2Snapshot = 1,
    BboQuote = 2,
    Trade = 3,
    OrderFlow = 4,
    Heartbeat = 5,
    ConnectionStatus = 6,
    Integrity = 7,
    HistoricalBar = 8
}
