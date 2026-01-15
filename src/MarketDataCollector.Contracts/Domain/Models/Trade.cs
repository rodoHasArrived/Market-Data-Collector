using MarketDataCollector.Contracts.Domain.Enums;
using MarketDataCollector.Contracts.Domain.Events;

namespace MarketDataCollector.Contracts.Domain.Models;

/// <summary>
/// Immutable tick-by-tick trade record.
/// </summary>
public sealed record Trade : MarketEventPayload
{
    /// <summary>
    /// Gets the timestamp when the trade was executed.
    /// </summary>
    public DateTimeOffset Timestamp { get; }

    /// <summary>
    /// Gets the ticker symbol for the security.
    /// </summary>
    public string Symbol { get; }

    /// <summary>
    /// Gets the execution price of the trade.
    /// </summary>
    public decimal Price { get; }

    /// <summary>
    /// Gets the number of shares traded.
    /// </summary>
    public long Size { get; }

    /// <summary>
    /// Gets the side that initiated the trade (buyer or seller).
    /// </summary>
    public AggressorSide Aggressor { get; }

    /// <summary>
    /// Gets the sequence number for ordering events.
    /// </summary>
    public long SequenceNumber { get; }

    /// <summary>
    /// Gets the stream identifier for data source tracking.
    /// </summary>
    public string? StreamId { get; }

    /// <summary>
    /// Gets the trading venue or exchange identifier.
    /// </summary>
    public string? Venue { get; }

    /// <summary>
    /// Validates trade data at construction time to prevent corrupt datasets.
    /// </summary>
    public Trade(
        DateTimeOffset Timestamp,
        string Symbol,
        decimal Price,
        long Size,
        AggressorSide Aggressor,
        long SequenceNumber,
        string? StreamId = null,
        string? Venue = null)
    {
        if (Price <= 0)
            throw new ArgumentOutOfRangeException(nameof(Price), Price, "Price must be greater than 0");

        if (Size < 0)
            throw new ArgumentOutOfRangeException(nameof(Size), Size, "Size must be greater than or equal to 0");

        if (string.IsNullOrWhiteSpace(Symbol))
            throw new ArgumentException("Symbol cannot be null or whitespace", nameof(Symbol));

        this.Timestamp = Timestamp;
        this.Symbol = Symbol;
        this.Price = Price;
        this.Size = Size;
        this.Aggressor = Aggressor;
        this.SequenceNumber = SequenceNumber;
        this.StreamId = StreamId;
        this.Venue = Venue;
    }
}
