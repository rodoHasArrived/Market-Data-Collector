namespace DataIngestion.Contracts.Configuration;

/// <summary>
/// Base configuration for all data ingestion microservices.
/// </summary>
public class MicroserviceConfig
{
    /// <summary>Service identifier.</summary>
    public string ServiceId { get; set; } = Guid.NewGuid().ToString("N")[..8];

    /// <summary>Service display name.</summary>
    public string ServiceName { get; set; } = "DataIngestionService";

    /// <summary>Environment (Development, Staging, Production).</summary>
    public string Environment { get; set; } = "Development";

    /// <summary>HTTP port for health checks and metrics.</summary>
    public int HttpPort { get; set; } = 8080;

    /// <summary>Enable Prometheus metrics endpoint.</summary>
    public bool EnableMetrics { get; set; } = true;

    /// <summary>Enable distributed tracing.</summary>
    public bool EnableTracing { get; set; } = true;

    /// <summary>Logging configuration.</summary>
    public LoggingConfig Logging { get; set; } = new();

    /// <summary>Message bus configuration.</summary>
    public MessageBusConfig MessageBus { get; set; } = new();

    /// <summary>Storage configuration.</summary>
    public StorageConfig Storage { get; set; } = new();

    /// <summary>Rate limiting configuration.</summary>
    public RateLimitConfig RateLimit { get; set; } = new();
}

/// <summary>
/// Logging configuration.
/// </summary>
public class LoggingConfig
{
    public string MinimumLevel { get; set; } = "Information";
    public bool EnableConsole { get; set; } = true;
    public bool EnableFile { get; set; } = true;
    public string LogDirectory { get; set; } = "logs";
    public int RetentionDays { get; set; } = 30;
}

/// <summary>
/// Message bus configuration for MassTransit.
/// </summary>
public class MessageBusConfig
{
    public string Transport { get; set; } = "InMemory";
    public RabbitMqBusConfig? RabbitMq { get; set; }
    public AzureServiceBusConfig? AzureServiceBus { get; set; }
    public int PrefetchCount { get; set; } = 16;
    public int ConcurrencyLimit { get; set; } = 8;
    public RetryPolicyConfig Retry { get; set; } = new();
}

/// <summary>
/// RabbitMQ-specific configuration.
/// </summary>
public class RabbitMqBusConfig
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string VirtualHost { get; set; } = "/";
    public string Username { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public bool UseSsl { get; set; } = false;
    public string[]? ClusterNodes { get; set; }
}

/// <summary>
/// Azure Service Bus configuration.
/// </summary>
public class AzureServiceBusConfig
{
    public string? ConnectionString { get; set; }
    public string? Namespace { get; set; }
    public bool UseManagedIdentity { get; set; } = false;
}

/// <summary>
/// Retry policy configuration.
/// </summary>
public class RetryPolicyConfig
{
    public int MaxRetries { get; set; } = 3;
    public int InitialIntervalMs { get; set; } = 100;
    public int MaxIntervalMs { get; set; } = 5000;
    public double BackoffMultiplier { get; set; } = 2.0;
}

/// <summary>
/// Storage configuration for data persistence.
/// </summary>
public class StorageConfig
{
    public string DataDirectory { get; set; } = "data";
    public string StorageType { get; set; } = "JsonLines";
    public bool EnableCompression { get; set; } = false;
    public int FlushIntervalSeconds { get; set; } = 5;
    public int BufferSize { get; set; } = 1000;
}

/// <summary>
/// Rate limiting configuration.
/// </summary>
public class RateLimitConfig
{
    public bool Enabled { get; set; } = true;
    public int RequestsPerSecond { get; set; } = 1000;
    public int BurstSize { get; set; } = 100;
    public string Strategy { get; set; } = "SlidingWindow";
}
