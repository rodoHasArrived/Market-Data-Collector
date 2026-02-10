using System.Text.Json;
using MarketDataCollector.Application.Subscriptions.Models;
using MarketDataCollector.Application.Subscriptions.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace MarketDataCollector.Application.UI;

/// <summary>
/// HTTP API endpoints for symbol management: CSV bulk import/export, templates,
/// schedules, metadata enrichment, symbol search, FIGI lookup, and index subscriptions.
/// Extracted from UiServer.ConfigureSymbolManagementRoutes().
/// </summary>
public static class SymbolManagementEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static void MapSymbolManagementEndpoints(this WebApplication app)
    {
        // ==================== CSV BULK IMPORT/EXPORT ====================

        app.MapPost("/api/symbols/bulk-import", async (
            SymbolImportExportService importExport,
            HttpRequest request) =>
        {
            try
            {
                using var reader = new StreamReader(request.Body);
                var csvContent = await reader.ReadToEndAsync();

                if (string.IsNullOrWhiteSpace(csvContent))
                    return Results.BadRequest("CSV content is required.");

                // Parse options from query string
                var options = new BulkImportOptions(
                    SkipExisting: request.Query["skipExisting"] != "false",
                    UpdateExisting: request.Query["updateExisting"] == "true",
                    HasHeader: request.Query["hasHeader"] != "false",
                    ValidateSymbols: request.Query["validate"] != "false"
                );

                var result = await importExport.ImportFromCsvAsync(csvContent, options);
                return Results.Json(result, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Import failed: {ex.Message}");
            }
        });

        app.MapGet("/api/symbols/bulk-export", (SymbolImportExportService importExport, HttpRequest request) =>
        {
            try
            {
                var options = new BulkExportOptions(
                    IncludeHeader: request.Query["includeHeader"] != "false",
                    IncludeMetadata: request.Query["includeMetadata"] == "true"
                );

                var csv = importExport.ExportToCsv(options);
                return Results.Text(csv, "text/csv");
            }
            catch (Exception ex)
            {
                return Results.Problem($"Export failed: {ex.Message}");
            }
        });

        app.MapGet("/api/symbols/bulk-export/download", (SymbolImportExportService importExport, HttpRequest request) =>
        {
            try
            {
                var options = new BulkExportOptions(
                    IncludeHeader: request.Query["includeHeader"] != "false",
                    IncludeMetadata: request.Query["includeMetadata"] == "true"
                );

                var bytes = importExport.ExportToCsvBytes(options);
                return Results.File(bytes, "text/csv", "symbols_export.csv");
            }
            catch (Exception ex)
            {
                return Results.Problem($"Export failed: {ex.Message}");
            }
        });

        // ==================== TEMPLATES ====================

        app.MapGet("/api/symbols/templates", async (TemplateService templates) =>
        {
            try
            {
                var all = await templates.GetAllTemplatesAsync();
                return Results.Json(all, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get templates: {ex.Message}");
            }
        });

        app.MapGet("/api/symbols/templates/{templateId}", async (TemplateService templates, string templateId) =>
        {
            try
            {
                var template = await templates.GetTemplateAsync(templateId);
                return template is null
                    ? Results.NotFound($"Template '{templateId}' not found")
                    : Results.Json(template, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get template: {ex.Message}");
            }
        });

        app.MapPost("/api/symbols/templates/apply", async (TemplateService templates, ApplyTemplateRequest request) =>
        {
            try
            {
                var result = await templates.ApplyTemplateAsync(request);
                return Results.Json(result, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to apply template: {ex.Message}");
            }
        });

        app.MapPost("/api/symbols/templates", async (TemplateService templates, CreateTemplateDto dto) =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(dto.Name))
                    return Results.BadRequest("Template name is required.");

                if (dto.Symbols is null || dto.Symbols.Length == 0)
                    return Results.BadRequest("At least one symbol is required.");

                var template = await templates.CreateTemplateAsync(
                    dto.Name,
                    dto.Description ?? "",
                    dto.Category,
                    dto.Symbols,
                    dto.Defaults);

                return Results.Json(template, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to create template: {ex.Message}");
            }
        });

        app.MapPost("/api/symbols/templates/from-current", async (TemplateService templates, CreateFromCurrentDto dto) =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(dto.Name))
                    return Results.BadRequest("Template name is required.");

                var template = await templates.CreateFromCurrentAsync(dto.Name, dto.Description ?? "");
                return Results.Json(template, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to create template: {ex.Message}");
            }
        });

        app.MapDelete("/api/symbols/templates/{templateId}", async (TemplateService templates, string templateId) =>
        {
            try
            {
                var deleted = await templates.DeleteTemplateAsync(templateId);
                return deleted
                    ? Results.Ok()
                    : Results.NotFound($"Template '{templateId}' not found or is a built-in template");
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to delete template: {ex.Message}");
            }
        });

        // ==================== SCHEDULES ====================

        app.MapGet("/api/symbols/schedules", (SchedulingService scheduling) =>
        {
            try
            {
                var schedules = scheduling.GetAllSchedules();
                return Results.Json(schedules, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get schedules: {ex.Message}");
            }
        });

        app.MapGet("/api/symbols/schedules/{scheduleId}", (SchedulingService scheduling, string scheduleId) =>
        {
            try
            {
                var schedule = scheduling.GetSchedule(scheduleId);
                return schedule is null
                    ? Results.NotFound($"Schedule '{scheduleId}' not found")
                    : Results.Json(schedule, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get schedule: {ex.Message}");
            }
        });

        app.MapGet("/api/symbols/schedules/{scheduleId}/status", (SchedulingService scheduling, string scheduleId) =>
        {
            try
            {
                var status = scheduling.GetExecutionStatus(scheduleId);
                return status is null
                    ? Results.NotFound($"No execution status for schedule '{scheduleId}'")
                    : Results.Json(status, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get schedule status: {ex.Message}");
            }
        });

        app.MapPost("/api/symbols/schedules", async (SchedulingService scheduling, CreateScheduleRequest request) =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Name))
                    return Results.BadRequest("Schedule name is required.");

                if (request.Symbols is null || request.Symbols.Length == 0)
                    return Results.BadRequest("At least one symbol is required.");

                var schedule = await scheduling.CreateScheduleAsync(request);
                return Results.Json(schedule, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to create schedule: {ex.Message}");
            }
        });

        app.MapPut("/api/symbols/schedules/{scheduleId}", async (
            SchedulingService scheduling,
            string scheduleId,
            CreateScheduleRequest request) =>
        {
            try
            {
                var schedule = await scheduling.UpdateScheduleAsync(scheduleId, request);
                return schedule is null
                    ? Results.NotFound($"Schedule '{scheduleId}' not found")
                    : Results.Json(schedule, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to update schedule: {ex.Message}");
            }
        });

        app.MapPost("/api/symbols/schedules/{scheduleId}/enable", async (
            SchedulingService scheduling,
            string scheduleId) =>
        {
            try
            {
                var success = await scheduling.SetScheduleEnabledAsync(scheduleId, true);
                return success ? Results.Ok() : Results.NotFound($"Schedule '{scheduleId}' not found");
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to enable schedule: {ex.Message}");
            }
        });

        app.MapPost("/api/symbols/schedules/{scheduleId}/disable", async (
            SchedulingService scheduling,
            string scheduleId) =>
        {
            try
            {
                var success = await scheduling.SetScheduleEnabledAsync(scheduleId, false);
                return success ? Results.Ok() : Results.NotFound($"Schedule '{scheduleId}' not found");
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to disable schedule: {ex.Message}");
            }
        });

        app.MapPost("/api/symbols/schedules/{scheduleId}/execute", async (
            SchedulingService scheduling,
            string scheduleId) =>
        {
            try
            {
                var status = await scheduling.ExecuteNowAsync(scheduleId);
                return Results.Json(status, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to execute schedule: {ex.Message}");
            }
        });

        app.MapDelete("/api/symbols/schedules/{scheduleId}", async (SchedulingService scheduling, string scheduleId) =>
        {
            try
            {
                var deleted = await scheduling.DeleteScheduleAsync(scheduleId);
                return deleted ? Results.Ok() : Results.NotFound($"Schedule '{scheduleId}' not found");
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to delete schedule: {ex.Message}");
            }
        });

        // ==================== METADATA ====================

        app.MapGet("/api/symbols/metadata/{symbol}", async (MetadataEnrichmentService metadata, string symbol) =>
        {
            try
            {
                var meta = await metadata.GetMetadataAsync(symbol);
                return meta is null
                    ? Results.NotFound($"No metadata found for symbol '{symbol}'")
                    : Results.Json(meta, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get metadata: {ex.Message}");
            }
        });

        app.MapPost("/api/symbols/metadata/batch", async (
            MetadataEnrichmentService metadata,
            string[] symbols) =>
        {
            try
            {
                var result = await metadata.GetMetadataBatchAsync(symbols);
                return Results.Json(result, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get metadata: {ex.Message}");
            }
        });

        app.MapPost("/api/symbols/metadata/filter", async (
            MetadataEnrichmentService metadata,
            SymbolMetadataFilter filter) =>
        {
            try
            {
                var result = await metadata.FilterSymbolsAsync(filter);
                return Results.Json(result, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to filter symbols: {ex.Message}");
            }
        });

        app.MapGet("/api/symbols/metadata/sectors", async (MetadataEnrichmentService metadata) =>
        {
            try
            {
                var sectors = await metadata.GetAvailableSectorsAsync();
                return Results.Json(sectors, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get sectors: {ex.Message}");
            }
        });

        app.MapGet("/api/symbols/metadata/industries", async (
            MetadataEnrichmentService metadata,
            HttpRequest request) =>
        {
            try
            {
                var sector = request.Query["sector"].FirstOrDefault();
                var industries = await metadata.GetAvailableIndustriesAsync(sector);
                return Results.Json(industries, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get industries: {ex.Message}");
            }
        });

        app.MapPost("/api/symbols/metadata", async (
            MetadataEnrichmentService metadata,
            SymbolMetadata meta) =>
        {
            try
            {
                await metadata.UpdateMetadataAsync(meta);
                return Results.Ok();
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to update metadata: {ex.Message}");
            }
        });

        // ==================== SYMBOL SEARCH & AUTOCOMPLETE ====================

        app.MapGet("/api/symbols/search", async (
            SymbolSearchService searchService,
            HttpRequest request,
            CancellationToken ct) =>
        {
            try
            {
                var query = request.Query["q"].FirstOrDefault() ?? request.Query["query"].FirstOrDefault() ?? "";
                var limitStr = request.Query["limit"].FirstOrDefault();
                var limit = int.TryParse(limitStr, out var l) ? l : 10;
                var assetType = request.Query["assetType"].FirstOrDefault();
                var exchange = request.Query["exchange"].FirstOrDefault();
                var provider = request.Query["provider"].FirstOrDefault();
                var includeFigiStr = request.Query["includeFigi"].FirstOrDefault();
                var includeFigi = !string.Equals(includeFigiStr, "false", StringComparison.OrdinalIgnoreCase);

                var searchRequest = new SymbolSearchRequest(
                    Query: query,
                    Limit: Math.Clamp(limit, 1, 50),
                    AssetType: assetType,
                    Exchange: exchange,
                    Provider: provider,
                    IncludeFigi: includeFigi
                );

                var result = await searchService.SearchAsync(searchRequest, ct);
                return Results.Json(result, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Symbol search failed: {ex.Message}");
            }
        });

        app.MapPost("/api/symbols/search", async (
            SymbolSearchService searchService,
            SymbolSearchRequest request,
            CancellationToken ct) =>
        {
            try
            {
                var result = await searchService.SearchAsync(request, ct);
                return Results.Json(result, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Symbol search failed: {ex.Message}");
            }
        });

        app.MapGet("/api/symbols/search/providers", async (
            SymbolSearchService searchService,
            CancellationToken ct) =>
        {
            try
            {
                var providers = await searchService.GetProvidersAsync(ct);
                return Results.Json(providers, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get providers: {ex.Message}");
            }
        });

        app.MapGet("/api/symbols/details/{symbol}", async (
            SymbolSearchService searchService,
            string symbol,
            HttpRequest request,
            CancellationToken ct) =>
        {
            try
            {
                var provider = request.Query["provider"].FirstOrDefault();
                var details = await searchService.GetDetailsAsync(symbol, provider, ct);

                if (details is null)
                    return Results.NotFound($"Symbol '{symbol}' not found");

                return Results.Json(details, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get symbol details: {ex.Message}");
            }
        });

        app.MapGet("/api/symbols/figi/{symbol}", async (
            SymbolSearchService searchService,
            string symbol,
            CancellationToken ct) =>
        {
            try
            {
                var figiMappings = await searchService.LookupFigiAsync(new[] { symbol }, ct);
                if (!figiMappings.TryGetValue(symbol.ToUpperInvariant(), out var mappings) || mappings.Count == 0)
                    return Results.NotFound($"No FIGI mappings found for '{symbol}'");

                return Results.Json(new { symbol = symbol.ToUpperInvariant(), mappings }, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"FIGI lookup failed: {ex.Message}");
            }
        });

        app.MapPost("/api/symbols/figi/bulk", async (
            SymbolSearchService searchService,
            FigiBulkLookupRequest request,
            CancellationToken ct) =>
        {
            try
            {
                if (request.Symbols is null || request.Symbols.Length == 0)
                    return Results.BadRequest("At least one symbol is required");

                var figiMappings = await searchService.LookupFigiAsync(request.Symbols, ct);
                return Results.Json(figiMappings, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Bulk FIGI lookup failed: {ex.Message}");
            }
        });

        app.MapGet("/api/symbols/figi/search", async (
            SymbolSearchService searchService,
            HttpRequest request,
            CancellationToken ct) =>
        {
            try
            {
                var query = request.Query["q"].FirstOrDefault() ?? request.Query["query"].FirstOrDefault() ?? "";
                var limitStr = request.Query["limit"].FirstOrDefault();
                var limit = int.TryParse(limitStr, out var l) ? l : 20;

                if (string.IsNullOrWhiteSpace(query))
                    return Results.BadRequest("Query parameter 'q' is required");

                var results = await searchService.SearchFigiAsync(query, Math.Clamp(limit, 1, 100), ct);
                return Results.Json(new { query, count = results.Count, results }, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"FIGI search failed: {ex.Message}");
            }
        });

        app.MapDelete("/api/symbols/search/cache", (SymbolSearchService searchService) =>
        {
            try
            {
                searchService.ClearCache();
                return Results.Ok(new { message = "Symbol search cache cleared" });
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to clear cache: {ex.Message}");
            }
        });

        // ==================== INDEX SUBSCRIPTIONS ====================

        app.MapGet("/api/symbols/indices", (IndexSubscriptionService indexService) =>
        {
            try
            {
                var indices = indexService.GetAvailableIndices();
                return Results.Json(indices, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get indices: {ex.Message}");
            }
        });

        app.MapGet("/api/symbols/indices/{indexId}/components", async (
            IndexSubscriptionService indexService,
            string indexId) =>
        {
            try
            {
                var components = await indexService.GetIndexComponentsAsync(indexId);
                return components is null
                    ? Results.NotFound($"Index '{indexId}' not found")
                    : Results.Json(components, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get index components: {ex.Message}");
            }
        });

        app.MapGet("/api/symbols/indices/{indexId}/status", async (
            IndexSubscriptionService indexService,
            string indexId) =>
        {
            try
            {
                var status = await indexService.GetSubscriptionStatusAsync(indexId);
                return Results.Json(status, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get subscription status: {ex.Message}");
            }
        });

        app.MapPost("/api/symbols/indices/{indexId}/subscribe", async (
            IndexSubscriptionService indexService,
            string indexId,
            IndexSubscribeRequestDto? dto) =>
        {
            try
            {
                var request = new IndexSubscribeRequest(
                    IndexId: indexId,
                    MaxComponents: dto?.MaxComponents,
                    Defaults: dto?.Defaults,
                    ReplaceExisting: dto?.ReplaceExisting ?? false,
                    FilterSectors: dto?.FilterSectors
                );

                var result = await indexService.SubscribeToIndexAsync(request);
                return Results.Json(result, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to subscribe to index: {ex.Message}");
            }
        });

        app.MapPost("/api/symbols/indices/{indexId}/unsubscribe", async (
            IndexSubscriptionService indexService,
            string indexId) =>
        {
            try
            {
                var result = await indexService.UnsubscribeFromIndexAsync(indexId);
                return Results.Json(result, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to unsubscribe from index: {ex.Message}");
            }
        });
    }
}
