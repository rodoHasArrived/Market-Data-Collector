using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace MarketDataCollector.Ui.Shared.Endpoints;

/// <summary>
/// Middleware that enforces API key authentication on mutating (POST/PUT/DELETE) API endpoints.
/// The API key is read from the MDC_API_KEY environment variable.
/// When no key is configured, all requests are allowed (backward-compatible).
/// </summary>
public sealed class ApiKeyMiddleware
{
    private const string ApiKeyHeaderName = "X-Api-Key";
    private const string ApiKeyQueryParam = "api_key";
    private const string ApiKeyEnvVar = "MDC_API_KEY";

    private readonly RequestDelegate _next;
    private readonly string? _expectedApiKey;

    public ApiKeyMiddleware(RequestDelegate next)
    {
        _next = next;
        _expectedApiKey = Environment.GetEnvironmentVariable(ApiKeyEnvVar);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // If no API key is configured, allow all requests (backward-compatible)
        if (string.IsNullOrWhiteSpace(_expectedApiKey))
        {
            await _next(context);
            return;
        }

        var path = context.Request.Path.Value ?? "";
        var method = context.Request.Method;

        // Only enforce on mutating API requests
        var isMutating = method is "POST" or "PUT" or "DELETE" or "PATCH";
        var isApiPath = path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase);

        if (!isMutating || !isApiPath)
        {
            await _next(context);
            return;
        }

        // Check for API key in header or query string
        var providedKey = context.Request.Headers[ApiKeyHeaderName].FirstOrDefault()
            ?? context.Request.Query[ApiKeyQueryParam].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(providedKey) || !string.Equals(providedKey, _expectedApiKey, StringComparison.Ordinal))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("""{"error":"Unauthorized. Provide a valid API key via X-Api-Key header or api_key query parameter."}""");
            return;
        }

        await _next(context);
    }
}

/// <summary>
/// Extension methods for registering the API key middleware.
/// </summary>
public static class ApiKeyMiddlewareExtensions
{
    /// <summary>
    /// Adds API key authentication middleware for mutating /api/* endpoints.
    /// The key is read from the MDC_API_KEY environment variable.
    /// When no key is set, all requests pass through (backward-compatible).
    /// </summary>
    public static IApplicationBuilder UseApiKeyAuthentication(this IApplicationBuilder app)
    {
        return app.UseMiddleware<ApiKeyMiddleware>();
    }
}
