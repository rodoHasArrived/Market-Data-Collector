namespace DataIngestion.Contracts.Messages;

/// <summary>
/// Command to route ingestion data through the gateway.
/// </summary>
public interface IRouteIngestionData : IIngestionMessage
{
    string Symbol { get; }
    IngestionDataType DataType { get; }
    string RawPayload { get; }
    string ContentType { get; }
    string Provider { get; }
    IReadOnlyDictionary<string, string> Headers { get; }
    /// <summary>Sequence number for ordering events.</summary>
    long Sequence { get; }
}

/// <summary>
/// Types of ingestion data routed through gateway.
/// </summary>
public enum IngestionDataType
{
    Trade,
    Quote,
    OrderBookSnapshot,
    OrderBookUpdate,
    HistoricalTrade,
    HistoricalQuote,
    HistoricalBar,
    Custom
}

/// <summary>
/// Gateway health status message.
/// </summary>
public interface IGatewayHealthStatus : IIngestionMessage
{
    string GatewayId { get; }
    bool IsHealthy { get; }
    IReadOnlyDictionary<string, ProviderConnectionStatus> ProviderStatuses { get; }
    long MessagesRoutedTotal { get; }
    long MessagesRoutedPerSecond { get; }
    long FailedRoutingCount { get; }
    TimeSpan Uptime { get; }
}

/// <summary>
/// Provider connection status.
/// </summary>
public record ProviderConnectionStatus(
    string ProviderName,
    bool IsConnected,
    DateTimeOffset LastConnectedAt,
    DateTimeOffset? LastDisconnectedAt,
    int ReconnectAttempts,
    string? LastError
);

/// <summary>
/// Command to subscribe to market data.
/// </summary>
public interface ISubscribeToMarketData : IIngestionMessage
{
    string Symbol { get; }
    string Provider { get; }
    SubscriptionType[] SubscriptionTypes { get; }
}

/// <summary>
/// Types of market data subscriptions.
/// </summary>
public enum SubscriptionType
{
    Trades,
    Quotes,
    OrderBook,
    All
}

/// <summary>
/// Subscription confirmation.
/// </summary>
public interface IMarketDataSubscriptionConfirmed : IIngestionMessage
{
    string Symbol { get; }
    string Provider { get; }
    SubscriptionType[] ActiveSubscriptions { get; }
    int SubscriptionId { get; }
    bool Success { get; }
    string? ErrorMessage { get; }
}

/// <summary>
/// Command to unsubscribe from market data.
/// </summary>
public interface IUnsubscribeFromMarketData : IIngestionMessage
{
    string Symbol { get; }
    string Provider { get; }
    int? SubscriptionId { get; }
    SubscriptionType[] SubscriptionTypes { get; }
}

/// <summary>
/// Provider rate limit notification.
/// </summary>
public interface IProviderRateLimitWarning : IIngestionMessage
{
    string Provider { get; }
    int CurrentRate { get; }
    int MaxRate { get; }
    double UtilizationPercent { get; }
    TimeSpan? RetryAfter { get; }
}
