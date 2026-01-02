namespace DataIngestion.Contracts.Messages;

/// <summary>
/// Message for raw quote/BBO ingestion.
/// </summary>
public interface IRawQuoteIngested : ISymbolIngestionMessage
{
    decimal BidPrice { get; }
    long BidSize { get; }
    decimal AskPrice { get; }
    long AskSize { get; }
    string? BidExchange { get; }
    string? AskExchange { get; }
    string? Conditions { get; }
}

/// <summary>
/// Message for validated and enriched quote data.
/// </summary>
public interface IValidatedQuote : ISymbolIngestionMessage
{
    decimal BidPrice { get; }
    long BidSize { get; }
    decimal AskPrice { get; }
    long AskSize { get; }
    decimal Spread { get; }
    decimal SpreadBps { get; }
    decimal MidPrice { get; }
    bool IsValid { get; }
    string[] ValidationErrors { get; }
    bool IsCrossed { get; }
    bool IsLocked { get; }
}

/// <summary>
/// Command to ingest a batch of quotes.
/// </summary>
public interface IIngestQuotesBatch : IIngestionMessage
{
    string Symbol { get; }
    IReadOnlyList<QuoteData> Quotes { get; }
}

/// <summary>
/// Quote data record for batch processing.
/// </summary>
public record QuoteData(
    DateTimeOffset Timestamp,
    decimal BidPrice,
    long BidSize,
    decimal AskPrice,
    long AskSize,
    string? BidExchange = null,
    string? AskExchange = null,
    string? Conditions = null
);

/// <summary>
/// NBBO (National Best Bid and Offer) update message.
/// </summary>
public interface INbboUpdate : ISymbolIngestionMessage
{
    decimal BidPrice { get; }
    long BidSize { get; }
    string BidExchange { get; }
    decimal AskPrice { get; }
    long AskSize { get; }
    string AskExchange { get; }
    decimal Spread { get; }
    decimal MidPrice { get; }
}
