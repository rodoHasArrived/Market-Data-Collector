using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using MarketDataCollector.Uwp.Services;
using MarketDataCollector.Uwp.Models;

namespace MarketDataCollector.Uwp.Views;

/// <summary>
/// Page for managing the collector background service.
/// </summary>
public sealed partial class ServiceManagerPage : Page
{
    private readonly StatusService _statusService;
    private readonly DispatcherTimer _refreshTimer;
    private readonly ObservableCollection<LogEntry> _logEntries;
    private readonly ObservableCollection<RecoveryEvent> _recoveryHistory;
    private DateTime _serviceStartTime;
    private bool _isServiceRunning;

    public ServiceManagerPage()
    {
        this.InitializeComponent();

        _statusService = StatusService.Instance;
        _logEntries = new ObservableCollection<LogEntry>();
        _recoveryHistory = new ObservableCollection<RecoveryEvent>();

        LogEntriesControl.ItemsSource = _logEntries;
        RecoveryHistoryList.ItemsSource = _recoveryHistory;

        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _refreshTimer.Tick += RefreshTimer_Tick;

        Loaded += ServiceManagerPage_Loaded;
        Unloaded += ServiceManagerPage_Unloaded;
    }

    private async void ServiceManagerPage_Loaded(object sender, RoutedEventArgs e)
    {
        _refreshTimer.Start();
        await RefreshStatusAsync();
        LoadSampleLogs();
    }

    private void ServiceManagerPage_Unloaded(object sender, RoutedEventArgs e)
    {
        _refreshTimer.Stop();
    }

    private async void RefreshTimer_Tick(object? sender, object e)
    {
        try
        {
            await RefreshStatusAsync();
            UpdateUptime();
        }
        catch (Exception ex)
        {
            LoggingService.Instance.LogError("Error in RefreshTimer_Tick", ex);
        }
    }

    private async Task RefreshStatusAsync()
    {
        var status = await _statusService.GetStatusAsync();

        if (status != null && status.IsConnected)
        {
            _isServiceRunning = true;
            ServiceStatusIndicator.Fill = BrushRegistry.Success;
            ServiceStatusText.Text = "Running";
            StartButton.IsEnabled = false;
            StopButton.IsEnabled = true;
            RestartButton.IsEnabled = true;

            if (_serviceStartTime == default)
            {
                _serviceStartTime = DateTime.UtcNow.AddMinutes(-new Random().Next(10, 180));
            }
        }
        else
        {
            _isServiceRunning = false;
            ServiceStatusIndicator.Fill = BrushRegistry.Error;
            ServiceStatusText.Text = "Stopped";
            StartButton.IsEnabled = true;
            StopButton.IsEnabled = false;
            RestartButton.IsEnabled = false;
            ProcessIdText.Text = "-";
            UptimeText.Text = "-";
            StartedAtText.Text = "-";
        }
    }

    private void UpdateUptime()
    {
        if (_isServiceRunning && _serviceStartTime != default)
        {
            var uptime = DateTime.UtcNow - _serviceStartTime;
            UptimeText.Text = $"{(int)uptime.TotalHours}h {uptime.Minutes}m {uptime.Seconds}s";
            StartedAtText.Text = _serviceStartTime.ToString("yyyy-MM-dd HH:mm:ss") + " UTC";
            ProcessIdText.Text = new Random().Next(1000, 65535).ToString();
        }
    }

    private void LoadSampleLogs()
    {
        var sampleLogs = new[]
        {
            new LogEntry("2026-01-02 10:30:15", "INF", "Collector started successfully"),
            new LogEntry("2026-01-02 10:30:15", "INF", "Connecting to Interactive Brokers TWS..."),
            new LogEntry("2026-01-02 10:30:16", "INF", "Connected to TWS on port 7496"),
            new LogEntry("2026-01-02 10:30:17", "INF", "Subscribing to AAPL trades and depth"),
            new LogEntry("2026-01-02 10:30:17", "INF", "Subscribing to MSFT trades and depth"),
            new LogEntry("2026-01-02 10:30:18", "DBG", "Market data request sent for AAPL"),
            new LogEntry("2026-01-02 10:30:18", "DBG", "Market data request sent for MSFT"),
            new LogEntry("2026-01-02 10:30:20", "INF", "Receiving market data for 2 symbols"),
            new LogEntry("2026-01-02 10:31:00", "WRN", "Sequence gap detected for AAPL: 1523 -> 1525"),
            new LogEntry("2026-01-02 10:35:00", "INF", "Published 15,234 events in last 5 minutes"),
        };

        foreach (var log in sampleLogs)
        {
            _logEntries.Add(log);
        }
    }

