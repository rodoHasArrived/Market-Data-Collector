using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using MarketDataCollector.Application.Logging;
using Serilog;

namespace MarketDataCollector.Application.Monitoring;

/// <summary>
/// Detects negative or zero prices in market data, which indicates data corruption
/// or provider issues. Negative prices are invalid for most financial instruments.
/// </summary>
/// <remarks>
/// This detector is part of the data quality framework (QW-107) and helps identify
/// bad data before it affects downstream systems like backtesting or analytics.
/// </remarks>
public sealed class NegativePriceDetector : IDisposable
{
    private readonly ILogger _log = LoggingSetup.ForContext<NegativePriceDetector>();
    private readonly ConcurrentDictionary<string, NegativePriceState> _symbolStates = new();
    private readonly NegativePriceConfig _config;
    private readonly Timer _cleanupTimer;
    private volatile bool _isDisposed;

    // Counters
    private long _totalNegativePriceEvents;
    private long _totalZeroPriceEvents;
    private long _totalEventsProcessed;

    /// <summary>
    /// Event raised when a negative price is detected.
    /// </summary>
    public event Action<NegativePriceAlert>? OnNegativePrice;

    /// <summary>
    /// Event raised when a zero price is detected (if configured to detect).
    /// </summary>
    public event Action<ZeroPriceAlert>? OnZeroPrice;

    public NegativePriceDetector(NegativePriceConfig? config = null)
    {
        _config = config ?? NegativePriceConfig.Default;
        _cleanupTimer = new Timer(CleanupOldAlerts, null,
            TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));

