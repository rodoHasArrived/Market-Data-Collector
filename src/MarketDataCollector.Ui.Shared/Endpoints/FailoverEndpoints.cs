using System.Text.Json;
using MarketDataCollector.Application.Config;
using MarketDataCollector.Contracts.Api;
using MarketDataCollector.Ui.Shared.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace MarketDataCollector.Ui.Shared.Endpoints;

/// <summary>
/// Extension methods for registering failover-related API endpoints.
/// Shared between web dashboard and desktop application hosts.
/// </summary>
public static class FailoverEndpoints
{
    /// <summary>
    /// Maps all failover API endpoints.
    /// </summary>
    public static void MapFailoverEndpoints(this WebApplication app, JsonSerializerOptions jsonOptions)
    {
        // Get failover configuration
        app.MapGet(UiApiRoutes.FailoverConfig, (ConfigStore store) =>
        {
            var cfg = store.Load();
            var dataSources = cfg.DataSources ?? new DataSourcesConfig();

            var response = new FailoverConfigResponse(
                EnableFailover: dataSources.EnableFailover,
                HealthCheckIntervalSeconds: dataSources.HealthCheckIntervalSeconds,
                AutoRecover: dataSources.AutoRecover,
                FailoverTimeoutSeconds: dataSources.FailoverTimeoutSeconds,
                Rules: (dataSources.FailoverRules ?? Array.Empty<FailoverRuleConfig>())
                    .Select(r => new FailoverRuleResponse(
                        Id: r.Id,
                        PrimaryProviderId: r.PrimaryProviderId,
                        BackupProviderIds: r.BackupProviderIds,
                        FailoverThreshold: r.FailoverThreshold,
                        RecoveryThreshold: r.RecoveryThreshold,
                        DataQualityThreshold: r.DataQualityThreshold,
                        MaxLatencyMs: r.MaxLatencyMs,
                        IsInFailoverState: false,
                        CurrentActiveProviderId: r.PrimaryProviderId
                    )).ToArray()
            );

            return Results.Json(response, jsonOptions);
        });

        // Update failover configuration
        app.MapPost(UiApiRoutes.FailoverConfig, async (ConfigStore store, FailoverConfigRequest req) =>
        {
            var cfg = store.Load();
            var dataSources = cfg.DataSources ?? new DataSourcesConfig();

            var next = cfg with
            {
                DataSources = dataSources with
                {
                    EnableFailover = req.EnableFailover,
                    HealthCheckIntervalSeconds = req.HealthCheckIntervalSeconds,
                    AutoRecover = req.AutoRecover,
                    FailoverTimeoutSeconds = req.FailoverTimeoutSeconds
                }
            };
            await store.SaveAsync(next);

            return Results.Ok();
        });

        // Get all failover rules
        app.MapGet(UiApiRoutes.FailoverRules, (ConfigStore store) =>
        {
            var cfg = store.Load();
            var rules = cfg.DataSources?.FailoverRules ?? Array.Empty<FailoverRuleConfig>();

            var response = rules.Select(r => new FailoverRuleResponse(
                Id: r.Id,
                PrimaryProviderId: r.PrimaryProviderId,
                BackupProviderIds: r.BackupProviderIds,
                FailoverThreshold: r.FailoverThreshold,
                RecoveryThreshold: r.RecoveryThreshold,
                DataQualityThreshold: r.DataQualityThreshold,
                MaxLatencyMs: r.MaxLatencyMs,
                IsInFailoverState: false,
                CurrentActiveProviderId: r.PrimaryProviderId
            )).ToArray();

            return Results.Json(response, jsonOptions);
        });

        // Create or update failover rule
        app.MapPost(UiApiRoutes.FailoverRules, async (ConfigStore store, FailoverRuleRequest req) =>
        {
            if (string.IsNullOrWhiteSpace(req.PrimaryProviderId))
                return Results.BadRequest("PrimaryProviderId is required.");

            if (req.BackupProviderIds is null || req.BackupProviderIds.Length == 0)
                return Results.BadRequest("At least one backup provider is required.");

            var cfg = store.Load();
            var dataSources = cfg.DataSources ?? new DataSourcesConfig();
            var rules = (dataSources.FailoverRules ?? Array.Empty<FailoverRuleConfig>()).ToList();

            var id = string.IsNullOrWhiteSpace(req.Id) ? Guid.NewGuid().ToString("N") : req.Id;
            var rule = new FailoverRuleConfig(
                Id: id,
                PrimaryProviderId: req.PrimaryProviderId,
                BackupProviderIds: req.BackupProviderIds,
                FailoverThreshold: req.FailoverThreshold,
                RecoveryThreshold: req.RecoveryThreshold,
                DataQualityThreshold: req.DataQualityThreshold,
                MaxLatencyMs: req.MaxLatencyMs
            );

            var idx = rules.FindIndex(r => string.Equals(r.Id, id, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0) rules[idx] = rule;
            else rules.Add(rule);

            var next = cfg with { DataSources = dataSources with { FailoverRules = rules.ToArray() } };
            await store.SaveAsync(next);

            return Results.Ok(new { id });
        });

        // Delete failover rule
        app.MapDelete(UiApiRoutes.FailoverRules + "/{id}", async (ConfigStore store, string id) =>
        {
            var cfg = store.Load();
            var dataSources = cfg.DataSources ?? new DataSourcesConfig();
            var rules = (dataSources.FailoverRules ?? Array.Empty<FailoverRuleConfig>()).ToList();

            var removed = rules.RemoveAll(r => string.Equals(r.Id, id, StringComparison.OrdinalIgnoreCase)) > 0;
            if (!removed)
                return Results.NotFound();

            var next = cfg with { DataSources = dataSources with { FailoverRules = rules.ToArray() } };
            await store.SaveAsync(next);

            return Results.Ok();
        });

        // Force failover — not yet wired to runtime MultiProviderService
        app.MapPost(UiApiRoutes.FailoverForce.Replace("{ruleId}", "{ruleId}"), (ConfigStore store, string ruleId, ForceFailoverRequest req) =>
        {
            // Validate the rule exists
            var cfg = store.Load();
            var rules = cfg.DataSources?.FailoverRules ?? Array.Empty<FailoverRuleConfig>();
            var rule = rules.FirstOrDefault(r => string.Equals(r.Id, ruleId, StringComparison.OrdinalIgnoreCase));

            if (rule is null)
                return Results.NotFound(new { error = $"Failover rule '{ruleId}' not found." });

            if (string.IsNullOrWhiteSpace(req.TargetProviderId))
                return Results.BadRequest(new { error = "TargetProviderId is required." });

            // Return honest status — failover coordination is not yet wired at runtime
            return Results.Json(new
            {
                success = false,
                implemented = false,
                message = $"Failover rule '{ruleId}' found, but runtime failover coordination is not yet connected. " +
                          $"Configure failover rules and enable automatic failover for production use.",
                ruleId,
                targetProviderId = req.TargetProviderId
            }, jsonOptions);
        });

        // Get provider health — returns honest data when runtime metrics unavailable
        app.MapGet(UiApiRoutes.FailoverHealth, (ConfigStore store) =>
        {
            var cfg = store.Load();
            var sources = cfg.DataSources?.Sources ?? Array.Empty<DataSourceConfig>();
            var metricsStatus = store.TryLoadProviderMetrics();

            var health = sources.Select(s =>
            {
                // Check if we have real metrics for this provider
                var realMetrics = metricsStatus?.Providers.FirstOrDefault(p =>
                    string.Equals(p.ProviderId, s.Id, StringComparison.OrdinalIgnoreCase));

                var hasRealData = realMetrics is not null;

                return new
                {
                    providerId = s.Id,
                    consecutiveFailures = hasRealData ? realMetrics!.ConnectionFailures : 0L,
                    consecutiveSuccesses = hasRealData ? (realMetrics!.ConnectionAttempts - realMetrics.ConnectionFailures) : 0L,
                    lastIssueTime = (DateTimeOffset?)null,
                    lastSuccessTime = hasRealData ? realMetrics!.Timestamp : (DateTimeOffset?)null,
                    recentIssues = Array.Empty<object>(),
                    isSimulated = !hasRealData
                };
            }).ToArray();

            return Results.Json(health, jsonOptions);
        });
    }
}
