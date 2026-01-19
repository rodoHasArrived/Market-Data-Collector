using System.Collections.Concurrent;
using MarketDataCollector.Application.Logging;
using Serilog;

namespace MarketDataCollector.Application.Monitoring;

/// <summary>
/// Base class for anomaly detectors that provides common functionality:
/// - Per-symbol state management with ConcurrentDictionary
/// - Alert cooldown logic
/// - Automatic cleanup of stale states
/// - Event counters and statistics
/// </summary>
/// <remarks>
/// This base class eliminates ~200+ lines of duplicated code per detector.
/// Concrete implementations only need to provide detection logic and state types.
/// </remarks>
/// <typeparam name="TState">Per-symbol state type for tracking detection state</typeparam>
/// <typeparam name="TAlert">Alert type raised when anomaly is detected</typeparam>
/// <typeparam name="TConfig">Configuration type for the detector</typeparam>
public abstract class AnomalyDetectorBase<TState, TAlert, TConfig> : IDisposable
    where TState : class, new()
    where TConfig : class, new()
{
    protected readonly ILogger Log;
    protected readonly ConcurrentDictionary<string, TState> SymbolStates = new();
    protected readonly TConfig Config;
    private readonly Timer _cleanupTimer;
    private readonly TimeSpan _staleStateAge;
    private volatile bool _isDisposed;

    private long _totalEventsProcessed;
    private long _totalAnomaliesDetected;

    /// <summary>
    /// Event raised when an anomaly is detected.
    /// </summary>
    public event Action<TAlert>? OnAnomaly;

    /// <summary>
    /// Whether the detector has been disposed.
    /// </summary>
    protected bool IsDisposed => _isDisposed;

    /// <summary>
    /// Total events processed by this detector.
    /// </summary>
    public long TotalEventsProcessed => Interlocked.Read(ref _totalEventsProcessed);

    /// <summary>
    /// Total anomalies detected by this detector.
    /// </summary>
    public long TotalAnomaliesDetected => Interlocked.Read(ref _totalAnomaliesDetected);

    /// <summary>
    /// Creates a new anomaly detector with the specified configuration.
    /// </summary>
    /// <param name="config">Configuration for this detector (uses default if null)</param>
    /// <param name="cleanupInterval">How often to clean up stale states (default: 5 minutes)</param>
    /// <param name="staleStateAge">Age after which a state is considered stale (default: 24 hours)</param>
    /// <param name="log">Logger instance</param>
    protected AnomalyDetectorBase(
        TConfig? config = null,
        TimeSpan? cleanupInterval = null,
        TimeSpan? staleStateAge = null,
        ILogger? log = null)
    {
        Config = config ?? new TConfig();
        Log = log ?? LoggingSetup.ForContext(GetType());
        _staleStateAge = staleStateAge ?? TimeSpan.FromHours(24);

        var interval = cleanupInterval ?? TimeSpan.FromMinutes(5);
        _cleanupTimer = new Timer(CleanupOldStates, null, interval, interval);

        Log.Information("{DetectorName} initialized", GetType().Name);
    }

    /// <summary>
    /// Increment the events processed counter.
    /// </summary>
    protected void IncrementEventsProcessed()
    {
        Interlocked.Increment(ref _totalEventsProcessed);
    }

    /// <summary>
    /// Increment the anomalies detected counter.
    /// </summary>
    protected void IncrementAnomaliesDetected()
    {
        Interlocked.Increment(ref _totalAnomaliesDetected);
    }

    /// <summary>
    /// Get or create state for a symbol.
    /// </summary>
    protected TState GetOrCreateState(string symbol)
    {
        return SymbolStates.GetOrAdd(symbol, _ => new TState());
    }

    /// <summary>
    /// Raise an anomaly alert, handling any exceptions in event handlers.
    /// </summary>
    protected void RaiseAlert(TAlert alert, string symbol)
    {
        try
        {
            OnAnomaly?.Invoke(alert);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in anomaly event handler for {Symbol}", symbol);
        }
    }

    /// <summary>
    /// Get the last activity time for a state (used for cleanup).
    /// Override in state classes that track activity time.
    /// </summary>
    protected virtual DateTimeOffset GetLastActivityTime(TState state)
    {
        // Default: use reflection to get LastActivityTime property if it exists
        var prop = typeof(TState).GetProperty("LastActivityTime");
        if (prop != null && prop.PropertyType == typeof(DateTimeOffset))
        {
            return (DateTimeOffset)prop.GetValue(state)!;
        }
        // If no LastActivityTime property, never clean up automatically
        return DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Get symbols that have had anomalies within the specified time window.
    /// </summary>
    public IReadOnlyList<string> GetRecentAnomalySymbols(int minutesBack = 60)
    {
        var cutoff = DateTimeOffset.UtcNow.AddMinutes(-minutesBack);
        return SymbolStates
            .Where(kvp => GetLastAnomalyTime(kvp.Value) > cutoff)
            .Select(kvp => kvp.Key)
            .ToList();
    }

    /// <summary>
    /// Get the last anomaly time for a state.
    /// Override in derived classes if state tracks anomaly time differently.
    /// </summary>
    protected virtual DateTimeOffset GetLastAnomalyTime(TState state)
    {
        // Default: use reflection to get LastAnomalyTime or similar property
        var propNames = new[] { "LastAnomalyTime", "LastAlertTime", "LastSpikeTime", "LastCrossedTime" };
        foreach (var propName in propNames)
        {
            var prop = typeof(TState).GetProperty(propName);
            if (prop != null && prop.PropertyType == typeof(DateTimeOffset))
            {
                return (DateTimeOffset)prop.GetValue(state)!;
            }
        }
        return DateTimeOffset.MinValue;
    }

    private void CleanupOldStates(object? state)
    {
        if (_isDisposed) return;

        try
        {
            var cutoff = DateTimeOffset.UtcNow - _staleStateAge;
            var toRemove = SymbolStates
                .Where(kvp => GetLastActivityTime(kvp.Value) < cutoff)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var symbol in toRemove)
            {
                SymbolStates.TryRemove(symbol, out _);
            }

            if (toRemove.Count > 0)
            {
                Log.Debug("Cleaned up {Count} stale states from {DetectorName}",
                    toRemove.Count, GetType().Name);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error during state cleanup in {DetectorName}", GetType().Name);
        }
    }

    public virtual void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        _cleanupTimer.Dispose();
        SymbolStates.Clear();
    }
}

