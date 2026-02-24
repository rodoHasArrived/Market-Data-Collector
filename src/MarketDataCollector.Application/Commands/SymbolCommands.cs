using MarketDataCollector.Application.ResultTypes;
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
            a.Equals("--symbol-status", StringComparison.OrdinalIgnoreCase) ||
            a.Equals("--symbols-import", StringComparison.OrdinalIgnoreCase) ||
            a.Equals("--symbols-export", StringComparison.OrdinalIgnoreCase));
    }

    public async Task<CliResult> ExecuteAsync(string[] args, CancellationToken ct = default)
    {
        if (CliArguments.HasFlag(args, "--symbols"))
        {
            await _symbolService.DisplayAllSymbolsAsync(ct);
            return CliResult.Ok();
        }

        if (CliArguments.HasFlag(args, "--symbols-monitored"))
        {
            var result = _symbolService.GetMonitoredSymbols();
            _symbolService.DisplayMonitoredSymbols(result);
            return CliResult.Ok();
        }

        if (CliArguments.HasFlag(args, "--symbols-archived"))
        {
            var result = await _symbolService.GetArchivedSymbolsAsync(ct: ct);
            _symbolService.DisplayArchivedSymbols(result);
            return CliResult.Ok();
        }

        if (CliArguments.HasFlag(args, "--symbols-add"))
            return await RunAddAsync(args, ct);

        if (CliArguments.HasFlag(args, "--symbols-remove"))
            return await RunRemoveAsync(args, ct);

        if (CliArguments.HasFlag(args, "--symbol-status"))
            return await RunStatusAsync(args, ct);

        if (CliArguments.HasFlag(args, "--symbols-import"))
            return await RunImportAsync(args, ct);

        if (CliArguments.HasFlag(args, "--symbols-export"))
            return RunExport(args);

        return CliResult.Fail(ErrorCode.Unknown);
    }

    private async Task<CliResult> RunAddAsync(string[] args, CancellationToken ct)
    {
        var symbolsToAdd = CliArguments.RequireList(args, "--symbols-add", "--symbols-add AAPL,MSFT,GOOGL");
        if (symbolsToAdd is null) return CliResult.Fail(ErrorCode.RequiredFieldMissing);
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

        return CliResult.FromBool(result.Success, ErrorCode.ValidationFailed);
    }

    private async Task<CliResult> RunRemoveAsync(string[] args, CancellationToken ct)
    {
        var symbolsToRemove = CliArguments.RequireList(args, "--symbols-remove", "--symbols-remove AAPL,MSFT");
        if (symbolsToRemove is null) return CliResult.Fail(ErrorCode.RequiredFieldMissing);
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

        return CliResult.FromBool(result.Success, ErrorCode.ValidationFailed);
    }

    private async Task<CliResult> RunStatusAsync(string[] args, CancellationToken ct)
    {
        var symbolArg = CliArguments.RequireValue(args, "--symbol-status", "--symbol-status AAPL");
        if (symbolArg is null) return CliResult.Fail(ErrorCode.RequiredFieldMissing);

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
        return CliResult.Ok();
    }

    /// <summary>
    /// Import symbols from a CSV or text file.
    /// Supports formats: one symbol per line, or CSV with symbol in the first column.
    /// Lines starting with # are treated as comments.
    /// </summary>
    private async Task<CliResult> RunImportAsync(string[] args, CancellationToken ct)
    {
        var filePath = CliArguments.RequireValue(args, "--symbols-import", "--symbols-import symbols.csv");
        if (filePath is null) return CliResult.Fail(ErrorCode.RequiredFieldMissing);

        if (!File.Exists(filePath))
        {
            Console.Error.WriteLine($"  Error: File not found: {filePath}");
            return CliResult.Fail(ErrorCode.FileNotFound);
        }

        var lines = await File.ReadAllLinesAsync(filePath, ct);
        var symbols = new List<string>();
        var skipped = 0;

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();

            // Skip empty lines and comments
            if (string.IsNullOrEmpty(line) || line.StartsWith('#'))
            {
                skipped++;
                continue;
            }

            // CSV: take first column (handles "AAPL,Stock,..." or just "AAPL")
            var symbol = line.Split(',', StringSplitOptions.TrimEntries)[0].Trim().ToUpperInvariant();

            // Skip header rows
            if (symbol.Equals("SYMBOL", StringComparison.OrdinalIgnoreCase) ||
                symbol.Equals("TICKER", StringComparison.OrdinalIgnoreCase))
            {
                skipped++;
                continue;
            }

            if (!string.IsNullOrEmpty(symbol) && !symbols.Contains(symbol))
            {
                symbols.Add(symbol);
            }
        }

        if (symbols.Count == 0)
        {
            Console.Error.WriteLine("  Error: No valid symbols found in file.");
            Console.Error.WriteLine("  Expected format: one symbol per line, or CSV with symbol in the first column.");
            return CliResult.Fail(ErrorCode.ValidationFailed);
        }

        Console.WriteLine();
        Console.WriteLine($"  Importing {symbols.Count} symbols from {Path.GetFileName(filePath)}...");

        var options = new SymbolAddOptions(
            SubscribeTrades: !CliArguments.HasFlag(args, "--no-trades"),
            SubscribeDepth: !CliArguments.HasFlag(args, "--no-depth"),
            DepthLevels: int.TryParse(CliArguments.GetValue(args, "--depth-levels"), out var levels) ? levels : 10,
            UpdateExisting: CliArguments.HasFlag(args, "--update")
        );

        var result = await _symbolService.AddSymbolsAsync(symbols.ToArray(), options, ct);

        Console.WriteLine();
        Console.WriteLine(result.Success ? "  Import Result" : "  Import Failed");
        Console.WriteLine("  " + new string('=', 50));
        Console.WriteLine($"  {result.Message}");
        Console.WriteLine($"  Symbols parsed: {symbols.Count}");
        Console.WriteLine($"  Lines skipped: {skipped}");
        if (result.AffectedSymbols.Length > 0)
        {
            Console.WriteLine($"  Added: {string.Join(", ", result.AffectedSymbols)}");
        }
        Console.WriteLine();

        _log.Information("Bulk symbol import from {File}: {Count} symbols, {Skipped} skipped",
            filePath, symbols.Count, skipped);

        return CliResult.FromBool(result.Success, ErrorCode.ValidationFailed);
    }

    /// <summary>
    /// Export current monitored symbols to a CSV file.
    /// </summary>
    private CliResult RunExport(string[] args)
    {
        var filePath = CliArguments.RequireValue(args, "--symbols-export", "--symbols-export symbols.csv");
        if (filePath is null) return CliResult.Fail(ErrorCode.RequiredFieldMissing);

        var result = _symbolService.GetMonitoredSymbols();
        var symbols = result.Symbols;

        if (symbols.Length == 0)
        {
            Console.Error.WriteLine("  No monitored symbols to export.");
            return CliResult.Fail(ErrorCode.NotFound);
        }

        using var writer = new StreamWriter(filePath);
        writer.WriteLine("# Market Data Collector - Symbol Export");
        writer.WriteLine($"# Exported: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        writer.WriteLine($"# Count: {symbols.Length}");
        writer.WriteLine("Symbol,SecurityType,SubscribeTrades,SubscribeDepth,DepthLevels,Exchange");

        foreach (var sym in symbols)
        {
            writer.WriteLine($"{sym.Symbol},{sym.SecurityType},{sym.SubscribeTrades},{sym.SubscribeDepth},{sym.DepthLevels},{sym.Exchange}");
        }

        Console.WriteLine();
        Console.WriteLine($"  Exported {symbols.Length} symbols to {filePath}");
        Console.WriteLine();

        _log.Information("Exported {Count} symbols to {File}", symbols.Length, filePath);

        return CliResult.Ok();
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
