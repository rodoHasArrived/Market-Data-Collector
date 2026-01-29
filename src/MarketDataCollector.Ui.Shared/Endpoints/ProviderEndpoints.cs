using System.Text.Json;
using MarketDataCollector.Application.Config;
using MarketDataCollector.Contracts.Api;
using MarketDataCollector.Contracts.Configuration;
using MarketDataCollector.Ui.Shared;
using MarketDataCollector.Ui.Shared.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace MarketDataCollector.Ui.Shared.Endpoints;

/// <summary>
/// Extension methods for registering provider-related API endpoints.
/// Shared between web dashboard and desktop application hosts.
/// </summary>
public static class ProviderEndpoints
{
    /// <summary>
    /// Maps all provider and data source API endpoints.
    /// </summary>
    public static void MapProviderEndpoints(this WebApplication app, JsonSerializerOptions jsonOptions)
    {
        // Get all data sources
        app.MapGet(UiApiRoutes.ConfigDataSources, (ConfigStore store) =>
        {
            var cfg = store.Load();
            return Results.Json(new
            {
                sources = cfg.DataSources?.Sources ?? Array.Empty<DataSourceConfig>(),
                defaultRealTimeSourceId = cfg.DataSources?.DefaultRealTimeSourceId,
                defaultHistoricalSourceId = cfg.DataSources?.DefaultHistoricalSourceId,
                enableFailover = cfg.DataSources?.EnableFailover ?? true,
                failoverTimeoutSeconds = cfg.DataSources?.FailoverTimeoutSeconds ?? 30
            }, jsonOptions);
        });

        // Create or update data source
        app.MapPost(UiApiRoutes.ConfigDataSources, async (ConfigStore store, DataSourceConfigRequest req) =>
        {
            if (string.IsNullOrWhiteSpace(req.Name))
                return Results.BadRequest("Name is required.");

            var cfg = store.Load();
            var dataSources = cfg.DataSources ?? new DataSourcesConfig();
            var sources = (dataSources.Sources ?? Array.Empty<DataSourceConfig>()).ToList();

            var id = string.IsNullOrWhiteSpace(req.Id) ? Guid.NewGuid().ToString("N") : req.Id;
            var source = new DataSourceConfig(
                Id: id,
                Name: req.Name,
                Provider: Enum.TryParse<DataSourceKind>(req.Provider, ignoreCase: true, out var p) ? p : DataSourceKind.IB,
                Enabled: req.Enabled,
                Type: Enum.TryParse<DataSourceType>(req.Type, ignoreCase: true, out var t) ? t : DataSourceType.RealTime,
                Priority: req.Priority,
                Alpaca: req.Alpaca?.ToDomain(),
                Polygon: req.Polygon?.ToDomain(),
                IB: req.IB?.ToDomain(),
                Symbols: req.Symbols,
                Description: req.Description,
                Tags: req.Tags
            );

            var idx = sources.FindIndex(s => string.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0) sources[idx] = source;
            else sources.Add(source);

            var next = cfg with { DataSources = dataSources with { Sources = sources.ToArray() } };
            await store.SaveAsync(next);

            return Results.Ok(new { id });
        });

        // Delete data source
        app.MapDelete(UiApiRoutes.ConfigDataSources + "/{id}", async (ConfigStore store, string id) =>
        {
            var cfg = store.Load();
            var dataSources = cfg.DataSources ?? new DataSourcesConfig();
            var sources = (dataSources.Sources ?? Array.Empty<DataSourceConfig>()).ToList();

            sources.RemoveAll(s => string.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase));

            var next = cfg with { DataSources = dataSources with { Sources = sources.ToArray() } };
            await store.SaveAsync(next);

            return Results.Ok();
        });

