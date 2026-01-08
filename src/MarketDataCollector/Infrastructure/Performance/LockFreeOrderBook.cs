using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using MarketDataCollector.Domain.Events;
using MarketDataCollector.Domain.Models;

namespace MarketDataCollector.Infrastructure.Performance;

/// <summary>
/// Lock-free order book implementation using immutable snapshots.
/// Provides wait-free reads and non-blocking writes via atomic snapshot swapping.
/// This design ensures writers never block readers and vice versa.
/// </summary>
public sealed class LockFreeOrderBook
{
    private volatile OrderBookSnapshot _currentSnapshot;
    private long _sequenceCounter;

    public LockFreeOrderBook(string symbol)
    {
        Symbol = symbol ?? throw new ArgumentNullException(nameof(symbol));
        _currentSnapshot = new OrderBookSnapshot(
            Symbol: symbol,
            Bids: ImmutableArray<OrderBookLevel>.Empty,
            Asks: ImmutableArray<OrderBookLevel>.Empty,
            SequenceNumber: 0,
            Timestamp: DateTimeOffset.UtcNow,
            IsStale: false
        );
    }

    public string Symbol { get; }

    /// <summary>
    /// Gets the current order book snapshot (wait-free read).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public OrderBookSnapshot GetSnapshot() => _currentSnapshot;

    /// <summary>
    /// Indicates if the order book is in a stale state due to integrity errors.
    /// </summary>
    public bool IsStale => _currentSnapshot.IsStale;

    /// <summary>
    /// Gets the current sequence number.
    /// </summary>
    public long SequenceNumber => Interlocked.Read(ref _sequenceCounter);

    /// <summary>
    /// Applies a depth update and atomically publishes a new snapshot.
    /// Returns the result of the operation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public OrderBookUpdateResult ApplyUpdate(MarketDepthUpdate update)
    {
        if (update is null)
            return OrderBookUpdateResult.Failed(DepthIntegrityKind.Unknown, "Null update");

        // Spin until we successfully apply the update
        // This is lock-free but not wait-free for writers
        SpinWait spinner = default;

        while (true)
        {
            var current = _currentSnapshot;

            // Check if stale
            if (current.IsStale)
            {
                return OrderBookUpdateResult.Failed(
                    DepthIntegrityKind.Stale,
                    "Order book is stale. Reset required.");
            }

            // Apply the update to get a new snapshot
            var result = ApplyUpdateToSnapshot(current, update);

            if (!result.Success)
                return result;

            // Atomically swap the snapshot
            var previous = Interlocked.CompareExchange(ref _currentSnapshot, result.NewSnapshot!, current);

            if (ReferenceEquals(previous, current))
            {
                // Successfully applied
                Interlocked.Increment(ref _sequenceCounter);
                return result;
            }

            // Another thread updated - retry
            spinner.SpinOnce();
        }
    }

    /// <summary>
    /// Resets the order book to an empty, non-stale state.
    /// </summary>
    public void Reset()
    {
        var newSnapshot = new OrderBookSnapshot(
            Symbol: Symbol,
            Bids: ImmutableArray<OrderBookLevel>.Empty,
            Asks: ImmutableArray<OrderBookLevel>.Empty,
            SequenceNumber: 0,
            Timestamp: DateTimeOffset.UtcNow,
            IsStale: false
        );

        Interlocked.Exchange(ref _currentSnapshot, newSnapshot);
        Interlocked.Exchange(ref _sequenceCounter, 0);
    }

    /// <summary>
    /// Marks the order book as stale (e.g., after an integrity error).
    /// </summary>
    public void MarkStale(string? reason = null)
    {
        var current = _currentSnapshot;
        var staleSnapshot = current with
        {
            IsStale = true,
            StaleReason = reason ?? "Marked stale"
        };

        Interlocked.Exchange(ref _currentSnapshot, staleSnapshot);
    }

