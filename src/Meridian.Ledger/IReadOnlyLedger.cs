namespace Meridian.Ledger;

/// <summary>
/// Read-only view of a double-entry accounting ledger.
/// Exposes query operations only; does not allow posting new entries.
/// Strategies and result consumers should receive this interface to prevent
/// accidental or malicious mutation of the ledger used for auditing.
/// </summary>
public interface IReadOnlyLedger
{
    /// <summary>All journal entries in chronological posting order.</summary>
    IReadOnlyList<JournalEntry> Journal { get; }

    /// <summary>Returns all individual ledger lines posted to <paramref name="account"/>.</summary>
    IReadOnlyList<LedgerEntry> GetEntries(LedgerAccount account);

    /// <summary>
    /// Returns the net balance for <paramref name="account"/> using normal-balance rules.
    /// Assets and expenses carry debit-normal balances (debits − credits).
    /// Liabilities, equity, and revenues carry credit-normal balances (credits − debits).
    /// </summary>
    decimal GetBalance(LedgerAccount account);

    /// <summary>
    /// Returns a trial balance mapping every account that has been posted to its net balance.
    /// </summary>
    IReadOnlyDictionary<LedgerAccount, decimal> TrialBalance();
}
