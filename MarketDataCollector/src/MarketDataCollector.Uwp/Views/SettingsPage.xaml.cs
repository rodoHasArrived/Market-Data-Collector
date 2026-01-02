using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MarketDataCollector.Uwp.Services;
using System.Diagnostics;

namespace MarketDataCollector.Uwp.Views;

/// <summary>
/// Settings page for application configuration.
/// </summary>
public sealed partial class SettingsPage : Page
{
    private readonly ConfigService _configService;

    public SettingsPage()
    {
        this.InitializeComponent();
        _configService = new ConfigService();

        Loaded += SettingsPage_Loaded;
    }

    private void SettingsPage_Loaded(object sender, RoutedEventArgs e)
    {
        ConfigPathText.Text = _configService.ConfigPath;
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
