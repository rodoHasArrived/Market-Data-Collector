using MarketDataCollector.Domain.Events;

namespace MarketDataCollector.Domain.Models;

/// <summary>
/// Historical trade data from Alpaca Markets API.
/// Represents a single executed trade at a specific exchange.
/// </summary>
public sealed record HistoricalTrade : MarketEventPayload
{
    public string Symbol { get; }
    public DateTimeOffset Timestamp { get; }
    public string Exchange { get; }
    public decimal Price { get; }
    public long Size { get; }
    public string TradeId { get; }
    public IReadOnlyList<string>? Conditions { get; }
    public string? Tape { get; }
    public string Source { get; }
    public long SequenceNumber { get; }

    public HistoricalTrade(
        string Symbol,
        DateTimeOffset Timestamp,
        string Exchange,
        decimal Price,
        long Size,
        string TradeId,
        IReadOnlyList<string>? Conditions = null,
        string? Tape = null,
        string Source = "alpaca",
        long SequenceNumber = 0)
    {
        if (string.IsNullOrWhiteSpace(Symbol))
            throw new ArgumentException("Symbol is required", nameof(Symbol));

        if (Price <= 0)
            throw new ArgumentOutOfRangeException(nameof(Price), "Price must be greater than zero.");

        if (Size <= 0)
            throw new ArgumentOutOfRangeException(nameof(Size), "Size must be greater than zero.");

        if (string.IsNullOrWhiteSpace(TradeId))
            throw new ArgumentException("Trade ID is required", nameof(TradeId));

        this.Symbol = Symbol;
        this.Timestamp = Timestamp;
        this.Exchange = Exchange;
        this.Price = Price;
        this.Size = Size;
        this.TradeId = TradeId;
        this.Conditions = Conditions;
        this.Tape = Tape;
        this.Source = Source;
        this.SequenceNumber = SequenceNumber;
    }

    /// <summary>
    /// Notional value of the trade (Price * Size).
    /// </summary>
    public decimal NotionalValue => Price * Size;
}
