namespace Meridian.Contracts.Ledger;

/// <summary>
/// Write surface for the double-entry ledger.
/// <para>
/// Every economic event flows through this interface as a balanced journal entry.
/// The implementation enforces that ΣDebits = ΣCredits before accepting any post.
/// </para>
/// <para>
/// Callers build a <see cref="LedgerEntryLine"/> list using <see cref="EntryDirection"/>
/// (never signed decimals) and pass them to <see cref="PostAsync"/>. The writer
/// validates balance and delegates to the underlying ledger store.
/// </para>
/// </summary>
public interface ILedgerWriter
{
    /// <summary>
    /// Post a balanced journal entry to the ledger.
    /// </summary>
    /// <param name="description">Human-readable description of the economic event.</param>
    /// <param name="timestamp">When the economic event occurred.</param>
    /// <param name="lines">The debit and credit lines. Must balance (ΣDebits = ΣCredits).</param>
    /// <param name="metadata">Optional correlation metadata (fill ID, order ID, etc.).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The ID of the posted journal entry.</returns>
    /// <exception cref="LedgerImbalanceException">
    /// Thrown when ΣDebits ≠ ΣCredits in the supplied lines.
    /// </exception>
    ValueTask<Guid> PostAsync(
        string description,
        DateTimeOffset timestamp,
        IReadOnlyList<LedgerEntryLine> lines,
        LedgerPostMetadata? metadata = null,
        CancellationToken ct = default);
}

/// <summary>
/// The direction of a ledger entry line.
/// Amounts are always non-negative; this enum carries the sign semantics.
/// </summary>
public enum EntryDirection
{
    /// <summary>
    /// Increases asset and expense accounts; decreases liability, equity, and revenue accounts.
    /// </summary>
    Debit = 1,

    /// <summary>
    /// Increases liability, equity, and revenue accounts; decreases asset and expense accounts.
    /// </summary>
    Credit = 2,
}

/// <summary>
/// A single debit or credit line in a journal entry.
/// </summary>
public sealed record LedgerEntryLine
{
    /// <summary>The account being debited or credited.</summary>
    public string AccountCode { get; init; } = "";

    /// <summary>The direction of this line.</summary>
    public EntryDirection Direction { get; init; }

    /// <summary>Non-negative amount for this line.</summary>
    public decimal Amount { get; init; }

    /// <summary>Optional per-line memo.</summary>
    public string? Memo { get; init; }

    /// <summary>Create a debit line.</summary>
    public static LedgerEntryLine Debit(string accountCode, decimal amount, string? memo = null) =>
        new() { AccountCode = accountCode, Direction = EntryDirection.Debit, Amount = amount, Memo = memo };

    /// <summary>Create a credit line.</summary>
    public static LedgerEntryLine Credit(string accountCode, decimal amount, string? memo = null) =>
        new() { AccountCode = accountCode, Direction = EntryDirection.Credit, Amount = amount, Memo = memo };
}

/// <summary>Correlation metadata attached to a journal entry for audit tracing.</summary>
public sealed record LedgerPostMetadata
{
    /// <summary>Strategy run that generated this entry.</summary>
    public string? StrategyRunId { get; init; }

    /// <summary>Order ID that triggered this entry.</summary>
    public string? OrderId { get; init; }

    /// <summary>Fill ID that triggered this entry.</summary>
    public string? FillId { get; init; }

    /// <summary>Correlation ID for distributed tracing.</summary>
    public string? CorrelationId { get; init; }
}

/// <summary>
/// Thrown when a journal entry's debits and credits do not balance.
/// </summary>
public sealed class LedgerImbalanceException(string message) : Exception(message);
