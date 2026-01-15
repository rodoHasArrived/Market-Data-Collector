namespace MarketDataCollector.Application.Config;

/// <summary>
/// MassTransit message broker configuration.
/// </summary>
/// <remarks>
/// <para><b>Credentials:</b> Credentials are resolved from environment variables:</para>
/// <list type="bullet">
/// <item><description>RabbitMQ: <c>RABBITMQ_USERNAME</c>, <c>RABBITMQ_PASSWORD</c></description></item>
/// <item><description>Azure Service Bus: <c>AZURE_SERVICEBUS_CONNECTION_STRING</c></description></item>
/// </list>
/// </remarks>
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
/// <remarks>
/// <para><b>Credentials:</b> Set <c>RABBITMQ_USERNAME</c> and <c>RABBITMQ_PASSWORD</c> environment variables.</para>
/// <para>Config values are used as fallback if environment variables are not set.</para>
/// </remarks>
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
    /// Username for authentication (fallback if RABBITMQ_USERNAME env var not set).
    /// </summary>
    string Username = "guest",

    /// <summary>
    /// Password for authentication (fallback if RABBITMQ_PASSWORD env var not set).
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
)
{
    /// <summary>
    /// Resolves username from environment variable or config fallback.
    /// </summary>
    public string ResolveUsername()
        => Environment.GetEnvironmentVariable("RABBITMQ_USERNAME") ?? Username;

    /// <summary>
    /// Resolves password from environment variable or config fallback.
    /// </summary>
    public string ResolvePassword()
        => Environment.GetEnvironmentVariable("RABBITMQ_PASSWORD") ?? Password;
}

/// <summary>
/// Azure Service Bus transport configuration.
/// </summary>
/// <remarks>
/// <para><b>Credentials:</b> Set <c>AZURE_SERVICEBUS_CONNECTION_STRING</c> environment variable,
/// or use managed identity authentication (recommended for Azure deployments).</para>
/// </remarks>
public sealed record AzureServiceBusConfig(
    /// <summary>
    /// Azure Service Bus connection string (fallback if env var not set).
    /// Prefer using AZURE_SERVICEBUS_CONNECTION_STRING environment variable.
    /// </summary>
    string ConnectionString = "",

    /// <summary>
    /// Service Bus namespace for managed identity authentication.
    /// Use either ConnectionString OR (Namespace + UseManagedIdentity).
    /// </summary>
    string? Namespace = null,

    /// <summary>
    /// Whether to use Azure Managed Identity instead of connection string.
    /// Recommended for Azure deployments (no credentials needed).
    /// </summary>
    bool UseManagedIdentity = false,

    /// <summary>
    /// Enable premium features (message sessions, large messages).
    /// </summary>
    bool EnablePremiumFeatures = false
)
{
    /// <summary>
    /// Resolves connection string from environment variable or config fallback.
    /// </summary>
    public string ResolveConnectionString()
        => Environment.GetEnvironmentVariable("AZURE_SERVICEBUS_CONNECTION_STRING") ?? ConnectionString;
}

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
