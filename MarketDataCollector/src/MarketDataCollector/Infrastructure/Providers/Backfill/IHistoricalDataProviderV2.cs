using MarketDataCollector.Domain.Models;

namespace MarketDataCollector.Infrastructure.Providers.Backfill;

/// <summary>
/// Enhanced contract for historical data providers with support for
/// fallback chains, rate limiting, and capability discovery.
/// </summary>
public interface IHistoricalDataProviderV2 : IHistoricalDataProvider
{
    /// <summary>
    /// Priority for fallback ordering (lower = higher priority, tried first).
    /// </summary>
    int Priority { get; }

    /// <summary>
    /// Minimum delay between API calls to respect rate limits.
    /// </summary>
    TimeSpan RateLimitDelay { get; }

    /// <summary>
    /// Maximum number of requests allowed per time window.
    /// </summary>
    int MaxRequestsPerWindow { get; }

    /// <summary>
    /// Time window for rate limiting (e.g., 1 minute, 1 hour).
    /// </summary>
    TimeSpan RateLimitWindow { get; }

    /// <summary>
    /// Whether this provider returns split/dividend adjusted prices.
    /// </summary>
    bool SupportsAdjustedPrices { get; }

    /// <summary>
    /// Whether this provider supports intraday bar data.
    /// </summary>
    bool SupportsIntraday { get; }

    /// <summary>
    /// Whether this provider includes dividend data.
    /// </summary>
    bool SupportsDividends { get; }

    /// <summary>
    /// Whether this provider includes split data.
    /// </summary>
    bool SupportsSplits { get; }

    /// <summary>
    /// Market regions/countries supported (e.g., "US", "UK", "DE").
    /// </summary>
    IReadOnlyList<string> SupportedMarkets { get; }

    /// <summary>
    /// Check if the provider is currently available and healthy.
    /// </summary>
    Task<bool> IsAvailableAsync(CancellationToken ct = default);

    /// <summary>
    /// Get extended bar data with adjustment information when supported.
    /// Falls back to GetDailyBarsAsync if not overridden.
    /// </summary>
    Task<IReadOnlyList<AdjustedHistoricalBar>> GetAdjustedDailyBarsAsync(
        string symbol,
        DateOnly? from,
        DateOnly? to,
        CancellationToken ct = default);
}

/// <summary>
/// Optional interface for providers that can report their rate limit status.
/// </summary>
public interface IRateLimitAwareProvider
{
    /// <summary>
    /// Get current rate limit usage information.
    /// </summary>
    RateLimitInfo GetRateLimitInfo();

    /// <summary>
    /// Event raised when the provider hits a rate limit.
    /// </summary>
    event Action<RateLimitInfo>? OnRateLimitHit;
}

/// <summary>
/// Information about a provider's current rate limit status.
/// </summary>
public sealed record RateLimitInfo(
    string ProviderName,
    int RequestsMade,
    int MaxRequests,
    TimeSpan Window,
    DateTimeOffset? ResetsAt = null,
    bool IsLimited = false,
    TimeSpan? RetryAfter = null
)
{
    public int RemainingRequests => Math.Max(0, MaxRequests - RequestsMade);
    public double UsageRatio => MaxRequests > 0 ? (double)RequestsMade / MaxRequests : 0;
    public TimeSpan? TimeUntilReset => ResetsAt.HasValue ? ResetsAt.Value - DateTimeOffset.UtcNow : null;
}

/// <summary>
/// Extended historical bar with adjustment factors and corporate action data.
/// </summary>
public sealed record AdjustedHistoricalBar(
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
    decimal? DividendAmount = null
) : MarketEventPayload
{
    /// <summary>
    /// Convert to standard HistoricalBar (uses adjusted values if available).
    /// </summary>
    public HistoricalBar ToHistoricalBar(bool preferAdjusted = true)
    {
        if (preferAdjusted && AdjustedClose.HasValue)
        {
            return new HistoricalBar(
                Symbol,
                SessionDate,
                AdjustedOpen ?? Open,
                AdjustedHigh ?? High,
                AdjustedLow ?? Low,
                AdjustedClose ?? Close,
                AdjustedVolume ?? Volume,
                Source,
                SequenceNumber
            );
        }

        return new HistoricalBar(Symbol, SessionDate, Open, High, Low, Close, Volume, Source, SequenceNumber);
    }
}

/// <summary>
/// Result of a provider health check.
/// </summary>
public sealed record ProviderHealthStatus(
    string ProviderName,
    bool IsAvailable,
    string? Message = null,
    DateTimeOffset CheckedAt = default,
    TimeSpan? ResponseTime = null
)
{
    public ProviderHealthStatus() : this("unknown", false) { }
}

/// <summary>
/// Progress information for backfill operations.
/// </summary>
public sealed record BackfillProgress(
    string Symbol,
    string Provider,
    int BarsDownloaded,
    int TotalSymbols,
    int CurrentSymbolIndex,
    DateTimeOffset StartedAt,
    string? CurrentStatus = null,
    string? Error = null
)
{
    public double PercentComplete => TotalSymbols > 0
        ? (CurrentSymbolIndex * 100.0) / TotalSymbols
        : 0;

    public TimeSpan Elapsed => DateTimeOffset.UtcNow - StartedAt;
}
