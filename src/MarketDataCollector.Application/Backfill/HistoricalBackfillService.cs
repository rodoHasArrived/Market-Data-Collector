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
    private readonly IEventMetrics _metrics;

    public HistoricalBackfillService(
        IEnumerable<IHistoricalDataProvider> providers,
        ILogger? logger = null,
        IEventMetrics? metrics = null)
    {
        _providers = providers.ToDictionary(p => p.Name.ToLowerInvariant());
        _log = logger ?? LoggingSetup.ForContext<HistoricalBackfillService>();
        _metrics = metrics ?? new DefaultEventMetrics();
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
        var failedSymbols = new List<string>();
        var errorMessages = new List<string>();

        foreach (var symbol in symbols)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                _log.Information("Starting backfill for {Symbol} via {Provider}", symbol, provider.DisplayName);
                var bars = await provider.GetDailyBarsAsync(symbol, request.From, request.To, ct).ConfigureAwait(false);
                foreach (var bar in bars)
                {
                    var evt = MarketEvent.HistoricalBar(bar.ToTimestampUtc(), bar.Symbol, bar, bar.SequenceNumber, provider.Name);
                    await pipeline.PublishAsync(evt, ct).ConfigureAwait(false);
                    _metrics.IncHistoricalBars();
                    barsWritten++;
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                _log.Error(ex, "Backfill failed for symbol {Symbol} via {Provider}, continuing with remaining symbols", symbol, provider.Name);
                failedSymbols.Add(symbol);
                errorMessages.Add($"{symbol}: {ex.Message}");
            }
        }

        try
        {
            await pipeline.FlushAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.Error(ex, "Pipeline flush failed after backfill");
        }

        var completed = DateTimeOffset.UtcNow;
        var allSucceeded = failedSymbols.Count == 0;
        var errorSummary = failedSymbols.Count > 0
            ? $"Failed symbols ({failedSymbols.Count}/{symbols.Length}): {string.Join("; ", errorMessages)}"
            : null;

        _log.Information("Backfill complete: {Count} bars written across {Total} symbols ({Failed} failed)",
            barsWritten, symbols.Length, failedSymbols.Count);

        return new BackfillResult(allSucceeded, provider.Name, symbols, request.From, request.To, barsWritten, started, completed, Error: errorSummary);
    }
}
