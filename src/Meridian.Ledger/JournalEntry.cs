namespace Meridian.Ledger;

/// <summary>
/// A balanced group of <see cref="LedgerEntry"/> lines representing a single economic event.
/// Per double-entry accounting rules the sum of debits must equal the sum of credits
/// (<see cref="IsBalanced"/>).
/// </summary>
public sealed record JournalEntry
{
    /// <summary>Unique identifier for this journal entry.</summary>
    public Guid JournalEntryId { get; private init; }

    /// <summary>When the underlying economic event occurred (replay / simulated time).</summary>
    public DateTimeOffset Timestamp { get; private init; }

    /// <summary>Human-readable description of the economic event.</summary>
    public string Description { get; private init; }

    /// <summary>The individual debit/credit lines that make up this entry.</summary>
    public IReadOnlyList<LedgerEntry> Lines { get; private init; }

    /// <summary>
    /// Initializes a new <see cref="JournalEntry"/> with validation.
    /// </summary>
    /// <exception cref="LedgerValidationException">
    /// Thrown when <paramref name="description"/> is null or whitespace,
    /// or when <paramref name="lines"/> is null or empty.
    /// </exception>
    public JournalEntry(
        Guid journalEntryId,
        DateTimeOffset timestamp,
        string description,
        IReadOnlyList<LedgerEntry> lines)
    {
        if (string.IsNullOrWhiteSpace(description))
            throw new LedgerValidationException("Journal entry description must not be null or whitespace.");

        ArgumentNullException.ThrowIfNull(lines);

        if (lines.Count == 0)
            throw new LedgerValidationException("A journal entry must have at least one line.");

        JournalEntryId = journalEntryId;
        Timestamp = timestamp;
        Description = description;
        Lines = lines;
    }

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
