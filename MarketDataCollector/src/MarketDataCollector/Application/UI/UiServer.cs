using System.Text.Json;
using MarketDataCollector.Application.Backfill;
using MarketDataCollector.Application.Config;
using MarketDataCollector.Application.Subscriptions.Models;
using MarketDataCollector.Application.Subscriptions.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MarketDataCollector.Application.UI;

public sealed class UiServer : IAsyncDisposable
{
    private readonly WebApplication _app;
    private readonly string _configPath;

    public UiServer(string configPath, int port = 8080)
    {
        _configPath = configPath;

        var builder = WebApplication.CreateBuilder();

        // Minimize logging from ASP.NET Core
        builder.Logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Warning);

        builder.WebHost.UseUrls($"http://localhost:{port}");

        var store = new ConfigStore(configPath);
        builder.Services.AddSingleton(store);
        builder.Services.AddSingleton<BackfillCoordinator>();

        // Symbol management services
        builder.Services.AddSingleton<SymbolImportExportService>();
        builder.Services.AddSingleton<TemplateService>();
        builder.Services.AddSingleton<SchedulingService>();
        builder.Services.AddSingleton<MetadataEnrichmentService>();
        builder.Services.AddSingleton<IndexSubscriptionService>();

        _app = builder.Build();

        ConfigureRoutes();
    }

    private void ConfigureRoutes()
    {
        _app.MapGet("/", (ConfigStore store) =>
        {
            var html = HtmlTemplates.Index(
                store.ConfigPath,
                store.GetStatusPath(),
                store.GetBackfillStatusPath());
            return Results.Content(html, "text/html");
        });

        _app.MapGet("/api/config", (ConfigStore store) =>
        {
            var cfg = store.Load();
            return Results.Json(new
            {
                dataRoot = cfg.DataRoot,
                compress = cfg.Compress,
                dataSource = cfg.DataSource.ToString(),
                alpaca = cfg.Alpaca,
                storage = cfg.Storage,
                symbols = cfg.Symbols ?? Array.Empty<SymbolConfig>(),
                backfill = cfg.Backfill
            }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        });

        _app.MapPost("/api/config/datasource", async (ConfigStore store, DataSourceRequest req) =>
        {
            try
            {
                var cfg = store.Load();

                if (!Enum.TryParse<DataSourceKind>(req.DataSource, ignoreCase: true, out var ds))
                    return Results.BadRequest("Invalid DataSource. Use 'IB', 'Alpaca', or 'Polygon'.");

                var next = cfg with { DataSource = ds };
                await store.SaveAsync(next);

                return Results.Ok();
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to update data source: {ex.Message}");
            }
        });

        _app.MapPost("/api/config/alpaca", async (ConfigStore store, AlpacaOptions alpaca) =>
        {
            try
            {
                var cfg = store.Load();
                var next = cfg with { Alpaca = alpaca };
                await store.SaveAsync(next);
                return Results.Ok();
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to save Alpaca settings: {ex.Message}");
            }
        });

        _app.MapPost("/api/config/storage", async (ConfigStore store, StorageSettingsRequest req) =>
        {
            try
            {
                var cfg = store.Load();
                var storage = new StorageConfig(
                    NamingConvention: req.NamingConvention ?? "BySymbol",
                    DatePartition: req.DatePartition ?? "Daily",
                    IncludeProvider: req.IncludeProvider,
                    FilePrefix: string.IsNullOrWhiteSpace(req.FilePrefix) ? null : req.FilePrefix
                );
                var next = cfg with
                {
                    DataRoot = string.IsNullOrWhiteSpace(req.DataRoot) ? "data" : req.DataRoot,
                    Compress = req.Compress,
                    Storage = storage
                };
                await store.SaveAsync(next);
                return Results.Ok();
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to save storage settings: {ex.Message}");
            }
        });

        _app.MapPost("/api/config/symbols", async (ConfigStore store, SymbolConfig symbol) =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(symbol.Symbol))
                    return Results.BadRequest("Symbol is required.");

                var cfg = store.Load();

                var list = (cfg.Symbols ?? Array.Empty<SymbolConfig>()).ToList();
                var idx = list.FindIndex(s => string.Equals(s.Symbol, symbol.Symbol, StringComparison.OrdinalIgnoreCase));
                if (idx >= 0) list[idx] = symbol;
                else list.Add(symbol);

                var next = cfg with { Symbols = list.ToArray() };
                await store.SaveAsync(next);

                return Results.Ok();
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to add symbol: {ex.Message}");
            }
        });

        _app.MapDelete("/api/config/symbols/{symbol}", async (ConfigStore store, string symbol) =>
        {
            try
            {
                var cfg = store.Load();
                var list = (cfg.Symbols ?? Array.Empty<SymbolConfig>()).ToList();
                list.RemoveAll(s => string.Equals(s.Symbol, symbol, StringComparison.OrdinalIgnoreCase));
                var next = cfg with { Symbols = list.ToArray() };
                await store.SaveAsync(next);
                return Results.Ok();
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to delete symbol: {ex.Message}");
            }
        });

        _app.MapGet("/api/status", (ConfigStore store) =>
        {
            try
            {
                var status = store.TryLoadStatusJson();
                return status is null ? Results.NotFound() : Results.Content(status, "application/json");
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to load status: {ex.Message}");
            }
        });

        _app.MapGet("/api/backfill/providers", (BackfillCoordinator backfill) =>
        {
            try
            {
                var providers = backfill.DescribeProviders();
                return Results.Json(providers, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get providers: {ex.Message}");
            }
        });

        _app.MapGet("/api/backfill/status", (BackfillCoordinator backfill) =>
        {
            try
            {
                var status = backfill.TryReadLast();
                return status is null
                    ? Results.NotFound()
                    : Results.Json(status, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true });
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to load backfill status: {ex.Message}");
            }
        });

        _app.MapPost("/api/backfill/run", async (BackfillCoordinator backfill, BackfillRequestDto req) =>
        {
            try
            {
                if (req.Symbols is null || req.Symbols.Length == 0)
                    return Results.BadRequest("At least one symbol is required.");

                var request = new BackfillRequest(
                    string.IsNullOrWhiteSpace(req.Provider) ? "composite" : req.Provider!,
                    req.Symbols,
                    req.From,
                    req.To);

                var result = await backfill.RunAsync(request);
                return Results.Json(result, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Backfill failed: {ex.Message}");
            }
        });

        _app.MapGet("/api/backfill/health", async (BackfillCoordinator backfill) =>
        {
            try
            {
                var health = await backfill.CheckProviderHealthAsync();
                return Results.Json(health, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            }
            catch (Exception ex)
            {
                return Results.Problem($"Health check failed: {ex.Message}");
            }
        });

        _app.MapGet("/api/backfill/resolve/{symbol}", async (BackfillCoordinator backfill, string symbol) =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(symbol))
                    return Results.BadRequest("Symbol is required.");

                var resolution = await backfill.ResolveSymbolAsync(symbol);
                if (resolution is null)
                    return Results.NotFound($"Symbol '{symbol}' not found.");

                return Results.Json(resolution, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            }
            catch (Exception ex)
            {
                return Results.Problem($"Symbol resolution failed: {ex.Message}");
            }
        });

        ConfigureSymbolManagementRoutes();
    }

    private void ConfigureSymbolManagementRoutes()
    {
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        // ==================== CSV BULK IMPORT/EXPORT ====================

        _app.MapPost("/api/symbols/bulk-import", async (
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
                return Results.Json(result, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Import failed: {ex.Message}");
            }
        });

        _app.MapGet("/api/symbols/bulk-export", (SymbolImportExportService importExport, HttpRequest request) =>
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

        _app.MapGet("/api/symbols/bulk-export/download", (SymbolImportExportService importExport, HttpRequest request) =>
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

        _app.MapGet("/api/symbols/templates", async (TemplateService templates) =>
        {
            try
            {
                var all = await templates.GetAllTemplatesAsync();
                return Results.Json(all, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get templates: {ex.Message}");
            }
        });

        _app.MapGet("/api/symbols/templates/{templateId}", async (TemplateService templates, string templateId) =>
        {
            try
            {
                var template = await templates.GetTemplateAsync(templateId);
                return template is null
                    ? Results.NotFound($"Template '{templateId}' not found")
                    : Results.Json(template, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get template: {ex.Message}");
            }
        });

        _app.MapPost("/api/symbols/templates/apply", async (TemplateService templates, ApplyTemplateRequest request) =>
        {
            try
            {
                var result = await templates.ApplyTemplateAsync(request);
                return Results.Json(result, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to apply template: {ex.Message}");
            }
        });

        _app.MapPost("/api/symbols/templates", async (TemplateService templates, CreateTemplateDto dto) =>
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

                return Results.Json(template, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to create template: {ex.Message}");
            }
        });

        _app.MapPost("/api/symbols/templates/from-current", async (TemplateService templates, CreateFromCurrentDto dto) =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(dto.Name))
                    return Results.BadRequest("Template name is required.");

                var template = await templates.CreateFromCurrentAsync(dto.Name, dto.Description ?? "");
                return Results.Json(template, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to create template: {ex.Message}");
            }
        });

        _app.MapDelete("/api/symbols/templates/{templateId}", async (TemplateService templates, string templateId) =>
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

        _app.MapGet("/api/symbols/schedules", (SchedulingService scheduling) =>
        {
            try
            {
                var schedules = scheduling.GetAllSchedules();
                return Results.Json(schedules, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get schedules: {ex.Message}");
            }
        });

        _app.MapGet("/api/symbols/schedules/{scheduleId}", (SchedulingService scheduling, string scheduleId) =>
        {
            try
            {
                var schedule = scheduling.GetSchedule(scheduleId);
                return schedule is null
                    ? Results.NotFound($"Schedule '{scheduleId}' not found")
                    : Results.Json(schedule, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get schedule: {ex.Message}");
            }
        });

        _app.MapGet("/api/symbols/schedules/{scheduleId}/status", (SchedulingService scheduling, string scheduleId) =>
        {
            try
            {
                var status = scheduling.GetExecutionStatus(scheduleId);
                return status is null
                    ? Results.NotFound($"No execution status for schedule '{scheduleId}'")
                    : Results.Json(status, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get schedule status: {ex.Message}");
            }
        });

        _app.MapPost("/api/symbols/schedules", async (SchedulingService scheduling, CreateScheduleRequest request) =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Name))
                    return Results.BadRequest("Schedule name is required.");

                if (request.Symbols is null || request.Symbols.Length == 0)
                    return Results.BadRequest("At least one symbol is required.");

                var schedule = await scheduling.CreateScheduleAsync(request);
                return Results.Json(schedule, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to create schedule: {ex.Message}");
            }
        });

        _app.MapPut("/api/symbols/schedules/{scheduleId}", async (
            SchedulingService scheduling,
            string scheduleId,
            CreateScheduleRequest request) =>
        {
            try
            {
                var schedule = await scheduling.UpdateScheduleAsync(scheduleId, request);
                return schedule is null
                    ? Results.NotFound($"Schedule '{scheduleId}' not found")
                    : Results.Json(schedule, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to update schedule: {ex.Message}");
            }
        });

        _app.MapPost("/api/symbols/schedules/{scheduleId}/enable", async (
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

        _app.MapPost("/api/symbols/schedules/{scheduleId}/disable", async (
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

        _app.MapPost("/api/symbols/schedules/{scheduleId}/execute", async (
            SchedulingService scheduling,
            string scheduleId) =>
        {
            try
            {
                var status = await scheduling.ExecuteNowAsync(scheduleId);
                return Results.Json(status, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to execute schedule: {ex.Message}");
            }
        });

        _app.MapDelete("/api/symbols/schedules/{scheduleId}", async (SchedulingService scheduling, string scheduleId) =>
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

        _app.MapGet("/api/symbols/metadata/{symbol}", async (MetadataEnrichmentService metadata, string symbol) =>
        {
            try
            {
                var meta = await metadata.GetMetadataAsync(symbol);
                return meta is null
                    ? Results.NotFound($"No metadata found for symbol '{symbol}'")
                    : Results.Json(meta, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get metadata: {ex.Message}");
            }
        });

        _app.MapPost("/api/symbols/metadata/batch", async (
            MetadataEnrichmentService metadata,
            string[] symbols) =>
        {
            try
            {
                var result = await metadata.GetMetadataBatchAsync(symbols);
                return Results.Json(result, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get metadata: {ex.Message}");
            }
        });

        _app.MapPost("/api/symbols/metadata/filter", async (
            MetadataEnrichmentService metadata,
            SymbolMetadataFilter filter) =>
        {
            try
            {
                var result = await metadata.FilterSymbolsAsync(filter);
                return Results.Json(result, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to filter symbols: {ex.Message}");
            }
        });

        _app.MapGet("/api/symbols/metadata/sectors", async (MetadataEnrichmentService metadata) =>
        {
            try
            {
                var sectors = await metadata.GetAvailableSectorsAsync();
                return Results.Json(sectors, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get sectors: {ex.Message}");
            }
        });

        _app.MapGet("/api/symbols/metadata/industries", async (
            MetadataEnrichmentService metadata,
            HttpRequest request) =>
        {
            try
            {
                var sector = request.Query["sector"].FirstOrDefault();
                var industries = await metadata.GetAvailableIndustriesAsync(sector);
                return Results.Json(industries, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get industries: {ex.Message}");
            }
        });

        _app.MapPost("/api/symbols/metadata", async (
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

        // ==================== INDEX SUBSCRIPTIONS ====================

        _app.MapGet("/api/symbols/indices", (IndexSubscriptionService indexService) =>
        {
            try
            {
                var indices = indexService.GetAvailableIndices();
                return Results.Json(indices, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get indices: {ex.Message}");
            }
        });

        _app.MapGet("/api/symbols/indices/{indexId}/components", async (
            IndexSubscriptionService indexService,
            string indexId) =>
        {
            try
            {
                var components = await indexService.GetIndexComponentsAsync(indexId);
                return components is null
                    ? Results.NotFound($"Index '{indexId}' not found")
                    : Results.Json(components, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get index components: {ex.Message}");
            }
        });

        _app.MapGet("/api/symbols/indices/{indexId}/status", async (
            IndexSubscriptionService indexService,
            string indexId) =>
        {
            try
            {
                var status = await indexService.GetSubscriptionStatusAsync(indexId);
                return Results.Json(status, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get subscription status: {ex.Message}");
            }
        });

        _app.MapPost("/api/symbols/indices/{indexId}/subscribe", async (
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
                return Results.Json(result, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to subscribe to index: {ex.Message}");
            }
        });

        _app.MapPost("/api/symbols/indices/{indexId}/unsubscribe", async (
            IndexSubscriptionService indexService,
            string indexId) =>
        {
            try
            {
                var result = await indexService.UnsubscribeFromIndexAsync(indexId);
                return Results.Json(result, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to unsubscribe from index: {ex.Message}");
            }
        });
    }

    public async Task StartAsync()
    {
        await _app.StartAsync();
    }

    public async Task StopAsync()
    {
        await _app.StopAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _app.DisposeAsync();
    }
}

public record DataSourceRequest(string DataSource);
public record StorageSettingsRequest(string? DataRoot, bool Compress, string? NamingConvention, string? DatePartition, bool IncludeProvider, string? FilePrefix);
public record BackfillRequestDto(string? Provider, string[] Symbols, DateOnly? From, DateOnly? To);

// Symbol management DTOs
public record CreateTemplateDto(
    string Name,
    string? Description,
    TemplateCategory Category,
    string[] Symbols,
    TemplateSubscriptionDefaults? Defaults
);

public record CreateFromCurrentDto(string Name, string? Description);

public record IndexSubscribeRequestDto(
    int? MaxComponents,
    TemplateSubscriptionDefaults? Defaults,
    bool ReplaceExisting,
    string[]? FilterSectors
);
