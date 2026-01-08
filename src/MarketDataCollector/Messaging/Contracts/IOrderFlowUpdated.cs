namespace MarketDataCollector.Messaging.Contracts;

/// <summary>
/// Published with rolling order flow statistics.
/// </summary>
public interface IOrderFlowUpdated : IMarketEventMessage
{
    /// <summary>
    /// Total buy volume in the window.
    /// </summary>
    long BuyVolume { get; }

    /// <summary>
    /// Total sell volume in the window.
    /// </summary>
    long SellVolume { get; }

    /// <summary>
    /// Net volume (buy - sell).
    /// </summary>
    long NetVolume { get; }

    /// <summary>
    /// Volume-weighted average price.
    /// </summary>
    decimal Vwap { get; }

    /// <summary>
    /// Number of trades in the window.
    /// </summary>
    int TradeCount { get; }

    /// <summary>
    /// Start of the statistics window.
    /// </summary>
    DateTimeOffset WindowStart { get; }

    /// <summary>
    /// End of the statistics window.
    /// </summary>
    DateTimeOffset WindowEnd { get; }
}
