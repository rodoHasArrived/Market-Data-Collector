using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using MarketDataCollector.Application.Logging;
using Serilog;

namespace MarketDataCollector.Application.Monitoring;

/// <summary>
/// Detects sudden price spikes that exceed configurable thresholds.
/// Part of the data quality framework (QW-6) to alert on unusual
/// price movements that may indicate data errors or extreme market events.
/// </summary>
/// <remarks>
/// Tracks a rolling history of prices per symbol and calculates
/// the percentage change from recent prices. Fires alerts when
/// the change exceeds the configured threshold.
/// </remarks>
public sealed class PriceSpikeDetector : IDisposable
{
    private readonly ILogger _log = LoggingSetup.ForContext<PriceSpikeDetector>();
    private readonly ConcurrentDictionary<string, PriceSpikeState> _symbolStates = new();
    private readonly PriceSpikeConfig _config;
    private readonly Timer _cleanupTimer;
    private volatile bool _isDisposed;

    // Counters
    private long _totalSpikesDetected;
    private long _totalEventsProcessed;

    /// <summary>
    /// Event raised when a price spike is detected.
    /// </summary>
    public event Action<PriceSpikeAlert>? OnPriceSpike;

    public PriceSpikeDetector(PriceSpikeConfig? config = null)
    {
        _config = config ?? PriceSpikeConfig.Default;
        _cleanupTimer = new Timer(CleanupOldStates, null,
            TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(10));

        _log.Information(
            "PriceSpikeDetector initialized with threshold {Threshold}%, window {WindowMs}ms",
            _config.SpikeThresholdPercent, _config.WindowMs);
    }

    /// <summary>
    /// Processes a trade price and checks for price spikes.
    /// </summary>
    /// <param name="symbol">The symbol ticker.</param>
    /// <param name="price">The trade price.</param>
    /// <param name="provider">The data provider (optional).</param>
    /// <returns>True if a price spike was detected.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ProcessTrade(string symbol, decimal price, string? provider = null)
    {
        return ProcessPrice(symbol, price, PriceSpikeType.Trade, provider);
    }

    /// <summary>
    /// Processes a quote mid-price and checks for price spikes.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ProcessQuote(string symbol, decimal bidPrice, decimal askPrice, string? provider = null)
    {
        if (bidPrice <= 0 || askPrice <= 0) return false;
        var midPrice = (bidPrice + askPrice) / 2;
        return ProcessPrice(symbol, midPrice, PriceSpikeType.Quote, provider);
    }

    /// <summary>
    /// Processes a bar close price and checks for price spikes.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ProcessBar(string symbol, decimal closePrice, string? provider = null)
    {
        return ProcessPrice(symbol, closePrice, PriceSpikeType.Bar, provider);
    }

    /// <summary>
    /// Core price spike detection logic.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool ProcessPrice(string symbol, decimal price, PriceSpikeType priceType, string? provider)
    {
        if (_isDisposed || price <= 0) return false;

        Interlocked.Increment(ref _totalEventsProcessed);

        var state = _symbolStates.GetOrAdd(symbol, _ => new PriceSpikeState(_config.WindowMs));
        var now = DateTimeOffset.UtcNow;

        // Calculate change from reference price
        var referencePrice = state.GetReferencePrice();
        if (referencePrice <= 0)
        {
            state.AddPrice(price);
            return false;
        }

        var changePercent = Math.Abs((double)(price - referencePrice) / (double)referencePrice * 100);

        // Add the new price to the window
        state.AddPrice(price);

        // Check if change exceeds threshold
        if (changePercent >= _config.SpikeThresholdPercent)
        {
            Interlocked.Increment(ref _totalSpikesDetected);
            state.IncrementSpikeCount();

            // Only alert if cooldown has passed
            if (state.CanAlert(now, _config.AlertCooldownMs))
            {
                var direction = price > referencePrice ? SpikeDirection.Up : SpikeDirection.Down;

                var alert = new PriceSpikeAlert(
                    Symbol: symbol,
                    CurrentPrice: price,
                    ReferencePrice: referencePrice,
                    ChangePercent: changePercent,
                    Direction: direction,
                    PriceType: priceType,
                    Provider: provider,
                    Timestamp: now,
                    ConsecutiveSpikes: state.ConsecutiveSpikeCount
                );

                _log.Warning(
                    "PRICE SPIKE: {Symbol} {Direction} {ChangePercent:F2}% from {ReferencePrice:F4} to {CurrentPrice:F4} ({PriceType}, {Provider})",
                    symbol, direction, changePercent, referencePrice, price, priceType, provider ?? "unknown");

                state.RecordAlert(now);

                try
                {
                    OnPriceSpike?.Invoke(alert);
                }
                catch (Exception ex)
                {
                    _log.Error(ex, "Error in price spike event handler for {Symbol}", symbol);
                }

                return true;
            }
        }
        else
        {
            state.ResetConsecutiveSpikeCount();
        }

        return false;
    }

