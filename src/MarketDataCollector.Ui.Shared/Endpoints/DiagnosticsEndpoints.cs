using System.Diagnostics;
using System.Text.Json;
using MarketDataCollector.Application.Config;
using MarketDataCollector.Application.ResultTypes;
using MarketDataCollector.Application.Services;
using MarketDataCollector.Application.UI;
using MarketDataCollector.Contracts.Api;
using MarketDataCollector.Ui.Shared.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace MarketDataCollector.Ui.Shared.Endpoints;

/// <summary>
/// Extension methods for registering diagnostics API endpoints.
/// Shared between web dashboard and desktop application hosts.
/// Provides system diagnostics, validation, configuration inspection,
/// and connectivity testing via the /api/diagnostics/* routes.
/// </summary>
public static class DiagnosticsEndpoints
{
    /// <summary>
    /// Maps all diagnostics API endpoints declared in <see cref="UiApiRoutes"/>.
    /// </summary>
    public static void MapDiagnosticsEndpoints(this WebApplication app, JsonSerializerOptions jsonOptions)
    {
        // POST /api/diagnostics/dry-run
        app.MapPost(UiApiRoutes.DiagnosticsDryRun, async (ConfigStore store, DryRunRequestBody? body, CancellationToken ct) =>
        {
            var cfg = store.Load();
            var dryRunService = new DryRunService();

            var options = new DryRunOptions(
                ValidateConfiguration: body?.ValidateConfiguration ?? true,
                ValidateFileSystem: body?.ValidateFileSystem ?? true,
                ValidateConnectivity: body?.ValidateConnectivity ?? true,
                ValidateProviders: body?.ValidateProviders ?? true,
                ValidateSymbols: body?.ValidateSymbols ?? true,
                ValidateResources: body?.ValidateResources ?? true
            );

            var result = await dryRunService.ValidateAsync(cfg, options, ct);

            return Results.Json(new
            {
                overallSuccess = result.OverallSuccess,
                startTime = result.StartTime,
                endTime = result.EndTime,
                durationMs = result.DurationMs,
                configuration = FormatValidationSection(result.ConfigurationValidation),
                fileSystem = FormatValidationSection(result.FileSystemValidation),
                connectivity = FormatValidationSection(result.ConnectivityValidation),
                providers = FormatValidationSection(result.ProviderValidation),
                symbols = FormatValidationSection(result.SymbolValidation),
                resources = FormatValidationSection(result.ResourceValidation)
            }, jsonOptions);
        });

        // GET /api/diagnostics/providers
        app.MapGet(UiApiRoutes.DiagnosticsProviders, (ConfigStore store) =>
        {
            var cfg = store.Load();
            var sources = cfg.DataSources?.Sources ?? Array.Empty<DataSourceConfig>();

            var providerDiagnostics = sources.Select(s => new
            {
                id = s.Id,
                name = s.Name,
                provider = s.Provider.ToString(),
                enabled = s.Enabled,
                type = s.Type.ToString(),
                priority = s.Priority,
                symbolCount = s.Symbols?.Length ?? 0,
                hasCredentials = HasCredentialsForProvider(s.Provider, cfg)
            }).ToArray();

            return Results.Json(new
            {
                activeDataSource = cfg.DataSource.ToString(),
                configuredProviders = providerDiagnostics,
                totalProviders = providerDiagnostics.Length,
                enabledProviders = providerDiagnostics.Count(p => p.enabled),
                backfillEnabled = cfg.Backfill?.Enabled ?? false,
                backfillProvider = cfg.Backfill?.Provider ?? "composite",
                timestamp = DateTimeOffset.UtcNow
            }, jsonOptions);
        });

        // GET /api/diagnostics/storage
        app.MapGet(UiApiRoutes.DiagnosticsStorage, (ConfigStore store) =>
        {
            var cfg = store.Load();
            var dataRoot = store.GetDataRoot(cfg);
            var storageInfo = GetStorageDiagnostics(dataRoot, cfg.Storage);

            return Results.Json(storageInfo, jsonOptions);
        });

        // GET /api/diagnostics/config
        app.MapGet(UiApiRoutes.DiagnosticsConfig, (ConfigStore store) =>
        {
            var cfg = store.Load();

            return Results.Json(new
            {
                configPath = store.ConfigPath,
                configExists = File.Exists(store.ConfigPath),
                dataRoot = cfg.DataRoot,
                dataSource = cfg.DataSource.ToString(),
                symbolCount = cfg.Symbols?.Length ?? 0,
                storageNaming = cfg.Storage?.NamingConvention ?? "BySymbol",
                storageDatePartition = cfg.Storage?.DatePartition ?? "Daily",
                storageProfile = cfg.Storage?.Profile,
                retentionDays = cfg.Storage?.RetentionDays,
                maxTotalMegabytes = cfg.Storage?.MaxTotalMegabytes,
                compress = cfg.Compress,
                backfillEnabled = cfg.Backfill?.Enabled ?? false,
                backfillProvider = cfg.Backfill?.Provider,
                derivativesEnabled = cfg.Derivatives?.Enabled ?? false,
                hasAlpacaConfig = cfg.Alpaca != null,
                hasIBConfig = cfg.IB != null,
                hasPolygonConfig = cfg.Polygon != null,
                hasStockSharpConfig = cfg.StockSharp != null,
                timestamp = DateTimeOffset.UtcNow
            }, jsonOptions);
        });

        // GET /api/diagnostics/bundle
        app.MapGet(UiApiRoutes.DiagnosticsBundle, async (ConfigStore store, CancellationToken ct) =>
        {
            var cfg = store.Load();
            var dataRoot = store.GetDataRoot(cfg);

            var bundleService = new DiagnosticBundleService(
                dataRoot,
                metricsProvider: null,
                configProvider: () => cfg
            );

            var options = new DiagnosticBundleOptions(
                IncludeSystemInfo: true,
                IncludeConfiguration: true,
                IncludeMetrics: true,
                IncludeLogs: true,
                IncludeStorageInfo: true,
                IncludeEnvironmentVariables: true,
                LogDays: 3
            );

            var result = await bundleService.GenerateAsync(options, ct);

            if (result.Success && result.ZipPath != null && File.Exists(result.ZipPath))
            {
                var bytes = bundleService.ReadBundle(result.ZipPath);

                // Clean up the temporary ZIP file after reading
                try { File.Delete(result.ZipPath); } catch { /* best effort cleanup */ }

                return Results.File(
                    bytes,
                    contentType: "application/zip",
                    fileDownloadName: $"{result.BundleId}.zip"
                );
            }

            return Results.Json(new
            {
                success = false,
                error = result.Message ?? "Failed to generate diagnostic bundle"
            }, jsonOptions, statusCode: StatusCodes.Status500InternalServerError);
        });

        // GET /api/diagnostics/metrics
        app.MapGet(UiApiRoutes.DiagnosticsMetrics, (HttpContext ctx) =>
        {
            var handlers = ctx.RequestServices.GetService<StatusEndpointHandlers>();
            if (handlers != null)
            {
                var content = handlers.GetPrometheusMetrics();
                return Results.Content(content, "text/plain; version=0.0.4");
            }

            // Fallback: return basic process metrics when StatusEndpointHandlers is not available
            using var process = Process.GetCurrentProcess();
            var gcInfo = GC.GetGCMemoryInfo();

            var basicMetrics = new
            {
                process = new
                {
                    workingSetMb = Math.Round(process.WorkingSet64 / (1024.0 * 1024.0), 2),
                    privateMemoryMb = Math.Round(process.PrivateMemorySize64 / (1024.0 * 1024.0), 2),
                    threadCount = process.Threads.Count,
                    totalProcessorTimeSeconds = Math.Round(process.TotalProcessorTime.TotalSeconds, 2),
                    startTime = process.StartTime.ToUniversalTime()
                },
                gc = new
                {
                    heapSizeMb = Math.Round(GC.GetTotalMemory(false) / (1024.0 * 1024.0), 2),
                    totalAvailableMb = Math.Round(gcInfo.TotalAvailableMemoryBytes / (1024.0 * 1024.0), 2),
                    gen0Collections = GC.CollectionCount(0),
                    gen1Collections = GC.CollectionCount(1),
                    gen2Collections = GC.CollectionCount(2)
                },
                timestamp = DateTimeOffset.UtcNow
            };

            return Results.Json(basicMetrics, jsonOptions);
        });

        // POST /api/diagnostics/validate
        app.MapPost(UiApiRoutes.DiagnosticsValidate, async (ConfigStore store, CancellationToken ct) =>
        {
            var cfg = store.Load();
            var dataRoot = store.GetDataRoot(cfg);

            // Run preflight checks
            var preflight = new PreflightChecker(new PreflightConfig
            {
                CheckProviderConnectivity = true
            });

            var result = await preflight.RunChecksAsync(dataRoot, ct);

            // Run config validation
            var configErrors = new List<string>();
            var configValid = ConfigValidationHelper.ValidateAndLog(cfg, configErrors);

            return Results.Json(new
            {
                preflight = new
                {
                    allPassed = result.AllChecksPassed,
                    hasWarnings = result.HasWarnings,
                    totalDurationMs = Math.Round(result.TotalDurationMs, 2),
                    checkedAt = result.CheckedAt,
                    checks = result.Checks.Select(c => new
                    {
                        name = c.Name,
                        status = c.Status.ToString().ToLowerInvariant(),
                        message = c.Message,
                        remediation = c.Remediation,
                        details = c.Details
                    })
                },
                configuration = new
                {
                    valid = configValid,
                    errors = configErrors
                },
                overallValid = result.AllChecksPassed && configValid,
                timestamp = DateTimeOffset.UtcNow
            }, jsonOptions);
        });

        // POST /api/diagnostics/providers/{providerName}/test
        app.MapPost(UiApiRoutes.DiagnosticsProviderTest, async (string providerName, CancellationToken ct) =>
        {
            var testResult = await TestProviderConnectivityAsync(providerName, ct);

            return Results.Json(testResult, jsonOptions);
        });

        // GET /api/diagnostics/quick-check
        app.MapGet(UiApiRoutes.DiagnosticsQuickCheck, (ConfigStore store) =>
        {
            var cfg = store.Load();
            var dataRoot = store.GetDataRoot(cfg);

            var checks = new List<object>();

            // Config file exists
            checks.Add(new
            {
                name = "Configuration File",
                passed = File.Exists(store.ConfigPath),
                message = File.Exists(store.ConfigPath)
                    ? $"Found at {store.ConfigPath}"
                    : $"Not found at {store.ConfigPath}"
            });

            // Data root exists and is writable
            var dataRootExists = Directory.Exists(dataRoot);
            checks.Add(new
            {
                name = "Data Directory",
                passed = dataRootExists,
                message = dataRootExists
                    ? $"Exists at {dataRoot}"
                    : $"Not found at {dataRoot}"
            });

            // Symbols configured
            var symbolCount = cfg.Symbols?.Length ?? 0;
            checks.Add(new
            {
                name = "Symbols",
                passed = symbolCount > 0,
                message = symbolCount > 0
                    ? $"{symbolCount} symbol(s) configured"
                    : "No symbols configured (default SPY will be used)"
            });

            // Data source configured
            checks.Add(new
            {
                name = "Data Source",
                passed = true,
                message = $"Active: {cfg.DataSource}"
            });

            // Memory check
            var gcInfo = GC.GetGCMemoryInfo();
            var availableMb = gcInfo.TotalAvailableMemoryBytes / (1024.0 * 1024.0);
            checks.Add(new
            {
                name = "Memory",
                passed = availableMb > 256,
                message = $"{availableMb:F0} MB available"
            });

            // Disk space check
            try
            {
                var root = Path.GetPathRoot(Path.GetFullPath(dataRoot)) ?? "/";
                var driveInfo = new DriveInfo(root);
                var freeGb = driveInfo.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0);
                checks.Add(new
                {
                    name = "Disk Space",
                    passed = freeGb > 1.0,
                    message = $"{freeGb:F1} GB free on {driveInfo.Name}"
                });
            }
            catch
            {
                checks.Add(new
                {
                    name = "Disk Space",
                    passed = true,
                    message = "Unable to determine disk space"
                });
            }

            var allPassed = checks.All(c => ((dynamic)c).passed);

            return Results.Json(new
            {
                allPassed,
                checks,
                timestamp = DateTimeOffset.UtcNow
            }, jsonOptions);
        });

