using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MarketDataCollector.Wpf.Contracts;
using MarketDataCollector.Wpf.Services;
using Timer = System.Timers.Timer;

namespace MarketDataCollector.Wpf.Views;

/// <summary>
/// Order book page showing market depth (L2 data).
/// </summary>
public partial class OrderBookPage : Page
{
    private readonly HttpClient _httpClient = new();
    private readonly ObservableCollection<OrderBookLevel> _bids = new();
    private readonly ObservableCollection<OrderBookLevel> _asks = new();
    private readonly ObservableCollection<RecentTradeModel> _recentTrades = new();
    private readonly List<string> _availableSymbols = new();
    private Timer? _refreshTimer;
    private CancellationTokenSource? _cts;
    private string _baseUrl = "http://localhost:8080";
    private string _selectedSymbol = string.Empty;
    private int _depthLevels = 10;

    public OrderBookPage()
    {
        InitializeComponent();

        BidsControl.ItemsSource = _bids;
        AsksControl.ItemsSource = _asks;
        RecentTradesControl.ItemsSource = _recentTrades;

        // Get base URL from StatusService
        _baseUrl = StatusService.Instance.BaseUrl;

        // Subscribe to connection events
        ConnectionService.Instance.StateChanged += OnConnectionStateChanged;

        Unloaded += OnPageUnloaded;
    }

    private void OnPageUnloaded(object sender, RoutedEventArgs e)
    {
        ConnectionService.Instance.StateChanged -= OnConnectionStateChanged;
        _refreshTimer?.Stop();
        _refreshTimer?.Dispose();
        _cts?.Cancel();
        _cts?.Dispose();
    }

    private async void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        UpdateConnectionStatus();
        await LoadSymbolsAsync();

