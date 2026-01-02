using System.Text.Json;
using MarketDataCollector.Application.Backfill;
using MarketDataCollector.Application.Config;
using MarketDataCollector.Application.Subscriptions.Models;
using MarketDataCollector.Application.Subscriptions.Services;
using MarketDataCollector.Storage;
using MarketDataCollector.Storage.Interfaces;
using MarketDataCollector.Storage.Services;
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

        // Storage organization services
        var config = store.Load();
        var storageOptions = config.Storage?.ToStorageOptions(config.DataRoot, config.Compress) ?? new StorageOptions { RootPath = config.DataRoot, Compress = config.Compress };
        builder.Services.AddSingleton(storageOptions);
        builder.Services.AddSingleton<ISourceRegistry>(sp => new SourceRegistry(config.Sources?.PersistencePath));
        builder.Services.AddSingleton<IFileMaintenanceService, FileMaintenanceService>();
        builder.Services.AddSingleton<IDataQualityService, DataQualityService>();
        builder.Services.AddSingleton<IStorageSearchService, StorageSearchService>();
        builder.Services.AddSingleton<ITierMigrationService, TierMigrationService>();

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
        ConfigureStorageOrganizationRoutes();
    }

    private void ConfigureStorageOrganizationRoutes()
    {
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        // ==================== DATA CATALOG & SEARCH ====================

        _app.MapGet("/api/storage/catalog", async (IStorageSearchService search) =>
        {
            try
            {
                var catalog = await search.DiscoverAsync(new DiscoveryQuery());
                return Results.Json(catalog, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to discover catalog: {ex.Message}");
            }
        });

        _app.MapPost("/api/storage/search/files", async (IStorageSearchService search, FileSearchRequest req) =>
        {
            try
            {
                var query = new FileSearchQuery(
                    Symbols: req.Symbols,
                    Types: req.Types?.Select(t => Enum.Parse<Domain.Events.MarketEventType>(t, true)).ToArray(),
                    Sources: req.Sources,
                    From: req.From,
                    To: req.To,
                    MinSize: req.MinSize,
                    MaxSize: req.MaxSize,
                    MinQualityScore: req.MinQualityScore,
                    PathPattern: req.PathPattern,
                    Skip: req.Skip,
                    Take: req.Take
                );
                var results = await search.SearchFilesAsync(query);
                return Results.Json(results, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Search failed: {ex.Message}");
            }
        });

        _app.MapPost("/api/storage/search/faceted", async (IStorageSearchService search, FacetedSearchRequest req) =>
        {
            try
            {
                var query = new FacetedSearchQuery(
                    Symbols: req.Symbols,
                    Types: req.Types?.Select(t => Enum.Parse<Domain.Events.MarketEventType>(t, true)).ToArray(),
                    Sources: req.Sources,
                    From: req.From,
                    To: req.To,
                    MaxResults: req.MaxResults
                );
                var results = await search.SearchWithFacetsAsync(query);
                return Results.Json(results, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Faceted search failed: {ex.Message}");
            }
        });

        _app.MapPost("/api/storage/search/natural", (IStorageSearchService search, NaturalSearchRequest req) =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(req.Query))
                    return Results.BadRequest("Query is required");

                var query = search.ParseNaturalLanguageQuery(req.Query);
                return Results.Json(query, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Natural language parsing failed: {ex.Message}");
            }
        });

        _app.MapPost("/api/storage/index/rebuild", async (IStorageSearchService search, HttpRequest request) =>
        {
            try
            {
                var paths = Array.Empty<string>();
                await search.RebuildIndexAsync(paths, new RebuildOptions());
                return Results.Ok(new { message = "Index rebuild started" });
            }
            catch (Exception ex)
            {
                return Results.Problem($"Index rebuild failed: {ex.Message}");
            }
        });

        // ==================== HEALTH & MAINTENANCE ====================

        _app.MapPost("/api/storage/health/check", async (IFileMaintenanceService maintenance, HealthCheckRequest req) =>
        {
            try
            {
                var options = new HealthCheckOptions(
                    ValidateChecksums: req.ValidateChecksums,
                    CheckSequenceContinuity: req.CheckSequenceContinuity,
                    IdentifyCorruption: req.IdentifyCorruption,
                    Paths: req.Paths,
                    ParallelChecks: req.ParallelChecks
                );
                var report = await maintenance.RunHealthCheckAsync(options);
                return Results.Json(report, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Health check failed: {ex.Message}");
            }
        });

        _app.MapGet("/api/storage/health/orphans", async (IFileMaintenanceService maintenance) =>
        {
            try
            {
                var report = await maintenance.FindOrphansAsync();
                return Results.Json(report, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Orphan scan failed: {ex.Message}");
            }
        });

        _app.MapPost("/api/storage/maintenance/repair", async (IFileMaintenanceService maintenance, RepairRequest req) =>
        {
            try
            {
                var strategy = Enum.Parse<RepairStrategy>(req.Strategy ?? "TruncateCorrupted", true);
                var options = new RepairOptions(
                    Strategy: strategy,
                    DryRun: req.DryRun,
                    BackupBeforeRepair: req.BackupBeforeRepair,
                    BackupPath: req.BackupPath
                );
                var result = await maintenance.RepairAsync(options);
                return Results.Json(result, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Repair failed: {ex.Message}");
            }
        });

        _app.MapPost("/api/storage/maintenance/defrag", async (IFileMaintenanceService maintenance, DefragRequest req) =>
        {
            try
            {
                var options = new DefragOptions(
                    MinFileSizeBytes: req.MinFileSizeBytes,
                    MaxFilesPerMerge: req.MaxFilesPerMerge,
                    PreserveOriginals: req.PreserveOriginals
                );
                var result = await maintenance.DefragmentAsync(options);
                return Results.Json(result, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Defragmentation failed: {ex.Message}");
            }
        });

        // ==================== DATA QUALITY ====================

        _app.MapPost("/api/storage/quality/score", async (IDataQualityService quality, string path) =>
        {
            try
            {
                var score = await quality.ScoreAsync(path);
                return Results.Json(score, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Quality scoring failed: {ex.Message}");
            }
        });

        _app.MapPost("/api/storage/quality/report", async (IDataQualityService quality, QualityReportRequest req) =>
        {
            try
            {
                var options = new QualityReportOptions(
                    Paths: req.Paths ?? Array.Empty<string>(),
                    From: req.From,
                    To: req.To,
                    MinScoreThreshold: req.MinScoreThreshold,
                    IncludeRecommendations: req.IncludeRecommendations
                );
                var report = await quality.GenerateReportAsync(options);
                return Results.Json(report, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Quality report failed: {ex.Message}");
            }
        });

        _app.MapGet("/api/storage/quality/alerts", async (IDataQualityService quality) =>
        {
            try
            {
                var alerts = await quality.GetQualityAlertsAsync();
                return Results.Json(alerts, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get quality alerts: {ex.Message}");
            }
        });

        _app.MapGet("/api/storage/quality/sources/{symbol}", async (IDataQualityService quality, string symbol, DateTimeOffset? date) =>
        {
            try
            {
                var rankings = await quality.RankSourcesAsync(
                    symbol,
                    date ?? DateTimeOffset.UtcNow.Date,
                    Domain.Events.MarketEventType.Trade);
                return Results.Json(rankings, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Source ranking failed: {ex.Message}");
            }
        });

        _app.MapGet("/api/storage/quality/trend/{symbol}", async (IDataQualityService quality, string symbol, int? days) =>
        {
            try
            {
                var window = TimeSpan.FromDays(days ?? 30);
                var trend = await quality.GetTrendAsync(symbol, window);
                return Results.Json(trend, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Trend analysis failed: {ex.Message}");
            }
        });

        // ==================== TIER MIGRATION ====================

        _app.MapGet("/api/storage/tiers/statistics", async (ITierMigrationService tiers) =>
        {
            try
            {
                var stats = await tiers.GetTierStatisticsAsync();
                return Results.Json(stats, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get tier statistics: {ex.Message}");
            }
        });

        _app.MapPost("/api/storage/tiers/plan", async (ITierMigrationService tiers, int? horizonDays) =>
        {
            try
            {
                var plan = await tiers.PlanMigrationAsync(TimeSpan.FromDays(horizonDays ?? 7));
                return Results.Json(plan, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Migration planning failed: {ex.Message}");
            }
        });

        _app.MapPost("/api/storage/tiers/migrate", async (ITierMigrationService tiers, TierMigrationRequest req) =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(req.SourcePath))
                    return Results.BadRequest("SourcePath is required");

                var tier = Enum.Parse<StorageTier>(req.TargetTier ?? "Warm", true);
                var options = new MigrationOptions(
                    DeleteSource: req.DeleteSource,
                    VerifyChecksum: req.VerifyChecksum,
                    ParallelFiles: req.ParallelFiles
                );
                var result = await tiers.MigrateAsync(req.SourcePath, tier, options);
                return Results.Json(result, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Migration failed: {ex.Message}");
            }
        });

        _app.MapGet("/api/storage/tiers/target", (ITierMigrationService tiers, string path) =>
        {
            try
            {
                var tier = tiers.DetermineTargetTier(path);
                return Results.Json(new { path, targetTier = tier.ToString() }, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to determine tier: {ex.Message}");
            }
        });

        // ==================== SOURCE REGISTRY ====================

        _app.MapGet("/api/storage/sources", (ISourceRegistry registry) =>
        {
            try
            {
                var sources = registry.GetAllSources();
                return Results.Json(sources, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get sources: {ex.Message}");
            }
        });

        _app.MapGet("/api/storage/sources/{sourceId}", (ISourceRegistry registry, string sourceId) =>
        {
            try
            {
                var source = registry.GetSourceInfo(sourceId);
                return source is null
                    ? Results.NotFound($"Source '{sourceId}' not found")
                    : Results.Json(source, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get source: {ex.Message}");
            }
        });

        _app.MapPost("/api/storage/sources", (ISourceRegistry registry, SourceInfo source) =>
        {
            try
            {
                registry.RegisterSource(source);
                return Results.Ok();
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to register source: {ex.Message}");
            }
        });

        _app.MapGet("/api/storage/sources/priority", (ISourceRegistry registry) =>
        {
            try
            {
                var order = registry.GetSourcePriorityOrder();
                return Results.Json(order, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get priority order: {ex.Message}");
            }
        });

        _app.MapGet("/api/storage/symbols/{symbol}", (ISourceRegistry registry, string symbol) =>
        {
            try
            {
                var info = registry.GetSymbolInfo(symbol);
                return info is null
                    ? Results.NotFound($"Symbol '{symbol}' not found in registry")
                    : Results.Json(info, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get symbol info: {ex.Message}");
            }
        });

        _app.MapPost("/api/storage/symbols/register", (ISourceRegistry registry, SymbolInfo symbol) =>
        {
            try
            {
                registry.RegisterSymbol(symbol);
                return Results.Ok();
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to register symbol: {ex.Message}");
            }
        });

        _app.MapGet("/api/storage/symbols/resolve/{alias}", (ISourceRegistry registry, string alias) =>
        {
            try
            {
                var canonical = registry.ResolveSymbolAlias(alias);
                return Results.Json(new { alias, canonical }, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to resolve alias: {ex.Message}");
            }
        });

        // ==================== STORAGE OVERVIEW ====================

        _app.MapGet("/api/storage/overview", async (
            IStorageSearchService search,
            IDataQualityService quality,
            ITierMigrationService tiers,
            ConfigStore store) =>
        {
            try
            {
                var catalog = await search.DiscoverAsync(new DiscoveryQuery());
                var tierStats = await tiers.GetTierStatisticsAsync();
                var alerts = await quality.GetQualityAlertsAsync();
                var config = store.Load();

                return Results.Json(new
                {
                    dataRoot = config.DataRoot,
                    namingConvention = config.Storage?.NamingConvention ?? "BySymbol",
                    datePartition = config.Storage?.DatePartition ?? "Daily",
                    totalSymbols = catalog.Symbols.Count,
                    totalFiles = catalog.Symbols.Sum(s => s.TotalEvents > 0 ? 1 : 0),
                    totalBytes = catalog.TotalBytes,
                    totalEvents = catalog.TotalEvents,
                    dateRange = new { start = catalog.DateRange.Start, end = catalog.DateRange.End },
                    sources = catalog.Sources,
                    eventTypes = catalog.EventTypes,
                    tiers = tierStats.TierInfo,
                    qualityAlerts = alerts.Length
                }, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get storage overview: {ex.Message}");
            }
        });
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

// Storage organization DTOs
public record FileSearchRequest(
    string[]? Symbols = null,
    string[]? Types = null,
    string[]? Sources = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    long? MinSize = null,
    long? MaxSize = null,
    double? MinQualityScore = null,
    string? PathPattern = null,
    int Skip = 0,
    int Take = 100
);

public record FacetedSearchRequest(
    string[]? Symbols = null,
    string[]? Types = null,
    string[]? Sources = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    int MaxResults = 100
);

public record NaturalSearchRequest(string Query);

public record HealthCheckRequest(
    bool ValidateChecksums = true,
    bool CheckSequenceContinuity = true,
    bool IdentifyCorruption = true,
    string[]? Paths = null,
    int ParallelChecks = 4
);

public record RepairRequest(
    string? Strategy = null,
    bool DryRun = false,
    bool BackupBeforeRepair = true,
    string? BackupPath = null
);

public record DefragRequest(
    long MinFileSizeBytes = 1_048_576,
    int MaxFilesPerMerge = 100,
    bool PreserveOriginals = false
);

public record QualityReportRequest(
    string[]? Paths = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    double MinScoreThreshold = 1.0,
    bool IncludeRecommendations = true
);

public record TierMigrationRequest(
    string SourcePath,
    string? TargetTier = null,
    bool DeleteSource = false,
    bool VerifyChecksum = true,
    int ParallelFiles = 4
);
