namespace MarketDataCollector.Messaging.Contracts;

/// <summary>
/// Published when a data integrity issue is detected.
/// </summary>
public interface IIntegrityEventOccurred : IMarketEventMessage
{
    /// <summary>
    /// Severity level: "Info", "Warning", "Error", "Critical".
    /// </summary>
    string Severity { get; }

    /// <summary>
    /// Description of the integrity issue.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Optional error code.
    /// </summary>
    int? ErrorCode { get; }

    /// <summary>
    /// Stream identifier where the issue was detected.
    /// </summary>
    string? StreamId { get; }

    /// <summary>
    /// Venue where the issue was detected.
    /// </summary>
    string? Venue { get; }
}
