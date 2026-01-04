using System.Runtime.CompilerServices;
using MarketDataCollector.Application.Logging;
using MarketDataCollector.Domain.Events;
using MarketDataCollector.Domain.Models;
using MarketDataCollector.Services.CandleBuilding;
using Serilog;

namespace MarketDataCollector.Services.DataConversion;

/// <summary>
/// Pipeline for converting between different market data types.
/// Inspired by StockSharp Hydra's data transformation capabilities.
///
/// Supported conversions:
/// - Ticks → Candles (any timeframe/type)
/// - L2 Snapshots → BBO Quotes
/// - BBO Quotes → Trades (synthetic)
/// - Candles → Higher timeframe candles
/// - Multiple formats → Unified format
/// </summary>
public sealed class DataConversionPipeline
{
    private readonly ILogger _log = LoggingSetup.ForContext<DataConversionPipeline>();

    /// <summary>
    /// Convert trades to candles of specified configuration.
    /// </summary>
    public async IAsyncEnumerable<Candle> TicksToCandlesAsync(
        IAsyncEnumerable<Trade> trades,
        CandleBuildConfig config,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var builder = new CandleBuilder(config);

        await foreach (var candle in builder.BuildCandlesAsync(trades, ct))
        {
            yield return candle;
        }
    }

    /// <summary>
    /// Convert synchronous trade collection to candles.
    /// </summary>
    public IEnumerable<Candle> TicksToCandles(
        IEnumerable<Trade> trades,
        CandleBuildConfig config)
    {
        var builder = new CandleBuilder(config);
        return builder.ProcessTrades(trades);
    }

    /// <summary>
    /// Convert L2 order book snapshots to BBO quotes.
    /// </summary>
    public async IAsyncEnumerable<BboQuotePayload> L2ToBboAsync(
        IAsyncEnumerable<LOBSnapshot> snapshots,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        long sequence = 0;

        await foreach (var snapshot in snapshots.WithCancellation(ct))
        {
            var bestBid = snapshot.Bids.FirstOrDefault();
            var bestAsk = snapshot.Asks.FirstOrDefault();

            if (bestBid == null && bestAsk == null) continue;

            var bidPrice = bestBid?.Price ?? 0;
            var askPrice = bestAsk?.Price ?? 0;
            var bidSize = (long)(bestBid?.Size ?? 0);
            var askSize = (long)(bestAsk?.Size ?? 0);

            decimal? midPrice = null;
            decimal? spread = null;

            if (bidPrice > 0 && askPrice > 0 && askPrice >= bidPrice)
            {
                spread = askPrice - bidPrice;
                midPrice = bidPrice + (spread.Value / 2m);
            }

            yield return new BboQuotePayload(
                Timestamp: snapshot.Timestamp,
                Symbol: snapshot.Symbol,
                BidPrice: bidPrice,
                BidSize: bidSize,
                AskPrice: askPrice,
                AskSize: askSize,
                MidPrice: midPrice,
                Spread: spread,
                SequenceNumber: sequence++,
                StreamId: snapshot.StreamId,
                Venue: snapshot.Venue
            );
        }
    }

    /// <summary>
    /// Convert lower timeframe candles to higher timeframe.
    /// </summary>
    public async IAsyncEnumerable<Candle> ResampleCandlesAsync(
        IAsyncEnumerable<Candle> candles,
        TimeSpan targetTimeframe,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        Candle? accumulator = null;
        DateTimeOffset? currentBucketStart = null;

        await foreach (var candle in candles.WithCancellation(ct))
        {
            var bucketStart = GetBucketStart(candle.OpenTime, targetTimeframe);

            if (currentBucketStart == null)
            {
                currentBucketStart = bucketStart;
                accumulator = candle;
                continue;
            }

            if (bucketStart != currentBucketStart)
            {
                // Emit accumulated candle
                if (accumulator != null)
                {
                    yield return accumulator with { State = CandleState.Finished };
                }

                currentBucketStart = bucketStart;
                accumulator = candle;
            }
            else
            {
                // Merge into accumulator
                accumulator = MergeCandles(accumulator!, candle);
            }
        }

        // Emit final candle
        if (accumulator != null)
        {
            yield return accumulator with { State = CandleState.Finished };
        }
    }

    /// <summary>
    /// Convert market events to specific data types.
    /// </summary>
    public async IAsyncEnumerable<T> ExtractPayloadAsync<T>(
        IAsyncEnumerable<MarketEvent> events,
        [EnumeratorCancellation] CancellationToken ct = default)
        where T : class
    {
        await foreach (var evt in events.WithCancellation(ct))
        {
            if (evt.Payload is T payload)
            {
                yield return payload;
            }
        }
    }

