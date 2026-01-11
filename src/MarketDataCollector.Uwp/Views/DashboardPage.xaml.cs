using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using MarketDataCollector.Uwp.ViewModels;
using MarketDataCollector.Uwp.Services;
using Windows.UI;

namespace MarketDataCollector.Uwp.Views;

/// <summary>
/// Enhanced dashboard page with sparklines, throughput graphs, and quick actions.
/// </summary>
public sealed partial class DashboardPage : Page
{
    public MainViewModel ViewModel { get; }

    private readonly DispatcherTimer _refreshTimer;
    private readonly DispatcherTimer _sparklineTimer;
    private readonly List<double> _publishedHistory = new();
    private readonly List<double> _droppedHistory = new();
    private readonly List<double> _integrityHistory = new();
    private readonly List<double> _throughputHistory = new();
    private readonly Random _random = new();
    private readonly DispatcherTimer _uptimeTimer;
    private readonly ActivityFeedService _activityFeedService;
    private readonly IntegrityEventsService _integrityEventsService;
    private readonly ObservableCollection<ActivityDisplayItem> _activityItems;
    private readonly ObservableCollection<IntegrityEventDisplayItem> _integrityItems;
    private bool _isCollectorRunning = true;
    private bool _isCollectorPaused = false;
    private bool _isIntegrityPanelExpanded = false;
    private DateTime _startTime;
    private DateTime _collectorStartTime;

    // Stream status tracking
    private int _tradesStreamCount = 5;
    private int _depthStreamCount = 3;
    private int _quotesStreamCount = 0;
    private bool _tradesStreamActive = true;
    private bool _depthStreamActive = true;
    private bool _quotesStreamActive = false;

    public DashboardPage()
    {
        this.InitializeComponent();
        ViewModel = new MainViewModel();
        DataContext = ViewModel;
        _startTime = DateTime.UtcNow;
        _collectorStartTime = DateTime.UtcNow;

        _activityFeedService = ActivityFeedService.Instance;
        _integrityEventsService = IntegrityEventsService.Instance;
        _activityItems = new ObservableCollection<ActivityDisplayItem>();
        _integrityItems = new ObservableCollection<IntegrityEventDisplayItem>();
        ActivityFeedList.ItemsSource = _activityItems;
        IntegrityEventsList.ItemsSource = _integrityItems;

        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _refreshTimer.Tick += RefreshTimer_Tick;

        _sparklineTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _sparklineTimer.Tick += SparklineTimer_Tick;

        _uptimeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _uptimeTimer.Tick += UptimeTimer_Tick;

        _activityFeedService.ActivityAdded += ActivityFeedService_ActivityAdded;
        _integrityEventsService.EventRecorded += IntegrityEventsService_EventRecorded;
        _integrityEventsService.EventsCleared += IntegrityEventsService_EventsCleared;

        Loaded += DashboardPage_Loaded;
        Unloaded += DashboardPage_Unloaded;
    }

