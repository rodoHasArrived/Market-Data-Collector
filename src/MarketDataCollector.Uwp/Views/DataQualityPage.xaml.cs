using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using MarketDataCollector.Uwp.ViewModels;
using MarketDataCollector.Uwp.Services;

namespace MarketDataCollector.Uwp.Views;

/// <summary>
/// Page for monitoring data quality scores, alerts, and trends.
/// </summary>
public sealed partial class DataQualityPage : Page
{
    public DataQualityViewModel ViewModel { get; }

    public DataQualityPage()
    {
        ViewModel = new DataQualityViewModel();
        InitializeComponent();
        DataContext = ViewModel;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await InitializePageAsync();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        ViewModel.Dispose();
    }

    private async Task InitializePageAsync()
    {
        LoadingRing.IsActive = true;
        LoadingRing.Visibility = Visibility.Visible;

        try
        {
            await ViewModel.InitializeAsync();
            UpdateUI();
        }
        catch (Exception ex)
        {
            ShowError($"Failed to load data quality information: {ex.Message}");
        }
        finally
        {
            LoadingRing.IsActive = false;
            LoadingRing.Visibility = Visibility.Collapsed;
        }
    }

    private void UpdateUI()
    {
        // Update overall score display
        OverallScoreText.Text = $"{ViewModel.OverallScore:F1}";
        OverallGradeText.Text = ViewModel.OverallGrade;
        StatusText.Text = ViewModel.OverallStatus;

        // Update status badge color
        StatusBadge.Background = ViewModel.OverallScore switch
        {
            >= 90 => (Microsoft.UI.Xaml.Media.Brush)Resources["SuccessColorBrush"],
            >= 75 => (Microsoft.UI.Xaml.Media.Brush)Resources["InfoColorBrush"],
            >= 50 => (Microsoft.UI.Xaml.Media.Brush)Resources["WarningColorBrush"],
            _ => (Microsoft.UI.Xaml.Media.Brush)Resources["ErrorColorBrush"]
        };

        // Update file health counts
        HealthyFilesText.Text = ViewModel.HealthyFiles.ToString("N0");
        WarningFilesText.Text = ViewModel.WarningFiles.ToString("N0");
        CriticalFilesText.Text = ViewModel.CriticalFiles.ToString("N0");

        // Update alert counts
        if (ViewModel.ActiveAlerts > 0)
        {
            AlertCountBadge.Visibility = Visibility.Visible;
            AlertCountText.Text = ViewModel.ActiveAlerts.ToString();
        }
        else
        {
            AlertCountBadge.Visibility = Visibility.Collapsed;
        }

        UnacknowledgedText.Text = ViewModel.UnacknowledgedAlerts.ToString();
        TotalActiveAlertsText.Text = ViewModel.ActiveAlerts.ToString();

        // Update last check time
        LastCheckTimeText.Text = FormatRelativeTime(ViewModel.LastChecked);

        // Update trend display
        UpdateTrendDisplay();

        // Bind lists
        SymbolQualityList.ItemsSource = ViewModel.SymbolQualities;
        AlertsList.ItemsSource = ViewModel.Alerts;
        AnomaliesList.ItemsSource = ViewModel.Anomalies;

        // Update no data messages
        NoAlertsText.Visibility = ViewModel.Alerts.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        NoAnomaliesText.Visibility = ViewModel.Anomalies.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        // Update anomaly counts
        UpdateAnomalyCounts();
    }

    private void UpdateTrendDisplay()
    {
        var trendChange = ViewModel.TrendChangePercent;
        var isPositive = trendChange >= 0;

        TrendIcon.Glyph = isPositive ? "\uE70E" : "\uE70D";
        TrendText.Text = $"{(isPositive ? "+" : "")}{trendChange:F1}% this {GetTimeWindowLabel()}";

        var trendColor = trendChange > 0.5
            ? (Microsoft.UI.Xaml.Media.Brush)Resources["SuccessColorBrush"]
            : trendChange < -0.5
                ? (Microsoft.UI.Xaml.Media.Brush)Resources["ErrorColorBrush"]
                : (Microsoft.UI.Xaml.Media.Brush)Resources["WarningColorBrush"];

        TrendIcon.Foreground = trendColor;
        TrendText.Foreground = trendColor;
        TrendBadge.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
            Microsoft.UI.Colors.Transparent);

