using System.Text.Json.Serialization;

namespace MarketDataCollector.Storage.Export;

/// <summary>
/// Request model for export operations.
/// </summary>
public class ExportRequest
{
    /// <summary>
    /// Export profile to use.
    /// </summary>
    [JsonPropertyName("profileId")]
    public string ProfileId { get; set; } = "python-pandas";

    /// <summary>
    /// Custom profile (overrides profileId if provided).
    /// </summary>
    [JsonPropertyName("customProfile")]
    public ExportProfile? CustomProfile { get; set; }

    /// <summary>
    /// Symbols to export (null = all symbols).
    /// </summary>
    [JsonPropertyName("symbols")]
    public string[]? Symbols { get; set; }

    /// <summary>
    /// Event types to export (Trade, Quote, Depth, Bar).
    /// </summary>
    [JsonPropertyName("eventTypes")]
    public string[] EventTypes { get; set; } = new[] { "Trade", "BboQuote" };

    /// <summary>
    /// Start date for export range.
    /// </summary>
    [JsonPropertyName("startDate")]
    public DateTime StartDate { get; set; } = DateTime.UtcNow.AddDays(-7);

    /// <summary>
    /// End date for export range.
    /// </summary>
    [JsonPropertyName("endDate")]
    public DateTime EndDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Output directory path.
    /// </summary>
    [JsonPropertyName("outputDirectory")]
    public string OutputDirectory { get; set; } = string.Empty;

    /// <summary>
    /// Whether to overwrite existing files.
    /// </summary>
    [JsonPropertyName("overwriteExisting")]
    public bool OverwriteExisting { get; set; }

    /// <summary>
    /// Whether to validate data before export.
    /// </summary>
    [JsonPropertyName("validateBeforeExport")]
    public bool ValidateBeforeExport { get; set; } = true;

    /// <summary>
    /// Time series aggregation settings (null = export raw data).
    /// </summary>
    [JsonPropertyName("aggregation")]
    public AggregationSettings? Aggregation { get; set; }

    /// <summary>
    /// Feature engineering settings.
    /// </summary>
    [JsonPropertyName("features")]
    public FeatureSettings? Features { get; set; }

    /// <summary>
    /// Quality threshold (0-1). Exclude data below this quality score.
    /// </summary>
    [JsonPropertyName("minQualityScore")]
    public double? MinQualityScore { get; set; }

    /// <summary>
    /// Session filter (include only regular market hours, etc.)
    /// </summary>
    [JsonPropertyName("sessionFilter")]
    public SessionFilter SessionFilter { get; set; } = SessionFilter.All;
}

/// <summary>
/// Time series aggregation settings for export.
/// </summary>
public class AggregationSettings
{
    /// <summary>
    /// Aggregation interval (e.g., "1min", "5min", "1hour", "1day").
    /// </summary>
    [JsonPropertyName("interval")]
    public string Interval { get; set; } = "1min";

    /// <summary>
    /// Price aggregation method.
    /// </summary>
    [JsonPropertyName("priceAggregation")]
    public PriceAggregation PriceAggregation { get; set; } = PriceAggregation.Ohlc;

    /// <summary>
    /// Volume aggregation method.
    /// </summary>
    [JsonPropertyName("volumeAggregation")]
    public VolumeAggregation VolumeAggregation { get; set; } = VolumeAggregation.Sum;

    /// <summary>
    /// Gap handling strategy.
    /// </summary>
    [JsonPropertyName("gapHandling")]
    public GapHandling GapHandling { get; set; } = GapHandling.ForwardFill;

    /// <summary>
    /// Maximum number of gaps to fill.
    /// </summary>
    [JsonPropertyName("maxGapIntervals")]
    public int MaxGapIntervals { get; set; } = 5;

    /// <summary>
    /// Mark filled gaps with a flag column.
    /// </summary>
    [JsonPropertyName("markFilledGaps")]
    public bool MarkFilledGaps { get; set; } = true;
}

/// <summary>
/// Price aggregation methods.
/// </summary>
public enum PriceAggregation
{
    /// <summary>Open, High, Low, Close.</summary>
    Ohlc,
    /// <summary>Volume-weighted average price.</summary>
    Vwap,
    /// <summary>Time-weighted average price.</summary>
    Twap,
    /// <summary>Last price in interval.</summary>
    Last,
    /// <summary>Average price.</summary>
    Mean
}

/// <summary>
/// Volume aggregation methods.
/// </summary>
public enum VolumeAggregation
{
    Sum,
    Mean,
    Max,
    Last
}

/// <summary>
/// Gap handling strategies.
/// </summary>
public enum GapHandling
{
    /// <summary>Leave gaps as null/NaN.</summary>
    Null,
    /// <summary>Forward fill from last known value.</summary>
    ForwardFill,
    /// <summary>Linear interpolation.</summary>
    Interpolate,
    /// <summary>Skip gaps entirely.</summary>
    Skip
}

/// <summary>
/// Session filter options.
/// </summary>
public enum SessionFilter
{
    /// <summary>Include all data.</summary>
    All,
    /// <summary>Regular market hours only (9:30-16:00 ET).</summary>
    RegularHours,
    /// <summary>Pre-market only (4:00-9:30 ET).</summary>
    PreMarket,
    /// <summary>After-hours only (16:00-20:00 ET).</summary>
    AfterHours,
    /// <summary>Extended hours (pre + after).</summary>
    ExtendedHours
}

/// <summary>
/// Feature engineering settings for export.
/// </summary>
public class FeatureSettings
{
    /// <summary>
    /// Include basic return features.
    /// </summary>
    [JsonPropertyName("includeReturns")]
    public bool IncludeReturns { get; set; }

    /// <summary>
    /// Return horizons to compute (in bars).
    /// </summary>
    [JsonPropertyName("returnHorizons")]
    public int[] ReturnHorizons { get; set; } = new[] { 1, 5, 10 };

    /// <summary>
    /// Use log returns instead of simple returns.
    /// </summary>
    [JsonPropertyName("useLogReturns")]
    public bool UseLogReturns { get; set; } = true;

    /// <summary>
    /// Include rolling statistics.
    /// </summary>
    [JsonPropertyName("includeRollingStats")]
    public bool IncludeRollingStats { get; set; }

    /// <summary>
    /// Rolling windows for statistics.
    /// </summary>
    [JsonPropertyName("rollingWindows")]
    public int[] RollingWindows { get; set; } = new[] { 5, 10, 20, 50 };

    /// <summary>
    /// Include technical indicators.
    /// </summary>
    [JsonPropertyName("includeTechnicalIndicators")]
    public bool IncludeTechnicalIndicators { get; set; }

    /// <summary>
    /// Technical indicators to include.
    /// </summary>
    [JsonPropertyName("technicalIndicators")]
    public string[] TechnicalIndicators { get; set; } = new[] { "SMA", "EMA", "RSI" };

    /// <summary>
    /// Include microstructure features.
    /// </summary>
    [JsonPropertyName("includeMicrostructure")]
    public bool IncludeMicrostructure { get; set; }

    /// <summary>
    /// Normalize features to [0,1] or z-score.
    /// </summary>
    [JsonPropertyName("normalization")]
    public NormalizationType Normalization { get; set; } = NormalizationType.None;
}

/// <summary>
/// Feature normalization types.
/// </summary>
public enum NormalizationType
{
    /// <summary>No normalization.</summary>
    None,
    /// <summary>Min-max scaling to [0,1].</summary>
    MinMax,
    /// <summary>Z-score normalization.</summary>
    ZScore,
    /// <summary>Robust scaling (using median and IQR).</summary>
    Robust
}
