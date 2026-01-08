namespace MarketDataCollector.Messaging.Contracts;

/// <summary>
/// Published when a Level 2 order book snapshot is received.
/// </summary>
public interface IL2SnapshotReceived : IMarketEventMessage
{
    /// <summary>
    /// Bid levels (price, size) ordered from best to worst.
    /// </summary>
    IReadOnlyList<OrderBookLevelData> Bids { get; }

    /// <summary>
    /// Ask levels (price, size) ordered from best to worst.
    /// </summary>
    IReadOnlyList<OrderBookLevelData> Asks { get; }

    /// <summary>
    /// Total bid volume across all levels.
    /// </summary>
    long TotalBidVolume { get; }

    /// <summary>
    /// Total ask volume across all levels.
    /// </summary>
    long TotalAskVolume { get; }
}

/// <summary>
/// Single order book level data.
/// </summary>
public record OrderBookLevelData(decimal Price, long Size, string? MakerId = null);
