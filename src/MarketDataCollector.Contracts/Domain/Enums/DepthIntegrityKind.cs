namespace MarketDataCollector.Contracts.Domain.Enums;

/// <summary>
/// Kind of depth integrity issue detected.
/// </summary>
public enum DepthIntegrityKind
{
    /// <summary>
    /// Unknown integrity issue.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Sequence gap detected.
    /// </summary>
    Gap = 1,

    /// <summary>
    /// Out-of-order message received.
    /// </summary>
    OutOfOrder = 2,

    /// <summary>
    /// Invalid position in order book.
    /// </summary>
    InvalidPosition = 3,

    /// <summary>
    /// Stale data detected.
    /// </summary>
    Stale = 4
}
