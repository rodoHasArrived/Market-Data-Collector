using System.Text.Json;
using System.Threading.Channels;
using MarketDataCollector.Application.Backfill;
using MarketDataCollector.Application.Config;
using MarketDataCollector.Application.Exceptions;
using MarketDataCollector.Application.Logging;
using MarketDataCollector.Application.Monitoring;
using MarketDataCollector.Application.Subscriptions;
using MarketDataCollector.Application.Pipeline;
using MarketDataCollector.Application.Services;
using MarketDataCollector.Application.Subscriptions.Services;
using MarketDataCollector.Application.Testing;
using MarketDataCollector.Application.UI;
using MarketDataCollector.Contracts.Domain.Models;
using MarketDataCollector.Domain.Collectors;
using MarketDataCollector.Domain.Events;
using MarketDataCollector.Domain.Models;
using MarketDataCollector.Infrastructure;
using MarketDataCollector.Infrastructure.Http;
using MarketDataCollector.Infrastructure.Providers.InteractiveBrokers;
using MarketDataCollector.Infrastructure.Providers.Alpaca;
using MarketDataCollector.Infrastructure.Providers.Polygon;
using MarketDataCollector.Infrastructure.Providers.StockSharp;
using MarketDataCollector.Infrastructure.Providers.Backfill;
using SymbolResolution = MarketDataCollector.Infrastructure.Providers.Backfill.SymbolResolution;
using BackfillRequest = MarketDataCollector.Application.Backfill.BackfillRequest;
using MarketDataCollector.Storage;
using MarketDataCollector.Storage.Packaging;
using MarketDataCollector.Storage.Policies;
using MarketDataCollector.Storage.Services;
using MarketDataCollector.Storage.Sinks;
using MarketDataCollector.Storage.Replay;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace MarketDataCollector;

internal static class Program
{
    private const string DefaultConfigFileName = "appsettings.json";
    private const string ConfigPathEnvVar = "MDC_CONFIG_PATH";

    public static async Task Main(string[] args)
    {
        // Initialize logging early - use minimal config load just for DataRoot
        var cfgPath = ResolveConfigPath(args);
        var initialCfg = LoadConfigMinimal(cfgPath);
        LoggingSetup.Initialize(dataRoot: initialCfg.DataRoot);
        var log = LoggingSetup.ForContext("Program");

        // Now use ConfigurationService for full config processing (with self-healing, credential resolution, etc.)
        await using var configService = new ConfigurationService(log);
        var cfg = configService.LoadAndPrepareConfig(cfgPath);

        try
        {
            await RunAsync(args, cfg, cfgPath, log, configService);
        }
        catch (Exception ex)
        {
            log.Fatal(ex, "MarketDataCollector terminated unexpectedly");
            throw;
        }
        finally
        {
            LoggingSetup.CloseAndFlush();
        }
    }

