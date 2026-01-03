using MarketDataCollector.Domain.Models;

namespace MarketDataCollector.Infrastructure.Providers.Abstractions;

/// <summary>
/// Connection state for streaming providers.
/// </summary>
public enum ConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Reconnecting,
    Failed,
    Disposed
}

/// <summary>
/// Core contract for all data providers. Provider-agnostic design enables
/// adding new providers without modifying consuming code.
/// </summary>
public interface IDataProvider : IAsyncDisposable
{
    /// <summary>
    /// Unique identifier for this provider type (e.g., "alpaca", "ibkr", "polygon").
    /// </summary>
    string ProviderId { get; }

    /// <summary>
    /// Human-readable display name.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Provider capabilities and metadata.
    /// </summary>
    ProviderCapabilityInfo CapabilityInfo { get; }

    /// <summary>
    /// Current health and connection status.
    /// </summary>
    ProviderHealthInfo HealthStatus { get; }

    /// <summary>
    /// Whether this provider is currently enabled.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Event raised when provider status changes.
    /// </summary>
    event EventHandler<ProviderStatusChangedEventArgs>? StatusChanged;

    /// <summary>
    /// Initialize the provider with configuration.
    /// </summary>
    Task InitializeAsync(CancellationToken ct = default);

    /// <summary>
    /// Perform health check and return detailed status.
    /// </summary>
    Task<HealthCheckResult> HealthCheckAsync(CancellationToken ct = default);

    /// <summary>
    /// Validate credentials without full connection.
    /// </summary>
    Task<CredentialValidationResult> ValidateCredentialsAsync(CancellationToken ct = default);
}

/// <summary>
/// Extension interface for providers supporting real-time streaming.
/// </summary>
public interface IStreamingDataProvider : IDataProvider
{
    /// <summary>
    /// Current connection state.
    /// </summary>
    ConnectionState ConnectionState { get; }

    /// <summary>
    /// Establish connection to data source.
    /// </summary>
    Task<ConnectionResult> ConnectAsync(CancellationToken ct = default);

    /// <summary>
    /// Gracefully disconnect from data source.
    /// </summary>
    Task DisconnectAsync(CancellationToken ct = default);

    /// <summary>
    /// Subscribe to market data for symbols.
    /// </summary>
    Task<SubscriptionResult> SubscribeAsync(SubscriptionRequest request, CancellationToken ct = default);

    /// <summary>
    /// Unsubscribe from market data.
    /// </summary>
    Task UnsubscribeAsync(string subscriptionId, CancellationToken ct = default);

    /// <summary>
    /// Get all active subscriptions.
    /// </summary>
    IReadOnlyList<SubscriptionInfo> GetActiveSubscriptions();
}

/// <summary>
/// Extension interface for providers supporting historical data retrieval.
/// </summary>
public interface IHistoricalDataProvider : IDataProvider
{
    /// <summary>
    /// Get available date range for a symbol.
    /// </summary>
    Task<DateRangeInfo?> GetAvailableDateRangeAsync(string symbol, CancellationToken ct = default);

    /// <summary>
    /// Retrieve historical bars for a symbol.
    /// </summary>
    Task<HistoricalDataResult> GetBarsAsync(HistoricalBarRequest request, CancellationToken ct = default);

    /// <summary>
    /// Stream historical data with automatic pagination.
    /// </summary>
    IAsyncEnumerable<HistoricalBar> StreamBarsAsync(HistoricalBarRequest request, CancellationToken ct = default);
}

/// <summary>
/// Combined interface for providers supporting both streaming and historical data.
/// </summary>
public interface IUnifiedDataProvider : IStreamingDataProvider, IHistoricalDataProvider
{
    /// <summary>
    /// Get the most appropriate mode for a data request.
    /// Some providers may prefer streaming for recent data and REST for historical.
    /// </summary>
    DataAccessMode GetPreferredAccessMode(DataAccessRequest request);
}

/// <summary>
/// Specifies the preferred data access mode.
/// </summary>
public enum DataAccessMode
{
    /// <summary>Use REST API.</summary>
    Rest,
    /// <summary>Use WebSocket streaming.</summary>
    Streaming,
    /// <summary>Let the provider decide.</summary>
    Auto
}

/// <summary>
/// Detailed capability metadata for a provider.
/// </summary>
public sealed record ProviderCapabilityInfo
{
    public required ProviderCapabilities Capabilities { get; init; }
    public IReadOnlyDictionary<string, object> Metadata { get; init; } = new Dictionary<string, object>();

    // Rate limiting information
    public int? MaxRequestsPerMinute { get; init; }
    public int? MaxRequestsPerHour { get; init; }
    public int? MaxConcurrentConnections { get; init; }
    public int? MaxSymbolsPerSubscription { get; init; }

