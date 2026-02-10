using System.Text.Json;
using MarketDataCollector.Contracts.Api;
using MarketDataCollector.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace MarketDataCollector.Ui.Shared.Endpoints;

/// <summary>
/// Extension methods for registering data sampling API endpoints.
/// </summary>
public static class SamplingEndpoints
{
    private static readonly Dictionary<string, object> s_savedSamples = new(StringComparer.OrdinalIgnoreCase);

    public static void MapSamplingEndpoints(this WebApplication app, JsonSerializerOptions jsonOptions)
    {
        var group = app.MapGroup("").WithTags("Sampling");

        // Create sample
        group.MapPost(UiApiRoutes.SamplingCreate, (SamplingCreateRequest req) =>
        {
            var sampleId = Guid.NewGuid().ToString("N")[..12];
            var sample = new
            {
                sampleId,
                symbol = req.Symbol,
                strategy = req.Strategy ?? "random",
                sampleSize = req.SampleSize ?? 1000,
                status = "created",
                createdAt = DateTimeOffset.UtcNow
            };

            s_savedSamples[sampleId] = sample;

            return Results.Json(sample, jsonOptions);
        })
        .WithName("CreateSample")
        .Produces(200)
        .RequireRateLimiting(UiEndpoints.MutationRateLimitPolicy);

        // Estimate sample size
        group.MapGet(UiApiRoutes.SamplingEstimate, (string? symbol, double? confidence, double? marginOfError) =>
        {
            var populationSize = 100000L;
            var conf = confidence ?? 0.95;
            var margin = marginOfError ?? 0.05;

            // Simple sample size estimation using Cochran's formula
            var z = conf >= 0.99 ? 2.576 : conf >= 0.95 ? 1.96 : 1.645;
            var p = 0.5;
            var n0 = (z * z * p * (1 - p)) / (margin * margin);
            var recommended = (int)Math.Ceiling(n0 / (1 + (n0 - 1) / populationSize));

            return Results.Json(new
            {
                symbol,
                confidence = conf,
                marginOfError = margin,
                estimatedPopulation = populationSize,
                recommendedSampleSize = recommended,
                timestamp = DateTimeOffset.UtcNow
            }, jsonOptions);
        })
        .WithName("EstimateSampleSize")
        .Produces(200);

        // List saved samples
        group.MapGet(UiApiRoutes.SamplingSaved, () =>
        {
            return Results.Json(new
            {
                samples = s_savedSamples.Values,
                total = s_savedSamples.Count,
                timestamp = DateTimeOffset.UtcNow
            }, jsonOptions);
        })
        .WithName("GetSavedSamples")
        .Produces(200);

        // Get sample by ID
        group.MapGet(UiApiRoutes.SamplingById, (string sampleId) =>
        {
            if (!s_savedSamples.TryGetValue(sampleId, out var sample))
                return Results.NotFound(new { error = $"Sample '{sampleId}' not found" });

            return Results.Json(sample, jsonOptions);
        })
        .WithName("GetSampleById")
        .Produces(200)
        .Produces(404);
    }

    private sealed record SamplingCreateRequest(string? Symbol, string? Strategy, int? SampleSize, DateTime? FromDate, DateTime? ToDate);
}