        // GET /api/diagnostics/show-config
        app.MapGet(UiApiRoutes.DiagnosticsShowConfig, (ConfigStore store) =>
        {
            var cfg = store.Load();

            // Return sanitized configuration (credentials masked)
            var sanitized = new
            {
                dataRoot = cfg.DataRoot,
                compress = cfg.Compress,
                dataSource = cfg.DataSource.ToString(),
                alpaca = cfg.Alpaca != null
                    ? SensitiveValueMasker.MaskAlpacaOptions(
                        cfg.Alpaca.KeyId, cfg.Alpaca.SecretKey,
                        cfg.Alpaca.Feed, cfg.Alpaca.UseSandbox)
                    : null,
                polygon = cfg.Polygon != null
                    ? new
                    {
                        apiKey = SensitiveValueMasker.Mask(cfg.Polygon.ApiKey),
                        feed = cfg.Polygon.Feed
                    }
                    : (object?)null,
                ib = cfg.IB != null
                    ? new
                    {
                        host = cfg.IB.Host,
                        port = cfg.IB.Port,
                        clientId = cfg.IB.ClientId
                    }
                    : (object?)null,
                storage = cfg.Storage != null
                    ? new
                    {
                        namingConvention = cfg.Storage.NamingConvention,
                        datePartition = cfg.Storage.DatePartition,
                        includeProvider = cfg.Storage.IncludeProvider,
                        filePrefix = cfg.Storage.FilePrefix,
                        profile = cfg.Storage.Profile,
                        retentionDays = cfg.Storage.RetentionDays,
                        maxTotalMegabytes = cfg.Storage.MaxTotalMegabytes
                    }
                    : (object?)null,
                symbols = cfg.Symbols?.Select(s => new
                {
                    symbol = s.Symbol,
                    subscribeTrades = s.SubscribeTrades,
                    subscribeDepth = s.SubscribeDepth,
                    depthLevels = s.DepthLevels,
                    securityType = s.SecurityType,
                    exchange = s.Exchange
                }).ToArray(),
                backfill = cfg.Backfill != null
                    ? new
                    {
                        enabled = cfg.Backfill.Enabled,
                        provider = cfg.Backfill.Provider
                    }
                    : (object?)null,
                dataSources = cfg.DataSources != null
                    ? new
                    {
                        defaultRealTimeSourceId = cfg.DataSources.DefaultRealTimeSourceId,
                        defaultHistoricalSourceId = cfg.DataSources.DefaultHistoricalSourceId,
                        enableFailover = cfg.DataSources.EnableFailover,
                        failoverTimeoutSeconds = cfg.DataSources.FailoverTimeoutSeconds,
                        sourceCount = cfg.DataSources.Sources?.Length ?? 0
                    }
                    : (object?)null,
                configPath = store.ConfigPath,
                timestamp = DateTimeOffset.UtcNow
            };

            return Results.Json(sanitized, jsonOptions);
        });

