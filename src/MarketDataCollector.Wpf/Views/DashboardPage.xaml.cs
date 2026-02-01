using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using MarketDataCollector.Wpf.Services;
using Microsoft.Extensions.DependencyInjection;

namespace MarketDataCollector.Wpf.Views;

/// <summary>
/// Dashboard page showing system status and quick actions.
/// </summary>
public partial class DashboardPage : Page
{
    private readonly IConnectionService _connectionService;
    private readonly INavigationService _navigationService;
    private readonly INotificationService _notificationService;
    private readonly DispatcherTimer _updateTimer;
    private DateTime _startTime = DateTime.UtcNow;
    private readonly ObservableCollection<ActivityLogEntry> _activityLog = new();

    public DashboardPage()
    {
        InitializeComponent();

        // Get services from DI
        _connectionService = App.Services?.GetRequiredService<IConnectionService>()!;
        _navigationService = App.Services?.GetRequiredService<INavigationService>()!;
        _notificationService = App.Services?.GetRequiredService<INotificationService>()!;

        // Set up activity log
        ActivityLog.ItemsSource = _activityLog;

        // Subscribe to connection status changes
        _connectionService.ConnectionStatusChanged += OnConnectionStatusChanged;

        // Set up periodic update timer
        _updateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _updateTimer.Tick += UpdateTimer_Tick;
        _updateTimer.Start();

        // Add initial log entry
        AddActivityLogEntry("Dashboard loaded");

        // Initial status refresh
        _ = RefreshStatusAsync();

        // Clean up when unloaded
        Unloaded += OnUnloaded;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _updateTimer.Stop();
        _connectionService.ConnectionStatusChanged -= OnConnectionStatusChanged;
    }

    private void UpdateTimer_Tick(object? sender, EventArgs e)
    {
        // Update uptime
        var uptime = DateTime.UtcNow - _startTime;
        UptimeText.Text = $"{uptime.Hours:D2}:{uptime.Minutes:D2}:{uptime.Seconds:D2}";
    }

    private async Task RefreshStatusAsync()
    {
        try
        {
            // This would normally call the backend API to get status
            // For now, just update with placeholder data
            SymbolCountText.Text = "5";
            EventRateText.Text = "127";

            AddActivityLogEntry("Status refreshed");
        }
        catch (Exception ex)
        {
            AddActivityLogEntry($"Error refreshing status: {ex.Message}");
        }
    }

    private void OnConnectionStatusChanged(object? sender, ConnectionStatusChangedEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            ConnectionStatusText.Text = e.Status;
            AddActivityLogEntry($"Connection status: {e.Status}");
        });
    }

    private void AddActivityLogEntry(string message)
    {
        Dispatcher.Invoke(() =>
        {
            _activityLog.Insert(0, new ActivityLogEntry
            {
                Timestamp = DateTime.Now,
                Message = message
            });

            // Keep only last 50 entries
            while (_activityLog.Count > 50)
            {
                _activityLog.RemoveAt(_activityLog.Count - 1);
            }
        });
    }

    private async void StartCollectionButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            AddActivityLogEntry("Starting data collection...");
            var success = await _connectionService.ConnectAsync("default");

            if (success)
            {
                await _notificationService.NotifySuccessAsync(
                    "Collection Started",
                    "Data collection has started successfully.");
                AddActivityLogEntry("Data collection started");
            }
            else
            {
                await _notificationService.NotifyErrorAsync(
                    "Start Failed",
                    "Failed to start data collection.");
                AddActivityLogEntry("Failed to start data collection");
            }
        }
        catch (Exception ex)
        {
            await _notificationService.NotifyErrorAsync(
                "Error",
                $"Error starting collection: {ex.Message}");
            AddActivityLogEntry($"Error: {ex.Message}");
        }
    }

    private async void StopCollectionButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            AddActivityLogEntry("Stopping data collection...");
            await _connectionService.DisconnectAsync();

            await _notificationService.NotifyWarningAsync(
                "Collection Stopped",
                "Data collection has been stopped.");
            AddActivityLogEntry("Data collection stopped");
        }
        catch (Exception ex)
        {
            await _notificationService.NotifyErrorAsync(
                "Error",
                $"Error stopping collection: {ex.Message}");
            AddActivityLogEntry($"Error: {ex.Message}");
        }
    }

    private void ViewSymbolsButton_Click(object sender, RoutedEventArgs e)
    {
        _navigationService.NavigateTo("Symbols");
    }

    private void RunBackfillButton_Click(object sender, RoutedEventArgs e)
    {
        _navigationService.NavigateTo("Backfill");
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        _navigationService.NavigateTo("Settings");
    }
}

/// <summary>
/// Activity log entry model.
/// </summary>
public class ActivityLogEntry
{
    public DateTime Timestamp { get; set; }
    public string Message { get; set; } = string.Empty;
}
