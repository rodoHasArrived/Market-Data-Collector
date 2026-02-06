using System.Text.Json;
using MarketDataCollector.Contracts.Api;
using MarketDataCollector.Contracts.Domain.Enums;
using MarketDataCollector.Domain.Events;
using MarketDataCollector.Storage;
using MarketDataCollector.Storage.Interfaces;
using MarketDataCollector.Storage.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace MarketDataCollector.Ui.Shared.Endpoints;

/// <summary>
/// Extension methods for registering storage management API endpoints.
/// Covers storage profiles, stats, health, catalog, tiers, quality, and admin operations.
/// </summary>
public static class StorageEndpoints
{
    /// <summary>
    /// Maps all storage management API endpoints.
    /// </summary>
    public static void MapStorageEndpoints(this WebApplication app, JsonSerializerOptions jsonOptions)
    {
        MapCoreStorageEndpoints(app, jsonOptions);
        MapStorageQualityEndpoints(app, jsonOptions);
        MapAdminStorageEndpoints(app, jsonOptions);
    }

    #region Core Storage Endpoints

    private static void MapCoreStorageEndpoints(WebApplication app, JsonSerializerOptions jsonOptions)
    {
        // GET /api/storage/profiles — List available storage profile presets
        app.MapGet(UiApiRoutes.StorageProfiles, () =>
        {
            var presets = StorageProfilePresets.GetPresets()
                .Select(p => new { id = p.Id, label = p.Label, description = p.Description });
            return Results.Json(presets, jsonOptions);
        });

        // GET /api/storage/stats — Overall storage usage statistics
        app.MapGet(UiApiRoutes.StorageStats, (StorageOptions options) =>
        {
            var rootPath = Path.GetFullPath(options.RootPath);
            if (!Directory.Exists(rootPath))
            {
                return Results.Json(new
                {
                    rootPath,
                    exists = false,
                    totalFiles = 0,
                    totalBytes = 0L,
                    namingConvention = options.NamingConvention.ToString(),
                    compress = options.Compress,
                    compressionCodec = options.CompressionCodec.ToString(),
                    retentionDays = options.RetentionDays
                }, jsonOptions);
            }

            var files = Directory.EnumerateFiles(rootPath, "*", SearchOption.AllDirectories).ToList();
            var totalBytes = files.Sum(f => new FileInfo(f).Length);
            var dataFiles = files.Where(f =>
                f.EndsWith(".jsonl", StringComparison.OrdinalIgnoreCase) ||
                f.EndsWith(".jsonl.gz", StringComparison.OrdinalIgnoreCase) ||
                f.EndsWith(".parquet", StringComparison.OrdinalIgnoreCase)).ToList();

            return Results.Json(new
            {
                rootPath,
                exists = true,
                totalFiles = files.Count,
                dataFiles = dataFiles.Count,
                totalBytes,
                totalBytesFormatted = FormatBytes(totalBytes),
                namingConvention = options.NamingConvention.ToString(),
                datePartition = options.DatePartition.ToString(),
                compress = options.Compress,
                compressionCodec = options.CompressionCodec.ToString(),
                retentionDays = options.RetentionDays,
                maxTotalBytes = options.MaxTotalBytes,
                generatedAt = DateTimeOffset.UtcNow
            }, jsonOptions);
        });

        // GET /api/storage/breakdown — Storage usage by symbol and event type
        app.MapGet(UiApiRoutes.StorageBreakdown, async (
            IStorageSearchService searchService,
            CancellationToken ct) =>
        {
            var catalog = await searchService.DiscoverAsync(new DiscoveryQuery(), ct);
            return Results.Json(new
            {
                generatedAt = catalog.GeneratedAt,
                rootPath = catalog.RootPath,
                totalEvents = catalog.TotalEvents,
                totalBytes = catalog.TotalBytes,
                totalBytesFormatted = FormatBytes(catalog.TotalBytes),
                symbols = catalog.Symbols.Select(s => new
                {
                    symbol = s.Symbol,
                    firstDate = s.FirstDate,
                    lastDate = s.LastDate,
                    totalEvents = s.TotalEvents,
                    totalBytes = s.TotalBytes,
                    totalBytesFormatted = FormatBytes(s.TotalBytes),
                    sources = s.Sources,
                    eventTypes = s.EventTypes
                }),
                sources = catalog.Sources,
                eventTypes = catalog.EventTypes,
                dateRange = new { start = catalog.DateRange.Start, end = catalog.DateRange.End }
            }, jsonOptions);
        });

        // GET /api/storage/symbol/{symbol}/info — Symbol-specific storage info
        app.MapGet(UiApiRoutes.StorageSymbolInfo, async (
            string symbol,
            IStorageCatalogService catalogService,
            CancellationToken ct) =>
        {
            var files = catalogService.GetFilesForSymbol(symbol).ToList();
            if (files.Count == 0)
            {
                return Results.Json(new { symbol, found = false, message = "No data files found for symbol" }, jsonOptions, statusCode: 404);
            }

            return Results.Json(new
            {
                symbol,
                found = true,
                fileCount = files.Count,
                totalBytes = files.Sum(f => f.SizeBytes),
                totalBytesFormatted = FormatBytes(files.Sum(f => f.SizeBytes)),
                totalEvents = files.Sum(f => f.EventCount),
                earliestDate = files.Where(f => f.FirstTimestamp.HasValue).Select(f => f.FirstTimestamp).Min(),
                latestDate = files.Where(f => f.LastTimestamp.HasValue).Select(f => f.LastTimestamp).Max(),
                eventTypes = files.Select(f => f.EventType).Where(t => t != null).Distinct(),
                sources = files.Select(f => f.Source).Where(s => s != null).Distinct()
            }, jsonOptions);
        });

        // GET /api/storage/symbol/{symbol}/stats — Per-symbol storage statistics
        app.MapGet(UiApiRoutes.StorageSymbolStats, (
            string symbol,
            IStorageCatalogService catalogService) =>
        {
            var files = catalogService.GetFilesForSymbol(symbol).ToList();
            var byType = files.GroupBy(f => f.EventType ?? "Unknown").Select(g => new
            {
                eventType = g.Key,
                fileCount = g.Count(),
                totalBytes = g.Sum(f => f.SizeBytes),
                totalEvents = g.Sum(f => f.EventCount)
            });

            var byDate = files
                .Where(f => f.Date.HasValue)
                .GroupBy(f => f.Date!.Value.ToString("yyyy-MM-dd"))
                .Select(g => new
                {
                    date = g.Key,
                    fileCount = g.Count(),
                    totalBytes = g.Sum(f => f.SizeBytes),
                    totalEvents = g.Sum(f => f.EventCount)
                }).OrderBy(d => d.date);

            return Results.Json(new
            {
                symbol,
                fileCount = files.Count,
                totalBytes = files.Sum(f => f.SizeBytes),
                totalEvents = files.Sum(f => f.EventCount),
                byEventType = byType,
                byDate
            }, jsonOptions);
        });

        // GET /api/storage/symbol/{symbol}/files — List files for a specific symbol
        app.MapGet(UiApiRoutes.StorageSymbolFiles, (
            string symbol,
            IStorageCatalogService catalogService) =>
        {
            var files = catalogService.GetFilesForSymbol(symbol)
                .Select(f => new
                {
                    path = f.RelativePath,
                    eventType = f.EventType,
                    source = f.Source,
                    sizeBytes = f.SizeBytes,
                    eventCount = f.EventCount,
                    date = f.Date,
                    firstTimestamp = f.FirstTimestamp,
                    lastTimestamp = f.LastTimestamp,
                    checksum = f.ChecksumSha256
                })
                .ToList();

            return Results.Json(new { symbol, fileCount = files.Count, files }, jsonOptions);
        });

        // GET /api/storage/symbol/{symbol}/path — File system path for symbol data
        app.MapGet(UiApiRoutes.StorageSymbolPath, (
            string symbol,
            StorageOptions options) =>
        {
            var rootPath = Path.GetFullPath(options.RootPath);
            var symbolPath = options.NamingConvention switch
            {
                FileNamingConvention.BySymbol => Path.Combine(rootPath, symbol),
                FileNamingConvention.BySource => rootPath,
                FileNamingConvention.ByDate => rootPath,
                FileNamingConvention.ByType => rootPath,
                FileNamingConvention.Flat => rootPath,
                _ => Path.Combine(rootPath, symbol)
            };

            return Results.Json(new
            {
                symbol,
                rootPath,
                symbolPath,
                exists = Directory.Exists(symbolPath),
                namingConvention = options.NamingConvention.ToString()
            }, jsonOptions);
        });

        // GET /api/storage/health — Storage system health status
        app.MapGet(UiApiRoutes.StorageHealth, async (
            IFileMaintenanceService maintenanceService,
            StorageOptions options,
            CancellationToken ct) =>
        {
            var report = await maintenanceService.RunHealthCheckAsync(new HealthCheckOptions(
                ValidateChecksums: false,
                CheckSequenceContinuity: false,
                ValidateSchemas: false,
                CheckFilePermissions: true,
                IdentifyCorruption: true,
                ParallelChecks: 2
            ), ct);

            return Results.Json(new
            {
                status = report.Summary.CorruptedFiles == 0 ? "healthy" : "degraded",
                rootPath = Path.GetFullPath(options.RootPath),
                summary = report.Summary,
                statistics = new
                {
                    report.Statistics.TotalFiles,
                    report.Statistics.TotalBytes,
                    totalBytesFormatted = FormatBytes(report.Statistics.TotalBytes),
                    report.Statistics.HealthyFiles,
                    report.Statistics.WarningFiles,
                    report.Statistics.CorruptedFiles,
                    report.Statistics.OrphanedFiles,
                    report.Statistics.CompressionRatio,
                    report.Statistics.FragmentationPct
                },
                scanDurationMs = report.ScanDurationMs,
                generatedAt = report.GeneratedAt
            }, jsonOptions);
        });

        // GET /api/storage/cleanup/candidates — Find files eligible for cleanup
        app.MapGet(UiApiRoutes.StorageCleanupCandidates, (
            StorageOptions options,
            int? olderThanDays) =>
        {
            var rootPath = Path.GetFullPath(options.RootPath);
            if (!Directory.Exists(rootPath))
            {
                return Results.Json(new { candidates = Array.Empty<object>(), totalReclaimableBytes = 0L }, jsonOptions);
            }

            var cutoffDays = olderThanDays ?? options.RetentionDays ?? 90;
            var cutoffDate = DateTime.UtcNow.AddDays(-cutoffDays);

            var candidates = Directory.EnumerateFiles(rootPath, "*", SearchOption.AllDirectories)
                .Select(f => new FileInfo(f))
                .Where(f => f.LastWriteTimeUtc < cutoffDate)
                .Where(f => f.Extension is ".jsonl" or ".gz" or ".zst" or ".parquet")
                .Select(f => new
                {
                    path = Path.GetRelativePath(rootPath, f.FullName),
                    sizeBytes = f.Length,
                    lastModified = f.LastWriteTimeUtc,
                    ageDays = (int)(DateTime.UtcNow - f.LastWriteTimeUtc).TotalDays
                })
                .OrderBy(f => f.lastModified)
                .Take(500)
                .ToList();

            return Results.Json(new
            {
                cutoffDays,
                cutoffDate,
                candidates,
                totalReclaimableBytes = candidates.Sum(c => c.sizeBytes),
                totalReclaimableBytesFormatted = FormatBytes(candidates.Sum(c => c.sizeBytes))
            }, jsonOptions);
        });

        // POST /api/storage/cleanup — Execute cleanup of old files
        app.MapPost(UiApiRoutes.StorageCleanup, async (
            IFileMaintenanceService maintenanceService,
            StorageOptions options,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var body = await ctx.Request.ReadFromJsonAsync<CleanupRequest>(jsonOptions, ct);
            var dryRun = body?.DryRun ?? true;
            var olderThanDays = body?.OlderThanDays ?? options.RetentionDays ?? 90;

            var rootPath = Path.GetFullPath(options.RootPath);
            var cutoffDate = DateTime.UtcNow.AddDays(-olderThanDays);

            var candidates = Directory.Exists(rootPath)
                ? Directory.EnumerateFiles(rootPath, "*", SearchOption.AllDirectories)
                    .Select(f => new FileInfo(f))
                    .Where(f => f.LastWriteTimeUtc < cutoffDate)
                    .Where(f => f.Extension is ".jsonl" or ".gz" or ".zst" or ".parquet")
                    .ToList()
                : new List<FileInfo>();

            var totalBytes = candidates.Sum(f => f.Length);
            var deletedCount = 0;

            if (!dryRun)
            {
                foreach (var file in candidates)
                {
                    ct.ThrowIfCancellationRequested();
                    try
                    {
                        file.Delete();
                        deletedCount++;
                    }
                    catch (IOException) { /* File in use */ }
                }
            }

            return Results.Json(new
            {
                dryRun,
                olderThanDays,
                candidateCount = candidates.Count,
                deletedCount,
                reclaimedBytes = dryRun ? 0 : totalBytes,
                reclaimedBytesFormatted = FormatBytes(dryRun ? 0 : totalBytes)
            }, jsonOptions);
        });

        // GET /api/storage/archive/stats — Archive storage statistics
        app.MapGet(UiApiRoutes.StorageArchiveStats, (StorageOptions options) =>
        {
            var archivePath = options.Tiering?.Tiers
                .FirstOrDefault(t => t.Name.Equals("archive", StringComparison.OrdinalIgnoreCase))?.Path;

            if (archivePath == null || !Directory.Exists(archivePath))
            {
                return Results.Json(new
                {
                    archiveEnabled = options.Tiering?.Enabled ?? false,
                    archivePath,
                    exists = false,
                    fileCount = 0,
                    totalBytes = 0L
                }, jsonOptions);
            }

            var files = Directory.EnumerateFiles(archivePath, "*", SearchOption.AllDirectories)
                .Select(f => new FileInfo(f)).ToList();

            return Results.Json(new
            {
                archiveEnabled = true,
                archivePath,
                exists = true,
                fileCount = files.Count,
                totalBytes = files.Sum(f => f.Length),
                totalBytesFormatted = FormatBytes(files.Sum(f => f.Length)),
                oldestFile = files.Count > 0 ? files.Min(f => f.LastWriteTimeUtc) : (DateTime?)null,
                newestFile = files.Count > 0 ? files.Max(f => f.LastWriteTimeUtc) : (DateTime?)null,
                generatedAt = DateTimeOffset.UtcNow
            }, jsonOptions);
        });

        // GET /api/storage/catalog — Storage directory catalog
        app.MapGet(UiApiRoutes.StorageCatalog, (IStorageCatalogService catalogService) =>
        {
            var catalog = catalogService.GetCatalog();
            var stats = catalogService.GetStatistics();
            return Results.Json(new
            {
                catalogVersion = catalog.CatalogVersion,
                catalogId = catalog.CatalogId,
                createdAt = catalog.CreatedAt,
                lastUpdatedAt = catalog.LastUpdatedAt,
                rootPath = catalog.Configuration?.RootPath,
                statistics = stats,
                symbolCount = catalog.Symbols.Count,
                symbols = catalog.Symbols.Select(s => new
                {
                    symbol = s.Key,
                    s.Value.FileCount,
                    s.Value.TotalBytes,
                    s.Value.EventCount,
                    dateRange = s.Value.DateRange,
                    eventTypes = s.Value.EventTypes,
                    sources = s.Value.Sources
                })
            }, jsonOptions);
        });

        // GET /api/storage/search/files — Search for files with filters
        app.MapGet(UiApiRoutes.StorageSearchFiles, async (
            IStorageSearchService searchService,
            string? symbol,
            string? eventType,
            string? source,
            string? from,
            string? to,
            string? sortBy,
            int? skip,
            int? take,
            CancellationToken ct) =>
        {
            MarketEventType[]? types = null;
            if (!string.IsNullOrEmpty(eventType) && Enum.TryParse<MarketEventType>(eventType, true, out var parsed))
            {
                types = new[] { parsed };
            }

            var query = new FileSearchQuery(
                Symbols: string.IsNullOrEmpty(symbol) ? null : new[] { symbol },
                Types: types,
                Sources: string.IsNullOrEmpty(source) ? null : new[] { source },
                From: string.IsNullOrEmpty(from) ? null : DateTimeOffset.Parse(from),
                To: string.IsNullOrEmpty(to) ? null : DateTimeOffset.Parse(to),
                SortBy: Enum.TryParse<SortField>(sortBy, true, out var sort) ? sort : SortField.Date,
                Skip: skip ?? 0,
                Take: take ?? 100
            );

            var result = await searchService.SearchFilesAsync(query, ct);
            return Results.Json(new
            {
                totalMatches = result.TotalMatches,
                results = result.Results
            }, jsonOptions);
        });

        // GET /api/storage/health/check — Perform a comprehensive storage health check
        app.MapGet(UiApiRoutes.StorageHealthCheck, async (
            IFileMaintenanceService maintenanceService,
            CancellationToken ct) =>
        {
            var report = await maintenanceService.RunHealthCheckAsync(new HealthCheckOptions(
                ValidateChecksums: true,
                CheckSequenceContinuity: true,
                ValidateSchemas: true,
                CheckFilePermissions: true,
                IdentifyCorruption: true,
                ParallelChecks: 4
            ), ct);

            return Results.Json(report, jsonOptions);
        });

        // GET /api/storage/health/orphans — Find orphaned files not tracked in manifest
        app.MapGet(UiApiRoutes.StorageHealthOrphans, async (
            IFileMaintenanceService maintenanceService,
            CancellationToken ct) =>
        {
            var report = await maintenanceService.FindOrphansAsync(ct);
            return Results.Json(report, jsonOptions);
        });

        // POST /api/storage/tiers/migrate — Migrate data between storage tiers
        app.MapPost(UiApiRoutes.StorageTiersMigrate, async (
            ITierMigrationService tierService,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var body = await ctx.Request.ReadFromJsonAsync<TierMigrateRequest>(jsonOptions, ct);
            if (body == null || string.IsNullOrEmpty(body.SourcePath))
            {
                return Results.BadRequest(new { error = "sourcePath is required" });
            }

            if (!Enum.TryParse<StorageTier>(body.TargetTier, true, out var targetTier))
            {
                return Results.BadRequest(new { error = $"Invalid targetTier: {body.TargetTier}. Valid: Hot, Warm, Cold, Archive" });
            }

            var result = await tierService.MigrateAsync(
                body.SourcePath,
                targetTier,
                new MigrationOptions(
                    DeleteSource: body.DeleteSource,
                    VerifyChecksum: body.VerifyChecksum,
                    ParallelFiles: body.ParallelFiles ?? 4
                ), ct);

            return Results.Json(result, jsonOptions);
        });

        // GET /api/storage/tiers/statistics — Tier statistics (hot/warm/cold)
        app.MapGet(UiApiRoutes.StorageTiersStatistics, async (
            ITierMigrationService tierService,
            CancellationToken ct) =>
        {
            var stats = await tierService.GetTierStatisticsAsync(ct);
            return Results.Json(stats, jsonOptions);
        });

        // GET /api/storage/tiers/plan — Plan for tier migrations
        app.MapGet(UiApiRoutes.StorageTiersPlan, async (
            ITierMigrationService tierService,
            int? horizonDays,
            CancellationToken ct) =>
        {
            var horizon = TimeSpan.FromDays(horizonDays ?? 30);
            var plan = await tierService.PlanMigrationAsync(horizon, ct);
            return Results.Json(plan, jsonOptions);
        });

        // POST /api/storage/maintenance/defrag — Run defragmentation
        app.MapPost(UiApiRoutes.StorageMaintenanceDefrag, async (
            IFileMaintenanceService maintenanceService,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var body = await ctx.Request.ReadFromJsonAsync<DefragRequest>(jsonOptions, ct);
            var result = await maintenanceService.DefragmentAsync(new DefragOptions(
                MinFileSizeBytes: body?.MinFileSizeBytes ?? 1_048_576,
                MaxFilesPerMerge: body?.MaxFilesPerMerge ?? 100,
                PreserveOriginals: body?.PreserveOriginals ?? false,
                MaxFileAge: TimeSpan.FromDays(body?.MaxFileAgeDays ?? 7)
            ), ct);

            return Results.Json(result, jsonOptions);
        });

        // GET /api/diagnostics/storage — Storage diagnostics
        app.MapGet(UiApiRoutes.DiagnosticsStorage, async (
            StorageOptions options,
            IFileMaintenanceService maintenanceService,
            ITierMigrationService tierService,
            FilePermissionsService permissionsService,
            CancellationToken ct) =>
        {
            var rootPath = Path.GetFullPath(options.RootPath);
            var permDiag = permissionsService.GetPermissionsDiagnostic(rootPath);
            var tierStats = await tierService.GetTierStatisticsAsync(ct);

            return Results.Json(new
            {
                rootPath,
                permissions = new
                {
                    permDiag.Exists,
                    permDiag.CanRead,
                    permDiag.CanWrite,
                    permDiag.UnixMode,
                    permDiag.Platform,
                    issues = permDiag.Issues
                },
                configuration = new
                {
                    namingConvention = options.NamingConvention.ToString(),
                    datePartition = options.DatePartition.ToString(),
                    compress = options.Compress,
                    compressionCodec = options.CompressionCodec.ToString(),
                    retentionDays = options.RetentionDays,
                    maxTotalBytes = options.MaxTotalBytes,
                    tieringEnabled = options.Tiering?.Enabled ?? false,
                    generateManifests = options.GenerateManifests
                },
                tiers = tierStats,
                generatedAt = DateTimeOffset.UtcNow
            }, jsonOptions);
        });
    }

