using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using MarketDataCollector.Application.Logging;
using Serilog;

namespace MarketDataCollector.Application.Monitoring;

/// <summary>
/// Detects events with timestamps in the future, which indicates data corruption,
/// clock synchronization issues, or provider problems.
/// </summary>
/// <remarks>
/// This detector is part of the data quality framework (QW-109) and helps identify
/// bad data before it affects downstream systems like backtesting or analytics.
/// A small tolerance is allowed to account for network latency and clock skew.
/// </remarks>
public sealed class FutureTimestampDetector : IDisposable
{
    private readonly ILogger _log = LoggingSetup.ForContext<FutureTimestampDetector>();
    private readonly ConcurrentDictionary<string, FutureTimestampState> _symbolStates = new();
    private readonly FutureTimestampConfig _config;
    private readonly Timer _cleanupTimer;
    private volatile bool _isDisposed;

    // Counters
    private long _totalFutureTimestampEvents;
    private long _totalEventsProcessed;

    /// <summary>
    /// Event raised when a future timestamp is detected.
    /// </summary>
    public event Action<FutureTimestampAlert>? OnFutureTimestamp;

    public FutureTimestampDetector(FutureTimestampConfig? config = null)
    {
        _config = config ?? FutureTimestampConfig.Default;
        _cleanupTimer = new Timer(CleanupOldAlerts, null,
            TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));

