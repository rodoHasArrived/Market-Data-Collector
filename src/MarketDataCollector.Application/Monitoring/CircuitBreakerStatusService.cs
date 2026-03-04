using System.Collections.Concurrent;
using MarketDataCollector.Application.Logging;
using Serilog;

namespace MarketDataCollector.Application.Monitoring;

/// <summary>
/// Tracks circuit breaker state transitions and provides an observable dashboard
/// of all circuit breakers in the system. Allows operators to see which providers
/// are healthy (closed), degraded (half-open), or failing (open) at a glance.
/// </summary>
public sealed class CircuitBreakerStatusService
{
    private readonly ILogger _log = LoggingSetup.ForContext<CircuitBreakerStatusService>();
    private readonly ConcurrentDictionary<string, CircuitBreakerInfo> _breakers = new();

    /// <summary>
    /// Event raised when any circuit breaker changes state.
    /// </summary>
    public event Action<CircuitBreakerStateChange>? OnStateChanged;

    /// <summary>
    /// Registers or updates a circuit breaker's state.
    /// Call this from resilience policy callbacks (OnOpened, OnClosed, OnHalfOpened).
    /// </summary>
    public void RecordStateTransition(
        string name,
        CircuitBreakerState newState,
        string? lastError = null)
    {
        var now = DateTimeOffset.UtcNow;
        var info = _breakers.AddOrUpdate(
            name,
            _ => new CircuitBreakerInfo
            {
                Name = name,
                State = newState,
                LastStateChange = now,
                TripCount = newState == CircuitBreakerState.Open ? 1 : 0,
                LastError = lastError
            },
            (_, existing) =>
            {
                var oldState = existing.State;
                existing.State = newState;
                existing.LastStateChange = now;
                if (newState == CircuitBreakerState.Open)
                    existing.TripCount++;
                if (lastError != null)
                    existing.LastError = lastError;
                return existing;
            });

        _log.Information(
            "CircuitBreaker {Name} transitioned to {NewState}. Trip count: {TripCount}",
            name, newState, info.TripCount);

        try
        {
            OnStateChanged?.Invoke(new CircuitBreakerStateChange(name, newState, now, lastError));
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Error in circuit breaker state change handler");
        }
    }

    /// <summary>
    /// Registers a circuit breaker so it appears in the dashboard even before it trips.
    /// </summary>
    public void Register(string name)
    {
        _breakers.TryAdd(name, new CircuitBreakerInfo
        {
            Name = name,
            State = CircuitBreakerState.Closed,
            LastStateChange = DateTimeOffset.UtcNow,
            TripCount = 0
        });
    }

    /// <summary>
    /// Gets the current status of all registered circuit breakers.
    /// </summary>
    public CircuitBreakerDashboard GetDashboard()
    {
        var breakers = _breakers.Values
            .OrderBy(b => b.Name)
            .Select(b => new CircuitBreakerStatus(
                Name: b.Name,
                State: b.State.ToString(),
                LastStateChange: b.LastStateChange,
                TripCount: b.TripCount,
                LastError: b.LastError,
                TimeSinceLastChange: DateTimeOffset.UtcNow - b.LastStateChange))
            .ToList();

        var openCount = breakers.Count(b => b.State == nameof(CircuitBreakerState.Open));
        var halfOpenCount = breakers.Count(b => b.State == nameof(CircuitBreakerState.HalfOpen));

        var overallHealth = openCount > 0
            ? "Red"
            : halfOpenCount > 0
                ? "Yellow"
                : "Green";

        return new CircuitBreakerDashboard(
            OverallHealth: overallHealth,
            TotalBreakers: breakers.Count,
            OpenCount: openCount,
            HalfOpenCount: halfOpenCount,
            ClosedCount: breakers.Count - openCount - halfOpenCount,
            Breakers: breakers,
            Timestamp: DateTimeOffset.UtcNow);
    }

    private sealed class CircuitBreakerInfo
    {
        public required string Name { get; init; }
        public CircuitBreakerState State { get; set; }
        public DateTimeOffset LastStateChange { get; set; }
        public int TripCount { get; set; }
        public string? LastError { get; set; }
    }
}

/// <summary>
/// Circuit breaker state enum.
/// </summary>
public enum CircuitBreakerState
{
    Closed,
    Open,
    HalfOpen
}

/// <summary>
/// Represents a circuit breaker state change event.
/// </summary>
public sealed record CircuitBreakerStateChange(
    string Name,
    CircuitBreakerState NewState,
    DateTimeOffset Timestamp,
    string? LastError);

/// <summary>
/// Status of a single circuit breaker.
/// </summary>
public sealed record CircuitBreakerStatus(
    string Name,
    string State,
    DateTimeOffset LastStateChange,
    int TripCount,
    string? LastError,
    TimeSpan TimeSinceLastChange);

/// <summary>
/// Dashboard view of all circuit breakers.
/// </summary>
public sealed record CircuitBreakerDashboard(
    string OverallHealth,
    int TotalBreakers,
    int OpenCount,
    int HalfOpenCount,
    int ClosedCount,
    IReadOnlyList<CircuitBreakerStatus> Breakers,
    DateTimeOffset Timestamp);
