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
using WpfServices = MarketDataCollector.Wpf.Services;
using Timer = System.Timers.Timer;

namespace MarketDataCollector.Wpf.Views;

/// <summary>
/// Live data viewer page showing real-time market data feed.
/// </summary>
public partial class LiveDataViewerPage : Page
{
    private readonly HttpClient _httpClient = new();
    private readonly ObservableCollection<LiveDataEventModel> _liveEvents = new();
    private readonly List<string> _availableSymbols = new();
    private Timer? _refreshTimer;
    private Timer? _statsTimer;
    private CancellationTokenSource? _cts;
    private string _baseUrl = "http://localhost:8080";
    private string _selectedSymbol = string.Empty;
    private bool _isPaused;
    private int _eventsThisSecond;
    private int _totalEvents;
    private DateTime _lastStatsUpdate = DateTime.UtcNow;

    // Session statistics
    private decimal? _sessionHigh;
    private decimal? _sessionLow;
    private long _sessionVolume;
    private int _tradeCount;
    private decimal _vwapNumerator;
    private decimal _lastPrice;
    private decimal _bidPrice;
    private decimal _askPrice;
    private int _bidSize;
    private int _askSize;

    private readonly StatusService _statusService;
    private readonly ConnectionService _connectionService;
    private readonly WpfServices.LoggingService _loggingService;
    private readonly WpfServices.NotificationService _notificationService;

    public LiveDataViewerPage(
        StatusService statusService,
        ConnectionService connectionService,
        WpfServices.LoggingService loggingService,
        WpfServices.NotificationService notificationService)
    {
        InitializeComponent();

        _statusService = statusService;
        _connectionService = connectionService;
        _loggingService = loggingService;
        _notificationService = notificationService;

        LiveFeedList.ItemsSource = _liveEvents;

        // Get base URL from StatusService
        _baseUrl = _statusService.BaseUrl;

        // Subscribe to connection events
        _connectionService.StateChanged += OnConnectionStateChanged;

        Unloaded += OnPageUnloaded;
    }

    private void OnPageUnloaded(object sender, RoutedEventArgs e)
    {
        _connectionService.StateChanged -= OnConnectionStateChanged;
        _refreshTimer?.Stop();
        _refreshTimer?.Dispose();
        _statsTimer?.Stop();
        _statsTimer?.Dispose();
        _cts?.Cancel();
        _cts?.Dispose();
    }

    private async void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        UpdateConnectionStatus();
        await LoadSymbolsAsync();

        // Start data refresh timer (every 500ms for live data)
        _refreshTimer = new Timer(500);
        _refreshTimer.Elapsed += async (_, _) => await Dispatcher.InvokeAsync(RefreshLiveDataAsync);
        _refreshTimer.Start();

