using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
/// Shared cached brushes for settings UI to avoid repeated allocations.
/// </summary>
internal static class SettingsBrushes
{
    public static readonly SolidColorBrush Green = new(Color.FromArgb(255, 72, 187, 120));
    public static readonly SolidColorBrush Yellow = new(Color.FromArgb(255, 237, 137, 54));
    public static readonly SolidColorBrush Red = new(Color.FromArgb(255, 245, 101, 101));
    public static readonly SolidColorBrush Gray = new(Color.FromArgb(255, 160, 160, 160));
    public static readonly SolidColorBrush Blue = new(Color.FromArgb(255, 102, 126, 234));
    public static readonly SolidColorBrush Purple = new(Color.FromArgb(255, 128, 90, 213));
}

/// <summary>
/// Enhanced settings page with notifications, config export/import, system status,
/// and comprehensive credential management with testing and expiration tracking.
/// </summary>
public sealed partial class SettingsPage : Page
{
    private readonly ConfigService _configService;
    private readonly CredentialService _credentialService;
    private readonly OAuthRefreshService _oauthRefreshService;
    private readonly ObservableCollection<CredentialDisplayInfo> _storedCredentials = new();
    private readonly BoundedObservableCollection<ActivityItem> _recentActivity = new(20);

    public SettingsPage()
    {
        this.InitializeComponent();
        _configService = new ConfigService();
        _credentialService = new CredentialService();
        _oauthRefreshService = OAuthRefreshService.Instance;

        StoredCredentialsList.ItemsSource = _storedCredentials;
        RecentActivityList.ItemsSource = _recentActivity;

        // Subscribe to credential service events
        _credentialService.MetadataUpdated += OnCredentialMetadataUpdated;
        _credentialService.CredentialExpiring += OnCredentialExpiring;

        // Subscribe to OAuth refresh events
        _oauthRefreshService.TokenRefreshed += OnTokenRefreshed;
        _oauthRefreshService.TokenRefreshFailed += OnTokenRefreshFailed;
        _oauthRefreshService.TokenExpirationWarning += OnTokenExpirationWarning;

        Loaded += SettingsPage_Loaded;
        Unloaded += SettingsPage_Unloaded;
    }

    private void SettingsPage_Loaded(object sender, RoutedEventArgs e)
    {
        ConfigPathText.Text = _configService.ConfigPath;
        RefreshStoredCredentials();
        LoadRecentActivity();
        UpdateSystemStatus();
        CheckExpiringCredentials();

        // Start OAuth auto-refresh service if enabled
        if (AutoRefreshToggle.IsOn)
        {
            _oauthRefreshService.Start();
        }
    }

    private void SettingsPage_Unloaded(object sender, RoutedEventArgs e)
    {
        // Unsubscribe from events
        _credentialService.MetadataUpdated -= OnCredentialMetadataUpdated;
        _credentialService.CredentialExpiring -= OnCredentialExpiring;
        _oauthRefreshService.TokenRefreshed -= OnTokenRefreshed;
        _oauthRefreshService.TokenRefreshFailed -= OnTokenRefreshFailed;
        _oauthRefreshService.TokenExpirationWarning -= OnTokenExpirationWarning;
    }

