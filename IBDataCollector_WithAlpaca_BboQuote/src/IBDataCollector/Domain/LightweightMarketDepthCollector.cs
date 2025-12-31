using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;

namespace IbDataCollector.Domain
{
    public enum MktDepthSide { Bid = 0, Ask = 1 }
    public enum MktDepthOperation { Insert = 0, Update = 1, Delete = 2 }

    /// <summary>
    /// Value-type update: avoids allocations per tick compared to class/record.
    /// </summary>
    public readonly record struct MktDepthUpdate(
        string Symbol,
        MktDepthSide Side,
        int Position,
        decimal Price,
        long Size,
        MktDepthOperation Operation,
        DateTimeOffset Timestamp
    );

    /// <summary>
    /// Value-type level: cheaper than record class.
    /// </summary>
    public readonly record struct LOBLevel(decimal Price, long Size);

    /// <summary>
    /// Strictly immutable snapshot: ImmutableArray prevents accidental mutation by consumers.
    /// </summary>
    public sealed record LOBSnapshot(
        string Symbol,
        DateTimeOffset Timestamp,
        ImmutableArray<LOBLevel> Bids,
        ImmutableArray<LOBLevel> Asks,
        decimal? BestBid,
        decimal? BestAsk,
        decimal? MidPrice,
        decimal? Spread,
        int Depth
    );

    public sealed record BookSubscription(string Symbol, int RequestedDepth, bool IsActive);

    /// <summary>
    /// Clock abstraction => deterministic tests (throttle behavior).
    /// </summary>
    public interface IClock
    {
        DateTimeOffset UtcNow { get; }
    }

