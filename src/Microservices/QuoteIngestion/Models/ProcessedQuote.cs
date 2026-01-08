namespace DataIngestion.QuoteService.Models;

public record ProcessedQuote
{
    public Guid MessageId { get; init; } = Guid.NewGuid();
    public Guid CorrelationId { get; init; }
    public required string Symbol { get; init; }
    public DateTimeOffset Timestamp { get; init; }
    public decimal BidPrice { get; init; }
    public long BidSize { get; init; }
    public decimal AskPrice { get; init; }
    public long AskSize { get; init; }
    public string? BidExchange { get; init; }
    public string? AskExchange { get; init; }
    public string? Source { get; init; }
    public long Sequence { get; init; }
    public DateTimeOffset ReceivedAt { get; init; } = DateTimeOffset.UtcNow;

    // Enriched fields
    public decimal Spread { get; init; }
    public decimal SpreadBps { get; init; }
    public decimal MidPrice { get; init; }
    public bool IsCrossed { get; init; }
    public bool IsLocked { get; init; }
}
