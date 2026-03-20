namespace Meridian.Execution.Models;

/// <summary>Supported order types.</summary>
public enum OrderType
{
    /// <summary>Execute immediately at the best available price.</summary>
    Market,

    /// <summary>Execute at the specified price or better.</summary>
    Limit,

    /// <summary>Become a market order once the stop price has been crossed.</summary>
    StopMarket,

    /// <summary>Become a limit order once the stop price has been crossed.</summary>
    StopLimit
}
