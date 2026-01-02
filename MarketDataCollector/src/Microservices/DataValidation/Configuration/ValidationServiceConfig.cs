using DataIngestion.Contracts.Configuration;

namespace DataIngestion.ValidationService.Configuration;

public class ValidationServiceConfig : MicroserviceConfig
{
    public ValidationServiceConfig()
    {
        ServiceName = "DataIngestion.ValidationService";
        HttpPort = 5005;
    }

    public TradeValidationRules TradeRules { get; set; } = new();
    public QuoteValidationRules QuoteRules { get; set; } = new();
    public OrderBookValidationRules OrderBookRules { get; set; } = new();
    public AlertConfig Alerts { get; set; } = new();
}

public class TradeValidationRules
{
    public decimal MinPrice { get; set; } = 0.0001m;
    public decimal MaxPrice { get; set; } = 1000000m;
    public long MinSize { get; set; } = 1;
    public long MaxSize { get; set; } = 10000000;
    public decimal MaxPriceChangePercent { get; set; } = 50m;
    public int MaxFutureTimestampSeconds { get; set; } = 60;
    public int MaxPastTimestampDays { get; set; } = 30;
}

public class QuoteValidationRules
{
    public decimal MinSpreadBps { get; set; } = 0;
    public decimal MaxSpreadBps { get; set; } = 1000;
    public bool RejectCrossedQuotes { get; set; } = true;
    public bool RejectLockedQuotes { get; set; } = false;
}

public class OrderBookValidationRules
{
    public int MinLevels { get; set; } = 1;
    public int MaxLevels { get; set; } = 100;
    public bool RequireSortedLevels { get; set; } = true;
}

public class AlertConfig
{
    public bool Enabled { get; set; } = true;
    public double ValidityRateThreshold { get; set; } = 0.95;
    public int AlertCooldownSeconds { get; set; } = 60;
}
