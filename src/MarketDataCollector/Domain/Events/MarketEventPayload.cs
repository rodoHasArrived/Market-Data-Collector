using System.Text.Json.Serialization;
using MarketDataCollector.Contracts.Domain.Models;
using MarketDataCollector.Domain.Models;

namespace MarketDataCollector.Domain.Events;

/// <summary>
/// Polymorphic payload base for MarketEvent.Payload.
/// Models are consolidated in Contracts project as single source of truth.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(Trade), "trade")]
[JsonDerivedType(typeof(LOBSnapshot), "l2")]
[JsonDerivedType(typeof(OrderFlowStatistics), "orderflow")]
[JsonDerivedType(typeof(IntegrityEvent), "integrity")]
[JsonDerivedType(typeof(DepthIntegrityEvent), "depth_integrity")]
[JsonDerivedType(typeof(L2SnapshotPayload), "l2payload")]
[JsonDerivedType(typeof(BboQuotePayload), "bbo")]
[JsonDerivedType(typeof(HistoricalBar), "historical_bar")]
[JsonDerivedType(typeof(HistoricalQuote), "historical_quote")]
[JsonDerivedType(typeof(HistoricalTrade), "historical_trade")]
[JsonDerivedType(typeof(HistoricalAuction), "historical_auction")]
[JsonDerivedType(typeof(AggregateBar), "aggregate_bar")]
public abstract record MarketEventPayload : Contracts.Domain.Events.IMarketEventPayload;
