using MarketDataCollector.Contracts.Domain.Models;

namespace MarketDataCollector.ProviderSdk.Providers;

/// <summary>
/// Contract for historical/backfill data providers in the plugin system.
/// Implementations fetch OHLCV bar data for backtesting and gap-fill operations.
/// </summary>
/// <remarks>
/// This interface parallels the internal IHistoricalDataProvider but is designed
/// for the plugin boundary: it depends only on Contracts types, not on core internals.
/// Provider plugins implement this interface and register via <see cref="IProviderPlugin"/>.
/// </remarks>
public interface IHistoricalProvider : IProviderIdentity, IDisposable
{
    /// <summary>
    /// Minimum delay between API calls to respect rate limits.
    /// </summary>
    TimeSpan RateLimitDelay => TimeSpan.Zero;

    /// <summary>
    /// Maximum number of requests allowed per time window.
    /// </summary>
    int MaxRequestsPerWindow => int.MaxValue;

    /// <summary>
    /// Time window for rate limiting.
    /// </summary>
    TimeSpan RateLimitWindow => TimeSpan.FromHours(1);

    /// <summary>
    /// Check if the provider is currently available and healthy.
    /// </summary>
    Task<bool> IsAvailableAsync(CancellationToken ct = default) => Task.FromResult(true);

    /// <summary>
    /// Fetch daily OHLCV bars for a symbol within the specified date range.
    /// </summary>
    /// <param name="symbol">Ticker symbol (e.g., "SPY", "AAPL").</param>
    /// <param name="from">Start date (inclusive). Null for earliest available.</param>
    /// <param name="to">End date (inclusive). Null for latest available.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of historical bars ordered by date ascending.</returns>
    Task<IReadOnlyList<HistoricalBar>> GetDailyBarsAsync(
        string symbol,
        DateOnly? from,
        DateOnly? to,
        CancellationToken ct = default);

    /// <summary>
    /// Get extended bar data with adjustment information when supported.
    /// Default implementation converts standard bars to adjusted bars.
    /// </summary>
    async Task<IReadOnlyList<AdjustedBar>> GetAdjustedDailyBarsAsync(
        string symbol,
        DateOnly? from,
        DateOnly? to,
        CancellationToken ct = default)
    {
        var bars = await GetDailyBarsAsync(symbol, from, to, ct).ConfigureAwait(false);
        return bars.Select(b => new AdjustedBar(
            b.Symbol, b.SessionDate, b.Open, b.High, b.Low, b.Close, b.Volume, b.Source, b.SequenceNumber
        )).ToList();
    }
}

/// <summary>
/// Extended historical bar with adjustment factors and corporate action data.
/// </summary>
public sealed record AdjustedBar(
    string Symbol,
    DateOnly SessionDate,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    long Volume,
    string Source = "unknown",
    long SequenceNumber = 0,
    decimal? AdjustedOpen = null,
    decimal? AdjustedHigh = null,
    decimal? AdjustedLow = null,
    decimal? AdjustedClose = null,
    long? AdjustedVolume = null,
    decimal? SplitFactor = null,
    decimal? DividendAmount = null)
{
    /// <summary>
    /// Convert to standard HistoricalBar (uses adjusted values if available).
    /// </summary>
    public HistoricalBar ToHistoricalBar(bool preferAdjusted = true)
    {
        if (preferAdjusted && AdjustedClose.HasValue)
        {
            return new HistoricalBar(
                Symbol, SessionDate,
                AdjustedOpen ?? Open, AdjustedHigh ?? High,
                AdjustedLow ?? Low, AdjustedClose ?? Close,
                AdjustedVolume ?? Volume, Source, SequenceNumber);
        }

        return new HistoricalBar(Symbol, SessionDate, Open, High, Low, Close, Volume, Source, SequenceNumber);
    }
}
