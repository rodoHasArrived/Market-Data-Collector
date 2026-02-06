using System.Collections.Concurrent;
using System.Collections.Generic;
using MarketDataCollector.Contracts.Domain.Models;
using MarketDataCollector.Domain.Events;
using MarketDataCollector.Domain.Models;
using MarketDataCollector.Infrastructure.Utilities;

namespace MarketDataCollector.Domain.Collectors;

/// <summary>
/// Maintains per-symbol Best-Bid/Offer (BBO) state and emits BboQuote market events.
/// </summary>
public sealed class QuoteCollector : IQuoteStateStore
{
    private readonly IMarketEventPublisher _publisher;
    private readonly ProviderDataNormalizer? _normalizer;

    private readonly ConcurrentDictionary<string, BboQuotePayload> _latest = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, long> _seq = new(StringComparer.OrdinalIgnoreCase);

    public QuoteCollector(IMarketEventPublisher publisher, ProviderDataNormalizer? normalizer = null)
    {
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        _normalizer = normalizer;
    }

    /// <summary>
    /// Adapter entry point (e.g., Alpaca WebSocket quote updates).
    /// </summary>
    public void OnQuote(MarketQuoteUpdate update)
    {
        if (update is null) throw new ArgumentNullException(nameof(update));
        if (string.IsNullOrWhiteSpace(update.Symbol)) return;

        // Normalize symbol casing and timestamps to UTC
        if (_normalizer is not null)
            update = _normalizer.NormalizeQuote(update);

        var payload = Upsert(update);
        _publisher.TryPublish(MarketEvent.BboQuote(payload.Timestamp, payload.Symbol, payload));
    }

    public bool TryGet(string symbol, out BboQuotePayload? quote)
        => _latest.TryGetValue(symbol, out quote);

    public BboQuotePayload Upsert(MarketQuoteUpdate update)
    {
        if (update is null) throw new ArgumentNullException(nameof(update));
        if (string.IsNullOrWhiteSpace(update.Symbol))
            throw new ArgumentException("Symbol is required", nameof(update));

        var symbol = update.Symbol;

        // We keep our own monotonically increasing per-symbol sequence for quotes.
        var nextSeq = _seq.AddOrUpdate(symbol, _ => 1, (_, v) => v + 1);

        var payload = BboQuotePayload.FromUpdate(update, nextSeq);
        _latest[symbol] = payload;

        return payload;
    }

    public bool TryRemove(string symbol, out BboQuotePayload? removed)
    {
        var removedLatest = _latest.TryRemove(symbol, out removed);
        _seq.TryRemove(symbol, out _);

        return removedLatest;
    }

    public IReadOnlyDictionary<string, BboQuotePayload> Snapshot()
        => new Dictionary<string, BboQuotePayload>(_latest, StringComparer.OrdinalIgnoreCase);
}
