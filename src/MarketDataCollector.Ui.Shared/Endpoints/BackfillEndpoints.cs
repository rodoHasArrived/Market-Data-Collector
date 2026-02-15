using System.Text.Json;
using System.Text.RegularExpressions;
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
    // Symbols should be 1-20 uppercase alphanumeric chars, dots, or hyphens
    private static readonly Regex SymbolPattern = new(@"^[A-Za-z0-9.\-]{1,20}$", RegexOptions.Compiled);

    /// <summary>
    /// Maps all backfill API endpoints.
    /// </summary>
    public static void MapBackfillEndpoints(this WebApplication app, JsonSerializerOptions jsonOptions, JsonSerializerOptions jsonOptionsIndented)
    {
        var group = app.MapGroup("").WithTags("Backfill");

        // Get available providers
        group.MapGet(UiApiRoutes.BackfillProviders, (BackfillCoordinator backfill) =>
        {
            var providers = backfill.DescribeProviders();
            return Results.Json(providers, jsonOptions);
        })
        .WithName("GetBackfillProviders")
        .WithDescription("Returns list of available historical data providers for backfill operations.")
        .Produces<BackfillProviderInfo[]>(200);

        // Get last backfill status
        group.MapGet(UiApiRoutes.BackfillStatus, (BackfillCoordinator backfill) =>
        {
            var status = backfill.TryReadLast();
            return status is null
                ? Results.NotFound()
                : Results.Json(status, jsonOptionsIndented);
        })
        .WithName("GetBackfillStatus")
        .WithDescription("Returns the result of the most recent backfill operation, or 404 if none has been run.")
        .Produces<BackfillResult>(200)
        .Produces(404);

        // Preview backfill (dry run - shows what would be fetched)
        group.MapPost(UiApiRoutes.BackfillRun + "/preview", async (BackfillCoordinator backfill, BackfillRequestDto req) =>
        {
            var validation = ValidateBackfillRequest(req);
            if (validation is not null) return validation;

            try
            {
                var request = new BackfillRequest(
                    string.IsNullOrWhiteSpace(req.Provider) ? "stooq" : req.Provider!,
                    req.Symbols!,
                    req.From,
                    req.To);

                var preview = await backfill.PreviewAsync(request);
                return Results.Json(preview, jsonOptionsIndented);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (Exception)
            {
                return Results.BadRequest(new { error = "Backfill preview failed. Check provider name and symbol format." });
            }
        })
        .WithName("PreviewBackfill")
        .WithDescription("Dry-run preview of a backfill operation showing what data would be fetched.")
        .Produces<BackfillResult>(200)
        .Produces(400)
        .RequireRateLimiting(UiEndpoints.MutationRateLimitPolicy);

        // Run backfill
        group.MapPost(UiApiRoutes.BackfillRun, async (BackfillCoordinator backfill, BackfillRequestDto req) =>
        {
            var validation = ValidateBackfillRequest(req);
            if (validation is not null) return validation;

            try
            {
                var request = new BackfillRequest(
                    string.IsNullOrWhiteSpace(req.Provider) ? "stooq" : req.Provider!,
                    req.Symbols!,
                    req.From,
                    req.To);

                var result = await backfill.RunAsync(request);
                return Results.Json(result, jsonOptionsIndented);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (Exception)
            {
                return Results.BadRequest(new { error = "Backfill execution failed. Check provider name and symbol format." });
            }
        })
        .WithName("RunBackfill")
        .WithDescription("Executes a backfill operation for the specified symbols and date range.")
        .Produces<BackfillResult>(200)
        .Produces(400)
        .RequireRateLimiting(UiEndpoints.MutationRateLimitPolicy);

        // Backfill progress endpoint
        group.MapGet("/api/backfill/progress", (BackfillCoordinator backfill) =>
        {
            var progress = backfill.GetProgress();
            return progress is not null
                ? Results.Json(progress, jsonOptions)
                : Results.Json(new { message = "No active backfill operation", symbols = Array.Empty<object>() }, jsonOptions);
        })
        .WithName("GetBackfillProgress")
        .WithDescription("Returns progress of the currently active backfill operation, if any.")
        .Produces(200);
    }

    private static IResult? ValidateBackfillRequest(BackfillRequestDto req)
    {
        if (req.Symbols is null || req.Symbols.Length == 0)
            return Results.BadRequest(new { error = "At least one symbol is required." });

        if (req.Symbols.Length > 100)
            return Results.BadRequest(new { error = "Maximum 100 symbols per backfill request." });

        var invalidSymbols = req.Symbols.Where(s => !SymbolPattern.IsMatch(s)).ToArray();
        if (invalidSymbols.Length > 0)
            return Results.BadRequest(new { error = $"Invalid symbol format: {string.Join(", ", invalidSymbols.Take(5))}. Symbols must be 1-20 alphanumeric characters." });

        if (req.From.HasValue && req.To.HasValue && req.From.Value > req.To.Value)
            return Results.BadRequest(new { error = "From date must be before or equal to To date." });

        if (req.From.HasValue && req.From.Value < new DateOnly(1970, 1, 1))
            return Results.BadRequest(new { error = "From date must be after 1970-01-01." });

        if (req.To.HasValue && req.To.Value > DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)))
            return Results.BadRequest(new { error = "To date cannot be in the future." });

        return null;
    }
}
