using IBDataCollector.Application.Config;
using IBDataCollector.Application.Subscriptions;
using IBDataCollector.Infrastructure.IB;

namespace IBDataCollector.Application.Monitoring;

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
    public static StatusSnapshot FromRuntime(AppConfig cfg, IIBMarketDataClient ib, SubscriptionManager subs)
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
