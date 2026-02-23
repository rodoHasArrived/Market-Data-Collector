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
/// Wires requests to <see cref="AnalysisExportService"/> for real export operations.
/// </summary>
public static class ExportEndpoints
{
    public static void MapExportEndpoints(this WebApplication app, JsonSerializerOptions jsonOptions)
    {
        var group = app.MapGroup("").WithTags("Export");

        // Analysis export - wired to AnalysisExportService
        group.MapPost(UiApiRoutes.ExportAnalysis, async (
            ExportAnalysisRequest req,
            [FromServices] AnalysisExportService? exportService,
            CancellationToken ct) =>
        {
            if (exportService is null)
            {
                return Results.Json(new
                {
                    error = "Export service not available",
                    suggestion = "Ensure the application is running with storage configured"
                }, jsonOptions, statusCode: 503);
            }

            var outputDir = Path.Combine(Path.GetTempPath(), "mdc-exports", Guid.NewGuid().ToString("N")[..12]);

            var profile = exportService.GetProfile(req.ProfileId ?? "python-pandas");
            var formatOverride = req.Format?.ToLowerInvariant() switch
            {
                "csv" => ExportFormat.Csv,
                "parquet" => ExportFormat.Parquet,
                "jsonl" => ExportFormat.Jsonl,
                "lean" => ExportFormat.Lean,
                "sql" => ExportFormat.Sql,
                "xlsx" => ExportFormat.Xlsx,
                "arrow" => ExportFormat.Arrow,
                _ => (ExportFormat?)null
            };

            var exportRequest = new ExportRequest
            {
                ProfileId = req.ProfileId ?? "python-pandas",
                CustomProfile = formatOverride.HasValue
                    ? new ExportProfile { Id = "custom", Format = formatOverride.Value }
                    : null,
                Symbols = req.Symbols,
                StartDate = req.StartDate ?? DateTime.UtcNow.AddDays(-7),
                EndDate = req.EndDate ?? DateTime.UtcNow,
                OutputDirectory = outputDir,
                OverwriteExisting = true
            };

            var result = await exportService.ExportAsync(exportRequest, ct);

            return Results.Json(new
            {
                jobId = result.JobId,
                success = result.Success,
                profileId = result.ProfileId,
                filesGenerated = result.FilesGenerated,
                totalRecords = result.TotalRecords,
                totalBytes = result.TotalBytes,
                symbols = result.Symbols,
                outputDirectory = result.Success ? outputDir : null,
                files = result.Files?.Select(f => new
                {
                    path = f.Path,
                    symbol = f.Symbol,
                    eventType = f.EventType,
                    format = f.Format,
                    sizeBytes = f.SizeBytes,
                    recordCount = f.RecordCount
                }),
                dataDictionaryPath = result.DataDictionaryPath,
                loaderScriptPath = result.LoaderScriptPath,
                warnings = result.Warnings,
                error = result.Error,
                timestamp = DateTimeOffset.UtcNow
            }, jsonOptions);
        })
        .WithName("ExportAnalysis")
        .Produces(200)
        .RequireRateLimiting(UiEndpoints.MutationRateLimitPolicy);

        // Available export formats - wired to AnalysisExportService profiles
        group.MapGet(UiApiRoutes.ExportFormats, ([FromServices] AnalysisExportService? exportService) =>
        {
            var formats = new[]
            {
                new { id = "parquet", name = "Apache Parquet", description = "Columnar format for analytics (Python/pandas, Spark)", extensions = new[] { ".parquet" } },
                new { id = "csv", name = "CSV", description = "Comma-separated values (Excel, R, SQL)", extensions = new[] { ".csv", ".csv.gz" } },
                new { id = "jsonl", name = "JSON Lines", description = "One JSON object per line (streaming, interchange)", extensions = new[] { ".jsonl", ".jsonl.gz" } },
                new { id = "lean", name = "QuantConnect Lean", description = "Native Lean Engine format for backtesting", extensions = new[] { ".zip" } },
                new { id = "xlsx", name = "Microsoft Excel", description = "Excel workbook with formatted sheets", extensions = new[] { ".xlsx" } },
                new { id = "sql", name = "SQL", description = "SQL INSERT/COPY statements for databases", extensions = new[] { ".sql" } },
                new { id = "arrow", name = "Apache Arrow", description = "In-memory columnar format for zero-copy reads", extensions = new[] { ".arrow" } }
            };

            var profiles = exportService?.GetProfiles().Select(p => new
            {
                id = p.Id,
                name = p.Name,
                format = p.Format.ToString().ToLowerInvariant(),
                description = p.Description
            }).ToArray() ?? new[]
            {
                new { id = "python-pandas", name = "Python / Pandas", format = "parquet", description = "Parquet export optimized for pandas" },
                new { id = "r-dataframe", name = "R / data.frame", format = "csv", description = "CSV export for R" },
                new { id = "quantconnect-lean", name = "QuantConnect Lean", format = "lean", description = "QuantConnect Lean Engine format" },
                new { id = "excel", name = "Microsoft Excel", format = "xlsx", description = "Excel workbook format" },
                new { id = "sql-postgres", name = "PostgreSQL / TimescaleDB", format = "csv", description = "CSV for PostgreSQL COPY" }
            };

            return Results.Json(new { formats, profiles, timestamp = DateTimeOffset.UtcNow }, jsonOptions);
        })
        .WithName("GetExportFormats")
        .Produces(200);

        // Quality report export
        group.MapPost(UiApiRoutes.ExportQualityReport, async (
            QualityReportExportRequest? req,
            [FromServices] AnalysisExportService? exportService,
            CancellationToken ct) =>
        {
            if (exportService is null)
            {
                return Results.Json(new { error = "Export service not available" }, jsonOptions, statusCode: 503);
            }

            var outputDir = Path.Combine(Path.GetTempPath(), "mdc-exports", "quality-" + Guid.NewGuid().ToString("N")[..8]);
            var exportRequest = new ExportRequest
            {
                ProfileId = "python-pandas",
                Symbols = req?.Symbols,
                StartDate = DateTime.UtcNow.AddDays(-30),
                EndDate = DateTime.UtcNow,
                OutputDirectory = outputDir,
                OverwriteExisting = true,
                ValidateBeforeExport = true,
                MinQualityScore = 0.0
            };

            var result = await exportService.ExportAsync(exportRequest, ct);

            return Results.Json(new
            {
                jobId = result.JobId,
                success = result.Success,
                format = req?.Format ?? "csv",
                qualitySummary = result.QualitySummary is not null ? new
                {
                    overallScore = result.QualitySummary.OverallScore,
                    completenessScore = result.QualitySummary.CompletenessScore,
                    gapsDetected = result.QualitySummary.GapsDetected,
                    outliersDetected = result.QualitySummary.OutliersDetected
                } : null,
                outputDirectory = result.Success ? outputDir : null,
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
            [FromServices] AnalysisExportService? exportService,
            CancellationToken ct) =>
        {
            if (exportService is null)
            {
                return Results.Json(new { error = "Export service not available" }, jsonOptions, statusCode: 503);
            }

            var outputDir = Path.Combine(Path.GetTempPath(), "mdc-exports", "orderflow-" + Guid.NewGuid().ToString("N")[..8]);
            var exportRequest = new ExportRequest
            {
                ProfileId = "python-pandas",
                Symbols = req?.Symbols,
                EventTypes = new[] { "Trade", "LOBSnapshot" },
                StartDate = DateTime.UtcNow.AddDays(-7),
                EndDate = DateTime.UtcNow,
                OutputDirectory = outputDir,
                OverwriteExisting = true
            };

            if (req?.Format?.Equals("parquet", StringComparison.OrdinalIgnoreCase) == true)
            {
                exportRequest.CustomProfile = new ExportProfile { Id = "orderflow", Format = ExportFormat.Parquet };
            }

            var result = await exportService.ExportAsync(exportRequest, ct);

            return Results.Json(new
            {
                jobId = result.JobId,
                success = result.Success,
                symbols = result.Symbols,
                format = req?.Format ?? "parquet",
                filesGenerated = result.FilesGenerated,
                totalRecords = result.TotalRecords,
                outputDirectory = result.Success ? outputDir : null,
                error = result.Error,
                timestamp = DateTimeOffset.UtcNow
            }, jsonOptions);
        })
        .WithName("ExportOrderflow")
        .Produces(200)
        .RequireRateLimiting(UiEndpoints.MutationRateLimitPolicy);

        // Integrity export
        group.MapPost(UiApiRoutes.ExportIntegrity, async (
            [FromServices] AnalysisExportService? exportService,
            CancellationToken ct) =>
        {
            if (exportService is null)
            {
                return Results.Json(new { error = "Export service not available" }, jsonOptions, statusCode: 503);
            }

            var outputDir = Path.Combine(Path.GetTempPath(), "mdc-exports", "integrity-" + Guid.NewGuid().ToString("N")[..8]);
            var exportRequest = new ExportRequest
            {
                ProfileId = "python-pandas",
                EventTypes = new[] { "IntegrityEvent" },
                StartDate = DateTime.UtcNow.AddDays(-30),
                EndDate = DateTime.UtcNow,
                OutputDirectory = outputDir,
                OverwriteExisting = true
            };

            var result = await exportService.ExportAsync(exportRequest, ct);

            return Results.Json(new
            {
                jobId = result.JobId,
                success = result.Success,
                format = "csv",
                filesGenerated = result.FilesGenerated,
                totalRecords = result.TotalRecords,
                outputDirectory = result.Success ? outputDir : null,
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
            [FromServices] AnalysisExportService? exportService,
            CancellationToken ct) =>
        {
            if (exportService is null)
            {
                return Results.Json(new { error = "Export service not available" }, jsonOptions, statusCode: 503);
            }

            var outputDir = Path.Combine(Path.GetTempPath(), "mdc-exports", "research-" + Guid.NewGuid().ToString("N")[..8]);
            var exportRequest = new ExportRequest
            {
                ProfileId = "python-pandas",
                Symbols = req?.Symbols,
                StartDate = DateTime.UtcNow.AddDays(-90),
                EndDate = DateTime.UtcNow,
                OutputDirectory = outputDir,
                OverwriteExisting = true,
                ValidateBeforeExport = true,
                Features = (req?.IncludeMetadata ?? true) ? new FeatureSettings
                {
                    IncludeReturns = true,
                    IncludeRollingStats = true,
                    IncludeTechnicalIndicators = true
                } : null
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
                totalBytes = result.TotalBytes,
                dataDictionaryPath = result.DataDictionaryPath,
                loaderScriptPath = result.LoaderScriptPath,
                outputDirectory = result.Success ? outputDir : null,
                warnings = result.Warnings,
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
