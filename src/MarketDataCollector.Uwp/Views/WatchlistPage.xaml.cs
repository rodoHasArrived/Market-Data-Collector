using System.Collections.ObjectModel;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using MarketDataCollector.Uwp.Services;

namespace MarketDataCollector.Uwp.Views;

/// <summary>
/// Page for managing user's watchlist of favorite symbols.
/// </summary>
public sealed partial class WatchlistPage : Page
{
    // Static cached brushes to avoid repeated allocations
    private static readonly SolidColorBrush LimeGreenBrush = new(Microsoft.UI.Colors.LimeGreen);
    private static readonly SolidColorBrush GrayBrush = new(Microsoft.UI.Colors.Gray);
    private static readonly SolidColorBrush GoldBrush = new(Microsoft.UI.Colors.Gold);
    private static readonly SolidColorBrush OrangeBrush = new(Microsoft.UI.Colors.Orange);
    private static readonly SolidColorBrush RedBrush = new(Microsoft.UI.Colors.Red);

    private readonly WatchlistService _watchlistService;
    private readonly ObservableCollection<WatchlistDisplayItem> _favorites;
    private readonly ObservableCollection<WatchlistDisplayItem> _allSymbols;
    private readonly DispatcherTimer _refreshTimer;

    public WatchlistPage()
    {
        this.InitializeComponent();
        _watchlistService = WatchlistService.Instance;
        _favorites = new ObservableCollection<WatchlistDisplayItem>();
        _allSymbols = new ObservableCollection<WatchlistDisplayItem>();

        FavoritesList.ItemsSource = _favorites;
        AllSymbolsList.ItemsSource = _allSymbols;
        AllSymbolsGrid.ItemsSource = _allSymbols;

        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5)
        };
        _refreshTimer.Tick += RefreshTimer_Tick;

        Loaded += WatchlistPage_Loaded;
        Unloaded += WatchlistPage_Unloaded;

        _watchlistService.WatchlistChanged += WatchlistService_WatchlistChanged;
    }

    private async void WatchlistPage_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadWatchlistAsync();
        _refreshTimer.Start();
    }

    private void WatchlistPage_Unloaded(object sender, RoutedEventArgs e)
    {
        _refreshTimer.Stop();
        // Unsubscribe from service events to prevent memory leaks
        _watchlistService.WatchlistChanged -= WatchlistService_WatchlistChanged;
    }

    private void RefreshTimer_Tick(object? sender, object e)
    {
        // Simulate status updates (in real app, this would come from the collector service)
        UpdateSymbolStatuses();
    }

    private async void WatchlistService_WatchlistChanged(object? sender, WatchlistChangedEventArgs e)
    {
        await LoadWatchlistAsync();
    }

    private async Task LoadWatchlistAsync()
    {
        var items = await _watchlistService.GetSortedSymbolsAsync();

        _favorites.Clear();
        _allSymbols.Clear();

        foreach (var item in items)
        {
            var displayItem = CreateDisplayItem(item);

            if (item.IsFavorite)
            {
                _favorites.Add(displayItem);
            }

            _allSymbols.Add(displayItem);
        }

        // Update UI
        SymbolCountBadge.Text = items.Count.ToString();
        FavoritesSection.Visibility = _favorites.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyState.Visibility = _allSymbols.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private WatchlistDisplayItem CreateDisplayItem(WatchlistItem item)
    {
        var isStreaming = item.Status?.IsStreaming ?? false;
        var eventRate = item.Status?.EventRate ?? 0;
        var healthScore = item.Status?.HealthScore ?? 100;

        return new WatchlistDisplayItem
        {
            Symbol = item.Symbol,
            Notes = item.Notes ?? string.Empty,
            IsFavorite = item.IsFavorite,
            FavoriteIcon = item.IsFavorite ? "\uE735" : "\uE734",
            FavoriteColor = item.IsFavorite ? GoldBrush : GrayBrush,
            IsStreaming = isStreaming,
            StatusText = isStreaming ? "Streaming" : "Idle",
            StatusColor = isStreaming ? LimeGreenBrush : GrayBrush,
            EventRateText = $"{eventRate:N1}",
            EventCount = item.Status?.EventCount ?? 0,
            HealthScore = healthScore,
            HealthText = $"{healthScore:N0}%",
            HealthColor = healthScore >= 95 ? LimeGreenBrush : healthScore >= 80 ? OrangeBrush : RedBrush,
            HealthIcon = healthScore >= 95 ? "\uE73E" : healthScore >= 80 ? "\uE7BA" : "\uE783",
            LastEventText = item.Status?.LastEventTime?.ToString("HH:mm:ss") ?? "No data",
            AddedAt = item.AddedAt
        };
    }

    private void UpdateSymbolStatuses()
    {
        // Simulate random status updates for demo purposes
        var random = new Random();
        foreach (var item in _allSymbols)
        {
            if (random.Next(100) < 30) // 30% chance of update
            {
                var isStreaming = random.Next(100) < 70; // 70% chance of streaming
                item.IsStreaming = isStreaming;
                item.StatusText = isStreaming ? "Streaming" : "Idle";
                // Use cached brushes to avoid allocations on every tick
                item.StatusColor = isStreaming ? LimeGreenBrush : GrayBrush;

                if (isStreaming)
                {
                    item.EventRateText = $"{random.Next(10, 500):N1}";
                    item.LastEventText = DateTime.Now.ToString("HH:mm:ss");
                }
            }
        }
    }

    private async void AddSymbol_Click(object sender, RoutedEventArgs e)
    {
        await AddSymbolFromInput();
    }

    private async void AddSymbol_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (!string.IsNullOrWhiteSpace(args.QueryText))
        {
            await AddSymbolFromInput();
        }
    }

    private void AddSymbol_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        // Could add symbol suggestions here
    }

    private async Task AddSymbolFromInput()
    {
        var symbol = AddSymbolBox.Text?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(symbol)) return;

        var added = await _watchlistService.AddSymbolAsync(symbol);
        if (added)
        {
            AddSymbolBox.Text = string.Empty;
            ShowInfoBar("Symbol Added", $"{symbol} has been added to your watchlist.", InfoBarSeverity.Success);
        }
        else
        {
            ShowInfoBar("Already Exists", $"{symbol} is already in your watchlist.", InfoBarSeverity.Warning);
        }
    }

    private async void QuickAdd_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string symbol)
        {
            var added = await _watchlistService.AddSymbolAsync(symbol);
            if (added)
            {
                ShowInfoBar("Symbol Added", $"{symbol} has been added to your watchlist.", InfoBarSeverity.Success);
            }
            else
            {
                ShowInfoBar("Already Exists", $"{symbol} is already in your watchlist.", InfoBarSeverity.Warning);
            }
        }
    }

    private async void RemoveSymbol_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string symbol)
        {
            var removed = await _watchlistService.RemoveSymbolAsync(symbol);
            if (removed)
            {
                ShowInfoBar("Symbol Removed", $"{symbol} has been removed from your watchlist.", InfoBarSeverity.Informational);
            }
        }
    }

    private async void ToggleFavorite_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string symbol)
        {
            await _watchlistService.ToggleFavoriteAsync(symbol);
        }
    }

    private void ViewDetails_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string symbol)
        {
            // Navigate to symbol details or storage page
            Frame.Navigate(typeof(SymbolStoragePage), symbol);
        }
    }

    private void Symbol_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is WatchlistDisplayItem item)
        {
            Frame.Navigate(typeof(SymbolStoragePage), item.Symbol);
        }
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        await LoadWatchlistAsync();
        ShowInfoBar("Refreshed", "Watchlist has been refreshed.", InfoBarSeverity.Success);
    }

    private void Sort_Click(object sender, RoutedEventArgs e)
    {
        // Show sort options flyout
        var flyout = new MenuFlyout();

        var sortByName = new MenuFlyoutItem { Text = "Sort by Name" };
        sortByName.Click += async (s, args) =>
        {
            var data = await _watchlistService.LoadWatchlistAsync();
            var order = data.Symbols.OrderBy(s => s.Symbol).Select(s => s.Symbol).ToList();
            await _watchlistService.ReorderAsync(order);
        };

        var sortByFavorite = new MenuFlyoutItem { Text = "Sort by Favorite" };
        sortByFavorite.Click += async (s, args) =>
        {
            var data = await _watchlistService.LoadWatchlistAsync();
            var order = data.Symbols.OrderByDescending(s => s.IsFavorite).ThenBy(s => s.Symbol).Select(s => s.Symbol).ToList();
            await _watchlistService.ReorderAsync(order);
        };

        var sortByAdded = new MenuFlyoutItem { Text = "Sort by Date Added" };
        sortByAdded.Click += async (s, args) =>
        {
            var data = await _watchlistService.LoadWatchlistAsync();
            var order = data.Symbols.OrderBy(s => s.AddedAt).Select(s => s.Symbol).ToList();
            await _watchlistService.ReorderAsync(order);
        };

        flyout.Items.Add(sortByName);
        flyout.Items.Add(sortByFavorite);
        flyout.Items.Add(sortByAdded);

        flyout.ShowAt(SortButton);
    }

    private void ViewMode_Click(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton radio && radio.Tag is string mode)
        {
            if (mode == "List")
            {
                AllSymbolsList.Visibility = Visibility.Visible;
                AllSymbolsGrid.Visibility = Visibility.Collapsed;
            }
            else
            {
                AllSymbolsList.Visibility = Visibility.Collapsed;
                AllSymbolsGrid.Visibility = Visibility.Visible;
            }
        }
    }

    private void ShowInfoBar(string title, string message, InfoBarSeverity severity)
    {
        WatchlistInfoBar.Title = title;
        WatchlistInfoBar.Message = message;
        WatchlistInfoBar.Severity = severity;
        WatchlistInfoBar.IsOpen = true;

        // Auto-close after 3 seconds
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        timer.Tick += (s, e) =>
        {
            WatchlistInfoBar.IsOpen = false;
            timer.Stop();
        };
        timer.Start();
    }
}

