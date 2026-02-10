namespace MarketDataCollector.Uwp.Models;

/// <summary>
/// Display model for sector ETFs.
/// </summary>
public sealed class SectorETFDisplay
{
    public string Symbol { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string HoldingsText { get; set; } = string.Empty;
}

/// <summary>
/// Display model for index constituents.
/// </summary>
public sealed class ConstituentDisplay
{
    public string Symbol { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string WeightText { get; set; } = string.Empty;
}

/// <summary>
/// Display model for active index subscriptions.
/// </summary>
public sealed class ActiveIndexDisplay
{
    public string Name { get; set; } = string.Empty;
    public string SymbolCount { get; set; } = string.Empty;
    public int SymbolCountValue { get; set; }
}
