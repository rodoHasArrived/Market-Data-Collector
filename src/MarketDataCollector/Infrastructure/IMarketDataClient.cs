using MarketDataCollector.Application.Config;
using MarketDataCollector.Infrastructure.Contracts;
using System.Threading;

namespace MarketDataCollector.Infrastructure;

/// <summary>
/// Market data client abstraction for provider-agnostic market data ingestion.
/// Implementations must be non-blocking on publish paths.
/// </summary>
/// <remarks>
/// This interface is the core contract for ADR-001 (Provider Abstraction Pattern).
/// All streaming data providers must implement this interface.
///
/// <para>
/// <strong>DEPRECATION NOTICE:</strong> This interface is being replaced by the unified
/// <see cref="Plugins.Core.IMarketDataPlugin"/> interface which provides a simpler,
/// more consistent API for both real-time and historical data sources.
/// Use <c>--plugin-mode</c> flag to run with the new architecture.
/// </para>
/// </remarks>
[ImplementsAdr("ADR-001", "Core streaming data provider contract")]
[ImplementsAdr("ADR-004", "All async methods support CancellationToken")]
[Obsolete("Use IMarketDataPlugin from Infrastructure.Plugins.Core instead. Run with --plugin-mode to use the new architecture.")]
public interface IMarketDataClient : IAsyncDisposable
{
    bool IsEnabled { get; }

    Task ConnectAsync(CancellationToken ct = default);
    Task DisconnectAsync(CancellationToken ct = default);

    /// <summary>Subscribe to market depth for the symbol described by cfg.</summary>
    int SubscribeMarketDepth(SymbolConfig cfg);

    /// <summary>Unsubscribe a previously returned depth subscription id.</summary>
    void UnsubscribeMarketDepth(int subscriptionId);

    /// <summary>Subscribe to tick-by-tick trade prints for the symbol described by cfg.</summary>
    int SubscribeTrades(SymbolConfig cfg);

    /// <summary>Unsubscribe a previously returned trade subscription id.</summary>
    void UnsubscribeTrades(int subscriptionId);
}
