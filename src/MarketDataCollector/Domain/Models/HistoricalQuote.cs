using MarketDataCollector.Domain.Events;

namespace MarketDataCollector.Domain.Models;

/// <summary>
/// Historical NBBO (National Best Bid and Offer) quote data from Alpaca Markets API.
/// Represents the best bid and ask prices available across all exchanges at a point in time.
/// </summary>
public sealed record HistoricalQuote : MarketEventPayload
{
    public string Symbol { get; }
    public DateTimeOffset Timestamp { get; }
    public string AskExchange { get; }
    public decimal AskPrice { get; }
    public long AskSize { get; }
    public string BidExchange { get; }
    public decimal BidPrice { get; }
    public long BidSize { get; }
    public IReadOnlyList<string>? Conditions { get; }
    public string? Tape { get; }
    public string Source { get; }
    public long SequenceNumber { get; }

    public HistoricalQuote(
        string Symbol,
        DateTimeOffset Timestamp,
        string AskExchange,
        decimal AskPrice,
        long AskSize,
        string BidExchange,
        decimal BidPrice,
        long BidSize,
        IReadOnlyList<string>? Conditions = null,
        string? Tape = null,
        string Source = "alpaca",
        long SequenceNumber = 0)
    {
        if (string.IsNullOrWhiteSpace(Symbol))
            throw new ArgumentException("Symbol is required", nameof(Symbol));

        if (AskPrice < 0)
            throw new ArgumentOutOfRangeException(nameof(AskPrice), "Ask price cannot be negative.");

        if (BidPrice < 0)
            throw new ArgumentOutOfRangeException(nameof(BidPrice), "Bid price cannot be negative.");

        if (AskSize < 0)
            throw new ArgumentOutOfRangeException(nameof(AskSize), "Ask size cannot be negative.");

        if (BidSize < 0)
            throw new ArgumentOutOfRangeException(nameof(BidSize), "Bid size cannot be negative.");

        this.Symbol = Symbol;
        this.Timestamp = Timestamp;
        this.AskExchange = AskExchange;
        this.AskPrice = AskPrice;
        this.AskSize = AskSize;
        this.BidExchange = BidExchange;
        this.BidPrice = BidPrice;
        this.BidSize = BidSize;
        this.Conditions = Conditions;
        this.Tape = Tape;
        this.Source = Source;
        this.SequenceNumber = SequenceNumber;
    }

    /// <summary>
    /// Calculated spread (Ask - Bid).
    /// </summary>
    public decimal Spread => AskPrice - BidPrice;

    /// <summary>
    /// Calculated mid-price ((Ask + Bid) / 2).
    /// </summary>
    public decimal MidPrice => (AskPrice + BidPrice) / 2m;

    /// <summary>
    /// Spread as a percentage of the mid-price.
    /// </summary>
    public decimal? SpreadBps => MidPrice > 0 ? (Spread / MidPrice) * 10000m : null;
}
