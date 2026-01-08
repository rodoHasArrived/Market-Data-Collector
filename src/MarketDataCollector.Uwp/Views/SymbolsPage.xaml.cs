using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using MarketDataCollector.Uwp.Models;
using MarketDataCollector.Uwp.Services;
using MarketDataCollector.Uwp.ViewModels;
using Windows.Storage.Pickers;
using Windows.UI;

namespace MarketDataCollector.Uwp.Views;

/// <summary>
/// Enhanced page for managing symbol subscriptions with bulk import,
/// search/filter, and subscription templates.
/// </summary>
public sealed partial class SymbolsPage : Page
{
    private readonly ConfigService _configService;
    private readonly ObservableCollection<EnhancedSymbolViewModel> _symbols = new();
    private readonly ObservableCollection<EnhancedSymbolViewModel> _filteredSymbols = new();
    private readonly List<WatchlistInfo> _watchlists = new();
    private EnhancedSymbolViewModel? _selectedSymbol;
    private bool _isEditMode;
    private DateTime _lastRefresh = DateTime.UtcNow;

    // Symbol templates
    private static readonly Dictionary<string, string[]> SymbolTemplates = new()
    {
        ["FAANG"] = new[] { "META", "AAPL", "AMZN", "NFLX", "GOOGL" },
        ["MagnificentSeven"] = new[] { "AAPL", "MSFT", "GOOGL", "AMZN", "NVDA", "META", "TSLA" },
        ["SPY_QQQ_IWM"] = new[] { "SPY", "QQQ", "IWM", "DIA", "VTI" },
        ["Semiconductors"] = new[] { "NVDA", "AMD", "INTC", "TSM", "AVGO", "QCOM" },
        ["Financials"] = new[] { "JPM", "BAC", "WFC", "GS", "MS", "C" }
    };

    // Popular symbols for autocomplete
    private static readonly string[] PopularSymbols = new[]
    {
        "AAPL", "MSFT", "GOOGL", "AMZN", "NVDA", "META", "TSLA", "BRK.B", "JPM", "V",
        "UNH", "XOM", "JNJ", "WMT", "MA", "PG", "HD", "CVX", "MRK", "ABBV",
        "LLY", "PFE", "KO", "PEP", "BAC", "COST", "AVGO", "TMO", "MCD", "CSCO",
        "ACN", "ABT", "DHR", "ADBE", "CRM", "NKE", "CMCSA", "TXN", "NEE", "VZ",
        "SPY", "QQQ", "IWM", "DIA", "VTI", "VOO", "XLF", "XLE", "XLK", "XLV"
    };

    public SymbolsPage()
    {
        this.InitializeComponent();
        _configService = new ConfigService();
        SymbolsListView.ItemsSource = _filteredSymbols;

        Loaded += SymbolsPage_Loaded;
    }