/// <summary>
/// Display model for watchlist items in the UI.
/// </summary>
public class WatchlistDisplayItem : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
{
    private string _symbol = string.Empty;
    private string _notes = string.Empty;
    private bool _isFavorite;
    private string _favoriteIcon = "\uE734";
    private SolidColorBrush _favoriteColor = new(Microsoft.UI.Colors.Gray);
    private bool _isStreaming;
    private string _statusText = "Idle";
    private SolidColorBrush _statusColor = new(Microsoft.UI.Colors.Gray);
    private string _eventRateText = "0";
    private long _eventCount;
    private double _healthScore = 100;
    private string _healthText = "100%";
    private SolidColorBrush _healthColor = new(Microsoft.UI.Colors.LimeGreen);
    private string _healthIcon = "\uE73E";
    private string _lastEventText = "No data";
    private DateTime _addedAt;

    public string Symbol
    {
        get => _symbol;
        set => SetProperty(ref _symbol, value);
    }

    public string Notes
    {
        get => _notes;
        set => SetProperty(ref _notes, value);
    }

    public bool IsFavorite
    {
        get => _isFavorite;
        set => SetProperty(ref _isFavorite, value);
    }

    public string FavoriteIcon
    {
        get => _favoriteIcon;
        set => SetProperty(ref _favoriteIcon, value);
    }

