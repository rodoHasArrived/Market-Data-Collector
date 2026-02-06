namespace MarketDataCollector.Infrastructure.Providers.Core;

/// <summary>
/// Types of market data providers.
/// </summary>
public enum ProviderType
{
    /// <summary>Real-time streaming data provider.</summary>
    Streaming,

    /// <summary>Historical backfill data provider.</summary>
    Backfill,

    /// <summary>Symbol search/lookup provider.</summary>
    SymbolSearch
}
