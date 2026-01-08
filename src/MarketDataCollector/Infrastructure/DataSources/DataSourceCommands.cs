using System.Text;
using MarketDataCollector.Application.Logging;
using Serilog;

namespace MarketDataCollector.Infrastructure.DataSources;

/// <summary>
/// CLI commands for managing data sources.
/// Provides list, test, validate, and status operations.
/// </summary>
public sealed class DataSourceCommands
{
    private readonly IDataSourceManager _manager;
    private readonly IFallbackDataSourceOrchestrator _orchestrator;
    private readonly UnifiedDataSourcesConfig _config;
    private readonly ILogger _log;

    public DataSourceCommands(
        IDataSourceManager manager,
        IFallbackDataSourceOrchestrator orchestrator,
        UnifiedDataSourcesConfig config,
        ILogger? logger = null)
    {
        _manager = manager;
        _orchestrator = orchestrator;
        _config = config;
        _log = logger ?? LoggingSetup.ForContext<DataSourceCommands>();
    }

    /// <summary>
    /// Executes a data source command from command line arguments.
    /// </summary>
    /// <param name="args">Command line arguments starting with "sources".</param>
    /// <returns>Exit code (0 for success).</returns>
    public async Task<int> ExecuteAsync(string[] args)
    {
        if (args.Length < 2)
        {
            ShowUsage();
            return 1;
        }

        var command = args[1].ToLowerInvariant();
        var remainingArgs = args.Skip(2).ToArray();

        return command switch
        {
            "list" => await ListSourcesAsync(),
            "test" => await TestSourceAsync(remainingArgs),
            "validate" => await ValidateSourcesAsync(),
            "capabilities" => await ShowCapabilitiesAsync(remainingArgs),
            "status" => await ShowStatusAsync(remainingArgs),
            "health" => await ShowHealthAsync(),
            "enable" => await SetEnabledAsync(remainingArgs, true),
            "disable" => await SetEnabledAsync(remainingArgs, false),
            "priority" => await SetPriorityAsync(remainingArgs),
            "fetch-history" => await FetchHistoryAsync(remainingArgs),
            "help" or "--help" or "-h" => ShowUsage(),
            _ => ShowUsage($"Unknown command: {command}")
        };
    }

    #region Commands

    private Task<int> ListSourcesAsync()
    {
        var sources = _manager.AllSources;

        Console.WriteLine();
        Console.WriteLine("Data Sources:");
        Console.WriteLine("─".PadRight(80, '─'));
        Console.WriteLine($"{"ID",-15} {"TYPE",-12} {"CATEGORY",-12} {"PRIORITY",-10} {"STATUS",-15} {"HEALTH",-10}");
        Console.WriteLine("─".PadRight(80, '─'));

        foreach (var source in sources.OrderBy(s => s.Priority))
        {
            var statusIcon = GetStatusIcon(source.Status);
            var healthStr = source.Status == DataSourceStatus.Disabled
                ? "N/A"
                : $"{source.Health.Score:F0}%";

            Console.WriteLine(
                $"{source.Id,-15} {source.Type,-12} {source.Category,-12} {source.Priority,-10} " +
                $"{statusIcon} {source.Status,-12} {healthStr,-10}");
        }

        Console.WriteLine();
        Console.WriteLine($"Total: {sources.Count} sources");
        return Task.FromResult(0);
    }

    private async Task<int> TestSourceAsync(string[] args)
    {
        if (args.Length == 0)
        {
            // Test all sources
            Console.WriteLine("\nTesting connectivity for all sources...\n");
            var result = await _manager.TestConnectivityAsync();

            foreach (var entry in result.Entries)
            {
                var icon = entry.IsConnected ? "✓" : "✗";
                var latency = entry.IsConnected ? $"(latency: {entry.ResponseTime.TotalMilliseconds:F0}ms)" : "";
                var error = entry.ErrorMessage != null ? $" - {entry.ErrorMessage}" : "";
                Console.WriteLine($"{icon} {entry.SourceId,-15} {latency}{error}");
            }

            return result.AllConnected ? 0 : 1;
        }

        var sourceId = args[0];
        var source = _manager.GetSource(sourceId);

        if (source == null)
        {
            Console.WriteLine($"Error: Unknown source '{sourceId}'");
            return 1;
        }

        Console.WriteLine($"\nTesting connectivity to {source.DisplayName}...");

        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var isConnected = await source.TestConnectivityAsync();
            sw.Stop();

            if (isConnected)
            {
                Console.WriteLine($"✓ {source.DisplayName} connectivity test passed (latency: {sw.ElapsedMilliseconds}ms)");
                return 0;
            }
            else
            {
                Console.WriteLine($"✗ {source.DisplayName} connectivity test failed");
                return 1;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ {source.DisplayName} connectivity test failed: {ex.Message}");
            return 1;
        }
    }

