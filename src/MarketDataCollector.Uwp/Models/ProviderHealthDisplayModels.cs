using System;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace MarketDataCollector.Uwp.Models;

/// <summary>
/// Represents a provider health item for display.
/// </summary>
public sealed class ProviderHealthItem
{
    public string ProviderId { get; set; } = string.Empty;
    public string ProviderName { get; set; } = string.Empty;
    public bool IsConnected { get; set; }
    public double OverallScore { get; set; }
    public double StabilityScore { get; set; }
    public double LatencyScore { get; set; }
    public double CompletenessScore { get; set; }
    public double ReconnectionScore { get; set; }
    public double AverageLatencyMs { get; set; }
    public double P99LatencyMs { get; set; }
    public double MessagesPerSecond { get; set; }
    public double UptimePercent { get; set; }
    public int ReconnectsLastHour { get; set; }
    public int ErrorsLastHour { get; set; }
    public int Rank { get; set; }
    public DateTime LastUpdated { get; set; }

    public string ScoreDisplay => $"{OverallScore:F1}";
    public string StabilityDisplay => $"{StabilityScore:F1}%";
    public string LatencyDisplay => $"{AverageLatencyMs:F0}ms";
    public string CompletenessDisplay => $"{CompletenessScore:F1}%";
    public string ReconnectsDisplay => ReconnectsLastHour.ToString();
    public string StatusText => IsConnected ? "Connected" : "Disconnected";
    public string RankDisplay => $"#{Rank}";

    public SolidColorBrush StatusColor => IsConnected
        ? new SolidColorBrush(Color.FromArgb(255, 63, 185, 80))
        : new SolidColorBrush(Color.FromArgb(255, 248, 81, 73));

    public SolidColorBrush ScoreColor => OverallScore switch
    {
        >= 90 => new SolidColorBrush(Color.FromArgb(255, 63, 185, 80)),
        >= 70 => new SolidColorBrush(Color.FromArgb(255, 210, 153, 34)),
        _ => new SolidColorBrush(Color.FromArgb(255, 248, 81, 73))
    };

    public SolidColorBrush RankColor => Rank switch
    {
        1 => new SolidColorBrush(Color.FromArgb(40, 63, 185, 80)),
        2 => new SolidColorBrush(Color.FromArgb(40, 88, 166, 255)),
        _ => new SolidColorBrush(Color.FromArgb(40, 139, 148, 158))
    };
}
