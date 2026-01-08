using MarketDataCollector.Domain.Models;
using MarketDataCollector.Infrastructure.Contracts;
using System.Threading;

namespace MarketDataCollector.Infrastructure.Providers.Backfill;

/// <summary>
/// Contract for fetching historical bars from a vendor.
/// </summary>
/// <remarks>
/// This interface is the core contract for ADR-001 (Provider Abstraction Pattern).
/// All historical data providers must implement this interface for backfill operations.
/// </remarks>
[ImplementsAdr("ADR-001", "Core historical data provider contract")]
[ImplementsAdr("ADR-004", "All async methods support CancellationToken")]
public interface IHistoricalDataProvider
{
    string Name { get; }
    string DisplayName { get; }
    string Description { get; }

    Task<IReadOnlyList<HistoricalBar>> GetDailyBarsAsync(string symbol, DateOnly? from, DateOnly? to, CancellationToken ct = default);
}
