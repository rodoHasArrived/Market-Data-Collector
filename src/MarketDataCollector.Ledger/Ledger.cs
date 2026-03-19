namespace MarketDataCollector.Ledger;

/// <summary>
/// Double-entry accounting ledger.
/// Holds all <see cref="JournalEntry"/> records posted during a run and provides
/// account-balance queries and a trial-balance summary.
/// </summary>
/// <remarks>
/// <para>
/// Every economic event (fill, commission, margin interest, etc.) is recorded as
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
public sealed class Ledger : IReadOnlyLedger
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

        if (string.IsNullOrWhiteSpace(entry.Description))
            throw new LedgerValidationException("Journal entry description must not be null or whitespace.");

        if (entry.Lines is null || entry.Lines.Count == 0)
            throw new LedgerValidationException("Journal entry must have at least one line.");

        if (!entry.IsBalanced)
        {
            var totalDebit = entry.Lines.Sum(l => l.Debit);
            var totalCredit = entry.Lines.Sum(l => l.Credit);
            throw new LedgerValidationException(
                $"Journal entry '{entry.JournalEntryId}' is not balanced " +
                $"(debits={totalDebit:F4}, credits={totalCredit:F4}).");
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
    /// Computed in a single pass over the journal to avoid O(A×N) scans.
    /// </summary>
    public IReadOnlyDictionary<LedgerAccount, decimal> TrialBalance()
    {
        // Aggregate debits and credits per account in one pass.
        var aggregates = new Dictionary<LedgerAccount, (decimal Debits, decimal Credits)>();

        foreach (var journalEntry in _journal)
        {
            foreach (var line in journalEntry.Lines)
            {
                if (!aggregates.TryGetValue(line.Account, out var totals))
                    totals = (0m, 0m);

                aggregates[line.Account] = (totals.Debits + line.Debit, totals.Credits + line.Credit);
            }
        }

        var result = new Dictionary<LedgerAccount, decimal>(aggregates.Count);
        foreach (var (account, (debits, credits)) in aggregates)
        {
            result[account] = account.AccountType is LedgerAccountType.Asset or LedgerAccountType.Expense
                ? debits - credits
                : credits - debits;
        }

        return result;
    }

    // ── Internal factory helpers ─────────────────────────────────────────────

    /// <summary>
    /// Creates a balanced <see cref="JournalEntry"/> from a list of (account, debit, credit) tuples
    /// and immediately posts it. All lines share the same journal entry ID and timestamp.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="lines"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="description"/> is null or whitespace, or when <paramref name="lines"/> is empty.
    /// </exception>
    public void PostLines(
        DateTimeOffset timestamp,
        string description,
        IReadOnlyList<(LedgerAccount account, decimal debit, decimal credit)> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Journal entry description must not be null or whitespace.", nameof(description));
        if (lines.Count == 0)
            throw new ArgumentException("A journal entry must have at least one line.", nameof(lines));

        var journalId = Guid.NewGuid();
        var entries = lines
            .Select(l => new LedgerEntry(Guid.NewGuid(), journalId, timestamp, l.account, l.debit, l.credit, description))
            .ToList();

        Post(new JournalEntry(journalId, timestamp, description, entries));
    }
}