    /// <summary>
    /// Gets statistics about price spike detection.
    /// </summary>
    public PriceSpikeStats GetStats()
    {
        var symbolStats = new List<SymbolPriceSpikeStats>();

        foreach (var kvp in _symbolStates)
        {
            if (kvp.Value.TotalSpikeCount > 0)
            {
                symbolStats.Add(new SymbolPriceSpikeStats(
                    Symbol: kvp.Key,
                    TotalSpikes: kvp.Value.TotalSpikeCount,
                    LastSpikeTime: kvp.Value.LastSpikeTime,
                    MaxChangePercent: kvp.Value.MaxChangePercent
                ));
            }
        }

        return new PriceSpikeStats(
            TotalEventsProcessed: Interlocked.Read(ref _totalEventsProcessed),
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
        return _symbolStates
            .Where(kvp => kvp.Value.LastSpikeTime > cutoff)
            .Select(kvp => kvp.Key)
            .ToList();
    }

    private void CleanupOldStates(object? state)
    {
        if (_isDisposed) return;

        try
        {
            var cutoff = DateTimeOffset.UtcNow.AddHours(-24);
            var toRemove = _symbolStates
                .Where(kvp => kvp.Value.LastActivityTime < cutoff)
                .Select(kvp => kvp.Key)
                .ToList();

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
    /// Per-symbol state for price spike tracking.
    /// </summary>
    private sealed class PriceSpikeState
    {
        private readonly Queue<(DateTimeOffset Time, decimal Price)> _priceWindow = new();
        private readonly object _lock = new();
        private readonly int _windowMs;

        private long _totalSpikeCount;
        private int _consecutiveSpikeCount;
        private double _maxChangePercent;
        private DateTimeOffset _lastAlertTime = DateTimeOffset.MinValue;
        private DateTimeOffset _lastSpikeTime;
        private DateTimeOffset _lastActivityTime;

        public long TotalSpikeCount => Interlocked.Read(ref _totalSpikeCount);
        public int ConsecutiveSpikeCount => _consecutiveSpikeCount;
        public double MaxChangePercent => _maxChangePercent;
        public DateTimeOffset LastSpikeTime => _lastSpikeTime;
        public DateTimeOffset LastActivityTime => _lastActivityTime;

        public PriceSpikeState(int windowMs)
        {
            _windowMs = windowMs;
        }

        public void AddPrice(decimal price)
        {
            var now = DateTimeOffset.UtcNow;
            _lastActivityTime = now;

            lock (_lock)
            {
                // Remove old prices outside the window
                var cutoff = now.AddMilliseconds(-_windowMs);
                while (_priceWindow.Count > 0 && _priceWindow.Peek().Time < cutoff)
                {
                    _priceWindow.Dequeue();
                }

                _priceWindow.Enqueue((now, price));
            }
        }

        public decimal GetReferencePrice()
        {
            lock (_lock)
            {
                if (_priceWindow.Count == 0) return 0;

                // Use the oldest price in the window as reference
                return _priceWindow.Peek().Price;
            }
        }

        public void IncrementSpikeCount()
        {
            Interlocked.Increment(ref _totalSpikeCount);
            Interlocked.Increment(ref _consecutiveSpikeCount);
            _lastSpikeTime = DateTimeOffset.UtcNow;
        }

        public void ResetConsecutiveSpikeCount()
        {
            _consecutiveSpikeCount = 0;
        }

        public void UpdateMaxChange(double changePercent)
        {
            if (changePercent > _maxChangePercent)
            {
                _maxChangePercent = changePercent;
            }
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
/// Configuration for price spike detection.
/// </summary>
public sealed record PriceSpikeConfig
{
    /// <summary>
    /// Minimum percentage change to trigger a spike alert.
    /// Default is 5% (significant move in most liquid markets).
    /// </summary>
    public double SpikeThresholdPercent { get; init; } = 5.0;

    /// <summary>
    /// Time window in milliseconds for calculating price changes.
    /// Default is 60000ms (1 minute).
    /// </summary>
    public int WindowMs { get; init; } = 60000;

    /// <summary>
    /// Minimum time between alerts for the same symbol in milliseconds.
    /// </summary>
    public int AlertCooldownMs { get; init; } = 10000;

    public static PriceSpikeConfig Default => new();

    /// <summary>
    /// Sensitive configuration for detecting smaller moves.
    /// </summary>
    public static PriceSpikeConfig Sensitive => new()
    {
        SpikeThresholdPercent = 2.0,
        WindowMs = 30000,
        AlertCooldownMs = 5000
    };

    /// <summary>
    /// Configuration for volatile markets.
    /// </summary>
    public static PriceSpikeConfig Volatile => new()
    {
        SpikeThresholdPercent = 10.0,
        WindowMs = 120000,
        AlertCooldownMs = 30000
    };
}

/// <summary>
/// Type of price being validated.
/// </summary>
public enum PriceSpikeType
{
    Trade,
    Quote,
    Bar
}

/// <summary>
/// Direction of the price spike.
/// </summary>
public enum SpikeDirection
{
    Up,
    Down
}

/// <summary>
/// Alert for a price spike condition.
/// </summary>
public readonly record struct PriceSpikeAlert(
    string Symbol,
    decimal CurrentPrice,
    decimal ReferencePrice,
    double ChangePercent,
    SpikeDirection Direction,
    PriceSpikeType PriceType,
    string? Provider,
    DateTimeOffset Timestamp,
    int ConsecutiveSpikes
);

/// <summary>
/// Statistics for price spike detection.
/// </summary>
public readonly record struct PriceSpikeStats(
    long TotalEventsProcessed,
    long TotalSpikesDetected,
    IReadOnlyList<SymbolPriceSpikeStats> SymbolStats
);

/// <summary>
/// Per-symbol price spike statistics.
/// </summary>
public readonly record struct SymbolPriceSpikeStats(
    string Symbol,
    long TotalSpikes,
    DateTimeOffset LastSpikeTime,
    double MaxChangePercent
);
