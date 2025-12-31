using System.Text.Json;
using MarketDataCollector.Application.Config;
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
using MarketDataCollector.Storage;
using MarketDataCollector.Storage.Policies;
using MarketDataCollector.Storage.Sinks;

namespace MarketDataCollector;

internal static class Program
{
    public static async Task Main(string[] args)
    {
        if (args.Any(a => a.Equals("--selftest", StringComparison.OrdinalIgnoreCase)))
        {
            DepthBufferSelfTests.Run();
            Console.WriteLine("Self-tests passed.");
            return;
        }

        var cfgPath = "appsettings.json";
        var cfg = LoadConfig(cfgPath);

        var statusPath = Path.Combine(cfg.DataRoot, "_status", "status.json");
        await using var statusWriter = new StatusWriter(statusPath, () => LoadConfig(cfgPath));
        if (args.Any(a => a.Equals("--serve-status", StringComparison.OrdinalIgnoreCase)))
            statusWriter.Start(TimeSpan.FromSeconds(1));

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
        Console.WriteLine($"Storage path: {storageOpt.RootPath}");
        Console.WriteLine($"Naming convention: {storageOpt.NamingConvention}");
        Console.WriteLine($"Date partitioning: {storageOpt.DatePartition}");
        Console.WriteLine($"Compression: {(storageOpt.Compress ? "enabled" : "disabled")}");
        Console.WriteLine($"Example path: {policy.GetPathPreview()}");
        Console.WriteLine();

        // Publisher adapter
        IMarketEventPublisher publisher = new PipelinePublisher(pipeline);

        // Collectors
        var quoteCollector = new QuoteCollector(publisher);
        var tradeCollector = new TradeDataCollector(publisher, quoteCollector);
        var depthCollector = new MarketDepthCollector(publisher, requireExplicitSubscription: true);

        // Market data client (provider selected by config)
        await using IMarketDataClient dataClient = cfg.DataSource == DataSourceKind.Alpaca
            ? new AlpacaMarketDataClient(tradeCollector, quoteCollector, cfg.Alpaca ?? throw new InvalidOperationException("Alpaca options required when DataSource=Alpaca"))
            : new IBMarketDataClient(publisher, tradeCollector, depthCollector);

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

        Console.WriteLine($"Wrote MarketEvents to ./{storageOpt.RootPath}/");
        Console.WriteLine($"Metrics: published={Metrics.Published}, integrity={Metrics.Integrity}, dropped={Metrics.Dropped}");

        if (args.Any(a => a.Equals("--serve-status", StringComparison.OrdinalIgnoreCase)))
        {
            Console.WriteLine("Status serving enabled (writing data/_status/status.json). Press Ctrl+C to stop.");
            var done = new TaskCompletionSource();
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                done.TrySetResult();
            };
            await done.Task;
        }

        await dataClient.DisconnectAsync();
    }

    private static AppConfig LoadConfig(string path)
    {
        try
        {
            if (!File.Exists(path))
                return new AppConfig();

            var json = File.ReadAllText(path);
            var cfg = JsonSerializer.Deserialize<AppConfig>(json, AppConfigJsonOptions.Read);
            return cfg ?? new AppConfig();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Warning] Failed to load config: {ex.Message}");
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