    private static async Task RunAsync(string[] args, AppConfig cfg, string cfgPath, ILogger log, ConfigurationService configService)
    {
        // Initialize HttpClientFactory for proper HTTP client lifecycle management (TD-10)
        InitializeHttpClientFactory(log);
        var runMode = ResolveRunMode(args, log);

        // Help Mode - Display usage information
        if (args.Any(a => a.Equals("--help", StringComparison.OrdinalIgnoreCase) || a.Equals("-h", StringComparison.OrdinalIgnoreCase)))
        {
            ShowHelp();
            return;
        }

        // Wizard Mode - Interactive configuration wizard
        if (args.Any(a => a.Equals("--wizard", StringComparison.OrdinalIgnoreCase)))
        {
            log.Information("Starting configuration wizard...");
            using var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

            var result = await configService.RunWizardAsync(cts.Token);
            Environment.Exit(result.Success ? 0 : 1);
            return;
        }

        // Auto-Config Mode - Automatic configuration based on environment
        if (args.Any(a => a.Equals("--auto-config", StringComparison.OrdinalIgnoreCase)))
        {
            log.Information("Running auto-configuration...");
            var result = configService.RunAutoConfig();
            Environment.Exit(result.Success ? 0 : 1);
            return;
        }

        // Detect Providers Mode - Show available providers (routed through ConfigurationService)
        if (args.Any(a => a.Equals("--detect-providers", StringComparison.OrdinalIgnoreCase)))
        {
            configService.PrintProviderDetection();
            return;
        }

        // Validate Credentials Mode - Test API credentials (routed through ConfigurationService)
        if (args.Any(a => a.Equals("--validate-credentials", StringComparison.OrdinalIgnoreCase)))
        {
            log.Information("Validating API credentials...");

            var validationResult = await configService.ValidateCredentialsAsync(cfg);
            await using var validationService = new CredentialValidationService();
            validationService.PrintSummary(validationResult);

            Environment.Exit(validationResult.AllValid ? 0 : 1);
            return;
        }

        // Generate Config Mode - Generate configuration template
        if (args.Any(a => a.Equals("--generate-config", StringComparison.OrdinalIgnoreCase)))
        {
            var templateName = GetArgValue(args, "--template") ?? "minimal";
            var outputPath = GetArgValue(args, "--output") ?? "config/appsettings.generated.json";

            var generator = new ConfigTemplateGenerator();
            var template = generator.GetTemplate(templateName);

            if (template == null)
            {
                Console.Error.WriteLine($"Unknown template: {templateName}");
                Console.Error.WriteLine("Available templates: minimal, full, alpaca, stocksharp, backfill, production, docker");
                Environment.Exit(1);
                return;
            }

            // Ensure directory exists
            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(outputPath, template.Json);
            Console.WriteLine($"Generated {template.Name} configuration template: {outputPath}");

            if (template.EnvironmentVariables?.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine("Required environment variables:");
                foreach (var (key, desc) in template.EnvironmentVariables)
                {
                    Console.WriteLine($"  {key}: {desc}");
                }
            }
            return;
        }

        // Quick Check Mode - Fast health diagnostics (routed through ConfigurationService)
        if (args.Any(a => a.Equals("--quick-check", StringComparison.OrdinalIgnoreCase)))
        {
            log.Information("Running quick configuration check...");

            var result = configService.PerformQuickCheck(cfg);
            var summary = new StartupSummary();
            summary.DisplayQuickCheck(result);

            Environment.Exit(result.Success ? 0 : 1);
            return;
        }

        // Test Connectivity Mode - Test provider connections (routed through ConfigurationService)
        if (args.Any(a => a.Equals("--test-connectivity", StringComparison.OrdinalIgnoreCase)))
        {
            log.Information("Testing provider connectivity...");

            var result = await configService.TestConnectivityAsync(cfg);
            await using var tester = new ConnectivityTestService();
            tester.DisplaySummary(result);

            Environment.Exit(result.AllReachable ? 0 : 1);
            return;
        }

        // Show Error Codes Mode - Display error code reference
        if (args.Any(a => a.Equals("--error-codes", StringComparison.OrdinalIgnoreCase)))
        {
            FriendlyErrorFormatter.DisplayErrorCodeReference();
            return;
        }

        // Check Schemas Mode - Verify stored data schema compatibility
        if (args.Any(a => a.Equals("--check-schemas", StringComparison.OrdinalIgnoreCase)))
        {
            log.Information("Checking stored data schema compatibility...");

            var schemaOptions = new SchemaValidationOptions
            {
                EnableVersionTracking = true,
                MaxFilesToCheck = int.TryParse(GetArgValue(args, "--max-files"), out var maxFiles) ? maxFiles : 100,
                FailOnFirstIncompatibility = args.Any(a => a.Equals("--fail-fast", StringComparison.OrdinalIgnoreCase))
            };

            await using var schemaService = new SchemaValidationService(schemaOptions, cfg.DataRoot);
            var result = await schemaService.PerformStartupCheckAsync();

            Console.WriteLine();
            if (result.Success)
            {
                Console.WriteLine("Schema Compatibility Check: PASSED");
                Console.WriteLine(new string('=', 50));
                Console.WriteLine($"  {result.Message}");
                Console.WriteLine($"  Current schema version: {SchemaValidationService.CurrentSchemaVersion}");
            }
            else
            {
                Console.WriteLine("Schema Compatibility Check: ISSUES FOUND");
                Console.WriteLine(new string('=', 50));
                Console.WriteLine($"  {result.Message}");
                Console.WriteLine();
                Console.WriteLine("  Incompatible files:");
                foreach (var incompat in result.Incompatibilities.Take(10))
                {
                    var migratable = incompat.CanMigrate ? " (can migrate)" : "";
                    Console.WriteLine($"    - {incompat.FilePath}");
                    Console.WriteLine($"      Version: {incompat.DetectedVersion} (expected {incompat.ExpectedVersion}){migratable}");
                }
                if (result.Incompatibilities.Length > 10)
                {
                    Console.WriteLine($"    ... and {result.Incompatibilities.Length - 10} more");
                }
            }
            Console.WriteLine();

            Environment.Exit(result.Success ? 0 : 1);
            return;
        }

        // Show Summary Mode - Display configuration summary (routed through ConfigurationService)
        if (args.Any(a => a.Equals("--show-config", StringComparison.OrdinalIgnoreCase)))
        {
            configService.DisplayConfigSummary(cfg, cfgPath, args);
            return;
        }

        // Symbol Management Commands
        var symbolManagementService = new SymbolManagementService(
            new ConfigStore(cfgPath),
            cfg.DataRoot,
            log
        );

        // List all symbols (monitored + archived)
        if (args.Any(a => a.Equals("--symbols", StringComparison.OrdinalIgnoreCase)))
        {
            await symbolManagementService.DisplayAllSymbolsAsync();
            return;
        }

        // List monitored symbols only
        if (args.Any(a => a.Equals("--symbols-monitored", StringComparison.OrdinalIgnoreCase)))
        {
            var result = symbolManagementService.GetMonitoredSymbols();
            symbolManagementService.DisplayMonitoredSymbols(result);
            return;
        }

        // List archived symbols only
        if (args.Any(a => a.Equals("--symbols-archived", StringComparison.OrdinalIgnoreCase)))
        {
            var result = await symbolManagementService.GetArchivedSymbolsAsync();
            symbolManagementService.DisplayArchivedSymbols(result);
            return;
        }

        // Add symbols
        if (args.Any(a => a.Equals("--symbols-add", StringComparison.OrdinalIgnoreCase)))
        {
            var symbolsArg = GetArgValue(args, "--symbols-add");
            if (string.IsNullOrWhiteSpace(symbolsArg))
            {
                Console.Error.WriteLine("Error: --symbols-add requires a comma-separated list of symbols");
                Console.Error.WriteLine("Example: --symbols-add AAPL,MSFT,GOOGL");
                Environment.Exit(1);
                return;
            }

            var symbolsToAdd = symbolsArg.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var options = new SymbolAddOptions(
                SubscribeTrades: !args.Any(a => a.Equals("--no-trades", StringComparison.OrdinalIgnoreCase)),
                SubscribeDepth: !args.Any(a => a.Equals("--no-depth", StringComparison.OrdinalIgnoreCase)),
                DepthLevels: int.TryParse(GetArgValue(args, "--depth-levels"), out var levels) ? levels : 10,
                UpdateExisting: args.Any(a => a.Equals("--update", StringComparison.OrdinalIgnoreCase))
            );

            var result = await symbolManagementService.AddSymbolsAsync(symbolsToAdd, options);
            Console.WriteLine();
            Console.WriteLine(result.Success ? "Symbol Addition Result" : "Symbol Addition Failed");
            Console.WriteLine(new string('=', 50));
            Console.WriteLine($"  {result.Message}");
            if (result.AffectedSymbols.Length > 0)
            {
                Console.WriteLine($"  Symbols: {string.Join(", ", result.AffectedSymbols)}");
            }
            Console.WriteLine();

            Environment.Exit(result.Success ? 0 : 1);
            return;
        }

        // Remove symbols
        if (args.Any(a => a.Equals("--symbols-remove", StringComparison.OrdinalIgnoreCase)))
        {
            var symbolsArg = GetArgValue(args, "--symbols-remove");
            if (string.IsNullOrWhiteSpace(symbolsArg))
            {
                Console.Error.WriteLine("Error: --symbols-remove requires a comma-separated list of symbols");
                Console.Error.WriteLine("Example: --symbols-remove AAPL,MSFT");
                Environment.Exit(1);
                return;
            }

            var symbolsToRemove = symbolsArg.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var result = await symbolManagementService.RemoveSymbolsAsync(symbolsToRemove);

            Console.WriteLine();
            Console.WriteLine(result.Success ? "Symbol Removal Result" : "Symbol Removal Failed");
            Console.WriteLine(new string('=', 50));
            Console.WriteLine($"  {result.Message}");
            if (result.AffectedSymbols.Length > 0)
            {
                Console.WriteLine($"  Removed: {string.Join(", ", result.AffectedSymbols)}");
            }
            Console.WriteLine();

            Environment.Exit(result.Success ? 0 : 1);
            return;
        }

        // Check status of a specific symbol
        if (args.Any(a => a.Equals("--symbol-status", StringComparison.OrdinalIgnoreCase)))
        {
            var symbolArg = GetArgValue(args, "--symbol-status");
            if (string.IsNullOrWhiteSpace(symbolArg))
            {
                Console.Error.WriteLine("Error: --symbol-status requires a symbol");
                Console.Error.WriteLine("Example: --symbol-status AAPL");
                Environment.Exit(1);
                return;
            }

            var status = await symbolManagementService.GetSymbolStatusAsync(symbolArg);

            Console.WriteLine();
            Console.WriteLine($"Symbol Status: {status.Symbol}");
            Console.WriteLine(new string('=', 50));
            Console.WriteLine($"  Monitored: {(status.IsMonitored ? "Yes" : "No")}");
            Console.WriteLine($"  Has Archived Data: {(status.HasArchivedData ? "Yes" : "No")}");

            if (status.MonitoredConfig != null)
            {
                Console.WriteLine();
                Console.WriteLine("  Monitoring Configuration:");
                Console.WriteLine($"    Subscribe Trades: {status.MonitoredConfig.SubscribeTrades}");
                Console.WriteLine($"    Subscribe Depth: {status.MonitoredConfig.SubscribeDepth}");
                Console.WriteLine($"    Depth Levels: {status.MonitoredConfig.DepthLevels}");
                Console.WriteLine($"    Security Type: {status.MonitoredConfig.SecurityType}");
                Console.WriteLine($"    Exchange: {status.MonitoredConfig.Exchange}");
            }

            if (status.ArchivedInfo != null)
            {
                Console.WriteLine();
                Console.WriteLine("  Archived Data:");
                Console.WriteLine($"    Files: {status.ArchivedInfo.FileCount}");
                Console.WriteLine($"    Size: {FormatBytes(status.ArchivedInfo.TotalSizeBytes)}");
                if (status.ArchivedInfo.OldestData.HasValue && status.ArchivedInfo.NewestData.HasValue)
                {
                    Console.WriteLine($"    Date Range: {status.ArchivedInfo.OldestData:yyyy-MM-dd} to {status.ArchivedInfo.NewestData:yyyy-MM-dd}");
                }
                if (status.ArchivedInfo.DataTypes.Length > 0)
                {
                    Console.WriteLine($"    Data Types: {string.Join(", ", status.ArchivedInfo.DataTypes)}");
                }
            }

            Console.WriteLine();
            return;
        }

        // Validate Config Mode - Check configuration without starting
        if (args.Any(a => a.Equals("--validate-config", StringComparison.OrdinalIgnoreCase)))
        {
            var configPathArg = GetArgValue(args, "--config") ?? cfgPath;
            var exitCode = configService.ValidateConfig(configPathArg);
            Environment.Exit(exitCode);
            return;
        }

        // Dry Run Mode - Validate everything without starting (QW-93) (routed through ConfigurationService)
        if (args.Any(a => a.Equals("--dry-run", StringComparison.OrdinalIgnoreCase)))
        {
            log.Information("Running in dry-run mode...");
            Application.Services.DryRunService.EnableDryRunMode();

            var options = new Application.Services.DryRunOptions(
                ValidateConfiguration: true,
                ValidateFileSystem: true,
                ValidateConnectivity: !args.Any(a => a.Equals("--offline", StringComparison.OrdinalIgnoreCase)),
                ValidateProviders: true,
                ValidateSymbols: true,
                ValidateResources: true
            );

            var result = await configService.DryRunValidationAsync(cfg, options);
            var dryRunService = new Application.Services.DryRunService();
            var report = dryRunService.GenerateReport(result);
            Console.WriteLine(report);

            Environment.Exit(result.OverallSuccess ? 0 : 1);
            return;
        }

        // UI Mode - Start web dashboard (handles both --mode web and legacy --ui flag)
        if (runMode == CliModeResolver.RunMode.Web)
        {
            var uiPort = int.TryParse(GetArgValue(args, "--http-port"), out var parsedUiPort) ? parsedUiPort : 8080;
            log.Information("Starting web dashboard on port {Port}...", uiPort);

            await using var uiServer = new UiServer(cfgPath, uiPort);
            await uiServer.StartAsync();

            log.Information("Web dashboard started at http://localhost:{Port}", uiPort);
            Console.WriteLine($"Web dashboard running at http://localhost:{uiPort}");
            Console.WriteLine("Press Ctrl+C to stop...");

            var done = new TaskCompletionSource();
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                log.Information("Shutdown requested");
                done.TrySetResult();
            };
            await done.Task;

            log.Information("Stopping web dashboard...");
            await uiServer.StopAsync();
            log.Information("Web dashboard stopped");
            return;
        }

