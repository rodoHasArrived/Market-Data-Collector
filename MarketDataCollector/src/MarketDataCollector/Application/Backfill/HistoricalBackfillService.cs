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

        try
        {
            long barsWritten = 0;
            foreach (var symbol in symbols)
            {
                ct.ThrowIfCancellationRequested();
                _log.Information("Starting backfill for {Symbol} via {Provider}", symbol, provider.DisplayName);
                var bars = await provider.GetDailyBarsAsync(symbol, request.From, request.To, ct).ConfigureAwait(false);
                foreach (var bar in bars)
                {
                    var evt = MarketEvent.HistoricalBar(bar.ToTimestampUtc(), bar.Symbol, bar, bar.SequenceNumber, provider.Name);
                    await pipeline.PublishAsync(evt, ct).ConfigureAwait(false);
                    Metrics.IncHistoricalBars();
                    barsWritten++;
                }
            }

            await pipeline.FlushAsync(ct).ConfigureAwait(false);
            var completed = DateTimeOffset.UtcNow;
            _log.Information("Backfill complete: {Count} bars written across {Symbols}", barsWritten, symbols.Length);

            return new BackfillResult(true, provider.Name, symbols, request.From, request.To, barsWritten, started, completed, Error: null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.Error(ex, "Backfill failed for provider {Provider}", provider.Name);
            return BackfillResult.Failed(provider.Name, symbols, request.From, request.To, started, ex);
        }
    }
}
