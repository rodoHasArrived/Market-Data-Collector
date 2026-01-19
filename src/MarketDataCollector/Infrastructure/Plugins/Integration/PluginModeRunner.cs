using MarketDataCollector.Application.Config;
using MarketDataCollector.Infrastructure.Plugins.Core;
using MarketDataCollector.Infrastructure.Plugins.Discovery;
using MarketDataCollector.Infrastructure.Plugins.Storage;
using Microsoft.Extensions.Logging;
using Serilog;
using ILogger = Serilog.ILogger;

namespace MarketDataCollector.Infrastructure.Plugins.Integration;

/// <summary>
/// Runs the application in plugin mode, using the new unified plugin architecture.
///
/// This replaces the legacy flow:
/// - Old: IMarketDataClient + IHistoricalDataProvider + collectors
/// - New: IMarketDataPlugin + PluginOrchestrator + IMarketDataStore
///
/// Usage in Program.cs:
/// <code>
/// if (args.Contains("--plugin-mode"))
/// {
///     await PluginModeRunner.RunAsync(cfg, args, log);
///     return;
/// }
/// </code>
/// </summary>
public static class PluginModeRunner
{
    /// <summary>
    /// Runs the application using the new plugin architecture.
    /// </summary>
    public static async Task RunAsync(AppConfig cfg, string[] args, ILogger log)
    {
        log.Information("Starting in plugin mode (new unified architecture)");

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            log.Information("Shutdown requested...");
            cts.Cancel();
        };

        // Create logger factory for plugins
        var loggerFactory = CreateLoggerFactory();

        // Create plugin registry and scan for plugins
        var registry = new PluginRegistry(
            new ServiceProviderStub(loggerFactory),
            loggerFactory.CreateLogger<PluginRegistry>());

        registry.ScanAssembly(typeof(PluginModeRunner).Assembly);

        // Log discovered plugins
        var plugins = registry.GetAll();
        log.Information("Discovered {Count} plugins:", plugins.Count);
        foreach (var p in plugins)
        {
            var status = p.IsConfigured ? "[OK]" : "[--]";
            log.Information("  {Status} {Id,-15} {DisplayName,-25} [{Type}]",
                status, p.Id, p.DisplayName, p.Type);
        }

        // Check if any plugins are configured
        var configuredPlugins = plugins.Where(p => p.IsConfigured).ToList();
        if (configuredPlugins.Count == 0)
        {
            log.Warning("No plugins are configured. Set environment variables to enable plugins:");
            log.Warning("  ALPACA__KEY_ID, ALPACA__SECRET_KEY (for Alpaca)");
            log.Warning("  IB__HOST, IB__PORT (for Interactive Brokers)");
            log.Warning("  YAHOO__USER_AGENT (optional, for Yahoo Finance)");
            return;
        }

        // Create storage
        var storeOptions = new StoreOptions
        {
            DataPath = cfg.DataRoot ?? "./data",
            Compress = cfg.Compress,
            BufferSize = 1000,
            FlushInterval = TimeSpan.FromSeconds(5)
        };

        await using var store = new FileSystemStore(storeOptions, loggerFactory.CreateLogger<FileSystemStore>());

        // Create orchestrator
        await using var orchestrator = new PluginOrchestrator(
            registry,
            store,
            loggerFactory.CreateLogger<PluginOrchestrator>());

        // Determine mode based on args
        if (args.Any(a => a.Equals("--backfill", StringComparison.OrdinalIgnoreCase)))
        {
            await RunBackfillAsync(args, cfg, orchestrator, log, cts.Token);
        }
        else
        {
            await RunRealtimeAsync(cfg, orchestrator, log, cts.Token);
        }

