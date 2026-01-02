using DataIngestion.Contracts.Configuration;

namespace DataIngestion.HistoricalService.Configuration;

public class HistoricalServiceConfig : MicroserviceConfig
{
    public HistoricalServiceConfig()
    {
        ServiceName = "DataIngestion.HistoricalService";
        HttpPort = 5004;
    }

    public BackfillConfig Backfill { get; set; } = new();
    public Dictionary<string, ProviderSettings> Providers { get; set; } = new();
}

public class BackfillConfig
{
    public int MaxConcurrentJobs { get; set; } = 5;
    public int BatchSize { get; set; } = 1000;
    public int RetryCount { get; set; } = 3;
    public int RetryDelayMs { get; set; } = 1000;
    public int ProgressReportIntervalSeconds { get; set; } = 10;
}

public class ProviderSettings
{
    public bool Enabled { get; set; } = true;
    public string? ApiKey { get; set; }
    public string? BaseUrl { get; set; }
    public int RateLimitPerMinute { get; set; } = 60;
    public int Priority { get; set; } = 0;
}
