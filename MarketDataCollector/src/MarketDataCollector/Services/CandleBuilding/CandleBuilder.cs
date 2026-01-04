using System.Collections.Concurrent;
using MarketDataCollector.Application.Logging;
using MarketDataCollector.Domain.Models;
using Serilog;

namespace MarketDataCollector.Services.CandleBuilding;

/// <summary>
/// Builds various types of candles from tick data in real-time.
/// Inspired by StockSharp Hydra's candle building capabilities.
///
/// Supported candle types:
/// - Time-based (1min, 5min, 1hour, etc.)
/// - Volume-based (new candle after N volume)
/// - Tick-based (new candle after N trades)
/// - Range (new candle when price moves N points)
/// - Renko (fixed size bricks)
/// - Heikin-Ashi (smoothed candles)
/// </summary>
public sealed class CandleBuilder
{
    private readonly ILogger _log = LoggingSetup.ForContext<CandleBuilder>();
    private readonly CandleBuildConfig _config;
    private readonly ConcurrentDictionary<string, CandleBuilderState> _states = new();

    /// <summary>
    /// Event raised when a candle is completed.
    /// </summary>
    public event Action<Candle>? CandleCompleted;

    /// <summary>
    /// Event raised when a candle is updated (for real-time display).
    /// </summary>
    public event Action<Candle>? CandleUpdated;

    public CandleBuilder(CandleBuildConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _log.Debug("CandleBuilder initialized: Type={Type}, Param={Param}",
            config.Type, config.Parameter);
    }

    /// <summary>
    /// Process a trade and update/complete candles as needed.
    /// </summary>
    public void ProcessTrade(Trade trade)
    {
        if (trade == null) return;

        var state = _states.GetOrAdd(trade.Symbol, _ => new CandleBuilderState(_config));
        var result = state.ProcessTrade(trade, _config);

        if (result.CompletedCandle != null)
        {
            CandleCompleted?.Invoke(result.CompletedCandle);
        }

        if (result.CurrentCandle != null)
        {
            CandleUpdated?.Invoke(result.CurrentCandle);
        }
    }

    /// <summary>
    /// Process multiple trades (for batch building from historical data).
    /// </summary>
    public IEnumerable<Candle> ProcessTrades(IEnumerable<Trade> trades)
    {
        var completedCandles = new List<Candle>();

        foreach (var trade in trades)
        {
            var state = _states.GetOrAdd(trade.Symbol, _ => new CandleBuilderState(_config));
            var result = state.ProcessTrade(trade, _config);

            if (result.CompletedCandle != null)
            {
                completedCandles.Add(result.CompletedCandle);
            }
        }

        return completedCandles;
    }

    /// <summary>
    /// Build candles from a sequence of trades asynchronously.
    /// </summary>
    public async IAsyncEnumerable<Candle> BuildCandlesAsync(
        IAsyncEnumerable<Trade> trades,
        CancellationToken ct = default)
    {
        await foreach (var trade in trades.WithCancellation(ct))
        {
            var state = _states.GetOrAdd(trade.Symbol, _ => new CandleBuilderState(_config));
            var result = state.ProcessTrade(trade, _config);

            if (result.CompletedCandle != null)
            {
                yield return result.CompletedCandle;
            }
        }

        // Yield any remaining incomplete candles
        foreach (var kvp in _states)
        {
            var finalCandle = kvp.Value.FlushCurrentCandle();
            if (finalCandle != null)
            {
                yield return finalCandle;
            }
        }
    }

    /// <summary>
    /// Force-close all current candles and return them.
    /// Useful at end-of-session or when shutting down.
    /// </summary>
    public IEnumerable<Candle> FlushAll()
    {
        foreach (var kvp in _states)
        {
            var candle = kvp.Value.FlushCurrentCandle();
            if (candle != null)
            {
                yield return candle;
            }
        }
        _states.Clear();
    }

    /// <summary>
    /// Get the current (incomplete) candle for a symbol.
    /// </summary>
    public Candle? GetCurrentCandle(string symbol)
    {
        if (_states.TryGetValue(symbol, out var state))
        {
            return state.GetCurrentCandle();
        }
        return null;
    }

    /// <summary>
    /// Reset state for a specific symbol.
    /// </summary>
    public void Reset(string symbol)
    {
        _states.TryRemove(symbol, out _);
    }

    /// <summary>
    /// Reset all state.
    /// </summary>
    public void ResetAll()
    {
        _states.Clear();
    }
}

/// <summary>
/// Internal state for building candles for a single symbol.
/// </summary>
internal sealed class CandleBuilderState
{
    private readonly CandleBuildConfig _config;
    private CandleInProgress? _current;
    private Candle? _previousCandle; // For Heikin-Ashi

    public CandleBuilderState(CandleBuildConfig config)
    {
        _config = config;
    }

    public (Candle? CompletedCandle, Candle? CurrentCandle) ProcessTrade(Trade trade, CandleBuildConfig config)
    {
        Candle? completed = null;

        // Initialize new candle if needed
        if (_current == null)
        {
            _current = new CandleInProgress(trade.Symbol, trade.Timestamp, config.Type);
        }

        // Check if we should complete the current candle before adding this trade
        if (ShouldComplete(trade, config))
        {
            completed = _current.Complete();
            _previousCandle = completed;

            // Start new candle
            _current = new CandleInProgress(trade.Symbol, trade.Timestamp, config.Type);
        }

        // Add trade to current candle
        _current.AddTrade(trade);

        // For Heikin-Ashi, apply transformation
        var currentCandle = config.Type == CandleType.HeikinAshi
            ? _current.ToHeikinAshi(_previousCandle)
            : _current.ToCandle();

        return (completed, currentCandle);
    }

