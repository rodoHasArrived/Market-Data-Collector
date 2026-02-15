using System.Text.Json;
using MarketDataCollector.Application.Monitoring;
using MarketDataCollector.Application.Pipeline;
using MarketDataCollector.Application.Services;
using MarketDataCollector.Contracts.Api;
using MarketDataCollector.Infrastructure.Providers.Core;
using MarketDataCollector.Storage;
using MarketDataCollector.Storage.Services;
using MarketDataCollector.Storage.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace MarketDataCollector.Ui.Shared.Endpoints;

/// <summary>
/// Extension methods for registering system health API endpoints.
/// Provides deep health checks for providers, storage, events, and diagnostics.
/// </summary>
public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this WebApplication app, JsonSerializerOptions jsonOptions)
    {
        var group = app.MapGroup("").WithTags("Health");

        // Health summary (D7: OpenAPI typed annotations)
        group.MapGet(UiApiRoutes.HealthSummary, (
            [FromServices] StorageOptions? storageOptions,
            [FromServices] ProviderRegistry? registry,
            [FromServices] EventPipeline? pipeline) =>
        {
            var registrySummary = registry?.GetSummary();
            var summary = new HealthSummaryResponse
            {
                Timestamp = DateTimeOffset.UtcNow,
                Status = "operational",
                Providers = new HealthSummaryProviders
                {
                    Streaming = registrySummary?.StreamingCount ?? 0,
                    Backfill = registrySummary?.BackfillCount ?? 0,
                    SymbolSearch = registrySummary?.SymbolSearchCount ?? 0,
                    TotalEnabled = registrySummary?.TotalEnabled ?? 0
                },
                StorageHealthy = storageOptions != null,
                PipelineActive = pipeline != null
            };
            return Results.Json(summary, jsonOptions);
        })
        .WithName("GetHealthSummary")
        .WithDescription("Returns a summary of system health including provider counts, storage status, and pipeline state.")
        .Produces<HealthSummaryResponse>(200);

        // Provider health
        group.MapGet(UiApiRoutes.HealthProviders, ([FromServices] ProviderRegistry? registry) =>
        {
            if (registry is null)
                return Results.Json(new { providers = Array.Empty<object>() }, jsonOptions);

            var providers = registry.GetAllProviders().Select(p => new
            {
                name = p.Name,
                displayName = p.DisplayName,
                type = p.ProviderType.ToString(),
                priority = p.Priority,
                isEnabled = p.IsEnabled,
                capabilities = p.Capabilities
            });

            return Results.Json(new { providers, timestamp = DateTimeOffset.UtcNow }, jsonOptions);
        })
        .WithName("GetHealthProviders")
        .Produces(200);

        // Provider diagnostics
        group.MapGet(UiApiRoutes.HealthProviderDiagnostics, (string provider, [FromServices] ProviderRegistry? registry) =>
        {
            if (registry is null)
                return Results.NotFound(new { error = "Provider registry not available" });

            var info = registry.GetAllProviders()
                .FirstOrDefault(p => string.Equals(p.Name, provider, StringComparison.OrdinalIgnoreCase));

            if (info.Name is null)
                return Results.NotFound(new { error = $"Provider '{provider}' not found" });

            return Results.Json(new
            {
                provider = info.Name,
                displayName = info.DisplayName,
                type = info.ProviderType.ToString(),
                priority = info.Priority,
                isEnabled = info.IsEnabled,
                capabilities = info.Capabilities,
                timestamp = DateTimeOffset.UtcNow
            }, jsonOptions);
        })
        .WithName("GetHealthProviderDiagnostics")
        .Produces(200)
        .Produces(404);

        // Storage health
        group.MapGet(UiApiRoutes.HealthStorage, ([FromServices] StorageOptions? storageOptions, [FromServices] IFileMaintenanceService? fileMaint) =>
        {
            var rootPath = storageOptions?.RootPath ?? "data";
            var exists = Directory.Exists(rootPath);
            long totalBytes = 0;
            int fileCount = 0;

            if (exists)
            {
                try
                {
                    var dirInfo = new DirectoryInfo(rootPath);
                    var files = dirInfo.GetFiles("*", SearchOption.AllDirectories);
                    totalBytes = files.Sum(f => f.Length);
                    fileCount = files.Length;
                }
                catch { /* ignore access errors */ }
            }

            return Results.Json(new
            {
                rootPath,
                exists,
                totalBytes,
                fileCount,
                namingConvention = storageOptions?.NamingConvention.ToString() ?? "BySymbol",
                compressionEnabled = storageOptions?.Compress ?? false,
                timestamp = DateTimeOffset.UtcNow
            }, jsonOptions);
        })
        .WithName("GetHealthStorage")
        .Produces(200);

        // Event stream health
        group.MapGet(UiApiRoutes.HealthEvents, ([FromServices] IEventMetrics? metrics) =>
        {
            return Results.Json(new
            {
                metricsAvailable = metrics != null,
                timestamp = DateTimeOffset.UtcNow
            }, jsonOptions);
        })
        .WithName("GetHealthEvents")
        .Produces(200);

        // Health metrics
        group.MapGet(UiApiRoutes.HealthMetrics, ([FromServices] IEventMetrics? metrics, [FromServices] ErrorTracker? errorTracker) =>
        {
            var errors = errorTracker?.GetStatistics();
            return Results.Json(new
            {
                errors = errors != null ? new
                {
                    total = errors.TotalErrors,
                    inWindow = errors.ErrorsInWindow,
                    byType = errors.ByExceptionType
                } : null,
                timestamp = DateTimeOffset.UtcNow
            }, jsonOptions);
        })
        .WithName("GetHealthMetrics")
        .Produces(200);

        // Test provider connection
        group.MapPost(UiApiRoutes.HealthProviderTest, (string provider, [FromServices] ProviderRegistry? registry) =>
        {
            if (registry is null)
                return Results.Json(new { success = false, error = "Provider registry not available" }, jsonOptions);

            var info = registry.GetAllProviders()
                .FirstOrDefault(p => string.Equals(p.Name, provider, StringComparison.OrdinalIgnoreCase));

            if (info.Name is null)
                return Results.NotFound(new { error = $"Provider '{provider}' not found" });

            return Results.Json(new
            {
                provider = info.Name,
                isEnabled = info.IsEnabled,
                reachable = info.IsEnabled,
                timestamp = DateTimeOffset.UtcNow
            }, jsonOptions);
        })
        .WithName("TestHealthProvider")
        .Produces(200)
        .Produces(404)
        .RequireRateLimiting(UiEndpoints.MutationRateLimitPolicy);

        // Diagnostics bundle
        group.MapGet(UiApiRoutes.HealthDiagnosticsBundle, async ([FromServices] DiagnosticBundleService? bundleService, CancellationToken ct) =>
        {
            if (bundleService is null)
                return Results.Json(new { error = "Diagnostic bundle service not available" }, jsonOptions, statusCode: 503);

            var result = await bundleService.GenerateAsync(new DiagnosticBundleOptions(), ct);
            return Results.Json(new
            {
                bundleId = result.BundleId,
                success = result.Success,
                sizeBytes = result.SizeBytes,
                filesIncluded = result.FilesIncluded,
                path = result.ZipPath,
                message = result.Message
            }, jsonOptions);
        })
        .WithName("GetHealthDiagnosticsBundle")
        .Produces(200)
        .Produces(503);
    }
}
