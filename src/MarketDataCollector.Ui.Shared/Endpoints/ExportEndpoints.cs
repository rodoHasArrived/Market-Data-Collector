using System.Text.Json;
using MarketDataCollector.Contracts.Api;
using MarketDataCollector.Storage;
using MarketDataCollector.Storage.Export;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace MarketDataCollector.Ui.Shared.Endpoints;

/// <summary>
/// Extension methods for registering data export API endpoints.
/// Wired to real AnalysisExportService for actual data export operations.
/// </summary>
public static class ExportEndpoints
{
    public static void MapExportEndpoints(this WebApplication app, JsonSerializerOptions jsonOptions)
    {
        var group = app.MapGroup("").WithTags("Export");

        // Analysis export - wired to real AnalysisExportService
        group.MapPost(UiApiRoutes.ExportAnalysis, async (
            ExportAnalysisRequest req,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var exportService = ctx.RequestServices.GetService<AnalysisExportService>();
            if (exportService is null)
            {
                return Results.Json(new
                {
                    error = "Export service not available",
                    suggestion = "Ensure the application is running in full mode with storage configured"
                }, jsonOptions, statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            var storageOptions = ctx.RequestServices.GetService<StorageOptions>();
            var outputDir = Path.Combine(
                storageOptions?.RootPath ?? "data",
                "_exports",
                DateTime.UtcNow.ToString("yyyyMMdd_HHmmss"));

            var exportRequest = new ExportRequest
            {
                ProfileId = req.ProfileId ?? "python-pandas",
                Symbols = req.Symbols,
                StartDate = req.StartDate ?? DateTime.UtcNow.AddDays(-7),
                EndDate = req.EndDate ?? DateTime.UtcNow,
                OutputDirectory = outputDir
            };

            var result = await exportService.ExportAsync(exportRequest, ct);

            return Results.Json(new
            {
                jobId = result.JobId,
                success = result.Success,
                profileId = result.ProfileId,
                symbols = result.Symbols,
                filesGenerated = result.FilesGenerated,
                totalRecords = result.TotalRecords,
                totalBytes = result.TotalBytes,
                outputDirectory = result.OutputDirectory,
                durationSeconds = result.DurationSeconds,
                error = result.Error,
                warnings = result.Warnings,
                timestamp = DateTimeOffset.UtcNow
            }, jsonOptions);
        })
        .WithName("ExportAnalysis")
        .Produces(200)
        .RequireRateLimiting(UiEndpoints.MutationRateLimitPolicy);

        // Available export formats - returns real profiles from AnalysisExportService
        group.MapGet(UiApiRoutes.ExportFormats, (HttpContext ctx) =>
        {
            var exportService = ctx.RequestServices.GetService<AnalysisExportService>();

            var formats = new[]
            {
                new { id = "parquet", name = "Apache Parquet", description = "Columnar format for analytics (Python/pandas, Spark)", extensions = new[] { ".parquet" } },
                new { id = "csv", name = "CSV", description = "Comma-separated values (Excel, R, SQL)", extensions = new[] { ".csv", ".csv.gz" } },
                new { id = "jsonl", name = "JSON Lines", description = "One JSON object per line (streaming, interchange)", extensions = new[] { ".jsonl", ".jsonl.gz" } },
                new { id = "lean", name = "QuantConnect Lean", description = "Native Lean Engine format for backtesting", extensions = new[] { ".zip" } },
                new { id = "xlsx", name = "Microsoft Excel", description = "Excel workbook with formatted sheets", extensions = new[] { ".xlsx" } },
                new { id = "sql", name = "SQL", description = "SQL INSERT/COPY statements for databases", extensions = new[] { ".sql" } },
                new { id = "arrow", name = "Apache Arrow IPC", description = "In-memory columnar format for zero-copy interchange", extensions = new[] { ".arrow" } }
            };

            // Get real profiles from the service if available
            object[] profiles;
            if (exportService is not null)
            {
                profiles = exportService.GetProfiles().Select(p => (object)new
                {
                    id = p.Id,
                    name = p.Name,
                    format = p.Format.ToString().ToLowerInvariant(),
                    compression = p.Compression,
                    includeDataDictionary = p.IncludeDataDictionary,
                    includeLoaderScript = p.IncludeLoaderScript
                }).ToArray();
            }
            else
            {
                profiles = new object[]
                {
                    new { id = "python-pandas", name = "Python / Pandas", format = "parquet", compression = "snappy" },
                    new { id = "r-dataframe", name = "R / data.frame", format = "csv", compression = "none" },
                    new { id = "quantconnect-lean", name = "QuantConnect Lean", format = "lean", compression = "zip" },
                    new { id = "excel", name = "Microsoft Excel", format = "xlsx", compression = "none" },
                    new { id = "sql-postgres", name = "PostgreSQL / TimescaleDB", format = "csv", compression = "none" }
                };
            }

            return Results.Json(new
            {
                formats,
                profiles,
                serviceAvailable = exportService is not null,
                timestamp = DateTimeOffset.UtcNow
            }, jsonOptions);
        })
        .WithName("GetExportFormats")
        .Produces(200);

        // Quality report export
        group.MapPost(UiApiRoutes.ExportQualityReport, async (
            QualityReportExportRequest? req,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var exportService = ctx.RequestServices.GetService<AnalysisExportService>();
            if (exportService is null)
            {
                return Results.Json(new
                {
                    jobId = Guid.NewGuid().ToString("N")[..12],
                    status = "unavailable",
                    error = "Export service not configured"
                }, jsonOptions, statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            var storageOptions = ctx.RequestServices.GetService<StorageOptions>();
            var outputDir = Path.Combine(
                storageOptions?.RootPath ?? "data",
                "_exports",
                "quality",
                DateTime.UtcNow.ToString("yyyyMMdd_HHmmss"));

            var exportRequest = new ExportRequest
            {
                ProfileId = req?.Format == "parquet" ? "python-pandas" : "r-dataframe",
                Symbols = req?.Symbols,
                OutputDirectory = outputDir
            };

            var result = await exportService.ExportAsync(exportRequest, ct);

            return Results.Json(new
            {
                jobId = result.JobId,
                success = result.Success,
                format = req?.Format ?? "csv",
                filesGenerated = result.FilesGenerated,
                totalRecords = result.TotalRecords,
                outputDirectory = result.OutputDirectory,
                error = result.Error,
                timestamp = DateTimeOffset.UtcNow
            }, jsonOptions);
        })
        .WithName("ExportQualityReport")
        .Produces(200)
        .RequireRateLimiting(UiEndpoints.MutationRateLimitPolicy);

        // Orderflow export
        group.MapPost(UiApiRoutes.ExportOrderflow, async (
            OrderflowExportRequest? req,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var exportService = ctx.RequestServices.GetService<AnalysisExportService>();
            if (exportService is null)
            {
                return Results.Json(new
                {
                    jobId = Guid.NewGuid().ToString("N")[..12],
                    status = "unavailable",
                    error = "Export service not configured"
                }, jsonOptions, statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            var storageOptions = ctx.RequestServices.GetService<StorageOptions>();
            var outputDir = Path.Combine(
                storageOptions?.RootPath ?? "data",
                "_exports",
                "orderflow",
                DateTime.UtcNow.ToString("yyyyMMdd_HHmmss"));

            var exportRequest = new ExportRequest
            {
                ProfileId = req?.Format == "csv" ? "r-dataframe" : "python-pandas",
                Symbols = req?.Symbols,
                EventTypes = new[] { "Trade", "LOBSnapshot" },
                OutputDirectory = outputDir,
                Features = new FeatureSettings
                {
                    IncludeMicrostructure = true,
                    IncludeReturns = true
                }
            };

            var result = await exportService.ExportAsync(exportRequest, ct);

            return Results.Json(new
            {
                jobId = result.JobId,
                success = result.Success,
                symbols = result.Symbols,
                format = req?.Format ?? "parquet",
                filesGenerated = result.FilesGenerated,
                totalRecords = result.TotalRecords,
                outputDirectory = result.OutputDirectory,
                error = result.Error,
                timestamp = DateTimeOffset.UtcNow
            }, jsonOptions);
        })
        .WithName("ExportOrderflow")
        .Produces(200)
        .RequireRateLimiting(UiEndpoints.MutationRateLimitPolicy);

        // Integrity export
        group.MapPost(UiApiRoutes.ExportIntegrity, async (
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var exportService = ctx.RequestServices.GetService<AnalysisExportService>();
            if (exportService is null)
            {
                return Results.Json(new
                {
                    jobId = Guid.NewGuid().ToString("N")[..12],
                    status = "unavailable",
                    error = "Export service not configured"
                }, jsonOptions, statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            var storageOptions = ctx.RequestServices.GetService<StorageOptions>();
            var outputDir = Path.Combine(
                storageOptions?.RootPath ?? "data",
                "_exports",
                "integrity",
                DateTime.UtcNow.ToString("yyyyMMdd_HHmmss"));

            var exportRequest = new ExportRequest
            {
                ProfileId = "r-dataframe",
                EventTypes = new[] { "IntegrityEvent" },
                OutputDirectory = outputDir
            };

            var result = await exportService.ExportAsync(exportRequest, ct);

            return Results.Json(new
            {
                jobId = result.JobId,
                success = result.Success,
                format = "csv",
                filesGenerated = result.FilesGenerated,
                totalRecords = result.TotalRecords,
                outputDirectory = result.OutputDirectory,
                error = result.Error,
                timestamp = DateTimeOffset.UtcNow
            }, jsonOptions);
        })
        .WithName("ExportIntegrity")
        .Produces(200)
        .RequireRateLimiting(UiEndpoints.MutationRateLimitPolicy);

        // Research package export
        group.MapPost(UiApiRoutes.ExportResearchPackage, async (
            ResearchPackageRequest? req,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var exportService = ctx.RequestServices.GetService<AnalysisExportService>();
            if (exportService is null)
            {
                return Results.Json(new
                {
                    jobId = Guid.NewGuid().ToString("N")[..12],
                    status = "unavailable",
                    error = "Export service not configured"
                }, jsonOptions, statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            var storageOptions = ctx.RequestServices.GetService<StorageOptions>();
            var outputDir = Path.Combine(
                storageOptions?.RootPath ?? "data",
                "_exports",
                "research",
                DateTime.UtcNow.ToString("yyyyMMdd_HHmmss"));

            var exportRequest = new ExportRequest
            {
                ProfileId = "python-pandas",
                Symbols = req?.Symbols,
                OutputDirectory = outputDir,
                Features = new FeatureSettings
                {
                    IncludeReturns = true,
                    IncludeRollingStats = true,
                    IncludeTechnicalIndicators = true,
                    IncludeMicrostructure = true
                }
            };

            var result = await exportService.ExportAsync(exportRequest, ct);

            return Results.Json(new
            {
                jobId = result.JobId,
                success = result.Success,
                symbols = result.Symbols,
                includeMetadata = req?.IncludeMetadata ?? true,
                filesGenerated = result.FilesGenerated,
                totalRecords = result.TotalRecords,
                outputDirectory = result.OutputDirectory,
                dataDictionary = result.DataDictionaryPath,
                loaderScript = result.LoaderScriptPath,
                qualitySummary = result.QualitySummary,
                error = result.Error,
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
