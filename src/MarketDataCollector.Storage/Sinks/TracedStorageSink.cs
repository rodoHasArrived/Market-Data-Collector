using System.Diagnostics;
using MarketDataCollector.Domain.Events;
using MarketDataCollector.Infrastructure.Contracts;
using MarketDataCollector.Storage.Interfaces;

namespace MarketDataCollector.Storage.Sinks;

/// <summary>
/// Decorates an <see cref="IStorageSink"/> with OpenTelemetry-compatible distributed tracing.
/// Propagates trace context from provider ingestion through to storage writes, enabling
/// end-to-end observability of the event pipeline.
/// </summary>
/// <remarks>
/// This is the G2 remainder (end-to-end distributed tracing) component from the project roadmap.
/// Each append operation starts a storage activity with the event's symbol, type, and source
/// as tags. Flush operations are traced as batch operations. Errors are recorded on the
/// activity for correlation in observability backends.
/// </remarks>
[ImplementsAdr("ADR-012", "Distributed tracing from provider through storage")]
public sealed class TracedStorageSink : IStorageSink
{
    private static readonly ActivitySource StorageActivitySource = new("MarketDataCollector.Storage", "1.0.0");

    private readonly IStorageSink _inner;
    private readonly string _sinkName;
    private long _appendCount;
    private long _flushCount;
    private long _errorCount;

    /// <summary>
    /// Creates a traced storage sink wrapping the specified inner sink.
    /// </summary>
    /// <param name="inner">The underlying storage sink to delegate to.</param>
    /// <param name="sinkName">Descriptive name for this sink (used in trace tags).</param>
    public TracedStorageSink(IStorageSink inner, string? sinkName = null)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _sinkName = sinkName ?? inner.GetType().Name;
    }

    /// <summary>Total events appended through this traced sink.</summary>
    public long AppendCount => Interlocked.Read(ref _appendCount);

    /// <summary>Total flush operations performed.</summary>
    public long FlushCount => Interlocked.Read(ref _flushCount);

    /// <summary>Total errors recorded.</summary>
    public long ErrorCount => Interlocked.Read(ref _errorCount);

    /// <summary>Gets the name used for trace identification.</summary>
    public string SinkName => _sinkName;

    public async ValueTask AppendAsync(MarketEvent evt, CancellationToken ct = default)
    {
        using var activity = StorageActivitySource.StartActivity(
            $"Storage.Append.{_sinkName}",
            ActivityKind.Producer);

        if (activity is not null)
        {
            activity.SetTag("storage.sink", _sinkName);
            activity.SetTag("market.symbol", evt.Symbol);
            activity.SetTag("event.type", evt.Type.ToString());
            activity.SetTag("event.source", evt.Source);
            activity.SetTag("event.sequence", evt.Sequence);
            activity.SetTag("event.timestamp", evt.Timestamp.ToString("O"));
        }

        try
        {
            await _inner.AppendAsync(evt, ct).ConfigureAwait(false);
            Interlocked.Increment(ref _appendCount);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Interlocked.Increment(ref _errorCount);

            if (activity is not null)
            {
                activity.SetStatus(ActivityStatusCode.Error, ex.Message);
                activity.AddException(ex);
            }

            throw;
        }
    }

    public async Task FlushAsync(CancellationToken ct = default)
    {
        using var activity = StorageActivitySource.StartActivity(
            $"Storage.Flush.{_sinkName}",
            ActivityKind.Internal);

        if (activity is not null)
        {
            activity.SetTag("storage.sink", _sinkName);
            activity.SetTag("storage.pending_appends", AppendCount);
        }

        try
        {
            await _inner.FlushAsync(ct).ConfigureAwait(false);
            Interlocked.Increment(ref _flushCount);

            activity?.SetTag("storage.flush_count", FlushCount);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Interlocked.Increment(ref _errorCount);

            if (activity is not null)
            {
                activity.SetStatus(ActivityStatusCode.Error, ex.Message);
                activity.AddException(ex);
            }

            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _inner.DisposeAsync().ConfigureAwait(false);
    }
}