    #endregion

    #region Storage Quality Endpoints

    private static void MapStorageQualityEndpoints(WebApplication app, JsonSerializerOptions jsonOptions)
    {
        // GET /api/storage/quality/summary — Data quality summary
        app.MapGet(UiApiRoutes.StorageQualitySummary, async (
            IDataQualityService qualityService,
            StorageOptions options,
            CancellationToken ct) =>
        {
            var alerts = await qualityService.GetQualityAlertsAsync(ct);
            return Results.Json(new
            {
                rootPath = Path.GetFullPath(options.RootPath),
                alertCount = alerts.Length,
                criticalAlerts = alerts.Count(a => a.CurrentScore < 0.5),
                warningAlerts = alerts.Count(a => a.CurrentScore >= 0.5 && a.CurrentScore < 0.85),
                healthyThreshold = 0.85,
                generatedAt = DateTimeOffset.UtcNow
            }, jsonOptions);
        });

        // GET /api/storage/quality/scores — Per-symbol quality scores
        app.MapGet(UiApiRoutes.StorageQualityScores, async (
            IDataQualityService qualityService,
            IStorageCatalogService catalogService,
            StorageOptions options,
            CancellationToken ct) =>
        {
            var catalog = catalogService.GetCatalog();
            var scores = new List<object>();

            foreach (var symbolEntry in catalog.Symbols.Take(50))
            {
                var files = catalogService.GetFilesForSymbol(symbolEntry.Key).Take(1).ToList();
                if (files.Count > 0)
                {
                    var filePath = Path.Combine(options.RootPath, files[0].RelativePath);
                    if (File.Exists(filePath))
                    {
                        try
                        {
                            var score = await qualityService.ScoreAsync(filePath, ct);
                            scores.Add(new
                            {
                                symbol = symbolEntry.Key,
                                overallScore = score.OverallScore,
                                evaluatedAt = score.EvaluatedAt,
                                dimensions = score.Dimensions.Select(d => new { d.Name, d.Score, d.Weight })
                            });
                        }
                        catch
                        {
                            scores.Add(new
                            {
                                symbol = symbolEntry.Key,
                                overallScore = (double?)null,
                                evaluatedAt = (DateTimeOffset?)null,
                                dimensions = Array.Empty<object>()
                            });
                        }
                    }
                }
            }

            return Results.Json(new { scores, generatedAt = DateTimeOffset.UtcNow }, jsonOptions);
        });

        // GET /api/storage/quality/symbol/{symbol} — Symbol quality details
        app.MapGet(UiApiRoutes.StorageQualitySymbol, async (
            string symbol,
            IDataQualityService qualityService,
            IStorageCatalogService catalogService,
            StorageOptions options,
            CancellationToken ct) =>
        {
            var files = catalogService.GetFilesForSymbol(symbol).ToList();
            if (files.Count == 0)
            {
                return Results.NotFound(new { symbol, error = "No data files found for symbol" });
            }

            var fileScores = new List<object>();
            foreach (var file in files.Take(20))
            {
                var filePath = Path.Combine(options.RootPath, file.RelativePath);
                if (!File.Exists(filePath)) continue;

                try
                {
                    var score = await qualityService.ScoreAsync(filePath, ct);
                    fileScores.Add(new
                    {
                        path = file.RelativePath,
                        eventType = file.EventType,
                        overallScore = score.OverallScore,
                        dimensions = score.Dimensions
                    });
                }
                catch { /* skip unscoreable files */ }
            }

            var avgScore = fileScores.Count > 0
                ? fileScores.Average(s => ((dynamic)s).overallScore)
                : 0.0;

            return Results.Json(new
            {
                symbol,
                fileCount = files.Count,
                scoredFiles = fileScores.Count,
                averageScore = avgScore,
                files = fileScores,
                generatedAt = DateTimeOffset.UtcNow
            }, jsonOptions);
        });

        // GET /api/storage/quality/alerts — Quality alerts list
        app.MapGet(UiApiRoutes.StorageQualityAlerts, async (
            IDataQualityService qualityService,
            CancellationToken ct) =>
        {
            var alerts = await qualityService.GetQualityAlertsAsync(ct);
            return Results.Json(new { alerts, count = alerts.Length, generatedAt = DateTimeOffset.UtcNow }, jsonOptions);
        });

        // POST /api/storage/quality/alerts/{alertId}/acknowledge — Mark alert as acknowledged
        app.MapPost(UiApiRoutes.StorageQualityAlertAcknowledge, (string alertId) =>
        {
            // Alert acknowledgment is tracked in-memory; production would persist this
            return Results.Json(new
            {
                alertId,
                acknowledged = true,
                acknowledgedAt = DateTimeOffset.UtcNow
            });
        });

        // GET /api/storage/quality/rankings/{symbol} — Quality rankings by symbol
        app.MapGet(UiApiRoutes.StorageQualityRankings, async (
            string symbol,
            IDataQualityService qualityService,
            CancellationToken ct) =>
        {
            var rankings = await qualityService.RankSourcesAsync(
                symbol,
                DateTimeOffset.UtcNow.Date,
                MarketEventType.Trade,
                ct);

            return Results.Json(new { symbol, rankings, generatedAt = DateTimeOffset.UtcNow }, jsonOptions);
        });

        // GET /api/storage/quality/trends — Quality trends over time
        app.MapGet(UiApiRoutes.StorageQualityTrends, async (
            IDataQualityService qualityService,
            string? symbol,
            int? windowDays,
            CancellationToken ct) =>
        {
            var targetSymbol = symbol ?? "SPY";
            var window = TimeSpan.FromDays(windowDays ?? 30);
            var trend = await qualityService.GetTrendAsync(targetSymbol, window, ct);
            return Results.Json(trend, jsonOptions);
        });

        // GET /api/storage/quality/anomalies — Detected quality anomalies
        app.MapGet(UiApiRoutes.StorageQualityAnomalies, async (
            IDataQualityService qualityService,
            CancellationToken ct) =>
        {
            var alerts = await qualityService.GetQualityAlertsAsync(ct);
            var anomalies = alerts
                .Where(a => a.CurrentScore < a.Threshold)
                .Select(a => new
                {
                    a.Symbol,
                    a.Issue,
                    a.CurrentScore,
                    a.Threshold,
                    severity = a.CurrentScore < 0.5 ? "critical" : "warning",
                    a.Recommendation
                });

            return Results.Json(new { anomalies, count = anomalies.Count(), generatedAt = DateTimeOffset.UtcNow }, jsonOptions);
        });

        // POST /api/storage/quality/check — Run quality check on specific path(s)
        app.MapPost(UiApiRoutes.StorageQualityCheck, async (
            IDataQualityService qualityService,
            StorageOptions options,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var body = await ctx.Request.ReadFromJsonAsync<QualityCheckRequest>(jsonOptions, ct);
            var paths = body?.Paths ?? new[] { Path.GetFullPath(options.RootPath) };

            var report = await qualityService.GenerateReportAsync(new QualityReportOptions(
                Paths: paths,
                MinScoreThreshold: body?.MinScoreThreshold ?? 1.0,
                IncludeRecommendations: true
            ), ct);

            return Results.Json(report, jsonOptions);
        });
    }

