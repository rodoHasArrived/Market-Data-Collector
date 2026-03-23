using Meridian.Contracts.Ledger;
using Meridian.Contracts.SecurityMaster;

namespace Meridian.Ledger;

/// <summary>
/// Default chart-of-accounts rules for a standard single-account trading setup.
/// <para>
/// Account numbering follows US GAAP conventions:
/// <list type="bullet">
///   <item>1000–1999 Assets</item>
///   <item>2000–2999 Liabilities (2100 = ShortOptionObligation per instrument)</item>
///   <item>3000–3999 Equity</item>
///   <item>4000–4999 Revenue (4100 = OptionsRealisedPnl, 4200 = SyntheticLendingIncome)</item>
///   <item>6000–6999 Expenses (6100 = CommissionExpense, 6200 = SyntheticBorrowingExpense)</item>
/// </list>
/// </para>
/// <para>
/// Short option treatment: selling an option credits 2100 (Short Option Obligation),
/// NOT 4100 (Options Realised PnL). Revenue is only recognised when the obligation
/// is extinguished.
/// </para>
/// </summary>
public sealed class ChartOfAccountsService : IChartOfAccounts
{
    // ── Account code constants ────────────────────────────────────────────────

    private const string CashCode             = "1000";   // Asset
    private const string SecuritiesCodePrefix = "1100";   // Asset (1100.{symbol})
    private const string OptionLongCodePrefix = "1200";   // Asset (1200.{instrumentId})

    private const string ShortPayablePrefix   = "2000";   // Liability (2000.{symbol})
    private const string ShortOptionOblPrefix = "2100";   // Liability (2100.{instrumentId})

    private const string CapitalCode          = "3000";   // Equity

    private const string RealizedGainCode     = "4000";   // Revenue
    private const string OptionsRealisedPnlPfx= "4100";   // Revenue (4100.{instrumentId})
    private const string SyntheticLendingPfx  = "4200";   // Revenue (4200.{underlyingId})

    private const string CommissionCode       = "6100";   // Expense
    private const string MarginInterestCode   = "6110";   // Expense
    private const string SyntheticBorrowPfx   = "6200";   // Expense (6200.{underlyingId})
    private const string RealizedLossCode     = "6300";   // Expense

    // ── IChartOfAccounts ──────────────────────────────────────────────────────

    /// <inheritdoc/>
    public FillAccounts ResolveFillAccounts(FillKind fillKind, InstrumentId instrumentId, string? financialAccountId = null)
    {
        var id = instrumentId.ToString();
        return fillKind switch
        {
            // Buying shares: DR Securities, CR Cash
            FillKind.BuyLong => new FillAccounts
            {
                DebitAccount  = $"{SecuritiesCodePrefix}.{id}",
                CreditAccount = CashCode,
            },

            // Selling short equity: DR Cash (proceeds), CR ShortSecuritiesPayable (liability 2000)
            FillKind.SellShort => new FillAccounts
            {
                DebitAccount  = CashCode,
                CreditAccount = $"{ShortPayablePrefix}.{id}",
            },

            // Buying a long option: DR OptionLong (asset), CR Cash
            FillKind.BuyOption => new FillAccounts
            {
                DebitAccount  = $"{OptionLongCodePrefix}.{id}",
                CreditAccount = CashCode,
            },

            // *** CRITICAL CORRECTNESS ***
            // Selling (writing) an option: DR Cash (premium received), CR ShortOptionObligation (LIABILITY 2100)
            // The premium is NOT revenue — it is a liability until the obligation is extinguished.
            FillKind.SellOption => new FillAccounts
            {
                DebitAccount  = CashCode,
                CreditAccount = $"{ShortOptionOblPrefix}.{id}",
            },

            _ => throw new ArgumentOutOfRangeException(nameof(fillKind), fillKind, null),
        };
    }

    /// <inheritdoc/>
    public CloseAccounts ResolveCloseAccounts(CloseKind closeKind, InstrumentId instrumentId, string? financialAccountId = null)
    {
        var id = instrumentId.ToString();
        return closeKind switch
        {
            // Selling long equity: DR Cash (proceeds), CR Securities; P&L split to 4000/6300
            CloseKind.SellLong => new CloseAccounts
            {
                DebitAccount      = CashCode,
                CreditAccount     = $"{SecuritiesCodePrefix}.{id}",
                RealisedPnlAccount = RealizedGainCode, // caller chooses gain or loss side
            },

            // Covering a short equity: DR ShortSecuritiesPayable, CR Cash; P&L split
            CloseKind.BuyCoverShort => new CloseAccounts
            {
                DebitAccount      = $"{ShortPayablePrefix}.{id}",
                CreditAccount     = CashCode,
                RealisedPnlAccount = RealizedGainCode,
            },

            // Selling a long option: DR Cash, CR OptionLong; P&L split
            CloseKind.SellOption => new CloseAccounts
            {
                DebitAccount      = CashCode,
                CreditAccount     = $"{OptionLongCodePrefix}.{id}",
                RealisedPnlAccount = $"{OptionsRealisedPnlPfx}.{id}",
            },

            // *** CRITICAL CORRECTNESS ***
            // Buying back a short option: DR ShortOptionObligation (closes liability), CR Cash
            // The P&L account (4100) absorbs the difference between obligation and buyback cost.
            CloseKind.BuyBackShortOption => new CloseAccounts
            {
                DebitAccount      = $"{ShortOptionOblPrefix}.{id}",
                CreditAccount     = CashCode,
                RealisedPnlAccount = $"{OptionsRealisedPnlPfx}.{id}",
            },

            // *** CRITICAL CORRECTNESS ***
            // Short option expired worthless: DR ShortOptionObligation (closes liability), CR OptionsRealisedPnl
            // The ENTIRE premium now becomes income because no cash outflow is needed.
            CloseKind.ShortOptionExpiredWorthless => new CloseAccounts
            {
                DebitAccount      = $"{ShortOptionOblPrefix}.{id}",
                CreditAccount     = $"{OptionsRealisedPnlPfx}.{id}",
                RealisedPnlAccount = null, // no separate split needed — both sides already P&L
            },

            // Long option expired worthless: DR OptionsRealisedPnL (loss), CR OptionLong (removes asset)
            CloseKind.LongOptionExpiredWorthless => new CloseAccounts
            {
                DebitAccount      = $"{OptionsRealisedPnlPfx}.{id}",
                CreditAccount     = $"{OptionLongCodePrefix}.{id}",
                RealisedPnlAccount = null,
            },

            _ => throw new ArgumentOutOfRangeException(nameof(closeKind), closeKind, null),
        };
    }

    /// <inheritdoc/>
    public string ResolveCommissionAccount(string? financialAccountId = null) => CommissionCode;

    /// <inheritdoc/>
    public string ResolveMarginInterestAccount(string? financialAccountId = null) => MarginInterestCode;

    /// <inheritdoc/>
    public string ResolveSyntheticBorrowingExpenseAccount(InstrumentId underlyingId) =>
        $"{SyntheticBorrowPfx}.{underlyingId}";

    /// <inheritdoc/>
    public string ResolveSyntheticLendingIncomeAccount(InstrumentId underlyingId) =>
        $"{SyntheticLendingPfx}.{underlyingId}";
}
