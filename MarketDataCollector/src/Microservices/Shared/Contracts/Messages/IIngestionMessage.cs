namespace DataIngestion.Contracts.Messages;

/// <summary>
/// Base interface for all data ingestion messages.
/// </summary>
public interface IIngestionMessage
{
    /// <summary>Unique message identifier for idempotency.</summary>
    Guid MessageId { get; }

    /// <summary>Correlation ID for tracking related messages across services.</summary>
    Guid CorrelationId { get; }

    /// <summary>Timestamp when the message was created.</summary>
    DateTimeOffset Timestamp { get; }

    /// <summary>Source system or provider that generated the data.</summary>
    string Source { get; }

    /// <summary>Schema version for forward/backward compatibility.</summary>
    int SchemaVersion { get; }
}

/// <summary>
/// Base interface for symbol-specific ingestion messages.
/// </summary>
public interface ISymbolIngestionMessage : IIngestionMessage
{
    /// <summary>The financial instrument symbol (e.g., AAPL, MSFT).</summary>
    string Symbol { get; }

    /// <summary>Sequence number for ordering events.</summary>
    long Sequence { get; }
}
