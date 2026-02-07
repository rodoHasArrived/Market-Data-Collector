using System.Linq;
using MarketDataCollector.Application.Config;

namespace MarketDataCollector.Application.Backfill;

/// <summary>
/// Incoming request describing a historical backfill.
/// </summary>
public sealed record BackfillRequest(
    string Provider,
    IReadOnlyList<string> Symbols,
    DateOnly? From = null,
    DateOnly? To = null
)
{
    public static BackfillRequest FromConfig(AppConfig cfg)
    {
        var defaults = cfg.Backfill;
        var symbols = (defaults?.Symbols?.Length > 0
            ? defaults.Symbols
            : cfg.Symbols?.Select(s => s.Symbol).ToArray()) ?? Array.Empty<string>();

        return new BackfillRequest(
            defaults?.Provider ?? "stooq",
            symbols,
            defaults?.From,
            defaults?.To);
    }
}