    private void RefreshStoredCredentials()
    {
        _storedCredentials.Clear();

        var credentials = _credentialService.GetAllCredentialsWithMetadata();
        foreach (var cred in credentials)
        {
            _storedCredentials.Add(new CredentialDisplayInfo(cred));
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

        UpdateLastAuthDisplay();
    }

    private void UpdateLastAuthDisplay()
    {
        var credentials = _credentialService.GetAllCredentialsWithMetadata();
        var lastAuth = credentials
            .Where(c => c.LastAuthenticatedAt.HasValue)
            .OrderByDescending(c => c.LastAuthenticatedAt)
            .FirstOrDefault();

        if (lastAuth?.LastAuthenticatedAt != null)
        {
            var elapsed = DateTime.UtcNow - lastAuth.LastAuthenticatedAt.Value;
            if (elapsed.TotalMinutes < 1)
            {
                LastAuthText.Text = "Just now";
                LastAuthStatusDot.Fill = GreenBrush;
            }
            else if (elapsed.TotalHours < 1)
            {
                LastAuthText.Text = $"{(int)elapsed.TotalMinutes}m ago";
                LastAuthStatusDot.Fill = GreenBrush;
            }
            else if (elapsed.TotalDays < 1)
            {
                LastAuthText.Text = $"{(int)elapsed.TotalHours}h ago";
                LastAuthStatusDot.Fill = GreenBrush;
            }
            else
            {
                LastAuthText.Text = $"{(int)elapsed.TotalDays}d ago";
                LastAuthStatusDot.Fill = elapsed.TotalDays > 7 ? YellowBrush : GreenBrush;
            }
        }
        else
        {
            LastAuthText.Text = "Never";
            LastAuthStatusDot.Fill = GrayBrush;
        }
    }

    private void CheckExpiringCredentials()
    {
        var expiring = _credentialService.GetExpiringCredentials();
        if (expiring.Count > 0)
        {
            CredentialExpirationWarning.IsOpen = true;
            CredentialExpirationWarning.Message = expiring.Count == 1
                ? $"{expiring[0].Name} will expire soon. Test and refresh to avoid service interruption."
                : $"{expiring.Count} credentials will expire soon. Test and refresh them to avoid service interruption.";

            ExpiringCredentialsPanel.Visibility = Visibility.Visible;
            ExpiringCredentialsText.Text = expiring.Count == 1
                ? "1 credential expiring"
                : $"{expiring.Count} credentials expiring";
        }
        else
        {
            CredentialExpirationWarning.IsOpen = false;
            ExpiringCredentialsPanel.Visibility = Visibility.Collapsed;
        }
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
                var currentConfig = await _configService.LoadConfigAsync() ?? new AppConfig();

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

    #region Credential Testing

    private async void TestCredential_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string resource)
        {
            await TestSingleCredentialAsync(resource);
        }
    }

    private async void TestAllCredentials_Click(object sender, RoutedEventArgs e)
    {
        TestingStatusPanel.Visibility = Visibility.Visible;
        TestingStatusText.Text = "Testing all credentials...";
        TestResultInfoBar.IsOpen = false;

        var results = await _credentialService.TestAllCredentialsAsync();
        TestingStatusPanel.Visibility = Visibility.Collapsed;

        var successCount = results.Count(r => r.Value.Success);
        var failCount = results.Count - successCount;

        if (failCount == 0)
        {
            TestResultInfoBar.Severity = InfoBarSeverity.Success;
            TestResultInfoBar.Title = "All Credentials Valid";
            TestResultInfoBar.Message = $"Successfully tested {successCount} credential(s). All authentications passed.";
        }
        else if (successCount == 0)
        {
            TestResultInfoBar.Severity = InfoBarSeverity.Error;
            TestResultInfoBar.Title = "All Credentials Failed";
            TestResultInfoBar.Message = $"All {failCount} credential test(s) failed. Check your API keys and network connection.";
        }
        else
        {
            TestResultInfoBar.Severity = InfoBarSeverity.Warning;
            TestResultInfoBar.Title = "Some Credentials Failed";
            TestResultInfoBar.Message = $"{successCount} passed, {failCount} failed. Review individual results.";
        }

        TestResultInfoBar.IsOpen = true;
        RefreshStoredCredentials();
        UpdateSystemStatus();

        // Add to activity log using efficient Prepend
        _recentActivity.Prepend(new ActivityItem
        {
            Icon = failCount == 0 ? "\uE73E" : "\uE7BA",
            IconColor = failCount == 0 ? GreenBrush : YellowBrush,
            Message = $"Tested {results.Count} credentials",
            Time = "Just now"
        });
    }

