using DataIngestion.Contracts.Configuration;

namespace DataIngestion.QuoteService.Configuration;

public class QuoteServiceConfig : MicroserviceConfig
{
    public QuoteServiceConfig()
    {
        ServiceName = "DataIngestion.QuoteService";
        HttpPort = 5003;
    }

    public QuoteProcessingConfig Processing { get; set; } = new();
    public QuoteValidationConfig Validation { get; set; } = new();
}

public class QuoteProcessingConfig
{
    public int ChannelCapacity { get; set; } = 100000;
    public int BatchSize { get; set; } = 200;
    public int FlushIntervalMs { get; set; } = 500;
    public bool TrackNbbo { get; set; } = true;
    public bool CalculateSpread { get; set; } = true;
}

public class QuoteValidationConfig
{
    public bool DetectCrossedQuotes { get; set; } = true;
    public bool DetectLockedQuotes { get; set; } = true;
    public bool ValidatePrices { get; set; } = true;
    public decimal MaxSpreadPercent { get; set; } = 10m;
}