    private async void DashboardPage_Loaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.LoadAsync();
        InitializeSparklineData();
        LoadActivityFeed();
        LoadIntegrityEvents();
        _refreshTimer.Start();
        _sparklineTimer.Start();
        _uptimeTimer.Start();
        UpdateCollectorStatus();
        UpdateQuickActionsCollectorStatus();
        UpdateStreamStatusBadges();
        UpdateCollectorUptime();
        UpdateSparklines();
        UpdateThroughputChart();
        UpdateQuickStats();
        UpdateIntegritySummary();
    }

    private void DashboardPage_Unloaded(object sender, RoutedEventArgs e)
    {
        _refreshTimer.Stop();
        _sparklineTimer.Stop();
        _uptimeTimer.Stop();
    }

    private void RefreshTimer_Tick(object? sender, object e)
    {
        UpdateUptime();
        UpdateLatency();
        UpdateThroughputStats();
    }

    private void SparklineTimer_Tick(object? sender, object e)
    {
        AddSparklineData();
        UpdateSparklines();
    }

    private void UptimeTimer_Tick(object? sender, object e)
    {
        UpdateCollectorUptime();
    }

    private void InitializeSparklineData()
    {
        // Initialize with sample data
        for (int i = 0; i < 20; i++)
        {
            _publishedHistory.Add(800 + _random.Next(0, 400));
            _droppedHistory.Add(_random.Next(0, 5));
            _integrityHistory.Add(_random.Next(0, 3));
            _throughputHistory.Add(1000 + _random.Next(-200, 400));
        }
    }

    private void AddSparklineData()
    {
        // Add new data points
        _publishedHistory.Add(800 + _random.Next(0, 400));
        _droppedHistory.Add(_random.Next(0, 5));
        _integrityHistory.Add(_random.Next(0, 3));
        _throughputHistory.Add(1000 + _random.Next(-200, 400));

        // Keep last 20 points
        if (_publishedHistory.Count > 20) _publishedHistory.RemoveAt(0);
        if (_droppedHistory.Count > 20) _droppedHistory.RemoveAt(0);
        if (_integrityHistory.Count > 20) _integrityHistory.RemoveAt(0);
        if (_throughputHistory.Count > 30) _throughputHistory.RemoveAt(0);
    }

    private void UpdateSparklines()
    {
        UpdateSparkline(PublishedSparklinePath, _publishedHistory, PublishedSparkline.ActualWidth, 30);
        UpdateSparkline(DroppedSparklinePath, _droppedHistory, DroppedSparkline.ActualWidth, 30);
        UpdateSparkline(IntegritySparklinePath, _integrityHistory, IntegritySparkline.ActualWidth, 30);

        // Update rate text
        if (_publishedHistory.Count > 0)
        {
            var rate = (int)_publishedHistory[^1];
            PublishedRateText.Text = $"+{rate:N0}/s";
        }
    }

    private void UpdateSparkline(Microsoft.UI.Xaml.Shapes.Polyline polyline, List<double> data, double width, double height)
    {
        if (data.Count < 2 || width <= 0) return;

        var points = new PointCollection();
        var max = 1.0;
        var min = 0.0;

        foreach (var val in data)
        {
            if (val > max) max = val;
        }

        var step = width / (data.Count - 1);

        for (int i = 0; i < data.Count; i++)
        {
            var x = i * step;
            var y = height - ((data[i] - min) / (max - min) * height);
            points.Add(new Windows.Foundation.Point(x, Math.Max(2, Math.Min(height - 2, y))));
        }

        polyline.Points = points;
    }

    private void UpdateThroughputChart()
    {
        // In a real implementation, this would update the throughput chart
        // For now, we'll update the stats
        UpdateThroughputStats();
    }

    private void UpdateThroughputStats()
    {
        if (_throughputHistory.Count > 0)
        {
            var current = (int)_throughputHistory[^1];
            var avg = 0.0;
            var peak = 0.0;

            foreach (var val in _throughputHistory)
            {
                avg += val;
                if (val > peak) peak = val;
            }
            avg /= _throughputHistory.Count;

            CurrentThroughputText.Text = $"{current:N0}/s";
            AvgThroughputText.Text = $"{(int)avg:N0}/s";
            PeakThroughputText.Text = $"{(int)peak:N0}/s";
        }
    }

    private void UpdateUptime()
    {
        var uptime = DateTime.UtcNow - _startTime;
        UptimeText.Text = uptime.TotalHours >= 1
            ? $"{(int)uptime.TotalHours}h {uptime.Minutes}m"
            : $"{uptime.Minutes}m {uptime.Seconds}s";
    }

    private void UpdateLatency()
    {
        var latency = 8 + _random.Next(0, 10);
        LatencyText.Text = $"{latency}ms";
        LatencyText.Foreground = latency < 20
            ? new SolidColorBrush(Color.FromArgb(255, 72, 187, 120))
            : latency < 50
                ? new SolidColorBrush(Color.FromArgb(255, 237, 137, 54))
                : new SolidColorBrush(Color.FromArgb(255, 245, 101, 101));
    }

    private void UpdateCollectorStatus()
    {
        if (_isCollectorRunning)
        {
            if (_isCollectorPaused)
            {
                CollectorStatusBadge.Background = new SolidColorBrush(Color.FromArgb(255, 237, 137, 54));
                CollectorStatusText.Text = "Paused";
            }
            else
            {
                CollectorStatusBadge.Background = new SolidColorBrush(Color.FromArgb(255, 72, 187, 120));
                CollectorStatusText.Text = "Running";
            }
            StartCollectorButton.IsEnabled = false;
            StopCollectorButton.IsEnabled = true;
        }
        else
        {
            CollectorStatusBadge.Background = new SolidColorBrush(Color.FromArgb(255, 245, 101, 101));
            CollectorStatusText.Text = "Stopped";
            StartCollectorButton.IsEnabled = true;
            StopCollectorButton.IsEnabled = false;
        }
    }

    private void UpdateQuickActionsCollectorStatus()
    {
        if (_isCollectorRunning)
        {
            QuickStartCollectorButton.IsEnabled = false;
            QuickStopCollectorButton.IsEnabled = true;
            QuickPauseCollectorButton.IsEnabled = true;

            if (_isCollectorPaused)
            {
                PauseButtonIcon.Glyph = "\uE768"; // Play icon
                PauseButtonText.Text = "Resume Collection";
            }
            else
            {
                PauseButtonIcon.Glyph = "\uE769"; // Pause icon
                PauseButtonText.Text = "Pause Collection";
            }
        }
        else
        {
            QuickStartCollectorButton.IsEnabled = true;
            QuickStopCollectorButton.IsEnabled = false;
            QuickPauseCollectorButton.IsEnabled = false;
            PauseButtonIcon.Glyph = "\uE769"; // Pause icon
            PauseButtonText.Text = "Pause Collection";
        }
    }

    private void UpdateStreamStatusBadges()
    {
        // Update Trades stream badge
        TradesStreamBadge.Background = new SolidColorBrush(
            _tradesStreamActive && _isCollectorRunning && !_isCollectorPaused
                ? Color.FromArgb(255, 72, 187, 120)  // Green - active
                : _tradesStreamActive && _isCollectorPaused
                    ? Color.FromArgb(255, 237, 137, 54)  // Orange - paused
                    : Color.FromArgb(255, 160, 174, 192)); // Gray - inactive
        TradesStreamCount.Text = $"({_tradesStreamCount})";

        // Update Depth stream badge
        DepthStreamBadge.Background = new SolidColorBrush(
            _depthStreamActive && _isCollectorRunning && !_isCollectorPaused
                ? Color.FromArgb(255, 72, 187, 120)
                : _depthStreamActive && _isCollectorPaused
                    ? Color.FromArgb(255, 237, 137, 54)
                    : Color.FromArgb(255, 160, 174, 192));
        DepthStreamCount.Text = $"({_depthStreamCount})";

        // Update Quotes stream badge
        QuotesStreamBadge.Background = new SolidColorBrush(
            _quotesStreamActive && _isCollectorRunning && !_isCollectorPaused
                ? Color.FromArgb(255, 72, 187, 120)
                : _quotesStreamActive && _isCollectorPaused
                    ? Color.FromArgb(255, 237, 137, 54)
                    : Color.FromArgb(255, 160, 174, 192));
        QuotesStreamCount.Text = $"({_quotesStreamCount})";
    }

    private void UpdateCollectorUptime()
    {
        if (_isCollectorRunning)
        {
            var uptime = DateTime.UtcNow - _collectorStartTime;
            if (uptime.TotalHours >= 1)
            {
                CollectorUptimeText.Text = $"{(int)uptime.TotalHours}h {uptime.Minutes}m {uptime.Seconds}s";
            }
            else if (uptime.TotalMinutes >= 1)
            {
                CollectorUptimeText.Text = $"{uptime.Minutes}m {uptime.Seconds}s";
            }
            else
            {
                CollectorUptimeText.Text = $"{uptime.Seconds}s";
            }
        }
        else
        {
            CollectorUptimeText.Text = "Stopped";
        }
    }

    private async void QuickStartCollector_Click(object sender, RoutedEventArgs e)
    {
        _isCollectorRunning = true;
        _isCollectorPaused = false;
        _collectorStartTime = DateTime.UtcNow;
        _startTime = DateTime.UtcNow;
        UpdateCollectorStatus();
        UpdateQuickActionsCollectorStatus();
        UpdateStreamStatusBadges();

        DashboardInfoBar.Severity = InfoBarSeverity.Success;
        DashboardInfoBar.Title = "Collector Started";
        DashboardInfoBar.Message = "Market data collection has been started.";
        DashboardInfoBar.IsOpen = true;

        await Task.Delay(3000);
        DashboardInfoBar.IsOpen = false;
    }

    private async void QuickStopCollector_Click(object sender, RoutedEventArgs e)
    {
        _isCollectorRunning = false;
        _isCollectorPaused = false;
        UpdateCollectorStatus();
        UpdateQuickActionsCollectorStatus();
        UpdateStreamStatusBadges();

        DashboardInfoBar.Severity = InfoBarSeverity.Warning;
        DashboardInfoBar.Title = "Collector Stopped";
        DashboardInfoBar.Message = "Market data collection has been stopped.";
        DashboardInfoBar.IsOpen = true;

        await Task.Delay(3000);
        DashboardInfoBar.IsOpen = false;
    }

    private async void QuickPauseCollector_Click(object sender, RoutedEventArgs e)
    {
        if (!_isCollectorRunning) return;

        _isCollectorPaused = !_isCollectorPaused;
        UpdateCollectorStatus();
        UpdateQuickActionsCollectorStatus();
        UpdateStreamStatusBadges();

        if (_isCollectorPaused)
        {
            DashboardInfoBar.Severity = InfoBarSeverity.Informational;
            DashboardInfoBar.Title = "Collection Paused";
            DashboardInfoBar.Message = "Market data collection has been paused. Click Resume to continue.";
        }
        else
        {
            DashboardInfoBar.Severity = InfoBarSeverity.Success;
            DashboardInfoBar.Title = "Collection Resumed";
            DashboardInfoBar.Message = "Market data collection has been resumed.";
        }
        DashboardInfoBar.IsOpen = true;

        await Task.Delay(3000);
        DashboardInfoBar.IsOpen = false;
    }

    private async void StartCollector_Click(object sender, RoutedEventArgs e)
    {
        _isCollectorRunning = true;
        _isCollectorPaused = false;
        _collectorStartTime = DateTime.UtcNow;
        _startTime = DateTime.UtcNow;
        UpdateCollectorStatus();
        UpdateQuickActionsCollectorStatus();
        UpdateStreamStatusBadges();

        DashboardInfoBar.Severity = InfoBarSeverity.Success;
        DashboardInfoBar.Title = "Collector Started";
        DashboardInfoBar.Message = "Market data collection has been started.";
        DashboardInfoBar.IsOpen = true;

        await Task.Delay(3000);
        DashboardInfoBar.IsOpen = false;
    }

    private async void StopCollector_Click(object sender, RoutedEventArgs e)
    {
        _isCollectorRunning = false;
        _isCollectorPaused = false;
        UpdateCollectorStatus();
        UpdateQuickActionsCollectorStatus();
        UpdateStreamStatusBadges();

        DashboardInfoBar.Severity = InfoBarSeverity.Warning;
        DashboardInfoBar.Title = "Collector Stopped";
        DashboardInfoBar.Message = "Market data collection has been stopped.";
        DashboardInfoBar.IsOpen = true;

        await Task.Delay(3000);
        DashboardInfoBar.IsOpen = false;
    }

    private void QuickAddSymbol_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
        {
            var suggestions = new List<string>();
            var text = sender.Text.ToUpper();

            if (!string.IsNullOrEmpty(text))
            {
                // Sample suggestions
                var allSymbols = new[] { "AAPL", "MSFT", "GOOGL", "AMZN", "TSLA", "META", "NVDA", "AMD", "INTC", "NFLX" };
                foreach (var symbol in allSymbols)
                {
                    if (symbol.StartsWith(text))
                    {
                        suggestions.Add(symbol);
                    }
                }
            }

            sender.ItemsSource = suggestions;
        }
    }

    private void QuickAddSymbol_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (args.ChosenSuggestion != null)
        {
            sender.Text = args.ChosenSuggestion.ToString();
        }
    }

    private async void QuickAddSymbol_Click(object sender, RoutedEventArgs e)
    {
        var symbol = QuickAddSymbolBox.Text?.ToUpper();

        if (string.IsNullOrWhiteSpace(symbol))
        {
            DashboardInfoBar.Severity = InfoBarSeverity.Warning;
            DashboardInfoBar.Title = "Invalid Symbol";
            DashboardInfoBar.Message = "Please enter a valid symbol.";
            DashboardInfoBar.IsOpen = true;
            return;
        }

        var trades = QuickAddTradesCheck.IsChecked == true;
        var depth = QuickAddDepthCheck.IsChecked == true;
        var quotes = QuickAddQuotesCheck.IsChecked == true;

        // Update stream counts based on subscriptions
        if (trades)
        {
            _tradesStreamCount++;
            _tradesStreamActive = true;
        }
        if (depth)
        {
            _depthStreamCount++;
            _depthStreamActive = true;
        }
        if (quotes)
        {
            _quotesStreamCount++;
            _quotesStreamActive = true;
        }
        UpdateStreamStatusBadges();

        // Build subscription details string
        var subscriptions = new List<string>();
        if (trades) subscriptions.Add("Trades");
        if (depth) subscriptions.Add("Depth");
        if (quotes) subscriptions.Add("Quotes");
        var subscriptionText = subscriptions.Count > 0 ? string.Join(", ", subscriptions) : "None";

        DashboardInfoBar.Severity = InfoBarSeverity.Success;
        DashboardInfoBar.Title = "Symbol Added";
        DashboardInfoBar.Message = $"Added {symbol} subscription ({subscriptionText})";
        DashboardInfoBar.IsOpen = true;

        QuickAddSymbolBox.Text = string.Empty;

        await Task.Delay(3000);
        DashboardInfoBar.IsOpen = false;
    }

    private void ViewLogs_Click(object sender, RoutedEventArgs e)
    {
        // Navigate to Service Manager page for logs
        if (this.Frame != null)
        {
            this.Frame.Navigate(typeof(ServiceManagerPage));
        }
    }

    private void RunBackfill_Click(object sender, RoutedEventArgs e)
    {
        // Navigate to Backfill page
        if (this.Frame != null)
        {
            this.Frame.Navigate(typeof(BackfillPage));
        }
    }

    private void LoadActivityFeed()
    {
        _activityItems.Clear();

        // Load recent activities from the service
        var activities = _activityFeedService.Activities.Take(5);

        foreach (var activity in activities)
        {
            _activityItems.Add(CreateActivityDisplayItem(activity));
        }

        // If no activities, add some sample data for demo
        if (_activityItems.Count == 0)
        {
            AddSampleActivities();
        }

        NoActivityText.Visibility = _activityItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void AddSampleActivities()
    {
        var sampleActivities = new[]
        {
            new ActivityDisplayItem
            {
                Title = "Collector Started",
                Description = "Data collection has been started for all providers",
                Icon = "\uE768",
                IconBackground = new SolidColorBrush(Color.FromArgb(255, 72, 187, 120)),
                RelativeTime = "Just now"
            },
            new ActivityDisplayItem
            {
                Title = "Symbol Added",
                Description = "NVDA has been added to your watchlist",
                Icon = "\uE710",
                IconBackground = new SolidColorBrush(Color.FromArgb(255, 88, 166, 255)),
                RelativeTime = "2m ago"
            },
            new ActivityDisplayItem
            {
                Title = "Backfill Completed",
                Description = "Downloaded 12,450 bars for SPY from Alpaca",
                Icon = "\uE73E",
                IconBackground = new SolidColorBrush(Color.FromArgb(255, 72, 187, 120)),
                RelativeTime = "15m ago"
            },
            new ActivityDisplayItem
            {
                Title = "Provider Connected",
                Description = "Interactive Brokers connection established",
                Icon = "\uE703",
                IconBackground = new SolidColorBrush(Color.FromArgb(255, 88, 166, 255)),
                RelativeTime = "1h ago"
            }
        };

        foreach (var item in sampleActivities)
        {
            _activityItems.Add(item);
        }
    }

    private static ActivityDisplayItem CreateActivityDisplayItem(ActivityItem activity)
    {
        var iconBackground = activity.ColorCategory switch
        {
            "Success" => new SolidColorBrush(Color.FromArgb(255, 72, 187, 120)),
            "Error" => new SolidColorBrush(Color.FromArgb(255, 248, 81, 73)),
            "Warning" => new SolidColorBrush(Color.FromArgb(255, 210, 153, 34)),
            _ => new SolidColorBrush(Color.FromArgb(255, 88, 166, 255))
        };

        return new ActivityDisplayItem
        {
            Title = activity.Title,
            Description = activity.Description ?? string.Empty,
            Icon = activity.Icon,
            IconBackground = iconBackground,
            RelativeTime = activity.RelativeTime
        };
    }

    private void ActivityFeedService_ActivityAdded(object? sender, ActivityItem e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            _activityItems.Insert(0, CreateActivityDisplayItem(e));
            while (_activityItems.Count > 5)
            {
                _activityItems.RemoveAt(_activityItems.Count - 1);
            }
            NoActivityText.Visibility = Visibility.Collapsed;
        });
    }

    private void ViewAllActivity_Click(object sender, RoutedEventArgs e)
    {
        // Navigate to a full activity log page or show a flyout
        // For now, navigate to service manager which has logs
        if (this.Frame != null)
        {
            this.Frame.Navigate(typeof(ServiceManagerPage));
        }
    }

    private void UpdateQuickStats()
    {
        // Update quick stats - in real app these would come from services
        var eventsToday = ViewModel.PublishedCount;
        if (eventsToday >= 1000000)
        {
            TotalEventsToday.Text = $"{eventsToday / 1000000.0:N1}M";
        }
        else if (eventsToday >= 1000)
        {
            TotalEventsToday.Text = $"{eventsToday / 1000.0:N1}K";
        }
        else
        {
            TotalEventsToday.Text = eventsToday.ToString("N0");
        }

        ActiveSymbolsCount.Text = ViewModel.Symbols?.Count.ToString() ?? "0";

        // Simulated values for demo
        StorageUsedText.Text = "2.4 GB";
        DataQualityText.Text = "99.8%";
        AvgLatencyText.Text = "12ms";
    }

    #region Integrity Events

    private void LoadIntegrityEvents()
    {
        _integrityItems.Clear();

        var events = _integrityEventsService.GetRecentEvents(10);

        foreach (var evt in events)
        {
            _integrityItems.Add(CreateIntegrityDisplayItem(evt));
        }

        // If no events, add sample data for demo
        if (_integrityItems.Count == 0)
        {
            AddSampleIntegrityEvents();
        }

        UpdateIntegrityListVisibility();
    }

    private void AddSampleIntegrityEvents()
    {
        // Add some sample integrity events for demonstration
        var sampleEvents = new[]
        {
            new IntegrityEventDisplayItem
            {
                Id = Guid.NewGuid().ToString(),
                Symbol = "SPY",
                EventTypeName = "Sequence Gap",
                Description = "Sequence gap detected: expected 12345, got 12350 (5 missing)",
                Severity = IntegritySeverity.Warning,
                SeverityColor = Color.FromArgb(255, 210, 153, 34),
                RelativeTime = "5m ago",
                IsNotAcknowledged = Visibility.Visible
            },
            new IntegrityEventDisplayItem
            {
                Id = Guid.NewGuid().ToString(),
                Symbol = "AAPL",
                EventTypeName = "Stale Data",
                Description = "No data received for 2m 30s",
                Severity = IntegritySeverity.Warning,
                SeverityColor = Color.FromArgb(255, 210, 153, 34),
                RelativeTime = "15m ago",
                IsNotAcknowledged = Visibility.Visible
            },
            new IntegrityEventDisplayItem
            {
                Id = Guid.NewGuid().ToString(),
                Symbol = "QQQ",
                EventTypeName = "Provider Switch",
                Description = "Provider switched from Alpaca to Polygon: connection lost",
                Severity = IntegritySeverity.Info,
                SeverityColor = Color.FromArgb(255, 88, 166, 255),
                RelativeTime = "1h ago",
                IsNotAcknowledged = Visibility.Collapsed
            }
        };

        foreach (var item in sampleEvents)
        {
            _integrityItems.Add(item);
        }
    }

    private static IntegrityEventDisplayItem CreateIntegrityDisplayItem(IntegrityEvent evt)
    {
        var severityColor = evt.Severity switch
        {
            IntegritySeverity.Critical => Color.FromArgb(255, 248, 81, 73),
            IntegritySeverity.Warning => Color.FromArgb(255, 210, 153, 34),
            _ => Color.FromArgb(255, 88, 166, 255)
        };

        var eventTypeName = evt.EventType switch
        {
            IntegrityEventType.SequenceGap => "Sequence Gap",
            IntegrityEventType.OutOfOrder => "Out of Order",
            IntegrityEventType.StaleData => "Stale Data",
            IntegrityEventType.ValidationFailure => "Validation",
            IntegrityEventType.Duplicate => "Duplicate",
            IntegrityEventType.ProviderSwitch => "Provider Switch",
            _ => "Other"
        };

        return new IntegrityEventDisplayItem
        {
            Id = evt.Id,
            Symbol = evt.Symbol,
            EventTypeName = eventTypeName,
            Description = evt.Description,
            Severity = evt.Severity,
            SeverityColor = severityColor,
            RelativeTime = evt.RelativeTime,
            IsNotAcknowledged = evt.IsAcknowledged ? Visibility.Collapsed : Visibility.Visible
        };
    }

    private void UpdateIntegritySummary()
    {
        var summary = _integrityEventsService.GetSummary();

        IntegrityTotalEventsText.Text = summary.TotalEvents.ToString();
        IntegrityLast24hText.Text = summary.EventsLast24Hours.ToString();
        IntegrityUnacknowledgedText.Text = summary.UnacknowledgedCount.ToString();
        IntegrityMostAffectedText.Text = summary.MostAffectedSymbol;

        // Update badge visibility
        if (summary.CriticalCount > 0)
        {
            CriticalAlertsBadge.Visibility = Visibility.Visible;
            CriticalAlertsCount.Text = summary.CriticalCount.ToString();
        }
        else
        {
            CriticalAlertsBadge.Visibility = Visibility.Collapsed;
        }

        if (summary.WarningCount > 0)
        {
            WarningAlertsBadge.Visibility = Visibility.Visible;
            WarningAlertsCount.Text = summary.WarningCount.ToString();
        }
        else
        {
            WarningAlertsBadge.Visibility = Visibility.Collapsed;
        }
    }

    private void UpdateIntegrityListVisibility()
    {
        NoIntegrityEventsText.Visibility = _integrityItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void IntegrityEventsService_EventRecorded(object? sender, IntegrityEvent e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            _integrityItems.Insert(0, CreateIntegrityDisplayItem(e));
            while (_integrityItems.Count > 10)
            {
                _integrityItems.RemoveAt(_integrityItems.Count - 1);
            }
            UpdateIntegritySummary();
            UpdateIntegrityListVisibility();
        });
    }

    private void IntegrityEventsService_EventsCleared(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            _integrityItems.Clear();
            UpdateIntegritySummary();
            UpdateIntegrityListVisibility();
        });
    }

    private void ExpandIntegrityPanel_Click(object sender, RoutedEventArgs e)
    {
        _isIntegrityPanelExpanded = !_isIntegrityPanelExpanded;

        if (_isIntegrityPanelExpanded)
        {
            IntegrityDetailsPanel.Visibility = Visibility.Visible;
            ExpandIntegrityIcon.Glyph = "\uE70E"; // Chevron up
            ExpandIntegrityText.Text = "Hide Details";
        }
        else
        {
            IntegrityDetailsPanel.Visibility = Visibility.Collapsed;
            ExpandIntegrityIcon.Glyph = "\uE70D"; // Chevron down
            ExpandIntegrityText.Text = "Show Details";
        }
    }

    private async void ClearIntegrityAlerts_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "Clear Integrity Alerts",
            Content = "Are you sure you want to clear all integrity alerts?",
            PrimaryButtonText = "Clear All",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            _integrityEventsService.ClearEvents();

            DashboardInfoBar.Severity = InfoBarSeverity.Success;
            DashboardInfoBar.Title = "Alerts Cleared";
            DashboardInfoBar.Message = "All integrity alerts have been cleared.";
            DashboardInfoBar.IsOpen = true;

            await Task.Delay(3000);
            DashboardInfoBar.IsOpen = false;
        }
    }

    private void AcknowledgeIntegrityEvent_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string eventId)
        {
            _integrityEventsService.AcknowledgeEvent(eventId);

            // Update the display item
            var item = _integrityItems.FirstOrDefault(i => i.Id == eventId);
            if (item != null)
            {
                item.IsNotAcknowledged = Visibility.Collapsed;
            }

            UpdateIntegritySummary();
        }
    }

    private void ViewAllIntegrityEvents_Click(object sender, RoutedEventArgs e)
    {
        // Navigate to Archive Health page which shows all integrity events
        if (this.Frame != null)
        {
            this.Frame.Navigate(typeof(ArchiveHealthPage));
        }
    }

    private async void ExportIntegrityReport_Click(object sender, RoutedEventArgs e)
    {
        // Export integrity report
        var events = _integrityEventsService.GetAllEvents();
        var summary = _integrityEventsService.GetSummary();

        // Build report content
        var report = $"Data Integrity Report\n" +
                     $"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
                     $"{'='.PadLeft(50, '=')}\n\n" +
                     $"Summary:\n" +
                     $"  Total Events: {summary.TotalEvents}\n" +
                     $"  Critical: {summary.CriticalCount}\n" +
                     $"  Warning: {summary.WarningCount}\n" +
                     $"  Info: {summary.InfoCount}\n" +
                     $"  Last 24 Hours: {summary.EventsLast24Hours}\n" +
                     $"  Unacknowledged: {summary.UnacknowledgedCount}\n" +
                     $"  Most Affected Symbol: {summary.MostAffectedSymbol}\n\n" +
                     $"Recent Events:\n";

        foreach (var evt in events.Take(20))
        {
            report += $"  [{evt.Timestamp:yyyy-MM-dd HH:mm:ss}] {evt.Symbol} - {evt.Description}\n";
        }

        // Copy to clipboard for now (in real app, save to file)
        var dataPackage = new Windows.ApplicationModel.DataTransfer.DataPackage();
        dataPackage.SetText(report);
        Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);

        DashboardInfoBar.Severity = InfoBarSeverity.Success;
        DashboardInfoBar.Title = "Report Exported";
        DashboardInfoBar.Message = "Integrity report has been copied to clipboard.";
        DashboardInfoBar.IsOpen = true;

        await Task.Delay(3000);
        DashboardInfoBar.IsOpen = false;
    }

    #endregion
}

/// <summary>
/// Display model for activity items in the dashboard feed.
/// </summary>
public class ActivityDisplayItem
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = "\uE946";
    public SolidColorBrush IconBackground { get; set; } = new(Microsoft.UI.Colors.Gray);
    public string RelativeTime { get; set; } = string.Empty;
}

/// <summary>
/// Display model for integrity events in the dashboard.
/// </summary>
public class IntegrityEventDisplayItem
{
    public string Id { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public string EventTypeName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public IntegritySeverity Severity { get; set; }
    public Color SeverityColor { get; set; }
    public string RelativeTime { get; set; } = string.Empty;
    public Visibility IsNotAcknowledged { get; set; } = Visibility.Visible;
}
