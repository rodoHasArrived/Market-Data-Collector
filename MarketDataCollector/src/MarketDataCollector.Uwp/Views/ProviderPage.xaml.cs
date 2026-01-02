using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MarketDataCollector.Uwp.Models;
using MarketDataCollector.Uwp.Services;

namespace MarketDataCollector.Uwp.Views;

/// <summary>
/// Page for configuring data providers.
/// </summary>
public sealed partial class ProviderPage : Page
{
    private readonly ConfigService _configService;
    private string _selectedProvider = "IB";

    public ProviderPage()
    {
        this.InitializeComponent();
        _configService = new ConfigService();

        Loaded += ProviderPage_Loaded;
    }

    private async void ProviderPage_Loaded(object sender, RoutedEventArgs e)
    {
        var config = await _configService.LoadConfigAsync();
        if (config != null)
        {
            _selectedProvider = config.DataSource ?? "IB";

            if (_selectedProvider == "Alpaca")
            {
                AlpacaRadio.IsChecked = true;
            }
            else
            {
                IbRadio.IsChecked = true;
            }

            if (config.Alpaca != null)
            {
                AlpacaKeyIdBox.Text = config.Alpaca.KeyId ?? string.Empty;
                AlpacaSecretKeyBox.Password = config.Alpaca.SecretKey ?? string.Empty;
                AlpacaSubscribeQuotesCheck.IsChecked = config.Alpaca.SubscribeQuotes;

                SelectComboItemByTag(AlpacaFeedCombo, config.Alpaca.Feed ?? "iex");
                SelectComboItemByTag(AlpacaEnvironmentCombo, config.Alpaca.UseSandbox ? "true" : "false");
            }

            UpdateProviderUI();
        }
    }

    private void ProviderRadios_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IbRadio.IsChecked == true)
        {
            _selectedProvider = "IB";
        }
        else if (AlpacaRadio.IsChecked == true)
        {
            _selectedProvider = "Alpaca";
        }

        UpdateProviderUI();
    }

    private void UpdateProviderUI()
    {
        IbSettings.Visibility = _selectedProvider == "IB" ? Visibility.Visible : Visibility.Collapsed;
        AlpacaSettings.Visibility = _selectedProvider == "Alpaca" ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void SaveProvider_Click(object sender, RoutedEventArgs e)
    {
        SaveProgress.IsActive = true;
        try
        {
            await _configService.SaveDataSourceAsync(_selectedProvider);

            SaveInfoBar.Severity = InfoBarSeverity.Success;
            SaveInfoBar.Title = "Success";
            SaveInfoBar.Message = "Provider selection saved. Restart collector to apply changes.";
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

    private async void SaveAlpacaSettings_Click(object sender, RoutedEventArgs e)
    {
        AlpacaSaveProgress.IsActive = true;
        try
        {
            var options = new AlpacaOptions
            {
                KeyId = AlpacaKeyIdBox.Text,
                SecretKey = AlpacaSecretKeyBox.Password,
                Feed = GetComboSelectedTag(AlpacaFeedCombo) ?? "iex",
                UseSandbox = GetComboSelectedTag(AlpacaEnvironmentCombo) == "true",
                SubscribeQuotes = AlpacaSubscribeQuotesCheck.IsChecked ?? false
            };

            await _configService.SaveAlpacaOptionsAsync(options);

            SaveInfoBar.Severity = InfoBarSeverity.Success;
            SaveInfoBar.Title = "Success";
            SaveInfoBar.Message = "Alpaca settings saved. Restart collector to apply changes.";
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
            AlpacaSaveProgress.IsActive = false;
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
