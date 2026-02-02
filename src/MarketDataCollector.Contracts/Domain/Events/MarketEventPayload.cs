using System.Text.Json.Serialization;
using MarketDataCollector.Contracts.Domain.Models;

namespace MarketDataCollector.Contracts.Domain.Events;

/// <summary>
/// Polymorphic payload base for MarketEvent.Payload.
/// Supports JSON serialization with type discriminator.
/// </summary>
#if !UWP_BUILD
// Note: [JsonPolymorphic] attribute not supported by WinUI 3 XAML compiler (net472-based)
// When building for UWP, these attributes are excluded via conditional compilation
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
[JsonDerivedType(typeof(AggregateBarPayload), "aggregate_bar")]
#endif
public abstract record MarketEventPayload : IMarketEventPayload;
