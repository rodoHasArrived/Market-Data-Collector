using System.Text.Json;
using MarketDataCollector.Application.Pipeline;
using MarketDataCollector.Contracts.Api;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace MarketDataCollector.Ui.Shared.Endpoints;

/// <summary>
/// Endpoints exposing dropped event statistics from the pipeline's audit trail.
/// Implements improvement #16 / C3 from the structural improvements analysis.
/// </summary>
public static class QualityDropsEndpoints
{
    /// <summary>
    /// Maps the /api/quality/drops endpoints.
    /// </summary>
    public static void MapQualityDropsEndpoints(
        this WebApplication app,
        DroppedEventAuditTrail? auditTrail,
        JsonSerializerOptions jsonOptions)
    {
        app.MapGet(UiApiRoutes.QualityDrops, () =>
        {
            if (auditTrail is null)
            {
                return Results.Json(new
                {
                    totalDropped = 0L,
                    dropsBySymbol = new Dictionary<string, long>(),
                    message = "Audit trail not configured",
                    timestamp = DateTimeOffset.UtcNow
                }, jsonOptions);
            }

            var stats = auditTrail.GetStatistics();
            return Results.Json(new
            {
                totalDropped = stats.TotalDropped,
                dropsBySymbol = stats.DropsBySymbol,
                auditFilePath = stats.AuditFilePath,
                timestamp = stats.Timestamp
            }, jsonOptions);
        });

        app.MapGet(UiApiRoutes.QualityDropsBySymbol, (string symbol) =>
        {
            if (auditTrail is null)
            {
                return Results.Json(new
                {
                    symbol,
                    dropped = 0L,
                    message = "Audit trail not configured",
                    timestamp = DateTimeOffset.UtcNow
                }, jsonOptions);
            }

            var stats = auditTrail.GetStatistics();
            var normalizedSymbol = symbol.ToUpperInvariant();
            var symbolDrops = stats.DropsBySymbol.TryGetValue(normalizedSymbol, out var count) ? count : 0;

            return Results.Json(new
            {
                symbol,
                dropped = symbolDrops,
                totalDropped = stats.TotalDropped,
                timestamp = stats.Timestamp
            }, jsonOptions);
        });
    }
}
