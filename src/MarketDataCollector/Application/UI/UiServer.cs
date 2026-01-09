using System.Text.Json;
using MarketDataCollector.Application.Backfill;
using MarketDataCollector.Application.Config;
using MarketDataCollector.Application.Monitoring;
using MarketDataCollector.Application.Services;
using MarketDataCollector.Application.Subscriptions.Models;
using MarketDataCollector.Application.Subscriptions.Services;
using MarketDataCollector.Infrastructure.Providers.SymbolSearch;
using MarketDataCollector.Storage;
using MarketDataCollector.Storage.Interfaces;
using MarketDataCollector.Storage.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MarketDataCollector.Application.UI;

public sealed class UiServer : IAsyncDisposable
{
    private readonly WebApplication _app;
    private readonly string _configPath;
    private readonly DateTimeOffset _startTime = DateTimeOffset.UtcNow;

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
        builder.Services.AddSingleton<WatchlistService>();
        builder.Services.AddSingleton<BatchOperationsService>();
        builder.Services.AddSingleton<PortfolioImportService>();

        // Symbol search and autocomplete services
        builder.Services.AddSingleton<OpenFigiClient>();
        builder.Services.AddSingleton<SymbolSearchService>(sp =>
        {
            var metadataService = sp.GetRequiredService<MetadataEnrichmentService>();
            var figiClient = sp.GetRequiredService<OpenFigiClient>();
            return new SymbolSearchService(
                new ISymbolSearchProvider[]
                {
                    new AlpacaSymbolSearchProvider(),
                    new FinnhubSymbolSearchProvider(),
                    new PolygonSymbolSearchProvider()
                },
                figiClient,
                metadataService);
        });

        // Storage organization services
        var config = store.Load();
        var storageOptions = config.Storage?.ToStorageOptions(config.DataRoot, config.Compress) ?? new StorageOptions { RootPath = config.DataRoot, Compress = config.Compress };
        builder.Services.AddSingleton(storageOptions);
        builder.Services.AddSingleton<ISourceRegistry>(sp => new SourceRegistry(config.Sources?.PersistencePath));
        builder.Services.AddSingleton<IFileMaintenanceService, FileMaintenanceService>();
        builder.Services.AddSingleton<IDataQualityService, DataQualityService>();
        builder.Services.AddSingleton<IStorageSearchService, StorageSearchService>();
        builder.Services.AddSingleton<ITierMigrationService, TierMigrationService>();

        // New services for QW features
        builder.Services.AddSingleton(new HistoricalDataQueryService(config.DataRoot));
        builder.Services.AddSingleton(new DiagnosticBundleService(config.DataRoot, null, () => store.Load()));
        builder.Services.AddSingleton<SampleDataGenerator>();
        builder.Services.AddSingleton(new ErrorTracker(config.DataRoot));
        builder.Services.AddSingleton<ConfigTemplateGenerator>();
        builder.Services.AddSingleton<ConfigEnvironmentOverride>();
        builder.Services.AddSingleton<DryRunService>();
        builder.Services.AddSingleton<ApiDocumentationService>();

        _app = builder.Build();

