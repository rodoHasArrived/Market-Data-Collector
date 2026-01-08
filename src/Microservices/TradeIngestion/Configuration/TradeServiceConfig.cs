using DataIngestion.Contracts.Configuration;

namespace DataIngestion.TradeService.Configuration;

/// <summary>
/// Configuration for the Trade Ingestion Service.
/// </summary>
public class TradeServiceConfig : MicroserviceConfig
{
    public TradeServiceConfig()
    {
        ServiceName = "DataIngestion.TradeService";
        HttpPort = 5001;
    }

    /// <summary>Processing pipeline configuration.</summary>
    public ProcessingConfig Processing { get; set; } = new();

    /// <summary>Validation settings.</summary>
    public ValidationConfig Validation { get; set; } = new();

    /// <summary>Aggregation settings for order flow statistics.</summary>
    public AggregationConfig Aggregation { get; set; } = new();

    /// <summary>Dead letter queue configuration for failed trades.</summary>
    public DeadLetterConfig DeadLetter { get; set; } = new();
}

/// <summary>
/// Processing pipeline configuration.
/// </summary>
public class ProcessingConfig
{
    /// <summary>Maximum channel buffer size.</summary>
    public int ChannelCapacity { get; set; } = 50000;

    /// <summary>Number of parallel processors.</summary>
    public int ProcessorCount { get; set; } = 4;

    /// <summary>Batch size for storage writes.</summary>
    public int BatchSize { get; set; } = 100;

    /// <summary>Maximum wait time before flushing incomplete batch (ms).</summary>
    public int FlushIntervalMs { get; set; } = 1000;

    /// <summary>Enable sequence validation.</summary>
    public bool ValidateSequence { get; set; } = true;

    /// <summary>Enable deduplication.</summary>
    public bool EnableDeduplication { get; set; } = true;

    /// <summary>Deduplication window in seconds.</summary>
    public int DeduplicationWindowSeconds { get; set; } = 60;
}

/// <summary>
/// Validation configuration.
/// </summary>
public class ValidationConfig
{
    /// <summary>Enable price validation.</summary>
    public bool ValidatePrice { get; set; } = true;

    /// <summary>Maximum allowed price.</summary>
    public decimal MaxPrice { get; set; } = 1_000_000m;

    /// <summary>Minimum allowed price.</summary>
    public decimal MinPrice { get; set; } = 0.0001m;

    /// <summary>Maximum price change percent per tick.</summary>
    public decimal MaxPriceChangePercent { get; set; } = 50m;

    /// <summary>Enable size validation.</summary>
    public bool ValidateSize { get; set; } = true;

    /// <summary>Maximum allowed size.</summary>
    public long MaxSize { get; set; } = 10_000_000;

    /// <summary>Reject invalid trades vs mark and continue.</summary>
    public bool RejectInvalid { get; set; } = false;
}

/// <summary>
/// Aggregation configuration for computing statistics.
/// </summary>
public class AggregationConfig
{
    /// <summary>Enable order flow aggregation.</summary>
    public bool EnableOrderFlow { get; set; } = true;

    /// <summary>Order flow window size in seconds.</summary>
    public int OrderFlowWindowSeconds { get; set; } = 60;

    /// <summary>Enable VWAP calculation.</summary>
    public bool EnableVwap { get; set; } = true;

    /// <summary>VWAP window size in seconds.</summary>
    public int VwapWindowSeconds { get; set; } = 300;

    /// <summary>Enable trade bar aggregation.</summary>
    public bool EnableBars { get; set; } = true;

    /// <summary>Bar size in seconds.</summary>
    public int BarSizeSeconds { get; set; } = 60;
}

/// <summary>
/// Dead letter queue configuration for failed trades.
/// </summary>
public class DeadLetterConfig
{
    /// <summary>Enable dead letter queue.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Maximum size of in-memory dead letter queue.</summary>
    public int MaxQueueSize { get; set; } = 10000;

    /// <summary>Directory for persisting dead letter queue to disk.</summary>
    public string PersistenceDirectory { get; set; } = "data/dead_letter";

    /// <summary>Enable persistence of dead letters to disk.</summary>
    public bool EnablePersistence { get; set; } = true;

    /// <summary>Maximum retry attempts before moving to dead letter.</summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>Base delay for exponential backoff in milliseconds.</summary>
    public int RetryBaseDelayMs { get; set; } = 100;

    /// <summary>Maximum delay for exponential backoff in milliseconds.</summary>
    public int RetryMaxDelayMs { get; set; } = 5000;

    /// <summary>Enable automatic retry of dead lettered trades.</summary>
    public bool EnableAutoRetry { get; set; } = false;

    /// <summary>Interval in seconds for auto-retry attempts.</summary>
    public int AutoRetryIntervalSeconds { get; set; } = 300;
}
