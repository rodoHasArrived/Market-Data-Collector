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
/// Wired to the real AnalysisExportService for actual data export.
/// </summary>
public static class ExportEndpoints
{
    public static void MapExportEndpoints(this WebApplication app, JsonSerializerOptions jsonOptions)
    {
        var group = app.MapGroup("").WithTags("Export");

        // Analysis export — wired to real AnalysisExportService
        group.MapPost(UiApiRoutes.ExportAnalysis, async (
            ExportAnalysisRequest req,
            [FromServices] AnalysisExportService? exportService,
            CancellationToken ct) =>
        {
            if (exportService is null)
            {
                return Results.Json(new { error = "Export service not available" }, jsonOptions, statusCode: 503);
            }

            var outputDir = Path.Combine(Path.GetTempPath(), "mdc-exports", Guid.NewGuid().ToString("N")[..12]);

            var exportRequest = new ExportRequest
            {
                ProfileId = req.ProfileId ?? "python-pandas",
                Symbols = req.Symbols,
                StartDate = req.StartDate ?? DateTime.UtcNow.AddDays(-7),
                EndDate = req.EndDate ?? DateTime.UtcNow,
                OutputDirectory = outputDir,
                EventTypes = new[] { "Trade", "BboQuote" }
            };

            var result = await exportService.ExportAsync(exportRequest, ct);

            return Results.Json(new
            {
                jobId = result.JobId,
                success = result.Success,
                status = result.Success ? "completed" : "failed",
                profileId = result.ProfileId,
                symbols = result.Symbols,
                filesGenerated = result.FilesGenerated,
                totalRecords = result.TotalRecords,
                totalBytes = result.TotalBytes,
                outputDirectory = result.OutputDirectory,
                durationSeconds = result.DurationSeconds,
                error = result.Error,
                warnings = result.Warnings,
                files = result.Files.Select(f => new
                {
                    path = f.RelativePath,
                    symbol = f.Symbol,
                    format = f.Format,
                    sizeBytes = f.SizeBytes,
                    recordCount = f.RecordCount
                }),
                timestamp = DateTimeOffset.UtcNow
            }, jsonOptions);
        })
        .WithName("ExportAnalysis")
        .Produces(200)
        .RequireRateLimiting(UiEndpoints.MutationRateLimitPolicy);

        // Available export formats — wired to real profiles from AnalysisExportService
        group.MapGet(UiApiRoutes.ExportFormats, ([FromServices] AnalysisExportService? exportService) =>
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

            // Pull real profiles from the service if available
            var profiles = exportService?.GetProfiles()
                .Select(p => new { id = p.Id, name = p.Name, format = p.Format.ToString().ToLowerInvariant(), compression = p.Compression.ToString().ToLowerInvariant() })
                .ToArray()
                ?? new[]
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

        // Quality report export — wired to real backend
        group.MapPost(UiApiRoutes.ExportQualityReport, async (
            QualityReportExportRequest? req,
            [FromServices] AnalysisExportService? exportService,
            CancellationToken ct) =>
        {
            if (exportService is null)
            {
                return Results.Json(new { error = "Export service not available" }, jsonOptions, statusCode: 503);
            }

            var outputDir = Path.Combine(Path.GetTempPath(), "mdc-exports", "quality-" + DateTime.UtcNow.ToString("yyyyMMdd"));

            var exportRequest = new ExportRequest
            {
                ProfileId = req?.Format == "csv" ? "r-dataframe" : "python-pandas",
                Symbols = req?.Symbols,
                OutputDirectory = outputDir,
                ValidateBeforeExport = true,
                EventTypes = new[] { "Trade", "BboQuote" }
            };

            var result = await exportService.ExportAsync(exportRequest, ct);

            return Results.Json(new
            {
                jobId = result.JobId,
                success = result.Success,
                status = result.Success ? "completed" : "failed",
                format = req?.Format ?? "csv",
                filesGenerated = result.FilesGenerated,
                totalRecords = result.TotalRecords,
                outputDirectory = result.OutputDirectory,
                qualitySummary = result.QualitySummary,
                error = result.Error,
                timestamp = DateTimeOffset.UtcNow
            }, jsonOptions);
        })
        .WithName("ExportQualityReport")
        .Produces(200)
        .RequireRateLimiting(UiEndpoints.MutationRateLimitPolicy);

        // Orderflow export — wired to real backend with Trade event type
        group.MapPost(UiApiRoutes.ExportOrderflow, async (
            OrderflowExportRequest? req,
            [FromServices] AnalysisExportService? exportService,
            CancellationToken ct) =>
        {
            if (exportService is null)
            {
                return Results.Json(new { error = "Export service not available" }, jsonOptions, statusCode: 503);
            }

            var outputDir = Path.Combine(Path.GetTempPath(), "mdc-exports", "orderflow-" + DateTime.UtcNow.ToString("yyyyMMddHHmm"));

            var formatProfile = (req?.Format ?? "parquet") switch
            {
                "csv" => "r-dataframe",
                "jsonl" => "python-pandas",
                _ => "python-pandas"
            };

            var exportRequest = new ExportRequest
            {
                ProfileId = formatProfile,
                Symbols = req?.Symbols,
                OutputDirectory = outputDir,
                EventTypes = new[] { "Trade" }
            };

            var result = await exportService.ExportAsync(exportRequest, ct);

            return Results.Json(new
            {
                jobId = result.JobId,
                success = result.Success,
                status = result.Success ? "completed" : "failed",
                symbols = result.Symbols,
                format = req?.Format ?? "parquet",
                filesGenerated = result.FilesGenerated,
                totalRecords = result.TotalRecords,
                totalBytes = result.TotalBytes,
                outputDirectory = result.OutputDirectory,
                error = result.Error,
                timestamp = DateTimeOffset.UtcNow
            }, jsonOptions);
        })
        .WithName("ExportOrderflow")
        .Produces(200)
        .RequireRateLimiting(UiEndpoints.MutationRateLimitPolicy);

        // Integrity export — wired to real backend
        group.MapPost(UiApiRoutes.ExportIntegrity, async (
            [FromServices] AnalysisExportService? exportService,
            CancellationToken ct) =>
        {
            if (exportService is null)
            {
                return Results.Json(new { error = "Export service not available" }, jsonOptions, statusCode: 503);
            }

            var outputDir = Path.Combine(Path.GetTempPath(), "mdc-exports", "integrity-" + DateTime.UtcNow.ToString("yyyyMMddHHmm"));

            var exportRequest = new ExportRequest
            {
                ProfileId = "r-dataframe",
                OutputDirectory = outputDir,
                EventTypes = new[] { "IntegrityEvent" },
                ValidateBeforeExport = true
            };

            var result = await exportService.ExportAsync(exportRequest, ct);

            return Results.Json(new
            {
                jobId = result.JobId,
                success = result.Success,
                status = result.Success ? "completed" : "failed",
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

        // Research package export — wired to real backend
        group.MapPost(UiApiRoutes.ExportResearchPackage, async (
            ResearchPackageRequest? req,
            [FromServices] AnalysisExportService? exportService,
            CancellationToken ct) =>
        {
            if (exportService is null)
            {
                return Results.Json(new { error = "Export service not available" }, jsonOptions, statusCode: 503);
            }

            var outputDir = Path.Combine(Path.GetTempPath(), "mdc-exports", "research-" + DateTime.UtcNow.ToString("yyyyMMddHHmm"));

            var exportRequest = new ExportRequest
            {
                ProfileId = "python-pandas",
                Symbols = req?.Symbols,
                OutputDirectory = outputDir,
                EventTypes = new[] { "Trade", "BboQuote", "LOBSnapshot" },
                ValidateBeforeExport = req?.IncludeMetadata ?? true
            };

            var result = await exportService.ExportAsync(exportRequest, ct);

            return Results.Json(new
            {
                jobId = result.JobId,
                success = result.Success,
                status = result.Success ? "completed" : "failed",
                symbols = result.Symbols,
                filesGenerated = result.FilesGenerated,
                totalRecords = result.TotalRecords,
                totalBytes = result.TotalBytes,
                outputDirectory = result.OutputDirectory,
                dataDictionaryPath = result.DataDictionaryPath,
                loaderScriptPath = result.LoaderScriptPath,
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
