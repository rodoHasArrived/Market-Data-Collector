namespace MarketDataCollector.Execution.Models;

/// <summary>Distinguishes paper (simulated) from live (real-money) execution modes.</summary>
public enum ExecutionMode
{
    /// <summary>Orders are simulated against a live or historical feed. No real orders are placed.</summary>
    Paper,

    /// <summary>
    /// Orders are routed to a real broker. Requires explicit opt-in; never the default.
    /// </summary>
    Live
}