        log.Information("Plugin mode completed");
    }

    private static async Task RunRealtimeAsync(
        AppConfig cfg,
        PluginOrchestrator orchestrator,
        ILogger log,
        CancellationToken ct)
    {
        // Get symbols from config
        var symbols = cfg.Symbols?.Select(s => s.Symbol).ToList()
            ?? new List<string> { "SPY", "AAPL" };

        log.Information("Starting real-time streaming for {Count} symbols: {Symbols}",
            symbols.Count, string.Join(", ", symbols.Take(5)));

        var request = DataStreamRequest.Realtime(symbols.ToArray());

        try
        {
            var eventCount = 0;
            var startTime = DateTimeOffset.UtcNow;

            await foreach (var evt in orchestrator.StreamAsync(request, ct))
            {
                eventCount++;

                // Log periodic updates
                if (eventCount % 1000 == 0)
                {
                    var elapsed = DateTimeOffset.UtcNow - startTime;
                    var rate = eventCount / elapsed.TotalSeconds;
                    log.Information("Received {Count} events ({Rate:F1}/s)", eventCount, rate);
                }

                // Log significant events
                if (evt is TradeEvent trade && trade.Size > 1000)
                {
                    log.Debug("{Symbol} Trade: {Price:F2} x {Size} @ {Timestamp}",
                        trade.Symbol, trade.Price, trade.Size, trade.Timestamp);
                }
            }

            log.Information("Streaming completed: {Count} events received", eventCount);
        }
        catch (OperationCanceledException)
        {
            log.Information("Streaming cancelled");
        }
    }

    private static async Task RunBackfillAsync(
        string[] args,
        AppConfig cfg,
        PluginOrchestrator orchestrator,
        ILogger log,
        CancellationToken ct)
    {
        // Parse backfill args
        var symbols = GetArgValue(args, "--backfill-symbols")?.Split(',').ToList()
            ?? cfg.Symbols?.Select(s => s.Symbol).ToList()
            ?? new List<string> { "SPY" };

        var fromStr = GetArgValue(args, "--backfill-from");
        var toStr = GetArgValue(args, "--backfill-to");

        var from = !string.IsNullOrEmpty(fromStr)
            ? DateOnly.Parse(fromStr)
            : DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-1));

        var to = !string.IsNullOrEmpty(toStr)
            ? DateOnly.Parse(toStr)
            : DateOnly.FromDateTime(DateTime.UtcNow);

        log.Information("Starting backfill for {Count} symbols from {From} to {To}",
            symbols.Count, from, to);

        var progress = new Progress<BackfillProgress>(p =>
        {
            log.Information("Backfill progress: {Current}/{Total} ({Percent:F1}%) - {Symbol}",
                p.CompletedSymbols, p.TotalSymbols, p.PercentComplete, p.CurrentSymbol);
        });

        try
        {
            await orchestrator.BackfillAsync(symbols, from, to, "1day", progress, ct);
            log.Information("Backfill completed successfully");
        }
        catch (OperationCanceledException)
        {
            log.Warning("Backfill cancelled");
        }
        catch (Exception ex)
        {
            log.Error(ex, "Backfill failed");
        }
    }

    private static string? GetArgValue(string[] args, string key)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i].Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }
        return null;
    }

    private static ILoggerFactory CreateLoggerFactory()
    {
        return LoggerFactory.Create(builder =>
        {
            builder.AddSerilog(Log.Logger);
            builder.SetMinimumLevel(LogLevel.Debug);
        });
    }
}

/// <summary>
/// Minimal service provider stub for plugin registry.
/// </summary>
internal sealed class ServiceProviderStub : IServiceProvider
{
    private readonly ILoggerFactory _loggerFactory;

    public ServiceProviderStub(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
    }

    public object? GetService(Type serviceType)
    {
        if (serviceType == typeof(ILoggerFactory))
            return _loggerFactory;

        if (serviceType.IsGenericType && serviceType.GetGenericTypeDefinition() == typeof(ILogger<>))
        {
            var loggerType = serviceType.GetGenericArguments()[0];
            var method = typeof(LoggerFactoryExtensions).GetMethod("CreateLogger",
                new[] { typeof(ILoggerFactory) });
            var genericMethod = method?.MakeGenericMethod(loggerType);
            return genericMethod?.Invoke(null, new object[] { _loggerFactory });
        }

        return null;
    }
}
