using Microsoft.UI.Xaml.Media;

namespace MarketDataCollector.Uwp.Models;

/// <summary>
/// Display model for quick check results.
/// </summary>
public sealed class QuickCheckDisplayItem
{
    public string Name { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public string StatusIcon { get; set; } = string.Empty;
    public SolidColorBrush? StatusColor { get; set; }
}

/// <summary>
/// Display model for storage tier information.
/// </summary>
public sealed class TierDisplayItem
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string SizeText { get; set; } = string.Empty;
    public string FileCountText { get; set; } = string.Empty;
    public string RetentionText { get; set; } = string.Empty;
}

/// <summary>
/// Display model for retention policy information.
/// </summary>
public sealed class RetentionPolicyDisplayItem
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string RetentionText { get; set; } = string.Empty;
    public bool Enabled { get; set; }
}

/// <summary>
/// Display model for cleanup file candidates.
/// </summary>
public sealed class CleanupFileDisplayItem
{
    public string Path { get; set; } = string.Empty;
    public string SizeText { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// Display model for maintenance history entries.
/// </summary>
public sealed class MaintenanceHistoryItem
{
    public string RunId { get; set; } = string.Empty;
    public string TimeText { get; set; } = string.Empty;
    public string OperationsText { get; set; } = string.Empty;
    public string DurationText { get; set; } = string.Empty;
    public string StatusIcon { get; set; } = string.Empty;
    public SolidColorBrush? StatusColor { get; set; }
}
