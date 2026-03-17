using MarketDataCollector.Backtesting.Portfolio;
using MarketDataCollector.Contracts.Domain.Models;
using MarketDataCollector.Domain.Events;

namespace MarketDataCollector.Backtesting.FillModels;

/// <summary>
/// Realistic fill model that walks stored <see cref="LOBSnapshot"/> bid/ask levels.
/// Buy orders consume ask levels in ascending price order; sell orders consume bid levels in descending order.
/// Emits one <see cref="FillEvent"/> per price level consumed (partial fills).
/// </summary>
internal sealed class OrderBookFillModel(ICommissionModel commissionModel) : IFillModel
{
    public IReadOnlyList<FillEvent> TryFill(Order order, MarketEvent evt)
    {
        if (evt.Payload is not LOBSnapshot lob) return [];
        if (!lob.Symbol.Equals(order.Symbol, StringComparison.OrdinalIgnoreCase)) return [];

        var fills = new List<FillEvent>();
        var remainingQty = Math.Abs(order.Quantity);
        var isBuy = order.Quantity > 0;

        var levels = isBuy
            ? lob.Asks.OrderBy(l => l.Price).ToList()   // buy hits asks (ascending)
            : lob.Bids.OrderByDescending(l => l.Price).ToList();  // sell hits bids (descending)

        foreach (var level in levels)
        {
            if (remainingQty == 0) break;

            // Limit order price check
            if (order.Type == OrderType.Limit)
            {
                if (isBuy && level.Price > order.LimitPrice!.Value) break;
                if (!isBuy && level.Price < order.LimitPrice!.Value) break;
            }

            var fillQty = Math.Min(remainingQty, (long)level.Size);
            var commission = commissionModel.Calculate(order.Symbol, isBuy ? fillQty : -fillQty, level.Price);
            fills.Add(new FillEvent(
                Guid.NewGuid(),
                order.OrderId,
                order.Symbol,
                isBuy ? fillQty : -fillQty,
                level.Price,
                commission,
                evt.Timestamp));
            remainingQty -= fillQty;
        }

        return fills;
    }
}
