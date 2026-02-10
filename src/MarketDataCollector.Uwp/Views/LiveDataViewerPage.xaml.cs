using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using MarketDataCollector.Uwp.Services;
using MarketDataCollector.Uwp.Models;

namespace MarketDataCollector.Uwp.Views;

/// <summary>
/// Page for viewing real-time market data including trades, quotes, and order book.
/// </summary>
public sealed partial class LiveDataViewerPage : Page
{
    private readonly LiveDataService _liveDataService;
    private readonly DispatcherTimer _refreshTimer;
    private string _currentSymbol = string.Empty;
    private bool _isSubscribed;

    public LiveDataViewerPage()
    {
        InitializeComponent();
        _liveDataService = LiveDataService.Instance;

        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(1000)
        };
        _refreshTimer.Tick += RefreshTimer_Tick;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (AutoRefreshToggle.IsOn)
        {
            _refreshTimer.Start();
        }
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        _refreshTimer.Stop();
    }

    private async void RefreshTimer_Tick(object? sender, object e)
    {
        try
        {
            if (!string.IsNullOrEmpty(_currentSymbol) && _isSubscribed)
            {
                await RefreshDataAsync();
            }
        }
        catch (Exception ex)
        {
            LoggingService.Instance.LogError("Error in RefreshTimer_Tick", ex);
        }
    }

    private void AutoRefresh_Toggled(object sender, RoutedEventArgs e)
    {
        if (AutoRefreshToggle.IsOn)
        {
            _refreshTimer.Start();
        }
        else
        {
            _refreshTimer.Stop();
        }
    }

    private void RefreshInterval_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (RefreshIntervalCombo.SelectedItem is ComboBoxItem item && item.Tag is string ms)
        {
            if (int.TryParse(ms, out var interval))
            {
                _refreshTimer.Interval = TimeSpan.FromMilliseconds(interval);
            }
        }
    }

    private async void Symbol_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        var symbol = args.QueryText?.Trim().ToUpperInvariant();
        if (!string.IsNullOrEmpty(symbol))
        {
            await LoadSymbolDataAsync(symbol);
        }
    }

    private async void Subscribe_Click(object sender, RoutedEventArgs e)
    {
        var symbol = SymbolInput.Text?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(symbol)) return;

        LoadingRing.IsActive = true;
        LoadingRing.Visibility = Visibility.Visible;

        try
        {
            var request = new SubscribeRequest
            {
                Symbol = symbol,
                SubscribeTrades = ShowTradesCheck.IsChecked == true,
                SubscribeDepth = ShowDepthCheck.IsChecked == true,
                SubscribeQuotes = ShowQuotesCheck.IsChecked == true,
                DepthLevels = 10
            };

            var result = await _liveDataService.SubscribeAsync(request);
            if (result?.Success == true)
            {
                _isSubscribed = true;
                await LoadSymbolDataAsync(symbol);
                StreamStatusBadge.Visibility = Visibility.Visible;
                StreamStatusText.Text = "Live";
                ShowSuccess($"Subscribed to {symbol}");
            }
            else
            {
                ShowError("Failed to subscribe");
            }
        }
        finally
        {
            LoadingRing.IsActive = false;
            LoadingRing.Visibility = Visibility.Collapsed;
        }
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_currentSymbol))
        {
            await RefreshDataAsync();
        }
    }

    private async Task LoadSymbolDataAsync(string symbol)
    {
        _currentSymbol = symbol;
        CurrentSymbolText.Text = symbol;

        await RefreshDataAsync();
    }

    private async Task RefreshDataAsync()
    {
        try
        {
            var tasks = new List<Task>();

            if (ShowDepthCheck.IsChecked == true)
            {
                tasks.Add(LoadOrderBookAsync());
            }

            if (ShowTradesCheck.IsChecked == true)
            {
                tasks.Add(LoadTradesAsync());
            }

            tasks.Add(LoadOrderFlowStatsAsync());
            tasks.Add(LoadBboAsync());

            await Task.WhenAll(tasks);
        }
        catch (Exception ex)
        {
            ShowError($"Error refreshing data: {ex.Message}");
        }
    }

    private async Task LoadOrderBookAsync()
    {
        var orderBook = await _liveDataService.GetOrderBookAsync(_currentSymbol, 10);
        if (orderBook == null) return;

        // Update stats
        BestBidText.Text = orderBook.BestBid.ToString("F2");
        BestAskText.Text = orderBook.BestAsk.ToString("F2");
        SpreadText.Text = $"{orderBook.Spread:F4} ({orderBook.Spread / orderBook.MidPrice * 10000:F1} bps)";
        MidPriceText.Text = orderBook.MidPrice.ToString("F2");

        // Calculate imbalance
        var imbalance = orderBook.Imbalance;
        ImbalanceText.Text = $"{imbalance:P1}";
        ImbalanceText.Foreground = imbalance > 0
            ? (Microsoft.UI.Xaml.Media.Brush)Resources["SuccessColorBrush"]
            : (Microsoft.UI.Xaml.Media.Brush)Resources["ErrorColorBrush"];

        // Update lists
        BidsList.ItemsSource = orderBook.Bids.Select(b => new OrderBookDisplayItem
        {
            Price = b.Price.ToString("F2"),
            Size = FormatSize(b.Size),
            VolumePercent = orderBook.TotalBidVolume > 0 ? (double)(b.Size / orderBook.TotalBidVolume) : 0
        }).ToList();

        AsksList.ItemsSource = orderBook.Asks.Select(a => new OrderBookDisplayItem
        {
            Price = a.Price.ToString("F2"),
            Size = FormatSize(a.Size),
            VolumePercent = orderBook.TotalAskVolume > 0 ? (double)(a.Size / orderBook.TotalAskVolume) : 0
        }).ToList();
    }

    private async Task LoadTradesAsync()
    {
        var trades = await _liveDataService.GetRecentTradesAsync(_currentSymbol, 50);
        if (trades == null) return;

        TradeCountText.Text = trades.Count.ToString();

        if (trades.Count > 0)
        {
            LastPriceText.Text = trades[0].Price.ToString("F2");
        }

        TradesList.ItemsSource = trades.Select(t => new TradeDisplayItem
        {
            Timestamp = t.Timestamp.ToString("HH:mm:ss.fff"),
            Price = t.Price.ToString("F2"),
            Size = FormatSize(t.Size),
            Side = t.Side,
            SideColor = t.Side == "Buy" ? "#48BB78" : t.Side == "Sell" ? "#F56565" : "#A0AEC0"
        }).ToList();
    }

    private async Task LoadOrderFlowStatsAsync()
    {
        var stats = await _liveDataService.GetOrderFlowStatsAsync(_currentSymbol);
        if (stats == null) return;

        VwapText.Text = stats.Vwap.ToString("F2");
        BuyVolumeText.Text = FormatVolume(stats.BuyVolume);
        SellVolumeText.Text = FormatVolume(stats.SellVolume);
        TotalVolumeText.Text = FormatVolume(stats.TotalVolume);
        VolumeText.Text = FormatVolume(stats.TotalVolume);

        TradeCountStatText.Text = stats.TradeCount.ToString("N0");
        AvgTradeSizeText.Text = stats.AvgTradeSize.ToString("N0");
        LargestTradeText.Text = stats.LargestTrade.ToString("N0");

        // Update imbalance bar
        var total = stats.BuyVolume + stats.SellVolume;
        if (total > 0)
        {
            var buyPct = stats.BuyVolume / total;
            var sellPct = stats.SellVolume / total;

            BuyBarColumn.Width = new GridLength(buyPct, GridUnitType.Star);
            SellBarColumn.Width = new GridLength(sellPct, GridUnitType.Star);

            BuyPercentText.Text = $"{buyPct:P0}";
            SellPercentText.Text = $"{sellPct:P0}";
        }
    }

    private async Task LoadBboAsync()
    {
        var bbo = await _liveDataService.GetBboAsync(_currentSymbol);
        if (bbo == null) return;

        BestBidText.Text = bbo.BidPrice.ToString("F2");
        BestAskText.Text = bbo.AskPrice.ToString("F2");
        SpreadText.Text = $"{bbo.Spread:F4} ({bbo.SpreadBps:F1} bps)";
        MidPriceText.Text = bbo.MidPrice.ToString("F2");
    }

    private static string FormatSize(decimal size)
    {
        return size >= 1000 ? $"{size / 1000:F1}K" : size.ToString("N0");
    }

    private static string FormatVolume(decimal volume)
    {
        return volume >= 1_000_000 ? $"{volume / 1_000_000:F2}M"
             : volume >= 1_000 ? $"{volume / 1_000:F1}K"
             : volume.ToString("N0");
    }

    private void ShowSuccess(string message)
    {
        PageInfoBar.Message = message;
        PageInfoBar.Severity = InfoBarSeverity.Success;
        PageInfoBar.IsOpen = true;
    }

    private void ShowError(string message)
    {
        PageInfoBar.Message = message;
        PageInfoBar.Severity = InfoBarSeverity.Error;
        PageInfoBar.IsOpen = true;
    }
}
