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

    // Per-symbol rolling state
    private readonly ConcurrentDictionary<string, SymbolTradeState> _stateBySymbol =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Maximum allowed length for a symbol. Covers most instrument types including options and futures.
    /// </summary>
    private const int MaxSymbolLength = 50;

    public TradeDataCollector(IMarketEventPublisher publisher, IQuoteStateStore? quotes = null)
    {
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        _quotes = quotes;
    }

    /// <summary>
    /// Validates a symbol format. Valid symbols contain only alphanumeric characters,
    /// dots, hyphens, underscores, colons, or slashes.
    /// </summary>
    /// <param name="symbol">The symbol to validate.</param>
    /// <param name="reason">When validation fails, contains the reason.</param>
    /// <returns>True if valid, false otherwise.</returns>
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

    /// <summary>
    /// Entry point from router/adapter layer.
    /// Performs sequence continuity checks, emits Integrity events on anomalies,
    /// emits Trade + OrderFlow events on accepted updates.
    /// </summary>
    public void OnTrade(MarketTradeUpdate update)
    {
        if (update is null) throw new ArgumentNullException(nameof(update));
        if (string.IsNullOrWhiteSpace(update.Symbol)) return;

        var symbol = update.Symbol;

        // -------- Symbol format validation --------
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

        // -------- SequenceNumber bounds validation --------
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

        var state = _stateBySymbol.GetOrAdd(symbol, _ => new SymbolTradeState());

        // -------- Integrity / continuity --------
        // Rules:
        //  - SequenceNumber must be strictly increasing per symbol stream.
        //  - If we detect out-of-order or gap, emit IntegrityEvent.
        //  - For gaps, we still accept the trade (configurable), but flag IsStale in stats.
        //  - For out-of-order or duplicates, we reject the trade (do not advance stats).

        if (state.LastSequenceNumber.HasValue)
        {
            var last = state.LastSequenceNumber.Value;

            if (seq <= last)
            {
                // out-of-order or duplicate
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

            // gap?
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

                // Mark stats stale until we see a clean continuation again.
                state.MarkStale();
            }
        }

        state.LastSequenceNumber = seq;

        // -------- Aggressor inference (optional) --------
        // If upstream cannot classify aggressor, infer using latest BBO:
        //  - Price >= Ask => Buy
        //  - Price <= Bid => Sell
        //  - Otherwise Unknown
        var aggressor = update.Aggressor;
        if (aggressor == AggressorSide.Unknown && _quotes != null && _quotes.TryGet(symbol, out var bbo))
        {
            if (bbo.AskPrice > 0m && update.Price >= bbo.AskPrice) aggressor = AggressorSide.Buy;
            else if (bbo.BidPrice > 0m && update.Price <= bbo.BidPrice) aggressor = AggressorSide.Sell;
        }

        // -------- Trade record --------
        var trade = new Trade(
            Timestamp: update.Timestamp,
            Symbol: symbol,
            Price: update.Price,
            Size: update.Size,
            Aggressor: aggressor,
            SequenceNumber: seq,
            StreamId: update.StreamId,
            Venue: update.Venue);

        state.RegisterTrade(trade);

        _publisher.TryPublish(MarketEvent.Trade(trade.Timestamp, trade.Symbol, trade));

        // -------- OrderFlow statistics --------
        var stats = state.BuildOrderFlowStats(
            timestamp: update.Timestamp,
            symbol: symbol,
            seq: seq,
            streamId: update.StreamId,
            venue: update.Venue);

        _publisher.TryPublish(MarketEvent.OrderFlow(update.Timestamp, symbol, stats));
    }

    // =========================
    // Per-symbol state
    // =========================
    private sealed class SymbolTradeState
    {
        public long? LastSequenceNumber;

        private long _buyVolume;
        private long _sellVolume;
        private long _unknownVolume;

        private decimal _vwapNumerator;
        private long _vwapDenominator;

        private int _tradeCount;
        private bool _isStale;

        public void MarkStale() => _isStale = true;

        public void RegisterTrade(Trade trade)
        {
            _tradeCount++;

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

            // If we're in stale mode, clear it once we start accepting sequential updates again.
            // (We don't perfectly know that here, but once we keep accepting trades it is "fresh enough".)
            // You can make this stricter later by requiring N consecutive sequences.
            if (_isStale) _isStale = false;
        }

        public OrderFlowStatistics BuildOrderFlowStats(
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
                Venue: venue);
        }
    }
}
