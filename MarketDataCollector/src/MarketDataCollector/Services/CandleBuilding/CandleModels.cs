using MarketDataCollector.Domain.Models;

namespace MarketDataCollector.Services.CandleBuilding;

/// <summary>
/// Types of candles that can be built from tick data.
/// Mirrors StockSharp Hydra's candle building capabilities.
/// </summary>
public enum CandleType
{
    /// <summary>Time-based candles (1min, 5min, 1hour, etc.)</summary>
    Time,

    /// <summary>Volume-based candles (new candle after N volume traded)</summary>
    Volume,

    /// <summary>Tick-based candles (new candle after N trades)</summary>
    Tick,

    /// <summary>Range candles (new candle when price moves N points)</summary>
    Range,

    /// <summary>Renko candles (fixed price movement bricks)</summary>
    Renko,

    /// <summary>Point & Figure candles (X and O boxes)</summary>
    PointAndFigure,

    /// <summary>Heikin-Ashi smoothed candles</summary>
    HeikinAshi
}

/// <summary>
/// Configuration for candle building.
/// </summary>
public sealed record CandleBuildConfig
{
    /// <summary>Type of candle to build.</summary>
    public CandleType Type { get; init; } = CandleType.Time;

    /// <summary>
    /// Parameter value based on candle type:
    /// - Time: seconds (60 = 1 minute, 3600 = 1 hour)
    /// - Volume: total volume threshold
    /// - Tick: number of trades
    /// - Range: price range in ticks
    /// - Renko: brick size in price units
    /// - P&F: box size in price units
    /// </summary>
    public decimal Parameter { get; init; } = 60;

    /// <summary>For P&F candles: reversal amount (number of boxes).</summary>
    public int ReversalAmount { get; init; } = 3;

    /// <summary>Whether to include extended hours data.</summary>
    public bool IncludeExtendedHours { get; init; } = false;

    /// <summary>Tick size for the instrument (for Range/Renko calculations).</summary>
    public decimal TickSize { get; init; } = 0.01m;
}

/// <summary>
/// Represents a built candle with OHLCV data.
/// Extended from HistoricalBar to include additional metadata.
/// </summary>
public sealed record Candle
{
    /// <summary>Symbol for this candle.</summary>
    public required string Symbol { get; init; }

    /// <summary>Candle open time.</summary>
    public required DateTimeOffset OpenTime { get; init; }

    /// <summary>Candle close time.</summary>
    public required DateTimeOffset CloseTime { get; init; }

    /// <summary>Opening price.</summary>
    public required decimal Open { get; init; }

    /// <summary>Highest price.</summary>
    public required decimal High { get; init; }

    /// <summary>Lowest price.</summary>
    public required decimal Low { get; init; }

    /// <summary>Closing price.</summary>
    public required decimal Close { get; init; }

    /// <summary>Total volume traded.</summary>
    public required long Volume { get; init; }

    /// <summary>Number of trades in this candle.</summary>
    public int TradeCount { get; init; }

    /// <summary>Volume-weighted average price.</summary>
    public decimal? Vwap { get; init; }

    /// <summary>Buy volume (aggressor = buy).</summary>
    public long BuyVolume { get; init; }

    /// <summary>Sell volume (aggressor = sell).</summary>
    public long SellVolume { get; init; }

    /// <summary>Candle type that was used to build this.</summary>
    public CandleType Type { get; init; }

    /// <summary>Whether this candle is complete or still building.</summary>
    public CandleState State { get; init; } = CandleState.Finished;

    /// <summary>
    /// Convert to HistoricalBar for storage.
    /// </summary>
    public HistoricalBar ToHistoricalBar(string source = "candlebuilder")
    {
        return new HistoricalBar(
            Symbol: Symbol,
            SessionDate: DateOnly.FromDateTime(OpenTime.DateTime),
            Open: Open,
            High: High,
            Low: Low,
            Close: Close,
            Volume: Volume,
            Source: source,
            SequenceNumber: OpenTime.ToUnixTimeMilliseconds()
        );
    }

    /// <summary>
    /// Calculate the body size (absolute difference between open and close).
    /// </summary>
    public decimal BodySize => Math.Abs(Close - Open);

    /// <summary>
    /// Calculate the full range (high - low).
    /// </summary>
    public decimal Range => High - Low;

    /// <summary>
    /// Whether this is a bullish (green) candle.
    /// </summary>
    public bool IsBullish => Close > Open;

    /// <summary>
    /// Whether this is a bearish (red) candle.
    /// </summary>
    public bool IsBearish => Close < Open;

    /// <summary>
    /// Upper shadow size.
    /// </summary>
    public decimal UpperShadow => High - Math.Max(Open, Close);

    /// <summary>
    /// Lower shadow size.
    /// </summary>
    public decimal LowerShadow => Math.Min(Open, Close) - Low;

    /// <summary>
    /// Order flow imbalance (-1 to +1, positive = more buying).
    /// </summary>
    public double Imbalance => Volume > 0
        ? (double)(BuyVolume - SellVolume) / Volume
        : 0;
}

/// <summary>
/// State of a candle during building.
/// </summary>
public enum CandleState
{
    /// <summary>Candle is still accepting trades.</summary>
    Active,

    /// <summary>Candle is complete.</summary>
    Finished
}
