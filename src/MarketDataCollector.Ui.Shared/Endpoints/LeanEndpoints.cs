using System.Text.Json;
using MarketDataCollector.Contracts.Api;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace MarketDataCollector.Ui.Shared.Endpoints;

/// <summary>
/// Extension methods for registering QuantConnect Lean integration API endpoints.
/// </summary>
public static class LeanEndpoints
{
    private static readonly Dictionary<string, BacktestInfo> s_backtests = new(StringComparer.OrdinalIgnoreCase);

    public static void MapLeanEndpoints(this WebApplication app, JsonSerializerOptions jsonOptions)
    {
        var group = app.MapGroup("").WithTags("Lean");

        // Lean status
        group.MapGet(UiApiRoutes.LeanStatus, () =>
        {
            return Results.Json(new
            {
                installed = false,
                leanPath = (string?)null,
                dataPath = (string?)null,
                version = (string?)null,
                activeBacktests = s_backtests.Count(b => b.Value.Status == "running"),
                timestamp = DateTimeOffset.UtcNow
            }, jsonOptions);
        })
        .WithName("GetLeanStatus")
        .Produces(200);

        // Lean config
        group.MapGet(UiApiRoutes.LeanConfig, () =>
        {
            return Results.Json(new
            {
                leanPath = (string?)null,
                dataDirectory = (string?)null,
                pythonEnabled = false,
                algorithmLanguage = "CSharp",
                timestamp = DateTimeOffset.UtcNow
            }, jsonOptions);
        })
        .WithName("GetLeanConfig")
        .Produces(200);

        // Verify Lean installation
        group.MapPost(UiApiRoutes.LeanVerify, () =>
        {
            return Results.Json(new
            {
                installed = false,
                message = "Lean Engine not detected. Set the LEAN_PATH environment variable to the Lean installation directory.",
                checks = new[]
                {
                    new { check = "lean_path_set", passed = false },
                    new { check = "lean_binary_exists", passed = false },
                    new { check = "data_directory_exists", passed = false }
                },
                timestamp = DateTimeOffset.UtcNow
            }, jsonOptions);
        })
        .WithName("VerifyLean")
        .Produces(200)
        .RequireRateLimiting(UiEndpoints.MutationRateLimitPolicy);

        // List algorithms
        group.MapGet(UiApiRoutes.LeanAlgorithms, () =>
        {
            return Results.Json(new
            {
                algorithms = Array.Empty<object>(),
                message = "Lean Engine not configured. No algorithms available.",
                timestamp = DateTimeOffset.UtcNow
            }, jsonOptions);
        })
        .WithName("GetLeanAlgorithms")
        .Produces(200);

        // Sync data to Lean format
        group.MapPost(UiApiRoutes.LeanSync, (LeanSyncRequest? req) =>
        {
            return Results.Json(new
            {
                jobId = Guid.NewGuid().ToString("N")[..12],
                symbols = req?.Symbols ?? Array.Empty<string>(),
                status = "queued",
                timestamp = DateTimeOffset.UtcNow
            }, jsonOptions);
        })
        .WithName("StartLeanSync")
        .Produces(200)
        .RequireRateLimiting(UiEndpoints.MutationRateLimitPolicy);

        // Sync status
        group.MapGet(UiApiRoutes.LeanSyncStatus, () =>
        {
            return Results.Json(new
            {
                isRunning = false,
                lastSyncAt = (DateTimeOffset?)null,
                timestamp = DateTimeOffset.UtcNow
            }, jsonOptions);
        })
        .WithName("GetLeanSyncStatus")
        .Produces(200);

        // Start backtest
        group.MapPost(UiApiRoutes.LeanBacktestStart, (BacktestStartRequest? req) =>
        {
            var backtestId = Guid.NewGuid().ToString("N")[..12];
            var info = new BacktestInfo(backtestId, req?.AlgorithmName ?? "unknown", "queued", DateTimeOffset.UtcNow);
            s_backtests[backtestId] = info;

            return Results.Json(new
            {
                backtestId,
                algorithmName = req?.AlgorithmName,
                status = "queued",
                timestamp = DateTimeOffset.UtcNow
            }, jsonOptions);
        })
        .WithName("StartLeanBacktest")
        .Produces(200)
        .RequireRateLimiting(UiEndpoints.MutationRateLimitPolicy);

        // Backtest status
        group.MapGet(UiApiRoutes.LeanBacktestStatus, (string backtestId) =>
        {
            if (!s_backtests.TryGetValue(backtestId, out var info))
                return Results.NotFound(new { error = $"Backtest '{backtestId}' not found" });

            return Results.Json(new
            {
                backtestId = info.Id,
                algorithmName = info.AlgorithmName,
                status = info.Status,
                startedAt = info.StartedAt
            }, jsonOptions);
        })
        .WithName("GetLeanBacktestStatus")
        .Produces(200)
        .Produces(404);

        // Backtest results
        group.MapGet(UiApiRoutes.LeanBacktestResults, (string backtestId) =>
        {
            if (!s_backtests.TryGetValue(backtestId, out var info))
                return Results.NotFound(new { error = $"Backtest '{backtestId}' not found" });

            return Results.Json(new
            {
                backtestId = info.Id,
                status = info.Status,
                results = (object?)null,
                message = "Results will be available when the backtest completes"
            }, jsonOptions);
        })
        .WithName("GetLeanBacktestResults")
        .Produces(200)
        .Produces(404);

        // Stop backtest
        group.MapPost(UiApiRoutes.LeanBacktestStop, (string backtestId) =>
        {
            if (!s_backtests.TryGetValue(backtestId, out var info))
                return Results.NotFound(new { error = $"Backtest '{backtestId}' not found" });

            info = info with { Status = "stopped" };
            s_backtests[backtestId] = info;
            return Results.Json(new { backtestId, status = "stopped" }, jsonOptions);
        })
        .WithName("StopLeanBacktest")
        .Produces(200)
        .Produces(404)
        .RequireRateLimiting(UiEndpoints.MutationRateLimitPolicy);

        // Backtest history
        group.MapGet(UiApiRoutes.LeanBacktestHistory, (int? limit) =>
        {
            var history = s_backtests.Values
                .OrderByDescending(b => b.StartedAt)
                .Take(limit ?? 20)
                .Select(b => new { backtestId = b.Id, algorithmName = b.AlgorithmName, status = b.Status, startedAt = b.StartedAt });

            return Results.Json(new { backtests = history, total = s_backtests.Count, timestamp = DateTimeOffset.UtcNow }, jsonOptions);
        })
        .WithName("GetLeanBacktestHistory")
        .Produces(200);

        // Delete backtest
        group.MapDelete(UiApiRoutes.LeanBacktestDelete, (string backtestId) =>
        {
            var removed = s_backtests.Remove(backtestId);
            return removed
                ? Results.Json(new { deleted = true, backtestId }, jsonOptions)
                : Results.NotFound(new { error = $"Backtest '{backtestId}' not found" });
        })
        .WithName("DeleteLeanBacktest")
        .Produces(200)
        .Produces(404)
        .RequireRateLimiting(UiEndpoints.MutationRateLimitPolicy);
    }

    private sealed record BacktestInfo(string Id, string AlgorithmName, string Status, DateTimeOffset StartedAt);
    private sealed record LeanSyncRequest(string[]? Symbols, DateTime? FromDate, DateTime? ToDate);
    private sealed record BacktestStartRequest(string? AlgorithmName, string? AlgorithmLanguage);
}