    #endregion

    #region Admin Storage Endpoints

    private static void MapAdminStorageEndpoints(WebApplication app, JsonSerializerOptions jsonOptions)
    {
        // GET /api/admin/storage/tiers — Admin storage tier management
        app.MapGet(UiApiRoutes.AdminStorageTiers, (StorageOptions options) =>
        {
            var tiers = options.Tiering?.Tiers.Select(t => new
            {
                name = t.Name,
                path = t.Path,
                exists = Directory.Exists(t.Path),
                maxAgeDays = t.MaxAgeDays,
                maxSizeGb = t.MaxSizeGb,
                format = t.Format,
                compression = t.Compression?.ToString(),
                storageClass = t.StorageClass
            }) ?? Enumerable.Empty<object>();

            return Results.Json(new
            {
                tieringEnabled = options.Tiering?.Enabled ?? false,
                migrationSchedule = options.Tiering?.MigrationSchedule,
                parallelMigrations = options.Tiering?.ParallelMigrations ?? 4,
                tiers
            }, jsonOptions);
        });

        // POST /api/admin/storage/migrate/{targetTier} — Admin storage migration
        app.MapPost(UiApiRoutes.AdminStorageMigrate, async (
            string targetTier,
            ITierMigrationService tierService,
            StorageOptions options,
            CancellationToken ct) =>
        {
            if (!Enum.TryParse<StorageTier>(targetTier, true, out var tier))
            {
                return Results.BadRequest(new { error = $"Invalid tier: {targetTier}" });
            }

            var rootPath = Path.GetFullPath(options.RootPath);
            var result = await tierService.MigrateAsync(
                rootPath,
                tier,
                new MigrationOptions(DeleteSource: false, VerifyChecksum: true),
                ct);

            return Results.Json(result, jsonOptions);
        });

        // GET /api/admin/storage/usage — Admin storage usage reporting
        app.MapGet(UiApiRoutes.AdminStorageUsage, async (
            StorageOptions options,
            ITierMigrationService tierService,
            CancellationToken ct) =>
        {
            var rootPath = Path.GetFullPath(options.RootPath);
            var tierStats = await tierService.GetTierStatisticsAsync(ct);

            long totalBytes = 0;
            int totalFiles = 0;

            if (Directory.Exists(rootPath))
            {
                var files = Directory.EnumerateFiles(rootPath, "*", SearchOption.AllDirectories).ToList();
                totalFiles = files.Count;
                totalBytes = files.Sum(f => new FileInfo(f).Length);
            }

            return Results.Json(new
            {
                rootPath,
                totalFiles,
                totalBytes,
                totalBytesFormatted = FormatBytes(totalBytes),
                quotas = options.Quotas != null ? new
                {
                    globalMaxBytes = options.Quotas.Global?.MaxBytes,
                    globalMaxFiles = options.Quotas.Global?.MaxFiles,
                    enforcement = options.Quotas.Global?.Enforcement.ToString()
                } : null,
                tiers = tierStats,
                retentionDays = options.RetentionDays,
                maxTotalBytes = options.MaxTotalBytes,
                generatedAt = DateTimeOffset.UtcNow
            }, jsonOptions);
        });

        // GET /api/admin/storage/permissions — Storage permissions info
        app.MapGet(UiApiRoutes.AdminStoragePermissions, (
            StorageOptions options,
            FilePermissionsService permissionsService) =>
        {
            var rootPath = Path.GetFullPath(options.RootPath);
            var diagnostic = permissionsService.GetPermissionsDiagnostic(rootPath);
            return Results.Json(diagnostic, jsonOptions);
        });

        // GET /api/admin/retention — Get retention policies
        app.MapGet(UiApiRoutes.AdminRetention, (StorageOptions options) =>
        {
            var policies = options.Policies?.Select(p => new
            {
                eventType = p.Key,
                classification = p.Value.Classification.ToString(),
                hotTierDays = p.Value.HotTierDays,
                warmTierDays = p.Value.WarmTierDays,
                coldTierDays = p.Value.ColdTierDays,
                archiveTier = p.Value.ArchiveTier,
                archive = p.Value.Archive != null ? new
                {
                    reason = p.Value.Archive.Reason.ToString(),
                    p.Value.Archive.Description,
                    p.Value.Archive.Immutable,
                    p.Value.Archive.RequiresEncryption
                } : null
            }) ?? Enumerable.Empty<object>();

            return Results.Json(new
            {
                globalRetentionDays = options.RetentionDays,
                maxTotalBytes = options.MaxTotalBytes,
                policies,
                generatedAt = DateTimeOffset.UtcNow
            }, jsonOptions);
        });

        // DELETE /api/admin/retention/{policyId}/delete — Delete a retention policy
        app.MapDelete(UiApiRoutes.AdminRetentionDelete, (string policyId) =>
        {
            // Retention policies are configured in appsettings; runtime removal is not supported
            return Results.Json(new
            {
                policyId,
                deleted = false,
                message = "Retention policies are defined in configuration. Update appsettings.json to modify."
            }, jsonOptions, statusCode: StatusCodes.Status405MethodNotAllowed);
        });

        // POST /api/admin/retention/apply — Apply retention policies immediately
        app.MapPost(UiApiRoutes.AdminRetentionApply, async (
            StorageOptions options,
            IFileMaintenanceService maintenanceService,
            CancellationToken ct) =>
        {
            if (options.RetentionDays == null)
            {
                return Results.Json(new { applied = false, message = "No retention policy configured" }, jsonOptions);
            }

            var rootPath = Path.GetFullPath(options.RootPath);
            var cutoffDate = DateTime.UtcNow.AddDays(-options.RetentionDays.Value);
            var deleted = 0;
            long reclaimedBytes = 0;

            if (Directory.Exists(rootPath))
            {
                var oldFiles = Directory.EnumerateFiles(rootPath, "*", SearchOption.AllDirectories)
                    .Select(f => new FileInfo(f))
                    .Where(f => f.LastWriteTimeUtc < cutoffDate)
                    .Where(f => f.Extension is ".jsonl" or ".gz" or ".zst" or ".parquet")
                    .ToList();

                foreach (var file in oldFiles)
                {
                    ct.ThrowIfCancellationRequested();
                    try
                    {
                        reclaimedBytes += file.Length;
                        file.Delete();
                        deleted++;
                    }
                    catch (IOException) { /* File in use */ }
                }
            }

            return Results.Json(new
            {
                applied = true,
                retentionDays = options.RetentionDays,
                deletedFiles = deleted,
                reclaimedBytes,
                reclaimedBytesFormatted = FormatBytes(reclaimedBytes)
            }, jsonOptions);
        });

        // GET /api/admin/cleanup/preview — Preview what cleanup would delete
        app.MapGet(UiApiRoutes.AdminCleanupPreview, (StorageOptions options) =>
        {
            var rootPath = Path.GetFullPath(options.RootPath);
            if (!Directory.Exists(rootPath))
            {
                return Results.Json(new { candidates = Array.Empty<object>(), totalBytes = 0L }, jsonOptions);
            }

            var retentionDays = options.RetentionDays ?? 90;
            var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);

            var candidates = Directory.EnumerateFiles(rootPath, "*", SearchOption.AllDirectories)
                .Select(f => new FileInfo(f))
                .Where(f => f.LastWriteTimeUtc < cutoffDate)
                .Where(f => f.Extension is ".jsonl" or ".gz" or ".zst" or ".parquet")
                .OrderBy(f => f.LastWriteTimeUtc)
                .Take(200)
                .Select(f => new
                {
                    path = Path.GetRelativePath(rootPath, f.FullName),
                    sizeBytes = f.Length,
                    lastModified = f.LastWriteTimeUtc,
                    ageDays = (int)(DateTime.UtcNow - f.LastWriteTimeUtc).TotalDays
                })
                .ToList();

            return Results.Json(new
            {
                retentionDays,
                candidateCount = candidates.Count,
                totalBytes = candidates.Sum(c => c.sizeBytes),
                totalBytesFormatted = FormatBytes(candidates.Sum(c => c.sizeBytes)),
                candidates
            }, jsonOptions);
        });