    public sealed class SystemClock : IClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }

    public interface IOrderBookCalculator
    {
        LOBSnapshot CreateSnapshot(
            string symbol,
            in BookArrays book,
            DateTimeOffset timestamp
        );
    }

    /// <summary>
    /// Raw book view passed to calculator without allocations.
    /// Arrays are owned by the collector; snapshot creation must copy.
    /// </summary>
    public readonly struct BookArrays
    {
        public readonly LOBLevel[] Bids;
        public readonly int BidCount;
        public readonly LOBLevel[] Asks;
        public readonly int AskCount;

        public BookArrays(LOBLevel[] bids, int bidCount, LOBLevel[] asks, int askCount)
        {
            Bids = bids;
            BidCount = bidCount;
            Asks = asks;
            AskCount = askCount;
        }
    }

    public sealed class DefaultOrderBookCalculator : IOrderBookCalculator
    {
        public LOBSnapshot CreateSnapshot(string symbol, in BookArrays book, DateTimeOffset timestamp)
        {
            if (string.IsNullOrWhiteSpace(symbol))
                throw new ArgumentException("Symbol must be non-empty.", nameof(symbol));

            decimal? bestBid = book.BidCount > 0 ? book.Bids[0].Price : null;
            decimal? bestAsk = book.AskCount > 0 ? book.Asks[0].Price : null;

            decimal? mid = (bestBid.HasValue && bestAsk.HasValue)
                ? (bestBid.Value + bestAsk.Value) / 2m
                : null;

            decimal? spread = (bestBid.HasValue && bestAsk.HasValue)
                ? (bestAsk.Value - bestBid.Value)
                : null;

            int depth = book.BidCount > book.AskCount ? book.BidCount : book.AskCount;

            // Copy exactly the live portion => strict immutability and minimal copy size.
            var bidsCopy = book.BidCount == 0
                ? ImmutableArray<LOBLevel>.Empty
                : CopyToImmutable(book.Bids, book.BidCount);

            var asksCopy = book.AskCount == 0
                ? ImmutableArray<LOBLevel>.Empty
                : CopyToImmutable(book.Asks, book.AskCount);

            return new LOBSnapshot(
                Symbol: symbol,
                Timestamp: timestamp,
                Bids: bidsCopy,
                Asks: asksCopy,
                BestBid: bestBid,
                BestAsk: bestAsk,
                MidPrice: mid,
                Spread: spread,
                Depth: depth
            );
        }

        private static ImmutableArray<LOBLevel> CopyToImmutable(LOBLevel[] src, int count)
        {
            var tmp = new LOBLevel[count];
            Array.Copy(src, 0, tmp, 0, count);
            return ImmutableArray.Create(tmp);
        }
    }

    public sealed record MarketDepthCollectorOptions(
        bool RequireExplicitSubscription = false,
        int DefaultDepth = 10,

        /// <summary>
        /// If false, no snapshots are created on updates; you can query snapshots via GetSnapshot().
        /// Big perf/GC win for high-frequency feeds.
        /// </summary>
        bool EmitSnapshotOnUpdate = true,

        /// <summary>
        /// Optional throttle: emit at most 1 snapshot per symbol per interval.
        /// </summary>
        TimeSpan? SnapshotThrottle = null
    );

    internal sealed class SymbolOrderBook
    {
        private readonly object _sync = new();

        private LOBLevel[] _bids;
        private int _bidCount;

        private LOBLevel[] _asks;
        private int _askCount;

        public SymbolOrderBook(int capacity)
        {
            if (capacity <= 0) capacity = 1;
            _bids = new LOBLevel[capacity];
            _asks = new LOBLevel[capacity];
            _bidCount = 0;
            _askCount = 0;
        }

        public void Clear()
        {
            lock (_sync)
            {
                _bidCount = 0;
                _askCount = 0;
            }
        }

        public int GetDepth()
        {
            lock (_sync)
            {
                return _bidCount > _askCount ? _bidCount : _askCount;
            }
        }

        public BookArrays SnapshotArrays()
        {
            lock (_sync)
            {
                return new BookArrays(_bids, _bidCount, _asks, _askCount);
            }
        }

        public void Apply(in MktDepthUpdate update, int depthLimit)
        {
            if (depthLimit <= 0) return;
            if (update.Position < 0 || update.Position >= depthLimit) return;

            lock (_sync)
            {
                ref var arr = ref (update.Side == MktDepthSide.Bid ? ref _bids : ref _asks);
                ref var count = ref (update.Side == MktDepthSide.Bid ? ref _bidCount : ref _askCount);

                switch (update.Operation)
                {
                    case MktDepthOperation.Insert:
                        Insert(ref arr, ref count, update.Position, new LOBLevel(update.Price, update.Size), depthLimit);
                        break;

                    case MktDepthOperation.Update:
                        if ((uint)update.Position < (uint)count)
                            arr[update.Position] = new LOBLevel(update.Price, update.Size);
                        break;

                    case MktDepthOperation.Delete:
                        if ((uint)update.Position < (uint)count)
                            Delete(ref arr, ref count, update.Position);
                        break;

                    default:
                        throw new ArgumentOutOfRangeException(nameof(update.Operation), update.Operation, "Unknown depth operation.");
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void EnsureCapacity(ref LOBLevel[] arr, int needed)
        {
            if (arr.Length >= needed) return;
            int newCap = arr.Length * 2;
            if (newCap < needed) newCap = needed;
            Array.Resize(ref arr, newCap);
        }

        private static void Insert(ref LOBLevel[] arr, ref int count, int pos, LOBLevel level, int depthLimit)
        {
            // Clamp insert point to [0, min(count, depthLimit)]
            if (pos < 0) pos = 0;
            if (pos > count) pos = count;
            if (pos > depthLimit) pos = depthLimit;

            // If already at limit and inserting at/after limit, ignore (would be dropped anyway).
            if (count >= depthLimit && pos >= depthLimit)
                return;

            // Ensure we can temporarily grow by 1 to perform the shift.
            EnsureCapacity(ref arr, Math.Min(depthLimit, count + 1));

            int newCount = count + 1;
            if (newCount > depthLimit) newCount = depthLimit;

            // Shift right from pos to end-1 (within depthLimit window)
            int lastIndexToShift = newCount - 2; // because we'll write level at pos
            if (lastIndexToShift >= pos)
            {
                Array.Copy(arr, pos, arr, pos + 1, lastIndexToShift - pos + 1);
            }

            arr[pos] = level;
            count = newCount;
        }

        private static void Delete(ref LOBLevel[] arr, ref int count, int pos)
        {
            int moveCount = count - pos - 1;
            if (moveCount > 0)
            {
                Array.Copy(arr, pos + 1, arr, pos, moveCount);
            }
            count--;
        }
    }

    public sealed class MarketDepthCollector
    {
        private readonly ConcurrentDictionary<string, SymbolOrderBook> _books =
            new(StringComparer.OrdinalIgnoreCase);

        private readonly ConcurrentDictionary<string, BookSubscription> _subs =
            new(StringComparer.OrdinalIgnoreCase);

        private readonly ConcurrentDictionary<string, DateTimeOffset> _lastEmit =
            new(StringComparer.OrdinalIgnoreCase);

        private readonly IOrderBookCalculator _calculator;
        private readonly IClock _clock;
        private readonly MarketDepthCollectorOptions _opt;

        public event Action<LOBSnapshot>? SnapshotCreated;

        public MarketDepthCollector(
            IOrderBookCalculator? calculator = null,
            IClock? clock = null,
            MarketDepthCollectorOptions? options = null)
        {
            _calculator = calculator ?? new DefaultOrderBookCalculator();
            _clock = clock ?? new SystemClock();
            _opt = options ?? new MarketDepthCollectorOptions();

            if (_opt.DefaultDepth <= 0)
                throw new ArgumentOutOfRangeException(nameof(_opt.DefaultDepth), "DefaultDepth must be positive.");
        }

        // ─────────────────────────────
        // Simplified API
        // ─────────────────────────────

        public BookSubscription Subscribe(string symbol, int depth)
        {
            if (string.IsNullOrWhiteSpace(symbol))
                throw new ArgumentException("Symbol must be non-empty.", nameof(symbol));
            if (depth <= 0)
                throw new ArgumentOutOfRangeException(nameof(depth), "Depth must be positive.");

            symbol = symbol.Trim();

            var sub = _subs.AddOrUpdate(
                symbol,
                s => new BookSubscription(s, depth, IsActive: true),
                (s, existing) => existing with { RequestedDepth = depth, IsActive = true });

            _books.GetOrAdd(symbol, _ => new SymbolOrderBook(capacity: depth));
            return sub;
        }

        public bool Unsubscribe(string symbol)
        {
            if (symbol is null) throw new ArgumentNullException(nameof(symbol));
            symbol = symbol.Trim();

            if (_subs.TryGetValue(symbol, out var existing))
                _subs[symbol] = existing with { IsActive = false };

            _lastEmit.TryRemove(symbol, out _);

            if (_books.TryRemove(symbol, out var book))
            {
                book.Clear();
                return true;
            }

            return false;
        }

        public bool IsSubscribed(string symbol)
        {
            if (symbol is null) throw new ArgumentNullException(nameof(symbol));
            symbol = symbol.Trim();
            return _subs.TryGetValue(symbol, out var s) && s.IsActive;
        }

        public IReadOnlyCollection<BookSubscription> GetSubscriptions()
            => _subs.Values.ToImmutableArray();

        public IReadOnlyCollection<string> GetTrackedSymbols()
            => _books.Keys.ToImmutableArray();

        /// <summary>
        /// Hot-path: apply update. Returns emitted snapshot (if any).
        /// If EmitSnapshotOnUpdate=false or throttled, returns null.
        /// </summary>
        public LOBSnapshot? OnDepthUpdate(in MktDepthUpdate update)
        {
            if (string.IsNullOrWhiteSpace(update.Symbol))
                throw new ArgumentException("Symbol must be non-empty.", nameof(update));

            var symbol = update.Symbol.Trim();

            int depthLimit;
            if (_opt.RequireExplicitSubscription)
            {
                if (!_subs.TryGetValue(symbol, out var sub) || !sub.IsActive)
                    return null;

                depthLimit = sub.RequestedDepth;
            }
            else
            {
                var sub = _subs.GetOrAdd(symbol, s => new BookSubscription(s, _opt.DefaultDepth, IsActive: true));
                depthLimit = sub.RequestedDepth;
                _books.GetOrAdd(symbol, _ => new SymbolOrderBook(capacity: depthLimit));
            }

            var book = _books.GetOrAdd(symbol, _ => new SymbolOrderBook(capacity: depthLimit));
            book.Apply(update, depthLimit);

            if (!_opt.EmitSnapshotOnUpdate)
                return null;

            if (_opt.SnapshotThrottle is { } interval && interval > TimeSpan.Zero)
            {
                var now = _clock.UtcNow;
                if (_lastEmit.TryGetValue(symbol, out var last) && (now - last) < interval)
                    return null;

                _lastEmit[symbol] = now;
            }

            var arrays = book.SnapshotArrays();
            var snap = _calculator.CreateSnapshot(symbol, in arrays, update.Timestamp);

            SnapshotCreated?.Invoke(snap);
            return snap;
        }

        public LOBSnapshot? GetSnapshot(string symbol, DateTimeOffset? timestamp = null)
        {
            if (symbol is null) throw new ArgumentNullException(nameof(symbol));
            symbol = symbol.Trim();

            if (!_books.TryGetValue(symbol, out var book))
                return null;

            var arrays = book.SnapshotArrays();
            return _calculator.CreateSnapshot(symbol, in arrays, timestamp ?? _clock.UtcNow);
        }

        public int GetBookDepth(string symbol)
        {
            if (symbol is null) throw new ArgumentNullException(nameof(symbol));
            symbol = symbol.Trim();
            return _books.TryGetValue(symbol, out var book) ? book.GetDepth() : 0;
        }
    }
}
