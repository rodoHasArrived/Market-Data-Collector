using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MarketDataCollector.Uwp.Models;
using MarketDataCollector.Uwp.Services;
using System.Collections.ObjectModel;

namespace MarketDataCollector.Uwp.Views;

/// <summary>
/// Page for managing multiple data source configurations.
/// </summary>
public sealed partial class DataSourcesPage : Page
{
    private readonly ConfigService _configService;
    private string? _editingSourceId;

    public ObservableCollection<DataSourceConfig> DataSources { get; } = new();

    public DataSourcesPage()
    {
        this.InitializeComponent();
        _configService = new ConfigService();

        Loaded += DataSourcesPage_Loaded;
    }

    private async void DataSourcesPage_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadDataSourcesAsync();
    }

    private async Task LoadDataSourcesAsync()
    {
        try
        {
            var config = await _configService.GetDataSourcesConfigAsync();

            // Update failover settings
            EnableFailoverToggle.IsOn = config.EnableFailover;
            FailoverTimeoutBox.Value = config.FailoverTimeoutSeconds;

            // Load data sources
            DataSources.Clear();
            var sources = config.Sources ?? Array.Empty<DataSourceConfig>();
            foreach (var source in sources)
            {
                DataSources.Add(source);
            }

            // Update UI state
            UpdateSourceCountText();
            UpdateDefaultSourceCombos(config);
            NoSourcesText.Visibility = DataSources.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            ShowError("Failed to load data sources", ex.Message);
        }
    }

    private void UpdateSourceCountText()
    {
        SourceCountText.Text = $"({DataSources.Count})";
    }

    private void UpdateDefaultSourceCombos(DataSourcesConfig config)
    {
        // Populate default source combos
        var realTimeSources = DataSources.Where(s => s.Type == "RealTime" || s.Type == "Both").ToList();
        var historicalSources = DataSources.Where(s => s.Type == "Historical" || s.Type == "Both").ToList();

        DefaultRealTimeCombo.ItemsSource = realTimeSources;
        DefaultHistoricalCombo.ItemsSource = historicalSources;

        // Select current defaults
        if (!string.IsNullOrEmpty(config.DefaultRealTimeSourceId))
        {
            DefaultRealTimeCombo.SelectedItem = realTimeSources.FirstOrDefault(s => s.Id == config.DefaultRealTimeSourceId);
        }

        if (!string.IsNullOrEmpty(config.DefaultHistoricalSourceId))
        {
            DefaultHistoricalCombo.SelectedItem = historicalSources.FirstOrDefault(s => s.Id == config.DefaultHistoricalSourceId);
        }
    }

    private void AddDataSource_Click(object sender, RoutedEventArgs e)
    {
        _editingSourceId = null;
        EditPanelTitle.Text = "Add Data Source";
        ClearEditForm();
        EditPanel.Visibility = Visibility.Visible;
    }

    private void EditDataSource_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string sourceId)
        {
            var source = DataSources.FirstOrDefault(s => s.Id == sourceId);
            if (source != null)
            {
                _editingSourceId = sourceId;
                EditPanelTitle.Text = "Edit Data Source";
                PopulateEditForm(source);
                EditPanel.Visibility = Visibility.Visible;
            }
        }
    }

    private async void DeleteDataSource_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string sourceId)
        {
            var source = DataSources.FirstOrDefault(s => s.Id == sourceId);
            if (source == null) return;

            var dialog = new ContentDialog
            {
                Title = "Delete Data Source",
                Content = $"Are you sure you want to delete '{source.Name}'?",
                PrimaryButtonText = "Delete",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                try
                {
                    await _configService.DeleteDataSourceAsync(sourceId);
                    await LoadDataSourcesAsync();
                    ShowSuccess("Data source deleted successfully.");
                }
                catch (Exception ex)
                {
                    ShowError("Failed to delete data source", ex.Message);
                }
            }
        }
    }

    private async void SaveDataSource_Click(object sender, RoutedEventArgs e)
    {
        // Validate required fields
        if (string.IsNullOrWhiteSpace(SourceNameBox.Text))
        {
            ShowError("Validation Error", "Name is required.");
            return;
        }

        SaveProgress.IsActive = true;
        SaveSourceButton.IsEnabled = false;

        try
        {
            var source = BuildDataSourceFromForm();
            await _configService.AddOrUpdateDataSourceAsync(source);

            EditPanel.Visibility = Visibility.Collapsed;
            await LoadDataSourcesAsync();

            ShowSuccess(_editingSourceId == null
                ? "Data source added successfully."
                : "Data source updated successfully.");
        }
        catch (Exception ex)
        {
            ShowError("Failed to save data source", ex.Message);
        }
        finally
        {
            SaveProgress.IsActive = false;
            SaveSourceButton.IsEnabled = true;
        }
    }

    private void CancelEdit_Click(object sender, RoutedEventArgs e)
    {
        EditPanel.Visibility = Visibility.Collapsed;
        _editingSourceId = null;
    }

    private DataSourceConfig BuildDataSourceFromForm()
    {
        var source = new DataSourceConfig
        {
            Id = _editingSourceId ?? Guid.NewGuid().ToString("N"),
            Name = SourceNameBox.Text.Trim(),
            Provider = GetComboSelectedTag(ProviderCombo) ?? "IB",
            Type = GetComboSelectedTag(TypeCombo) ?? "RealTime",
            Priority = (int)PriorityBox.Value,
            Description = DescriptionBox.Text.Trim(),
            Enabled = true
        };

        // Parse symbols
        if (!string.IsNullOrWhiteSpace(SymbolsBox.Text))
        {
            source.Symbols = SymbolsBox.Text
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim().ToUpperInvariant())
                .Where(s => !string.IsNullOrEmpty(s))
                .ToArray();
        }

        // Provider-specific settings
        switch (source.Provider)
        {
            case "IB":
                source.IB = new IBOptions
                {
                    Host = IBHostBox.Text.Trim(),
                    Port = (int)IBPortBox.Value,
                    ClientId = (int)IBClientIdBox.Value,
                    UsePaperTrading = IBPaperTradingCheck.IsChecked ?? false,
                    SubscribeDepth = IBSubscribeDepthCheck.IsChecked ?? true,
                    TickByTick = IBTickByTickCheck.IsChecked ?? true
                };
                break;

            case "Alpaca":
                source.Alpaca = new AlpacaOptions
                {
                    Feed = GetComboSelectedTag(AlpacaFeedCombo) ?? "iex",
                    UseSandbox = GetComboSelectedTag(AlpacaEnvironmentCombo) == "true",
                    SubscribeQuotes = AlpacaSubscribeQuotesCheck.IsChecked ?? false
                };
                break;

            case "Polygon":
                source.Polygon = new PolygonOptions
                {
                    ApiKey = PolygonApiKeyBox.Password,
                    Feed = GetComboSelectedTag(PolygonFeedCombo) ?? "stocks",
                    UseDelayed = PolygonDelayedCheck.IsChecked ?? false,
                    SubscribeTrades = PolygonTradesCheck.IsChecked ?? true,
                    SubscribeQuotes = PolygonQuotesCheck.IsChecked ?? false,
                    SubscribeAggregates = PolygonAggregatesCheck.IsChecked ?? false
                };
                break;
        }

        return source;
    }

    private void PopulateEditForm(DataSourceConfig source)
    {
        SourceNameBox.Text = source.Name;
        SelectComboItemByTag(ProviderCombo, source.Provider);
        SelectComboItemByTag(TypeCombo, source.Type);
        PriorityBox.Value = source.Priority;
        DescriptionBox.Text = source.Description ?? string.Empty;
        SymbolsBox.Text = source.Symbols != null ? string.Join(", ", source.Symbols) : string.Empty;

        UpdateProviderSettingsPanels(source.Provider);

        // Provider-specific settings
        if (source.IB != null)
        {
            IBHostBox.Text = source.IB.Host;
            IBPortBox.Value = source.IB.Port;
            IBClientIdBox.Value = source.IB.ClientId;
            IBPaperTradingCheck.IsChecked = source.IB.UsePaperTrading;
            IBSubscribeDepthCheck.IsChecked = source.IB.SubscribeDepth;
            IBTickByTickCheck.IsChecked = source.IB.TickByTick;
        }

        if (source.Alpaca != null)
        {
            SelectComboItemByTag(AlpacaFeedCombo, source.Alpaca.Feed ?? "iex");
            SelectComboItemByTag(AlpacaEnvironmentCombo, source.Alpaca.UseSandbox ? "true" : "false");
            AlpacaSubscribeQuotesCheck.IsChecked = source.Alpaca.SubscribeQuotes;
        }

        if (source.Polygon != null)
        {
            PolygonApiKeyBox.Password = source.Polygon.ApiKey ?? string.Empty;
            SelectComboItemByTag(PolygonFeedCombo, source.Polygon.Feed);
            PolygonDelayedCheck.IsChecked = source.Polygon.UseDelayed;
            PolygonTradesCheck.IsChecked = source.Polygon.SubscribeTrades;
            PolygonQuotesCheck.IsChecked = source.Polygon.SubscribeQuotes;
            PolygonAggregatesCheck.IsChecked = source.Polygon.SubscribeAggregates;
        }
    }

    private void ClearEditForm()
    {
        SourceNameBox.Text = string.Empty;
        ProviderCombo.SelectedIndex = 0;
        TypeCombo.SelectedIndex = 0;
        PriorityBox.Value = 100;
        DescriptionBox.Text = string.Empty;
        SymbolsBox.Text = string.Empty;

        // Reset IB settings
        IBHostBox.Text = "127.0.0.1";
        IBPortBox.Value = 7496;
        IBClientIdBox.Value = 0;
        IBPaperTradingCheck.IsChecked = false;
        IBSubscribeDepthCheck.IsChecked = true;
        IBTickByTickCheck.IsChecked = true;

        // Reset Alpaca settings
        AlpacaFeedCombo.SelectedIndex = 0;
        AlpacaEnvironmentCombo.SelectedIndex = 0;
        AlpacaSubscribeQuotesCheck.IsChecked = false;

        // Reset Polygon settings
        PolygonApiKeyBox.Password = string.Empty;
        PolygonFeedCombo.SelectedIndex = 0;
        PolygonDelayedCheck.IsChecked = false;
        PolygonTradesCheck.IsChecked = true;
        PolygonQuotesCheck.IsChecked = false;
        PolygonAggregatesCheck.IsChecked = false;

        UpdateProviderSettingsPanels("IB");
    }

    private void ProviderCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var provider = GetComboSelectedTag(ProviderCombo) ?? "IB";
        UpdateProviderSettingsPanels(provider);
    }

    private void UpdateProviderSettingsPanels(string provider)
    {
        IBSettingsPanel.Visibility = provider == "IB" ? Visibility.Visible : Visibility.Collapsed;
        AlpacaSettingsPanel.Visibility = provider == "Alpaca" ? Visibility.Visible : Visibility.Collapsed;
        PolygonSettingsPanel.Visibility = provider == "Polygon" ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void EnableFailoverToggle_Toggled(object sender, RoutedEventArgs e)
    {
        try
        {
            await _configService.UpdateFailoverSettingsAsync(
                EnableFailoverToggle.IsOn,
                (int)FailoverTimeoutBox.Value);
        }
        catch (Exception ex)
        {
            ShowError("Failed to update failover settings", ex.Message);
        }
    }

    private async void FailoverTimeoutBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (double.IsNaN(args.NewValue)) return;

        try
        {
            await _configService.UpdateFailoverSettingsAsync(
                EnableFailoverToggle.IsOn,
                (int)args.NewValue);
        }
        catch (Exception ex)
        {
            ShowError("Failed to update failover timeout", ex.Message);
        }
    }

    private async void DefaultRealTimeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DefaultRealTimeCombo.SelectedItem is DataSourceConfig source)
        {
            try
            {
                await _configService.SetDefaultDataSourceAsync(source.Id, isHistorical: false);
            }
            catch (Exception ex)
            {
                ShowError("Failed to set default real-time source", ex.Message);
            }
        }
    }

    private async void DefaultHistoricalCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DefaultHistoricalCombo.SelectedItem is DataSourceConfig source)
        {
            try
            {
                await _configService.SetDefaultDataSourceAsync(source.Id, isHistorical: true);
            }
            catch (Exception ex)
            {
                ShowError("Failed to set default historical source", ex.Message);
            }
        }
    }

    private void ShowSuccess(string message)
    {
        StatusInfoBar.Severity = InfoBarSeverity.Success;
        StatusInfoBar.Title = "Success";
        StatusInfoBar.Message = message;
        StatusInfoBar.IsOpen = true;
    }

    private void ShowError(string title, string message)
    {
        StatusInfoBar.Severity = InfoBarSeverity.Error;
        StatusInfoBar.Title = title;
        StatusInfoBar.Message = message;
        StatusInfoBar.IsOpen = true;
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
