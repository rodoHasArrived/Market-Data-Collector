using MarketDataCollector.Domain.Events;
using MarketDataCollector.Storage.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace MarketDataCollector.Storage.Sinks;

/// <summary>
/// Fans out events to multiple storage sinks, enabling multi-format storage
/// (e.g., JSONL + Parquet simultaneously) without modifying the EventPipeline.
/// </summary>
public sealed class CompositeSink : IStorageSink
{
    private readonly IReadOnlyList<IStorageSink> _sinks;
    private readonly ILogger<CompositeSink> _logger;

    public CompositeSink(IEnumerable<IStorageSink> sinks, ILogger<CompositeSink>? logger = null)
    {
        _sinks = sinks?.ToList() ?? throw new ArgumentNullException(nameof(sinks));
        _logger = logger ?? NullLogger<CompositeSink>.Instance;

        if (_sinks.Count == 0)
            throw new ArgumentException("At least one sink must be provided.", nameof(sinks));
    }

    /// <summary>Gets the number of underlying sinks.</summary>
    public int SinkCount => _sinks.Count;

    public async ValueTask AppendAsync(MarketEvent evt, CancellationToken ct = default)
    {
        for (var i = 0; i < _sinks.Count; i++)
        {
            try
            {
                await _sinks[i].AppendAsync(evt, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex,
                    "Sink {SinkIndex}/{SinkCount} ({SinkType}) failed to append event for {Symbol}",
                    i + 1, _sinks.Count, _sinks[i].GetType().Name, evt.Symbol);
            }
        }
    }

    public async Task FlushAsync(CancellationToken ct = default)
    {
        var exceptions = new List<Exception>();

        for (var i = 0; i < _sinks.Count; i++)
        {
            try
            {
                await _sinks[i].FlushAsync(ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex,
                    "Sink {SinkIndex}/{SinkCount} ({SinkType}) failed to flush",
                    i + 1, _sinks.Count, _sinks[i].GetType().Name);
                exceptions.Add(ex);
            }
        }

        if (exceptions.Count > 0)
        {
            throw new AggregateException("One or more sinks failed to flush.", exceptions);
        }
    }

    public async ValueTask DisposeAsync()
    {
        for (var i = 0; i < _sinks.Count; i++)
        {
            try
            {
                await _sinks[i].DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex,
                    "Sink {SinkIndex}/{SinkCount} ({SinkType}) failed during disposal",
                    i + 1, _sinks.Count, _sinks[i].GetType().Name);
            }
        }
    }
}
