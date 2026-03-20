using FluentAssertions;
using Meridian.Backtesting.Portfolio;
using Meridian.Backtesting.Sdk;
using Meridian.Ledger;

namespace Meridian.Backtesting.Tests;

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

    // ── Ledger tests ─────────────────────────────────────────────────────────

    private static Meridian.Ledger.Ledger NewLedger() => new();

    [Fact]
    public void Ledger_InitialDeposit_PostsBalancedEntry()
    {
        var ledger = NewLedger();
        var startTs = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        _ = new SimulatedPortfolio(10_000m, new FixedCommissionModel(0m), 0.05, 0.02, ledger, startTs);

        ledger.Journal.Should().HaveCount(1);
        ledger.Journal[0].IsBalanced.Should().BeTrue();
        ledger.GetBalance(LedgerAccounts.Cash).Should().Be(10_000m);
        ledger.GetBalance(LedgerAccounts.CapitalAccount).Should().Be(10_000m);
        ledger.Journal[0].Timestamp.Should().Be(startTs);
    }

    [Fact]
    public void Ledger_BuyFill_PostsSecuritiesAndCash()
    {
        var ledger = NewLedger();
        var startTs = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var portfolio = new SimulatedPortfolio(10_000m, new FixedCommissionModel(0m), 0.05, 0.02, ledger, startTs);

        portfolio.ProcessFill(new FillEvent(Guid.NewGuid(), Guid.NewGuid(), "SPY", 10L, 400m, 0m, DateTimeOffset.UtcNow));

        var securities = LedgerAccounts.Securities("SPY");
        ledger.GetBalance(securities).Should().Be(4_000m);   // DR Securities
        ledger.GetBalance(LedgerAccounts.Cash).Should().Be(6_000m);  // CR Cash (10k - 4k)
        ledger.Journal.All(j => j.IsBalanced).Should().BeTrue();
    }

    [Fact]
    public void Ledger_SellWithGain_PostsRealizedGain()
    {
        var ledger = NewLedger();
        var startTs = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var portfolio = new SimulatedPortfolio(10_000m, new FixedCommissionModel(0m), 0.05, 0.02, ledger, startTs);
        var orderId = Guid.NewGuid();

        portfolio.ProcessFill(new FillEvent(Guid.NewGuid(), orderId, "SPY", 10L, 400m, 0m, DateTimeOffset.UtcNow));
        portfolio.ProcessFill(new FillEvent(Guid.NewGuid(), orderId, "SPY", -10L, 450m, 0m, DateTimeOffset.UtcNow));

        ledger.GetBalance(LedgerAccounts.RealizedGain).Should().Be(500m);  // (450-400)*10
        ledger.GetBalance(LedgerAccounts.RealizedLoss).Should().Be(0m);
        ledger.Journal.All(j => j.IsBalanced).Should().BeTrue();
    }

    [Fact]
    public void Ledger_SellWithLoss_PostsRealizedLoss()
    {
        var ledger = NewLedger();
        var startTs = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var portfolio = new SimulatedPortfolio(10_000m, new FixedCommissionModel(0m), 0.05, 0.02, ledger, startTs);
        var orderId = Guid.NewGuid();

        portfolio.ProcessFill(new FillEvent(Guid.NewGuid(), orderId, "SPY", 10L, 400m, 0m, DateTimeOffset.UtcNow));
        portfolio.ProcessFill(new FillEvent(Guid.NewGuid(), orderId, "SPY", -10L, 350m, 0m, DateTimeOffset.UtcNow));

        ledger.GetBalance(LedgerAccounts.RealizedLoss).Should().Be(500m);  // (400-350)*10
        ledger.GetBalance(LedgerAccounts.RealizedGain).Should().Be(0m);
        ledger.Journal.All(j => j.IsBalanced).Should().BeTrue();
    }

    [Fact]
    public void Ledger_SellAtCost_PostsNoGainOrLoss()
    {
        var ledger = NewLedger();
        var startTs = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var portfolio = new SimulatedPortfolio(10_000m, new FixedCommissionModel(0m), 0.05, 0.02, ledger, startTs);
        var orderId = Guid.NewGuid();

        portfolio.ProcessFill(new FillEvent(Guid.NewGuid(), orderId, "SPY", 10L, 400m, 0m, DateTimeOffset.UtcNow));
        portfolio.ProcessFill(new FillEvent(Guid.NewGuid(), orderId, "SPY", -10L, 400m, 0m, DateTimeOffset.UtcNow));

        ledger.GetBalance(LedgerAccounts.RealizedGain).Should().Be(0m);
        ledger.GetBalance(LedgerAccounts.RealizedLoss).Should().Be(0m);
        ledger.Journal.All(j => j.IsBalanced).Should().BeTrue();
    }

    [Fact]
    public void Ledger_Commission_PostsExpenseEntry()
    {
        var ledger = NewLedger();
        var startTs = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var portfolio = new SimulatedPortfolio(10_000m, new FixedCommissionModel(5m), 0.05, 0.02, ledger, startTs);

        portfolio.ProcessFill(new FillEvent(Guid.NewGuid(), Guid.NewGuid(), "AAPL", 5L, 200m, 5m, DateTimeOffset.UtcNow));

        ledger.GetBalance(LedgerAccounts.CommissionExpense).Should().Be(5m);
        ledger.Journal.All(j => j.IsBalanced).Should().BeTrue();
    }

    [Fact]
    public void Ledger_FillEntries_IncludeBacktestAuditMetadata()
    {
        var ledger = NewLedger();
        var startTs = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var portfolio = new SimulatedPortfolio(10_000m, new FixedCommissionModel(5m), 0.05, 0.02, ledger, startTs);
        var fill = new FillEvent(Guid.NewGuid(), Guid.NewGuid(), "aapl", 5L, 200m, 5m, startTs.AddMinutes(1));

        portfolio.ProcessFill(fill);

        ledger.GetJournalEntries(new LedgerQuery(OrderId: fill.OrderId))
            .Select(entry => entry.Metadata.ActivityType)
            .Should()
            .BeEquivalentTo(["buy", "commission"]);

        ledger.GetJournalEntries(new LedgerQuery(FillId: fill.FillId))
            .Should()
            .OnlyContain(entry => entry.Metadata.Symbol == "AAPL");
    }

    [Fact]
    public void Ledger_SymbolNormalization_SameAccountForDifferentCase()
    {
        var lower = LedgerAccounts.Securities("spy");
        var upper = LedgerAccounts.Securities("SPY");

        lower.Should().Be(upper);  // record equality; both use "SPY"
    }

    [Fact]
    public void Ledger_ShortSell_PostsShortPayable()
    {
        var ledger = NewLedger();
        var startTs = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var portfolio = new SimulatedPortfolio(10_000m, new FixedCommissionModel(0m), 0.05, 0.02, ledger, startTs);

        // Short sell 10 SPY @ $400 (no existing position)
        portfolio.ProcessFill(new FillEvent(Guid.NewGuid(), Guid.NewGuid(), "SPY", -10L, 400m, 0m, DateTimeOffset.UtcNow));

        var shortPayable = LedgerAccounts.ShortSecuritiesPayable("SPY");
        ledger.GetBalance(shortPayable).Should().Be(4_000m);     // CR Short Payable
        ledger.GetBalance(LedgerAccounts.Cash).Should().Be(14_000m); // 10k + 4k proceeds
        ledger.Journal.All(j => j.IsBalanced).Should().BeTrue();
    }

    [Fact]
    public void Ledger_CoverShortWithGain_PostsRealizedGain()
    {
        var ledger = NewLedger();
        var startTs = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var portfolio = new SimulatedPortfolio(10_000m, new FixedCommissionModel(0m), 0.05, 0.02, ledger, startTs);
        var orderId = Guid.NewGuid();

        // Short 10 @ $400, cover @ $350 → gain $500
        portfolio.ProcessFill(new FillEvent(Guid.NewGuid(), orderId, "SPY", -10L, 400m, 0m, DateTimeOffset.UtcNow));
        portfolio.ProcessFill(new FillEvent(Guid.NewGuid(), orderId, "SPY", 10L, 350m, 0m, DateTimeOffset.UtcNow));

        var shortPayable = LedgerAccounts.ShortSecuritiesPayable("SPY");
        ledger.GetBalance(shortPayable).Should().Be(0m);              // payable cleared
        ledger.GetBalance(LedgerAccounts.RealizedGain).Should().Be(500m);  // (400-350)*10
        ledger.GetBalance(LedgerAccounts.RealizedLoss).Should().Be(0m);
        ledger.Journal.All(j => j.IsBalanced).Should().BeTrue();
    }

    [Fact]
    public void Ledger_CoverShortWithLoss_PostsRealizedLoss()
    {
        var ledger = NewLedger();
        var startTs = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var portfolio = new SimulatedPortfolio(10_000m, new FixedCommissionModel(0m), 0.05, 0.02, ledger, startTs);
        var orderId = Guid.NewGuid();

        // Short 10 @ $400, cover @ $450 → loss $500
        portfolio.ProcessFill(new FillEvent(Guid.NewGuid(), orderId, "SPY", -10L, 400m, 0m, DateTimeOffset.UtcNow));
        portfolio.ProcessFill(new FillEvent(Guid.NewGuid(), orderId, "SPY", 10L, 450m, 0m, DateTimeOffset.UtcNow));

        var shortPayable = LedgerAccounts.ShortSecuritiesPayable("SPY");
        ledger.GetBalance(shortPayable).Should().Be(0m);               // payable cleared
        ledger.GetBalance(LedgerAccounts.RealizedLoss).Should().Be(500m);  // (450-400)*10
        ledger.GetBalance(LedgerAccounts.RealizedGain).Should().Be(0m);
        ledger.Journal.All(j => j.IsBalanced).Should().BeTrue();
    }

    [Fact]
    public void ApplyAssetEvent_Dividend_CreditsCash_AndRecordsLedgerIncome()
    {
        var ledger = NewLedger();
        var portfolio = new SimulatedPortfolio(10_000m, new FixedCommissionModel(0m), 0.05, 0.02, ledger);
        portfolio.ProcessFill(new FillEvent(Guid.NewGuid(), Guid.NewGuid(), "SPY", 10L, 400m, 0m, DateTimeOffset.UtcNow));

        var exDate = new DateTimeOffset(2024, 2, 1, 0, 0, 0, TimeSpan.Zero);
        portfolio.ApplyAssetEvent(new AssetEvent(exDate, "SPY", AssetEventType.Dividend, CashPerShare: 1.25m, Description: "Quarterly dividend"));

        portfolio.Cash.Should().Be(6_012.5m);
        var snapshot = portfolio.TakeSnapshot(exDate, DateOnly.FromDateTime(exDate.Date));
        snapshot.DayCashFlows.OfType<AssetEventCashFlow>().Should().ContainSingle(flow => flow.Amount == 12.5m && flow.EventType == AssetEventType.Dividend);
        ledger.GetBalance(LedgerAccounts.DividendIncome).Should().Be(12.5m);
    }

    [Fact]
    public void ApplyAssetEvent_Coupon_OnShortPosition_RecordsExpense()
    {
        var ledger = NewLedger();
        var portfolio = new SimulatedPortfolio(10_000m, new FixedCommissionModel(0m), 0.05, 0.02, ledger);
        portfolio.ProcessFill(new FillEvent(Guid.NewGuid(), Guid.NewGuid(), "TLT", -5L, 100m, 0m, DateTimeOffset.UtcNow));

        var paymentDate = new DateTimeOffset(2024, 2, 15, 0, 0, 0, TimeSpan.Zero);
        portfolio.ApplyAssetEvent(new AssetEvent(paymentDate, "TLT", AssetEventType.Coupon, CashPerShare: 2m));

        portfolio.Cash.Should().Be(10_490m);
        ledger.GetBalance(LedgerAccounts.CouponExpense).Should().Be(10m);
        var snapshot = portfolio.TakeSnapshot(paymentDate, DateOnly.FromDateTime(paymentDate.Date));
        snapshot.DayCashFlows.OfType<AssetEventCashFlow>().Should().ContainSingle(flow => flow.Amount == -10m && flow.EventType == AssetEventType.Coupon);
    }

    [Fact]
    public void ApplyAssetEvent_Split_AdjustsShareCount_AndAverageCost()
    {
        var portfolio = CreatePortfolio(10_000m);
        portfolio.ProcessFill(new FillEvent(Guid.NewGuid(), Guid.NewGuid(), "AAPL", 3L, 100m, 0m, DateTimeOffset.UtcNow));
        portfolio.UpdateLastPrice("AAPL", 120m);

        portfolio.ApplyAssetEvent(new AssetEvent(
            new DateTimeOffset(2024, 3, 1, 0, 0, 0, TimeSpan.Zero),
            "AAPL",
            AssetEventType.Split,
            PositionFactor: 2m));

        var position = portfolio.GetCurrentPositions()["AAPL"];
        position.Quantity.Should().Be(6);
        position.AverageCostBasis.Should().Be(50m);
    }

    [Fact]
    public void ApplyAssetEvent_Acquisition_TransfersPosition_AndBooksCashInLieu()
    {
        var ledger = NewLedger();
        var portfolio = new SimulatedPortfolio(10_000m, new FixedCommissionModel(0m), 0.05, 0.02, ledger);
        portfolio.ProcessFill(new FillEvent(Guid.NewGuid(), Guid.NewGuid(), "ATVI", 3L, 90m, 0m, DateTimeOffset.UtcNow));
        portfolio.UpdateLastPrice("ATVI", 95m);

        portfolio.ApplyAssetEvent(new AssetEvent(
            new DateTimeOffset(2024, 4, 1, 0, 0, 0, TimeSpan.Zero),
            "ATVI",
            AssetEventType.Acquisition,
            CashPerShare: 5m,
            PositionFactor: 0.5m,
            TargetSymbol: "MSFT",
            ReferencePrice: 400m,
            Description: "ATVI acquired by MSFT"));

        portfolio.GetCurrentPositions().Should().NotContainKey("ATVI");
        var newPosition = portfolio.GetCurrentPositions()["MSFT"];
        newPosition.Quantity.Should().Be(1);
        newPosition.AverageCostBasis.Should().Be(180m);
        portfolio.Cash.Should().Be(9_945m);
        ledger.GetBalance(LedgerAccounts.CorporateActionIncome).Should().Be(215m);
    }

}
