using MarketDataCollector.Application.Monitoring;
using MarketDataCollector.Contracts.Catalog;
using MarketDataCollector.Contracts.Domain.Enums;
using MarketDataCollector.Contracts.Domain.Models;
using MarketDataCollector.Domain.Events;
using Serilog;

namespace MarketDataCollector.Application.Canonicalization;

/// <summary>
/// Default implementation of <see cref="IEventCanonicalizer"/> that resolves symbols,
/// maps condition codes, and normalizes venue identifiers using in-memory lookup tables.
/// Runs synchronously before <c>EventPipeline.PublishAsync()</c> to avoid adding latency
/// to the high-throughput sink path.
/// </summary>
public sealed class EventCanonicalizer : IEventCanonicalizer
{
    private readonly ICanonicalSymbolRegistry _symbols;
    private readonly ConditionCodeMapper _conditions;
    private readonly VenueMicMapper _venues;
    private readonly int _version;
    private readonly ILogger _log = Log.ForContext<EventCanonicalizer>();

    /// <summary>
    /// Initializes a new instance of <see cref="EventCanonicalizer"/>.
    /// </summary>
    /// <param name="symbols">Symbol registry for canonical symbol resolution.</param>
    /// <param name="conditions">Condition code mapper for trade condition normalization.</param>
    /// <param name="venues">Venue mapper for ISO 10383 MIC normalization.</param>
    /// <param name="version">Canonicalization version to stamp on enriched events.</param>
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
        // Hard-fail: missing required identity fields
        if (string.IsNullOrWhiteSpace(raw.Symbol))
        {
            CanonicalizationMetrics.RecordHardFail(raw.Source, raw.Type.ToString());
            return raw;
        }

        var canonicalSymbol = TryResolveSymbol(raw.Symbol, raw.Source);
        var rawVenue = ExtractVenue(raw.Payload);
        var canonicalVenue = _venues.TryMapVenue(rawVenue, raw.Source);

        // Track metrics
        if (canonicalSymbol is null)
        {
            CanonicalizationMetrics.RecordUnresolved(raw.Source, "symbol");
        }
        if (rawVenue is not null && canonicalVenue is null)
        {
            CanonicalizationMetrics.RecordUnresolved(raw.Source, "venue");
        }
        CanonicalizationMetrics.RecordSuccess(raw.Source, raw.Type.ToString());

        return raw with
        {
            CanonicalSymbol = canonicalSymbol ?? raw.Symbol,
            CanonicalVenue = canonicalVenue,
            CanonicalizationVersion = _version,
            Tier = raw.Tier < MarketEventTier.Enriched ? MarketEventTier.Enriched : raw.Tier
        };
    }

    /// <summary>
    /// Resolves a raw symbol to its canonical form using provider hint for disambiguation.
    /// Falls back to direct registry lookup without provider context.
    /// </summary>
    private string? TryResolveSymbol(string rawSymbol, string provider)
    {
        // First try provider-specific resolution
        var resolved = _symbols.TryResolveWithProvider(rawSymbol, provider);
        if (resolved is not null)
            return resolved;

        // Fall back to provider-agnostic resolution
        return _symbols.ResolveToCanonical(rawSymbol);
    }

    /// <summary>
    /// Extracts the venue string from a market event payload via pattern matching.
    /// Returns <c>null</c> if the payload type doesn't carry a venue field.
    /// </summary>
    private static string? ExtractVenue(MarketDataCollector.Contracts.Domain.Events.MarketEventPayload? payload)
    {
        return payload switch
        {
            Trade t => t.Venue,
            BboQuotePayload q => q.Venue,
            LOBSnapshot l => l.Venue,
            L2SnapshotPayload lp => lp.Venue,
            OrderFlowStatistics o => o.Venue,
            IntegrityEvent i => i.Venue,
            DepthIntegrityEvent d => d.Venue,
            _ => null
        };
    }
}
