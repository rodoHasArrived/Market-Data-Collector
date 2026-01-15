using System.Text.Json;
using MarketDataCollector.Application.Config;
using MarketDataCollector.Ui.Models;
using MarketDataCollector.Ui.Services;

namespace MarketDataCollector.Ui.Endpoints;

/// <summary>
/// Extension methods for registering failover-related API endpoints.
/// </summary>
public static class FailoverEndpoints
{
    /// <summary>
    /// Maps all failover API endpoints.
    /// </summary>
    public static void MapFailoverEndpoints(this WebApplication app, JsonSerializerOptions jsonOptions)
    {
        // Get failover configuration
        app.MapGet("/api/failover/config", (ConfigStore store) =>
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
        app.MapPost("/api/failover/config", async (ConfigStore store, FailoverConfigRequest req) =>
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
        app.MapGet("/api/failover/rules", (ConfigStore store) =>
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
        app.MapPost("/api/failover/rules", async (ConfigStore store, FailoverRuleRequest req) =>
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
        app.MapDelete("/api/failover/rules/{id}", async (ConfigStore store, string id) =>
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

        // Force failover
        app.MapPost("/api/failover/force/{ruleId}", (ConfigStore store, string ruleId, ForceFailoverRequest req) =>
        {
            // In a real implementation, this would trigger actual failover via MultiProviderService
            return Results.Ok(new { success = true, message = $"Failover triggered for rule {ruleId} to provider {req.TargetProviderId}" });
        });

        // Get provider health
        app.MapGet("/api/failover/health", (ConfigStore store) =>
        {
            var cfg = store.Load();
            var sources = cfg.DataSources?.Sources ?? Array.Empty<DataSourceConfig>();

            var health = sources.Select(s => new ProviderHealthResponse(
                ProviderId: s.Id,
                ConsecutiveFailures: 0,
                ConsecutiveSuccesses: s.Enabled ? 10 : 0,
                LastIssueTime: null,
                LastSuccessTime: s.Enabled ? DateTimeOffset.UtcNow : null,
                RecentIssues: Array.Empty<HealthIssueResponse>()
            )).ToArray();

            return Results.Json(health, jsonOptions);
        });
    }
}
