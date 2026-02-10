using System.Text.Json;
using MarketDataCollector.Contracts.Domain.Enums;
using MarketDataCollector.Storage;
using MarketDataCollector.Storage.Interfaces;
using MarketDataCollector.Storage.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace MarketDataCollector.Application.UI;

/// <summary>
/// HTTP API endpoints for storage organization: catalog, search, health,
/// quality, tier migration, and source registry.
/// Extracted from UiServer.ConfigureStorageOrganizationRoutes().
/// </summary>
public static class StorageOrganizationEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static void MapStorageOrganizationEndpoints(this WebApplication app)
    {
        // ==================== DATA CATALOG & SEARCH ====================

        app.MapGet("/api/storage/catalog", async (IStorageSearchService search, CancellationToken ct) =>
        {
            try
            {
                var catalog = await search.DiscoverAsync(new DiscoveryQuery(), ct);
                return Results.Json(catalog, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to discover catalog: {ex.Message}");
            }
        });

        app.MapPost("/api/storage/search/files", async (IStorageSearchService search, FileSearchRequest req, CancellationToken ct) =>
        {
            try
            {
                var query = new FileSearchQuery(
                    Symbols: req.Symbols,
                    Types: req.Types?.Select(t => Enum.Parse<MarketEventType>(t, true)).ToArray(),
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
                return Results.Json(results, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Search failed: {ex.Message}");
            }
        });

        app.MapPost("/api/storage/search/faceted", async (IStorageSearchService search, FacetedSearchRequest req) =>
        {
            try
            {
                var query = new FacetedSearchQuery(
                    Symbols: req.Symbols,
                    Types: req.Types?.Select(t => Enum.Parse<MarketEventType>(t, true)).ToArray(),
                    Sources: req.Sources,
                    From: req.From,
                    To: req.To,
                    MaxResults: req.MaxResults
                );
                var results = await search.SearchWithFacetsAsync(query);
                return Results.Json(results, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Faceted search failed: {ex.Message}");
            }
        });

        app.MapPost("/api/storage/search/natural", (IStorageSearchService search, NaturalSearchRequest req) =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(req.Query))
                    return Results.BadRequest("Query is required");

                var query = search.ParseNaturalLanguageQuery(req.Query);
                return Results.Json(query, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Natural language parsing failed: {ex.Message}");
            }
        });

        app.MapPost("/api/storage/index/rebuild", async (IStorageSearchService search, HttpRequest request) =>
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

        app.MapPost("/api/storage/health/check", async (IFileMaintenanceService maintenance, HealthCheckRequest req) =>
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
                return Results.Json(report, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Health check failed: {ex.Message}");
            }
        });

        app.MapGet("/api/storage/health/orphans", async (IFileMaintenanceService maintenance) =>
        {
            try
            {
                var report = await maintenance.FindOrphansAsync();
                return Results.Json(report, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Orphan scan failed: {ex.Message}");
            }
        });

        app.MapPost("/api/storage/maintenance/repair", async (IFileMaintenanceService maintenance, RepairRequest req) =>
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
                return Results.Json(result, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Repair failed: {ex.Message}");
            }
        });

        app.MapPost("/api/storage/maintenance/defrag", async (IFileMaintenanceService maintenance, DefragRequest req) =>
        {
            try
            {
                var options = new DefragOptions(
                    MinFileSizeBytes: req.MinFileSizeBytes,
                    MaxFilesPerMerge: req.MaxFilesPerMerge,
                    PreserveOriginals: req.PreserveOriginals
                );
                var result = await maintenance.DefragmentAsync(options);
                return Results.Json(result, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Defragmentation failed: {ex.Message}");
            }
        });

        // ==================== DATA QUALITY ====================

        app.MapPost("/api/storage/quality/score", async (IDataQualityService quality, string path) =>
        {
            try
            {
                var score = await quality.ScoreAsync(path);
                return Results.Json(score, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Quality scoring failed: {ex.Message}");
            }
        });

        app.MapPost("/api/storage/quality/report", async (IDataQualityService quality, QualityReportRequest req) =>
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
                return Results.Json(report, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Quality report failed: {ex.Message}");
            }
        });

        app.MapGet("/api/storage/quality/alerts", async (IDataQualityService quality) =>
        {
            try
            {
                var alerts = await quality.GetQualityAlertsAsync();
                return Results.Json(alerts, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get quality alerts: {ex.Message}");
            }
        });

        app.MapGet("/api/storage/quality/sources/{symbol}", async (IDataQualityService quality, string symbol, DateTimeOffset? date) =>
        {
            try
            {
                var rankings = await quality.RankSourcesAsync(
                    symbol,
                    date ?? DateTimeOffset.UtcNow.Date,
                    MarketEventType.Trade);
                return Results.Json(rankings, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Source ranking failed: {ex.Message}");
            }
        });

        app.MapGet("/api/storage/quality/trend/{symbol}", async (IDataQualityService quality, string symbol, int? days) =>
        {
            try
            {
                var window = TimeSpan.FromDays(days ?? 30);
                var trend = await quality.GetTrendAsync(symbol, window);
                return Results.Json(trend, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Trend analysis failed: {ex.Message}");
            }
        });

        // ==================== TIER MIGRATION ====================

        app.MapGet("/api/storage/tiers/statistics", async (ITierMigrationService tiers) =>
        {
            try
            {
                var stats = await tiers.GetTierStatisticsAsync();
                return Results.Json(stats, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get tier statistics: {ex.Message}");
            }
        });

        app.MapPost("/api/storage/tiers/plan", async (ITierMigrationService tiers, int? horizonDays) =>
        {
            try
            {
                var plan = await tiers.PlanMigrationAsync(TimeSpan.FromDays(horizonDays ?? 7));
                return Results.Json(plan, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Migration planning failed: {ex.Message}");
            }
        });

        app.MapPost("/api/storage/tiers/migrate", async (ITierMigrationService tiers, TierMigrationRequest req) =>
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
                return Results.Json(result, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Migration failed: {ex.Message}");
            }
        });

        app.MapGet("/api/storage/tiers/target", (ITierMigrationService tiers, string path) =>
        {
            try
            {
                var tier = tiers.DetermineTargetTier(path);
                return Results.Json(new { path, targetTier = tier.ToString() }, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to determine tier: {ex.Message}");
            }
        });

        // ==================== SOURCE REGISTRY ====================

        app.MapGet("/api/storage/sources", (ISourceRegistry registry) =>
        {
            try
            {
                var sources = registry.GetAllSources();
                return Results.Json(sources, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get sources: {ex.Message}");
            }
        });

        app.MapGet("/api/storage/sources/{sourceId}", (ISourceRegistry registry, string sourceId) =>
        {
            try
            {
                var source = registry.GetSourceInfo(sourceId);
                return source is null
                    ? Results.NotFound($"Source '{sourceId}' not found")
                    : Results.Json(source, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get source: {ex.Message}");
            }
        });

        app.MapPost("/api/storage/sources", (ISourceRegistry registry, SourceInfo source) =>
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

        app.MapGet("/api/storage/sources/priority", (ISourceRegistry registry) =>
        {
            try
            {
                var order = registry.GetSourcePriorityOrder();
                return Results.Json(order, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get priority order: {ex.Message}");
            }
        });

        app.MapGet("/api/storage/symbols/{symbol}", (ISourceRegistry registry, string symbol) =>
        {
            try
            {
                var info = registry.GetSymbolInfo(symbol);
                return info is null
                    ? Results.NotFound($"Symbol '{symbol}' not found in registry")
                    : Results.Json(info, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get symbol info: {ex.Message}");
            }
        });

        app.MapPost("/api/storage/symbols/register", (ISourceRegistry registry, SymbolInfo symbol) =>
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

        app.MapGet("/api/storage/symbols/resolve/{alias}", (ISourceRegistry registry, string alias) =>
        {
            try
            {
                var canonical = registry.ResolveSymbolAlias(alias);
                return Results.Json(new { alias, canonical }, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to resolve alias: {ex.Message}");
            }
        });

        // ==================== STORAGE OVERVIEW ====================

        app.MapGet("/api/storage/overview", async (
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
                }, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get storage overview: {ex.Message}");
            }
        });
    }
}