        if (args.Any(a => a.Equals("--selftest", StringComparison.OrdinalIgnoreCase)))
        {
            log.Information("Running self-tests...");
            DepthBufferSelfTests.Run();
            log.Information("Self-tests passed");
            Console.WriteLine("Self-tests passed.");
            return;
        }

        // Package Mode - Create a portable data package
        if (args.Any(a => a.Equals("--package", StringComparison.OrdinalIgnoreCase)))
        {
            await RunPackageCommandAsync(cfg, args, log);
            return;
        }

        // Import Package Mode - Import a package into storage
        if (args.Any(a => a.Equals("--import-package", StringComparison.OrdinalIgnoreCase)))
        {
            var packagePath = GetArgValue(args, "--import-package");
            if (string.IsNullOrWhiteSpace(packagePath))
            {
                Console.Error.WriteLine("Error: --import-package requires a path to the package file");
                Environment.Exit(1);
                return;
            }

            await RunImportPackageCommandAsync(cfg, packagePath, args, log);
            return;
        }

        // List Package Contents Mode
        if (args.Any(a => a.Equals("--list-package", StringComparison.OrdinalIgnoreCase)))
        {
            var packagePath = GetArgValue(args, "--list-package");
            if (string.IsNullOrWhiteSpace(packagePath))
            {
                Console.Error.WriteLine("Error: --list-package requires a path to the package file");
                Environment.Exit(1);
                return;
            }

            await RunListPackageCommandAsync(packagePath, log);
            return;
        }

        // Validate Package Mode
        if (args.Any(a => a.Equals("--validate-package", StringComparison.OrdinalIgnoreCase)))
        {
            var packagePath = GetArgValue(args, "--validate-package");
            if (string.IsNullOrWhiteSpace(packagePath))
            {
                Console.Error.WriteLine("Error: --validate-package requires a path to the package file");
                Environment.Exit(1);
                return;
            }

            await RunValidatePackageCommandAsync(packagePath, log);
            return;
        }

        // Validate configuration (routed through ConfigurationService)
        if (!configService.ValidateConfig(cfg, out _))
        {
            log.Error("Exiting due to configuration errors");
            Environment.Exit(1);
            return;
        }

        // Ensure data directory exists with proper permissions
        var permissionsService = new FilePermissionsService(new FilePermissionsOptions
        {
            DirectoryMode = "755",
            FileMode = "644",
            ValidateOnStartup = true
        });

        var permissionsResult = permissionsService.EnsureDirectoryPermissions(cfg.DataRoot);
        if (!permissionsResult.Success)
        {
            log.Error("Failed to configure data directory permissions: {Message}. " +
                "Troubleshooting: 1) Check that the application has write access to the parent directory. " +
                "2) On Linux/macOS, ensure the user has appropriate permissions. " +
                "3) On Windows, run as administrator if needed.",
                permissionsResult.Message);
            Environment.Exit(1);
            return;
        }
        log.Information("Data directory permissions configured: {Message}", permissionsResult.Message);

        // Optional startup schema compatibility check
        if (args.Any(a => a.Equals("--validate-schemas", StringComparison.OrdinalIgnoreCase)))
        {
            log.Information("Running startup schema compatibility check...");
            await using var schemaService = new SchemaValidationService(
                new SchemaValidationOptions { EnableVersionTracking = true },
                cfg.DataRoot);

            var schemaCheckResult = await schemaService.PerformStartupCheckAsync();
            if (!schemaCheckResult.Success)
            {
                log.Warning("Schema compatibility check found issues: {Message}", schemaCheckResult.Message);
                if (args.Any(a => a.Equals("--strict-schemas", StringComparison.OrdinalIgnoreCase)))
                {
                    log.Error("Exiting due to schema incompatibilities (--strict-schemas enabled)");
                    Environment.Exit(1);
                    return;
                }
            }
            else
            {
                log.Information("Schema compatibility check passed: {Message}", schemaCheckResult.Message);
            }
        }

        var replayPath = GetArgValue(args, "--replay");

        var statusPath = Path.Combine(cfg.DataRoot, "_status", "status.json");
        await using var statusWriter = new StatusWriter(statusPath, () => configService.LoadAndPrepareConfig(cfgPath));
        ConfigWatcher? watcher = null;
        UiServer? uiServer = null;

        if (runMode == CliModeResolver.RunMode.Desktop)
        {
            var uiPort = int.TryParse(GetArgValue(args, "--http-port"), out var parsedUiPort) ? parsedUiPort : 8080;
            log.Information("Desktop mode: starting UI server on port {Port}...", uiPort);
            uiServer = new UiServer(cfgPath, uiPort);
            await uiServer.StartAsync();
            log.Information("Desktop mode UI server started at http://localhost:{Port}", uiPort);
        }

        // Build storage options from config - uses default profile (Research) when no config provided
        var compressionEnabled = cfg.Compress ?? false;
        var storageOpt = cfg.Storage?.ToStorageOptions(cfg.DataRoot, compressionEnabled)
            ?? StorageProfilePresets.CreateFromProfile(null, cfg.DataRoot, compressionEnabled);

        var policy = new JsonlStoragePolicy(storageOpt);
        await using var sink = new JsonlStorageSink(storageOpt, policy);
        await using var pipeline = new EventPipeline(sink, EventPipelinePolicy.HighThroughput);

        // Log storage configuration
        log.Information("Storage path: {RootPath}", storageOpt.RootPath);
        log.Information("Naming convention: {NamingConvention}", storageOpt.NamingConvention);
        log.Information("Date partitioning: {DatePartition}", storageOpt.DatePartition);
        log.Information("Compression: {CompressionEnabled}", storageOpt.Compress ? "enabled" : "disabled");
        log.Debug("Example path: {ExamplePath}", policy.GetPathPreview());

