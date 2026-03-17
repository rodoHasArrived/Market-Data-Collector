using FluentAssertions;
using MarketDataCollector.Backtesting.Portfolio;
using MarketDataCollector.Backtesting.Sdk;

namespace MarketDataCollector.Backtesting.Tests;

public sealed class SimulatedPortfolioTests
{
    private static SimulatedPortfolio CreatePortfolio(decimal initialCash = 10_000m)
        => new(initialCash, new FixedCommissionModel(0m), annualMarginRate: 0.05, annualShortRebateRate: 0.02);

    [Fact]
    public void ProcessFill_BuyOrder_DeductsCashAndCreatesPosition()
    {
        var portfolio = CreatePortfolio(10_000m);
        var fill = new FillEvent(Guid.NewGuid(), Guid.NewGuid(), "SPY", 10L, 400m, 0m, DateTimeOffset.UtcNow);

        portfolio.ProcessFill(fill);

        portfolio.Cash.Should().Be(10_000m - 10 * 400m);
        portfolio.GetCurrentPositions().Should().ContainKey("SPY");
        portfolio.GetCurrentPositions()["SPY"].Quantity.Should().Be(10);
    }

    [Fact]
    public void ProcessFill_SellOrder_AddsCashAndRemovesPosition()
    {
        var portfolio = CreatePortfolio(10_000m);
        var orderId = Guid.NewGuid();

        // Buy 10 shares at 400
        portfolio.ProcessFill(new FillEvent(Guid.NewGuid(), orderId, "SPY", 10L, 400m, 0m, DateTimeOffset.UtcNow));
        // Sell 10 shares at 450
        portfolio.ProcessFill(new FillEvent(Guid.NewGuid(), orderId, "SPY", -10L, 450m, 0m, DateTimeOffset.UtcNow));

        portfolio.Cash.Should().Be(10_000m + (450m - 400m) * 10);
        portfolio.GetCurrentPositions().Should().NotContainKey("SPY");
    }

    [Fact]
    public void AccrueDailyInterest_ChargesMarginOnDebitBalance()
    {
        var portfolio = CreatePortfolio(1_000m);
        // Buy more than cash → creates debit balance
        portfolio.ProcessFill(new FillEvent(Guid.NewGuid(), Guid.NewGuid(), "SPY", 10L, 200m, 0m, DateTimeOffset.UtcNow));
        // Now cash = 1000 - 2000 = -1000 (debit balance)

        var cashBefore = portfolio.Cash;
        portfolio.AccrueDailyInterest(DateOnly.FromDateTime(DateTime.Today));

        // Interest = -1000 * 0.05 / 252 ≈ -0.198
        portfolio.Cash.Should().BeLessThan(cashBefore);
    }

    [Fact]
    public void TakeSnapshot_ReturnsCorrectEquityWithMarkToMarket()
    {
        var portfolio = CreatePortfolio(10_000m);
        portfolio.ProcessFill(new FillEvent(Guid.NewGuid(), Guid.NewGuid(), "SPY", 10L, 400m, 0m, DateTimeOffset.UtcNow));
        portfolio.UpdateLastPrice("SPY", 420m);  // price went up

        var snapshot = portfolio.TakeSnapshot(DateTimeOffset.UtcNow, DateOnly.FromDateTime(DateTime.Today));

        // Cash = 10000 - 4000 = 6000; LongMV = 10 * 420 = 4200; Equity = 10200
        snapshot.TotalEquity.Should().Be(10_200m);
    }

    [Fact]
    public void Commission_IsRecordedAsCashFlow()
    {
        var portfolio = new SimulatedPortfolio(10_000m, new FixedCommissionModel(5m), 0.05, 0.02);
        var fill = new FillEvent(Guid.NewGuid(), Guid.NewGuid(), "AAPL", 5L, 200m, 5m, DateTimeOffset.UtcNow);

        portfolio.ProcessFill(fill);
        var snapshot = portfolio.TakeSnapshot(DateTimeOffset.UtcNow, DateOnly.FromDateTime(DateTime.Today));

        snapshot.DayCashFlows.OfType<CommissionCashFlow>().Should().HaveCount(1);
        snapshot.DayCashFlows.OfType<CommissionCashFlow>().Single().Amount.Should().Be(-5m);
    }
}
