namespace MarketDataCollector.Infrastructure.Plugins.Core;

/// <summary>
/// Health status of a plugin.
/// Simplified from the complex DataSourceHealth with nested records.
/// </summary>
public sealed record PluginHealth
{
    /// <summary>
    /// Overall health status.
    /// </summary>
    public HealthStatus Status { get; init; }

    /// <summary>
    /// Health score from 0.0 (dead) to 1.0 (perfect).
    /// </summary>
    public double Score { get; init; }

    /// <summary>
    /// Human-readable status message.
    /// </summary>
    public string? Message { get; init; }

    /// <summary>
    /// Last response time from the data source.
    /// </summary>
    public TimeSpan? LastLatency { get; init; }

    /// <summary>
    /// Number of consecutive failures.
    /// </summary>
    public int ConsecutiveFailures { get; init; }

    /// <summary>
    /// Last successful operation timestamp.
    /// </summary>
    public DateTimeOffset? LastSuccess { get; init; }

    /// <summary>
    /// Last error timestamp.
    /// </summary>
    public DateTimeOffset? LastError { get; init; }

    /// <summary>
    /// Last error message (for diagnostics).
    /// </summary>
    public string? LastErrorMessage { get; init; }

    /// <summary>
    /// Rate limit state.
    /// </summary>
    public RateLimitStatus? RateLimitStatus { get; init; }

    /// <summary>
    /// When this health status was recorded.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    #region Factory Methods

    /// <summary>
    /// Creates a healthy status.
    /// </summary>
    public static PluginHealth Healthy(TimeSpan? latency = null) => new()
    {
        Status = HealthStatus.Healthy,
        Score = 1.0,
        LastLatency = latency,
        LastSuccess = DateTimeOffset.UtcNow
    };

    /// <summary>
    /// Creates a degraded health status (working but with issues).
    /// </summary>
    public static PluginHealth Degraded(string message, double score = 0.5, int failures = 0) => new()
    {
        Status = HealthStatus.Degraded,
        Score = Math.Clamp(score, 0.0, 0.99),
        Message = message,
        ConsecutiveFailures = failures
    };

    /// <summary>
    /// Creates an unhealthy status.
    /// </summary>
    public static PluginHealth Unhealthy(string message, int failures = 0) => new()
    {
        Status = HealthStatus.Unhealthy,
        Score = 0.0,
        Message = message,
        ConsecutiveFailures = failures,
        LastError = DateTimeOffset.UtcNow,
        LastErrorMessage = message
    };

    /// <summary>
    /// Creates a rate-limited status.
    /// </summary>
    public static PluginHealth RateLimited(TimeSpan resetIn, int remaining = 0, int limit = 0) => new()
    {
        Status = HealthStatus.RateLimited,
        Score = 0.25,
        Message = $"Rate limited, resets in {resetIn.TotalSeconds:F0}s",
        RateLimitStatus = new RateLimitStatus(remaining, limit, DateTimeOffset.UtcNow.Add(resetIn))
    };

    /// <summary>
    /// Creates an unknown/uninitialized status.
    /// </summary>
    public static PluginHealth Unknown => new()
    {
        Status = HealthStatus.Unknown,
        Score = 0.0,
        Message = "Not initialized"
    };

    #endregion

    #region Status Helpers

    /// <summary>
    /// Whether the plugin can accept requests.
    /// </summary>
    public bool CanAcceptRequests => Status is HealthStatus.Healthy or HealthStatus.Degraded;

    /// <summary>
    /// Whether the plugin should be avoided for new requests.
    /// </summary>
    public bool ShouldAvoid => Status is HealthStatus.Unhealthy or HealthStatus.RateLimited;

    #endregion
}

/// <summary>
/// Overall health status categories.
/// </summary>
public enum HealthStatus
{
    /// <summary>Status not yet determined.</summary>
    Unknown,

    /// <summary>Operating normally.</summary>
    Healthy,

    /// <summary>Working but with reduced performance or reliability.</summary>
    Degraded,

    /// <summary>Temporarily unavailable due to rate limiting.</summary>
    RateLimited,

    /// <summary>Not operational, likely needs intervention.</summary>
    Unhealthy
}

/// <summary>
/// Current rate limit status.
/// </summary>
public sealed record RateLimitStatus(
    int RemainingRequests,
    int LimitPerWindow,
    DateTimeOffset? ResetsAt)
{
    /// <summary>
    /// Percentage of rate limit consumed.
    /// </summary>
    public double UtilizationPercent =>
        LimitPerWindow > 0
            ? 100.0 * (LimitPerWindow - RemainingRequests) / LimitPerWindow
            : 0.0;

    /// <summary>
    /// Whether any requests remain.
    /// </summary>
    public bool HasCapacity => RemainingRequests > 0;
}
