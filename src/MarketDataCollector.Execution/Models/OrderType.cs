namespace MarketDataCollector.Execution.Models;

/// <summary>Supported order types.</summary>
public enum OrderType
{
    /// <summary>Execute immediately at the best available price.</summary>
    Market,

    /// <summary>Execute at the specified price or better.</summary>
    Limit
}
