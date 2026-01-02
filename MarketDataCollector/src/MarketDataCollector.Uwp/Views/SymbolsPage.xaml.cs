using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MarketDataCollector.Uwp.Models;
using MarketDataCollector.Uwp.Services;
using MarketDataCollector.Uwp.ViewModels;

namespace MarketDataCollector.Uwp.Views;

/// <summary>
/// Page for managing symbol subscriptions.
/// </summary>
public sealed partial class SymbolsPage : Page
{
    private readonly ConfigService _configService;
    private readonly ObservableCollection<SymbolViewModel> _symbols = new();
    private SymbolViewModel? _selectedSymbol;
    private bool _isEditMode;

    public SymbolsPage()
    {
        this.InitializeComponent();
        _configService = new ConfigService();
        SymbolsListView.ItemsSource = _symbols;

        Loaded += SymbolsPage_Loaded;
    }

    private async void SymbolsPage_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadSymbolsAsync();
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
                    _symbols.Add(new SymbolViewModel(symbol));
                }
            }

            SymbolCountText.Text = $"{_symbols.Count} symbols";
        }
        finally
        {
            RefreshProgress.IsActive = false;
        }
    }

    private void SymbolsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SymbolsListView.SelectedItem is SymbolViewModel symbol)
        {
            _selectedSymbol = symbol;
            _isEditMode = true;

            // Populate form
            SymbolBox.Text = symbol.Symbol;
            SubscribeTradesToggle.IsOn = symbol.SubscribeTrades;
            SubscribeDepthToggle.IsOn = symbol.SubscribeDepth;
            DepthLevelsBox.Value = symbol.DepthLevels;
            ExchangeBox.Text = symbol.Exchange ?? "SMART";
            PrimaryExchangeBox.Text = string.Empty; // Not stored in SymbolViewModel
            LocalSymbolBox.Text = symbol.LocalSymbol ?? string.Empty;

            // Update UI
            FormTitle.Text = "Edit Symbol";
            SaveSymbolButton.Content = "Update Symbol";
            DeleteSymbolButton.Visibility = Visibility.Visible;
        }
    }

    private async void SaveSymbol_Click(object sender, RoutedEventArgs e)
    {
        var symbolName = SymbolBox.Text?.Trim();
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
}
