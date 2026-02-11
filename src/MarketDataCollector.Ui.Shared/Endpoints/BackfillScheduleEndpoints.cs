using System.Text.Json;
using MarketDataCollector.Application.Scheduling;
using MarketDataCollector.Contracts.Api;
using MarketDataCollector.Infrastructure.Providers.Core;
using MarketDataCollector.Ui.Shared.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace MarketDataCollector.Ui.Shared.Endpoints;

/// <summary>
/// Extension methods for registering backfill schedule, execution, and utility API endpoints.
/// </summary>
public static class BackfillScheduleEndpoints
{
    public static void MapBackfillScheduleEndpoints(this WebApplication app, JsonSerializerOptions jsonOptions)
    {
        var group = app.MapGroup("").WithTags("Backfill");

        // Backfill health
        group.MapGet(UiApiRoutes.BackfillHealth, ([FromServices] BackfillScheduleManager? schedMgr, [FromServices] BackfillCoordinator backfill) =>
        {
            var summary = schedMgr?.GetStatusSummary();
            return Results.Json(new
            {
                healthy = true,
                schedules = new
                {
                    total = summary?.TotalSchedules ?? 0,
                    enabled = summary?.EnabledSchedules ?? 0,
                    dueNow = summary?.SchedulesDueNow ?? 0,
                    nextExecution = summary?.NextScheduledExecution
                },
                successRate = summary?.OverallSuccessRate ?? 0,
                timestamp = DateTimeOffset.UtcNow
            }, jsonOptions);
        })
        .WithName("GetBackfillHealth")
        .Produces(200);

        // Resolve symbol for backfill
        group.MapGet(UiApiRoutes.BackfillResolve, (string symbol, [FromServices] ProviderRegistry? registry) =>
        {
            var backfillProviders = registry?.GetBackfillProviders()
                .Select(p => new { name = p.Name, displayName = p.DisplayName, priority = p.Priority })
                .ToArray() ?? Array.Empty<object>();

            return Results.Json(new
            {
                symbol,
                availableProviders = backfillProviders,
                timestamp = DateTimeOffset.UtcNow
            }, jsonOptions);
        })
        .WithName("ResolveBackfillSymbol")
        .Produces(200);

        // Gap fill
        group.MapPost(UiApiRoutes.BackfillGapFill, async (BackfillCoordinator backfill, GapFillRequest req) =>
        {
            if (req.Symbols is null || req.Symbols.Length == 0)
                return Results.BadRequest(new { error = "At least one symbol is required." });

            try
            {
                var request = new Application.Backfill.BackfillRequest(
                    req.Provider ?? "stooq",
                    req.Symbols,
                    req.From,
                    req.To);

                var result = await backfill.RunAsync(request);
                return Results.Json(result, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("RunBackfillGapFill")
        .Produces(200)
        .Produces(400)
        .RequireRateLimiting(UiEndpoints.MutationRateLimitPolicy);

        // Backfill presets
        group.MapGet(UiApiRoutes.BackfillPresets, () =>
        {
            var presets = new[]
            {
                new { name = "daily-eod", description = "End-of-day bars for US equities", symbols = new[] { "SPY", "QQQ", "IWM" }, provider = "stooq", cronExpression = "0 18 * * 1-5" },
                new { name = "weekly-full", description = "Weekly full backfill for watchlist", symbols = Array.Empty<string>(), provider = "alpaca", cronExpression = "0 6 * * 6" },
                new { name = "gap-fill", description = "Automatic gap detection and repair", symbols = Array.Empty<string>(), provider = "auto", cronExpression = "0 2 * * *" }
            };
            return Results.Json(new { presets, timestamp = DateTimeOffset.UtcNow }, jsonOptions);
        })
        .WithName("GetBackfillPresets")
        .Produces(200);

        // Backfill executions
        group.MapGet(UiApiRoutes.BackfillExecutions, (int? limit, [FromServices] BackfillExecutionHistory? history) =>
        {
            if (history is null)
                return Results.Json(new { executions = Array.Empty<object>(), total = 0 }, jsonOptions);

            var executions = history.GetRecentExecutions(limit ?? 50);
            return Results.Json(new
            {
                executions,
                total = executions.Count,
                timestamp = DateTimeOffset.UtcNow
            }, jsonOptions);
        })
        .WithName("GetBackfillExecutions")
        .Produces(200);

        // Backfill statistics
        group.MapGet(UiApiRoutes.BackfillStatistics, ([FromServices] BackfillExecutionHistory? history, [FromServices] BackfillScheduleManager? schedMgr) =>
        {
            var systemSummary = history?.GetSystemSummary(TimeSpan.FromDays(30));
            var statusSummary = schedMgr?.GetStatusSummary();

            return Results.Json(new
            {
                schedules = statusSummary,
                executions = systemSummary,
                timestamp = DateTimeOffset.UtcNow
            }, jsonOptions);
        })
        .WithName("GetBackfillStatistics")
        .Produces(200);

        // List backfill schedules
        group.MapGet(UiApiRoutes.BackfillSchedules, ([FromServices] BackfillScheduleManager? schedMgr) =>
        {
            var schedules = schedMgr?.GetAllSchedules() ?? Array.Empty<BackfillSchedule>();
            return Results.Json(new
            {
                schedules,
                total = schedules.Count,
                timestamp = DateTimeOffset.UtcNow
            }, jsonOptions);
        })
        .WithName("GetBackfillSchedules")
        .Produces(200);

        // Create backfill schedule
        group.MapPost(UiApiRoutes.BackfillSchedules, async ([FromServices] BackfillScheduleManager? schedMgr, BackfillSchedule schedule) =>
        {
            if (schedMgr is null)
                return Results.Json(new { error = "Schedule manager not available" }, jsonOptions, statusCode: 503);

            var created = await schedMgr.CreateScheduleAsync(schedule);
            return Results.Json(created, jsonOptions);
        })
        .WithName("CreateBackfillSchedule")
        .Produces(200)
        .Produces(503)
        .RequireRateLimiting(UiEndpoints.MutationRateLimitPolicy);

        // Get backfill schedule by ID
        group.MapGet(UiApiRoutes.BackfillSchedulesById, (string id, [FromServices] BackfillScheduleManager? schedMgr) =>
        {
            var schedule = schedMgr?.GetSchedule(id);
            return schedule is null ? Results.NotFound() : Results.Json(schedule, jsonOptions);
        })
        .WithName("GetBackfillScheduleById")
        .Produces(200)
        .Produces(404);

        // Delete backfill schedule
        group.MapDelete(UiApiRoutes.BackfillSchedulesDelete, async (string id, [FromServices] BackfillScheduleManager? schedMgr) =>
        {
            if (schedMgr is null)
                return Results.Json(new { error = "Schedule manager not available" }, jsonOptions, statusCode: 503);

            var deleted = await schedMgr.DeleteScheduleAsync(id);
            return deleted ? Results.Ok() : Results.NotFound();
        })
        .WithName("DeleteBackfillSchedule")
        .Produces(200)
        .Produces(404)
        .RequireRateLimiting(UiEndpoints.MutationRateLimitPolicy);

        // Enable backfill schedule
        group.MapPost(UiApiRoutes.BackfillSchedulesEnable, async (string id, [FromServices] BackfillScheduleManager? schedMgr) =>
        {
            if (schedMgr is null)
                return Results.Json(new { error = "Schedule manager not available" }, jsonOptions, statusCode: 503);

            var ok = await schedMgr.SetScheduleEnabledAsync(id, true);
            return ok ? Results.Ok() : Results.NotFound();
        })
        .WithName("EnableBackfillSchedule")
        .Produces(200)
        .Produces(404)
        .RequireRateLimiting(UiEndpoints.MutationRateLimitPolicy);

        // Disable backfill schedule
        group.MapPost(UiApiRoutes.BackfillSchedulesDisable, async (string id, [FromServices] BackfillScheduleManager? schedMgr) =>
        {
            if (schedMgr is null)
                return Results.Json(new { error = "Schedule manager not available" }, jsonOptions, statusCode: 503);

            var ok = await schedMgr.SetScheduleEnabledAsync(id, false);
            return ok ? Results.Ok() : Results.NotFound();
        })
        .WithName("DisableBackfillSchedule")
        .Produces(200)
        .Produces(404)
        .RequireRateLimiting(UiEndpoints.MutationRateLimitPolicy);

        // Run backfill schedule now
        group.MapPost(UiApiRoutes.BackfillSchedulesRun, (string id, [FromServices] BackfillScheduleManager? schedMgr) =>
        {
            if (schedMgr is null)
                return Results.Json(new { error = "Schedule manager not available" }, jsonOptions, statusCode: 503);

            var schedule = schedMgr.GetSchedule(id);
            if (schedule is null)
                return Results.NotFound();

            var execution = schedMgr.CreateManualExecution(schedule);
            return Results.Json(new
            {
                executionId = execution.ExecutionId,
                scheduleId = id,
                status = execution.Status.ToString(),
                timestamp = DateTimeOffset.UtcNow
            }, jsonOptions);
        })
        .WithName("RunBackfillScheduleNow")
        .Produces(200)
        .Produces(404)
        .RequireRateLimiting(UiEndpoints.MutationRateLimitPolicy);

        // Backfill schedule history
        group.MapGet(UiApiRoutes.BackfillSchedulesHistory, (string id, int? limit, [FromServices] BackfillExecutionHistory? history) =>
        {
            if (history is null)
                return Results.Json(new { executions = Array.Empty<object>() }, jsonOptions);

            var executions = history.GetExecutionsForSchedule(id, limit ?? 50);
            return Results.Json(new { executions, total = executions.Count }, jsonOptions);
        })
        .WithName("GetBackfillScheduleHistory")
        .Produces(200);

        // Backfill schedule templates
        group.MapGet(UiApiRoutes.BackfillSchedulesTemplates, () =>
        {
            var templates = new[]
            {
                new { id = "eod-equities", name = "End-of-Day Equities", cronExpression = "0 18 * * 1-5", backfillType = "EndOfDay", description = "Daily EOD bar backfill after US market close" },
                new { id = "gap-fill-daily", name = "Daily Gap Fill", cronExpression = "0 2 * * *", backfillType = "GapFill", description = "Nightly gap detection and repair" },
                new { id = "weekly-full", name = "Weekly Full Backfill", cronExpression = "0 6 * * 6", backfillType = "FullBackfill", description = "Full backfill on Saturday mornings" },
                new { id = "rolling-30d", name = "Rolling 30-Day Window", cronExpression = "0 3 * * 1-5", backfillType = "RollingWindow", description = "Maintain rolling 30-day data window" }
            };
            return Results.Json(new { templates }, jsonOptions);
        })
        .WithName("GetBackfillScheduleTemplates")
        .Produces(200);
    }

    private sealed record GapFillRequest(string[]? Symbols, string? Provider, DateOnly? From, DateOnly? To);
}
