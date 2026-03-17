using FluentAssertions;
using MarketDataCollector.Backtesting.FillModels;
using MarketDataCollector.Backtesting.Portfolio;
using MarketDataCollector.Backtesting.Sdk;
using MarketDataCollector.Contracts.Domain.Enums;
using MarketDataCollector.Contracts.Domain.Models;
using MarketDataCollector.Domain.Events;

namespace MarketDataCollector.Backtesting.Tests;

public sealed class FillModelTests
{
    private static MarketEvent MakeBarEvent(string symbol, decimal open, decimal high, decimal low, decimal close) =>
        MarketEvent.HistoricalBar(DateTimeOffset.UtcNow, symbol, new HistoricalBar(
            symbol, DateOnly.FromDateTime(DateTime.Today), open, high, low, close, 100_000L, "test"));

    private static MarketEvent MakeLobEvent(string symbol, decimal askPrice, long askQty) =>
        MarketEvent.L2Snapshot(DateTimeOffset.UtcNow, symbol, new LOBSnapshot(
            DateTimeOffset.UtcNow, symbol,
            Bids: [new OrderBookLevel(OrderBookSide.Bid, 0, askPrice - 0.01m, 1000m)],
            Asks: [new OrderBookLevel(OrderBookSide.Ask, 0, askPrice, (decimal)askQty)]));

    [Fact]
    public void BarMidpointFillModel_FillsMarketOrder_AtMidpoint()
    {
        var model = new BarMidpointFillModel(new FixedCommissionModel(0m), slippageBasisPoints: 0m);
        var order = new Order(Guid.NewGuid(), "SPY", OrderType.Market, 10L, null, null, DateTimeOffset.UtcNow);
        var evt = MakeBarEvent("SPY", 400m, 410m, 395m, 405m);

        var fills = model.TryFill(order, evt);

        fills.Should().HaveCount(1);
        fills[0].FillPrice.Should().Be(402.5m);  // (400+405)/2
        fills[0].FilledQuantity.Should().Be(10L);
    }

    [Fact]
    public void BarMidpointFillModel_LimitBuy_FillsIfLowTouched()
    {
        var model = new BarMidpointFillModel(new FixedCommissionModel(0m));
        var order = new Order(Guid.NewGuid(), "SPY", OrderType.Limit, 10L, 397m, null, DateTimeOffset.UtcNow);
        var evt = MakeBarEvent("SPY", 400m, 410m, 395m, 405m);  // low=395 touches limit 397

        var fills = model.TryFill(order, evt);

        fills.Should().HaveCount(1);
        fills[0].FillPrice.Should().Be(397m);
    }

    [Fact]
    public void BarMidpointFillModel_LimitBuy_NoFillIfNotTouched()
    {
        var model = new BarMidpointFillModel(new FixedCommissionModel(0m));
        var order = new Order(Guid.NewGuid(), "SPY", OrderType.Limit, 10L, 390m, null, DateTimeOffset.UtcNow);
        var evt = MakeBarEvent("SPY", 400m, 410m, 395m, 405m);  // low=395 does NOT reach 390

        var fills = model.TryFill(order, evt);

        fills.Should().BeEmpty();
    }

    [Fact]
    public void OrderBookFillModel_FillsBuyAtAsk()
    {
        var model = new OrderBookFillModel(new FixedCommissionModel(0m));
        var order = new Order(Guid.NewGuid(), "SPY", OrderType.Market, 100L, null, null, DateTimeOffset.UtcNow);
        var evt = MakeLobEvent("SPY", 410m, 200L);

        var fills = model.TryFill(order, evt);

        fills.Should().HaveCount(1);
        fills[0].FillPrice.Should().Be(410m);
        fills[0].FilledQuantity.Should().Be(100L);
    }

    [Fact]
    public void BarMidpointFillModel_WrongSymbol_ReturnsEmpty()
    {
        var model = new BarMidpointFillModel(new FixedCommissionModel(0m));
        var order = new Order(Guid.NewGuid(), "AAPL", OrderType.Market, 10L, null, null, DateTimeOffset.UtcNow);
        var evt = MakeBarEvent("SPY", 400m, 410m, 395m, 405m);

        var fills = model.TryFill(order, evt);

        fills.Should().BeEmpty();
    }
}
