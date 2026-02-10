using Microsoft.UI.Xaml.Media;
using MarketDataCollector.Uwp.Services;

namespace MarketDataCollector.Uwp.Models;

/// <summary>
/// Represents a tutorial step.
/// </summary>
public sealed class TutorialStep
{
    public string StepNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
    public bool IsCurrent { get; set; }

    public SolidColorBrush StepBackground => IsCompleted
        ? BrushRegistry.Success
        : IsCurrent
            ? BrushRegistry.ChartPrimary
            : BrushRegistry.Inactive;

    public string StatusGlyph => IsCompleted ? "\uE73E" : IsCurrent ? "\uE768" : "\uE739";

    public SolidColorBrush StatusColor => IsCompleted
        ? BrushRegistry.Success
        : IsCurrent
            ? BrushRegistry.ChartPrimary
            : BrushRegistry.Inactive;
}

/// <summary>
/// Represents a feature discovery card.
/// </summary>
public sealed class FeatureCard
{
    public string Icon { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
