using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using MarketDataCollector.Wpf.Contracts;
using MarketDataCollector.Wpf.Services;

namespace MarketDataCollector.Wpf.Views;

/// <summary>
/// Dashboard page showing real-time metrics and system status.
/// </summary>
public partial class DashboardPage : Page
{
    private readonly NavigationService _navigationService;
    private readonly IConnectionService _connectionService;
    private readonly StatusService _statusService;
    private readonly DispatcherTimer _refreshTimer;

    public DashboardPage()
    {
        InitializeComponent();

        _navigationService = NavigationService.Instance;
        _connectionService = ConnectionService.Instance;
        _statusService = StatusService.Instance;

        // Set up refresh timer
        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _refreshTimer.Tick += OnRefreshTimerTick;

        // Subscribe to messaging
        MessagingService.Instance.MessageReceived += OnMessageReceived;

        // Subscribe to connection changes
        _connectionService.ConnectionStateChanged += OnConnectionStateChanged;
        _connectionService.LatencyUpdated += OnLatencyUpdated;
    }

    private void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        // Start refresh timer
        _refreshTimer.Start();

        // Initial load
        RefreshStatus();
    }

    private void OnPageUnloaded(object sender, RoutedEventArgs e)
    {
        // Stop refresh timer
        _refreshTimer.Stop();

        // Unsubscribe from events to prevent memory leaks
        MessagingService.Instance.MessageReceived -= OnMessageReceived;
        _connectionService.ConnectionStateChanged -= OnConnectionStateChanged;
        _connectionService.LatencyUpdated -= OnLatencyUpdated;
    }

    private async void RefreshStatus()
    {
        try
        {
            var status = await _statusService.GetStatusAsync();

            if (status != null)
            {
                PublishedCount.Text = FormatNumber(status.Published);
                DroppedCount.Text = FormatNumber(status.Dropped);
                IntegrityCount.Text = FormatNumber(status.Integrity);
                HistoricalCount.Text = FormatNumber(status.Historical);

                ActiveProviderText.Text = status.Provider ?? "Not Connected";
            }

            // Update connection info
            UpdateConnectionInfo();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to refresh status: {ex.Message}");
        }
    }

    private void UpdateConnectionInfo()
    {
        var latency = _connectionService.LastLatencyMs;
        LatencyText.Text = latency > 0 ? $"{latency:F0} ms" : "-- ms";

        var uptime = _connectionService.Uptime;
        UptimeText.Text = uptime.HasValue
            ? $"{uptime.Value.Hours:D2}:{uptime.Value.Minutes:D2}:{uptime.Value.Seconds:D2}"
            : "--:--:--";
    }

    private void OnRefreshTimerTick(object? sender, EventArgs e)
    {
        RefreshStatus();
    }

    private void OnMessageReceived(object? sender, string message)
    {
        if (message == "RefreshStatus")
        {
            Dispatcher.Invoke(RefreshStatus);
        }
    }

    private void OnConnectionStateChanged(object? sender, ConnectionStateEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            var state = e.State == ConnectionState.Connected ? "Connected" : e.State.ToString();
            ActiveProviderText.Text = e.Provider ?? state;
            AddActivityLog($"Connection state changed: {state}");
        });
    }

    private void OnLatencyUpdated(object? sender, int latencyMs)
    {
        Dispatcher.Invoke(() =>
        {
            LatencyText.Text = $"{latencyMs} ms";
        });
    }

    private void AddActivityLog(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        var logEntry = new TextBlock
        {
            Style = (Style)FindResource("TerminalMessageStyle"),
            Text = $"  [{timestamp}] {message}"
        };

        ActivityLogPanel.Children.Add(logEntry);

        // Keep only last 50 entries
        while (ActivityLogPanel.Children.Count > 50)
        {
            ActivityLogPanel.Children.RemoveAt(0);
        }
    }

    private static string FormatNumber(long number)
    {
        return number switch
        {
            >= 1_000_000_000 => $"{number / 1_000_000_000.0:F1}B",
            >= 1_000_000 => $"{number / 1_000_000.0:F1}M",
            >= 1_000 => $"{number / 1_000.0:F1}K",
            _ => number.ToString("N0")
        };
    }

    private async void OnStartCollectorClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var provider = _connectionService.CurrentProvider ?? "default";
            var success = await _connectionService.ConnectAsync(provider);

            if (success)
            {
                AddActivityLog("Collector started successfully");
                NotificationService.Instance.ShowNotification(
                    "Collector Started",
                    "Data collection has started.",
                    NotificationType.Success);
            }
            else
            {
                AddActivityLog("Failed to start collector");
                NotificationService.Instance.ShowNotification(
                    "Start Failed",
                    "Failed to start the data collector.",
                    NotificationType.Error);
            }
        }
        catch (Exception ex)
        {
            AddActivityLog($"Error: {ex.Message}");
            NotificationService.Instance.ShowNotification(
                "Error",
                ex.Message,
                NotificationType.Error);
        }
    }

    private void OnRunBackfillClick(object sender, RoutedEventArgs e)
    {
        _navigationService.NavigateTo("Backfill");
    }

    private void OnViewLogsClick(object sender, RoutedEventArgs e)
    {
        _navigationService.NavigateTo("ServiceManager");
    }
}