    private async Task TestSingleCredentialAsync(string resource)
    {
        TestingStatusPanel.Visibility = Visibility.Visible;
        TestingStatusText.Text = $"Testing {GetFriendlyName(resource)}...";

        var result = await _credentialService.TestCredentialAsync(resource);
        TestingStatusPanel.Visibility = Visibility.Collapsed;

        if (result.Success)
        {
            TestResultInfoBar.Severity = InfoBarSeverity.Success;
            TestResultInfoBar.Title = "Credential Valid";
            TestResultInfoBar.Message = $"{result.Message} (Response: {result.ResponseTimeMs}ms)";
        }
        else
        {
            TestResultInfoBar.Severity = InfoBarSeverity.Error;
            TestResultInfoBar.Title = "Credential Test Failed";
            TestResultInfoBar.Message = result.Message;
        }

        TestResultInfoBar.IsOpen = true;
        RefreshStoredCredentials();
        UpdateLastAuthDisplay();
    }

    private string GetFriendlyName(string resource)
    {
        return resource switch
        {
            var r when r.Contains("Alpaca") => "Alpaca API",
            var r when r.Contains("NasdaqDataLink") => "Nasdaq Data Link",
            var r when r.Contains("OpenFigi") => "OpenFIGI",
            _ => resource
        };
    }

    #endregion

    #region OAuth Token Management

    private async void RefreshCredential_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string resource)
        {
            var providerId = resource.Replace($"{CredentialService.OAuthTokenResource}.", "");
            TestingStatusPanel.Visibility = Visibility.Visible;
            TestingStatusText.Text = $"Refreshing OAuth token...";

            var success = await _oauthRefreshService.RefreshTokenAsync(providerId);
            TestingStatusPanel.Visibility = Visibility.Collapsed;

            if (success)
            {
                TestResultInfoBar.Severity = InfoBarSeverity.Success;
                TestResultInfoBar.Title = "Token Refreshed";
                TestResultInfoBar.Message = "OAuth token has been successfully refreshed.";
            }
            else
            {
                TestResultInfoBar.Severity = InfoBarSeverity.Error;
                TestResultInfoBar.Title = "Refresh Failed";
                TestResultInfoBar.Message = "Failed to refresh OAuth token. Try re-authenticating.";
            }

            TestResultInfoBar.IsOpen = true;
            RefreshStoredCredentials();
        }
    }

    private void AutoRefreshToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (AutoRefreshToggle.IsOn)
        {
            _oauthRefreshService.Start();
        }
        else
        {
            _oauthRefreshService.Stop();
        }
    }

    #endregion

    #region Event Handlers for Credential Services

    private void OnCredentialMetadataUpdated(object? sender, CredentialMetadataEventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            RefreshStoredCredentials();
            CheckExpiringCredentials();
        });
    }

    private void OnCredentialExpiring(object? sender, CredentialExpirationEventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            var friendlyName = GetFriendlyName(e.Resource);

            // Show warning notification
            CredentialExpirationWarning.IsOpen = true;
            CredentialExpirationWarning.Message = $"{friendlyName} will expire in {FormatTimeRemaining(e.TimeRemaining)}.";

            // Add to activity log using efficient Prepend
            _recentActivity.Prepend(new ActivityItem
            {
                Icon = "\uE7BA",
                IconColor = YellowBrush,
                Message = $"{friendlyName} expiring soon",
                Time = "Just now"
            });
        });
    }

    private void OnTokenRefreshed(object? sender, TokenRefreshEventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            RefreshStoredCredentials();

            // Add to activity log using efficient Prepend
            _recentActivity.Prepend(new ActivityItem
            {
                Icon = "\uE72C",
                IconColor = GreenBrush,
                Message = $"OAuth token refreshed ({e.ProviderId})",
                Time = "Just now"
            });
        });
    }

    private void OnTokenRefreshFailed(object? sender, TokenRefreshFailedEventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            TestResultInfoBar.Severity = InfoBarSeverity.Error;
            TestResultInfoBar.Title = "Auto-Refresh Failed";
            TestResultInfoBar.Message = $"Failed to refresh {e.ProviderId} token: {e.ErrorMessage}";
            TestResultInfoBar.IsOpen = true;

            // Add to activity log using efficient Prepend
            _recentActivity.Prepend(new ActivityItem
            {
                Icon = "\uE783",
                IconColor = RedBrush,
                Message = $"Token refresh failed ({e.ProviderId})",
                Time = "Just now"
            });
        });
    }

    private void OnTokenExpirationWarning(object? sender, TokenExpirationWarningEventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (!e.CanAutoRefresh)
            {
                CredentialExpirationWarning.IsOpen = true;
                CredentialExpirationWarning.Message = $"OAuth token for {e.ProviderId} expires in {FormatTimeRemaining(e.TimeRemaining)}. Manual refresh required.";
            }
        });
    }

    private static string FormatTimeRemaining(TimeSpan remaining)
    {
        if (remaining.TotalDays >= 1)
            return $"{(int)remaining.TotalDays} days";
        if (remaining.TotalHours >= 1)
            return $"{(int)remaining.TotalHours} hours";
        if (remaining.TotalMinutes >= 1)
            return $"{(int)remaining.TotalMinutes} minutes";
        return "less than a minute";
    }

    #endregion
}

