using System.Text.Json;
using MarketDataCollector.Contracts.Api;
using MarketDataCollector.Storage;
using MarketDataCollector.Storage.Export;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

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

        // Export preview - returns sample schema, record estimates, and size predictions without running a full export
        group.MapPost(UiApiRoutes.ExportPreview, (
            ExportPreviewRequest req,
            HttpContext ctx) =>
        {
            var exportService = ctx.RequestServices.GetService<AnalysisExportService>();
            var storageOptions = ctx.RequestServices.GetService<StorageOptions>();
            var rootPath = storageOptions?.RootPath ?? "data";

            var startDate = req.StartDate ?? DateTime.UtcNow.AddDays(-7);
            var endDate = req.EndDate ?? DateTime.UtcNow;
            var symbols = req.Symbols ?? Array.Empty<string>();
            var format = req.Format ?? "parquet";
            var eventTypes = req.EventTypes ?? new[] { "Trade", "BboQuote" };

            // Scan storage to estimate record counts and file sizes
            long totalRecordEstimate = 0;
            long totalSourceBytes = 0;
            int sourceFileCount = 0;

            try
            {
                var fullPath = Path.GetFullPath(rootPath);
                if (Directory.Exists(fullPath))
                {
                    foreach (var file in Directory.EnumerateFiles(fullPath, "*.jsonl*", SearchOption.AllDirectories))
                    {
                        if (file.Contains("_wal", StringComparison.OrdinalIgnoreCase) ||
                            file.Contains("_archive", StringComparison.OrdinalIgnoreCase))
                            continue;

                        var fileName = Path.GetFileName(file);
                        if (symbols.Length > 0 &&
                            !symbols.Any(s => fileName.Contains(s, StringComparison.OrdinalIgnoreCase)))
                            continue;

                        var fi = new FileInfo(file);
                        sourceFileCount++;
                        totalSourceBytes += fi.Length;
                        totalRecordEstimate += fi.Length / 100; // ~100 bytes per JSONL line
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to scan storage for export preview");
            }

            // Estimate output size based on format
            var estimatedOutputBytes = format.ToLowerInvariant() switch
            {
                "parquet" => (long)(totalSourceBytes * 0.3),
                "csv" => (long)(totalSourceBytes * 0.7),
                "arrow" => (long)(totalSourceBytes * 0.4),
                "xlsx" => (long)(totalSourceBytes * 0.5),
                _ => totalSourceBytes
            };

            // Build column schema based on event types
            var columns = BuildColumnSchema(eventTypes);

            // Warnings
            var warnings = new List<string>();
            if (format.Equals("xlsx", StringComparison.OrdinalIgnoreCase) && totalRecordEstimate > 1_000_000)
                warnings.Add($"Excel format is limited to ~1M rows; estimated {totalRecordEstimate:N0} rows. Consider Parquet or CSV.");
            if (totalRecordEstimate == 0)
                warnings.Add("No matching data found for the specified symbols and date range.");
            if (estimatedOutputBytes > 1_073_741_824)
                warnings.Add($"Estimated output is {estimatedOutputBytes / 1_073_741_824.0:F1} GB. Consider narrowing the date range or symbol list.");

            return Results.Json(new
            {
                timestamp = DateTimeOffset.UtcNow,
                parameters = new
                {
                    symbols = symbols.Length > 0 ? symbols : new[] { "(all)" },
                    startDate,
                    endDate,
                    format,
                    eventTypes
                },
                estimates = new
                {
                    totalRecords = totalRecordEstimate,
                    sourceFiles = sourceFileCount,
                    sourceBytes = totalSourceBytes,
                    outputBytes = estimatedOutputBytes,
                    outputFormatted = FormatBytesLocal(estimatedOutputBytes)
                },
                schema = new { columns, columnCount = columns.Count },
                profiles = exportService?.GetProfiles().Select(p => new
                {
                    p.Id,
                    p.Name,
                    format = p.Format.ToString().ToLowerInvariant()
                }) ?? Enumerable.Empty<object>(),
                warnings,
                serviceAvailable = exportService is not null
            }, jsonOptions);
        })
        .WithName("ExportPreview")
        .WithDescription("Returns a preview of what an export would produce including schema, record estimates, and output size predictions.")
        .Produces(200);
    }

    private static List<object> BuildColumnSchema(string[] eventTypes)
    {
        var columns = new List<object>();
        foreach (var eventType in eventTypes)
        {
            switch (eventType.ToLowerInvariant())
            {
                case "trade":
                    columns.AddRange(new object[]
                    {
                        new { name = "Timestamp", type = "datetime64[ns]", eventType = "Trade" },
                        new { name = "Symbol", type = "string", eventType = "Trade" },
                        new { name = "Price", type = "decimal(18,8)", eventType = "Trade" },
                        new { name = "Size", type = "int64", eventType = "Trade" },
                        new { name = "Side", type = "enum(Buy,Sell,Unknown)", eventType = "Trade" },
                        new { name = "Exchange", type = "string", eventType = "Trade" }
                    });
                    break;
                case "bboquote":
                case "quote":
                    columns.AddRange(new object[]
                    {
                        new { name = "Timestamp", type = "datetime64[ns]", eventType = "BboQuote" },
                        new { name = "Symbol", type = "string", eventType = "BboQuote" },
                        new { name = "BidPrice", type = "decimal(18,8)", eventType = "BboQuote" },
                        new { name = "BidSize", type = "int64", eventType = "BboQuote" },
                        new { name = "AskPrice", type = "decimal(18,8)", eventType = "BboQuote" },
                        new { name = "AskSize", type = "int64", eventType = "BboQuote" }
                    });
                    break;
                case "lobsnapshot":
                case "depth":
                    columns.AddRange(new object[]
                    {
                        new { name = "Timestamp", type = "datetime64[ns]", eventType = "LOBSnapshot" },
                        new { name = "Symbol", type = "string", eventType = "LOBSnapshot" },
                        new { name = "Bids", type = "array<{Price,Size}>", eventType = "LOBSnapshot" },
                        new { name = "Asks", type = "array<{Price,Size}>", eventType = "LOBSnapshot" }
                    });
                    break;
                case "bar":
                case "historicalbar":
                    columns.AddRange(new object[]
                    {
                        new { name = "Timestamp", type = "datetime64[ns]", eventType = "Bar" },
                        new { name = "Symbol", type = "string", eventType = "Bar" },
                        new { name = "Open", type = "decimal(18,8)", eventType = "Bar" },
                        new { name = "High", type = "decimal(18,8)", eventType = "Bar" },
                        new { name = "Low", type = "decimal(18,8)", eventType = "Bar" },
                        new { name = "Close", type = "decimal(18,8)", eventType = "Bar" },
                        new { name = "Volume", type = "int64", eventType = "Bar" }
                    });
                    break;
            }
        }
        return columns;
    }

    private static string FormatBytesLocal(long bytes) => bytes switch
    {
        >= 1_073_741_824 => $"{bytes / 1_073_741_824.0:F1} GB",
        >= 1_048_576 => $"{bytes / 1_048_576.0:F1} MB",
        >= 1024 => $"{bytes / 1024.0:F1} KB",
        _ => $"{bytes} B"
    };

    private sealed record ExportAnalysisRequest(string? ProfileId, string[]? Symbols, string? Format, DateTime? StartDate, DateTime? EndDate);
    private sealed record QualityReportExportRequest(string? Format, string[]? Symbols);
    private sealed record OrderflowExportRequest(string[]? Symbols, string? Format);
    private sealed record ResearchPackageRequest(string[]? Symbols, bool? IncludeMetadata);
    private sealed record ExportPreviewRequest(string[]? Symbols, string? Format, DateTime? StartDate, DateTime? EndDate, string[]? EventTypes);
}
