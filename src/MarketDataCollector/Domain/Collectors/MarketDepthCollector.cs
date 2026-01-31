using System.Collections.Concurrent;
using MarketDataCollector.Contracts.Domain.Enums;
using MarketDataCollector.Contracts.Domain.Models;
using MarketDataCollector.Contracts.Domain.Events;
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
    private readonly ConcurrentDictionary<string, IntegrityWindow> _integrityWindows = new(StringComparer.OrdinalIgnoreCase);

    private const int AutoResetThreshold = 3;
    private static readonly TimeSpan AutoResetWindow = TimeSpan.FromSeconds(15);

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
    /// Apply a single depth delta update.
    /// </summary>
    public void OnDepth(MarketDepthUpdate update)
    {
        if (update is null) throw new ArgumentNullException(nameof(update));
        if (string.IsNullOrWhiteSpace(update.Symbol)) return;

        var symbol = update.Symbol.Trim();

        if (!ShouldProcessUpdate(symbol))
            return;

        var book = _books.GetOrAdd(symbol, _ => new SymbolOrderBookBuffer());

        var result = book.Apply(update, out var snapshot);

        if (result != DepthIntegrityKind.Unknown)
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

            if (ShouldAutoReset(symbol, evt.Timestamp))
            {
                ResetSymbolStream(symbol);
            }
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

        var window = _integrityWindows.GetOrAdd(evt.Symbol, _ => new IntegrityWindow(AutoResetWindow, AutoResetThreshold));
        window.Add(evt.Timestamp);
    }

    private bool ShouldAutoReset(string symbol, DateTimeOffset timestamp)
    {
        if (!_integrityWindows.TryGetValue(symbol, out var window))
            return false;

        return window.ShouldReset(timestamp);
    }

    // =========================
    // Internal per-symbol buffer
    // =========================
    private sealed class IntegrityWindow
    {
        private readonly TimeSpan _window;
        private readonly int _threshold;
        private readonly Queue<DateTimeOffset> _events = new();
        private readonly object _sync = new();

        public IntegrityWindow(TimeSpan window, int threshold)
        {
            _window = window;
            _threshold = threshold;
        }

        public void Add(DateTimeOffset timestamp)
        {
            lock (_sync)
            {
                _events.Enqueue(timestamp);
                Trim(timestamp);
            }
        }

        public bool ShouldReset(DateTimeOffset now)
        {
            lock (_sync)
            {
                Trim(now);
                return _events.Count >= _threshold;
            }
        }

        private void Trim(DateTimeOffset now)
        {
            while (_events.Count > 0 && now - _events.Peek() > _window)
                _events.Dequeue();
        }
    }

    internal sealed class SymbolOrderBookBuffer
    {
        private readonly object _sync = new();

        private readonly List<OrderBookLevel> _bids = new();
        private readonly List<OrderBookLevel> _asks = new();

        private bool _stale;

        private long _sequenceCounter;

        public bool IsStale
        {
            get { lock (_sync) return _stale; }
        }

        public string? LastErrorDescription { get; private set; }

        public void Reset()
        {
            lock (_sync)
            {
                _bids.Clear();
                _asks.Clear();
                _stale = false;
                _sequenceCounter = 0;
                LastErrorDescription = null;
            }
        }

        public DepthIntegrityKind Apply(MarketDepthUpdate upd, out LOBSnapshot? snapshot)
        {
            lock (_sync)
            {
                snapshot = null;

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
                        Reindex(sideList, upd.Side);
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
                        Reindex(sideList, upd.Side);
                        break;

                    default:
                        _stale = true;
                        LastErrorDescription = $"Unknown depth operation: {upd.Operation}";
                        return DepthIntegrityKind.Unknown;
                }

                // increment local sequence counter (IB depth doesn't carry explicit seq)
                _sequenceCounter++;

                // Build derived microstructure metrics
                var bidsCopy = _bids.ToArray();
                var asksCopy = _asks.ToArray();

                decimal? mid = null;
                if (bidsCopy.Length > 0 && asksCopy.Length > 0)
                    mid = (bidsCopy[0].Price + asksCopy[0].Price) / 2m;

                // Simple top-of-book imbalance using best level sizes
                decimal? imb = null;
                if (bidsCopy.Length > 0 && asksCopy.Length > 0)
                {
                    var b = bidsCopy[0].Size;
                    var a = asksCopy[0].Size;
                    var tot = b + a;
                    if (tot > 0) imb = (b - a) / tot;
                }

                snapshot = new LOBSnapshot(
                    Timestamp: upd.Timestamp,
                    Symbol: upd.Symbol,
                    Bids: bidsCopy,
                    Asks: asksCopy,
                    MidPrice: mid,
                    MicroPrice: null,
                    Imbalance: imb,
                    MarketState: MarketState.Normal,
                    SequenceNumber: upd.SequenceNumber != 0 ? upd.SequenceNumber : _sequenceCounter,
                    StreamId: upd.StreamId,
                    Venue: upd.Venue
                );

                LastErrorDescription = null;
                return DepthIntegrityKind.Unknown; // ok
            }
        }

        private static void Reindex(List<OrderBookLevel> levels, OrderBookSide side)
        {
            for (int i = 0; i < levels.Count; i++)
                levels[i] = levels[i] with { Side = side, Level = i };
        }
    }
}
