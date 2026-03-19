namespace MarketDataCollector.Ledger;

/// <summary>
/// A balanced group of <see cref="LedgerEntry"/> lines representing a single economic event.
/// Per double-entry accounting rules the sum of debits must equal the sum of credits
/// (<see cref="IsBalanced"/>).
/// </summary>
public sealed record JournalEntry(
    Guid JournalEntryId,
    DateTimeOffset Timestamp,
    string Description,
    IReadOnlyList<LedgerEntry> Lines)
{
    /// <summary>Returns <c>true</c> when the total debits equal the total credits.</summary>
    public bool IsBalanced =>
        Lines.Sum(l => l.Debit) == Lines.Sum(l => l.Credit);
}
