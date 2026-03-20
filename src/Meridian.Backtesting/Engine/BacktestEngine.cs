using System.Diagnostics;
using Meridian.Backtesting.FillModels;
using Meridian.Backtesting.Metrics;
using Meridian.Backtesting.Portfolio;
using Meridian.Domain.Events;
using Meridian.Storage.Replay;
using Meridian.Storage.Services;

namespace Meridian.Backtesting.Engine;

/// <summary>
/// Core backtesting engine. Drives a multi-symbol chronological merge over locally-stored
/// JSONL data, dispatches events to the strategy, processes fills, and records cash flows.
/// </summary>
public sealed class BacktestEngine(
    ILogger<BacktestEngine> logger,
    StorageCatalogService catalogService)
{
    /// <summary>
    /// Runs a complete backtest, replaying all events in the requested date/symbol range.
    /// </summary>
    /// <param name="request">Backtest parameters.</param>
    /// <param name="strategy">Strategy implementation to drive.</param>
    /// <param name="progress">Optional real-time progress notifications.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<BacktestResult> RunAsync(
        BacktestRequest request,
        IBacktestStrategy strategy,
        IProgress<BacktestProgressEvent>? progress = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(strategy);

        var sw = Stopwatch.StartNew();
        logger.LogInformation("Backtesting '{Strategy}' from {From} to {To} in {DataRoot}",
            strategy.Name, request.From, request.To, request.DataRoot);

        // 1. Discover universe
        var universe = await UniverseDiscovery.DiscoverAsync(
            catalogService, request.DataRoot, request.Symbols, request.From, request.To, ct);

        if (universe.Count == 0)
        {
            logger.LogWarning("No symbols found in data root '{DataRoot}' for the requested date range", request.DataRoot);
            return CreateEmptyResult(request, universe, sw.Elapsed);
        }

        logger.LogInformation("Universe contains {Count} symbols: {Symbols}",
            universe.Count, string.Join(", ", universe.Take(10)) + (universe.Count > 10 ? "…" : ""));

        // 2. Set up portfolio, fill models, context
        var commissionModel = new PerShareCommissionModel();
        var ledger = new BacktestLedger();
        var startTimestamp = new DateTimeOffset(request.From.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var portfolio = new SimulatedPortfolio(request.InitialCash, commissionModel, request.AnnualMarginRate, request.AnnualShortRebateRate, ledger, startTimestamp);
        var ctx = new BacktestContext(portfolio, universe, ledger);
        var orderBookFillModel = new OrderBookFillModel(commissionModel);
        var barFillModel = new BarMidpointFillModel(commissionModel);

        var pendingOrders = new List<Order>();
        var allSnapshots = new List<PortfolioSnapshot>();
        var allCashFlows = new List<CashFlowEntry>();
        var allFills = new List<FillEvent>();

        // 3. Initialise strategy
        ctx.CurrentTime = DateTimeOffset.UtcNow;
        ctx.CurrentDate = request.From;
        strategy.Initialize(ctx);

        // 4. Build per-symbol replay streams
        var streams = BuildSymbolStreams(universe, request);

        // 5. Replay loop — multi-symbol chronological merge
        var currentDay = request.From;
        long eventsProcessed = 0;
        var totalDays = (request.To.ToDateTime(TimeOnly.MinValue) - request.From.ToDateTime(TimeOnly.MinValue)).Days + 1;

        await foreach (var evt in MultiSymbolMergeEnumerator.MergeAsync(streams, ct))
        {
            ct.ThrowIfCancellationRequested();

            var evtDate = DateOnly.FromDateTime(evt.Timestamp.LocalDateTime);

            // Day boundary — close out the previous day
            if (evtDate > currentDay)
            {
                await ProcessDayEndAsync(currentDay, portfolio, pendingOrders, ctx, strategy, allSnapshots, allCashFlows);
                currentDay = evtDate;

                var daysElapsed = (currentDay.ToDateTime(TimeOnly.MinValue) - request.From.ToDateTime(TimeOnly.MinValue)).Days;
                progress?.Report(new BacktestProgressEvent(
                    (double)daysElapsed / totalDays,
                    currentDay,
                    portfolio.ComputeCurrentEquity(),
                    eventsProcessed));
            }

            ctx.CurrentTime = evt.Timestamp;
            ctx.CurrentDate = evtDate;
            eventsProcessed++;

            // Update last known price from event
            UpdateLastPrice(portfolio, evt);

            // Dispatch to strategy
            DispatchEvent(strategy, ctx, evt);

            // Collect new orders placed by strategy
            var newOrders = ctx.DrainPendingOrders();
            pendingOrders.AddRange(newOrders);

            // Try to fill pending orders against current event
            ProcessPendingOrders(pendingOrders, evt, orderBookFillModel, barFillModel, portfolio, strategy, ctx, allFills);
        }

        // Final day-end
        await ProcessDayEndAsync(currentDay, portfolio, pendingOrders, ctx, strategy, allSnapshots, allCashFlows);
        strategy.OnFinished(ctx);

        progress?.Report(new BacktestProgressEvent(1.0, request.To, portfolio.ComputeCurrentEquity(), eventsProcessed, "Complete"));

        // 6. Compute metrics
        var metrics = BacktestMetricsEngine.Compute(allSnapshots, allCashFlows, allFills, request);
        sw.Stop();

        logger.LogInformation(
            "Backtest complete: {Events} events, final equity {Equity:C}, net PnL {NetPnl:C} in {Elapsed}ms",
            eventsProcessed, metrics.FinalEquity, metrics.NetPnl, sw.ElapsedMilliseconds);

        return new BacktestResult(request, universe, allSnapshots, allCashFlows, allFills, metrics, ledger, sw.Elapsed, eventsProcessed);
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private static IReadOnlyList<IAsyncEnumerable<MarketEvent>> BuildSymbolStreams(
        IReadOnlySet<string> universe,
        BacktestRequest request)
    {
        var streams = new List<IAsyncEnumerable<MarketEvent>>();
        foreach (var symbol in universe)
        {
            var symbolRoot = Path.Combine(request.DataRoot, symbol.ToUpperInvariant());
            if (!Directory.Exists(symbolRoot))
                symbolRoot = request.DataRoot;  // flat layout fallback

            var reader = new JsonlReplayer(symbolRoot);
            streams.Add(FilterBySymbolAndDate(reader.ReadEventsAsync(), symbol, request.From, request.To));
        }
        return streams;
    }

    private static async IAsyncEnumerable<MarketEvent> FilterBySymbolAndDate(
        IAsyncEnumerable<MarketEvent> source,
        string symbol,
        DateOnly from,
        DateOnly to,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var evt in source.WithCancellation(ct))
        {
            if (!evt.EffectiveSymbol.Equals(symbol, StringComparison.OrdinalIgnoreCase))
                continue;
            var date = DateOnly.FromDateTime(evt.Timestamp.LocalDateTime);
            if (date < from || date > to)
                continue;
            yield return evt;
        }
    }

    private static void UpdateLastPrice(SimulatedPortfolio portfolio, MarketEvent evt)
    {
        decimal? price = evt.Payload switch
        {
            Trade t => t.Price,
            BboQuotePayload bbo => bbo.MidPrice ?? (bbo.BidPrice + bbo.AskPrice) / 2m,
            HistoricalBar bar => bar.Close,
            _ => null
        };
        if (price.HasValue && price.Value > 0)
            portfolio.UpdateLastPrice(evt.EffectiveSymbol, price.Value);
    }

    private static void DispatchEvent(IBacktestStrategy strategy, BacktestContext ctx, MarketEvent evt)
    {
        switch (evt.Payload)
        {
            case Trade t:
                strategy.OnTrade(t, ctx);
                break;
            case BboQuotePayload q:
                strategy.OnQuote(q, ctx);
                break;
            case HistoricalBar bar:
                strategy.OnBar(bar, ctx);
                break;
            case LOBSnapshot lob:
                strategy.OnOrderBook(lob, ctx);
                break;
        }
    }

    private static void ProcessPendingOrders(
        List<Order> pendingOrders,
        MarketEvent evt,
        IFillModel lobModel,
        IFillModel barModel,
        SimulatedPortfolio portfolio,
        IBacktestStrategy strategy,
        BacktestContext ctx,
        List<FillEvent> allFills)
    {
        var filled = new List<Guid>();
        for (var i = pendingOrders.Count - 1; i >= 0; i--)
        {
            var order = pendingOrders[i];
            if (!order.Symbol.Equals(evt.EffectiveSymbol, StringComparison.OrdinalIgnoreCase))
                continue;

            var model = SelectFillModel(order, evt, lobModel, barModel);
            var result = model.TryFill(order, evt);

            foreach (var fill in result.Fills)
            {
                portfolio.ProcessFill(fill);
                allFills.Add(fill);
                strategy.OnOrderFill(fill, ctx);
            }

            if (result.RemoveOrder)
            {
                filled.Add(order.OrderId);
                continue;
            }

            pendingOrders[i] = result.UpdatedOrder;
        }

        pendingOrders.RemoveAll(o => filled.Contains(o.OrderId));
    }

    private static async Task ProcessDayEndAsync(
        DateOnly date,
        SimulatedPortfolio portfolio,
        List<Order> pendingOrders,
        BacktestContext ctx,
        IBacktestStrategy strategy,
        List<PortfolioSnapshot> snapshots,
        List<CashFlowEntry> allCashFlows, CancellationToken ct = default)
    {
        _ = ct;
        await Task.Yield();  // allow UI thread to breathe during long replays
        portfolio.AccrueDailyInterest(date);
        ctx.CurrentDate = date;
        strategy.OnDayEnd(date, ctx);

        for (var i = pendingOrders.Count - 1; i >= 0; i--)
        {
            if (pendingOrders[i].TimeInForce != TimeInForce.Day)
                continue;

            pendingOrders.RemoveAt(i);
        }

        var ts = new DateTimeOffset(date.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero);
        var snapshot = portfolio.TakeSnapshot(ts, date);
        snapshots.Add(snapshot);
        allCashFlows.AddRange(snapshot.DayCashFlows);
    }

    private static BacktestResult CreateEmptyResult(BacktestRequest request, IReadOnlySet<string> universe, TimeSpan elapsed)
    {
        var metrics = BacktestMetricsEngine.Compute([], [], [], request);
        return new BacktestResult(request, universe, [], [], [], metrics, new BacktestLedger(), elapsed, 0);
    }

    private static IFillModel SelectFillModel(
        Order order,
        MarketEvent evt,
        IFillModel lobModel,
        IFillModel barModel)
    {
        return order.ExecutionModel switch
        {
            ExecutionModel.OrderBook => lobModel,
            ExecutionModel.BarMidpoint => barModel,
            _ => evt.Payload is LOBSnapshot ? lobModel : barModel
        };
    }
}
