using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace MarketDataCollector.Uwp.Models;

/// <summary>
/// Display model for a month in the calendar view.
/// </summary>
public sealed class MonthViewModel
{
    public string MonthName { get; set; } = string.Empty;
    public string CompletenessText { get; set; } = string.Empty;
    public List<WeekRowViewModel> WeekRows { get; set; } = new();
}

/// <summary>
/// Display model for a week row in the calendar view.
/// </summary>
public sealed class WeekRowViewModel
{
    public List<DayCellViewModel> Days { get; set; } = new();
}

/// <summary>
/// Display model for a day cell in the calendar view.
/// </summary>
public sealed class DayCellViewModel
{
    public SolidColorBrush Color { get; set; } = new(Microsoft.UI.Colors.Transparent);
    public string Tooltip { get; set; } = string.Empty;
}

/// <summary>
/// Display model for data gaps.
/// </summary>
public sealed class GapViewModel
{
    public string Symbol { get; set; } = string.Empty;
    public string DateRange { get; set; } = string.Empty;
    public string GapType { get; set; } = string.Empty;
    public string MissingEvents { get; set; } = string.Empty;
    public bool CanRepair { get; set; }
    public Visibility CanRepairVisibility { get; set; }
    public GapInfo GapInfo { get; set; } = new();
}

/// <summary>
/// Display model for symbol coverage.
/// </summary>
public sealed class SymbolCoverageViewModel
{
    public string Symbol { get; set; } = string.Empty;
    public double Completeness { get; set; }
    public string CompletenessText { get; set; } = string.Empty;
    public SolidColorBrush BarColor { get; set; } = new(Microsoft.UI.Colors.Green);
    public SolidColorBrush TextColor { get; set; } = new(Microsoft.UI.Colors.Green);
}
