using System.Text.Json;
using MarketDataCollector.Ui.Shared.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace MarketDataCollector.Ui.Shared.Endpoints;

/// <summary>
/// Master extension methods for registering all UI API endpoints.
/// Provides a single entry point for mapping all shared endpoints.
/// </summary>
public static class UiEndpoints
{
    /// <summary>
    /// Registers all shared services required by UI endpoints.
    /// </summary>
    public static IServiceCollection AddUiSharedServices(this IServiceCollection services)
    {
        services.AddSingleton<ConfigStore>();
        services.AddSingleton<BackfillCoordinator>();
        return services;
    }

    /// <summary>
    /// Maps all UI API endpoints using default JSON serializer options.
    /// </summary>
    public static WebApplication MapUiEndpoints(this WebApplication app)
    {
        var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var jsonOptionsIndented = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        return app.MapUiEndpoints(jsonOptions, jsonOptionsIndented);
    }

    /// <summary>
    /// Maps all UI API endpoints with custom JSON serializer options.
    /// </summary>
    public static WebApplication MapUiEndpoints(this WebApplication app, JsonSerializerOptions jsonOptions, JsonSerializerOptions? jsonOptionsIndented = null)
    {
        jsonOptionsIndented ??= new JsonSerializerOptions
        {
            PropertyNamingPolicy = jsonOptions.PropertyNamingPolicy,
            WriteIndented = true
        };

        // Map all endpoint groups
        app.MapConfigEndpoints(jsonOptions);
        app.MapBackfillEndpoints(jsonOptions, jsonOptionsIndented);
        app.MapProviderEndpoints(jsonOptions);
        app.MapFailoverEndpoints(jsonOptions);
        app.MapSymbolMappingEndpoints(jsonOptions);

        return app;
    }

    /// <summary>
    /// Maps the dashboard HTML endpoint at the root path.
    /// </summary>
    public static WebApplication MapDashboard(this WebApplication app)
    {
        app.MapGet("/", (ConfigStore store) =>
        {
            var html = HtmlTemplates.Index(store.ConfigPath, store.GetStatusPath(), store.GetBackfillStatusPath());
            return Results.Content(html, "text/html");
        });

        return app;
    }

    /// <summary>
    /// Maps all UI endpoints including the dashboard.
    /// Convenience method that combines MapUiEndpoints and MapDashboard.
    /// </summary>
    public static WebApplication MapAllUiEndpoints(this WebApplication app)
    {
        app.MapDashboard();
        app.MapUiEndpoints();
        return app;
    }
}