    private async void SymbolsPage_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadSymbolsAsync();
        LoadWatchlists();
    }

    private async Task LoadSymbolsAsync()
    {
        RefreshProgress.IsActive = true;
        try
        {
            var config = await _configService.LoadConfigAsync();
            _symbols.Clear();

            if (config?.Symbols != null)
            {
                foreach (var symbol in config.Symbols)
                {
                    _symbols.Add(new EnhancedSymbolViewModel(symbol));
                }
            }

            ApplyFilters();
            SymbolCountText.Text = $"{_symbols.Count} symbols";
            _lastRefresh = DateTime.UtcNow;
            LastRefreshText.Text = "Last refreshed: just now";
        }
        finally
        {
            RefreshProgress.IsActive = false;
        }
    }

    private void LoadWatchlists()
    {
        // Sample watchlists - in real implementation, would load from storage
        _watchlists.Clear();
        _watchlists.Add(new WatchlistInfo { Name = "My Tech Picks", SymbolCount = "8 symbols" });
        _watchlists.Add(new WatchlistInfo { Name = "Dividend Stocks", SymbolCount = "12 symbols" });
        _watchlists.Add(new WatchlistInfo { Name = "Day Trading", SymbolCount = "5 symbols" });

        WatchlistsView.ItemsSource = _watchlists;
    }

    private void ApplyFilters()
    {
        var searchText = SymbolSearchBox.Text?.ToUpper() ?? "";
        var filter = GetComboSelectedTag(FilterCombo) ?? "All";
        var exchangeFilter = GetComboSelectedTag(ExchangeFilterCombo) ?? "All";

        _filteredSymbols.Clear();

        foreach (var symbol in _symbols)
        {
            // Text search
            if (!string.IsNullOrEmpty(searchText) &&
                !symbol.Symbol.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                continue;

            // Subscription filter
            if (filter == "Trades" && !symbol.SubscribeTrades) continue;
            if (filter == "Depth" && !symbol.SubscribeDepth) continue;
            if (filter == "Both" && !(symbol.SubscribeTrades && symbol.SubscribeDepth)) continue;

            // Exchange filter
            if (exchangeFilter != "All" && symbol.Exchange != exchangeFilter) continue;

            _filteredSymbols.Add(symbol);
        }

        UpdateSelectionCount();
    }

    private void SymbolSearch_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
        {
            var suggestions = new List<string>();
            var text = sender.Text?.ToUpper() ?? "";

            if (!string.IsNullOrEmpty(text))
            {
                // Add matching popular symbols
                suggestions.AddRange(PopularSymbols.Where(s => s.StartsWith(text)).Take(10));

                // Add "Add new" option if not found
                if (!_symbols.Any(s => s.Symbol.Equals(text, StringComparison.OrdinalIgnoreCase)))
                {
                    suggestions.Add($"+ Add \"{text}\"");
                }
            }

            sender.ItemsSource = suggestions;
        }

        ApplyFilters();
    }

    private void SymbolSearch_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        var query = args.QueryText?.Trim().ToUpper() ?? "";

        if (query.StartsWith("+ ADD"))
        {
            // Extract symbol from "Add new" option
            var symbol = query.Replace("+ ADD \"", "").Replace("\"", "").Trim();
            SymbolBox.Text = symbol;
        }
        else if (!string.IsNullOrEmpty(query))
        {
            ApplyFilters();
        }
    }

    private void SymbolSearch_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        var selected = args.SelectedItem?.ToString() ?? "";

        if (selected.StartsWith("+ Add"))
        {
            var symbol = selected.Replace("+ Add \"", "").Replace("\"", "").Trim();
            SymbolBox.Text = symbol;
            sender.Text = "";
        }
        else
        {
            sender.Text = selected;
            ApplyFilters();
        }
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

        // Update SelectAll checkbox state
        if (count == 0)
            SelectAllCheck.IsChecked = false;
        else if (count == _filteredSymbols.Count)
            SelectAllCheck.IsChecked = true;
    }

    private async void BulkEnableTrades_Click(object sender, RoutedEventArgs e)
    {
        var selected = _filteredSymbols.Where(s => s.IsSelected).ToList();
        foreach (var symbol in selected)
        {
            symbol.SubscribeTrades = true;
        }

        await SaveAllSymbolsAsync();

        FormInfoBar.Severity = InfoBarSeverity.Success;
        FormInfoBar.Title = "Bulk Update";
        FormInfoBar.Message = $"Enabled trades for {selected.Count} symbols.";
        FormInfoBar.IsOpen = true;
    }

    private async void BulkEnableDepth_Click(object sender, RoutedEventArgs e)
    {
        var selected = _filteredSymbols.Where(s => s.IsSelected).ToList();
        foreach (var symbol in selected)
        {
            symbol.SubscribeDepth = true;
        }

        await SaveAllSymbolsAsync();

        FormInfoBar.Severity = InfoBarSeverity.Success;
        FormInfoBar.Title = "Bulk Update";
        FormInfoBar.Message = $"Enabled depth for {selected.Count} symbols.";
        FormInfoBar.IsOpen = true;
    }

    private async void BulkDelete_Click(object sender, RoutedEventArgs e)
    {
        var selected = _filteredSymbols.Where(s => s.IsSelected).ToList();

        var dialog = new ContentDialog
        {
            Title = "Delete Symbols",
            Content = $"Are you sure you want to delete {selected.Count} symbols?",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return;

        foreach (var symbol in selected)
        {
            _symbols.Remove(symbol);
            await _configService.DeleteSymbolAsync(symbol.Symbol);
        }

        ApplyFilters();
        SymbolCountText.Text = $"{_symbols.Count} symbols";

        FormInfoBar.Severity = InfoBarSeverity.Success;
        FormInfoBar.Title = "Bulk Delete";
        FormInfoBar.Message = $"Deleted {selected.Count} symbols.";
        FormInfoBar.IsOpen = true;
    }

    private async Task SaveAllSymbolsAsync()
    {
        foreach (var symbol in _symbols)
        {
            var config = symbol.ToSymbolConfig();
            await _configService.AddOrUpdateSymbolAsync(config);
        }
    }

    private async void ImportCsv_Click(object sender, RoutedEventArgs e)
    {
        var filePicker = new FileOpenPicker();
        filePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        filePicker.FileTypeFilter.Add(".csv");
        filePicker.FileTypeFilter.Add(".txt");

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(filePicker, hwnd);

        var file = await filePicker.PickSingleFileAsync();
        if (file != null)
        {
            try
            {
                var content = await Windows.Storage.FileIO.ReadTextAsync(file);
                var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                var imported = 0;

                foreach (var line in lines.Skip(1)) // Skip header
                {
                    var parts = line.Split(',');
                    if (parts.Length >= 1)
                    {
                        var symbol = parts[0].Trim().ToUpper();
                        if (!string.IsNullOrEmpty(symbol) && !_symbols.Any(s => s.Symbol == symbol))
                        {
                            var config = new SymbolConfig
                            {
                                Symbol = symbol,
                                SubscribeTrades = parts.Length > 1 && parts[1].Trim().ToLower() == "true",
                                SubscribeDepth = parts.Length > 2 && parts[2].Trim().ToLower() == "true",
                                DepthLevels = parts.Length > 3 && int.TryParse(parts[3], out var levels) ? levels : 10,
                                Exchange = parts.Length > 4 ? parts[4].Trim() : "SMART",
                                SecurityType = "STK",
                                Currency = "USD"
                            };

                            await _configService.AddOrUpdateSymbolAsync(config);
                            imported++;
                        }
                    }
                }

                await LoadSymbolsAsync();

                FormInfoBar.Severity = InfoBarSeverity.Success;
                FormInfoBar.Title = "Import Complete";
                FormInfoBar.Message = $"Imported {imported} symbols from CSV.";
                FormInfoBar.IsOpen = true;
            }
            catch (Exception ex)
            {
                FormInfoBar.Severity = InfoBarSeverity.Error;
                FormInfoBar.Title = "Import Failed";
                FormInfoBar.Message = ex.Message;
                FormInfoBar.IsOpen = true;
            }
        }
    }

    private async void ExportCsv_Click(object sender, RoutedEventArgs e)
    {
        var savePicker = new FileSavePicker();
        savePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        savePicker.FileTypeChoices.Add("CSV Files", new List<string> { ".csv" });
        savePicker.SuggestedFileName = $"symbols_{DateTime.Now:yyyyMMdd}";

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(savePicker, hwnd);

        var file = await savePicker.PickSaveFileAsync();
        if (file != null)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Symbol,Trades,Depth,DepthLevels,Exchange,LocalSymbol");

            foreach (var symbol in _symbols)
            {
                sb.AppendLine($"{symbol.Symbol},{symbol.SubscribeTrades},{symbol.SubscribeDepth},{symbol.DepthLevels},{symbol.Exchange},{symbol.LocalSymbol}");
            }

            await Windows.Storage.FileIO.WriteTextAsync(file, sb.ToString());

            FormInfoBar.Severity = InfoBarSeverity.Success;
            FormInfoBar.Title = "Export Complete";
            FormInfoBar.Message = $"Exported {_symbols.Count} symbols to {file.Name}.";
            FormInfoBar.IsOpen = true;
        }
    }

    private void SymbolBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
        {
            var text = sender.Text?.ToUpper() ?? "";
            var suggestions = PopularSymbols.Where(s => s.StartsWith(text)).Take(10).ToList();
            sender.ItemsSource = suggestions;
        }
    }

    private void SymbolBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        sender.Text = args.SelectedItem?.ToString() ?? "";
    }

    private async void AddTemplate_Click(object sender, RoutedEventArgs e)
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
                        var config = new SymbolConfig
                        {
                            Symbol = symbol,
                            SubscribeTrades = true,
                            SubscribeDepth = false,
                            DepthLevels = 10,
                            Exchange = "SMART",
                            SecurityType = "STK",
                            Currency = "USD"
                        };

                        await _configService.AddOrUpdateSymbolAsync(config);
                        added++;
                    }
                }

                await LoadSymbolsAsync();

                FormInfoBar.Severity = InfoBarSeverity.Success;
                FormInfoBar.Title = "Template Added";
                FormInfoBar.Message = $"Added {added} new symbols from {templateName} template.";
                FormInfoBar.IsOpen = true;
            }
        }
    }

    private void LoadWatchlist_Click(object sender, RoutedEventArgs e)
    {
        FormInfoBar.Severity = InfoBarSeverity.Informational;
        FormInfoBar.Title = "Watchlist";
        FormInfoBar.Message = "Watchlist loading will be available in a future update.";
        FormInfoBar.IsOpen = true;
    }

    private void SaveWatchlist_Click(object sender, RoutedEventArgs e)
    {
        FormInfoBar.Severity = InfoBarSeverity.Informational;
        FormInfoBar.Title = "Save Watchlist";
        FormInfoBar.Message = "Watchlist saving will be available in a future update.";
        FormInfoBar.IsOpen = true;
    }

    private void ManageWatchlists_Click(object sender, RoutedEventArgs e)
    {
        FormInfoBar.Severity = InfoBarSeverity.Informational;
        FormInfoBar.Title = "Manage Watchlists";
        FormInfoBar.Message = "Watchlist management will be available in a future update.";
        FormInfoBar.IsOpen = true;
    }

    private void SymbolsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SymbolsListView.SelectedItem is EnhancedSymbolViewModel symbol)
        {
            _selectedSymbol = symbol;
            _isEditMode = true;

            // Populate form
            SymbolBox.Text = symbol.Symbol;
            SubscribeTradesToggle.IsOn = symbol.SubscribeTrades;
            SubscribeDepthToggle.IsOn = symbol.SubscribeDepth;
            DepthLevelsBox.Value = symbol.DepthLevels;
            ExchangeBox.Text = symbol.Exchange ?? "SMART";
            PrimaryExchangeBox.Text = string.Empty;
            LocalSymbolBox.Text = symbol.LocalSymbol ?? string.Empty;

            // Update UI
            FormTitle.Text = "Edit Symbol";
            SaveSymbolButton.Content = "Update Symbol";
            DeleteSymbolButton.Visibility = Visibility.Visible;
        }
    }

    private async void SaveSymbol_Click(object sender, RoutedEventArgs e)
    {
        var symbolName = SymbolBox.Text?.Trim().ToUpper();
        if (string.IsNullOrEmpty(symbolName))
        {
            FormInfoBar.Severity = InfoBarSeverity.Error;
            FormInfoBar.Title = "Validation Error";
            FormInfoBar.Message = "Symbol is required.";
            FormInfoBar.IsOpen = true;
            return;
        }

        SaveProgress.IsActive = true;
        try
        {
            var symbolConfig = new SymbolConfig
            {
                Symbol = symbolName,
                SubscribeTrades = SubscribeTradesToggle.IsOn,
                SubscribeDepth = SubscribeDepthToggle.IsOn,
                DepthLevels = (int)DepthLevelsBox.Value,
                Exchange = string.IsNullOrWhiteSpace(ExchangeBox.Text) ? "SMART" : ExchangeBox.Text,
                PrimaryExchange = string.IsNullOrWhiteSpace(PrimaryExchangeBox.Text) ? null : PrimaryExchangeBox.Text,
                LocalSymbol = string.IsNullOrWhiteSpace(LocalSymbolBox.Text) ? null : LocalSymbolBox.Text,
                SecurityType = "STK",
                Currency = "USD"
            };

            await _configService.AddOrUpdateSymbolAsync(symbolConfig);

            FormInfoBar.Severity = InfoBarSeverity.Success;
            FormInfoBar.Title = "Success";
            FormInfoBar.Message = _isEditMode
                ? $"Symbol {symbolName} updated successfully."
                : $"Symbol {symbolName} added successfully.";
            FormInfoBar.IsOpen = true;

            ClearForm();
            await LoadSymbolsAsync();
        }
        catch (Exception ex)
        {
            FormInfoBar.Severity = InfoBarSeverity.Error;
            FormInfoBar.Title = "Error";
            FormInfoBar.Message = ex.Message;
            FormInfoBar.IsOpen = true;
        }
        finally
        {
            SaveProgress.IsActive = false;
        }
    }

    private async void DeleteSymbol_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedSymbol == null) return;

        var dialog = new ContentDialog
        {
            Title = "Delete Symbol",
            Content = $"Are you sure you want to delete {_selectedSymbol.Symbol}?",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return;

        SaveProgress.IsActive = true;
        try
        {
            await _configService.DeleteSymbolAsync(_selectedSymbol.Symbol);

            FormInfoBar.Severity = InfoBarSeverity.Success;
            FormInfoBar.Title = "Success";
            FormInfoBar.Message = $"Symbol {_selectedSymbol.Symbol} deleted.";
            FormInfoBar.IsOpen = true;

            ClearForm();
            await LoadSymbolsAsync();
        }
        catch (Exception ex)
        {
            FormInfoBar.Severity = InfoBarSeverity.Error;
            FormInfoBar.Title = "Error";
            FormInfoBar.Message = ex.Message;
            FormInfoBar.IsOpen = true;
        }
        finally
        {
            SaveProgress.IsActive = false;
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
        SubscribeTradesToggle.IsOn = true;
        SubscribeDepthToggle.IsOn = false;
        DepthLevelsBox.Value = 10;
        ExchangeBox.Text = "SMART";
        PrimaryExchangeBox.Text = string.Empty;
        LocalSymbolBox.Text = string.Empty;

        FormTitle.Text = "Add Symbol";
        SaveSymbolButton.Content = "Add Symbol";
        DeleteSymbolButton.Visibility = Visibility.Collapsed;

        SymbolsListView.SelectedItem = null;
    }

    private async void RefreshList_Click(object sender, RoutedEventArgs e)
    {
        await LoadSymbolsAsync();
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
/// Enhanced symbol view model with selection and status properties.
/// </summary>
public class EnhancedSymbolViewModel : SymbolViewModel
{
    public bool IsSelected { get; set; }
    public string StatusText => SubscribeTrades || SubscribeDepth ? "Active" : "Inactive";

    public SolidColorBrush StatusBackground => SubscribeTrades || SubscribeDepth
        ? new SolidColorBrush(Color.FromArgb(40, 72, 187, 120))
        : new SolidColorBrush(Color.FromArgb(40, 160, 160, 160));

    public SolidColorBrush TradesStatusColor => SubscribeTrades
        ? new SolidColorBrush(Color.FromArgb(255, 72, 187, 120))
        : new SolidColorBrush(Color.FromArgb(255, 160, 160, 160));

    public SolidColorBrush DepthStatusColor => SubscribeDepth
        ? new SolidColorBrush(Color.FromArgb(255, 72, 187, 120))
        : new SolidColorBrush(Color.FromArgb(255, 160, 160, 160));

    public EnhancedSymbolViewModel(SymbolConfig config) : base(config)
    {
    }

    public SymbolConfig ToSymbolConfig()
    {
        return new SymbolConfig
        {
            Symbol = Symbol,
            SubscribeTrades = SubscribeTrades,
            SubscribeDepth = SubscribeDepth,
            DepthLevels = DepthLevels,
            Exchange = Exchange,
            LocalSymbol = LocalSymbol,
            SecurityType = "STK",
            Currency = "USD"
        };
    }
}

/// <summary>
/// Watchlist information model.
/// </summary>
public class WatchlistInfo
{
    public string Name { get; set; } = string.Empty;
    public string SymbolCount { get; set; } = string.Empty;
}
