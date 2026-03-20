namespace Meridian.Backtesting.Sdk;

/// <summary>Parameters for a single backtest run.</summary>
/// <param name="From">Inclusive start date.</param>
/// <param name="To">Inclusive end date.</param>
/// <param name="Symbols">Symbols to restrict to; <c>null</c> means use the entire discovered universe.</param>
/// <param name="InitialCash">Starting cash balance in USD.</param>
/// <param name="AnnualMarginRate">Annual interest rate charged on margin debit balances (e.g. 0.05 = 5%).</param>
/// <param name="AnnualShortRebateRate">Annual rebate rate received on short-sale proceeds (e.g. 0.02 = 2%).</param>
/// <param name="DataRoot">Root directory of the locally-collected JSONL data.</param>
/// <param name="StrategyAssemblyPath">
/// Optional path to a compiled strategy .dll; <c>null</c> means the strategy instance is supplied
/// directly to <c>BacktestEngine.RunAsync</c>.
/// </param>
public sealed record BacktestRequest(
    DateOnly From,
    DateOnly To,
    IReadOnlyList<string>? Symbols = null,
    decimal InitialCash = 100_000m,
    double AnnualMarginRate = 0.05,
    double AnnualShortRebateRate = 0.02,
    string DataRoot = "./data",
    string? StrategyAssemblyPath = null);
