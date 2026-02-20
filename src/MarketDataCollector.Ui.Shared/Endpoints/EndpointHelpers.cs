using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace MarketDataCollector.Ui.Shared.Endpoints;

/// <summary>
/// Shared helpers to reduce boilerplate in endpoint handlers.
/// Provides consistent null-check, try/catch, and JSON response patterns.
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
            return Results.Problem($"Failed: {ex.Message}");
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
            return Results.Problem($"Failed: {ex.Message}");
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
            return Results.Problem($"Failed: {ex.Message}");
        }
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
