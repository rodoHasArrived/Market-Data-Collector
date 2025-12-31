using IBDataCollector.Domain.Events;

namespace IBDataCollector.Storage.Interfaces;

public interface IStorageSink : IAsyncDisposable
{
    ValueTask AppendAsync(MarketEvent evt, CancellationToken ct = default);
    Task FlushAsync(CancellationToken ct = default);
}
