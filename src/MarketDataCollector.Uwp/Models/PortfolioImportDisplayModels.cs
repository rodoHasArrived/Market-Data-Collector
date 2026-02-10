namespace MarketDataCollector.Uwp.Models;

/// <summary>
/// Display model for portfolio entries.
/// </summary>
public sealed class PortfolioEntryDisplay
{
    public string Symbol { get; set; } = string.Empty;
    public decimal? Quantity { get; set; }
    public string QuantityText { get; set; } = string.Empty;
    public string Exchange { get; set; } = string.Empty;
}
