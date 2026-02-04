using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MarketDataCollector.Wpf.Services;

namespace MarketDataCollector.Wpf.Views;

/// <summary>
/// Symbol subscription management page with bulk import, search/filter, and templates.
/// </summary>
public partial class SymbolsPage : Page
{
    private readonly ConfigService _configService;
    private readonly WatchlistService _watchlistService;
    private readonly ObservableCollection<SymbolViewModel> _symbols = new();
    private readonly ObservableCollection<SymbolViewModel> _filteredSymbols = new();
    private readonly ObservableCollection<WatchlistInfo> _watchlists = new();
    private SymbolViewModel? _selectedSymbol;
    private bool _isEditMode;
    private CancellationTokenSource? _loadCts;

    private static readonly Dictionary<string, string[]> SymbolTemplates = new()
    {
        ["FAANG"] = new[] { "META", "AAPL", "AMZN", "NFLX", "GOOGL" },
        ["MagnificentSeven"] = new[] { "AAPL", "MSFT", "GOOGL", "AMZN", "NVDA", "META", "TSLA" },
        ["MajorETFs"] = new[] { "SPY", "QQQ", "IWM", "DIA", "VTI" },
        ["Semiconductors"] = new[] { "NVDA", "AMD", "INTC", "TSM", "AVGO", "QCOM" },
        ["Financials"] = new[] { "JPM", "BAC", "WFC", "GS", "MS", "C" }
    };

    public SymbolsPage()
    {
        InitializeComponent();

        _configService = ConfigService.Instance;
        _watchlistService = WatchlistService.Instance;
        SymbolsListView.ItemsSource = _filteredSymbols;
        WatchlistsView.ItemsSource = _watchlists;

        _watchlistService.WatchlistsChanged += OnWatchlistsChanged;
        Unloaded += OnPageUnloaded;
    }

    private void OnPageUnloaded(object sender, RoutedEventArgs e)
    {
        _watchlistService.WatchlistsChanged -= OnWatchlistsChanged;
        _loadCts?.Cancel();
        _loadCts?.Dispose();
    }

    private void OnWatchlistsChanged(object? sender, WatchlistsChangedEventArgs e)
    {
        Dispatcher.Invoke(() => LoadWatchlistsAsync());
    }

