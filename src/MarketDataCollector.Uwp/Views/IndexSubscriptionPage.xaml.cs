using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MarketDataCollector.Uwp.Services;
using MarketDataCollector.Uwp.Models;

namespace MarketDataCollector.Uwp.Views;

/// <summary>
/// Page for subscribing to index constituents.
/// </summary>
public sealed partial class IndexSubscriptionPage : Page
{
    private readonly PortfolioImportService _importService;
    private List<ConstituentDisplay> _currentConstituents = new();

    public IndexSubscriptionPage()
    {
        this.InitializeComponent();
        _importService = PortfolioImportService.Instance;

        EnableDepthCheck.Checked += (s, e) => DepthLevelsCombo.IsEnabled = true;
        EnableDepthCheck.Unchecked += (s, e) => DepthLevelsCombo.IsEnabled = false;

        Loaded += IndexSubscriptionPage_Loaded;
    }

    private void IndexSubscriptionPage_Loaded(object sender, RoutedEventArgs e)
    {
        LoadSectorETFs();
        LoadActiveIndices();
    }

    private void LoadSectorETFs()
    {
        var sectors = new List<SectorETFDisplay>
        {
            new() { Symbol = "XLF", Name = "Financial Select Sector", HoldingsText = "~75 holdings" },
            new() { Symbol = "XLK", Name = "Technology Select Sector", HoldingsText = "~70 holdings" },
            new() { Symbol = "XLE", Name = "Energy Select Sector", HoldingsText = "~25 holdings" },
            new() { Symbol = "XLV", Name = "Health Care Select Sector", HoldingsText = "~65 holdings" },
            new() { Symbol = "XLI", Name = "Industrial Select Sector", HoldingsText = "~80 holdings" },
            new() { Symbol = "XLY", Name = "Consumer Discretionary", HoldingsText = "~60 holdings" },
            new() { Symbol = "XLP", Name = "Consumer Staples", HoldingsText = "~35 holdings" },
            new() { Symbol = "XLU", Name = "Utilities Select Sector", HoldingsText = "~30 holdings" },
            new() { Symbol = "XLB", Name = "Materials Select Sector", HoldingsText = "~30 holdings" },
            new() { Symbol = "XLRE", Name = "Real Estate Select Sector", HoldingsText = "~30 holdings" }
        };

        SectorETFsList.ItemsSource = sectors;
    }

    private void LoadActiveIndices()
    {
        // This would load from a persistent store
        var activeIndices = new List<ActiveIndexDisplay>();
        // For demo, show empty state
        ActiveIndicesList.ItemsSource = activeIndices;
        NoActiveIndicesText.Visibility = activeIndices.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        ActiveIndicesText.Text = activeIndices.Count.ToString();
        TotalSymbolsText.Text = activeIndices.Sum(i => i.SymbolCountValue).ToString();
    }

    private async void SubscribeIndex_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string indexTag) return;

        var (indexName, limit) = ParseIndexTag(indexTag);
        await LoadIndexConstituentsAsync(indexName, limit);
    }

    private async void SubscribeSector_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not SectorETFDisplay sector) return;

        await LoadIndexConstituentsAsync(sector.Symbol, null);
    }

    private async void FetchCustomIndex_Click(object sender, RoutedEventArgs e)
    {
        var symbol = CustomIndexBox.Text?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(symbol)) return;

        await LoadIndexConstituentsAsync(symbol, null);
    }

    private async System.Threading.Tasks.Task LoadIndexConstituentsAsync(string indexName, int? limit)
    {
        var result = await _importService.GetIndexConstituentsAsync(indexName);

        if (result.Success && result.Symbols.Count > 0)
        {
            var symbols = limit.HasValue ? result.Symbols.Take(limit.Value).ToList() : result.Symbols;

            _currentConstituents = symbols.Select((s, i) => new ConstituentDisplay
            {
                Symbol = s,
                Name = "", // Would be populated from symbol metadata
                WeightText = $"#{i + 1}"
            }).ToList();

            ConstituentsList.ItemsSource = _currentConstituents;
            PreviewIndexName.Text = result.IndexName;
            PreviewCountText.Text = $"({symbols.Count} symbols)";

            ConstituentPreviewCard.Visibility = Visibility.Visible;

            // Select all by default
            ConstituentsList.SelectAll();
        }
        else
        {
            var dialog = new ContentDialog
            {
                Title = "Index Not Found",
                Content = result.Error ?? $"Could not find constituents for {indexName}",
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
        }
    }

    private static (string indexName, int? limit) ParseIndexTag(string tag)
    {
        var parts = tag.Split('_');
        var indexName = parts[0];
        int? limit = parts.Length > 1 && int.TryParse(parts[1], out var l) ? l : null;
        return (indexName, limit);
    }

    private void SelectAllConstituents_Click(object sender, RoutedEventArgs e)
    {
        ConstituentsList.SelectAll();
    }

    private void ClearConstituents_Click(object sender, RoutedEventArgs e)
    {
        ConstituentsList.SelectedItems.Clear();
    }

    private async void SubscribeSelected_Click(object sender, RoutedEventArgs e)
    {
        var selectedSymbols = ConstituentsList.SelectedItems
            .Cast<ConstituentDisplay>()
            .Select(c => new PortfolioEntry { Symbol = c.Symbol })
            .ToList();

        if (selectedSymbols.Count == 0)
        {
            SubscribeStatusText.Text = "Please select at least one symbol";
            return;
        }

        SubscribeProgress.IsActive = true;
        SubscribeSelectedButton.IsEnabled = false;
        SubscribeStatusText.Text = "Subscribing...";

        try
        {
            var enableTrades = EnableTradesCheck.IsChecked == true;
            var enableDepth = EnableDepthCheck.IsChecked == true;
            var depthLevels = int.Parse((DepthLevelsCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "5");

            var result = await _importService.ImportAsSubscriptionsAsync(
                selectedSymbols,
                enableTrades,
                enableDepth,
                depthLevels);

            if (result.Success)
            {
                SubscribeStatusText.Text = $"Subscribed to {result.ImportedCount} symbols";
                LoadActiveIndices();
            }
            else
            {
                SubscribeStatusText.Text = result.Error ?? "Subscription failed";
            }
        }
        catch (Exception ex)
        {
            SubscribeStatusText.Text = $"Error: {ex.Message}";
        }
        finally
        {
            SubscribeProgress.IsActive = false;
            SubscribeSelectedButton.IsEnabled = true;
        }
    }

    private void RemoveIndex_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not ActiveIndexDisplay index) return;

        // Remove index subscriptions
        LoadActiveIndices();
    }
}
