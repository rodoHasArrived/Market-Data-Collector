using System.Text.Json.Serialization;

namespace MarketDataCollector.Contracts.Domain;

/// <summary>
/// Trade data transfer object.
/// </summary>
public class TradeDto
{
    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; set; }

    [JsonPropertyName("symbol")]
    public string Symbol { get; set; } = string.Empty;

    [JsonPropertyName("price")]
    public decimal Price { get; set; }

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("aggressor")]
    public string Aggressor { get; set; } = "Unknown";

    [JsonPropertyName("sequenceNumber")]
    public long SequenceNumber { get; set; }

    [JsonPropertyName("streamId")]
    public string? StreamId { get; set; }

    [JsonPropertyName("venue")]
    public string? Venue { get; set; }

    [JsonPropertyName("tradeId")]
    public string? TradeId { get; set; }

    [JsonPropertyName("conditions")]
    public string[]? Conditions { get; set; }
}

/// <summary>
/// Quote (BBO) data transfer object.
/// </summary>
public class QuoteDto
{
    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; set; }

    [JsonPropertyName("symbol")]
    public string Symbol { get; set; } = string.Empty;

    [JsonPropertyName("bidPrice")]
    public decimal BidPrice { get; set; }

    [JsonPropertyName("bidSize")]
    public long BidSize { get; set; }

    [JsonPropertyName("askPrice")]
    public decimal AskPrice { get; set; }

    [JsonPropertyName("askSize")]
    public long AskSize { get; set; }

    [JsonPropertyName("midPrice")]
    public decimal? MidPrice { get; set; }

    [JsonPropertyName("spread")]
    public decimal? Spread { get; set; }

    [JsonPropertyName("sequenceNumber")]
    public long SequenceNumber { get; set; }

    [JsonPropertyName("streamId")]
    public string? StreamId { get; set; }

    [JsonPropertyName("venue")]
    public string? Venue { get; set; }
}

/// <summary>
/// Order book level data transfer object.
/// </summary>
public class OrderBookLevelDto
{
    [JsonPropertyName("side")]
    public string Side { get; set; } = "Bid";

    [JsonPropertyName("level")]
    public int Level { get; set; }

    [JsonPropertyName("price")]
    public decimal Price { get; set; }

    [JsonPropertyName("size")]
    public decimal Size { get; set; }

    [JsonPropertyName("marketMaker")]
    public string? MarketMaker { get; set; }

    [JsonPropertyName("orderCount")]
    public int? OrderCount { get; set; }
}

/// <summary>
/// Order book snapshot data transfer object.
/// </summary>
public class OrderBookSnapshotDto
{
    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; set; }

    [JsonPropertyName("symbol")]
    public string Symbol { get; set; } = string.Empty;

    [JsonPropertyName("bids")]
    public OrderBookLevelDto[] Bids { get; set; } = Array.Empty<OrderBookLevelDto>();

    [JsonPropertyName("asks")]
    public OrderBookLevelDto[] Asks { get; set; } = Array.Empty<OrderBookLevelDto>();

    [JsonPropertyName("midPrice")]
    public decimal? MidPrice { get; set; }

    [JsonPropertyName("microPrice")]
    public decimal? MicroPrice { get; set; }

    [JsonPropertyName("imbalance")]
    public decimal? Imbalance { get; set; }

    [JsonPropertyName("marketState")]
    public string MarketState { get; set; } = "Normal";

    [JsonPropertyName("sequenceNumber")]
    public long SequenceNumber { get; set; }
}

/// <summary>
/// Historical bar data transfer object.
/// </summary>
public class HistoricalBarDto
{
    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; set; }

    [JsonPropertyName("symbol")]
    public string Symbol { get; set; } = string.Empty;

    [JsonPropertyName("open")]
    public decimal Open { get; set; }

    [JsonPropertyName("high")]
    public decimal High { get; set; }

    [JsonPropertyName("low")]
    public decimal Low { get; set; }

    [JsonPropertyName("close")]
    public decimal Close { get; set; }

    [JsonPropertyName("volume")]
    public long Volume { get; set; }

    [JsonPropertyName("vwap")]
    public decimal? Vwap { get; set; }

    [JsonPropertyName("tradeCount")]
    public int? TradeCount { get; set; }

    [JsonPropertyName("interval")]
    public string Interval { get; set; } = "Daily";

    [JsonPropertyName("source")]
    public string? Source { get; set; }

    [JsonPropertyName("isAdjusted")]
    public bool IsAdjusted { get; set; }
}

/// <summary>
/// Aggressor side enumeration values.
/// </summary>
public static class AggressorSideValues
{
    public const string Buy = "Buy";
    public const string Sell = "Sell";
    public const string Unknown = "Unknown";
}

/// <summary>
/// Order book side enumeration values.
/// </summary>
public static class OrderBookSideValues
{
    public const string Bid = "Bid";
    public const string Ask = "Ask";
}

/// <summary>
/// Market state enumeration values.
/// </summary>
public static class MarketStateValues
{
    public const string Normal = "Normal";
    public const string PreMarket = "PreMarket";
    public const string AfterHours = "AfterHours";
    public const string Closed = "Closed";
    public const string Halted = "Halted";
    public const string Auction = "Auction";
}

/// <summary>
/// Bar interval enumeration values.
/// </summary>
public static class BarIntervalValues
{
    public const string Minute1 = "Minute1";
    public const string Minute5 = "Minute5";
    public const string Minute15 = "Minute15";
    public const string Minute30 = "Minute30";
    public const string Hour1 = "Hour1";
    public const string Hour4 = "Hour4";
    public const string Daily = "Daily";
    public const string Weekly = "Weekly";
    public const string Monthly = "Monthly";
}

/// <summary>
/// Integrity event data transfer object.
/// </summary>
public class IntegrityEventDto
{
    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; set; }

    [JsonPropertyName("symbol")]
    public string Symbol { get; set; } = string.Empty;

    [JsonPropertyName("kind")]
    public string Kind { get; set; } = string.Empty;

    [JsonPropertyName("severity")]
    public string Severity { get; set; } = "Warning";

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("expectedValue")]
    public string? ExpectedValue { get; set; }

    [JsonPropertyName("actualValue")]
    public string? ActualValue { get; set; }
}