        ConfigureRoutes();
    }

    private void ConfigureRoutes()
    {
        // ==================== HEALTH CHECK ENDPOINTS ====================
        // These endpoints support container orchestration (Docker, Kubernetes)

        _app.MapGet("/health", () =>
        {
            var uptime = DateTimeOffset.UtcNow - _startTime;
            return Results.Json(new
            {
                status = "healthy",
                timestamp = DateTimeOffset.UtcNow,
                uptime = uptime.ToString(),
                version = "1.1.0"
            });
        });

        _app.MapGet("/healthz", () => Results.Ok("healthy"));

        _app.MapGet("/ready", () => Results.Ok("ready"));

        _app.MapGet("/readyz", () => Results.Ok("ready"));

        _app.MapGet("/live", () => Results.Ok("alive"));

        _app.MapGet("/livez", () => Results.Ok("alive"));

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
        ConfigureNewFeatureRoutes();
        ConfigureBulkSymbolManagementRoutes();
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

        // ==================== SYMBOL SEARCH & AUTOCOMPLETE ====================

        _app.MapGet("/api/symbols/search", async (
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
                return Results.Json(result, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Symbol search failed: {ex.Message}");
            }
        });

        _app.MapPost("/api/symbols/search", async (
            SymbolSearchService searchService,
            SymbolSearchRequest request,
            CancellationToken ct) =>
        {
            try
            {
                var result = await searchService.SearchAsync(request, ct);
                return Results.Json(result, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Symbol search failed: {ex.Message}");
            }
        });

        _app.MapGet("/api/symbols/search/providers", async (
            SymbolSearchService searchService,
            CancellationToken ct) =>
        {
            try
            {
                var providers = await searchService.GetProvidersAsync(ct);
                return Results.Json(providers, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get providers: {ex.Message}");
            }
        });

        _app.MapGet("/api/symbols/details/{symbol}", async (
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

                return Results.Json(details, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get symbol details: {ex.Message}");
            }
        });

        _app.MapGet("/api/symbols/figi/{symbol}", async (
            SymbolSearchService searchService,
            string symbol,
            CancellationToken ct) =>
        {
            try
            {
                var figiMappings = await searchService.LookupFigiAsync(new[] { symbol }, ct);
                if (!figiMappings.TryGetValue(symbol.ToUpperInvariant(), out var mappings) || mappings.Count == 0)
                    return Results.NotFound($"No FIGI mappings found for '{symbol}'");

                return Results.Json(new { symbol = symbol.ToUpperInvariant(), mappings }, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"FIGI lookup failed: {ex.Message}");
            }
        });

        _app.MapPost("/api/symbols/figi/bulk", async (
            SymbolSearchService searchService,
            FigiBulkLookupRequest request,
            CancellationToken ct) =>
        {
            try
            {
                if (request.Symbols is null || request.Symbols.Length == 0)
                    return Results.BadRequest("At least one symbol is required");

                var figiMappings = await searchService.LookupFigiAsync(request.Symbols, ct);
                return Results.Json(figiMappings, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Bulk FIGI lookup failed: {ex.Message}");
            }
        });

        _app.MapGet("/api/symbols/figi/search", async (
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
                return Results.Json(new { query, count = results.Count, results }, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"FIGI search failed: {ex.Message}");
            }
        });

        _app.MapDelete("/api/symbols/search/cache", (SymbolSearchService searchService) =>
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

    private void ConfigureNewFeatureRoutes()
    {
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        // ==================== QW-15: HISTORICAL DATA QUERY ====================

        _app.MapGet("/api/historical/symbols", (HistoricalDataQueryService query) =>
        {
            try
            {
                var symbols = query.GetAvailableSymbols();
                return Results.Json(symbols, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get symbols: {ex.Message}");
            }
        });

        _app.MapGet("/api/historical/symbols/{symbol}/range", (HistoricalDataQueryService query, string symbol) =>
        {
            try
            {
                var range = query.GetDateRange(symbol);
                return Results.Json(range, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get date range: {ex.Message}");
            }
        });

        _app.MapPost("/api/historical/query", async (HistoricalDataQueryService query, HistoricalQueryRequest req) =>
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
                return Results.Json(result, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Query failed: {ex.Message}");
            }
        });

        // ==================== QW-16: DIAGNOSTIC BUNDLE ====================

        _app.MapPost("/api/diagnostics/bundle", async (DiagnosticBundleService diag, DiagnosticBundleRequest? req) =>
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
                return Results.Json(result, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Bundle generation failed: {ex.Message}");
            }
        });

        _app.MapGet("/api/diagnostics/bundle/{bundleId}/download", (DiagnosticBundleService diag, string bundleId) =>
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

        _app.MapPost("/api/tools/sample-data", (SampleDataGenerator gen, SampleDataRequest? req) =>
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
                }, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Sample data generation failed: {ex.Message}");
            }
        });

        _app.MapGet("/api/tools/sample-data/preview", (SampleDataGenerator gen) =>
        {
            try
            {
                var preview = gen.GeneratePreview(new SampleDataOptions(MaxEvents: 20));
                return Results.Json(preview, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Preview failed: {ex.Message}");
            }
        });

        // ==================== QW-58: LAST N ERRORS ====================

        _app.MapGet("/api/diagnostics/errors", (ErrorTracker errors, int? count, string? type, string? context) =>
        {
            try
            {
                var result = errors.GetLastErrors(count ?? 10, type, context);
                return Results.Json(result, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get errors: {ex.Message}");
            }
        });

        _app.MapGet("/api/diagnostics/errors/stats", (ErrorTracker errors, int? hours) =>
        {
            try
            {
                var window = hours.HasValue ? TimeSpan.FromHours(hours.Value) : TimeSpan.FromHours(24);
                var stats = errors.GetStatistics(window);
                return Results.Json(stats, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get error stats: {ex.Message}");
            }
        });

        _app.MapGet("/api/diagnostics/errors/logs", async (ErrorTracker errors, int? count, int? days) =>
        {
            try
            {
                var result = await errors.ParseErrorsFromLogsAsync(count ?? 100, days ?? 1);
                return Results.Json(result, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to parse log errors: {ex.Message}");
            }
        });

        // ==================== QW-76: CONFIG TEMPLATE GENERATOR ====================

        _app.MapGet("/api/tools/config-templates", (ConfigTemplateGenerator gen) =>
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
                }), jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get templates: {ex.Message}");
            }
        });

        _app.MapGet("/api/tools/config-templates/{name}", (ConfigTemplateGenerator gen, string name) =>
        {
            try
            {
                var template = gen.GetTemplate(name);
                if (template == null)
                    return Results.NotFound($"Template '{name}' not found");

                return Results.Json(template, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get template: {ex.Message}");
            }
        });

        _app.MapPost("/api/tools/config-templates/validate", (ConfigTemplateGenerator gen, ConfigValidateRequest req) =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(req.Json))
                    return Results.BadRequest("JSON configuration is required");

                var result = gen.ValidateTemplate(req.Json);
                return Results.Json(result, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Validation failed: {ex.Message}");
            }
        });

        // ==================== QW-25: CONFIG ENVIRONMENT OVERRIDE ====================

        _app.MapGet("/api/config/env-overrides", (ConfigEnvironmentOverride envOverride) =>
        {
            try
            {
                var variables = envOverride.GetRecognizedVariables();
                return Results.Json(variables, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get environment overrides: {ex.Message}");
            }
        });

        _app.MapGet("/api/config/env-overrides/docs", (ConfigEnvironmentOverride envOverride) =>
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

        _app.MapPost("/api/tools/dry-run", async (DryRunService dryRun, ConfigStore store, DryRunRequest? req) =>
        {
            try
            {
                var config = store.Load();
                var options = new DryRunOptions(
                    ValidateConfiguration: req?.ValidateConfiguration ?? true,
                    ValidateFileSystem: req?.ValidateFileSystem ?? true,
                    ValidateConnectivity: req?.ValidateConnectivity ?? true,
                    ValidateProviders: req?.ValidateProviders ?? true,
                    ValidateSymbols: req?.ValidateSymbols ?? true,
                    ValidateResources: req?.ValidateResources ?? true
                );

                var result = await dryRun.ValidateAsync(config, options);
                return Results.Json(result, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Dry run failed: {ex.Message}");
            }
        });

        _app.MapPost("/api/tools/dry-run/report", async (DryRunService dryRun, ConfigStore store) =>
        {
            try
            {
                var config = store.Load();
                var result = await dryRun.ValidateAsync(config, new DryRunOptions());
                var report = dryRun.GenerateReport(result);
                return Results.Text(report, "text/plain");
            }
            catch (Exception ex)
            {
                return Results.Problem($"Report generation failed: {ex.Message}");
            }
        });

        // ==================== DEV-9 & QW-121: API DOCUMENTATION ====================

        _app.MapGet("/api/openapi.json", (ApiDocumentationService apiDocs) =>
        {
            try
            {
                var spec = apiDocs.GenerateOpenApiSpec();
                return Results.Json(spec, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = true,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                });
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to generate OpenAPI spec: {ex.Message}");
            }
        });

        _app.MapGet("/api/docs", (ApiDocumentationService apiDocs) =>
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

        _app.MapGet("/api/docs/markdown", (ApiDocumentationService apiDocs) =>
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

        _app.MapGet("/swagger", (ApiDocumentationService apiDocs) =>
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

    private void ConfigureBulkSymbolManagementRoutes()
    {
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        // ==================== TEXT/CSV IMPORT ====================

        _app.MapPost("/api/symbols/import/text", async (
            SymbolImportExportService importExport,
            HttpRequest request) =>
        {
            try
            {
                using var reader = new StreamReader(request.Body);
                var content = await reader.ReadToEndAsync();

                if (string.IsNullOrWhiteSpace(content))
                    return Results.BadRequest("Content is required.");

                var options = new BulkImportOptions(
                    SkipExisting: request.Query["skipExisting"] != "false",
                    UpdateExisting: request.Query["updateExisting"] == "true",
                    HasHeader: false,
                    ValidateSymbols: request.Query["validate"] != "false"
                );

                var result = await importExport.ImportFromTextAsync(content, options);
                return Results.Json(result, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Text import failed: {ex.Message}");
            }
        });

        _app.MapPost("/api/symbols/import/auto", async (
            SymbolImportExportService importExport,
            HttpRequest request) =>
        {
            try
            {
                using var reader = new StreamReader(request.Body);
                var content = await reader.ReadToEndAsync();

                if (string.IsNullOrWhiteSpace(content))
                    return Results.BadRequest("Content is required.");

                var options = new BulkImportOptions(
                    SkipExisting: request.Query["skipExisting"] != "false",
                    UpdateExisting: request.Query["updateExisting"] == "true",
                    ValidateSymbols: request.Query["validate"] != "false"
                );

                var result = await importExport.ImportAutoDetectAsync(content, options);
                return Results.Json(result, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Import failed: {ex.Message}");
            }
        });

        // ==================== WATCHLISTS ====================

        _app.MapGet("/api/watchlists", async (WatchlistService watchlists) =>
        {
            try
            {
                var all = await watchlists.GetAllWatchlistsAsync();
                return Results.Json(all, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get watchlists: {ex.Message}");
            }
        });

        _app.MapGet("/api/watchlists/summaries", async (WatchlistService watchlists) =>
        {
            try
            {
                var summaries = await watchlists.GetWatchlistSummariesAsync();
                return Results.Json(summaries, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get watchlist summaries: {ex.Message}");
            }
        });

        _app.MapGet("/api/watchlists/{watchlistId}", async (WatchlistService watchlists, string watchlistId) =>
        {
            try
            {
                var watchlist = await watchlists.GetWatchlistAsync(watchlistId);
                return watchlist is null
                    ? Results.NotFound($"Watchlist '{watchlistId}' not found")
                    : Results.Json(watchlist, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get watchlist: {ex.Message}");
            }
        });

        _app.MapPost("/api/watchlists", async (WatchlistService watchlists, CreateWatchlistRequest request) =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Name))
                    return Results.BadRequest("Watchlist name is required.");

                var watchlist = await watchlists.CreateWatchlistAsync(request);
                return Results.Json(watchlist, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to create watchlist: {ex.Message}");
            }
        });

        _app.MapPut("/api/watchlists/{watchlistId}", async (
            WatchlistService watchlists,
            string watchlistId,
            UpdateWatchlistRequest request) =>
        {
            try
            {
                var updated = await watchlists.UpdateWatchlistAsync(watchlistId, request);
                return updated is null
                    ? Results.NotFound($"Watchlist '{watchlistId}' not found")
                    : Results.Json(updated, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to update watchlist: {ex.Message}");
            }
        });

        _app.MapDelete("/api/watchlists/{watchlistId}", async (WatchlistService watchlists, string watchlistId) =>
        {
            try
            {
                var deleted = await watchlists.DeleteWatchlistAsync(watchlistId);
                return deleted ? Results.Ok() : Results.NotFound($"Watchlist '{watchlistId}' not found");
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to delete watchlist: {ex.Message}");
            }
        });

        _app.MapPost("/api/watchlists/{watchlistId}/symbols", async (
            WatchlistService watchlists,
            string watchlistId,
            WatchlistSymbolsRequest request) =>
        {
            try
            {
                var result = await watchlists.AddSymbolsAsync(new AddSymbolsToWatchlistRequest(
                    WatchlistId: watchlistId,
                    Symbols: request.Symbols,
                    SubscribeImmediately: request.SubscribeImmediately
                ));
                return Results.Json(result, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to add symbols: {ex.Message}");
            }
        });

        _app.MapDelete("/api/watchlists/{watchlistId}/symbols", async (
            WatchlistService watchlists,
            string watchlistId,
            WatchlistSymbolsRequest request) =>
        {
            try
            {
                var result = await watchlists.RemoveSymbolsAsync(new RemoveSymbolsFromWatchlistRequest(
                    WatchlistId: watchlistId,
                    Symbols: request.Symbols,
                    UnsubscribeIfOrphaned: request.UnsubscribeIfOrphaned
                ));
                return Results.Json(result, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to remove symbols: {ex.Message}");
            }
        });

        _app.MapPost("/api/watchlists/{watchlistId}/subscribe", async (
            WatchlistService watchlists,
            string watchlistId,
            WatchlistSubscriptionRequest? request) =>
        {
            try
            {
                var result = await watchlists.SubscribeWatchlistAsync(new WatchlistSubscriptionRequest(
                    WatchlistId: watchlistId,
                    SubscribeTrades: request?.SubscribeTrades,
                    SubscribeDepth: request?.SubscribeDepth,
                    DepthLevels: request?.DepthLevels
                ));
                return Results.Json(result, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to subscribe watchlist: {ex.Message}");
            }
        });

        _app.MapPost("/api/watchlists/{watchlistId}/unsubscribe", async (
            WatchlistService watchlists,
            string watchlistId) =>
        {
            try
            {
                var result = await watchlists.UnsubscribeWatchlistAsync(watchlistId);
                return Results.Json(result, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to unsubscribe watchlist: {ex.Message}");
            }
        });

        _app.MapGet("/api/watchlists/{watchlistId}/export", async (WatchlistService watchlists, string watchlistId) =>
        {
            try
            {
                var json = await watchlists.ExportWatchlistAsync(watchlistId);
                return json is null
                    ? Results.NotFound($"Watchlist '{watchlistId}' not found")
                    : Results.Text(json, "application/json");
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to export watchlist: {ex.Message}");
            }
        });

        _app.MapPost("/api/watchlists/import", async (WatchlistService watchlists, HttpRequest request) =>
        {
            try
            {
                using var reader = new StreamReader(request.Body);
                var json = await reader.ReadToEndAsync();

                var watchlist = await watchlists.ImportWatchlistAsync(json);
                return watchlist is null
                    ? Results.BadRequest("Invalid watchlist JSON")
                    : Results.Json(watchlist, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to import watchlist: {ex.Message}");
            }
        });

        _app.MapPost("/api/watchlists/reorder", async (WatchlistService watchlists, string[] watchlistIds) =>
        {
            try
            {
                await watchlists.ReorderWatchlistsAsync(watchlistIds);
                return Results.Ok();
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to reorder watchlists: {ex.Message}");
            }
        });

        // ==================== PORTFOLIO IMPORT ====================

        _app.MapGet("/api/portfolio/brokers", (PortfolioImportService portfolio) =>
        {
            try
            {
                var brokers = portfolio.GetAvailableBrokers();
                return Results.Json(brokers, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get brokers: {ex.Message}");
            }
        });

        _app.MapGet("/api/portfolio/{broker}/summary", async (PortfolioImportService portfolio, string broker) =>
        {
            try
            {
                var summary = await portfolio.GetPortfolioSummaryAsync(broker);
                return summary is null
                    ? Results.NotFound($"Broker '{broker}' not configured or not available")
                    : Results.Json(summary, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get portfolio summary: {ex.Message}");
            }
        });

        _app.MapPost("/api/portfolio/{broker}/import", async (
            PortfolioImportService portfolio,
            string broker,
            PortfolioImportOptionsDto? options) =>
        {
            try
            {
                var importOptions = new PortfolioImportOptions(
                    MinPositionValue: options?.MinPositionValue,
                    MinQuantity: options?.MinQuantity,
                    AssetClasses: options?.AssetClasses,
                    ExcludeSymbols: options?.ExcludeSymbols,
                    LongOnly: options?.LongOnly ?? false,
                    CreateWatchlist: options?.CreateWatchlist ?? false,
                    WatchlistName: options?.WatchlistName,
                    SubscribeTrades: options?.SubscribeTrades ?? true,
                    SubscribeDepth: options?.SubscribeDepth ?? true,
                    SkipExisting: options?.SkipExisting ?? true
                );

                var result = await portfolio.ImportFromBrokerAsync(new PortfolioImportRequest(broker, importOptions));
                return Results.Json(result, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to import from portfolio: {ex.Message}");
            }
        });

        _app.MapPost("/api/portfolio/manual/import", async (
            PortfolioImportService portfolio,
            ManualPortfolioImportRequest request) =>
        {
            try
            {
                if (request.Entries is null || request.Entries.Length == 0)
                    return Results.BadRequest("At least one entry is required.");

                var entries = request.Entries.Select(e => new ManualPortfolioEntry(
                    Symbol: e.Symbol,
                    Quantity: e.Quantity,
                    AssetClass: e.AssetClass
                )).ToArray();

                var importOptions = new PortfolioImportOptions(
                    CreateWatchlist: request.CreateWatchlist,
                    WatchlistName: request.WatchlistName,
                    SubscribeTrades: request.SubscribeTrades,
                    SubscribeDepth: request.SubscribeDepth,
                    SkipExisting: request.SkipExisting
                );

                var result = await portfolio.ImportManualAsync(entries, importOptions);
                return Results.Json(result, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to import manual portfolio: {ex.Message}");
            }
        });

        // ==================== BATCH OPERATIONS ====================

        _app.MapPost("/api/symbols/batch/add", async (BatchOperationsService batch, BatchAddRequest request) =>
        {
            try
            {
                if (request.Symbols is null || request.Symbols.Length == 0)
                    return Results.BadRequest("At least one symbol is required.");

                var result = await batch.AddSymbolsAsync(request);
                return Results.Json(result, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Batch add failed: {ex.Message}");
            }
        });

        _app.MapPost("/api/symbols/batch/delete", async (BatchOperationsService batch, BatchDeleteRequest request) =>
        {
            try
            {
                if (request.Symbols is null || request.Symbols.Length == 0)
                    return Results.BadRequest("At least one symbol is required.");

                var result = await batch.DeleteSymbolsAsync(request);
                return Results.Json(result, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Batch delete failed: {ex.Message}");
            }
        });

        _app.MapPost("/api/symbols/batch/toggle", async (BatchOperationsService batch, BatchToggleRequest request) =>
        {
            try
            {
                if (request.Symbols is null || request.Symbols.Length == 0)
                    return Results.BadRequest("At least one symbol is required.");

                var result = await batch.ToggleSubscriptionsAsync(request);
                return Results.Json(result, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Batch toggle failed: {ex.Message}");
            }
        });

        _app.MapPost("/api/symbols/batch/update", async (BatchOperationsService batch, BatchUpdateRequest request) =>
        {
            try
            {
                if (request.Symbols is null || request.Symbols.Length == 0)
                    return Results.BadRequest("At least one symbol is required.");

                var result = await batch.UpdateSymbolsAsync(request);
                return Results.Json(result, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Batch update failed: {ex.Message}");
            }
        });

        _app.MapPost("/api/symbols/batch/enable-trades", async (BatchOperationsService batch, string[] symbols) =>
        {
            try
            {
                if (symbols is null || symbols.Length == 0)
                    return Results.BadRequest("At least one symbol is required.");

                var result = await batch.EnableTradesAsync(symbols);
                return Results.Json(result, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Enable trades failed: {ex.Message}");
            }
        });

        _app.MapPost("/api/symbols/batch/disable-trades", async (BatchOperationsService batch, string[] symbols) =>
        {
            try
            {
                if (symbols is null || symbols.Length == 0)
                    return Results.BadRequest("At least one symbol is required.");

                var result = await batch.DisableTradesAsync(symbols);
                return Results.Json(result, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Disable trades failed: {ex.Message}");
            }
        });

        _app.MapPost("/api/symbols/batch/enable-depth", async (BatchOperationsService batch, string[] symbols) =>
        {
            try
            {
                if (symbols is null || symbols.Length == 0)
                    return Results.BadRequest("At least one symbol is required.");

                var result = await batch.EnableDepthAsync(symbols);
                return Results.Json(result, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Enable depth failed: {ex.Message}");
            }
        });

        _app.MapPost("/api/symbols/batch/disable-depth", async (BatchOperationsService batch, string[] symbols) =>
        {
            try
            {
                if (symbols is null || symbols.Length == 0)
                    return Results.BadRequest("At least one symbol is required.");

                var result = await batch.DisableDepthAsync(symbols);
                return Results.Json(result, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Disable depth failed: {ex.Message}");
            }
        });

        _app.MapPost("/api/symbols/batch/copy-settings", async (
            BatchOperationsService batch,
            BatchCopySettingsRequest request) =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.SourceSymbol))
                    return Results.BadRequest("Source symbol is required.");

                if (request.TargetSymbols is null || request.TargetSymbols.Length == 0)
                    return Results.BadRequest("At least one target symbol is required.");

                var result = await batch.CopySettingsAsync(request);
                return Results.Json(result, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Copy settings failed: {ex.Message}");
            }
        });

        _app.MapPost("/api/symbols/batch/move-to-watchlist", async (
            BatchOperationsService batch,
            BatchMoveToWatchlistRequest request) =>
        {
            try
            {
                if (request.Symbols is null || request.Symbols.Length == 0)
                    return Results.BadRequest("At least one symbol is required.");

                if (string.IsNullOrWhiteSpace(request.TargetWatchlistId))
                    return Results.BadRequest("Target watchlist ID is required.");

                var result = await batch.MoveToWatchlistAsync(request);
                return Results.Json(result, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Move to watchlist failed: {ex.Message}");
            }
        });

        _app.MapPost("/api/symbols/batch/filter", async (BatchOperationsService batch, BatchFilter filter) =>
        {
            try
            {
                var symbols = await batch.GetFilteredSymbolsAsync(filter);
                return Results.Json(symbols, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Filter failed: {ex.Message}");
            }
        });

        _app.MapPost("/api/symbols/batch/filtered-operation", async (
            BatchOperationsService batch,
            BatchFilteredOperationRequest request) =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Operation))
                    return Results.BadRequest("Operation is required.");

                var result = await batch.PerformFilteredOperationAsync(request);
                return Results.Json(result, jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Filtered operation failed: {ex.Message}");
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

// ==================== NEW FEATURE DTOs ====================

public record HistoricalQueryRequest(
    string Symbol,
    DateOnly? From = null,
    DateOnly? To = null,
    string? DataType = null,
    int? Skip = null,
    int? Limit = null
);

public record DiagnosticBundleRequest(
    bool IncludeSystemInfo = true,
    bool IncludeConfiguration = true,
    bool IncludeMetrics = true,
    bool IncludeLogs = true,
    bool IncludeStorageInfo = true,
    bool IncludeEnvironmentVariables = true,
    int LogDays = 3
);

public record SampleDataRequest(
    string[]? Symbols = null,
    int DurationMinutes = 60,
    int MaxEvents = 10000,
    bool IncludeTrades = true,
    bool IncludeQuotes = true,
    bool IncludeDepth = true,
    bool IncludeBars = true
);

public record ConfigValidateRequest(string Json);

public record DryRunRequest(
    bool ValidateConfiguration = true,
    bool ValidateFileSystem = true,
    bool ValidateConnectivity = true,
    bool ValidateProviders = true,
    bool ValidateSymbols = true,
    bool ValidateResources = true
);

public record DataSourceRequest(string DataSource);
public record StorageSettingsRequest(string? DataRoot, bool Compress, string? NamingConvention, string? DatePartition, bool IncludeProvider, string? FilePrefix);
public record BackfillRequestDto(string? Provider, string[] Symbols, DateOnly? From, DateOnly? To);

// Symbol search DTOs
public record FigiBulkLookupRequest(string[] Symbols);

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

// ==================== BULK SYMBOL MANAGEMENT DTOs ====================

public record WatchlistSymbolsRequest(
    string[] Symbols,
    bool SubscribeImmediately = true,
    bool UnsubscribeIfOrphaned = false
);

public record PortfolioImportOptionsDto(
    decimal? MinPositionValue = null,
    decimal? MinQuantity = null,
    string[]? AssetClasses = null,
    string[]? ExcludeSymbols = null,
    bool LongOnly = false,
    bool CreateWatchlist = false,
    string? WatchlistName = null,
    bool SubscribeTrades = true,
    bool SubscribeDepth = true,
    bool SkipExisting = true
);

public record ManualPortfolioEntryDto(
    string Symbol,
    decimal? Quantity = null,
    string? AssetClass = null
);

public record ManualPortfolioImportRequest(
    ManualPortfolioEntryDto[] Entries,
    bool CreateWatchlist = false,
    string? WatchlistName = null,
    bool SubscribeTrades = true,
    bool SubscribeDepth = true,
    bool SkipExisting = true
);
