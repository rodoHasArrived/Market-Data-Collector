namespace MarketDataCollector.Uwp.Models;

/// <summary>
/// Display model for replay source files.
/// </summary>
public sealed class ReplayFileDisplay
{
    public string Path { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string DateText { get; set; } = string.Empty;
    public string EventCountText { get; set; } = string.Empty;
    public string SizeText { get; set; } = string.Empty;
}

/// <summary>
/// Display model for replay events.
/// </summary>
public sealed class ReplayEventDisplay
{
    public string TimestampText { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
}

/// <summary>
/// Display model for event type breakdown statistics.
/// </summary>
public sealed class EventTypeBreakdown
{
    public string EventType { get; set; } = string.Empty;
    public string CountText { get; set; } = string.Empty;
}
