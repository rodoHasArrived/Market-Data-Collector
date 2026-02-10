using System.Text.Json;
using MarketDataCollector.Contracts.Api;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace MarketDataCollector.Ui.Shared.Endpoints;

/// <summary>
/// Extension methods for registering time series alignment API endpoints.
/// </summary>
public static class AlignmentEndpoints
{
    public static void MapAlignmentEndpoints(this WebApplication app, JsonSerializerOptions jsonOptions)
    {
        var group = app.MapGroup("").WithTags("Alignment");

        // Create alignment
        group.MapPost(UiApiRoutes.AlignmentCreate, (AlignmentCreateRequest req) =>
        {
            var jobId = Guid.NewGuid().ToString("N")[..12];

            return Results.Json(new
            {
                jobId,
                symbols = req.Symbols ?? Array.Empty<string>(),
                interval = req.Interval ?? "1min",
                aggregationMethod = req.AggregationMethod ?? "last",
                gapStrategy = req.GapStrategy ?? "forward_fill",
                status = "queued",
                timestamp = DateTimeOffset.UtcNow
            }, jsonOptions);
        })
        .WithName("CreateAlignment")
        .Produces(200)
        .RequireRateLimiting(UiEndpoints.MutationRateLimitPolicy);

        // Preview alignment
        group.MapPost(UiApiRoutes.AlignmentPreview, (AlignmentCreateRequest req) =>
        {
            var symbols = req.Symbols ?? Array.Empty<string>();
            var interval = req.Interval ?? "1min";

            return Results.Json(new
            {
                symbols,
                interval,
                estimatedOutputRows = symbols.Length * 390, // ~390 minutes in trading day
                supportedIntervals = new[] { "1sec", "5sec", "30sec", "1min", "5min", "15min", "30min", "1hour", "1day" },
                supportedAggregations = new[] { "last", "first", "mean", "median", "vwap", "ohlc" },
                supportedGapStrategies = new[] { "forward_fill", "backward_fill", "interpolate", "null", "skip" },
                timestamp = DateTimeOffset.UtcNow
            }, jsonOptions);
        })
        .WithName("PreviewAlignment")
        .Produces(200)
        .RequireRateLimiting(UiEndpoints.MutationRateLimitPolicy);
    }

    private sealed record AlignmentCreateRequest(string[]? Symbols, string? Interval, string? AggregationMethod, string? GapStrategy, DateTime? StartDate, DateTime? EndDate);
}
