using System.Linq;

namespace MarketDataCollector.Application.Backfill;

/// <summary>
/// Outcome of a single historical backfill run.
/// </summary>
public sealed record BackfillResult(
    bool Success,
    string Provider,
    string[] Symbols,
    DateOnly? From,
    DateOnly? To,
    long BarsWritten,
    DateTimeOffset StartedUtc,
    DateTimeOffset CompletedUtc,
    string? Error = null,
    IReadOnlyList<SymbolBackfillResult>? PerSymbolResults = null
)
{
    public static BackfillResult Failed(string provider, IReadOnlyList<string> symbols, DateOnly? from, DateOnly? to, DateTimeOffset started, Exception ex)
        => new(false, provider, symbols.ToArray(), from, to, 0, started, DateTimeOffset.UtcNow, ex.Message);
}

/// <summary>
/// Outcome for an individual symbol within a backfill run.
/// </summary>
public sealed record SymbolBackfillResult(
    string Symbol,
    bool Success,
    long BarsWritten,
    string? Error = null
);
