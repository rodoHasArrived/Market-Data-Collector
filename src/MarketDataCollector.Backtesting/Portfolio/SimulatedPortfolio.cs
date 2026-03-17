namespace MarketDataCollector.Backtesting.Portfolio;

/// <summary>
/// Tracks simulated cash, margin, positions, and a typed cash-flow ledger.
/// All mutations are single-threaded (called from the engine replay loop).
/// </summary>
internal sealed class SimulatedPortfolio
{
    private readonly ICommissionModel _commission;
    private readonly double _annualMarginRate;
    private readonly double _annualShortRebateRate;

    // FIFO cost-basis lots: symbol → queue of (quantity, avgPrice)
    private readonly Dictionary<string, Queue<(long qty, decimal price)>> _lots = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, decimal> _lastPrices = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, long> _positions = new(StringComparer.OrdinalIgnoreCase);  // net qty (neg=short)
    private readonly Dictionary<string, decimal> _avgCost = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, decimal> _realizedPnl = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<CashFlowEntry> _cashFlows = [];
    private decimal _prevEquity;

    public decimal Cash { get; private set; }
    public decimal MarginBalance { get; private set; }  // negative = debit (we owe broker)

    public IReadOnlyDictionary<string, decimal> LastPrices => _lastPrices;

    public SimulatedPortfolio(
        decimal initialCash,
        ICommissionModel commission,
        double annualMarginRate,
        double annualShortRebateRate)
    {
        Cash = initialCash;
        _prevEquity = initialCash;
        _commission = commission;
        _annualMarginRate = annualMarginRate;
        _annualShortRebateRate = annualShortRebateRate;
    }

    // ── Price updates ────────────────────────────────────────────────────────

    public void UpdateLastPrice(string symbol, decimal price) => _lastPrices[symbol] = price;

    // ── Order fill processing ────────────────────────────────────────────────

    public void ProcessFill(FillEvent fill)
    {
        var symbol = fill.Symbol;
        var qty = fill.FilledQuantity;   // positive=buy, negative=sell
        var price = fill.FillPrice;
        var commission = fill.Commission;

        // Update cash: buying costs cash, selling raises cash (before commission)
        var cashImpact = -(qty * price) - commission;
        Cash += cashImpact;

        // If cash goes negative we're using margin
        if (Cash < 0)
        {
            MarginBalance = Math.Min(MarginBalance, Cash);  // track peak debit
        }

        // Update net position
        _positions.TryGetValue(symbol, out var existingQty);
        var newQty = existingQty + qty;
        _positions[symbol] = newQty;

        // Update average cost basis
        if (qty > 0)  // buying
        {
            if (!_lots.TryGetValue(symbol, out var queue))
            {
                queue = new Queue<(long, decimal)>();
                _lots[symbol] = queue;
            }
            queue.Enqueue((qty, price));
            // Recalculate average
            _avgCost[symbol] = ComputeAvgCost(symbol);
        }
        else if (qty < 0 && existingQty > 0)  // closing long
        {
            var closeQty = Math.Min(-qty, existingQty);
            var realised = RealiseFifo(symbol, closeQty, price);
            _realizedPnl[symbol] = (_realizedPnl.GetValueOrDefault(symbol)) + realised;
        }

        // Record trade cash flow
        _cashFlows.Add(new TradeCashFlow(fill.FilledAt, cashImpact, symbol, qty, price));

        if (commission > 0)
            _cashFlows.Add(new CommissionCashFlow(fill.FilledAt, -commission, symbol, fill.OrderId));
    }

    // ── Day-end accruals ─────────────────────────────────────────────────────

    public void AccrueDailyInterest(DateOnly date)
    {
        var ts = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        // Margin debit interest (we owe; negative cash flow)
        if (MarginBalance < 0)
        {
            var interest = MarginBalance * (decimal)(_annualMarginRate / 252.0);  // daily charge (negative * positive rate = negative)
            Cash += interest;  // deduct from cash
            MarginBalance += interest;
            _cashFlows.Add(new MarginInterestCashFlow(ts, interest, MarginBalance, _annualMarginRate));
        }

        // Short rebate (we receive; positive cash flow)
        foreach (var (symbol, qty) in _positions)
        {
            if (qty >= 0) continue;
            var lastPrice = _lastPrices.GetValueOrDefault(symbol, 0m);
            if (lastPrice <= 0) continue;
            var shortNotional = Math.Abs(qty) * lastPrice;
            var rebate = shortNotional * (decimal)(_annualShortRebateRate / 252.0);
            Cash += rebate;
            _cashFlows.Add(new ShortRebateCashFlow(ts, rebate, symbol, Math.Abs(qty), _annualShortRebateRate));
        }
    }

