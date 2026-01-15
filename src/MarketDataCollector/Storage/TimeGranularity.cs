namespace MarketDataCollector.Storage;

/// <summary>
/// Time granularity for bar/candle data.
/// Supports standard trading timeframes from tick-level to monthly.
/// </summary>
public enum TimeGranularity
{
    /// <summary>
    /// Tick-by-tick data (no aggregation).
    /// File suffix: tick
    /// </summary>
    Tick = 0,

    /// <summary>
    /// 1-second bars.
    /// File suffix: 1s
    /// </summary>
    Second1 = 1,

    /// <summary>
    /// 5-second bars.
    /// File suffix: 5s
    /// </summary>
    Second5 = 5,

    /// <summary>
    /// 15-second bars.
    /// File suffix: 15s
    /// </summary>
    Second15 = 15,

    /// <summary>
    /// 30-second bars.
    /// File suffix: 30s
    /// </summary>
    Second30 = 30,

    /// <summary>
    /// 1-minute bars.
    /// File suffix: 1m
    /// </summary>
    Minute1 = 60,

    /// <summary>
    /// 5-minute bars.
    /// File suffix: 5m
    /// </summary>
    Minute5 = 300,

    /// <summary>
    /// 15-minute bars.
    /// File suffix: 15m
    /// </summary>
    Minute15 = 900,

    /// <summary>
    /// 30-minute bars.
    /// File suffix: 30m
    /// </summary>
    Minute30 = 1800,

    /// <summary>
    /// 1-hour bars.
    /// File suffix: 1h
    /// </summary>
    Hour1 = 3600,

    /// <summary>
    /// 4-hour bars.
    /// File suffix: 4h
    /// </summary>
    Hour4 = 14400,

    /// <summary>
    /// Daily bars (end-of-day).
    /// File suffix: daily
    /// </summary>
    Daily = 86400,

    /// <summary>
    /// Weekly bars.
    /// File suffix: weekly
    /// </summary>
    Weekly = 604800,

    /// <summary>
    /// Monthly bars.
    /// File suffix: monthly
    /// </summary>
    Monthly = 2592000
}

/// <summary>
/// Extension methods for TimeGranularity.
/// </summary>
public static class TimeGranularityExtensions
{
    /// <summary>
    /// Gets the file suffix for a time granularity (e.g., "1m", "daily").
    /// </summary>
    public static string ToFileSuffix(this TimeGranularity granularity) => granularity switch
    {
        TimeGranularity.Tick => "tick",
        TimeGranularity.Second1 => "1s",
        TimeGranularity.Second5 => "5s",
        TimeGranularity.Second15 => "15s",
        TimeGranularity.Second30 => "30s",
        TimeGranularity.Minute1 => "1m",
        TimeGranularity.Minute5 => "5m",
        TimeGranularity.Minute15 => "15m",
        TimeGranularity.Minute30 => "30m",
        TimeGranularity.Hour1 => "1h",
        TimeGranularity.Hour4 => "4h",
        TimeGranularity.Daily => "daily",
        TimeGranularity.Weekly => "weekly",
        TimeGranularity.Monthly => "monthly",
        _ => "unknown"
    };

    /// <summary>
    /// Gets the display name for a time granularity.
    /// </summary>
    public static string ToDisplayName(this TimeGranularity granularity) => granularity switch
    {
        TimeGranularity.Tick => "Tick",
        TimeGranularity.Second1 => "1 Second",
        TimeGranularity.Second5 => "5 Seconds",
        TimeGranularity.Second15 => "15 Seconds",
        TimeGranularity.Second30 => "30 Seconds",
        TimeGranularity.Minute1 => "1 Minute",
        TimeGranularity.Minute5 => "5 Minutes",
        TimeGranularity.Minute15 => "15 Minutes",
        TimeGranularity.Minute30 => "30 Minutes",
        TimeGranularity.Hour1 => "1 Hour",
        TimeGranularity.Hour4 => "4 Hours",
        TimeGranularity.Daily => "Daily",
        TimeGranularity.Weekly => "Weekly",
        TimeGranularity.Monthly => "Monthly",
        _ => "Unknown"
    };

    /// <summary>
    /// Gets the TimeSpan for a time granularity (returns TimeSpan.Zero for Tick).
    /// </summary>
    public static TimeSpan ToTimeSpan(this TimeGranularity granularity) => granularity switch
    {
        TimeGranularity.Tick => TimeSpan.Zero,
        TimeGranularity.Monthly => TimeSpan.FromDays(30), // Approximate
        _ => TimeSpan.FromSeconds((int)granularity)
    };

    /// <summary>
    /// Parses a file suffix string into a TimeGranularity.
    /// </summary>
    public static TimeGranularity? ParseFileSuffix(string suffix) => suffix?.ToLowerInvariant() switch
    {
        "tick" => TimeGranularity.Tick,
        "1s" => TimeGranularity.Second1,
        "5s" => TimeGranularity.Second5,
        "15s" => TimeGranularity.Second15,
        "30s" => TimeGranularity.Second30,
        "1m" => TimeGranularity.Minute1,
        "5m" => TimeGranularity.Minute5,
        "15m" => TimeGranularity.Minute15,
        "30m" => TimeGranularity.Minute30,
        "1h" => TimeGranularity.Hour1,
        "4h" => TimeGranularity.Hour4,
        "daily" or "1d" => TimeGranularity.Daily,
        "weekly" or "1w" => TimeGranularity.Weekly,
        "monthly" or "1mo" => TimeGranularity.Monthly,
        _ => null
    };

    /// <summary>
    /// Determines the appropriate date partition for a granularity.
    /// Tick/second data partitions by hour, minute data by day, daily+ by month.
    /// </summary>
    public static DatePartition GetRecommendedPartition(this TimeGranularity granularity) => granularity switch
    {
        TimeGranularity.Tick => DatePartition.Hourly,
        TimeGranularity.Second1 or TimeGranularity.Second5 or
        TimeGranularity.Second15 or TimeGranularity.Second30 => DatePartition.Hourly,
        TimeGranularity.Minute1 or TimeGranularity.Minute5 or
        TimeGranularity.Minute15 or TimeGranularity.Minute30 or
        TimeGranularity.Hour1 or TimeGranularity.Hour4 => DatePartition.Daily,
        TimeGranularity.Daily => DatePartition.Monthly,
        TimeGranularity.Weekly or TimeGranularity.Monthly => DatePartition.None,
        _ => DatePartition.Daily
    };
}
