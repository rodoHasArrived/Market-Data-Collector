using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using MarketDataCollector.Uwp.Services;

namespace MarketDataCollector.Uwp.Views;

/// <summary>
/// Live order book visualization page with depth heatmap and time &amp; sales.
/// </summary>
public sealed partial class OrderBookPage : Page
{
    private readonly OrderBookVisualizationService _orderBookService;
    private readonly SymbolManagementService _symbolService;
    private string? _selectedSymbol;
    private bool _isStreaming;
    private readonly DispatcherTimer _updateTimer;

    public OrderBookPage()
    {
        this.InitializeComponent();
        _orderBookService = new OrderBookVisualizationService();
        _symbolService = new SymbolManagementService();

        _updateTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _updateTimer.Tick += UpdateTimer_Tick;

        _orderBookService.OrderBookUpdated += OnOrderBookUpdated;
        _orderBookService.TradeReceived += OnTradeReceived;

        LoadSymbolsAsync();
    }

    private async void LoadSymbolsAsync()
    {
        try
        {
            var symbols = await _symbolService.GetAllSymbolsAsync();
            foreach (var symbol in symbols)
            {
                SymbolSelector.Items.Add(new ComboBoxItem { Content = symbol.Symbol, Tag = symbol.Symbol });
            }

            if (SymbolSelector.Items.Count > 0)
            {
                SymbolSelector.SelectedIndex = 0;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading symbols: {ex.Message}");
        }
    }

    private async void SymbolSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SymbolSelector.SelectedItem is ComboBoxItem item && item.Tag is string symbol)
        {
            if (_selectedSymbol != null && _isStreaming)
            {
                await _orderBookService.UnsubscribeAsync(_selectedSymbol);
            }

            _selectedSymbol = symbol;

            if (_isStreaming)
            {
                await StartStreamingAsync();
            }
        }
    }

    private async void StreamToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_isStreaming)
        {
            await StopStreamingAsync();
        }
        else
        {
            await StartStreamingAsync();
        }
    }

    private async Task StartStreamingAsync()
    {
        if (string.IsNullOrEmpty(_selectedSymbol))
            return;

        try
        {
            ConnectionStatus.Visibility = Visibility.Visible;
            ConnectionStatusText.Text = $"Connecting to {_selectedSymbol}...";

            var depthLevels = GetSelectedDepthLevels();
            await _orderBookService.SubscribeAsync(_selectedSymbol, depthLevels);

            _isStreaming = true;
            StreamToggle.IsChecked = true;
            StreamIcon.Glyph = "\uE71A"; // Stop icon
            StreamText.Text = "Stop";

            _updateTimer.Start();

            NoOrderBookText.Visibility = Visibility.Collapsed;
            NoTradesText.Visibility = Visibility.Collapsed;
            NoDepthText.Visibility = Visibility.Collapsed;

            ConnectionStatus.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            ConnectionStatusText.Text = $"Connection failed: {ex.Message}";
            await Task.Delay(2000);
            ConnectionStatus.Visibility = Visibility.Collapsed;
        }
    }

    private async Task StopStreamingAsync()
    {
        if (string.IsNullOrEmpty(_selectedSymbol))
            return;

        try
        {
            _updateTimer.Stop();
            await _orderBookService.UnsubscribeAsync(_selectedSymbol);

            _isStreaming = false;
            StreamToggle.IsChecked = false;
            StreamIcon.Glyph = "\uE768"; // Play icon
            StreamText.Text = "Start";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error stopping stream: {ex.Message}");
        }
    }

    private int GetSelectedDepthLevels()
    {
        if (DepthLevelSelector.SelectedItem is ComboBoxItem item && item.Tag is string tagStr)
        {
            return int.Parse(tagStr);
        }
        return 10;
    }

    private void UpdateTimer_Tick(object? sender, object e)
    {
        if (string.IsNullOrEmpty(_selectedSymbol))
            return;

        UpdateOrderBookDisplay();
        UpdateDepthChart();
        UpdateOrderFlowStats();
    }

    private void OnOrderBookUpdated(object? sender, OrderBookUpdateEventArgs e)
    {
        if (e.Symbol != _selectedSymbol)
            return;

        DispatcherQueue.TryEnqueue(() =>
        {
            UpdateOrderBookDisplay();
            UpdateDepthChart();
            UpdateOrderFlowStats();
        });
    }

    private void OnTradeReceived(object? sender, TradeEventArgs e)
    {
        if (e.Symbol != _selectedSymbol)
            return;

        DispatcherQueue.TryEnqueue(() =>
        {
            UpdateTimeAndSales();
        });
    }

    private void UpdateOrderBookDisplay()
    {
        if (string.IsNullOrEmpty(_selectedSymbol))
            return;

        var heatmap = _orderBookService.GetHeatmapData(_selectedSymbol, GetSelectedDepthLevels());

        // Update stats
        MidPriceText.Text = heatmap.BestBid > 0 ? $"{(heatmap.BestBid + heatmap.BestAsk) / 2:F2}" : "--";
        BestBidText.Text = heatmap.BestBid > 0 ? $"{heatmap.BestBid:F2}" : "--";
        BestAskText.Text = heatmap.BestAsk > 0 && heatmap.BestAsk < decimal.MaxValue ? $"{heatmap.BestAsk:F2}" : "--";
        SpreadText.Text = heatmap.SpreadBps > 0 ? $"{heatmap.SpreadBps:F1}" : "--";

        var imbalance = heatmap.Imbalance * 100;
        ImbalanceText.Text = $"{imbalance:F1}%";
        ImbalanceText.Foreground = imbalance > 0
            ? (SolidColorBrush)Application.Current.Resources["SuccessColorBrush"]
            : (SolidColorBrush)Application.Current.Resources["ErrorColorBrush"];

        // Update bid levels
        var bidLevels = heatmap.Levels
            .Where(l => l.BidSize > 0)
            .OrderByDescending(l => l.Price)
            .Select((l, i) => new OrderBookLevelViewModel
            {
                Price = $"{l.Price:F2}",
                Size = $"{l.BidSize:N0}",
                Cumulative = $"{heatmap.Levels.Where(x => x.Price >= l.Price && x.BidSize > 0).Sum(x => x.BidSize):N0}",
                BarWidth = l.BidIntensity * 200,
                IsBid = true
            })
            .ToList();

        BidLevelsList.ItemsSource = bidLevels;

        // Update ask levels
        var askLevels = heatmap.Levels
            .Where(l => l.AskSize > 0)
            .OrderBy(l => l.Price)
            .Select((l, i) => new OrderBookLevelViewModel
            {
                Price = $"{l.Price:F2}",
                Size = $"{l.AskSize:N0}",
                Cumulative = $"{heatmap.Levels.Where(x => x.Price <= l.Price && x.AskSize > 0).Sum(x => x.AskSize):N0}",
                BarWidth = l.AskIntensity * 200,
                IsBid = false
            })
            .ToList();

        AskLevelsList.ItemsSource = askLevels;

        NoOrderBookText.Visibility = bidLevels.Count == 0 && askLevels.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void UpdateDepthChart()
    {
        if (string.IsNullOrEmpty(_selectedSymbol))
            return;

        var depthData = _orderBookService.GetDepthChartData(_selectedSymbol, 30);

        var maxDepth = Math.Max(depthData.TotalBidDepth, depthData.TotalAskDepth);
        if (maxDepth <= 0)
        {
            NoDepthText.Visibility = Visibility.Visible;
            return;
        }

        NoDepthText.Visibility = Visibility.Collapsed;

        // Bid depth bars (reversed for right-to-left display)
        var bidBars = depthData.BidPoints
            .Select(p => new DepthBarViewModel
            {
                BarHeight = (double)(p.CumulativeSize / maxDepth) * 120,
                Tooltip = $"{p.Price:F2}: {p.CumulativeSize:N0} cumulative"
            })
            .Reverse()
            .ToList();

        BidDepthBars.ItemsSource = bidBars;

        // Ask depth bars
        var askBars = depthData.AskPoints
            .Select(p => new DepthBarViewModel
            {
                BarHeight = (double)(p.CumulativeSize / maxDepth) * 120,
                Tooltip = $"{p.Price:F2}: {p.CumulativeSize:N0} cumulative"
            })
            .ToList();

        AskDepthBars.ItemsSource = askBars;

        // Update imbalance indicator
        DepthImbalanceText.Text = depthData.DepthImbalance != 0
            ? $"Depth Imbalance: {depthData.DepthImbalance * 100:F1}% ({(depthData.DepthImbalance > 0 ? "Bid Heavy" : "Ask Heavy")})"
            : "";
    }

    private void UpdateTimeAndSales()
    {
        if (string.IsNullOrEmpty(_selectedSymbol))
            return;

        var tasData = _orderBookService.GetTimeAndSales(_selectedSymbol, 50);

        TotalVolumeText.Text = $"{tasData.TotalVolume:N0}";
        BuyVolumeText.Text = $"{tasData.BuyVolume:N0}";
        SellVolumeText.Text = $"{tasData.SellVolume:N0}";

        var trades = tasData.RecentTrades
            .Select(t => new TradeViewModel
            {
                Time = t.Timestamp.ToString("HH:mm:ss.fff"),
                Price = $"{t.Price:F2}",
                Size = $"{t.Size:N0}",
                PriceColor = t.Side == TradeSide.Buy
                    ? (SolidColorBrush)Application.Current.Resources["SuccessColorBrush"]
                    : t.Side == TradeSide.Sell
                        ? (SolidColorBrush)Application.Current.Resources["ErrorColorBrush"]
                        : new SolidColorBrush(Microsoft.UI.Colors.White)
            })
            .ToList();

        TradesList.ItemsSource = trades;
        NoTradesText.Visibility = trades.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateOrderFlowStats()
    {
        if (string.IsNullOrEmpty(_selectedSymbol))
            return;

        var stats = _orderBookService.GetOrderFlowStats(_selectedSymbol);
        VwapText.Text = stats.Vwap > 0 ? $"{stats.Vwap:F2}" : "--";
    }
}

// View Models
public class OrderBookLevelViewModel
{
    public string Price { get; set; } = string.Empty;
    public string Size { get; set; } = string.Empty;
    public string Cumulative { get; set; } = string.Empty;
    public double BarWidth { get; set; }
    public bool IsBid { get; set; }
}

public class TradeViewModel
{
    public string Time { get; set; } = string.Empty;
    public string Price { get; set; } = string.Empty;
    public string Size { get; set; } = string.Empty;
    public SolidColorBrush PriceColor { get; set; } = new(Microsoft.UI.Colors.White);
}

public class DepthBarViewModel
{
    public double BarHeight { get; set; }
    public string Tooltip { get; set; } = string.Empty;
}
