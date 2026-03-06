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
        if (_lastSequenceByStream.TryGetValue(streamKey, out var last))
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

        _lastSequenceByStream[streamKey] = seq;

        var state = _stateBySymbol.GetOrAdd(symbol, _ => new SymbolTradeState());

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
                var staleQuoteIntegrity = IntegrityEvent.CanonicalizationHardFail(
                    update.Timestamp,
                    symbol,
                    $"Aggressor inference skipped due to stale quote ({(update.Timestamp - bbo.Timestamp).TotalMilliseconds:0}ms old).",
                    update.SequenceNumber,
                    update.StreamId,
                    update.Venue);
                _publisher.TryPublish(MarketEvent.Integrity(update.Timestamp, symbol, staleQuoteIntegrity));
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

        var currentStats = state.RegisterTradeAndBuildStats(trade, RollingWindows);

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
            venue: null,
            RollingWindows);
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
        private readonly Queue<Trade> _rollingTrades = new();

        private long _buyVolume;
        private long _sellVolume;
        private long _unknownVolume;

        private decimal _vwapNumerator;
        private long _vwapDenominator;

        private int _tradeCount;
        private long _lastSequence;

        public long LastSequenceNumber
        {
            get
            {
                lock (_sync) return _lastSequence;
            }
        }

        public OrderFlowStatistics RegisterTradeAndBuildStats(Trade trade, TimeSpan[] windows)
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

                _rollingTrades.Enqueue(trade);
                var maxWindow = windows.Max();
                while (_rollingTrades.Count > 0 && trade.Timestamp - _rollingTrades.Peek().Timestamp > maxWindow)
                    _rollingTrades.Dequeue();

                return BuildStatsLocked(trade.Timestamp, trade.Symbol, trade.SequenceNumber, trade.StreamId, trade.Venue, windows);
            }
        }

        public OrderFlowStatistics BuildOrderFlowStats(
            DateTimeOffset timestamp,
            string symbol,
            long seq,
            string? streamId,
            string? venue,
            TimeSpan[] windows)
        {
            lock (_sync)
            {
                return BuildStatsLocked(timestamp, symbol, seq, streamId, venue, windows);
            }
        }

        private OrderFlowStatistics BuildStatsLocked(
            DateTimeOffset timestamp,
            string symbol,
            long seq,
            string? streamId,
            string? venue,
            TimeSpan[] windows)
        {
            var total = _buyVolume + _sellVolume + _unknownVolume;

            var imbalance = total == 0
                ? 0m
                : (decimal)(_buyVolume - _sellVolume) / total;

            var vwap = _vwapDenominator == 0
                ? 0m
                : _vwapNumerator / _vwapDenominator;

            var rolling = new List<RollingOrderFlowWindow>(windows.Length);
            foreach (var window in windows)
            {
                long buy = 0;
                long sell = 0;
                long unknown = 0;
                decimal num = 0;
                long den = 0;
                var cutoff = timestamp - window;
                foreach (var t in _rollingTrades)
                {
                    if (t.Timestamp < cutoff) continue;
                    switch (t.Aggressor)
                    {
                        case AggressorSide.Buy: buy += t.Size; break;
                        case AggressorSide.Sell: sell += t.Size; break;
                        default: unknown += t.Size; break;
                    }
                    num += t.Price * t.Size;
                    den += t.Size;
                }

                var rollTotal = buy + sell + unknown;
                var rollVwap = den == 0 ? 0m : num / den;
                var rollImbalance = rollTotal == 0 ? 0m : (decimal)(buy - sell) / rollTotal;
                rolling.Add(new RollingOrderFlowWindow((int)window.TotalSeconds, buy, sell, unknown, rollVwap, rollImbalance));
            }

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
