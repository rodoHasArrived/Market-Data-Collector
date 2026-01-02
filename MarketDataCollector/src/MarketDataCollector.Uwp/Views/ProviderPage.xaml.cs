using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MarketDataCollector.Uwp.Models;
using MarketDataCollector.Uwp.Services;

namespace MarketDataCollector.Uwp.Views;

/// <summary>
/// Page for configuring data providers with secure credential management.
/// </summary>
public sealed partial class ProviderPage : Page
{
    private readonly ConfigService _configService;
    private readonly CredentialService _credentialService;
    private string _selectedProvider = "IB";

    public ProviderPage()
    {
        this.InitializeComponent();
        _configService = new ConfigService();
        _credentialService = new CredentialService();

        Loaded += ProviderPage_Loaded;
    }

    private async void ProviderPage_Loaded(object sender, RoutedEventArgs e)
    {
        // Load config for non-credential settings
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
                AlpacaSubscribeQuotesCheck.IsChecked = config.Alpaca.SubscribeQuotes;
                SelectComboItemByTag(AlpacaFeedCombo, config.Alpaca.Feed ?? "iex");
                SelectComboItemByTag(AlpacaEnvironmentCombo, config.Alpaca.UseSandbox ? "true" : "false");
            }

            UpdateProviderUI();
        }

        // Update credential status
        UpdateCredentialStatus();
    }

    private void UpdateCredentialStatus()
    {
        if (_credentialService.HasAlpacaCredentials())
        {
            var credentials = _credentialService.GetAlpacaCredentials();
            if (credentials.HasValue)
            {
                var maskedKey = MaskCredential(credentials.Value.KeyId);
                CredentialStatusText.Text = $"Stored: {maskedKey}";
                SetCredentialsButton.Content = "Update Credentials";
                ClearCredentialsButton.Visibility = Visibility.Visible;
            }
        }
        else
        {
            CredentialStatusText.Text = "No credentials stored";
            SetCredentialsButton.Content = "Set Credentials";
            ClearCredentialsButton.Visibility = Visibility.Collapsed;
        }
    }

    private static string MaskCredential(string credential)
    {
        if (string.IsNullOrEmpty(credential) || credential.Length <= 8)
        {
            return "****";
        }
        return credential.Substring(0, 4) + "..." + credential.Substring(credential.Length - 4);
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

    private async void SetAlpacaCredentials_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var result = await _credentialService.PromptForAlpacaCredentialsAsync();
            if (result.HasValue)
            {
                UpdateCredentialStatus();

                SaveInfoBar.Severity = InfoBarSeverity.Success;
                SaveInfoBar.Title = "Credentials Saved";
                SaveInfoBar.Message = "Alpaca API credentials have been securely stored in Windows Credential Manager.";
                SaveInfoBar.IsOpen = true;
            }
        }
        catch (Exception ex)
        {
            SaveInfoBar.Severity = InfoBarSeverity.Error;
            SaveInfoBar.Title = "Error";
            SaveInfoBar.Message = $"Failed to save credentials: {ex.Message}";
            SaveInfoBar.IsOpen = true;
        }
    }

    private void ClearAlpacaCredentials_Click(object sender, RoutedEventArgs e)
    {
        _credentialService.RemoveAlpacaCredentials();
        UpdateCredentialStatus();

        SaveInfoBar.Severity = InfoBarSeverity.Informational;
        SaveInfoBar.Title = "Credentials Removed";
        SaveInfoBar.Message = "Alpaca API credentials have been removed from Windows Credential Manager.";
        SaveInfoBar.IsOpen = true;
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
            // Save non-credential settings only (credentials are in Credential Manager)
            var options = new AlpacaOptions
            {
                // Don't store credentials in config - they're in Credential Manager
                KeyId = null,
                SecretKey = null,
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
