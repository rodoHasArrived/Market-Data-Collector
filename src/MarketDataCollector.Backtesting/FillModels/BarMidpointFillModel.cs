using Meridian.Backtesting.Portfolio;
using Meridian.Contracts.Domain.Models;
using Meridian.Domain.Events;

namespace Meridian.Backtesting.FillModels;

/// <summary>
/// Fallback fill model used when only OHLCV bar data is available.
/// Fills market orders at bar midpoint ((Open+Close)/2) with configurable slippage.
/// Limit orders fill at bar close if the limit is satisfied.
/// </summary>
internal sealed class BarMidpointFillModel(
    ICommissionModel commissionModel,
    decimal slippageBasisPoints = 5m) : IFillModel
{
    public IReadOnlyList<FillEvent> TryFill(Order order, MarketEvent evt)
    {
        if (evt.Payload is not HistoricalBar bar)
            return [];
        if (!bar.Symbol.Equals(order.Symbol, StringComparison.OrdinalIgnoreCase))
            return [];

        var isBuy = order.Quantity > 0;
        decimal fillPrice;

        if (order.Type == OrderType.Market)
        {
            var mid = (bar.Open + bar.Close) / 2m;
            var slip = mid * (slippageBasisPoints / 10_000m);
            fillPrice = isBuy ? mid + slip : mid - slip;
        }
        else // Limit
        {
            var limitPrice = order.LimitPrice!.Value;
            // Check if limit was touched during the bar
            if (isBuy && bar.Low > limitPrice)
                return [];
            if (!isBuy && bar.High < limitPrice)
                return [];
            fillPrice = limitPrice;
        }

        var commission = commissionModel.Calculate(order.Symbol, order.Quantity, fillPrice);
        return
        [
            new FillEvent(
                Guid.NewGuid(),
                order.OrderId,
                order.Symbol,
                order.Quantity,
                fillPrice,
                commission,
                evt.Timestamp)
        ];
    }
}
