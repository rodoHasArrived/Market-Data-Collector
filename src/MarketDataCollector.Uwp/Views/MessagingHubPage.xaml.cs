using System;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using MarketDataCollector.Uwp.Services;

namespace MarketDataCollector.Uwp.Views;

/// <summary>
/// Page for monitoring MassTransit message bus activity.
/// </summary>
public sealed partial class MessagingHubPage : Page
{
    private readonly MessagingService _messagingService;
    private readonly DispatcherTimer _refreshTimer;
    private readonly ObservableCollection<ActivityDisplay> _recentActivity = new();

    public MessagingHubPage()
    {
        this.InitializeComponent();
        _messagingService = MessagingService.Instance;
        RecentActivityList.ItemsSource = _recentActivity;

        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _refreshTimer.Tick += RefreshTimer_Tick;

        Loaded += MessagingHubPage_Loaded;
        Unloaded += MessagingHubPage_Unloaded;
    }

    private async void MessagingHubPage_Loaded(object sender, RoutedEventArgs e)
    {
        await RefreshAllAsync();
        _refreshTimer.Start();
    }

    private void MessagingHubPage_Unloaded(object sender, RoutedEventArgs e)
    {
        _refreshTimer.Stop();
    }

    private async void RefreshTimer_Tick(object? sender, object e)
    {
        if (LiveActivityToggle.IsOn)
        {
            await RefreshActivityAsync();
        }
    }

    private async System.Threading.Tasks.Task RefreshAllAsync()
    {
        await RefreshStatusAsync();
        await RefreshStatisticsAsync();
        await RefreshConsumersAsync();
        await RefreshEndpointsAsync();
        await RefreshErrorQueueAsync();
        await RefreshActivityAsync();
    }

    private async System.Threading.Tasks.Task RefreshStatusAsync()
    {
        try
        {
            var status = await _messagingService.GetStatusAsync();

            ConnectionIndicator.Fill = new SolidColorBrush(
                status.IsConnected ? Windows.UI.Color.FromArgb(255, 72, 187, 120) : Windows.UI.Color.FromArgb(255, 245, 101, 101));

            ConnectionStatusText.Text = status.IsConnected ? "Connected" : "Disconnected";

            var config = await _messagingService.GetConfigurationAsync();
            ConnectionDetailsText.Text = $"Transport: {config.TransportType} | Publishing: {(config.PublishingEnabled ? "Enabled" : "Disabled")}";

            // Update config UI
            TransportTypeCombo.SelectedIndex = config.TransportType switch
            {
                "RabbitMQ" => 1,
                "AzureServiceBus" => 2,
                "AmazonSqs" => 3,
                _ => 0
            };
            HostBox.Text = config.Host ?? "";
            EnablePublishingToggle.IsOn = config.PublishingEnabled;
        }
        catch
        {
            ConnectionStatusText.Text = "Error";
        }
    }

    private async System.Threading.Tasks.Task RefreshStatisticsAsync()
    {
        try
        {
            var stats = await _messagingService.GetStatisticsAsync();

            PublishedCountText.Text = stats.TotalPublished.ToString("N0");
            ConsumedCountText.Text = stats.TotalConsumed.ToString("N0");
            FailedCountText.Text = stats.TotalFailed.ToString("N0");
            LatencyText.Text = $"{stats.AverageLatencyMs:F0}ms";
            PublishRateText.Text = $"{stats.PublishedPerSecond}/s";
            ConsumeRateText.Text = $"{stats.ConsumedPerSecond}/s";

            if (stats.MessageTypeBreakdown != null)
            {
                MessageTypesList.ItemsSource = stats.MessageTypeBreakdown.Select(kvp => new MessageTypeDisplay
                {
                    Type = kvp.Key,
                    CountText = kvp.Value.ToString("N0")
                }).ToList();
            }
        }
        catch
        {
            // Ignore stats errors
        }
    }

    private async System.Threading.Tasks.Task RefreshConsumersAsync()
    {
        try
        {
            var result = await _messagingService.GetConsumersAsync();
            if (result.Success && result.Consumers.Count > 0)
            {
                ConsumersList.ItemsSource = result.Consumers.Select(c => new ConsumerDisplay
                {
                    Name = c.Name,
                    MessageType = c.MessageType,
                    ConsumedText = c.MessagesConsumed.ToString("N0"),
                    AvgTimeText = $"{c.AverageProcessingMs:F0}ms",
                    StatusColor = new SolidColorBrush(
                        c.IsActive ? Windows.UI.Color.FromArgb(255, 72, 187, 120) : Windows.UI.Color.FromArgb(255, 128, 128, 128))
                }).ToList();
                NoConsumersText.Visibility = Visibility.Collapsed;
            }
            else
            {
                ConsumersList.ItemsSource = null;
                NoConsumersText.Visibility = Visibility.Visible;
            }
        }
        catch
        {
            NoConsumersText.Visibility = Visibility.Visible;
        }
    }

