namespace MarketDataCollector.Ledger;

/// <summary>
/// One line in a <see cref="JournalEntry"/> representing a debit or credit to a specific account.
/// Exactly one of <see cref="Debit"/> or <see cref="Credit"/> should be non-zero per line.
/// </summary>
public sealed record LedgerEntry(
    Guid EntryId,
    Guid JournalEntryId,
    DateTimeOffset Timestamp,
    LedgerAccount Account,
    decimal Debit,
    decimal Credit,
    string Description);
