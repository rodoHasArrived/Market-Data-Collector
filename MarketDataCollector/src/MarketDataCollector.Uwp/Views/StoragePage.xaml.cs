using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MarketDataCollector.Uwp.Models;
using MarketDataCollector.Uwp.Services;
using Windows.Storage.Pickers;

namespace MarketDataCollector.Uwp.Views;

/// <summary>
/// Page for configuring storage settings.
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
