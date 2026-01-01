using System.Text.Json;
using MassTransit;
using MarketDataCollector.Application.Config;
using MarketDataCollector.Application.Logging;
using MarketDataCollector.Application.Monitoring;
using MarketDataCollector.Application.Subscriptions;
using MarketDataCollector.Application.Pipeline;
using MarketDataCollector.Application.Testing;
using MarketDataCollector.Domain.Collectors;
using MarketDataCollector.Domain.Events;
using MarketDataCollector.Domain.Models;
using MarketDataCollector.Infrastructure;
using MarketDataCollector.Infrastructure.Providers.InteractiveBrokers;
using MarketDataCollector.Infrastructure.Providers.Alpaca;
using MarketDataCollector.Infrastructure.Providers.Polygon;
using MarketDataCollector.Messaging.Configuration;
using MarketDataCollector.Messaging.Publishers;
using MarketDataCollector.Storage;
using MarketDataCollector.Storage.Policies;
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

        var replayPath = GetArgValue(args, "--replay");
        var statusPort = int.TryParse(GetArgValue(args, "--status-port"), out var parsedPort) ? parsedPort : 8080;

        var statusPath = Path.Combine(cfg.DataRoot, "_status", "status.json");
        await using var statusWriter = new StatusWriter(statusPath, () => LoadConfig(cfgPath));
        if (args.Any(a => a.Equals("--serve-status", StringComparison.OrdinalIgnoreCase)))
            statusWriter.Start(TimeSpan.FromSeconds(1));
        StatusHttpServer? statusHttp = null;

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
        await using IMarketDataClient dataClient = cfg.DataSource switch
        {
            DataSourceKind.Alpaca => new AlpacaMarketDataClient(tradeCollector, quoteCollector, cfg.Alpaca ?? throw new InvalidOperationException("Alpaca options required when DataSource=Alpaca")),
            DataSourceKind.Polygon => new PolygonMarketDataClient(publisher, tradeCollector, quoteCollector),
            _ => new IBMarketDataClient(publisher, tradeCollector, depthCollector)
        };

        await dataClient.ConnectAsync();

        var symbols = cfg.Symbols?.Length > 0
            ? cfg.Symbols
            : new[] { new SymbolConfig("SPY", SubscribeTrades: true, SubscribeDepth: true, DepthLevels: 10) };

        foreach (var s in symbols)
        {
            if (s.SubscribeDepth)
            {
                depthCollector.RegisterSubscription(s.Symbol);
                dataClient.SubscribeMarketDepth(s);
            }

            if (s.SubscribeTrades)
                dataClient.SubscribeTrades(s);
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
            return new AppConfig();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Error] Failed to load configuration: {ex.Message}");
            Console.Error.WriteLine("Using default configuration.");
            return new AppConfig();
        }
    }

    private sealed class PipelinePublisher : IMarketEventPublisher
    {
        private readonly EventPipeline _pipeline;

        public PipelinePublisher(EventPipeline pipeline) => _pipeline = pipeline;

        public bool TryPublish(MarketEvent evt)
        {
            var ok = _pipeline.TryPublish(evt);
            if (ok) Metrics.IncPublished();
            else Metrics.IncDropped();

            if (evt.Type == MarketEventType.Integrity) Metrics.IncIntegrity();
            return ok;
        }
    }
}
