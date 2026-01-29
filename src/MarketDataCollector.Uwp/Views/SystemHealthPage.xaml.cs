using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using MarketDataCollector.Uwp.Services;
using Windows.UI;

namespace MarketDataCollector.Uwp.Views;

/// <summary>
/// Page for monitoring system health, connection diagnostics, and resources.
/// </summary>
public sealed partial class SystemHealthPage : Page
{
    private readonly SystemHealthService _healthService;
    private readonly DispatcherTimer _refreshTimer;
    private List<ProviderHealthDisplay> _providerHealthItems = new();
    private List<SystemEvent> _allEvents = new();

    public SystemHealthPage()
    {
        InitializeComponent();
        _healthService = SystemHealthService.Instance;

        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5)
        };
        _refreshTimer.Tick += RefreshTimer_Tick;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await LoadDataAsync();
        _refreshTimer.Start();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        _refreshTimer.Stop();
    }

    private async void RefreshTimer_Tick(object? sender, object e)
    {
        try
        {
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in RefreshTimer_Tick: {ex.Message}");
        }
    }

    private async Task LoadDataAsync()
    {
        try
        {
            await Task.WhenAll(
                LoadHealthSummaryAsync(),
                LoadProviderHealthAsync(),
                LoadStorageHealthAsync(),
                LoadEventsAsync(),
                LoadMetricsAsync()
            );
        }
        catch (Exception ex)
        {
            ShowError($"Error loading health data: {ex.Message}");
        }
    }

    private async Task LoadHealthSummaryAsync()
    {
        var summary = await _healthService.GetHealthSummaryAsync();
        if (summary == null) return;

        // Update status banner
        var isHealthy = summary.IsHealthy;
        StatusBanner.Background = new SolidColorBrush(isHealthy
            ? Color.FromArgb(255, 72, 187, 120)
            : Color.FromArgb(255, 245, 101, 101));
        StatusIcon.Glyph = isHealthy ? "\uE73E" : "\uEA39";
        StatusText.Text = summary.OverallStatus;
        StatusDescription.Text = isHealthy
            ? "All systems operational"
            : $"{summary.UnhealthyConnections} connection(s) have issues";

        // Update stats
        UptimeText.Text = FormatUptime(summary.Uptime);
        EventsText.Text = $"{summary.EventsLast24Hours / 86400:N0}";
        LatencyText.Text = $"{summary.AverageLatencyMs:F0}ms";

        // Connection stats
        ConnectionsHealthyText.Text = $"{summary.HealthyConnections}/{summary.ActiveConnections}";
        ConnectionsStatusText.Text = summary.UnhealthyConnections == 0 ? "Healthy" : "Issues";
        ConnectionsStatusText.Foreground = summary.UnhealthyConnections == 0
            ? (Brush)Resources["SuccessColorBrush"]
            : (Brush)Resources["ErrorColorBrush"];

        // Storage
        StorageUsedText.Text = $"{summary.StorageUsedPercent:F0}%";
        StorageProgressBar.Value = summary.StorageUsedPercent;

        // Alerts
        ActiveAlertsText.Text = summary.ActiveAlerts.ToString();
    }

    private async Task LoadProviderHealthAsync()
    {
        var providers = await _healthService.GetProviderHealthAsync();
        if (providers == null) return;

        _providerHealthItems = providers.Select(p => new ProviderHealthDisplay
        {
            Provider = p.Provider,
            DisplayName = p.DisplayName,
            Status = p.Status,
            StatusColor = p.IsConnected ? Color.FromArgb(255, 72, 187, 120)
                        : p.IsEnabled ? Color.FromArgb(255, 245, 101, 101)
                        : Color.FromArgb(255, 160, 174, 192),
            LatencyText = $"{p.LatencyMs:F0}ms",
            EventsPerSecond = p.EventsPerSecond.ToString(),
            LastEventText = p.LastEventAt.HasValue ? FormatRelativeTime(p.LastEventAt.Value) : "Never"
        }).ToList();

        ProviderHealthList.ItemsSource = _providerHealthItems;
        NoProvidersText.Visibility = _providerHealthItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private async Task LoadStorageHealthAsync()
    {
        var storage = await _healthService.GetStorageHealthAsync();
        if (storage == null) return;

        StoragePercentText.Text = $"{storage.UsedPercent:F1}%";
        StorageBar.Value = storage.UsedPercent;
        StorageUsedBytesText.Text = FormatBytes(storage.UsedBytes);
        StorageAvailableText.Text = FormatBytes(storage.AvailableBytes);
        TotalFilesText.Text = storage.TotalFiles.ToString("N0");
        LastStorageCheckText.Text = FormatRelativeTime(storage.LastChecked);
        CorruptedFilesText.Text = storage.CorruptedFiles.ToString();
        OrphanedFilesText.Text = storage.OrphanedFiles.ToString();

        if (storage.Issues.Count > 0)
        {
            StorageIssuesPanel.Visibility = Visibility.Visible;
            StorageIssuesList.ItemsSource = storage.Issues;
        }
        else
        {
            StorageIssuesPanel.Visibility = Visibility.Collapsed;
        }
    }

    private async Task LoadEventsAsync()
    {
        var events = await _healthService.GetRecentEventsAsync(50);
        if (events == null) return;

        _allEvents = events;
        ApplyEventFilter();
    }

    private async Task LoadMetricsAsync()
    {
        var metrics = await _healthService.GetSystemMetricsAsync();
        if (metrics == null) return;

        CpuUsageText.Text = $"{metrics.CpuUsagePercent:F1}%";
        MemoryDetailText.Text = FormatBytes(metrics.MemoryUsedBytes);
        MemoryUsedText.Text = $"{metrics.MemoryUsedPercent:F0}%";
        MemoryProgressBar.Value = metrics.MemoryUsedPercent;
        ThreadCountText.Text = metrics.ThreadCount.ToString();
        EventRateText.Text = $"{metrics.EventsPerSecond:F0}/s";
        PendingOpsText.Text = metrics.PendingOperations.ToString();
    }

    private void ApplyEventFilter()
    {
        var filter = (EventFilterCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        var filtered = filter == "All" || string.IsNullOrEmpty(filter)
            ? _allEvents
            : _allEvents.Where(e => e.Severity == filter).ToList();

        EventsList.ItemsSource = filtered.Select(e => new SystemEventDisplay
        {
            EventType = e.EventType,
            Source = e.Source,
            Message = e.Message,
            Timestamp = e.Timestamp.ToString("g"),
            SeverityColor = e.Severity switch
            {
                "Error" => Color.FromArgb(255, 245, 101, 101),
                "Warning" => Color.FromArgb(255, 237, 137, 54),
                _ => Color.FromArgb(255, 88, 166, 255)
            }
        }).ToList();

        NoEventsText.Visibility = filtered.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        LoadingRing.IsActive = true;
        LoadingRing.Visibility = Visibility.Visible;
        await LoadDataAsync();
        LoadingRing.IsActive = false;
        LoadingRing.Visibility = Visibility.Collapsed;
    }

    private async void GenerateDiagnostics_Click(object sender, RoutedEventArgs e)
    {
        LoadingRing.IsActive = true;
        LoadingRing.Visibility = Visibility.Visible;

        try
        {
            var bundle = await _healthService.GenerateDiagnosticBundleAsync();
            if (bundle != null)
            {
                ShowSuccess($"Diagnostic bundle created: {bundle.FilePath}");
            }
        }
        catch (Exception ex)
        {
            ShowError($"Failed to generate diagnostics: {ex.Message}");
        }
        finally
        {
            LoadingRing.IsActive = false;
            LoadingRing.Visibility = Visibility.Collapsed;
        }
    }

    private async void TestConnection_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string provider) return;

        var result = await _healthService.TestConnectionAsync(provider);
        if (result != null)
        {
            if (result.Success)
            {
                ShowSuccess($"Connection test successful ({result.LatencyMs:F0}ms)");
            }
            else
            {
                ShowError($"Connection test failed: {result.Message}");
            }
        }
    }

    private async void ViewDiagnostics_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string provider) return;

        var diagnostics = await _healthService.GetProviderDiagnosticsAsync(provider);
        if (diagnostics == null) return;

        var content = new StackPanel
        {
            Spacing = 12,
            Children =
            {
                new TextBlock { Text = $"Provider: {diagnostics.Provider}", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold },
                new TextBlock { Text = $"Connection State: {diagnostics.ConnectionState}" },
                new TextBlock { Text = $"Latency: {diagnostics.LatencyMs:F0}ms" },
                new TextBlock { Text = $"Reconnect Attempts: {diagnostics.ReconnectAttempts}" },
                new TextBlock { Text = $"Active Subscriptions: {diagnostics.ActiveSubscriptions.Count}" }
            }
        };

        if (diagnostics.Issues.Count > 0)
        {
            content.Children.Add(new TextBlock { Text = "Issues:", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Margin = new Thickness(0, 8, 0, 0) });
            foreach (var issue in diagnostics.Issues)
            {
                content.Children.Add(new TextBlock
                {
                    Text = $"[{issue.Severity}] {issue.Description}",
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = new SolidColorBrush(issue.Severity == "Error" ? Colors.Red : Colors.Orange)
                });
            }
        }

        await new ContentDialog
        {
            Title = $"Diagnostics - {provider}",
            Content = new ScrollViewer { Content = content, MaxHeight = 400 },
            CloseButtonText = "Close",
            XamlRoot = XamlRoot
        }.ShowAsync();
    }

    private void ViewAlerts_Click(object sender, RoutedEventArgs e)
    {
        // Navigate to data quality page
        Frame.Navigate(typeof(DataQualityPage));
    }

    private async void RunStorageCheck_Click(object sender, RoutedEventArgs e)
    {
        LoadingRing.IsActive = true;
        LoadingRing.Visibility = Visibility.Visible;

        try
        {
            await LoadStorageHealthAsync();
            ShowSuccess("Storage check completed");
        }
        finally
        {
            LoadingRing.IsActive = false;
            LoadingRing.Visibility = Visibility.Collapsed;
        }
    }

    private void EventFilter_Changed(object sender, SelectionChangedEventArgs e)
    {
        ApplyEventFilter();
    }

    private static string FormatUptime(TimeSpan uptime)
    {
        if (uptime.TotalDays >= 1)
            return $"{(int)uptime.TotalDays}d {uptime.Hours}h {uptime.Minutes}m";
        if (uptime.TotalHours >= 1)
            return $"{(int)uptime.TotalHours}h {uptime.Minutes}m";
        return $"{uptime.Minutes}m {uptime.Seconds}s";
    }

    private static string FormatRelativeTime(DateTime time)
    {
        var span = DateTime.UtcNow - time;
        return span.TotalSeconds < 60 ? "Just now"
             : span.TotalMinutes < 60 ? $"{(int)span.TotalMinutes}m ago"
             : span.TotalHours < 24 ? $"{(int)span.TotalHours}h ago"
             : $"{(int)span.TotalDays}d ago";
    }

    private static string FormatBytes(long bytes)
    {
        return bytes >= 1_000_000_000 ? $"{bytes / 1_000_000_000.0:F1} GB"
             : bytes >= 1_000_000 ? $"{bytes / 1_000_000.0:F1} MB"
             : bytes >= 1_000 ? $"{bytes / 1_000.0:F1} KB"
             : $"{bytes} B";
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

/// <summary>
/// Display model for provider health.
/// </summary>
public class ProviderHealthDisplay
{
    public string Provider { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public Color StatusColor { get; set; }
    public string LatencyText { get; set; } = string.Empty;
    public string EventsPerSecond { get; set; } = string.Empty;
    public string LastEventText { get; set; } = string.Empty;
}

/// <summary>
/// Display model for system events.
/// </summary>
public class SystemEventDisplay
{
    public string EventType { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Timestamp { get; set; } = string.Empty;
    public Color SeverityColor { get; set; }
}