        // Create publisher for pipeline
        IMarketEventPublisher publisher = new PipelinePublisher(pipeline);

        var backfillRequested = args.Any(a => a.Equals("--backfill", StringComparison.OrdinalIgnoreCase))
            || (cfg.Backfill?.Enabled == true);
        if (backfillRequested)
        {
            var backfillRequest = BuildBackfillRequest(cfg, args);

            // Create providers based on configuration
            var backfillProviders = CreateBackfillProviders(cfg, log, configService);

            // Wrap in composite provider if fallback enabled
            IHistoricalDataProvider[] providersArray;
            if (cfg.Backfill?.EnableFallback ?? true)
            {
                var symbolResolver = (cfg.Backfill?.EnableSymbolResolution ?? true)
                    ? new SymbolResolution.OpenFigiSymbolResolver(cfg.Backfill?.Providers?.OpenFigi?.ApiKey, log: log)
                    : null;

                var composite = new CompositeHistoricalDataProvider(
                    backfillProviders,
                    symbolResolver,
                    enableCrossValidation: false,
                    log: log
                );

                providersArray = new IHistoricalDataProvider[] { composite };
            }
            else
            {
                providersArray = backfillProviders.ToArray();
            }

            var backfill = new HistoricalBackfillService(providersArray, log);
            var result = await backfill.RunAsync(backfillRequest, pipeline);
            var statusStore = BackfillStatusStore.FromConfig(cfg);
            await statusStore.WriteAsync(result);
            await pipeline.FlushAsync();
            await statusWriter.WriteOnceAsync();

            if (!result.Success)
                Environment.ExitCode = 1;
            if (uiServer != null)
            {
                await uiServer.StopAsync();
                await uiServer.DisposeAsync();
            }
            return;
        }

        // Collectors
        var quoteCollector = new QuoteCollector(publisher);
        var tradeCollector = new TradeDataCollector(publisher, quoteCollector);
        var depthCollector = new MarketDepthCollector(publisher, requireExplicitSubscription: true);

        if (!string.IsNullOrWhiteSpace(replayPath))
        {
            log.Information("Replaying events from {ReplayPath}...", replayPath);
            var replayer = new JsonlReplayer(replayPath);
            await foreach (var evt in replayer.ReadEventsAsync())
                await pipeline.PublishAsync(evt);

            await pipeline.FlushAsync();
            await statusWriter.WriteOnceAsync();
            return;
        }

        // Market data client (provider selected by config) - credentials resolved via ConfigurationService
        var (alpacaKeyId, alpacaSecretKey) = configService.ResolveAlpacaCredentials(cfg.Alpaca?.KeyId, cfg.Alpaca?.SecretKey);

        await using IMarketDataClient dataClient = cfg.DataSource switch
        {
            DataSourceKind.Alpaca => new AlpacaMarketDataClient(tradeCollector, quoteCollector,
                cfg.Alpaca! with { KeyId = alpacaKeyId ?? "", SecretKey = alpacaSecretKey ?? "" }),
            DataSourceKind.Polygon => new PolygonMarketDataClient(publisher, tradeCollector, quoteCollector),
            DataSourceKind.StockSharp => new StockSharpMarketDataClient(
                tradeCollector,
                depthCollector,
                quoteCollector,
                cfg.StockSharp ?? new StockSharpConfig()),
            _ => new IBMarketDataClient(publisher, tradeCollector, depthCollector)
        };

        await dataClient.ConnectAsync();

        var subscriptionManager = new SubscriptionManager(
            depthCollector,
            tradeCollector,
            dataClient,
            LoggingSetup.ForContext<SubscriptionManager>());

        var runtimeCfg = EnsureDefaultSymbols(cfg);
        subscriptionManager.Apply(runtimeCfg);
        var symbols = runtimeCfg.Symbols ?? Array.Empty<SymbolConfig>();

        if (args.Any(a => a.Equals("--watch-config", StringComparison.OrdinalIgnoreCase)))
        {
            watcher = configService.StartHotReload(cfgPath, newCfg =>
            {
                try
                {
                    var nextCfg = EnsureDefaultSymbols(newCfg);
                    subscriptionManager.Apply(nextCfg);
                    _ = statusWriter.WriteOnceAsync();
                    log.Information("Applied hot-reloaded configuration: {Count} symbols", nextCfg.Symbols?.Length ?? 0);
                }
                catch (Exception ex)
                {
                    log.Error(ex, "Failed to apply hot-reloaded configuration");
                }
            }, ex => log.Error(ex, "Configuration watcher error"));
            log.Information("Watching {ConfigPath} for subscription changes", cfgPath);
        }

        // --- Simulated feed smoke test (depth + trade) ---

        // Leave this as a sanity check in non-IB builds. In IBAPI builds, live data should flow too.
        if (args.Any(a => a.Equals("--simulate-feed", StringComparison.OrdinalIgnoreCase)))
        {
            var now = DateTimeOffset.UtcNow;
            var sym = symbols[0].Symbol;

            depthCollector.OnDepth(new MarketDepthUpdate(now, sym, 0, DepthOperation.Insert, OrderBookSide.Bid, 500.24m, 300m, "MM1"));
            depthCollector.OnDepth(new MarketDepthUpdate(now, sym, 0, DepthOperation.Insert, OrderBookSide.Ask, 500.26m, 250m, "MM2"));
            depthCollector.OnDepth(new MarketDepthUpdate(now, sym, 0, DepthOperation.Update, OrderBookSide.Bid, 500.24m, 350m, "MM1"));
            depthCollector.OnDepth(new MarketDepthUpdate(now, sym, 3, DepthOperation.Update, OrderBookSide.Ask, 500.30m, 100m, "MMX")); // induce integrity
            depthCollector.ResetSymbolStream(sym);
            depthCollector.OnDepth(new MarketDepthUpdate(now, sym, 0, DepthOperation.Insert, OrderBookSide.Bid, 500.20m, 100m, "MM3"));
            depthCollector.OnDepth(new MarketDepthUpdate(now, sym, 0, DepthOperation.Insert, OrderBookSide.Ask, 500.22m, 90m, "MM4"));

            tradeCollector.OnTrade(new MarketTradeUpdate(now, sym, 500.21m, 100, Domain.Models.AggressorSide.Buy, SequenceNumber: 1, StreamId: "SIM", Venue: "TEST"));

            await Task.Delay(200);
        }

        log.Information("Wrote MarketEvents to {StoragePath}", storageOpt.RootPath);
        log.Information("Metrics: published={Published}, integrity={Integrity}, dropped={Dropped}",
            Metrics.Published, Metrics.Integrity, Metrics.Dropped);

        log.Information("Disconnecting from data provider...");
        await dataClient.DisconnectAsync();

        log.Information("Shutdown complete");

