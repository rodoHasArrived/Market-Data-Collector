using DataIngestion.Contracts.Configuration;

namespace DataIngestion.OrderBookService.Configuration;

/// <summary>
/// Configuration for the OrderBook Ingestion Service.
/// </summary>
public class OrderBookServiceConfig : MicroserviceConfig
{
    public OrderBookServiceConfig()
    {
        ServiceName = "DataIngestion.OrderBookService";
        HttpPort = 5002;
    }

    /// <summary>Order book management settings.</summary>
    public OrderBookConfig OrderBook { get; set; } = new();

    /// <summary>Snapshot settings.</summary>
    public SnapshotConfig Snapshot { get; set; } = new();
}

public class OrderBookConfig
{
    /// <summary>Maximum depth levels to maintain.</summary>
    public int MaxDepthLevels { get; set; } = 50;

    /// <summary>Enable integrity checking.</summary>
    public bool EnableIntegrityCheck { get; set; } = true;

    /// <summary>Freeze book on integrity error.</summary>
    public bool FreezeOnIntegrityError { get; set; } = true;

    /// <summary>Stale timeout in seconds.</summary>
    public int StaleTimeoutSeconds { get; set; } = 30;

    /// <summary>Maximum books to keep in memory.</summary>
    public int MaxActiveBooks { get; set; } = 1000;
}

public class SnapshotConfig
{
    /// <summary>Snapshot interval in milliseconds.</summary>
    public int IntervalMs { get; set; } = 1000;

    /// <summary>Only snapshot if changed.</summary>
    public bool OnlyIfChanged { get; set; } = true;

    /// <summary>Enable snapshot storage.</summary>
    public bool EnableStorage { get; set; } = true;
}
