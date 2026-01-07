namespace MarketDataCollector.Integrations.Lean;

/// <summary>
/// Local stub interface matching QuantConnect's IDataProvider.
/// Used when QuantConnect.Lean.Engine.DataFeeds.IDataProvider is not accessible.
/// </summary>
public interface ILeanDataProvider : IDisposable
{
    /// <summary>
    /// Fetches data from the specified key.
    /// </summary>
    /// <param name="key">The file path or data key to fetch</param>
    /// <returns>Stream containing the data, or Stream.Null if not found</returns>
    Stream Fetch(string key);
}
