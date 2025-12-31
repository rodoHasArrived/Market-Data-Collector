using System.Collections.Concurrent;
using MarketDataCollector.Domain.Collectors;
using MarketDataCollector.Domain.Models;

namespace MarketDataCollector.Infrastructure.Providers.InteractiveBrokers;

/// <summary>
/// Thin adapter that turns raw IB callbacks into domain updates.
/// This file is written to compile without the IBApi package; wire the real EWrapper implementation
/// behind conditional compilation if/when you add the official IB API reference.
/// </summary>
public sealed class IBCallbackRouter
{
    private readonly MarketDepthCollector _depthCollector;
    private readonly TradeDataCollector _tradeCollector;

    // requestId/tickerId -> symbol maps
    private readonly ConcurrentDictionary<int, string> _depthTickerMap = new();
    private readonly ConcurrentDictionary<int, string> _tradeTickerMap = new();

    public IBCallbackRouter(MarketDepthCollector depthCollector, TradeDataCollector tradeCollector)
    {
        _depthCollector = depthCollector ?? throw new ArgumentNullException(nameof(depthCollector));
        _tradeCollector = tradeCollector ?? throw new ArgumentNullException(nameof(tradeCollector));
    }

    public void RegisterDepthTicker(int tickerId, string symbol) => _depthTickerMap[tickerId] = symbol;
    public void RegisterTradeTicker(int tickerId, string symbol) => _tradeTickerMap[tickerId] = symbol;

    // ---------------------------
    // Depth callbacks (IB shape)
    // ---------------------------

    public void UpdateMktDepth(int tickerId, int position, int operation, int side, double price, double size)
    {
        if (!_depthTickerMap.TryGetValue(tickerId, out var symbol)) return;

        var upd = new MarketDepthUpdate(
            Timestamp: DateTimeOffset.UtcNow,
            Symbol: symbol,
            Position: position,
            Operation: (DepthOperation)operation,
            Side: side == 0 ? OrderBookSide.Bid : OrderBookSide.Ask,
            Price: price,
            Size: size,
            MarketMaker: null,
            SequenceNumber: 0,
            StreamId: "IB",
            Venue: null
        );

        _depthCollector.OnDepth(upd);
    }

    public void UpdateMktDepthL2(int tickerId, int position, string marketMaker, int operation, int side, double price, double size, bool isSmartDepth)
    {
        if (!_depthTickerMap.TryGetValue(tickerId, out var symbol)) return;

        var upd = new MarketDepthUpdate(
            Timestamp: DateTimeOffset.UtcNow,
            Symbol: symbol,
            Position: position,
            Operation: (DepthOperation)operation,
            Side: side == 0 ? OrderBookSide.Bid : OrderBookSide.Ask,
            Price: price,
            Size: size,
            MarketMaker: marketMaker,
            SequenceNumber: 0,
            StreamId: isSmartDepth ? "IB-SMARTDEPTH" : "IB-L2",
            Venue: marketMaker
        );

        _depthCollector.OnDepth(upd);
    }


    // ---------------------------
    // Tick-by-tick trades
    // ---------------------------
    public void OnTickByTickAllLast(int reqId, int tickType, long time, double price, double size, string exchange, string specialConditions)
    {
        if (!_tradeTickerMap.TryGetValue(reqId, out var symbol)) return;

        // IB provides epoch seconds in 'time'
        var ts = DateTimeOffset.FromUnixTimeSeconds(time);

        // Aggressor inference from tickType isn't perfect; keep Unknown if not sure.
        var aggressor = AggressorSide.Unknown;

        var trade = new MarketTradeUpdate(
            Timestamp: ts,
            Symbol: symbol,
            Price: (decimal)price,
            Size: (long)Math.Round(size),
            Aggressor: aggressor,
            SequenceNumber: 0,
            StreamId: "IB-TBT",
            Venue: exchange
        );

        _tradeCollector.OnTrade(trade);
    }

}
