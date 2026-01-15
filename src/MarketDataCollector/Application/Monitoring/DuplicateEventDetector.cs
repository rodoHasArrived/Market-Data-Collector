using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using MarketDataCollector.Application.Logging;
using Serilog;

namespace MarketDataCollector.Application.Monitoring;

/// <summary>
/// Detects duplicate events based on timestamp and key fields.
/// Part of the data quality framework (DQ-2) to identify when the same
/// event is received multiple times, which may indicate provider issues.
/// </summary>
/// <remarks>
/// Uses a time-windowed hash set per symbol to track recent event fingerprints.
/// Events with the same fingerprint within the window are flagged as duplicates.
/// </remarks>
public sealed class DuplicateEventDetector : IDisposable
{
    private readonly ILogger _log = LoggingSetup.ForContext<DuplicateEventDetector>();
    private readonly ConcurrentDictionary<string, DuplicateState> _symbolStates = new();
    private readonly DuplicateDetectorConfig _config;
    private readonly Timer _cleanupTimer;
    private volatile bool _isDisposed;

    // Counters
    private long _totalDuplicatesDetected;
    private long _totalEventsProcessed;

    /// <summary>
    /// Event raised when a duplicate is detected.
    /// </summary>
    public event Action<DuplicateEventAlert>? OnDuplicate;

    public DuplicateEventDetector(DuplicateDetectorConfig? config = null)
    {
        _config = config ?? DuplicateDetectorConfig.Default;
        _cleanupTimer = new Timer(CleanupOldStates, null,
            TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));

