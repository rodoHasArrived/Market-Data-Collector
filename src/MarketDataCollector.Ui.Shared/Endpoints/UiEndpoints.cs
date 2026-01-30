using System.Text.Json;
using MarketDataCollector.Application.UI;
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
    #region Consolidated Host Setup

    /// <summary>
    /// Configures the application with all UI services and endpoints.
    /// This is the single entry point for setting up the UI host and should be used
    /// instead of calling AddUiSharedServices and MapAllUiEndpoints separately.
    /// </summary>
    /// <param name="builder">The web application builder.</param>
    /// <returns>A configured WebApplication ready to run.</returns>
    public static WebApplication BuildUiHost(this WebApplicationBuilder builder)
    {
        builder.Services.AddUiSharedServices();
        var app = builder.Build();
        app.MapAllUiEndpoints();
        return app;
    }

    /// <summary>
    /// Configures the application with UI services, endpoints, and shared status handlers.
    /// This overload allows sharing StatusEndpointHandlers with StatusHttpServer.
    /// </summary>
    /// <param name="builder">The web application builder.</param>
    /// <param name="statusHandlers">Pre-configured status endpoint handlers to share.</param>
    /// <returns>A configured WebApplication ready to run.</returns>
    public static WebApplication BuildUiHost(this WebApplicationBuilder builder, StatusEndpointHandlers statusHandlers)
    {
        builder.Services.AddUiSharedServices(statusHandlers);
        var app = builder.Build();
        app.MapUiEndpointsWithStatus(statusHandlers);
        return app;
    }

    #endregion

    #region Service Registration

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
    /// Registers shared services with a pre-configured StatusEndpointHandlers instance.
    /// Use this when you want to share the same handlers with StatusHttpServer.
    /// </summary>
    public static IServiceCollection AddUiSharedServices(this IServiceCollection services, StatusEndpointHandlers statusHandlers)
    {
        services.AddSingleton<ConfigStore>();
        services.AddSingleton<BackfillCoordinator>();
        services.AddSingleton(statusHandlers);
        return services;
    }

    #endregion

    #region Endpoint Mapping

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
    /// Maps all UI API endpoints including status endpoints.
    /// Use this when StatusEndpointHandlers has been registered in DI.
    /// </summary>
    public static WebApplication MapUiEndpointsWithStatus(this WebApplication app, StatusEndpointHandlers statusHandlers)
    {
        var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var jsonOptionsIndented = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        // Map status endpoints using shared handlers
        app.MapStatusEndpoints(statusHandlers, jsonOptions);

        // Map all other endpoint groups
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

    #endregion
}
