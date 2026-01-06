using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using MarketDataCollector.Application.Logging;
using Serilog;

namespace MarketDataCollector.Application.Monitoring;

/// <summary>
/// Detects when symbols haven't received data for a configurable threshold period.
/// Thread-safe and designed for hot-path usage with minimal allocations.
/// </summary>
public sealed class StaleDataDetector : IDisposable
{
    private readonly ILogger _log = LoggingSetup.ForContext<StaleDataDetector>();
    private readonly ConcurrentDictionary<string, SymbolDataState> _symbolStates = new();
    private readonly TimeSpan _staleThreshold;
    private readonly TimeSpan _checkInterval;
    private readonly Timer _checkTimer;
    private readonly CancellationTokenSource _cts = new();
    private volatile bool _isDisposed;

    /// <summary>
    /// Event raised when a symbol becomes stale.
    /// </summary>
    public event Action<StaleDataAlert>? OnStaleData;

    /// <summary>
    /// Event raised when a stale symbol receives new data.
    /// </summary>
    public event Action<string>? OnDataRecovered;

    /// <summary>
    /// Creates a new stale data detector.
    /// </summary>
    /// <param name="staleThresholdSeconds">Seconds without data before a symbol is considered stale. Default: 30 seconds.</param>
    /// <param name="checkIntervalSeconds">How often to check for stale data. Default: 5 seconds.</param>
    public StaleDataDetector(int staleThresholdSeconds = 30, int checkIntervalSeconds = 5)
    {
        _staleThreshold = TimeSpan.FromSeconds(staleThresholdSeconds);
        _checkInterval = TimeSpan.FromSeconds(checkIntervalSeconds);
        _checkTimer = new Timer(CheckStaleSymbols, null, _checkInterval, _checkInterval);

        _log.Information("StaleDataDetector initialized with threshold {ThresholdSeconds}s, check interval {IntervalSeconds}s",
            staleThresholdSeconds, checkIntervalSeconds);
    }

    /// <summary>
    /// Records data received for a symbol. Call this on every trade/quote/depth update.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RecordDataReceived(string symbol)
    {
        if (_isDisposed) return;

        var now = Stopwatch.GetTimestamp();
        var state = _symbolStates.GetOrAdd(symbol, _ => new SymbolDataState());

        var wasStale = state.IsStale;
        state.RecordData(now);

        if (wasStale)
        {
            _log.Information("Symbol {Symbol} recovered - received data after being stale", symbol);
            OnDataRecovered?.Invoke(symbol);
        }
    }

    /// <summary>
    /// Registers a symbol for monitoring. Call this when subscribing to a symbol.
    /// </summary>
    public void RegisterSymbol(string symbol)
    {
        if (_isDisposed) return;

        _symbolStates.GetOrAdd(symbol, _ => new SymbolDataState());
        _log.Debug("Registered symbol {Symbol} for stale data monitoring", symbol);
    }

    /// <summary>
    /// Unregisters a symbol from monitoring. Call this when unsubscribing.
    /// </summary>
    public void UnregisterSymbol(string symbol)
    {
        if (_symbolStates.TryRemove(symbol, out _))
        {
            _log.Debug("Unregistered symbol {Symbol} from stale data monitoring", symbol);
        }
    }

    /// <summary>
    /// Gets the current list of stale symbols.
    /// </summary>
    public IReadOnlyList<StaleDataAlert> GetStaleSymbols()
    {
        var now = Stopwatch.GetTimestamp();
        var staleList = new List<StaleDataAlert>();

        foreach (var kvp in _symbolStates)
        {
            var elapsed = GetElapsedTimeSpan(kvp.Value.LastDataTimestamp, now);
            if (elapsed > _staleThreshold && kvp.Value.HasReceivedData)
            {
                staleList.Add(new StaleDataAlert(
                    Symbol: kvp.Key,
                    LastDataReceived: kvp.Value.LastDataReceivedUtc,
                    StaleDuration: elapsed,
                    TotalEventsReceived: kvp.Value.TotalEventsReceived
                ));
            }
        }

        return staleList;
    }

    /// <summary>
    /// Gets a snapshot of all symbol states for monitoring purposes.
    /// </summary>
    public IReadOnlyList<SymbolDataSnapshot> GetAllSymbolSnapshots()
    {
        var now = Stopwatch.GetTimestamp();
        var snapshots = new List<SymbolDataSnapshot>();

        foreach (var kvp in _symbolStates)
        {
            var elapsed = GetElapsedTimeSpan(kvp.Value.LastDataTimestamp, now);
            snapshots.Add(new SymbolDataSnapshot(
                Symbol: kvp.Key,
                LastDataReceived: kvp.Value.HasReceivedData ? kvp.Value.LastDataReceivedUtc : null,
                SecondsSinceLastData: kvp.Value.HasReceivedData ? elapsed.TotalSeconds : null,
                IsStale: kvp.Value.IsStale,
                TotalEventsReceived: kvp.Value.TotalEventsReceived
            ));
        }

        return snapshots.OrderBy(s => s.Symbol).ToList();
    }

