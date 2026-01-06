using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using MarketDataCollector.Application.Logging;
using Serilog;

namespace MarketDataCollector.Application.Monitoring;

/// <summary>
/// Detects crossed markets where bid price exceeds ask price, which indicates
/// a data quality issue or market anomaly.
/// </summary>
public sealed class CrossedMarketDetector : IDisposable
{
    private readonly ILogger _log = LoggingSetup.ForContext<CrossedMarketDetector>();
    private readonly ConcurrentDictionary<string, CrossedMarketState> _symbolStates = new();
    private readonly CrossedMarketConfig _config;
    private readonly Timer _cleanupTimer;
    private volatile bool _isDisposed;

    // Counters
    private long _totalCrossedEvents;
    private long _totalLockedEvents;
    private long _totalQuotesProcessed;

    /// <summary>
    /// Event raised when a crossed market is detected.
    /// </summary>
    public event Action<CrossedMarketAlert>? OnCrossedMarket;

    /// <summary>
    /// Event raised when a locked market (bid = ask) is detected.
    /// </summary>
    public event Action<LockedMarketAlert>? OnLockedMarket;

    public CrossedMarketDetector(CrossedMarketConfig? config = null)
    {
        _config = config ?? CrossedMarketConfig.Default;
        _cleanupTimer = new Timer(CleanupOldAlerts, null,
            TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));

