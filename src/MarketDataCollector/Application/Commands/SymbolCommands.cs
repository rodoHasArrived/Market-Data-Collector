using MarketDataCollector.Application.Subscriptions.Services;
using Serilog;

namespace MarketDataCollector.Application.Commands;

/// <summary>
/// Handles all symbol management CLI commands:
/// --symbols, --symbols-monitored, --symbols-archived, --symbols-add, --symbols-remove, --symbol-status
/// </summary>
internal sealed class SymbolCommands : ICliCommand
{
    private readonly SymbolManagementService _symbolService;
    private readonly ILogger _log;

    public SymbolCommands(SymbolManagementService symbolService, ILogger log)
    {
        _symbolService = symbolService;
        _log = log;
    }

    public bool CanHandle(string[] args)
    {
        return args.Any(a =>
            a.Equals("--symbols", StringComparison.OrdinalIgnoreCase) ||
            a.Equals("--symbols-monitored", StringComparison.OrdinalIgnoreCase) ||
            a.Equals("--symbols-archived", StringComparison.OrdinalIgnoreCase) ||
            a.Equals("--symbols-add", StringComparison.OrdinalIgnoreCase) ||
            a.Equals("--symbols-remove", StringComparison.OrdinalIgnoreCase) ||
            a.Equals("--symbol-status", StringComparison.OrdinalIgnoreCase));
    }

    public async Task<int> ExecuteAsync(string[] args, CancellationToken ct = default)
    {
        if (CliArguments.HasFlag(args, "--symbols"))
        {
            await _symbolService.DisplayAllSymbolsAsync(ct);
            return 0;
        }

        if (CliArguments.HasFlag(args, "--symbols-monitored"))
        {
            var result = _symbolService.GetMonitoredSymbols();
            _symbolService.DisplayMonitoredSymbols(result);
            return 0;
        }

        if (CliArguments.HasFlag(args, "--symbols-archived"))
        {
            var result = await _symbolService.GetArchivedSymbolsAsync(ct: ct);
            _symbolService.DisplayArchivedSymbols(result);
            return 0;
        }

        if (CliArguments.HasFlag(args, "--symbols-add"))
            return await RunAddAsync(args, ct);

        if (CliArguments.HasFlag(args, "--symbols-remove"))
            return await RunRemoveAsync(args, ct);

        if (CliArguments.HasFlag(args, "--symbol-status"))
            return await RunStatusAsync(args, ct);

        return 1;
    }

    private async Task<int> RunAddAsync(string[] args, CancellationToken ct)
    {
        var symbolsArg = CliArguments.GetValue(args, "--symbols-add");
        if (string.IsNullOrWhiteSpace(symbolsArg))
        {
            Console.Error.WriteLine("Error: --symbols-add requires a comma-separated list of symbols");
            Console.Error.WriteLine("Example: --symbols-add AAPL,MSFT,GOOGL");
            return 1;
        }

        var symbolsToAdd = symbolsArg.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var options = new SymbolAddOptions(
            SubscribeTrades: !CliArguments.HasFlag(args, "--no-trades"),
            SubscribeDepth: !CliArguments.HasFlag(args, "--no-depth"),
            DepthLevels: int.TryParse(CliArguments.GetValue(args, "--depth-levels"), out var levels) ? levels : 10,
            UpdateExisting: CliArguments.HasFlag(args, "--update")
        );

        var result = await _symbolService.AddSymbolsAsync(symbolsToAdd, options, ct);
        Console.WriteLine();
        Console.WriteLine(result.Success ? "Symbol Addition Result" : "Symbol Addition Failed");
        Console.WriteLine(new string('=', 50));
        Console.WriteLine($"  {result.Message}");
        if (result.AffectedSymbols.Length > 0)
        {
            Console.WriteLine($"  Symbols: {string.Join(", ", result.AffectedSymbols)}");
        }
        Console.WriteLine();

        return result.Success ? 0 : 1;
    }

    private async Task<int> RunRemoveAsync(string[] args, CancellationToken ct)
    {
        var symbolsArg = CliArguments.GetValue(args, "--symbols-remove");
        if (string.IsNullOrWhiteSpace(symbolsArg))
        {
            Console.Error.WriteLine("Error: --symbols-remove requires a comma-separated list of symbols");
            Console.Error.WriteLine("Example: --symbols-remove AAPL,MSFT");
            return 1;
        }

        var symbolsToRemove = symbolsArg.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var result = await _symbolService.RemoveSymbolsAsync(symbolsToRemove, ct);

        Console.WriteLine();
        Console.WriteLine(result.Success ? "Symbol Removal Result" : "Symbol Removal Failed");
        Console.WriteLine(new string('=', 50));
        Console.WriteLine($"  {result.Message}");
        if (result.AffectedSymbols.Length > 0)
        {
            Console.WriteLine($"  Removed: {string.Join(", ", result.AffectedSymbols)}");
        }
        Console.WriteLine();

        return result.Success ? 0 : 1;
    }

    private async Task<int> RunStatusAsync(string[] args, CancellationToken ct)
    {
        var symbolArg = CliArguments.GetValue(args, "--symbol-status");
        if (string.IsNullOrWhiteSpace(symbolArg))
        {
            Console.Error.WriteLine("Error: --symbol-status requires a symbol");
            Console.Error.WriteLine("Example: --symbol-status AAPL");
            return 1;
        }

        var status = await _symbolService.GetSymbolStatusAsync(symbolArg, ct);

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
        return 0;
    }

    internal static string FormatBytes(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB", "TB"];
        var order = 0;
        double size = bytes;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        return $"{size:0.##} {sizes[order]}";
    }
}
