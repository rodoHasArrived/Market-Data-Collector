namespace Meridian.Ledger;

/// <summary>
/// Well-known chart-of-accounts entries used in double-entry bookkeeping.
/// </summary>
public static class LedgerAccounts
{
    /// <summary>Simulated cash held in the trading account.</summary>
    public static readonly LedgerAccount Cash =
        new("Cash", LedgerAccountType.Asset);

    /// <summary>Initial capital deposited into the account at the start of the run.</summary>
    public static readonly LedgerAccount CapitalAccount =
        new("Capital Account", LedgerAccountType.Equity);

    /// <summary>Realized gain on trades where proceeds exceed cost basis.</summary>
    public static readonly LedgerAccount RealizedGain =
        new("Realized Gain", LedgerAccountType.Revenue);

    /// <summary>Realized loss on trades where cost basis exceeds proceeds.</summary>
    public static readonly LedgerAccount RealizedLoss =
        new("Realized Loss", LedgerAccountType.Expense);

    /// <summary>Brokerage commission expense charged on each order fill.</summary>
    public static readonly LedgerAccount CommissionExpense =
        new("Commission Expense", LedgerAccountType.Expense);

    /// <summary>Margin interest expense charged daily on debit (borrowed) balances.</summary>
    public static readonly LedgerAccount MarginInterestExpense =
        new("Margin Interest Expense", LedgerAccountType.Expense);

    /// <summary>Short-sale rebate income credited daily by the broker on short positions.</summary>
    public static readonly LedgerAccount ShortRebateIncome =
        new("Short Rebate Income", LedgerAccountType.Revenue);

    /// <summary>Dividend income received on long positions.</summary>
    public static readonly LedgerAccount DividendIncome =
        new("Dividend Income", LedgerAccountType.Revenue);

    /// <summary>
    /// Returns the asset account representing equity holdings in <paramref name="symbol"/>.
    /// Each symbol has its own securities account so per-symbol cost-basis is tracked separately.
    /// The symbol is normalized to upper-case so accounts are case-insensitive by identity.
    /// </summary>
    public static LedgerAccount Securities(string symbol)
    {
        var normalizedSymbol = symbol.ToUpperInvariant();
        return new("Securities", LedgerAccountType.Asset, normalizedSymbol);
    }

    /// <summary>
    /// Returns the liability account representing the obligation to return borrowed shares for
    /// a short position in <paramref name="symbol"/>.
    /// Each symbol has its own short payable account. The symbol is normalized to upper-case.
    /// </summary>
    public static LedgerAccount ShortSecuritiesPayable(string symbol)
    {
        var normalizedSymbol = symbol.ToUpperInvariant();
        return new("Short Securities Payable", LedgerAccountType.Liability, normalizedSymbol);
    }
}
