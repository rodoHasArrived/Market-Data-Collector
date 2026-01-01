using MarketDataCollector.Domain.Events;

namespace MarketDataCollector.Domain.Models;

/// <summary>
/// Immutable tick-by-tick trade record.
/// </summary>
public sealed record Trade(
    DateTimeOffset Timestamp,
    string Symbol,
    decimal Price,
    long Size,
    AggressorSide Aggressor,
    long SequenceNumber,
    string? StreamId = null,
    string? Venue = null
) : MarketEventPayload
{
    /// <summary>
    /// Validates trade data at construction time to prevent corrupt datasets.
    /// </summary>
    public Trade
    {
        if (Price <= 0)
            throw new ArgumentOutOfRangeException(nameof(Price), Price, "Price must be greater than 0");

        if (Size < 0)
            throw new ArgumentOutOfRangeException(nameof(Size), Size, "Size must be greater than or equal to 0");

        if (string.IsNullOrWhiteSpace(Symbol))
            throw new ArgumentException("Symbol cannot be null or whitespace", nameof(Symbol));
    }
}
