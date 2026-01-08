using System.Collections.Concurrent;
using DataIngestion.Contracts.Messages;

namespace DataIngestion.OrderBookService.Models;

/// <summary>
/// Managed order book level for internal processing.
/// </summary>
public record ManagedOrderBookLevel(decimal Price, long Size, string? MarketMaker = null);

/// <summary>
/// Managed order book with thread-safe state.
/// </summary>
public sealed class ManagedOrderBook
{
    public string Symbol { get; }
    public object SyncLock { get; } = new();

    public ConcurrentDictionary<decimal, ManagedOrderBookLevel> Bids { get; } = new();
    public ConcurrentDictionary<decimal, ManagedOrderBookLevel> Asks { get; } = new();

    public long LastSequence { get; set; }
    public DateTimeOffset LastUpdateTime { get; set; }
    public bool IsFrozen { get; set; }
    public bool HasChanged { get; set; }
    public long SnapshotCount { get; set; }
    public long UpdateCount { get; set; }

    public decimal? BestBid => Bids.Count > 0 ? Bids.Keys.Max() : null;
    public decimal? BestAsk => Asks.Count > 0 ? Asks.Keys.Min() : null;
    public decimal? Spread => BestBid.HasValue && BestAsk.HasValue ? BestAsk - BestBid : null;
    public decimal? MidPrice => BestBid.HasValue && BestAsk.HasValue ? (BestBid + BestAsk) / 2 : null;

    public ManagedOrderBook(string symbol, int maxDepth = 50)
    {
        Symbol = symbol;
    }
}

/// <summary>
/// Order book snapshot for processing.
/// </summary>
public record OrderBookSnapshot(
    string Symbol,
    DateTimeOffset Timestamp,
    long Sequence,
    IReadOnlyList<OrderBookLevel> Bids,
    IReadOnlyList<OrderBookLevel> Asks
);

/// <summary>
/// Order book update for processing.
/// </summary>
public record OrderBookUpdate(
    string Symbol,
    DateTimeOffset Timestamp,
    long Sequence,
    OrderBookUpdateType UpdateType,
    OrderBookSide Side,
    int Position,
    decimal? Price,
    long? Size,
    string? MarketMaker
);
