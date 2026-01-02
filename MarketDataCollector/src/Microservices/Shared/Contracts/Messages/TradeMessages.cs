namespace DataIngestion.Contracts.Messages;

/// <summary>
/// Message for raw trade data ingestion from external providers.
/// </summary>
public interface IRawTradeIngested : ISymbolIngestionMessage
{
    decimal Price { get; }
    long Size { get; }
    string AggressorSide { get; }
    string? TradeId { get; }
    string? Exchange { get; }
    string? Conditions { get; }
}

/// <summary>
/// Message for validated and enriched trade data.
/// </summary>
public interface IValidatedTrade : ISymbolIngestionMessage
{
    decimal Price { get; }
    long Size { get; }
    string AggressorSide { get; }
    string? TradeId { get; }
    string? Exchange { get; }
    bool IsValid { get; }
    string[] ValidationErrors { get; }
    decimal? PriceChangePercent { get; }
    decimal? VolumeWeightedPrice { get; }
}

/// <summary>
/// Command to ingest a batch of trades.
/// </summary>
public interface IIngestTradesBatch : IIngestionMessage
{
    string Symbol { get; }
    IReadOnlyList<TradeData> Trades { get; }
}

/// <summary>
/// Trade data record for batch processing.
/// </summary>
public record TradeData(
    DateTimeOffset Timestamp,
    decimal Price,
    long Size,
    string AggressorSide,
    string? TradeId = null,
    string? Exchange = null,
    string? Conditions = null
);

/// <summary>
/// Response after trade batch ingestion.
/// </summary>
public interface ITradesBatchIngested : IIngestionMessage
{
    string Symbol { get; }
    int TotalCount { get; }
    int SuccessCount { get; }
    int FailedCount { get; }
    string[] Errors { get; }
}
