using System.Text.Json;
using MarketDataCollector.Application.Backfill;
using MarketDataCollector.Contracts.Api;
using MarketDataCollector.Ui.Shared.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using BackfillRequest = MarketDataCollector.Application.Backfill.BackfillRequest;

namespace MarketDataCollector.Ui.Shared.Endpoints;

/// <summary>
/// Extension methods for registering backfill-related API endpoints.
/// Shared between web dashboard and desktop application hosts.
/// </summary>
public static class BackfillEndpoints
{
    /// <summary>
    /// Maps all backfill API endpoints.
    /// </summary>
    public static void MapBackfillEndpoints(this WebApplication app, JsonSerializerOptions jsonOptions, JsonSerializerOptions jsonOptionsIndented)
    {
        // Get available providers
        app.MapGet(UiApiRoutes.BackfillProviders, (BackfillCoordinator backfill) =>
        {
            var providers = backfill.DescribeProviders();
            return Results.Json(providers, jsonOptions);
        });

        // Get last backfill status
        app.MapGet(UiApiRoutes.BackfillStatus, (BackfillCoordinator backfill) =>
        {
            var status = backfill.TryReadLast();
            return status is null
                ? Results.NotFound()
                : Results.Json(status, jsonOptionsIndented);
        });

        // Preview backfill (dry run - shows what would be fetched)
        app.MapPost(UiApiRoutes.BackfillRun + "/preview", async (BackfillCoordinator backfill, BackfillRequestDto req) =>
        {
            if (req.Symbols is null || req.Symbols.Length == 0)
                return Results.BadRequest("At least one symbol is required.");

            try
            {
                var request = new BackfillRequest(
                    string.IsNullOrWhiteSpace(req.Provider) ? "stooq" : req.Provider!,
                    req.Symbols,
                    req.From,
                    req.To);

                var preview = await backfill.PreviewAsync(request);
                return Results.Json(preview, jsonOptionsIndented);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(ex.Message);
            }
        });

        // Run backfill
        app.MapPost(UiApiRoutes.BackfillRun, async (BackfillCoordinator backfill, BackfillRequestDto req) =>
        {
            if (req.Symbols is null || req.Symbols.Length == 0)
                return Results.BadRequest("At least one symbol is required.");

            try
            {
                var request = new BackfillRequest(
                    string.IsNullOrWhiteSpace(req.Provider) ? "stooq" : req.Provider!,
                    req.Symbols,
                    req.From,
                    req.To);

                var result = await backfill.RunAsync(request);
                return Results.Json(result, jsonOptionsIndented);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(ex.Message);
            }
        });
    }
}