    private async Task<int> ValidateSourcesAsync()
    {
        Console.WriteLine("\nValidating credentials for all sources...\n");

        var result = await _manager.ValidateAllAsync();

        foreach (var entry in result.Entries)
        {
            var icon = entry.IsValid ? "✓" : "✗";
            var message = entry.ErrorMessage ?? (entry.IsValid ? "Credentials valid" : "Invalid");
            Console.WriteLine($"{icon} {entry.SourceId,-15}: {message}");
        }

        Console.WriteLine();
        return result.AllValid ? 0 : 1;
    }

    private Task<int> ShowCapabilitiesAsync(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Usage: sources capabilities <source-id>");
            return Task.FromResult(1);
        }

        var sourceId = args[0];
        var source = _manager.GetSource(sourceId);

        if (source == null)
        {
            Console.WriteLine($"Error: Unknown source '{sourceId}'");
            return Task.FromResult(1);
        }

        Console.WriteLine();
        Console.WriteLine($"{source.DisplayName} Capabilities:");
        Console.WriteLine("─".PadRight(50, '─'));

        // Real-time capabilities
        var realtimeCaps = new List<string>();
        if (source.Capabilities.HasFlag(DataSourceCapabilities.RealtimeTrades))
            realtimeCaps.Add("Trades");
        if (source.Capabilities.HasFlag(DataSourceCapabilities.RealtimeQuotes))
            realtimeCaps.Add("Quotes");
        if (source.Capabilities.HasFlag(DataSourceCapabilities.RealtimeDepthL2))
            realtimeCaps.Add($"L2 Depth ({source.CapabilityInfo.MaxDepthLevels ?? 10} levels)");
        if (source.Capabilities.HasFlag(DataSourceCapabilities.RealtimeDepthL3))
            realtimeCaps.Add("L3 Depth (Full Book)");

        if (realtimeCaps.Count > 0)
            Console.WriteLine($"  Real-time: {string.Join(", ", realtimeCaps)}");

        // Historical capabilities
        var histCaps = new List<string>();
        if (source.Capabilities.HasFlag(DataSourceCapabilities.HistoricalDailyBars))
            histCaps.Add("Daily Bars");
        if (source.Capabilities.HasFlag(DataSourceCapabilities.HistoricalIntradayBars))
        {
            var intervals = source.CapabilityInfo.SupportedBarIntervals;
            var intervalStr = intervals?.Count > 0 ? $"Intraday ({string.Join(", ", intervals)})" : "Intraday";
            histCaps.Add(intervalStr);
        }
        if (source.Capabilities.HasFlag(DataSourceCapabilities.HistoricalAdjustedPrices))
            histCaps.Add("Adjusted Prices");
        if (source.Capabilities.HasFlag(DataSourceCapabilities.HistoricalDividends))
            histCaps.Add("Dividends");
        if (source.Capabilities.HasFlag(DataSourceCapabilities.HistoricalSplits))
            histCaps.Add("Splits");

        if (histCaps.Count > 0)
            Console.WriteLine($"  Historical: {string.Join(", ", histCaps)}");

        // Lookback
        if (source.CapabilityInfo.MaxHistoricalLookback.HasValue)
        {
            var lookback = source.CapabilityInfo.MaxHistoricalLookback.Value;
            var lookbackStr = lookback.TotalDays >= 365
                ? $"{lookback.TotalDays / 365:F0} years"
                : $"{lookback.TotalDays:F0} days";
            Console.WriteLine($"  Lookback: {lookbackStr}");
        }

        // Markets
        if (source.SupportedMarkets.Count > 0)
            Console.WriteLine($"  Markets: {string.Join(", ", source.SupportedMarkets)}");

        // Asset classes
        if (source.SupportedAssetClasses.Count > 0)
            Console.WriteLine($"  Asset Classes: {string.Join(", ", source.SupportedAssetClasses)}");