        watcher?.Dispose();
        if (uiServer != null)
        {
            await uiServer.StopAsync();
            await uiServer.DisposeAsync();
        }
    }

    // Use CliModeResolver.RunMode for run mode handling
    private static CliModeResolver.RunMode ResolveRunMode(string[] args, ILogger log)
    {
        var (mode, error) = CliModeResolver.ResolveWithError(args);
        if (error != null)
        {
            log.Error(error);
            Environment.Exit(1);
        }
        return mode;
    }

    private static void ShowHelp()
    {
        Console.WriteLine(@"
╔══════════════════════════════════════════════════════════════════════╗
║                    Market Data Collector v1.0                        ║
║          Real-time and historical market data collection             ║
╚══════════════════════════════════════════════════════════════════════╝

USAGE:
    MarketDataCollector [OPTIONS]

MODES:
    --mode <web|desktop|headless> Unified deployment mode selector
    --ui                    Start web dashboard (http://localhost:8080) [legacy]
    --backfill              Run historical data backfill
    --replay <path>         Replay events from JSONL file
    --package               Create a portable data package
    --import-package <path> Import a package into storage
    --list-package <path>   List contents of a package
    --validate-package <path> Validate a package
    --selftest              Run system self-tests
    --validate-config       Validate configuration without starting
    --dry-run               Comprehensive validation without starting (QW-93)
    --help, -h              Show this help message

AUTO-CONFIGURATION (First-time setup):
    --wizard                Interactive configuration wizard (recommended for new users)
    --auto-config           Quick auto-configuration based on environment variables
    --detect-providers      Show available data providers and their status
    --validate-credentials  Validate configured API credentials
    --generate-config       Generate a configuration template

DIAGNOSTICS & TROUBLESHOOTING:
    --quick-check           Fast configuration health check
    --test-connectivity     Test connectivity to all configured providers
    --show-config           Display current configuration summary
    --error-codes           Show error code reference guide
    --check-schemas         Check stored data schema compatibility
    --simulate-feed         Emit a synthetic depth/trade event for smoke testing

SCHEMA VALIDATION OPTIONS:
    --validate-schemas      Run schema check during startup
    --strict-schemas        Exit if schema incompatibilities found (use with --validate-schemas)
    --max-files <n>         Max files to check (default: 100, use with --check-schemas)
    --fail-fast             Stop on first incompatibility (use with --check-schemas)

SYMBOL MANAGEMENT:
    --symbols               Show all symbols (monitored + archived)
    --symbols-monitored     List symbols currently configured for monitoring
    --symbols-archived      List symbols with archived data files
    --symbols-add <list>    Add symbols to configuration (comma-separated)
    --symbols-remove <list> Remove symbols from configuration
    --symbol-status <sym>   Show detailed status for a specific symbol

SYMBOL OPTIONS (use with --symbols-add):
    --no-trades             Don't subscribe to trade data
    --no-depth              Don't subscribe to depth/L2 data
    --depth-levels <n>      Number of depth levels (default: 10)
    --update                Update existing symbols instead of skipping

OPTIONS:
    --config <path>         Path to configuration file (default: appsettings.json)
    --http-port <port>      HTTP server port (default: 8080)
    --watch-config          Enable hot-reload of configuration

ENVIRONMENT VARIABLES:
    MDC_CONFIG_PATH         Alternative to --config argument for specifying config path
    MDC_ENVIRONMENT         Environment name (e.g., Development, Production)
                            Loads appsettings.{Environment}.json as overlay
    DOTNET_ENVIRONMENT      Standard .NET environment variable (fallback for MDC_ENVIRONMENT)

BACKFILL OPTIONS:
    --backfill-provider <name>      Provider to use (default: stooq)
    --backfill-symbols <list>       Comma-separated symbols (e.g., AAPL,MSFT)
    --backfill-from <date>          Start date (YYYY-MM-DD)
    --backfill-to <date>            End date (YYYY-MM-DD)

PACKAGING OPTIONS:
    --package-name <name>           Package name (default: market-data-YYYYMMDD)
    --package-description <text>    Package description
    --package-output <path>         Output directory (default: packages)
    --package-symbols <list>        Comma-separated symbols to include
    --package-events <list>         Event types (Trade,BboQuote,L2Snapshot)
    --package-from <date>           Start date (YYYY-MM-DD)
    --package-to <date>             End date (YYYY-MM-DD)
    --package-format <fmt>          Format: zip, tar.gz (default: zip)
    --package-compression <level>   Compression: none, fast, balanced, max
    --no-quality-report             Exclude quality report from package
    --no-data-dictionary            Exclude data dictionary
    --no-loader-scripts             Exclude loader scripts
    --skip-checksums                Skip checksum verification

IMPORT OPTIONS:
    --import-destination <path>     Destination directory (default: data root)
    --skip-validation               Skip checksum validation during import
    --merge                         Merge with existing data (don't overwrite)

AUTO-CONFIGURATION OPTIONS:
    --template <name>       Template for --generate-config: minimal, full, alpaca,
                            stocksharp, backfill, production, docker (default: minimal)
    --output <path>         Output path for generated config (default: config/appsettings.generated.json)

EXAMPLES:
    # Start web dashboard on default port
    MarketDataCollector --mode web

    # Start web dashboard on custom port
    MarketDataCollector --mode web --http-port 9000

    # Desktop mode (collector + UI server) with hot-reload
    MarketDataCollector --mode desktop --watch-config

    # Run historical backfill
    MarketDataCollector --backfill --backfill-symbols AAPL,MSFT,GOOGL \\
        --backfill-from 2024-01-01 --backfill-to 2024-12-31

    # Run self-tests
    MarketDataCollector --selftest

    # Validate configuration without starting
    MarketDataCollector --validate-config

    # Validate a specific configuration file
    MarketDataCollector --validate-config --config /path/to/config.json

    # Create a portable data package
    MarketDataCollector --package --package-name my-data \\
        --package-symbols AAPL,MSFT --package-from 2024-01-01

    # Create package with maximum compression
    MarketDataCollector --package --package-compression max

    # Import a package
    MarketDataCollector --import-package ./packages/my-data.zip

    # List package contents
    MarketDataCollector --list-package ./packages/my-data.zip

    # Validate a package
    MarketDataCollector --validate-package ./packages/my-data.zip

    # Run interactive configuration wizard (recommended for new users)
    MarketDataCollector --wizard

    # Quick auto-configuration based on environment variables
    MarketDataCollector --auto-config

    # Detect available data providers
    MarketDataCollector --detect-providers

    # Validate configured API credentials
    MarketDataCollector --validate-credentials

    # Generate a configuration template
    MarketDataCollector --generate-config --template alpaca --output config/appsettings.json

    # Quick configuration health check
    MarketDataCollector --quick-check

    # Test connectivity to all providers
    MarketDataCollector --test-connectivity

    # Show current configuration summary
    MarketDataCollector --show-config

    # View all error codes and their meanings
    MarketDataCollector --error-codes

    # Show all symbols (both monitored and archived)
    MarketDataCollector --symbols

    # Show only symbols currently being monitored
    MarketDataCollector --symbols-monitored

    # Show symbols that have archived data
    MarketDataCollector --symbols-archived

    # Add new symbols for monitoring
    MarketDataCollector --symbols-add AAPL,MSFT,GOOGL

    # Add symbols with custom options
    MarketDataCollector --symbols-add SPY,QQQ --no-depth --depth-levels 5

    # Remove symbols from monitoring
    MarketDataCollector --symbols-remove AAPL,MSFT

    # Check status of a specific symbol
    MarketDataCollector --symbol-status AAPL

CONFIGURATION:
    Configuration is loaded from appsettings.json by default, but can be customized:

    Priority for config file path:
      1. --config argument (highest priority)
      2. MDC_CONFIG_PATH environment variable
      3. appsettings.json (default)

    Environment-specific overlays:
      Set MDC_ENVIRONMENT=Production to automatically load appsettings.Production.json
      as an overlay on top of the base configuration.

    To get started:
      Copy appsettings.sample.json to appsettings.json and customize.

DATA PROVIDERS:
    - Interactive Brokers (IB): Level 2 market depth + trades
    - Alpaca: Real-time trades and quotes via WebSocket
    - Polygon: Real-time and historical data (coming soon)

DOCUMENTATION:
    For detailed documentation, see:
    - HELP.md                    - Complete user guide
    - README.md                  - Project overview
    - docs/CONFIGURATION.md      - Configuration reference
    - docs/GETTING_STARTED.md    - Setup guide
    - docs/TROUBLESHOOTING.md    - Common issues

SUPPORT:
    Report issues: https://github.com/rodoHasArrived/Test/issues
    Documentation: ./HELP.md

╔══════════════════════════════════════════════════════════════════════╗
║  NEW USER?     Run: ./MarketDataCollector --wizard                   ║
║  QUICK CHECK:  Run: ./MarketDataCollector --quick-check              ║
║  START UI:     Run: ./MarketDataCollector --ui                       ║
║  Then open http://localhost:8080 in your browser                     ║
╚══════════════════════════════════════════════════════════════════════╝
");
    }

    private static string? GetArgValue(string[] args, string key)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i].Equals(key, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }
        return null;
    }

    private static BackfillRequest BuildBackfillRequest(AppConfig cfg, string[] args)
    {
        var baseRequest = BackfillRequest.FromConfig(cfg);
        var provider = GetArgValue(args, "--backfill-provider") ?? baseRequest.Provider;
        var symbolsArg = GetArgValue(args, "--backfill-symbols");
        var symbols = !string.IsNullOrWhiteSpace(symbolsArg)
            ? symbolsArg.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : baseRequest.Symbols;
        var from = ParseDate(GetArgValue(args, "--backfill-from")) ?? baseRequest.From;
        var to = ParseDate(GetArgValue(args, "--backfill-to")) ?? baseRequest.To;

        return new BackfillRequest(provider, symbols.ToArray(), from, to);
    }

    private static DateOnly? ParseDate(string? value)
        => DateOnly.TryParse(value, out var date) ? date : null;

    /// <summary>
    /// Minimal configuration load for early startup (before logging is set up).
    /// Only used to get DataRoot for logging initialization.
    /// For full configuration processing, use ConfigurationService.LoadAndPrepareConfig().
    /// </summary>
    private static AppConfig LoadConfigMinimal(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                Console.Error.WriteLine($"[Warning] Configuration file not found: {path}");
                Console.Error.WriteLine("Using default configuration. Copy appsettings.sample.json to appsettings.json to customize.");
                return new AppConfig();
            }

            var json = File.ReadAllText(path);
            var cfg = JsonSerializer.Deserialize<AppConfig>(json, AppConfigJsonOptions.Read);
            return cfg ?? new AppConfig();
        }
        catch (JsonException ex)
        {
            Console.Error.WriteLine($"[Error] Invalid JSON in configuration file: {path}");
            Console.Error.WriteLine($"  Error: {ex.Message}");
            Console.Error.WriteLine("  Troubleshooting:");
            Console.Error.WriteLine("    1. Validate JSON syntax at jsonlint.com");
            Console.Error.WriteLine("    2. Check for trailing commas or missing quotes");
            Console.Error.WriteLine("    3. Compare against appsettings.sample.json");
            Console.Error.WriteLine("    4. Run: dotnet user-secrets init (for sensitive data)");
            return new AppConfig();
        }
        catch (UnauthorizedAccessException)
        {
            throw new Application.Exceptions.ConfigurationException(
                $"Access denied reading configuration file: {path}. Check file permissions.",
                path, null);
        }
        catch (IOException ex)
        {
            throw new Application.Exceptions.ConfigurationException(
                $"I/O error reading configuration file: {path}. {ex.Message}",
                path, null);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Error] Failed to load configuration: {ex.Message}");
            Console.Error.WriteLine("Using default configuration.");
            Console.Error.WriteLine("For detailed help, see HELP.md or run with --help");
            return new AppConfig();
        }
    }

    /// <summary>
    /// Resolves the configuration file path from command line arguments, environment variables, or defaults.
    /// Priority: --config argument > MDC_CONFIG_PATH env var > appsettings.json
    /// </summary>
    private static string ResolveConfigPath(string[] args)
    {
        // 1. Check command line argument (highest priority)
        var argValue = GetArgValue(args, "--config");
        if (!string.IsNullOrWhiteSpace(argValue))
            return argValue;

        // 2. Check environment variable
        var envValue = Environment.GetEnvironmentVariable(ConfigPathEnvVar);
        if (!string.IsNullOrWhiteSpace(envValue))
            return envValue;

        // 3. Default to appsettings.json
        return DefaultConfigFileName;
    }

    // NOTE: GetEnvironmentName(), LoadConfigWithEnvironmentOverlay(), and MergeConfigs()
    // have been removed to consolidate configuration logic through ConfigurationService.
    // Use ConfigurationService.LoadAndPrepareConfig() for full configuration processing.

    private sealed class PipelinePublisher : IMarketEventPublisher
    {
        private readonly EventPipeline _pipeline;

        public PipelinePublisher(EventPipeline pipeline) => _pipeline = pipeline;

        public bool TryPublish(in MarketEvent evt)
        {
            var ok = _pipeline.TryPublish(evt);
            if (ok) Metrics.IncPublished();
            else Metrics.IncDropped();

            if (evt.Type == MarketEventType.Integrity) Metrics.IncIntegrity();
            return ok;
        }
    }

    private static AppConfig EnsureDefaultSymbols(AppConfig cfg)
    {
        if (cfg.Symbols is { Length: > 0 }) return cfg;

        var fallback = new[] { new SymbolConfig("SPY", SubscribeTrades: true, SubscribeDepth: true, DepthLevels: 10) };
        return cfg with { Symbols = fallback };
    }

    /// <summary>
    /// Run the package creation command.
    /// </summary>
    private static async Task RunPackageCommandAsync(AppConfig cfg, string[] args, ILogger log)
    {
        log.Information("Creating portable data package...");

        var options = new PackageOptions
        {
            Name = GetArgValue(args, "--package-name") ?? $"market-data-{DateTime.UtcNow:yyyyMMdd}",
            Description = GetArgValue(args, "--package-description"),
            OutputDirectory = GetArgValue(args, "--package-output") ?? "packages",
            IncludeQualityReport = !args.Any(a => a.Equals("--no-quality-report", StringComparison.OrdinalIgnoreCase)),
            IncludeDataDictionary = !args.Any(a => a.Equals("--no-data-dictionary", StringComparison.OrdinalIgnoreCase)),
            IncludeLoaderScripts = !args.Any(a => a.Equals("--no-loader-scripts", StringComparison.OrdinalIgnoreCase)),
            VerifyChecksums = !args.Any(a => a.Equals("--skip-checksums", StringComparison.OrdinalIgnoreCase))
        };

        // Parse symbols
        var symbolsArg = GetArgValue(args, "--package-symbols");
        if (!string.IsNullOrWhiteSpace(symbolsArg))
        {
            options.Symbols = symbolsArg.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        // Parse event types
        var eventTypesArg = GetArgValue(args, "--package-events");
        if (!string.IsNullOrWhiteSpace(eventTypesArg))
        {
            options.EventTypes = eventTypesArg.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        // Parse date range
        var fromArg = GetArgValue(args, "--package-from");
        if (DateTime.TryParse(fromArg, out var from))
        {
            options.StartDate = from;
        }

        var toArg = GetArgValue(args, "--package-to");
        if (DateTime.TryParse(toArg, out var to))
        {
            options.EndDate = to;
        }

        // Parse format
        var formatArg = GetArgValue(args, "--package-format");
        if (!string.IsNullOrWhiteSpace(formatArg))
        {
            options.Format = formatArg.ToLowerInvariant() switch
            {
                "zip" => PackageFormat.Zip,
                "tar.gz" or "targz" or "tgz" => PackageFormat.TarGz,
                "7z" or "7zip" => PackageFormat.SevenZip,
                _ => PackageFormat.Zip
            };
        }

        // Parse compression level
        var compressionArg = GetArgValue(args, "--package-compression");
        if (!string.IsNullOrWhiteSpace(compressionArg))
        {
            options.CompressionLevel = compressionArg.ToLowerInvariant() switch
            {
                "none" => PackageCompressionLevel.None,
                "fast" => PackageCompressionLevel.Fast,
                "balanced" => PackageCompressionLevel.Balanced,
                "maximum" or "max" => PackageCompressionLevel.Maximum,
                _ => PackageCompressionLevel.Balanced
            };
        }

        var packager = new PortableDataPackager(cfg.DataRoot);

        // Subscribe to progress events
        packager.ProgressChanged += (_, progress) =>
        {
            var percent = progress.TotalFiles > 0
                ? (double)progress.FilesProcessed / progress.TotalFiles * 100
                : 0;
            Console.Write($"\r[{progress.Stage}] {progress.FilesProcessed}/{progress.TotalFiles} files ({percent:F1}%)    ");
        };

        var result = await packager.CreatePackageAsync(options);

        Console.WriteLine(); // New line after progress

        if (result.Success)
        {
            Console.WriteLine();
            Console.WriteLine("╔══════════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                    Package Created Successfully                       ║");
            Console.WriteLine("╚══════════════════════════════════════════════════════════════════════╝");
            Console.WriteLine();
            Console.WriteLine($"  Package: {result.PackagePath}");
            Console.WriteLine($"  Size: {result.PackageSizeBytes:N0} bytes");
            Console.WriteLine($"  Compression: {result.CompressionRatio:F2}x");
            Console.WriteLine($"  Files: {result.FilesIncluded:N0}");
            Console.WriteLine($"  Events: {result.TotalEvents:N0}");
            Console.WriteLine($"  Symbols: {string.Join(", ", result.Symbols)}");
            Console.WriteLine($"  Event Types: {string.Join(", ", result.EventTypes)}");
            if (result.DateRange != null)
            {
                Console.WriteLine($"  Date Range: {result.DateRange.Start:yyyy-MM-dd} to {result.DateRange.End:yyyy-MM-dd}");
            }
            Console.WriteLine($"  Checksum: {result.PackageChecksum}");
            Console.WriteLine();

            if (result.Warnings.Length > 0)
            {
                Console.WriteLine("Warnings:");
                foreach (var warning in result.Warnings)
                {
                    Console.WriteLine($"  - {warning}");
                }
            }

            log.Information("Package created: {PackagePath} ({SizeBytes:N0} bytes, {CompressionRatio:F2}x compression)",
                result.PackagePath, result.PackageSizeBytes, result.CompressionRatio);
        }
        else
        {
            Console.Error.WriteLine($"Error: {result.Error}");
            log.Error("Package creation failed: {Error}", result.Error);
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// Run the package import command.
    /// </summary>
    private static async Task RunImportPackageCommandAsync(AppConfig cfg, string packagePath, string[] args, ILogger log)
    {
        log.Information("Importing package: {PackagePath}", packagePath);

        var destinationDir = GetArgValue(args, "--import-destination") ?? cfg.DataRoot;
        var validateChecksums = !args.Any(a => a.Equals("--skip-validation", StringComparison.OrdinalIgnoreCase));
        var mergeWithExisting = args.Any(a => a.Equals("--merge", StringComparison.OrdinalIgnoreCase));

        var packager = new PortableDataPackager(cfg.DataRoot);

        // Subscribe to progress events
        packager.ProgressChanged += (_, progress) =>
        {
            var percent = progress.TotalFiles > 0
                ? (double)progress.FilesProcessed / progress.TotalFiles * 100
                : 0;
            Console.Write($"\r[{progress.Stage}] {progress.FilesProcessed}/{progress.TotalFiles} files ({percent:F1}%)    ");
        };

        var result = await packager.ImportPackageAsync(packagePath, destinationDir, validateChecksums, mergeWithExisting);

        Console.WriteLine(); // New line after progress

        if (result.Success)
        {
            Console.WriteLine();
            Console.WriteLine("╔══════════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                    Package Imported Successfully                      ║");
            Console.WriteLine("╚══════════════════════════════════════════════════════════════════════╝");
            Console.WriteLine();
            Console.WriteLine($"  Source: {result.SourcePath}");
            Console.WriteLine($"  Destination: {result.DestinationPath}");
            Console.WriteLine($"  Files Extracted: {result.FilesExtracted:N0}");
            Console.WriteLine($"  Bytes Extracted: {result.BytesExtracted:N0}");
            Console.WriteLine($"  Files Validated: {result.FilesValidated:N0}");
            Console.WriteLine($"  Symbols: {string.Join(", ", result.Symbols)}");
            Console.WriteLine($"  Event Types: {string.Join(", ", result.EventTypes)}");
            Console.WriteLine($"  Duration: {result.DurationSeconds:F2} seconds");
            Console.WriteLine();

            if (result.Warnings.Length > 0)
            {
                Console.WriteLine("Warnings:");
                foreach (var warning in result.Warnings)
                {
                    Console.WriteLine($"  - {warning}");
                }
            }

            log.Information("Package imported: {FilesExtracted} files, {BytesExtracted:N0} bytes",
                result.FilesExtracted, result.BytesExtracted);
        }
        else
        {
            Console.Error.WriteLine($"Error: {result.Error}");

            if (result.ValidationErrors?.Length > 0)
            {
                Console.Error.WriteLine("\nValidation Errors:");
                foreach (var error in result.ValidationErrors)
                {
                    Console.Error.WriteLine($"  - {error.FilePath}: {error.Message}");
                }
            }

            log.Error("Package import failed: {Error}", result.Error);
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// Run the list package contents command.
    /// </summary>
    private static async Task RunListPackageCommandAsync(string packagePath, ILogger log)
    {
        log.Information("Listing package contents: {PackagePath}", packagePath);

        var packager = new PortableDataPackager(".");

        try
        {
            var contents = await packager.ListPackageContentsAsync(packagePath);

            Console.WriteLine();
            Console.WriteLine("╔══════════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                         Package Contents                              ║");
            Console.WriteLine("╚══════════════════════════════════════════════════════════════════════╝");
            Console.WriteLine();
            Console.WriteLine($"  Name: {contents.Name}");
            Console.WriteLine($"  Package ID: {contents.PackageId}");
            if (!string.IsNullOrEmpty(contents.Description))
            {
                Console.WriteLine($"  Description: {contents.Description}");
            }
            Console.WriteLine($"  Created: {contents.CreatedAt:yyyy-MM-dd HH:mm:ss} UTC");
            Console.WriteLine();
            Console.WriteLine("  Summary:");
            Console.WriteLine($"    Total Files: {contents.TotalFiles:N0}");
            Console.WriteLine($"    Total Events: {contents.TotalEvents:N0}");
            Console.WriteLine($"    Package Size: {contents.PackageSizeBytes:N0} bytes");
            Console.WriteLine($"    Uncompressed Size: {contents.UncompressedSizeBytes:N0} bytes");
            Console.WriteLine();
            Console.WriteLine($"  Symbols: {string.Join(", ", contents.Symbols)}");
            Console.WriteLine($"  Event Types: {string.Join(", ", contents.EventTypes)}");
            if (contents.DateRange != null)
            {
                Console.WriteLine($"  Date Range: {contents.DateRange.Start:yyyy-MM-dd} to {contents.DateRange.End:yyyy-MM-dd}");
                Console.WriteLine($"  Trading Days: {contents.DateRange.TradingDays}");
            }
            Console.WriteLine();

            if (contents.Quality != null)
            {
                Console.WriteLine("  Quality Metrics:");
                Console.WriteLine($"    Overall Score: {contents.Quality.OverallScore:F2}");
                Console.WriteLine($"    Completeness: {contents.Quality.CompletenessScore:F2}");
                Console.WriteLine($"    Integrity: {contents.Quality.IntegrityScore:F2}");
                Console.WriteLine($"    Grade: {contents.Quality.Grade}");
                Console.WriteLine();
            }

            Console.WriteLine("  Files:");
            foreach (var file in contents.Files.Take(20)) // Show first 20 files
            {
                var size = file.SizeBytes > 1024 * 1024
                    ? $"{file.SizeBytes / (1024.0 * 1024.0):F1} MB"
                    : $"{file.SizeBytes / 1024.0:F1} KB";
                Console.WriteLine($"    {file.Path} ({size}, {file.EventCount:N0} events)");
            }

            if (contents.Files.Length > 20)
            {
                Console.WriteLine($"    ... and {contents.Files.Length - 20} more files");
            }
            Console.WriteLine();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error reading package: {ex.Message}");
            log.Error(ex, "Failed to list package contents");
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// Run the validate package command.
    /// </summary>
    private static async Task RunValidatePackageCommandAsync(string packagePath, ILogger log)
    {
        log.Information("Validating package: {PackagePath}", packagePath);

        var packager = new PortableDataPackager(".");
        var result = await packager.ValidatePackageAsync(packagePath);

        Console.WriteLine();
        if (result.IsValid)
        {
            Console.WriteLine("╔══════════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                      Package Validation: PASSED                       ║");
            Console.WriteLine("╚══════════════════════════════════════════════════════════════════════╝");
            Console.WriteLine();
            Console.WriteLine($"  Package: {packagePath}");
            if (result.Manifest != null)
            {
                Console.WriteLine($"  Name: {result.Manifest.Name}");
                Console.WriteLine($"  Package ID: {result.Manifest.PackageId}");
                Console.WriteLine($"  Files: {result.Manifest.TotalFiles:N0}");
                Console.WriteLine($"  Events: {result.Manifest.TotalEvents:N0}");
            }
            Console.WriteLine();
            log.Information("Package validation passed: {PackagePath}", packagePath);
        }
        else
        {
            Console.WriteLine("╔══════════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                      Package Validation: FAILED                       ║");
            Console.WriteLine("╚══════════════════════════════════════════════════════════════════════╝");
            Console.WriteLine();
            Console.WriteLine($"  Package: {packagePath}");

            if (!string.IsNullOrEmpty(result.Error))
            {
                Console.WriteLine($"  Error: {result.Error}");
            }

            if (result.Issues?.Length > 0)
            {
                Console.WriteLine("\n  Issues:");
                foreach (var issue in result.Issues)
                {
                    Console.WriteLine($"    - {issue}");
                }
            }

            if (result.MissingFiles?.Length > 0)
            {
                Console.WriteLine("\n  Missing Files:");
                foreach (var file in result.MissingFiles.Take(10))
                {
                    Console.WriteLine($"    - {file}");
                }
                if (result.MissingFiles.Length > 10)
                {
                    Console.WriteLine($"    ... and {result.MissingFiles.Length - 10} more");
                }
            }

            Console.WriteLine();
            log.Warning("Package validation failed: {PackagePath}", packagePath);
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// Creates backfill providers based on configuration.
    /// Credential resolution is handled through ConfigurationService for consistency.
    /// </summary>
    private static List<IHistoricalDataProvider> CreateBackfillProviders(AppConfig cfg, ILogger log, ConfigurationService configService)
    {
        var backfillCfg = cfg.Backfill;
        var providersCfg = backfillCfg?.Providers;
        var providers = new List<IHistoricalDataProvider>();

        // Alpaca Markets (highest priority when configured - reliable API with adjustments)
        var alpacaCfg = providersCfg?.Alpaca;
        if (alpacaCfg?.Enabled ?? true)
        {
            // Credentials resolved via ConfigurationService for consistency
            var (keyId, secretKey) = configService.ResolveAlpacaCredentials(alpacaCfg?.KeyId, alpacaCfg?.SecretKey);

            if (!string.IsNullOrEmpty(keyId) && !string.IsNullOrEmpty(secretKey))
            {
                providers.Add(new AlpacaHistoricalDataProvider(
                    keyId: keyId,
                    secretKey: secretKey,
                    feed: alpacaCfg?.Feed ?? "iex",
                    adjustment: alpacaCfg?.Adjustment ?? "all",
                    priority: alpacaCfg?.Priority ?? 5,
                    rateLimitPerMinute: alpacaCfg?.RateLimitPerMinute ?? 200,
                    log: log
                ));
            }
        }

        // Yahoo Finance (broadest free coverage)
        var yahooCfg = providersCfg?.Yahoo;
        if (yahooCfg?.Enabled ?? true)
        {
            providers.Add(new YahooFinanceHistoricalDataProvider(log: log));
        }

        // Polygon.io (high-quality data, 2-year free tier)
        var polygonCfg = providersCfg?.Polygon;
        if (polygonCfg?.Enabled ?? true)
        {
            var polygonApiKey = configService.ResolvePolygonCredentials(polygonCfg?.ApiKey);
            if (!string.IsNullOrEmpty(polygonApiKey))
            {
                providers.Add(new PolygonHistoricalDataProvider(
                    apiKey: polygonApiKey,
                    log: log
                ));
            }
        }

        // Tiingo (best for dividend-adjusted data)
        var tiingoCfg = providersCfg?.Tiingo;
        if (tiingoCfg?.Enabled ?? true)
        {
            var tiingoToken = configService.ResolveTiingoCredentials(tiingoCfg?.ApiToken);
            if (!string.IsNullOrEmpty(tiingoToken))
            {
                providers.Add(new TiingoHistoricalDataProvider(
                    apiToken: tiingoToken,
                    log: log
                ));
            }
        }

        // Finnhub (generous 60 calls/min free tier)
        var finnhubCfg = providersCfg?.Finnhub;
        if (finnhubCfg?.Enabled ?? true)
        {
            var finnhubApiKey = configService.ResolveFinnhubCredentials(finnhubCfg?.ApiKey);
            if (!string.IsNullOrEmpty(finnhubApiKey))
            {
                providers.Add(new FinnhubHistoricalDataProvider(
                    apiKey: finnhubApiKey,
                    log: log
                ));
            }
        }

        // Stooq (reliable free EOD data - no API key required)
        var stooqCfg = providersCfg?.Stooq;
        if (stooqCfg?.Enabled ?? true)
        {
            providers.Add(new StooqHistoricalDataProvider(log: log));
        }

        // Alpha Vantage (unique intraday historical data - limited free tier)
        var alphaVantageCfg = providersCfg?.AlphaVantage;
        if (alphaVantageCfg?.Enabled ?? false) // Disabled by default due to very limited free tier
        {
            var alphaVantageApiKey = configService.ResolveAlphaVantageCredentials(alphaVantageCfg?.ApiKey);
            if (!string.IsNullOrEmpty(alphaVantageApiKey))
            {
                providers.Add(new AlphaVantageHistoricalDataProvider(
                    apiKey: alphaVantageApiKey,
                    log: log
                ));
            }
        }

        // Nasdaq Data Link (Quandl - may require API key for better limits)
        var nasdaqCfg = providersCfg?.Nasdaq;
        if (nasdaqCfg?.Enabled ?? true)
        {
            var nasdaqApiKey = configService.ResolveNasdaqCredentials(nasdaqCfg?.ApiKey);
            providers.Add(new NasdaqDataLinkHistoricalDataProvider(
                apiKey: nasdaqApiKey,
                database: nasdaqCfg?.Database ?? "WIKI",
                log: log
            ));
        }

        // Sort by priority (lower = tried first)
        return providers
            .OrderBy(p => p.Priority)
            .ToList();
    }

    /// <summary>
    /// Format bytes as human-readable string.
    /// </summary>
    private static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        var order = 0;
        double size = bytes;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        return $"{size:0.##} {sizes[order]}";
    }

    /// <summary>
    /// Initializes HttpClientFactory for proper HTTP client lifecycle management.
    /// Implements TD-10: Replace instance HttpClient with IHttpClientFactory.
    /// </summary>
    private static void InitializeHttpClientFactory(ILogger log)
    {
        var services = new ServiceCollection();

        // Register all named HttpClient configurations with Polly policies
        services.AddMarketDataHttpClients();

        // Build the service provider
        var serviceProvider = services.BuildServiceProvider();

        // Initialize the static factory provider for backward compatibility
        HttpClientFactoryProvider.Initialize(serviceProvider);

        log.Debug("HttpClientFactory initialized with named clients for all data providers (TD-10)");
    }
}
