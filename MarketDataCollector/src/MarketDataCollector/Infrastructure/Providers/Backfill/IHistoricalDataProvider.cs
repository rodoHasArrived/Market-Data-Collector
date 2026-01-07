using MarketDataCollector.Domain.Models;
using System.Threading;

namespace MarketDataCollector.Infrastructure.Providers.Backfill;

/// <summary>
/// Contract for fetching historical bars from a vendor.
/// </summary>
public interface IHistoricalDataProvider
{
    string Name { get; }
    string DisplayName { get; }
    string Description { get; }

    Task<IReadOnlyList<HistoricalBar>> GetDailyBarsAsync(string symbol, DateOnly? from, DateOnly? to, CancellationToken ct = default);
}