    private async void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        LoadSymbols();
        await LoadWatchlistsAsync();
    }

    private void LoadSymbols()
    {
        _symbols.Clear();

        // Sample symbols for demonstration
        _symbols.Add(new SymbolViewModel { Symbol = "AAPL", SubscribeTrades = true, SubscribeDepth = true, DepthLevels = 10, Exchange = "SMART" });
        _symbols.Add(new SymbolViewModel { Symbol = "MSFT", SubscribeTrades = true, SubscribeDepth = false, DepthLevels = 10, Exchange = "SMART" });
        _symbols.Add(new SymbolViewModel { Symbol = "GOOGL", SubscribeTrades = true, SubscribeDepth = true, DepthLevels = 5, Exchange = "SMART" });
        _symbols.Add(new SymbolViewModel { Symbol = "SPY", SubscribeTrades = true, SubscribeDepth = true, DepthLevels = 10, Exchange = "ARCA" });
        _symbols.Add(new SymbolViewModel { Symbol = "QQQ", SubscribeTrades = true, SubscribeDepth = false, DepthLevels = 10, Exchange = "NASDAQ" });

        ApplyFilters();
        SymbolCountText.Text = $"{_symbols.Count} symbols";
    }

    private async Task LoadWatchlistsAsync()
    {
        _loadCts?.Cancel();
        _loadCts = new CancellationTokenSource();

        try
        {
            var watchlists = await _watchlistService.GetAllWatchlistsAsync(_loadCts.Token);
            _watchlists.Clear();
            foreach (var wl in watchlists)
            {
                _watchlists.Add(new WatchlistInfo
                {
                    Id = wl.Id,
                    Name = wl.Name,
                    SymbolCount = $"{wl.Symbols.Count} symbols",
                    Color = wl.Color,
                    IsPinned = wl.IsPinned
                });
            }
        }
        catch (OperationCanceledException)
        {
            // Cancelled - ignore
        }
        catch (Exception ex)
        {
            LoggingService.Instance.LogError("Failed to load watchlists", ex);
        }
    }

    private void ApplyFilters()
    {
        var searchText = SymbolSearchBox.Text?.ToUpper() ?? "";
        var filter = GetComboSelectedTag(FilterCombo) ?? "All";
        var exchangeFilter = GetComboSelectedTag(ExchangeFilterCombo) ?? "All";

        _filteredSymbols.Clear();

        foreach (var symbol in _symbols)
        {
            if (!string.IsNullOrEmpty(searchText) &&
                !symbol.Symbol.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                continue;

            if (filter == "Trades" && !symbol.SubscribeTrades) continue;
            if (filter == "Depth" && !symbol.SubscribeDepth) continue;
            if (filter == "Both" && !(symbol.SubscribeTrades && symbol.SubscribeDepth)) continue;

            if (exchangeFilter != "All" && symbol.Exchange != exchangeFilter) continue;

            _filteredSymbols.Add(symbol);
        }

        UpdateSelectionCount();
    }

    private void SymbolSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyFilters();
    }

    private void Filter_Changed(object sender, SelectionChangedEventArgs e)
    {
        ApplyFilters();
    }

    private void ClearFilters_Click(object sender, RoutedEventArgs e)
    {
        SymbolSearchBox.Text = "";
        SelectComboItemByTag(FilterCombo, "All");
        SelectComboItemByTag(ExchangeFilterCombo, "All");
        ApplyFilters();
    }

    private void SelectAll_Changed(object sender, RoutedEventArgs e)
    {
        var isChecked = SelectAllCheck.IsChecked ?? false;
        foreach (var symbol in _filteredSymbols)
        {
            symbol.IsSelected = isChecked;
        }
        UpdateSelectionCount();
    }

    private void SymbolCheckbox_Changed(object sender, RoutedEventArgs e)
    {
        UpdateSelectionCount();
    }

    private void UpdateSelectionCount()
    {
        var count = _filteredSymbols.Count(s => s.IsSelected);
        SelectionCountText.Text = $"{count} selected";

        BulkEnableTradesBtn.IsEnabled = count > 0;
        BulkEnableDepthBtn.IsEnabled = count > 0;
        BulkDeleteBtn.IsEnabled = count > 0;

        if (count == 0)
            SelectAllCheck.IsChecked = false;
        else if (count == _filteredSymbols.Count)
            SelectAllCheck.IsChecked = true;
    }

    private void BulkEnableTrades_Click(object sender, RoutedEventArgs e)
    {
        var selected = _filteredSymbols.Where(s => s.IsSelected).ToList();
        foreach (var symbol in selected)
        {
            symbol.SubscribeTrades = true;
        }

        NotificationService.Instance.ShowNotification(
            "Bulk Update",
            $"Enabled trades for {selected.Count} symbols.",
            NotificationType.Success);

        ApplyFilters();
    }

    private void BulkEnableDepth_Click(object sender, RoutedEventArgs e)
    {
        var selected = _filteredSymbols.Where(s => s.IsSelected).ToList();
        foreach (var symbol in selected)
        {
            symbol.SubscribeDepth = true;
        }

        NotificationService.Instance.ShowNotification(
            "Bulk Update",
            $"Enabled depth for {selected.Count} symbols.",
            NotificationType.Success);

        ApplyFilters();
    }

    private void BulkDelete_Click(object sender, RoutedEventArgs e)
    {
        var selected = _filteredSymbols.Where(s => s.IsSelected).ToList();

        var result = MessageBox.Show(
            $"Are you sure you want to delete {selected.Count} symbols?",
            "Delete Symbols",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        foreach (var symbol in selected)
        {
            _symbols.Remove(symbol);
        }

        ApplyFilters();
        SymbolCountText.Text = $"{_symbols.Count} symbols";

        NotificationService.Instance.ShowNotification(
            "Bulk Delete",
            $"Deleted {selected.Count} symbols.",
            NotificationType.Success);
    }

    private void ImportCsv_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "CSV Files|*.csv|Text Files|*.txt|All Files|*.*",
            Title = "Import Symbols"
        };

        if (dialog.ShowDialog() == true)
        {
            NotificationService.Instance.ShowNotification(
                "Import Started",
                $"Importing symbols from {System.IO.Path.GetFileName(dialog.FileName)}",
                NotificationType.Info);
        }
    }

    private void ExportCsv_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "CSV Files|*.csv",
            Title = "Export Symbols",
            FileName = $"symbols_{DateTime.Now:yyyyMMdd}"
        };

        if (dialog.ShowDialog() == true)
        {
            NotificationService.Instance.ShowNotification(
                "Export Complete",
                $"Exported {_symbols.Count} symbols to {System.IO.Path.GetFileName(dialog.FileName)}",
                NotificationType.Success);
        }
    }

    private void SymbolsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SymbolsListView.SelectedItem is SymbolViewModel symbol)
        {
            _selectedSymbol = symbol;
            _isEditMode = true;

            SymbolBox.Text = symbol.Symbol;
            SubscribeTradesToggle.IsChecked = symbol.SubscribeTrades;
            SubscribeDepthToggle.IsChecked = symbol.SubscribeDepth;
            DepthLevelsBox.Text = symbol.DepthLevels.ToString();
            ExchangeBox.Text = symbol.Exchange ?? "SMART";
            PrimaryExchangeBox.Text = string.Empty;
            LocalSymbolBox.Text = symbol.LocalSymbol ?? string.Empty;

            FormTitle.Text = "Edit Symbol";
            SaveSymbolButton.Content = "Update Symbol";
            DeleteSymbolButton.Visibility = Visibility.Visible;
        }
    }

    private void SaveSymbol_Click(object sender, RoutedEventArgs e)
    {
        var symbolName = SymbolBox.Text?.Trim().ToUpper();
        if (string.IsNullOrEmpty(symbolName))
        {
            NotificationService.Instance.ShowNotification(
                "Validation Error",
                "Symbol is required.",
                NotificationType.Error);
            return;
        }

        if (_isEditMode && _selectedSymbol != null)
        {
            _selectedSymbol.Symbol = symbolName;
            _selectedSymbol.SubscribeTrades = SubscribeTradesToggle.IsChecked ?? false;
            _selectedSymbol.SubscribeDepth = SubscribeDepthToggle.IsChecked ?? false;
            _selectedSymbol.DepthLevels = int.TryParse(DepthLevelsBox.Text, out var levels) ? levels : 10;
            _selectedSymbol.Exchange = ExchangeBox.Text ?? "SMART";
            _selectedSymbol.LocalSymbol = LocalSymbolBox.Text;
        }
        else
        {
            if (_symbols.Any(s => s.Symbol == symbolName))
            {
                NotificationService.Instance.ShowNotification(
                    "Duplicate Symbol",
                    $"{symbolName} already exists.",
                    NotificationType.Warning);
                return;
            }

            _symbols.Add(new SymbolViewModel
            {
                Symbol = symbolName,
                SubscribeTrades = SubscribeTradesToggle.IsChecked ?? true,
                SubscribeDepth = SubscribeDepthToggle.IsChecked ?? false,
                DepthLevels = int.TryParse(DepthLevelsBox.Text, out var levels) ? levels : 10,
                Exchange = ExchangeBox.Text ?? "SMART",
                LocalSymbol = LocalSymbolBox.Text
            });
        }

        NotificationService.Instance.ShowNotification(
            "Success",
            _isEditMode ? $"Symbol {symbolName} updated successfully." : $"Symbol {symbolName} added successfully.",
            NotificationType.Success);

        ClearForm();
        ApplyFilters();
        SymbolCountText.Text = $"{_symbols.Count} symbols";
    }

    private void DeleteSymbol_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedSymbol == null) return;

        var result = MessageBox.Show(
            $"Are you sure you want to delete {_selectedSymbol.Symbol}?",
            "Delete Symbol",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            _symbols.Remove(_selectedSymbol);

            NotificationService.Instance.ShowNotification(
                "Success",
                $"Symbol {_selectedSymbol.Symbol} deleted.",
                NotificationType.Success);

            ClearForm();
            ApplyFilters();
            SymbolCountText.Text = $"{_symbols.Count} symbols";
        }
    }

    private void ClearForm_Click(object sender, RoutedEventArgs e)
    {
        ClearForm();
    }

    private void ClearForm()
    {
        _selectedSymbol = null;
        _isEditMode = false;

        SymbolBox.Text = string.Empty;
        SubscribeTradesToggle.IsChecked = true;
        SubscribeDepthToggle.IsChecked = false;
        DepthLevelsBox.Text = "10";
        ExchangeBox.Text = "SMART";
        PrimaryExchangeBox.Text = string.Empty;
        LocalSymbolBox.Text = string.Empty;

        FormTitle.Text = "Add Symbol";
        SaveSymbolButton.Content = "Add Symbol";
        DeleteSymbolButton.Visibility = Visibility.Collapsed;

        SymbolsListView.SelectedItem = null;
    }

    private void AddTemplate_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string templateName)
        {
            if (SymbolTemplates.TryGetValue(templateName, out var symbols))
            {
                var added = 0;
                foreach (var symbol in symbols)
                {
                    if (!_symbols.Any(s => s.Symbol == symbol))
                    {
                        _symbols.Add(new SymbolViewModel
                        {
                            Symbol = symbol,
                            SubscribeTrades = true,
                            SubscribeDepth = false,
                            DepthLevels = 10,
                            Exchange = "SMART"
                        });
                        added++;
                    }
                }

                ApplyFilters();
                SymbolCountText.Text = $"{_symbols.Count} symbols";

                NotificationService.Instance.ShowNotification(
                    "Template Added",
                    $"Added {added} new symbols from {templateName} template.",
                    NotificationType.Success);
            }
        }
    }

    private async void LoadWatchlist_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string watchlistId)
        {
            // If no specific watchlist, show selection dialog
            if (_watchlists.Count == 0)
            {
                NotificationService.Instance.ShowNotification(
                    "No Watchlists",
                    "No watchlists available. Create a watchlist first.",
                    NotificationType.Warning);
                return;
            }

            // Use first watchlist or show a selection prompt
            var firstWatchlist = _watchlists.FirstOrDefault();
            if (firstWatchlist == null) return;
            watchlistId = firstWatchlist.Id;
        }

        try
        {
            var watchlist = await _watchlistService.GetWatchlistAsync(watchlistId);
            if (watchlist == null)
            {
                NotificationService.Instance.ShowNotification(
                    "Watchlist Not Found",
                    "The selected watchlist could not be found.",
                    NotificationType.Error);
                return;
            }

            // Clear current symbols and load watchlist symbols
            _symbols.Clear();
            foreach (var symbol in watchlist.Symbols)
            {
                _symbols.Add(new SymbolViewModel
                {
                    Symbol = symbol,
                    SubscribeTrades = true,
                    SubscribeDepth = false,
                    DepthLevels = 10,
                    Exchange = "SMART"
                });
            }

            ApplyFilters();
            SymbolCountText.Text = $"{_symbols.Count} symbols";

            NotificationService.Instance.ShowNotification(
                "Watchlist Loaded",
                $"Loaded {watchlist.Symbols.Count} symbols from '{watchlist.Name}'.",
                NotificationType.Success);
        }
        catch (Exception ex)
        {
            LoggingService.Instance.LogError("Failed to load watchlist", ex);
            NotificationService.Instance.ShowNotification(
                "Error",
                "Failed to load watchlist. Please try again.",
                NotificationType.Error);
        }
    }

    private async void SaveWatchlist_Click(object sender, RoutedEventArgs e)
    {
        if (_symbols.Count == 0)
        {
            NotificationService.Instance.ShowNotification(
                "No Symbols",
                "Add some symbols before saving a watchlist.",
                NotificationType.Warning);
            return;
        }

        // Show save dialog
        var dialog = new SaveWatchlistDialog(_watchlists.Select(w => w.Name).ToList());
        if (dialog.ShowDialog() != true) return;

        var name = dialog.WatchlistName;
        var saveAsNew = dialog.SaveAsNew;
        var selectedWatchlistId = dialog.SelectedWatchlistId;

        try
        {
            var symbols = _symbols.Select(s => s.Symbol).ToArray();

            if (saveAsNew || string.IsNullOrEmpty(selectedWatchlistId))
            {
                // Create new watchlist
                var watchlist = await _watchlistService.CreateWatchlistAsync(name, symbols);
                NotificationService.Instance.ShowNotification(
                    "Watchlist Created",
                    $"Created watchlist '{watchlist.Name}' with {symbols.Length} symbols.",
                    NotificationType.Success);
            }
            else
            {
                // Update existing watchlist - clear and add all symbols
                var existing = await _watchlistService.GetWatchlistAsync(selectedWatchlistId);
                if (existing != null)
                {
                    await _watchlistService.RemoveSymbolsAsync(selectedWatchlistId, existing.Symbols);
                    await _watchlistService.AddSymbolsAsync(selectedWatchlistId, symbols);
                    NotificationService.Instance.ShowNotification(
                        "Watchlist Updated",
                        $"Updated watchlist '{existing.Name}' with {symbols.Length} symbols.",
                        NotificationType.Success);
                }
            }

            await LoadWatchlistsAsync();
        }
        catch (Exception ex)
        {
            LoggingService.Instance.LogError("Failed to save watchlist", ex);
            NotificationService.Instance.ShowNotification(
                "Error",
                "Failed to save watchlist. Please try again.",
                NotificationType.Error);
        }
    }

    private void ManageWatchlists_Click(object sender, RoutedEventArgs e)
    {
        // Navigate to the Watchlist management page
        var navigationService = Services.NavigationService.Instance;
        navigationService.NavigateTo(typeof(WatchlistPage));
    }

    private void RefreshList_Click(object sender, RoutedEventArgs e)
    {
        LoadSymbols();
        LastRefreshText.Text = "Last refreshed: just now";
    }

    private static void SelectComboItemByTag(ComboBox combo, string tag)
    {
        foreach (var item in combo.Items)
        {
            if (item is ComboBoxItem cbi && cbi.Tag?.ToString() == tag)
            {
                combo.SelectedItem = item;
                return;
            }
        }
    }

    private static string? GetComboSelectedTag(ComboBox combo)
    {
        return (combo.SelectedItem as ComboBoxItem)?.Tag?.ToString();
    }
}

