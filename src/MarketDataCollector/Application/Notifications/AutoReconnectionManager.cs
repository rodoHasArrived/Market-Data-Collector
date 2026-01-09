using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading;
using MarketDataCollector.Application.Logging;
using MarketDataCollector.Application.Monitoring;
using Serilog;

namespace MarketDataCollector.Application.Notifications;

/// <summary>
/// Manages automatic reconnection with exponential backoff and visual status tracking.
/// Provides events for UI integration and notification integration.
/// </summary>
public sealed class AutoReconnectionManager : IDisposable
{
    private readonly ILogger _log = LoggingSetup.ForContext<AutoReconnectionManager>();
    private readonly ConcurrentDictionary<string, ReconnectionState> _connectionStates = new();
    private readonly AutoReconnectionConfig _config;
    private readonly Timer _statusTimer;
    private volatile bool _isDisposed;

    // Global state
    private volatile bool _isPaused;
    private int _activeReconnections;

    /// <summary>
    /// Event raised when reconnection attempt starts.
    /// </summary>
    public event Action<ReconnectionAttemptEvent>? OnReconnectionAttempt;

    /// <summary>
    /// Event raised when reconnection succeeds.
    /// </summary>
    public event Action<ReconnectionSuccessEvent>? OnReconnectionSuccess;

    /// <summary>
    /// Event raised when reconnection fails (after all attempts exhausted).
    /// </summary>
    public event Action<ReconnectionFailedEvent>? OnReconnectionFailed;

    /// <summary>
    /// Event raised when reconnection status changes (for UI updates).
    /// </summary>
    public event Action<ReconnectionStatusSnapshot>? OnStatusChanged;

    /// <summary>
    /// Delegate for performing the actual reconnection.
    /// </summary>
    public Func<string, CancellationToken, Task<bool>>? ReconnectHandler { get; set; }

    public AutoReconnectionManager(AutoReconnectionConfig? config = null)
    {
        _config = config ?? AutoReconnectionConfig.Default;

        _statusTimer = new Timer(PublishStatus, null,
            TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));

