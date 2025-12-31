using System.Collections.Concurrent;
using IBDataCollector.Application.Config;
using IBDataCollector.Domain.Collectors;
using IBDataCollector.Infrastructure.IB;

namespace IBDataCollector.Application.Subscriptions;

/// <summary>
/// Applies AppConfig symbol changes at runtime (hot reload).
/// Responsible for:
/// - registering symbols with collectors (domain)
/// - subscribing/unsubscribing market depth (infrastructure) via IIBMarketDataClient
/// 
/// Trades are currently always accepted by TradeDataCollector, but this class is future-proofed to support
/// explicit per-symbol trade subscriptions once you wire them in (tick-by-tick reqs).
/// </summary>
public sealed class SubscriptionManager
{
    private readonly MarketDepthCollector _depthCollector;
    private readonly TradeDataCollector _tradeCollector;
    private readonly IIBMarketDataClient _ib;

    // symbol -> depth subscription id
    private readonly ConcurrentDictionary<string, int> _tradeSubs = new(StringComparer.OrdinalIgnoreCase);

    private readonly ConcurrentDictionary<string, int> _depthSubs = new(StringComparer.OrdinalIgnoreCase);

    public SubscriptionManager(MarketDepthCollector depthCollector, TradeDataCollector tradeCollector, IIBMarketDataClient ibClient)
    {
        _depthCollector = depthCollector ?? throw new ArgumentNullException(nameof(depthCollector));
        _tradeCollector = tradeCollector ?? throw new ArgumentNullException(nameof(tradeCollector));
        _ib = ibClient ?? throw new ArgumentNullException(nameof(ibClient));
    }

    public IReadOnlyDictionary<string, int> DepthSubscriptions => _depthSubs;
    public IReadOnlyDictionary<string, int> TradeSubscriptions => _tradeSubs;

    public void Apply(AppConfig cfg)
    {
        var desired = (cfg.Symbols ?? Array.Empty<SymbolConfig>())
            .Where(s => !string.IsNullOrWhiteSpace(s.Symbol))
            .ToDictionary(s => s.Symbol.Trim(), s => s, StringComparer.OrdinalIgnoreCase);

        // Unsubscribe removed symbols
        foreach (var existing in desired.Keys.Concat(_depthSubs.Keys).Concat(_tradeSubs.Keys).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!desired.ContainsKey(existing))
            {
                if (_depthSubs.TryRemove(existing, out var depthId) && depthId > 0)
                {
                    try { _ib.UnsubscribeMarketDepth(depthId); } catch { /* ignore */ }
                }
                if (_tradeSubs.TryRemove(existing, out var tradeId) && tradeId > 0)
                {
                    try { _ib.UnsubscribeTrades(tradeId); } catch { /* ignore */ }
                }
                _depthCollector.UnregisterSubscription(existing);
            }
        }

        // Apply desired set
        foreach (var kvp in desired)
        {
            var symbol = kvp.Key;
            var sc = kvp.Value;

            // Depth
            if (sc.SubscribeDepth)
            {
                _depthCollector.RegisterSubscription(symbol);

                if (!_depthSubs.ContainsKey(symbol))
                {
                    try
                    {
                        var id = _ib.SubscribeMarketDepth(sc);
                        if (id > 0) _depthSubs[symbol] = id;
                    }
                    catch
                    {
                        // if IB isn't enabled, SubscribeMarketDepth returns -1 via NoOp
                        _depthSubs[symbol] = -1;
                    }
                }
            }
            else
            {
                _depthCollector.UnregisterSubscription(symbol);

                if (_depthSubs.TryRemove(symbol, out var subId) && subId > 0)
                {
                    try { _ib.UnsubscribeMarketDepth(subId); } catch { /* ignore */ }
                }
            }

            // Trades (tick-by-tick)
            if (sc.SubscribeTrades)
            {
                if (!_tradeSubs.ContainsKey(symbol))
                {
                    try
                    {
                        var id = _ib.SubscribeTrades(sc);
                        if (id > 0) _tradeSubs[symbol] = id;
                    }
                    catch
                    {
                        _tradeSubs[symbol] = -1;
                    }
                }
            }
            else
            {
                if (_tradeSubs.TryRemove(symbol, out var tradeId) && tradeId > 0)
                {
                    try { _ib.UnsubscribeTrades(tradeId); } catch { /* ignore */ }
                }
            }
        }
    }
}