/// <summary>
/// Base state class that tracks common timing information.
/// Derive from this class to get automatic activity/alert time tracking.
/// </summary>
public abstract class AnomalyStateBase
{
    private DateTimeOffset _lastActivityTime;
    private DateTimeOffset _lastAlertTime = DateTimeOffset.MinValue;
    private long _totalAnomalyCount;
    private int _consecutiveAnomalyCount;

    /// <summary>
    /// Last time any activity occurred for this symbol.
    /// </summary>
    public DateTimeOffset LastActivityTime
    {
        get => _lastActivityTime;
        protected set => _lastActivityTime = value;
    }

    /// <summary>
    /// Last time an alert was raised for this symbol.
    /// </summary>
    public DateTimeOffset LastAlertTime
    {
        get => _lastAlertTime;
        protected set => _lastAlertTime = value;
    }

    /// <summary>
    /// Total count of anomalies detected for this symbol.
    /// </summary>
    public long TotalAnomalyCount => Interlocked.Read(ref _totalAnomalyCount);

    /// <summary>
    /// Current count of consecutive anomalies.
    /// </summary>
    public int ConsecutiveAnomalyCount => _consecutiveAnomalyCount;

    /// <summary>
    /// Record that an anomaly was detected.
    /// </summary>
    public void RecordAnomaly()
    {
        Interlocked.Increment(ref _totalAnomalyCount);
        Interlocked.Increment(ref _consecutiveAnomalyCount);
        _lastActivityTime = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Record that an alert was sent.
    /// </summary>
    public void RecordAlert(DateTimeOffset time)
    {
        _lastAlertTime = time;
    }

    /// <summary>
    /// Reset the consecutive anomaly counter.
    /// </summary>
    public void ResetConsecutiveCount()
    {
        _consecutiveAnomalyCount = 0;
    }

    /// <summary>
    /// Check if enough time has passed since the last alert.
    /// </summary>
    public bool CanAlert(DateTimeOffset now, int cooldownMs)
    {
        return (now - _lastAlertTime).TotalMilliseconds >= cooldownMs;
    }

    /// <summary>
    /// Update activity time without recording an anomaly.
    /// </summary>
    public void UpdateActivity()
    {
        _lastActivityTime = DateTimeOffset.UtcNow;
    }
}

/// <summary>
/// Base configuration class for anomaly detectors.
/// </summary>
public abstract class AnomalyConfigBase
{
    /// <summary>
    /// Minimum time between alerts for the same symbol in milliseconds.
    /// </summary>
    public int AlertCooldownMs { get; init; } = 5000;
}
