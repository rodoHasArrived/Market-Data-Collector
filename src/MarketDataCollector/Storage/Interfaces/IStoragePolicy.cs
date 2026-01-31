

namespace MarketDataCollector.Storage.Interfaces;

public interface IStoragePolicy
{
    string GetPath(MarketEvent evt);
}
