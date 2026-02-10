using System;
using System.Collections.ObjectModel;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace MarketDataCollector.Uwp.Models;

/// <summary>
/// Tree view node for data browser.
/// </summary>
public sealed class DataTreeNode
{
    public string Name { get; set; } = string.Empty;
    public string Icon { get; set; } = "\uE8B7";
    public string NodeType { get; set; } = string.Empty;
    public string? Tag { get; set; }
    public string? Badge { get; set; }
    public bool HasBadge => !string.IsNullOrEmpty(Badge);
    public bool IsExpanded { get; set; }
    public ObservableCollection<DataTreeNode> Children { get; } = new();
}

/// <summary>
/// Data file item for the list view.
/// </summary>
public sealed class DataFileItem
{
    public string Symbol { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string DateText { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public SolidColorBrush TypeColor { get; set; } = new(Color.FromArgb(40, 72, 187, 120));
    public string Provider { get; set; } = string.Empty;
    public int BarCount { get; set; }
    public string BarCountText { get; set; } = "0";
    public long SizeBytes { get; set; }
    public string SizeText { get; set; } = "0 KB";
}

/// <summary>
/// Chart bar data for the chart view.
/// </summary>
public sealed class ChartBarData
{
    public string Label { get; set; } = string.Empty;
    public int Value { get; set; }
    public int Height { get; set; }
}
