using MarketDataCollector.Domain.Models;

namespace MarketDataCollector.Domain.Events;

public sealed record MarketEvent(
    DateTimeOffset Timestamp,
    string Symbol,
    MarketEventType Type,
    MarketEventPayload? Payload,
    long Sequence = 0,
    string Source = "IB",
    int SchemaVersion = 1,
    MarketEventTier Tier = MarketEventTier.Raw
)
{
    public static MarketEvent Trade(DateTimeOffset ts, string symbol, Trade trade, long seq = 0, string source = "IB")
        => new(ts, symbol, MarketEventType.Trade, trade, seq == 0 ? trade.SequenceNumber : seq, source);

    public static MarketEvent L2Snapshot(DateTimeOffset ts, string symbol, LOBSnapshot snap, long seq = 0, string source = "IB")
        => new(ts, symbol, MarketEventType.L2Snapshot, snap, seq == 0 ? snap.SequenceNumber : seq, source);

    public static MarketEvent BboQuote(DateTimeOffset ts, string symbol, BboQuotePayload quote, long seq = 0, string source = "ALPACA")
        => new(ts, symbol, MarketEventType.BboQuote, quote, seq == 0 ? quote.SequenceNumber : seq, source);

    public static MarketEvent L2SnapshotPayload(DateTimeOffset ts, string symbol, L2SnapshotPayload payload, long seq = 0, string source = "IB")
        => new(ts, symbol, MarketEventType.L2Snapshot, payload, seq == 0 ? payload.SequenceNumber : seq, source);

    public static MarketEvent OrderFlow(DateTimeOffset ts, string symbol, OrderFlowStatistics stats, long seq = 0, string source = "IB")
        => new(ts, symbol, MarketEventType.OrderFlow, stats, seq == 0 ? stats.SequenceNumber : seq, source);

    public static MarketEvent Integrity(DateTimeOffset ts, string symbol, IntegrityEvent integrity, long seq = 0, string source = "IB")
        => new(ts, symbol, MarketEventType.Integrity, integrity, seq == 0 ? integrity.SequenceNumber : seq, source);

    public static MarketEvent DepthIntegrity(DateTimeOffset ts, string symbol, DepthIntegrityEvent integrity, long seq = 0, string source = "IB")
        => new(ts, symbol, MarketEventType.Integrity, integrity, seq == 0 ? integrity.SequenceNumber : seq, source);

    public static MarketEvent Heartbeat(DateTimeOffset ts, string source = "IB")
        => new(ts, "SYSTEM", MarketEventType.Heartbeat, Payload: null, Sequence: 0, Source: source);

    public static MarketEvent HistoricalBar(DateTimeOffset ts, string symbol, HistoricalBar bar, long seq = 0, string source = "stooq")
        => new(ts, symbol, MarketEventType.HistoricalBar, bar, seq == 0 ? bar.SequenceNumber : seq, source);
}
