using System.Collections.Concurrent;
using MarketDataCollector.Contracts.Domain.Enums;
using MarketDataCollector.Contracts.Domain.Models;
using MarketDataCollector.Domain.Events;
using MarketDataCollector.Domain.Models;

namespace MarketDataCollector.Domain.Collectors;

/// <summary>
/// Captures tick-by-tick trades, maintains rolling order-flow statistics,
/// and emits unified MarketEvents with strongly-typed payloads.
/// </summary>
public sealed class TradeDataCollector
{
    private readonly IMarketEventPublisher _publisher;
    private readonly IQuoteStateStore? _quotes;

    private readonly ConcurrentDictionary<string, SymbolTradeState> _stateBySymbol =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly ConcurrentDictionary<TradeStreamKey, long> _lastSequenceByStream = new();

    private readonly ConcurrentDictionary<string, RecentTradeRing> _recentTrades =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly TimeSpan QuoteFreshnessThreshold = TimeSpan.FromMilliseconds(500);

    private static readonly TimeSpan[] RollingWindows =
    {
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(60)
    };

    private const int MaxSymbolLength = 50;
    private const int MaxRecentTrades = 200;

    public TradeDataCollector(IMarketEventPublisher publisher, IQuoteStateStore? quotes = null)
    {
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        _quotes = quotes;
    }

    private static bool IsValidSymbolFormat(string symbol, out string reason)
    {
        reason = string.Empty;

        if (symbol.Length > MaxSymbolLength)
        {
            reason = $"exceeds maximum length of {MaxSymbolLength} characters";
            return false;
        }

        foreach (char c in symbol)
        {
            if (!char.IsLetterOrDigit(c) && c != '.' && c != '-' && c != '_' && c != ':' && c != '/')
            {
                reason = $"contains invalid character '{c}'";
                return false;
            }
        }

        return true;
    }

    public void OnTrade(MarketTradeUpdate update)
    {
        if (update is null) throw new ArgumentNullException(nameof(update));
        if (string.IsNullOrWhiteSpace(update.Symbol)) return;

        var symbol = update.Symbol;

        if (!IsValidSymbolFormat(symbol, out var symbolValidationReason))
        {
            var integrity = IntegrityEvent.InvalidSymbol(
                update.Timestamp,
                symbol,
                symbolValidationReason,
                update.SequenceNumber,
                update.StreamId,
                update.Venue);

            _publisher.TryPublish(MarketEvent.Integrity(update.Timestamp, symbol, integrity));
            return;
        }

        var seq = update.SequenceNumber;
        if (seq < 0)
        {
            var integrity = IntegrityEvent.InvalidSequenceNumber(
                update.Timestamp,
                symbol,
                seq,
                "sequence number must be non-negative",
                update.StreamId,
                update.Venue);

            _publisher.TryPublish(MarketEvent.Integrity(update.Timestamp, symbol, integrity));
            return;
        }

        var streamKey = new TradeStreamKey(symbol, update.StreamId, update.Venue);
        // Skip continuity checks when seq == 0, as some adapters use 0 to mean "unsequenced".
        if (seq > 0 && _lastSequenceByStream.TryGetValue(streamKey, out var last))
        {
            if (seq <= last)
            {
                var integrity = IntegrityEvent.OutOfOrder(
                    update.Timestamp,
                    symbol,
                    last: last,
                    received: seq,
                    streamId: update.StreamId,
                    venue: update.Venue);

                _publisher.TryPublish(MarketEvent.Integrity(update.Timestamp, symbol, integrity));
                return;
            }

            var expected = last + 1;
            if (seq > expected)
            {
                var integrity = IntegrityEvent.SequenceGap(
                    update.Timestamp,
                    symbol,
                    expectedNext: expected,
                    received: seq,
                    streamId: update.StreamId,
                    venue: update.Venue);

                _publisher.TryPublish(MarketEvent.Integrity(update.Timestamp, symbol, integrity));
            }
        }

        if (seq > 0)
        {
            _lastSequenceByStream[streamKey] = seq;
        }

        var state = _stateBySymbol.GetOrAdd(symbol, _ => new SymbolTradeState(RollingWindows));

        var aggressor = update.Aggressor;
        var quoteFresh = false;
        if (aggressor == AggressorSide.Unknown && _quotes != null && _quotes.TryGet(symbol, out var bbo) && bbo != null)
        {
            quoteFresh = update.Timestamp - bbo.Timestamp <= QuoteFreshnessThreshold;
            if (quoteFresh)
            {
                if (bbo.AskPrice > 0m && update.Price >= bbo.AskPrice) aggressor = AggressorSide.Buy;
                else if (bbo.BidPrice > 0m && update.Price <= bbo.BidPrice) aggressor = AggressorSide.Sell;
            }
            else
            {
                // Quote is stale; skip aggressor inference and leave AggressorSide.Unknown without raising a hard canonicalization failure.
            }
        }

        var trade = new Trade(
            Timestamp: update.Timestamp,
            Symbol: symbol,
            Price: update.Price,
            Size: update.Size,
            Aggressor: aggressor,
            SequenceNumber: seq,
            StreamId: update.StreamId,
            Venue: update.Venue);

        var currentStats = state.RegisterTradeAndBuildStats(trade);

        var ring = _recentTrades.GetOrAdd(symbol, _ => new RecentTradeRing(MaxRecentTrades));
        ring.Add(trade);

        _publisher.TryPublish(MarketEvent.Trade(trade.Timestamp, trade.Symbol, trade));

        _publisher.TryPublish(MarketEvent.OrderFlow(update.Timestamp, symbol, currentStats));
    }

