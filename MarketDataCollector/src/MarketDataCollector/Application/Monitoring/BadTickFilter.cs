using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading;
using MarketDataCollector.Application.Logging;
using Serilog;

namespace MarketDataCollector.Application.Monitoring;

/// <summary>
/// Filters out bad ticks - data points that are clearly erroneous such as
/// negative prices, zero prices, extreme outliers, or impossible values.
/// Helps maintain data quality by flagging or rejecting invalid market data.
/// </summary>
public sealed class BadTickFilter : IDisposable
{
    private readonly ILogger _log = LoggingSetup.ForContext<BadTickFilter>();
    private readonly ConcurrentDictionary<string, BadTickState> _symbolStates = new();
    private readonly BadTickFilterConfig _config;
    private readonly Timer _cleanupTimer;
    private volatile bool _isDisposed;

    // Counters
    private long _totalTicksProcessed;
    private long _totalBadTicksFiltered;
    private long _negativepriceFiltered;
    private long _zeroPriceFiltered;
    private long _extremeOutlierFiltered;
    private long _impossibleSizeFiltered;

    /// <summary>
    /// Event raised when a bad tick is detected.
    /// </summary>
    public event Action<BadTickAlert>? OnBadTick;

    public BadTickFilter(BadTickFilterConfig? config = null)
    {
        _config = config ?? BadTickFilterConfig.Default;
        _cleanupTimer = new Timer(CleanupOldStates, null,
            TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));

