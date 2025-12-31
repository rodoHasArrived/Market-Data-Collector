using System.Collections.Concurrent;
using IBDataCollector.Domain.Events;
using IBDataCollector.Domain.Models;

namespace IBDataCollector.Domain.Collectors;

/// <summary>
/// Maintains per-symbol Best-Bid/Offer (BBO) state and emits BboQuote market events.
/// </summary>
public sealed class QuoteCollector : IQuoteStateStore
{
    private readonly IMarketEventPublisher _publisher;

    private readonly ConcurrentDictionary<string, BboQuotePayload> _latest = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, long> _seq = new(StringComparer.OrdinalIgnoreCase);

    public QuoteCollector(IMarketEventPublisher publisher)
    {
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
    }

    /// <summary>
    /// Adapter entry point (e.g., Alpaca WebSocket quote updates).
    /// </summary>
    public void OnQuote(MarketQuoteUpdate update)
    {
        if (update is null) throw new ArgumentNullException(nameof(update));
        if (string.IsNullOrWhiteSpace(update.Symbol)) return;

        var symbol = update.Symbol;

        // We keep our own monotonically increasing per-symbol sequence for quotes.
        var nextSeq = _seq.AddOrUpdate(symbol, _ => 1, (_, v) => v + 1);

        var payload = BboQuotePayload.FromUpdate(update, nextSeq);
        _latest[symbol] = payload;

        _publisher.TryPublish(MarketEvent.BboQuote(payload.Timestamp, payload.Symbol, payload));
    }

    public bool TryGet(string symbol, out BboQuotePayload quote)
        => _latest.TryGetValue(symbol, out quote);
}