        // GET /api/diagnostics/error-codes
        app.MapGet(UiApiRoutes.DiagnosticsErrorCodes, () =>
        {
            var codes = FriendlyErrorFormatter.GetAllErrorCodes()
                .Select(e => new
                {
                    code = e.Code,
                    title = e.Title,
                    suggestion = e.Suggestion
                })
                .ToArray();

            // Also include the ErrorCode enum values with categories
            var enumCodes = Enum.GetValues<ErrorCode>()
                .Select(ec => new
                {
                    value = (int)ec,
                    name = ec.ToString(),
                    category = ec.GetCategory(),
                    isTransient = ec.IsTransient(),
                    httpStatusCode = ec.ToHttpStatusCode()
                })
                .ToArray();

            return Results.Json(new
            {
                friendlyErrorCodes = codes,
                systemErrorCodes = enumCodes,
                timestamp = DateTimeOffset.UtcNow
            }, jsonOptions);
        });

        // POST /api/diagnostics/selftest
        app.MapPost(UiApiRoutes.DiagnosticsSelftest, async (ConfigStore store, CancellationToken ct) =>
        {
            var cfg = store.Load();
            var dataRoot = store.GetDataRoot(cfg);
            var results = new List<object>();
            var startTime = Stopwatch.GetTimestamp();

            // Test 1: Configuration loading
            results.Add(RunSelfTestCheck("Configuration Loading", () =>
            {
                var loadedCfg = store.Load();
                return loadedCfg.DataRoot != null;
            }));

            // Test 2: Config validation
            results.Add(RunSelfTestCheck("Configuration Validation", () =>
            {
                var errors = new List<string>();
                return ConfigValidationHelper.ValidateAndLog(cfg, errors);
            }));

            // Test 3: Data directory access
            results.Add(RunSelfTestCheck("Data Directory Access", () =>
            {
                if (!Directory.Exists(dataRoot))
                {
                    Directory.CreateDirectory(dataRoot);
                }

                var testFile = Path.Combine(dataRoot, $".selftest_{Guid.NewGuid():N}");
                File.WriteAllText(testFile, "selftest");
                var content = File.ReadAllText(testFile);
                File.Delete(testFile);
                return content == "selftest";
            }));

            // Test 4: Memory allocation
            results.Add(RunSelfTestCheck("Memory Allocation", () =>
            {
                var buffer = new byte[1024 * 1024]; // 1MB
                Array.Fill<byte>(buffer, 42);
                return buffer[0] == 42;
            }));

            // Test 5: System time
            results.Add(RunSelfTestCheck("System Time", () =>
            {
                var utcNow = DateTimeOffset.UtcNow;
                return utcNow.Year >= 2020;
            }));

            // Test 6: DNS resolution
            results.Add(await RunSelfTestCheckAsync("DNS Resolution", async () =>
            {
                var addresses = await System.Net.Dns.GetHostAddressesAsync("api.alpaca.markets", ct);
                return addresses.Length > 0;
            }));

            var elapsed = (double)(Stopwatch.GetTimestamp() - startTime) / Stopwatch.Frequency * 1000;
            var allPassed = results.All(r => ((dynamic)r).passed);

            return Results.Json(new
            {
                allPassed,
                tests = results,
                totalDurationMs = Math.Round(elapsed, 2),
                timestamp = DateTimeOffset.UtcNow
            }, jsonOptions);
        });

