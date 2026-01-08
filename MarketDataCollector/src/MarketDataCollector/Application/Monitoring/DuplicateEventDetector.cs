using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading;
using MarketDataCollector.Application.Logging;
using Serilog;

namespace MarketDataCollector.Application.Monitoring;

/// <summary>
/// Detects duplicate market events - events with the same timestamp, price, and size
/// for a given symbol. Duplicates can indicate data feed issues or provider problems.
/// </summary>
public sealed class DuplicateEventDetector : IDisposable
{
    private readonly ILogger _log = LoggingSetup.ForContext<DuplicateEventDetector>();
    private readonly ConcurrentDictionary<string, DuplicateState> _symbolStates = new();
    private readonly DuplicateDetectorConfig _config;
    private readonly Timer _cleanupTimer;
    private volatile bool _isDisposed;

    // Counters
    private long _totalEventsProcessed;
    private long _totalDuplicatesDetected;
    private long _totalExactDuplicates;
    private long _totalNearDuplicates;

    /// <summary>
    /// Event raised when a duplicate is detected.
    /// </summary>
    public event Action<DuplicateEventAlert>? OnDuplicateDetected;

    public DuplicateEventDetector(DuplicateDetectorConfig? config = null)
    {
        _config = config ?? DuplicateDetectorConfig.Default;
        _cleanupTimer = new Timer(CleanupOldStates, null,
            TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));

