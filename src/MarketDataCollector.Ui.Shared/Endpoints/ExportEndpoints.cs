using System.Text.Json;
using System.Text.RegularExpressions;
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

    /// <summary>
    /// Regex that matches only safe opaque export identifiers (lowercase hex characters).
    /// Used to prevent path traversal in the download endpoint.
    /// </summary>
    private static readonly Regex SafeExportIdRegex = new(@"^[0-9a-f]{1,32}$", RegexOptions.Compiled);

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

            var exportId = Guid.NewGuid().ToString("N")[..16];
            var outputDir = Path.Combine(ExportBaseDir, exportId);

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
                exportId = result.Success ? exportId : null,
                downloadUrl = result.Success ? $"/api/export/download/{exportId}" : null,
                files = result.Files?.Select(f => new
                {
                    relativePath = f.RelativePath,
                    symbol = f.Symbol,
                    eventType = f.EventType,
                    format = f.Format,
                    sizeBytes = f.SizeBytes,
                    recordCount = f.RecordCount
                }),
                dataDictionaryPath = result.DataDictionaryPath is null ? null : Path.GetFileName(result.DataDictionaryPath),
                loaderScriptPath = result.LoaderScriptPath is null ? null : Path.GetFileName(result.LoaderScriptPath),
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

            var exportId = Guid.NewGuid().ToString("N")[..16];
            var outputDir = Path.Combine(ExportBaseDir, exportId);
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
                format = "parquet",
                qualitySummary = result.QualitySummary is not null ? new
                {
                    overallScore = result.QualitySummary.OverallScore,
                    completenessScore = result.QualitySummary.CompletenessScore,
                    gapsDetected = result.QualitySummary.GapsDetected,
                    outliersDetected = result.QualitySummary.OutliersDetected
                } : null,
                exportId = result.Success ? exportId : null,
                downloadUrl = result.Success ? $"/api/export/download/{exportId}" : null,
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

            var exportId = Guid.NewGuid().ToString("N")[..16];
            var outputDir = Path.Combine(ExportBaseDir, exportId);
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
                exportId = result.Success ? exportId : null,
                downloadUrl = result.Success ? $"/api/export/download/{exportId}" : null,
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

            var exportId = Guid.NewGuid().ToString("N")[..16];
            var outputDir = Path.Combine(ExportBaseDir, exportId);
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
                exportId = result.Success ? exportId : null,
                downloadUrl = result.Success ? $"/api/export/download/{exportId}" : null,
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

            var exportId = Guid.NewGuid().ToString("N")[..16];
            var outputDir = Path.Combine(ExportBaseDir, exportId);
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
                exportId = result.Success ? exportId : null,
                downloadUrl = result.Success ? $"/api/export/download/{exportId}" : null,
                dataDictionaryPath = result.DataDictionaryPath is null ? null : Path.GetFileName(result.DataDictionaryPath),
                loaderScriptPath = result.LoaderScriptPath is null ? null : Path.GetFileName(result.LoaderScriptPath),
                warnings = result.Warnings,
                error = result.Error,
                timestamp = DateTimeOffset.UtcNow
            }, jsonOptions);
        })
        .WithName("ExportResearchPackage")
        .Produces(200)
        .RequireRateLimiting(UiEndpoints.MutationRateLimitPolicy);

        // Download a file from a completed export by opaque exportId + relative file path
        group.MapGet(UiApiRoutes.ExportDownload, (
            string exportId,
            [FromQuery] string? file,
            HttpContext http) =>
        {
            if (!SafeExportIdRegex.IsMatch(exportId))
            {
                return Results.Json(new { error = "Invalid export ID" }, jsonOptions, statusCode: 400);
            }

            var baseDir = Path.GetFullPath(ExportBaseDir);
            var exportDir = Path.GetFullPath(Path.Combine(ExportBaseDir, exportId));

            if (!exportDir.StartsWith(baseDir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                && !exportDir.Equals(baseDir, StringComparison.OrdinalIgnoreCase))
            {
                return Results.Json(new { error = "Invalid export ID" }, jsonOptions, statusCode: 400);
            }

            if (!Directory.Exists(exportDir))
            {
                return Results.Json(new { error = "Export not found or expired" }, jsonOptions, statusCode: 404);
            }

            if (string.IsNullOrEmpty(file))
            {
                var files = Directory.GetFiles(exportDir, "*", SearchOption.AllDirectories)
                    .Select(f => Path.GetRelativePath(exportDir, f))
                    .ToArray();
                return Results.Json(new
                {
                    exportId,
                    files,
                    downloadUrlTemplate = $"/api/export/download/{exportId}?file={{relativePath}}"
                }, jsonOptions);
            }

            var filePath = Path.GetFullPath(Path.Combine(exportDir, file));
            if (!filePath.StartsWith(exportDir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                && !filePath.Equals(exportDir, StringComparison.OrdinalIgnoreCase))
            {
                return Results.Json(new { error = "Invalid file path" }, jsonOptions, statusCode: 400);
            }

            if (!File.Exists(filePath))
            {
                return Results.Json(new { error = "File not found" }, jsonOptions, statusCode: 404);
            }

            var fileName = Path.GetFileName(filePath);
            var contentType = Path.GetExtension(filePath).ToLowerInvariant() switch
            {
                ".parquet" => "application/octet-stream",
                ".csv" => "text/csv",
                ".jsonl" => "application/x-ndjson",
                ".json" => "application/json",
                ".md" => "text/markdown",
                ".py" => "text/x-python",
                ".r" => "text/plain",
                ".sh" => "text/plain",
                ".sql" => "text/plain",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                ".arrow" => "application/octet-stream",
                ".zip" => "application/zip",
                _ => "application/octet-stream"
            };

            return Results.File(filePath, contentType, fileName);
        })
        .WithName("DownloadExportFile")
        .Produces(200)
        .Produces(400)
        .Produces(404);
    }

    private sealed record ExportAnalysisRequest(string? ProfileId, string[]? Symbols, string? Format, DateTime? StartDate, DateTime? EndDate);
    private sealed record QualityReportExportRequest(string? Format, string[]? Symbols);
    private sealed record OrderflowExportRequest(string[]? Symbols, string? Format);
    private sealed record ResearchPackageRequest(string[]? Symbols, bool? IncludeMetadata);

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
