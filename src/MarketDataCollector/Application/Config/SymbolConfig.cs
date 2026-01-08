namespace MarketDataCollector.Application.Config;

/// <summary>
/// Configuration for a subscribed symbol and how to build its IB contract.
/// 
/// Notes for preferred shares on IB:
/// - Preferreds are usually represented as SecType=STK with a LocalSymbol like "PCG PRA" or "PCG PR A".
/// - To avoid ambiguity, set LocalSymbol explicitly when possible.
/// </summary>
public sealed record SymbolConfig(
    string Symbol,

    // Data collection toggles
    bool SubscribeTrades = true,
    bool SubscribeDepth = true,
    int DepthLevels = 10,

    // Contract fields (IB)
    string SecurityType = "STK",        // STK, OPT, FUT, CASH, etc.
    string Exchange = "SMART",          // SMART is usually best; set direct venue if needed
    string Currency = "USD",
    string? PrimaryExchange = null,     // e.g. NYSE, NASDAQ
    string? LocalSymbol = null,         // strongly recommended for preferreds (e.g. "PCG PRA")
    string? TradingClass = null,
    int? ConId = null                  // if you know the exact contract id, this wins
);
