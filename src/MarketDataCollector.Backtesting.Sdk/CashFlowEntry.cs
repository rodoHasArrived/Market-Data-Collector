namespace MarketDataCollector.Backtesting.Sdk;

/// <summary>
/// Base record for all typed cash-flow entries that form the ledger used to compute XIRR
/// and distinguish time-weighted from cash-weighted returns.
/// </summary>
public abstract record CashFlowEntry(DateTimeOffset Timestamp, decimal Amount);

/// <summary>Cash flow arising from executing a trade (buy or sell).</summary>
public sealed record TradeCashFlow(
    DateTimeOffset Timestamp,
    decimal Amount,
    string Symbol,
    long Quantity,
    decimal Price) : CashFlowEntry(Timestamp, Amount);

/// <summary>Daily margin interest charge on a debit balance (negative amount = cash outflow).</summary>
/// <param name="MarginBalance">Balance on which interest was charged.</param>
/// <param name="AnnualRate">Annual rate (e.g. 0.05 for 5%).</param>
public sealed record MarginInterestCashFlow(
    DateTimeOffset Timestamp,
    decimal Amount,
    decimal MarginBalance,
    double AnnualRate) : CashFlowEntry(Timestamp, Amount);

/// <summary>Short-sale rebate received from the broker (positive amount = cash inflow).</summary>
public sealed record ShortRebateCashFlow(
    DateTimeOffset Timestamp,
    decimal Amount,
    string Symbol,
    long ShortShares,
    double AnnualRebateRate) : CashFlowEntry(Timestamp, Amount);

/// <summary>Brokerage commission paid on an order (negative amount).</summary>
public sealed record CommissionCashFlow(
    DateTimeOffset Timestamp,
    decimal Amount,
    string Symbol,
    Guid OrderId) : CashFlowEntry(Timestamp, Amount);

/// <summary>Dividend received on a long position (positive amount).</summary>
public sealed record DividendCashFlow(
    DateTimeOffset Timestamp,
    decimal Amount,
    string Symbol,
    long Shares,
    decimal DividendPerShare) : CashFlowEntry(Timestamp, Amount);
