namespace DataIngestion.TradeService.Models;

/// <summary>
/// Represents a processed trade with validation status.
/// </summary>
public record ProcessedTrade
{
    /// <summary>Unique message identifier.</summary>
    public Guid MessageId { get; init; } = Guid.NewGuid();

    /// <summary>Correlation ID for tracing.</summary>
    public Guid CorrelationId { get; init; }

    /// <summary>Trading symbol.</summary>
    public required string Symbol { get; init; }

    /// <summary>Trade timestamp.</summary>
    public DateTimeOffset Timestamp { get; init; }

    /// <summary>Trade price.</summary>
    public decimal Price { get; init; }

    /// <summary>Trade size/volume.</summary>
    public long Size { get; init; }

    /// <summary>Aggressor side (buy/sell).</summary>
    public string? AggressorSide { get; init; }

    /// <summary>Unique trade identifier from source.</summary>
    public string? TradeId { get; init; }

    /// <summary>Exchange where trade occurred.</summary>
    public string? Exchange { get; init; }

    /// <summary>Trade conditions/flags.</summary>
    public string? Conditions { get; init; }

    /// <summary>Data source/provider.</summary>
    public string? Source { get; init; }

    /// <summary>Sequence number for ordering.</summary>
    public long Sequence { get; init; }

    /// <summary>When this record was received.</summary>
    public DateTimeOffset ReceivedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Whether the trade passed validation.</summary>
    public bool IsValid { get; init; } = true;

    /// <summary>Validation errors if any.</summary>
    public List<string>? ValidationErrors { get; init; }

    /// <summary>Calculated dollar volume (price * size).</summary>
    public decimal DollarVolume => Price * Size;
}

/// <summary>
/// Trade aggregation statistics.
/// </summary>
public record TradeAggregation
{
    public required string Symbol { get; init; }
    public DateTimeOffset WindowStart { get; init; }
    public DateTimeOffset WindowEnd { get; init; }
    public int TradeCount { get; init; }
    public long TotalVolume { get; init; }
    public decimal TotalDollarVolume { get; init; }
    public decimal Vwap { get; init; }
    public decimal HighPrice { get; init; }
    public decimal LowPrice { get; init; }
    public decimal OpenPrice { get; init; }
    public decimal ClosePrice { get; init; }
    public long BuyVolume { get; init; }
    public long SellVolume { get; init; }
}