        // Toggle data source enabled status
        app.MapPost(UiApiRoutes.ConfigDataSources + "/{id}/toggle", async (ConfigStore store, string id, ToggleRequest req) =>
        {
            var cfg = store.Load();
            var dataSources = cfg.DataSources ?? new DataSourcesConfig();
            var sources = (dataSources.Sources ?? Array.Empty<DataSourceConfig>()).ToList();

            var source = sources.FirstOrDefault(s => string.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase));
            if (source == null)
                return Results.NotFound();

            var idx = sources.IndexOf(source);
            sources[idx] = source with { Enabled = req.Enabled };

            var next = cfg with { DataSources = dataSources with { Sources = sources.ToArray() } };
            await store.SaveAsync(next);

            return Results.Ok();
        });

        // Set default data sources
        app.MapPost(UiApiRoutes.ConfigDataSourcesDefaults, async (ConfigStore store, DefaultSourcesRequest req) =>
        {
            var cfg = store.Load();
            var dataSources = cfg.DataSources ?? new DataSourcesConfig();

            var next = cfg with
            {
                DataSources = dataSources with
                {
                    DefaultRealTimeSourceId = req.DefaultRealTimeSourceId,
                    DefaultHistoricalSourceId = req.DefaultHistoricalSourceId
                }
            };
            await store.SaveAsync(next);

            return Results.Ok();
        });

        // Update failover settings
        app.MapPost(UiApiRoutes.ConfigDataSourcesFailover, async (ConfigStore store, FailoverSettingsRequest req) =>
        {
            var cfg = store.Load();
            var dataSources = cfg.DataSources ?? new DataSourcesConfig();

            var next = cfg with
            {
                DataSources = dataSources with
                {
                    EnableFailover = req.EnableFailover,
                    FailoverTimeoutSeconds = req.FailoverTimeoutSeconds
                }
            };
            await store.SaveAsync(next);

            return Results.Ok();
        });

        // Provider comparison view
        app.MapGet(UiApiRoutes.ProviderComparison, (ConfigStore store) =>
        {
            var metricsStatus = store.TryLoadProviderMetrics();

            if (metricsStatus is not null)
            {
                var providers = metricsStatus.Providers.Select(p => new ProviderMetricsResponse(
                    ProviderId: p.ProviderId,
                    ProviderType: p.ProviderType,
                    TradesReceived: p.TradesReceived,
                    DepthUpdatesReceived: p.DepthUpdatesReceived,
                    QuotesReceived: p.QuotesReceived,
                    ConnectionAttempts: p.ConnectionAttempts,
                    ConnectionFailures: p.ConnectionFailures,
                    MessagesDropped: p.MessagesDropped,
                    ActiveSubscriptions: p.ActiveSubscriptions,
                    AverageLatencyMs: p.AverageLatencyMs,
                    MinLatencyMs: p.MinLatencyMs,
                    MaxLatencyMs: p.MaxLatencyMs,
                    DataQualityScore: p.DataQualityScore,
                    ConnectionSuccessRate: p.ConnectionSuccessRate,
                    Timestamp: p.Timestamp
                )).ToArray();

                var comparison = new ProviderComparisonResponse(
                    Timestamp: metricsStatus.Timestamp,
                    Providers: providers,
                    TotalProviders: metricsStatus.TotalProviders,
                    HealthyProviders: metricsStatus.HealthyProviders
                );
                return Results.Json(comparison, jsonOptions);
            }

            // Fallback to configuration-based data
            var cfg = store.Load();
            var sources = cfg.DataSources?.Sources ?? Array.Empty<DataSourceConfig>();
            var fallbackProviders = sources.Select(s => CreateFallbackMetrics(s)).ToArray();

            var fallbackComparison = new ProviderComparisonResponse(
                Timestamp: DateTimeOffset.UtcNow,
                Providers: fallbackProviders,
                TotalProviders: sources.Length,
                HealthyProviders: sources.Count(s => s.Enabled)
            );
            return Results.Json(fallbackComparison, jsonOptions);
        });

        // Provider status
        app.MapGet(UiApiRoutes.ProviderStatus, (ConfigStore store) =>
        {
            var cfg = store.Load();
            var sources = cfg.DataSources?.Sources ?? Array.Empty<DataSourceConfig>();
            var metricsStatus = store.TryLoadProviderMetrics();

            var status = sources.Select(s =>
            {
                var realMetrics = metricsStatus?.Providers.FirstOrDefault(p =>
                    string.Equals(p.ProviderId, s.Id, StringComparison.OrdinalIgnoreCase));

                return new ProviderStatusResponse(
                    ProviderId: s.Id,
                    Name: s.Name,
                    ProviderType: s.Provider.ToString(),
                    IsConnected: realMetrics?.IsConnected ?? s.Enabled,
                    IsEnabled: s.Enabled,
                    Priority: s.Priority,
                    ActiveSubscriptions: (int)(realMetrics?.ActiveSubscriptions ?? 0),
                    LastHeartbeat: realMetrics?.Timestamp ?? DateTimeOffset.UtcNow
                );
            }).ToArray();

            return Results.Json(status, jsonOptions);
        });

        // Provider metrics
        app.MapGet(UiApiRoutes.ProviderMetrics, (ConfigStore store) =>
        {
            var metricsStatus = store.TryLoadProviderMetrics();

            if (metricsStatus is not null)
            {
                var metrics = metricsStatus.Providers.Select(p => new ProviderMetricsResponse(
                    ProviderId: p.ProviderId,
                    ProviderType: p.ProviderType,
                    TradesReceived: p.TradesReceived,
                    DepthUpdatesReceived: p.DepthUpdatesReceived,
                    QuotesReceived: p.QuotesReceived,
                    ConnectionAttempts: p.ConnectionAttempts,
                    ConnectionFailures: p.ConnectionFailures,
                    MessagesDropped: p.MessagesDropped,
                    ActiveSubscriptions: p.ActiveSubscriptions,
                    AverageLatencyMs: p.AverageLatencyMs,
                    MinLatencyMs: p.MinLatencyMs,
                    MaxLatencyMs: p.MaxLatencyMs,
                    DataQualityScore: p.DataQualityScore,
                    ConnectionSuccessRate: p.ConnectionSuccessRate,
                    Timestamp: p.Timestamp
                )).ToArray();
                return Results.Json(metrics, jsonOptions);
            }

            // Fallback to configuration-based placeholder data
            var cfg = store.Load();
            var sources = cfg.DataSources?.Sources ?? Array.Empty<DataSourceConfig>();
            var fallbackMetrics = sources.Select(s => CreateFallbackMetrics(s)).ToArray();

            return Results.Json(fallbackMetrics, jsonOptions);
        });

        // Single provider metrics
        app.MapGet(UiApiRoutes.ProviderMetrics + "/{providerId}", (ConfigStore store, string providerId) =>
        {
            var metricsStatus = store.TryLoadProviderMetrics();
            var providerMetrics = metricsStatus?.Providers.FirstOrDefault(p =>
                string.Equals(p.ProviderId, providerId, StringComparison.OrdinalIgnoreCase));

            if (providerMetrics is not null)
            {
                var metrics = new ProviderMetricsResponse(
                    ProviderId: providerMetrics.ProviderId,
                    ProviderType: providerMetrics.ProviderType,
                    TradesReceived: providerMetrics.TradesReceived,
                    DepthUpdatesReceived: providerMetrics.DepthUpdatesReceived,
                    QuotesReceived: providerMetrics.QuotesReceived,
                    ConnectionAttempts: providerMetrics.ConnectionAttempts,
                    ConnectionFailures: providerMetrics.ConnectionFailures,
                    MessagesDropped: providerMetrics.MessagesDropped,
                    ActiveSubscriptions: providerMetrics.ActiveSubscriptions,
                    AverageLatencyMs: providerMetrics.AverageLatencyMs,
                    MinLatencyMs: providerMetrics.MinLatencyMs,
                    MaxLatencyMs: providerMetrics.MaxLatencyMs,
                    DataQualityScore: providerMetrics.DataQualityScore,
                    ConnectionSuccessRate: providerMetrics.ConnectionSuccessRate,
                    Timestamp: providerMetrics.Timestamp
                );
                return Results.Json(metrics, jsonOptions);
            }

            // Fallback
            var cfg = store.Load();
            var source = cfg.DataSources?.Sources?.FirstOrDefault(s =>
                string.Equals(s.Id, providerId, StringComparison.OrdinalIgnoreCase));

            if (source == null)
                return Results.NotFound();

            return Results.Json(CreateFallbackMetrics(source), jsonOptions);
        });
    }

    private static ProviderMetricsResponse CreateFallbackMetrics(DataSourceConfig source) => new(
        ProviderId: source.Id,
        ProviderType: source.Provider.ToString(),
        TradesReceived: 0,
        DepthUpdatesReceived: 0,
        QuotesReceived: 0,
        ConnectionAttempts: 0,
        ConnectionFailures: 0,
        MessagesDropped: 0,
        ActiveSubscriptions: 0,
        AverageLatencyMs: 0,
        MinLatencyMs: 0,
        MaxLatencyMs: 0,
        DataQualityScore: 100,
        ConnectionSuccessRate: 100,
        Timestamp: DateTimeOffset.UtcNow
    );
}
