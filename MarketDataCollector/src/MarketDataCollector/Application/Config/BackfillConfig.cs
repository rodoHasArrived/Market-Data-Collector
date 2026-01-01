namespace MarketDataCollector.Application.Config;

/// <summary>
/// Configuration for historical backfill operations.
/// </summary>
/// <param name="Enabled">When true, the collector will run a backfill instead of live collection.</param>
/// <param name="Provider">Historical data provider to use (e.g. "stooq").</param>
/// <param name="Symbols">Symbols to backfill; defaults to configured live symbols.</param>
/// <param name="From">Optional inclusive start date (UTC).</param>
/// <param name="To">Optional inclusive end date (UTC).</param>
public sealed record BackfillConfig(
    bool Enabled = false,
    string Provider = "stooq",
    string[]? Symbols = null,
    DateOnly? From = null,
    DateOnly? To = null
);