        _log.Information("CrossedMarketDetector initialized with alert cooldown {CooldownMs}ms",
            _config.AlertCooldownMs);
    }

    /// <summary>
    /// Processes a quote update and checks for crossed/locked market conditions.
    /// Call this for every quote update received.
    /// </summary>
    /// <param name="symbol">The symbol ticker.</param>
    /// <param name="bidPrice">The best bid price.</param>
    /// <param name="askPrice">The best ask price.</param>
    /// <param name="bidSize">The bid size (optional).</param>
    /// <param name="askSize">The ask size (optional).</param>
    /// <returns>True if a crossed or locked market was detected.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ProcessQuote(string symbol, decimal bidPrice, decimal askPrice, decimal bidSize = 0, decimal askSize = 0)
    {
        if (_isDisposed) return false;

        Interlocked.Increment(ref _totalQuotesProcessed);

        // Skip invalid prices
        if (bidPrice <= 0 || askPrice <= 0) return false;

        var state = _symbolStates.GetOrAdd(symbol, _ => new CrossedMarketState());
        var now = DateTimeOffset.UtcNow;

        // Check for crossed market (bid > ask)
        if (bidPrice > askPrice)
        {
            Interlocked.Increment(ref _totalCrossedEvents);
            state.IncrementCrossedCount();

            // Only alert if cooldown has passed
            if (state.CanAlert(now, _config.AlertCooldownMs))
            {
                var spread = bidPrice - askPrice;
                var spreadPercent = (spread / askPrice) * 100;

                var alert = new CrossedMarketAlert(
                    Symbol: symbol,
                    BidPrice: bidPrice,
                    AskPrice: askPrice,
                    BidSize: bidSize,
                    AskSize: askSize,
                    SpreadAmount: spread,
                    SpreadPercent: spreadPercent,
                    Timestamp: now,
                    ConsecutiveCount: state.ConsecutiveCrossedCount
                );

                _log.Warning("CROSSED MARKET: {Symbol} Bid={Bid:F4} > Ask={Ask:F4} (spread: {Spread:F4}, {SpreadPct:F2}%)",
                    symbol, bidPrice, askPrice, spread, spreadPercent);

                state.RecordAlert(now);

                try
                {
                    OnCrossedMarket?.Invoke(alert);
                }
                catch (Exception ex)
                {
                    _log.Error(ex, "Error in crossed market event handler for {Symbol}", symbol);
                }

                return true;
            }
        }
        // Check for locked market (bid = ask) - this is unusual but not as severe
        else if (_config.DetectLockedMarkets && bidPrice == askPrice)
        {
            Interlocked.Increment(ref _totalLockedEvents);
            state.IncrementLockedCount();

            if (state.CanAlertLocked(now, _config.LockedMarketCooldownMs))
            {
                var alert = new LockedMarketAlert(
                    Symbol: symbol,
                    Price: bidPrice,
                    BidSize: bidSize,
                    AskSize: askSize,
                    Timestamp: now,
                    ConsecutiveCount: state.ConsecutiveLockedCount
                );

                _log.Information("LOCKED MARKET: {Symbol} Bid=Ask={Price:F4}", symbol, bidPrice);

                state.RecordLockedAlert(now);

                try
                {
                    OnLockedMarket?.Invoke(alert);
                }
                catch (Exception ex)
                {
                    _log.Error(ex, "Error in locked market event handler for {Symbol}", symbol);
                }

                return true;
            }
        }
        else
        {
            // Normal market - reset consecutive counts
            state.ResetConsecutiveCounts();
        }

        return false;
    }

    /// <summary>
    /// Gets statistics about crossed market detection.
    /// </summary>
    public CrossedMarketStats GetStats()
    {
        var symbolStats = new List<SymbolCrossedStats>();

        foreach (var kvp in _symbolStates)
        {
            if (kvp.Value.TotalCrossedCount > 0 || kvp.Value.TotalLockedCount > 0)
            {
                symbolStats.Add(new SymbolCrossedStats(
                    Symbol: kvp.Key,
                    TotalCrossedEvents: kvp.Value.TotalCrossedCount,
                    TotalLockedEvents: kvp.Value.TotalLockedCount,
                    LastCrossedTime: kvp.Value.LastCrossedTime,
                    LastLockedTime: kvp.Value.LastLockedTime
                ));
            }
        }

        return new CrossedMarketStats(
            TotalQuotesProcessed: Interlocked.Read(ref _totalQuotesProcessed),
            TotalCrossedEvents: Interlocked.Read(ref _totalCrossedEvents),
            TotalLockedEvents: Interlocked.Read(ref _totalLockedEvents),
            SymbolStats: symbolStats.OrderByDescending(s => s.TotalCrossedEvents).ToList()
        );
    }

    /// <summary>
    /// Gets the count of crossed market events detected.
    /// </summary>
    public long TotalCrossedEvents => Interlocked.Read(ref _totalCrossedEvents);

    /// <summary>
    /// Gets the count of locked market events detected.
    /// </summary>
    public long TotalLockedEvents => Interlocked.Read(ref _totalLockedEvents);

    /// <summary>
    /// Gets symbols that have had crossed market events in the last N minutes.
    /// </summary>
    public IReadOnlyList<string> GetRecentCrossedSymbols(int minutesBack = 60)
    {
        var cutoff = DateTimeOffset.UtcNow.AddMinutes(-minutesBack);
        var symbols = new List<string>();

        foreach (var kvp in _symbolStates)
        {
            if (kvp.Value.LastCrossedTime > cutoff)
            {
                symbols.Add(kvp.Key);
            }
        }

        return symbols;
    }

    private void CleanupOldAlerts(object? state)
    {
        if (_isDisposed) return;

        try
        {
            var cutoff = DateTimeOffset.UtcNow.AddHours(-24);
            var toRemove = new List<string>();

            foreach (var kvp in _symbolStates)
            {
                // Remove symbols that haven't had any events in 24 hours
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
                _log.Debug("Cleaned up {Count} inactive symbol states from crossed market detector", toRemove.Count);
            }
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Error during crossed market state cleanup");
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
    /// Per-symbol state for crossed market tracking.
    /// </summary>
    private sealed class CrossedMarketState
    {
        private long _totalCrossedCount;
        private long _totalLockedCount;
        private int _consecutiveCrossedCount;
        private int _consecutiveLockedCount;
        private DateTimeOffset _lastAlertTime = DateTimeOffset.MinValue;
        private DateTimeOffset _lastLockedAlertTime = DateTimeOffset.MinValue;
        private DateTimeOffset _lastCrossedTime;
        private DateTimeOffset _lastLockedTime;
        private DateTimeOffset _lastActivityTime;

        public long TotalCrossedCount => Interlocked.Read(ref _totalCrossedCount);
        public long TotalLockedCount => Interlocked.Read(ref _totalLockedCount);
        public int ConsecutiveCrossedCount => _consecutiveCrossedCount;
        public int ConsecutiveLockedCount => _consecutiveLockedCount;
        public DateTimeOffset LastCrossedTime => _lastCrossedTime;
        public DateTimeOffset LastLockedTime => _lastLockedTime;
        public DateTimeOffset LastActivityTime => _lastActivityTime;

        public void IncrementCrossedCount()
        {
            Interlocked.Increment(ref _totalCrossedCount);
            Interlocked.Increment(ref _consecutiveCrossedCount);
            _lastCrossedTime = DateTimeOffset.UtcNow;
            _lastActivityTime = _lastCrossedTime;
        }

        public void IncrementLockedCount()
        {
            Interlocked.Increment(ref _totalLockedCount);
            Interlocked.Increment(ref _consecutiveLockedCount);
            _lastLockedTime = DateTimeOffset.UtcNow;
            _lastActivityTime = _lastLockedTime;
        }

        public void ResetConsecutiveCounts()
        {
            _consecutiveCrossedCount = 0;
            _consecutiveLockedCount = 0;
            _lastActivityTime = DateTimeOffset.UtcNow;
        }

        public bool CanAlert(DateTimeOffset now, int cooldownMs)
        {
            return (now - _lastAlertTime).TotalMilliseconds >= cooldownMs;
        }

        public bool CanAlertLocked(DateTimeOffset now, int cooldownMs)
        {
            return (now - _lastLockedAlertTime).TotalMilliseconds >= cooldownMs;
        }

        public void RecordAlert(DateTimeOffset time)
        {
            _lastAlertTime = time;
        }

        public void RecordLockedAlert(DateTimeOffset time)
        {
            _lastLockedAlertTime = time;
        }
    }
}

/// <summary>
/// Configuration for crossed market detection.
/// </summary>
public sealed record CrossedMarketConfig
{
    /// <summary>
    /// Minimum time between alerts for the same symbol in milliseconds.
    /// </summary>
    public int AlertCooldownMs { get; init; } = 5000;

    /// <summary>
    /// Whether to also detect locked markets (bid = ask).
    /// </summary>
    public bool DetectLockedMarkets { get; init; } = true;

    /// <summary>
    /// Cooldown for locked market alerts in milliseconds.
    /// </summary>
    public int LockedMarketCooldownMs { get; init; } = 30000;

    public static CrossedMarketConfig Default => new();
}

/// <summary>
/// Alert for a crossed market condition (bid > ask).
/// </summary>
public readonly record struct CrossedMarketAlert(
    string Symbol,
    decimal BidPrice,
    decimal AskPrice,
    decimal BidSize,
    decimal AskSize,
    decimal SpreadAmount,
    decimal SpreadPercent,
    DateTimeOffset Timestamp,
    int ConsecutiveCount
);

/// <summary>
/// Alert for a locked market condition (bid = ask).
/// </summary>
public readonly record struct LockedMarketAlert(
    string Symbol,
    decimal Price,
    decimal BidSize,
    decimal AskSize,
    DateTimeOffset Timestamp,
    int ConsecutiveCount
);

/// <summary>
/// Statistics for crossed market detection.
/// </summary>
public readonly record struct CrossedMarketStats(
    long TotalQuotesProcessed,
    long TotalCrossedEvents,
    long TotalLockedEvents,
    IReadOnlyList<SymbolCrossedStats> SymbolStats
);

/// <summary>
/// Per-symbol crossed market statistics.
/// </summary>
public readonly record struct SymbolCrossedStats(
    string Symbol,
    long TotalCrossedEvents,
    long TotalLockedEvents,
    DateTimeOffset LastCrossedTime,
    DateTimeOffset LastLockedTime
);