    /// <summary>
    /// Calculate VWAP from trades.
    /// </summary>
    public async Task<VwapResult> CalculateVwapAsync(
        IAsyncEnumerable<Trade> trades,
        CancellationToken ct = default)
    {
        decimal totalValue = 0;
        long totalVolume = 0;
        int tradeCount = 0;
        decimal high = decimal.MinValue;
        decimal low = decimal.MaxValue;
        DateTimeOffset? firstTime = null;
        DateTimeOffset lastTime = default;

        await foreach (var trade in trades.WithCancellation(ct))
        {
            firstTime ??= trade.Timestamp;
            lastTime = trade.Timestamp;

            totalValue += trade.Price * trade.Size;
            totalVolume += trade.Size;
            tradeCount++;
            high = Math.Max(high, trade.Price);
            low = Math.Min(low, trade.Price);
        }

        return new VwapResult
        {
            Vwap = totalVolume > 0 ? totalValue / totalVolume : 0,
            TotalVolume = totalVolume,
            TradeCount = tradeCount,
            High = high == decimal.MinValue ? 0 : high,
            Low = low == decimal.MaxValue ? 0 : low,
            StartTime = firstTime ?? DateTimeOffset.MinValue,
            EndTime = lastTime
        };
    }

    /// <summary>
    /// Build volume profile from trades.
    /// </summary>
    public async Task<VolumeProfile> BuildVolumeProfileAsync(
        IAsyncEnumerable<Trade> trades,
        decimal tickSize,
        CancellationToken ct = default)
    {
        var levels = new Dictionary<decimal, VolumeLevelData>();
        long totalVolume = 0;

        await foreach (var trade in trades.WithCancellation(ct))
        {
            // Round price to tick size
            var priceLevel = Math.Round(trade.Price / tickSize) * tickSize;

            if (!levels.TryGetValue(priceLevel, out var level))
            {
                level = new VolumeLevelData { Price = priceLevel };
                levels[priceLevel] = level;
            }

            level.Volume += trade.Size;
            level.TradeCount++;
            totalVolume += trade.Size;

            if (trade.Aggressor == AggressorSide.Buy)
                level.BuyVolume += trade.Size;
            else if (trade.Aggressor == AggressorSide.Sell)
                level.SellVolume += trade.Size;
        }

        var sortedLevels = levels.Values.OrderByDescending(l => l.Price).ToList();
        var poc = sortedLevels.MaxBy(l => l.Volume);

        return new VolumeProfile
        {
            Levels = sortedLevels,
            TotalVolume = totalVolume,
            PointOfControl = poc?.Price ?? 0,
            ValueAreaHigh = CalculateValueAreaHigh(sortedLevels, totalVolume),
            ValueAreaLow = CalculateValueAreaLow(sortedLevels, totalVolume)
        };
    }

