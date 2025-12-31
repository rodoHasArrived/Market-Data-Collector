using MarketDataCollector.Application.Config;
using MarketDataCollector.Application.Subscriptions;
using MarketDataCollector.Infrastructure;

namespace MarketDataCollector.Application.Monitoring;

public sealed record StatusSnapshot(
    DateTimeOffset TimestampUtc,
    long Published,
    long Dropped,
    long Integrity,
    bool IbEnabled,
    int SymbolCount,
    IReadOnlyDictionary<string, int> DepthSubscriptions,
    IReadOnlyDictionary<string, int> TradeSubscriptions
)
{
    public static StatusSnapshot FromRuntime(AppConfig cfg, IMarketDataClient ib, SubscriptionManager subs)
        => new(
            TimestampUtc: DateTimeOffset.UtcNow,
            Published: Metrics.Published,
            Dropped: Metrics.Dropped,
            Integrity: Metrics.Integrity,
            IbEnabled: ib.IsEnabled,
            SymbolCount: cfg.Symbols?.Length ?? 0,
            DepthSubscriptions: new Dictionary<string, int>(subs.DepthSubscriptions, StringComparer.OrdinalIgnoreCase),
            TradeSubscriptions: new Dictionary<string, int>(subs.TradeSubscriptions, StringComparer.OrdinalIgnoreCase)
        );
}
