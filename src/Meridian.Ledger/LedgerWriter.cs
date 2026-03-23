using Meridian.Contracts.Ledger;

namespace Meridian.Ledger;

/// <summary>
/// Async write surface over the in-memory <see cref="Ledger"/>.
/// <para>
/// Enforces ΣDebits = ΣCredits before accepting any entry, then delegates
/// to the synchronous <see cref="Ledger.Post"/> method.
/// </para>
/// </summary>
public sealed class LedgerWriter : ILedgerWriter
{
    private const decimal BalanceTolerance = 0.000001m;

    private readonly Ledger _ledger;

    public LedgerWriter(Ledger ledger)
    {
        ArgumentNullException.ThrowIfNull(ledger);
        _ledger = ledger;
    }

    /// <inheritdoc/>
    public ValueTask<Guid> PostAsync(
        string description,
        DateTimeOffset timestamp,
        IReadOnlyList<LedgerEntryLine> lines,
        LedgerPostMetadata? metadata = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(lines);

        EnforceBalance(lines);

        var journalEntryId = Guid.NewGuid();
        var journalLines = lines.Select(line => new LedgerEntry(
            entryId: Guid.NewGuid(),
            journalEntryId: journalEntryId,
            timestamp: timestamp,
            account: AccountCodeToLedgerAccount(line.AccountCode),
            debit: line.Direction == EntryDirection.Debit ? line.Amount : 0m,
            credit: line.Direction == EntryDirection.Credit ? line.Amount : 0m,
            description: description
        )).ToList();

        var entry = new JournalEntry(
            journalEntryId: journalEntryId,
            timestamp: timestamp,
            description: description,
            lines: journalLines,
            metadata: metadata is null ? null : BuildMetadata(metadata)
        );

        _ledger.Post(entry);
        return ValueTask.FromResult(journalEntryId);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static void EnforceBalance(IReadOnlyList<LedgerEntryLine> lines)
    {
        var totalDebit = 0m;
        var totalCredit = 0m;

        foreach (var line in lines)
        {
            if (line.Amount < 0m)
                throw new LedgerImbalanceException($"Line amount cannot be negative (account: {line.AccountCode}, amount: {line.Amount}).");

            if (line.Direction == EntryDirection.Debit)
                totalDebit += line.Amount;
            else
                totalCredit += line.Amount;
        }

        if (Math.Abs(totalDebit - totalCredit) > BalanceTolerance)
            throw new LedgerImbalanceException(
                $"Journal entry does not balance: total debits = {totalDebit:F6}, total credits = {totalCredit:F6}, " +
                $"difference = {Math.Abs(totalDebit - totalCredit):F6}.");
    }

    private static LedgerAccount AccountCodeToLedgerAccount(string accountCode)
    {
        // Account codes are stored verbatim as the Name field in LedgerAccount.
        // The account type is inferred from the account code prefix (1xxx = Asset, etc.).
        var accountType = AccountCodeToType(accountCode);
        return new LedgerAccount(accountCode, accountType);
    }

    private static LedgerAccountType AccountCodeToType(string accountCode)
    {
        // Standard account code ranges (US GAAP numbering convention):
        // 1000-1999: Assets
        // 2000-2999: Liabilities
        // 3000-3999: Equity
        // 4000-4999: Revenue
        // 5000-5999: Cost of Goods Sold / Direct Costs
        // 6000-6999: Operating Expenses
        if (accountCode.Length >= 4 && int.TryParse(accountCode[..4], out var prefix))
        {
            return prefix switch
            {
                >= 1000 and <= 1999 => LedgerAccountType.Asset,
                >= 2000 and <= 2999 => LedgerAccountType.Liability,
                >= 3000 and <= 3999 => LedgerAccountType.Equity,
                >= 4000 and <= 4999 => LedgerAccountType.Revenue,
                >= 5000 and <= 6999 => LedgerAccountType.Expense,
                _ => LedgerAccountType.Asset, // fallback
            };
        }
        return LedgerAccountType.Asset; // fallback for non-numeric codes
    }

    private static JournalEntryMetadata? BuildMetadata(LedgerPostMetadata metadata)
    {
        Guid? orderId = metadata.OrderId is not null && Guid.TryParse(metadata.OrderId, out var g) ? g : null;
        Guid? fillId  = metadata.FillId is not null && Guid.TryParse(metadata.FillId, out var f)  ? f : null;

        return new JournalEntryMetadata(
            StrategyId: metadata.StrategyRunId,
            OrderId: orderId,
            FillId: fillId,
            Tags: metadata.CorrelationId is not null
                ? new Dictionary<string, string> { ["correlationId"] = metadata.CorrelationId }
                : null
        );
    }
}
