using MarketDataCollector.Application.Logging;
using MarketDataCollector.Contracts.Catalog;
using MarketDataCollector.Contracts.Domain.Enums;
using MarketDataCollector.Contracts.Domain.Models;
using MarketDataCollector.Domain.Events;
using Serilog;

using ContractPayload = MarketDataCollector.Contracts.Domain.Events.MarketEventPayload;

namespace MarketDataCollector.Application.Canonicalization;

/// <summary>
/// Default canonicalization implementation that resolves symbols, maps condition codes,
/// and normalizes venue identifiers using in-memory lookup tables.
/// Follows the <c>with</c> expression pattern established by <see cref="Domain.Events.MarketEvent.StampReceiveTime"/>.
/// </summary>
public sealed class EventCanonicalizer : IEventCanonicalizer
{
    private readonly ILogger _log = LoggingSetup.ForContext<EventCanonicalizer>();
    private readonly ICanonicalSymbolRegistry _symbols;
    private readonly ConditionCodeMapper _conditions;
    private readonly VenueMicMapper _venues;
    private readonly int _version;

    public EventCanonicalizer(
        ICanonicalSymbolRegistry symbols,
        ConditionCodeMapper conditions,
        VenueMicMapper venues,
        int version = 1)
    {
        _symbols = symbols ?? throw new ArgumentNullException(nameof(symbols));
        _conditions = conditions ?? throw new ArgumentNullException(nameof(conditions));
        _venues = venues ?? throw new ArgumentNullException(nameof(venues));
        _version = version;
    }

    /// <inheritdoc />
    public MarketEvent Canonicalize(MarketEvent raw, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(raw);

        // Skip heartbeats and already-canonicalized events
        if (raw.Type == MarketEventType.Heartbeat || raw.CanonicalizationVersion > 0)
            return raw;

        // Symbol resolution: use provider-aware resolution first, fall back to generic
        var canonicalSymbol = _symbols.ResolveToCanonical(raw.Symbol);

        // Venue normalization
        var rawVenue = ExtractVenue(raw.Payload);
        var canonicalVenue = _venues.TryMapVenue(rawVenue, raw.Source);

        return raw with
        {
            CanonicalSymbol = canonicalSymbol,
            CanonicalVenue = canonicalVenue,
            CanonicalizationVersion = _version,
            Tier = raw.Tier < MarketEventTier.Enriched ? MarketEventTier.Enriched : raw.Tier
        };
    }

    /// <summary>
    /// Extracts the venue string from a market event payload, if present.
    /// </summary>
    private static string? ExtractVenue(ContractPayload? payload) => payload switch
    {
        Trade trade => trade.Venue,
        BboQuotePayload bbo => bbo.Venue,
        LOBSnapshot lob => lob.Venue,
        L2SnapshotPayload l2 => l2.Venue,
        OrderFlowStatistics ofs => ofs.Venue,
        IntegrityEvent integrity => integrity.Venue,
        _ => null
    };
}
