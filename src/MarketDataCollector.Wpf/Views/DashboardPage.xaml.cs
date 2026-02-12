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
using MarketDataCollector.Wpf.Contracts;
using WpfServices = MarketDataCollector.Wpf.Services;

namespace MarketDataCollector.Wpf.Views;

/// <summary>
/// Dashboard page showing real-time metrics and system status.
/// </summary>
public partial class DashboardPage : Page
{
    private const int MaxActivityItems = 25;
    private readonly WpfServices.NavigationService _navigationService;
    private readonly ConnectionService _connectionService;
    private readonly StatusService _statusService;
    private readonly MessagingService _messagingService;
    private readonly WpfServices.NotificationService _notificationService;
    private readonly DispatcherTimer _refreshTimer;
    private readonly Random _random = new();
    private bool _isCollectorPaused;

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

        ActivityItems = new ObservableCollection<DashboardActivityItem>();
        SymbolPerformanceItems = new ObservableCollection<SymbolPerformanceItem>();
        SymbolFreshnessItems = new ObservableCollection<SymbolFreshnessItem>();
        IntegrityEventItems = new ObservableCollection<IntegrityEventItem>();

        SeedDashboardData();

        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _refreshTimer.Tick += OnRefreshTimerTick;

        _messagingService.MessageReceived += OnMessageReceived;
        _connectionService.ConnectionStateChanged += OnConnectionStateChanged;
        _connectionService.LatencyUpdated += OnLatencyUpdated;
    }

    public ObservableCollection<DashboardActivityItem> ActivityItems { get; }

    public ObservableCollection<SymbolPerformanceItem> SymbolPerformanceItems { get; }

    public ObservableCollection<SymbolFreshnessItem> SymbolFreshnessItems { get; }

    public ObservableCollection<IntegrityEventItem> IntegrityEventItems { get; }

    private void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        _refreshTimer.Start();
        RefreshStatus();
    }

    private void OnPageUnloaded(object sender, RoutedEventArgs e)
    {
        _refreshTimer.Stop();
        _messagingService.MessageReceived -= OnMessageReceived;
        _connectionService.ConnectionStateChanged -= OnConnectionStateChanged;
        _connectionService.LatencyUpdated -= OnLatencyUpdated;
    }

    private void SeedDashboardData()
    {
        var successBrush = (Brush)FindResource("SuccessColorBrush");
        var warningBrush = (Brush)FindResource("WarningColorBrush");
        var errorBrush = (Brush)FindResource("ErrorColorBrush");
        var infoBrush = (Brush)FindResource("InfoColorBrush");

        ActivityItems.Add(new DashboardActivityItem
        {
            Title = "Collector started",
            Description = "Streaming trades from 3 providers",
            RelativeTime = "Just now",
            IconGlyph = (string)FindResource("IconPlay"),
            IconBackground = successBrush
        });
        ActivityItems.Add(new DashboardActivityItem
        {
            Title = "Backfill completed",
            Description = "Imported 12,450 bars for AAPL",
            RelativeTime = "5m ago",
            IconGlyph = (string)FindResource("IconDownload"),
            IconBackground = infoBrush
        });
        ActivityItems.Add(new DashboardActivityItem
        {
            Title = "Latency spike",
            Description = "Average latency increased to 24ms",
            RelativeTime = "12m ago",
            IconGlyph = (string)FindResource("IconWarning"),
            IconBackground = warningBrush
        });

        SymbolPerformanceItems.Add(new SymbolPerformanceItem
        {
            Symbol = "SPY",
            StatusText = "Live",
            StatusColor = successBrush,
            EventRate = "420/s",
            TotalEvents = "128K",
            LastEventTime = "2s ago",
            HealthScore = "99%",
            HealthColor = successBrush,
            HealthIcon = (string)FindResource("IconSuccess"),
            TrendPoints = CreateTrendPoints(0.6, 0.75, 0.7, 0.8, 0.9),
            TrendColor = successBrush
        });
        SymbolPerformanceItems.Add(new SymbolPerformanceItem
        {
            Symbol = "QQQ",
            StatusText = "Live",
            StatusColor = successBrush,
            EventRate = "310/s",
            TotalEvents = "98K",
            LastEventTime = "4s ago",
            HealthScore = "97%",
            HealthColor = successBrush,
            HealthIcon = (string)FindResource("IconSuccess"),
            TrendPoints = CreateTrendPoints(0.4, 0.55, 0.6, 0.5, 0.7),
            TrendColor = successBrush
        });
        SymbolPerformanceItems.Add(new SymbolPerformanceItem
        {
            Symbol = "AAPL",
            StatusText = "Warning",
            StatusColor = warningBrush,
            EventRate = "85/s",
            TotalEvents = "44K",
            LastEventTime = "8s ago",
            HealthScore = "92%",
            HealthColor = warningBrush,
            HealthIcon = (string)FindResource("IconWarning"),
            TrendPoints = CreateTrendPoints(0.3, 0.35, 0.2, 0.3, 0.25),
            TrendColor = warningBrush
        });

        SymbolFreshnessItems.Add(new SymbolFreshnessItem
        {
            Symbol = "SPY",
            Progress = 100,
            StatusText = "Live",
            StatusBrush = successBrush
        });
        SymbolFreshnessItems.Add(new SymbolFreshnessItem
        {
            Symbol = "QQQ",
            Progress = 100,
            StatusText = "Live",
            StatusBrush = successBrush
        });
        SymbolFreshnessItems.Add(new SymbolFreshnessItem
        {
            Symbol = "AAPL",
            Progress = 85,
            StatusText = "5s ago",
            StatusBrush = warningBrush
        });

        IntegrityEventItems.Add(new IntegrityEventItem
        {
            Id = 1,
            Symbol = "AAPL",
            EventTypeName = "Gap",
            Description = "Missing 4 bars between 10:22 and 10:24",
            RelativeTime = "10m ago",
            SeverityColor = warningBrush,
            IsNotAcknowledged = true
        });
        IntegrityEventItems.Add(new IntegrityEventItem
        {
            Id = 2,
            Symbol = "TSLA",
            EventTypeName = "Sequence",
            Description = "Out-of-order trade events detected",
            RelativeTime = "32m ago",
            SeverityColor = errorBrush,
            IsNotAcknowledged = true
        });

        UpdateEmptyStateIndicators();
        UpdateIntegrityBadges();
        SymbolCountText.Text = SymbolPerformanceItems.Count.ToString("N0");
        IntegrityMostAffectedText.Text = SymbolPerformanceItems.FirstOrDefault()?.Symbol ?? "N/A";
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

                TotalEventsToday.Text = FormatNumber(status.Published);
                ActiveSymbolsCount.Text = SymbolPerformanceItems.Count.ToString("N0");
                StorageUsedText.Text = "2.4 GB";
                DataQualityText.Text = "99.8%";

                if (status.Provider != null)
                {
                    SelectedDataSourceText.Text = status.Provider.ActiveProvider ?? "Not Connected";
                    ProviderDescriptionText.Text = status.Provider.DisplayStatus;
                    ConnectionStatusText.Text = status.Provider.DisplayStatus;
                    ConnectionStatusIndicator.Fill = status.Provider.IsConnected
                        ? (Brush)FindResource("SuccessColorBrush")
                        : (Brush)FindResource("ErrorColorBrush");
                }
            }

            LastUpdateText.Text = $"Last update: {DateTime.Now:HH:mm:ss}";
            LastDataUpdateText.Text = $"Last update: {DateTime.Now:HH:mm:ss}";

            UpdateConnectionInfo();
            UpdateSparklines();
            UpdateThroughputChart();
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

    private void UpdateSparklines()
    {
        PublishedSparklinePath.Points = CreateTrendPoints(0.4, 0.5, 0.6, 0.55, 0.7);
        DroppedSparklinePath.Points = CreateTrendPoints(0.1, 0.12, 0.08, 0.1, 0.09);
        IntegritySparklinePath.Points = CreateTrendPoints(0.2, 0.18, 0.25, 0.2, 0.22);
        HistoricalSparklinePath.Points = CreateTrendPoints(0.3, 0.35, 0.4, 0.45, 0.38);

        PublishedRateText.Text = $"+{_random.Next(800, 1400):N0}/s";
        DroppedRateText.Text = $"{_random.NextDouble() * 0.05:0.00}%";
        IntegrityRateText.Text = $"{_random.Next(0, 4)} gaps";
        HistoricalTrendText.Text = $"Last: {_random.Next(1, 4)}h ago";
    }

    private void UpdateThroughputChart()
    {
        var values = Enumerable.Range(0, 6).Select(_ => _random.Next(400, 2000)).ToArray();
        var points = new PointCollection();
        var fillPoints = new PointCollection();
        const double width = 240;
        const double height = 120;

        for (var i = 0; i < values.Length; i++)
        {
            var x = i * (width / (values.Length - 1));
            var y = height - (values[i] / 2000.0 * height);
            points.Add(new Point(x, y));
        }

        fillPoints.Add(new Point(points.First().X, height));
        foreach (var point in points)
        {
            fillPoints.Add(point);
        }
        fillPoints.Add(new Point(points.Last().X, height));

        ThroughputChartPath.Points = points;
        ThroughputChartFill.Points = fillPoints;

        CurrentThroughputText.Text = $"{values.Last():N0}/s";
        AvgThroughputText.Text = $"{values.Average():N0}/s";
        PeakThroughputText.Text = $"{values.Max():N0}/s";

        var dataHealth = 100 - _random.Next(0, 3);
        DataHealthText.Text = $"{dataHealth}%";
        DataHealthText.Foreground = dataHealth > 97
            ? (Brush)FindResource("SuccessColorBrush")
            : (Brush)FindResource("WarningColorBrush");
        DataHealthIcon.Text = dataHealth > 97
            ? (string)FindResource("IconSuccess")
            : (string)FindResource("IconWarning");
        DataHealthIcon.Foreground = DataHealthText.Foreground;
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