    private async System.Threading.Tasks.Task RefreshEndpointsAsync()
    {
        try
        {
            var result = await _messagingService.GetEndpointsAsync();
            if (result.Success && result.Endpoints.Count > 0)
            {
                EndpointsList.ItemsSource = result.Endpoints.Select(ep => new EndpointDisplay
                {
                    Name = ep.Name,
                    Address = ep.Address,
                    TypeIcon = ep.Type == "Queue" ? "\uE8B7" : "\uE8F4",
                    PendingText = $"{ep.PendingMessages} pending",
                    HealthText = ep.IsHealthy ? "OK" : "Error",
                    HealthBackground = new SolidColorBrush(
                        ep.IsHealthy ? Windows.UI.Color.FromArgb(50, 72, 187, 120) : Windows.UI.Color.FromArgb(50, 245, 101, 101))
                }).ToList();
                NoEndpointsText.Visibility = Visibility.Collapsed;
            }
            else
            {
                EndpointsList.ItemsSource = null;
                NoEndpointsText.Visibility = Visibility.Visible;
            }
        }
        catch
        {
            NoEndpointsText.Visibility = Visibility.Visible;
        }
    }

    private async System.Threading.Tasks.Task RefreshErrorQueueAsync()
    {
        try
        {
            var result = await _messagingService.GetErrorQueueMessagesAsync();
            ErrorCountBadge.Text = $"({result.TotalCount})";

            if (result.Success && result.Messages.Count > 0)
            {
                ErrorMessagesList.ItemsSource = result.Messages.Select(m => new ErrorMessageDisplay
                {
                    MessageType = m.MessageType,
                    Error = m.Error,
                    TimestampText = m.Timestamp.ToString("HH:mm:ss")
                }).ToList();
                NoErrorsText.Visibility = Visibility.Collapsed;
            }
            else
            {
                ErrorMessagesList.ItemsSource = null;
                NoErrorsText.Visibility = Visibility.Visible;
            }
        }
        catch
        {
            NoErrorsText.Visibility = Visibility.Visible;
        }
    }

    private async System.Threading.Tasks.Task RefreshActivityAsync()
    {
        try
        {
            var result = await _messagingService.GetRecentActivityAsync(20);
            if (result.Success)
            {
                _recentActivity.Clear();
                foreach (var msg in result.Messages)
                {
                    _recentActivity.Add(new ActivityDisplay
                    {
                        MessageType = msg.MessageType,
                        Symbol = msg.Symbol ?? "",
                        DirectionIcon = msg.Direction == "Published" ? "\uE898" : "\uE896",
                        DirectionColor = new SolidColorBrush(
                            msg.Direction == "Published" ? Windows.UI.Color.FromArgb(255, 88, 166, 255) : Windows.UI.Color.FromArgb(255, 72, 187, 120)),
                        TimeText = msg.Timestamp.ToString("HH:mm:ss")
                    });
                }
            }
        }
        catch
        {
            // Ignore activity errors
        }
    }

    private async void TestConnection_Click(object sender, RoutedEventArgs e)
    {
        var result = await _messagingService.TestConnectionAsync();

        var dialog = new ContentDialog
        {
            Title = result.Success ? "Connection Successful" : "Connection Failed",
            Content = result.Success
                ? $"Connected successfully. Latency: {result.LatencyMs:F0}ms"
                : result.Error ?? "Unknown error",
            CloseButtonText = "OK",
            XamlRoot = this.XamlRoot
        };
        await dialog.ShowAsync();
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        await RefreshAllAsync();
    }

    private async void SaveConfig_Click(object sender, RoutedEventArgs e)
    {
        var config = new BusConfigurationUpdate
        {
            TransportType = (TransportTypeCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString(),
            Host = HostBox.Text,
            Port = (int?)PortBox.Value,
            Username = UsernameBox.Text,
            Password = PasswordBox.Password,
            PublishingEnabled = EnablePublishingToggle.IsOn
        };

        var success = await _messagingService.UpdateConfigurationAsync(config);
        if (success)
        {
            await RefreshStatusAsync();
        }
    }

    private async void ClearErrors_Click(object sender, RoutedEventArgs e)
    {
        // This would purge the error queue
        await RefreshErrorQueueAsync();
    }

    private void LiveActivity_Toggled(object sender, RoutedEventArgs e)
    {
        if (LiveActivityToggle.IsOn)
        {
            _refreshTimer.Start();
        }
        else
        {
            _refreshTimer.Stop();
        }
    }
}

public class ConsumerDisplay
{
    public string Name { get; set; } = string.Empty;
    public string MessageType { get; set; } = string.Empty;
    public string ConsumedText { get; set; } = string.Empty;
    public string AvgTimeText { get; set; } = string.Empty;
    public SolidColorBrush? StatusColor { get; set; }
}

public class EndpointDisplay
{
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string TypeIcon { get; set; } = string.Empty;
    public string PendingText { get; set; } = string.Empty;
    public string HealthText { get; set; } = string.Empty;
    public SolidColorBrush? HealthBackground { get; set; }
}

public class ErrorMessageDisplay
{
    public string MessageType { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
    public string TimestampText { get; set; } = string.Empty;
}

public class ActivityDisplay
{
    public string MessageType { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public string DirectionIcon { get; set; } = string.Empty;
    public SolidColorBrush? DirectionColor { get; set; }
    public string TimeText { get; set; } = string.Empty;
}

public class MessageTypeDisplay
{
    public string Type { get; set; } = string.Empty;
    public string CountText { get; set; } = string.Empty;
}