    // Historical data limits
    public int? MaxHistoricalDaysPerRequest { get; init; }
    public DateOnly? EarliestHistoricalDate { get; init; }
    public TimeSpan? MinBarInterval { get; init; }

    // Data quality indicators
    public decimal? TypicalLatencyMs { get; init; }
    public decimal? DataCompletenessScore { get; init; }

    public bool HasCapability(ProviderCapabilities capability) =>
        (Capabilities & capability) == capability;

    public bool HasAnyCapability(ProviderCapabilities capabilities) =>
        (Capabilities & capabilities) != 0;
}

/// <summary>
/// Health status information for a provider.
/// </summary>
public sealed record ProviderHealthInfo(
    bool IsHealthy,
    string? Message = null,
    DateTimeOffset? LastSuccessfulOperation = null,
    DateTimeOffset? LastFailure = null,
    int ConsecutiveFailures = 0,
    TimeSpan? AverageResponseTime = null
);

/// <summary>
/// Event args for provider status changes.
/// </summary>
public sealed class ProviderStatusChangedEventArgs : EventArgs
{
    public required string ProviderId { get; init; }
    public required ConnectionState PreviousState { get; init; }
    public required ConnectionState CurrentState { get; init; }
    public string? Reason { get; init; }
    public Exception? Exception { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Result of a health check operation.
/// </summary>
public sealed record HealthCheckResult(
    bool IsHealthy,
    string? Message = null,
    TimeSpan? ResponseTime = null,
    IReadOnlyDictionary<string, object>? Details = null
);

/// <summary>
/// Result of credential validation.
/// </summary>
public sealed record CredentialValidationResult(
    bool IsValid,
    string? Message = null,
    string? AccountId = null,
    IReadOnlyList<string>? Permissions = null
);

/// <summary>
/// Result of a connection attempt.
/// </summary>
public sealed record ConnectionResult(
    bool Success,
    string? Message = null,
    string? SessionId = null,
    DateTimeOffset? ConnectedAt = null
);

/// <summary>
/// Request for market data subscription.
/// </summary>
public sealed record SubscriptionRequest
{
    public required IReadOnlyList<string> Symbols { get; init; }
    public bool SubscribeTrades { get; init; } = true;
    public bool SubscribeQuotes { get; init; } = true;
    public bool SubscribeDepth { get; init; } = false;
    public int? DepthLevels { get; init; }
}

/// <summary>
/// Result of a subscription request.
/// </summary>
public sealed record SubscriptionResult(
    bool Success,
    string? SubscriptionId = null,
    string? Message = null,
    IReadOnlyList<string>? SubscribedSymbols = null,
    IReadOnlyList<string>? FailedSymbols = null
);

/// <summary>
/// Information about an active subscription.
/// </summary>
public sealed record SubscriptionInfo(
    string SubscriptionId,
    IReadOnlyList<string> Symbols,
    DateTimeOffset CreatedAt,
    bool IsActive
);

/// <summary>
/// Available date range information for a symbol.
/// </summary>
public sealed record DateRangeInfo(
    string Symbol,
    DateOnly EarliestDate,
    DateOnly LatestDate,
    string? Source = null
);

/// <summary>
/// Request for data access mode determination.
/// </summary>
public sealed record DataAccessRequest(
    string Symbol,
    DateOnly? StartDate = null,
    DateOnly? EndDate = null,
    bool IsRealTime = false
);

/// <summary>
/// Request for historical bar data.
/// </summary>
public sealed record HistoricalBarRequest
{
    public required string Symbol { get; init; }
    public DateOnly? StartDate { get; init; }
    public DateOnly? EndDate { get; init; }
    public BarInterval Interval { get; init; } = BarInterval.Daily;
    public bool AdjustForSplits { get; init; } = true;
    public bool AdjustForDividends { get; init; } = true;
    public int? Limit { get; init; }
}

/// <summary>
/// Bar interval/timeframe.
/// </summary>
public enum BarInterval
{
    Minute1,
    Minute5,
    Minute15,
    Minute30,
    Hour1,
    Hour4,
    Daily,
    Weekly,
    Monthly
}

/// <summary>
/// Result of a historical data request.
/// </summary>
public sealed record HistoricalDataResult
{
    public bool Success { get; init; }
    public IReadOnlyList<HistoricalBar> Bars { get; init; } = Array.Empty<HistoricalBar>();
    public string? ErrorMessage { get; init; }
    public string? Source { get; init; }
    public DateTimeOffset? RequestedAt { get; init; }
    public TimeSpan? ResponseTime { get; init; }

    public static HistoricalDataResult Failed(string message) =>
        new() { Success = false, ErrorMessage = message };

    public static HistoricalDataResult Empty() =>
        new() { Success = true, Bars = Array.Empty<HistoricalBar>() };
}