        _log.Information("NegativePriceDetector initialized with alert cooldown {CooldownMs}ms, DetectZeroPrices: {DetectZero}",
            _config.AlertCooldownMs, _config.DetectZeroPrices);
    }

    /// <summary>
    /// Processes a trade update and checks for negative/zero price conditions.
    /// </summary>
    /// <param name="symbol">The symbol ticker.</param>
    /// <param name="price">The trade price.</param>
    /// <param name="size">The trade size (optional).</param>
    /// <param name="provider">The data provider (optional).</param>
    /// <returns>True if an invalid price was detected.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ProcessTrade(string symbol, decimal price, decimal size = 0, string? provider = null)
    {
        return ProcessPrice(symbol, price, PriceType.Trade, size, provider);
    }

    /// <summary>
    /// Processes a quote update and checks for negative/zero price conditions.
    /// </summary>
    /// <param name="symbol">The symbol ticker.</param>
    /// <param name="bidPrice">The bid price.</param>
    /// <param name="askPrice">The ask price.</param>
    /// <param name="provider">The data provider (optional).</param>
    /// <returns>True if an invalid price was detected.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ProcessQuote(string symbol, decimal bidPrice, decimal askPrice, string? provider = null)
    {
        var detected = false;

        if (ProcessPrice(symbol, bidPrice, PriceType.Bid, 0, provider))
            detected = true;

        if (ProcessPrice(symbol, askPrice, PriceType.Ask, 0, provider))
            detected = true;

        return detected;
    }

    /// <summary>
    /// Processes a bar/candle update and checks for negative/zero price conditions.
    /// </summary>
    /// <param name="symbol">The symbol ticker.</param>
    /// <param name="open">The open price.</param>
    /// <param name="high">The high price.</param>
    /// <param name="low">The low price.</param>
    /// <param name="close">The close price.</param>
    /// <param name="provider">The data provider (optional).</param>
    /// <returns>True if an invalid price was detected.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ProcessBar(string symbol, decimal open, decimal high, decimal low, decimal close, string? provider = null)
    {
        var detected = false;

        if (ProcessPrice(symbol, open, PriceType.Open, 0, provider))
            detected = true;

        if (ProcessPrice(symbol, high, PriceType.High, 0, provider))
            detected = true;

        if (ProcessPrice(symbol, low, PriceType.Low, 0, provider))
            detected = true;

        if (ProcessPrice(symbol, close, PriceType.Close, 0, provider))
            detected = true;

        return detected;
    }

    /// <summary>
    /// Core price validation logic.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool ProcessPrice(string symbol, decimal price, PriceType priceType, decimal size, string? provider)
    {
        if (_isDisposed) return false;

        Interlocked.Increment(ref _totalEventsProcessed);

        var state = _symbolStates.GetOrAdd(symbol, _ => new NegativePriceState());
        var now = DateTimeOffset.UtcNow;

        // Check for negative price
        if (price < 0)
        {
            Interlocked.Increment(ref _totalNegativePriceEvents);
            state.IncrementNegativeCount();

            // Only alert if cooldown has passed
            if (state.CanAlert(now, _config.AlertCooldownMs))
            {
                var alert = new NegativePriceAlert(
                    Symbol: symbol,
                    Price: price,
                    PriceType: priceType,
                    Size: size,
                    Provider: provider,
                    Timestamp: now,
                    ConsecutiveCount: state.ConsecutiveNegativeCount
                );

                _log.Error("NEGATIVE PRICE: {Symbol} {PriceType}={Price:F4} from {Provider}",
                    symbol, priceType, price, provider ?? "unknown");

                state.RecordAlert(now);

                try
                {
                    OnNegativePrice?.Invoke(alert);
                }
                catch (Exception ex)
                {
                    _log.Error(ex, "Error in negative price event handler for {Symbol}", symbol);
                }

                return true;
            }
        }
        // Check for zero price (if configured)
        else if (_config.DetectZeroPrices && price == 0)
        {
            Interlocked.Increment(ref _totalZeroPriceEvents);
            state.IncrementZeroCount();

            if (state.CanAlertZero(now, _config.ZeroPriceCooldownMs))
            {
                var alert = new ZeroPriceAlert(
                    Symbol: symbol,
                    PriceType: priceType,
                    Size: size,
                    Provider: provider,
                    Timestamp: now,
                    ConsecutiveCount: state.ConsecutiveZeroCount
                );

                _log.Warning("ZERO PRICE: {Symbol} {PriceType}=0 from {Provider}",
                    symbol, priceType, provider ?? "unknown");

                state.RecordZeroAlert(now);

                try
                {
                    OnZeroPrice?.Invoke(alert);
                }
                catch (Exception ex)
                {
                    _log.Error(ex, "Error in zero price event handler for {Symbol}", symbol);
                }

                return true;
            }
        }
        else
        {
            // Valid price - reset consecutive counts
            state.ResetConsecutiveCounts();
        }

        return false;
    }

    /// <summary>
    /// Gets statistics about negative price detection.
    /// </summary>
    public NegativePriceStats GetStats()
    {
        var symbolStats = new List<SymbolNegativePriceStats>();

        foreach (var kvp in _symbolStates)
        {
            if (kvp.Value.TotalNegativeCount > 0 || kvp.Value.TotalZeroCount > 0)
            {
                symbolStats.Add(new SymbolNegativePriceStats(
                    Symbol: kvp.Key,
                    TotalNegativePriceEvents: kvp.Value.TotalNegativeCount,
                    TotalZeroPriceEvents: kvp.Value.TotalZeroCount,
                    LastNegativePriceTime: kvp.Value.LastNegativeTime,
                    LastZeroPriceTime: kvp.Value.LastZeroTime
                ));
            }
        }

        return new NegativePriceStats(
            TotalEventsProcessed: Interlocked.Read(ref _totalEventsProcessed),
            TotalNegativePriceEvents: Interlocked.Read(ref _totalNegativePriceEvents),
            TotalZeroPriceEvents: Interlocked.Read(ref _totalZeroPriceEvents),
            SymbolStats: symbolStats.OrderByDescending(s => s.TotalNegativePriceEvents).ToList()
        );
    }

    /// <summary>
    /// Gets the count of negative price events detected.
    /// </summary>
    public long TotalNegativePriceEvents => Interlocked.Read(ref _totalNegativePriceEvents);

    /// <summary>
    /// Gets the count of zero price events detected.
    /// </summary>
    public long TotalZeroPriceEvents => Interlocked.Read(ref _totalZeroPriceEvents);

    /// <summary>
    /// Gets symbols that have had negative price events in the last N minutes.
    /// </summary>
    public IReadOnlyList<string> GetRecentNegativePriceSymbols(int minutesBack = 60)
    {
        var cutoff = DateTimeOffset.UtcNow.AddMinutes(-minutesBack);
        var symbols = new List<string>();

        foreach (var kvp in _symbolStates)
        {
            if (kvp.Value.LastNegativeTime > cutoff)
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
                _log.Debug("Cleaned up {Count} inactive symbol states from negative price detector", toRemove.Count);
            }
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Error during negative price state cleanup");
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
    /// Per-symbol state for negative price tracking.
    /// </summary>
    private sealed class NegativePriceState
    {
        private long _totalNegativeCount;
        private long _totalZeroCount;
        private int _consecutiveNegativeCount;
        private int _consecutiveZeroCount;
        private DateTimeOffset _lastAlertTime = DateTimeOffset.MinValue;
        private DateTimeOffset _lastZeroAlertTime = DateTimeOffset.MinValue;
        private DateTimeOffset _lastNegativeTime;
        private DateTimeOffset _lastZeroTime;
        private DateTimeOffset _lastActivityTime;

        public long TotalNegativeCount => Interlocked.Read(ref _totalNegativeCount);
        public long TotalZeroCount => Interlocked.Read(ref _totalZeroCount);
        public int ConsecutiveNegativeCount => _consecutiveNegativeCount;
        public int ConsecutiveZeroCount => _consecutiveZeroCount;
        public DateTimeOffset LastNegativeTime => _lastNegativeTime;
        public DateTimeOffset LastZeroTime => _lastZeroTime;
        public DateTimeOffset LastActivityTime => _lastActivityTime;

        public void IncrementNegativeCount()
        {
            Interlocked.Increment(ref _totalNegativeCount);
            Interlocked.Increment(ref _consecutiveNegativeCount);
            _lastNegativeTime = DateTimeOffset.UtcNow;
            _lastActivityTime = _lastNegativeTime;
        }

        public void IncrementZeroCount()
        {
            Interlocked.Increment(ref _totalZeroCount);
            Interlocked.Increment(ref _consecutiveZeroCount);
            _lastZeroTime = DateTimeOffset.UtcNow;
            _lastActivityTime = _lastZeroTime;
        }

        public void ResetConsecutiveCounts()
        {
            _consecutiveNegativeCount = 0;
            _consecutiveZeroCount = 0;
            _lastActivityTime = DateTimeOffset.UtcNow;
        }

        public bool CanAlert(DateTimeOffset now, int cooldownMs)
        {
            return (now - _lastAlertTime).TotalMilliseconds >= cooldownMs;
        }

        public bool CanAlertZero(DateTimeOffset now, int cooldownMs)
        {
            return (now - _lastZeroAlertTime).TotalMilliseconds >= cooldownMs;
        }

        public void RecordAlert(DateTimeOffset time)
        {
            _lastAlertTime = time;
        }

        public void RecordZeroAlert(DateTimeOffset time)
        {
            _lastZeroAlertTime = time;
        }
    }
}