        _log.Information(
            "DuplicateEventDetector initialized with window {WindowMs}ms, max entries {MaxEntries}",
            _config.WindowMs, _config.MaxEntriesPerSymbol);
    }

    /// <summary>
    /// Checks if a trade event is a duplicate.
    /// </summary>
    /// <param name="symbol">The symbol ticker.</param>
    /// <param name="timestamp">The event timestamp.</param>
    /// <param name="price">The trade price.</param>
    /// <param name="size">The trade size.</param>
    /// <param name="provider">The data provider (optional).</param>
    /// <returns>True if this is a duplicate event.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsDuplicateTrade(string symbol, DateTimeOffset timestamp, decimal price, decimal size, string? provider = null)
    {
        // Fingerprint: timestamp + price + size
        var fingerprint = HashCode.Combine(timestamp.UtcTicks, price, size);
        return CheckDuplicate(symbol, fingerprint, EventKind.Trade, timestamp, provider);
    }

    /// <summary>
    /// Checks if a quote event is a duplicate.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsDuplicateQuote(string symbol, DateTimeOffset timestamp, decimal bidPrice, decimal askPrice,
        int bidSize, int askSize, string? provider = null)
    {
        // Fingerprint: timestamp + bid/ask prices and sizes
        var fingerprint = HashCode.Combine(timestamp.UtcTicks, bidPrice, askPrice, bidSize, askSize);
        return CheckDuplicate(symbol, fingerprint, EventKind.Quote, timestamp, provider);
    }

    /// <summary>
    /// Checks if a bar event is a duplicate.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsDuplicateBar(string symbol, DateTimeOffset timestamp, decimal open, decimal high,
        decimal low, decimal close, long volume, string? provider = null)
    {
        // Fingerprint: timestamp + OHLCV
        var fingerprint = HashCode.Combine(timestamp.UtcTicks, open, high, low, close, volume);
        return CheckDuplicate(symbol, fingerprint, EventKind.Bar, timestamp, provider);
    }

    /// <summary>
    /// Checks if a generic event fingerprint is a duplicate.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsDuplicate(string symbol, int fingerprint, EventKind kind, DateTimeOffset timestamp, string? provider = null)
    {
        return CheckDuplicate(symbol, fingerprint, kind, timestamp, provider);
    }

    /// <summary>
    /// Core duplicate detection logic.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool CheckDuplicate(string symbol, int fingerprint, EventKind kind, DateTimeOffset eventTimestamp, string? provider)
    {
        if (_isDisposed) return false;

        Interlocked.Increment(ref _totalEventsProcessed);

        var state = _symbolStates.GetOrAdd(symbol, _ => new DuplicateState(_config.MaxEntriesPerSymbol));
        var now = DateTimeOffset.UtcNow;

        // Remove old fingerprints
        state.CleanupOld(now, _config.WindowMs);

        // Check if fingerprint exists
        if (state.Contains(fingerprint))
        {
            Interlocked.Increment(ref _totalDuplicatesDetected);
            state.IncrementDuplicateCount();

            // Only alert if cooldown has passed
            if (state.CanAlert(now, _config.AlertCooldownMs))
            {
                var alert = new DuplicateEventAlert(
                    Symbol: symbol,
                    Kind: kind,
                    EventTimestamp: eventTimestamp,
                    DetectedAt: now,
                    Fingerprint: fingerprint,
                    Provider: provider,
                    DuplicateCountInWindow: state.DuplicateCountInWindow
                );

                _log.Warning(
                    "DUPLICATE EVENT: {Symbol} {Kind} at {Timestamp} (fingerprint {Fingerprint:X8}) from {Provider}",
                    symbol, kind, eventTimestamp, fingerprint, provider ?? "unknown");

                state.RecordAlert(now);

                try
                {
                    OnDuplicate?.Invoke(alert);
                }
                catch (Exception ex)
                {
                    _log.Error(ex, "Error in duplicate event handler for {Symbol}", symbol);
                }
            }

            return true;
        }

        // Add new fingerprint
        state.Add(fingerprint, now);
        return false;
    }

    /// <summary>
    /// Gets statistics about duplicate detection.
    /// </summary>
    public DuplicateDetectorStats GetStats()
    {
        var symbolStats = _symbolStates
            .Where(kvp => kvp.Value.TotalDuplicateCount > 0)
            .Select(kvp => new SymbolDuplicateStats(
                Symbol: kvp.Key,
                TotalDuplicates: kvp.Value.TotalDuplicateCount,
                RecentDuplicates: kvp.Value.DuplicateCountInWindow,
                LastDuplicateTime: kvp.Value.LastDuplicateTime
            ))
            .OrderByDescending(s => s.TotalDuplicates)
            .ToList();

        return new DuplicateDetectorStats(
            TotalEventsProcessed: Interlocked.Read(ref _totalEventsProcessed),
            TotalDuplicatesDetected: Interlocked.Read(ref _totalDuplicatesDetected),
            SymbolStats: symbolStats
        );
    }

    /// <summary>
    /// Gets the total count of duplicates detected.
    /// </summary>
    public long TotalDuplicatesDetected => Interlocked.Read(ref _totalDuplicatesDetected);

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

            // Also cleanup old fingerprints in active states
            var now = DateTimeOffset.UtcNow;
            foreach (var kvp in _symbolStates)
            {
                kvp.Value.CleanupOld(now, _config.WindowMs);
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
    /// Per-symbol state for duplicate tracking.
    /// </summary>
    private sealed class DuplicateState
    {
        private readonly Dictionary<int, DateTimeOffset> _fingerprints;
        private readonly object _lock = new();
        private readonly int _maxEntries;

        private long _totalDuplicateCount;
        private int _duplicateCountInWindow;
        private DateTimeOffset _lastAlertTime = DateTimeOffset.MinValue;
        private DateTimeOffset _lastDuplicateTime;
        private DateTimeOffset _lastActivityTime;

        public long TotalDuplicateCount => Interlocked.Read(ref _totalDuplicateCount);
        public int DuplicateCountInWindow => _duplicateCountInWindow;
        public DateTimeOffset LastDuplicateTime => _lastDuplicateTime;
        public DateTimeOffset LastActivityTime => _lastActivityTime;

        public DuplicateState(int maxEntries)
        {
            _maxEntries = maxEntries;
            _fingerprints = new Dictionary<int, DateTimeOffset>(maxEntries);
        }

        public bool Contains(int fingerprint)
        {
            lock (_lock)
            {
                return _fingerprints.ContainsKey(fingerprint);
            }
        }

        public void Add(int fingerprint, DateTimeOffset time)
        {
            lock (_lock)
            {
                _lastActivityTime = time;

                // If at capacity, remove oldest
                if (_fingerprints.Count >= _maxEntries)
                {
                    var oldest = _fingerprints.MinBy(kvp => kvp.Value);
                    _fingerprints.Remove(oldest.Key);
                }

                _fingerprints[fingerprint] = time;
            }
        }

        public void CleanupOld(DateTimeOffset now, int windowMs)
        {
            lock (_lock)
            {
                var cutoff = now.AddMilliseconds(-windowMs);
                var toRemove = _fingerprints
                    .Where(kvp => kvp.Value < cutoff)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var key in toRemove)
                {
                    _fingerprints.Remove(key);
                }

                // Reset window counter if window has passed
                if (_lastDuplicateTime < cutoff)
                {
                    _duplicateCountInWindow = 0;
                }
            }
        }

        public void IncrementDuplicateCount()
        {
            Interlocked.Increment(ref _totalDuplicateCount);
            Interlocked.Increment(ref _duplicateCountInWindow);
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
/// Configuration for duplicate event detection.
/// </summary>
public sealed record DuplicateDetectorConfig
{
    /// <summary>
    /// Time window in milliseconds for tracking fingerprints.
    /// Events with the same fingerprint within this window are duplicates.
    /// Default is 5000ms (5 seconds).
    /// </summary>
    public int WindowMs { get; init; } = 5000;

    /// <summary>
    /// Maximum fingerprint entries to keep per symbol.
    /// Older entries are evicted when this limit is reached.
    /// </summary>
    public int MaxEntriesPerSymbol { get; init; } = 10000;

    /// <summary>
    /// Minimum time between alerts for the same symbol in milliseconds.
    /// </summary>
    public int AlertCooldownMs { get; init; } = 5000;

    public static DuplicateDetectorConfig Default => new();

    /// <summary>
    /// Strict configuration for high-frequency data.
    /// </summary>
    public static DuplicateDetectorConfig Strict => new()
    {
        WindowMs = 1000,
        MaxEntriesPerSymbol = 50000,
        AlertCooldownMs = 1000
    };
}

/// <summary>
/// Kind of event being checked for duplicates.
/// </summary>
public enum EventKind
{
    Trade,
    Quote,
    Bar,
    OrderBook,
    Other
}

/// <summary>
/// Alert for a duplicate event condition.
/// </summary>
public readonly record struct DuplicateEventAlert(
    string Symbol,
    EventKind Kind,
    DateTimeOffset EventTimestamp,
    DateTimeOffset DetectedAt,
    int Fingerprint,
    string? Provider,
    int DuplicateCountInWindow
);

/// <summary>
/// Statistics for duplicate detection.
/// </summary>
public readonly record struct DuplicateDetectorStats(
    long TotalEventsProcessed,
    long TotalDuplicatesDetected,
    IReadOnlyList<SymbolDuplicateStats> SymbolStats
);

/// <summary>
/// Per-symbol duplicate statistics.
/// </summary>
public readonly record struct SymbolDuplicateStats(
    string Symbol,
    long TotalDuplicates,
    int RecentDuplicates,
    DateTimeOffset LastDuplicateTime
);