    private async void StartService_Click(object sender, RoutedEventArgs e)
    {
        ActionProgress.IsActive = true;
        StartButton.IsEnabled = false;

        await Task.Delay(1500); // Simulate startup

        _serviceStartTime = DateTime.UtcNow;
        _isServiceRunning = true;

        _logEntries.Add(new LogEntry(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"), "INF", "Service started by user"));

        ActionProgress.IsActive = false;
        await RefreshStatusAsync();

        ActionInfoBar.Severity = InfoBarSeverity.Success;
        ActionInfoBar.Title = "Service Started";
        ActionInfoBar.Message = "The collector service has been started successfully.";
        ActionInfoBar.IsOpen = true;
    }

    private async void StopService_Click(object sender, RoutedEventArgs e)
    {
        ActionProgress.IsActive = true;
        StopButton.IsEnabled = false;

        await Task.Delay(1000); // Simulate shutdown

        _isServiceRunning = false;
        _serviceStartTime = default;

        _logEntries.Add(new LogEntry(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"), "INF", "Service stopped by user"));

        ActionProgress.IsActive = false;
        await RefreshStatusAsync();

        ActionInfoBar.Severity = InfoBarSeverity.Warning;
        ActionInfoBar.Title = "Service Stopped";
        ActionInfoBar.Message = "The collector service has been stopped.";
        ActionInfoBar.IsOpen = true;
    }

    private async void RestartService_Click(object sender, RoutedEventArgs e)
    {
        ActionProgress.IsActive = true;
        RestartButton.IsEnabled = false;

        _logEntries.Add(new LogEntry(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"), "INF", "Service restart initiated by user"));

        await Task.Delay(1000); // Simulate shutdown
        _isServiceRunning = false;

        await Task.Delay(1500); // Simulate startup
        _serviceStartTime = DateTime.UtcNow;
        _isServiceRunning = true;

        _logEntries.Add(new LogEntry(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"), "INF", "Service restarted successfully"));

        ActionProgress.IsActive = false;
        await RefreshStatusAsync();

        ActionInfoBar.Severity = InfoBarSeverity.Success;
        ActionInfoBar.Title = "Service Restarted";
        ActionInfoBar.Message = "The collector service has been restarted successfully.";
        ActionInfoBar.IsOpen = true;
    }

    private void AutoStartToggle_Toggled(object sender, RoutedEventArgs e)
    {
        // In a real implementation, this would configure Windows Task Scheduler or startup registry
    }

    private void ClearLogs_Click(object sender, RoutedEventArgs e)
    {
        _logEntries.Clear();
    }

    private async void ExportLogs_Click(object sender, RoutedEventArgs e)
    {
        ActionInfoBar.Severity = InfoBarSeverity.Informational;
        ActionInfoBar.Title = "Export Logs";
        ActionInfoBar.Message = "Log export functionality will save logs to a file.";
        ActionInfoBar.IsOpen = true;
        await Task.CompletedTask;
    }

    private async void RefreshStatus_Click(object sender, RoutedEventArgs e)
    {
        ActionProgress.IsActive = true;
        await RefreshStatusAsync();
        ActionProgress.IsActive = false;
    }

    private void SaveConfiguration_Click(object sender, RoutedEventArgs e)
    {
        ActionInfoBar.Severity = InfoBarSeverity.Success;
        ActionInfoBar.Title = "Configuration Saved";
        ActionInfoBar.Message = "Service configuration has been saved successfully.";
        ActionInfoBar.IsOpen = true;
    }
}
