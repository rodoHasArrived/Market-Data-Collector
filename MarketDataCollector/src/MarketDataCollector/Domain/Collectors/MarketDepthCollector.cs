using System.Collections.Concurrent;
using MarketDataCollector.Domain.Events;
using MarketDataCollector.Domain.Models;

namespace MarketDataCollector.Domain.Collectors;

/// <summary>
/// Maintains per-symbol Level-2 order books from depth deltas and emits L2 snapshots + depth integrity events.
/// </summary>
public sealed class MarketDepthCollector
{
    private readonly IMarketEventPublisher _publisher;
    private readonly bool _requireExplicitSubscription;

    private readonly ConcurrentDictionary<string, bool> _subscriptions = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, SymbolOrderBookBuffer> _books = new(StringComparer.OrdinalIgnoreCase);

    public MarketDepthCollector(IMarketEventPublisher publisher, bool requireExplicitSubscription = true)
    {
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        _requireExplicitSubscription = requireExplicitSubscription;
    }

    public void RegisterSubscription(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol)) throw new ArgumentException("Symbol required.", nameof(symbol));
        _subscriptions[symbol.Trim()] = true;
    }

    public void UnregisterSubscription(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol)) return;
        _subscriptions.TryRemove(symbol.Trim(), out _);
    }

    public bool IsSubscribed(string symbol)
        => !string.IsNullOrWhiteSpace(symbol) && _subscriptions.TryGetValue(symbol.Trim(), out var v) && v;

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

    /// <summary>
    /// Apply a single depth delta update.
    /// </summary>
    public void OnDepth(MarketDepthUpdate update)
    {
        if (update is null) throw new ArgumentNullException(nameof(update));
        if (string.IsNullOrWhiteSpace(update.Symbol)) return;

        var symbol = update.Symbol.Trim();

        if (_requireExplicitSubscription && !IsSubscribed(symbol))
            return;

        if (!_requireExplicitSubscription)
            _subscriptions.TryAdd(symbol, true);

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

            _publisher.TryPublish(MarketEvent.DepthIntegrity(update.Timestamp, symbol, evt));
            return;
        }

        if (snapshot is null) return;

        // Emit snapshot. Support explicit payload wrapper too if you want to swap later.
        _publisher.TryPublish(MarketEvent.L2Snapshot(snapshot.Timestamp, symbol, snapshot));
    }

    // =========================
    // Internal per-symbol buffer
    // =========================
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

                double? mid = null;
                if (bidsCopy.Length > 0 && asksCopy.Length > 0)
                    mid = (bidsCopy[0].Price + asksCopy[0].Price) / 2.0;

                // Simple top-of-book imbalance using best level sizes
                double? imb = null;
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