    // ── Snapshot ─────────────────────────────────────────────────────────────

    public PortfolioSnapshot TakeSnapshot(DateTimeOffset timestamp, DateOnly date)
    {
        var positions = BuildPositions();
        var longMv = positions.Values.Where(p => p.Quantity > 0).Sum(p => p.NotionalValue(_lastPrices.GetValueOrDefault(p.Symbol, p.AverageCostBasis)));
        var shortMv = positions.Values.Where(p => p.Quantity < 0).Sum(p => p.NotionalValue(_lastPrices.GetValueOrDefault(p.Symbol, p.AverageCostBasis)));
        var equity = Cash + longMv + shortMv;
        var dailyReturn = _prevEquity == 0 ? 0m : (equity - _prevEquity) / _prevEquity;
        _prevEquity = equity;

        var dayCashFlows = _cashFlows.ToList();
        _cashFlows.Clear();

        return new PortfolioSnapshot(timestamp, date, Cash, MarginBalance, longMv, shortMv, equity, dailyReturn, positions, dayCashFlows);
    }

    public decimal ComputeCurrentEquity()
    {
        var longMv = _positions.Where(p => p.Value > 0).Sum(p => p.Value * _lastPrices.GetValueOrDefault(p.Key, 0m));
        var shortMv = _positions.Where(p => p.Value < 0).Sum(p => p.Value * _lastPrices.GetValueOrDefault(p.Key, 0m));
        return Cash + longMv + shortMv;
    }

    public IReadOnlyDictionary<string, Position> GetCurrentPositions() => BuildPositions();

    // ── Private helpers ──────────────────────────────────────────────────────

    private IReadOnlyDictionary<string, Position> BuildPositions()
    {
        var result = new Dictionary<string, Position>(StringComparer.OrdinalIgnoreCase);
        foreach (var (symbol, qty) in _positions)
        {
            if (qty == 0) continue;
            var lastPrice = _lastPrices.GetValueOrDefault(symbol, _avgCost.GetValueOrDefault(symbol, 0m));
            var avgCost = _avgCost.GetValueOrDefault(symbol, 0m);
            var unrealised = (lastPrice - avgCost) * qty;
            var realised = _realizedPnl.GetValueOrDefault(symbol, 0m);
            result[symbol] = new Position(symbol, qty, avgCost, unrealised, realised);
        }
        return result;
    }

    private decimal ComputeAvgCost(string symbol)
    {
        if (!_lots.TryGetValue(symbol, out var queue) || queue.Count == 0) return 0m;
        var totalQty = 0L;
        var totalCost = 0m;
        foreach (var (q, p) in queue) { totalQty += q; totalCost += q * p; }
        return totalQty == 0 ? 0m : totalCost / totalQty;
    }

    private decimal RealiseFifo(string symbol, long closeQty, decimal sellPrice)
    {
        if (!_lots.TryGetValue(symbol, out var queue)) return 0m;
        var realised = 0m;
        var remaining = closeQty;
        while (remaining > 0 && queue.Count > 0)
        {
            var (lotQty, lotPrice) = queue.Peek();
            if (lotQty <= remaining)
            {
                realised += lotQty * (sellPrice - lotPrice);
                remaining -= lotQty;
                queue.Dequeue();
            }
            else
            {
                realised += remaining * (sellPrice - lotPrice);
                queue = new Queue<(long, decimal)>(queue.Skip(1).Prepend((lotQty - remaining, lotPrice)));
                _lots[symbol] = queue;
                remaining = 0;
            }
        }
        _avgCost[symbol] = ComputeAvgCost(symbol);
        return realised;
    }
}
