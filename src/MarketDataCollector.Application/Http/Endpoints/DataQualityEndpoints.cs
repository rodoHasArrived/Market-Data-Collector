using System.Text.Json;
using MarketDataCollector.Contracts.Api;
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
    /// Wraps a synchronous handler with consistent error handling and JSON serialization.
    /// </summary>
    private static IResult HandleSync(Func<IResult> handler)
    {
        try
        {
            return handler();
        }
        catch (Exception ex)
        {
            return Results.Problem(ex.Message);
        }
    }

    /// <summary>
    /// Wraps an async handler with consistent error handling and JSON serialization.
    /// </summary>
    private static async Task<IResult> HandleAsync(Func<Task<IResult>> handler)
    {
        try
        {
            return await handler();
        }
        catch (Exception ex)
        {
            return Results.Problem(ex.Message);
        }
    }

    /// <summary>
    /// Parses an optional date string, defaulting to today (UTC).
    /// </summary>
    private static DateOnly ParseDateOrToday(string? date) =>
        date != null ? DateOnly.Parse(date) : DateOnly.FromDateTime(DateTime.UtcNow);

    /// <summary>
    /// Returns the result as JSON using the shared serializer options.
    /// </summary>
    private static IResult Json(object value) => Results.Json(value, s_jsonOptions);

    /// <summary>
    /// Maps all data quality monitoring endpoints.
    /// </summary>
    public static void MapDataQualityEndpoints(this WebApplication app, DataQualityMonitoringService qualityService)
    {
        // ==================== DASHBOARD ====================

        app.MapGet(UiApiRoutes.QualityDashboard, () =>
            HandleSync(() => Json(qualityService.GetDashboard())));

        app.MapGet(UiApiRoutes.QualityMetrics, () =>
            HandleSync(() => Json(qualityService.GetRealTimeMetrics())));

        // ==================== COMPLETENESS ====================

        app.MapGet(UiApiRoutes.QualityCompleteness, (string? date) =>
            HandleSync(() =>
            {
                var targetDate = ParseDateOrToday(date);
                return Json(qualityService.Completeness.GetScoresForDate(targetDate));
            }));

        app.MapGet(UiApiRoutes.QualityCompletenessBySymbol, (string symbol, string? date) =>
            HandleSync(() =>
            {
                if (date != null)
                {
                    var targetDate = DateOnly.Parse(date);
                    var score = qualityService.Completeness.GetScore(symbol, targetDate);
                    return score != null
                        ? Json(score)
                        : Results.NotFound($"No completeness data for {symbol} on {date}");
                }

                return Json(qualityService.Completeness.GetScoresForSymbol(symbol));
            }));

        app.MapGet(UiApiRoutes.QualityCompletenessSummary, () =>
            HandleSync(() => Json(qualityService.Completeness.GetSummary())));

        app.MapGet(UiApiRoutes.QualityCompletenessLow, (string? date, double? threshold) =>
            HandleSync(() =>
            {
                var targetDate = ParseDateOrToday(date);
                return Json(qualityService.Completeness.GetLowCompletenessSymbols(targetDate, threshold ?? 0.8));
            }));

        // ==================== GAP ANALYSIS ====================

        app.MapGet(UiApiRoutes.QualityGaps, (string? date, int? count) =>
            HandleSync(() =>
            {
                if (date != null)
                {
                    var targetDate = DateOnly.Parse(date);
                    return Json(qualityService.GapAnalyzer.GetGapsForDate(targetDate));
                }

                return Json(qualityService.GapAnalyzer.GetRecentGaps(count ?? 100));
            }));

        app.MapGet(UiApiRoutes.QualityGapsBySymbol, (string symbol, string? date) =>
            HandleSync(() =>
            {
                var targetDate = ParseDateOrToday(date);
                return Json(qualityService.GapAnalyzer.AnalyzeGaps(symbol, targetDate));
            }));

        app.MapGet(UiApiRoutes.QualityGapsTimeline, (string symbol, string? date) =>
            HandleSync(() =>
            {
                var targetDate = ParseDateOrToday(date);
                var analysis = qualityService.GapAnalyzer.AnalyzeGaps(symbol, targetDate);
                return Json(new { symbol, date = targetDate, timeline = analysis.Timeline });
            }));

        app.MapGet(UiApiRoutes.QualityGapsStatistics, (string? date) =>
            HandleSync(() =>
            {
                var targetDate = date != null ? DateOnly.Parse(date) : (DateOnly?)null;
                return Json(qualityService.GapAnalyzer.GetStatistics(targetDate));
            }));

        // ==================== SEQUENCE ERRORS ====================

        app.MapGet(UiApiRoutes.QualityErrors, (string? date, int? count) =>
            HandleSync(() =>
            {
                if (date != null)
                {
                    var targetDate = DateOnly.Parse(date);
                    return Json(qualityService.SequenceTracker.GetErrorsForDate(targetDate));
                }

                return Json(qualityService.SequenceTracker.GetRecentErrors(count ?? 100));
            }));

        app.MapGet(UiApiRoutes.QualityErrorsBySymbol, (string symbol, string? date, int? count) =>
            HandleSync(() =>
            {
                var targetDate = date != null ? DateOnly.Parse(date) : (DateOnly?)null;
                return Json(qualityService.SequenceTracker.GetSummary(symbol, targetDate));
            }));

        app.MapGet(UiApiRoutes.QualityErrorsStatistics, () =>
            HandleSync(() => Json(qualityService.SequenceTracker.GetStatistics())));

        app.MapGet(UiApiRoutes.QualityErrorsTopSymbols, (int? count) =>
            HandleSync(() => Json(qualityService.SequenceTracker.GetSymbolsWithMostErrors(count ?? 10))));

        // ==================== ANOMALIES ====================

        app.MapGet(UiApiRoutes.QualityAnomalies, (string? date, string? type, string? severity, int? count) =>
            HandleSync(() =>
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

                return Json(anomalies);
            }));

        app.MapGet(UiApiRoutes.QualityAnomaliesBySymbol, (string symbol, int? count) =>
            HandleSync(() => Json(qualityService.AnomalyDetector.GetAnomalies(symbol, count ?? 100))));

        app.MapGet(UiApiRoutes.QualityAnomaliesUnacknowledged, (int? count) =>
            HandleSync(() => Json(qualityService.AnomalyDetector.GetUnacknowledgedAnomalies(count ?? 100))));

        app.MapPost(UiApiRoutes.QualityAnomaliesAcknowledge, (string anomalyId) =>
            HandleSync(() =>
            {
                var success = qualityService.AnomalyDetector.AcknowledgeAnomaly(anomalyId);
                return success
                    ? Results.Ok(new { acknowledged = true })
                    : Results.NotFound($"Anomaly {anomalyId} not found");
            }));

        app.MapGet(UiApiRoutes.QualityAnomaliesStatistics, () =>
            HandleSync(() => Json(qualityService.AnomalyDetector.GetStatistics())));

        app.MapGet(UiApiRoutes.QualityAnomaliesStale, () =>
            HandleSync(() => Json(qualityService.AnomalyDetector.GetStaleSymbols())));

        // ==================== LATENCY ====================

        app.MapGet(UiApiRoutes.QualityLatency, () =>
            HandleSync(() => Json(qualityService.LatencyHistogram.GetAllDistributions())));

        app.MapGet(UiApiRoutes.QualityLatencyBySymbol, (string symbol, string? provider) =>
            HandleSync(() =>
            {
                var distribution = qualityService.LatencyHistogram.GetDistribution(symbol, provider);
                return distribution != null
                    ? Json(distribution)
                    : Results.NotFound($"No latency data for {symbol}");
            }));

        app.MapGet(UiApiRoutes.QualityLatencyHistogram, (string symbol, string? provider) =>
            HandleSync(() => Json(new { symbol, provider, buckets = qualityService.LatencyHistogram.GetBuckets(symbol, provider) })));

        app.MapGet(UiApiRoutes.QualityLatencyStatistics, () =>
            HandleSync(() => Json(qualityService.LatencyHistogram.GetStatistics())));

        app.MapGet(UiApiRoutes.QualityLatencyHigh, (double? thresholdMs) =>
            HandleSync(() => Json(qualityService.LatencyHistogram.GetHighLatencySymbols(thresholdMs ?? 100))));

        // ==================== CROSS-PROVIDER COMPARISON ====================

        app.MapGet(UiApiRoutes.QualityComparison, (string symbol, string? date, string? eventType) =>
            HandleSync(() =>
            {
                var targetDate = ParseDateOrToday(date);
                return Json(qualityService.CrossProvider.Compare(symbol, targetDate, eventType ?? "Trade"));
            }));

        app.MapGet(UiApiRoutes.QualityComparisonDiscrepancies, (string? date, int? count) =>
            HandleSync(() =>
            {
                if (date != null)
                {
                    var targetDate = DateOnly.Parse(date);
                    return Json(qualityService.CrossProvider.GetDiscrepanciesForDate(targetDate));
                }

                return Json(qualityService.CrossProvider.GetRecentDiscrepancies(count ?? 100));
            }));

        app.MapGet(UiApiRoutes.QualityComparisonStatistics, () =>
            HandleSync(() => Json(qualityService.CrossProvider.GetStatistics())));

        // ==================== REPORTS ====================

        app.MapGet(UiApiRoutes.QualityReportsDaily, async (string? date, CancellationToken ct) =>
            await HandleAsync(async () =>
            {
                var targetDate = ParseDateOrToday(date);
                var report = await qualityService.GenerateDailyReportAsync(targetDate, null, ct);
                return Json(report);
            }));

        app.MapGet(UiApiRoutes.QualityReportsWeekly, async (string? weekStart, CancellationToken ct) =>
            await HandleAsync(async () =>
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
                    start = today.AddDays(-dayOfWeek);
                }

                var report = await qualityService.GenerateWeeklyReportAsync(start, null, ct);
                return Json(report);
            }));

        app.MapPost(UiApiRoutes.QualityReportsExport, async (ReportExportRequest request, CancellationToken ct) =>
            await HandleAsync(async () =>
            {
                var targetDate = ParseDateOrToday(request.Date);
                var format = Enum.TryParse<ReportExportFormat>(request.Format, true, out var f)
                    ? f : ReportExportFormat.Json;

                var report = await qualityService.GenerateDailyReportAsync(targetDate, null, ct);
                var filePath = await qualityService.ExportReportAsync(report, format, ct);

                return Results.Ok(new { filePath, format = format.ToString() });
            }));

        // ==================== HEALTH ====================

        app.MapGet(UiApiRoutes.QualityHealth, () =>
            HandleSync(() =>
            {
                var metrics = qualityService.GetRealTimeMetrics();
                var status = metrics.OverallHealthScore switch
                {
                    >= 0.9 => "healthy",
                    >= 0.7 => "degraded",
                    _ => "unhealthy"
                };

                return Json(new
                {
                    status,
                    score = metrics.OverallHealthScore,
                    activeSymbols = metrics.ActiveSymbols,
                    symbolsWithIssues = metrics.SymbolsWithIssues,
                    gapsLast5Min = metrics.GapsLast5Minutes,
                    errorsLast5Min = metrics.SequenceErrorsLast5Minutes,
                    anomaliesLast5Min = metrics.AnomaliesLast5Minutes,
                    timestamp = metrics.Timestamp
                });
            }));

        app.MapGet(UiApiRoutes.QualityHealthBySymbol, (string symbol) =>
            HandleSync(() =>
            {
                var health = qualityService.GetSymbolHealth(symbol);
                return health != null
                    ? Json(health)
                    : Results.NotFound($"No health data for {symbol}");
            }));

        app.MapGet(UiApiRoutes.QualityHealthUnhealthy, () =>
            HandleSync(() => Json(qualityService.GetUnhealthySymbols())));
    }

    /// <summary>
    /// Maps SLA monitoring endpoints (ADQ-4.6).
    /// </summary>
    public static void MapSlaEndpoints(this WebApplication app, DataFreshnessSlaMonitor slaMonitor)
    {
        app.MapGet(UiApiRoutes.SlaStatus, () =>
            HandleSync(() => Json(slaMonitor.GetSnapshot())));

        app.MapGet(UiApiRoutes.SlaStatusBySymbol, (string symbol) =>
            HandleSync(() =>
            {
                var status = slaMonitor.GetSymbolStatus(symbol);
                return status != null
                    ? Json(status)
                    : Results.NotFound($"No SLA data for {symbol}");
            }));

        app.MapGet(UiApiRoutes.SlaViolations, () =>
            HandleSync(() =>
            {
                var snapshot = slaMonitor.GetSnapshot();
                var violations = snapshot.SymbolStatuses
                    .Where(s => s.State == SlaState.Violation)
                    .ToList();

                return Json(new
                {
                    count = violations.Count,
                    totalViolations = snapshot.TotalViolations,
                    violations
                });
            }));

        app.MapGet(UiApiRoutes.SlaHealth, () =>
            HandleSync(() =>
            {
                var snapshot = slaMonitor.GetSnapshot();
                var status = snapshot.OverallFreshnessScore switch
                {
                    >= 90 => "healthy",
                    >= 70 => "degraded",
                    _ => "unhealthy"
                };

                return Json(new
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
                });
            }));

        app.MapGet(UiApiRoutes.SlaMetrics, () =>
            HandleSync(() => Json(new
            {
                totalViolations = slaMonitor.TotalViolations,
                currentViolations = slaMonitor.CurrentViolations,
                totalRecoveries = slaMonitor.TotalRecoveries,
                isMarketOpen = slaMonitor.IsMarketOpen(),
                timestamp = DateTimeOffset.UtcNow
            })));
    }
}

/// <summary>
/// Request DTO for report export.
/// </summary>
public record ReportExportRequest(
    string? Date,
    string? Format
);
