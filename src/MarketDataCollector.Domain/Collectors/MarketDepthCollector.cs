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
    private const int MaxDepth = 50;

    public MarketDepthCollector(IMarketEventPublisher publisher, bool requireExplicitSubscription = true)
        : base(requireExplicitSubscription)
    {
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
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

        var book = _books.GetOrAdd(symbol, _ => new SymbolOrderBookBuffer(MaxDepth));

        var result = book.Apply(update, out var snapshot);

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

            _publisher.TryPublish(MarketEvent.ResyncRequested(update.Timestamp, symbol, evt.Description, update.StreamId, update.Venue));
            return;
        }

        if (snapshot is null) return;

        // Emit snapshot. Support explicit payload wrapper too if you want to swap later.
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
        private string? _lastStreamId;
        private string? _lastVenue;
        private DateTimeOffset _lastUpdateTimestamp;

        private long _ingestSequenceCounter;
        private long _lastAppliedSequenceNumber;

        public SymbolOrderBookBuffer(int maxDepth)
        {
            _maxDepth = Math.Max(1, maxDepth);
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

        public string? LastErrorDescription { get; private set; }

        public void Reset()
        {
            _rwLock.EnterWriteLock();
            try
            {
                _bids.Clear();
                _asks.Clear();
                _stale = false;
                _lastUpdateTimestamp = default;
                _lastStreamId = null;
                _lastVenue = null;
                _ingestSequenceCounter = 0;
                _lastAppliedSequenceNumber = 0;
                LastErrorDescription = null;
            }
            finally
            {
                _rwLock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Returns a read-only snapshot of the current order book state.
        /// Returns null if the book is empty.
        /// </summary>
        public LOBSnapshot? GetSnapshot(string symbol)
        {
            _rwLock.EnterReadLock();
            try
            {
                if (_bids.Count == 0 && _asks.Count == 0) return null;

                var bidsCopy = _bids.ToArray();
                var asksCopy = _asks.ToArray();
                var stale = _stale;
                var seqNum = _lastAppliedSequenceNumber;
                var ts = _lastUpdateTimestamp == default ? DateTimeOffset.UtcNow : _lastUpdateTimestamp;
                var streamId = _lastStreamId;
                var venue = _lastVenue;

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
                    Timestamp: ts,
                    Symbol: symbol,
                    Bids: bidsCopy,
                    Asks: asksCopy,
                    MidPrice: mid,
                    MicroPrice: null,
                    Imbalance: imb,
                    MarketState: stale ? MarketState.Unknown : MarketState.Normal,
                    SequenceNumber: seqNum,
                    StreamId: streamId,
                    Venue: venue
                );
            }
            finally
            {
                _rwLock.ExitReadLock();
            }
        }

        public DepthIntegrityKind Apply(MarketDepthUpdate upd, out LOBSnapshot? snapshot)
        {
            snapshot = null;
            long seqNum;
            DateTimeOffset lastUpdateTimestamp;
            string? streamId;
            string? venue;

            _rwLock.EnterWriteLock();
            try
            {
                if (_stale)
                {
                    LastErrorDescription = "Stream is stale (previous integrity failure). Reset required.";
                    return DepthIntegrityKind.Stale;
                }

                var sideList = upd.Side == OrderBookSide.Bid ? _bids : _asks;

                // Validate and apply operation
                switch (upd.Operation)
                {
                    case DepthOperation.Insert:
                        if (upd.Position < 0 || upd.Position > sideList.Count)
                        {
                            _stale = true;
                            LastErrorDescription = $"Insert position {upd.Position} out of range (count={sideList.Count}).";
                            return DepthIntegrityKind.Gap;
                        }
                        sideList.Insert(upd.Position, new OrderBookLevel(upd.Side, upd.Position, upd.Price, upd.Size, upd.MarketMaker));
                        ReindexFrom(sideList, upd.Side, upd.Position + 1);
                        TrimToMaxDepth(sideList);
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
                        ReindexFrom(sideList, upd.Side, upd.Position);
                        break;

                    default:
                        _stale = true;
                        LastErrorDescription = $"Unknown depth operation: {upd.Operation}";
                        return DepthIntegrityKind.Unknown;
                }

                // Track continuity where upstream sequence numbers are available.
                if (upd.SequenceNumber > 0)
                {
                    if (_lastAppliedSequenceNumber > 0)
                    {
                        if (upd.SequenceNumber == _lastAppliedSequenceNumber)
                        {
                            _stale = true;
                            LastErrorDescription = $"Duplicate depth sequence {_lastAppliedSequenceNumber}.";
                            return DepthIntegrityKind.OutOfOrder;
                        }

                        if (upd.SequenceNumber < _lastAppliedSequenceNumber)
                        {
                            _stale = true;
                            LastErrorDescription = $"Out-of-order depth sequence: last {_lastAppliedSequenceNumber}, received {upd.SequenceNumber}.";
                            return DepthIntegrityKind.OutOfOrder;
                        }

                        if (upd.SequenceNumber > _lastAppliedSequenceNumber + 1)
                        {
                            _stale = true;
                            LastErrorDescription = $"Depth sequence gap: expected {_lastAppliedSequenceNumber + 1}, received {upd.SequenceNumber}.";
                            return DepthIntegrityKind.Gap;
                        }
                    }

                    seqNum = upd.SequenceNumber;
                }
                else
                {
                    _ingestSequenceCounter++;
                    seqNum = _ingestSequenceCounter;
                }

                _lastAppliedSequenceNumber = seqNum;
                _lastUpdateTimestamp = upd.Timestamp;
                _lastStreamId = upd.StreamId;
                _lastVenue = upd.Venue;
                lastUpdateTimestamp = _lastUpdateTimestamp;
                streamId = _lastStreamId;
                venue = _lastVenue;

                LastErrorDescription = null;
                snapshot = BuildSnapshot(upd.Symbol, lastUpdateTimestamp, seqNum, streamId, venue);
                return DepthIntegrityKind.Ok;
            }
            finally
            {
                _rwLock.ExitWriteLock();
            }
        }

        private static void ReindexFrom(List<OrderBookLevel> levels, OrderBookSide side, int startIndex)
        {
            for (int i = Math.Max(0, startIndex); i < levels.Count; i++)
                levels[i] = levels[i] with { Side = side, Level = i };
        }

        private void TrimToMaxDepth(List<OrderBookLevel> levels)
        {
            if (levels.Count <= _maxDepth) return;
            levels.RemoveRange(_maxDepth, levels.Count - _maxDepth);
        }

        private LOBSnapshot BuildSnapshot(string symbol, DateTimeOffset timestamp, long seqNum, string? streamId, string? venue)
        {
            var bidsCopy = _bids.ToArray();
            var asksCopy = _asks.ToArray();

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
                Timestamp: timestamp,
                Symbol: symbol,
                Bids: bidsCopy,
                Asks: asksCopy,
                MidPrice: mid,
                MicroPrice: null,
                Imbalance: imb,
                MarketState: _stale ? MarketState.Unknown : MarketState.Normal,
                SequenceNumber: seqNum,
                StreamId: streamId,
                Venue: venue
            );
        }

        public void Dispose()
        {
            _rwLock.Dispose();
        }
    }
}