        // POST /api/diagnostics/validate-credentials
        app.MapPost(UiApiRoutes.DiagnosticsValidateCredentials, (ConfigStore store) =>
        {
            var cfg = store.Load();
            var credentials = new List<object>();

            // Check Alpaca credentials
            var alpacaKeyId = cfg.Alpaca?.KeyId ?? Environment.GetEnvironmentVariable("ALPACA_KEY_ID")
                              ?? Environment.GetEnvironmentVariable("ALPACA__KEYID");
            var alpacaSecretKey = cfg.Alpaca?.SecretKey ?? Environment.GetEnvironmentVariable("ALPACA_SECRET_KEY")
                                 ?? Environment.GetEnvironmentVariable("ALPACA__SECRETKEY");
            credentials.Add(new
            {
                provider = "Alpaca",
                hasKeyId = !string.IsNullOrWhiteSpace(alpacaKeyId),
                hasSecretKey = !string.IsNullOrWhiteSpace(alpacaSecretKey),
                keyIdMasked = SensitiveValueMasker.Mask(alpacaKeyId),
                source = DetermineCredentialSource("Alpaca", cfg.Alpaca?.KeyId,
                    "ALPACA_KEY_ID", "ALPACA__KEYID")
            });

            // Check Polygon credentials
            var polygonKey = cfg.Polygon?.ApiKey ?? Environment.GetEnvironmentVariable("POLYGON_API_KEY")
                             ?? Environment.GetEnvironmentVariable("POLYGON__APIKEY");
            credentials.Add(new
            {
                provider = "Polygon",
                hasApiKey = !string.IsNullOrWhiteSpace(polygonKey),
                apiKeyMasked = SensitiveValueMasker.Mask(polygonKey),
                source = DetermineCredentialSource("Polygon", cfg.Polygon?.ApiKey,
                    "POLYGON_API_KEY", "POLYGON__APIKEY")
            });

            // Check Finnhub credentials
            var finnhubToken = Environment.GetEnvironmentVariable("FINNHUB_API_KEY")
                               ?? Environment.GetEnvironmentVariable("FINNHUB__TOKEN");
            credentials.Add(new
            {
                provider = "Finnhub",
                hasToken = !string.IsNullOrWhiteSpace(finnhubToken),
                tokenMasked = SensitiveValueMasker.Mask(finnhubToken),
                source = !string.IsNullOrWhiteSpace(finnhubToken) ? "environment" : "not configured"
            });

            // Check Tiingo credentials
            var tiingoToken = Environment.GetEnvironmentVariable("TIINGO__TOKEN")
                              ?? Environment.GetEnvironmentVariable("TIINGO_TOKEN");
            credentials.Add(new
            {
                provider = "Tiingo",
                hasToken = !string.IsNullOrWhiteSpace(tiingoToken),
                tokenMasked = SensitiveValueMasker.Mask(tiingoToken),
                source = !string.IsNullOrWhiteSpace(tiingoToken) ? "environment" : "not configured"
            });

            // Check Alpha Vantage credentials
            var alphaVantageKey = Environment.GetEnvironmentVariable("ALPHAVANTAGE__APIKEY")
                                 ?? Environment.GetEnvironmentVariable("ALPHA_VANTAGE_API_KEY");
            credentials.Add(new
            {
                provider = "AlphaVantage",
                hasApiKey = !string.IsNullOrWhiteSpace(alphaVantageKey),
                apiKeyMasked = SensitiveValueMasker.Mask(alphaVantageKey),
                source = !string.IsNullOrWhiteSpace(alphaVantageKey) ? "environment" : "not configured"
            });

            return Results.Json(new
            {
                credentials,
                timestamp = DateTimeOffset.UtcNow
            }, jsonOptions);
        });

