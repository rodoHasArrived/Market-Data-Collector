using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace MarketDataCollector.Uwp.Models;

/// <summary>
/// View model for active incidents.
/// </summary>
public sealed class IncidentViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = "\uE946";
    public string StartedAt { get; set; } = string.Empty;
    public string Duration { get; set; } = string.Empty;
    public string RelatedEvents { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
}

/// <summary>
/// View model for incident timeline items.
/// </summary>
public sealed class IncidentTimelineItem
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Timestamp { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string TypeLabel { get; set; } = string.Empty;
    public SolidColorBrush TypeBackground { get; set; } = new SolidColorBrush(Colors.Gray);
    public SolidColorBrush StatusColor { get; set; } = new SolidColorBrush(Colors.Gray);
    public Visibility ShowConnector { get; set; } = Visibility.Visible;
    public string NavigationTarget { get; set; } = string.Empty;
}

/// <summary>
/// Model for snooze rules.
/// </summary>
public sealed class SnoozeRule
{
    public string Id { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public string ExpiresIn { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// Converts NotificationType to color brush.
/// </summary>
public sealed class NotificationTypeToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is NotificationType type)
        {
            return type switch
            {
                NotificationType.Error => new SolidColorBrush(Windows.UI.Color.FromArgb(255, 245, 101, 101)),
                NotificationType.Warning => new SolidColorBrush(Windows.UI.Color.FromArgb(255, 237, 137, 54)),
                NotificationType.Success => new SolidColorBrush(Windows.UI.Color.FromArgb(255, 72, 187, 120)),
                _ => new SolidColorBrush(Windows.UI.Color.FromArgb(255, 66, 153, 225))
            };
        }
        return new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        // Map SolidColorBrush back to NotificationType based on color
        if (value is SolidColorBrush brush)
        {
            var color = brush.Color;

            // Error: #F56565 (RGB: 245, 101, 101)
            if (color.R >= 240 && color.G >= 95 && color.G <= 110 && color.B >= 95 && color.B <= 110)
                return NotificationType.Error;

            // Warning: #ED8936 (RGB: 237, 137, 54)
            if (color.R >= 230 && color.G >= 130 && color.G <= 145 && color.B >= 50 && color.B <= 60)
                return NotificationType.Warning;

            // Success: #48BB78 (RGB: 72, 187, 120)
            if (color.R >= 65 && color.R <= 80 && color.G >= 180 && color.G <= 195 && color.B >= 115 && color.B <= 130)
                return NotificationType.Success;
        }

        // Default to Info
        return NotificationType.Info;
    }
}

/// <summary>
/// Converts NotificationType to icon glyph.
/// </summary>
public sealed class NotificationTypeToIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is NotificationType type)
        {
            return type switch
            {
                NotificationType.Error => "\uEA39",
                NotificationType.Warning => "\uE7BA",
                NotificationType.Success => "\uE73E",
                _ => "\uE946"
            };
        }
        return "\uE946";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        // Map icon glyph back to NotificationType
        if (value is string glyph)
        {
            return glyph switch
            {
                "\uEA39" => NotificationType.Error,     // Error icon
                "\uE7BA" => NotificationType.Warning,   // Warning icon
                "\uE73E" => NotificationType.Success,   // Checkmark icon
                "\uE946" => NotificationType.Info,      // Info icon
                _ => NotificationType.Info
            };
        }

        return NotificationType.Info;
    }
}
