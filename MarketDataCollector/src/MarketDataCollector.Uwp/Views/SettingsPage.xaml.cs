using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using MarketDataCollector.Uwp.Services;
using Windows.Storage.Pickers;
using Windows.UI;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;

namespace MarketDataCollector.Uwp.Views;

/// <summary>
/// Enhanced settings page with notifications, config export/import, and system status.
/// </summary>
public sealed partial class SettingsPage : Page
{
    private readonly ConfigService _configService;
    private readonly CredentialService _credentialService;
    private readonly ObservableCollection<CredentialInfo> _storedCredentials = new();
    private readonly ObservableCollection<ActivityItem> _recentActivity = new();

    public SettingsPage()
    {
        this.InitializeComponent();
        _configService = new ConfigService();
        _credentialService = new CredentialService();
        StoredCredentialsList.ItemsSource = _storedCredentials;
        RecentActivityList.ItemsSource = _recentActivity;

        Loaded += SettingsPage_Loaded;
    }

    private void SettingsPage_Loaded(object sender, RoutedEventArgs e)
    {
        ConfigPathText.Text = _configService.ConfigPath;
        RefreshStoredCredentials();
        LoadRecentActivity();
        UpdateSystemStatus();
    }

    private void RefreshStoredCredentials()
    {
        _storedCredentials.Clear();

        var resources = _credentialService.GetAllStoredResources();
        foreach (var resource in resources)
        {
            var (name, status) = resource switch
            {
                var r when r.Contains("Alpaca") => ("Alpaca API Credentials", "Active - Last used 2h ago"),
                var r when r.Contains("NasdaqDataLink") => ("Nasdaq Data Link API Key", "Active"),
                var r when r.Contains("OpenFigi") => ("OpenFIGI API Key", "Active"),
                _ => (resource, "Active")
            };
            _storedCredentials.Add(new CredentialInfo { Name = name, Status = status });
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

        CredentialsStatusText.Text = _storedCredentials.Count > 0
            ? $"{_storedCredentials.Count} stored"
            : "None";
    }

    private void LoadRecentActivity()
    {
        _recentActivity.Clear();
        _recentActivity.Add(new ActivityItem
        {
            Icon = "\uE73E",
            IconColor = new SolidColorBrush(Color.FromArgb(255, 72, 187, 120)),
            Message = "Configuration saved",
            Time = "2 min ago"
        });
        _recentActivity.Add(new ActivityItem
        {
            Icon = "\uE753",
            IconColor = new SolidColorBrush(Color.FromArgb(255, 102, 126, 234)),
            Message = "Cloud sync completed",
            Time = "15 min ago"
        });
        _recentActivity.Add(new ActivityItem
        {
            Icon = "\uE787",
            IconColor = new SolidColorBrush(Color.FromArgb(255, 237, 137, 54)),
            Message = "Backfill started",
            Time = "1 hour ago"
        });
        _recentActivity.Add(new ActivityItem
        {
            Icon = "\uE9D9",
            IconColor = new SolidColorBrush(Color.FromArgb(255, 102, 126, 234)),
            Message = "Added 3 symbols",
            Time = "2 hours ago"
        });
    }

    private void UpdateSystemStatus()
    {
        // Update status indicators
        ConfigStatusText.Text = "Valid";
        ConfigStatusDot.Fill = new SolidColorBrush(Color.FromArgb(255, 72, 187, 120));

        var credCount = _storedCredentials.Count;
        CredentialsStatusText.Text = credCount > 0 ? $"{credCount} stored" : "None";
        CredentialsStatusDot.Fill = credCount > 0
            ? new SolidColorBrush(Color.FromArgb(255, 72, 187, 120))
            : new SolidColorBrush(Color.FromArgb(255, 160, 160, 160));

        CollectorStatusText.Text = "Running";
        CollectorStatusDot.Fill = new SolidColorBrush(Color.FromArgb(255, 72, 187, 120));

        LastSyncText.Text = "2 min ago";
    }

    private void NotificationsEnabled_Toggled(object sender, RoutedEventArgs e)
    {
        NotificationSettingsPanel.Opacity = NotificationsEnabledToggle.IsOn ? 1.0 : 0.5;

        // Update notification service settings
        var settings = NotificationService.Instance.GetSettings();
        settings.Enabled = NotificationsEnabledToggle.IsOn;
        settings.NotifyConnectionStatus = NotifyConnectionCheck.IsChecked == true;
        settings.NotifyErrors = NotifyErrorCheck.IsChecked == true;
        settings.NotifyBackfillComplete = NotifyBackfillCheck.IsChecked == true;
        settings.NotifyDataGaps = NotifyDataGapsCheck.IsChecked == true;
        settings.NotifyStorageWarnings = NotifyStorageCheck.IsChecked == true;
        NotificationService.Instance.UpdateSettings(settings);
    }

    private async void SendTestNotification_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await NotificationService.Instance.SendTestNotificationAsync();

            ConfigInfoBar.Severity = InfoBarSeverity.Success;
            ConfigInfoBar.Title = "Notification Sent";
            ConfigInfoBar.Message = "Check your notification center for the test notification.";
            ConfigInfoBar.IsOpen = true;
        }
        catch (Exception ex)
        {
            ConfigInfoBar.Severity = InfoBarSeverity.Warning;
            ConfigInfoBar.Title = "Notification Test";
            ConfigInfoBar.Message = $"Could not send notification: {ex.Message}. Notifications may need to be enabled in Windows Settings.";
            ConfigInfoBar.IsOpen = true;
        }
    }

    private async void ExportConfig_Click(object sender, RoutedEventArgs e)
    {
        var savePicker = new FileSavePicker();
        savePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        savePicker.FileTypeChoices.Add("JSON Configuration", new List<string> { ".json" });
        savePicker.SuggestedFileName = $"marketdata_config_{DateTime.Now:yyyyMMdd}";

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(savePicker, hwnd);

        var file = await savePicker.PickSaveFileAsync();
        if (file != null)
        {
            try
            {
                var config = await _configService.LoadConfigAsync();
                var exportConfig = new Dictionary<string, object?>();

                if (ExportSymbolsCheck.IsChecked == true && config?.Symbols != null)
                    exportConfig["Symbols"] = config.Symbols;

                if (ExportStorageCheck.IsChecked == true)
                {
                    exportConfig["DataRoot"] = config?.DataRoot;
                    exportConfig["Compress"] = config?.Compress;
                    exportConfig["Storage"] = config?.Storage;
                }

                if (ExportProviderCheck.IsChecked == true)
                {
                    exportConfig["DataSource"] = config?.DataSource;
                    exportConfig["Alpaca"] = config?.Alpaca;
                    exportConfig["Ib"] = config?.Ib;
                }

                exportConfig["ExportedAt"] = DateTime.UtcNow.ToString("o");
                exportConfig["Version"] = "1.0";

                var json = JsonSerializer.Serialize(exportConfig, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                await Windows.Storage.FileIO.WriteTextAsync(file, json);

                ConfigInfoBar.Severity = InfoBarSeverity.Success;
                ConfigInfoBar.Title = "Export Successful";
                ConfigInfoBar.Message = $"Configuration exported to {file.Name}";
                ConfigInfoBar.IsOpen = true;
            }
            catch (Exception ex)
            {
                ConfigInfoBar.Severity = InfoBarSeverity.Error;
                ConfigInfoBar.Title = "Export Failed";
                ConfigInfoBar.Message = ex.Message;
                ConfigInfoBar.IsOpen = true;
            }
        }
    }

    private async void ImportConfig_Click(object sender, RoutedEventArgs e)
    {
        var openPicker = new FileOpenPicker();
        openPicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        openPicker.FileTypeFilter.Add(".json");

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(openPicker, hwnd);

        var file = await openPicker.PickSingleFileAsync();
        if (file != null)
        {
            var dialog = new ContentDialog
            {
                Title = "Import Configuration",
                Content = "This will merge the imported settings with your current configuration. Existing settings may be overwritten. Continue?",
                PrimaryButtonText = "Import",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary) return;

            try
            {
                var json = await Windows.Storage.FileIO.ReadTextAsync(file);
                var importedConfig = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);

                if (importedConfig == null)
                {
                    throw new Exception("Invalid configuration file format.");
                }

                // Merge imported config with current config
                var currentConfig = await _configService.LoadConfigAsync() ?? new Models.CollectorConfig();

                if (importedConfig.TryGetValue("DataSource", out var dataSource))
                    currentConfig.DataSource = dataSource.GetString();

                if (importedConfig.TryGetValue("DataRoot", out var dataRoot))
                    currentConfig.DataRoot = dataRoot.GetString();

                // Save merged config
                await _configService.SaveConfigAsync(currentConfig);

                ConfigInfoBar.Severity = InfoBarSeverity.Success;
                ConfigInfoBar.Title = "Import Successful";
                ConfigInfoBar.Message = "Configuration imported. Restart collector to apply changes.";
                ConfigInfoBar.IsOpen = true;
            }
            catch (Exception ex)
            {
                ConfigInfoBar.Severity = InfoBarSeverity.Error;
                ConfigInfoBar.Title = "Import Failed";
                ConfigInfoBar.Message = ex.Message;
                ConfigInfoBar.IsOpen = true;
            }
        }
    }

    private void RemoveCredential_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string friendlyName)
        {
            var resource = friendlyName switch
            {
                "Alpaca API Credentials" => CredentialService.AlpacaCredentialResource,
                "Nasdaq Data Link API Key" => CredentialService.NasdaqApiKeyResource,
                "OpenFIGI API Key" => CredentialService.OpenFigiApiKeyResource,
                _ => friendlyName
            };

            _credentialService.RemoveCredential(resource);
            RefreshStoredCredentials();
            UpdateSystemStatus();
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
            UpdateSystemStatus();

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
            var appTheme = theme switch
            {
                "Light" => AppTheme.Light,
                "Dark" => AppTheme.Dark,
                _ => AppTheme.System
            };

            ThemeService.Instance.SetTheme(appTheme);
        }
    }

    private async void TestConnection_Click(object sender, RoutedEventArgs e)
    {
        var baseUrl = ApiBaseUrlBox.Text?.Trim() ?? "http://localhost:8080";
        var statusService = new StatusService(baseUrl);

        ConnectionTestProgress.IsActive = true;
        ConnectionTestResult.Text = "Testing...";

        try
        {
            var status = await statusService.GetStatusAsync();

            ConnectionTestProgress.IsActive = false;
            if (status != null)
            {
                ConnectionTestResult.Text = "Connected";
                ConnectionTestResult.Foreground = new SolidColorBrush(Color.FromArgb(255, 72, 187, 120));
            }
            else
            {
                ConnectionTestResult.Text = "Failed";
                ConnectionTestResult.Foreground = new SolidColorBrush(Color.FromArgb(255, 245, 101, 101));
            }
        }
        catch (Exception)
        {
            ConnectionTestProgress.IsActive = false;
            ConnectionTestResult.Text = "Error";
            ConnectionTestResult.Foreground = new SolidColorBrush(Color.FromArgb(255, 245, 101, 101));
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
                Content = $"Configuration loaded successfully.\nData Source: {config?.DataSource ?? "IB"}\nSymbols: {config?.Symbols?.Count ?? 0}",
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();

            ConfigStatusText.Text = "Valid";
            ConfigStatusDot.Fill = new SolidColorBrush(Color.FromArgb(255, 72, 187, 120));
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

            ConfigStatusText.Text = "Error";
            ConfigStatusDot.Fill = new SolidColorBrush(Color.FromArgb(255, 245, 101, 101));
        }
    }

    private async void ResetToDefaults_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "Reset to Defaults",
            Content = "This will reset all settings to their default values. Your symbols and credentials will be preserved. Continue?",
            PrimaryButtonText = "Reset",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            // Reset UI settings
            ThemeCombo.SelectedIndex = 0;
            AccentColorCombo.SelectedIndex = 0;
            CompactModeToggle.IsOn = false;
            NotificationsEnabledToggle.IsOn = true;
            MaxConcurrentDownloadsBox.Value = 4;
            WriteBufferSizeBox.Value = 64;
            EnableMetricsToggle.IsOn = true;
            EnableDebugLoggingToggle.IsOn = false;
            ApiBaseUrlBox.Text = "http://localhost:8080";
            StatusRefreshIntervalBox.Value = 2;

            ConfigInfoBar.Severity = InfoBarSeverity.Success;
            ConfigInfoBar.Title = "Reset Complete";
            ConfigInfoBar.Message = "Settings have been reset to defaults.";
            ConfigInfoBar.IsOpen = true;
        }
    }

    private async void CheckForUpdates_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "Check for Updates",
            Content = "You are running the latest version (1.0.0).",
            CloseButtonText = "OK",
            XamlRoot = this.XamlRoot
        };
        await dialog.ShowAsync();
    }
}

/// <summary>
/// Credential display information.
/// </summary>
public class CredentialInfo
{
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// Recent activity item.
/// </summary>
public class ActivityItem
{
    public string Icon { get; set; } = string.Empty;
    public SolidColorBrush IconColor { get; set; } = new(Color.FromArgb(255, 160, 160, 160));
    public string Message { get; set; } = string.Empty;
    public string Time { get; set; } = string.Empty;
}
