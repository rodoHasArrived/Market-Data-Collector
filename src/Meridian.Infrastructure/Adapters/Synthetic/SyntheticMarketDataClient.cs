using System.Collections.Concurrent;
using Meridian.Application.Config;
using Meridian.Application.Subscriptions.Models;
using Meridian.Contracts.Configuration;
using Meridian.Contracts.Domain.Enums;
using Meridian.Contracts.Domain.Models;
using Meridian.Domain.Events;
using Meridian.Infrastructure;
using Meridian.Infrastructure.Adapters.Core;
using Meridian.Infrastructure.DataSources;

namespace Meridian.Infrastructure.Adapters.Synthetic;

[DataSource("synthetic", "Synthetic Market Data", DataSources.DataSourceType.Hybrid, DataSourceCategory.Free, Description = "Deterministic synthetic real-time and historical market data for offline development.")]
public sealed class SyntheticMarketDataClient : IMarketDataClient, ISymbolSearchProvider
{
    private readonly SyntheticMarketDataConfig _config;
    private readonly IMarketEventPublisher _publisher;
    private readonly ConcurrentDictionary<int, CancellationTokenSource> _tradeSubscriptions = new();
    private readonly ConcurrentDictionary<int, CancellationTokenSource> _depthSubscriptions = new();
    private readonly ConcurrentDictionary<int, Task> _tradeWorkers = new();
    private readonly ConcurrentDictionary<int, Task> _depthWorkers = new();
    private int _nextSubscriptionId;
    private long _tradeEventsPublished;
    private long _quoteEventsPublished;
    private long _depthSnapshotsPublished;
    private volatile bool _connected;

    public SyntheticMarketDataClient(IMarketEventPublisher publisher, SyntheticMarketDataConfig? config = null)
    {
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        _config = config ?? new SyntheticMarketDataConfig(Enabled: true);
    }

    public bool IsEnabled => _config.Enabled;
    public string ProviderId => "synthetic";
    public string ProviderDisplayName => "Synthetic Market Data";
    public string ProviderDescription => "Synthetic trades, BBO quotes, level-2 order books, and reference data for offline development.";
    public int ProviderPriority => Math.Max(0, _config.Priority);
    public ProviderCapabilities ProviderCapabilities { get; } = ProviderCapabilities.Hybrid(trades: true, quotes: true, depth: true, adjustedPrices: true, intraday: true) with
    {
        SupportsSymbolSearch = true,
        SupportsAssetTypeFilter = true,
        SupportsExchangeFilter = true,
        SupportedAssetTypes = new[] { "Stock", "ETF" },
        SupportedExchanges = new[] { "XNAS", "XNYS", "ARCX" },
        MaxDepthLevels = 50
    };

    public string Name => ProviderId;
    public string DisplayName => ProviderDisplayName;
    public int Priority => ProviderPriority;
    public bool IsConnected => _connected;
    public int ActiveTradeSubscriptionCount => _tradeWorkers.Count;
    public int ActiveDepthSubscriptionCount => _depthWorkers.Count;

