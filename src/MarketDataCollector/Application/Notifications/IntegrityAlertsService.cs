using System.Collections.Concurrent;
using System.Threading;
using MarketDataCollector.Application.Logging;
using MarketDataCollector.Domain.Models;
using Serilog;

namespace MarketDataCollector.Application.Notifications;

/// <summary>
/// Service for tracking, aggregating, and alerting on data integrity events.
/// Implements thresholds and windowed aggregation to avoid notification spam.
/// </summary>
public sealed class IntegrityAlertsService : IDisposable
{
    private readonly ILogger _log = LoggingSetup.ForContext<IntegrityAlertsService>();
    private readonly ConcurrentDictionary<string, SymbolIntegrityState> _symbolStates = new();
    private readonly ConcurrentQueue<IntegrityAlertRecord> _recentAlerts = new();
    private readonly IntegrityAlertsConfig _config;
    private readonly Timer _cleanupTimer;
    private readonly Timer _aggregationTimer;
    private volatile bool _isDisposed;

    // Global counters
    private long _totalGaps;
    private long _totalOutOfOrder;
    private long _totalErrors;
    private long _totalWarnings;

    /// <summary>
    /// Event raised when an integrity alert should be notified.
    /// </summary>
    public event Action<IntegrityAlertRecord>? OnIntegrityAlert;

    /// <summary>
    /// Event raised when integrity summary is ready (for dashboard widget).
    /// </summary>
    public event Action<IntegritySummary>? OnIntegritySummaryUpdated;

    public IntegrityAlertsService(IntegrityAlertsConfig? config = null)
    {
        _config = config ?? IntegrityAlertsConfig.Default;

        _cleanupTimer = new Timer(CleanupOldAlerts, null,
            TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));

        _aggregationTimer = new Timer(PublishSummary, null,
            TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(_config.SummaryIntervalSeconds));