    /// <summary>
    /// Gets the count of currently stale symbols.
    /// </summary>
    public int StaleSymbolCount
    {
        get
        {
            var count = 0;
            foreach (var kvp in _symbolStates)
            {
                if (kvp.Value.IsStale) count++;
            }
            return count;
        }
    }

    /// <summary>
    /// Gets the total number of monitored symbols.
    /// </summary>
    public int MonitoredSymbolCount => _symbolStates.Count;

    private void CheckStaleSymbols(object? state)
    {
        if (_isDisposed) return;

        try
        {
            var now = Stopwatch.GetTimestamp();
            var staleAlerts = new List<StaleDataAlert>();

            foreach (var kvp in _symbolStates)
            {
                var symbolState = kvp.Value;
                if (!symbolState.HasReceivedData) continue;

                var elapsed = GetElapsedTimeSpan(symbolState.LastDataTimestamp, now);

                if (elapsed > _staleThreshold && !symbolState.IsStale)
                {
                    symbolState.MarkStale();
                    var alert = new StaleDataAlert(
                        Symbol: kvp.Key,
                        LastDataReceived: symbolState.LastDataReceivedUtc,
                        StaleDuration: elapsed,
                        TotalEventsReceived: symbolState.TotalEventsReceived
                    );
                    staleAlerts.Add(alert);

                    _log.Warning("Symbol {Symbol} is STALE - no data for {ElapsedSeconds:F1}s (last: {LastData})",
                        kvp.Key, elapsed.TotalSeconds, symbolState.LastDataReceivedUtc);
                }
            }

            foreach (var alert in staleAlerts)
            {
                try
                {
                    OnStaleData?.Invoke(alert);
                }
                catch (Exception ex)
                {
                    _log.Error(ex, "Error in stale data event handler for {Symbol}", alert.Symbol);
                }
            }

            // Log summary periodically
            var staleCount = StaleSymbolCount;
            if (staleCount > 0)
            {
                _log.Debug("Stale data check: {StaleCount}/{TotalCount} symbols stale",
                    staleCount, _symbolStates.Count);
            }
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Error checking for stale symbols");
        }
    }

    private static TimeSpan GetElapsedTimeSpan(long startTimestamp, long endTimestamp)
    {
        var elapsedTicks = endTimestamp - startTimestamp;
        if (elapsedTicks <= 0) return TimeSpan.Zero;
        return TimeSpan.FromSeconds((double)elapsedTicks / Stopwatch.Frequency);
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        _checkTimer.Dispose();
        _cts.Cancel();
        _cts.Dispose();
        _symbolStates.Clear();
    }

    /// <summary>
    /// Per-symbol state tracking.
    /// </summary>
    private sealed class SymbolDataState
    {
        private long _lastDataTimestamp;
        private long _totalEventsReceived;
        private DateTimeOffset _lastDataReceivedUtc;
        private volatile bool _isStale;
        private volatile bool _hasReceivedData;

        public long LastDataTimestamp => Interlocked.Read(ref _lastDataTimestamp);
        public long TotalEventsReceived => Interlocked.Read(ref _totalEventsReceived);
        public DateTimeOffset LastDataReceivedUtc => _lastDataReceivedUtc;
        public bool IsStale => _isStale;
        public bool HasReceivedData => _hasReceivedData;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RecordData(long timestamp)
        {
            Interlocked.Exchange(ref _lastDataTimestamp, timestamp);
            Interlocked.Increment(ref _totalEventsReceived);
            _lastDataReceivedUtc = DateTimeOffset.UtcNow;
            _hasReceivedData = true;
            _isStale = false;
        }

        public void MarkStale()
        {
            _isStale = true;
        }
    }
}

/// <summary>
/// Alert raised when a symbol hasn't received data for the threshold period.
/// </summary>
public readonly record struct StaleDataAlert(
    string Symbol,
    DateTimeOffset LastDataReceived,
    TimeSpan StaleDuration,
    long TotalEventsReceived
);

/// <summary>
/// Snapshot of a symbol's data state for monitoring.
/// </summary>
public readonly record struct SymbolDataSnapshot(
    string Symbol,
    DateTimeOffset? LastDataReceived,
    double? SecondsSinceLastData,
    bool IsStale,
    long TotalEventsReceived
);
