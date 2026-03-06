using System.Collections.Concurrent;
using MarketDataCollector.Contracts.Domain.Enums;
using MarketDataCollector.Contracts.Domain.Models;
using MarketDataCollector.Domain.Events;
using MarketDataCollector.Domain.Models;

namespace MarketDataCollector.Domain.Collectors;

/// <summary>
/// Maintains per-symbol Level-2 order books from depth deltas and emits L2 snapshots + depth integrity events.
/// </summary>
public sealed class MarketDepthCollector : SymbolSubscriptionTracker
{
    private readonly IMarketEventPublisher _publisher;

    private readonly ConcurrentDictionary<string, SymbolOrderBookBuffer> _books = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentQueue<DepthIntegrityEvent> _recentIntegrity = new();

    private readonly int _maxDepth;

    public MarketDepthCollector(
        IMarketEventPublisher publisher,
        bool requireExplicitSubscription = true,
        int maxDepth = 200)
        : base(requireExplicitSubscription)
    {
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        _maxDepth = Math.Max(1, maxDepth);
    }

    public void ResetSymbolStream(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol)) return;
        if (_books.TryGetValue(symbol.Trim(), out var buf))
            buf.Reset();
    }

    public bool IsSymbolStreamStale(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol)) return false;
        return _books.TryGetValue(symbol.Trim(), out var buf) && buf.IsStale;
    }

    public IReadOnlyList<DepthIntegrityEvent> GetRecentIntegrityEvents(int max = 20)
    {
        var snapshot = _recentIntegrity.ToArray();
        return snapshot.Reverse().Take(max).ToArray();
    }

    /// <summary>
    /// Returns the current L2 order book snapshot for a symbol, or null if no book exists.
    /// Thread-safe: acquires a read lock on the internal buffer.
    /// </summary>
    public LOBSnapshot? GetCurrentSnapshot(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol)) return null;
        if (!_books.TryGetValue(symbol.Trim(), out var book)) return null;
        return book.GetSnapshot(symbol.Trim());
    }

    /// <summary>
    /// Returns all symbols that currently have order book data.
    /// </summary>
    public IReadOnlyList<string> GetTrackedSymbols()
        => _books.Keys.ToList();

    /// <summary>
    /// Apply a single depth delta update.
    /// </summary>
    public void OnDepth(MarketDepthUpdate update)
    {
        if (update is null) throw new ArgumentNullException(nameof(update));
        if (string.IsNullOrWhiteSpace(update.Symbol)) return;

        var symbol = update.Symbol.Trim();

        if (!ShouldProcessUpdate(symbol))
            return;

        var book = _books.GetOrAdd(symbol, _ => new SymbolOrderBookBuffer(_maxDepth));

        var result = book.Apply(update);

        if (result != DepthIntegrityKind.Ok)
        {
            var evt = new DepthIntegrityEvent(
                Timestamp: update.Timestamp,
                Symbol: symbol,
                Kind: result,
                Description: book.LastErrorDescription ?? $"Depth integrity: {result}",
                Position: update.Position,
                Operation: update.Operation,
                Side: update.Side,
                SequenceNumber: update.SequenceNumber,
                StreamId: update.StreamId,
                Venue: update.Venue
            );

            TrackIntegrity(evt);
            _publisher.TryPublish(MarketEvent.DepthIntegrity(update.Timestamp, symbol, evt));

            return;
        }

        var snapshot = book.GetSnapshot(symbol);
        if (snapshot is null) return;

        _publisher.TryPublish(MarketEvent.L2Snapshot(snapshot.Timestamp, symbol, snapshot));
    }

    private void TrackIntegrity(DepthIntegrityEvent evt)
    {
        _recentIntegrity.Enqueue(evt);
        while (_recentIntegrity.Count > 100)
            _recentIntegrity.TryDequeue(out _);
    }

    internal sealed class SymbolOrderBookBuffer : IDisposable
    {
        private readonly ReaderWriterLockSlim _rwLock = new(LockRecursionPolicy.NoRecursion);

        private readonly List<OrderBookLevel> _bids = new();
        private readonly List<OrderBookLevel> _asks = new();
        private readonly int _maxDepth;

        private bool _stale;
        private long _localSequenceCounter;
        private long _lastObservedSequence;
        private string? _lastStreamId;
        private string? _lastVenue;
        private DateTimeOffset _lastUpdateTimestamp;

        public SymbolOrderBookBuffer(int maxDepth)
        {
            _maxDepth = maxDepth;
        }

        public bool IsStale
        {
            get
            {
                _rwLock.EnterReadLock();
                try { return _stale; }
                finally { _rwLock.ExitReadLock(); }
            }
        }

        public long LastObservedSequence
        {
            get
            {
                _rwLock.EnterReadLock();
                try { return _lastObservedSequence; }
                finally { _rwLock.ExitReadLock(); }
            }
        }

        public string? LastErrorDescription { get; private set; }

        public void Reset()
        {
            _rwLock.EnterWriteLock();
            try
            {
                _bids.Clear();
                _asks.Clear();
                _stale = false;
                _localSequenceCounter = 0;
                _lastObservedSequence = 0;
                _lastStreamId = null;
                _lastVenue = null;
                _lastUpdateTimestamp = default;
                LastErrorDescription = null;
            }
            finally
            {
                _rwLock.ExitWriteLock();
            }
        }

        public LOBSnapshot? GetSnapshot(string symbol)
        {
            _rwLock.EnterReadLock();
            try
            {
                if (_bids.Count == 0 && _asks.Count == 0) return null;

                var bidsCopy = _bids.ToArray();
                var asksCopy = _asks.ToArray();
                var stale = _stale;
                var seqNum = _lastObservedSequence;
                var snapshotTimestamp = _lastUpdateTimestamp == default ? DateTimeOffset.UtcNow : _lastUpdateTimestamp;

                decimal? mid = null;
                if (bidsCopy.Length > 0 && asksCopy.Length > 0)
                    mid = (bidsCopy[0].Price + asksCopy[0].Price) / 2m;

                decimal? imb = null;
                if (bidsCopy.Length > 0 && asksCopy.Length > 0)
                {
                    var b = bidsCopy[0].Size;
                    var a = asksCopy[0].Size;
                    var tot = b + a;
                    if (tot > 0) imb = (b - a) / tot;
                }

                return new LOBSnapshot(
                    Timestamp: snapshotTimestamp,
                    Symbol: symbol,
                    Bids: bidsCopy,
                    Asks: asksCopy,
                    MidPrice: mid,
                    MicroPrice: null,
                    Imbalance: imb,
                    MarketState: stale ? MarketState.Unknown : MarketState.Normal,
                    SequenceNumber: seqNum,
                    StreamId: _lastStreamId,
                    Venue: _lastVenue
                );
            }
            finally
            {
                _rwLock.ExitReadLock();
            }
        }

        public DepthIntegrityKind Apply(MarketDepthUpdate upd)
        {
            _rwLock.EnterWriteLock();
            try
            {
                if (_stale)
                {
                    LastErrorDescription = "Stream is stale (previous integrity failure). Resync required.";
                    return DepthIntegrityKind.Stale;
                }

                var keyChanged = !string.Equals(_lastStreamId, upd.StreamId, StringComparison.Ordinal)
                    || !string.Equals(_lastVenue, upd.Venue, StringComparison.Ordinal);

                if (keyChanged && _lastStreamId is not null)
                {
                    _stale = true;
                    LastErrorDescription = "Depth stream changed; continuity reset detected and resync required.";
                    return DepthIntegrityKind.OutOfOrder;
                }

                // Track continuity (when seq is provided).
                if (upd.SequenceNumber > 0 && _lastObservedSequence > 0)
                {
                    if (upd.SequenceNumber <= _lastObservedSequence)
                    {
                        _stale = true;
                        LastErrorDescription = $"Out-of-order/duplicate depth sequence: last {_lastObservedSequence}, received {upd.SequenceNumber}.";
                        return DepthIntegrityKind.OutOfOrder;
                    }

                    var expected = _lastObservedSequence + 1;
                    if (upd.SequenceNumber > expected)
                    {
                        _stale = true;
                        LastErrorDescription = $"Depth sequence gap: expected {expected}, received {upd.SequenceNumber}.";
                        return DepthIntegrityKind.Gap;
                    }
                }

                var sideList = upd.Side == OrderBookSide.Bid ? _bids : _asks;

                switch (upd.Operation)
                {
                    case DepthOperation.Insert:
                        if (upd.Position < 0 || upd.Position > sideList.Count)
                        {
                            _stale = true;
                            LastErrorDescription = $"Insert position {upd.Position} out of range (count={sideList.Count}).";
                            return DepthIntegrityKind.InvalidPosition;
                        }
                        sideList.Insert(upd.Position, new OrderBookLevel(upd.Side, upd.Position, upd.Price, upd.Size, upd.MarketMaker));
                        ReindexRange(sideList, upd.Side, upd.Position);
                        TrimDepth(sideList);
                        break;

                    case DepthOperation.Update:
                        if (upd.Position < 0 || upd.Position >= sideList.Count)
                        {
                            _stale = true;
                            LastErrorDescription = $"Update position {upd.Position} missing (count={sideList.Count}).";
                            return DepthIntegrityKind.OutOfOrder;
                        }
                        sideList[upd.Position] = sideList[upd.Position] with { Price = upd.Price, Size = upd.Size, MarketMaker = upd.MarketMaker };
                        break;

                    case DepthOperation.Delete:
                        if (upd.Position < 0 || upd.Position >= sideList.Count)
                        {
                            _stale = true;
                            LastErrorDescription = $"Delete position {upd.Position} missing (count={sideList.Count}).";
                            return DepthIntegrityKind.InvalidPosition;
                        }
                        sideList.RemoveAt(upd.Position);
                        ReindexRange(sideList, upd.Side, upd.Position);
                        break;

                    default:
                        _stale = true;
                        LastErrorDescription = $"Unknown depth operation: {upd.Operation}";
                        return DepthIntegrityKind.Unknown;
                }

                _localSequenceCounter++;
                _lastObservedSequence = upd.SequenceNumber > 0 ? upd.SequenceNumber : _localSequenceCounter;
                _lastStreamId = upd.StreamId;
                _lastVenue = upd.Venue;
                _lastUpdateTimestamp = upd.Timestamp;

                LastErrorDescription = null;
                return DepthIntegrityKind.Ok;
            }
            finally
            {
                _rwLock.ExitWriteLock();
            }
        }

        private void TrimDepth(List<OrderBookLevel> levels)
        {
            if (levels.Count <= _maxDepth) return;
            levels.RemoveRange(_maxDepth, levels.Count - _maxDepth);
        }

        private static void ReindexRange(List<OrderBookLevel> levels, OrderBookSide side, int startIndex)
        {
            for (int i = Math.Max(0, startIndex); i < levels.Count; i++)
                levels[i] = levels[i] with { Side = side, Level = i };
        }

        public void Dispose()
        {
            _rwLock.Dispose();
        }
    }
}
