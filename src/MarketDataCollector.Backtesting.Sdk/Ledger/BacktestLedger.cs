namespace MarketDataCollector.Backtesting.Sdk;

/// <summary>
/// Double-entry accounting ledger for a backtest run.
/// Holds all <see cref="JournalEntry"/> records posted during replay and provides
/// account-balance queries and a trial-balance summary.
/// </summary>
/// <remarks>
/// <para>
/// Every economic event in the backtest (fill, commission, margin interest, etc.) is recorded as
/// a balanced journal entry: the sum of debits always equals the sum of credits.
/// </para>
/// <para>
/// Normal-balance rules followed here:
/// <list type="bullet">
///   <item><term>Asset / Expense</term><description>Debit-normal (debit increases, credit decreases).</description></item>
///   <item><term>Liability / Equity / Revenue</term><description>Credit-normal (credit increases, debit decreases).</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed class BacktestLedger
{
    private readonly List<JournalEntry> _journal = [];

    /// <summary>All journal entries in chronological posting order.</summary>
    public IReadOnlyList<JournalEntry> Journal => _journal;

    /// <summary>
    /// Posts a <see cref="JournalEntry"/> to the ledger.
    /// </summary>
    /// <param name="entry">The journal entry to post.</param>
    /// <exception cref="ArgumentException">Thrown when the entry is not balanced.</exception>
    public void Post(JournalEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (!entry.IsBalanced)
        {
            throw new ArgumentException(
                $"Journal entry '{entry.JournalEntryId}' is not balanced " +
                $"(debits={entry.Lines.Sum(l => l.Debit):F4}, credits={entry.Lines.Sum(l => l.Credit):F4}).",
                nameof(entry));
        }

        _journal.Add(entry);
    }

    /// <summary>Returns all individual ledger lines posted to <paramref name="account"/>.</summary>
    public IReadOnlyList<LedgerEntry> GetEntries(LedgerAccount account)
    {
        ArgumentNullException.ThrowIfNull(account);
        return _journal
            .SelectMany(j => j.Lines)
            .Where(l => l.Account == account)
            .ToList();
    }

    /// <summary>
    /// Returns the net balance for <paramref name="account"/> using normal-balance rules.
    /// Assets and expenses carry debit-normal balances (debits − credits).
    /// Liabilities, equity, and revenues carry credit-normal balances (credits − debits).
    /// </summary>
    public decimal GetBalance(LedgerAccount account)
    {
        ArgumentNullException.ThrowIfNull(account);
        var entries = GetEntries(account);
        var debits = entries.Sum(l => l.Debit);
        var credits = entries.Sum(l => l.Credit);
        return account.AccountType is LedgerAccountType.Asset or LedgerAccountType.Expense
            ? debits - credits
            : credits - debits;
    }

    /// <summary>
    /// Returns a trial balance mapping every account that has been posted to its net balance.
    /// If accounting is correct the sum of asset and expense balances equals the sum of liability,
    /// equity, and revenue balances (the accounting equation holds).
    /// </summary>
    public IReadOnlyDictionary<LedgerAccount, decimal> TrialBalance()
    {
        var accounts = _journal
            .SelectMany(j => j.Lines)
            .Select(l => l.Account)
            .Distinct()
            .ToList();

        return accounts.ToDictionary(a => a, GetBalance);
    }

    // ── Internal factory helpers ─────────────────────────────────────────────

    /// <summary>
    /// Creates a balanced <see cref="JournalEntry"/> from a list of (account, debit, credit) tuples
    /// and immediately posts it. All lines share the same journal entry ID and timestamp.
    /// </summary>
    internal void PostLines(
        DateTimeOffset timestamp,
        string description,
        IReadOnlyList<(LedgerAccount account, decimal debit, decimal credit)> lines)
    {
        var journalId = Guid.NewGuid();
        var entries = lines
            .Select(l => new LedgerEntry(Guid.NewGuid(), journalId, timestamp, l.account, l.debit, l.credit, description))
            .ToList();

        Post(new JournalEntry(journalId, timestamp, description, entries));
    }
}
