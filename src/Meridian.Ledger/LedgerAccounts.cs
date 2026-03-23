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

    /// <summary>Interest income credited to positive idle cash balances.</summary>
    public static readonly LedgerAccount CashInterestIncome =
        new("Cash Interest Income", LedgerAccountType.Revenue);

    /// <summary>Short-sale rebate income credited daily by the broker on short positions.</summary>
    public static readonly LedgerAccount ShortRebateIncome =
        new("Short Rebate Income", LedgerAccountType.Revenue);

    /// <summary>Dividend income received on long positions.</summary>
    public static readonly LedgerAccount DividendIncome =
        new("Dividend Income", LedgerAccountType.Revenue);

    public static LedgerAccount CashAccount(string financialAccountId) =>
        CreateScoped("Cash", LedgerAccountType.Asset, financialAccountId);

    public static LedgerAccount CapitalAccountFor(string financialAccountId) =>
        CreateScoped("Capital Account", LedgerAccountType.Equity, financialAccountId);

    public static LedgerAccount RealizedGainFor(string financialAccountId) =>
        CreateScoped("Realized Gain", LedgerAccountType.Revenue, financialAccountId);

    public static LedgerAccount RealizedLossFor(string financialAccountId) =>
        CreateScoped("Realized Loss", LedgerAccountType.Expense, financialAccountId);

    public static LedgerAccount CommissionExpenseFor(string financialAccountId) =>
        CreateScoped("Commission Expense", LedgerAccountType.Expense, financialAccountId);

    public static LedgerAccount MarginInterestExpenseFor(string financialAccountId) =>
        CreateScoped("Margin Interest Expense", LedgerAccountType.Expense, financialAccountId);

    public static LedgerAccount CashInterestIncomeFor(string financialAccountId) =>
        CreateScoped("Cash Interest Income", LedgerAccountType.Revenue, financialAccountId);

    public static LedgerAccount ShortRebateIncomeFor(string financialAccountId) =>
        CreateScoped("Short Rebate Income", LedgerAccountType.Revenue, financialAccountId);

    public static LedgerAccount DividendIncomeFor(string financialAccountId) =>
        CreateScoped("Dividend Income", LedgerAccountType.Revenue, financialAccountId);
    /// <summary>Dividend expense owed on short positions or other negative dividend adjustments.</summary>
    public static readonly LedgerAccount DividendExpense =
        new("Dividend Expense", LedgerAccountType.Expense);

    /// <summary>Bond or fund coupon income received on held units.</summary>
    public static readonly LedgerAccount CouponIncome =
        new("Coupon Income", LedgerAccountType.Revenue);

    /// <summary>Coupon expense owed on short positions or negative coupon adjustments.</summary>
    public static readonly LedgerAccount CouponExpense =
        new("Coupon Expense", LedgerAccountType.Expense);

    /// <summary>Income from non-dividend corporate actions, merger cash, and miscellaneous asset events.</summary>
    public static readonly LedgerAccount CorporateActionIncome =
        new("Corporate Action Income", LedgerAccountType.Revenue);

    /// <summary>Expense from fees, merger charges, and other negative asset-event cash adjustments.</summary>
    public static readonly LedgerAccount CorporateActionExpense =
        new("Corporate Action Expense", LedgerAccountType.Expense);

    /// <summary>
    /// Returns the asset account representing equity holdings in <paramref name="symbol"/>.
    /// Each symbol has its own securities account so per-symbol cost-basis is tracked separately.
    /// The symbol is normalized to upper-case so accounts are case-insensitive by identity.
    /// </summary>
    public static LedgerAccount Securities(string symbol, string? financialAccountId = null)
    {
        var normalizedSymbol = NormalizeSymbol(symbol);
        return new("Securities", LedgerAccountType.Asset, normalizedSymbol, NormalizeOptionalAccountId(financialAccountId));
    }

    // ── Options-specific accounts ─────────────────────────────────────────────

    /// <summary>
    /// Liability account for a short option obligation on a specific instrument.
    /// When an option is written (sold), the premium received is credited here — NOT to revenue.
    /// Revenue is only recognised when the obligation is extinguished (buyback / expiry / assignment).
    /// Account code: 2100.{instrumentId}
    /// </summary>
    public static LedgerAccount ShortOptionObligation(string instrumentId, string? financialAccountId = null) =>
        new("Short Option Obligation", LedgerAccountType.Liability,
            NormalizeSymbol(instrumentId), NormalizeOptionalAccountId(financialAccountId));

    /// <summary>
    /// Realised profit-and-loss account for options positions.
    /// Credited when a short option obligation is extinguished at a profit;
    /// debited when extinguished at a loss.
    /// Account code: 4100.{instrumentId}
    /// </summary>
    public static LedgerAccount OptionsRealisedPnl(string instrumentId, string? financialAccountId = null) =>
        new("Options Realised P&amp;L", LedgerAccountType.Revenue,
            NormalizeSymbol(instrumentId), NormalizeOptionalAccountId(financialAccountId));

    /// <summary>
    /// Expense account for implied borrowing cost via a box spread on the given underlying.
    /// Debited when the implied box spread rate exceeds the risk-free rate (you are paying
    /// more than the market rate to borrow).
    /// Account code: 6200.{underlyingId}
    /// </summary>
    public static LedgerAccount SyntheticBorrowingExpense(string underlyingId, string? financialAccountId = null) =>
        new("Synthetic Borrowing Expense", LedgerAccountType.Expense,
            NormalizeSymbol(underlyingId), NormalizeOptionalAccountId(financialAccountId));

    /// <summary>
    /// Revenue account for implied lending income via a box spread on the given underlying.
    /// Credited when the implied box spread rate is below the margin rate (the box is cheaper
    /// than margin borrowing — you are effectively lending at above-market rates by holding the box).
    /// Account code: 4200.{underlyingId}
    /// </summary>
    public static LedgerAccount SyntheticLendingIncome(string underlyingId, string? financialAccountId = null) =>
        new("Synthetic Lending Income", LedgerAccountType.Revenue,
            NormalizeSymbol(underlyingId), NormalizeOptionalAccountId(financialAccountId));

    // ── Equity long/short ─────────────────────────────────────────────────────

    /// <summary>
    /// Returns the liability account representing the obligation to return borrowed shares for
    /// a short position in <paramref name="symbol"/>.
    /// Each symbol has its own short payable account. The symbol is normalized to upper-case.
    /// </summary>
    public static LedgerAccount ShortSecuritiesPayable(string symbol, string? financialAccountId = null)
    {
        var normalizedSymbol = NormalizeSymbol(symbol);
        return new("Short Securities Payable", LedgerAccountType.Liability, normalizedSymbol, NormalizeOptionalAccountId(financialAccountId));
    }

    private static LedgerAccount CreateScoped(string name, LedgerAccountType accountType, string financialAccountId)
        => new(name, accountType, FinancialAccountId: NormalizeAccountId(financialAccountId));

    private static string NormalizeSymbol(string symbol)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        return symbol.Trim().ToUpperInvariant();
    }

    private static string NormalizeAccountId(string? financialAccountId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(financialAccountId);
        return financialAccountId.Trim();
    }

    private static string? NormalizeOptionalAccountId(string? financialAccountId)
        => string.IsNullOrWhiteSpace(financialAccountId) ? null : financialAccountId.Trim();
}
