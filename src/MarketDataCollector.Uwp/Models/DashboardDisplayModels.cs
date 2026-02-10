using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace MarketDataCollector.Uwp.Models;

/// <summary>
/// Display model for activity items in the dashboard feed.
/// </summary>
public sealed class ActivityDisplayItem
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = "\uE946";
    public SolidColorBrush IconBackground { get; set; } = new(Microsoft.UI.Colors.Gray);
    public string RelativeTime { get; set; } = string.Empty;
}

/// <summary>
/// Display model for integrity events in the dashboard.
/// </summary>
public sealed class IntegrityEventDisplayItem
{
    public string Id { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public string EventTypeName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public IntegritySeverity Severity { get; set; }
    public Color SeverityColor { get; set; }
    public string RelativeTime { get; set; } = string.Empty;
    public Visibility IsNotAcknowledged { get; set; } = Visibility.Visible;
}
