namespace DataIngestion.Contracts.Messages;

/// <summary>
/// Command to request historical data backfill.
/// </summary>
public interface IRequestHistoricalBackfill : IIngestionMessage
{
    string Symbol { get; }
    DateTimeOffset StartDate { get; }
    DateTimeOffset EndDate { get; }
    HistoricalDataType DataType { get; }
    string? Exchange { get; }
    BarTimeframe? Timeframe { get; }
    int? Priority { get; }
}

/// <summary>
/// Historical data types supported for backfill.
/// </summary>
public enum HistoricalDataType
{
    Trades,
    Quotes,
    OHLCV,
    OrderBook
}

/// <summary>
/// Bar timeframes for OHLCV data.
/// </summary>
public enum BarTimeframe
{
    Second,
    Minute,
    FiveMinutes,
    FifteenMinutes,
    ThirtyMinutes,
    Hour,
    FourHours,
    Day,
    Week,
    Month
}

/// <summary>
/// Event when historical backfill starts.
/// </summary>
public interface IHistoricalBackfillStarted : IIngestionMessage
{
    string Symbol { get; }
    DateTimeOffset StartDate { get; }
    DateTimeOffset EndDate { get; }
    HistoricalDataType DataType { get; }
    Guid BackfillJobId { get; }
    long EstimatedRecords { get; }
}

/// <summary>
/// Progress update during historical backfill.
/// </summary>
public interface IHistoricalBackfillProgress : IIngestionMessage
{
    Guid BackfillJobId { get; }
    string Symbol { get; }
    long RecordsProcessed { get; }
    long TotalRecords { get; }
    double ProgressPercent { get; }
    DateTimeOffset CurrentDate { get; }
    TimeSpan ElapsedTime { get; }
    TimeSpan? EstimatedTimeRemaining { get; }
}

/// <summary>
/// Event when historical backfill completes.
/// </summary>
public interface IHistoricalBackfillCompleted : IIngestionMessage
{
    Guid BackfillJobId { get; }
    string Symbol { get; }
    DateTimeOffset StartDate { get; }
    DateTimeOffset EndDate { get; }
    HistoricalDataType DataType { get; }
    long TotalRecords { get; }
    TimeSpan Duration { get; }
    bool Success { get; }
    string? ErrorMessage { get; }
}

/// <summary>
/// OHLCV bar data for historical ingestion.
/// </summary>
public record OhlcvBar(
    DateTimeOffset Timestamp,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    long Volume,
    long? TradeCount = null,
    decimal? Vwap = null
);

/// <summary>
/// Batch of historical OHLCV bars.
/// </summary>
public interface IHistoricalBarsIngested : IIngestionMessage
{
    string Symbol { get; }
    BarTimeframe Timeframe { get; }
    IReadOnlyList<OhlcvBar> Bars { get; }
    DateTimeOffset RangeStart { get; }
    DateTimeOffset RangeEnd { get; }
}