    public IReadOnlyList<Trade> GetRecentTrades(string symbol, int limit = 50)
    {
        if (string.IsNullOrWhiteSpace(symbol)) return Array.Empty<Trade>();
        if (!_recentTrades.TryGetValue(symbol, out var ring)) return Array.Empty<Trade>();
        return ring.GetRecent(Math.Min(limit, MaxRecentTrades));
    }

    public OrderFlowStatistics? GetOrderFlowSnapshot(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol)) return null;
        if (!_stateBySymbol.TryGetValue(symbol, out var state)) return null;

        return state.BuildOrderFlowStats(
            timestamp: DateTimeOffset.UtcNow,
            symbol: symbol,
            seq: state.LastSequenceNumber,
            streamId: null,
            venue: null);
    }

    public IReadOnlyList<string> GetTrackedSymbols()
        => _stateBySymbol.Keys.ToList();

    private readonly record struct TradeStreamKey
    {
        public string Symbol { get; }
        public string? StreamId { get; }
        public string? Venue { get; }

        public TradeStreamKey(string symbol, string? streamId, string? venue)
        {
            Symbol = symbol.ToUpperInvariant();
            StreamId = streamId;
            Venue = venue;
        }
    }
    private sealed class SymbolTradeState
    {
        private readonly object _sync = new();
        private readonly RollingWindowState[] _rollingWindows;

        private long _buyVolume;
        private long _sellVolume;
        private long _unknownVolume;

        private decimal _vwapNumerator;
        private long _vwapDenominator;

        private int _tradeCount;
        private long _lastSequence;

        public SymbolTradeState(TimeSpan[] windows)
        {
            _rollingWindows = new RollingWindowState[windows.Length];
            for (var i = 0; i < windows.Length; i++)
                _rollingWindows[i] = new RollingWindowState(windows[i]);
        }

        public long LastSequenceNumber
        {
            get { lock (_sync) return _lastSequence; }
        }

        public OrderFlowStatistics RegisterTradeAndBuildStats(Trade trade)
        {
            lock (_sync)
            {
                _tradeCount++;
                _lastSequence = trade.SequenceNumber;

                _vwapNumerator += trade.Price * trade.Size;
                _vwapDenominator += trade.Size;

                switch (trade.Aggressor)
                {
                    case AggressorSide.Buy:
                        _buyVolume += trade.Size;
                        break;
                    case AggressorSide.Sell:
                        _sellVolume += trade.Size;
                        break;
                    default:
                        _unknownVolume += trade.Size;
                        break;
                }

                // Update each rolling window in O(1) amortized: evict stale entries
                // then record the new trade using per-window running sums.
                foreach (var ws in _rollingWindows)
                    ws.AddAndEvict(trade);

                return BuildStatsLocked(trade.Timestamp, trade.Symbol, trade.SequenceNumber, trade.StreamId, trade.Venue);
            }
        }

        public OrderFlowStatistics BuildOrderFlowStats(
            DateTimeOffset timestamp,
            string symbol,
            long seq,
            string? streamId,
            string? venue)
        {
            lock (_sync)
            {
                // Evict stale trades from each window before reading the snapshot.
                foreach (var ws in _rollingWindows)
                    ws.EvictStale(timestamp);

                return BuildStatsLocked(timestamp, symbol, seq, streamId, venue);
            }
        }

        private OrderFlowStatistics BuildStatsLocked(
            DateTimeOffset timestamp,
            string symbol,
            long seq,
            string? streamId,
            string? venue)
        {
            var total = _buyVolume + _sellVolume + _unknownVolume;

            var imbalance = total == 0
                ? 0m
                : (decimal)(_buyVolume - _sellVolume) / total;

            var vwap = _vwapDenominator == 0
                ? 0m
                : _vwapNumerator / _vwapDenominator;

            // Build rolling window stats from per-window running sums in O(1) per window.
            var rolling = new List<RollingOrderFlowWindow>(_rollingWindows.Length);
            foreach (var ws in _rollingWindows)
                rolling.Add(ws.BuildWindow());

            return new OrderFlowStatistics(
                Timestamp: timestamp,
                Symbol: symbol,
                BuyVolume: _buyVolume,
                SellVolume: _sellVolume,
                UnknownVolume: _unknownVolume,
                VWAP: vwap,
                Imbalance: imbalance,
                TradeCount: _tradeCount,
                SequenceNumber: seq,
                StreamId: streamId,
                Venue: venue,
                RollingWindows: rolling);
        }

        /// <summary>
        /// Maintains a time-bounded queue and running aggregates for a single rolling window.
        /// All methods must be called under <see cref="SymbolTradeState"/>'s lock.
        /// </summary>
        private sealed class RollingWindowState
        {
            private readonly TimeSpan _window;
            private readonly Queue<Trade> _trades = new();

            private long _buyVolume;
            private long _sellVolume;
            private long _unknownVolume;
            private decimal _vwapNumerator;
            private long _vwapDenominator;
            private int _tradeCount;

            public RollingWindowState(TimeSpan window) => _window = window;

            /// <summary>
            /// Evicts trades that have aged out of the window, then records the new trade.
            /// O(1) amortized — each trade is added and removed at most once.
            /// </summary>
            public void AddAndEvict(Trade trade)
            {
                EvictStale(trade.Timestamp);

                _trades.Enqueue(trade);
                _tradeCount++;
                switch (trade.Aggressor)
                {
                    case AggressorSide.Buy:   _buyVolume    += trade.Size; break;
                    case AggressorSide.Sell:  _sellVolume   += trade.Size; break;
                    default:                  _unknownVolume += trade.Size; break;
                }
                _vwapNumerator   += trade.Price * trade.Size;
                _vwapDenominator += trade.Size;
            }

            /// <summary>
            /// Evicts trades older than <paramref name="now"/> minus the window duration.
            /// Called before snapshot reads to ensure the window reflects the current time.
            /// </summary>
            public void EvictStale(DateTimeOffset now)
            {
                var cutoff = now - _window;
                while (_trades.Count > 0 && _trades.Peek().Timestamp < cutoff)
                {
                    var expired = _trades.Dequeue();
                    _tradeCount = Math.Max(0, _tradeCount - 1);
                    switch (expired.Aggressor)
                    {
                        case AggressorSide.Buy:   _buyVolume    -= expired.Size; break;
                        case AggressorSide.Sell:  _sellVolume   -= expired.Size; break;
                        default:                  _unknownVolume -= expired.Size; break;
                    }
                    _vwapNumerator   -= expired.Price * expired.Size;
                    _vwapDenominator -= expired.Size;
                }
            }

            /// <summary>
            /// Returns the current rolling window stats directly from running sums — O(1).
            /// </summary>
            public RollingOrderFlowWindow BuildWindow()
            {
                var rollTotal = _buyVolume + _sellVolume + _unknownVolume;
                var rollVwap = _vwapDenominator == 0 ? 0m : _vwapNumerator / _vwapDenominator;
                var rollImbalance = rollTotal == 0 ? 0m : (decimal)(_buyVolume - _sellVolume) / rollTotal;

                return new RollingOrderFlowWindow(
                    (int)_window.TotalSeconds,
                    _buyVolume,
                    _sellVolume,
                    _unknownVolume,
                    rollVwap,
                    rollImbalance);
            }
        }
    }

    private sealed class RecentTradeRing
    {
        private readonly Trade[] _buffer;
        private readonly object _sync = new();
        private int _head;
        private int _count;

        public RecentTradeRing(int capacity) => _buffer = new Trade[capacity];

        public void Add(Trade trade)
        {
            lock (_sync)
            {
                _buffer[_head] = trade;
                _head = (_head + 1) % _buffer.Length;
                if (_count < _buffer.Length) _count++;
            }
        }

        public IReadOnlyList<Trade> GetRecent(int limit)
        {
            lock (_sync)
            {
                var take = Math.Min(limit, _count);
                var result = new Trade[take];
                for (int i = 0; i < take; i++)
                {
                    var idx = (_head - 1 - i + _buffer.Length) % _buffer.Length;
                    result[i] = _buffer[idx];
                }
                return result;
            }
        }
    }
}