        _log.Information("BadTickFilter initialized with outlier threshold {OutlierMult}x, min price window {MinPrices}",
            _config.OutlierMultiplier, _config.MinPricesForOutlierDetection);
    }

    /// <summary>
    /// Validates a trade tick and returns whether it should be accepted or filtered.
    /// </summary>
    /// <param name="symbol">The symbol ticker.</param>
    /// <param name="price">The trade price.</param>
    /// <param name="size">The trade size.</param>
    /// <param name="timestamp">The timestamp of the trade.</param>
    /// <param name="reason">Output: If filtered, the reason for filtering.</param>
    /// <returns>True if the tick is valid and should be accepted; false if it should be filtered.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ValidateTick(string symbol, decimal price, decimal size, DateTimeOffset timestamp, out BadTickReason reason)
    {
        if (_isDisposed)
        {
            reason = BadTickReason.None;
            return true;
        }

        Interlocked.Increment(ref _totalTicksProcessed);

        var state = _symbolStates.GetOrAdd(symbol, _ => new BadTickState());
        reason = BadTickReason.None;

        // Check 1: Negative price
        if (price < 0)
        {
            Interlocked.Increment(ref _totalBadTicksFiltered);
            Interlocked.Increment(ref _negativepriceFiltered);
            state.IncrementBadTickCount();
            reason = BadTickReason.NegativePrice;
            RaiseAlert(symbol, price, size, timestamp, reason, "Negative price is impossible");
            return false;
        }

        // Check 2: Zero price (configurable)
        if (_config.FilterZeroPrices && price == 0)
        {
            Interlocked.Increment(ref _totalBadTicksFiltered);
            Interlocked.Increment(ref _zeroPriceFiltered);
            state.IncrementBadTickCount();
            reason = BadTickReason.ZeroPrice;
            RaiseAlert(symbol, price, size, timestamp, reason, "Zero price is likely invalid");
            return false;
        }

        // Check 3: Impossible size (negative or excessively large)
        if (size < 0 || (_config.MaxReasonableSize > 0 && size > _config.MaxReasonableSize))
        {
            Interlocked.Increment(ref _totalBadTicksFiltered);
            Interlocked.Increment(ref _impossibleSizeFiltered);
            state.IncrementBadTickCount();
            reason = BadTickReason.ImpossibleSize;
            RaiseAlert(symbol, price, size, timestamp, reason, $"Size {size} is outside valid range");
            return false;
        }

        // Check 4: Extreme outlier detection using rolling statistics
        if (_config.EnableOutlierDetection)
        {
            var stats = state.GetPriceStats();
            if (stats.HasValue && stats.Value.Count >= _config.MinPricesForOutlierDetection)
            {
                var (mean, stdDev, _) = stats.Value;

                if (stdDev > 0)
                {
                    var deviationsFromMean = Math.Abs(price - mean) / stdDev;

                    if (deviationsFromMean > _config.OutlierMultiplier)
                    {
                        Interlocked.Increment(ref _totalBadTicksFiltered);
                        Interlocked.Increment(ref _extremeOutlierFiltered);
                        state.IncrementBadTickCount();
                        state.IncrementOutlierCount();
                        reason = BadTickReason.ExtremeOutlier;

                        var percentFromMean = ((price - mean) / mean) * 100;
                        RaiseAlert(symbol, price, size, timestamp, reason,
                            $"Price {deviationsFromMean:F1} std devs from mean ({percentFromMean:F1}% deviation)");

                        // Don't add this price to the rolling statistics
                        state.UpdateLastActivity();
                        return false;
                    }
                }
            }
        }

        // Valid tick - update rolling statistics
        state.RecordPrice(price);
        return true;
    }

    /// <summary>
    /// Validates a quote and returns whether it should be accepted or filtered.
    /// </summary>
    public bool ValidateQuote(string symbol, decimal bidPrice, decimal askPrice, decimal bidSize, decimal askSize,
        DateTimeOffset timestamp, out BadTickReason reason)
    {
        if (_isDisposed)
        {
            reason = BadTickReason.None;
            return true;
        }

        Interlocked.Increment(ref _totalTicksProcessed);

        var state = _symbolStates.GetOrAdd(symbol, _ => new BadTickState());
        reason = BadTickReason.None;

        // Check negative prices
        if (bidPrice < 0 || askPrice < 0)
        {
            Interlocked.Increment(ref _totalBadTicksFiltered);
            Interlocked.Increment(ref _negativepriceFiltered);
            state.IncrementBadTickCount();
            reason = BadTickReason.NegativePrice;
            RaiseAlert(symbol, bidPrice < 0 ? bidPrice : askPrice, 0, timestamp, reason, "Negative quote price");
            return false;
        }

        // Check for impossible size
        if (bidSize < 0 || askSize < 0)
        {
            Interlocked.Increment(ref _totalBadTicksFiltered);
            Interlocked.Increment(ref _impossibleSizeFiltered);
            state.IncrementBadTickCount();
            reason = BadTickReason.ImpossibleSize;
            RaiseAlert(symbol, bidPrice, bidSize < 0 ? bidSize : askSize, timestamp, reason, "Negative quote size");
            return false;
        }

        // Valid quote - update with mid price for outlier detection
        if (bidPrice > 0 && askPrice > 0)
        {
            var midPrice = (bidPrice + askPrice) / 2;
            state.RecordPrice(midPrice);
        }

        return true;
    }

    private void RaiseAlert(string symbol, decimal price, decimal size, DateTimeOffset timestamp,
        BadTickReason reason, string message)
    {
        var state = _symbolStates.GetOrAdd(symbol, _ => new BadTickState());
        var now = DateTimeOffset.UtcNow;

        // Only raise alert if cooldown has passed
        if (!state.CanAlert(now, _config.AlertCooldownMs)) return;

        _log.Warning("BAD TICK: {Symbol} {Reason} - Price={Price:F4} Size={Size} - {Message}",
            symbol, reason, price, size, message);

        state.RecordAlert(now);

        var alert = new BadTickAlert(
            Symbol: symbol,
            Price: price,
            Size: size,
            Timestamp: timestamp,
            Reason: reason,
            Message: message,
            TotalBadTicksForSymbol: state.TotalBadTickCount
        );

        try
        {
            OnBadTick?.Invoke(alert);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Error in bad tick event handler for {Symbol}", symbol);
        }
    }

    /// <summary>
    /// Gets statistics about bad tick filtering.
    /// </summary>
    public BadTickStats GetStats()
    {
        var symbolStats = new List<SymbolBadTickStats>();

        foreach (var kvp in _symbolStates)
        {
            if (kvp.Value.TotalBadTickCount > 0)
            {
                symbolStats.Add(new SymbolBadTickStats(
                    Symbol: kvp.Key,
                    TotalBadTicks: kvp.Value.TotalBadTickCount,
                    OutlierCount: kvp.Value.OutlierCount,
                    LastBadTickTime: kvp.Value.LastBadTickTime
                ));
            }
        }

        return new BadTickStats(
            TotalTicksProcessed: Interlocked.Read(ref _totalTicksProcessed),
            TotalBadTicksFiltered: Interlocked.Read(ref _totalBadTicksFiltered),
            NegativePriceFiltered: Interlocked.Read(ref _negativepriceFiltered),
            ZeroPriceFiltered: Interlocked.Read(ref _zeroPriceFiltered),
            ExtremeOutlierFiltered: Interlocked.Read(ref _extremeOutlierFiltered),
            ImpossibleSizeFiltered: Interlocked.Read(ref _impossibleSizeFiltered),
            SymbolStats: symbolStats.OrderByDescending(s => s.TotalBadTicks).ToList()
        );
    }

    /// <summary>
    /// Gets the total count of bad ticks filtered.
    /// </summary>
    public long TotalBadTicksFiltered => Interlocked.Read(ref _totalBadTicksFiltered);

    /// <summary>
    /// Gets the filter rate as a percentage.
    /// </summary>
    public double FilterRatePercent
    {
        get
        {
            var total = Interlocked.Read(ref _totalTicksProcessed);
            var filtered = Interlocked.Read(ref _totalBadTicksFiltered);
            return total > 0 ? (filtered / (double)total) * 100 : 0;
        }
    }

    private void CleanupOldStates(object? state)
    {
        if (_isDisposed) return;

        try
        {
            var cutoff = DateTimeOffset.UtcNow.AddHours(-24);
            var toRemove = new List<string>();

            foreach (var kvp in _symbolStates)
            {
                if (kvp.Value.LastActivityTime < cutoff)
                {
                    toRemove.Add(kvp.Key);
                }
            }

            foreach (var symbol in toRemove)
            {
                _symbolStates.TryRemove(symbol, out _);
            }

            if (toRemove.Count > 0)
            {
                _log.Debug("Cleaned up {Count} inactive symbol states from bad tick filter", toRemove.Count);
            }
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Error during bad tick filter state cleanup");
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        _cleanupTimer.Dispose();
        _symbolStates.Clear();
    }

    /// <summary>
    /// Per-symbol state for bad tick tracking with rolling statistics.
    /// </summary>
    private sealed class BadTickState
    {
        private readonly object _lock = new();
        private readonly Queue<decimal> _recentPrices = new();
        private const int MaxRecentPrices = 100;

        private long _totalBadTickCount;
        private long _outlierCount;
        private decimal _priceSum;
        private decimal _priceSumSquares;
        private int _priceCount;
        private DateTimeOffset _lastAlertTime = DateTimeOffset.MinValue;
        private DateTimeOffset _lastBadTickTime;
        private DateTimeOffset _lastActivityTime;

        public long TotalBadTickCount => Interlocked.Read(ref _totalBadTickCount);
        public long OutlierCount => Interlocked.Read(ref _outlierCount);
        public DateTimeOffset LastBadTickTime => _lastBadTickTime;
        public DateTimeOffset LastActivityTime => _lastActivityTime;

        public void RecordPrice(decimal price)
        {
            lock (_lock)
            {
                _recentPrices.Enqueue(price);

                if (_recentPrices.Count > MaxRecentPrices)
                {
                    var removed = _recentPrices.Dequeue();
                    _priceSum -= removed;
                    _priceSumSquares -= removed * removed;
                    _priceCount--;
                }

                _priceSum += price;
                _priceSumSquares += price * price;
                _priceCount++;
                _lastActivityTime = DateTimeOffset.UtcNow;
            }
        }

        public (decimal Mean, decimal StdDev, int Count)? GetPriceStats()
        {
            lock (_lock)
            {
                if (_priceCount < 2) return null;

                var mean = _priceSum / _priceCount;
                var variance = (_priceSumSquares / _priceCount) - (mean * mean);
                var stdDev = variance > 0 ? (decimal)Math.Sqrt((double)variance) : 0;

                return (mean, stdDev, _priceCount);
            }
        }

        public void IncrementBadTickCount()
        {
            Interlocked.Increment(ref _totalBadTickCount);
            _lastBadTickTime = DateTimeOffset.UtcNow;
            _lastActivityTime = _lastBadTickTime;
        }

        public void IncrementOutlierCount()
        {
            Interlocked.Increment(ref _outlierCount);
        }

        public void UpdateLastActivity()
        {
            _lastActivityTime = DateTimeOffset.UtcNow;
        }

        public bool CanAlert(DateTimeOffset now, int cooldownMs)
        {
            return (now - _lastAlertTime).TotalMilliseconds >= cooldownMs;
        }

        public void RecordAlert(DateTimeOffset time)
        {
            _lastAlertTime = time;
        }
    }
}