        // POST /api/diagnostics/test-connectivity
        app.MapPost(UiApiRoutes.DiagnosticsTestConnectivity, async (CancellationToken ct) =>
        {
            var endpoints = new (string Name, string Host, int Port)[]
            {
                ("DNS (Google)", "8.8.8.8", 53),
                ("Alpaca REST API", "api.alpaca.markets", 443),
                ("Alpaca Streaming", "stream.data.alpaca.markets", 443),
                ("Polygon API", "api.polygon.io", 443),
                ("Finnhub API", "finnhub.io", 443),
                ("Stooq", "stooq.com", 443),
                ("Yahoo Finance", "query1.finance.yahoo.com", 443)
            };

            var results = new List<object>();

            foreach (var (name, host, port) in endpoints)
            {
                if (ct.IsCancellationRequested) break;

                var sw = Stopwatch.StartNew();
                bool success;
                string? error = null;

                try
                {
                    using var client = new System.Net.Sockets.TcpClient();
                    var connectTask = client.ConnectAsync(host, port);
                    var completed = await Task.WhenAny(connectTask, Task.Delay(5000, ct));

                    if (completed == connectTask && !connectTask.IsFaulted)
                    {
                        success = true;
                    }
                    else
                    {
                        success = false;
                        error = connectTask.IsFaulted
                            ? connectTask.Exception?.InnerException?.Message ?? "Connection failed"
                            : "Connection timeout";
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    success = false;
                    error = ex.Message;
                }

                sw.Stop();

                results.Add(new
                {
                    name,
                    host,
                    port,
                    reachable = success,
                    latencyMs = Math.Round(sw.Elapsed.TotalMilliseconds, 2),
                    error
                });
            }

            var reachableCount = results.Count(r => ((dynamic)r).reachable);

            return Results.Json(new
            {
                reachableCount,
                totalCount = results.Count,
                allReachable = reachableCount == results.Count,
                endpoints = results,
                timestamp = DateTimeOffset.UtcNow
            }, jsonOptions);
        });

        // POST /api/diagnostics/validate-config
        app.MapPost(UiApiRoutes.DiagnosticsValidateConfig, (ConfigStore store) =>
        {
            var cfg = store.Load();
            var errors = new List<string>();
            var isValid = ConfigValidationHelper.ValidateAndLog(cfg, errors);

            // Additional semantic checks
            var warnings = new List<string>();

            if (cfg.Symbols == null || cfg.Symbols.Length == 0)
            {
                warnings.Add("No symbols configured. Default symbol SPY will be used.");
            }

            if (cfg.Storage?.RetentionDays == null)
            {
                warnings.Add("No retention policy configured. Data will be kept indefinitely.");
            }

            if (cfg.Compress != true && cfg.Storage?.Profile == null)
            {
                warnings.Add("Compression is not explicitly enabled. Consider enabling for storage efficiency.");
            }

            if (cfg.DataSource == DataSourceKind.Alpaca &&
                string.IsNullOrWhiteSpace(cfg.Alpaca?.KeyId) &&
                string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ALPACA_KEY_ID")))
            {
                errors.Add("Alpaca is selected as data source but no API credentials are configured.");
            }

            return Results.Json(new
            {
                valid = isValid && errors.Count == 0,
                errors,
                warnings,
                configPath = store.ConfigPath,
                dataSource = cfg.DataSource.ToString(),
                symbolCount = cfg.Symbols?.Length ?? 0,
                timestamp = DateTimeOffset.UtcNow
            }, jsonOptions);
        });
    }

    #region Helper Methods

    private static object? FormatValidationSection(ValidationSection? section)
    {
        if (section == null) return null;

        return new
        {
            name = section.Name,
            success = section.Success,
            checks = section.Checks.Select(c => new
            {
                name = c.Name,
                passed = c.Passed,
                message = c.Message
            }),
            warnings = section.Warnings,
            errors = section.Errors
        };
    }

    private static object GetStorageDiagnostics(string dataRoot, StorageConfig? storageConfig)
    {
        var fullPath = Path.GetFullPath(dataRoot);
        var exists = Directory.Exists(fullPath);

        long totalSizeBytes = 0;
        int fileCount = 0;
        int directoryCount = 0;
        var subdirectories = Array.Empty<object>();

        if (exists)
        {
            try
            {
                var dirInfo = new DirectoryInfo(fullPath);
                var files = dirInfo.GetFiles("*", SearchOption.AllDirectories);
                totalSizeBytes = files.Sum(f => f.Length);
                fileCount = files.Length;
                directoryCount = dirInfo.GetDirectories("*", SearchOption.AllDirectories).Length;

                // Top-level subdirectory summary
                subdirectories = dirInfo.GetDirectories()
                    .Select(d =>
                    {
                        long dirSize;
                        int dirFiles;
                        try
                        {
                            var subFiles = d.GetFiles("*", SearchOption.AllDirectories);
                            dirSize = subFiles.Sum(f => f.Length);
                            dirFiles = subFiles.Length;
                        }
                        catch
                        {
                            dirSize = 0;
                            dirFiles = 0;
                        }

                        return (object)new
                        {
                            name = d.Name,
                            sizeMb = Math.Round(dirSize / (1024.0 * 1024.0), 2),
                            fileCount = dirFiles
                        };
                    })
                    .ToArray();
            }
            catch
            {
                // Access denied or other error
            }
        }

        // Disk space info
        double freeGb = 0;
        double totalGb = 0;
        string driveName = "";
        try
        {
            var root = Path.GetPathRoot(fullPath) ?? "/";
            var driveInfo = new DriveInfo(root);
            freeGb = driveInfo.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0);
            totalGb = driveInfo.TotalSize / (1024.0 * 1024.0 * 1024.0);
            driveName = driveInfo.Name;
        }
        catch
        {
            // Disk info not available on all systems
        }

        return new
        {
            dataRoot = fullPath,
            exists,
            totalSizeMb = Math.Round(totalSizeBytes / (1024.0 * 1024.0), 2),
            fileCount,
            directoryCount,
            subdirectories,
            disk = new
            {
                drive = driveName,
                freeGb = Math.Round(freeGb, 2),
                totalGb = Math.Round(totalGb, 2),
                usedPercent = totalGb > 0 ? Math.Round((totalGb - freeGb) / totalGb * 100, 1) : 0
            },
            configuration = new
            {
                namingConvention = storageConfig?.NamingConvention ?? "BySymbol",
                datePartition = storageConfig?.DatePartition ?? "Daily",
                includeProvider = storageConfig?.IncludeProvider ?? false,
                profile = storageConfig?.Profile,
                retentionDays = storageConfig?.RetentionDays,
                maxTotalMegabytes = storageConfig?.MaxTotalMegabytes
            },
            timestamp = DateTimeOffset.UtcNow
        };
    }