        _log.Information("DuplicateEventDetector initialized with window {WindowMs}ms, max tracked {MaxTracked}",
            _config.DeduplicationWindowMs, _config.MaxTrackedEventsPerSymbol);
    }

    /// <summary>
    /// Processes a trade event and checks for duplicates.
    /// </summary>
    /// <param name="symbol">The symbol ticker.</param>
    /// <param name="price">The trade price.</param>
    /// <param name="size">The trade size.</param>
    /// <param name="timestamp">The timestamp of the trade.</param>
    /// <param name="sequenceNumber">Optional sequence number for more accurate detection.</param>
    /// <returns>True if this is a duplicate event; false if it's unique.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsDuplicate(string symbol, decimal price, decimal size, DateTimeOffset timestamp, long? sequenceNumber = null)
    {
        if (_isDisposed) return false;

        Interlocked.Increment(ref _totalEventsProcessed);

        var state = _symbolStates.GetOrAdd(symbol, _ => new DuplicateState(_config.MaxTrackedEventsPerSymbol));
        var now = DateTimeOffset.UtcNow;

        // Create event fingerprint
        var fingerprint = new EventFingerprint(timestamp, price, size, sequenceNumber);

        // Check for duplicate
        var duplicateType = state.CheckAndAddEvent(fingerprint, _config.DeduplicationWindowMs, _config.TimestampToleranceMs);

        if (duplicateType != DuplicateType.None)
        {
            Interlocked.Increment(ref _totalDuplicatesDetected);

            if (duplicateType == DuplicateType.Exact)
                Interlocked.Increment(ref _totalExactDuplicates);
            else
                Interlocked.Increment(ref _totalNearDuplicates);

            state.IncrementDuplicateCount();

            // Only raise alert if cooldown has passed
            if (state.CanAlert(now, _config.AlertCooldownMs))
            {
                _log.Warning("DUPLICATE EVENT: {Symbol} Type={DupType} Price={Price:F4} Size={Size} Time={Time:O}",
                    symbol, duplicateType, price, size, timestamp);

                state.RecordAlert(now);

                var alert = new DuplicateEventAlert(
                    Symbol: symbol,
                    Price: price,
                    Size: size,
                    Timestamp: timestamp,
                    DuplicateType: duplicateType,
                    SequenceNumber: sequenceNumber,
                    TotalDuplicatesForSymbol: state.TotalDuplicateCount,
                    DuplicateRatePercent: state.DuplicateRatePercent
                );

                try
                {
                    OnDuplicateDetected?.Invoke(alert);
                }
                catch (Exception ex)
                {
                    _log.Error(ex, "Error in duplicate event handler for {Symbol}", symbol);
                }
            }

            return true;
        }

        return false;
    }

    /// <summary>
    /// Processes a quote event and checks for duplicates.
    /// </summary>
    public bool IsQuoteDuplicate(string symbol, decimal bidPrice, decimal askPrice,
        decimal bidSize, decimal askSize, DateTimeOffset timestamp)
    {
        if (_isDisposed) return false;

        Interlocked.Increment(ref _totalEventsProcessed);

        var state = _symbolStates.GetOrAdd(symbol, _ => new DuplicateState(_config.MaxTrackedEventsPerSymbol));
        var now = DateTimeOffset.UtcNow;

        // Create quote fingerprint (combine bid and ask into a single representation)
        var combinedPrice = bidPrice + askPrice;
        var combinedSize = bidSize + askSize;
        var fingerprint = new EventFingerprint(timestamp, combinedPrice, combinedSize, null);

        var duplicateType = state.CheckAndAddEvent(fingerprint, _config.DeduplicationWindowMs, _config.TimestampToleranceMs);

        if (duplicateType != DuplicateType.None)
        {
            Interlocked.Increment(ref _totalDuplicatesDetected);

            if (duplicateType == DuplicateType.Exact)
                Interlocked.Increment(ref _totalExactDuplicates);
            else
                Interlocked.Increment(ref _totalNearDuplicates);

            state.IncrementDuplicateCount();

            if (state.CanAlert(now, _config.AlertCooldownMs))
            {
                _log.Warning("DUPLICATE QUOTE: {Symbol} Bid={Bid:F4} Ask={Ask:F4} Time={Time:O}",
                    symbol, bidPrice, askPrice, timestamp);

                state.RecordAlert(now);

                var alert = new DuplicateEventAlert(
                    Symbol: symbol,
                    Price: bidPrice,
                    Size: bidSize,
                    Timestamp: timestamp,
                    DuplicateType: duplicateType,
                    SequenceNumber: null,
                    TotalDuplicatesForSymbol: state.TotalDuplicateCount,
                    DuplicateRatePercent: state.DuplicateRatePercent
                );

                try
                {
                    OnDuplicateDetected?.Invoke(alert);
                }
                catch (Exception ex)
                {
                    _log.Error(ex, "Error in duplicate quote event handler for {Symbol}", symbol);
                }
            }

            return true;
        }

        return false;
    }

    /// <summary>
    /// Gets statistics about duplicate detection.
    /// </summary>
    public DuplicateDetectorStats GetStats()
    {
        var symbolStats = new List<SymbolDuplicateStats>();

        foreach (var kvp in _symbolStates)
        {
            if (kvp.Value.TotalDuplicateCount > 0)
            {
                symbolStats.Add(new SymbolDuplicateStats(
                    Symbol: kvp.Key,
                    TotalDuplicates: kvp.Value.TotalDuplicateCount,
                    TotalEventsProcessed: kvp.Value.TotalEventsProcessed,
                    DuplicateRatePercent: kvp.Value.DuplicateRatePercent,
                    LastDuplicateTime: kvp.Value.LastDuplicateTime
                ));
            }
        }

        var totalProcessed = Interlocked.Read(ref _totalEventsProcessed);
        var totalDuplicates = Interlocked.Read(ref _totalDuplicatesDetected);
        var overallRate = totalProcessed > 0 ? (totalDuplicates / (double)totalProcessed) * 100 : 0;

        return new DuplicateDetectorStats(
            TotalEventsProcessed: totalProcessed,
            TotalDuplicatesDetected: totalDuplicates,
            TotalExactDuplicates: Interlocked.Read(ref _totalExactDuplicates),
            TotalNearDuplicates: Interlocked.Read(ref _totalNearDuplicates),
            OverallDuplicateRatePercent: overallRate,
            SymbolStats: symbolStats.OrderByDescending(s => s.DuplicateRatePercent).ToList()
        );
    }

    /// <summary>
    /// Gets the total count of duplicates detected.
    /// </summary>
    public long TotalDuplicatesDetected => Interlocked.Read(ref _totalDuplicatesDetected);

    /// <summary>
    /// Gets the overall duplicate rate as a percentage.
    /// </summary>
    public double DuplicateRatePercent
    {
        get
        {
            var total = Interlocked.Read(ref _totalEventsProcessed);
            var dups = Interlocked.Read(ref _totalDuplicatesDetected);
            return total > 0 ? (dups / (double)total) * 100 : 0;
        }
    }

    /// <summary>
    /// Gets symbols with high duplicate rates (above threshold).
    /// </summary>
    public IReadOnlyList<string> GetHighDuplicateSymbols(double thresholdPercent = 1.0)
    {
        var symbols = new List<string>();

        foreach (var kvp in _symbolStates)
        {
            if (kvp.Value.DuplicateRatePercent >= thresholdPercent)
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
                _log.Debug("Cleaned up {Count} inactive symbol states from duplicate detector", toRemove.Count);
            }
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Error during duplicate detector state cleanup");
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
    /// Compact representation of an event for duplicate detection.
    /// </summary>
    private readonly record struct EventFingerprint(
        DateTimeOffset Timestamp,
        decimal Price,
        decimal Size,
        long? SequenceNumber
    );

    /// <summary>
    /// Per-symbol state for duplicate tracking.
    /// </summary>
    private sealed class DuplicateState
    {
        private readonly object _lock = new();
        private readonly LinkedList<(EventFingerprint Fingerprint, DateTimeOffset ReceivedAt)> _recentEvents;
        private readonly int _maxEvents;

        private long _totalDuplicateCount;
        private long _totalEventsProcessed;
        private DateTimeOffset _lastAlertTime = DateTimeOffset.MinValue;
        private DateTimeOffset _lastDuplicateTime;
        private DateTimeOffset _lastActivityTime;

        public long TotalDuplicateCount => Interlocked.Read(ref _totalDuplicateCount);
        public long TotalEventsProcessed => Interlocked.Read(ref _totalEventsProcessed);
        public DateTimeOffset LastDuplicateTime => _lastDuplicateTime;
        public DateTimeOffset LastActivityTime => _lastActivityTime;

        public double DuplicateRatePercent
        {
            get
            {
                var total = Interlocked.Read(ref _totalEventsProcessed);
                var dups = Interlocked.Read(ref _totalDuplicateCount);
                return total > 0 ? (dups / (double)total) * 100 : 0;
            }
        }

        public DuplicateState(int maxEvents)
        {
            _maxEvents = maxEvents;
            _recentEvents = new LinkedList<(EventFingerprint, DateTimeOffset)>();
        }

        public DuplicateType CheckAndAddEvent(EventFingerprint fingerprint, int windowMs, int toleranceMs)
        {
            lock (_lock)
            {
                Interlocked.Increment(ref _totalEventsProcessed);
                _lastActivityTime = DateTimeOffset.UtcNow;

                var cutoff = _lastActivityTime.AddMilliseconds(-windowMs);

                // Remove old events outside the window
                while (_recentEvents.Count > 0 && _recentEvents.First!.Value.ReceivedAt < cutoff)
                {
                    _recentEvents.RemoveFirst();
                }

                // Check for duplicates
                DuplicateType duplicateType = DuplicateType.None;

                foreach (var (existing, _) in _recentEvents)
                {
                    // Exact duplicate: same timestamp, price, size
                    if (existing.Timestamp == fingerprint.Timestamp &&
                        existing.Price == fingerprint.Price &&
                        existing.Size == fingerprint.Size)
                    {
                        duplicateType = DuplicateType.Exact;
                        break;
                    }

                    // Near duplicate: same price and size, timestamp within tolerance
                    if (existing.Price == fingerprint.Price &&
                        existing.Size == fingerprint.Size &&
                        Math.Abs((existing.Timestamp - fingerprint.Timestamp).TotalMilliseconds) <= toleranceMs)
                    {
                        duplicateType = DuplicateType.Near;
                        // Don't break - continue looking for exact duplicates
                    }

                    // Check sequence number if available
                    if (fingerprint.SequenceNumber.HasValue &&
                        existing.SequenceNumber.HasValue &&
                        existing.SequenceNumber == fingerprint.SequenceNumber)
                    {
                        duplicateType = DuplicateType.Exact;
                        break;
                    }
                }

                // Add the event (even if duplicate, for window tracking)
                _recentEvents.AddLast((fingerprint, _lastActivityTime));

                // Enforce max events limit
                while (_recentEvents.Count > _maxEvents)
                {
                    _recentEvents.RemoveFirst();
                }

                return duplicateType;
            }
        }

        public void IncrementDuplicateCount()
        {
            Interlocked.Increment(ref _totalDuplicateCount);
            _lastDuplicateTime = DateTimeOffset.UtcNow;
            _lastActivityTime = _lastDuplicateTime;
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
/// Type of duplicate detected.
/// </summary>
public enum DuplicateType
{
    /// <summary>No duplicate detected.</summary>
    None,
    /// <summary>Exact duplicate: same timestamp, price, and size.</summary>
    Exact,
    /// <summary>Near duplicate: same price and size, timestamp within tolerance.</summary>
    Near
}

/// <summary>
/// Configuration for duplicate event detection.
/// </summary>
public sealed record DuplicateDetectorConfig
{
    /// <summary>
    /// Time window in milliseconds for duplicate detection.
    /// Events older than this are not checked. Default is 5000ms (5 seconds).
    /// </summary>
    public int DeduplicationWindowMs { get; init; } = 5000;

    /// <summary>
    /// Timestamp tolerance in milliseconds for near-duplicate detection.
    /// Default is 100ms.
    /// </summary>
    public int TimestampToleranceMs { get; init; } = 100;

    /// <summary>
    /// Maximum number of events to track per symbol for duplicate detection.
    /// Default is 1000.
    /// </summary>
    public int MaxTrackedEventsPerSymbol { get; init; } = 1000;

    /// <summary>
    /// Minimum time between alerts for the same symbol in milliseconds.
    /// </summary>
    public int AlertCooldownMs { get; init; } = 5000;

    public static DuplicateDetectorConfig Default => new();

    /// <summary>
    /// Configuration preset for strict duplicate detection.
    /// </summary>
    public static DuplicateDetectorConfig Strict => new()
    {
        DeduplicationWindowMs = 10000,
        TimestampToleranceMs = 50,
        MaxTrackedEventsPerSymbol = 2000,
        AlertCooldownMs = 2000
    };

    /// <summary>
    /// Configuration preset for lenient duplicate detection.
    /// </summary>
    public static DuplicateDetectorConfig Lenient => new()
    {
        DeduplicationWindowMs = 2000,
        TimestampToleranceMs = 200,
        MaxTrackedEventsPerSymbol = 500,
        AlertCooldownMs = 10000
    };
}

/// <summary>
/// Alert for a duplicate event.
/// </summary>
public readonly record struct DuplicateEventAlert(
    string Symbol,
    decimal Price,
    decimal Size,
    DateTimeOffset Timestamp,
    DuplicateType DuplicateType,
    long? SequenceNumber,
    long TotalDuplicatesForSymbol,
    double DuplicateRatePercent
);

/// <summary>
/// Statistics for duplicate detection.
/// </summary>
public readonly record struct DuplicateDetectorStats(
    long TotalEventsProcessed,
    long TotalDuplicatesDetected,
    long TotalExactDuplicates,
    long TotalNearDuplicates,
    double OverallDuplicateRatePercent,
    IReadOnlyList<SymbolDuplicateStats> SymbolStats
);

/// <summary>
/// Per-symbol duplicate statistics.
/// </summary>
public readonly record struct SymbolDuplicateStats(
    string Symbol,
    long TotalDuplicates,
    long TotalEventsProcessed,
    double DuplicateRatePercent,
    DateTimeOffset LastDuplicateTime
);
