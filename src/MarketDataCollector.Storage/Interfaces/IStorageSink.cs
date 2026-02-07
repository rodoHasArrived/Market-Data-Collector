using MarketDataCollector.Application.Services;
using System.Threading;
using MarketDataCollector.Domain.Events;

namespace MarketDataCollector.Storage.Interfaces;

public interface IStorageSink : IAsyncDisposable, IFlushable
{
    ValueTask AppendAsync(MarketEvent evt, CancellationToken ct = default);
    new Task FlushAsync(CancellationToken ct = default);
}
