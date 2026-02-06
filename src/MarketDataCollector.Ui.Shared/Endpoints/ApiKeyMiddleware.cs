using System.Collections.Concurrent;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace MarketDataCollector.Ui.Shared.Endpoints;

/// <summary>
/// Middleware that enforces API key authentication on /api/* endpoints.
/// The API key is read from the MDC_API_KEY environment variable and supports
/// key rotation (re-reads the variable on each request).
/// When no key is configured, all requests are allowed (backward-compatible).
/// Health check endpoints (/healthz, /readyz, /livez) are always exempt.
/// </summary>
public sealed class ApiKeyMiddleware
{
    private const string ApiKeyHeaderName = "X-Api-Key";
    private const string ApiKeyQueryParam = "api_key";
    private const string ApiKeyEnvVar = "MDC_API_KEY";

    private static readonly HashSet<string> ExemptPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/healthz",
        "/readyz",
        "/livez"
    };

    private readonly RequestDelegate _next;

    public ApiKeyMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Re-read on each request to support key rotation without restart
        var expectedApiKey = Environment.GetEnvironmentVariable(ApiKeyEnvVar);

        // If no API key is configured, allow all requests (backward-compatible)
        if (string.IsNullOrWhiteSpace(expectedApiKey))
        {
            await _next(context);
            return;
        }

        var path = context.Request.Path.Value ?? "";

        // Health check endpoints are always exempt from authentication
        if (ExemptPaths.Contains(path.TrimEnd('/')))
        {
            await _next(context);
            return;
        }

        // Only enforce on API paths
        if (!path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        // Check for API key in header or query string
        var providedKey = context.Request.Headers[ApiKeyHeaderName].FirstOrDefault()
            ?? context.Request.Query[ApiKeyQueryParam].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(providedKey) ||
            !CryptographicEquals(providedKey, expectedApiKey))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                """{"error":"Unauthorized. Provide a valid API key via X-Api-Key header or api_key query parameter."}""");
            return;
        }

        // Store the validated API key identifier for downstream rate limiting
        context.Items["ApiKey"] = providedKey;

        await _next(context);
    }

    /// <summary>
    /// Constant-time string comparison to prevent timing attacks on API key validation.
    /// </summary>
    private static bool CryptographicEquals(string a, string b)
    {
        if (a.Length != b.Length)
            return false;

        var result = 0;
        for (var i = 0; i < a.Length; i++)
        {
            result |= a[i] ^ b[i];
        }
        return result == 0;
    }
}

/// <summary>
/// Per-API-key rate limiting middleware.
/// Tracks request counts per API key using a sliding window and returns 429 when exceeded.
/// </summary>
public sealed class ApiKeyRateLimitMiddleware
{
    private const int MaxRequestsPerMinute = 120;
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    private readonly RequestDelegate _next;
    private readonly ConcurrentDictionary<string, RateLimitEntry> _clients = new();

    public ApiKeyRateLimitMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";

        // Only apply to API paths
        if (!path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        // Partition by API key if present, otherwise by IP
        var partitionKey = context.Items.TryGetValue("ApiKey", out var apiKey) && apiKey is string key
            ? $"key:{key}"
            : $"ip:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";

        var entry = _clients.GetOrAdd(partitionKey, _ => new RateLimitEntry());

        var now = DateTime.UtcNow;
        int remaining;

        lock (entry)
        {
            // Reset window if expired
            if (now - entry.WindowStart >= Window)
            {
                entry.WindowStart = now;
                entry.RequestCount = 0;
            }

            entry.RequestCount++;
            remaining = Math.Max(0, MaxRequestsPerMinute - entry.RequestCount);

            if (entry.RequestCount > MaxRequestsPerMinute)
            {
                var retryAfter = (int)(Window - (now - entry.WindowStart)).TotalSeconds + 1;

                context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.Response.ContentType = "application/json";
                context.Response.Headers["Retry-After"] = retryAfter.ToString();
                context.Response.Headers["X-RateLimit-Limit"] = MaxRequestsPerMinute.ToString();
                context.Response.Headers["X-RateLimit-Remaining"] = "0";

                // Fire-and-forget write (response already has status code)
                _ = context.Response.WriteAsync(
                    $$$"""{"error":"Rate limit exceeded. Maximum {{{MaxRequestsPerMinute}}} requests per minute.","retry_after":{{{retryAfter}}}}""");
                return;
            }
        }

        // Add rate limit headers to successful responses
        context.Response.OnStarting(() =>
        {
            context.Response.Headers["X-RateLimit-Limit"] = MaxRequestsPerMinute.ToString();
            context.Response.Headers["X-RateLimit-Remaining"] = remaining.ToString();
            return Task.CompletedTask;
        });

        await _next(context);
    }

    private sealed class RateLimitEntry
    {
        public DateTime WindowStart = DateTime.UtcNow;
        public int RequestCount;
    }
}

/// <summary>
/// Extension methods for registering the API key middleware.
/// </summary>
public static class ApiKeyMiddlewareExtensions
{
    /// <summary>
    /// Adds API key authentication middleware for /api/* endpoints.
    /// The key is read from the MDC_API_KEY environment variable.
    /// When no key is set, all requests pass through (backward-compatible).
    /// Health check endpoints (/healthz, /readyz, /livez) are always exempt.
    /// </summary>
    public static IApplicationBuilder UseApiKeyAuthentication(this IApplicationBuilder app)
    {
        app.UseMiddleware<ApiKeyMiddleware>();
        app.UseMiddleware<ApiKeyRateLimitMiddleware>();
        return app;
    }
}
