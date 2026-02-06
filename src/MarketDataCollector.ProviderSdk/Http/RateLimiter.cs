using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace MarketDataCollector.ProviderSdk.Http;

/// <summary>
/// Thread-safe rate limiter for API calls with sliding window support.
/// Provider plugins use this to enforce rate limits without depending on core infrastructure.
/// </summary>
public sealed class RateLimiter : IDisposable
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly ConcurrentQueue<DateTimeOffset> _requestTimestamps = new();
    private readonly int _maxRequests;
    private readonly TimeSpan _window;
    private readonly TimeSpan _minDelay;
    private readonly ILogger? _logger;
    private DateTimeOffset _lastRequest = DateTimeOffset.MinValue;
    private bool _disposed;

    public RateLimiter(
        int maxRequestsPerWindow,
        TimeSpan window,
        TimeSpan? minDelayBetweenRequests = null,
        ILogger? logger = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maxRequestsPerWindow, 0, nameof(maxRequestsPerWindow));
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(window, TimeSpan.Zero, nameof(window));

        _maxRequests = maxRequestsPerWindow;
        _window = window;
        _minDelay = minDelayBetweenRequests ?? TimeSpan.Zero;
        _logger = logger;
    }

    /// <summary>
    /// Wait until a request can be made within rate limits.
    /// Returns the time waited.
    /// </summary>
    public async Task<TimeSpan> WaitForSlotAsync(CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var waitStart = DateTimeOffset.UtcNow;

        await _semaphore.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            CleanupOldTimestamps();

            while (_requestTimestamps.Count >= _maxRequests)
            {
                if (_requestTimestamps.TryPeek(out var oldest))
                {
                    var waitTime = oldest.Add(_window) - DateTimeOffset.UtcNow;
                    if (waitTime > TimeSpan.Zero)
                    {
                        _logger?.LogDebug("Rate limit reached, waiting {WaitMs}ms", waitTime.TotalMilliseconds);
                        await Task.Delay(waitTime, ct).ConfigureAwait(false);
                    }
                }
                CleanupOldTimestamps();
            }

            var timeSinceLastRequest = DateTimeOffset.UtcNow - _lastRequest;
            if (timeSinceLastRequest < _minDelay)
            {
                var delayNeeded = _minDelay - timeSinceLastRequest;
                await Task.Delay(delayNeeded, ct).ConfigureAwait(false);
            }

            var now = DateTimeOffset.UtcNow;
            _requestTimestamps.Enqueue(now);
            _lastRequest = now;

            return now - waitStart;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Record a request without waiting (for tracking external calls).
    /// </summary>
    public void RecordRequest()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _requestTimestamps.Enqueue(DateTimeOffset.UtcNow);
        _lastRequest = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Get current usage statistics.
    /// </summary>
    public (int RequestsInWindow, int MaxRequests, TimeSpan WindowRemaining) GetStatus()
    {
        CleanupOldTimestamps();
        var remaining = TimeSpan.Zero;

        if (_requestTimestamps.TryPeek(out var oldest))
        {
            remaining = oldest.Add(_window) - DateTimeOffset.UtcNow;
            if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;
        }

        return (_requestTimestamps.Count, _maxRequests, remaining);
    }

    private void CleanupOldTimestamps()
    {
        var cutoff = DateTimeOffset.UtcNow - _window;
        while (_requestTimestamps.TryPeek(out var oldest) && oldest < cutoff)
        {
            _requestTimestamps.TryDequeue(out _);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _semaphore.Dispose();
    }
}
