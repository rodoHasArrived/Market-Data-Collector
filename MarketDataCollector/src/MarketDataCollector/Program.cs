using System.Text.Json;
using MassTransit;
using MarketDataCollector.Application.Backfill;
using MarketDataCollector.Application.Config;
using MarketDataCollector.Application.Exceptions;
using MarketDataCollector.Application.Logging;
using MarketDataCollector.Application.Monitoring;
using MarketDataCollector.Application.Subscriptions;
using MarketDataCollector.Application.Pipeline;
using MarketDataCollector.Application.Config.Credentials;
using MarketDataCollector.Application.Testing;
using MarketDataCollector.Application.UI;
using MarketDataCollector.Domain.Collectors;
using MarketDataCollector.Domain.Events;
using MarketDataCollector.Domain.Models;
using MarketDataCollector.Infrastructure;
using MarketDataCollector.Infrastructure.Providers.InteractiveBrokers;
using MarketDataCollector.Infrastructure.Providers.Alpaca;
using MarketDataCollector.Infrastructure.Providers.Polygon;
using MarketDataCollector.Infrastructure.Providers.Backfill;
using SymbolResolution = MarketDataCollector.Infrastructure.Providers.Backfill.SymbolResolution;
using BackfillRequest = MarketDataCollector.Application.Backfill.BackfillRequest;
using MarketDataCollector.Messaging.Configuration;
using MarketDataCollector.Messaging.Publishers;
using MarketDataCollector.Storage;
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
    public static async Task Main(string[] args)
    {
        // Initialize logging early
        var cfgPath = "appsettings.json";
        var cfg = LoadConfig(cfgPath);
        LoggingSetup.Initialize(dataRoot: cfg.DataRoot);
        var log = LoggingSetup.ForContext("Program");

        try
        {
            await RunAsync(args, cfg, cfgPath, log);
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

    private static async Task RunAsync(string[] args, AppConfig cfg, string cfgPath, ILogger log)
    {
        // Help Mode - Display usage information
        if (args.Any(a => a.Equals("--help", StringComparison.OrdinalIgnoreCase) || a.Equals("-h", StringComparison.OrdinalIgnoreCase)))
        {
            ShowHelp();
            return;
        }

        // Validate Config Mode - Check configuration without starting
        if (args.Any(a => a.Equals("--validate-config", StringComparison.OrdinalIgnoreCase)))
        {
            var configPathArg = GetArgValue(args, "--config") ?? cfgPath;
            var validator = new ConfigValidatorCli(log);
            var exitCode = validator.Validate(configPathArg);
            Environment.Exit(exitCode);
            return;
        }

        // UI Mode - Start web dashboard
        if (args.Any(a => a.Equals("--ui", StringComparison.OrdinalIgnoreCase)))
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

        // Validate configuration
        if (!ConfigValidationHelper.ValidateAndLog(cfg))
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

        var replayPath = GetArgValue(args, "--replay");
        var statusPort = int.TryParse(GetArgValue(args, "--status-port"), out var parsedPort) ? parsedPort : 8080;

        var statusPath = Path.Combine(cfg.DataRoot, "_status", "status.json");
        await using var statusWriter = new StatusWriter(statusPath, () => LoadConfig(cfgPath));
        if (args.Any(a => a.Equals("--serve-status", StringComparison.OrdinalIgnoreCase)))
            statusWriter.Start(TimeSpan.FromSeconds(1));
        StatusHttpServer? statusHttp = null;
        ConfigWatcher? watcher = null;

        // Build storage options from config
        var storageOpt = cfg.Storage?.ToStorageOptions(cfg.DataRoot, cfg.Compress)
            ?? new StorageOptions
            {
                RootPath = cfg.DataRoot,
                Compress = cfg.Compress,
                NamingConvention = FileNamingConvention.BySymbol,
                DatePartition = DatePartition.Daily
            };

        var policy = new JsonlStoragePolicy(storageOpt);
        await using var sink = new JsonlStorageSink(storageOpt, policy);
        await using var pipeline = new EventPipeline(sink, capacity: 50_000);

        // Log storage configuration
        log.Information("Storage path: {RootPath}", storageOpt.RootPath);
        log.Information("Naming convention: {NamingConvention}", storageOpt.NamingConvention);
        log.Information("Date partitioning: {DatePartition}", storageOpt.DatePartition);
        log.Information("Compression: {CompressionEnabled}", storageOpt.Compress ? "enabled" : "disabled");
        log.Debug("Example path: {ExamplePath}", policy.GetPathPreview());

        // Build service provider for MassTransit (if enabled)
        ServiceProvider? serviceProvider = null;
        IHost? massTransitHost = null;
        IMarketEventPublisher publisher;

        var mtConfig = cfg.MassTransit ?? new MassTransitConfig();
        var pipelinePublisher = new PipelinePublisher(pipeline);

        if (mtConfig.Enabled)
        {
            log.Information("Initializing MassTransit with {Transport} transport...", mtConfig.Transport);

            var services = new ServiceCollection();
            services.AddMassTransitMessaging(mtConfig);

            serviceProvider = services.BuildServiceProvider();

            // Start the MassTransit bus
            var busControl = serviceProvider.GetRequiredService<IBusControl>();
            await busControl.StartAsync();
            log.Information("MassTransit bus started successfully");

            // Get the publish endpoint
            var publishEndpoint = serviceProvider.GetRequiredService<IPublishEndpoint>();
            var massTransitPublisher = new MassTransitPublisher(publishEndpoint, enableMetrics: false);

            // Create composite publisher: events go to both local storage AND MassTransit
            publisher = new CompositePublisher(pipelinePublisher, massTransitPublisher);
            log.Information("Composite publisher configured: events will be published to local storage AND MassTransit");
        }
        else
        {
            log.Debug("MassTransit messaging is disabled");
            publisher = pipelinePublisher;
        }

        var backfillRequested = args.Any(a => a.Equals("--backfill", StringComparison.OrdinalIgnoreCase))
            || (cfg.Backfill?.Enabled == true);
        if (backfillRequested)
        {
            var backfillRequest = BuildBackfillRequest(cfg, args);

            // Create providers based on configuration
            var backfillProviders = CreateBackfillProviders(cfg, log);

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

            if (statusHttp is not null)
                await statusHttp.DisposeAsync();

            if (!result.Success)
                Environment.ExitCode = 1;
            return;
        }

        // Collectors
        var quoteCollector = new QuoteCollector(publisher);
        var tradeCollector = new TradeDataCollector(publisher, quoteCollector);
        var depthCollector = new MarketDepthCollector(publisher, requireExplicitSubscription: true);

        if (args.Any(a => a.Equals("--serve-status", StringComparison.OrdinalIgnoreCase)))
        {
            statusHttp = new StatusHttpServer(statusPort, Metrics.GetSnapshot, pipeline.GetStatistics, () => depthCollector.GetRecentIntegrityEvents());
            statusHttp.Start();
            log.Information("Status/metrics dashboard running at http://localhost:{Port}/", statusPort);
        }

        if (!string.IsNullOrWhiteSpace(replayPath))
        {
            log.Information("Replaying events from {ReplayPath}...", replayPath);
            var replayer = new JsonlReplayer(replayPath);
            await foreach (var evt in replayer.ReadEventsAsync())
                await pipeline.PublishAsync(evt);

            await pipeline.FlushAsync();
            await statusWriter.WriteOnceAsync();
            if (statusHttp is not null)
                await statusHttp.DisposeAsync();
            return;
        }

        // Market data client (provider selected by config)
        var credentialResolver = new CredentialResolver(
            new IAlpacaCredentialSource[]
            {
                new EnvironmentAlpacaCredentialSource(),
                new FileAlpacaCredentialSource(),
                new ConfigAlpacaCredentialSource()
            },
            LoggingSetup.ForContext<CredentialResolver>());

        await using IMarketDataClient dataClient = cfg.DataSource switch
        {
            DataSourceKind.Alpaca => new AlpacaMarketDataClient(tradeCollector, quoteCollector, credentialResolver.ResolveAlpaca(cfg)),
            DataSourceKind.Polygon => new PolygonMarketDataClient(publisher, tradeCollector, quoteCollector),
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
            watcher = new ConfigWatcher(cfgPath);
            watcher.ConfigChanged += newCfg =>
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
            };
            watcher.Error += ex => log.Error(ex, "Configuration watcher error");
            watcher.Start();
            log.Information("Watching {ConfigPath} for subscription changes", cfgPath);
        }

        // --- Simulated feed smoke test (depth + trade) ---

        // Leave this as a sanity check in non-IB builds. In IBAPI builds, live data should flow too.
        var now = DateTimeOffset.UtcNow;
        var sym = symbols[0].Symbol;

        depthCollector.OnDepth(new MarketDepthUpdate(now, sym, 0, DepthOperation.Insert, OrderBookSide.Bid, 500.24, 300, "MM1"));
        depthCollector.OnDepth(new MarketDepthUpdate(now, sym, 0, DepthOperation.Insert, OrderBookSide.Ask, 500.26, 250, "MM2"));
        depthCollector.OnDepth(new MarketDepthUpdate(now, sym, 0, DepthOperation.Update, OrderBookSide.Bid, 500.24, 350, "MM1"));
        depthCollector.OnDepth(new MarketDepthUpdate(now, sym, 3, DepthOperation.Update, OrderBookSide.Ask, 500.30, 100, "MMX")); // induce integrity
        depthCollector.ResetSymbolStream(sym);
        depthCollector.OnDepth(new MarketDepthUpdate(now, sym, 0, DepthOperation.Insert, OrderBookSide.Bid, 500.20, 100, "MM3"));
        depthCollector.OnDepth(new MarketDepthUpdate(now, sym, 0, DepthOperation.Insert, OrderBookSide.Ask, 500.22, 90, "MM4"));

        tradeCollector.OnTrade(new MarketTradeUpdate(now, sym, 500.21m, 100, AggressorSide.Buy, SequenceNumber: 1, StreamId: "SIM", Venue: "TEST"));

        await Task.Delay(200);

        log.Information("Wrote MarketEvents to {StoragePath}", storageOpt.RootPath);
        log.Information("Metrics: published={Published}, integrity={Integrity}, dropped={Dropped}",
            Metrics.Published, Metrics.Integrity, Metrics.Dropped);

        if (args.Any(a => a.Equals("--serve-status", StringComparison.OrdinalIgnoreCase)))
        {
            log.Information("Status serving enabled (writing data/_status/status.json). Press Ctrl+C to stop.");
            Console.WriteLine("Press Ctrl+C to stop...");
            var done = new TaskCompletionSource();
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                log.Information("Shutdown requested, stopping gracefully...");
                done.TrySetResult();
            };
            await done.Task;
        }

        log.Information("Disconnecting from data provider...");
        await dataClient.DisconnectAsync();

        // Stop MassTransit if it was started
        if (serviceProvider is not null)
        {
            log.Information("Stopping MassTransit bus...");
            var busControl = serviceProvider.GetRequiredService<IBusControl>();
            await busControl.StopAsync();
            await serviceProvider.DisposeAsync();
            log.Information("MassTransit bus stopped");
        }

        log.Information("Shutdown complete");

        if (statusHttp is not null)
            await statusHttp.DisposeAsync();

        watcher?.Dispose();
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
    --ui                    Start web dashboard (http://localhost:8080)
    --serve-status          Enable status monitoring endpoint
    --backfill              Run historical data backfill
    --replay <path>         Replay events from JSONL file
    --selftest              Run system self-tests
    --validate-config       Validate configuration without starting
    --help, -h              Show this help message

OPTIONS:
    --config <path>         Path to configuration file (default: appsettings.json)
    --http-port <port>      HTTP server port (default: 8080)
    --status-port <port>    Status endpoint port
    --watch-config          Enable hot-reload of configuration

BACKFILL OPTIONS:
    --backfill-provider <name>      Provider to use (default: stooq)
    --backfill-symbols <list>       Comma-separated symbols (e.g., AAPL,MSFT)
    --backfill-from <date>          Start date (YYYY-MM-DD)
    --backfill-to <date>            End date (YYYY-MM-DD)

EXAMPLES:
    # Start web dashboard on default port
    MarketDataCollector --ui

    # Start web dashboard on custom port
    MarketDataCollector --ui --http-port 9000

    # Production mode with status endpoint and hot-reload
    MarketDataCollector --serve-status --watch-config

    # Run historical backfill
    MarketDataCollector --backfill --backfill-symbols AAPL,MSFT,GOOGL \\
        --backfill-from 2024-01-01 --backfill-to 2024-12-31

    # Run self-tests
    MarketDataCollector --selftest

    # Validate configuration without starting
    MarketDataCollector --validate-config

    # Validate a specific configuration file
    MarketDataCollector --validate-config --config /path/to/config.json

CONFIGURATION:
    Configuration is loaded from appsettings.json in the current directory.
    Copy appsettings.sample.json to appsettings.json to get started.

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
║  Quick Start: ./MarketDataCollector --ui                             ║
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

    private static AppConfig LoadConfig(string path)
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
        catch (UnauthorizedAccessException ex)
        {
            throw new ConfigurationException(
                $"Access denied reading configuration file: {path}. Check file permissions.",
                path, null);
        }
        catch (IOException ex)
        {
            throw new ConfigurationException(
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
    /// Creates backfill providers based on configuration.
    /// </summary>
    private static List<IHistoricalDataProvider> CreateBackfillProviders(AppConfig cfg, ILogger log)
    {
        var backfillCfg = cfg.Backfill;
        var providersCfg = backfillCfg?.Providers;
        var providers = new List<IHistoricalDataProvider>();

        // Alpaca Markets (highest priority when configured - reliable API with adjustments)
        var alpacaCfg = providersCfg?.Alpaca;
        if (alpacaCfg?.Enabled ?? true)
        {
            // Only add if credentials are available (env vars or config)
            var keyId = alpacaCfg?.KeyId ?? Environment.GetEnvironmentVariable("ALPACA_KEY_ID");
            var secretKey = alpacaCfg?.SecretKey ?? Environment.GetEnvironmentVariable("ALPACA_SECRET_KEY");

            if (!string.IsNullOrEmpty(keyId) && !string.IsNullOrEmpty(secretKey))
            {
                providers.Add(new AlpacaHistoricalDataProvider(
                    keyId: alpacaCfg?.KeyId,
                    secretKey: alpacaCfg?.SecretKey,
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
            var polygonApiKey = polygonCfg?.ApiKey ?? Environment.GetEnvironmentVariable("POLYGON_API_KEY");
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
            var tiingoToken = tiingoCfg?.ApiToken ?? Environment.GetEnvironmentVariable("TIINGO_API_TOKEN");
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
            var finnhubApiKey = finnhubCfg?.ApiKey ?? Environment.GetEnvironmentVariable("FINNHUB_API_KEY");
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
            var alphaVantageApiKey = alphaVantageCfg?.ApiKey ?? Environment.GetEnvironmentVariable("ALPHA_VANTAGE_API_KEY");
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
            providers.Add(new NasdaqDataLinkHistoricalDataProvider(
                apiKey: nasdaqCfg?.ApiKey,
                database: nasdaqCfg?.Database ?? "WIKI",
                log: log
            ));
        }

        // Sort by priority (lower = tried first)
        return providers
            .OrderBy(p => p is IHistoricalDataProviderV2 v2 ? v2.Priority : 100)
            .ToList();
    }
}