        _log.Information("IntegrityAlertsService initialized with config: {Config}", _config);
    }

    /// <summary>
    /// Records an integrity event and determines if it should trigger an alert.
    /// </summary>
    public void RecordEvent(IntegrityEvent evt)
    {
        if (_isDisposed) return;

        var state = _symbolStates.GetOrAdd(evt.Symbol, _ => new SymbolIntegrityState(evt.Symbol));
        state.RecordEvent(evt);

        // Update global counters
        switch (evt.Severity)
        {
            case IntegritySeverity.Error:
                Interlocked.Increment(ref _totalErrors);
                if (evt.Description.Contains("gap", StringComparison.OrdinalIgnoreCase))
                {
                    Interlocked.Increment(ref _totalGaps);
                }
                else if (evt.Description.Contains("order", StringComparison.OrdinalIgnoreCase))
                {
                    Interlocked.Increment(ref _totalOutOfOrder);
                }
                break;
            case IntegritySeverity.Warning:
                Interlocked.Increment(ref _totalWarnings);
                break;
        }

        // Check if we should alert
        var alert = EvaluateAlertConditions(evt, state);
        if (alert != null)
        {
            EnqueueAlert(alert);
        }
    }

    /// <summary>
    /// Gets the current integrity summary for all symbols.
    /// </summary>
    public IntegritySummary GetSummary()
    {
        var recentAlerts = _recentAlerts.ToArray()
            .OrderByDescending(a => a.Timestamp)
            .Take(_config.MaxRecentAlertsInSummary)
            .ToList();

        var symbolSummaries = _symbolStates.Values
            .Select(s => s.GetSummary())
            .OrderByDescending(s => s.TotalErrors + s.TotalWarnings)
            .Take(20)
            .ToList();

        return new IntegritySummary
        {
            Timestamp = DateTimeOffset.UtcNow,
            TotalGaps = Interlocked.Read(ref _totalGaps),
            TotalOutOfOrder = Interlocked.Read(ref _totalOutOfOrder),
            TotalErrors = Interlocked.Read(ref _totalErrors),
            TotalWarnings = Interlocked.Read(ref _totalWarnings),
            SymbolsWithIssues = _symbolStates.Values.Count(s => s.HasRecentIssues()),
            RecentAlerts = recentAlerts,
            SymbolSummaries = symbolSummaries
        };
    }

    /// <summary>
    /// Gets the integrity state for a specific symbol.
    /// </summary>
    public SymbolIntegritySummary? GetSymbolState(string symbol)
    {
        return _symbolStates.TryGetValue(symbol, out var state) ? state.GetSummary() : null;
    }

    /// <summary>
    /// Clears all alerts and resets counters.
    /// </summary>
    public void Reset()
    {
        _symbolStates.Clear();
        while (_recentAlerts.TryDequeue(out _)) { }
        Interlocked.Exchange(ref _totalGaps, 0);
        Interlocked.Exchange(ref _totalOutOfOrder, 0);
        Interlocked.Exchange(ref _totalErrors, 0);
        Interlocked.Exchange(ref _totalWarnings, 0);

        _log.Information("IntegrityAlertsService reset");
    }

    private IntegrityAlertRecord? EvaluateAlertConditions(IntegrityEvent evt, SymbolIntegrityState state)
    {
        // Check if we should suppress due to rate limiting
        if (state.ShouldSuppressAlert(_config.MinAlertIntervalSeconds))
        {
            return null;
        }

        // Determine alert priority
        var priority = DetermineAlertPriority(evt, state);

        // Only alert on Warning or higher
        if (priority < IntegrityAlertPriority.Warning)
        {
            return null;
        }

        state.MarkAlerted();

        return new IntegrityAlertRecord
        {
            Id = Guid.NewGuid().ToString(),
            Timestamp = evt.Timestamp,
            Symbol = evt.Symbol,
            Severity = evt.Severity,
            Priority = priority,
            Description = evt.Description,
            EventCount = state.RecentEventCount,
            StreamId = evt.StreamId,
            Venue = evt.Venue,
            SequenceNumber = evt.SequenceNumber
        };
    }

    private IntegrityAlertPriority DetermineAlertPriority(IntegrityEvent evt, SymbolIntegrityState state)
    {
        // Critical: burst of errors or prolonged issues
        if (state.RecentErrorCount >= _config.CriticalErrorThreshold ||
            state.ConsecutiveErrors >= _config.CriticalConsecutiveErrors)
        {
            return IntegrityAlertPriority.Critical;
        }

        // High: multiple errors in window
        if (state.RecentErrorCount >= _config.HighErrorThreshold ||
            evt.Severity == IntegritySeverity.Error)
        {
            return IntegrityAlertPriority.High;
        }

        // Warning: warnings or single errors
        if (evt.Severity == IntegritySeverity.Warning)
        {
            return IntegrityAlertPriority.Warning;
        }

        return IntegrityAlertPriority.Info;
    }

    private void EnqueueAlert(IntegrityAlertRecord alert)
    {
        _recentAlerts.Enqueue(alert);

        // Trim queue if too large
        while (_recentAlerts.Count > _config.MaxRecentAlerts)
        {
            _recentAlerts.TryDequeue(out _);
        }

        _log.Warning("Integrity alert [{Priority}]: {Symbol} - {Description}",
            alert.Priority, alert.Symbol, alert.Description);

        try
        {
            OnIntegrityAlert?.Invoke(alert);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Error in integrity alert handler");
        }
    }

    private void CleanupOldAlerts(object? state)
    {
        if (_isDisposed) return;

        var cutoff = DateTimeOffset.UtcNow.AddMinutes(-_config.AlertRetentionMinutes);

        foreach (var symbolState in _symbolStates.Values)
        {
            symbolState.CleanupOldEvents(cutoff);
        }

        // Remove symbols with no recent activity
        var inactiveSymbols = _symbolStates
            .Where(kvp => !kvp.Value.HasRecentActivity(TimeSpan.FromHours(1)))
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var symbol in inactiveSymbols)
        {
            _symbolStates.TryRemove(symbol, out _);
        }
    }

    private void PublishSummary(object? state)
    {
        if (_isDisposed) return;

        try
        {
            var summary = GetSummary();
            OnIntegritySummaryUpdated?.Invoke(summary);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Error publishing integrity summary");
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        _cleanupTimer.Dispose();
        _aggregationTimer.Dispose();
        _symbolStates.Clear();
    }
}

/// <summary>
/// Tracks integrity state for a single symbol.
/// </summary>
internal sealed class SymbolIntegrityState
{
    private readonly string _symbol;
    private readonly ConcurrentQueue<IntegrityEvent> _recentEvents = new();
    private readonly object _lock = new();

    private DateTimeOffset _lastAlertTime = DateTimeOffset.MinValue;
    private DateTimeOffset _lastEventTime = DateTimeOffset.MinValue;
    private int _consecutiveErrors;

    public string Symbol => _symbol;
    public int RecentEventCount => _recentEvents.Count;
    public int RecentErrorCount => _recentEvents.Count(e => e.Severity == IntegritySeverity.Error);
    public int ConsecutiveErrors => _consecutiveErrors;

    public SymbolIntegrityState(string symbol)
    {
        _symbol = symbol;
    }

    public void RecordEvent(IntegrityEvent evt)
    {
        _recentEvents.Enqueue(evt);
        _lastEventTime = evt.Timestamp;

        // Trim queue
        while (_recentEvents.Count > 100)
        {
            _recentEvents.TryDequeue(out _);
        }

        // Track consecutive errors
        if (evt.Severity == IntegritySeverity.Error)
        {
            Interlocked.Increment(ref _consecutiveErrors);
        }
        else
        {
            Interlocked.Exchange(ref _consecutiveErrors, 0);
        }
    }

    public bool ShouldSuppressAlert(int minIntervalSeconds)
    {
        var elapsed = DateTimeOffset.UtcNow - _lastAlertTime;
        return elapsed.TotalSeconds < minIntervalSeconds;
    }

    public void MarkAlerted()
    {
        _lastAlertTime = DateTimeOffset.UtcNow;
    }

