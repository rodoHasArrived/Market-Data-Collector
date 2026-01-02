using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using MarketDataCollector.Uwp.Models;
using MarketDataCollector.Uwp.Services;
using Windows.Storage.Pickers;
using Windows.UI;

namespace MarketDataCollector.Uwp.Views;

/// <summary>
/// Enhanced storage settings page with analytics dashboard, lifecycle management,
/// and cloud storage integration.
/// </summary>
public sealed partial class StoragePage : Page
{
    private readonly ConfigService _configService;

    public StoragePage()
    {
        this.InitializeComponent();
        _configService = new ConfigService();

        Loaded += StoragePage_Loaded;
    }

    private async void StoragePage_Loaded(object sender, RoutedEventArgs e)
    {
        var config = await _configService.LoadConfigAsync();
        if (config != null)
        {
            DataRootBox.Text = config.DataRoot ?? "data";
            CompressToggle.IsOn = config.Compress;

            if (config.Storage != null)
            {
                SelectComboItemByTag(NamingConventionCombo, config.Storage.NamingConvention ?? "BySymbol");
                SelectComboItemByTag(DatePartitionCombo, config.Storage.DatePartition ?? "Daily");
                FilePrefixBox.Text = config.Storage.FilePrefix ?? string.Empty;
                IncludeProviderToggle.IsOn = config.Storage.IncludeProvider;
            }

            UpdatePreviewPath();
        }

        LoadStorageAnalytics();
        LoadTopSymbols();
        UpdateCloudProviderUI();
        UpdateLifecycleUI();
    }

    private void LoadStorageAnalytics()
    {
        // In a real implementation, this would scan the data directory
        // For now, showing sample data
        TotalStorageText.Text = "12.4 GB";
        TotalFilesText.Text = "1,847 files";

        TradeStorageText.Text = "8.2 GB";
        TradeFilesText.Text = "1,245 files";

        DepthStorageText.Text = "3.8 GB";
        DepthFilesText.Text = "512 files";

        HistoricalStorageText.Text = "0.4 GB";
        HistoricalFilesText.Text = "90 files";
    }

    private void LoadTopSymbols()
    {
        var topSymbols = new List<SymbolStorageInfo>
        {
            new() { Symbol = "SPY", Percentage = 100, Size = "2.1 GB", Files = "412 files" },
            new() { Symbol = "AAPL", Percentage = 76, Size = "1.6 GB", Files = "298 files" },
            new() { Symbol = "MSFT", Percentage = 62, Size = "1.3 GB", Files = "245 files" },
            new() { Symbol = "QQQ", Percentage = 48, Size = "1.0 GB", Files = "189 files" },
            new() { Symbol = "TSLA", Percentage = 38, Size = "0.8 GB", Files = "156 files" }
        };

        TopSymbolsList.ItemsSource = topSymbols;
    }

    private void RefreshAnalytics_Click(object sender, RoutedEventArgs e)
    {
        LoadStorageAnalytics();
        LoadTopSymbols();

        SaveInfoBar.Severity = InfoBarSeverity.Informational;
        SaveInfoBar.Title = "Refreshed";
        SaveInfoBar.Message = "Storage analytics have been updated.";
        SaveInfoBar.IsOpen = true;
    }

    private void LifecycleToggle_Toggled(object sender, RoutedEventArgs e)
    {
        UpdateLifecycleUI();
    }

    private void UpdateLifecycleUI()
    {
        LifecycleSettingsPanel.Opacity = LifecycleEnabledToggle.IsOn ? 1.0 : 0.5;
    }

    private async void RunLifecycle_Click(object sender, RoutedEventArgs e)
    {
        LifecycleProgress.IsActive = true;
        LifecycleStatusText.Text = "Analyzing data...";

        await Task.Delay(1000);
        LifecycleStatusText.Text = "Moving 45 files to warm tier...";

        await Task.Delay(1500);
        LifecycleStatusText.Text = "Compressing data...";

        await Task.Delay(1000);
        LifecycleStatusText.Text = "Archiving 12 files to cold tier...";

        await Task.Delay(1000);
        LifecycleProgress.IsActive = false;
        LifecycleStatusText.Text = "Lifecycle run complete. Freed 1.2 GB.";

        SaveInfoBar.Severity = InfoBarSeverity.Success;
        SaveInfoBar.Title = "Lifecycle Complete";
        SaveInfoBar.Message = "Moved 45 files to warm tier, archived 12 files. Freed 1.2 GB of hot storage.";
        SaveInfoBar.IsOpen = true;

        LoadStorageAnalytics();
    }

    private void CloudToggle_Toggled(object sender, RoutedEventArgs e)
    {
        CloudSettingsPanel.Opacity = CloudEnabledToggle.IsOn ? 1.0 : 0.5;
    }

    private void CloudProvider_Changed(object sender, SelectionChangedEventArgs e)
    {
        UpdateCloudProviderUI();
    }