        Console.WriteLine();
        return Task.FromResult(0);
    }

    private Task<int> ShowStatusAsync(string[] args)
    {
        var allStatus = _manager.GetAllSourceStatus();

        if (args.Length > 0)
        {
            var sourceId = args[0];
            if (!allStatus.TryGetValue(sourceId, out var status))
            {
                Console.WriteLine($"Error: Unknown source '{sourceId}'");
                return Task.FromResult(1);
            }

            ShowDetailedStatus(status);
            return Task.FromResult(0);
        }

        Console.WriteLine("\nData Source Status:");
        Console.WriteLine("─".PadRight(80, '─'));

        foreach (var (_, status) in allStatus.OrderBy(kvp => kvp.Value.Priority))
        {
            ShowBriefStatus(status);
        }

        return Task.FromResult(0);
    }

    private Task<int> ShowHealthAsync()
    {
        var health = _manager.GetAggregatedHealth();

        Console.WriteLine();
        Console.WriteLine("Data Source Health Summary:");
        Console.WriteLine("─".PadRight(50, '─'));
        Console.WriteLine($"  Overall Status: {(health.IsHealthy ? "✓ Healthy" : "✗ Unhealthy")}");
        Console.WriteLine($"  Overall Score: {health.OverallScore:F1}%");
        Console.WriteLine($"  Healthy Sources: {health.HealthySources}/{health.TotalSources}");

        if (health.Message != null)
            Console.WriteLine($"  Note: {health.Message}");

        Console.WriteLine();

        // Show orchestrator status
        var orchStatus = _orchestrator.GetStatus();
        if (orchStatus.SourcesInCooldown.Count > 0)
        {
            Console.WriteLine("Sources in Cooldown:");
            foreach (var cooldown in orchStatus.SourcesInCooldown)
            {
                Console.WriteLine($"  - {cooldown.SourceId}: {cooldown.RemainingTime.TotalSeconds:F0}s remaining");
            }
        }

        return Task.FromResult(0);
    }

    private Task<int> SetEnabledAsync(string[] args, bool enabled)
    {
        if (args.Length == 0)
        {
            Console.WriteLine($"Usage: sources {(enabled ? "enable" : "disable")} <source-id>");
            return Task.FromResult(1);
        }

        var sourceId = args[0];
        var source = _manager.GetSource(sourceId);

        if (source == null)
        {
            Console.WriteLine($"Error: Unknown source '{sourceId}'");
            return Task.FromResult(1);
        }

        // Note: This requires runtime configuration update
        Console.WriteLine($"To {(enabled ? "enable" : "disable")} {sourceId}, update the configuration:");
        Console.WriteLine($"  DataSources:Sources:{sourceId}:Enabled = {enabled}");
        Console.WriteLine();
        Console.WriteLine("Changes will take effect on restart.");

        return Task.FromResult(0);
    }

    private Task<int> SetPriorityAsync(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Usage: sources priority <source-id> <new-priority>");
            return Task.FromResult(1);
        }

        var sourceId = args[0];
        if (!int.TryParse(args[1], out var newPriority))
        {
            Console.WriteLine("Error: Priority must be a number");
            return Task.FromResult(1);
        }

        var source = _manager.GetSource(sourceId);
        if (source == null)
        {
            Console.WriteLine($"Error: Unknown source '{sourceId}'");
            return Task.FromResult(1);
        }

        Console.WriteLine($"To change {sourceId} priority to {newPriority}, update the configuration:");
        Console.WriteLine($"  DataSources:Sources:{sourceId}:Priority = {newPriority}");
        Console.WriteLine();
        Console.WriteLine("Changes will take effect on restart.");

        return Task.FromResult(0);
    }

    private async Task<int> FetchHistoryAsync(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Usage: sources fetch-history <source-id> <symbol> [--from YYYY-MM-DD] [--to YYYY-MM-DD]");
            return 1;
        }

        var sourceId = args[0];
        var symbol = args[1];

        DateOnly? from = null;
        DateOnly? to = null;

        for (int i = 2; i < args.Length; i++)
        {
            if (args[i] == "--from" && i + 1 < args.Length)
            {
                if (DateOnly.TryParse(args[++i], out var fromDate))
                    from = fromDate;
            }
            else if (args[i] == "--to" && i + 1 < args.Length)
            {
                if (DateOnly.TryParse(args[++i], out var toDate))
                    to = toDate;
            }
        }

        var source = _manager.GetSource<IHistoricalDataSource>(sourceId);
        if (source == null)
        {
            Console.WriteLine($"Error: Unknown or non-historical source '{sourceId}'");
            return 1;
        }

        Console.WriteLine($"\nFetching historical data for {symbol} from {source.DisplayName}...");
        Console.WriteLine($"  Date range: {from?.ToString() ?? "earliest"} to {to?.ToString() ?? "latest"}");

        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var bars = await source.GetDailyBarsAsync(symbol, from, to);
            sw.Stop();

            Console.WriteLine($"✓ Retrieved {bars.Count} bars in {sw.ElapsedMilliseconds}ms");

            if (bars.Count > 0)
            {
                Console.WriteLine($"  First bar: {bars[0].SessionDate} (O:{bars[0].Open:F2} H:{bars[0].High:F2} L:{bars[0].Low:F2} C:{bars[0].Close:F2})");
                Console.WriteLine($"  Last bar:  {bars[^1].SessionDate} (O:{bars[^1].Open:F2} H:{bars[^1].High:F2} L:{bars[^1].Low:F2} C:{bars[^1].Close:F2})");
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Failed: {ex.Message}");
            return 1;
        }
    }

    #endregion

    #region Helpers

    private static int ShowUsage(string? error = null)
    {
        if (error != null)
        {
            Console.WriteLine($"Error: {error}");
            Console.WriteLine();
        }

        Console.WriteLine("Usage: MarketDataCollector sources <command> [options]");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  list                           List all registered data sources");
        Console.WriteLine("  test [source-id]               Test connectivity (all sources if no ID)");
        Console.WriteLine("  validate                       Validate credentials for all sources");
        Console.WriteLine("  capabilities <source-id>       Show capabilities of a source");
        Console.WriteLine("  status [source-id]             Show status (all sources if no ID)");
        Console.WriteLine("  health                         Show aggregated health status");
        Console.WriteLine("  enable <source-id>             Enable a data source");
        Console.WriteLine("  disable <source-id>            Disable a data source");
        Console.WriteLine("  priority <source-id> <value>   Set source priority");
        Console.WriteLine("  fetch-history <source-id> <symbol> [--from DATE] [--to DATE]");
        Console.WriteLine("                                 Fetch historical data from a source");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  sources list");
        Console.WriteLine("  sources test alpaca");
        Console.WriteLine("  sources capabilities yahoo");
        Console.WriteLine("  sources fetch-history yahoo SPY --from 2024-01-01 --to 2024-01-31");

        return error != null ? 1 : 0;
    }

    private static string GetStatusIcon(DataSourceStatus status)
    {
        return status switch
        {
            DataSourceStatus.Connected => "●",
            DataSourceStatus.Disconnected => "○",
            DataSourceStatus.Reconnecting => "◐",
            DataSourceStatus.RateLimited => "◑",
            DataSourceStatus.Disabled => "◌",
            DataSourceStatus.ConfigurationError => "✗",
            DataSourceStatus.Unavailable => "✗",
            _ => "?"
        };
    }

    private static void ShowBriefStatus(DataSourceStatusSummary status)
    {
        var icon = GetStatusIcon(status.Status);
        var health = status.Status == DataSourceStatus.Disabled ? "N/A" : $"{status.Health.Score:F0}%";
        var rateLimit = status.RateLimitState.CanMakeRequest
            ? $"{status.RateLimitState.RemainingRequests}/{status.RateLimitState.MaxRequests}"
            : "Throttled";

        Console.WriteLine($"  {icon} {status.DisplayName,-25} {status.Status,-15} Health: {health,-6} Rate: {rateLimit}");
    }

    private static void ShowDetailedStatus(DataSourceStatusSummary status)
    {
        Console.WriteLine();
        Console.WriteLine($"{status.DisplayName} ({status.Id}):");
        Console.WriteLine("─".PadRight(50, '─'));
        Console.WriteLine($"  Type: {status.Type}");
        Console.WriteLine($"  Category: {status.Category}");
        Console.WriteLine($"  Priority: {status.Priority}");
        Console.WriteLine($"  Status: {GetStatusIcon(status.Status)} {status.Status}");
        Console.WriteLine($"  Health Score: {status.Health.Score:F1}%");
        Console.WriteLine($"  Healthy: {(status.Health.IsHealthy ? "Yes" : "No")}");

        if (status.Health.Message != null)
            Console.WriteLine($"  Message: {status.Health.Message}");

        Console.WriteLine($"  Consecutive Failures: {status.Health.ConsecutiveFailures}");
        Console.WriteLine($"  Last Checked: {status.Health.LastChecked:yyyy-MM-dd HH:mm:ss}");

        if (status.Health.LastResponseTime.HasValue)
            Console.WriteLine($"  Last Response Time: {status.Health.LastResponseTime.Value.TotalMilliseconds:F0}ms");

        Console.WriteLine();
        Console.WriteLine("  Rate Limit State:");
        Console.WriteLine($"    Can Make Request: {(status.RateLimitState.CanMakeRequest ? "Yes" : "No")}");
        Console.WriteLine($"    Remaining: {status.RateLimitState.RemainingRequests}/{status.RateLimitState.MaxRequests}");

        if (status.RateLimitState.ResetIn.HasValue)
            Console.WriteLine($"    Reset In: {status.RateLimitState.ResetIn.Value.TotalSeconds:F0}s");

        Console.WriteLine();
    }

    #endregion
}

/// <summary>
/// Extension methods for integrating data source commands with the main program.
/// </summary>
public static class DataSourceCommandsExtensions
{
    /// <summary>
    /// Checks if the arguments contain a data source command.
    /// </summary>
    public static bool IsDataSourceCommand(this string[] args)
    {
        return args.Length > 0 &&
            args[0].Equals("sources", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Creates a DataSourceCommands instance from a service provider.
    /// </summary>
    public static DataSourceCommands CreateDataSourceCommands(
        IDataSourceManager manager,
        IFallbackDataSourceOrchestrator orchestrator,
        UnifiedDataSourcesConfig config)
    {
        return new DataSourceCommands(manager, orchestrator, config);
    }
}
