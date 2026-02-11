using System.Text.Json;
using MarketDataCollector.Application.Scheduling;
using MarketDataCollector.Core.Scheduling;
using MarketDataCollector.Infrastructure.Providers.Backfill;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace MarketDataCollector.Application.UI;

/// <summary>
/// HTTP API endpoints for scheduled backfill management.
/// </summary>
public static class ScheduledBackfillEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    /// <summary>
    /// Configure all scheduled backfill routes.
    /// </summary>
    public static void MapScheduledBackfillEndpoints(this WebApplication app)
    {
        // ==================== SCHEDULE MANAGEMENT ====================

        app.MapGet("/api/backfill/schedules", (BackfillScheduleManager scheduleManager) =>
        {
            try
            {
                var schedules = scheduleManager.GetAllSchedules();
                return Results.Json(schedules, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get schedules: {ex.Message}");
            }
        });

        app.MapGet("/api/backfill/schedules/{scheduleId}", (BackfillScheduleManager scheduleManager, string scheduleId) =>
        {
            try
            {
                var schedule = scheduleManager.GetSchedule(scheduleId);
                return schedule is null
                    ? Results.NotFound($"Schedule '{scheduleId}' not found")
                    : Results.Json(schedule, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get schedule: {ex.Message}");
            }
        });

        app.MapPost("/api/backfill/schedules", async (
            BackfillScheduleManager scheduleManager,
            CreateBackfillScheduleRequest req) =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(req.Name))
                    return Results.BadRequest("Schedule name is required");

                if (string.IsNullOrWhiteSpace(req.CronExpression) && string.IsNullOrWhiteSpace(req.Preset))
                    return Results.BadRequest("Either cronExpression or preset is required");

                BackfillSchedule schedule;

                if (!string.IsNullOrWhiteSpace(req.Preset))
                {
                    schedule = await scheduleManager.CreateFromPresetAsync(
                        req.Preset,
                        req.Name,
                        req.Symbols);
                }
                else
                {
                    schedule = new BackfillSchedule
                    {
                        Name = req.Name,
                        Description = req.Description ?? string.Empty,
                        CronExpression = req.CronExpression!,
                        TimeZoneId = req.TimeZoneId ?? "UTC",
                        BackfillType = Enum.TryParse<ScheduledBackfillType>(req.BackfillType, true, out var bt)
                            ? bt : ScheduledBackfillType.GapFill,
                        Symbols = req.Symbols?.ToList() ?? new List<string>(),
                        LookbackDays = req.LookbackDays ?? 30,
                        Granularity = Enum.TryParse<DataGranularity>(req.Granularity, true, out var g)
                            ? g : DataGranularity.Daily,
                        Priority = Enum.TryParse<BackfillPriority>(req.Priority, true, out var p)
                            ? p : BackfillPriority.Normal,
                        Enabled = req.Enabled ?? true,
                        Tags = req.Tags?.ToList() ?? new List<string>()
                    };

                    schedule = await scheduleManager.CreateScheduleAsync(schedule);
                }

                return Results.Json(schedule, JsonOptions);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to create schedule: {ex.Message}");
            }
        });

        app.MapPut("/api/backfill/schedules/{scheduleId}", async (
            BackfillScheduleManager scheduleManager,
            string scheduleId,
            UpdateBackfillScheduleRequest req) =>
        {
            try
            {
                var schedule = scheduleManager.GetSchedule(scheduleId);
                if (schedule is null)
                    return Results.NotFound($"Schedule '{scheduleId}' not found");

                if (!string.IsNullOrWhiteSpace(req.Name))
                    schedule.Name = req.Name;
                if (!string.IsNullOrWhiteSpace(req.Description))
                    schedule.Description = req.Description;
                if (!string.IsNullOrWhiteSpace(req.CronExpression))
                    schedule.CronExpression = req.CronExpression;
                if (!string.IsNullOrWhiteSpace(req.TimeZoneId))
                    schedule.TimeZoneId = req.TimeZoneId;
                if (req.BackfillType != null && Enum.TryParse<ScheduledBackfillType>(req.BackfillType, true, out var bt))
                    schedule.BackfillType = bt;
                if (req.Symbols != null)
                {
                    schedule.Symbols.Clear();
                    schedule.Symbols.AddRange(req.Symbols);
                }
                if (req.LookbackDays.HasValue)
                    schedule.LookbackDays = req.LookbackDays.Value;
                if (req.Granularity != null && Enum.TryParse<DataGranularity>(req.Granularity, true, out var g))
                    schedule.Granularity = g;
                if (req.Priority != null && Enum.TryParse<BackfillPriority>(req.Priority, true, out var p))
                    schedule.Priority = p;
                if (req.Enabled.HasValue)
                    schedule.Enabled = req.Enabled.Value;
                if (req.Tags != null)
                {
                    schedule.Tags.Clear();
                    schedule.Tags.AddRange(req.Tags);
                }

                schedule = await scheduleManager.UpdateScheduleAsync(schedule);
                return Results.Json(schedule, JsonOptions);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to update schedule: {ex.Message}");
            }
        });

        app.MapDelete("/api/backfill/schedules/{scheduleId}", async (
            BackfillScheduleManager scheduleManager,
            string scheduleId) =>
        {
            try
            {
                var deleted = await scheduleManager.DeleteScheduleAsync(scheduleId);
                return deleted
                    ? Results.Ok(new { message = $"Schedule '{scheduleId}' deleted" })
                    : Results.NotFound($"Schedule '{scheduleId}' not found");
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to delete schedule: {ex.Message}");
            }
        });

        // ==================== SCHEDULE CONTROL ====================

        app.MapPost("/api/backfill/schedules/{scheduleId}/enable", async (
            BackfillScheduleManager scheduleManager,
            string scheduleId) =>
        {
            try
            {
                var success = await scheduleManager.SetScheduleEnabledAsync(scheduleId, true);
                return success
                    ? Results.Ok(new { message = $"Schedule '{scheduleId}' enabled" })
                    : Results.NotFound($"Schedule '{scheduleId}' not found");
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to enable schedule: {ex.Message}");
            }
        });

        app.MapPost("/api/backfill/schedules/{scheduleId}/disable", async (
            BackfillScheduleManager scheduleManager,
            string scheduleId) =>
        {
            try
            {
                var success = await scheduleManager.SetScheduleEnabledAsync(scheduleId, false);
                return success
                    ? Results.Ok(new { message = $"Schedule '{scheduleId}' disabled" })
                    : Results.NotFound($"Schedule '{scheduleId}' not found");
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to disable schedule: {ex.Message}");
            }
        });

        app.MapPost("/api/backfill/schedules/{scheduleId}/trigger", async (
            ScheduledBackfillService scheduledService,
            string scheduleId) =>
        {
            try
            {
                var execution = await scheduledService.TriggerManualExecutionAsync(scheduleId);
                return Results.Json(execution, JsonOptions);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound($"Schedule '{scheduleId}' not found");
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to trigger schedule: {ex.Message}");
            }
        });

        // ==================== EXECUTION HISTORY ====================

        app.MapGet("/api/backfill/executions", (BackfillScheduleManager scheduleManager, int? limit) =>
        {
            try
            {
                var executions = scheduleManager.ExecutionHistory.GetRecentExecutions(limit ?? 50);
                return Results.Json(executions, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get executions: {ex.Message}");
            }
        });

        app.MapGet("/api/backfill/executions/{executionId}", (
            BackfillScheduleManager scheduleManager,
            string executionId) =>
        {
            try
            {
                var execution = scheduleManager.ExecutionHistory.GetExecution(executionId);
                return execution is null
                    ? Results.NotFound($"Execution '{executionId}' not found")
                    : Results.Json(execution, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get execution: {ex.Message}");
            }
        });

        app.MapGet("/api/backfill/schedules/{scheduleId}/executions", (
            BackfillScheduleManager scheduleManager,
            string scheduleId,
            int? limit) =>
        {
            try
            {
                var executions = scheduleManager.ExecutionHistory.GetExecutionsForSchedule(scheduleId, limit ?? 50);
                return Results.Json(executions, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get schedule executions: {ex.Message}");
            }
        });

        app.MapGet("/api/backfill/executions/failed", (BackfillScheduleManager scheduleManager, int? limit) =>
        {
            try
            {
                var executions = scheduleManager.ExecutionHistory.GetFailedExecutions(limit ?? 50);
                return Results.Json(executions, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get failed executions: {ex.Message}");
            }
        });

        // ==================== STATISTICS & SUMMARIES ====================

        app.MapGet("/api/backfill/schedules/summary", (BackfillScheduleManager scheduleManager) =>
        {
            try
            {
                var summary = scheduleManager.GetStatusSummary();
                return Results.Json(summary, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get schedule summary: {ex.Message}");
            }
        });

        app.MapGet("/api/backfill/schedules/{scheduleId}/summary", (
            BackfillScheduleManager scheduleManager,
            string scheduleId,
            int? recentCount) =>
        {
            try
            {
                var summary = scheduleManager.ExecutionHistory.GetScheduleSummary(scheduleId, recentCount ?? 30);
                return Results.Json(summary, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get schedule summary: {ex.Message}");
            }
        });

        app.MapGet("/api/backfill/statistics", (BackfillScheduleManager scheduleManager, int? hours) =>
        {
            try
            {
                var period = hours.HasValue ? TimeSpan.FromHours(hours.Value) : (TimeSpan?)null;
                var stats = scheduleManager.ExecutionHistory.GetSystemSummary(period);
                return Results.Json(stats, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get statistics: {ex.Message}");
            }
        });

        // ==================== SERVICE STATUS ====================

        app.MapGet("/api/backfill/scheduler/status", (ScheduledBackfillService scheduledService) =>
        {
            try
            {
                return Results.Json(new
                {
                    isRunning = scheduledService.IsRunning,
                    queuedExecutions = scheduledService.QueuedExecutions
                }, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get scheduler status: {ex.Message}");
            }
        });

        // ==================== IMMEDIATE GAP FILL ====================

        app.MapPost("/api/backfill/gap-fill", async (
            ScheduledBackfillService scheduledService,
            ImmediateGapFillRequest req) =>
        {
            try
            {
                if (req.Symbols == null || req.Symbols.Length == 0)
                    return Results.BadRequest("At least one symbol is required");

                var priority = Enum.TryParse<BackfillPriority>(req.Priority, true, out var p)
                    ? p : BackfillPriority.High;

                var execution = await scheduledService.RunImmediateGapFillAsync(
                    req.Symbols,
                    req.LookbackDays ?? 30,
                    priority);

                return Results.Json(execution, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Gap fill failed: {ex.Message}");
            }
        });

        // ==================== CRON VALIDATION ====================

        app.MapPost("/api/backfill/validate-cron", (ValidateCronRequest req) =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(req.CronExpression))
                    return Results.BadRequest("Cron expression is required");

                var isValid = CronExpressionParser.IsValid(req.CronExpression);
                var description = isValid
                    ? CronExpressionParser.GetDescription(req.CronExpression)
                    : "Invalid cron expression";

                DateTimeOffset? nextExecution = null;
                if (isValid)
                {
                    var tz = string.IsNullOrWhiteSpace(req.TimeZoneId)
                        ? TimeZoneInfo.Utc
                        : TimeZoneInfo.FindSystemTimeZoneById(req.TimeZoneId);
                    nextExecution = CronExpressionParser.GetNextOccurrence(
                        req.CronExpression, tz, DateTimeOffset.UtcNow);
                }

                return Results.Json(new
                {
                    isValid,
                    description,
                    nextExecution
                }, JsonOptions);
            }
            catch (TimeZoneNotFoundException)
            {
                return Results.BadRequest($"Invalid timezone: {req.TimeZoneId}");
            }
            catch (Exception ex)
            {
                return Results.Problem($"Validation failed: {ex.Message}");
            }
        });

        // ==================== PRESETS ====================

        app.MapGet("/api/backfill/presets", () =>
        {
            try
            {
                var presets = new[]
                {
                    new
                    {
                        name = "daily",
                        displayName = "Daily Gap-Fill",
                        description = "Run daily at 2 AM UTC to fill gaps from the past 7 days",
                        cronExpression = "0 2 * * *",
                        lookbackDays = 7
                    },
                    new
                    {
                        name = "weekly",
                        displayName = "Weekly Full Backfill",
                        description = "Run every Sunday at 3 AM UTC for 30-day backfill",
                        cronExpression = "0 3 * * 0",
                        lookbackDays = 30
                    },
                    new
                    {
                        name = "eod",
                        displayName = "End-of-Day Update",
                        description = "Run weekdays at 11 PM UTC (after US market close)",
                        cronExpression = "0 23 * * 1-5",
                        lookbackDays = 1
                    },
                    new
                    {
                        name = "monthly",
                        displayName = "Monthly Deep Backfill",
                        description = "Run on first Sunday of month at 1 AM UTC for 1-year backfill",
                        cronExpression = "0 1 1-7 * 0",
                        lookbackDays = 365
                    }
                };

                return Results.Json(presets, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get presets: {ex.Message}");
            }
        });
    }
}

// ==================== REQUEST DTOs ====================

public record CreateBackfillScheduleRequest(
    string Name,
    string? Description = null,
    string? Preset = null,
    string? CronExpression = null,
    string? TimeZoneId = null,
    string? BackfillType = null,
    string[]? Symbols = null,
    int? LookbackDays = null,
    string? Granularity = null,
    string? Priority = null,
    bool? Enabled = null,
    string[]? Tags = null
);

public record UpdateBackfillScheduleRequest(
    string? Name = null,
    string? Description = null,
    string? CronExpression = null,
    string? TimeZoneId = null,
    string? BackfillType = null,
    string[]? Symbols = null,
    int? LookbackDays = null,
    string? Granularity = null,
    string? Priority = null,
    bool? Enabled = null,
    string[]? Tags = null
);

public record ImmediateGapFillRequest(
    string[] Symbols,
    int? LookbackDays = null,
    string? Priority = null
);

public record ValidateCronRequest(
    string CronExpression,
    string? TimeZoneId = null
);
