using System.Text.Json.Serialization;
using MarketDataCollector.Domain.Models;

namespace MarketDataCollector.Domain.Events;

/// <summary>
/// Polymorphic payload base for MarketEvent.Payload.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(Trade), "trade")]
[JsonDerivedType(typeof(LOBSnapshot), "l2")]
[JsonDerivedType(typeof(OrderFlowStatistics), "orderflow")]
[JsonDerivedType(typeof(IntegrityEvent), "integrity")]
[JsonDerivedType(typeof(DepthIntegrityEvent), "depth_integrity")]
[JsonDerivedType(typeof(L2SnapshotPayload), "l2payload")]
[JsonDerivedType(typeof(BboQuotePayload), "bbo")]
public abstract record MarketEventPayload : IMarketEventPayload;
