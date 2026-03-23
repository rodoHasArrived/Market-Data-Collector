using Meridian.Contracts.SecurityMaster;

namespace Meridian.Contracts.Ledger;

/// <summary>
/// Resolves the correct ledger accounts for any trading event.
/// <para>
/// This is the single place where accounting rules are encoded.
/// The chart of accounts determines WHERE in the ledger each economic event lands.
/// </para>
/// <para>
/// Critical correctness rules encoded here:
/// <list type="bullet">
///   <item>
///     Selling an option (opening a short) credits account 2100 (ShortOptionObligation),
///     NOT a revenue account. The premium received is a LIABILITY until the obligation
///     is extinguished.
///   </item>
///   <item>
///     Revenue (Options Realised PnL, account 4100) is only recognised when the
///     short option obligation is closed — by buyback, expiry, or assignment.
///   </item>
///   <item>
///     Short equity positions credit ShortSecuritiesPayable (2000), not revenue.
///   </item>
/// </list>
/// </para>
/// </summary>
public interface IChartOfAccounts
{
    /// <summary>
    /// Resolve the accounts for opening a position (a buy or sell fill that opens a new position).
    /// </summary>
    FillAccounts ResolveFillAccounts(FillKind fillKind, InstrumentId instrumentId, string? financialAccountId = null);

    /// <summary>
    /// Resolve the accounts for closing a position.
    /// </summary>
    CloseAccounts ResolveCloseAccounts(CloseKind closeKind, InstrumentId instrumentId, string? financialAccountId = null);

    /// <summary>
    /// Resolve the account for commission expense.
    /// </summary>
    string ResolveCommissionAccount(string? financialAccountId = null);

    /// <summary>
    /// Resolve the account for margin interest expense.
    /// </summary>
    string ResolveMarginInterestAccount(string? financialAccountId = null);

    /// <summary>
    /// Resolve the account for synthetic borrowing expense (from box spreads).
    /// </summary>
    string ResolveSyntheticBorrowingExpenseAccount(InstrumentId underlyingId);

    /// <summary>
    /// Resolve the account for synthetic lending income (from box spreads where rate is below margin rate).
    /// </summary>
    string ResolveSyntheticLendingIncomeAccount(InstrumentId underlyingId);
}

/// <summary>Categorises the kind of fill for account resolution.</summary>
public enum FillKind
{
    /// <summary>Buying shares/units to open or add to a long position.</summary>
    BuyLong = 1,

    /// <summary>Selling shares/units short (proceeds received, obligation created).</summary>
    SellShort = 2,

    /// <summary>Buying an option to open a long option position.</summary>
    BuyOption = 3,

    /// <summary>Selling (writing) an option to open a short option position.</summary>
    SellOption = 4,
}

/// <summary>Categorises the kind of close for account resolution.</summary>
public enum CloseKind
{
    /// <summary>Selling shares/units to close a long equity position (realise gain/loss).</summary>
    SellLong = 1,

    /// <summary>Buying to cover a short equity position (realise gain/loss).</summary>
    BuyCoverShort = 2,

    /// <summary>Selling an option to close a long option position.</summary>
    SellOption = 3,

    /// <summary>Buying back an option to close a short option position.</summary>
    BuyBackShortOption = 4,

    /// <summary>A short option expired worthless — obligation extinguished, full premium becomes income.</summary>
    ShortOptionExpiredWorthless = 5,

    /// <summary>A long option expired worthless — the premium paid becomes a loss.</summary>
    LongOptionExpiredWorthless = 6,
}

/// <summary>
/// The ledger accounts for opening a position fill.
/// </summary>
public sealed record FillAccounts
{
    /// <summary>Account to debit for the fill (e.g., Securities for a long buy).</summary>
    public string DebitAccount { get; init; } = "";

    /// <summary>Account to credit for the fill (e.g., Cash for a long buy).</summary>
    public string CreditAccount { get; init; } = "";
}

/// <summary>
/// The ledger accounts for closing a position.
/// </summary>
public sealed record CloseAccounts
{
    /// <summary>Account to debit for the close (removes the asset or liability).</summary>
    public string DebitAccount { get; init; } = "";

    /// <summary>Account to credit for the close (removes the asset or liability).</summary>
    public string CreditAccount { get; init; } = "";

    /// <summary>
    /// Account for the realised gain/loss. If null, no separate P&amp;L entry is needed
    /// (the two sides balance without a P&amp;L split).
    /// </summary>
    public string? RealisedPnlAccount { get; init; }
}