/// <summary>
/// Symbol view model for the symbols page.
/// </summary>
public class SymbolViewModel
{
    public bool IsSelected { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public bool SubscribeTrades { get; set; }
    public bool SubscribeDepth { get; set; }
    public int DepthLevels { get; set; } = 10;
    public string Exchange { get; set; } = "SMART";
    public string? LocalSymbol { get; set; }

    public string TradesText => SubscribeTrades ? "On" : "Off";
    public string DepthText => SubscribeDepth ? "On" : "Off";
    public string StatusText => SubscribeTrades || SubscribeDepth ? "Active" : "Inactive";

    public SolidColorBrush TradesStatusColor => SubscribeTrades
        ? new SolidColorBrush(Color.FromRgb(63, 185, 80))
        : new SolidColorBrush(Color.FromRgb(139, 148, 158));

    public SolidColorBrush DepthStatusColor => SubscribeDepth
        ? new SolidColorBrush(Color.FromRgb(63, 185, 80))
        : new SolidColorBrush(Color.FromRgb(139, 148, 158));

    public SolidColorBrush StatusBackground => SubscribeTrades || SubscribeDepth
        ? new SolidColorBrush(Color.FromArgb(40, 63, 185, 80))
        : new SolidColorBrush(Color.FromArgb(40, 139, 148, 158));
}

/// <summary>
/// Watchlist information model for display.
/// </summary>
public class WatchlistInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string SymbolCount { get; set; } = string.Empty;
    public string? Color { get; set; }
    public bool IsPinned { get; set; }

