using System.Threading.Channels;
using IBDataCollector.Domain.Events;
using IBDataCollector.Storage.Interfaces;

namespace IBDataCollector.Application.Pipeline;

/// <summary>
/// High-throughput, backpressured pipeline that decouples producers from storage sinks.
/// </summary>
public sealed class EventPipeline : IMarketEventPublisher, IAsyncDisposable
{
    private readonly Channel<MarketEvent> _channel;
    private readonly IStorageSink _sink;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _consumer;

    public EventPipeline(IStorageSink sink, int capacity = 100_000, BoundedChannelFullMode fullMode = BoundedChannelFullMode.DropOldest)
    {
        _sink = sink ?? throw new ArgumentNullException(nameof(sink));

        _channel = Channel.CreateBounded<MarketEvent>(new BoundedChannelOptions(capacity)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = fullMode
        });

        _consumer = Task.Run(ConsumeAsync);
    }

    public bool TryPublish(in MarketEvent evt) => _channel.Writer.TryWrite(evt);

    public ValueTask PublishAsync(MarketEvent evt, CancellationToken ct = default)
        => _channel.Writer.WriteAsync(evt, ct);

    public void Complete() => _channel.Writer.TryComplete();

    private async Task ConsumeAsync()
    {
        try
        {
            await foreach (var evt in _channel.Reader.ReadAllAsync(_cts.Token).ConfigureAwait(false))
            {
                await _sink.AppendAsync(evt, _cts.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            try { await _sink.FlushAsync(CancellationToken.None).ConfigureAwait(false); } catch { }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        _channel.Writer.TryComplete();
        try { await _consumer.ConfigureAwait(false); } catch { }
        _cts.Dispose();
        await _sink.DisposeAsync().ConfigureAwait(false);
    }
}