    private OrderBookUpdateResult ApplyUpdateToSnapshot(OrderBookSnapshot current, MarketDepthUpdate update)
    {
        var sideArray = update.Side == OrderBookSide.Bid ? current.Bids : current.Asks;
        ImmutableArray<OrderBookLevel> newSideArray;

        switch (update.Operation)
        {
            case DepthOperation.Insert:
                if (update.Position < 0 || update.Position > sideArray.Length)
                {
                    return OrderBookUpdateResult.Failed(
                        DepthIntegrityKind.Gap,
                        $"Insert position {update.Position} out of range (count={sideArray.Length})");
                }

                var newLevel = new OrderBookLevel(
                    update.Side,
                    update.Position,
                    update.Price,
                    update.Size,
                    update.MarketMaker);

                newSideArray = sideArray.Insert(update.Position, newLevel);
                newSideArray = ReindexLevels(newSideArray, update.Side);
                break;

            case DepthOperation.Update:
                if (update.Position < 0 || update.Position >= sideArray.Length)
                {
                    return OrderBookUpdateResult.Failed(
                        DepthIntegrityKind.OutOfOrder,
                        $"Update position {update.Position} missing (count={sideArray.Length})");
                }

                var existingLevel = sideArray[update.Position];
                var updatedLevel = existingLevel with
                {
                    Price = update.Price,
                    Size = update.Size,
                    MarketMaker = update.MarketMaker
                };

                newSideArray = sideArray.SetItem(update.Position, updatedLevel);
                break;

            case DepthOperation.Delete:
                if (update.Position < 0 || update.Position >= sideArray.Length)
                {
                    return OrderBookUpdateResult.Failed(
                        DepthIntegrityKind.InvalidPosition,
                        $"Delete position {update.Position} missing (count={sideArray.Length})");
                }

                newSideArray = sideArray.RemoveAt(update.Position);
                newSideArray = ReindexLevels(newSideArray, update.Side);
                break;

            default:
                return OrderBookUpdateResult.Failed(
                    DepthIntegrityKind.Unknown,
                    $"Unknown operation: {update.Operation}");
        }

        // Build new snapshot with computed metrics
        var newBids = update.Side == OrderBookSide.Bid ? newSideArray : current.Bids;
        var newAsks = update.Side == OrderBookSide.Ask ? newSideArray : current.Asks;

        var newSnapshot = BuildSnapshotWithMetrics(newBids, newAsks, update.Timestamp, update.SequenceNumber);

        return OrderBookUpdateResult.Succeeded(newSnapshot);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ImmutableArray<OrderBookLevel> ReindexLevels(ImmutableArray<OrderBookLevel> levels, OrderBookSide side)
    {
        if (levels.Length == 0)
            return levels;

        var builder = ImmutableArray.CreateBuilder<OrderBookLevel>(levels.Length);
        for (int i = 0; i < levels.Length; i++)
        {
            builder.Add(levels[i] with { Side = side, Level = i });
        }
        return builder.MoveToImmutable();
    }

    private OrderBookSnapshot BuildSnapshotWithMetrics(
        ImmutableArray<OrderBookLevel> bids,
        ImmutableArray<OrderBookLevel> asks,
        DateTimeOffset timestamp,
        long sequenceNumber)
    {
        double? midPrice = null;
        double? microPrice = null;
        double? imbalance = null;
        double? spread = null;

        if (bids.Length > 0 && asks.Length > 0)
        {
            var bestBid = bids[0];
            var bestAsk = asks[0];

            midPrice = (double)(bestBid.Price + bestAsk.Price) / 2.0;
            spread = (double)(bestAsk.Price - bestBid.Price);

            // Micro-price: Volume-weighted midpoint
            var totalSize = bestBid.Size + bestAsk.Size;
            if (totalSize > 0)
            {
                microPrice = (double)(bestBid.Price * bestAsk.Size + bestAsk.Price * bestBid.Size) / (double)totalSize;
                imbalance = (double)(bestBid.Size - bestAsk.Size) / (double)totalSize;
            }
        }

        return new OrderBookSnapshot(
            Symbol: Symbol,
            Bids: bids,
            Asks: asks,
            SequenceNumber: sequenceNumber != 0 ? sequenceNumber : Interlocked.Read(ref _sequenceCounter) + 1,
            Timestamp: timestamp,
            IsStale: false,
            MidPrice: midPrice,
            MicroPrice: microPrice,
            Imbalance: imbalance,
            Spread: spread
        );
    }
}

/// <summary>
/// Immutable order book snapshot for lock-free reads.
/// </summary>
public sealed record OrderBookSnapshot(
    string Symbol,
    ImmutableArray<OrderBookLevel> Bids,
    ImmutableArray<OrderBookLevel> Asks,
    long SequenceNumber,
    DateTimeOffset Timestamp,
    bool IsStale,
    double? MidPrice = null,
    double? MicroPrice = null,
    double? Imbalance = null,
    double? Spread = null,
    string? StaleReason = null
)
{
    /// <summary>
    /// Converts this snapshot to the domain LOBSnapshot type.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public LOBSnapshot ToLOBSnapshot(string? streamId = null, string? venue = null)
    {
        return new LOBSnapshot(
            Timestamp: Timestamp,
            Symbol: Symbol,
            Bids: Bids,
            Asks: Asks,
            MidPrice: (decimal?)MidPrice,
            MicroPrice: (decimal?)MicroPrice,
            Imbalance: (decimal?)Imbalance,
            MarketState: IsStale ? MarketState.Unknown : MarketState.Normal,
            SequenceNumber: SequenceNumber,
            StreamId: streamId,
            Venue: venue
        );
    }
}

/// <summary>
/// Result of an order book update operation.
/// </summary>
public readonly struct OrderBookUpdateResult
{
    public bool Success { get; }
    public OrderBookSnapshot? NewSnapshot { get; }
    public DepthIntegrityKind IntegrityKind { get; }
    public string? ErrorDescription { get; }

    private OrderBookUpdateResult(bool success, OrderBookSnapshot? snapshot, DepthIntegrityKind kind, string? error)
    {
        Success = success;
        NewSnapshot = snapshot;
        IntegrityKind = kind;
        ErrorDescription = error;
    }

    public static OrderBookUpdateResult Succeeded(OrderBookSnapshot snapshot)
        => new(true, snapshot, DepthIntegrityKind.Unknown, null);

    public static OrderBookUpdateResult Failed(DepthIntegrityKind kind, string description)
        => new(false, null, kind, description);
}

/// <summary>
/// Thread-safe collection of lock-free order books keyed by symbol.
/// </summary>
public sealed class LockFreeOrderBookCollection
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, LockFreeOrderBook> _books
        = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets or creates an order book for the specified symbol.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public LockFreeOrderBook GetOrCreate(string symbol)
    {
        return _books.GetOrAdd(symbol, s => new LockFreeOrderBook(s));
    }

    /// <summary>
    /// Tries to get an existing order book for the specified symbol.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGet(string symbol, out LockFreeOrderBook? book)
    {
        return _books.TryGetValue(symbol, out book);
    }

    /// <summary>
    /// Gets a snapshot of all symbols currently tracked.
    /// </summary>
    public IReadOnlyCollection<string> GetSymbols() => _books.Keys.ToList();

    /// <summary>
    /// Removes an order book for the specified symbol.
    /// </summary>
    public bool Remove(string symbol) => _books.TryRemove(symbol, out _);

    /// <summary>
    /// Resets all order books to empty state.
    /// </summary>
    public void ResetAll()
    {
        foreach (var book in _books.Values)
        {
            book.Reset();
        }
    }

    /// <summary>
    /// Gets statistics for all order books.
    /// </summary>
    public IEnumerable<(string Symbol, OrderBookSnapshot Snapshot)> GetAllSnapshots()
    {
        foreach (var kvp in _books)
        {
            yield return (kvp.Key, kvp.Value.GetSnapshot());
        }
    }
}