    public bool HasRecentIssues()
    {
        var cutoff = DateTimeOffset.UtcNow.AddMinutes(-15);
        return _recentEvents.Any(e => e.Timestamp >= cutoff && e.Severity >= IntegritySeverity.Warning);
    }

    public bool HasRecentActivity(TimeSpan window)
    {
        return DateTimeOffset.UtcNow - _lastEventTime < window;
    }

    public void CleanupOldEvents(DateTimeOffset cutoff)
    {
        while (_recentEvents.TryPeek(out var oldest) && oldest.Timestamp < cutoff)
        {
            _recentEvents.TryDequeue(out _);
        }
    }

    public SymbolIntegritySummary GetSummary()
    {
        var events = _recentEvents.ToArray();
        var cutoff15Min = DateTimeOffset.UtcNow.AddMinutes(-15);
        var recentEvents = events.Where(e => e.Timestamp >= cutoff15Min).ToList();

        return new SymbolIntegritySummary
        {
            Symbol = _symbol,
            TotalErrors = events.Count(e => e.Severity == IntegritySeverity.Error),
            TotalWarnings = events.Count(e => e.Severity == IntegritySeverity.Warning),
            RecentErrors = recentEvents.Count(e => e.Severity == IntegritySeverity.Error),
            RecentWarnings = recentEvents.Count(e => e.Severity == IntegritySeverity.Warning),
            ConsecutiveErrors = _consecutiveErrors,
            LastEventTime = _lastEventTime,
            LastAlertTime = _lastAlertTime,
            RecentEvents = recentEvents
                .OrderByDescending(e => e.Timestamp)
                .Take(10)
                .ToList()
        };
    }
}

/// <summary>
/// Configuration for integrity alerts.
/// </summary>
public sealed record IntegrityAlertsConfig
{
    /// <summary>
    /// Minimum seconds between alerts for the same symbol.
    /// </summary>
    public int MinAlertIntervalSeconds { get; init; } = 30;

    /// <summary>
    /// Number of errors in window to trigger high priority.
    /// </summary>
    public int HighErrorThreshold { get; init; } = 3;

    /// <summary>
    /// Number of errors in window to trigger critical priority.
    /// </summary>
    public int CriticalErrorThreshold { get; init; } = 10;

    /// <summary>
    /// Number of consecutive errors to trigger critical priority.
    /// </summary>
    public int CriticalConsecutiveErrors { get; init; } = 5;

    /// <summary>
    /// How long to retain alerts in minutes.
    /// </summary>
    public int AlertRetentionMinutes { get; init; } = 60;

    /// <summary>
    /// Maximum recent alerts to keep.
    /// </summary>
    public int MaxRecentAlerts { get; init; } = 100;

    /// <summary>
    /// Maximum recent alerts to include in summary.
    /// </summary>
    public int MaxRecentAlertsInSummary { get; init; } = 10;

    /// <summary>
    /// How often to publish summary updates in seconds.
    /// </summary>
    public int SummaryIntervalSeconds { get; init; } = 10;

    public static IntegrityAlertsConfig Default => new();
}

/// <summary>
/// Priority levels for integrity alerts.
/// </summary>
public enum IntegrityAlertPriority
{
    Info = 0,
    Warning = 1,
    High = 2,
    Critical = 3
}

/// <summary>
/// Record of an integrity alert.
/// </summary>
public sealed record IntegrityAlertRecord
{
    public string Id { get; init; } = string.Empty;
    public DateTimeOffset Timestamp { get; init; }
    public string Symbol { get; init; } = string.Empty;
    public IntegritySeverity Severity { get; init; }
    public IntegrityAlertPriority Priority { get; init; }
    public string Description { get; init; } = string.Empty;
    public int EventCount { get; init; }
    public string? StreamId { get; init; }
    public string? Venue { get; init; }
    public long SequenceNumber { get; init; }
}

/// <summary>
/// Summary of integrity state for a single symbol.
/// </summary>
public sealed record SymbolIntegritySummary
{
    public string Symbol { get; init; } = string.Empty;
    public int TotalErrors { get; init; }
    public int TotalWarnings { get; init; }
    public int RecentErrors { get; init; }
    public int RecentWarnings { get; init; }
    public int ConsecutiveErrors { get; init; }
    public DateTimeOffset LastEventTime { get; init; }
    public DateTimeOffset LastAlertTime { get; init; }
    public IReadOnlyList<IntegrityEvent> RecentEvents { get; init; } = Array.Empty<IntegrityEvent>();
}

/// <summary>
/// Overall integrity summary for the dashboard.
/// </summary>
public sealed record IntegritySummary
{
    public DateTimeOffset Timestamp { get; init; }
    public long TotalGaps { get; init; }
    public long TotalOutOfOrder { get; init; }
    public long TotalErrors { get; init; }
    public long TotalWarnings { get; init; }
    public int SymbolsWithIssues { get; init; }
    public IReadOnlyList<IntegrityAlertRecord> RecentAlerts { get; init; } = Array.Empty<IntegrityAlertRecord>();
    public IReadOnlyList<SymbolIntegritySummary> SymbolSummaries { get; init; } = Array.Empty<SymbolIntegritySummary>();
}