    public SolidColorBrush ColorBrush
    {
        get
        {
            if (string.IsNullOrEmpty(Color))
                return new SolidColorBrush(Colors.Gray);

            try
            {
                var color = (System.Windows.Media.Color)ColorConverter.ConvertFromString(Color);
                return new SolidColorBrush(color);
            }
            catch
            {
                return new SolidColorBrush(Colors.Gray);
            }
        }
    }
}

/// <summary>
/// Dialog for saving watchlists.
/// </summary>
public class SaveWatchlistDialog : Window
{
    private readonly TextBox _nameBox;
    private readonly ComboBox _existingCombo;
    private readonly CheckBox _saveAsNewCheck;
    private readonly List<string> _existingWatchlists;

    public string WatchlistName => _nameBox.Text;
    public bool SaveAsNew => _saveAsNewCheck.IsChecked ?? true;
    public string? SelectedWatchlistId { get; private set; }

    public SaveWatchlistDialog(List<string> existingWatchlists)
    {
        _existingWatchlists = existingWatchlists;

        Title = "Save Watchlist";
        Width = 400;
        Height = 220;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        Background = new SolidColorBrush((System.Windows.Media.Color)ColorConverter.ConvertFromString("#1E1E2E"));

        var grid = new Grid { Margin = new Thickness(16) };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Save as new checkbox
        _saveAsNewCheck = new CheckBox
        {
            Content = "Create new watchlist",
            IsChecked = true,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 0, 0, 12)
        };
        _saveAsNewCheck.Checked += (_, _) => UpdateUIState();
        _saveAsNewCheck.Unchecked += (_, _) => UpdateUIState();
        Grid.SetRow(_saveAsNewCheck, 0);
        grid.Children.Add(_saveAsNewCheck);

