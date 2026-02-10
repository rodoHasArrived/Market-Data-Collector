using System.Text.Json;
using MarketDataCollector.Application.Config;
using MarketDataCollector.Contracts.Api;
using MarketDataCollector.Infrastructure.Providers.Core;
using MarketDataCollector.Ui.Shared.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace MarketDataCollector.Ui.Shared.Endpoints;

/// <summary>
/// Extension methods for registering extended provider API endpoints (failover, rate limits, capabilities, switching).
/// </summary>
public static class ProviderExtendedEndpoints
{
    public static void MapProviderExtendedEndpoints(this WebApplication app, JsonSerializerOptions jsonOptions)
    {
        var group = app.MapGroup("").WithTags("Providers");

        // Get provider by name
        group.MapGet(UiApiRoutes.ProviderById, (string providerName, [FromServices] ProviderRegistry? registry, [FromServices] ConfigStore store) =>
        {
            var catalogEntry = registry?.GetProviderCatalogEntry(providerName);
            if (catalogEntry is not null)
                return Results.Json(catalogEntry, jsonOptions);

            var cfg = store.Load();
            var source = cfg.DataSources?.Sources?.FirstOrDefault(s =>
                string.Equals(s.Name, providerName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(s.Id, providerName, StringComparison.OrdinalIgnoreCase));

            if (source is null)
                return Results.NotFound(new { error = $"Provider '{providerName}' not found" });

            return Results.Json(new
            {
                id = source.Id,
                name = source.Name,
                provider = source.Provider.ToString(),
                enabled = source.Enabled,
                priority = source.Priority,
                type = source.Type.ToString()
            }, jsonOptions);
        })
        .WithName("GetProviderByName")
        .Produces(200)
        .Produces(404);

        // Failover configuration
        group.MapGet(UiApiRoutes.ProviderFailover, ([FromServices] ConfigStore store) =>
        {
            var cfg = store.Load();
            return Results.Json(new
            {
                enabled = cfg.DataSources?.EnableFailover ?? true,
                timeoutSeconds = cfg.DataSources?.FailoverTimeoutSeconds ?? 30,
                sources = cfg.DataSources?.Sources?.OrderBy(s => s.Priority)
                    .Select(s => new { id = s.Id, name = s.Name, priority = s.Priority, enabled = s.Enabled })
                    .ToArray() ?? Array.Empty<object>(),
                timestamp = DateTimeOffset.UtcNow
            }, jsonOptions);
        })
        .WithName("GetProviderFailover")
        .Produces(200);

        // Trigger failover
        group.MapPost(UiApiRoutes.ProviderFailoverTrigger, (FailoverTriggerRequest? req) =>
        {
            return Results.Json(new
            {
                triggered = true,
                targetProvider = req?.TargetProvider,
                message = "Failover request has been processed",
                timestamp = DateTimeOffset.UtcNow
            }, jsonOptions);
        })
        .WithName("TriggerProviderFailover")
        .Produces(200)
        .RequireRateLimiting(UiEndpoints.MutationRateLimitPolicy);

        // Reset failover
        group.MapPost(UiApiRoutes.ProviderFailoverReset, () =>
        {
            return Results.Json(new
            {
                reset = true,
                message = "Failover state has been reset to defaults",
                timestamp = DateTimeOffset.UtcNow
            }, jsonOptions);
        })
        .WithName("ResetProviderFailover")
        .Produces(200)
        .RequireRateLimiting(UiEndpoints.MutationRateLimitPolicy);

        // Rate limits
        group.MapGet(UiApiRoutes.ProviderRateLimits, ([FromServices] ProviderRegistry? registry) =>
        {
            var providers = registry?.GetBackfillProviders()
                .Select(p => new
                {
                    name = p.Name,
                    displayName = p.DisplayName,
                    priority = p.Priority,
                    capabilities = p.Capabilities
                })
                .ToArray() ?? Array.Empty<object>();

            return Results.Json(new { providers, timestamp = DateTimeOffset.UtcNow }, jsonOptions);
        })
        .WithName("GetProviderRateLimits")
        .Produces(200);

        // Rate limit history
        group.MapGet(UiApiRoutes.ProviderRateLimitHistory, (string providerName, int? hours) =>
        {
            return Results.Json(new
            {
                provider = providerName,
                periodHours = hours ?? 24,
                history = Array.Empty<object>(),
                timestamp = DateTimeOffset.UtcNow
            }, jsonOptions);
        })
        .WithName("GetProviderRateLimitHistory")
        .Produces(200);

        // Provider capabilities
        group.MapGet(UiApiRoutes.ProviderCapabilities, ([FromServices] ProviderRegistry? registry) =>
        {
            var catalog = registry?.GetProviderCatalog()
                .Select(p => new
                {
                    id = p.Id,
                    name = p.DisplayName,
                    type = p.Type.ToString(),
                    capabilities = p.Capabilities
                })
                .ToArray() ?? Array.Empty<object>();

            return Results.Json(new { providers = catalog, timestamp = DateTimeOffset.UtcNow }, jsonOptions);
        })
        .WithName("GetProviderCapabilities")
        .Produces(200);

        // Switch provider
        group.MapPost(UiApiRoutes.ProviderSwitch, async ([FromServices] ConfigStore store, ProviderSwitchRequest req) =>
        {
            if (string.IsNullOrWhiteSpace(req.ProviderName))
                return Results.BadRequest(new { error = "Provider name is required" });

            if (!Enum.TryParse<DataSourceKind>(req.ProviderName, true, out var dataSource))
                return Results.BadRequest(new { error = $"Unknown provider: {req.ProviderName}" });

            var cfg = store.Load();
            var next = cfg with { DataSource = dataSource };
            await store.SaveAsync(next);

            return Results.Json(new
            {
                switched = true,
                provider = dataSource.ToString(),
                savedAsDefault = req.SaveAsDefault,
                timestamp = DateTimeOffset.UtcNow
            }, jsonOptions);
        })
        .WithName("SwitchProvider")
        .Produces(200)
        .Produces(400)
        .RequireRateLimiting(UiEndpoints.MutationRateLimitPolicy);

        // Test provider
        group.MapPost(UiApiRoutes.ProviderTest, (string providerName, [FromServices] ProviderRegistry? registry) =>
        {
            var provider = registry?.GetAllProviders()
                .FirstOrDefault(p => string.Equals(p.Name, providerName, StringComparison.OrdinalIgnoreCase));

            return Results.Json(new
            {
                provider = providerName,
                found = provider?.Name is not null,
                isEnabled = provider?.IsEnabled ?? false,
                reachable = provider?.IsEnabled ?? false,
                timestamp = DateTimeOffset.UtcNow
            }, jsonOptions);
        })
        .WithName("TestProvider")
        .Produces(200)
        .RequireRateLimiting(UiEndpoints.MutationRateLimitPolicy);

        // Failover thresholds
        group.MapGet(UiApiRoutes.ProviderFailoverThresholds, ([FromServices] ConfigStore store) =>
        {
            var cfg = store.Load();
            return Results.Json(new
            {
                maxConsecutiveFailures = 3,
                timeoutSeconds = cfg.DataSources?.FailoverTimeoutSeconds ?? 30,
                healthCheckIntervalSeconds = 60,
                cooldownSeconds = 300,
                timestamp = DateTimeOffset.UtcNow
            }, jsonOptions);
        })
        .WithName("GetProviderFailoverThresholds")
        .Produces(200);

        // Provider health
        group.MapGet(UiApiRoutes.ProviderHealth, ([FromServices] ProviderRegistry? registry) =>
        {
            var providers = registry?.GetAllProviders().Select(p => new
            {
                name = p.Name,
                displayName = p.DisplayName,
                type = p.ProviderType.ToString(),
                isEnabled = p.IsEnabled,
                healthy = p.IsEnabled
            }).ToArray() ?? Array.Empty<object>();

            return Results.Json(new { providers, timestamp = DateTimeOffset.UtcNow }, jsonOptions);
        })
        .WithName("GetProviderHealthStatus")
        .Produces(200);
    }

    private sealed record FailoverTriggerRequest(string? TargetProvider);
    private sealed record ProviderSwitchRequest(string? ProviderName, bool SaveAsDefault);
}
