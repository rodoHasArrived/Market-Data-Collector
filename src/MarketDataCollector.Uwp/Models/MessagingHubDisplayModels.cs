using Microsoft.UI.Xaml.Media;

namespace MarketDataCollector.Uwp.Models;

/// <summary>
/// Display model for message consumers.
/// </summary>
public sealed class ConsumerDisplay
{
    public string Name { get; set; } = string.Empty;
    public string MessageType { get; set; } = string.Empty;
    public string ConsumedText { get; set; } = string.Empty;
    public string AvgTimeText { get; set; } = string.Empty;
    public SolidColorBrush? StatusColor { get; set; }
}

/// <summary>
/// Display model for messaging endpoints.
/// </summary>
public sealed class EndpointDisplay
{
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string TypeIcon { get; set; } = string.Empty;
    public string PendingText { get; set; } = string.Empty;
    public string HealthText { get; set; } = string.Empty;
    public SolidColorBrush? HealthBackground { get; set; }
}

/// <summary>
/// Display model for error messages.
/// </summary>
public sealed class ErrorMessageDisplay
{
    public string MessageType { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
    public string TimestampText { get; set; } = string.Empty;
}

/// <summary>
/// Display model for messaging activity.
/// </summary>
public sealed class ActivityDisplay
{
    public string MessageType { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public string DirectionIcon { get; set; } = string.Empty;
    public SolidColorBrush? DirectionColor { get; set; }
    public string TimeText { get; set; } = string.Empty;
}

/// <summary>
/// Display model for message type statistics.
/// </summary>
public sealed class MessageTypeDisplay
{
    public string Type { get; set; } = string.Empty;
    public string CountText { get; set; } = string.Empty;
}
