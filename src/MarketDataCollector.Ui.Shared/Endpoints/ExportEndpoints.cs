using System.Text.Json;
using System.Text.RegularExpressions;
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

            var baseProfileId = req.ProfileId ?? "python-pandas";
            var baseProfile = exportService.GetProfile(baseProfileId) ?? ExportProfile.PythonPandas;

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

        // Export preview - shows record counts, file sizes, and sample data without writing
        group.MapPost(UiApiRoutes.ExportPreview, async (
            ExportPreviewRequest req,
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

            var exportRequest = new ExportRequest
            {
                ProfileId = req.ProfileId ?? "python-pandas",
                Symbols = req.Symbols,
                EventTypes = req.EventTypes ?? new[] { "Trade", "BboQuote" },
                StartDate = req.StartDate ?? DateTime.UtcNow.AddDays(-7),
                EndDate = req.EndDate ?? DateTime.UtcNow
            };

            var preview = await exportService.PreviewAsync(exportRequest, req.SampleSize ?? 5, ct);

            return Results.Json(preview, jsonOptions);
        })
        .WithName("ExportPreview")
        .Produces(200);

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

            var actualFormat = formatOverride.HasValue
                ? formatOverride.Value.ToString().ToLowerInvariant()
                : "parquet";

            return Results.Json(new
            {
                jobId = result.JobId,
                success = result.Success,
                symbols = result.Symbols,
                format = orderflowFormatOverride?.ToString().ToLowerInvariant() ?? "parquet",
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
    private sealed record ExportPreviewRequest(string? ProfileId, string[]? Symbols, string[]? EventTypes, DateTime? StartDate, DateTime? EndDate, int? SampleSize);
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
