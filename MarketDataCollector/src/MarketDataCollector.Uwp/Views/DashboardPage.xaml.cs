using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using MarketDataCollector.Uwp.ViewModels;
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
    private bool _isCollectorRunning = true;
    private DateTime _startTime;

    public DashboardPage()
    {
        this.InitializeComponent();
        ViewModel = new MainViewModel();
        DataContext = ViewModel;
        _startTime = DateTime.UtcNow;

        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _refreshTimer.Tick += RefreshTimer_Tick;

        _sparklineTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _sparklineTimer.Tick += SparklineTimer_Tick;

        Loaded += DashboardPage_Loaded;
        Unloaded += DashboardPage_Unloaded;
    }

    private async void DashboardPage_Loaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.LoadAsync();
        InitializeSparklineData();
        _refreshTimer.Start();
        _sparklineTimer.Start();
        UpdateCollectorStatus();
        UpdateSparklines();
        UpdateThroughputChart();
    }

    private void DashboardPage_Unloaded(object sender, RoutedEventArgs e)
    {
        _refreshTimer.Stop();
        _sparklineTimer.Stop();
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
            CollectorStatusBadge.Background = new SolidColorBrush(Color.FromArgb(255, 72, 187, 120));
            CollectorStatusText.Text = "Running";
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

    private async void StartCollector_Click(object sender, RoutedEventArgs e)
    {
        _isCollectorRunning = true;
        _startTime = DateTime.UtcNow;
        UpdateCollectorStatus();

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
        UpdateCollectorStatus();

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

        DashboardInfoBar.Severity = InfoBarSeverity.Success;
        DashboardInfoBar.Title = "Symbol Added";
        DashboardInfoBar.Message = $"Added {symbol} subscription (Trades: {trades}, Depth: {depth})";
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
}
