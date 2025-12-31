using IBDataCollector.Domain.Events;

namespace IBDataCollector.Storage.Interfaces;

public interface IStoragePolicy
{
    string GetPath(MarketEvent evt);
}
