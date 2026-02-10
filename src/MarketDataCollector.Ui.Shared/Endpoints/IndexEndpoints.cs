using System.Text.Json;
using MarketDataCollector.Contracts.Api;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace MarketDataCollector.Ui.Shared.Endpoints;

/// <summary>
/// Extension methods for registering index/constituent API endpoints.
/// </summary>
public static class IndexEndpoints
{
    public static void MapIndexEndpoints(this WebApplication app, JsonSerializerOptions jsonOptions)
    {
        var group = app.MapGroup("").WithTags("Indices");

        // Index constituents
        group.MapGet(UiApiRoutes.IndicesConstituents, (string indexName) =>
        {
            return Results.Json(new
            {
                index = indexName,
                constituents = Array.Empty<object>(),
                message = $"Index '{indexName}' constituent data is not yet available. Configure an index data provider.",
                timestamp = DateTimeOffset.UtcNow
            }, jsonOptions);
        })
        .WithName("GetIndexConstituents")
        .Produces(200);
    }
}