        // Start refresh timer (every 250ms for order book updates)
        _refreshTimer = new Timer(250);
        _refreshTimer.Elapsed += async (_, _) => await Dispatcher.InvokeAsync(RefreshOrderBookAsync);
        _refreshTimer.Start();
    }

    private void OnConnectionStateChanged(object? sender, ConnectionStateChangedEventArgs e)
    {
        Dispatcher.Invoke(UpdateConnectionStatus);
    }

    private void UpdateConnectionStatus()
    {
        var state = ConnectionService.Instance.State;

        ConnectionStatusText.Text = state switch
        {
            ConnectionState.Connected => "Connected",
            ConnectionState.Reconnecting => "Reconnecting...",
            _ => "Disconnected"
        };

        ConnectionIndicator.Fill = new SolidColorBrush(state switch
        {
            ConnectionState.Connected => Color.FromRgb(63, 185, 80),
            ConnectionState.Reconnecting => Color.FromRgb(255, 193, 7),
            _ => Color.FromRgb(139, 148, 158)
        });
    }

    private async Task LoadSymbolsAsync()
    {
        try
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();

            var status = await StatusService.Instance.GetStatusAsync(_cts.Token);
            if (status != null)
            {
                _availableSymbols.Clear();

                if (status.TryGetProperty("activeSubscriptions", out var subs) && subs.ValueKind == JsonValueKind.Array)
                {
                    foreach (var sub in subs.EnumerateArray())
                    {
                        if (sub.TryGetProperty("symbol", out var symProp))
                        {
                            var symbol = symProp.GetString();
                            if (!string.IsNullOrEmpty(symbol) && !_availableSymbols.Contains(symbol))
                            {
                                _availableSymbols.Add(symbol);
                            }
                        }
                    }
                }

                if (_availableSymbols.Count == 0)
                {
                    _availableSymbols.AddRange(new[] { "SPY", "AAPL", "MSFT", "GOOGL", "AMZN" });
                }

                SymbolComboBox.ItemsSource = _availableSymbols;
                if (_availableSymbols.Count > 0 && SymbolComboBox.SelectedItem == null)
                {
                    SymbolComboBox.SelectedIndex = 0;
                }
            }
        }
        catch (Exception ex)
        {
            LoggingService.Instance.LogError("Failed to load symbols", ex);
        }
    }

    private async Task RefreshOrderBookAsync()
    {
        if (string.IsNullOrEmpty(_selectedSymbol))
            return;

        try
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();

            // Fetch order book data from API
            var response = await _httpClient.GetAsync(
                $"{_baseUrl}/api/orderbook/{Uri.EscapeDataString(_selectedSymbol)}?levels={_depthLevels}",
                _cts.Token);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(_cts.Token);
                var data = JsonSerializer.Deserialize<JsonElement>(json);

                ProcessOrderBookData(data);
                NoDataText.Visibility = Visibility.Collapsed;
            }
            else
            {
                // Load demo data if API not available
                if (_bids.Count == 0 && _asks.Count == 0)
                {
                    LoadDemoOrderBook();
                }
            }

            // Fetch recent trades
            await RefreshRecentTradesAsync();
        }
        catch (OperationCanceledException)
        {
            // Cancelled - ignore
        }
        catch (HttpRequestException)
        {
            if (_bids.Count == 0 && _asks.Count == 0)
            {
                LoadDemoOrderBook();
            }
        }
        catch (Exception ex)
        {
            LoggingService.Instance.LogError("Failed to refresh order book", ex);
        }
    }

    private void ProcessOrderBookData(JsonElement data)
    {
        decimal maxSize = 0;

        // Process bids
        var newBids = new List<OrderBookLevel>();
        if (data.TryGetProperty("bids", out var bids) && bids.ValueKind == JsonValueKind.Array)
        {
            decimal runningTotal = 0;
            foreach (var bid in bids.EnumerateArray().Take(_depthLevels))
            {
                var price = bid.TryGetProperty("price", out var p) ? p.GetDecimal() : 0;
                var size = bid.TryGetProperty("size", out var s) ? s.GetInt32() : 0;
                runningTotal += size;

                newBids.Add(new OrderBookLevel
                {
                    RawPrice = price,
                    Price = price.ToString("F2"),
                    RawSize = size,
                    Size = FormatSize(size),
                    RawTotal = runningTotal,
                    Total = FormatSize((int)runningTotal)
                });

                if (size > maxSize) maxSize = size;
            }
        }

        // Process asks
        var newAsks = new List<OrderBookLevel>();
        if (data.TryGetProperty("asks", out var asks) && asks.ValueKind == JsonValueKind.Array)
        {
            decimal runningTotal = 0;
            foreach (var ask in asks.EnumerateArray().Take(_depthLevels))
            {
                var price = ask.TryGetProperty("price", out var p) ? p.GetDecimal() : 0;
                var size = ask.TryGetProperty("size", out var s) ? s.GetInt32() : 0;
                runningTotal += size;

                newAsks.Add(new OrderBookLevel
                {
                    RawPrice = price,
                    Price = price.ToString("F2"),
                    RawSize = size,
                    Size = FormatSize(size),
                    RawTotal = runningTotal,
                    Total = FormatSize((int)runningTotal)
                });

                if (size > maxSize) maxSize = size;
            }
        }

        // Calculate depth widths (relative to max size)
        var maxWidth = 200.0;
        foreach (var level in newBids.Concat(newAsks))
        {
            level.DepthWidth = maxSize > 0 ? (double)level.RawSize / (double)maxSize * maxWidth : 0;
        }

        // Update collections
        _bids.Clear();
        foreach (var bid in newBids)
        {
            _bids.Add(bid);
        }

        _asks.Clear();
        foreach (var ask in newAsks.OrderByDescending(a => a.RawPrice))
        {
            _asks.Add(ask);
        }

        // Update statistics
        UpdateStatistics(newBids, newAsks);
    }

    private void LoadDemoOrderBook()
    {
        var basePrice = _selectedSymbol switch
        {
            "SPY" => 485.50m,
            "AAPL" => 178.25m,
            "MSFT" => 405.75m,
            "GOOGL" => 142.30m,
            "AMZN" => 175.80m,
            _ => 100.00m
        };

        var random = new Random();
        var bids = new List<OrderBookLevel>();
        var asks = new List<OrderBookLevel>();

        decimal bidTotal = 0;
        decimal askTotal = 0;
        decimal maxSize = 0;

        for (int i = 0; i < _depthLevels; i++)
        {
            var bidSize = random.Next(100, 5000);
            var askSize = random.Next(100, 5000);
            bidTotal += bidSize;
            askTotal += askSize;

            if (bidSize > maxSize) maxSize = bidSize;
            if (askSize > maxSize) maxSize = askSize;

            bids.Add(new OrderBookLevel
            {
                RawPrice = basePrice - (i * 0.01m),
                Price = (basePrice - (i * 0.01m)).ToString("F2"),
                RawSize = bidSize,
                Size = FormatSize(bidSize),
                RawTotal = bidTotal,
                Total = FormatSize((int)bidTotal)
            });

            asks.Add(new OrderBookLevel
            {
                RawPrice = basePrice + 0.01m + (i * 0.01m),
                Price = (basePrice + 0.01m + (i * 0.01m)).ToString("F2"),
                RawSize = askSize,
                Size = FormatSize(askSize),
                RawTotal = askTotal,
                Total = FormatSize((int)askTotal)
            });
        }

        // Calculate depth widths
        var maxWidth = 200.0;
        foreach (var level in bids.Concat(asks))
        {
            level.DepthWidth = maxSize > 0 ? (double)level.RawSize / (double)maxSize * maxWidth : 0;
        }

        _bids.Clear();
        foreach (var bid in bids)
        {
            _bids.Add(bid);
        }

        _asks.Clear();
        foreach (var ask in asks.OrderByDescending(a => a.RawPrice))
        {
            _asks.Add(ask);
        }

        UpdateStatistics(bids, asks);
        NoDataText.Visibility = Visibility.Collapsed;

        // Load demo trades
        LoadDemoTrades(basePrice);
    }

    private void LoadDemoTrades(decimal basePrice)
    {
        var random = new Random();
        _recentTrades.Clear();

        for (int i = 0; i < 15; i++)
        {
            var isBuy = random.Next(2) == 0;
            var price = basePrice + (random.Next(-10, 11) * 0.01m);

            _recentTrades.Add(new RecentTradeModel
            {
                Time = DateTime.Now.AddSeconds(-i * random.Next(1, 5)).ToString("HH:mm:ss"),
                Price = price.ToString("F2"),
                Size = FormatSize(random.Next(10, 1000)),
                PriceColor = new SolidColorBrush(isBuy ?
                    Color.FromRgb(63, 185, 80) : Color.FromRgb(244, 67, 54))
            });
        }

        NoTradesText.Visibility = Visibility.Collapsed;
    }

    private void UpdateStatistics(List<OrderBookLevel> bids, List<OrderBookLevel> asks)
    {
        if (bids.Count == 0 || asks.Count == 0)
        {
            BestBidText.Text = "--";
            BestAskText.Text = "--";
            MidPriceText.Text = "--";
            SpreadText.Text = "--";
            SpreadPercentText.Text = "";
            BidVolumeText.Text = "--";
            AskVolumeText.Text = "--";
            ImbalanceText.Text = "--";
            return;
        }

        var bestBid = bids.Max(b => b.RawPrice);
        var bestAsk = asks.Min(a => a.RawPrice);
        var spread = bestAsk - bestBid;
        var mid = (bestBid + bestAsk) / 2;
        var spreadPercent = mid > 0 ? spread / mid * 100 : 0;

        var bidVolume = bids.Sum(b => b.RawSize);
        var askVolume = asks.Sum(a => a.RawSize);
        var totalVolume = bidVolume + askVolume;
        var imbalance = totalVolume > 0 ? (bidVolume - askVolume) / totalVolume * 100 : 0;

        BestBidText.Text = bestBid.ToString("F2");
        BestAskText.Text = bestAsk.ToString("F2");
        MidPriceText.Text = mid.ToString("F2");
        SpreadText.Text = spread.ToString("F2");
        SpreadPercentText.Text = $"({spreadPercent:F3}%)";
        BidVolumeText.Text = FormatSize((int)bidVolume);
        AskVolumeText.Text = FormatSize((int)askVolume);
        ImbalanceText.Text = $"{imbalance:+0.0;-0.0;0.0}%";

        // Update imbalance bar
        var bidRatio = totalVolume > 0 ? (double)bidVolume / totalVolume : 0.5;
        BidBarColumn.Width = new GridLength(bidRatio, GridUnitType.Star);
        AskBarColumn.Width = new GridLength(1 - bidRatio, GridUnitType.Star);
    }

    private async Task RefreshRecentTradesAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync(
                $"{_baseUrl}/api/live/{Uri.EscapeDataString(_selectedSymbol)}/trades?limit=15",
                _cts!.Token);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(_cts.Token);
                var data = JsonSerializer.Deserialize<JsonElement>(json);

                if (data.ValueKind == JsonValueKind.Array)
                {
                    _recentTrades.Clear();

                    foreach (var trade in data.EnumerateArray())
                    {
                        var price = trade.TryGetProperty("price", out var p) ? p.GetDecimal() : 0;
                        var size = trade.TryGetProperty("size", out var s) ? s.GetInt32() : 0;
                        var timestamp = trade.TryGetProperty("timestamp", out var ts) ? ts.GetDateTime() : DateTime.UtcNow;
                        var side = trade.TryGetProperty("side", out var sd) ? sd.GetString() ?? "" : "";

                        var isBuy = side.Equals("buy", StringComparison.OrdinalIgnoreCase);

                        _recentTrades.Add(new RecentTradeModel
                        {
                            Time = timestamp.ToString("HH:mm:ss"),
                            Price = price.ToString("F2"),
                            Size = FormatSize(size),
                            PriceColor = new SolidColorBrush(isBuy ?
                                Color.FromRgb(63, 185, 80) : Color.FromRgb(244, 67, 54))
                        });
                    }

                    NoTradesText.Visibility = _recentTrades.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                }
            }
        }
        catch
        {
            // Ignore trade fetch errors
        }
    }

    private static string FormatSize(int size)
    {
        if (size >= 1000000) return $"{size / 1000000.0:F1}M";
        if (size >= 1000) return $"{size / 1000.0:F1}K";
        return size.ToString("N0");
    }

    private void Symbol_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SymbolComboBox.SelectedItem is string symbol)
        {
            _selectedSymbol = symbol;
            _bids.Clear();
            _asks.Clear();
            _recentTrades.Clear();
            NoDataText.Visibility = Visibility.Visible;
            NoTradesText.Visibility = Visibility.Visible;
        }
    }

    private void Levels_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LevelsComboBox.SelectedItem is ComboBoxItem item)
        {
            if (int.TryParse(item.Content?.ToString(), out var levels))
            {
                _depthLevels = levels;
            }
        }
    }
}

/// <summary>
/// Model for order book level display.
/// </summary>
public class OrderBookLevel
{
    public decimal RawPrice { get; set; }
    public string Price { get; set; } = string.Empty;
    public int RawSize { get; set; }
    public string Size { get; set; } = string.Empty;
    public decimal RawTotal { get; set; }
    public string Total { get; set; } = string.Empty;
    public double DepthWidth { get; set; }
}

/// <summary>
/// Model for recent trade display.
/// </summary>
public class RecentTradeModel
{
    public string Time { get; set; } = string.Empty;
    public string Price { get; set; } = string.Empty;
    public string Size { get; set; } = string.Empty;
    public SolidColorBrush PriceColor { get; set; } = new(Colors.Gray);
}
