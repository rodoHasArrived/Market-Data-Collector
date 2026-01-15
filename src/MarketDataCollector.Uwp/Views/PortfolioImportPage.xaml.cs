using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using MarketDataCollector.Uwp.Services;

namespace MarketDataCollector.Uwp.Views;

/// <summary>
/// Page for importing portfolios from CSV/JSON files and quick index import.
/// </summary>
public sealed partial class PortfolioImportPage : Page
{
    private readonly PortfolioImportService _importService;
    private List<PortfolioEntryDisplay> _parsedEntries = new();
    private string? _selectedFilePath;

    public PortfolioImportPage()
    {
        this.InitializeComponent();
        _importService = PortfolioImportService.Instance;

        EnableDepthCheck.Checked += (s, e) => DepthLevelsCombo.IsEnabled = true;
        EnableDepthCheck.Unchecked += (s, e) => DepthLevelsCombo.IsEnabled = false;

        ImportAsSubscriptionsRadio.Checked += (s, e) =>
        {
            SubscriptionOptionsPanel.Visibility = Visibility.Visible;
            WatchlistOptionsPanel.Visibility = Visibility.Collapsed;
        };
        ImportAsWatchlistRadio.Checked += (s, e) =>
        {
            SubscriptionOptionsPanel.Visibility = Visibility.Collapsed;
            WatchlistOptionsPanel.Visibility = Visibility.Visible;
        };
    }

