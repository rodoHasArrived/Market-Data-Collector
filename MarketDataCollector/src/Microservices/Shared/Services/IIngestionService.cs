namespace DataIngestion.Contracts.Services;

/// <summary>
/// Base interface for all ingestion services.
/// </summary>
public interface IIngestionService
{
    /// <summary>Service health status.</summary>
    IngestionServiceHealth GetHealth();

    /// <summary>Service metrics.</summary>
    IngestionServiceMetrics GetMetrics();

    /// <summary>Start the service.</summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>Stop the service gracefully.</summary>
    Task StopAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Health status for an ingestion service.
/// </summary>
public record IngestionServiceHealth(
    string ServiceName,
    bool IsHealthy,
    DateTimeOffset LastChecked,
    IReadOnlyDictionary<string, ComponentHealth> Components
);

/// <summary>
/// Health status for a service component.
/// </summary>
public record ComponentHealth(
    string Name,
    bool IsHealthy,
    string? StatusMessage = null,
    DateTimeOffset? LastSuccessfulOperation = null
);

/// <summary>
/// Metrics for an ingestion service.
/// </summary>
public record IngestionServiceMetrics(
    long TotalMessagesReceived,
    long TotalMessagesProcessed,
    long TotalMessagesFailed,
    double MessagesPerSecond,
    double AverageProcessingTimeMs,
    double P95ProcessingTimeMs,
    double P99ProcessingTimeMs,
    long QueueDepth,
    DateTimeOffset CollectedAt
);

/// <summary>
/// Interface for trade ingestion operations.
/// </summary>
public interface ITradeIngestionService : IIngestionService
{
    /// <summary>Ingest a single trade.</summary>
    Task<IngestionResult> IngestTradeAsync(TradeIngestionRequest request, CancellationToken ct = default);

    /// <summary>Ingest a batch of trades.</summary>
    Task<BatchIngestionResult> IngestTradesBatchAsync(IEnumerable<TradeIngestionRequest> requests, CancellationToken ct = default);
}

/// <summary>
/// Interface for order book ingestion operations.
/// </summary>
public interface IOrderBookIngestionService : IIngestionService
{
    /// <summary>Ingest order book snapshot.</summary>
    Task<IngestionResult> IngestSnapshotAsync(OrderBookSnapshotRequest request, CancellationToken ct = default);

    /// <summary>Ingest order book update/delta.</summary>
    Task<IngestionResult> IngestUpdateAsync(OrderBookUpdateRequest request, CancellationToken ct = default);
}

/// <summary>
/// Interface for quote ingestion operations.
/// </summary>
public interface IQuoteIngestionService : IIngestionService
{
    /// <summary>Ingest a single quote.</summary>
    Task<IngestionResult> IngestQuoteAsync(QuoteIngestionRequest request, CancellationToken ct = default);

    /// <summary>Ingest NBBO update.</summary>
    Task<IngestionResult> IngestNbboAsync(NbboIngestionRequest request, CancellationToken ct = default);
}

/// <summary>
/// Interface for historical data ingestion.
/// </summary>
public interface IHistoricalIngestionService : IIngestionService
{
    /// <summary>Start a backfill job.</summary>
    Task<BackfillJobStatus> StartBackfillAsync(BackfillRequest request, CancellationToken ct = default);

    /// <summary>Get backfill job status.</summary>
    Task<BackfillJobStatus> GetBackfillStatusAsync(Guid jobId, CancellationToken ct = default);

    /// <summary>Cancel a backfill job.</summary>
    Task CancelBackfillAsync(Guid jobId, CancellationToken ct = default);
}

#region Request/Result Types

public record TradeIngestionRequest(
    string Symbol,
    DateTimeOffset Timestamp,
    decimal Price,
    long Size,
    string AggressorSide,
    string Source,
    string? TradeId = null,
    string? Exchange = null
);

public record OrderBookSnapshotRequest(
    string Symbol,
    DateTimeOffset Timestamp,
    IReadOnlyList<(decimal Price, long Size, string? MarketMaker)> Bids,
    IReadOnlyList<(decimal Price, long Size, string? MarketMaker)> Asks,
    string Source,
    string? Exchange = null
);

public record OrderBookUpdateRequest(
    string Symbol,
    DateTimeOffset Timestamp,
    string UpdateType,
    string Side,
    int Position,
    decimal? Price,
    long? Size,
    string Source,
    string? MarketMaker = null,
    string? Exchange = null
);

public record QuoteIngestionRequest(
    string Symbol,
    DateTimeOffset Timestamp,
    decimal BidPrice,
    long BidSize,
    decimal AskPrice,
    long AskSize,
    string Source,
    string? BidExchange = null,
    string? AskExchange = null
);

public record NbboIngestionRequest(
    string Symbol,
    DateTimeOffset Timestamp,
    decimal BidPrice,
    long BidSize,
    string BidExchange,
    decimal AskPrice,
    long AskSize,
    string AskExchange,
    string Source
);

public record BackfillRequest(
    string Symbol,
    DateTimeOffset StartDate,
    DateTimeOffset EndDate,
    string DataType,
    string Source,
    string? Exchange = null,
    string? Timeframe = null,
    int Priority = 0
);

public record IngestionResult(
    bool Success,
    string? MessageId = null,
    string? ErrorMessage = null,
    TimeSpan? ProcessingTime = null
);

public record BatchIngestionResult(
    int TotalCount,
    int SuccessCount,
    int FailedCount,
    IReadOnlyList<string> Errors,
    TimeSpan ProcessingTime
);

public record BackfillJobStatus(
    Guid JobId,
    string Symbol,
    string Status,
    DateTimeOffset StartDate,
    DateTimeOffset EndDate,
    long? RecordsProcessed = null,
    long? TotalRecords = null,
    double? ProgressPercent = null,
    DateTimeOffset? StartedAt = null,
    DateTimeOffset? CompletedAt = null,
    string? ErrorMessage = null
);

#endregion
