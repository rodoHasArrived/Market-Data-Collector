using System.Text.Json;
using MarketDataCollector.Application.Monitoring;
using MarketDataCollector.Application.UI;
using MarketDataCollector.Contracts.Api;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace MarketDataCollector.Ui.Shared.Endpoints;

/// <summary>
/// Extension methods for registering status and health API endpoints.
/// Shared between web dashboard and desktop application hosts.
/// Uses StatusEndpointHandlers for the actual response generation.
/// </summary>
public static class StatusEndpoints
{
    /// <summary>
    /// Maps all status and health API endpoints.
    /// </summary>
    public static void MapStatusEndpoints(this WebApplication app, StatusEndpointHandlers handlers, JsonSerializerOptions jsonOptions)
    {
        // Health check endpoint - comprehensive health status
        app.MapGet(UiApiRoutes.Health, () =>
        {
            var response = handlers.GetHealthCheck();
            var statusCode = handlers.GetHealthStatusCode(response);
            return Results.Json(response, jsonOptions, statusCode: statusCode);
        });

        // Kubernetes-style health endpoints
        app.MapGet("/healthz", () => Results.Ok("healthy"));

        // Readiness probe
        app.MapGet(UiApiRoutes.Ready, () =>
        {
            var (isReady, message) = handlers.CheckReadiness();
            return isReady ? Results.Ok(message) : Results.StatusCode(503);
        });

        app.MapGet("/readyz", () =>
        {
            var (isReady, message) = handlers.CheckReadiness();
            return isReady ? Results.Ok(message) : Results.StatusCode(503);
        });

        // Liveness probe
        app.MapGet(UiApiRoutes.Live, () => Results.Ok("alive"));
        app.MapGet("/livez", () => Results.Ok("alive"));

        // Prometheus metrics
        app.MapGet(UiApiRoutes.Metrics, () =>
        {
            var content = handlers.GetPrometheusMetrics();
            return Results.Content(content, "text/plain; version=0.0.4");
        });

        // Full status endpoint
        app.MapGet(UiApiRoutes.Status, () =>
        {
            var response = handlers.GetStatus();
            return Results.Json(response, jsonOptions);
        });

        // Errors endpoint with optional filtering
        app.MapGet(UiApiRoutes.Errors, (int? count, string? level, string? symbol) =>
        {
            var response = handlers.GetErrors(count ?? 10, level, symbol);
            return Results.Json(response, jsonOptions);
        });

        // Backpressure status
        app.MapGet(UiApiRoutes.Backpressure, () =>
        {
            var response = handlers.GetBackpressure();
            return Results.Json(response, jsonOptions);
        });

        // Provider latency
        app.MapGet(UiApiRoutes.ProvidersLatency, () =>
        {
            var (summary, error) = handlers.GetProviderLatency();
            if (error != null)
            {
                return Results.Json(new { error, providers = Array.Empty<object>() }, jsonOptions);
            }
            return Results.Json(summary, jsonOptions);
        });

        // Connection health
        app.MapGet(UiApiRoutes.Connections, () =>
        {
            var (snapshot, error) = handlers.GetConnectionHealth();
            if (error != null)
            {
                return Results.Json(new { error, connections = Array.Empty<object>() }, jsonOptions);
            }
            return Results.Json(snapshot, jsonOptions);
        });

        // Detailed health (async)
        app.MapGet(UiApiRoutes.HealthDetailed, async () =>
        {
            var (report, error) = await handlers.GetDetailedHealthAsync();
            if (error != null)
            {
                return Results.Json(new { error }, jsonOptions, statusCode: 501);
            }

            var statusCode = report!.Status switch
            {
                DetailedHealthStatus.Healthy => 200,
                DetailedHealthStatus.Degraded => 200,
                DetailedHealthStatus.Unhealthy => 503,
                _ => 200
            };
            return Results.Json(report, jsonOptions, statusCode: statusCode);
        });
    }
}
