using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace MarketDataCollector.Uwp.Models;

/// <summary>
/// Per-symbol progress information.
/// </summary>
public sealed class SymbolProgressInfo
{
    public string Symbol { get; set; } = string.Empty;
    public double Progress { get; set; }
    public string BarsText { get; set; } = "0 bars";
    public string StatusText { get; set; } = "Pending";
    public string TimeText { get; set; } = "--";
    public SolidColorBrush StatusBackground { get; set; } = new(Color.FromArgb(40, 160, 160, 160));
}

/// <summary>
/// Data validation issue.
/// </summary>
public sealed class ValidationIssue
{
    public string Symbol { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string DateRange { get; set; } = string.Empty;
}

/// <summary>
/// Scheduled backfill job.
/// </summary>
public sealed class ScheduledJob
{
    public string Name { get; set; } = string.Empty;
    public string NextRun { get; set; } = string.Empty;
}

/// <summary>
/// Backfill history item.
/// </summary>
public sealed class BackfillHistoryItem
{
    public string Date { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Duration { get; set; } = string.Empty;
    public SolidColorBrush StatusColor { get; set; } = new(Color.FromArgb(255, 72, 187, 120));
}