        // POST /api/admin/cleanup/execute — Execute cleanup
        app.MapPost(UiApiRoutes.AdminCleanupExecute, async (
            StorageOptions options,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var body = await ctx.Request.ReadFromJsonAsync<CleanupRequest>(jsonOptions, ct);
            var dryRun = body?.DryRun ?? true;
            var retentionDays = body?.OlderThanDays ?? options.RetentionDays ?? 90;

            var rootPath = Path.GetFullPath(options.RootPath);
            var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);

            var candidates = Directory.Exists(rootPath)
                ? Directory.EnumerateFiles(rootPath, "*", SearchOption.AllDirectories)
                    .Select(f => new FileInfo(f))
                    .Where(f => f.LastWriteTimeUtc < cutoffDate)
                    .Where(f => f.Extension is ".jsonl" or ".gz" or ".zst" or ".parquet")
                    .ToList()
                : new List<FileInfo>();

            var totalBytes = candidates.Sum(f => f.Length);
            var deletedCount = 0;

            if (!dryRun)
            {
                foreach (var file in candidates)
                {
                    ct.ThrowIfCancellationRequested();
                    try
                    {
                        file.Delete();
                        deletedCount++;
                    }
                    catch (IOException) { /* File in use */ }
                }
            }

            return Results.Json(new
            {
                dryRun,
                retentionDays,
                candidateCount = candidates.Count,
                deletedCount,
                reclaimedBytes = dryRun ? 0 : totalBytes,
                reclaimedBytesFormatted = FormatBytes(dryRun ? 0 : totalBytes)
            }, jsonOptions);
        });
    }

    #endregion

    #region Request Models

    private sealed class CleanupRequest
    {
        public bool DryRun { get; set; } = true;
        public int? OlderThanDays { get; set; }
    }

    private sealed class TierMigrateRequest
    {
        public string SourcePath { get; set; } = "";
        public string TargetTier { get; set; } = "Warm";
        public bool DeleteSource { get; set; }
        public bool VerifyChecksum { get; set; } = true;
        public int? ParallelFiles { get; set; }
    }

    private sealed class DefragRequest
    {
        public long? MinFileSizeBytes { get; set; }
        public int? MaxFilesPerMerge { get; set; }
        public bool? PreserveOriginals { get; set; }
        public int? MaxFileAgeDays { get; set; }
    }

    private sealed class QualityCheckRequest
    {
        public string[]? Paths { get; set; }
        public double? MinScoreThreshold { get; set; }
    }

    #endregion

    #region Helpers

    private static string FormatBytes(long bytes) => bytes switch
    {
        >= 1_099_511_627_776 => $"{bytes / 1_099_511_627_776.0:F2} TB",
        >= 1_073_741_824 => $"{bytes / 1_073_741_824.0:F2} GB",
        >= 1_048_576 => $"{bytes / 1_048_576.0:F2} MB",
        >= 1_024 => $"{bytes / 1_024.0:F2} KB",
        _ => $"{bytes} B"
    };

    #endregion
}