/// <summary>
/// Reasons why a tick was filtered as bad.
/// </summary>
public enum BadTickReason
{
    None,
    NegativePrice,
    ZeroPrice,
    ExtremeOutlier,
    ImpossibleSize
}

/// <summary>
/// Configuration for bad tick filtering.
/// </summary>
public sealed record BadTickFilterConfig
{
    /// <summary>
    /// Whether to filter zero prices. Default is true.
    /// </summary>
    public bool FilterZeroPrices { get; init; } = true;

    /// <summary>
    /// Whether to enable outlier detection using rolling statistics. Default is true.
    /// </summary>
    public bool EnableOutlierDetection { get; init; } = true;

    /// <summary>
    /// Number of standard deviations from mean to consider an outlier.
    /// Default is 10 (very conservative - only filter extreme outliers).
    /// </summary>
    public decimal OutlierMultiplier { get; init; } = 10.0m;

    /// <summary>
    /// Minimum number of prices needed before outlier detection kicks in.
    /// Default is 20.
    /// </summary>
    public int MinPricesForOutlierDetection { get; init; } = 20;

    /// <summary>
    /// Maximum reasonable trade size. 0 means no limit. Default is 0.
    /// </summary>
    public decimal MaxReasonableSize { get; init; } = 0;

    /// <summary>
    /// Minimum time between alerts for the same symbol in milliseconds.
    /// </summary>
    public int AlertCooldownMs { get; init; } = 5000;

