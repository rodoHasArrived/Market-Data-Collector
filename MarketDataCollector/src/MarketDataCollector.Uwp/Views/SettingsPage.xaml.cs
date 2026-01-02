using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MarketDataCollector.Uwp.Services;
using System.Diagnostics;

namespace MarketDataCollector.Uwp.Views;

/// <summary>
/// Settings page for application configuration and credential management.
/// </summary>
public sealed partial class SettingsPage : Page
{
    private readonly ConfigService _configService;
    private readonly CredentialService _credentialService;
    private readonly ObservableCollection<string> _storedCredentials = new();

    public SettingsPage()
    {
        this.InitializeComponent();
        _configService = new ConfigService();
        _credentialService = new CredentialService();
        StoredCredentialsList.ItemsSource = _storedCredentials;

        Loaded += SettingsPage_Loaded;
    }

    private void SettingsPage_Loaded(object sender, RoutedEventArgs e)
    {
        ConfigPathText.Text = _configService.ConfigPath;
        RefreshStoredCredentials();
    }

    private void RefreshStoredCredentials()
    {
        _storedCredentials.Clear();

        var resources = _credentialService.GetAllStoredResources();
        foreach (var resource in resources)
        {
            // Show a friendly name instead of the full resource name
            var friendlyName = resource switch
            {
                var r when r.Contains("Alpaca") => "Alpaca API Credentials",
                var r when r.Contains("NasdaqDataLink") => "Nasdaq Data Link API Key",
                var r when r.Contains("OpenFigi") => "OpenFIGI API Key",
                _ => resource
            };
            _storedCredentials.Add(friendlyName);
        }

        if (_storedCredentials.Count == 0)
        {
            StoredCredentialsList.Visibility = Visibility.Collapsed;
            NoCredentialsText.Visibility = Visibility.Visible;
        }
        else
        {
            StoredCredentialsList.Visibility = Visibility.Visible;
            NoCredentialsText.Visibility = Visibility.Collapsed;
        }
    }

    private void RemoveCredential_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string friendlyName)
        {
            // Map friendly name back to resource name
            var resource = friendlyName switch
            {
                "Alpaca API Credentials" => CredentialService.AlpacaCredentialResource,
                "Nasdaq Data Link API Key" => CredentialService.NasdaqApiKeyResource,
                "OpenFIGI API Key" => CredentialService.OpenFigiApiKeyResource,
                _ => friendlyName
            };

            _credentialService.RemoveCredential(resource);
            RefreshStoredCredentials();
        }
    }

    private async void ClearAllCredentials_Click(object sender, RoutedEventArgs e)
    {
        if (_storedCredentials.Count == 0)
        {
            var infoDialog = new ContentDialog
            {
                Title = "No Credentials",
                Content = "There are no stored credentials to clear.",
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            await infoDialog.ShowAsync();
            return;
        }

        var dialog = new ContentDialog
        {
            Title = "Clear All Credentials",
            Content = $"Are you sure you want to remove all {_storedCredentials.Count} stored credential(s)?\n\nThis action cannot be undone.",
            PrimaryButtonText = "Clear All",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            _credentialService.RemoveAllCredentials();
            RefreshStoredCredentials();

            var successDialog = new ContentDialog
            {
                Title = "Credentials Cleared",
                Content = "All stored credentials have been removed from Windows Credential Manager.",
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            await successDialog.ShowAsync();
        }
    }

    private void ThemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ThemeCombo.SelectedItem is ComboBoxItem item)
        {
            var theme = item.Tag?.ToString();
            var requestedTheme = theme switch
            {
                "Light" => ElementTheme.Light,
                "Dark" => ElementTheme.Dark,
                _ => ElementTheme.Default
            };

            if (App.MainWindow?.Content is FrameworkElement rootElement)
            {
                rootElement.RequestedTheme = requestedTheme;
            }
        }
    }

    private async void TestConnection_Click(object sender, RoutedEventArgs e)
    {
        var baseUrl = ApiBaseUrlBox.Text?.Trim() ?? "http://localhost:8080";
        var statusService = new StatusService(baseUrl);

        try
        {
            var status = await statusService.GetStatusAsync();
            var message = status != null
                ? $"Connection successful! Status: {(status.IsConnected ? "Connected" : "Disconnected")}"
                : "Connection failed. The collector service may not be running.";

            var dialog = new ContentDialog
            {
                Title = status != null ? "Success" : "Connection Failed",
                Content = message,
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
        }
        catch (Exception ex)
        {
            var dialog = new ContentDialog
            {
                Title = "Connection Error",
                Content = $"Failed to connect: {ex.Message}",
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
        }
    }

    private void OpenConfigFolder_Click(object sender, RoutedEventArgs e)
    {
        var folder = Path.GetDirectoryName(_configService.ConfigPath);
        if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = folder,
                UseShellExecute = true
            });
        }
    }

    private async void ReloadConfig_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var config = await _configService.LoadConfigAsync();
            var dialog = new ContentDialog
            {
                Title = "Configuration Reloaded",
                Content = $"Configuration loaded successfully.\nData Source: {config?.DataSource ?? "IB"}\nSymbols: {config?.Symbols?.Length ?? 0}",
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
        }
        catch (Exception ex)
        {
            var dialog = new ContentDialog
            {
                Title = "Error",
                Content = $"Failed to load configuration: {ex.Message}",
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
        }
    }
}