        // Start stats update timer (every second)
        _statsTimer = new Timer(1000);
        _statsTimer.Elapsed += (_, _) => Dispatcher.Invoke(UpdateStats);
        _statsTimer.Start();
    }

    private void OnConnectionStateChanged(object? sender, ConnectionStateChangedEventArgs e)
    {
        Dispatcher.Invoke(UpdateConnectionStatus);
    }

    private void UpdateConnectionStatus()
    {
        var state = _connectionService.State;
        var isConnected = state == ConnectionState.Connected;

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

        PauseResumeButton.IsEnabled = isConnected;
    }

    private async Task LoadSymbolsAsync()
    {
        try
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();

            // Get symbols from status service
            var status = await _statusService.GetStatusAsync(_cts.Token);
            if (status != null)
            {
                _availableSymbols.Clear();

                // SimpleStatus doesn't contain subscription details, so use default symbols
                // In a real implementation, this would call a separate API endpoint
                _availableSymbols.AddRange(new[] { "SPY", "AAPL", "MSFT", "GOOGL", "AMZN", "QQQ", "IWM", "DIA" });

                SymbolComboBox.ItemsSource = _availableSymbols;
                if (_availableSymbols.Count > 0 && SymbolComboBox.SelectedItem == null)
                {
                    SymbolComboBox.SelectedIndex = 0;
                }
            }
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Failed to load symbols", ex);
        }
    }

    private async Task RefreshLiveDataAsync()
    {
        if (_isPaused || string.IsNullOrEmpty(_selectedSymbol))
            return;

        try
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();

            // Fetch live data from API
            var response = await _httpClient.GetAsync(
                $"{_baseUrl}/api/live/{Uri.EscapeDataString(_selectedSymbol)}/recent?limit=50",
                _cts.Token);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(_cts.Token);
                var data = JsonSerializer.Deserialize<JsonElement>(json);

                if (data.ValueKind == JsonValueKind.Array)
                {
                    var newEvents = new List<LiveDataEventModel>();

                    foreach (var item in data.EnumerateArray())
                    {
                        var eventModel = ParseLiveEvent(item);
                        if (eventModel != null)
                        {
                            newEvents.Add(eventModel);
                            UpdateSessionStats(eventModel);
                        }
                    }

                    // Add new events (that we haven't seen before)
                    var existingIds = _liveEvents.Select(e => e.Id).ToHashSet();
                    foreach (var evt in newEvents.Where(e => !existingIds.Contains(e.Id)).OrderBy(e => e.RawTimestamp))
                    {
                        if (ShouldShowEvent(evt))
                        {
                            _liveEvents.Add(evt);
                            _eventsThisSecond++;
                            _totalEvents++;

                            // Limit list size
                            while (_liveEvents.Count > 500)
                            {
                                _liveEvents.RemoveAt(0);
                            }
                        }
                    }

                    // Auto-scroll if enabled
                    if (AutoScrollCheck.IsChecked == true && _liveEvents.Count > 0)
                    {
                        LiveFeedList.ScrollIntoView(_liveEvents.Last());
                    }

                    NoDataText.Visibility = _liveEvents.Count > 0 ? Visibility.Collapsed : Visibility.Visible;
                }
            }

            // Also fetch current quote
            await RefreshQuoteAsync();
        }
        catch (OperationCanceledException)
        {
            // Cancelled - ignore
        }
        catch (HttpRequestException)
        {
            // Network error - will retry
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Failed to refresh live data", ex);
        }
    }

    private async Task RefreshQuoteAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync(
                $"{_baseUrl}/api/live/{Uri.EscapeDataString(_selectedSymbol)}/quote",
                _cts!.Token);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(_cts.Token);
                var quote = JsonSerializer.Deserialize<JsonElement>(json);

                if (quote.TryGetProperty("bid", out var bidProp))
                    _bidPrice = bidProp.GetDecimal();
                if (quote.TryGetProperty("ask", out var askProp))
                    _askPrice = askProp.GetDecimal();
                if (quote.TryGetProperty("bidSize", out var bidSizeProp))
                    _bidSize = bidSizeProp.GetInt32();
                if (quote.TryGetProperty("askSize", out var askSizeProp))
                    _askSize = askSizeProp.GetInt32();

                UpdateQuoteDisplay();
            }
        }
        catch
        {
            // Ignore quote fetch errors
        }
    }

    private LiveDataEventModel? ParseLiveEvent(JsonElement item)
    {
        try
        {
            var type = item.TryGetProperty("type", out var typeProp) ? typeProp.GetString() ?? "TRADE" : "TRADE";
            var symbol = item.TryGetProperty("symbol", out var symProp) ? symProp.GetString() ?? "" : "";
            var price = item.TryGetProperty("price", out var priceProp) ? priceProp.GetDecimal() : 0m;
            var size = item.TryGetProperty("size", out var sizeProp) ? sizeProp.GetInt32() :
                       item.TryGetProperty("volume", out var volProp) ? volProp.GetInt32() : 0;
            var exchange = item.TryGetProperty("exchange", out var exchProp) ? exchProp.GetString() ?? "" : "";
            var timestamp = item.TryGetProperty("timestamp", out var tsProp) ? tsProp.GetDateTime() : DateTime.UtcNow;

            var id = item.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? "" :
                     $"{timestamp:HHmmssfffff}_{type}_{price}";

            var isBuy = type.Equals("TRADE", StringComparison.OrdinalIgnoreCase) &&
                       item.TryGetProperty("side", out var sideProp) &&
                       sideProp.GetString()?.Equals("buy", StringComparison.OrdinalIgnoreCase) == true;

            return new LiveDataEventModel
            {
                Id = id,
                RawTimestamp = timestamp,
                Timestamp = timestamp.ToString("HH:mm:ss.fff"),
                Type = type.ToUpperInvariant() switch
                {
                    "TRADE" => "TRD",
                    "QUOTE" => "QTE",
                    "BBO" => "BBO",
                    _ => type[..Math.Min(3, type.Length)].ToUpperInvariant()
                },
                Symbol = symbol,
                Price = price.ToString("F2"),
                RawPrice = price,
                Size = FormatSize(size),
                Exchange = exchange,
                TypeColor = new SolidColorBrush(type.ToUpperInvariant() switch
                {
                    "TRADE" => Color.FromRgb(63, 185, 80),
                    "QUOTE" or "BBO" => Color.FromRgb(88, 166, 255),
                    _ => Color.FromRgb(139, 148, 158)
                }),
                PriceColor = new SolidColorBrush(isBuy ?
                    Color.FromRgb(63, 185, 80) : Color.FromRgb(244, 67, 54))
            };
        }
        catch
        {
            return null;
        }
    }

    private bool ShouldShowEvent(LiveDataEventModel evt)
    {
        if (evt.Type == "TRD" && ShowTradesCheck.IsChecked != true)
            return false;
        if ((evt.Type == "QTE" || evt.Type == "BBO") && ShowQuotesCheck.IsChecked != true)
            return false;
        return true;
    }

    private void UpdateSessionStats(LiveDataEventModel evt)
    {
        if (evt.Type != "TRD" || evt.RawPrice <= 0)
            return;

        _lastPrice = evt.RawPrice;

        if (!_sessionHigh.HasValue || evt.RawPrice > _sessionHigh)
            _sessionHigh = evt.RawPrice;
        if (!_sessionLow.HasValue || evt.RawPrice < _sessionLow)
            _sessionLow = evt.RawPrice;

        // Parse size for volume
        if (int.TryParse(evt.Size.Replace(",", ""), out var size))
        {
            _sessionVolume += size;
            _tradeCount++;
            _vwapNumerator += evt.RawPrice * size;
        }
    }

    private void UpdateStats()
    {
        // Calculate events per second
        var now = DateTime.UtcNow;
        if ((now - _lastStatsUpdate).TotalSeconds >= 1)
        {
            EventsPerSecText.Text = _eventsThisSecond.ToString();
            _eventsThisSecond = 0;
            _lastStatsUpdate = now;
        }

        TotalEventsText.Text = FormatNumber(_totalEvents);
    }

    private void UpdateQuoteDisplay()
    {
        BidPriceText.Text = _bidPrice > 0 ? _bidPrice.ToString("F2") : "--";
        BidSizeText.Text = _bidSize > 0 ? FormatSize(_bidSize) : "--";
        AskPriceText.Text = _askPrice > 0 ? _askPrice.ToString("F2") : "--";
        AskSizeText.Text = _askSize > 0 ? FormatSize(_askSize) : "--";

        if (_bidPrice > 0 && _askPrice > 0)
        {
            var spread = _askPrice - _bidPrice;
            var mid = (_bidPrice + _askPrice) / 2;
            SpreadText.Text = spread.ToString("F2");
            MidPriceText.Text = mid.ToString("F2");
        }
        else
        {
            SpreadText.Text = "--";
            MidPriceText.Text = "--";
        }

        LastTradeText.Text = _lastPrice > 0 ? _lastPrice.ToString("F2") : "--";
        LastTradeTimeText.Text = _lastPrice > 0 ? DateTime.Now.ToString("HH:mm:ss") : "--";

        SessionHighText.Text = _sessionHigh?.ToString("F2") ?? "--";
        SessionLowText.Text = _sessionLow?.ToString("F2") ?? "--";
        SessionVolumeText.Text = _sessionVolume > 0 ? FormatNumber(_sessionVolume) : "--";
        TradeCountText.Text = _tradeCount > 0 ? FormatNumber(_tradeCount) : "--";
        VwapText.Text = _sessionVolume > 0 ? (_vwapNumerator / _sessionVolume).ToString("F2") : "--";
    }

    private static string FormatSize(int size)
    {
        return size >= 1000 ? $"{size:N0}" : size.ToString();
    }

    private static string FormatNumber(long num)
    {
        if (num >= 1_000_000) return $"{num / 1_000_000.0:F1}M";
        if (num >= 1_000) return $"{num / 1_000.0:F1}K";
        return num.ToString("N0");
    }

    private void Symbol_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SymbolComboBox.SelectedItem is string symbol)
        {
            _selectedSymbol = symbol;
            ResetSessionStats();
            _liveEvents.Clear();
            NoDataText.Visibility = Visibility.Visible;
        }
    }

    private void ResetSessionStats()
    {
        _sessionHigh = null;
        _sessionLow = null;
        _sessionVolume = 0;
        _tradeCount = 0;
        _vwapNumerator = 0;
        _lastPrice = 0;
        _bidPrice = 0;
        _askPrice = 0;
        _bidSize = 0;
        _askSize = 0;
        _totalEvents = 0;

        UpdateQuoteDisplay();
        TotalEventsText.Text = "0";
    }

    private void AddSymbol_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new AddSymbolDialog();
        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.Symbol))
        {
            var symbol = dialog.Symbol.ToUpperInvariant().Trim();
            if (!_availableSymbols.Contains(symbol))
            {
                _availableSymbols.Add(symbol);
                SymbolComboBox.ItemsSource = null;
                SymbolComboBox.ItemsSource = _availableSymbols;
            }
            SymbolComboBox.SelectedItem = symbol;

            _notificationService.ShowNotification(
                "Symbol Added",
                $"Added {symbol} to the symbol list.",
                NotificationType.Success);
        }
    }

    private void PauseResume_Click(object sender, RoutedEventArgs e)
    {
        _isPaused = !_isPaused;
        PauseResumeButton.Content = _isPaused ? "Resume" : "Pause";

        _notificationService.ShowNotification(
            _isPaused ? "Paused" : "Resumed",
            _isPaused ? "Live data feed has been paused." : "Live data feed has been resumed.",
            NotificationType.Info);
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        _liveEvents.Clear();
        ResetSessionStats();
        NoDataText.Visibility = Visibility.Visible;

        _notificationService.ShowNotification(
            "Cleared",
            "Live data feed has been cleared.",
            NotificationType.Info);
    }
}