    public SolidColorBrush FavoriteColor
    {
        get => _favoriteColor;
        set => SetProperty(ref _favoriteColor, value);
    }

    public bool IsStreaming
    {
        get => _isStreaming;
        set => SetProperty(ref _isStreaming, value);
    }

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public SolidColorBrush StatusColor
    {
        get => _statusColor;
        set => SetProperty(ref _statusColor, value);
    }

    public string EventRateText
    {
        get => _eventRateText;
        set => SetProperty(ref _eventRateText, value);
    }

    public long EventCount
    {
        get => _eventCount;
        set => SetProperty(ref _eventCount, value);
    }

    public double HealthScore
    {
        get => _healthScore;
        set => SetProperty(ref _healthScore, value);
    }

    public string HealthText
    {
        get => _healthText;
        set => SetProperty(ref _healthText, value);
    }

    public SolidColorBrush HealthColor
    {
        get => _healthColor;
        set => SetProperty(ref _healthColor, value);
    }

    public string HealthIcon
    {
        get => _healthIcon;
        set => SetProperty(ref _healthIcon, value);
    }

    public string LastEventText
    {
        get => _lastEventText;
        set => SetProperty(ref _lastEventText, value);
    }

    public DateTime AddedAt
    {
        get => _addedAt;
        set => SetProperty(ref _addedAt, value);
    }
}