    private bool ShouldComplete(Trade trade, CandleBuildConfig config)
    {
        if (_current == null) return false;

        return config.Type switch
        {
            CandleType.Time => ShouldCompleteTime(trade, config),
            CandleType.Volume => _current.Volume >= (long)config.Parameter,
            CandleType.Tick => _current.TradeCount >= (int)config.Parameter,
            CandleType.Range => _current.Range >= config.Parameter,
            CandleType.Renko => ShouldCompleteRenko(trade, config),
            CandleType.PointAndFigure => ShouldCompletePnF(trade, config),
            CandleType.HeikinAshi => ShouldCompleteTime(trade, config),
            _ => false
        };
    }

    private bool ShouldCompleteTime(Trade trade, CandleBuildConfig config)
    {
        if (_current == null) return false;

        var intervalSeconds = (long)config.Parameter;
        var currentBucket = _current.OpenTime.ToUnixTimeSeconds() / intervalSeconds;
        var tradeBucket = trade.Timestamp.ToUnixTimeSeconds() / intervalSeconds;

        return tradeBucket > currentBucket;
    }

    private bool ShouldCompleteRenko(Trade trade, CandleBuildConfig config)
    {
        if (_current == null) return false;

        var brickSize = config.Parameter;
        var priceMove = Math.Abs(trade.Price - _current.Open);

        return priceMove >= brickSize;
    }

    private bool ShouldCompletePnF(Trade trade, CandleBuildConfig config)
    {
        if (_current == null) return false;

        var boxSize = config.Parameter;
        var reversalAmount = config.ReversalAmount;

        // Simplified P&F logic - complete when price moves enough boxes
        var boxes = Math.Abs(trade.Price - _current.Close) / boxSize;
        return boxes >= reversalAmount;
    }

    public Candle? FlushCurrentCandle()
    {
        if (_current == null || _current.TradeCount == 0) return null;

        var candle = _current.Complete();
        _current = null;
        return candle;
    }

    public Candle? GetCurrentCandle()
    {
        return _current?.ToCandle();
    }
}

/// <summary>
/// Candle being built (mutable state).
/// </summary>
internal sealed class CandleInProgress
{
    public string Symbol { get; }
    public DateTimeOffset OpenTime { get; }
    public CandleType Type { get; }

    public decimal Open { get; private set; }
    public decimal High { get; private set; }
    public decimal Low { get; private set; }
    public decimal Close { get; private set; }
    public long Volume { get; private set; }
    public int TradeCount { get; private set; }
    public long BuyVolume { get; private set; }
    public long SellVolume { get; private set; }
    public decimal VwapNumerator { get; private set; }
    public DateTimeOffset LastTradeTime { get; private set; }

    public decimal Range => High - Low;

    public CandleInProgress(string symbol, DateTimeOffset openTime, CandleType type)
    {
        Symbol = symbol;
        OpenTime = openTime;
        Type = type;
        High = decimal.MinValue;
        Low = decimal.MaxValue;
    }

    public void AddTrade(Trade trade)
    {
        if (TradeCount == 0)
        {
            Open = trade.Price;
            High = trade.Price;
            Low = trade.Price;
        }
        else
        {
            High = Math.Max(High, trade.Price);
            Low = Math.Min(Low, trade.Price);
        }

        Close = trade.Price;
        Volume += trade.Size;
        TradeCount++;
        LastTradeTime = trade.Timestamp;

        // VWAP calculation
        VwapNumerator += trade.Price * trade.Size;

        // Track buy/sell volume
        if (trade.Aggressor == AggressorSide.Buy)
            BuyVolume += trade.Size;
        else if (trade.Aggressor == AggressorSide.Sell)
            SellVolume += trade.Size;
    }

    public Candle ToCandle()
    {
        return new Candle
        {
            Symbol = Symbol,
            OpenTime = OpenTime,
            CloseTime = LastTradeTime,
            Open = Open,
            High = High == decimal.MinValue ? Open : High,
            Low = Low == decimal.MaxValue ? Open : Low,
            Close = Close,
            Volume = Volume,
            TradeCount = TradeCount,
            Vwap = Volume > 0 ? VwapNumerator / Volume : null,
            BuyVolume = BuyVolume,
            SellVolume = SellVolume,
            Type = Type,
            State = CandleState.Active
        };
    }

    public Candle Complete()
    {
        var candle = ToCandle();
        return candle with { State = CandleState.Finished };
    }

    /// <summary>
    /// Convert to Heikin-Ashi candle using previous candle data.
    /// </summary>
    public Candle ToHeikinAshi(Candle? previous)
    {
        var regularCandle = ToCandle();

        // Heikin-Ashi formulas:
        // HA Close = (Open + High + Low + Close) / 4
        // HA Open = (Previous HA Open + Previous HA Close) / 2
        // HA High = Max(High, HA Open, HA Close)
        // HA Low = Min(Low, HA Open, HA Close)

        var haClose = (Open + High + Low + Close) / 4;
        var haOpen = previous != null
            ? (previous.Open + previous.Close) / 2
            : (Open + Close) / 2;
        var haHigh = Math.Max(Math.Max(High, haOpen), haClose);
        var haLow = Math.Min(Math.Min(Low, haOpen), haClose);

        return regularCandle with
        {
            Open = haOpen,
            High = haHigh,
            Low = haLow,
            Close = haClose
        };
    }
}