/// <summary>
/// Model for live data events in the feed.
/// </summary>
public class LiveDataEventModel
{
    public string Id { get; set; } = string.Empty;
    public DateTime RawTimestamp { get; set; }
    public string Timestamp { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public string Price { get; set; } = string.Empty;
    public decimal RawPrice { get; set; }
    public string Size { get; set; } = string.Empty;
    public string Exchange { get; set; } = string.Empty;
    public SolidColorBrush TypeColor { get; set; } = new(Colors.Gray);
    public SolidColorBrush PriceColor { get; set; } = new(Colors.Gray);
}

/// <summary>
/// Dialog for adding a new symbol to watch.
/// </summary>
public class AddSymbolDialog : Window
{
    private readonly TextBox _symbolBox;

    public string Symbol => _symbolBox.Text;

    public AddSymbolDialog()
    {
        Title = "Add Symbol";
        Width = 300;
        Height = 150;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        Background = new SolidColorBrush(Color.FromRgb(30, 30, 30));

        var grid = new Grid { Margin = new Thickness(16) };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var label = new TextBlock
        {
            Text = "Enter symbol to add:",
            Foreground = new SolidColorBrush(Colors.White),
            Margin = new Thickness(0, 0, 0, 8)
        };
        Grid.SetRow(label, 0);
        grid.Children.Add(label);

        _symbolBox = new TextBox
        {
            Background = new SolidColorBrush(Color.FromRgb(45, 45, 45)),
            Foreground = new SolidColorBrush(Colors.White),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(8, 6, 8, 6),
            Margin = new Thickness(0, 0, 0, 16)
        };
        Grid.SetRow(_symbolBox, 1);
        grid.Children.Add(_symbolBox);

        var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        Grid.SetRow(buttonPanel, 2);

        var cancelBtn = new Button
        {
            Content = "Cancel",
            Padding = new Thickness(16, 6, 16, 6),
            Margin = new Thickness(0, 0, 8, 0),
            Background = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
            Foreground = new SolidColorBrush(Colors.White),
            BorderThickness = new Thickness(0)
        };
        cancelBtn.Click += (_, _) => { DialogResult = false; Close(); };
        buttonPanel.Children.Add(cancelBtn);

        var okBtn = new Button
        {
            Content = "Add",
            Padding = new Thickness(16, 6, 16, 6),
            Background = new SolidColorBrush(Color.FromRgb(88, 166, 255)),
            Foreground = new SolidColorBrush(Colors.White),
            BorderThickness = new Thickness(0)
        };
        okBtn.Click += (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(_symbolBox.Text))
            {
                DialogResult = true;
                Close();
            }
        };
        buttonPanel.Children.Add(okBtn);

        grid.Children.Add(buttonPanel);
        Content = grid;

        _symbolBox.Focus();
    }
}
