using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using MarketDataCollector.Uwp.Services;
using Windows.UI;

namespace MarketDataCollector.Uwp.Views;

/// <summary>
/// Page for displaying provider health metrics and score breakdowns.
/// </summary>
public sealed partial class ProviderHealthPage : Page
{
    private readonly ProviderHealthService _healthService;
    private readonly ObservableCollection<ProviderHealthItem> _providers;
    private ProviderHealthItem? _selectedProvider;

    public ProviderHealthPage()
    {
        this.InitializeComponent();

        _healthService = ProviderHealthService.Instance;
        _providers = new ObservableCollection<ProviderHealthItem>();

        ProviderCardsGrid.ItemsSource = _providers;
        ComparisonList.ItemsSource = _providers;

        Loaded += ProviderHealthPage_Loaded;
        Unloaded += ProviderHealthPage_Unloaded;
    }

    private async void ProviderHealthPage_Loaded(object sender, RoutedEventArgs e)
    {
        _healthService.HealthUpdated += OnHealthUpdated;
        _healthService.HealthAlert += OnHealthAlert;
        _healthService.StartMonitoring();

        await LoadHealthDataAsync();
        await LoadThresholdsAsync();
    }

    private void ProviderHealthPage_Unloaded(object sender, RoutedEventArgs e)
    {
        _healthService.HealthUpdated -= OnHealthUpdated;
        _healthService.HealthAlert -= OnHealthAlert;
        _healthService.StopMonitoring();
    }