    public Task ConnectAsync(CancellationToken ct = default)
    {
        _connected = true;
        return Task.CompletedTask;
    }

    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        _connected = false;
        await CancelSubscriptionsAsync(_tradeSubscriptions, _tradeWorkers, ct).ConfigureAwait(false);
        await CancelSubscriptionsAsync(_depthSubscriptions, _depthWorkers, ct).ConfigureAwait(false);
    }

    public int SubscribeMarketDepth(SymbolConfig cfg)
    {
        return StartSubscription(cfg, _depthSubscriptions, _depthWorkers, PublishDepthAsync);
    }

    public void UnsubscribeMarketDepth(int subscriptionId)
    {
        CancelSubscription(subscriptionId, _depthSubscriptions, _depthWorkers);
    }

    public int SubscribeTrades(SymbolConfig cfg)
    {
        return StartSubscription(cfg, _tradeSubscriptions, _tradeWorkers, PublishTradesAsync);
    }

    public void UnsubscribeTrades(int subscriptionId)
    {
        CancelSubscription(subscriptionId, _tradeSubscriptions, _tradeWorkers);
    }

    public Task<bool> IsAvailableAsync(CancellationToken ct = default) => Task.FromResult(_config.Enabled);

    public Task<IReadOnlyList<SymbolSearchResult>> SearchAsync(string query, int limit = 10, CancellationToken ct = default)
        => Task.FromResult(SyntheticReferenceDataCatalog.Search(query, limit));

    public Task<SymbolDetails?> GetDetailsAsync(string symbol, CancellationToken ct = default)
        => Task.FromResult<SymbolDetails?>(SyntheticReferenceDataCatalog.GetProfile(symbol)?.ReferenceData);

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    public SyntheticMarketDataDiagnostics GetDiagnostics()
        => new(
            IsConnected,
            ActiveTradeSubscriptionCount,
            ActiveDepthSubscriptionCount,
            Interlocked.Read(ref _tradeEventsPublished),
            Interlocked.Read(ref _quoteEventsPublished),
            Interlocked.Read(ref _depthSnapshotsPublished),
            _config.EventsPerSecond,
            _config.DefaultDepthLevels);

    private async Task PublishTradesAsync(SymbolConfig cfg, CancellationToken ct)
    {
        var profile = SyntheticReferenceDataCatalog.GetProfileOrDefault(cfg.Symbol);
        var seq = 0L;
        var delay = TimeSpan.FromMilliseconds(Math.Max(35, 1000 / Math.Max(1, _config.EventsPerSecond)));

        while (!ct.IsCancellationRequested && _connected)
        {
            var now = DateTimeOffset.UtcNow;
            var anchor = ComputeAnchor(profile, now, seq);
            var price = Round4(anchor * (1m + Noise(profile.Symbol, now.Millisecond, (int)seq, 0.0008m)));
            var size = 25L * (1 + (long)(PositiveNoise(profile.Symbol, now.Second, (int)seq + 11, 40m)));
            var trade = new Trade(
                Timestamp: now,
                Symbol: profile.Symbol,
                Price: price,
                Size: size,
                Aggressor: seq % 2 == 0 ? AggressorSide.Buy : AggressorSide.Sell,
                SequenceNumber: ++seq,
                StreamId: $"synthetic-{profile.Symbol}",
                Venue: profile.Exchange);

            PublishEvent(MarketEvent.Trade(now, profile.Symbol, trade, source: ProviderId));
            await DelayAsync(delay, ct).ConfigureAwait(false);
        }
    }

    private async Task PublishDepthAsync(SymbolConfig cfg, CancellationToken ct)
    {
        var profile = SyntheticReferenceDataCatalog.GetProfileOrDefault(cfg.Symbol);
        var seq = 0L;
        var levels = Math.Clamp(cfg.DepthLevels > 0 ? cfg.DepthLevels : _config.DefaultDepthLevels, 1, 25);
        var delay = TimeSpan.FromMilliseconds(Math.Max(60, 1200 / Math.Max(1, _config.EventsPerSecond)));

        while (!ct.IsCancellationRequested && _connected)
        {
            var now = DateTimeOffset.UtcNow;
            var anchor = ComputeAnchor(profile, now, seq);
            var tick = Math.Max(0.0001m, anchor * 0.00005m);
            var spread = Math.Max(0.01m, Round4(anchor * (0.00008m + PositiveNoise(profile.Symbol, now.Second, (int)seq, 0.00006m))));
            var bestBid = Round4(anchor - spread / 2m);
            var bestAsk = Round4(anchor + spread / 2m);
            var bids = new List<OrderBookLevel>(levels);
            var asks = new List<OrderBookLevel>(levels);

            for (ushort level = 0; level < levels; level++)
            {
                var bidPrice = Round4(bestBid - level * tick);
                var askPrice = Round4(bestAsk + level * tick);
                var bidSize = Math.Max(100m, Math.Round(100m * (1m + PositiveNoise(profile.Symbol, level, now.Millisecond + 3, 35m)), 0));
                var askSize = Math.Max(100m, Math.Round(100m * (1m + PositiveNoise(profile.Symbol, level, now.Millisecond + 7, 35m)), 0));
                bids.Add(new OrderBookLevel(OrderBookSide.Bid, level, bidPrice, bidSize, $"MM{level + 1:00}"));
                asks.Add(new OrderBookLevel(OrderBookSide.Ask, level, askPrice, askSize, $"MM{level + 11:00}"));
            }

            var quote = new BboQuotePayload(
                Timestamp: now,
                Symbol: profile.Symbol,
                BidPrice: bestBid,
                BidSize: (int)bids[0].Size,
                AskPrice: bestAsk,
                AskSize: (int)asks[0].Size,
                MidPrice: Round4((bestBid + bestAsk) / 2m),
                Spread: Round4(bestAsk - bestBid),
                SequenceNumber: ++seq,
                StreamId: $"synthetic-{profile.Symbol}",
                Venue: profile.Exchange);
            var snapshot = new LOBSnapshot(
                Timestamp: now,
                Symbol: profile.Symbol,
                Bids: bids,
                Asks: asks,
                MidPrice: quote.MidPrice,
                MicroPrice: Round4(((bestAsk * bids[0].Size) + (bestBid * asks[0].Size)) / (bids[0].Size + asks[0].Size)),
                Imbalance: Math.Round(bids.Sum(b => b.Size) / (bids.Sum(b => b.Size) + asks.Sum(a => a.Size)), 4),
                MarketState: MarketState.Normal,
                SequenceNumber: seq,
                StreamId: quote.StreamId,
                Venue: profile.Exchange);

            PublishEvent(MarketEvent.BboQuote(now, profile.Symbol, quote, source: ProviderId));
            PublishEvent(MarketEvent.L2Snapshot(now, profile.Symbol, snapshot, source: ProviderId));
            await DelayAsync(delay, ct).ConfigureAwait(false);
        }
    }

    private int StartSubscription(
        SymbolConfig cfg,
        ConcurrentDictionary<int, CancellationTokenSource> subscriptions,
        ConcurrentDictionary<int, Task> workers,
        Func<SymbolConfig, CancellationToken, Task> workerFactory)
    {
        ArgumentNullException.ThrowIfNull(cfg);

        var id = Interlocked.Increment(ref _nextSubscriptionId);
        var cts = new CancellationTokenSource();
        subscriptions[id] = cts;

        var worker = Task.Factory
            .StartNew(() => workerFactory(cfg, cts.Token), cts.Token, TaskCreationOptions.DenyChildAttach, TaskScheduler.Default)
            .Unwrap();

        workers[id] = worker;
        _ = worker.ContinueWith(
            _ => CompleteSubscription(id, subscriptions, workers),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

        return id;
    }

    private static void CancelSubscription(
        int subscriptionId,
        ConcurrentDictionary<int, CancellationTokenSource> subscriptions,
        ConcurrentDictionary<int, Task> workers)
    {
        if (subscriptions.TryGetValue(subscriptionId, out var cts))
        {
            cts.Cancel();

            if (!workers.ContainsKey(subscriptionId) && subscriptions.TryRemove(subscriptionId, out var completedCts))
            {
                completedCts.Dispose();
            }
        }
    }

    private static async Task CancelSubscriptionsAsync(
        ConcurrentDictionary<int, CancellationTokenSource> subscriptions,
        ConcurrentDictionary<int, Task> workers,
        CancellationToken ct)
    {
        foreach (var (_, cts) in subscriptions)
        {
            cts.Cancel();
        }

        try
        {
            if (workers.Count > 0)
            {
                var activeWorkers = workers.Values.ToArray();
                await Task.WhenAll(activeWorkers).WaitAsync(ct).ConfigureAwait(false);
            }
        }
        finally
        {
            foreach (var (_, cts) in subscriptions)
            {
                cts.Dispose();
            }

            subscriptions.Clear();
            workers.Clear();
        }
    }

    private static async Task DelayAsync(TimeSpan delay, CancellationToken ct)
    {
        try
        {
            await Task.Delay(delay, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void PublishEvent(in MarketEvent evt)
    {
        if (!_publisher.TryPublish(evt))
        {
            return;
        }

        switch (evt.Type)
        {
            case MarketEventType.Trade:
                Interlocked.Increment(ref _tradeEventsPublished);
                break;
            case MarketEventType.BboQuote:
                Interlocked.Increment(ref _quoteEventsPublished);
                break;
            case MarketEventType.L2Snapshot:
                Interlocked.Increment(ref _depthSnapshotsPublished);
                break;
        }
    }

    private static void CompleteSubscription(
        int subscriptionId,
        ConcurrentDictionary<int, CancellationTokenSource> subscriptions,
        ConcurrentDictionary<int, Task> workers)
    {
        if (subscriptions.TryRemove(subscriptionId, out var cts))
        {
            cts.Dispose();
        }

        workers.TryRemove(subscriptionId, out _);
    }

    private static decimal ComputeAnchor(SyntheticSymbolProfile profile, DateTimeOffset timestamp, long seq)
    {
        var date = DateOnly.FromDateTime(timestamp.UtcDateTime.Date);
        var minutesSinceOpen = Math.Max(0m, (decimal)(timestamp.TimeOfDay - new TimeSpan(9, 30, 0)).TotalMinutes);
        var sessionBase = profile.BasePrice * (1m + (decimal)Math.Sin(date.DayNumber / 21d) * 0.015m);
        var intraday = 1m + (decimal)Math.Sin((double)(minutesSinceOpen / 390m) * Math.PI * 2d) * 0.0018m;
        var drift = 1m + seq * 0.00002m;
        return Round4(sessionBase * intraday * drift);
    }

    private static decimal Noise(string symbol, int a, int b, decimal amplitude)
    {
        var value = StableUnit(symbol, a, b);
        return ((decimal)value * 2m - 1m) * amplitude;
    }

    private static decimal PositiveNoise(string symbol, int a, int b, decimal amplitude)
        => ((decimal)StableUnit(symbol, a, b) + 0.25m) * amplitude;

    private static double StableUnit(string symbol, int a, int b)
    {
        var hash = HashCode.Combine(symbol.ToUpperInvariant(), a, b);
        return (hash & 0x7fffffff) / (double)int.MaxValue;
    }

    private static decimal Round4(decimal value) => Math.Round(value, 4, MidpointRounding.AwayFromZero);

    public sealed record SyntheticMarketDataDiagnostics(
        bool IsConnected,
        int ActiveTradeSubscriptions,
        int ActiveDepthSubscriptions,
        long TradesPublished,
        long QuotesPublished,
        long DepthSnapshotsPublished,
        int ConfiguredEventsPerSecond,
        int DefaultDepthLevels);
}
