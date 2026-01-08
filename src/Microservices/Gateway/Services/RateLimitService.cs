using System.Collections.Concurrent;
using System.Threading;
using DataIngestion.Gateway.Configuration;

namespace DataIngestion.Gateway.Services;

/// <summary>
/// Rate limiting service for API requests.
/// </summary>
public interface IRateLimitService
{
    /// <summary>Check if request is allowed.</summary>
    bool IsAllowed(string clientId, string endpoint);

    /// <summary>Get remaining quota for client.</summary>
    RateLimitInfo GetRateLimitInfo(string clientId, string endpoint);
}

/// <summary>
/// Rate limit information.
/// </summary>
public record RateLimitInfo(
    int Limit,
    int Remaining,
    DateTimeOffset ResetAt
);

/// <summary>
/// Sliding window rate limiter implementation.
/// </summary>
public sealed class RateLimitService : IRateLimitService
{
    private readonly GatewayConfig _config;
    private readonly ConcurrentDictionary<string, RateLimitBucket> _buckets = new();
    private readonly Timer _cleanupTimer;

    public RateLimitService(GatewayConfig config)
    {
        _config = config;
        _cleanupTimer = new Timer(Cleanup, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
    }

    public bool IsAllowed(string clientId, string endpoint)
    {
        if (!_config.RateLimit.Enabled)
            return true;

        var key = $"{clientId}:{endpoint}";
        var bucket = _buckets.GetOrAdd(key, _ => new RateLimitBucket(
            _config.RateLimit.RequestsPerSecond,
            _config.RateLimit.BurstSize
        ));

        return bucket.TryConsume();
    }

    public RateLimitInfo GetRateLimitInfo(string clientId, string endpoint)
    {
        var key = $"{clientId}:{endpoint}";

        if (_buckets.TryGetValue(key, out var bucket))
        {
            return new RateLimitInfo(
                Limit: bucket.Limit,
                Remaining: bucket.GetRemaining(),
                ResetAt: bucket.ResetAt
            );
        }

        return new RateLimitInfo(
            Limit: _config.RateLimit.RequestsPerSecond,
            Remaining: _config.RateLimit.RequestsPerSecond,
            ResetAt: DateTimeOffset.UtcNow.AddSeconds(1)
        );
    }

    private void Cleanup(object? state)
    {
        var now = DateTimeOffset.UtcNow;
        var keysToRemove = _buckets
            .Where(kvp => kvp.Value.IsExpired(now))
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in keysToRemove)
        {
            _buckets.TryRemove(key, out _);
        }
    }

    private class RateLimitBucket
    {
        private readonly int _limit;
        private readonly int _burstSize;
        private readonly object _lock = new();
        private readonly Queue<DateTimeOffset> _requests = new();
        private DateTimeOffset _windowStart;

        public int Limit => _limit;
        public DateTimeOffset ResetAt => _windowStart.AddSeconds(1);

        public RateLimitBucket(int limit, int burstSize)
        {
            _limit = limit;
            _burstSize = burstSize;
            _windowStart = DateTimeOffset.UtcNow;
        }

        public bool TryConsume()
        {
            lock (_lock)
            {
                var now = DateTimeOffset.UtcNow;
                var windowStart = now.AddSeconds(-1);

                // Remove old requests outside the window
                while (_requests.Count > 0 && _requests.Peek() < windowStart)
                {
                    _requests.Dequeue();
                }

                // Check if under limit
                if (_requests.Count >= _limit + _burstSize)
                {
                    return false;
                }

                _requests.Enqueue(now);
                _windowStart = windowStart;
                return true;
            }
        }

        public int GetRemaining()
        {
            lock (_lock)
            {
                var windowStart = DateTimeOffset.UtcNow.AddSeconds(-1);

                while (_requests.Count > 0 && _requests.Peek() < windowStart)
                {
                    _requests.Dequeue();
                }

                return Math.Max(0, _limit + _burstSize - _requests.Count);
            }
        }

        public bool IsExpired(DateTimeOffset now)
        {
            lock (_lock)
            {
                return _requests.Count == 0 &&
                       now - _windowStart > TimeSpan.FromMinutes(5);
            }
        }
    }
}