    public static BadTickFilterConfig Default => new();

    /// <summary>
    /// Configuration preset for aggressive filtering (5 std devs).
    /// </summary>
    public static BadTickFilterConfig Aggressive => new()
    {
        OutlierMultiplier = 5.0m,
        MinPricesForOutlierDetection = 10,
        AlertCooldownMs = 2000
    };

    /// <summary>
    /// Configuration preset for lenient filtering (15 std devs).
    /// </summary>
    public static BadTickFilterConfig Lenient => new()
    {
        OutlierMultiplier = 15.0m,
        MinPricesForOutlierDetection = 50,
        AlertCooldownMs = 10000
    };
}

/// <summary>
/// Alert for a bad tick event.
/// </summary>
public readonly record struct BadTickAlert(
    string Symbol,
    decimal Price,
    decimal Size,
    DateTimeOffset Timestamp,
    BadTickReason Reason,
    string Message,
    long TotalBadTicksForSymbol
);

/// <summary>
/// Statistics for bad tick filtering.
/// </summary>
public readonly record struct BadTickStats(
    long TotalTicksProcessed,
    long TotalBadTicksFiltered,
    long NegativePriceFiltered,
    long ZeroPriceFiltered,
    long ExtremeOutlierFiltered,
    long ImpossibleSizeFiltered,
    IReadOnlyList<SymbolBadTickStats> SymbolStats
);

/// <summary>
/// Per-symbol bad tick statistics.
/// </summary>
public readonly record struct SymbolBadTickStats(
    string Symbol,
    long TotalBadTicks,
    long OutlierCount,
    DateTimeOffset LastBadTickTime
);
