using System.Text.Json;
using MarketDataCollector.Contracts.Api;
using MarketDataCollector.Storage;
using MarketDataCollector.Storage.Export;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace MarketDataCollector.Ui.Shared.Endpoints;

/// <summary>
/// Extension methods for registering data export API endpoints.
/// </summary>
public static class ExportEndpoints
{
    public static void MapExportEndpoints(this WebApplication app, JsonSerializerOptions jsonOptions)
    {
        var group = app.MapGroup("").WithTags("Export");

        // Analysis export
        group.MapPost(UiApiRoutes.ExportAnalysis, (ExportAnalysisRequest req) =>
        {
            return Results.Json(new
            {
                jobId = Guid.NewGuid().ToString("N")[..12],
                status = "queued",
                profileId = req.ProfileId ?? "python-pandas",
                symbols = req.Symbols ?? Array.Empty<string>(),
                format = req.Format ?? "parquet",
                timestamp = DateTimeOffset.UtcNow
            }, jsonOptions);
        })
        .WithName("ExportAnalysis")
        .Produces(200)
        .RequireRateLimiting(UiEndpoints.MutationRateLimitPolicy);

        // Available export formats
        group.MapGet(UiApiRoutes.ExportFormats, () =>
        {
            var formats = new[]
            {
                new { id = "parquet", name = "Apache Parquet", description = "Columnar format for analytics (Python/pandas, Spark)", extensions = new[] { ".parquet" } },
                new { id = "csv", name = "CSV", description = "Comma-separated values (Excel, R, SQL)", extensions = new[] { ".csv", ".csv.gz" } },
                new { id = "jsonl", name = "JSON Lines", description = "One JSON object per line (streaming, interchange)", extensions = new[] { ".jsonl", ".jsonl.gz" } },
                new { id = "lean", name = "QuantConnect Lean", description = "Native Lean Engine format for backtesting", extensions = new[] { ".zip" } },
                new { id = "xlsx", name = "Microsoft Excel", description = "Excel workbook with formatted sheets", extensions = new[] { ".xlsx" } },
                new { id = "sql", name = "SQL", description = "SQL INSERT/COPY statements for databases", extensions = new[] { ".sql" } }
            };

            var profiles = new[]
            {
                new { id = "python-pandas", name = "Python / Pandas", format = "parquet", compression = "snappy" },
                new { id = "r-dataframe", name = "R / data.frame", format = "csv", compression = "none" },
                new { id = "quantconnect-lean", name = "QuantConnect Lean", format = "lean", compression = "zip" },
                new { id = "excel", name = "Microsoft Excel", format = "xlsx", compression = "none" },
                new { id = "sql-postgres", name = "PostgreSQL / TimescaleDB", format = "csv", compression = "none" }
            };

            return Results.Json(new { formats, profiles, timestamp = DateTimeOffset.UtcNow }, jsonOptions);
        })
        .WithName("GetExportFormats")
        .Produces(200);

        // Quality report export
        group.MapPost(UiApiRoutes.ExportQualityReport, (QualityReportExportRequest? req) =>
        {
            return Results.Json(new
            {
                jobId = Guid.NewGuid().ToString("N")[..12],
                status = "queued",
                format = req?.Format ?? "csv",
                timestamp = DateTimeOffset.UtcNow
            }, jsonOptions);
        })
        .WithName("ExportQualityReport")
        .Produces(200)
        .RequireRateLimiting(UiEndpoints.MutationRateLimitPolicy);

        // Orderflow export
        group.MapPost(UiApiRoutes.ExportOrderflow, (OrderflowExportRequest? req) =>
        {
            return Results.Json(new
            {
                jobId = Guid.NewGuid().ToString("N")[..12],
                status = "queued",
                symbols = req?.Symbols ?? Array.Empty<string>(),
                format = req?.Format ?? "parquet",
                timestamp = DateTimeOffset.UtcNow
            }, jsonOptions);
        })
        .WithName("ExportOrderflow")
        .Produces(200)
        .RequireRateLimiting(UiEndpoints.MutationRateLimitPolicy);

        // Integrity export
        group.MapPost(UiApiRoutes.ExportIntegrity, () =>
        {
            return Results.Json(new
            {
                jobId = Guid.NewGuid().ToString("N")[..12],
                status = "queued",
                format = "csv",
                timestamp = DateTimeOffset.UtcNow
            }, jsonOptions);
        })
        .WithName("ExportIntegrity")
        .Produces(200)
        .RequireRateLimiting(UiEndpoints.MutationRateLimitPolicy);

        // Research package export
        group.MapPost(UiApiRoutes.ExportResearchPackage, (ResearchPackageRequest? req) =>
        {
            return Results.Json(new
            {
                jobId = Guid.NewGuid().ToString("N")[..12],
                status = "queued",
                symbols = req?.Symbols ?? Array.Empty<string>(),
                includeMetadata = req?.IncludeMetadata ?? true,
                timestamp = DateTimeOffset.UtcNow
            }, jsonOptions);
        })
        .WithName("ExportResearchPackage")
        .Produces(200)
        .RequireRateLimiting(UiEndpoints.MutationRateLimitPolicy);
    }

    private sealed record ExportAnalysisRequest(string? ProfileId, string[]? Symbols, string? Format, DateTime? StartDate, DateTime? EndDate);
    private sealed record QualityReportExportRequest(string? Format, string[]? Symbols);
    private sealed record OrderflowExportRequest(string[]? Symbols, string? Format);
    private sealed record ResearchPackageRequest(string[]? Symbols, bool? IncludeMetadata);
}
