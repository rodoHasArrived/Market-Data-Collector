namespace MarketDataCollector.Backtesting.Sdk;

/// <summary>
/// Context object passed to every strategy callback. Provides read-only portfolio state
/// and order submission methods. All order methods are fire-and-forget within the same
/// simulated tick; fills are delivered via <see cref="IBacktestStrategy.OnOrderFill"/>.
/// </summary>
public interface IBacktestContext
{
    /// <summary>All symbols for which data is available in the requested date range.</summary>
    IReadOnlySet<string> Universe { get; }

    /// <summary>Wall-clock timestamp of the current market event being processed.</summary>
    DateTimeOffset CurrentTime { get; }

    /// <summary>Current simulated date.</summary>
    DateOnly CurrentDate { get; }

    /// <summary>Available cash (not including unrealised margin).</summary>
    decimal Cash { get; }

    /// <summary>Gross portfolio value: cash + long market value + short market value.</summary>
    decimal PortfolioValue { get; }

    /// <summary>Current open positions keyed by symbol.</summary>
    IReadOnlyDictionary<string, Position> Positions { get; }

    /// <summary>Returns the last known price for <paramref name="symbol"/>, or <c>null</c> if unseen.</summary>
    decimal? GetLastPrice(string symbol);

    /// <summary>Submit a market order. Returns the assigned order ID.</summary>
    Guid PlaceMarketOrder(string symbol, long quantity);

    /// <summary>Submit a limit order. Returns the assigned order ID.</summary>
    Guid PlaceLimitOrder(string symbol, long quantity, decimal limitPrice);

    /// <summary>Cancel a pending order.</summary>
    void CancelOrder(Guid orderId);

    /// <summary>
    /// The double-entry accounting ledger for this backtest run.
    /// Strategies can query account balances and journal entries to audit or report on costs.
    /// The ledger is read-only to prevent strategy code from corrupting the audit trail.
    /// </summary>
    IReadOnlyLedger Ledger { get; }
}
