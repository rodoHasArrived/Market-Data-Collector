namespace MarketDataCollector.Uwp.Models;

/// <summary>
/// Display item for order book levels.
/// </summary>
public sealed class OrderBookDisplayItem
{
    public string Price { get; set; } = string.Empty;
    public string Size { get; set; } = string.Empty;
    public double VolumePercent { get; set; }
}

/// <summary>
/// Display item for trades.
/// </summary>
public sealed class TradeDisplayItem
{
    public string Timestamp { get; set; } = string.Empty;
    public string Price { get; set; } = string.Empty;
    public string Size { get; set; } = string.Empty;
    public string Side { get; set; } = string.Empty;
    public string SideColor { get; set; } = string.Empty;
}
