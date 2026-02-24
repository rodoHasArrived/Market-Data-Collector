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

        // Export preview - shows estimated size, record count, and sample data before committing (improvement 10.5)
        group.MapPost(UiApiRoutes.ExportPreview, async (
            ExportAnalysisRequest req,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var exportService = ctx.RequestServices.GetService<AnalysisExportService>();
            if (exportService is null)
            {
                return Results.Json(new { error = "Export service not available" },
                    jsonOptions, statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            var exportRequest = new ExportRequest
            {
                ProfileId = req.ProfileId ?? "python-pandas",
                Symbols = req.Symbols,
                StartDate = req.StartDate ?? DateTime.UtcNow.AddDays(-7),
                EndDate = req.EndDate ?? DateTime.UtcNow,
                OutputDirectory = string.Empty // preview only, no output
            };

            try
            {
                var preview = await exportService.PreviewAsync(exportRequest, ct);
                return Results.Json(preview, jsonOptions);
            }
            catch (NotImplementedException)
            {
                // Fall back to estimate if PreviewAsync not yet implemented
                return Results.Json(new
                {
                    profileId = exportRequest.ProfileId,
                    symbols = exportRequest.Symbols ?? Array.Empty<string>(),
                    startDate = exportRequest.StartDate,
                    endDate = exportRequest.EndDate,
                    estimatedRecords = 0L,
                    estimatedSizeBytes = 0L,
                    estimatedSizeMb = 0.0,
                    sampleData = Array.Empty<object>(),
                    availableEventTypes = new[] { "Trade", "BboQuote", "LOBSnapshot", "HistoricalBar" },
                    note = "Full preview not yet available; export to see actual data",
                    timestamp = DateTimeOffset.UtcNow
                }, jsonOptions);
            }
        })
        .WithName("ExportPreview")
        .Produces(200);

        // Loader script generation endpoint (improvement 10.3)
        group.MapPost(UiApiRoutes.ExportLoaderScript, (
            LoaderScriptRequest req,
            HttpContext ctx) =>
        {
            var script = req.Language?.ToLowerInvariant() switch
            {
                "python" => GeneratePythonLoaderScript(req),
                "r" => GenerateRLoaderScript(req),
                _ => GeneratePythonLoaderScript(req)
            };

            return Results.Json(new
            {
                language = req.Language ?? "python",
                script,
                fileName = req.Language?.ToLowerInvariant() == "r" ? "load_data.R" : "load_data.py",
                timestamp = DateTimeOffset.UtcNow
            }, jsonOptions);
        })
        .WithName("GenerateLoaderScript")
        .Produces(200);
    }

    private static string GeneratePythonLoaderScript(LoaderScriptRequest req)
    {
        var format = req.Format?.ToLowerInvariant() ?? "parquet";
        var dataDir = req.DataDirectory ?? "./data";

        return $"""
            #!/usr/bin/env python3
            """
            + "\"\"\"\n"
            + "Market Data Loader\n"
            + "Generated by MarketDataCollector API\n"
            + "\"\"\"\n"
            + """

            import pandas as pd
            from pathlib import Path

            """
            + $"DATA_DIR = Path(\"{dataDir}\")\n"
            + """


            def load_data(symbol: str = None, event_type: str = "trade") -> pd.DataFrame:
                """
            + "\"\"\"Load market data into a pandas DataFrame.\"\"\"\n"
            + (format == "parquet"
                ? """
                pattern = f"{symbol}_{event_type}_*.parquet" if symbol else f"*_{event_type}_*.parquet"
                files = sorted(DATA_DIR.rglob(pattern))
                if not files:
                    raise FileNotFoundError(f"No files found matching {pattern} in {DATA_DIR}")
                return pd.concat([pd.read_parquet(f) for f in files], ignore_index=True)
            """
                : """
                pattern = f"{symbol}_{event_type}_*.csv" if symbol else f"*_{event_type}_*.csv"
                files = sorted(DATA_DIR.rglob(pattern))
                if not files:
                    raise FileNotFoundError(f"No files found matching {pattern} in {DATA_DIR}")
                return pd.concat([pd.read_csv(f, parse_dates=["Timestamp"]) for f in files], ignore_index=True)
            """)
            + """

            def load_trades(symbol: str = None) -> pd.DataFrame:
                return load_data(symbol, "trade")


            def load_quotes(symbol: str = None) -> pd.DataFrame:
                return load_data(symbol, "quote")


            if __name__ == "__main__":
                df = load_trades()
                print(f"Loaded {len(df):,} records")
                print(f"Symbols: {df['Symbol'].unique().tolist() if 'Symbol' in df.columns else 'N/A'}")
                print(df.head())
            """;
    }

    private static string GenerateRLoaderScript(LoaderScriptRequest req)
    {
        var dataDir = req.DataDirectory ?? "./data";

        return $"""
            # Market Data Loader
            # Generated by MarketDataCollector API

            library(tidyverse)
            library(lubridate)

            data_dir <- "{dataDir}"

            load_data <- function(symbol = NULL, event_type = "trade") {{
              pattern <- if (!is.null(symbol)) {{
                paste0(symbol, "_", event_type, "_.*\\.csv$")
              }} else {{
                paste0(".*_", event_type, "_.*\\.csv$")
              }}

              files <- list.files(data_dir, pattern = pattern, full.names = TRUE, recursive = TRUE)

              if (length(files) == 0) {{
                stop(paste("No files found matching", pattern, "in", data_dir))
              }}

              df <- files %>%
                map_dfr(read_csv, show_col_types = FALSE) %>%
                mutate(Timestamp = ymd_hms(Timestamp))

              return(df)
            }}

            load_trades <- function(symbol = NULL) load_data(symbol, "trade")
            load_quotes <- function(symbol = NULL) load_data(symbol, "quote")

            # Example usage:
            # trades <- load_trades("AAPL")
            # head(trades)
            """;
    }

    private sealed record ExportAnalysisRequest(string? ProfileId, string[]? Symbols, string? Format, DateTime? StartDate, DateTime? EndDate);
    private sealed record QualityReportExportRequest(string? Format, string[]? Symbols);
    private sealed record OrderflowExportRequest(string[]? Symbols, string? Format);
    private sealed record ResearchPackageRequest(string[]? Symbols, bool? IncludeMetadata);
    private sealed record LoaderScriptRequest(string? Language, string? Format, string? DataDirectory);
}
