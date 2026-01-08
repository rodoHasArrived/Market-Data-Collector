using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading;
using MarketDataCollector.Application.Logging;
using Serilog;

namespace MarketDataCollector.Application.Monitoring;

/// <summary>
/// Detects price spikes - rapid price changes exceeding a configurable threshold
/// within a specified time window. This can indicate data quality issues or
/// significant market events.
/// </summary>
public sealed class PriceSpikeDetector : IDisposable
{
    private readonly ILogger _log = LoggingSetup.ForContext<PriceSpikeDetector>();
    private readonly ConcurrentDictionary<string, PriceSpikeState> _symbolStates = new();
    private readonly PriceSpikeConfig _config;
    private readonly Timer _cleanupTimer;
    private volatile bool _isDisposed;

    // Counters
    private long _totalSpikesDetected;
    private long _totalPricesProcessed;

    /// <summary>
    /// Event raised when a price spike is detected.
    /// </summary>
    public event Action<PriceSpikeAlert>? OnPriceSpike;

    public PriceSpikeDetector(PriceSpikeConfig? config = null)
    {
        _config = config ?? PriceSpikeConfig.Default;
        _cleanupTimer = new Timer(CleanupOldStates, null,
            TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));

        _log.Information("PriceSpikeDetector initialized with threshold {Threshold}% over {WindowSeconds}s window",
            _config.SpikeThresholdPercent, _config.TimeWindowSeconds);
    }

    /// <summary>
    /// Processes a trade/price update and checks for price spikes.
    /// </summary>
    /// <param name="symbol">The symbol ticker.</param>
    /// <param name="price">The current price.</param>
    /// <param name="timestamp">The timestamp of the price update.</param>
    /// <param name="volume">Optional trade volume.</param>
    /// <returns>True if a price spike was detected.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ProcessPrice(string symbol, decimal price, DateTimeOffset timestamp, decimal volume = 0)
    {
        if (_isDisposed) return false;

        Interlocked.Increment(ref _totalPricesProcessed);

        // Skip invalid prices
        if (price <= 0) return false;

        var state = _symbolStates.GetOrAdd(symbol, _ => new PriceSpikeState());
        var now = DateTimeOffset.UtcNow;

        // Get the reference price (price from the start of the time window)
        var referencePrice = state.GetReferencePrice(timestamp, _config.TimeWindowSeconds);

        if (referencePrice.HasValue && referencePrice.Value > 0)
        {
            var priceChange = Math.Abs(price - referencePrice.Value);
            var changePercent = (priceChange / referencePrice.Value) * 100;

            if (changePercent >= _config.SpikeThresholdPercent)
            {
                Interlocked.Increment(ref _totalSpikesDetected);
                state.IncrementSpikeCount();

                // Only alert if cooldown has passed
                if (state.CanAlert(now, _config.AlertCooldownMs))
                {
                    var direction = price > referencePrice.Value ? "UP" : "DOWN";
                    var alert = new PriceSpikeAlert(
                        Symbol: symbol,
                        CurrentPrice: price,
                        ReferencePrice: referencePrice.Value,
                        PriceChange: priceChange,
                        ChangePercent: changePercent,
                        Direction: direction,
                        Volume: volume,
                        Timestamp: timestamp,
                        WindowSeconds: _config.TimeWindowSeconds,
                        ConsecutiveCount: state.ConsecutiveSpikeCount
                    );

                    _log.Warning("PRICE SPIKE: {Symbol} {Direction} {Percent:F2}% in {Window}s ({RefPrice:F4} -> {Price:F4})",
                        symbol, direction, changePercent, _config.TimeWindowSeconds, referencePrice.Value, price);

                    state.RecordAlert(now);

                    try
                    {
                        OnPriceSpike?.Invoke(alert);
                    }
                    catch (Exception ex)
                    {
                        _log.Error(ex, "Error in price spike event handler for {Symbol}", symbol);
                    }

                    // Record the new price for next comparison
                    state.RecordPrice(timestamp, price);
                    return true;
                }
            }
            else
            {
                // Normal price movement - reset consecutive count
                state.ResetConsecutiveCount();
            }
        }

        // Record the price
        state.RecordPrice(timestamp, price);
        return false;
    }

    /// <summary>
    /// Gets statistics about price spike detection.
    /// </summary>
    public PriceSpikeStats GetStats()
    {
        var symbolStats = new List<SymbolSpikeStats>();

        foreach (var kvp in _symbolStates)
        {
            if (kvp.Value.TotalSpikeCount > 0)
            {
                symbolStats.Add(new SymbolSpikeStats(
                    Symbol: kvp.Key,
                    TotalSpikes: kvp.Value.TotalSpikeCount,
                    LastSpikeTime: kvp.Value.LastSpikeTime,
                    MaxSpikePercent: kvp.Value.MaxSpikePercent
                ));
            }
        }

        return new PriceSpikeStats(
            TotalPricesProcessed: Interlocked.Read(ref _totalPricesProcessed),
            TotalSpikesDetected: Interlocked.Read(ref _totalSpikesDetected),
            SymbolStats: symbolStats.OrderByDescending(s => s.TotalSpikes).ToList()
        );
    }

    /// <summary>
    /// Gets the total count of price spikes detected.
    /// </summary>
    public long TotalSpikesDetected => Interlocked.Read(ref _totalSpikesDetected);

    /// <summary>
    /// Gets symbols that have had price spikes in the last N minutes.
    /// </summary>
    public IReadOnlyList<string> GetRecentSpikeSymbols(int minutesBack = 60)
    {
        var cutoff = DateTimeOffset.UtcNow.AddMinutes(-minutesBack);
        var symbols = new List<string>();

        foreach (var kvp in _symbolStates)
        {
            if (kvp.Value.LastSpikeTime > cutoff)
            {
                symbols.Add(kvp.Key);
            }
        }

        return symbols;
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
                _log.Debug("Cleaned up {Count} inactive symbol states from price spike detector", toRemove.Count);
            }
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Error during price spike state cleanup");
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
    /// Per-symbol state for price spike tracking with rolling window.
    /// </summary>
    private sealed class PriceSpikeState
    {
        private readonly object _lock = new();
        private readonly LinkedList<(DateTimeOffset Timestamp, decimal Price)> _priceHistory = new();
        private long _totalSpikeCount;
        private int _consecutiveSpikeCount;
        private decimal _maxSpikePercent;
        private DateTimeOffset _lastAlertTime = DateTimeOffset.MinValue;
        private DateTimeOffset _lastSpikeTime;
        private DateTimeOffset _lastActivityTime;

        public long TotalSpikeCount => Interlocked.Read(ref _totalSpikeCount);
        public int ConsecutiveSpikeCount => _consecutiveSpikeCount;
        public decimal MaxSpikePercent => _maxSpikePercent;
        public DateTimeOffset LastSpikeTime => _lastSpikeTime;
        public DateTimeOffset LastActivityTime => _lastActivityTime;

        public void RecordPrice(DateTimeOffset timestamp, decimal price)
        {
            lock (_lock)
            {
                _priceHistory.AddLast((timestamp, price));
                _lastActivityTime = DateTimeOffset.UtcNow;

                // Keep only last 1000 prices or 1 hour of data to prevent unbounded growth
                while (_priceHistory.Count > 1000 ||
                       (_priceHistory.First != null &&
                        (timestamp - _priceHistory.First.Value.Timestamp).TotalHours > 1))
                {
                    _priceHistory.RemoveFirst();
                }
            }
        }

        public decimal? GetReferencePrice(DateTimeOffset currentTimestamp, int windowSeconds)
        {
            lock (_lock)
            {
                var windowStart = currentTimestamp.AddSeconds(-windowSeconds);

                // Find the oldest price within the window
                foreach (var entry in _priceHistory)
                {
                    if (entry.Timestamp >= windowStart && entry.Timestamp <= currentTimestamp)
                    {
                        return entry.Price;
                    }
                }

                // If no price in window, use the most recent price before the window
                var lastBeforeWindow = _priceHistory
                    .Where(p => p.Timestamp < windowStart)
                    .LastOrDefault();

                return lastBeforeWindow.Price > 0 ? lastBeforeWindow.Price : null;
            }
        }

        public void IncrementSpikeCount()
        {
            Interlocked.Increment(ref _totalSpikeCount);
            Interlocked.Increment(ref _consecutiveSpikeCount);
            _lastSpikeTime = DateTimeOffset.UtcNow;
            _lastActivityTime = _lastSpikeTime;
        }

        public void ResetConsecutiveCount()
        {
            _consecutiveSpikeCount = 0;
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

        public void UpdateMaxSpike(decimal spikePercent)
        {
            if (spikePercent > _maxSpikePercent)
            {
                _maxSpikePercent = spikePercent;
            }
        }
    }
}

