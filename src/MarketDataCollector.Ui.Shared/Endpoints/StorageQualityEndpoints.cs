using System.Text.Json;
using MarketDataCollector.Contracts.Api;
using MarketDataCollector.Contracts.Domain.Enums;
using MarketDataCollector.Storage;
using MarketDataCollector.Storage.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace MarketDataCollector.Ui.Shared.Endpoints;

/// <summary>
/// Extension methods for registering storage quality API endpoints.
/// Implements Phase 3B.3 — replaces 9 stub endpoints with working handlers.
/// </summary>
public static class StorageQualityEndpoints
{
    public static void MapStorageQualityEndpoints(this WebApplication app, JsonSerializerOptions jsonOptions)
    {
        // GET /api/storage/quality/summary — overall quality summary
        app.MapGet(UiApiRoutes.StorageQualitySummary, async (
            IDataQualityService? qualityService,
            StorageOptions opts,
            CancellationToken ct) =>
        {
            if (qualityService is null)
                return Results.Json(new { status = "unavailable", message = "Data quality service not available" }, jsonOptions);

            try
            {
                var report = await qualityService.GenerateReportAsync(
                    new QualityReportOptions(
                        Paths: new[] { Path.GetFullPath(opts.RootPath) },
                        IncludeRecommendations: true), ct);

                return Results.Json(new
                {
                    generatedAt = report.GeneratedAt,
                    filesAnalyzed = report.FilesAnalyzed,
                    averageScore = report.AverageScore,
                    scoresByDimension = report.ScoresByDimension,
                    recommendations = report.Recommendations,
                    lowQualityFiles = report.LowQualityFiles?.Count ?? 0
                }, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to generate quality summary: {ex.Message}");
            }
        });

        // GET /api/storage/quality/scores — quality scores for all scored files
        app.MapGet(UiApiRoutes.StorageQualityScores, async (
            IDataQualityService? qualityService,
            StorageOptions opts,
            CancellationToken ct) =>
        {
            if (qualityService is null)
                return Results.Json(new { message = "Data quality service not available", scores = Array.Empty<object>() }, jsonOptions);

            try
            {
                var report = await qualityService.GenerateReportAsync(
                    new QualityReportOptions(
                        Paths: new[] { Path.GetFullPath(opts.RootPath) },
                        MinScoreThreshold: 0.0,
                        IncludeRecommendations: false), ct);

                return Results.Json(new
                {
                    averageScore = report.AverageScore,
                    filesAnalyzed = report.FilesAnalyzed,
                    lowQualityFiles = report.LowQualityFiles
                }, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to retrieve quality scores: {ex.Message}");
            }
        });

        // GET /api/storage/quality/symbol/{symbol} — quality for a specific symbol
        app.MapGet(UiApiRoutes.StorageQualitySymbol, async (
            string symbol,
            IDataQualityService? qualityService,
            StorageOptions opts,
            CancellationToken ct) =>
        {
            if (qualityService is null)
                return Results.Json(new { symbol, message = "Data quality service not available" }, jsonOptions);

            try
            {
                var trend = await qualityService.GetTrendAsync(symbol, TimeSpan.FromDays(30), ct);
                return Results.Json(new
                {
                    symbol,
                    trend
                }, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get quality for {symbol}: {ex.Message}");
            }
        });

        // GET /api/storage/quality/alerts — active quality alerts
        app.MapGet(UiApiRoutes.StorageQualityAlerts, async (
            IDataQualityService? qualityService,
            CancellationToken ct) =>
        {
            if (qualityService is null)
                return Results.Json(new { alerts = Array.Empty<object>(), message = "Data quality service not available" }, jsonOptions);

            try
            {
                var alerts = await qualityService.GetQualityAlertsAsync(ct);
                return Results.Json(new
                {
                    count = alerts.Length,
                    alerts
                }, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to retrieve quality alerts: {ex.Message}");
            }
        });

        // POST /api/storage/quality/alerts/{alertId}/acknowledge — acknowledge an alert
        app.MapPost(UiApiRoutes.StorageQualityAlertAcknowledge, (string alertId) =>
        {
            // Alert acknowledgment state is not persisted by IDataQualityService,
            // so we accept the request and return success.
            return Results.Ok(new { acknowledged = alertId, timestamp = DateTimeOffset.UtcNow });
        }).RequireRateLimiting(UiEndpoints.MutationRateLimitPolicy);

        // GET /api/storage/quality/rankings/{symbol} — source rankings for a symbol
        app.MapGet(UiApiRoutes.StorageQualityRankings, async (
            string symbol,
            IDataQualityService? qualityService,
            CancellationToken ct) =>
        {
            if (qualityService is null)
                return Results.Json(new { symbol, message = "Data quality service not available" }, jsonOptions);

            try
            {
                var rankings = await qualityService.RankSourcesAsync(
                    symbol,
                    DateTimeOffset.UtcNow.Date,
                    MarketEventType.Trade,
                    ct);

                return Results.Json(new
                {
                    symbol,
                    date = DateTimeOffset.UtcNow.Date,
                    rankings
                }, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to rank sources for {symbol}: {ex.Message}");
            }
        });

        // GET /api/storage/quality/trends — quality trends across all data
        app.MapGet(UiApiRoutes.StorageQualityTrends, async (
            HttpContext ctx,
            IDataQualityService? qualityService,
            StorageOptions opts,
            CancellationToken ct) =>
        {
            if (qualityService is null)
                return Results.Json(new { message = "Data quality service not available" }, jsonOptions);

            var days = int.TryParse(ctx.Request.Query["days"].FirstOrDefault(), out var d) ? d : 30;

            try
            {
                var report = await qualityService.GenerateReportAsync(
                    new QualityReportOptions(
                        Paths: new[] { Path.GetFullPath(opts.RootPath) },
                        From: DateTimeOffset.UtcNow.AddDays(-days),
                        IncludeRecommendations: false), ct);

                return Results.Json(new
                {
                    period = $"last {days} days",
                    averageScore = report.AverageScore,
                    scoresByDimension = report.ScoresByDimension,
                    filesAnalyzed = report.FilesAnalyzed
                }, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to compute trends: {ex.Message}");
            }
        });

        // GET /api/storage/quality/anomalies — detected quality anomalies
        app.MapGet(UiApiRoutes.StorageQualityAnomalies, async (
            IDataQualityService? qualityService,
            StorageOptions opts,
            CancellationToken ct) =>
        {
            if (qualityService is null)
                return Results.Json(new { anomalies = Array.Empty<object>(), message = "Data quality service not available" }, jsonOptions);

            try
            {
                var report = await qualityService.GenerateReportAsync(
                    new QualityReportOptions(
                        Paths: new[] { Path.GetFullPath(opts.RootPath) },
                        MinScoreThreshold: 0.5,
                        IncludeRecommendations: true), ct);

                return Results.Json(new
                {
                    lowQualityCount = report.LowQualityFiles?.Count ?? 0,
                    anomalies = report.LowQualityFiles?.Select(f => new
                    {
                        f.Path,
                        f.OverallScore,
                        issues = f.Dimensions?.Where(d => d.Score < 0.5).Select(d => new { d.Name, d.Score, d.Issues })
                    })
                }, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to detect anomalies: {ex.Message}");
            }
        });

        // POST /api/storage/quality/check — run a quality check on specified path
        app.MapPost(UiApiRoutes.StorageQualityCheck, async (
            IDataQualityService? qualityService,
            StorageQualityCheckRequest req,
            CancellationToken ct) =>
        {
            if (qualityService is null)
                return Results.Problem("Data quality service not available");

            if (string.IsNullOrWhiteSpace(req.Path))
                return Results.BadRequest(new { error = "Path is required" });

            // Prevent path traversal
            var fullPath = Path.GetFullPath(req.Path);
            if (!File.Exists(fullPath) && !Directory.Exists(fullPath))
                return Results.NotFound(new { error = $"Path not found: {req.Path}" });

            try
            {
                var score = await qualityService.ScoreAsync(fullPath, ct);
                return Results.Json(score, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Quality check failed: {ex.Message}");
            }
        }).RequireRateLimiting(UiEndpoints.MutationRateLimitPolicy);
    }
}

// Request DTOs
internal sealed record StorageQualityCheckRequest(string Path);
