using Microsoft.UI.Xaml.Media;

namespace MarketDataCollector.Uwp.Models;

/// <summary>
/// Display model for preflight check results.
/// </summary>
public sealed class PreflightCheckDisplay
{
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool Passed { get; set; }
    public string StatusIcon { get; set; } = string.Empty;
    public SolidColorBrush? StatusColor { get; set; }
}