/// <summary>
/// Configuration for negative price detection.
/// </summary>
public sealed record NegativePriceConfig
{
    /// <summary>
    /// Minimum time between alerts for the same symbol in milliseconds.
    /// </summary>
    public int AlertCooldownMs { get; init; } = 5000;

    /// <summary>
    /// Whether to also detect zero prices (price = 0).
    /// </summary>
    public bool DetectZeroPrices { get; init; } = true;

    /// <summary>
    /// Cooldown for zero price alerts in milliseconds.
    /// </summary>
    public int ZeroPriceCooldownMs { get; init; } = 30000;

    public static NegativePriceConfig Default => new();
}

/// <summary>
/// Type of price being validated.
/// </summary>
public enum PriceType
{
    Trade,
    Bid,
    Ask,
    Open,
    High,
    Low,
    Close,
    Mid
}

/// <summary>
/// Alert for a negative price condition.
/// </summary>
public readonly record struct NegativePriceAlert(
    string Symbol,
    decimal Price,
    PriceType PriceType,
    decimal Size,
    string? Provider,
    DateTimeOffset Timestamp,
    int ConsecutiveCount
);

/// <summary>
/// Alert for a zero price condition.
/// </summary>
public readonly record struct ZeroPriceAlert(
    string Symbol,
    PriceType PriceType,
    decimal Size,
    string? Provider,
    DateTimeOffset Timestamp,
    int ConsecutiveCount
);

/// <summary>
/// Statistics for negative price detection.
/// </summary>
public readonly record struct NegativePriceStats(
    long TotalEventsProcessed,
    long TotalNegativePriceEvents,
    long TotalZeroPriceEvents,
    IReadOnlyList<SymbolNegativePriceStats> SymbolStats
);

/// <summary>
/// Per-symbol negative price statistics.
/// </summary>
public readonly record struct SymbolNegativePriceStats(
    string Symbol,
    long TotalNegativePriceEvents,
    long TotalZeroPriceEvents,
    DateTimeOffset LastNegativePriceTime,
    DateTimeOffset LastZeroPriceTime
);
