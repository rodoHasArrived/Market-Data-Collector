using Windows.UI;

namespace MarketDataCollector.Uwp.Models;

/// <summary>
/// Display model for provider health.
/// </summary>
public sealed class ProviderHealthDisplay
{
    public string Provider { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public Color StatusColor { get; set; }
    public string LatencyText { get; set; } = string.Empty;
    public string EventsPerSecond { get; set; } = string.Empty;
    public string LastEventText { get; set; } = string.Empty;
}

/// <summary>
/// Display model for system events.
/// </summary>
public sealed class SystemEventDisplay
{
    public string EventType { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Timestamp { get; set; } = string.Empty;
    public Color SeverityColor { get; set; }
}
