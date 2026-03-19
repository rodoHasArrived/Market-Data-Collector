namespace MarketDataCollector.Ledger;

/// <summary>
/// Named ledger account in the chart of accounts.
/// For per-symbol securities accounts, <see cref="Symbol"/> identifies the underlying instrument.
/// </summary>
public sealed record LedgerAccount(
    string Name,
    LedgerAccountType AccountType,
    string? Symbol = null)
{
    /// <inheritdoc/>
    public override string ToString() => Symbol is null ? Name : $"{Name} ({Symbol})";
}