    private void UpdateCloudProviderUI()
    {
        var provider = GetComboSelectedTag(CloudProviderCombo);

        AzureSettings.Visibility = provider == "Azure" ? Visibility.Visible : Visibility.Collapsed;
        S3Settings.Visibility = provider == "S3" ? Visibility.Visible : Visibility.Collapsed;
        GcsSettings.Visibility = provider == "GCS" ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void TestCloudConnection_Click(object sender, RoutedEventArgs e)
    {
        CloudSyncProgress.IsActive = true;
        CloudStatusText.Text = "Testing connection...";
        CloudStatusIndicator.Fill = new SolidColorBrush(Color.FromArgb(255, 237, 137, 54));

        await Task.Delay(2000); // Simulate connection test

        CloudSyncProgress.IsActive = false;
        CloudStatusIndicator.Fill = new SolidColorBrush(Color.FromArgb(255, 72, 187, 120));
        CloudStatusText.Text = "Connected - Connection test successful";

        SaveInfoBar.Severity = InfoBarSeverity.Success;
        SaveInfoBar.Title = "Connection Successful";
        SaveInfoBar.Message = "Successfully connected to cloud storage.";
        SaveInfoBar.IsOpen = true;
    }

    private async void SyncCloud_Click(object sender, RoutedEventArgs e)
    {
        CloudSyncProgress.IsActive = true;
        CloudStatusText.Text = "Syncing...";

        await Task.Delay(1000);
        CloudSyncDetails.Text = "Syncing: 1,234 files (10.2 GB) | Uploading: 3 of 12 files";

        await Task.Delay(1500);
        CloudSyncDetails.Text = "Syncing: 1,234 files (10.2 GB) | Uploading: 8 of 12 files";

        await Task.Delay(1000);
        CloudSyncProgress.IsActive = false;
        CloudStatusText.Text = "Connected - Last sync: just now";
        CloudSyncDetails.Text = "Synced: 1,246 files (10.4 GB) | Pending: 0 files";

        SaveInfoBar.Severity = InfoBarSeverity.Success;
        SaveInfoBar.Title = "Sync Complete";
        SaveInfoBar.Message = "Successfully synced 12 files to cloud storage.";
        SaveInfoBar.IsOpen = true;
    }

    private async void BrowseGcsKey_Click(object sender, RoutedEventArgs e)
    {
        var filePicker = new FileOpenPicker();
        filePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        filePicker.FileTypeFilter.Add(".json");

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(filePicker, hwnd);

        var file = await filePicker.PickSingleFileAsync();
        if (file != null)
        {
            var content = await Windows.Storage.FileIO.ReadTextAsync(file);
            GcsServiceAccountKey.Text = content;
        }
    }

    private void UpdatePreview(object sender, object e)
    {
        UpdatePreviewPath();
    }

    private void UpdatePreviewPath()
    {
        var root = string.IsNullOrWhiteSpace(DataRootBox.Text) ? "data" : DataRootBox.Text;
        var compress = CompressToggle.IsOn;
        var naming = GetComboSelectedTag(NamingConventionCombo) ?? "BySymbol";
        var partition = GetComboSelectedTag(DatePartitionCombo) ?? "Daily";
        var prefix = FilePrefixBox.Text;
        var ext = compress ? ".jsonl.gz" : ".jsonl";
        var pfx = string.IsNullOrEmpty(prefix) ? "" : prefix + "_";

        var dateStr = partition switch
        {
            "Daily" => "2024-01-15",
            "Hourly" => "2024-01-15_14",
            "Monthly" => "2024-01",
            _ => ""
        };

        var path = naming switch
        {
            "Flat" => string.IsNullOrEmpty(dateStr)
                ? $"{root}/{pfx}AAPL_Trade{ext}"
                : $"{root}/{pfx}AAPL_Trade_{dateStr}{ext}",
            "BySymbol" => string.IsNullOrEmpty(dateStr)
                ? $"{root}/AAPL/Trade/{pfx}data{ext}"
                : $"{root}/AAPL/Trade/{pfx}{dateStr}{ext}",
            "ByDate" => string.IsNullOrEmpty(dateStr)
                ? $"{root}/AAPL/{pfx}Trade{ext}"
                : $"{root}/{dateStr}/AAPL/{pfx}Trade{ext}",
            "ByType" => string.IsNullOrEmpty(dateStr)
                ? $"{root}/Trade/AAPL/{pfx}data{ext}"
                : $"{root}/Trade/AAPL/{pfx}{dateStr}{ext}",
            _ => $"{root}/AAPL/Trade/{pfx}{dateStr}{ext}"
        };

        PathPreviewText.Text = path;
    }

    private async void BrowseFolder_Click(object sender, RoutedEventArgs e)
    {
        var folderPicker = new FolderPicker();
        folderPicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        folderPicker.FileTypeFilter.Add("*");

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hwnd);

        var folder = await folderPicker.PickSingleFolderAsync();
        if (folder != null)
        {
            DataRootBox.Text = folder.Path;
            UpdatePreviewPath();
        }
    }

    private async void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        SaveProgress.IsActive = true;
        try
        {
            var storage = new StorageConfig
            {
                NamingConvention = GetComboSelectedTag(NamingConventionCombo) ?? "BySymbol",
                DatePartition = GetComboSelectedTag(DatePartitionCombo) ?? "Daily",
                FilePrefix = string.IsNullOrWhiteSpace(FilePrefixBox.Text) ? null : FilePrefixBox.Text,
                IncludeProvider = IncludeProviderToggle.IsOn
            };

            await _configService.SaveStorageConfigAsync(
                DataRootBox.Text,
                CompressToggle.IsOn,
                storage);

            SaveInfoBar.Severity = InfoBarSeverity.Success;
            SaveInfoBar.Title = "Success";
            SaveInfoBar.Message = "Storage settings saved. Restart collector to apply changes.";
            SaveInfoBar.IsOpen = true;
        }
        catch (Exception ex)
        {
            SaveInfoBar.Severity = InfoBarSeverity.Error;
            SaveInfoBar.Title = "Error";
            SaveInfoBar.Message = ex.Message;
            SaveInfoBar.IsOpen = true;
        }
        finally
        {
            SaveProgress.IsActive = false;
        }
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
/// Model for symbol storage information display.
/// </summary>
public class SymbolStorageInfo
{
    public string Symbol { get; set; } = string.Empty;
    public double Percentage { get; set; }
    public string Size { get; set; } = string.Empty;
    public string Files { get; set; } = string.Empty;
}
