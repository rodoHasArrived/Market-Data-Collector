using System.Collections.Concurrent;
using MarketDataCollector.Application.Logging;
using Serilog;

namespace MarketDataCollector.Infrastructure.Providers.Resilience;

/// <summary>
/// Circuit breaker states following the standard pattern.
/// </summary>
public enum CircuitState
{
    /// <summary>Circuit is closed, requests flow through normally.</summary>
    Closed,

    /// <summary>Circuit is open, requests are immediately rejected.</summary>
    Open,

    /// <summary>Circuit is half-open, allowing a single test request.</summary>
    HalfOpen
}

/// <summary>
/// Configuration options for the circuit breaker.
/// </summary>
public sealed record CircuitBreakerOptions
{
    /// <summary>Number of consecutive failures before opening the circuit.</summary>
    public int FailureThreshold { get; init; } = 5;

    /// <summary>Duration the circuit stays open before transitioning to half-open.</summary>
    public TimeSpan OpenDuration { get; init; } = TimeSpan.FromMinutes(1);

    /// <summary>Number of successful operations required to close the circuit from half-open.</summary>
    public int SuccessThreshold { get; init; } = 1;

    /// <summary>Sliding window for tracking failures.</summary>
    public TimeSpan SlidingWindow { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>Optional exception filter - return true if exception should count as failure.</summary>
    public Func<Exception, bool>? ExceptionFilter { get; init; }
}

/// <summary>
/// Status information about a circuit breaker.
/// </summary>
public sealed record CircuitBreakerStatus(
    string Name,
    CircuitState State,
    int ConsecutiveFailures,
    int ConsecutiveSuccesses,
    DateTimeOffset? LastFailure,
    DateTimeOffset? LastSuccess,
    DateTimeOffset? OpenUntil,
    int TotalFailures,
    int TotalSuccesses
);

/// <summary>
/// Circuit breaker implementation for provider resilience.
/// Prevents cascading failures by failing fast when a provider is unhealthy.
/// </summary>
public sealed class CircuitBreaker
{
    private readonly string _name;
    private readonly CircuitBreakerOptions _options;
    private readonly ILogger _log;
    private readonly object _lock = new();

    private CircuitState _state = CircuitState.Closed;
    private int _consecutiveFailures;
    private int _consecutiveSuccesses;
    private DateTimeOffset? _lastFailure;
    private DateTimeOffset? _lastSuccess;
    private DateTimeOffset? _openUntil;
    private int _totalFailures;
    private int _totalSuccesses;

    public CircuitBreaker(string name, CircuitBreakerOptions? options = null, ILogger? log = null)
    {
        _name = name ?? throw new ArgumentNullException(nameof(name));
        _options = options ?? new CircuitBreakerOptions();
        _log = log ?? LoggingSetup.ForContext<CircuitBreaker>();
    }

    /// <summary>
    /// Current state of the circuit.
    /// </summary>
    public CircuitState State
    {
        get
        {
            lock (_lock)
            {
                // Check if we should transition from Open to HalfOpen
                if (_state == CircuitState.Open && _openUntil.HasValue && DateTimeOffset.UtcNow >= _openUntil)
                {
                    _state = CircuitState.HalfOpen;
                    _log.Information("Circuit breaker {Name} transitioned to HalfOpen", _name);
                }
                return _state;
            }
        }
    }

    /// <summary>
    /// Check if the circuit allows requests.
    /// </summary>
    public bool IsAllowed
    {
        get
        {
            var state = State; // This checks for Open->HalfOpen transition
            return state != CircuitState.Open;
        }
    }

    /// <summary>
    /// Get the current status of the circuit breaker.
    /// </summary>
    public CircuitBreakerStatus GetStatus()
    {
        lock (_lock)
        {
            return new CircuitBreakerStatus(
                _name,
                State, // Use property to check for transitions
                _consecutiveFailures,
                _consecutiveSuccesses,
                _lastFailure,
                _lastSuccess,
                _openUntil,
                _totalFailures,
                _totalSuccesses
            );
        }
    }

    /// <summary>
    /// Record a successful operation.
    /// </summary>
    public void RecordSuccess(TimeSpan? responseTime = null)
    {
        lock (_lock)
        {
            _consecutiveSuccesses++;
            _consecutiveFailures = 0;
            _lastSuccess = DateTimeOffset.UtcNow;
            _totalSuccesses++;

            if (_state == CircuitState.HalfOpen && _consecutiveSuccesses >= _options.SuccessThreshold)
            {
                _state = CircuitState.Closed;
                _openUntil = null;
                _log.Information("Circuit breaker {Name} closed after {Successes} successful operations", _name, _consecutiveSuccesses);
            }
        }
    }

