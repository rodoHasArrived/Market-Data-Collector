using System.Text.Json;
using MarketDataCollector.Contracts.Api;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace MarketDataCollector.Ui.Shared.Endpoints;

/// <summary>
/// Registers all unimplemented API routes with a 501 Not Implemented response.
/// Prevents clients from receiving unexplained 404/405 errors for declared routes.
/// </summary>
public static class StubEndpoints
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Maps all unimplemented routes declared in <see cref="UiApiRoutes"/> with 501 responses.
    /// Only registers routes that have not already been mapped by other endpoint groups.
    /// </summary>
    public static WebApplication MapStubEndpoints(this WebApplication app)
    {
        // Symbol management endpoints
        MapStub(app, "GET", UiApiRoutes.Symbols);
        MapStub(app, "GET", UiApiRoutes.SymbolsMonitored);
        MapStub(app, "GET", UiApiRoutes.SymbolsArchived);
        MapStub(app, "GET", UiApiRoutes.SymbolStatus);
        MapStub(app, "POST", UiApiRoutes.SymbolsAdd);
        MapStub(app, "POST", UiApiRoutes.SymbolRemove);
        MapStub(app, "GET", UiApiRoutes.SymbolTrades);
        MapStub(app, "GET", UiApiRoutes.SymbolDepth);
        MapStub(app, "GET", UiApiRoutes.SymbolsStatistics);
        MapStub(app, "POST", UiApiRoutes.SymbolsValidate);
        MapStub(app, "POST", UiApiRoutes.SymbolArchive);
        MapStub(app, "POST", UiApiRoutes.SymbolsBulkAdd);
        MapStub(app, "POST", UiApiRoutes.SymbolsBulkRemove);
        MapStub(app, "GET", UiApiRoutes.SymbolsSearch);
        MapStub(app, "POST", UiApiRoutes.SymbolsBatch);

        // Backfill endpoints (unimplemented subset)
        MapStub(app, "GET", UiApiRoutes.BackfillHealth);
        MapStub(app, "GET", UiApiRoutes.BackfillResolve);
        MapStub(app, "POST", UiApiRoutes.BackfillGapFill);
        MapStub(app, "GET", UiApiRoutes.BackfillPresets);
        MapStub(app, "GET", UiApiRoutes.BackfillExecutions);
        MapStub(app, "GET", UiApiRoutes.BackfillStatistics);
        MapStub(app, "GET", UiApiRoutes.BackfillSchedules);
        MapStub(app, "POST", UiApiRoutes.BackfillSchedules);
        MapStub(app, "GET", UiApiRoutes.BackfillSchedulesById);
        MapStub(app, "DELETE", UiApiRoutes.BackfillSchedulesDelete);
        MapStub(app, "POST", UiApiRoutes.BackfillSchedulesEnable);
        MapStub(app, "POST", UiApiRoutes.BackfillSchedulesDisable);
        MapStub(app, "POST", UiApiRoutes.BackfillSchedulesRun);
        MapStub(app, "GET", UiApiRoutes.BackfillSchedulesHistory);
        MapStub(app, "GET", UiApiRoutes.BackfillSchedulesTemplates);

        // Provider endpoints (unimplemented subset)
        MapStub(app, "GET", UiApiRoutes.ProviderById);
        MapStub(app, "GET", UiApiRoutes.ProviderFailover);
        MapStub(app, "POST", UiApiRoutes.ProviderFailoverTrigger);
        MapStub(app, "POST", UiApiRoutes.ProviderFailoverReset);
        MapStub(app, "GET", UiApiRoutes.ProviderRateLimits);
        MapStub(app, "GET", UiApiRoutes.ProviderRateLimitHistory);
        MapStub(app, "GET", UiApiRoutes.ProviderCapabilities);
        MapStub(app, "POST", UiApiRoutes.ProviderSwitch);
        MapStub(app, "POST", UiApiRoutes.ProviderTest);
        MapStub(app, "GET", UiApiRoutes.ProviderFailoverThresholds);
        MapStub(app, "GET", UiApiRoutes.ProviderHealth);

        // Storage endpoints
        MapStub(app, "GET", UiApiRoutes.StorageProfiles);
        MapStub(app, "GET", UiApiRoutes.StorageStats);
        MapStub(app, "GET", UiApiRoutes.StorageBreakdown);
        MapStub(app, "GET", UiApiRoutes.StorageSymbolInfo);
        MapStub(app, "GET", UiApiRoutes.StorageSymbolStats);
        MapStub(app, "GET", UiApiRoutes.StorageSymbolFiles);
        MapStub(app, "GET", UiApiRoutes.StorageSymbolPath);
        MapStub(app, "GET", UiApiRoutes.StorageHealth);
        MapStub(app, "GET", UiApiRoutes.StorageCleanupCandidates);
        MapStub(app, "POST", UiApiRoutes.StorageCleanup);
        MapStub(app, "GET", UiApiRoutes.StorageArchiveStats);
        MapStub(app, "GET", UiApiRoutes.StorageCatalog);
        MapStub(app, "GET", UiApiRoutes.StorageSearchFiles);
        MapStub(app, "GET", UiApiRoutes.StorageHealthCheck);
        MapStub(app, "GET", UiApiRoutes.StorageHealthOrphans);
        MapStub(app, "POST", UiApiRoutes.StorageTiersMigrate);
        MapStub(app, "GET", UiApiRoutes.StorageTiersStatistics);
        MapStub(app, "GET", UiApiRoutes.StorageTiersPlan);
        MapStub(app, "POST", UiApiRoutes.StorageMaintenanceDefrag);

        // Storage quality endpoints
        MapStub(app, "GET", UiApiRoutes.StorageQualitySummary);
        MapStub(app, "GET", UiApiRoutes.StorageQualityScores);
        MapStub(app, "GET", UiApiRoutes.StorageQualitySymbol);
        MapStub(app, "GET", UiApiRoutes.StorageQualityAlerts);
        MapStub(app, "POST", UiApiRoutes.StorageQualityAlertAcknowledge);
        MapStub(app, "GET", UiApiRoutes.StorageQualityRankings);
        MapStub(app, "GET", UiApiRoutes.StorageQualityTrends);
        MapStub(app, "GET", UiApiRoutes.StorageQualityAnomalies);
        MapStub(app, "POST", UiApiRoutes.StorageQualityCheck);

        // Diagnostics endpoints — now implemented in DiagnosticsEndpoints.cs

        // Admin/Maintenance endpoints
        MapStub(app, "GET", UiApiRoutes.AdminMaintenanceSchedule);
        MapStub(app, "POST", UiApiRoutes.AdminMaintenanceRun);
        MapStub(app, "GET", UiApiRoutes.AdminMaintenanceRunById);
        MapStub(app, "GET", UiApiRoutes.AdminMaintenanceHistory);
        MapStub(app, "GET", UiApiRoutes.AdminStorageTiers);
        MapStub(app, "POST", UiApiRoutes.AdminStorageMigrate);
        MapStub(app, "GET", UiApiRoutes.AdminStorageUsage);
        MapStub(app, "GET", UiApiRoutes.AdminRetention);
        MapStub(app, "DELETE", UiApiRoutes.AdminRetentionDelete);
        MapStub(app, "POST", UiApiRoutes.AdminRetentionApply);
        MapStub(app, "GET", UiApiRoutes.AdminCleanupPreview);
        MapStub(app, "POST", UiApiRoutes.AdminCleanupExecute);
        MapStub(app, "GET", UiApiRoutes.AdminStoragePermissions);
        MapStub(app, "POST", UiApiRoutes.AdminSelftest);
        MapStub(app, "GET", UiApiRoutes.AdminErrorCodes);
        MapStub(app, "GET", UiApiRoutes.AdminShowConfig);
        MapStub(app, "GET", UiApiRoutes.AdminQuickCheck);

        // Maintenance schedules
        MapStub(app, "GET", UiApiRoutes.MaintenanceSchedules);
        MapStub(app, "POST", UiApiRoutes.MaintenanceSchedules);
        MapStub(app, "GET", UiApiRoutes.MaintenanceSchedulesById);
        MapStub(app, "DELETE", UiApiRoutes.MaintenanceSchedulesDelete);
        MapStub(app, "POST", UiApiRoutes.MaintenanceSchedulesEnable);
        MapStub(app, "POST", UiApiRoutes.MaintenanceSchedulesDisable);
        MapStub(app, "POST", UiApiRoutes.MaintenanceSchedulesRun);
        MapStub(app, "GET", UiApiRoutes.MaintenanceSchedulesHistory);

        // Cron validation
        MapStub(app, "POST", UiApiRoutes.SchedulesCronValidate);
        MapStub(app, "POST", UiApiRoutes.SchedulesCronNextRuns);

        // Analytics endpoints
        MapStub(app, "GET", UiApiRoutes.AnalyticsGaps);
        MapStub(app, "POST", UiApiRoutes.AnalyticsGapsRepair);
        MapStub(app, "GET", UiApiRoutes.AnalyticsCompare);
        MapStub(app, "GET", UiApiRoutes.AnalyticsLatency);
        MapStub(app, "GET", UiApiRoutes.AnalyticsLatencyStats);
        MapStub(app, "GET", UiApiRoutes.AnalyticsAnomalies);
        MapStub(app, "GET", UiApiRoutes.AnalyticsQualityReport);
        MapStub(app, "GET", UiApiRoutes.AnalyticsCompleteness);
        MapStub(app, "GET", UiApiRoutes.AnalyticsThroughput);
        MapStub(app, "GET", UiApiRoutes.AnalyticsRateLimits);

        // System health endpoints
        MapStub(app, "GET", UiApiRoutes.HealthSummary);
        MapStub(app, "GET", UiApiRoutes.HealthProviders);
        MapStub(app, "GET", UiApiRoutes.HealthProviderDiagnostics);
        MapStub(app, "GET", UiApiRoutes.HealthStorage);
        MapStub(app, "GET", UiApiRoutes.HealthEvents);
        MapStub(app, "GET", UiApiRoutes.HealthMetrics);
        MapStub(app, "POST", UiApiRoutes.HealthProviderTest);
        MapStub(app, "GET", UiApiRoutes.HealthDiagnosticsBundle);

        // Messaging endpoints
        MapStub(app, "GET", UiApiRoutes.MessagingConfig);
        MapStub(app, "GET", UiApiRoutes.MessagingStatus);
        MapStub(app, "GET", UiApiRoutes.MessagingStats);
        MapStub(app, "GET", UiApiRoutes.MessagingActivity);
        MapStub(app, "GET", UiApiRoutes.MessagingConsumers);
        MapStub(app, "GET", UiApiRoutes.MessagingEndpoints);
        MapStub(app, "POST", UiApiRoutes.MessagingTest);
        MapStub(app, "GET", UiApiRoutes.MessagingPublishing);
        MapStub(app, "POST", UiApiRoutes.MessagingQueuePurge);
        MapStub(app, "GET", UiApiRoutes.MessagingErrors);
        MapStub(app, "POST", UiApiRoutes.MessagingErrorRetry);

        // Time series alignment
        MapStub(app, "POST", UiApiRoutes.AlignmentCreate);
        MapStub(app, "POST", UiApiRoutes.AlignmentPreview);

        // Sampling endpoints
        MapStub(app, "POST", UiApiRoutes.SamplingCreate);
        MapStub(app, "GET", UiApiRoutes.SamplingEstimate);
        MapStub(app, "GET", UiApiRoutes.SamplingSaved);
        MapStub(app, "GET", UiApiRoutes.SamplingById);

        // Live data endpoints — now implemented in LiveDataEndpoints.cs

        // Subscription endpoints
        MapStub(app, "GET", UiApiRoutes.SubscriptionsActive);
        MapStub(app, "POST", UiApiRoutes.SubscriptionsSubscribe);
        MapStub(app, "POST", UiApiRoutes.SubscriptionsUnsubscribe);

        // Replay endpoints
        MapStub(app, "GET", UiApiRoutes.ReplayFiles);
        MapStub(app, "POST", UiApiRoutes.ReplayStart);
        MapStub(app, "POST", UiApiRoutes.ReplayPause);
        MapStub(app, "POST", UiApiRoutes.ReplayResume);
        MapStub(app, "POST", UiApiRoutes.ReplayStop);
        MapStub(app, "POST", UiApiRoutes.ReplaySeek);
        MapStub(app, "POST", UiApiRoutes.ReplaySpeed);
        MapStub(app, "GET", UiApiRoutes.ReplayStatus);
        MapStub(app, "GET", UiApiRoutes.ReplayPreview);
        MapStub(app, "GET", UiApiRoutes.ReplayStats);

        // Export endpoints
        MapStub(app, "POST", UiApiRoutes.ExportAnalysis);
        MapStub(app, "GET", UiApiRoutes.ExportFormats);
        MapStub(app, "POST", UiApiRoutes.ExportQualityReport);
        MapStub(app, "POST", UiApiRoutes.ExportOrderflow);
        MapStub(app, "POST", UiApiRoutes.ExportIntegrity);
        MapStub(app, "POST", UiApiRoutes.ExportResearchPackage);

        // Lean integration endpoints
        MapStub(app, "GET", UiApiRoutes.LeanStatus);
        MapStub(app, "GET", UiApiRoutes.LeanConfig);
        MapStub(app, "POST", UiApiRoutes.LeanVerify);
        MapStub(app, "GET", UiApiRoutes.LeanAlgorithms);
        MapStub(app, "POST", UiApiRoutes.LeanSync);
        MapStub(app, "GET", UiApiRoutes.LeanSyncStatus);
        MapStub(app, "POST", UiApiRoutes.LeanBacktestStart);
        MapStub(app, "GET", UiApiRoutes.LeanBacktestStatus);
        MapStub(app, "GET", UiApiRoutes.LeanBacktestResults);
        MapStub(app, "POST", UiApiRoutes.LeanBacktestStop);
        MapStub(app, "GET", UiApiRoutes.LeanBacktestHistory);
        MapStub(app, "DELETE", UiApiRoutes.LeanBacktestDelete);

        // Index endpoints
        MapStub(app, "GET", UiApiRoutes.IndicesConstituents);

        return app;
    }

    private static void MapStub(WebApplication app, string method, string route)
    {
        var handler = (HttpContext ctx) =>
        {
            var response = new
            {
                error = "Not yet implemented",
                route = ctx.Request.Path.Value,
                planned = true
            };
            return Results.Json(response, s_jsonOptions, statusCode: StatusCodes.Status501NotImplemented);
        };

        switch (method)
        {
            case "GET":
                app.MapGet(route, handler);
                break;
            case "POST":
                app.MapPost(route, handler);
                break;
            case "DELETE":
                app.MapDelete(route, handler);
                break;
            case "PUT":
                app.MapPut(route, handler);
                break;
        }
    }
}