        // Update trend stats
        if (ViewModel.TrendPoints.Count > 0)
        {
            var scores = ViewModel.TrendPoints.Select(t => t.Score).ToList();
            AvgScoreText.Text = $"{scores.Average():F1}%";
            MinScoreText.Text = $"{scores.Min():F1}%";
            MaxScoreText.Text = $"{scores.Max():F1}%";

            if (scores.Count > 1)
            {
                var avg = scores.Average();
                var sumOfSquares = scores.Sum(s => Math.Pow(s - avg, 2));
                var stdDev = Math.Sqrt(sumOfSquares / scores.Count);
                StdDevText.Text = $"{stdDev:F1}%";
            }
        }
    }

    private void UpdateAnomalyCounts()
    {
        var anomalies = ViewModel.Anomalies;

        CrossedSpreadCount.Text = anomalies.Count(a => a.Type == "CrossedSpread").ToString();
        StaleDataCount.Text = anomalies.Count(a => a.Type == "StaleData").ToString();
        FutureTimestampCount.Text = anomalies.Count(a => a.Type == "FutureTimestamp").ToString();
        NegativePriceCount.Text = anomalies.Count(a => a.Type == "NegativePrice").ToString();
        SequenceGapCount.Text = anomalies.Count(a => a.Type == "SequenceGap").ToString();

        if (anomalies.Count > 0)
        {
            AnomalyCountBadge.Visibility = Visibility.Visible;
            AnomalyCountText.Text = anomalies.Count.ToString();
        }
        else
        {
            AnomalyCountBadge.Visibility = Visibility.Collapsed;
        }
    }

    private string GetTimeWindowLabel()
    {
        return ViewModel.SelectedTimeWindow switch
        {
            "1d" => "day",
            "7d" => "week",
            "30d" => "month",
            "90d" => "quarter",
            _ => "period"
        };
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.RefreshAsync();
        UpdateUI();
    }

    private async void RunQualityCheck_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "Run Quality Check",
            Content = new StackPanel
            {
                Spacing = 12,
                Children =
                {
                    new TextBlock { Text = "Enter path or symbol to check:" },
                    new TextBox { Name = "PathBox", PlaceholderText = "/data/live/SPY or SPY" }
                }
            },
            PrimaryButtonText = "Run Check",
            CloseButtonText = "Cancel",
            XamlRoot = XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            var pathBox = (dialog.Content as StackPanel)?.Children
                .OfType<TextBox>()
                .FirstOrDefault();

            if (pathBox != null && !string.IsNullOrWhiteSpace(pathBox.Text))
            {
                await ViewModel.RunQualityCheckAsync(pathBox.Text);
                UpdateUI();
                ShowSuccess("Quality check completed");
            }
        }
    }

    private void ViewAlerts_Click(object sender, RoutedEventArgs e)
    {
        // Scroll to alerts section or expand it
        AlertsList.Focus(FocusState.Programmatic);
    }

    private void TimeWindow_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (TimeWindowCombo.SelectedItem is ComboBoxItem item && item.Tag is string window)
        {
            _ = ViewModel.ChangeTimeWindowAsync(window);
            UpdateTrendDisplay();
        }
    }

    private void SeverityFilter_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (SeverityFilterCombo.SelectedItem is ComboBoxItem item && item.Tag is string severity)
        {
            _ = ViewModel.FilterBySeverityAsync(severity);
        }
    }

    private void SymbolFilter_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
        {
            var query = sender.Text.ToUpperInvariant();
            var filtered = ViewModel.SymbolQualities
                .Where(s => string.IsNullOrEmpty(query) || s.Symbol.Contains(query))
                .ToList();

            SymbolQualityList.ItemsSource = filtered;
        }
    }

    private void SymbolQuality_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SymbolQualityList.SelectedItem is SymbolQualityItem selected)
        {
            _ = ViewModel.ViewSymbolQualityAsync(selected.Symbol);
        }
    }

    private async void AcknowledgeAlert_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string alertId)
        {
            await ViewModel.AcknowledgeAlertAsync(alertId);
            UpdateUI();
        }
    }

    private async void AcknowledgeAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var alert in ViewModel.Alerts.Where(a => !a.IsAcknowledged).ToList())
        {
            await ViewModel.AcknowledgeAlertAsync(alert.Id);
        }
        UpdateUI();
        ShowSuccess("All alerts acknowledged");
    }

    private void AnomalyType_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (AnomalyTypeCombo.SelectedItem is ComboBoxItem item && item.Tag is string type)
        {
            var filtered = type == "All"
                ? ViewModel.Anomalies.ToList()
                : ViewModel.Anomalies.Where(a => a.Type == type).ToList();

            AnomaliesList.ItemsSource = filtered;
            NoAnomaliesText.Visibility = filtered.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private static string FormatRelativeTime(DateTime time)
    {
        var span = DateTime.UtcNow - time;
        return span.TotalSeconds < 60 ? "Just now"
             : span.TotalMinutes < 60 ? $"{(int)span.TotalMinutes} minutes ago"
             : span.TotalHours < 24 ? $"{(int)span.TotalHours} hours ago"
             : $"{(int)span.TotalDays} days ago";
    }

    private void ShowSuccess(string message)
    {
        PageInfoBar.Message = message;
        PageInfoBar.Severity = InfoBarSeverity.Success;
        PageInfoBar.IsOpen = true;
    }

    private void ShowError(string message)
    {
        PageInfoBar.Message = message;
        PageInfoBar.Severity = InfoBarSeverity.Error;
        PageInfoBar.IsOpen = true;
    }
}