/// <summary>
/// Configuration for price spike detection.
/// </summary>
public sealed record PriceSpikeConfig
{
    /// <summary>
    /// Threshold percentage for what constitutes a price spike.
    /// Default is 5% (a 5% move in the time window triggers an alert).
    /// </summary>
    public decimal SpikeThresholdPercent { get; init; } = 5.0m;

    /// <summary>
    /// Time window in seconds to measure the price change over.
    /// Default is 60 seconds (1 minute).
    /// </summary>
    public int TimeWindowSeconds { get; init; } = 60;

    /// <summary>
    /// Minimum time between alerts for the same symbol in milliseconds.
    /// </summary>
    public int AlertCooldownMs { get; init; } = 10000;

    public static PriceSpikeConfig Default => new();

    /// <summary>
    /// Configuration preset for aggressive spike detection (2% in 30 seconds).
    /// </summary>
    public static PriceSpikeConfig Aggressive => new()
    {
        SpikeThresholdPercent = 2.0m,
        TimeWindowSeconds = 30,
        AlertCooldownMs = 5000
    };

    /// <summary>
    /// Configuration preset for conservative spike detection (10% in 5 minutes).
    /// </summary>
    public static PriceSpikeConfig Conservative => new()
    {
        SpikeThresholdPercent = 10.0m,
        TimeWindowSeconds = 300,
        AlertCooldownMs = 30000
    };
}

/// <summary>
/// Alert for a price spike event.
/// </summary>
public readonly record struct PriceSpikeAlert(
    string Symbol,
    decimal CurrentPrice,
    decimal ReferencePrice,
    decimal PriceChange,
    decimal ChangePercent,
    string Direction,
    decimal Volume,
    DateTimeOffset Timestamp,
    int WindowSeconds,
    int ConsecutiveCount
);

/// <summary>
/// Statistics for price spike detection.
/// </summary>
public readonly record struct PriceSpikeStats(
    long TotalPricesProcessed,
    long TotalSpikesDetected,
    IReadOnlyList<SymbolSpikeStats> SymbolStats
);

/// <summary>
/// Per-symbol price spike statistics.
/// </summary>
public readonly record struct SymbolSpikeStats(
    string Symbol,
    long TotalSpikes,
    DateTimeOffset LastSpikeTime,
    decimal MaxSpikePercent
);
