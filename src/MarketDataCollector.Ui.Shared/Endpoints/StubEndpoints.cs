using System.Text.Json;
using MarketDataCollector.Contracts.Api;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace MarketDataCollector.Ui.Shared.Endpoints;

/// <summary>
/// Registers any remaining unimplemented API routes with a 501 Not Implemented response.
/// Prevents clients from receiving unexplained 404/405 errors for declared routes.
/// As of Phase 3B completion, all declared routes have been implemented.
/// This class is retained for future route declarations that are not yet backed by handlers.
/// </summary>
public static class StubEndpoints
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Maps any remaining unimplemented routes with 501 responses.
    /// Currently empty — all declared routes have been implemented.
    /// </summary>
    public static WebApplication MapStubEndpoints(this WebApplication app)
    {
        // All previously stubbed routes are now implemented:
        // - Backfill schedule/utility endpoints → BackfillScheduleEndpoints.cs
        // - Provider extended endpoints → ProviderExtendedEndpoints.cs
        // - Diagnostics endpoints → DiagnosticsEndpoints.cs
        // - Admin/Maintenance endpoints → AdminEndpoints.cs
        // - Maintenance schedule endpoints → MaintenanceScheduleEndpoints.cs
        // - Cron validation endpoints → CronEndpoints.cs
        // - Analytics endpoints → AnalyticsEndpoints.cs
        // - System health endpoints → HealthEndpoints.cs
        // - Messaging endpoints → MessagingEndpoints.cs
        // - Time series alignment endpoints → AlignmentEndpoints.cs
        // - Sampling endpoints → SamplingEndpoints.cs
        // - Subscription endpoints → SubscriptionEndpoints.cs
        // - Replay endpoints → ReplayEndpoints.cs
        // - Export endpoints → ExportEndpoints.cs
        // - Lean integration endpoints → LeanEndpoints.cs
        // - Index endpoints → IndexEndpoints.cs
        // - Symbol endpoints → SymbolEndpoints.cs
        // - Storage endpoints → StorageEndpoints.cs
        // - Storage quality endpoints → StorageQualityEndpoints.cs
        // - Live data endpoints → LiveDataEndpoints.cs

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