    /// <summary>
    /// Aggregate order book imbalance over time.
    /// </summary>
    public async IAsyncEnumerable<ImbalanceData> CalculateImbalanceAsync(
        IAsyncEnumerable<LOBSnapshot> snapshots,
        int levels = 5,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var snapshot in snapshots.WithCancellation(ct))
        {
            var bidVolume = snapshot.Bids.Take(levels).Sum(b => b.Size);
            var askVolume = snapshot.Asks.Take(levels).Sum(a => a.Size);
            var totalVolume = bidVolume + askVolume;

            yield return new ImbalanceData
            {
                Timestamp = snapshot.Timestamp,
                Symbol = snapshot.Symbol,
                BidVolume = bidVolume,
                AskVolume = askVolume,
                Imbalance = totalVolume > 0 ? (double)(bidVolume - askVolume) / (double)totalVolume : 0,
                BidLevels = snapshot.Bids.Count,
                AskLevels = snapshot.Asks.Count
            };
        }
    }

    #region Private Helpers

    private static DateTimeOffset GetBucketStart(DateTimeOffset time, TimeSpan interval)
    {
        var ticks = time.UtcTicks;
        var intervalTicks = interval.Ticks;
        var bucketTicks = (ticks / intervalTicks) * intervalTicks;
        return new DateTimeOffset(bucketTicks, TimeSpan.Zero);
    }

    private static Candle MergeCandles(Candle first, Candle second)
    {
        return first with
        {
            CloseTime = second.CloseTime,
            High = Math.Max(first.High, second.High),
            Low = Math.Min(first.Low, second.Low),
            Close = second.Close,
            Volume = first.Volume + second.Volume,
            TradeCount = first.TradeCount + second.TradeCount,
            BuyVolume = first.BuyVolume + second.BuyVolume,
            SellVolume = first.SellVolume + second.SellVolume,
            Vwap = CalculateCombinedVwap(first, second)
        };
    }

    private static decimal? CalculateCombinedVwap(Candle first, Candle second)
    {
        if (!first.Vwap.HasValue && !second.Vwap.HasValue) return null;

        var totalVolume = first.Volume + second.Volume;
        if (totalVolume == 0) return null;

        var firstValue = (first.Vwap ?? first.Close) * first.Volume;
        var secondValue = (second.Vwap ?? second.Close) * second.Volume;

        return (firstValue + secondValue) / totalVolume;
    }

    private static decimal CalculateValueAreaHigh(List<VolumeLevelData> levels, long totalVolume)
    {
        // Value area contains 70% of volume
        var targetVolume = totalVolume * 0.7m;
        var poc = levels.MaxBy(l => l.Volume);
        if (poc == null) return 0;

        var pocIndex = levels.IndexOf(poc);
        var accumulated = poc.Volume;
        var highIndex = pocIndex;
        var lowIndex = pocIndex;

        while (accumulated < targetVolume && (highIndex > 0 || lowIndex < levels.Count - 1))
        {
            var upVolume = highIndex > 0 ? levels[highIndex - 1].Volume : 0;
            var downVolume = lowIndex < levels.Count - 1 ? levels[lowIndex + 1].Volume : 0;

            if (upVolume >= downVolume && highIndex > 0)
            {
                highIndex--;
                accumulated += levels[highIndex].Volume;
            }
            else if (lowIndex < levels.Count - 1)
            {
                lowIndex++;
                accumulated += levels[lowIndex].Volume;
            }
            else break;
        }

        return levels[highIndex].Price;
    }

    private static decimal CalculateValueAreaLow(List<VolumeLevelData> levels, long totalVolume)
    {
        var targetVolume = totalVolume * 0.7m;
        var poc = levels.MaxBy(l => l.Volume);
        if (poc == null) return 0;

        var pocIndex = levels.IndexOf(poc);
        var accumulated = poc.Volume;
        var highIndex = pocIndex;
        var lowIndex = pocIndex;

        while (accumulated < targetVolume && (highIndex > 0 || lowIndex < levels.Count - 1))
        {
            var upVolume = highIndex > 0 ? levels[highIndex - 1].Volume : 0;
            var downVolume = lowIndex < levels.Count - 1 ? levels[lowIndex + 1].Volume : 0;

            if (upVolume >= downVolume && highIndex > 0)
            {
                highIndex--;
                accumulated += levels[highIndex].Volume;
            }
            else if (lowIndex < levels.Count - 1)
            {
                lowIndex++;
                accumulated += levels[lowIndex].Volume;
            }
            else break;
        }

        return levels[lowIndex].Price;
    }

    #endregion
}

/// <summary>
/// VWAP calculation result.
/// </summary>
public sealed record VwapResult
{
    public decimal Vwap { get; init; }
    public long TotalVolume { get; init; }
    public int TradeCount { get; init; }
    public decimal High { get; init; }
    public decimal Low { get; init; }
    public DateTimeOffset StartTime { get; init; }
    public DateTimeOffset EndTime { get; init; }
}

/// <summary>
/// Volume profile result.
/// </summary>
public sealed record VolumeProfile
{
    public required IReadOnlyList<VolumeLevelData> Levels { get; init; }
    public long TotalVolume { get; init; }
    public decimal PointOfControl { get; init; }
    public decimal ValueAreaHigh { get; init; }
    public decimal ValueAreaLow { get; init; }
}

/// <summary>
/// Volume data at a single price level.
/// </summary>
public sealed class VolumeLevelData
{
    public decimal Price { get; init; }
    public long Volume { get; set; }
    public long BuyVolume { get; set; }
    public long SellVolume { get; set; }
    public int TradeCount { get; set; }

    public double Imbalance => Volume > 0 ? (double)(BuyVolume - SellVolume) / Volume : 0;
}

/// <summary>
/// Order book imbalance data point.
/// </summary>
public sealed record ImbalanceData
{
    public DateTimeOffset Timestamp { get; init; }
    public required string Symbol { get; init; }
    public decimal BidVolume { get; init; }
    public decimal AskVolume { get; init; }
    public double Imbalance { get; init; }
    public int BidLevels { get; init; }
    public int AskLevels { get; init; }
}
