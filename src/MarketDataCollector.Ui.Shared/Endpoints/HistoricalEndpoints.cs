using System.Text.Json;
using MarketDataCollector.Application.Services;
using MarketDataCollector.Contracts.Api;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace MarketDataCollector.Ui.Shared.Endpoints;

/// <summary>
/// Extension methods for registering historical data query API endpoints.
/// Shared between web dashboard and desktop application hosts.
/// </summary>
public static class HistoricalEndpoints
{
    /// <summary>
    /// Maps all historical data query API endpoints.
    /// </summary>
    public static void MapHistoricalEndpoints(this WebApplication app, JsonSerializerOptions jsonOptions)
    {
        var group = app.MapGroup(UiApiRoutes.HistoricalData).WithTags("Historical");

        // Query historical data
        group.MapGet("", async (
            HttpContext context,
            HistoricalDataQueryService queryService,
            string symbol,
            DateOnly? from = null,
            DateOnly? to = null,
            string? dataType = null,
            int? skip = null,
            int? limit = null) =>
        {
            if (string.IsNullOrWhiteSpace(symbol))
            {
                return Results.BadRequest(new { error = "Symbol is required" });
            }

            var query = new HistoricalDataQuery(
                symbol.ToUpperInvariant(),
                from,
                to,
                dataType,
                skip,
                limit);

            try
            {
                var result = await queryService.QueryAsync(query, context.RequestAborted);
                return Results.Json(result, jsonOptions);
            }
            catch (OperationCanceledException)
            {
                return Results.StatusCode(499); // Client Closed Request
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("QueryHistoricalData")
        .Produces(200)
        .Produces(400)
        .Produces(499);

        // Get available symbols
        group.MapGet("/symbols", (HistoricalDataQueryService queryService) =>
        {
            try
            {
                var symbols = queryService.GetAvailableSymbols();
                return Results.Json(new { symbols, count = symbols.Count }, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("GetAvailableSymbols")
        .Produces(200)
        .Produces(400);

        // Get date range for a symbol
        group.MapGet("/{symbol}/daterange", (HistoricalDataQueryService queryService, string symbol) =>
        {
            if (string.IsNullOrWhiteSpace(symbol))
            {
                return Results.BadRequest(new { error = "Symbol is required" });
            }

            try
            {
                var dateRange = queryService.GetDateRange(symbol.ToUpperInvariant());
                return Results.Json(dateRange, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("GetSymbolDateRange")
        .Produces(200)
        .Produces(400);
    }
}
