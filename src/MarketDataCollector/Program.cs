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
using MarketDataCollector.Infrastructure.Providers.Core;
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
        // Parse CLI arguments once into a typed record
        var cliArgs = CliArguments.Parse(args);

        // Initialize logging early - use minimal config load just for DataRoot
        var cfgPath = ResolveConfigPath(cliArgs);
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
            await RunAsync(cliArgs, cfg, cfgPath, log, configService, deploymentContext);
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

    private static async Task RunAsync(CliArguments cliArgs, AppConfig cfg, string cfgPath, ILogger log, ConfigurationService configService, DeploymentContext deployment)
    {
        // Initialize HttpClientFactory for proper HTTP client lifecycle management (TD-10)
        InitializeHttpClientFactory(log);

        // Build all CLI command handlers and dispatch through a single dispatcher.
        // Registration order determines priority when multiple flags are present.
        var symbolService = new SymbolManagementService(new ConfigStore(cfgPath), cfg.DataRoot, log);

        var dispatcher = new CommandDispatcher(
            new HelpCommand(),
            new ConfigCommands(configService, log),
            new DiagnosticsCommands(cfg, cfgPath, configService, log),
            new SchemaCheckCommand(cfg, log),
            new SymbolCommands(symbolService, log),
            new ValidateConfigCommand(configService, cfgPath, log),
            new DryRunCommand(cfg, configService, log),
            new SelfTestCommand(log),
            new PackageCommands(cfg, log)
        );

        var (handled, exitCode) = await dispatcher.TryDispatchAsync(cliArgs.Raw);
        if (handled)
        {
            if (exitCode != 0) Environment.Exit(exitCode);
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
        if (cliArgs.ValidateSchemas)
        {
            log.Information("Running startup schema compatibility check...");
            await using var schemaService = new SchemaValidationService(
                new SchemaValidationOptions { EnableVersionTracking = true },
                cfg.DataRoot);

            var schemaCheckResult = await schemaService.PerformStartupCheckAsync();
            if (!schemaCheckResult.Success)
            {
                log.Warning("Schema compatibility check found issues: {Message}", schemaCheckResult.Message);
                if (cliArgs.StrictSchemas)
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

        var backfillRequested = cliArgs.Backfill || (cfg.Backfill?.Enabled == true);
        if (backfillRequested)
        {
            var backfillRequest = BuildBackfillRequest(cfg, cliArgs);

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

        if (!string.IsNullOrWhiteSpace(cliArgs.Replay))
        {
            log.Information("Replaying events from {ReplayPath}...", cliArgs.Replay);
            var replayer = new JsonlReplayer(cliArgs.Replay);
            await foreach (var evt in replayer.ReadEventsAsync())
                await pipeline.PublishAsync(evt);

            await pipeline.FlushAsync();
            await statusWriter.WriteOnceAsync();
            return;
        }

        // Create the ProviderRegistry with streaming factories for DI-based provider resolution.
        // This replaces the old MarketDataClientFactory switch-statement approach.
        var providerRegistry = CreateProviderRegistry(cfg, configService, publisher, tradeCollector, depthCollector, quoteCollector);

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
                    var client = providerRegistry.CreateStreamingClient(providerKind);
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
                dataClient = providerRegistry.CreateStreamingClient(cfg.DataSource);
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
            dataClient = providerRegistry.CreateStreamingClient(cfg.DataSource);
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

        var subscriptionManager = new Application.Subscriptions.SubscriptionManager(
            depthCollector,
            tradeCollector,
            dataClient,
            LoggingSetup.ForContext<Application.Subscriptions.SubscriptionManager>());

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
        if (cliArgs.SimulateFeed)
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

    private static BackfillRequest BuildBackfillRequest(AppConfig cfg, CliArguments cliArgs)
    {
        var baseRequest = BackfillRequest.FromConfig(cfg);
        var provider = cliArgs.BackfillProvider ?? baseRequest.Provider;
        var symbols = !string.IsNullOrWhiteSpace(cliArgs.BackfillSymbols)
            ? cliArgs.BackfillSymbols.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : baseRequest.Symbols;
        var from = ParseDate(cliArgs.BackfillFrom) ?? baseRequest.From;
        var to = ParseDate(cliArgs.BackfillTo) ?? baseRequest.To;

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
    /// Resolves the configuration file path from CLI arguments, environment variables, or defaults.
    /// Priority: --config argument > MDC_CONFIG_PATH env var > appsettings.json
    /// </summary>
    private static string ResolveConfigPath(CliArguments cliArgs)
    {
        // 1. Check typed CLI argument (highest priority)
        if (!string.IsNullOrWhiteSpace(cliArgs.ConfigPath))
            return cliArgs.ConfigPath;

        // 2. Check environment variable
        var envValue = Environment.GetEnvironmentVariable(ConfigPathEnvVar);
        if (!string.IsNullOrWhiteSpace(envValue))
            return envValue;

        // 3. Default to appsettings.json
        return DefaultConfigFileName;
    }

    private static AppConfig EnsureDefaultSymbols(AppConfig cfg)
    {
        if (cfg.Symbols is { Length: > 0 }) return cfg;

        var fallback = new[] { new SymbolConfig("SPY", SubscribeTrades: true, SubscribeDepth: true, DepthLevels: 10) };
        return cfg with { Symbols = fallback };
    }

    /// <summary>
    /// Creates a <see cref="ProviderRegistry"/> populated with streaming factory functions
    /// for the direct startup path (bypasses full DI composition root).
    /// Each <see cref="DataSourceKind"/> maps to a factory that creates the appropriate client.
    /// </summary>
    private static ProviderRegistry CreateProviderRegistry(
        AppConfig cfg,
        ConfigurationService configService,
        IMarketEventPublisher publisher,
        TradeDataCollector tradeCollector,
        MarketDepthCollector depthCollector,
        QuoteCollector quoteCollector)
    {
        var registry = new ProviderRegistry(log: LoggingSetup.ForContext<ProviderRegistry>());

        registry.RegisterStreamingFactory(DataSourceKind.IB, () =>
            new IBMarketDataClient(publisher, tradeCollector, depthCollector));

        registry.RegisterStreamingFactory(DataSourceKind.Alpaca, () =>
        {
            var (keyId, secretKey) = configService.ResolveAlpacaCredentials(
                cfg.Alpaca?.KeyId, cfg.Alpaca?.SecretKey);
            return new AlpacaMarketDataClient(tradeCollector, quoteCollector,
                cfg.Alpaca! with { KeyId = keyId ?? "", SecretKey = secretKey ?? "" });
        });

        registry.RegisterStreamingFactory(DataSourceKind.Polygon, () =>
            new PolygonMarketDataClient(publisher, tradeCollector, quoteCollector));

        registry.RegisterStreamingFactory(DataSourceKind.StockSharp, () =>
            new StockSharpMarketDataClient(tradeCollector, depthCollector, quoteCollector,
                cfg.StockSharp ?? new StockSharpConfig()));

        registry.RegisterStreamingFactory(DataSourceKind.NYSE, () =>
            new IBMarketDataClient(publisher, tradeCollector, depthCollector));

        return registry;
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
