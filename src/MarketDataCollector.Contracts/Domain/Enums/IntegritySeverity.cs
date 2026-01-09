namespace MarketDataCollector.Contracts.Domain.Enums;

/// <summary>
/// Severity level for data integrity events.
/// </summary>
public enum IntegritySeverity
{
    /// <summary>
    /// Informational event, no action required.
    /// </summary>
    Info = 0,

    /// <summary>
    /// Warning event, may require attention.
    /// </summary>
    Warning = 1,

    /// <summary>
    /// Error event, requires immediate attention.
    /// </summary>
    Error = 2
}