    private async void BrowseFile_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();
        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        picker.FileTypeFilter.Add(".csv");
        picker.FileTypeFilter.Add(".json");
        picker.FileTypeFilter.Add(".txt");

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        var file = await picker.PickSingleFileAsync();
        if (file != null)
        {
            _selectedFilePath = file.Path;
            FilePathBox.Text = file.Path;
            ParseFileButton.IsEnabled = true;
        }
    }

    private async void ParseFile_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_selectedFilePath)) return;

        ParseProgress.IsActive = true;
        ParseStatusText.Text = "Parsing file...";
        ParseFileButton.IsEnabled = false;

        try
        {
            PortfolioParseResult result;
            var extension = System.IO.Path.GetExtension(_selectedFilePath).ToLowerInvariant();

            if (extension == ".json")
            {
                result = await _importService.ParseJsonAsync(_selectedFilePath);
            }
            else
            {
                result = await _importService.ParseCsvAsync(_selectedFilePath);
            }

            if (result.Success)
            {
                _parsedEntries = result.Entries.Select(e => new PortfolioEntryDisplay
                {
                    Symbol = e.Symbol,
                    Quantity = e.Quantity,
                    QuantityText = e.Quantity?.ToString("N0") ?? "-",
                    Exchange = e.Exchange ?? "-"
                }).ToList();

                ParsedSymbolsList.ItemsSource = _parsedEntries;
                ParsedCountText.Text = $"({result.TotalSymbols} symbols)";
                ParseStatusText.Text = $"Found {result.TotalSymbols} symbols";

                ParsedResultsCard.Visibility = Visibility.Visible;
                ImportOptionsCard.Visibility = Visibility.Visible;
                ImportResultsCard.Visibility = Visibility.Collapsed;

                // Select all by default
                ParsedSymbolsList.SelectAll();
            }
            else
            {
                ParseStatusText.Text = result.Error ?? "Failed to parse file";
                ParsedResultsCard.Visibility = Visibility.Collapsed;
                ImportOptionsCard.Visibility = Visibility.Collapsed;
            }
        }
        catch (Exception ex)
        {
            ParseStatusText.Text = $"Error: {ex.Message}";
        }
        finally
        {
            ParseProgress.IsActive = false;
            ParseFileButton.IsEnabled = true;
        }
    }

    private void SelectAll_Click(object sender, RoutedEventArgs e)
    {
        ParsedSymbolsList.SelectAll();
    }

    private void ClearSelection_Click(object sender, RoutedEventArgs e)
    {
        ParsedSymbolsList.SelectedItems.Clear();
    }

    private async void Import_Click(object sender, RoutedEventArgs e)
    {
        var selectedItems = ParsedSymbolsList.SelectedItems
            .Cast<PortfolioEntryDisplay>()
            .Select(d => new PortfolioEntry { Symbol = d.Symbol, Quantity = d.Quantity })
            .ToList();

        if (selectedItems.Count == 0)
        {
            ImportStatusText.Text = "Please select at least one symbol";
            return;
        }

        ImportProgress.IsActive = true;
        ImportStatusText.Text = "Importing...";
        ImportButton.IsEnabled = false;

        try
        {
            PortfolioImportResult result;

            if (ImportAsSubscriptionsRadio.IsChecked == true)
            {
                var enableTrades = EnableTradesCheck.IsChecked == true;
                var enableDepth = EnableDepthCheck.IsChecked == true;
                var depthLevels = int.Parse((DepthLevelsCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "5");

                result = await _importService.ImportAsSubscriptionsAsync(
                    selectedItems,
                    enableTrades,
                    enableDepth,
                    depthLevels);
            }
            else
            {
                var watchlistName = WatchlistNameBox.Text;
                if (string.IsNullOrWhiteSpace(watchlistName))
                {
                    watchlistName = "Imported Portfolio";
                }

                result = await _importService.ImportToWatchlistAsync(selectedItems, watchlistName);
            }

            ShowImportResults(result);
        }
        catch (Exception ex)
        {
            ImportStatusText.Text = $"Error: {ex.Message}";
        }
        finally
        {
            ImportProgress.IsActive = false;
            ImportButton.IsEnabled = true;
        }
    }

    private void ShowImportResults(PortfolioImportResult result)
    {
        ImportResultsCard.Visibility = Visibility.Visible;

        ImportedCountText.Text = result.ImportedCount.ToString();
        SkippedCountText.Text = result.SkippedCount.ToString();
        FailedCountText.Text = result.FailedCount.ToString();

        if (result.Success)
        {
            ImportResultIcon.Glyph = "\uE73E";
            ImportResultIcon.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Windows.UI.Color.FromArgb(255, 72, 187, 120));
            ImportStatusText.Text = "Import completed successfully";
        }
        else
        {
            ImportResultIcon.Glyph = "\uEA39";
            ImportResultIcon.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Windows.UI.Color.FromArgb(255, 245, 101, 101));
            ImportStatusText.Text = result.Error ?? "Import failed";
        }

        if (result.Errors.Count > 0)
        {
            ImportErrorsList.ItemsSource = result.Errors;
            ImportErrorsList.Visibility = Visibility.Visible;
        }
        else
        {
            ImportErrorsList.Visibility = Visibility.Collapsed;
        }
    }

    private async void QuickImportIndex_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string indexName) return;

        var result = await _importService.GetIndexConstituentsAsync(indexName);
        if (result.Success && result.Symbols.Count > 0)
        {
            _parsedEntries = result.Symbols.Select(s => new PortfolioEntryDisplay
            {
                Symbol = s,
                QuantityText = "-",
                Exchange = "-"
            }).ToList();

            ParsedSymbolsList.ItemsSource = _parsedEntries;
            ParsedCountText.Text = $"({result.Symbols.Count} symbols from {result.IndexName})";

            ParsedResultsCard.Visibility = Visibility.Visible;
            ImportOptionsCard.Visibility = Visibility.Visible;
            ImportResultsCard.Visibility = Visibility.Collapsed;

            ParsedSymbolsList.SelectAll();
        }
    }

    private void QuickImportSymbols_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string symbols) return;

        var symbolList = symbols.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        _parsedEntries = symbolList.Select(s => new PortfolioEntryDisplay
        {
            Symbol = s.ToUpperInvariant(),
            QuantityText = "-",
            Exchange = "-"
        }).ToList();

        ParsedSymbolsList.ItemsSource = _parsedEntries;
        ParsedCountText.Text = $"({symbolList.Length} symbols)";

        ParsedResultsCard.Visibility = Visibility.Visible;
        ImportOptionsCard.Visibility = Visibility.Visible;
        ImportResultsCard.Visibility = Visibility.Collapsed;

        ParsedSymbolsList.SelectAll();
    }

    private void AddManualSymbols_Click(object sender, RoutedEventArgs e)
    {
        var text = ManualSymbolsBox.Text;
        if (string.IsNullOrWhiteSpace(text)) return;

        var symbols = text.Split(new[] { ',', '\n', '\r', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => s.ToUpperInvariant())
            .Distinct()
            .ToList();

        if (symbols.Count == 0) return;

        _parsedEntries = symbols.Select(s => new PortfolioEntryDisplay
        {
            Symbol = s,
            QuantityText = "-",
            Exchange = "-"
        }).ToList();

        ParsedSymbolsList.ItemsSource = _parsedEntries;
        ParsedCountText.Text = $"({symbols.Count} symbols)";

        ParsedResultsCard.Visibility = Visibility.Visible;
        ImportOptionsCard.Visibility = Visibility.Visible;
        ImportResultsCard.Visibility = Visibility.Collapsed;

        ParsedSymbolsList.SelectAll();
        ManualSymbolsBox.Text = string.Empty;
    }
}

/// <summary>
/// Display model for portfolio entries.
/// </summary>
public class PortfolioEntryDisplay
{
    public string Symbol { get; set; } = string.Empty;
    public decimal? Quantity { get; set; }
    public string QuantityText { get; set; } = string.Empty;
    public string Exchange { get; set; } = string.Empty;
}
