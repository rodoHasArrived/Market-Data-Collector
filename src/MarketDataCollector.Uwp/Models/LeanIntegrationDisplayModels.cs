using Microsoft.UI.Xaml.Media;

namespace MarketDataCollector.Uwp.Models;

/// <summary>
/// Display model for backtest results.
/// </summary>
public sealed class BacktestDisplayInfo
{
    public string BacktestId { get; set; } = string.Empty;
    public string AlgorithmName { get; set; } = string.Empty;
    public string DateText { get; set; } = string.Empty;
    public string ReturnText { get; set; } = string.Empty;
    public SolidColorBrush? ReturnColor { get; set; }
}