        _log.Information("AutoReconnectionManager initialized with config: MaxAttempts={MaxAttempts}, BaseDelay={BaseDelay}s, MaxDelay={MaxDelay}s",
            _config.MaxAttempts, _config.BaseDelaySeconds, _config.MaxDelaySeconds);
    }

    /// <summary>
    /// Starts automatic reconnection for a connection.
    /// </summary>
    public async Task StartReconnectionAsync(string connectionId, string providerName, CancellationToken ct = default)
    {
        if (_isDisposed || _isPaused)
        {
            _log.Warning("Reconnection not started for {ConnectionId}: manager is {Status}",
                connectionId, _isDisposed ? "disposed" : "paused");
            return;
        }

        var state = _connectionStates.GetOrAdd(connectionId, _ => new ReconnectionState(connectionId, providerName));

        // Check if already reconnecting
        if (state.IsReconnecting)
        {
            _log.Debug("Already reconnecting {ConnectionId}", connectionId);
            return;
        }

        state.StartReconnection(_config.MaxAttempts);
        Interlocked.Increment(ref _activeReconnections);

        _log.Information("Starting reconnection for {ConnectionId} ({Provider})", connectionId, providerName);

        try
        {
            await ExecuteReconnectionLoopAsync(state, ct);
        }
        finally
        {
            Interlocked.Decrement(ref _activeReconnections);
        }
    }

    private async Task ExecuteReconnectionLoopAsync(ReconnectionState state, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && !_isDisposed && !_isPaused && state.HasAttemptsRemaining)
        {
            state.IncrementAttempt();

            // Calculate delay with exponential backoff and jitter
            var delay = CalculateDelay(state.CurrentAttempt);
            state.SetNextAttemptTime(DateTimeOffset.UtcNow.Add(delay));

            // Raise attempt event
            var attemptEvent = new ReconnectionAttemptEvent(
                state.ConnectionId,
                state.ProviderName,
                state.CurrentAttempt,
                state.MaxAttempts,
                delay,
                DateTimeOffset.UtcNow);

            _log.Information("Reconnection attempt {Attempt}/{Max} for {ConnectionId} in {Delay:F1}s",
                state.CurrentAttempt, state.MaxAttempts, state.ConnectionId, delay.TotalSeconds);

            try
            {
                OnReconnectionAttempt?.Invoke(attemptEvent);
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Error in reconnection attempt handler");
            }

            // Wait for delay
            state.SetStatus(ReconnectionStatus.Waiting);
            await Task.Delay(delay, ct);

            if (ct.IsCancellationRequested || _isDisposed || _isPaused) break;

            // Attempt reconnection
            state.SetStatus(ReconnectionStatus.Connecting);

            try
            {
                var success = false;
                if (ReconnectHandler != null)
                {
                    success = await ReconnectHandler(state.ConnectionId, ct);
                }
                else
                {
                    _log.Warning("No reconnect handler configured");
                    break;
                }

                if (success)
                {
                    state.MarkSuccess();

                    _log.Information("Reconnection successful for {ConnectionId} after {Attempts} attempts",
                        state.ConnectionId, state.CurrentAttempt);

                    var successEvent = new ReconnectionSuccessEvent(
                        state.ConnectionId,
                        state.ProviderName,
                        state.CurrentAttempt,
                        state.TotalDowntime,
                        DateTimeOffset.UtcNow);

                    try
                    {
                        OnReconnectionSuccess?.Invoke(successEvent);
                    }
                    catch (Exception ex)
                    {
                        _log.Error(ex, "Error in reconnection success handler");
                    }

                    return;
                }

                _log.Warning("Reconnection attempt {Attempt} failed for {ConnectionId}",
                    state.CurrentAttempt, state.ConnectionId);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "Reconnection attempt {Attempt} failed for {ConnectionId}",
                    state.CurrentAttempt, state.ConnectionId);
            }
        }

        // All attempts exhausted or cancelled
        if (state.CurrentAttempt >= state.MaxAttempts)
        {
            state.MarkFailed();

            _log.Error("Reconnection failed for {ConnectionId} after {Attempts} attempts",
                state.ConnectionId, state.CurrentAttempt);

            var failedEvent = new ReconnectionFailedEvent(
                state.ConnectionId,
                state.ProviderName,
                state.CurrentAttempt,
                state.TotalDowntime,
                DateTimeOffset.UtcNow);

            try
            {
                OnReconnectionFailed?.Invoke(failedEvent);
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Error in reconnection failed handler");
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private TimeSpan CalculateDelay(int attempt)
    {
        // Exponential backoff: baseDelay * 2^(attempt-1)
        var exponentialDelay = _config.BaseDelaySeconds * Math.Pow(2, attempt - 1);

        // Cap at max delay
        var cappedDelay = Math.Min(exponentialDelay, _config.MaxDelaySeconds);

        // Add jitter (0-25% of delay)
        var jitter = cappedDelay * Random.Shared.NextDouble() * 0.25;

        return TimeSpan.FromSeconds(cappedDelay + jitter);
    }

    /// <summary>
    /// Pauses all reconnection attempts (for maintenance).
    /// </summary>
    public void Pause()
    {
        _isPaused = true;
        _log.Information("Auto-reconnection paused");

        foreach (var state in _connectionStates.Values)
        {
            state.SetStatus(ReconnectionStatus.Paused);
        }
    }

    /// <summary>
    /// Resumes reconnection attempts.
    /// </summary>
    public void Resume()
    {
        _isPaused = false;
        _log.Information("Auto-reconnection resumed");
    }

    /// <summary>
    /// Cancels reconnection for a specific connection.
    /// </summary>
    public void CancelReconnection(string connectionId)
    {
        if (_connectionStates.TryGetValue(connectionId, out var state))
        {
            state.SetStatus(ReconnectionStatus.Cancelled);
            _log.Information("Reconnection cancelled for {ConnectionId}", connectionId);
        }
    }

    /// <summary>
    /// Gets the current reconnection status snapshot.
    /// </summary>
    public ReconnectionStatusSnapshot GetStatusSnapshot()
    {
        var states = _connectionStates.Values
            .Select(s => s.GetSnapshot())
            .ToList();

        return new ReconnectionStatusSnapshot
        {
            Timestamp = DateTimeOffset.UtcNow,
            IsPaused = _isPaused,
            ActiveReconnections = _activeReconnections,
            ConnectionStates = states,
            TotalAttempts = states.Sum(s => s.CurrentAttempt),
            TotalSuccesses = states.Count(s => s.Status == ReconnectionStatus.Connected),
            TotalFailures = states.Count(s => s.Status == ReconnectionStatus.Failed)
        };
    }

    /// <summary>
    /// Gets the state for a specific connection.
    /// </summary>
    public ReconnectionStateSnapshot? GetConnectionState(string connectionId)
    {
        return _connectionStates.TryGetValue(connectionId, out var state) ? state.GetSnapshot() : null;
    }

    private void PublishStatus(object? state)
    {
        if (_isDisposed) return;

        try
        {
            var snapshot = GetStatusSnapshot();
            OnStatusChanged?.Invoke(snapshot);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Error publishing reconnection status");
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        _statusTimer.Dispose();
        _connectionStates.Clear();
    }
}

/// <summary>
/// Internal state tracking for a single connection's reconnection.
/// </summary>
internal sealed class ReconnectionState
{
    private readonly string _connectionId;
    private readonly string _providerName;
    private readonly object _lock = new();

    private ReconnectionStatus _status = ReconnectionStatus.Disconnected;
    private int _currentAttempt;
    private int _maxAttempts;
    private DateTimeOffset _disconnectedAt = DateTimeOffset.UtcNow;
    private DateTimeOffset _nextAttemptAt;
    private DateTimeOffset _lastSuccessAt;
    private int _totalReconnections;

    public string ConnectionId => _connectionId;
    public string ProviderName => _providerName;
    public bool IsReconnecting => _status is ReconnectionStatus.Waiting or ReconnectionStatus.Connecting;
    public int CurrentAttempt => _currentAttempt;
    public int MaxAttempts => _maxAttempts;
    public bool HasAttemptsRemaining => _currentAttempt < _maxAttempts;
    public TimeSpan TotalDowntime => DateTimeOffset.UtcNow - _disconnectedAt;

    public ReconnectionState(string connectionId, string providerName)
    {
        _connectionId = connectionId;
        _providerName = providerName;
    }

    public void StartReconnection(int maxAttempts)
    {
        lock (_lock)
        {
            _status = ReconnectionStatus.Waiting;
            _currentAttempt = 0;
            _maxAttempts = maxAttempts;
            _disconnectedAt = DateTimeOffset.UtcNow;
        }
    }

    public void IncrementAttempt()
    {
        Interlocked.Increment(ref _currentAttempt);
    }

    public void SetNextAttemptTime(DateTimeOffset time)
    {
        _nextAttemptAt = time;
    }

    public void SetStatus(ReconnectionStatus status)
    {
        lock (_lock)
        {
            _status = status;
        }
    }

    public void MarkSuccess()
    {
        lock (_lock)
        {
            _status = ReconnectionStatus.Connected;
            _lastSuccessAt = DateTimeOffset.UtcNow;
            Interlocked.Increment(ref _totalReconnections);
        }
    }

    public void MarkFailed()
    {
        lock (_lock)
        {
            _status = ReconnectionStatus.Failed;
        }
    }

    public ReconnectionStateSnapshot GetSnapshot()
    {
        lock (_lock)
        {
            var timeUntilNext = _nextAttemptAt > DateTimeOffset.UtcNow
                ? _nextAttemptAt - DateTimeOffset.UtcNow
                : TimeSpan.Zero;

            return new ReconnectionStateSnapshot
            {
                ConnectionId = _connectionId,
                ProviderName = _providerName,
                Status = _status,
                CurrentAttempt = _currentAttempt,
                MaxAttempts = _maxAttempts,
                TimeUntilNextAttempt = timeUntilNext,
                TotalDowntime = TotalDowntime,
                TotalReconnections = _totalReconnections,
                DisconnectedAt = _disconnectedAt,
                NextAttemptAt = _nextAttemptAt,
                LastSuccessAt = _lastSuccessAt
            };
        }
    }
}

/// <summary>
/// Configuration for auto-reconnection.
/// </summary>
public sealed record AutoReconnectionConfig
{
    /// <summary>
    /// Maximum number of reconnection attempts.
    /// </summary>
    public int MaxAttempts { get; init; } = 10;

    /// <summary>
    /// Base delay in seconds before exponential backoff.
    /// </summary>
    public double BaseDelaySeconds { get; init; } = 2;

    /// <summary>
    /// Maximum delay in seconds (caps exponential backoff).
    /// </summary>
    public double MaxDelaySeconds { get; init; } = 300; // 5 minutes

    public static AutoReconnectionConfig Default => new();
}

/// <summary>
/// Status of a reconnection attempt.
/// </summary>
public enum ReconnectionStatus
{
    Disconnected,
    Waiting,
    Connecting,
    Connected,
    Failed,
    Paused,
    Cancelled
}

/// <summary>
/// Snapshot of a connection's reconnection state.
/// </summary>
public sealed record ReconnectionStateSnapshot
{
    public string ConnectionId { get; init; } = string.Empty;
    public string ProviderName { get; init; } = string.Empty;
    public ReconnectionStatus Status { get; init; }
    public int CurrentAttempt { get; init; }
    public int MaxAttempts { get; init; }
    public TimeSpan TimeUntilNextAttempt { get; init; }
    public TimeSpan TotalDowntime { get; init; }
    public int TotalReconnections { get; init; }
    public DateTimeOffset DisconnectedAt { get; init; }
    public DateTimeOffset NextAttemptAt { get; init; }
    public DateTimeOffset LastSuccessAt { get; init; }
}

/// <summary>
/// Overall reconnection status snapshot.
/// </summary>
public sealed record ReconnectionStatusSnapshot
{
    public DateTimeOffset Timestamp { get; init; }
    public bool IsPaused { get; init; }
    public int ActiveReconnections { get; init; }
    public IReadOnlyList<ReconnectionStateSnapshot> ConnectionStates { get; init; } = Array.Empty<ReconnectionStateSnapshot>();
    public int TotalAttempts { get; init; }
    public int TotalSuccesses { get; init; }
    public int TotalFailures { get; init; }
}

/// <summary>
/// Event raised when a reconnection attempt starts.
/// </summary>
public readonly record struct ReconnectionAttemptEvent(
    string ConnectionId,
    string ProviderName,
    int Attempt,
    int MaxAttempts,
    TimeSpan DelayBeforeAttempt,
    DateTimeOffset Timestamp
);

/// <summary>
/// Event raised when reconnection succeeds.
/// </summary>
public readonly record struct ReconnectionSuccessEvent(
    string ConnectionId,
    string ProviderName,
    int TotalAttempts,
    TimeSpan TotalDowntime,
    DateTimeOffset Timestamp
);

/// <summary>
/// Event raised when reconnection fails after all attempts.
/// </summary>
public readonly record struct ReconnectionFailedEvent(
    string ConnectionId,
    string ProviderName,
    int TotalAttempts,
    TimeSpan TotalDowntime,
    DateTimeOffset Timestamp
);
