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
using Windows.Foundation;

namespace MarketDataCollector.Uwp.Views;

/// <summary>
/// Enhanced dashboard page with sparklines, throughput graphs, and quick actions.
/// </summary>
public sealed partial class DashboardPage : Page
{
    #region Cached Brushes (Performance Optimization)

    // Static cached brushes to avoid repeated allocations
    private static readonly SolidColorBrush s_successBrush = new(Color.FromArgb(255, 72, 187, 120));
    private static readonly SolidColorBrush s_warningBrush = new(Color.FromArgb(255, 237, 137, 54));
    private static readonly SolidColorBrush s_dangerBrush = new(Color.FromArgb(255, 245, 101, 101));
    private static readonly SolidColorBrush s_infoBrush = new(Color.FromArgb(255, 88, 166, 255));
    private static readonly SolidColorBrush s_inactiveBrush = new(Color.FromArgb(255, 160, 174, 192));
    private static readonly SolidColorBrush s_criticalBrush = new(Color.FromArgb(255, 248, 81, 73));
    private static readonly SolidColorBrush s_warningEventBrush = new(Color.FromArgb(255, 210, 153, 34));

    #endregion

    public MainViewModel ViewModel { get; }

    // Consolidated single timer for all updates (performance optimization)
    private readonly DispatcherTimer _unifiedTimer;
    private int _timerTickCount = 0;

    // Use fixed-size arrays with circular buffer pattern for O(1) operations
    private const int SparklineCapacity = 20;
    private const int ThroughputCapacity = 30;
    private readonly double[] _publishedHistory = new double[SparklineCapacity];
    private readonly double[] _droppedHistory = new double[SparklineCapacity];
    private readonly double[] _integrityHistory = new double[SparklineCapacity];
    private readonly double[] _throughputHistory = new double[ThroughputCapacity];
    private int _sparklineIndex = 0;
    private int _sparklineCount = 0;
    private int _throughputIndex = 0;
    private int _throughputCount = 0;

    // Reusable PointCollection instances to avoid allocations
    private readonly PointCollection _publishedPoints = new();
    private readonly PointCollection _droppedPoints = new();
    private readonly PointCollection _integrityPoints = new();

    private readonly Random _random = new();
    private readonly ActivityFeedService _activityFeedService;
    private readonly IntegrityEventsService _integrityEventsService;
    private readonly ObservableCollection<ActivityDisplayItem> _activityItems;
    private readonly ObservableCollection<IntegrityEventDisplayItem> _integrityItems;
    private bool _isCollectorRunning = true;
    private bool _isCollectorPaused = false;
    private bool _isIntegrityPanelExpanded = false;
    private DateTime _startTime;
    private DateTime _collectorStartTime;

    // Cancellation token source for async operations (e.g., InfoBar auto-dismiss)
    private CancellationTokenSource? _infoDismissCts;

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

        // Single unified timer at 500ms interval (replaces 3 separate timers)
        // - Sparkline updates: every tick (500ms)
        // - Uptime updates: every 2 ticks (1s)
        // - Refresh updates: every 4 ticks (2s)
        _unifiedTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _unifiedTimer.Tick += UnifiedTimer_Tick;

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
        _unifiedTimer.Start();
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
        // Stop and dispose timer to prevent resource leaks
        _unifiedTimer.Stop();
        _unifiedTimer.Tick -= UnifiedTimer_Tick;

        // Cancel any pending InfoBar dismiss operations
        _infoDismissCts?.Cancel();
        _infoDismissCts?.Dispose();
        _infoDismissCts = null;

