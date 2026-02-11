using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace MarketDataCollector.Application.Monitoring.DataQuality;

/// <summary>
/// HTTP endpoint extensions for data quality monitoring dashboard.
/// </summary>
public static class DataQualityEndpoints
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    /// <summary>
    /// Maps all data quality monitoring endpoints.
    /// </summary>
    public static void MapDataQualityEndpoints(this WebApplication app, DataQualityMonitoringService qualityService)
    {
        // ==================== DASHBOARD ====================

        app.MapGet("/api/quality/dashboard", () =>
        {
            try
            {
                var dashboard = qualityService.GetDashboard();
                return Results.Json(dashboard, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get dashboard: {ex.Message}");
            }
        });

        app.MapGet("/api/quality/metrics", () =>
        {
            try
            {
                var metrics = qualityService.GetRealTimeMetrics();
                return Results.Json(metrics, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get metrics: {ex.Message}");
            }
        });

        // ==================== COMPLETENESS ====================

        app.MapGet("/api/quality/completeness", (string? date) =>
        {
            try
            {
                var targetDate = date != null
                    ? DateOnly.Parse(date)
                    : DateOnly.FromDateTime(DateTime.UtcNow);

                var scores = qualityService.Completeness.GetScoresForDate(targetDate);
                return Results.Json(scores, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get completeness scores: {ex.Message}");
            }
        });

        app.MapGet("/api/quality/completeness/{symbol}", (string symbol, string? date) =>
        {
            try
            {
                if (date != null)
                {
                    var targetDate = DateOnly.Parse(date);
                    var score = qualityService.Completeness.GetScore(symbol, targetDate);
                    return score != null
                        ? Results.Json(score, s_jsonOptions)
                        : Results.NotFound($"No completeness data for {symbol} on {date}");
                }

                var scores = qualityService.Completeness.GetScoresForSymbol(symbol);
                return Results.Json(scores, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get completeness score: {ex.Message}");
            }
        });

        app.MapGet("/api/quality/completeness/summary", () =>
        {
            try
            {
                var summary = qualityService.Completeness.GetSummary();
                return Results.Json(summary, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get completeness summary: {ex.Message}");
            }
        });

        app.MapGet("/api/quality/completeness/low", (string? date, double? threshold) =>
        {
            try
            {
                var targetDate = date != null
                    ? DateOnly.Parse(date)
                    : DateOnly.FromDateTime(DateTime.UtcNow);

                var scores = qualityService.Completeness.GetLowCompletenessSymbols(targetDate, threshold ?? 0.8);
                return Results.Json(scores, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get low completeness symbols: {ex.Message}");
            }
        });

        // ==================== GAP ANALYSIS ====================

        app.MapGet("/api/quality/gaps", (string? date, int? count) =>
        {
            try
            {
                if (date != null)
                {
                    var targetDate = DateOnly.Parse(date);
                    var gaps = qualityService.GapAnalyzer.GetGapsForDate(targetDate);
                    return Results.Json(gaps, s_jsonOptions);
                }

                var recent = qualityService.GapAnalyzer.GetRecentGaps(count ?? 100);
                return Results.Json(recent, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get gaps: {ex.Message}");
            }
        });

        app.MapGet("/api/quality/gaps/{symbol}", (string symbol, string? date) =>
        {
            try
            {
                var targetDate = date != null
                    ? DateOnly.Parse(date)
                    : DateOnly.FromDateTime(DateTime.UtcNow);

                var analysis = qualityService.GapAnalyzer.AnalyzeGaps(symbol, targetDate);
                return Results.Json(analysis, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to analyze gaps: {ex.Message}");
            }
        });

        app.MapGet("/api/quality/gaps/timeline/{symbol}", (string symbol, string? date) =>
        {
            try
            {
                var targetDate = date != null
                    ? DateOnly.Parse(date)
                    : DateOnly.FromDateTime(DateTime.UtcNow);

                var analysis = qualityService.GapAnalyzer.AnalyzeGaps(symbol, targetDate);
                return Results.Json(new { symbol, date = targetDate, timeline = analysis.Timeline }, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get timeline: {ex.Message}");
            }
        });

        app.MapGet("/api/quality/gaps/statistics", (string? date) =>
        {
            try
            {
                var targetDate = date != null
                    ? DateOnly.Parse(date)
                    : (DateOnly?)null;

                var stats = qualityService.GapAnalyzer.GetStatistics(targetDate);
                return Results.Json(stats, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get gap statistics: {ex.Message}");
            }
        });

        // ==================== SEQUENCE ERRORS ====================

        app.MapGet("/api/quality/errors", (string? date, int? count) =>
        {
            try
            {
                if (date != null)
                {
                    var targetDate = DateOnly.Parse(date);
                    var errors = qualityService.SequenceTracker.GetErrorsForDate(targetDate);
                    return Results.Json(errors, s_jsonOptions);
                }

                var recent = qualityService.SequenceTracker.GetRecentErrors(count ?? 100);
                return Results.Json(recent, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get sequence errors: {ex.Message}");
            }
        });

        app.MapGet("/api/quality/errors/{symbol}", (string symbol, string? date, int? count) =>
        {
            try
            {
                var targetDate = date != null ? DateOnly.Parse(date) : (DateOnly?)null;
                var summary = qualityService.SequenceTracker.GetSummary(symbol, targetDate);
                return Results.Json(summary, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get error summary: {ex.Message}");
            }
        });

        app.MapGet("/api/quality/errors/statistics", () =>
        {
            try
            {
                var stats = qualityService.SequenceTracker.GetStatistics();
                return Results.Json(stats, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get error statistics: {ex.Message}");
            }
        });

        app.MapGet("/api/quality/errors/top-symbols", (int? count) =>
        {
            try
            {
                var symbols = qualityService.SequenceTracker.GetSymbolsWithMostErrors(count ?? 10);
                return Results.Json(symbols, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get top error symbols: {ex.Message}");
            }
        });

        // ==================== ANOMALIES ====================

        app.MapGet("/api/quality/anomalies", (string? date, string? type, string? severity, int? count) =>
        {
            try
            {
                IReadOnlyList<DataAnomaly> anomalies;

                if (date != null)
                {
                    var targetDate = DateOnly.Parse(date);
                    anomalies = qualityService.AnomalyDetector.GetAnomaliesForDate(targetDate);
                }
                else if (type != null && Enum.TryParse<AnomalyType>(type, true, out var anomalyType))
                {
                    anomalies = qualityService.AnomalyDetector.GetAnomaliesByType(anomalyType, count ?? 100);
                }
                else if (severity != null && Enum.TryParse<AnomalySeverity>(severity, true, out var sev))
                {
                    anomalies = qualityService.AnomalyDetector.GetAnomaliesBySeverity(sev, count ?? 100);
                }
                else
                {
                    anomalies = qualityService.AnomalyDetector.GetRecentAnomalies(count ?? 100);
                }

                return Results.Json(anomalies, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get anomalies: {ex.Message}");
            }
        });

        app.MapGet("/api/quality/anomalies/{symbol}", (string symbol, int? count) =>
        {
            try
            {
                var anomalies = qualityService.AnomalyDetector.GetAnomalies(symbol, count ?? 100);
                return Results.Json(anomalies, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get anomalies: {ex.Message}");
            }
        });

        app.MapGet("/api/quality/anomalies/unacknowledged", (int? count) =>
        {
            try
            {
                var anomalies = qualityService.AnomalyDetector.GetUnacknowledgedAnomalies(count ?? 100);
                return Results.Json(anomalies, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get unacknowledged anomalies: {ex.Message}");
            }
        });

        app.MapPost("/api/quality/anomalies/{anomalyId}/acknowledge", (string anomalyId) =>
        {
            try
            {
                var success = qualityService.AnomalyDetector.AcknowledgeAnomaly(anomalyId);
                return success
                    ? Results.Ok(new { acknowledged = true })
                    : Results.NotFound($"Anomaly {anomalyId} not found");
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to acknowledge anomaly: {ex.Message}");
            }
        });

        app.MapGet("/api/quality/anomalies/statistics", () =>
        {
            try
            {
                var stats = qualityService.AnomalyDetector.GetStatistics();
                return Results.Json(stats, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get anomaly statistics: {ex.Message}");
            }
        });

        app.MapGet("/api/quality/anomalies/stale", () =>
        {
            try
            {
                var stale = qualityService.AnomalyDetector.GetStaleSymbols();
                return Results.Json(stale, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get stale symbols: {ex.Message}");
            }
        });

        // ==================== LATENCY ====================

        app.MapGet("/api/quality/latency", () =>
        {
            try
            {
                var distributions = qualityService.LatencyHistogram.GetAllDistributions();
                return Results.Json(distributions, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get latency distributions: {ex.Message}");
            }
        });

        app.MapGet("/api/quality/latency/{symbol}", (string symbol, string? provider) =>
        {
            try
            {
                var distribution = qualityService.LatencyHistogram.GetDistribution(symbol, provider);
                return distribution != null
                    ? Results.Json(distribution, s_jsonOptions)
                    : Results.NotFound($"No latency data for {symbol}");
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get latency distribution: {ex.Message}");
            }
        });

        app.MapGet("/api/quality/latency/{symbol}/histogram", (string symbol, string? provider) =>
        {
            try
            {
                var buckets = qualityService.LatencyHistogram.GetBuckets(symbol, provider);
                return Results.Json(new { symbol, provider, buckets }, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get histogram: {ex.Message}");
            }
        });

        app.MapGet("/api/quality/latency/statistics", () =>
        {
            try
            {
                var stats = qualityService.LatencyHistogram.GetStatistics();
                return Results.Json(stats, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get latency statistics: {ex.Message}");
            }
        });

        app.MapGet("/api/quality/latency/high", (double? thresholdMs) =>
        {
            try
            {
                var high = qualityService.LatencyHistogram.GetHighLatencySymbols(thresholdMs ?? 100);
                return Results.Json(high, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get high latency symbols: {ex.Message}");
            }
        });

        // ==================== CROSS-PROVIDER COMPARISON ====================

        app.MapGet("/api/quality/comparison/{symbol}", (string symbol, string? date, string? eventType) =>
        {
            try
            {
                var targetDate = date != null
                    ? DateOnly.Parse(date)
                    : DateOnly.FromDateTime(DateTime.UtcNow);

                var comparison = qualityService.CrossProvider.Compare(symbol, targetDate, eventType ?? "Trade");
                return Results.Json(comparison, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to compare providers: {ex.Message}");
            }
        });

        app.MapGet("/api/quality/comparison/discrepancies", (string? date, int? count) =>
        {
            try
            {
                IReadOnlyList<ProviderDiscrepancy> discrepancies;
                if (date != null)
                {
                    var targetDate = DateOnly.Parse(date);
                    discrepancies = qualityService.CrossProvider.GetDiscrepanciesForDate(targetDate);
                }
                else
                {
                    discrepancies = qualityService.CrossProvider.GetRecentDiscrepancies(count ?? 100);
                }

                return Results.Json(discrepancies, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get discrepancies: {ex.Message}");
            }
        });

        app.MapGet("/api/quality/comparison/statistics", () =>
        {
            try
            {
                var stats = qualityService.CrossProvider.GetStatistics();
                return Results.Json(stats, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get comparison statistics: {ex.Message}");
            }
        });

        // ==================== REPORTS ====================

        app.MapGet("/api/quality/reports/daily", async (string? date, CancellationToken ct) =>
        {
            try
            {
                var targetDate = date != null
                    ? DateOnly.Parse(date)
                    : DateOnly.FromDateTime(DateTime.UtcNow);

                var report = await qualityService.GenerateDailyReportAsync(targetDate, null, ct);
                return Results.Json(report, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to generate daily report: {ex.Message}");
            }
        });

        app.MapGet("/api/quality/reports/weekly", async (string? weekStart, CancellationToken ct) =>
        {
            try
            {
                DateOnly start;
                if (weekStart != null)
                {
                    start = DateOnly.Parse(weekStart);
                }
                else
                {
                    var today = DateOnly.FromDateTime(DateTime.UtcNow);
                    var dayOfWeek = (int)today.DayOfWeek;
                    start = today.AddDays(-dayOfWeek); // Start of current week (Sunday)
                }

                var report = await qualityService.GenerateWeeklyReportAsync(start, null, ct);
                return Results.Json(report, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to generate weekly report: {ex.Message}");
            }
        });

        app.MapPost("/api/quality/reports/export", async (ReportExportRequest request, CancellationToken ct) =>
        {
            try
            {
                var targetDate = request.Date != null
                    ? DateOnly.Parse(request.Date)
                    : DateOnly.FromDateTime(DateTime.UtcNow);

                var format = Enum.TryParse<ReportExportFormat>(request.Format, true, out var f)
                    ? f : ReportExportFormat.Json;

                var report = await qualityService.GenerateDailyReportAsync(targetDate, null, ct);
                var filePath = await qualityService.ExportReportAsync(report, format, ct);

                return Results.Ok(new { filePath, format = format.ToString() });
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to export report: {ex.Message}");
            }
        });

        // ==================== HEALTH ====================

        app.MapGet("/api/quality/health", () =>
        {
            try
            {
                var metrics = qualityService.GetRealTimeMetrics();
                var status = metrics.OverallHealthScore switch
                {
                    >= 0.9 => "healthy",
                    >= 0.7 => "degraded",
                    _ => "unhealthy"
                };

                return Results.Json(new
                {
                    status,
                    score = metrics.OverallHealthScore,
                    activeSymbols = metrics.ActiveSymbols,
                    symbolsWithIssues = metrics.SymbolsWithIssues,
                    gapsLast5Min = metrics.GapsLast5Minutes,
                    errorsLast5Min = metrics.SequenceErrorsLast5Minutes,
                    anomaliesLast5Min = metrics.AnomaliesLast5Minutes,
                    timestamp = metrics.Timestamp
                }, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get health: {ex.Message}");
            }
        });

        app.MapGet("/api/quality/health/{symbol}", (string symbol) =>
        {
            try
            {
                var health = qualityService.GetSymbolHealth(symbol);
                return health != null
                    ? Results.Json(health, s_jsonOptions)
                    : Results.NotFound($"No health data for {symbol}");
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get symbol health: {ex.Message}");
            }
        });

        app.MapGet("/api/quality/health/unhealthy", () =>
        {
            try
            {
                var unhealthy = qualityService.GetUnhealthySymbols();
                return Results.Json(unhealthy, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get unhealthy symbols: {ex.Message}");
            }
        });
    }

    /// <summary>
    /// Maps SLA monitoring endpoints (ADQ-4.6).
    /// </summary>
    public static void MapSlaEndpoints(this WebApplication app, DataFreshnessSlaMonitor slaMonitor)
    {
        // ==================== SLA STATUS ====================

        app.MapGet("/api/sla/status", () =>
        {
            try
            {
                var snapshot = slaMonitor.GetSnapshot();
                return Results.Json(snapshot, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get SLA status: {ex.Message}");
            }
        });

        app.MapGet("/api/sla/status/{symbol}", (string symbol) =>
        {
            try
            {
                var status = slaMonitor.GetSymbolStatus(symbol);
                return status != null
                    ? Results.Json(status, s_jsonOptions)
                    : Results.NotFound($"No SLA data for {symbol}");
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get SLA status for symbol: {ex.Message}");
            }
        });

        app.MapGet("/api/sla/violations", () =>
        {
            try
            {
                var snapshot = slaMonitor.GetSnapshot();
                var violations = snapshot.SymbolStatuses
                    .Where(s => s.State == SlaState.Violation)
                    .ToList();

                return Results.Json(new
                {
                    count = violations.Count,
                    totalViolations = snapshot.TotalViolations,
                    violations
                }, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get SLA violations: {ex.Message}");
            }
        });

        app.MapGet("/api/sla/health", () =>
        {
            try
            {
                var snapshot = slaMonitor.GetSnapshot();
                var status = snapshot.OverallFreshnessScore switch
                {
                    >= 90 => "healthy",
                    >= 70 => "degraded",
                    _ => "unhealthy"
                };

                return Results.Json(new
                {
                    status,
                    score = snapshot.OverallFreshnessScore,
                    totalSymbols = snapshot.TotalSymbols,
                    healthySymbols = snapshot.HealthySymbols,
                    warningSymbols = snapshot.WarningSymbols,
                    violationSymbols = snapshot.ViolationSymbols,
                    noDataSymbols = snapshot.NoDataSymbols,
                    totalViolations = snapshot.TotalViolations,
                    isMarketOpen = snapshot.IsMarketOpen,
                    timestamp = snapshot.Timestamp
                }, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get SLA health: {ex.Message}");
            }
        });

        app.MapGet("/api/sla/metrics", () =>
        {
            try
            {
                return Results.Json(new
                {
                    totalViolations = slaMonitor.TotalViolations,
                    currentViolations = slaMonitor.CurrentViolations,
                    totalRecoveries = slaMonitor.TotalRecoveries,
                    isMarketOpen = slaMonitor.IsMarketOpen(),
                    timestamp = DateTimeOffset.UtcNow
                }, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get SLA metrics: {ex.Message}");
            }
        });
    }
}

/// <summary>
/// Request DTO for report export.
/// </summary>
public record ReportExportRequest(
    string? Date,
    string? Format
);
