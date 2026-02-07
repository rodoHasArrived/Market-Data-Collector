using System.Net.WebSockets;
using MarketDataCollector.Application.Logging;
using Serilog;

namespace MarketDataCollector.Infrastructure.Shared;

/// <summary>
/// Standardized reconnection helper for WebSocket-based providers.
/// Eliminates duplicated reconnection logic across Polygon (~40 LOC),
/// NYSE (~30 LOC), and StockSharp (~35 LOC) providers by providing
/// a single gated exponential-backoff-with-jitter reconnection algorithm.
/// </summary>
/// <remarks>
/// <para>
/// This helper consolidates the three divergent reconnection algorithms:
/// - <c>WebSocketProviderBase</c>: Polly pipeline with circuit breaker
/// - <c>PolygonMarketDataClient</c>: Manual exponential + jitter
/// - <c>NYSEDataSource</c>: Linear multiply backoff
/// - <c>StockSharpMarketDataClient</c>: Exponential + jitter with connector recreation
/// </para>
/// <para>
/// <b>Migration path for full WebSocketProviderBase adoption (ADR-005):</b>
/// 1. NYSE (simplest): Create inner <c>NYSEStreamingClient : WebSocketProviderBase</c>,
///    delegate streaming operations from <c>NYSEDataSource</c>.
/// 2. Polygon: Convert <c>PolygonMarketDataClient</c> to extend <c>WebSocketProviderBase</c>,
///    override <c>ConnectionUri</c>, <c>AuthenticateAsync</c>, <c>HandleMessageAsync</c>.
/// 3. StockSharp: Convert after removing <c>#if STOCKSHARP</c> guard, use connector
///    as internal detail within <c>HandleMessageAsync</c>.
/// </para>
/// </remarks>
[ImplementsAdr("ADR-004", "All async methods support CancellationToken")]
public sealed class WebSocketReconnectionHelper
{
    private readonly SemaphoreSlim _reconnectGate = new(1, 1);
    private readonly ILogger _log;
    private readonly string _providerName;
    private readonly int _maxAttempts;
    private readonly TimeSpan _baseDelay;
    private readonly TimeSpan _maxDelay;
    private volatile bool _isReconnecting;

    public WebSocketReconnectionHelper(
        string providerName,
        int maxAttempts = 10,
        TimeSpan? baseDelay = null,
        TimeSpan? maxDelay = null,
        ILogger? log = null)
    {
        _providerName = providerName;
        _maxAttempts = maxAttempts;
        _baseDelay = baseDelay ?? TimeSpan.FromSeconds(2);
        _maxDelay = maxDelay ?? TimeSpan.FromSeconds(60);
        _log = log ?? LoggingSetup.ForContext<WebSocketReconnectionHelper>();
    }

    /// <summary>
    /// Gets whether a reconnection attempt is currently in progress.
    /// </summary>
    public bool IsReconnecting => _isReconnecting;

    /// <summary>
    /// Attempts reconnection with exponential backoff and jitter.
    /// Guarantees only one reconnection attempt runs at a time via semaphore gating.
    /// </summary>
    /// <param name="reconnectAction">The async action that performs the actual reconnection
    /// (connect WebSocket, authenticate, resubscribe).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if reconnection succeeded; false if max attempts exhausted.</returns>
    public async Task<bool> TryReconnectAsync(
        Func<CancellationToken, Task> reconnectAction,
        CancellationToken ct = default)
    {
        if (!await _reconnectGate.WaitAsync(TimeSpan.Zero, ct).ConfigureAwait(false))
        {
            _log.Debug("{Provider} reconnection already in progress, skipping", _providerName);
            return false;
        }

        _isReconnecting = true;
        try
        {
            for (int attempt = 1; attempt <= _maxAttempts; attempt++)
            {
                ct.ThrowIfCancellationRequested();

                var delay = CalculateDelay(attempt);
                _log.Information(
                    "{Provider} reconnection attempt {Attempt}/{MaxAttempts} in {Delay:F1}s",
                    _providerName, attempt, _maxAttempts, delay.TotalSeconds);

                await Task.Delay(delay, ct).ConfigureAwait(false);

                try
                {
                    await reconnectAction(ct).ConfigureAwait(false);
                    _log.Information("{Provider} reconnected successfully on attempt {Attempt}",
                        _providerName, attempt);
                    return true;
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    _log.Warning(ex,
                        "{Provider} reconnection attempt {Attempt}/{MaxAttempts} failed",
                        _providerName, attempt, _maxAttempts);
                }
            }

            _log.Error("{Provider} reconnection failed after {MaxAttempts} attempts",
                _providerName, _maxAttempts);
            return false;
        }
        finally
        {
            _isReconnecting = false;
            _reconnectGate.Release();
        }
    }

    /// <summary>
    /// Calculates delay with exponential backoff and ±20% jitter.
    /// </summary>
    private TimeSpan CalculateDelay(int attempt)
    {
        var exponentialDelay = _baseDelay * Math.Pow(2, attempt - 1);
        var cappedDelay = TimeSpan.FromMilliseconds(
            Math.Min(exponentialDelay.TotalMilliseconds, _maxDelay.TotalMilliseconds));

        // Add ±20% jitter to prevent thundering herd
        var jitterFactor = 0.8 + (Random.Shared.NextDouble() * 0.4);
        return TimeSpan.FromMilliseconds(cappedDelay.TotalMilliseconds * jitterFactor);
    }
}
