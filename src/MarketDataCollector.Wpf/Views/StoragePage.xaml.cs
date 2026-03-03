using System;
using System.Windows;
using System.Windows.Controls;
using MarketDataCollector.Ui.Services;
using MarketDataCollector.Ui.Services.Services;
using WpfServices = MarketDataCollector.Wpf.Services;

namespace MarketDataCollector.Wpf.Views;

public partial class StoragePage : Page
{
    private readonly StorageAnalyticsService _analyticsService;

    public StoragePage()
    {
        InitializeComponent();
        _analyticsService = StorageAnalyticsService.Instance;
    }

    private async void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        await LoadStorageMetricsAsync();
    }

    private async System.Threading.Tasks.Task LoadStorageMetricsAsync()
    {
        try
        {
            var analytics = await _analyticsService.GetAnalyticsAsync();

            TotalSizeText.Text = FormatHelpers.FormatBytes(analytics.TotalSizeBytes);
            TotalFilesText.Text = analytics.TotalFileCount.ToString("N0");
            SymbolCountText.Text = analytics.SymbolBreakdown.Length.ToString("N0");

            // Tier sizes: use trade data as hot, historical as cold, remainder as warm
            HotTierSizeText.Text = FormatHelpers.FormatBytes(analytics.TradeSizeBytes);
            WarmTierSizeText.Text = FormatHelpers.FormatBytes(analytics.DepthSizeBytes);
            ColdTierSizeText.Text = FormatHelpers.FormatBytes(analytics.HistoricalSizeBytes);
        }
        catch (Exception)
        {
            // Leave placeholder "--" values in place on error
        }
    }
}