    private void OnHealthUpdated(object? sender, HealthUpdateEventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            UpdateProviderList(e.Providers);
        });
    }

    private void OnHealthAlert(object? sender, HealthAlertEventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            ActionInfoBar.Severity = InfoBarSeverity.Warning;
            ActionInfoBar.Title = $"Health Alert: {e.ProviderName}";
            ActionInfoBar.Message = e.Message;
            ActionInfoBar.IsOpen = true;
        });
    }

    private async Task LoadHealthDataAsync()
    {
        var healthData = await _healthService.GetAllProviderHealthAsync();
        UpdateProviderList(healthData);
    }

    private void UpdateProviderList(System.Collections.Generic.List<ProviderHealthData> healthData)
    {
        var ranked = healthData.OrderByDescending(p => p.OverallScore).ToList();

        _providers.Clear();
        for (int i = 0; i < ranked.Count; i++)
        {
            var provider = ranked[i];
            _providers.Add(new ProviderHealthItem
            {
                ProviderId = provider.ProviderId,
                ProviderName = provider.ProviderName,
                IsConnected = provider.IsConnected,
                OverallScore = provider.OverallScore,
                StabilityScore = provider.Breakdown.ConnectionStability.Score,
                LatencyScore = provider.Breakdown.LatencyConsistency.Score,
                CompletenessScore = provider.Breakdown.DataCompleteness.Score,
                ReconnectionScore = provider.Breakdown.ReconnectionFrequency.Score,
                AverageLatencyMs = provider.Metrics.AverageLatencyMs,
                P99LatencyMs = provider.Metrics.LatencyP99Ms,
                MessagesPerSecond = provider.Metrics.MessagesPerSecond,
                UptimePercent = provider.Metrics.UptimePercent,
                ReconnectsLastHour = provider.Metrics.ReconnectsLastHour,
                ErrorsLastHour = provider.Metrics.ErrorsLastHour,
                Rank = i + 1,
                LastUpdated = provider.LastUpdated
            });
        }

        if (_selectedProvider != null)
        {
            var updated = _providers.FirstOrDefault(p => p.ProviderId == _selectedProvider.ProviderId);
            if (updated != null)
            {
                ShowProviderDetails(updated);
            }
        }
    }

    private async Task LoadThresholdsAsync()
    {
        var thresholds = await _healthService.GetFailoverThresholdsAsync();
        MinHealthScoreBox.Value = thresholds.MinHealthScore;
        MaxLatencyBox.Value = thresholds.MaxLatencyMs;
        MaxReconnectsBox.Value = thresholds.MaxReconnectsPerHour;
        MinCompletenessBox.Value = thresholds.MinDataCompletenessPercent;
        AutoFailoverToggle.IsOn = thresholds.AutoFailoverEnabled;
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        await LoadHealthDataAsync();
        ActionInfoBar.Severity = InfoBarSeverity.Success;
        ActionInfoBar.Title = "Refreshed";
        ActionInfoBar.Message = "Health data has been refreshed.";
        ActionInfoBar.IsOpen = true;
    }

    private void ConfigureThresholds_Click(object sender, RoutedEventArgs e)
    {
        // Scroll to thresholds section
        MinHealthScoreBox.Focus(FocusState.Programmatic);
    }

    private void ProviderCard_Click(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is ProviderHealthItem provider)
        {
            _selectedProvider = provider;
            ShowProviderDetails(provider);
        }
    }

    private void ShowProviderDetails(ProviderHealthItem provider)
    {
        DetailPanel.Visibility = Visibility.Visible;
        DetailProviderName.Text = $"{provider.ProviderName} Details";

        // Score breakdown
        StabilityScoreText.Text = $"{provider.StabilityScore:F1}";
        StabilityBar.Value = provider.StabilityScore;

        LatencyScoreText.Text = $"{provider.LatencyScore:F1}";
        LatencyBar.Value = provider.LatencyScore;

        CompletenessScoreText.Text = $"{provider.CompletenessScore:F1}";
        CompletenessBar.Value = provider.CompletenessScore;

        ReconnectionScoreText.Text = $"{provider.ReconnectionScore:F1}";
        ReconnectionBar.Value = provider.ReconnectionScore;

        OverallScoreText.Text = $"{provider.OverallScore:F1}";

        // Real-time metrics
        AvgLatencyText.Text = $"{provider.AverageLatencyMs:F0} ms";
        P99LatencyText.Text = $"{provider.P99LatencyMs:F0} ms";
        MsgPerSecText.Text = $"{provider.MessagesPerSecond:N0}";
        UptimeText.Text = $"{provider.UptimePercent:F1}%";
        ReconnectsText.Text = provider.ReconnectsLastHour.ToString();
        ErrorsText.Text = provider.ErrorsLastHour.ToString();
        LastUpdatedText.Text = provider.LastUpdated.ToString("HH:mm:ss");

        // Draw trend chart
        DrawTrendChart(provider.ProviderId);
    }

    private void DrawTrendChart(string providerId)
    {
        TrendChart.Children.Clear();

        var history = _healthService.GetHealthHistory(providerId, TimeSpan.FromHours(24));
        if (history.Count < 2) return;

        var width = TrendChart.ActualWidth > 0 ? TrendChart.ActualWidth : 300;
        var height = TrendChart.ActualHeight > 0 ? TrendChart.ActualHeight : 200;
        var padding = 20.0;

        var chartWidth = width - padding * 2;
        var chartHeight = height - padding * 2;

        // Draw health score line
        var healthPoints = history.Select((h, i) => new Windows.Foundation.Point(
            padding + (i * chartWidth / (history.Count - 1)),
            padding + chartHeight - (h.OverallScore / 100.0 * chartHeight)
        )).ToList();

        var healthLine = new Polyline
        {
            Points = new PointCollection(),
            Stroke = new SolidColorBrush(Color.FromArgb(255, 88, 166, 255)),
            StrokeThickness = 2
        };

        foreach (var point in healthPoints)
        {
            healthLine.Points.Add(point);
        }

        TrendChart.Children.Add(healthLine);

        // Draw latency line (normalized to 0-100 range, assuming max 200ms)
        var latencyPoints = history.Select((h, i) => new Windows.Foundation.Point(
            padding + (i * chartWidth / (history.Count - 1)),
            padding + chartHeight - (Math.Min(h.LatencyMs, 200) / 200.0 * chartHeight)
        )).ToList();

        var latencyLine = new Polyline
        {
            Points = new PointCollection(),
            Stroke = new SolidColorBrush(Colors.Orange),
            StrokeThickness = 2,
            StrokeDashArray = new DoubleCollection { 4, 2 }
        };

        foreach (var point in latencyPoints)
        {
            latencyLine.Points.Add(point);
        }

        TrendChart.Children.Add(latencyLine);

        // Draw axis labels
        var topLabel = new TextBlock { Text = "100", FontSize = 10, Foreground = new SolidColorBrush(Colors.Gray) };
        Canvas.SetLeft(topLabel, 0);
        Canvas.SetTop(topLabel, padding - 5);
        TrendChart.Children.Add(topLabel);

        var bottomLabel = new TextBlock { Text = "0", FontSize = 10, Foreground = new SolidColorBrush(Colors.Gray) };
        Canvas.SetLeft(bottomLabel, 0);
        Canvas.SetTop(bottomLabel, height - padding - 5);
        TrendChart.Children.Add(bottomLabel);
    }

    private void CloseDetail_Click(object sender, RoutedEventArgs e)
    {
        DetailPanel.Visibility = Visibility.Collapsed;
        _selectedProvider = null;
    }

    private void ResetThresholds_Click(object sender, RoutedEventArgs e)
    {
        MinHealthScoreBox.Value = 70;
        MaxLatencyBox.Value = 500;
        MaxReconnectsBox.Value = 5;
        MinCompletenessBox.Value = 95;
        AutoFailoverToggle.IsOn = true;
    }

    private async void SaveThresholds_Click(object sender, RoutedEventArgs e)
    {
        var thresholds = new FailoverThresholds
        {
            MinHealthScore = MinHealthScoreBox.Value,
            MaxLatencyMs = MaxLatencyBox.Value,
            MaxReconnectsPerHour = (int)MaxReconnectsBox.Value,
            MinDataCompletenessPercent = MinCompletenessBox.Value,
            AutoFailoverEnabled = AutoFailoverToggle.IsOn
        };

        var success = await _healthService.UpdateFailoverThresholdsAsync(thresholds);

        ActionInfoBar.Severity = success ? InfoBarSeverity.Success : InfoBarSeverity.Error;
        ActionInfoBar.Title = success ? "Thresholds Saved" : "Save Failed";
        ActionInfoBar.Message = success
            ? "Failover thresholds have been updated."
            : "Failed to save failover thresholds.";
        ActionInfoBar.IsOpen = true;
    }
}

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
