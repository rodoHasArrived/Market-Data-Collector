using System;
using Microsoft.UI.Xaml.Data;

namespace MarketDataCollector.Uwp.Models;

/// <summary>
/// Converts bytes to formatted string.
/// </summary>
public sealed class BytesToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }
            return $"{len:F1} {sizes[order]}";
        }
        return "0 B";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        // Parse formatted string back to bytes (e.g., "1.5 GB" -> 1610612736)
        if (value is string str && !string.IsNullOrWhiteSpace(str))
        {
            var parts = str.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2 && double.TryParse(parts[0], out var num))
            {
                var multiplier = parts[1].ToUpperInvariant() switch
                {
                    "B" => 1L,
                    "KB" => 1024L,
                    "MB" => 1024L * 1024,
                    "GB" => 1024L * 1024 * 1024,
                    "TB" => 1024L * 1024 * 1024 * 1024,
                    _ => 0L
                };

                if (multiplier > 0)
                {
                    return (long)(num * multiplier);
                }
            }
        }

        return 0L;
    }
}

/// <summary>
/// Converts priority to color.
/// </summary>
public sealed class PriorityToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is RecommendationPriority priority)
        {
            return priority switch
            {
                RecommendationPriority.Critical => Windows.UI.Color.FromArgb(255, 245, 101, 101),
                RecommendationPriority.High => Windows.UI.Color.FromArgb(255, 237, 137, 54),
                RecommendationPriority.Medium => Windows.UI.Color.FromArgb(255, 66, 153, 225),
                _ => Windows.UI.Color.FromArgb(255, 160, 174, 192)
            };
        }
        return Windows.UI.Color.FromArgb(255, 160, 174, 192);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        // Map color back to priority based on RGB values
        if (value is Windows.UI.Color color)
        {
            // Critical: #F56565 (RGB: 245, 101, 101)
            if (color.R >= 240 && color.G >= 95 && color.G <= 110 && color.B >= 95 && color.B <= 110)
                return RecommendationPriority.Critical;

            // High: #ED8936 (RGB: 237, 137, 54)
            if (color.R >= 230 && color.G >= 130 && color.G <= 145 && color.B >= 50 && color.B <= 60)
                return RecommendationPriority.High;

            // Medium: #4299E1 (RGB: 66, 153, 225)
            if (color.R >= 60 && color.R <= 75 && color.G >= 148 && color.G <= 160 && color.B >= 220)
                return RecommendationPriority.Medium;
        }

        // Default to Low priority
        return RecommendationPriority.Low;
    }
}
