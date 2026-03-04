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
    private static readonly string ExportBaseDir = Path.Combine(Path.GetTempPath(), "mdc-exports");
    private static readonly TimeSpan ExportMaxAge = TimeSpan.FromHours(24);

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

            CleanupOldExportDirectories();

            var outputDir = Path.Combine(ExportBaseDir, Guid.NewGuid().ToString("N")[..12]);

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

            var baseProfileId = req.ProfileId ?? "python-pandas";
            var baseProfile = exportService.GetProfile(baseProfileId) ?? ExportProfile.PythonPandas;

            var exportRequest = new ExportRequest
            {
                ProfileId = baseProfileId,
                CustomProfile = formatOverride.HasValue
                    ? CloneWithFormat(baseProfile, formatOverride.Value)
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
                exportId = Path.GetFileName(outputDir),
                files = result.Files?.Select(f => new
                {
                    relativePath = f.RelativePath,
                    symbol = f.Symbol,
                    eventType = f.EventType,
                    format = f.Format,
                    sizeBytes = f.SizeBytes,
                    recordCount = f.RecordCount
                }),
                dataDictionaryFile = Path.GetFileName(result.DataDictionaryPath),
                loaderScriptFile = Path.GetFileName(result.LoaderScriptPath),
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
                new { id = "r-stats", name = "R / data.frame", format = "csv", description = "CSV export for R" },
                new { id = "quantconnect-lean", name = "QuantConnect Lean", format = "lean", description = "QuantConnect Lean Engine format" },
                new { id = "excel", name = "Microsoft Excel", format = "xlsx", description = "Excel workbook format" },
                new { id = "postgresql", name = "PostgreSQL / TimescaleDB", format = "csv", description = "CSV for PostgreSQL COPY" }
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

            CleanupOldExportDirectories();

            var outputDir = Path.Combine(ExportBaseDir, "quality-" + Guid.NewGuid().ToString("N")[..8]);

            var qualityFormatOverride = req?.Format?.ToLowerInvariant() switch
            {
                "csv" => ExportFormat.Csv,
                "parquet" => ExportFormat.Parquet,
                "jsonl" => ExportFormat.Jsonl,
                "xlsx" => ExportFormat.Xlsx,
                _ => (ExportFormat?)null
            };

            var qualityBaseProfile = exportService.GetProfile("python-pandas") ?? ExportProfile.PythonPandas;
            var exportRequest = new ExportRequest
            {
                ProfileId = "python-pandas",
                CustomProfile = qualityFormatOverride.HasValue
                    ? CloneWithFormat(qualityBaseProfile, qualityFormatOverride.Value)
                    : null,
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
                format = req?.Format ?? "parquet",
                qualitySummary = result.QualitySummary is not null ? new
                {
                    overallScore = result.QualitySummary.OverallScore,
                    completenessScore = result.QualitySummary.CompletenessScore,
                    gapsDetected = result.QualitySummary.GapsDetected,
                    outliersDetected = result.QualitySummary.OutliersDetected
                } : null,
                exportId = Path.GetFileName(outputDir),
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

            CleanupOldExportDirectories();

            var outputDir = Path.Combine(ExportBaseDir, "orderflow-" + Guid.NewGuid().ToString("N")[..8]);

            var orderflowFormatOverride = req?.Format?.ToLowerInvariant() switch
            {
                "csv" => ExportFormat.Csv,
                "parquet" => ExportFormat.Parquet,
                "jsonl" => ExportFormat.Jsonl,
                "arrow" => ExportFormat.Arrow,
                _ => (ExportFormat?)null
            };

            var orderflowBaseProfile = exportService.GetProfile("python-pandas") ?? ExportProfile.PythonPandas;
            var exportRequest = new ExportRequest
            {
                ProfileId = "python-pandas",
                Symbols = req?.Symbols,
                EventTypes = new[] { "Trade", "LOBSnapshot" },
                StartDate = DateTime.UtcNow.AddDays(-7),
                EndDate = DateTime.UtcNow,
                OutputDirectory = outputDir,
                OverwriteExisting = true,
                CustomProfile = orderflowFormatOverride.HasValue
                    ? CloneWithFormat(orderflowBaseProfile, orderflowFormatOverride.Value)
                    : null
            };

            var result = await exportService.ExportAsync(exportRequest, ct);

            return Results.Json(new
            {
                jobId = result.JobId,
                success = result.Success,
                symbols = result.Symbols,
                format = orderflowFormatOverride?.ToString().ToLowerInvariant() ?? "parquet",
                filesGenerated = result.FilesGenerated,
                totalRecords = result.TotalRecords,
                exportId = Path.GetFileName(outputDir),
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

            CleanupOldExportDirectories();

            var outputDir = Path.Combine(ExportBaseDir, "integrity-" + Guid.NewGuid().ToString("N")[..8]);
            var exportRequest = new ExportRequest
            {
                ProfileId = "python-pandas",
                EventTypes = new[] { "Integrity" },
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
                format = "parquet",
                filesGenerated = result.FilesGenerated,
                totalRecords = result.TotalRecords,
                exportId = Path.GetFileName(outputDir),
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

            CleanupOldExportDirectories();

            var outputDir = Path.Combine(ExportBaseDir, "research-" + Guid.NewGuid().ToString("N")[..8]);
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
                dataDictionaryFile = Path.GetFileName(result.DataDictionaryPath),
                loaderScriptFile = Path.GetFileName(result.LoaderScriptPath),
                exportId = Path.GetFileName(outputDir),
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

    /// <summary>
    /// Creates a copy of <paramref name="source"/> with only the <see cref="ExportProfile.Format"/> changed.
    /// All other profile settings (compression, timestamps, flags, etc.) are preserved.
    /// </summary>
    private static ExportProfile CloneWithFormat(ExportProfile source, ExportFormat format) => new()
    {
        Id = "custom-" + format.ToString().ToLowerInvariant(),
        Name = source.Name,
        Description = source.Description,
        TargetTool = source.TargetTool,
        Format = format,
        Compression = source.Compression,
        TimestampSettings = source.TimestampSettings,
        IncludeFields = source.IncludeFields,
        ExcludeFields = source.ExcludeFields,
        IncludeLoaderScript = source.IncludeLoaderScript,
        IncludeDataDictionary = source.IncludeDataDictionary,
        FileNamePattern = source.FileNamePattern,
        SplitBySymbol = source.SplitBySymbol,
        SplitByDate = source.SplitByDate,
        MaxRecordsPerFile = source.MaxRecordsPerFile
    };

    /// <summary>
    /// Removes export directories older than <see cref="ExportMaxAge"/> to prevent unbounded disk usage.
    /// </summary>
    private static void CleanupOldExportDirectories()
    {
        try
        {
            if (!Directory.Exists(ExportBaseDir)) return;

            foreach (var dir in Directory.EnumerateDirectories(ExportBaseDir))
            {
                try
                {
                    var created = Directory.GetCreationTimeUtc(dir);
                    if (DateTime.UtcNow - created > ExportMaxAge)
                    {
                        Directory.Delete(dir, recursive: true);
                    }
                }
                catch (IOException)
                {
                    // Directory may be in use or already deleted
                }
                catch (UnauthorizedAccessException)
                {
                    // Insufficient permissions to delete
                }
            }
        }
        catch (IOException)
        {
            // Base directory inaccessible, skip cleanup
        }
    }
}
