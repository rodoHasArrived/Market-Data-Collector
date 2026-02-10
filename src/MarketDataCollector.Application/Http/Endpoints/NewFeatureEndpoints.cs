using System.Text.Json;
using System.Text.Json.Serialization;
using MarketDataCollector.Application.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace MarketDataCollector.Application.UI;

/// <summary>
/// HTTP API endpoints for new features: historical data query, diagnostic bundles,
/// sample data generation, error tracking, config templates, environment overrides,
/// dry-run mode, and API documentation.
/// Extracted from UiServer.ConfigureNewFeatureRoutes().
/// </summary>
public static class NewFeatureEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static void MapNewFeatureEndpoints(this WebApplication app)
    {
        // ==================== QW-15: HISTORICAL DATA QUERY ====================

        app.MapGet("/api/historical/symbols", (HistoricalDataQueryService query) =>
        {
            try
            {
                var symbols = query.GetAvailableSymbols();
                return Results.Json(symbols, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get symbols: {ex.Message}");
            }
        });

        app.MapGet("/api/historical/symbols/{symbol}/range", (HistoricalDataQueryService query, string symbol) =>
        {
            try
            {
                var range = query.GetDateRange(symbol);
                return Results.Json(range, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get date range: {ex.Message}");
            }
        });

        app.MapPost("/api/historical/query", async (HistoricalDataQueryService query, HistoricalQueryRequest req) =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(req.Symbol))
                    return Results.BadRequest("Symbol is required");

                var queryParams = new HistoricalDataQuery(
                    Symbol: req.Symbol,
                    From: req.From,
                    To: req.To,
                    DataType: req.DataType,
                    Skip: req.Skip,
                    Limit: req.Limit
                );

                var result = await query.QueryAsync(queryParams);
                return Results.Json(result, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Query failed: {ex.Message}");
            }
        });

        // ==================== QW-16: DIAGNOSTIC BUNDLE ====================

        app.MapPost("/api/diagnostics/bundle", async (DiagnosticBundleService diag, DiagnosticBundleRequest? req) =>
        {
            try
            {
                var options = new DiagnosticBundleOptions(
                    IncludeSystemInfo: req?.IncludeSystemInfo ?? true,
                    IncludeConfiguration: req?.IncludeConfiguration ?? true,
                    IncludeMetrics: req?.IncludeMetrics ?? true,
                    IncludeLogs: req?.IncludeLogs ?? true,
                    IncludeStorageInfo: req?.IncludeStorageInfo ?? true,
                    IncludeEnvironmentVariables: req?.IncludeEnvironmentVariables ?? true,
                    LogDays: req?.LogDays ?? 3
                );

                var result = await diag.GenerateAsync(options);
                return Results.Json(result, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Bundle generation failed: {ex.Message}");
            }
        });

        app.MapGet("/api/diagnostics/bundle/{bundleId}/download", (DiagnosticBundleService diag, string bundleId) =>
        {
            try
            {
                var zipPath = Path.Combine(Path.GetTempPath(), $"{bundleId}.zip");
                if (!File.Exists(zipPath))
                    return Results.NotFound("Bundle not found or expired");

                var bytes = diag.ReadBundle(zipPath);
                return Results.File(bytes, "application/zip", $"{bundleId}.zip");
            }
            catch (Exception ex)
            {
                return Results.Problem($"Download failed: {ex.Message}");
            }
        });

        // ==================== QW-17: SAMPLE DATA GENERATOR ====================

        app.MapPost("/api/tools/sample-data", (SampleDataGenerator gen, SampleDataRequest? req) =>
        {
            try
            {
                var options = new SampleDataOptions(
                    Symbols: req?.Symbols,
                    DurationMinutes: req?.DurationMinutes ?? 60,
                    MaxEvents: req?.MaxEvents ?? 10000,
                    IncludeTrades: req?.IncludeTrades ?? true,
                    IncludeQuotes: req?.IncludeQuotes ?? true,
                    IncludeDepth: req?.IncludeDepth ?? true,
                    IncludeBars: req?.IncludeBars ?? true
                );

                var result = gen.Generate(options);
                return Results.Json(new
                {
                    result.Success,
                    result.Message,
                    result.TotalEvents,
                    result.TradeCount,
                    result.QuoteCount,
                    result.DepthUpdateCount,
                    result.BarCount,
                    // Don't include raw events in response (too large)
                    sampleEvents = result.Events?.Take(5)
                }, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Sample data generation failed: {ex.Message}");
            }
        });

        app.MapGet("/api/tools/sample-data/preview", (SampleDataGenerator gen) =>
        {
            try
            {
                var preview = gen.GeneratePreview(new SampleDataOptions(MaxEvents: 20));
                return Results.Json(preview, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Preview failed: {ex.Message}");
            }
        });

        // ==================== QW-58: LAST N ERRORS ====================

        app.MapGet("/api/diagnostics/errors", (ErrorTracker errors, int? count, string? type, string? context) =>
        {
            try
            {
                var result = errors.GetLastErrors(count ?? 10, type, context);
                return Results.Json(result, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get errors: {ex.Message}");
            }
        });

        app.MapGet("/api/diagnostics/errors/stats", (ErrorTracker errors, int? hours) =>
        {
            try
            {
                var window = hours.HasValue ? TimeSpan.FromHours(hours.Value) : TimeSpan.FromHours(24);
                var stats = errors.GetStatistics(window);
                return Results.Json(stats, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get error stats: {ex.Message}");
            }
        });

        app.MapGet("/api/diagnostics/errors/logs", async (ErrorTracker errors, int? count, int? days) =>
        {
            try
            {
                var result = await errors.ParseErrorsFromLogsAsync(count ?? 100, days ?? 1);
                return Results.Json(result, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to parse log errors: {ex.Message}");
            }
        });

        // ==================== QW-76: CONFIG TEMPLATE GENERATOR ====================

        app.MapGet("/api/tools/config-templates", (ConfigTemplateGenerator gen) =>
        {
            try
            {
                var templates = gen.GetAllTemplates();
                return Results.Json(templates.Select(t => new
                {
                    t.Name,
                    t.Description,
                    t.Category,
                    t.EnvironmentVariables
                }), JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get templates: {ex.Message}");
            }
        });

        app.MapGet("/api/tools/config-templates/{name}", (ConfigTemplateGenerator gen, string name) =>
        {
            try
            {
                var template = gen.GetTemplate(name);
                if (template == null)
                    return Results.NotFound($"Template '{name}' not found");

                return Results.Json(template, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get template: {ex.Message}");
            }
        });

        app.MapPost("/api/tools/config-templates/validate", (ConfigTemplateGenerator gen, ConfigValidateRequest req) =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(req.Json))
                    return Results.BadRequest("JSON configuration is required");

                var result = gen.ValidateTemplate(req.Json);
                return Results.Json(result, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Validation failed: {ex.Message}");
            }
        });

        // ==================== QW-25: CONFIG ENVIRONMENT OVERRIDE ====================

        app.MapGet("/api/config/env-overrides", (ConfigEnvironmentOverride envOverride) =>
        {
            try
            {
                var variables = envOverride.GetRecognizedVariables();
                return Results.Json(variables, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get environment overrides: {ex.Message}");
            }
        });

        app.MapGet("/api/config/env-overrides/docs", (ConfigEnvironmentOverride envOverride) =>
        {
            try
            {
                var docs = envOverride.GetDocumentation();
                return Results.Text(docs, "text/markdown");
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get documentation: {ex.Message}");
            }
        });

        // ==================== QW-93: DRY RUN MODE ====================

        app.MapPost("/api/tools/dry-run", async (
            ConfigurationService configService,
            ConfigStore store,
            DryRunRequest? req) =>
        {
            try
            {
                var config = configService.LoadAndPrepareConfig(store.ConfigPath);
                var options = new DryRunOptions(
                    ValidateConfiguration: req?.ValidateConfiguration ?? true,
                    ValidateFileSystem: req?.ValidateFileSystem ?? true,
                    ValidateConnectivity: req?.ValidateConnectivity ?? true,
                    ValidateProviders: req?.ValidateProviders ?? true,
                    ValidateSymbols: req?.ValidateSymbols ?? true,
                    ValidateResources: req?.ValidateResources ?? true
                );

                var result = await configService.DryRunValidationAsync(config, options);
                return Results.Json(result, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Dry run failed: {ex.Message}");
            }
        });

        app.MapPost("/api/tools/dry-run/report", async (
            ConfigurationService configService,
            ConfigStore store,
            DryRunService dryRunService) =>
        {
            try
            {
                var config = configService.LoadAndPrepareConfig(store.ConfigPath);
                var result = await configService.DryRunValidationAsync(config, new DryRunOptions());
                var report = dryRunService.GenerateReport(result);
                return Results.Text(report, "text/plain");
            }
            catch (Exception ex)
            {
                return Results.Problem($"Report generation failed: {ex.Message}");
            }
        });

        // ==================== DEV-9 & QW-121: API DOCUMENTATION ====================

        app.MapGet("/api/openapi.json", (ApiDocumentationService apiDocs) =>
        {
            try
            {
                var spec = apiDocs.GenerateOpenApiSpec();
                return Results.Json(spec, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                });
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to generate OpenAPI spec: {ex.Message}");
            }
        });

        app.MapGet("/api/docs", (ApiDocumentationService apiDocs) =>
        {
            try
            {
                var html = apiDocs.GenerateSwaggerHtml();
                return Results.Content(html, "text/html");
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to generate docs: {ex.Message}");
            }
        });

        app.MapGet("/api/docs/markdown", (ApiDocumentationService apiDocs) =>
        {
            try
            {
                var markdown = apiDocs.GenerateMarkdownDocs();
                return Results.Text(markdown, "text/markdown");
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to generate markdown: {ex.Message}");
            }
        });

        app.MapGet("/swagger", (ApiDocumentationService apiDocs) =>
        {
            try
            {
                var html = apiDocs.GenerateSwaggerHtml();
                return Results.Content(html, "text/html");
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to generate Swagger UI: {ex.Message}");
            }
        });
    }
}
