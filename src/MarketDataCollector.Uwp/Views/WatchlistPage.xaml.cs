using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using MarketDataCollector.Uwp.Services;
using Windows.ApplicationModel.DataTransfer;

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

    // Extended list of popular symbols for autocomplete (when SearchService is unavailable)
    private static readonly string[] PopularSymbols =
    [
        // Mega-cap tech
        "AAPL", "MSFT", "GOOGL", "GOOG", "AMZN", "NVDA", "META", "TSLA",
        // Large-cap tech
        "AVGO", "ADBE", "CRM", "ORCL", "CSCO", "ACN", "IBM", "INTC", "AMD", "QCOM",
        // Financials
        "BRK.B", "BRK.A", "JPM", "V", "MA", "BAC", "WFC", "GS", "MS", "AXP",
        // Healthcare
        "UNH", "JNJ", "LLY", "PFE", "MRK", "ABBV", "TMO", "ABT", "DHR", "BMY",
        // Consumer
        "WMT", "PG", "KO", "PEP", "COST", "HD", "MCD", "NKE", "SBUX", "TGT",
        // Energy
        "XOM", "CVX", "COP", "SLB", "EOG", "MPC", "PSX", "VLO", "OXY", "HAL",
        // Industrials
        "CAT", "DE", "UNP", "HON", "GE", "BA", "RTX", "LMT", "UPS", "FDX",
        // ETFs
        "SPY", "QQQ", "IWM", "DIA", "VTI", "VOO", "VEA", "VWO", "BND", "GLD",
        // Sector ETFs
        "XLF", "XLE", "XLK", "XLV", "XLI", "XLY", "XLP", "XLU", "XLB", "XLRE"
    ];

    // Debounce configuration for autocomplete
    private const int DebounceDelayMs = 150;
    private CancellationTokenSource? _searchCts;
    private readonly DispatcherTimer _debounceTimer;
    private string _pendingSearchText = string.Empty;
    private AutoSuggestBox? _pendingSearchBox;

    private readonly WatchlistService _watchlistService;
    private readonly SearchService _searchService;
    private readonly ContextMenuService _contextMenuService;
    private readonly ObservableCollection<WatchlistDisplayItem> _favorites;
    private readonly ObservableCollection<WatchlistDisplayItem> _allSymbols;
    private readonly DispatcherTimer _refreshTimer;

    public WatchlistPage()
    {
        this.InitializeComponent();
        _watchlistService = WatchlistService.Instance;
        _searchService = SearchService.Instance;
        _contextMenuService = ContextMenuService.Instance;
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

        // Initialize debounce timer for autocomplete
        _debounceTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(DebounceDelayMs)
        };
        _debounceTimer.Tick += DebounceTimer_Tick;

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
        _debounceTimer.Stop();
        _searchCts?.Cancel();
        _searchCts?.Dispose();
        _searchCts = null;
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
        // Stop any pending searches
        _debounceTimer.Stop();
        _searchCts?.Cancel();

        string? symbolToAdd = null;

        if (args.ChosenSuggestion is SymbolSuggestion suggestion)
        {
            // User selected a SymbolSuggestion from the dropdown
            symbolToAdd = suggestion.Symbol.ToUpperInvariant();
        }
        else if (args.ChosenSuggestion is string strSuggestion)
        {
            // Fallback for string suggestions (backwards compatibility)
            if (strSuggestion.StartsWith("+ Add \"", StringComparison.OrdinalIgnoreCase))
            {
                symbolToAdd = strSuggestion.Replace("+ Add \"", "").Replace("\"", "").Trim().ToUpperInvariant();
            }
            else
            {
                symbolToAdd = strSuggestion.ToUpperInvariant();
            }
        }
        else if (!string.IsNullOrWhiteSpace(args.QueryText))
        {
            // User pressed Enter without selecting a suggestion
            var query = args.QueryText.Trim().ToUpperInvariant();

            // Validate symbol format before adding
            if (!IsValidSymbolFormat(query))
            {
                ShowInfoBar("Invalid Symbol", "Please enter a valid symbol (letters, numbers, dots, or hyphens).", InfoBarSeverity.Warning);
                return;
            }

            symbolToAdd = query;
        }

        if (!string.IsNullOrWhiteSpace(symbolToAdd))
        {
            AddSymbolBox.Text = symbolToAdd;
            await AddSymbolFromInput();
        }
    }

    private void AddSymbol_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput)
            return;

        var text = sender.Text?.Trim() ?? string.Empty;

        if (string.IsNullOrEmpty(text))
        {
            _debounceTimer.Stop();
            sender.ItemsSource = null;
            return;
        }

        // Store pending search info and restart debounce timer
        _pendingSearchText = text;
        _pendingSearchBox = sender;
        _debounceTimer.Stop();
        _debounceTimer.Start();
    }

    private async void DebounceTimer_Tick(object? sender, object e)
    {
        _debounceTimer.Stop();

        if (_pendingSearchBox == null || string.IsNullOrEmpty(_pendingSearchText))
            return;

        var searchBox = _pendingSearchBox;
        var searchText = _pendingSearchText.ToUpperInvariant();

        // Cancel any previous search operation
        _searchCts?.Cancel();
        _searchCts?.Dispose();
        _searchCts = new CancellationTokenSource();
        var ct = _searchCts.Token;

        try
        {
            var suggestions = await BuildSymbolSuggestionsAsync(searchText, ct);

            if (ct.IsCancellationRequested)
                return;

            searchBox.ItemsSource = suggestions.Count > 0 ? suggestions : null;
        }
        catch (OperationCanceledException)
        {
            // Search was cancelled, ignore
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WatchlistPage] Error fetching suggestions: {ex.Message}");
            // Fall back to simple local search on error
            searchBox.ItemsSource = BuildLocalSuggestions(searchText);
        }
    }

    /// <summary>
    /// Builds comprehensive symbol suggestions using SearchService and local data.
    /// </summary>
    private async Task<List<SymbolSuggestion>> BuildSymbolSuggestionsAsync(string searchText, CancellationToken ct)
    {
        var suggestions = new List<SymbolSuggestion>();
        var addedSymbols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 1. First check current watchlist for matches (highest priority - already tracked)
        foreach (var item in _allSymbols.Where(s =>
            s.Symbol.StartsWith(searchText, StringComparison.OrdinalIgnoreCase)))
        {
            if (addedSymbols.Add(item.Symbol))
            {
                suggestions.Add(new SymbolSuggestion(
                    item.Symbol,
                    "In Watchlist",
                    item.IsFavorite ? "\uE735" : "\uE728"));
            }
        }

        // 2. Use SearchService for comprehensive search (config, watchlist, popular)
        try
        {
            var searchSuggestions = await _searchService.GetSuggestionsAsync(searchText);
            ct.ThrowIfCancellationRequested();

            foreach (var suggestion in searchSuggestions
                .Where(s => s.Category is "Symbol" or "Watchlist" or "Popular Symbol"))
            {
                if (addedSymbols.Add(suggestion.Text))
                {
                    suggestions.Add(new SymbolSuggestion(
                        suggestion.Text,
                        suggestion.Category,
                        suggestion.Icon));
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // SearchService failed, continue with local fallback
        }

        // 3. Add matching symbols from extended popular list (fallback/supplement)
        foreach (var symbol in PopularSymbols
            .Where(s => s.StartsWith(searchText, StringComparison.OrdinalIgnoreCase))
            .Take(10))
        {
            if (addedSymbols.Add(symbol))
            {
                suggestions.Add(new SymbolSuggestion(symbol, "Popular", "\uE8D6"));
            }
        }

        // 4. Also include symbols that contain (but don't start with) the text
        foreach (var symbol in PopularSymbols
            .Where(s => !s.StartsWith(searchText, StringComparison.OrdinalIgnoreCase)
                        && s.Contains(searchText, StringComparison.OrdinalIgnoreCase))
            .Take(5))
        {
            if (addedSymbols.Add(symbol))
            {
                suggestions.Add(new SymbolSuggestion(symbol, "Related", "\uE721"));
            }
        }

        // 5. If the typed text looks like a valid symbol and isn't in suggestions, offer to add it
        if (IsValidSymbolFormat(searchText) && !addedSymbols.Contains(searchText))
        {
            suggestions.Add(new SymbolSuggestion(
                searchText,
                $"+ Add \"{searchText}\"",
                "\uE710",
                isAddNew: true));
        }

        // Limit total suggestions
        return suggestions.Take(12).ToList();
    }

    /// <summary>
    /// Builds simple local suggestions when SearchService is unavailable.
    /// </summary>
    private List<SymbolSuggestion> BuildLocalSuggestions(string searchText)
    {
        var suggestions = new List<SymbolSuggestion>();
        var addedSymbols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Check watchlist
        foreach (var item in _allSymbols
            .Where(s => s.Symbol.StartsWith(searchText, StringComparison.OrdinalIgnoreCase))
            .Take(5))
        {
            if (addedSymbols.Add(item.Symbol))
            {
                suggestions.Add(new SymbolSuggestion(item.Symbol, "In Watchlist", "\uE728"));
            }
        }

        // Popular symbols
        foreach (var symbol in PopularSymbols
            .Where(s => s.StartsWith(searchText, StringComparison.OrdinalIgnoreCase))
            .Take(8))
        {
            if (addedSymbols.Add(symbol))
            {
                suggestions.Add(new SymbolSuggestion(symbol, "Popular", "\uE8D6"));
            }
        }

        // Add new option
        if (IsValidSymbolFormat(searchText) && !addedSymbols.Contains(searchText))
        {
            suggestions.Add(new SymbolSuggestion(
                searchText,
                $"+ Add \"{searchText}\"",
                "\uE710",
                isAddNew: true));
        }

        return suggestions;
    }

    /// <summary>
    /// Validates that the input looks like a valid stock symbol.
    /// </summary>
    private static bool IsValidSymbolFormat(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length < 1 || text.Length > 10)
            return false;

        // Must start with a letter, can contain letters, numbers, dots, and hyphens
        if (!char.IsLetter(text[0]))
            return false;

        foreach (var c in text)
        {
            if (!char.IsLetterOrDigit(c) && c != '.' && c != '-')
                return false;
        }

        return true;
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

    #region Context Menu

    private void SymbolItem_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        string? symbol = null;
        bool isFavorite = false;

        // Try to get symbol from the element's Tag or DataContext
        if (sender is FrameworkElement element)
        {
            if (element.Tag is string tagSymbol)
            {
                symbol = tagSymbol;
            }
            else if (element.DataContext is WatchlistDisplayItem item)
            {
                symbol = item.Symbol;
                isFavorite = item.IsFavorite;
            }
        }

        if (string.IsNullOrEmpty(symbol)) return;

        // Find the display item to get favorite status
        var displayItem = _allSymbols.FirstOrDefault(s => s.Symbol == symbol);
        if (displayItem != null)
        {
            isFavorite = displayItem.IsFavorite;
        }

        var menu = _contextMenuService.CreateSymbolContextMenu(
            symbol,
            isFavorite,
            onToggleFavorite: async (s) =>
            {
                await _watchlistService.ToggleFavoriteAsync(s);
                ShowInfoBar("Updated", $"{s} favorite status changed.", InfoBarSeverity.Success);
            },
            onViewDetails: async (s) =>
            {
                await Task.CompletedTask;
                Frame.Navigate(typeof(SymbolStoragePage), s);
            },
            onViewLiveData: async (s) =>
            {
                await Task.CompletedTask;
                Frame.Navigate(typeof(LiveDataViewerPage), s);
            },
            onRunBackfill: async (s) =>
            {
                await Task.CompletedTask;
                Frame.Navigate(typeof(BackfillPage), s);
            },
            onCopySymbol: async (s) =>
            {
                await Task.CompletedTask;
                var dataPackage = new DataPackage();
                dataPackage.SetText(s);
                Clipboard.SetContent(dataPackage);
                ShowInfoBar("Copied", $"{s} copied to clipboard.", InfoBarSeverity.Success);
            },
            onRemove: async (s) =>
            {
                var removed = await _watchlistService.RemoveSymbolAsync(s);
                if (removed)
                {
                    ShowInfoBar("Removed", $"{s} removed from watchlist.", InfoBarSeverity.Informational);
                }
            },
            onAddNote: async (s) =>
            {
                await ShowAddNoteDialogAsync(s);
            });

        // Show the menu at the pointer position
        if (sender is UIElement uiElement)
        {
            menu.ShowAt(uiElement, e.GetPosition(uiElement));
        }

        e.Handled = true;
    }

    private async Task ShowAddNoteDialogAsync(string symbol)
    {
        var noteBox = new TextBox
        {
            PlaceholderText = "Enter a note for this symbol...",
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Height = 100
        };

        // Get existing note
        var item = _allSymbols.FirstOrDefault(s => s.Symbol == symbol);
        if (item != null)
        {
            noteBox.Text = item.Notes;
        }

        var dialog = new ContentDialog
        {
            Title = $"Note for {symbol}",
            Content = noteBox,
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            XamlRoot = this.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            await _watchlistService.UpdateNoteAsync(symbol, noteBox.Text);
            ShowInfoBar("Saved", "Note saved successfully.", InfoBarSeverity.Success);
        }
    }

    #endregion
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

/// <summary>
/// Represents a symbol suggestion in the autocomplete dropdown.
/// </summary>
public sealed class SymbolSuggestion
{
    /// <summary>
    /// The stock symbol (e.g., AAPL, MSFT).
    /// </summary>
    public string Symbol { get; }

    /// <summary>
    /// Category or description text (e.g., "In Watchlist", "Popular", "+ Add").
    /// </summary>
    public string Category { get; }

    /// <summary>
    /// Icon character for display (Segoe MDL2 Assets).
    /// </summary>
    public string Icon { get; }

    /// <summary>
    /// Whether this is an "Add new symbol" suggestion.
    /// </summary>
    public bool IsAddNew { get; }

    /// <summary>
    /// Display text shown in the dropdown.
    /// </summary>
    public string DisplayText => IsAddNew ? Category : Symbol;

    /// <summary>
    /// Secondary text shown below the symbol (category info).
    /// </summary>
    public string SecondaryText => IsAddNew ? string.Empty : Category;

    public SymbolSuggestion(string symbol, string category, string icon, bool isAddNew = false)
    {
        Symbol = symbol;
        Category = category;
        Icon = icon;
        IsAddNew = isAddNew;
    }

    /// <summary>
    /// Returns the display text for the AutoSuggestBox.
    /// </summary>
    public override string ToString() => DisplayText;
}