        _log.Information(
            "FutureTimestampDetector initialized with tolerance {ToleranceMs}ms, alert cooldown {CooldownMs}ms",
            _config.ToleranceMs, _config.AlertCooldownMs);
    }

    /// <summary>
    /// Processes an event timestamp and checks if it is in the future.
    /// </summary>
    /// <param name="symbol">The symbol ticker.</param>
    /// <param name="eventTimestamp">The timestamp from the event.</param>
    /// <param name="eventType">The type of event (trade, quote, bar, etc.).</param>
    /// <param name="provider">The data provider (optional).</param>
    /// <returns>True if a future timestamp was detected.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ProcessTimestamp(
        string symbol,
        DateTimeOffset eventTimestamp,
        EventTimestampType eventType = EventTimestampType.Unknown,
        string? provider = null)
    {
        if (_isDisposed) return false;

        Interlocked.Increment(ref _totalEventsProcessed);

        var now = DateTimeOffset.UtcNow;
        var drift = eventTimestamp - now;

        // Check if timestamp is beyond tolerance into the future
        if (drift.TotalMilliseconds > _config.ToleranceMs)
        {
            Interlocked.Increment(ref _totalFutureTimestampEvents);

            var state = _symbolStates.GetOrAdd(symbol, _ => new FutureTimestampState());
            state.IncrementCount();

            // Only alert if cooldown has passed
            if (state.CanAlert(now, _config.AlertCooldownMs))
            {
                var alert = new FutureTimestampAlert(
                    Symbol: symbol,
                    EventTimestamp: eventTimestamp,
                    ServerTimestamp: now,
                    DriftMs: (long)drift.TotalMilliseconds,
                    EventType: eventType,
                    Provider: provider,
                    DetectedAt: now,
                    ConsecutiveCount: state.ConsecutiveCount
                );

                _log.Error(
                    "FUTURE TIMESTAMP: {Symbol} event at {EventTimestamp:O} is {DriftMs}ms ahead of server time {ServerTime:O} (event type: {EventType}, provider: {Provider})",
                    symbol, eventTimestamp, (long)drift.TotalMilliseconds, now, eventType, provider ?? "unknown");

                state.RecordAlert(now);

                try
                {
                    OnFutureTimestamp?.Invoke(alert);
                }
                catch (Exception ex)
                {
                    _log.Error(ex, "Error in future timestamp event handler for {Symbol}", symbol);
                }

                return true;
            }
        }
        else
        {
            // Valid timestamp - reset consecutive count if tracking this symbol
            if (_symbolStates.TryGetValue(symbol, out var state))
            {
                state.ResetConsecutiveCount();
            }
        }

        return false;
    }

    /// <summary>
    /// Processes a trade event timestamp.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ProcessTrade(string symbol, DateTimeOffset timestamp, string? provider = null)
    {
        return ProcessTimestamp(symbol, timestamp, EventTimestampType.Trade, provider);
    }

    /// <summary>
    /// Processes a quote event timestamp.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ProcessQuote(string symbol, DateTimeOffset timestamp, string? provider = null)
    {
        return ProcessTimestamp(symbol, timestamp, EventTimestampType.Quote, provider);
    }

    /// <summary>
    /// Processes a bar/candle event timestamp.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ProcessBar(string symbol, DateTimeOffset timestamp, string? provider = null)
    {
        return ProcessTimestamp(symbol, timestamp, EventTimestampType.Bar, provider);
    }

    /// <summary>
    /// Processes an order book update timestamp.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ProcessOrderBook(string symbol, DateTimeOffset timestamp, string? provider = null)
    {
        return ProcessTimestamp(symbol, timestamp, EventTimestampType.OrderBook, provider);
    }

    /// <summary>
    /// Gets statistics about future timestamp detection.
    /// </summary>
    public FutureTimestampStats GetStats()
    {
        var symbolStats = new List<SymbolFutureTimestampStats>();

        foreach (var kvp in _symbolStates)
        {
            if (kvp.Value.TotalCount > 0)
            {
                symbolStats.Add(new SymbolFutureTimestampStats(
                    Symbol: kvp.Key,
                    TotalFutureTimestampEvents: kvp.Value.TotalCount,
                    MaxDriftMs: kvp.Value.MaxDriftMs,
                    LastFutureTimestampTime: kvp.Value.LastFutureTime
                ));
            }
        }

        return new FutureTimestampStats(
            TotalEventsProcessed: Interlocked.Read(ref _totalEventsProcessed),
            TotalFutureTimestampEvents: Interlocked.Read(ref _totalFutureTimestampEvents),
            SymbolStats: symbolStats.OrderByDescending(s => s.TotalFutureTimestampEvents).ToList()
        );
    }

    /// <summary>
    /// Gets the count of future timestamp events detected.
    /// </summary>
    public long TotalFutureTimestampEvents => Interlocked.Read(ref _totalFutureTimestampEvents);

    /// <summary>
    /// Gets the total number of events processed.
    /// </summary>
    public long TotalEventsProcessed => Interlocked.Read(ref _totalEventsProcessed);

    /// <summary>
    /// Gets symbols that have had future timestamp events in the last N minutes.
    /// </summary>
    public IReadOnlyList<string> GetRecentFutureTimestampSymbols(int minutesBack = 60)
    {
        var cutoff = DateTimeOffset.UtcNow.AddMinutes(-minutesBack);
        var symbols = new List<string>();

        foreach (var kvp in _symbolStates)
        {
            if (kvp.Value.LastFutureTime > cutoff)
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
                _log.Debug("Cleaned up {Count} inactive symbol states from future timestamp detector", toRemove.Count);
            }
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Error during future timestamp state cleanup");
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
    /// Per-symbol state for future timestamp tracking.
    /// </summary>
    private sealed class FutureTimestampState
    {
        private long _totalCount;
        private int _consecutiveCount;
        private long _maxDriftMs;
        private DateTimeOffset _lastAlertTime = DateTimeOffset.MinValue;
        private DateTimeOffset _lastFutureTime;
        private DateTimeOffset _lastActivityTime;

        public long TotalCount => Interlocked.Read(ref _totalCount);
        public int ConsecutiveCount => _consecutiveCount;
        public long MaxDriftMs => Interlocked.Read(ref _maxDriftMs);
        public DateTimeOffset LastFutureTime => _lastFutureTime;
        public DateTimeOffset LastActivityTime => _lastActivityTime;

        public void IncrementCount()
        {
            Interlocked.Increment(ref _totalCount);
            Interlocked.Increment(ref _consecutiveCount);
            _lastFutureTime = DateTimeOffset.UtcNow;
            _lastActivityTime = _lastFutureTime;
        }

        public void UpdateMaxDrift(long driftMs)
        {
            // Thread-safe max update
            long currentMax;
            do
            {
                currentMax = Interlocked.Read(ref _maxDriftMs);
                if (driftMs <= currentMax) return;
            }
            while (Interlocked.CompareExchange(ref _maxDriftMs, driftMs, currentMax) != currentMax);
        }

        public void ResetConsecutiveCount()
        {
            _consecutiveCount = 0;
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
/// Configuration for future timestamp detection.
/// </summary>
public sealed record FutureTimestampConfig
{
    /// <summary>
    /// Tolerance in milliseconds for timestamps in the future.
    /// Timestamps beyond this threshold are considered invalid.
    /// Default is 5000ms (5 seconds) to account for clock skew and network delays.
    /// </summary>
    public int ToleranceMs { get; init; } = 5000;

    /// <summary>
    /// Minimum time between alerts for the same symbol in milliseconds.
    /// </summary>
    public int AlertCooldownMs { get; init; } = 10000;

    /// <summary>
    /// Default configuration.
    /// </summary>
    public static FutureTimestampConfig Default => new();

    /// <summary>
    /// Strict configuration with lower tolerance (1 second).
    /// </summary>
    public static FutureTimestampConfig Strict => new()
    {
        ToleranceMs = 1000,
        AlertCooldownMs = 5000
    };

    /// <summary>
    /// Lenient configuration with higher tolerance (30 seconds).
    /// Useful for providers with known clock synchronization issues.
    /// </summary>
    public static FutureTimestampConfig Lenient => new()
    {
        ToleranceMs = 30000,
        AlertCooldownMs = 30000
    };
}

/// <summary>
/// Type of event being validated.
/// </summary>
public enum EventTimestampType
{
    Unknown,
    Trade,
    Quote,
    Bar,
    OrderBook,
    Heartbeat,
    Status
}

/// <summary>
/// Alert for a future timestamp condition.
/// </summary>
public readonly record struct FutureTimestampAlert(
    string Symbol,
    DateTimeOffset EventTimestamp,
    DateTimeOffset ServerTimestamp,
    long DriftMs,
    EventTimestampType EventType,
    string? Provider,
    DateTimeOffset DetectedAt,
    int ConsecutiveCount
);

/// <summary>
/// Statistics for future timestamp detection.
/// </summary>
public readonly record struct FutureTimestampStats(
    long TotalEventsProcessed,
    long TotalFutureTimestampEvents,
    IReadOnlyList<SymbolFutureTimestampStats> SymbolStats
);

/// <summary>
/// Per-symbol future timestamp statistics.
/// </summary>
public readonly record struct SymbolFutureTimestampStats(
    string Symbol,
    long TotalFutureTimestampEvents,
    long MaxDriftMs,
    DateTimeOffset LastFutureTimestampTime
);
