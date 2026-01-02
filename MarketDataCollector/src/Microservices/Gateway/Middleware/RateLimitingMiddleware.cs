using DataIngestion.Gateway.Services;

namespace DataIngestion.Gateway.Middleware;

/// <summary>
/// Middleware for rate limiting API requests.
/// </summary>
public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;

    public RateLimitingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        IRateLimitService rateLimitService,
        MetricsCollector metrics)
    {
        var clientId = GetClientId(context);
        var endpoint = context.Request.Path.Value ?? "/";

        // Skip rate limiting for health endpoints
        if (endpoint.StartsWith("/health") || endpoint.StartsWith("/live") ||
            endpoint.StartsWith("/ready") || endpoint.StartsWith("/metrics"))
        {
            await _next(context);
            return;
        }

        if (!rateLimitService.IsAllowed(clientId, endpoint))
        {
            metrics.RecordRateLimited(clientId, endpoint);

            var rateLimitInfo = rateLimitService.GetRateLimitInfo(clientId, endpoint);

            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.Headers["X-RateLimit-Limit"] = rateLimitInfo.Limit.ToString();
            context.Response.Headers["X-RateLimit-Remaining"] = "0";
            context.Response.Headers["X-RateLimit-Reset"] =
                rateLimitInfo.ResetAt.ToUnixTimeSeconds().ToString();
            context.Response.Headers["Retry-After"] = "1";

            await context.Response.WriteAsJsonAsync(new
            {
                error = "Rate limit exceeded",
                message = "Too many requests. Please slow down.",
                retryAfter = 1
            });
            return;
        }

        // Add rate limit headers
        var info = rateLimitService.GetRateLimitInfo(clientId, endpoint);
        context.Response.Headers["X-RateLimit-Limit"] = info.Limit.ToString();
        context.Response.Headers["X-RateLimit-Remaining"] = info.Remaining.ToString();
        context.Response.Headers["X-RateLimit-Reset"] = info.ResetAt.ToUnixTimeSeconds().ToString();

        await _next(context);
    }

    private static string GetClientId(HttpContext context)
    {
        // Try to get client ID from header
        if (context.Request.Headers.TryGetValue("X-ClientId", out var clientId) &&
            !string.IsNullOrEmpty(clientId))
        {
            return clientId.ToString();
        }

        // Try API key
        if (context.Request.Headers.TryGetValue("X-Api-Key", out var apiKey) &&
            !string.IsNullOrEmpty(apiKey))
        {
            return $"apikey:{apiKey.ToString()[..Math.Min(8, apiKey.ToString().Length)]}";
        }

        // Fall back to IP address
        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