    /// <summary>
    /// Record a failed operation.
    /// </summary>
    public void RecordFailure(Exception? exception = null)
    {
        // Check if this exception should be counted as a failure
        if (exception != null && _options.ExceptionFilter != null && !_options.ExceptionFilter(exception))
        {
            return;
        }

        lock (_lock)
        {
            _consecutiveFailures++;
            _consecutiveSuccesses = 0;
            _lastFailure = DateTimeOffset.UtcNow;
            _totalFailures++;

            if (_state == CircuitState.Closed && _consecutiveFailures >= _options.FailureThreshold)
            {
                OpenCircuit();
            }
            else if (_state == CircuitState.HalfOpen)
            {
                // Any failure in half-open state re-opens the circuit
                OpenCircuit();
            }
        }
    }

    /// <summary>
    /// Manually open the circuit.
    /// </summary>
    public void Open(TimeSpan? duration = null)
    {
        lock (_lock)
        {
            OpenCircuit(duration);
        }
    }

    /// <summary>
    /// Manually reset the circuit to closed state.
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _state = CircuitState.Closed;
            _consecutiveFailures = 0;
            _consecutiveSuccesses = 0;
            _openUntil = null;
            _log.Information("Circuit breaker {Name} manually reset to Closed", _name);
        }
    }

    private void OpenCircuit(TimeSpan? duration = null)
    {
        var openDuration = duration ?? _options.OpenDuration;
        _state = CircuitState.Open;
        _openUntil = DateTimeOffset.UtcNow + openDuration;
        _consecutiveSuccesses = 0;

        _log.Warning(
            "Circuit breaker {Name} opened after {Failures} consecutive failures, will re-evaluate at {OpenUntil}",
            _name, _consecutiveFailures, _openUntil);
    }

    /// <summary>
    /// Execute an operation with circuit breaker protection.
    /// </summary>
    public async Task<TResult> ExecuteAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken ct = default)
    {
        if (!IsAllowed)
        {
            throw new CircuitBreakerOpenException(_name, _openUntil);
        }

        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var result = await operation(ct).ConfigureAwait(false);
            sw.Stop();

            RecordSuccess(sw.Elapsed);
            return result;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Don't count cancellation as failure
            throw;
        }
        catch (Exception ex)
        {
            RecordFailure(ex);
            throw;
        }
    }

    /// <summary>
    /// Execute an operation with circuit breaker protection (void return).
    /// </summary>
    public async Task ExecuteAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken ct = default)
    {
        await ExecuteAsync<object?>(async token =>
        {
            await operation(token).ConfigureAwait(false);
            return null;
        }, ct).ConfigureAwait(false);
    }
}

/// <summary>
/// Exception thrown when attempting to execute through an open circuit.
/// </summary>
public sealed class CircuitBreakerOpenException : Exception
{
    public string CircuitName { get; }
    public DateTimeOffset? OpenUntil { get; }

    public CircuitBreakerOpenException(string circuitName, DateTimeOffset? openUntil)
        : base($"Circuit breaker '{circuitName}' is open. Will re-evaluate at {openUntil}")
    {
        CircuitName = circuitName;
        OpenUntil = openUntil;
    }
}

/// <summary>
/// Registry for managing circuit breakers across providers.
/// </summary>
public sealed class CircuitBreakerRegistry : IDisposable
{
    private readonly ConcurrentDictionary<string, CircuitBreaker> _breakers = new(StringComparer.OrdinalIgnoreCase);
    private readonly CircuitBreakerOptions _defaultOptions;
    private readonly ILogger _log;

    public CircuitBreakerRegistry(CircuitBreakerOptions? defaultOptions = null, ILogger? log = null)
    {
        _defaultOptions = defaultOptions ?? new CircuitBreakerOptions();
        _log = log ?? LoggingSetup.ForContext<CircuitBreakerRegistry>();
    }

    /// <summary>
    /// Get or create a circuit breaker for a provider.
    /// </summary>
    public CircuitBreaker GetOrCreate(string name, CircuitBreakerOptions? options = null)
    {
        return _breakers.GetOrAdd(name, n =>
        {
            _log.Debug("Creating circuit breaker for {Name}", n);
            return new CircuitBreaker(n, options ?? _defaultOptions, _log);
        });
    }

    /// <summary>
    /// Get status of all circuit breakers.
    /// </summary>
    public IReadOnlyDictionary<string, CircuitBreakerStatus> GetAllStatus()
    {
        return _breakers.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.GetStatus(),
            StringComparer.OrdinalIgnoreCase
        );
    }

    /// <summary>
    /// Reset all circuit breakers.
    /// </summary>
    public void ResetAll()
    {
        foreach (var breaker in _breakers.Values)
        {
            breaker.Reset();
        }
        _log.Information("All circuit breakers reset");
    }

    /// <summary>
    /// Check if a provider's circuit is allowing requests.
    /// </summary>
    public bool IsProviderAvailable(string providerName)
    {
        if (_breakers.TryGetValue(providerName, out var breaker))
        {
            return breaker.IsAllowed;
        }
        return true; // Unknown provider is assumed available
    }

    public void Dispose()
    {
        _breakers.Clear();
    }
}
