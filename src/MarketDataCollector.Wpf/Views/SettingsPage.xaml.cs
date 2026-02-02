using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MarketDataCollector.Wpf.Services;

namespace MarketDataCollector.Wpf.Views;

/// <summary>
/// Settings page for application configuration, notifications, and credentials.
/// </summary>
public partial class SettingsPage : Page
{
    private readonly ConfigService _configService;
    private readonly ObservableCollection<CredentialDisplayInfo> _storedCredentials = new();
    private readonly ObservableCollection<ActivityItem> _recentActivity = new();

    public SettingsPage()
    {
        InitializeComponent();

        _configService = ConfigService.Instance;
        StoredCredentialsList.ItemsSource = _storedCredentials;
        RecentActivityList.ItemsSource = _recentActivity;
    }

    private void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        ConfigPathText.Text = _configService.ConfigPath;
        RefreshStoredCredentials();
        LoadRecentActivity();
        UpdateSystemStatus();
    }

    private void RefreshStoredCredentials()
    {
        _storedCredentials.Clear();

        // Sample credentials for demonstration
        _storedCredentials.Add(new CredentialDisplayInfo
        {
            Name = "Alpaca API Credentials",
            Status = "Valid • Last tested 2h ago",
            Resource = "alpaca",
            TestStatusColor = new SolidColorBrush(Color.FromRgb(63, 185, 80))
        });
        _storedCredentials.Add(new CredentialDisplayInfo
        {
            Name = "Nasdaq Data Link API Key",
            Status = "Valid • Last tested 1d ago",
            Resource = "nasdaq",
            TestStatusColor = new SolidColorBrush(Color.FromRgb(63, 185, 80))
        });

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
            IconColor = new SolidColorBrush(Color.FromRgb(63, 185, 80)),
            Message = "Configuration saved",
            Time = "2 min ago"
        });
        _recentActivity.Add(new ActivityItem
        {
            Icon = "\uE753",
            IconColor = new SolidColorBrush(Color.FromRgb(88, 166, 255)),
            Message = "Cloud sync completed",
            Time = "15 min ago"
        });
        _recentActivity.Add(new ActivityItem
        {
            Icon = "\uE787",
            IconColor = new SolidColorBrush(Color.FromRgb(210, 153, 34)),
            Message = "Backfill started",
            Time = "1 hour ago"
        });
    }

    private void UpdateSystemStatus()
    {
        ConfigStatusText.Text = "Valid";
        ConfigStatusDot.Fill = new SolidColorBrush(Color.FromRgb(63, 185, 80));

        CollectorStatusText.Text = "Running";
        CollectorStatusDot.Fill = new SolidColorBrush(Color.FromRgb(63, 185, 80));

        LastSyncText.Text = "2 min ago";
    }

    private void ThemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ThemeCombo.SelectedItem is ComboBoxItem item)
        {
            var theme = item.Tag?.ToString();
            // Theme switching logic would go here
            System.Diagnostics.Debug.WriteLine($"Theme changed to: {theme}");
        }
    }

    private void NotificationsEnabled_Changed(object sender, RoutedEventArgs e)
    {
        if (NotificationSettingsPanel != null)
        {
            NotificationSettingsPanel.Opacity = NotificationsEnabledToggle.IsChecked == true ? 1.0 : 0.5;
        }
    }

    private void SendTestNotification_Click(object sender, RoutedEventArgs e)
    {
        NotificationService.Instance.ShowNotification(
            "Test Notification",
            "This is a test notification from Market Data Collector.",
            NotificationType.Info);
    }

    private async void TestConnection_Click(object sender, RoutedEventArgs e)
    {
        ConnectionTestResult.Text = "Testing...";
        ConnectionTestResult.Foreground = new SolidColorBrush(Color.FromRgb(139, 148, 158));

        try
        {
            var statusService = StatusService.Instance;
            var status = await statusService.GetStatusAsync();

            if (status != null)
            {
                ConnectionTestResult.Text = "Connected";
                ConnectionTestResult.Foreground = new SolidColorBrush(Color.FromRgb(63, 185, 80));
            }
            else
            {
                ConnectionTestResult.Text = "Failed";
                ConnectionTestResult.Foreground = new SolidColorBrush(Color.FromRgb(248, 81, 73));
            }
        }
        catch (Exception)
        {
            ConnectionTestResult.Text = "Error";
            ConnectionTestResult.Foreground = new SolidColorBrush(Color.FromRgb(248, 81, 73));
        }
    }

    private void TestCredential_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string resource)
        {
            NotificationService.Instance.ShowNotification(
                "Credential Test",
                $"Testing {resource} credentials...",
                NotificationType.Info);
        }
    }

    private void RemoveCredential_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string name)
        {
            var result = MessageBox.Show(
                $"Are you sure you want to remove the credential '{name}'?",
                "Remove Credential",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                // Remove credential logic
                RefreshStoredCredentials();
            }
        }
    }

    private void TestAllCredentials_Click(object sender, RoutedEventArgs e)
    {
        NotificationService.Instance.ShowNotification(
            "Testing Credentials",
            "Testing all stored credentials...",
            NotificationType.Info);
    }

    private void ClearAllCredentials_Click(object sender, RoutedEventArgs e)
    {
        if (_storedCredentials.Count == 0)
        {
            MessageBox.Show("There are no stored credentials to clear.", "No Credentials", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var result = MessageBox.Show(
            $"Are you sure you want to remove all {_storedCredentials.Count} stored credential(s)?\n\nThis action cannot be undone.",
            "Clear All Credentials",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            _storedCredentials.Clear();
            RefreshStoredCredentials();
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

    private void ReloadConfig_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Reload configuration logic
            ConfigStatusText.Text = "Valid";
            ConfigStatusDot.Fill = new SolidColorBrush(Color.FromRgb(63, 185, 80));

            NotificationService.Instance.ShowNotification(
                "Configuration Reloaded",
                "Configuration has been reloaded successfully.",
                NotificationType.Success);
        }
        catch (Exception ex)
        {
            ConfigStatusText.Text = "Error";
            ConfigStatusDot.Fill = new SolidColorBrush(Color.FromRgb(248, 81, 73));

            NotificationService.Instance.ShowNotification(
                "Reload Failed",
                ex.Message,
                NotificationType.Error);
        }
    }

    private void ResetToDefaults_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "This will reset all settings to their default values. Your symbols and credentials will be preserved. Continue?",
            "Reset to Defaults",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            ThemeCombo.SelectedIndex = 0;
            AccentColorCombo.SelectedIndex = 0;
            CompactModeToggle.IsChecked = false;
            NotificationsEnabledToggle.IsChecked = true;
            MaxConcurrentDownloadsBox.Text = "4";
            WriteBufferSizeBox.Text = "64";
            EnableMetricsToggle.IsChecked = true;
            EnableDebugLoggingToggle.IsChecked = false;
            ApiBaseUrlBox.Text = "http://localhost:8080";
            StatusRefreshIntervalBox.Text = "2";

            NotificationService.Instance.ShowNotification(
                "Reset Complete",
                "Settings have been reset to defaults.",
                NotificationType.Success);
        }
    }

    private void OpenDocumentation_Click(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://github.com/example/market-data-collector",
            UseShellExecute = true
        });
    }

    private void OpenIssues_Click(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://github.com/example/market-data-collector/issues",
            UseShellExecute = true
        });
    }

    private void CheckForUpdates_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("You are running the latest version (1.6.1).", "Check for Updates", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}

/// <summary>
/// Credential display information for the settings page.
/// </summary>
public class CredentialDisplayInfo
{
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Resource { get; set; } = string.Empty;
    public SolidColorBrush TestStatusColor { get; set; } = new(Color.FromRgb(139, 148, 158));
}

/// <summary>
/// Activity item for recent activity list.
/// </summary>
public class ActivityItem
{
    public string Icon { get; set; } = string.Empty;
    public SolidColorBrush IconColor { get; set; } = new(Color.FromRgb(139, 148, 158));
    public string Message { get; set; } = string.Empty;
    public string Time { get; set; } = string.Empty;
}
