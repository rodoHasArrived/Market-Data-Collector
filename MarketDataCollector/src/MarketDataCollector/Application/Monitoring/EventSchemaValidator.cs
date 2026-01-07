using MarketDataCollector.Domain.Events;

namespace MarketDataCollector.Application.Monitoring;

/// <summary>
/// Lightweight schema validation for <see cref="MarketEvent"/> instances before they are persisted.
/// This is intentionally fast and dependency-free but still enforces the documented contract
/// (timestamp, symbol, event type, payload presence, and schema version).
/// </summary>
public static class EventSchemaValidator
{
    /// <summary>
    /// Current schema version for serialized <see cref="MarketEvent"/> documents.
    /// </summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>
    /// Validates an event and throws <see cref="InvalidOperationException"/> when a contract violation is detected.
    /// </summary>
    public static void Validate(MarketEvent evt)
    {
        if (evt.Timestamp == default)
            throw new InvalidOperationException("Event timestamp is required.");

        if (string.IsNullOrWhiteSpace(evt.Symbol))
            throw new InvalidOperationException("Event symbol is required.");

        // Note: MarketEventType.Unknown removed from enum, checking for valid type using default value
        if (!Enum.IsDefined(typeof(MarketEventType), evt.Type))
            throw new InvalidOperationException("Event type must be specified.");

        if (evt.SchemaVersion != CurrentSchemaVersion)
            throw new InvalidOperationException($"Unsupported schema version {evt.SchemaVersion}. Expected {CurrentSchemaVersion}.");

        // Ensure payload aligns with event type for the most common cases.
        if (evt.Payload is null && evt.Type != MarketEventType.Heartbeat)
            throw new InvalidOperationException($"Event payload required for type {evt.Type}.");
    }
}
