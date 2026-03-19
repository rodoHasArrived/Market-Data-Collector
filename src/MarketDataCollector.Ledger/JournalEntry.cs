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
    /// <summary>
    /// Tolerance used when comparing total debits to total credits.
    /// Prevents false negatives caused by separate rounding paths.
    /// </summary>
    private const decimal BalanceTolerance = 0.000001m;

    /// <summary>
    /// Returns <c>true</c> when the total debits approximately equal the total credits
    /// (within <see cref="BalanceTolerance"/>).
    /// </summary>
    public bool IsBalanced
    {
        get
        {
            var totalDebit = 0m;
            var totalCredit = 0m;
            foreach (var line in Lines)
            {
                totalDebit += line.Debit;
                totalCredit += line.Credit;
            }

            return Math.Abs(totalDebit - totalCredit) <= BalanceTolerance;
        }
    }
}
