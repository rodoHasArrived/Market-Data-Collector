using System.Text.Json;
using MarketDataCollector.Application.Composition;
using MarketDataCollector.Application.Config;
using MarketDataCollector.Application.Config.Credentials;
using MarketDataCollector.Application.Services;
using MarketDataCollector.Application.Subscriptions.Models;
using MarketDataCollector.Application.Subscriptions.Services;
using MarketDataCollector.Contracts.Api;
using MarketDataCollector.Infrastructure.Contracts;
using MarketDataCollector.Storage;
using MarketDataCollector.Storage.Interfaces;
using MarketDataCollector.Storage.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MarketDataCollector.Application.UI;

/// <summary>
/// Embedded HTTP server for the web dashboard UI.
/// Uses ServiceCompositionRoot for centralized service registration.
/// </summary>
[ImplementsAdr("ADR-001", "UiServer uses centralized composition root")]
public sealed class UiServer : IAsyncDisposable
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly WebApplication _app;
    private readonly DateTimeOffset _startTime = DateTimeOffset.UtcNow;
    private readonly ILogger<UiServer> _logger;

    /// <summary>
    /// Logs an exception and returns a Problem result with a user-friendly message.
    /// </summary>
    private IResult LogAndProblem(Exception ex, string operation, string? context = null)
    {
        var contextInfo = context is not null ? $" Context: {context}" : "";
        _logger.LogError(ex, "API operation failed: {Operation}.{Context}", operation, contextInfo);
        return Results.Problem($"{operation}. Please check server logs for details.");
    }

    /// <summary>
    /// Creates a new UiServer using the centralized ServiceCompositionRoot.
    /// </summary>
    /// <param name="configPath">Path to the configuration file.</param>
    /// <param name="port">HTTP port to listen on.</param>
    public UiServer(string configPath, int port = 8080)
    {
        var builder = WebApplication.CreateBuilder();

        // Minimize logging from ASP.NET Core
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        builder.WebHost.UseUrls($"http://localhost:{port}");

        // Use centralized service composition root
        var compositionOptions = CompositionOptions.WebDashboard with { ConfigPath = configPath };
        builder.Services.AddMarketDataServices(compositionOptions);

        _app = builder.Build();
        _logger = _app.Services.GetRequiredService<ILoggerFactory>().CreateLogger<UiServer>();

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
                version = "1.6.1"
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
                compress = cfg.Compress ?? false,
                dataSource = cfg.DataSource.ToString(),
                alpaca = cfg.Alpaca,
                storage = cfg.Storage,
                symbols = cfg.Symbols ?? Array.Empty<SymbolConfig>(),
                backfill = cfg.Backfill
            }, s_jsonOptions);
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
                return LogAndProblem(ex, "Failed to update data source", $"DataSource={req.DataSource}");
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
                return LogAndProblem(ex, "Failed to save Alpaca settings");
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
                    FilePrefix: string.IsNullOrWhiteSpace(req.FilePrefix) ? null : req.FilePrefix,
                    Profile: string.IsNullOrWhiteSpace(req.Profile) ? null : req.Profile
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
                return LogAndProblem(ex, "Failed to save storage settings");
            }
        });

        _app.MapGet("/api/storage/profiles", () =>
        {
            var profiles = StorageProfilePresets.GetPresets()
                .Select(p => new StorageProfileResponse(p.Id, p.Label, p.Description))
                .ToArray();
            return Results.Json(profiles, s_jsonOptions);
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
                return LogAndProblem(ex, "Failed to add symbol", $"Symbol={symbol.Symbol}");
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
                return LogAndProblem(ex, "Failed to delete symbol", $"Symbol={symbol}");
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
                return LogAndProblem(ex, "Failed to load status");
            }
        });

        _app.MapGet("/api/backfill/providers", (BackfillCoordinator backfill) =>
        {
            try
            {
                var providers = backfill.DescribeProviders();
                return Results.Json(providers, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Failed to get providers");
            }
        });

        _app.MapGet("/api/backfill/status", (BackfillCoordinator backfill) =>
        {
            try
            {
                var status = backfill.TryReadLast();
                return status is null
                    ? Results.NotFound()
                    : Results.Json(status, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Failed to load backfill status");
            }
        });

        _app.MapPost("/api/backfill/run", async (BackfillCoordinator backfill, BackfillRequestDto req, CancellationToken ct) =>
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

                var result = await backfill.RunAsync(request, ct);
                return Results.Json(result, s_jsonOptions);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Backfill failed");
            }
        });

        _app.MapGet("/api/backfill/health", async (BackfillCoordinator backfill, CancellationToken ct) =>
        {
            try
            {
                var health = await backfill.CheckProviderHealthAsync(ct);
                return Results.Json(health, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Health check failed");
            }
        });

        _app.MapGet("/api/backfill/resolve/{symbol}", async (BackfillCoordinator backfill, string symbol, CancellationToken ct) =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(symbol))
                    return Results.BadRequest("Symbol is required.");

                var resolution = await backfill.ResolveSymbolAsync(symbol, ct);
                if (resolution is null)
                    return Results.NotFound($"Symbol '{symbol}' not found.");

                return Results.Json(resolution, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Symbol resolution failed");
            }
        });

        ConfigureSymbolManagementRoutes();
        ConfigureStorageOrganizationRoutes();
        ConfigureNewFeatureRoutes();
        ConfigureCredentialManagementRoutes();

        // Configure scheduled backfill endpoints
        _app.MapScheduledBackfillEndpoints();
        ConfigureBulkSymbolManagementRoutes();

        // Configure packaging endpoints
        var config = _app.Services.GetRequiredService<ConfigStore>().Load();
        _app.MapPackagingEndpoints(config.DataRoot);

        // Configure archive maintenance endpoints
        _app.MapArchiveMaintenanceEndpoints();
    }

    private void ConfigureStorageOrganizationRoutes()
    {
        // ==================== DATA CATALOG & SEARCH ====================

        _app.MapGet("/api/storage/catalog", async (IStorageSearchService search, CancellationToken ct) =>
        {
            try
            {
                var catalog = await search.DiscoverAsync(new DiscoveryQuery(), ct);
                return Results.Json(catalog, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Failed to discover catalog");
            }
        });

        _app.MapPost("/api/storage/search/files", async (IStorageSearchService search, FileSearchRequest req, CancellationToken ct) =>
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
                var results = await search.SearchFilesAsync(query, ct);
                return Results.Json(results, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Search failed");
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
                return Results.Json(results, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Faceted search failed");
            }
        });

        _app.MapPost("/api/storage/search/natural", (IStorageSearchService search, NaturalSearchRequest req) =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(req.Query))
                    return Results.BadRequest("Query is required");

                var query = search.ParseNaturalLanguageQuery(req.Query);
                return Results.Json(query, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Natural language parsing failed");
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
                return LogAndProblem(ex, "Index rebuild failed");
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
                return Results.Json(report, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Health check failed");
            }
        });

        _app.MapGet("/api/storage/health/orphans", async (IFileMaintenanceService maintenance) =>
        {
            try
            {
                var report = await maintenance.FindOrphansAsync();
                return Results.Json(report, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Orphan scan failed");
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
                return Results.Json(result, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Repair failed");
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
                return Results.Json(result, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Defragmentation failed");
            }
        });

        // ==================== DATA QUALITY ====================

        _app.MapPost("/api/storage/quality/score", async (IDataQualityService quality, string path) =>
        {
            try
            {
                var score = await quality.ScoreAsync(path);
                return Results.Json(score, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Quality scoring failed");
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
                return Results.Json(report, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Quality report failed");
            }
        });

        _app.MapGet("/api/storage/quality/alerts", async (IDataQualityService quality) =>
        {
            try
            {
                var alerts = await quality.GetQualityAlertsAsync();
                return Results.Json(alerts, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Failed to get quality alerts");
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
                return Results.Json(rankings, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Source ranking failed");
            }
        });

        _app.MapGet("/api/storage/quality/trend/{symbol}", async (IDataQualityService quality, string symbol, int? days) =>
        {
            try
            {
                var window = TimeSpan.FromDays(days ?? 30);
                var trend = await quality.GetTrendAsync(symbol, window);
                return Results.Json(trend, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Trend analysis failed");
            }
        });

        // ==================== TIER MIGRATION ====================

        _app.MapGet("/api/storage/tiers/statistics", async (ITierMigrationService tiers) =>
        {
            try
            {
                var stats = await tiers.GetTierStatisticsAsync();
                return Results.Json(stats, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Failed to get tier statistics");
            }
        });

        _app.MapPost("/api/storage/tiers/plan", async (ITierMigrationService tiers, int? horizonDays) =>
        {
            try
            {
                var plan = await tiers.PlanMigrationAsync(TimeSpan.FromDays(horizonDays ?? 7));
                return Results.Json(plan, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Migration planning failed");
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
                return Results.Json(result, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Migration failed");
            }
        });

        _app.MapGet("/api/storage/tiers/target", (ITierMigrationService tiers, string path) =>
        {
            try
            {
                var tier = tiers.DetermineTargetTier(path);
                return Results.Json(new { path, targetTier = tier.ToString() }, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Failed to determine tier");
            }
        });

        // ==================== SOURCE REGISTRY ====================

        _app.MapGet("/api/storage/sources", (ISourceRegistry registry) =>
        {
            try
            {
                var sources = registry.GetAllSources();
                return Results.Json(sources, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Failed to get sources");
            }
        });

        _app.MapGet("/api/storage/sources/{sourceId}", (ISourceRegistry registry, string sourceId) =>
        {
            try
            {
                var source = registry.GetSourceInfo(sourceId);
                return source is null
                    ? Results.NotFound($"Source '{sourceId}' not found")
                    : Results.Json(source, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Failed to get source");
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
                return LogAndProblem(ex, "Failed to register source");
            }
        });

        _app.MapGet("/api/storage/sources/priority", (ISourceRegistry registry) =>
        {
            try
            {
                var order = registry.GetSourcePriorityOrder();
                return Results.Json(order, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Failed to get priority order");
            }
        });

        _app.MapGet("/api/storage/symbols/{symbol}", (ISourceRegistry registry, string symbol) =>
        {
            try
            {
                var info = registry.GetSymbolInfo(symbol);
                return info is null
                    ? Results.NotFound($"Symbol '{symbol}' not found in registry")
                    : Results.Json(info, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Failed to get symbol info");
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
                return LogAndProblem(ex, "Failed to register symbol");
            }
        });

        _app.MapGet("/api/storage/symbols/resolve/{alias}", (ISourceRegistry registry, string alias) =>
        {
            try
            {
                var canonical = registry.ResolveSymbolAlias(alias);
                return Results.Json(new { alias, canonical }, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Failed to resolve alias");
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
                }, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Failed to get storage overview");
            }
        });
    }

    private void ConfigureSymbolManagementRoutes()
    {
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
                return Results.Json(result, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Import failed");
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
                return LogAndProblem(ex, "Export failed");
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
                return LogAndProblem(ex, "Export failed");
            }
        });

        // ==================== TEMPLATES ====================

        _app.MapGet("/api/symbols/templates", async (TemplateService templates) =>
        {
            try
            {
                var all = await templates.GetAllTemplatesAsync();
                return Results.Json(all, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Failed to get templates");
            }
        });

        _app.MapGet("/api/symbols/templates/{templateId}", async (TemplateService templates, string templateId) =>
        {
            try
            {
                var template = await templates.GetTemplateAsync(templateId);
                return template is null
                    ? Results.NotFound($"Template '{templateId}' not found")
                    : Results.Json(template, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Failed to get template");
            }
        });

        _app.MapPost("/api/symbols/templates/apply", async (TemplateService templates, ApplyTemplateRequest request) =>
        {
            try
            {
                var result = await templates.ApplyTemplateAsync(request);
                return Results.Json(result, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Failed to apply template");
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

                return Results.Json(template, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Failed to create template");
            }
        });

        _app.MapPost("/api/symbols/templates/from-current", async (TemplateService templates, CreateFromCurrentDto dto) =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(dto.Name))
                    return Results.BadRequest("Template name is required.");

                var template = await templates.CreateFromCurrentAsync(dto.Name, dto.Description ?? "");
                return Results.Json(template, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Failed to create template");
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
                return LogAndProblem(ex, "Failed to delete template");
            }
        });

        // ==================== SCHEDULES ====================

        _app.MapGet("/api/symbols/schedules", (SchedulingService scheduling) =>
        {
            try
            {
                var schedules = scheduling.GetAllSchedules();
                return Results.Json(schedules, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Failed to get schedules");
            }
        });

        _app.MapGet("/api/symbols/schedules/{scheduleId}", (SchedulingService scheduling, string scheduleId) =>
        {
            try
            {
                var schedule = scheduling.GetSchedule(scheduleId);
                return schedule is null
                    ? Results.NotFound($"Schedule '{scheduleId}' not found")
                    : Results.Json(schedule, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Failed to get schedule");
            }
        });

        _app.MapGet("/api/symbols/schedules/{scheduleId}/status", (SchedulingService scheduling, string scheduleId) =>
        {
            try
            {
                var status = scheduling.GetExecutionStatus(scheduleId);
                return status is null
                    ? Results.NotFound($"No execution status for schedule '{scheduleId}'")
                    : Results.Json(status, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Failed to get schedule status");
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
                return Results.Json(schedule, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Failed to create schedule");
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
                    : Results.Json(schedule, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Failed to update schedule");
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
                return LogAndProblem(ex, "Failed to enable schedule");
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
                return LogAndProblem(ex, "Failed to disable schedule");
            }
        });

        _app.MapPost("/api/symbols/schedules/{scheduleId}/execute", async (
            SchedulingService scheduling,
            string scheduleId) =>
        {
            try
            {
                var status = await scheduling.ExecuteNowAsync(scheduleId);
                return Results.Json(status, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Failed to execute schedule");
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
                return LogAndProblem(ex, "Failed to delete schedule");
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
                    : Results.Json(meta, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Failed to get metadata");
            }
        });

        _app.MapPost("/api/symbols/metadata/batch", async (
            MetadataEnrichmentService metadata,
            string[] symbols) =>
        {
            try
            {
                var result = await metadata.GetMetadataBatchAsync(symbols);
                return Results.Json(result, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Failed to get metadata");
            }
        });

        _app.MapPost("/api/symbols/metadata/filter", async (
            MetadataEnrichmentService metadata,
            SymbolMetadataFilter filter) =>
        {
            try
            {
                var result = await metadata.FilterSymbolsAsync(filter);
                return Results.Json(result, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Failed to filter symbols");
            }
        });

        _app.MapGet("/api/symbols/metadata/sectors", async (MetadataEnrichmentService metadata) =>
        {
            try
            {
                var sectors = await metadata.GetAvailableSectorsAsync();
                return Results.Json(sectors, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Failed to get sectors");
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
                return Results.Json(industries, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Failed to get industries");
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
                return LogAndProblem(ex, "Failed to update metadata");
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
                return Results.Json(result, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Symbol search failed");
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
                return Results.Json(result, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Symbol search failed");
            }
        });

        _app.MapGet("/api/symbols/search/providers", async (
            SymbolSearchService searchService,
            CancellationToken ct) =>
        {
            try
            {
                var providers = await searchService.GetProvidersAsync(ct);
                return Results.Json(providers, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Failed to get providers");
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

                return Results.Json(details, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Failed to get symbol details");
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

                return Results.Json(new { symbol = symbol.ToUpperInvariant(), mappings }, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "FIGI lookup failed");
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
                return Results.Json(figiMappings, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Bulk FIGI lookup failed");
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
                return Results.Json(new { query, count = results.Count, results }, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "FIGI search failed");
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
                return LogAndProblem(ex, "Failed to clear cache");
            }
        });

        // ==================== INDEX SUBSCRIPTIONS ====================

        _app.MapGet("/api/symbols/indices", (IndexSubscriptionService indexService) =>
        {
            try
            {
                var indices = indexService.GetAvailableIndices();
                return Results.Json(indices, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Failed to get indices");
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
                    : Results.Json(components, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Failed to get index components");
            }
        });

        _app.MapGet("/api/symbols/indices/{indexId}/status", async (
            IndexSubscriptionService indexService,
            string indexId) =>
        {
            try
            {
                var status = await indexService.GetSubscriptionStatusAsync(indexId);
                return Results.Json(status, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Failed to get subscription status");
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
                return Results.Json(result, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Failed to subscribe to index");
            }
        });

        _app.MapPost("/api/symbols/indices/{indexId}/unsubscribe", async (
            IndexSubscriptionService indexService,
            string indexId) =>
        {
            try
            {
                var result = await indexService.UnsubscribeFromIndexAsync(indexId);
                return Results.Json(result, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Failed to unsubscribe from index");
            }
        });
    }

    private void ConfigureNewFeatureRoutes()
    {
        // ==================== QW-15: HISTORICAL DATA QUERY ====================

        _app.MapGet("/api/historical/symbols", (HistoricalDataQueryService query) =>
        {
            try
            {
                var symbols = query.GetAvailableSymbols();
                return Results.Json(symbols, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Failed to get symbols");
            }
        });

        _app.MapGet("/api/historical/symbols/{symbol}/range", (HistoricalDataQueryService query, string symbol) =>
        {
            try
            {
                var range = query.GetDateRange(symbol);
                return Results.Json(range, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Failed to get date range");
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
                return Results.Json(result, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Query failed");
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
                return Results.Json(result, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Bundle generation failed");
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
                return LogAndProblem(ex, "Download failed");
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
                }, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Sample data generation failed");
            }
        });

        _app.MapGet("/api/tools/sample-data/preview", (SampleDataGenerator gen) =>
        {
            try
            {
                var preview = gen.GeneratePreview(new SampleDataOptions(MaxEvents: 20));
                return Results.Json(preview, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Preview failed");
            }
        });

        // ==================== QW-58: LAST N ERRORS ====================

        _app.MapGet("/api/diagnostics/errors", (ErrorTracker errors, int? count, string? type, string? context) =>
        {
            try
            {
                var result = errors.GetLastErrors(count ?? 10, type, context);
                return Results.Json(result, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Failed to get errors");
            }
        });

        _app.MapGet("/api/diagnostics/errors/stats", (ErrorTracker errors, int? hours) =>
        {
            try
            {
                var window = hours.HasValue ? TimeSpan.FromHours(hours.Value) : TimeSpan.FromHours(24);
                var stats = errors.GetStatistics(window);
                return Results.Json(stats, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Failed to get error stats");
            }
        });

        _app.MapGet("/api/diagnostics/errors/logs", async (ErrorTracker errors, int? count, int? days) =>
        {
            try
            {
                var result = await errors.ParseErrorsFromLogsAsync(count ?? 100, days ?? 1);
                return Results.Json(result, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Failed to parse log errors");
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
                }), s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Failed to get templates");
            }
        });

        _app.MapGet("/api/tools/config-templates/{name}", (ConfigTemplateGenerator gen, string name) =>
        {
            try
            {
                var template = gen.GetTemplate(name);
                if (template == null)
                    return Results.NotFound($"Template '{name}' not found");

                return Results.Json(template, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Failed to get template");
            }
        });

        _app.MapPost("/api/tools/config-templates/validate", (ConfigTemplateGenerator gen, ConfigValidateRequest req) =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(req.Json))
                    return Results.BadRequest("JSON configuration is required");

                var result = gen.ValidateTemplate(req.Json);
                return Results.Json(result, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Validation failed");
            }
        });

        // ==================== QW-25: CONFIG ENVIRONMENT OVERRIDE ====================

        _app.MapGet("/api/config/env-overrides", (ConfigEnvironmentOverride envOverride) =>
        {
            try
            {
                var variables = envOverride.GetRecognizedVariables();
                return Results.Json(variables, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Failed to get environment overrides");
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
                return LogAndProblem(ex, "Failed to get documentation");
            }
        });

        // ==================== QW-93: DRY RUN MODE ====================

        _app.MapPost("/api/tools/dry-run", async (
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
                return Results.Json(result, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Dry run failed");
            }
        });

        _app.MapPost("/api/tools/dry-run/report", async (
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
                return LogAndProblem(ex, "Report generation failed");
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
                return LogAndProblem(ex, "Failed to generate OpenAPI spec");
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
                return LogAndProblem(ex, "Failed to generate docs");
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
                return LogAndProblem(ex, "Failed to generate markdown");
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
                return LogAndProblem(ex, "Failed to generate Swagger UI");
            }
        });
    }

    private void ConfigureCredentialManagementRoutes()
    {
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        // ==================== CREDENTIAL TESTING ====================

        // Test credentials for a specific provider
        _app.MapPost("/api/credentials/test", async (
            CredentialTestingService credentialService,
            CredentialTestRequest req) =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(req.Provider))
                    return Results.BadRequest("Provider name is required");

                var result = await credentialService.TestCredentialAsync(
                    req.Provider,
                    req.ApiKey,
                    req.ApiSecret,
                    req.CredentialSource);

                return Results.Json(result, jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Credential test failed");
            }
        });

        // Test all configured credentials
        _app.MapPost("/api/credentials/test-all", async (
            CredentialTestingService credentialService,
            ConfigurationService configService,
            ConfigStore store) =>
        {
            try
            {
                var config = configService.LoadAndPrepareConfig(store.ConfigPath);
                var summary = await credentialService.TestAllCredentialsAsync(config);
                return Results.Json(summary, jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Credential test failed");
            }
        });

        // Get all credential statuses (cached)
        _app.MapGet("/api/credentials/status", (CredentialTestingService credentialService) =>
        {
            try
            {
                var statuses = credentialService.GetAllCachedStatuses();

                var response = statuses.Select(kvp => new
                {
                    provider = kvp.Key,
                    lastSuccessfulAuth = kvp.Value.LastSuccessfulAuth,
                    lastTestResult = kvp.Value.LastTestResult.ToString(),
                    lastTestedAt = kvp.Value.LastTestedAt,
                    consecutiveFailures = kvp.Value.ConsecutiveFailures,
                    expiresAt = kvp.Value.ExpiresAt,
                    isExpiringSoon = kvp.Value.ExpiresAt.HasValue &&
                        (kvp.Value.ExpiresAt.Value - DateTimeOffset.UtcNow).TotalDays <= 7,
                    daysUntilExpiration = kvp.Value.ExpiresAt.HasValue
                        ? (kvp.Value.ExpiresAt.Value - DateTimeOffset.UtcNow).TotalDays
                        : (double?)null
                }).ToList();

                return Results.Json(response, jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Failed to get credential status");
            }
        });

        // Get credential status for a specific provider
        _app.MapGet("/api/credentials/status/{provider}", (
            CredentialTestingService credentialService,
            string provider) =>
        {
            try
            {
                var status = credentialService.GetCachedStatus(provider);
                if (status == null)
                    return Results.NotFound($"No status found for provider: {provider}");

                var response = new
                {
                    provider = status.ProviderName,
                    lastSuccessfulAuth = status.LastSuccessfulAuth,
                    lastTestResult = status.LastTestResult.ToString(),
                    lastTestedAt = status.LastTestedAt,
                    consecutiveFailures = status.ConsecutiveFailures,
                    expiresAt = status.ExpiresAt,
                    isExpiringSoon = status.ExpiresAt.HasValue &&
                        (status.ExpiresAt.Value - DateTimeOffset.UtcNow).TotalDays <= 7,
                    daysUntilExpiration = status.ExpiresAt.HasValue
                        ? (status.ExpiresAt.Value - DateTimeOffset.UtcNow).TotalDays
                        : (double?)null
                };

                return Results.Json(response, jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Failed to get credential status");
            }
        });

        // ==================== OAUTH TOKEN MANAGEMENT ====================

        // Get all OAuth token statuses
        _app.MapGet("/api/credentials/oauth/tokens", (OAuthTokenRefreshService oauthService) =>
        {
            try
            {
                var tokens = oauthService.GetAllTokens();

                var response = tokens.Select(kvp => new
                {
                    provider = kvp.Key,
                    status = kvp.Value.Status.ToString(),
                    expiresAt = kvp.Value.Token.ExpiresAt,
                    isExpired = kvp.Value.Token.IsExpired,
                    isExpiringSoon = kvp.Value.Token.IsExpiringSoon,
                    canRefresh = kvp.Value.Token.CanRefresh,
                    lifetimeRemainingPercent = kvp.Value.Token.LifetimeRemainingPercent,
                    timeUntilExpiration = kvp.Value.Token.TimeUntilExpiration.ToString()
                }).ToList();

                return Results.Json(response, jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Failed to get OAuth tokens");
            }
        });

        // Manually refresh OAuth token for a provider
        _app.MapPost("/api/credentials/oauth/refresh/{provider}", async (
            OAuthTokenRefreshService oauthService,
            string provider) =>
        {
            try
            {
                var result = await oauthService.RefreshTokenAsync(provider);
                return Results.Json(result, jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Token refresh failed");
            }
        });

        // Store OAuth token for a provider
        _app.MapPost("/api/credentials/oauth/store", async (
            OAuthTokenRefreshService oauthService,
            OAuthTokenStoreRequest req) =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(req.Provider))
                    return Results.BadRequest("Provider name is required");

                if (string.IsNullOrWhiteSpace(req.AccessToken))
                    return Results.BadRequest("Access token is required");

                var token = new OAuthToken(
                    AccessToken: req.AccessToken,
                    TokenType: req.TokenType ?? "Bearer",
                    ExpiresAt: req.ExpiresAt ?? DateTimeOffset.UtcNow.AddHours(1),
                    RefreshToken: req.RefreshToken,
                    RefreshTokenExpiresAt: req.RefreshTokenExpiresAt,
                    Scope: req.Scope,
                    IssuedAt: DateTimeOffset.UtcNow
                );

                await oauthService.StoreTokenAsync(req.Provider, token);
                return Results.Ok(new { message = "Token stored successfully" });
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Failed to store token");
            }
        });

        // Remove OAuth token for a provider
        _app.MapDelete("/api/credentials/oauth/{provider}", async (
            OAuthTokenRefreshService oauthService,
            string provider) =>
        {
            try
            {
                await oauthService.RemoveTokenAsync(provider);
                return Results.Ok(new { message = $"Token removed for {provider}" });
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Failed to remove token");
            }
        });

        // ==================== CONSOLIDATED CONFIGURATION SERVICE ENDPOINTS ====================
        // These endpoints route through ConfigurationService for unified configuration operations

        // Get detected providers via ConfigurationService
        _app.MapGet("/api/config/providers", (ConfigurationService configService) =>
        {
            try
            {
                var providers = configService.DetectProviders();
                return Results.Json(providers, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Failed to detect providers");
            }
        });

        // Get provider credential status summary
        _app.MapGet("/api/config/providers/status", (ConfigurationService configService) =>
        {
            try
            {
                var status = configService.GetCredentialStatus();
                return Results.Json(status, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Failed to get credential status");
            }
        });

        // Get best real-time provider
        _app.MapGet("/api/config/providers/best-realtime", (ConfigurationService configService) =>
        {
            try
            {
                var provider = configService.GetBestRealTimeProvider();
                if (provider == null)
                    return Results.NotFound(new { message = "No real-time providers with credentials configured" });

                return Results.Json(provider, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Failed to get best real-time provider");
            }
        });

        // Get historical providers
        _app.MapGet("/api/config/providers/historical", (ConfigurationService configService) =>
        {
            try
            {
                var providers = configService.GetHistoricalProviders();
                return Results.Json(providers, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Failed to get historical providers");
            }
        });

        // Apply self-healing fixes to configuration
        _app.MapPost("/api/config/self-healing", (ConfigurationService configService, ConfigStore store) =>
        {
            try
            {
                var config = configService.LoadAndPrepareConfig(store.ConfigPath, applySelfHealing: false);
                var (fixedConfig, appliedFixes, warnings) = configService.ApplySelfHealingFixes(config);

                return Results.Json(new
                {
                    success = true,
                    appliedFixes = appliedFixes,
                    warnings = warnings,
                    configChanged = appliedFixes.Count > 0
                }, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Failed to apply self-healing fixes");
            }
        });

        // Apply and save self-healing fixes
        _app.MapPost("/api/config/self-healing/apply", async (ConfigurationService configService, ConfigStore store) =>
        {
            try
            {
                var config = configService.LoadAndPrepareConfig(store.ConfigPath, applySelfHealing: false);
                var (fixedConfig, appliedFixes, warnings) = configService.ApplySelfHealingFixes(config);

                if (appliedFixes.Count > 0)
                {
                    await store.SaveAsync(fixedConfig);
                }

                return Results.Json(new
                {
                    success = true,
                    appliedFixes = appliedFixes,
                    warnings = warnings,
                    configSaved = appliedFixes.Count > 0
                }, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Failed to apply and save self-healing fixes");
            }
        });

        // Validate configuration via ConfigurationService
        _app.MapPost("/api/config/validate", (ConfigurationService configService, ConfigStore store) =>
        {
            try
            {
                var config = configService.LoadAndPrepareConfig(store.ConfigPath);
                var isValid = configService.ValidateConfig(config, out var errors);

                return Results.Json(new
                {
                    isValid = isValid,
                    errors = errors
                }, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Failed to validate configuration");
            }
        });

        // Perform quick check via ConfigurationService
        _app.MapGet("/api/config/quick-check", (ConfigurationService configService, ConfigStore store) =>
        {
            try
            {
                var config = configService.LoadAndPrepareConfig(store.ConfigPath);
                var result = configService.PerformQuickCheck(config);
                return Results.Json(result, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Failed to perform quick check");
            }
        });

        // Resolve all credentials for configuration
        _app.MapPost("/api/config/resolve-credentials", (ConfigurationService configService, ConfigStore store) =>
        {
            try
            {
                var resolvedConfig = configService.LoadAndPrepareConfig(store.ConfigPath, applySelfHealing: false);

                // Don't return sensitive data - just indicate which credentials were resolved
                var resolvedProviders = new List<string>();

                if (!string.IsNullOrEmpty(resolvedConfig.Alpaca?.KeyId))
                    resolvedProviders.Add("Alpaca");
                if (!string.IsNullOrEmpty(resolvedConfig.Polygon?.ApiKey))
                    resolvedProviders.Add("Polygon");
                if (!string.IsNullOrEmpty(resolvedConfig.Backfill?.Providers?.Tiingo?.ApiToken))
                    resolvedProviders.Add("Tiingo");
                if (!string.IsNullOrEmpty(resolvedConfig.Backfill?.Providers?.Finnhub?.ApiKey))
                    resolvedProviders.Add("Finnhub");
                if (!string.IsNullOrEmpty(resolvedConfig.Backfill?.Providers?.AlphaVantage?.ApiKey))
                    resolvedProviders.Add("AlphaVantage");
                if (!string.IsNullOrEmpty(resolvedConfig.Backfill?.Providers?.Polygon?.ApiKey))
                    resolvedProviders.Add("Polygon (Backfill)");

                return Results.Json(new
                {
                    resolvedProviders = resolvedProviders,
                    message = $"Resolved credentials for {resolvedProviders.Count} provider(s)"
                }, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Failed to resolve credentials");
            }
        });

        // Check if IB Gateway is available
        _app.MapGet("/api/config/ib-gateway/status", (ConfigurationService configService) =>
        {
            try
            {
                var available = configService.IsIBGatewayAvailable();
                return Results.Json(new
                {
                    available = available,
                    checkedPorts = new[] { 7496, 7497, 4001, 4002 },
                    message = available ? "IB Gateway/TWS is running" : "IB Gateway/TWS not detected"
                }, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Failed to check IB Gateway status");
            }
        });

        // Get environment name
        _app.MapGet("/api/config/environment", () =>
        {
            try
            {
                var envName = ConfigurationService.GetEnvironmentName();
                return Results.Json(new
                {
                    environment = envName ?? "default",
                    isConfigured = envName != null,
                    envVars = new
                    {
                        MDC_ENVIRONMENT = Environment.GetEnvironmentVariable("MDC_ENVIRONMENT"),
                        DOTNET_ENVIRONMENT = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
                    }
                }, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Failed to get environment");
            }
        });

        // ==================== CREDENTIAL MANAGEMENT UI ====================

        // Get credentials dashboard HTML
        _app.MapGet("/credentials", (ConfigStore store, CredentialTestingService credentialService) =>
        {
            try
            {
                var config = store.Load();
                var statuses = credentialService.GetAllCachedStatuses();
                var html = HtmlTemplates.CredentialsDashboard(config, statuses);
                return Results.Content(html, "text/html");
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Failed to render credentials dashboard");
            }
        });
    }

    private void ConfigureBulkSymbolManagementRoutes()
    {
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
                return Results.Json(result, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Text import failed");
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
                return Results.Json(result, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Import failed");
            }
        });

        // ==================== WATCHLISTS ====================

        _app.MapGet("/api/watchlists", async (WatchlistService watchlists) =>
        {
            try
            {
                var all = await watchlists.GetAllWatchlistsAsync();
                return Results.Json(all, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Failed to get watchlists");
            }
        });

        _app.MapGet("/api/watchlists/summaries", async (WatchlistService watchlists) =>
        {
            try
            {
                var summaries = await watchlists.GetWatchlistSummariesAsync();
                return Results.Json(summaries, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Failed to get watchlist summaries");
            }
        });

        _app.MapGet("/api/watchlists/{watchlistId}", async (WatchlistService watchlists, string watchlistId) =>
        {
            try
            {
                var watchlist = await watchlists.GetWatchlistAsync(watchlistId);
                return watchlist is null
                    ? Results.NotFound($"Watchlist '{watchlistId}' not found")
                    : Results.Json(watchlist, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Failed to get watchlist");
            }
        });

        _app.MapPost("/api/watchlists", async (WatchlistService watchlists, CreateWatchlistRequest request) =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Name))
                    return Results.BadRequest("Watchlist name is required.");

                var watchlist = await watchlists.CreateWatchlistAsync(request);
                return Results.Json(watchlist, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Failed to create watchlist");
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
                    : Results.Json(updated, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Failed to update watchlist");
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
                return LogAndProblem(ex, "Failed to delete watchlist");
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
                return Results.Json(result, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Failed to add symbols");
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
                return Results.Json(result, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Failed to remove symbols");
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
                return Results.Json(result, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Failed to subscribe watchlist");
            }
        });

        _app.MapPost("/api/watchlists/{watchlistId}/unsubscribe", async (
            WatchlistService watchlists,
            string watchlistId) =>
        {
            try
            {
                var result = await watchlists.UnsubscribeWatchlistAsync(watchlistId);
                return Results.Json(result, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Failed to unsubscribe watchlist");
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
                return LogAndProblem(ex, "Failed to export watchlist");
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
                    : Results.Json(watchlist, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Failed to import watchlist");
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
                return LogAndProblem(ex, "Failed to reorder watchlists");
            }
        });

        // ==================== PORTFOLIO IMPORT ====================

        _app.MapGet("/api/portfolio/brokers", (PortfolioImportService portfolio) =>
        {
            try
            {
                var brokers = portfolio.GetAvailableBrokers();
                return Results.Json(brokers, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Failed to get brokers");
            }
        });

        _app.MapGet("/api/portfolio/{broker}/summary", async (PortfolioImportService portfolio, string broker) =>
        {
            try
            {
                var summary = await portfolio.GetPortfolioSummaryAsync(broker);
                return summary is null
                    ? Results.NotFound($"Broker '{broker}' not configured or not available")
                    : Results.Json(summary, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Failed to get portfolio summary");
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
                return Results.Json(result, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Failed to import from portfolio");
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
                return Results.Json(result, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Failed to import manual portfolio");
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
                return Results.Json(result, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Batch add failed");
            }
        });

        _app.MapPost("/api/symbols/batch/delete", async (BatchOperationsService batch, BatchDeleteRequest request) =>
        {
            try
            {
                if (request.Symbols is null || request.Symbols.Length == 0)
                    return Results.BadRequest("At least one symbol is required.");

                var result = await batch.DeleteSymbolsAsync(request);
                return Results.Json(result, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Batch delete failed");
            }
        });

        _app.MapPost("/api/symbols/batch/toggle", async (BatchOperationsService batch, BatchToggleRequest request) =>
        {
            try
            {
                if (request.Symbols is null || request.Symbols.Length == 0)
                    return Results.BadRequest("At least one symbol is required.");

                var result = await batch.ToggleSubscriptionsAsync(request);
                return Results.Json(result, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Batch toggle failed");
            }
        });

        _app.MapPost("/api/symbols/batch/update", async (BatchOperationsService batch, BatchUpdateRequest request) =>
        {
            try
            {
                if (request.Symbols is null || request.Symbols.Length == 0)
                    return Results.BadRequest("At least one symbol is required.");

                var result = await batch.UpdateSymbolsAsync(request);
                return Results.Json(result, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Batch update failed");
            }
        });

        _app.MapPost("/api/symbols/batch/enable-trades", async (BatchOperationsService batch, string[] symbols) =>
        {
            try
            {
                if (symbols is null || symbols.Length == 0)
                    return Results.BadRequest("At least one symbol is required.");

                var result = await batch.EnableTradesAsync(symbols);
                return Results.Json(result, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Enable trades failed");
            }
        });

        _app.MapPost("/api/symbols/batch/disable-trades", async (BatchOperationsService batch, string[] symbols) =>
        {
            try
            {
                if (symbols is null || symbols.Length == 0)
                    return Results.BadRequest("At least one symbol is required.");

                var result = await batch.DisableTradesAsync(symbols);
                return Results.Json(result, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Disable trades failed");
            }
        });

        _app.MapPost("/api/symbols/batch/enable-depth", async (BatchOperationsService batch, string[] symbols) =>
        {
            try
            {
                if (symbols is null || symbols.Length == 0)
                    return Results.BadRequest("At least one symbol is required.");

                var result = await batch.EnableDepthAsync(symbols);
                return Results.Json(result, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Enable depth failed");
            }
        });

        _app.MapPost("/api/symbols/batch/disable-depth", async (BatchOperationsService batch, string[] symbols) =>
        {
            try
            {
                if (symbols is null || symbols.Length == 0)
                    return Results.BadRequest("At least one symbol is required.");

                var result = await batch.DisableDepthAsync(symbols);
                return Results.Json(result, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Disable depth failed");
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
                return Results.Json(result, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Copy settings failed");
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
                return Results.Json(result, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Move to watchlist failed");
            }
        });

        _app.MapPost("/api/symbols/batch/filter", async (BatchOperationsService batch, BatchFilter filter) =>
        {
            try
            {
                var symbols = await batch.GetFilteredSymbolsAsync(filter);
                return Results.Json(symbols, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Filter failed");
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
                return Results.Json(result, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Filtered operation failed");
            }
        });
    }

    public async Task StartAsync(CancellationToken ct = default)
    {
        await _app.StartAsync(ct);
    }

    public async Task StopAsync(CancellationToken ct = default)
    {
        await _app.StopAsync(ct);
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

// Credential management DTOs
public record CredentialTestRequest(
    string Provider,
    string? ApiKey = null,
    string? ApiSecret = null,
    string? CredentialSource = null
);

public record OAuthTokenStoreRequest(
    string Provider,
    string AccessToken,
    string? TokenType = null,
    DateTimeOffset? ExpiresAt = null,
    string? RefreshToken = null,
    DateTimeOffset? RefreshTokenExpiresAt = null,
    string? Scope = null
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