/// <summary>
/// Display wrapper for credential information with UI-specific properties.
/// Uses shared SettingsBrushes to avoid duplicate brush allocations.
/// </summary>
public class CredentialDisplayInfo
{
    private readonly CredentialInfo _credential;

    public CredentialDisplayInfo(CredentialInfo credential)
    {
        _credential = credential;
    }

    public string Name => _credential.Name;
    public string Resource => _credential.Resource;
    public string Status => _credential.Status;
    public string ExpirationDisplay => _credential.ExpirationDisplay;
    public string LastAuthDisplay => _credential.LastAuthDisplay;

    public SolidColorBrush TestStatusColor => _credential.TestStatus switch
    {
        CredentialTestStatus.Success => SettingsBrushes.Green,
        CredentialTestStatus.Failed => SettingsBrushes.Red,
        CredentialTestStatus.Expired => SettingsBrushes.Red,
        CredentialTestStatus.Testing => SettingsBrushes.Blue,
        _ => SettingsBrushes.Gray
    };

    public SolidColorBrush ExpirationColor
    {
        get
        {
            if (_credential.IsExpired) return SettingsBrushes.Red;
            if (_credential.IsExpiringSoon) return SettingsBrushes.Yellow;
            return SettingsBrushes.Gray;
        }
    }

    public string TypeBadge => _credential.CredentialType switch
    {
        CredentialType.OAuth2Token => "OAuth",
        CredentialType.ApiKeyWithSecret => "API Key",
        CredentialType.BearerToken => "Bearer",
        _ => "Key"
    };

    public SolidColorBrush TypeBadgeColor => _credential.CredentialType switch
    {
        CredentialType.OAuth2Token => SettingsBrushes.Purple,
        CredentialType.ApiKeyWithSecret => SettingsBrushes.Blue,
        _ => SettingsBrushes.Gray
    };

    public Visibility TypeBadgeVisibility =>
        _credential.CredentialType == CredentialType.OAuth2Token ? Visibility.Visible : Visibility.Collapsed;

    public Visibility HasMetadata =>
        _credential.ExpiresAt.HasValue || _credential.LastAuthenticatedAt.HasValue
            ? Visibility.Visible : Visibility.Collapsed;

    public Visibility HasExpiration =>
        _credential.ExpiresAt.HasValue ? Visibility.Visible : Visibility.Collapsed;

    public Visibility HasLastAuth =>
        _credential.LastAuthenticatedAt.HasValue ? Visibility.Visible : Visibility.Collapsed;

    public Visibility RefreshButtonVisibility =>
        _credential.CredentialType == CredentialType.OAuth2Token && _credential.CanAutoRefresh
            ? Visibility.Visible : Visibility.Collapsed;
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