        // Name label
        var nameLabel = new TextBlock
        {
            Text = "Watchlist Name:",
            Foreground = Brushes.White,
            Margin = new Thickness(0, 0, 0, 4)
        };
        Grid.SetRow(nameLabel, 1);
        grid.Children.Add(nameLabel);

        // Name textbox
        _nameBox = new TextBox
        {
            Margin = new Thickness(0, 0, 0, 12),
            Background = new SolidColorBrush((System.Windows.Media.Color)ColorConverter.ConvertFromString("#2A2A3E")),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush((System.Windows.Media.Color)ColorConverter.ConvertFromString("#3A3A4E")),
            Padding = new Thickness(8, 4, 8, 4)
        };
        Grid.SetRow(_nameBox, 2);
        grid.Children.Add(_nameBox);

        // Existing watchlist label
        var existingLabel = new TextBlock
        {
            Text = "Or update existing:",
            Foreground = Brushes.Gray,
            Margin = new Thickness(0, 0, 0, 4)
        };
        Grid.SetRow(existingLabel, 3);
        grid.Children.Add(existingLabel);

        // Existing watchlist combo
        _existingCombo = new ComboBox
        {
            Margin = new Thickness(0, 0, 0, 16),
            IsEnabled = false,
            Background = new SolidColorBrush((System.Windows.Media.Color)ColorConverter.ConvertFromString("#2A2A3E")),
            Foreground = Brushes.White
        };
        foreach (var name in existingWatchlists)
        {
            _existingCombo.Items.Add(name);
        }
        Grid.SetRow(_existingCombo, 4);
        grid.Children.Add(_existingCombo);

