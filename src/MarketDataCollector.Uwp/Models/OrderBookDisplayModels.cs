using Microsoft.UI.Xaml.Media;

namespace MarketDataCollector.Uwp.Models;

/// <summary>
/// Display model for order book price levels.
/// </summary>
public sealed class OrderBookLevelViewModel
{
    public string Price { get; set; } = string.Empty;
    public string Size { get; set; } = string.Empty;
    public string Cumulative { get; set; } = string.Empty;
    public double BarWidth { get; set; }
    public bool IsBid { get; set; }
}

/// <summary>
/// Display model for trade entries.
/// </summary>
public sealed class TradeViewModel
{
    public string Time { get; set; } = string.Empty;
    public string Price { get; set; } = string.Empty;
    public string Size { get; set; } = string.Empty;
    public SolidColorBrush PriceColor { get; set; } = new(Microsoft.UI.Colors.White);
}

/// <summary>
/// Display model for depth chart bars.
/// </summary>
public sealed class DepthBarViewModel
{
    public double BarHeight { get; set; }
    public string Tooltip { get; set; } = string.Empty;
}
