using Microsoft.UI.Xaml.Media;

namespace MarketDataCollector.Uwp.Models;

/// <summary>
/// Display model for data packages.
/// </summary>
public sealed class PackageDisplayInfo
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string SizeText { get; set; } = string.Empty;
    public string DateText { get; set; } = string.Empty;
}

/// <summary>
/// Display model for package validation issues.
/// </summary>
public sealed class ValidationIssueDisplay
{
    public string Message { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public SolidColorBrush? IconColor { get; set; }
}