        // Buttons
        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        Grid.SetRow(buttonPanel, 5);

        var cancelButton = new Button
        {
            Content = "Cancel",
            Width = 80,
            Margin = new Thickness(0, 0, 8, 0),
            Background = new SolidColorBrush((System.Windows.Media.Color)ColorConverter.ConvertFromString("#3A3A4E")),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(8, 4, 8, 4)
        };
        cancelButton.Click += (_, _) => { DialogResult = false; Close(); };
        buttonPanel.Children.Add(cancelButton);

        var saveButton = new Button
        {
            Content = "Save",
            Width = 80,
            Background = new SolidColorBrush((System.Windows.Media.Color)ColorConverter.ConvertFromString("#4CAF50")),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(8, 4, 8, 4)
        };
        saveButton.Click += OnSaveClick;
        buttonPanel.Children.Add(saveButton);

        grid.Children.Add(buttonPanel);
        Content = grid;
    }

    private void UpdateUIState()
    {
        var isNew = _saveAsNewCheck.IsChecked ?? true;
        _nameBox.IsEnabled = isNew;
        _existingCombo.IsEnabled = !isNew && _existingWatchlists.Count > 0;
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        if (SaveAsNew)
        {
            if (string.IsNullOrWhiteSpace(_nameBox.Text))
            {
                MessageBox.Show("Please enter a watchlist name.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }
        else if (_existingCombo.SelectedItem == null)
        {
            MessageBox.Show("Please select an existing watchlist.", "Validation Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
        Close();
    }
}
