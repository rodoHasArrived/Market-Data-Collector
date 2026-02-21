using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using MarketDataCollector.Ui.Services;
using MarketDataCollector.Wpf.Contracts;
using MarketDataCollector.Wpf.Services;
using WpfServices = MarketDataCollector.Wpf.Services;

namespace MarketDataCollector.Wpf.Views;

/// <summary>
/// Dashboard page showing real-time metrics and system status.
/// Wired to live backend API data with stale data indicators.
/// </summary>
public partial class DashboardPage : Page
{
    private const int MaxActivityItems = 25;
    private readonly WpfServices.NavigationService _navigationService;
    private readonly ConnectionService _connectionService;
    private readonly StatusService _statusService;
    private readonly MessagingService _messagingService;
    private readonly WpfServices.NotificationService _notificationService;
    private readonly AlertService _alertService;
    private readonly DispatcherTimer _refreshTimer;
    private readonly DispatcherTimer _staleCheckTimer;
    private bool _isCollectorPaused;
    private long _previousPublished;
    private DateTime _lastRateCalcTime = DateTime.UtcNow;

    public DashboardPage(
        WpfServices.NavigationService navigationService,
        ConnectionService connectionService,
        StatusService statusService,
        MessagingService messagingService,
        WpfServices.NotificationService notificationService)
    {
        InitializeComponent();
        DataContext = this;

        _navigationService = navigationService;
        _connectionService = connectionService;
        _statusService = statusService;
        _messagingService = messagingService;
        _notificationService = notificationService;
        _alertService = AlertService.Instance;

        ActivityItems = new ObservableCollection<DashboardActivityItem>();
        SymbolPerformanceItems = new ObservableCollection<SymbolPerformanceItem>();
        SymbolFreshnessItems = new ObservableCollection<SymbolFreshnessItem>();
        IntegrityEventItems = new ObservableCollection<IntegrityEventItem>();

        InitializeEmptyState();

        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _refreshTimer.Tick += OnRefreshTimerTick;

        _staleCheckTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _staleCheckTimer.Tick += OnStaleCheckTimerTick;

        _messagingService.MessageReceived += OnMessageReceived;
        _connectionService.ConnectionStateChanged += OnConnectionStateChanged;
        _connectionService.LatencyUpdated += OnLatencyUpdated;
        _statusService.LiveStatusReceived += OnLiveStatusReceived;
        _statusService.BackendReachabilityChanged += OnBackendReachabilityChanged;
        _alertService.AlertRaised += OnAlertCountChanged;
    }

    public ObservableCollection<DashboardActivityItem> ActivityItems { get; }

    public ObservableCollection<SymbolPerformanceItem> SymbolPerformanceItems { get; }

    public ObservableCollection<SymbolFreshnessItem> SymbolFreshnessItems { get; }

    public ObservableCollection<IntegrityEventItem> IntegrityEventItems { get; }

