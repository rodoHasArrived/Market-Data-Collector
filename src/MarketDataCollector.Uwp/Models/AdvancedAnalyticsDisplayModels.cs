using Microsoft.UI.Xaml.Media;

namespace MarketDataCollector.Uwp.Models;

/// <summary>
/// Display model for symbol quality in analytics view.
/// </summary>
public sealed class SymbolQualityDisplayItem
{
    public string Symbol { get; set; } = string.Empty;
    public string Grade { get; set; } = string.Empty;
    public SolidColorBrush? GradeBackground { get; set; }
    public double OverallScore { get; set; }
    public string CompletenessText { get; set; } = string.Empty;
    public string IntegrityText { get; set; } = string.Empty;
    public string IssueCount { get; set; } = string.Empty;
}

/// <summary>
/// Display model for data gaps in analytics view.
/// </summary>
public sealed class GapDisplayItem
{
    public string Symbol { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string TimeRange { get; set; } = string.Empty;
    public string DurationText { get; set; } = string.Empty;
    public string RepairableText { get; set; } = string.Empty;
    public SolidColorBrush? RepairableBackground { get; set; }
}

/// <summary>
/// Display model for data discrepancies in analytics view.
/// </summary>
public sealed class DiscrepancyDisplayItem
{
    public string TimestampText { get; set; } = string.Empty;
    public string DiscrepancyType { get; set; } = string.Empty;
    public string Values { get; set; } = string.Empty;
    public string DifferenceText { get; set; } = string.Empty;
}

/// <summary>
/// Display model for latency metrics in analytics view.
/// </summary>
public sealed class LatencyDisplayItem
{
    public string Provider { get; set; } = string.Empty;
    public string P50Text { get; set; } = string.Empty;
    public string P95Text { get; set; } = string.Empty;
    public string P99Text { get; set; } = string.Empty;
    public double LatencyPercent { get; set; }
}

/// <summary>
/// Display model for rate limit status in analytics view.
/// </summary>
public sealed class RateLimitDisplayItem
{
    public string Provider { get; set; } = string.Empty;
    public double UsagePercent { get; set; }
    public string UsageText { get; set; } = string.Empty;
    public string StatusText { get; set; } = string.Empty;
    public SolidColorBrush? UsageColor { get; set; }
    public SolidColorBrush? StatusBackground { get; set; }
}
