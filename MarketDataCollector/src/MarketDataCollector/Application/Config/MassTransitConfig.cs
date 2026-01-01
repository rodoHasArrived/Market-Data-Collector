namespace MarketDataCollector.Application.Config;

/// <summary>
/// MassTransit message broker configuration.
/// </summary>
public sealed record MassTransitConfig(
    /// <summary>
    /// Whether MassTransit messaging is enabled.
    /// When false, events are only persisted locally.
    /// </summary>
    bool Enabled = false,

    /// <summary>
    /// Transport type: "InMemory", "RabbitMQ", "AzureServiceBus".
    /// </summary>
    string Transport = "InMemory",

    /// <summary>
    /// RabbitMQ configuration (required when Transport is "RabbitMQ").
    /// </summary>
    RabbitMqConfig? RabbitMQ = null,

    /// <summary>
    /// Azure Service Bus configuration (required when Transport is "AzureServiceBus").
    /// </summary>
    AzureServiceBusConfig? AzureServiceBus = null,

    /// <summary>
    /// Retry configuration for message publishing.
    /// </summary>
    RetryConfig? Retry = null,

    /// <summary>
    /// Whether to enable message scheduling.
    /// </summary>
    bool EnableScheduling = false,

    /// <summary>
    /// Prefix for all endpoint/queue names.
    /// </summary>
    string? EndpointPrefix = null
);

/// <summary>
/// RabbitMQ transport configuration.
/// </summary>
public sealed record RabbitMqConfig(
    /// <summary>
    /// RabbitMQ server hostname.
    /// </summary>
    string Host = "localhost",

    /// <summary>
    /// RabbitMQ port (default 5672).
    /// </summary>
    int Port = 5672,

    /// <summary>
    /// Virtual host (default "/").
    /// </summary>
    string VirtualHost = "/",

    /// <summary>
    /// Username for authentication.
    /// </summary>
    string Username = "guest",

    /// <summary>
    /// Password for authentication.
    /// Consider using environment variables in production.
    /// </summary>
    string Password = "guest",

    /// <summary>
    /// Whether to use SSL/TLS.
    /// </summary>
    bool UseSsl = false,

    /// <summary>
    /// Publisher confirms for reliable publishing.
    /// </summary>
    bool PublisherConfirmation = true,

    /// <summary>
    /// Cluster nodes for high availability (optional).
    /// </summary>
    string[]? ClusterNodes = null
);

/// <summary>
/// Azure Service Bus transport configuration.
/// </summary>
public sealed record AzureServiceBusConfig(
    /// <summary>
    /// Azure Service Bus connection string.
    /// Consider using environment variables in production.
    /// </summary>
    string ConnectionString = "",

    /// <summary>
    /// Service Bus namespace for managed identity authentication.
    /// Use either ConnectionString OR (Namespace + UseManagedIdentity).
    /// </summary>
    string? Namespace = null,

    /// <summary>
    /// Whether to use Azure Managed Identity instead of connection string.
    /// </summary>
    bool UseManagedIdentity = false,

    /// <summary>
    /// Enable premium features (message sessions, large messages).
    /// </summary>
    bool EnablePremiumFeatures = false
);

/// <summary>
/// Retry configuration for message handling.
/// </summary>
public sealed record RetryConfig(
    /// <summary>
    /// Maximum retry attempts.
    /// </summary>
    int MaxRetries = 3,

    /// <summary>
    /// Initial retry interval in milliseconds.
    /// </summary>
    int InitialIntervalMs = 100,

    /// <summary>
    /// Maximum retry interval in milliseconds.
    /// </summary>
    int MaxIntervalMs = 5000,

    /// <summary>
    /// Interval increment for each retry attempt (exponential backoff).
    /// </summary>
    double IntervalMultiplier = 2.0
);

/// <summary>
/// Transport type enumeration.
/// </summary>
public enum MassTransitTransport
{
    InMemory,
    RabbitMQ,
    AzureServiceBus
}