    private void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        _statusService.StartLiveMonitoring(intervalSeconds: 2);
        _refreshTimer.Start();
        _staleCheckTimer.Start();
        RefreshStatus();
        UpdateAlertSummary();
    }

    private void OnPageUnloaded(object sender, RoutedEventArgs e)
    {
        _refreshTimer.Stop();
        _staleCheckTimer.Stop();
        _statusService.StopLiveMonitoring();
        _messagingService.MessageReceived -= OnMessageReceived;
        _connectionService.ConnectionStateChanged -= OnConnectionStateChanged;
        _connectionService.LatencyUpdated -= OnLatencyUpdated;
        _statusService.LiveStatusReceived -= OnLiveStatusReceived;
        _statusService.BackendReachabilityChanged -= OnBackendReachabilityChanged;
        _alertService.AlertRaised -= OnAlertCountChanged;
    }

    private void InitializeEmptyState()
    {
        UpdateEmptyStateIndicators();
        UpdateIntegrityBadges();
        SymbolCountText.Text = "0";
        IntegrityMostAffectedText.Text = "N/A";
    }

    private void OnLiveStatusReceived(object? sender, LiveStatusEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            if (e.Status != null)
            {
                ApplyLiveStatus(e.Status);
            }

            UpdateStaleIndicator(e.IsStale);
        });
    }

    private void OnBackendReachabilityChanged(object? sender, bool isReachable)
    {
        Dispatcher.Invoke(() =>
        {
            if (!isReachable)
            {
                AddActivityItem("Backend unreachable", "Cannot connect to the Market Data Collector service");
            }
            else
            {
                AddActivityItem("Backend connected", "Successfully connected to the Market Data Collector service");
            }
        });
    }

    private void ApplyLiveStatus(SimpleStatus status)
    {
        // Calculate event rate from delta
        var now = DateTime.UtcNow;
        var elapsed = (now - _lastRateCalcTime).TotalSeconds;
        if (elapsed > 0 && _previousPublished > 0)
        {
            var rate = (status.Published - _previousPublished) / elapsed;
            PublishedRateText.Text = rate >= 0 ? $"+{rate:N0}/s" : "0/s";
        }
        _previousPublished = status.Published;
        _lastRateCalcTime = now;

        PublishedCount.Text = FormatNumber(status.Published);
        DroppedCount.Text = FormatNumber(status.Dropped);
        IntegrityCount.Text = FormatNumber(status.Integrity);
        HistoricalCount.Text = FormatNumber(status.Historical);

        TotalEventsToday.Text = FormatNumber(status.Published);
        ActiveSymbolsCount.Text = SymbolPerformanceItems.Count.ToString("N0");

        if (status.Dropped > 0 && status.Published > 0)
        {
            var dropRate = (double)status.Dropped / status.Published * 100;
            DroppedRateText.Text = $"{dropRate:0.00}%";
        }
        else
        {
            DroppedRateText.Text = "0.00%";
        }

        IntegrityRateText.Text = $"{status.Integrity} gaps";
        HistoricalTrendText.Text = status.Historical > 0 ? $"{FormatNumber(status.Historical)} bars" : "No data";

        if (status.Provider != null)
        {
            SelectedDataSourceText.Text = status.Provider.ActiveProvider ?? "Not Connected";
            ProviderDescriptionText.Text = status.Provider.DisplayStatus;
            ConnectionStatusText.Text = status.Provider.DisplayStatus;
            ConnectionStatusIndicator.Fill = status.Provider.IsConnected
                ? (Brush)FindResource("SuccessColorBrush")
                : (Brush)FindResource("ErrorColorBrush");
        }

        LastUpdateText.Text = $"Last update: {DateTime.Now:HH:mm:ss}";
        LastDataUpdateText.Text = $"Last update: {DateTime.Now:HH:mm:ss}";
    }

    private void UpdateStaleIndicator(bool isStale)
    {
        var staleBrush = (Brush)FindResource("WarningColorBrush");
        var normalBrush = (Brush)FindResource("ConsoleTextSecondaryBrush");

        if (_statusService.SecondsSinceLastUpdate is { } seconds)
        {
            var staleText = seconds < 5 ? "Just now" : $"{seconds:F0}s ago";
            LastUpdateText.Text = $"Last update: {staleText}";
        }

        if (isStale)
        {
            LastUpdateText.Foreground = staleBrush;
        }
        else
        {
            LastUpdateText.Foreground = normalBrush;
        }
    }

    private void OnStaleCheckTimerTick(object? sender, EventArgs e)
    {
        UpdateStaleIndicator(_statusService.IsDataStale);
    }

    private async void RefreshStatus()
    {
        try
        {
            var status = await _statusService.GetStatusAsync();

            if (status != null)
            {
                ApplyLiveStatus(status);
            }

            UpdateConnectionInfo();
            UpdateCollectorBadge();
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
        AvgLatencyText.Text = latency > 0 ? $"{latency:F0} ms" : "-- ms";

        var uptime = _connectionService.Uptime;
        if (uptime.HasValue)
        {
            UptimeText.Text = $"{uptime.Value.Hours:D2}:{uptime.Value.Minutes:D2}:{uptime.Value.Seconds:D2}";
            CollectorUptimeText.Text = $"{uptime.Value.Hours}h {uptime.Value.Minutes}m {uptime.Value.Seconds}s";
        }
        else
        {
            UptimeText.Text = "--:--:--";
            CollectorUptimeText.Text = "0h 0m 0s";
        }
    }

    private void UpdateThroughputFromMetrics(SimpleStatus status)
    {
        // Derive throughput from published event count rate
        var now = DateTime.UtcNow;
        var elapsed = (now - _lastRateCalcTime).TotalSeconds;
        if (elapsed > 0 && _previousPublished > 0)
        {
            var currentRate = (int)((status.Published - _previousPublished) / elapsed);
            CurrentThroughputText.Text = $"{currentRate:N0}/s";
        }

        // Calculate data health from drop rate
        if (status.Published > 0)
        {
            var dropPercent = (double)status.Dropped / status.Published * 100;
            var dataHealth = Math.Max(0, 100 - dropPercent);
            DataHealthText.Text = $"{dataHealth:F1}%";
            DataHealthText.Foreground = dataHealth > 97
                ? (Brush)FindResource("SuccessColorBrush")
                : (Brush)FindResource("WarningColorBrush");
            DataHealthIcon.Text = dataHealth > 97
                ? (string)FindResource("IconSuccess")
                : (string)FindResource("IconWarning");
            DataHealthIcon.Foreground = DataHealthText.Foreground;

            DataQualityText.Text = $"{dataHealth:F1}%";
        }
    }

    private void UpdateCollectorBadge()
    {
        var isConnected = _connectionService.State == ConnectionState.Connected;
        CollectorStatusText.Text = isConnected ? "Running" : "Stopped";
        CollectorStatusBadge.Background = isConnected
            ? (Brush)FindResource("SuccessColorBrush")
            : (Brush)FindResource("ErrorColorBrush");
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
            ConnectionStatusText.Text = e.Provider ?? state;
            ConnectionStatusIndicator.Fill = e.State == ConnectionState.Connected
                ? (Brush)FindResource("SuccessColorBrush")
                : (Brush)FindResource("ErrorColorBrush");
            AddActivityItem($"Connection {state.ToLowerInvariant()}", $"Provider: {e.Provider ?? "Unknown"}");
            UpdateCollectorBadge();
        });
    }

    private void OnLatencyUpdated(object? sender, int latencyMs)
    {
        Dispatcher.Invoke(() =>
        {
            LatencyText.Text = $"{latencyMs} ms";
            AvgLatencyText.Text = $"{latencyMs} ms";
        });
    }

    private void AddActivityItem(string title, string description)
    {
        ActivityItems.Insert(0, new DashboardActivityItem
        {
            Title = title,
            Description = description,
            RelativeTime = "Just now",
            IconGlyph = (string)FindResource("IconInfo"),
            IconBackground = (Brush)FindResource("InfoColorBrush")
        });

        while (ActivityItems.Count > MaxActivityItems)
        {
            ActivityItems.RemoveAt(ActivityItems.Count - 1);
        }

        UpdateEmptyStateIndicators();
    }

    private void UpdateEmptyStateIndicators()
    {
        NoActivityText.Visibility = ActivityItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        NoIntegrityEventsText.Visibility = IntegrityEventItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateIntegrityBadges()
    {
        var criticalCount = IntegrityEventItems.Count(item => item.SeverityColor == (Brush)FindResource("ErrorColorBrush"));
        var warningCount = IntegrityEventItems.Count(item => item.SeverityColor == (Brush)FindResource("WarningColorBrush"));

        CriticalAlertsCount.Text = criticalCount.ToString("N0");
        WarningAlertsCount.Text = warningCount.ToString("N0");

        CriticalAlertsBadge.Visibility = criticalCount > 0 ? Visibility.Visible : Visibility.Collapsed;
        WarningAlertsBadge.Visibility = warningCount > 0 ? Visibility.Visible : Visibility.Collapsed;

        IntegrityTotalEventsText.Text = IntegrityEventItems.Count.ToString("N0");
        IntegrityLast24hText.Text = warningCount.ToString("N0");
        IntegrityUnacknowledgedText.Text = IntegrityEventItems.Count(item => item.IsNotAcknowledged).ToString("N0");
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

    private static PointCollection CreateTrendPoints(params double[] values)
    {
        if (values.Length == 0)
        {
            return new PointCollection();
        }

        var points = new PointCollection();
        var width = 60.0;
        var height = 20.0;

        var step = values.Length > 1 ? width / (values.Length - 1) : 0;

        for (var i = 0; i < values.Length; i++)
        {
            var x = i * step;
            var y = height - (values[i] * height);
            points.Add(new Point(x, y));
        }

        return points;
    }

    private async Task StartCollectorAsync()
    {
        try
        {
            var provider = _connectionService.CurrentProvider ?? "default";
            var success = await _connectionService.ConnectAsync(provider);

            if (success)
            {
                AddActivityItem("Collector started", $"Provider: {provider}");
                _notificationService.NotifySuccess("Collector Started", "Data collection has started.");
            }
            else
            {
                AddActivityItem("Collector start failed", "Unable to connect to provider");
                _notificationService.ShowNotification(
                    "Start Failed",
                    "Failed to start the data collector.",
                    NotificationType.Error);
            }
        }
        catch (Exception ex)
        {
            AddActivityItem("Collector error", ex.Message);
            _notificationService.ShowNotification(
                "Error",
                ex.Message,
                NotificationType.Error);
        }
    }

    private async Task StopCollectorAsync()
    {
        try
        {
            await _connectionService.DisconnectAsync();
            AddActivityItem("Collector stopped", "Streaming paused");
            _notificationService.NotifyInfo("Collector Stopped", "Data collection has stopped.");
        }
        catch (Exception ex)
        {
            AddActivityItem("Stop failed", ex.Message);
            _notificationService.ShowNotification(
                "Error",
                ex.Message,
                NotificationType.Error);
        }
    }

    private async void StartCollector_Click(object sender, RoutedEventArgs e)
    {
        await StartCollectorAsync();
    }

    private async void StopCollector_Click(object sender, RoutedEventArgs e)
    {
        await StopCollectorAsync();
    }

    private async void QuickStartCollector_Click(object sender, RoutedEventArgs e)
    {
        await StartCollectorAsync();
    }

    private async void QuickStopCollector_Click(object sender, RoutedEventArgs e)
    {
        await StopCollectorAsync();
    }

    private void QuickPauseCollector_Click(object sender, RoutedEventArgs e)
    {
        if (_isCollectorPaused)
        {
            _connectionService.ResumeAutoReconnect();
            _isCollectorPaused = false;
            PauseButtonText.Text = "Pause Collection";
            PauseButtonIcon.Text = (string)FindResource("IconPause");
            AddActivityItem("Collector resumed", "Auto-reconnect enabled");
        }
        else
        {
            _connectionService.PauseAutoReconnect();
            _isCollectorPaused = true;
            PauseButtonText.Text = "Resume Collection";
            PauseButtonIcon.Text = (string)FindResource("IconPlay");
            AddActivityItem("Collector paused", "Auto-reconnect disabled");
        }
    }

    private void ViewLogs_Click(object sender, RoutedEventArgs e)
    {
        _navigationService.NavigateTo("ActivityLog");
    }

    private void RunBackfill_Click(object sender, RoutedEventArgs e)
    {
        _navigationService.NavigateTo("Backfill");
    }

    private void RefreshStatus_Click(object sender, RoutedEventArgs e)
    {
        RefreshStatus();
    }

    private void ViewAllActivity_Click(object sender, RoutedEventArgs e)
    {
        _navigationService.NavigateTo("ActivityLog");
    }

    private void ClearIntegrityAlerts_Click(object sender, RoutedEventArgs e)
    {
        IntegrityEventItems.Clear();
        UpdateIntegrityBadges();
        UpdateEmptyStateIndicators();
    }

    private void ExpandIntegrityPanel_Click(object sender, RoutedEventArgs e)
    {
        var isCollapsed = IntegrityDetailsPanel.Visibility != Visibility.Visible;
        IntegrityDetailsPanel.Visibility = isCollapsed ? Visibility.Visible : Visibility.Collapsed;
        ExpandIntegrityText.Text = isCollapsed ? "Hide Details" : "Show Details";
        ExpandIntegrityIcon.Text = isCollapsed
            ? (string)FindResource("IconChevronUp")
            : (string)FindResource("IconChevronDown");
    }

    private void AcknowledgeIntegrityEvent_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is int id)
        {
            var item = IntegrityEventItems.FirstOrDefault(current => current.Id == id);
            if (item != null)
            {
                item.IsNotAcknowledged = false;
                UpdateIntegrityBadges();
            }
        }
    }

    private void ViewAllIntegrityEvents_Click(object sender, RoutedEventArgs e)
    {
        _navigationService.NavigateTo("DataQuality");
    }

    private void ExportIntegrityReport_Click(object sender, RoutedEventArgs e)
    {
        _notificationService.NotifyInfo("Report queued", "Integrity report export started.");
    }

    private void QuickAddSymbol_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(QuickAddSymbolBox.Text))
        {
            return;
        }

        AddActivityItem("Symbol added", $"Added {QuickAddSymbolBox.Text.Trim().ToUpperInvariant()} to watchlist");
        QuickAddSymbolBox.Text = string.Empty;
    }

    #region Alert Summary

    private void OnAlertCountChanged(object? sender, AlertEventArgs e)
    {
        Dispatcher.Invoke(UpdateAlertSummary);
    }

    private void UpdateAlertSummary()
    {
        var alerts = _alertService.GetActiveAlerts();
        var criticalCount = alerts.Count(a => a.Severity >= AlertSeverity.Critical && !a.IsSuppressed && !a.IsSnoozed);
        var warningCount = alerts.Count(a => a.Severity == AlertSeverity.Warning && !a.IsSuppressed && !a.IsSnoozed);
        var infoCount = alerts.Count(a => a.Severity == AlertSeverity.Info && !a.IsSuppressed && !a.IsSnoozed);

        var hasAlerts = criticalCount > 0 || warningCount > 0 || infoCount > 0;

        AlertSummaryBanner.Visibility = hasAlerts ? Visibility.Visible : Visibility.Collapsed;

        if (criticalCount > 0)
        {
            AlertCriticalBadge.Visibility = Visibility.Visible;
            AlertCriticalCount.Text = criticalCount == 1 ? "1 Critical" : $"{criticalCount} Critical";
        }
        else
        {
            AlertCriticalBadge.Visibility = Visibility.Collapsed;
        }

        if (warningCount > 0)
        {
            AlertWarningBadge.Visibility = Visibility.Visible;
            AlertWarningCount.Text = warningCount == 1 ? "1 Warning" : $"{warningCount} Warnings";
        }
        else
        {
            AlertWarningBadge.Visibility = Visibility.Collapsed;
        }

        if (infoCount > 0)
        {
            AlertInfoBadge.Visibility = Visibility.Visible;
            AlertInfoCount.Text = infoCount == 1 ? "1 Info" : $"{infoCount} Info";
        }
        else
        {
            AlertInfoBadge.Visibility = Visibility.Collapsed;
        }
    }

    private void ViewAlerts_Click(object sender, RoutedEventArgs e)
    {
        _navigationService.NavigateTo("NotificationCenter");
    }

    #endregion

    public sealed class DashboardActivityItem
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string RelativeTime { get; set; } = string.Empty;
        public string IconGlyph { get; set; } = string.Empty;
        public Brush IconBackground { get; set; } = Brushes.Transparent;
    }

    public sealed class SymbolPerformanceItem
    {
        public string Symbol { get; set; } = string.Empty;
        public string StatusText { get; set; } = string.Empty;
        public Brush StatusColor { get; set; } = Brushes.Transparent;
        public string EventRate { get; set; } = string.Empty;
        public string TotalEvents { get; set; } = string.Empty;
        public string LastEventTime { get; set; } = string.Empty;
        public string HealthScore { get; set; } = string.Empty;
        public Brush HealthColor { get; set; } = Brushes.Transparent;
        public string HealthIcon { get; set; } = string.Empty;
        public PointCollection TrendPoints { get; set; } = new();
        public Brush TrendColor { get; set; } = Brushes.Transparent;
    }

    public sealed class SymbolFreshnessItem
    {
        public string Symbol { get; set; } = string.Empty;
        public double Progress { get; set; }
        public string StatusText { get; set; } = string.Empty;
        public Brush StatusBrush { get; set; } = Brushes.Transparent;
    }

    public sealed class IntegrityEventItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public int Id { get; set; }
        public string Symbol { get; set; } = string.Empty;
        public string EventTypeName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string RelativeTime { get; set; } = string.Empty;
        public Brush SeverityColor { get; set; } = Brushes.Transparent;
        private bool _isNotAcknowledged;

        public bool IsNotAcknowledged
        {
            get => _isNotAcknowledged;
            set
            {
                if (_isNotAcknowledged != value)
                {
                    _isNotAcknowledged = value;
                    OnPropertyChanged();
                }
            }
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