        // Unsubscribe from service events to prevent memory leaks
        _activityFeedService.ActivityAdded -= ActivityFeedService_ActivityAdded;
        _integrityEventsService.EventRecorded -= IntegrityEventsService_EventRecorded;
        _integrityEventsService.EventsCleared -= IntegrityEventsService_EventsCleared;
    }

    /// <summary>
    /// Shows the InfoBar with auto-dismiss after a delay based on severity.
    /// Errors stay visible longer (10s) to ensure users notice them.
    /// Success messages dismiss quickly (3s).
    /// </summary>
    private async Task ShowInfoBarAsync(InfoBarSeverity severity, string title, string message, int? customDelayMs = null)
    {
        // Cancel any previous pending dismiss
        _infoDismissCts?.Cancel();
        _infoDismissCts?.Dispose();
        _infoDismissCts = new CancellationTokenSource();

        DashboardInfoBar.Severity = severity;
        DashboardInfoBar.Title = title;
        DashboardInfoBar.Message = message;
        DashboardInfoBar.IsOpen = true;

        // Use severity-appropriate durations:
        // - Success: 3 seconds (quick confirmation)
        // - Info: 4 seconds
        // - Warning: 6 seconds (user should notice)
        // - Error: 10 seconds (requires attention)
        var delayMs = customDelayMs ?? InfoBarService.GetDurationForSeverity(severity);

        if (delayMs > 0)
        {
            try
            {
                await Task.Delay(delayMs, _infoDismissCts.Token);
                DashboardInfoBar.IsOpen = false;
            }
            catch (OperationCanceledException)
            {
                // Dismiss was cancelled (page unloaded or new message shown) - this is expected
            }
        }
        // If delayMs is 0, keep the InfoBar open until manually closed
    }

    /// <summary>
    /// Shows an error InfoBar with context and remedy information.
    /// Error messages stay visible for 10 seconds.
    /// </summary>
    private async Task ShowErrorAsync(string title, string message, string? context = null, string? remedy = null)
    {
        var fullMessage = message;
        if (!string.IsNullOrEmpty(context))
        {
            fullMessage += $"\n\nDetails: {context}";
        }
        if (!string.IsNullOrEmpty(remedy))
        {
            fullMessage += $"\n\nSuggestion: {remedy}";
        }

        await ShowInfoBarAsync(InfoBarSeverity.Error, title, fullMessage);
    }

    /// <summary>
    /// Shows an error InfoBar from an exception with user-friendly details.
    /// </summary>
    private async Task ShowExceptionErrorAsync(Exception ex, string operation)
    {
        var errorDetails = InfoBarService.CreateErrorDetails(ex, operation);
        await ShowInfoBarAsync(errorDetails.Severity, errorDetails.Title, errorDetails.GetFormattedMessage());
    }

    /// <summary>
    /// Unified timer tick handler that replaces 3 separate timers.
    /// Runs at 500ms and dispatches work based on tick count.
    /// </summary>
    private void UnifiedTimer_Tick(object? sender, object e)
    {
        _timerTickCount++;

        // Sparkline updates: every tick (500ms) - highest frequency
        AddSparklineData();
        UpdateSparklines();

        // Uptime updates: every 2 ticks (1s)
        if (_timerTickCount % 2 == 0)
        {
            UpdateCollectorUptime();
        }

        // Refresh updates: every 4 ticks (2s) - lowest frequency
        if (_timerTickCount % 4 == 0)
        {
            UpdateUptime();
            UpdateLatency();
            UpdateThroughputStats();
        }
    }

    private void InitializeSparklineData()
    {
        // Initialize circular buffers with sample data
        for (int i = 0; i < SparklineCapacity; i++)
        {
            _publishedHistory[i] = 800 + _random.Next(0, 400);
            _droppedHistory[i] = _random.Next(0, 5);
            _integrityHistory[i] = _random.Next(0, 3);
        }
        _sparklineIndex = 0;
        _sparklineCount = SparklineCapacity;

        for (int i = 0; i < ThroughputCapacity; i++)
        {
            _throughputHistory[i] = 1000 + _random.Next(-200, 400);
        }
        _throughputIndex = 0;
        _throughputCount = ThroughputCapacity;
    }

    private void AddSparklineData()
    {
        // Add new data points using circular buffer pattern (O(1) instead of O(n))
        _publishedHistory[_sparklineIndex] = 800 + _random.Next(0, 400);
        _droppedHistory[_sparklineIndex] = _random.Next(0, 5);
        _integrityHistory[_sparklineIndex] = _random.Next(0, 3);

        _sparklineIndex = (_sparklineIndex + 1) % SparklineCapacity;
        if (_sparklineCount < SparklineCapacity) _sparklineCount++;

        _throughputHistory[_throughputIndex] = 1000 + _random.Next(-200, 400);
        _throughputIndex = (_throughputIndex + 1) % ThroughputCapacity;
        if (_throughputCount < ThroughputCapacity) _throughputCount++;
    }

    private void UpdateSparklines()
    {
        // Use reusable PointCollections to avoid allocations
        UpdateSparklineCircular(PublishedSparklinePath, _publishedHistory, _sparklineIndex, _sparklineCount,
            PublishedSparkline.ActualWidth, 30, _publishedPoints);
        UpdateSparklineCircular(DroppedSparklinePath, _droppedHistory, _sparklineIndex, _sparklineCount,
            DroppedSparkline.ActualWidth, 30, _droppedPoints);
        UpdateSparklineCircular(IntegritySparklinePath, _integrityHistory, _sparklineIndex, _sparklineCount,
            IntegritySparkline.ActualWidth, 30, _integrityPoints);

        // Update rate text - get most recent value from circular buffer
        if (_sparklineCount > 0)
        {
            var lastIndex = (_sparklineIndex - 1 + SparklineCapacity) % SparklineCapacity;
            var rate = (int)_publishedHistory[lastIndex];
            PublishedRateText.Text = $"+{rate:N0}/s";
        }
    }

    /// <summary>
    /// Updates sparkline using circular buffer data and reusable PointCollection.
    /// </summary>
    private static void UpdateSparklineCircular(
        Microsoft.UI.Xaml.Shapes.Polyline polyline,
        double[] data,
        int currentIndex,
        int count,
        double width,
        double height,
        PointCollection points)
    {
        if (count < 2 || width <= 0) return;

        // Clear and reuse the PointCollection instead of creating new
        points.Clear();

        var max = 1.0;
        var min = 0.0;

        // Find max value in circular buffer
        for (int i = 0; i < count; i++)
        {
            var idx = (currentIndex - count + i + data.Length) % data.Length;
            if (data[idx] > max) max = data[idx];
        }

        var step = width / (count - 1);

        // Build points in order from oldest to newest
        for (int i = 0; i < count; i++)
        {
            var idx = (currentIndex - count + i + data.Length) % data.Length;
            var x = i * step;
            var y = height - ((data[idx] - min) / (max - min) * height);
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
        if (_throughputCount > 0)
        {
            // Get most recent value from circular buffer
            var lastIndex = (_throughputIndex - 1 + ThroughputCapacity) % ThroughputCapacity;
            var current = (int)_throughputHistory[lastIndex];
            var avg = 0.0;
            var peak = 0.0;

            // Calculate from circular buffer
            for (int i = 0; i < _throughputCount; i++)
            {
                var idx = (_throughputIndex - _throughputCount + i + ThroughputCapacity) % ThroughputCapacity;
                avg += _throughputHistory[idx];
                if (_throughputHistory[idx] > peak) peak = _throughputHistory[idx];
            }
            avg /= _throughputCount;

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
        // Use cached brushes instead of creating new instances
        LatencyText.Foreground = latency < 20 ? s_successBrush
            : latency < 50 ? s_warningBrush : s_dangerBrush;
    }

    private void UpdateCollectorStatus()
    {
        if (_isCollectorRunning)
        {
            if (_isCollectorPaused)
            {
                CollectorStatusBadge.Background = s_warningBrush;
                CollectorStatusText.Text = "Paused";
            }
            else
            {
                CollectorStatusBadge.Background = s_successBrush;
                CollectorStatusText.Text = "Running";
            }
            StartCollectorButton.IsEnabled = false;
            StopCollectorButton.IsEnabled = true;
        }
        else
        {
            CollectorStatusBadge.Background = s_dangerBrush;
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
        // Update Trades stream badge using cached brushes
        TradesStreamBadge.Background = GetStreamStatusBrush(_tradesStreamActive);
        TradesStreamCount.Text = $"({_tradesStreamCount})";

        // Update Depth stream badge
        DepthStreamBadge.Background = GetStreamStatusBrush(_depthStreamActive);
        DepthStreamCount.Text = $"({_depthStreamCount})";

        // Update Quotes stream badge
        QuotesStreamBadge.Background = GetStreamStatusBrush(_quotesStreamActive);
        QuotesStreamCount.Text = $"({_quotesStreamCount})";
    }

    /// <summary>
    /// Gets the appropriate cached brush for stream status.
    /// </summary>
    private SolidColorBrush GetStreamStatusBrush(bool isStreamActive)
    {
        if (isStreamActive && _isCollectorRunning && !_isCollectorPaused)
            return s_successBrush;
        if (isStreamActive && _isCollectorPaused)
            return s_warningBrush;
        return s_inactiveBrush;
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
        try
        {
            _isCollectorRunning = true;
            _isCollectorPaused = false;
            _collectorStartTime = DateTime.UtcNow;
            _startTime = DateTime.UtcNow;
            UpdateCollectorStatus();
            UpdateQuickActionsCollectorStatus();
            UpdateStreamStatusBadges();

            await ShowInfoBarAsync(InfoBarSeverity.Success, "Collector Started", "Market data collection has been started.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error starting collector: {ex.Message}");
        }
    }

    private async void QuickStopCollector_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _isCollectorRunning = false;
            _isCollectorPaused = false;
            UpdateCollectorStatus();
            UpdateQuickActionsCollectorStatus();
            UpdateStreamStatusBadges();

            await ShowInfoBarAsync(InfoBarSeverity.Warning, "Collector Stopped", "Market data collection has been stopped.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error stopping collector: {ex.Message}");
        }
    }

    private async void QuickPauseCollector_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!_isCollectorRunning) return;

            _isCollectorPaused = !_isCollectorPaused;
            UpdateCollectorStatus();
            UpdateQuickActionsCollectorStatus();
            UpdateStreamStatusBadges();

            if (_isCollectorPaused)
            {
                await ShowInfoBarAsync(InfoBarSeverity.Informational, "Collection Paused", "Market data collection has been paused. Click Resume to continue.");
            }
            else
            {
                await ShowInfoBarAsync(InfoBarSeverity.Success, "Collection Resumed", "Market data collection has been resumed.");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error pausing collector: {ex.Message}");
        }
    }

    private async void StartCollector_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _isCollectorRunning = true;
            _isCollectorPaused = false;
            _collectorStartTime = DateTime.UtcNow;
            _startTime = DateTime.UtcNow;
            UpdateCollectorStatus();
            UpdateQuickActionsCollectorStatus();
            UpdateStreamStatusBadges();

            await ShowInfoBarAsync(InfoBarSeverity.Success, "Collector Started", "Market data collection has been started.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error starting collector: {ex.Message}");
        }
    }

    private async void StopCollector_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _isCollectorRunning = false;
            _isCollectorPaused = false;
            UpdateCollectorStatus();
            UpdateQuickActionsCollectorStatus();
            UpdateStreamStatusBadges();

            await ShowInfoBarAsync(InfoBarSeverity.Warning, "Collector Stopped", "Market data collection has been stopped.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error stopping collector: {ex.Message}");
        }
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
            await ShowInfoBarAsync(InfoBarSeverity.Warning, "Invalid Symbol", "Please enter a valid symbol.");
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

        QuickAddSymbolBox.Text = string.Empty;

        await ShowInfoBarAsync(InfoBarSeverity.Success, "Symbol Added", $"Added {symbol} subscription ({subscriptionText})");
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
        // Use cached brushes for sample activities
        var sampleActivities = new[]
        {
            new ActivityDisplayItem
            {
                Title = "Collector Started",
                Description = "Data collection has been started for all providers",
                Icon = "\uE768",
                IconBackground = s_successBrush,
                RelativeTime = "Just now"
            },
            new ActivityDisplayItem
            {
                Title = "Symbol Added",
                Description = "NVDA has been added to your watchlist",
                Icon = "\uE710",
                IconBackground = s_infoBrush,
                RelativeTime = "2m ago"
            },
            new ActivityDisplayItem
            {
                Title = "Backfill Completed",
                Description = "Downloaded 12,450 bars for SPY from Alpaca",
                Icon = "\uE73E",
                IconBackground = s_successBrush,
                RelativeTime = "15m ago"
            },
            new ActivityDisplayItem
            {
                Title = "Provider Connected",
                Description = "Interactive Brokers connection established",
                Icon = "\uE703",
                IconBackground = s_infoBrush,
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
        // Use cached brushes based on color category
        var iconBackground = activity.ColorCategory switch
        {
            "Success" => s_successBrush,
            "Error" => s_criticalBrush,
            "Warning" => s_warningEventBrush,
            _ => s_infoBrush
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
            await ShowInfoBarAsync(InfoBarSeverity.Success, "Alerts Cleared", "All integrity alerts have been cleared.");
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

        await ShowInfoBarAsync(InfoBarSeverity.Success, "Report Exported", "Integrity report has been copied to clipboard.");
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
