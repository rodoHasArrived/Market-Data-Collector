using System.Linq;
using System.Threading;
using MarketDataCollector.Application.Logging;
using MarketDataCollector.Application.Monitoring;
using MarketDataCollector.Application.Pipeline;
using MarketDataCollector.Domain.Events;
using MarketDataCollector.Infrastructure.Providers.Backfill;
using Serilog;

namespace MarketDataCollector.Application.Backfill;

/// <summary>
/// Orchestrates historical backfills from free/public data providers into the storage pipeline.
/// </summary>
public sealed class HistoricalBackfillService
{
    private readonly IReadOnlyDictionary<string, IHistoricalDataProvider> _providers;
    private readonly ILogger _log;

    public HistoricalBackfillService(IEnumerable<IHistoricalDataProvider> providers, ILogger? logger = null)
    {
        _providers = providers.ToDictionary(p => p.Name.ToLowerInvariant());
        _log = logger ?? LoggingSetup.ForContext<HistoricalBackfillService>();
    }

    public IReadOnlyCollection<IHistoricalDataProvider> Providers => _providers.Values.ToList();

    public async Task<BackfillResult> RunAsync(BackfillRequest request, EventPipeline pipeline, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(pipeline);

        var started = DateTimeOffset.UtcNow;
        var symbols = request.Symbols?.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).ToArray() ?? Array.Empty<string>();
        if (symbols.Length == 0)
            throw new InvalidOperationException("At least one symbol is required for backfill.");

        if (!_providers.TryGetValue(request.Provider.ToLowerInvariant(), out var provider))
            throw new InvalidOperationException($"Unknown backfill provider '{request.Provider}'.");

        long barsWritten = 0;
        var perSymbolResults = new List<SymbolBackfillResult>();

        foreach (var symbol in symbols)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                _log.Information("Starting backfill for {Symbol} via {Provider}", symbol, provider.DisplayName);
                long symbolBars = 0;
                var bars = await provider.GetDailyBarsAsync(symbol, request.From, request.To, ct).ConfigureAwait(false);
                foreach (var bar in bars)
                {
                    var evt = MarketEvent.HistoricalBar(bar.ToTimestampUtc(), bar.Symbol, bar, bar.SequenceNumber, provider.Name);
                    await pipeline.PublishAsync(evt, ct).ConfigureAwait(false);
                    Metrics.IncHistoricalBars();
                    symbolBars++;
                }
                barsWritten += symbolBars;
                perSymbolResults.Add(new SymbolBackfillResult(symbol, true, symbolBars));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _log.Error(ex, "Backfill failed for symbol {Symbol} via {Provider}", symbol, provider.Name);
                perSymbolResults.Add(new SymbolBackfillResult(symbol, false, 0, ex.Message));
            }
        }

        try
        {
            await pipeline.FlushAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.Error(ex, "Pipeline flush failed after backfill for provider {Provider}", provider.Name);
        }

        var completed = DateTimeOffset.UtcNow;
        var allSucceeded = perSymbolResults.All(r => r.Success);
        var failedSymbols = perSymbolResults.Where(r => !r.Success).Select(r => r.Symbol).ToArray();

        if (failedSymbols.Length > 0)
        {
            _log.Warning("Backfill completed with {FailedCount}/{TotalCount} symbol failures: {FailedSymbols}",
                failedSymbols.Length, symbols.Length, failedSymbols);
        }
        else
        {
            _log.Information("Backfill complete: {Count} bars written across {Symbols}", barsWritten, symbols.Length);
        }

        var errorSummary = failedSymbols.Length > 0
            ? $"Failed symbols: {string.Join(", ", failedSymbols)}"
            : null;

        return new BackfillResult(allSucceeded, provider.Name, symbols, request.From, request.To, barsWritten, started, completed, errorSummary, perSymbolResults);
    }
}