    private static bool HasCredentialsForProvider(DataSourceKind provider, AppConfig cfg)
    {
        return provider switch
        {
            DataSourceKind.Alpaca =>
                !string.IsNullOrWhiteSpace(cfg.Alpaca?.KeyId) ||
                !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ALPACA_KEY_ID")) ||
                !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ALPACA__KEYID")),

            DataSourceKind.Polygon =>
                !string.IsNullOrWhiteSpace(cfg.Polygon?.ApiKey) ||
                !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("POLYGON_API_KEY")) ||
                !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("POLYGON__APIKEY")),

            DataSourceKind.IB => true, // IB uses TWS/Gateway, no API key needed in config

            _ => false
        };
    }

    private static string DetermineCredentialSource(string provider, string? configValue, params string[] envVarNames)
    {
        if (!string.IsNullOrWhiteSpace(configValue))
            return "configuration";

        foreach (var envVar in envVarNames)
        {
            if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(envVar)))
                return $"environment ({envVar})";
        }

        return "not configured";
    }

    private static async Task<object> TestProviderConnectivityAsync(string providerName, CancellationToken ct)
    {
        var endpoints = providerName.ToLowerInvariant() switch
        {
            "alpaca" => new[]
            {
                ("Alpaca REST API", "https://api.alpaca.markets"),
                ("Alpaca Data API", "https://data.alpaca.markets"),
                ("Alpaca Stream", "https://stream.data.alpaca.markets")
            },
            "polygon" => new[]
            {
                ("Polygon API", "https://api.polygon.io")
            },
            "finnhub" => new[]
            {
                ("Finnhub API", "https://finnhub.io/api/v1")
            },
            "tiingo" => new[]
            {
                ("Tiingo API", "https://api.tiingo.com")
            },
            "yahoo" => new[]
            {
                ("Yahoo Finance", "https://query1.finance.yahoo.com")
            },
            "stooq" => new[]
            {
                ("Stooq", "https://stooq.com")
            },
            "alphavantage" or "alpha_vantage" => new[]
            {
                ("Alpha Vantage", "https://www.alphavantage.co")
            },
            "ib" or "interactivebrokers" => new[]
            {
                ("IB Gateway", "https://www.interactivebrokers.com")
            },
            _ => Array.Empty<(string, string)>()
        };

        if (endpoints.Length == 0)
        {
            return new
            {
                provider = providerName,
                error = $"Unknown provider '{providerName}'",
                knownProviders = new[] { "alpaca", "polygon", "finnhub", "tiingo", "yahoo", "stooq", "alphavantage", "ib" },
                timestamp = DateTimeOffset.UtcNow
            };
        }

        var results = new List<object>();

        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

        foreach (var (name, url) in endpoints)
        {
            if (ct.IsCancellationRequested) break;

            var sw = Stopwatch.StartNew();
            bool reachable;
            int? statusCode = null;
            string? error = null;

            try
            {
                using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
                statusCode = (int)response.StatusCode;
                // Any response (even 401/403) means the endpoint is reachable
                reachable = true;
            }
            catch (TaskCanceledException)
            {
                reachable = false;
                error = "Timeout";
            }
            catch (HttpRequestException ex)
            {
                reachable = false;
                error = ex.Message;
            }
            catch (Exception ex)
            {
                reachable = false;
                error = ex.Message;
            }

            sw.Stop();

            results.Add(new
            {
                name,
                url,
                reachable,
                statusCode,
                latencyMs = Math.Round(sw.Elapsed.TotalMilliseconds, 2),
                error
            });
        }

        var allReachable = results.All(r => ((dynamic)r).reachable);

        return new
        {
            provider = providerName,
            allReachable,
            endpoints = results,
            timestamp = DateTimeOffset.UtcNow
        };
    }

    private static object RunSelfTestCheck(string name, Func<bool> test)
    {
        var sw = Stopwatch.StartNew();
        bool passed;
        string? error = null;

        try
        {
            passed = test();
        }
        catch (Exception ex)
        {
            passed = false;
            error = ex.Message;
        }

        sw.Stop();

        return new
        {
            name,
            passed,
            durationMs = Math.Round(sw.Elapsed.TotalMilliseconds, 2),
            error
        };
    }

    private static async Task<object> RunSelfTestCheckAsync(string name, Func<Task<bool>> test)
    {
        var sw = Stopwatch.StartNew();
        bool passed;
        string? error = null;

        try
        {
            passed = await test();
        }
        catch (Exception ex)
        {
            passed = false;
            error = ex.Message;
        }

        sw.Stop();

        return new
        {
            name,
            passed,
            durationMs = Math.Round(sw.Elapsed.TotalMilliseconds, 2),
            error
        };
    }

    #endregion
}

/// <summary>
/// Request body for the dry-run endpoint, allowing callers to select which validations to run.
/// </summary>
internal sealed record DryRunRequestBody(
    bool ValidateConfiguration = true,
    bool ValidateFileSystem = true,
    bool ValidateConnectivity = true,
    bool ValidateProviders = true,
    bool ValidateSymbols = true,
    bool ValidateResources = true
);
