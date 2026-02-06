using System.Text.Json;
using System.Threading.Channels;
using MarketDataCollector.Application.Backfill;
using MarketDataCollector.Application.Commands;
using MarketDataCollector.Application.Composition;
using MarketDataCollector.Application.Config;
using DeploymentContext = MarketDataCollector.Application.Config.DeploymentContext;
using DeploymentMode = MarketDataCollector.Application.Config.DeploymentMode;
using MarketDataCollector.Application.Exceptions;
using MarketDataCollector.Application.Logging;
using MarketDataCollector.Application.Monitoring;
using MarketDataCollector.Application.Subscriptions;
using MarketDataCollector.Application.Pipeline;
using MarketDataCollector.Application.Services;
using MarketDataCollector.Application.Subscriptions.Services;
using MarketDataCollector.Application.Testing;
using MarketDataCollector.Application.UI;
using MarketDataCollector.Contracts.Domain.Enums;
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
using MarketDataCollector.Infrastructure.Providers.Streaming.Failover;
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

        // Create deployment context for unified startup logic
        var deploymentContext = DeploymentContext.FromArgs(args, cfgPath);
        log.Debug("Deployment context: {Mode}, Command: {Command}, Docker: {IsDocker}",
            deploymentContext.Mode, deploymentContext.Command, deploymentContext.IsDocker);

        // Now use ConfigurationService for full config processing (with self-healing, credential resolution, etc.)
        await using var configService = new ConfigurationService(log);
        var cfg = configService.LoadAndPrepareConfig(cfgPath);

        try
        {
            await RunAsync(args, cfg, cfgPath, log, configService, deploymentContext);
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

    private static async Task RunAsync(string[] args, AppConfig cfg, string cfgPath, ILogger log, ConfigurationService configService, DeploymentContext deployment)
    {
        // Initialize HttpClientFactory for proper HTTP client lifecycle management (TD-10)
        InitializeHttpClientFactory(log);

        // Use deployment context for mode resolution (replaces scattered conditional logic)
        var runMode = deployment.Mode switch
        {
            DeploymentMode.Web => CliModeResolver.RunMode.Web,
            DeploymentMode.Desktop => CliModeResolver.RunMode.Desktop,
            _ => CliModeResolver.RunMode.Headless
        };

        // Help Mode - Display usage information
        if (args.Any(a => a.Equals("--help", StringComparison.OrdinalIgnoreCase) || a.Equals("-h", StringComparison.OrdinalIgnoreCase)))
        {
            ShowHelp();
            return;
        }

        // Configuration setup commands (--wizard, --auto-config, --detect-providers, --generate-config)
        // Extracted to Application/Commands/ConfigCommands.cs
        var configCommands = new ConfigCommands(configService, log);
        if (configCommands.CanHandle(args))
        {
            var exitCode = await configCommands.ExecuteAsync(args);
            if (exitCode != 0) Environment.Exit(exitCode);
            return;
        }

        // Diagnostics commands (--quick-check, --test-connectivity, --error-codes, --show-config, --validate-credentials)
        // Extracted to Application/Commands/DiagnosticsCommands.cs
        var diagCommands = new DiagnosticsCommands(cfg, cfgPath, configService, log);
        if (diagCommands.CanHandle(args))
        {
            var exitCode = await diagCommands.ExecuteAsync(args);
            if (exitCode != 0) Environment.Exit(exitCode);
            return;
        }

        // Schema check command (--check-schemas)
        // Extracted to Application/Commands/SchemaCheckCommand.cs
        var schemaCheck = new SchemaCheckCommand(cfg, log);
        if (schemaCheck.CanHandle(args))
        {
            var exitCode = await schemaCheck.ExecuteAsync(args);
            if (exitCode != 0) Environment.Exit(exitCode);
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
        if (deployment.Mode == DeploymentMode.Web)
        {
            log.Information("Starting web dashboard ({ModeDescription})...", deployment.ModeDescription);

            await using var webServer = new UiServer(cfgPath, deployment.HttpPort);
            await webServer.StartAsync();

            log.Information("Web dashboard started at http://localhost:{Port}", deployment.HttpPort);
            Console.WriteLine($"Web dashboard running at http://localhost:{deployment.HttpPort}");
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
            await webServer.StopAsync();
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

        // Package commands (--package, --import-package, --list-package, --validate-package)
        // Extracted to Application/Commands/PackageCommands.cs for testability
        var packageCommands = new PackageCommands(cfg, log);
        if (packageCommands.CanHandle(args))
        {
            var exitCode = await packageCommands.ExecuteAsync(args);
            if (exitCode != 0) Environment.Exit(exitCode);
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

        if (deployment.Mode == DeploymentMode.Desktop)
        {
            log.Information("Desktop mode: starting UI server ({ModeDescription})...", deployment.ModeDescription);
            uiServer = new UiServer(cfgPath, deployment.HttpPort);
            await uiServer.StartAsync();
            log.Information("Desktop mode UI server started at http://localhost:{Port}", deployment.HttpPort);
        }

        // Build storage options from config - uses default profile (Research) when no config provided
        var compressionEnabled = cfg.Compress ?? false;
        var storageOpt = cfg.Storage?.ToStorageOptions(cfg.DataRoot, compressionEnabled)
            ?? StorageProfilePresets.CreateFromProfile(null, cfg.DataRoot, compressionEnabled);

        var policy = new JsonlStoragePolicy(storageOpt);
        await using var sink = new JsonlStorageSink(storageOpt, policy);

        // Create WAL for crash-safe durability
        var walDir = Path.Combine(storageOpt.RootPath, "_wal");
        var wal = new Storage.Archival.WriteAheadLog(walDir, new Storage.Archival.WalOptions
        {
            SyncMode = Storage.Archival.WalSyncMode.BatchedSync,
            SyncBatchSize = 1000,
            MaxFlushDelay = TimeSpan.FromSeconds(1)
        });
        await using var pipeline = new EventPipeline(sink, EventPipelinePolicy.HighThroughput, wal: wal);

        // Recover any uncommitted events from prior crash
        await pipeline.RecoverAsync();
        log.Information("WAL enabled for pipeline durability at {WalDirectory}", walDir);

        // Log storage configuration
        log.Information("Storage path: {RootPath}", storageOpt.RootPath);
        log.Information("Naming convention: {NamingConvention}", storageOpt.NamingConvention);
        log.Information("Date partitioning: {DatePartition}", storageOpt.DatePartition);
        log.Information("Compression: {CompressionEnabled}", storageOpt.Compress ? "enabled" : "disabled");
        log.Debug("Example path: {ExamplePath}", policy.GetPathPreview());

        // Create publisher for pipeline (using unified PipelinePublisher from composition root)
        IMarketEventPublisher publisher = new Application.Composition.PipelinePublisher(pipeline);

        var backfillRequested = args.Any(a => a.Equals("--backfill", StringComparison.OrdinalIgnoreCase))
            || (cfg.Backfill?.Enabled == true);
        if (backfillRequested)
        {
            var backfillRequest = BuildBackfillRequest(cfg, args);

            // Use HostStartup for unified service creation via composition root
            await using var hostStartup = HostStartupFactory.CreateForBackfill(cfgPath);
            var backfillProviders = hostStartup.CreateBackfillProviders();

            // Wrap in composite provider if fallback enabled
            IHistoricalDataProvider[] providersArray;
            if (cfg.Backfill?.EnableFallback ?? true)
            {
                var composite = hostStartup.CreateCompositeBackfillProvider(backfillProviders);
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

        // Market data client (provider selected by config) via factory for runtime switching
        var clientFactory = new MarketDataClientFactory(
            alpacaCredentialResolver: (_, appCfg) => configService.ResolveAlpacaCredentials(appCfg.Alpaca?.KeyId, appCfg.Alpaca?.SecretKey),
            log: LoggingSetup.ForContext<MarketDataClientFactory>());

        // Check if streaming failover is configured
        var failoverCfg = cfg.DataSources;
        var failoverRules = failoverCfg?.FailoverRules ?? Array.Empty<FailoverRuleConfig>();
        var useFailover = failoverCfg?.EnableFailover == true && failoverRules.Length > 0;

        ConnectionHealthMonitor? healthMonitor = null;
        StreamingFailoverService? failoverService = null;
        IMarketDataClient dataClient;

        if (useFailover)
        {
            log.Information("Streaming failover enabled with {RuleCount} rules", failoverRules.Length);

            healthMonitor = new ConnectionHealthMonitor();
            failoverService = new StreamingFailoverService(healthMonitor);

            // Use the first failover rule (primary use case)
            var rule = failoverRules[0];
            var providerMap = new Dictionary<string, IMarketDataClient>(StringComparer.OrdinalIgnoreCase);

            // Create a client for each provider in the failover chain
            var allProviderIds = new[] { rule.PrimaryProviderId }
                .Concat(rule.BackupProviderIds)
                .Distinct(StringComparer.OrdinalIgnoreCase);

            var sources = failoverCfg!.Sources ?? Array.Empty<DataSourceConfig>();
            foreach (var providerId in allProviderIds)
            {
                var source = sources.FirstOrDefault(s => string.Equals(s.Id, providerId, StringComparison.OrdinalIgnoreCase));
                var providerKind = source?.Provider ?? cfg.DataSource;

                try
                {
                    var client = clientFactory.Create(providerKind, cfg, publisher, tradeCollector, depthCollector, quoteCollector);
                    providerMap[providerId] = client;
                    failoverService.RegisterProvider(providerId);
                    log.Information("Created streaming client for failover provider {ProviderId} ({Kind})", providerId, providerKind);
                }
                catch (Exception ex)
                {
                    log.Warning(ex, "Failed to create streaming client for provider {ProviderId}; skipping", providerId);
                }
            }

            if (providerMap.Count == 0)
            {
                log.Error("No streaming providers could be created for failover; falling back to single provider");
                dataClient = clientFactory.Create(cfg.DataSource, cfg, publisher, tradeCollector, depthCollector, quoteCollector);
            }
            else
            {
                var initialProvider = providerMap.ContainsKey(rule.PrimaryProviderId)
                    ? rule.PrimaryProviderId
                    : providerMap.Keys.First();

                dataClient = new FailoverAwareMarketDataClient(providerMap, failoverService, rule.Id, initialProvider);
                failoverService.Start(failoverCfg!);
            }
        }
        else
        {
            dataClient = clientFactory.Create(cfg.DataSource, cfg, publisher, tradeCollector, depthCollector, quoteCollector);
        }

        await using var dataClientDisposable = dataClient;

        try
        {
            await dataClient.ConnectAsync();
        }
        catch (Exception ex)
        {
            log.Error(ex, "Failed to connect to {DataSource} data provider. Check credentials and connectivity.", cfg.DataSource);
            throw;
        }

        var subscriptionManager = new SubscriptionManager(
            depthCollector,
            tradeCollector,
            dataClient,
            LoggingSetup.ForContext<SubscriptionManager>());

        var runtimeCfg = EnsureDefaultSymbols(cfg);
        subscriptionManager.Apply(runtimeCfg);
        var symbols = runtimeCfg.Symbols ?? Array.Empty<SymbolConfig>();

        if (deployment.HotReloadEnabled)
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

            tradeCollector.OnTrade(new MarketTradeUpdate(now, sym, 500.21m, 100, MarketDataCollector.Contracts.Domain.Enums.AggressorSide.Buy, SequenceNumber: 1, StreamId: "SIM", Venue: "TEST"));

            await Task.Delay(200);
        }

        log.Information("Wrote MarketEvents to {StoragePath}", storageOpt.RootPath);
        log.Information("Metrics: published={Published}, integrity={Integrity}, dropped={Dropped}",
            Metrics.Published, Metrics.Integrity, Metrics.Dropped);

        log.Information("Disconnecting from data provider...");
        await dataClient.DisconnectAsync();

        failoverService?.Dispose();
        healthMonitor?.Dispose();

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

    // GetEnvironmentName(), LoadConfigWithEnvironmentOverlay(), and MergeConfigs()
    // have been removed to consolidate configuration logic through ConfigurationService.
    // Use ConfigurationService.LoadAndPrepareConfig() for full configuration processing.

    // PipelinePublisher has been consolidated into ServiceCompositionRoot
    // and is accessed via DI through the composition root.

    private static AppConfig EnsureDefaultSymbols(AppConfig cfg)
    {
        if (cfg.Symbols is { Length: > 0 }) return cfg;

        var fallback = new[] { new SymbolConfig("SPY", SubscribeTrades: true, SubscribeDepth: true, DepthLevels: 10) };
        return cfg with { Symbols = fallback };
    }

    // Package commands now handled by Application/Commands/PackageCommands.cs

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
    /// <remarks>
    /// When using HostStartup via the composition root, HttpClientFactory is initialized
    /// automatically as part of AddMarketDataServices with EnableHttpClientFactory = true.
    /// This method is used for the direct startup path that doesn't go through HostStartup.
    /// </remarks>
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
