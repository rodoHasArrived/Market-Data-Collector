using System.Text.Json.Serialization;
using MarketDataCollector.Contracts.Domain.Enums;
using MarketDataCollector.Contracts.Domain.Events;

namespace MarketDataCollector.Contracts.Domain.Models;

/// <summary>
/// Integrity event specific to market depth streams.
/// </summary>
public sealed record DepthIntegrityEvent(
    DateTimeOffset Timestamp,
    string Symbol,
    [property: JsonPropertyName("integrityKind")] DepthIntegrityKind Kind,
    string Description,
    int Position,
    DepthOperation Operation,
    OrderBookSide Side,
    long SequenceNumber,
    string? StreamId = null,
    string? Venue = null
) : MarketEventPayload;
