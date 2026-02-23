using System.Text.Json;
using MarketDataCollector.Application.Services;
using Microsoft.AspNetCore.Http;

namespace MarketDataCollector.Ui.Shared.Endpoints;

/// <summary>
/// Shared helpers to reduce boilerplate in endpoint handlers.
/// Provides consistent null-check, try/catch, and JSON response patterns.
/// Uses FriendlyErrorFormatter for user-friendly error responses.
/// </summary>
internal static class EndpointHelpers
{
    /// <summary>
    /// Handles a synchronous endpoint handler with service null-check and error handling.
    /// </summary>
    internal static IResult HandleSync<TService>(
        TService? service,
        Func<TService, object> handler,
        JsonSerializerOptions opts) where TService : class
    {
        if (service is null)
            return Results.Json(new { error = "Service unavailable" }, opts);

        try
        {
            return Results.Json(handler(service), opts);
        }
        catch (Exception ex)
        {
            return FormatErrorResult(ex, opts);
        }
    }

    /// <summary>
    /// Handles an async endpoint handler with service null-check and error handling.
    /// </summary>
    internal static async Task<IResult> HandleAsync<TService>(
        TService? service,
        Func<TService, Task<object>> handler,
        JsonSerializerOptions opts) where TService : class
    {
        if (service is null)
            return Results.Json(new { error = "Service unavailable" }, opts);

        try
        {
            var result = await handler(service);
            return Results.Json(result, opts);
        }
        catch (Exception ex)
        {
            return FormatErrorResult(ex, opts);
        }
    }

    /// <summary>
    /// Handles an async endpoint with a cancellation token.
    /// </summary>
    internal static async Task<IResult> HandleAsync<TService>(
        TService? service,
        Func<TService, CancellationToken, Task<object>> handler,
        JsonSerializerOptions opts,
        CancellationToken ct) where TService : class
    {
        if (service is null)
            return Results.Json(new { error = "Service unavailable" }, opts);

        try
        {
            var result = await handler(service, ct);
            return Results.Json(result, opts);
        }
        catch (Exception ex)
        {
            return FormatErrorResult(ex, opts);
        }
    }

    /// <summary>
    /// Converts an exception into a structured, user-friendly JSON error response
    /// using FriendlyErrorFormatter for consistent error classification.
    /// </summary>
    private static IResult FormatErrorResult(Exception ex, JsonSerializerOptions opts)
    {
        var formatted = FriendlyErrorFormatter.Format(ex);

        var statusCode = formatted.Code switch
        {
            var c when c.StartsWith("MDC-AUTH") => 401,
            var c when c.StartsWith("MDC-RATE") => 429,
            var c when c.StartsWith("MDC-DATA-001") => 404,
            _ => 500
        };

        return Results.Json(new
        {
            error = formatted.Title,
            code = formatted.Code,
            message = formatted.Message,
            suggestion = formatted.Suggestion,
            docs = formatted.DocsLink,
            details = formatted.Details
        }, opts, statusCode: statusCode);
    }

    /// <summary>
    /// Parses a date string or returns today's date.
    /// </summary>
    internal static DateOnly ParseDateOrToday(string? dateStr)
    {
        if (string.IsNullOrWhiteSpace(dateStr))
            return DateOnly.FromDateTime(DateTime.UtcNow);

        return DateOnly.TryParse(dateStr, out var date) ? date : DateOnly.FromDateTime(DateTime.UtcNow);
    }
}
