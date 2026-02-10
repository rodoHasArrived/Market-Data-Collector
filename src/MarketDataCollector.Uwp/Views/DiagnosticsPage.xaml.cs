using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.System;
using MarketDataCollector.Uwp.Services;

namespace MarketDataCollector.Uwp.Views;

/// <summary>
/// Page for system diagnostics including preflight checks, dry-run, and diagnostic bundles.
/// </summary>
public sealed partial class DiagnosticsPage : Page
{
    private readonly DiagnosticsService _diagnosticsService;

    public DiagnosticsPage()
    {
        this.InitializeComponent();
        _diagnosticsService = DiagnosticsService.Instance;
        Loaded += DiagnosticsPage_Loaded;
    }

    private async void DiagnosticsPage_Loaded(object sender, RoutedEventArgs e)
    {
        await RefreshMetricsAsync();
    }

    private async void RunPreflight_Click(object sender, RoutedEventArgs e)
    {
        PreflightProgress.IsActive = true;
        PreflightButton.IsEnabled = false;

        try
        {
            var result = await _diagnosticsService.RunPreflightCheckAsync();
            ShowPreflightResults(result);
        }
        catch (Exception ex)
        {
            ShowPreflightError(ex.Message);
        }
        finally
        {
            PreflightProgress.IsActive = false;
            PreflightButton.IsEnabled = true;
        }
    }

    private void ShowPreflightResults(PreflightResult result)
    {
        PreflightResultsCard.Visibility = Visibility.Visible;

        PreflightResultIcon.Glyph = result.Success ? "\uE73E" : "\uEA39";
        PreflightResultIcon.Foreground = new SolidColorBrush(
            result.Success ? Windows.UI.Color.FromArgb(255, 72, 187, 120) : Windows.UI.Color.FromArgb(255, 245, 101, 101));

        PreflightPassedText.Text = $"{result.PassedCount} passed";
        PreflightFailedText.Text = $"{result.FailedCount} failed";

        var displayItems = result.Checks.Select(c => new PreflightCheckDisplay
        {
            Name = c.Name,
            Category = c.Category,
            Message = c.Message,
            Passed = c.Passed,
            StatusIcon = c.Passed ? "\uE73E" : (c.Severity == CheckSeverity.Warning ? "\uE7BA" : "\uEA39"),
            StatusColor = new SolidColorBrush(c.Passed
                ? Windows.UI.Color.FromArgb(255, 72, 187, 120)
                : (c.Severity == CheckSeverity.Warning
                    ? Windows.UI.Color.FromArgb(255, 237, 137, 54)
                    : Windows.UI.Color.FromArgb(255, 245, 101, 101)))
        }).ToList();

        PreflightChecksList.ItemsSource = displayItems;
    }

    private void ShowPreflightError(string error)
    {
        PreflightResultsCard.Visibility = Visibility.Visible;
        PreflightResultIcon.Glyph = "\uEA39";
        PreflightResultIcon.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 245, 101, 101));
        PreflightPassedText.Text = "";
        PreflightFailedText.Text = error;
        PreflightChecksList.ItemsSource = null;
    }

    private async void RunDryRun_Click(object sender, RoutedEventArgs e)
    {
        DryRunProgress.IsActive = true;
        DryRunButton.IsEnabled = false;

        try
        {
            var result = await _diagnosticsService.RunDryRunAsync();
            ShowDryRunResults(result);
        }
        catch (Exception ex)
        {
            ShowDryRunError(ex.Message);
        }
        finally
        {
            DryRunProgress.IsActive = false;
            DryRunButton.IsEnabled = true;
        }
    }

    private void ShowDryRunResults(DryRunResult result)
    {
        DryRunResultsCard.Visibility = Visibility.Visible;

        DryRunResultIcon.Glyph = result.Success ? "\uE73E" : "\uEA39";
        DryRunResultIcon.Foreground = new SolidColorBrush(
            result.Success ? Windows.UI.Color.FromArgb(255, 72, 187, 120) : Windows.UI.Color.FromArgb(255, 245, 101, 101));

        SetValidationIcon(ConfigValidIcon, ConfigValidText, result.ConfigurationValid, "Valid", "Invalid");
        SetValidationIcon(CredentialsValidIcon, CredentialsValidText, result.CredentialsValid, "Valid", "Invalid");
        SetValidationIcon(StorageValidIcon, StorageValidText, result.StorageWritable, "Writable", "Not Writable");
        SetValidationIcon(ProvidersValidIcon, ProvidersValidText, result.ProvidersReachable, "Reachable", "Unreachable");

        if (result.Warnings.Count > 0)
        {
            DryRunWarningsPanel.Visibility = Visibility.Visible;
            DryRunWarningsList.ItemsSource = result.Warnings;
        }
        else
        {
            DryRunWarningsPanel.Visibility = Visibility.Collapsed;
        }

        if (result.Errors.Count > 0)
        {
            DryRunErrorsPanel.Visibility = Visibility.Visible;
            DryRunErrorsList.ItemsSource = result.Errors;
        }
        else
        {
            DryRunErrorsPanel.Visibility = Visibility.Collapsed;
        }
    }

    private static void SetValidationIcon(FontIcon icon, TextBlock text, bool valid, string validText, string invalidText)
    {
        icon.Glyph = valid ? "\uE73E" : "\uEA39";
        icon.Foreground = new SolidColorBrush(
            valid ? Windows.UI.Color.FromArgb(255, 72, 187, 120) : Windows.UI.Color.FromArgb(255, 245, 101, 101));
        text.Text = valid ? validText : invalidText;
    }

    private void ShowDryRunError(string error)
    {
        DryRunResultsCard.Visibility = Visibility.Visible;
        DryRunResultIcon.Glyph = "\uEA39";
        DryRunResultIcon.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 245, 101, 101));

        DryRunErrorsPanel.Visibility = Visibility.Visible;
        DryRunErrorsList.ItemsSource = new[] { error };
    }

    private void CreateBundle_Click(object sender, RoutedEventArgs e)
    {
        BundleOptionsCard.Visibility = BundleOptionsCard.Visibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private async void GenerateBundle_Click(object sender, RoutedEventArgs e)
    {
        BundleProgress.IsActive = true;
        BundleButton.IsEnabled = false;

        try
        {
            var options = new DiagnosticBundleOptions
            {
                IncludeLogs = IncludeLogsCheck.IsChecked == true,
                IncludeConfig = IncludeConfigCheck.IsChecked == true,
                IncludeMetrics = IncludeMetricsCheck.IsChecked == true,
                IncludeSampleData = IncludeSampleDataCheck.IsChecked == true,
                LogDays = int.Parse((LogDaysCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "7"),
                RedactSecrets = RedactSecretsCheck.IsChecked == true
            };

            var result = await _diagnosticsService.GenerateDiagnosticBundleAsync(options);

            if (result.Success)
            {
                BundleResultCard.Visibility = Visibility.Visible;
                BundlePathBox.Text = result.BundlePath ?? "";
                BundleSizeText.Text = $"Size: {FormatBytes(result.FileSizeBytes)}";
                BundleFilesList.ItemsSource = result.IncludedFiles;
            }
            else
            {
                BundleResultCard.Visibility = Visibility.Collapsed;
                // Show error via notification or inline
            }
        }
        catch (Exception ex)
        {
            // Handle error
            LoggingService.Instance.LogError("Bundle generation failed", ex);
        }
        finally
        {
            BundleProgress.IsActive = false;
            BundleButton.IsEnabled = true;
        }
    }

    private async void OpenBundleFolder_Click(object sender, RoutedEventArgs e)
    {
        var path = BundlePathBox.Text;
        if (string.IsNullOrEmpty(path)) return;

        var folder = System.IO.Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(folder))
        {
            await Launcher.LaunchFolderPathAsync(folder);
        }
    }

    private async void TestProvider_Click(object sender, RoutedEventArgs e)
    {
        var selectedItem = ProviderTestCombo.SelectedItem as ComboBoxItem;
        if (selectedItem?.Tag is not string providerName) return;

        ProviderTestResult.Visibility = Visibility.Visible;
        ProviderTestIcon.Glyph = "\uE895";
        ProviderTestIcon.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 88, 166, 255));
        ProviderTestStatusText.Text = "Testing...";
        ProviderTestDetailsText.Text = "";

        try
        {
            var result = await _diagnosticsService.TestProviderAsync(providerName);

            ProviderTestIcon.Glyph = result.Success ? "\uE73E" : "\uEA39";
            ProviderTestIcon.Foreground = new SolidColorBrush(
                result.Success ? Windows.UI.Color.FromArgb(255, 72, 187, 120) : Windows.UI.Color.FromArgb(255, 245, 101, 101));
            ProviderTestStatusText.Text = result.Success ? "Connected" : "Failed";
            ProviderTestDetailsText.Text = result.Success
                ? $"Latency: {result.LatencyMs:F0}ms" + (result.Version != null ? $" | Version: {result.Version}" : "")
                : result.Error ?? "Connection failed";
        }
        catch (Exception ex)
        {
            ProviderTestIcon.Glyph = "\uEA39";
            ProviderTestIcon.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 245, 101, 101));
            ProviderTestStatusText.Text = "Error";
            ProviderTestDetailsText.Text = ex.Message;
        }
    }

    private async void RefreshMetrics_Click(object sender, RoutedEventArgs e)
    {
        await RefreshMetricsAsync();
    }

    private async System.Threading.Tasks.Task RefreshMetricsAsync()
    {
        try
        {
            var metrics = await _diagnosticsService.GetSystemMetricsAsync();

            CpuUsageText.Text = $"{metrics.CpuUsagePercent:F0}%";
            MemoryUsageText.Text = FormatBytes(metrics.MemoryUsedBytes);
            EventsPerSecText.Text = metrics.EventsPerSecond.ToString("N0");
            UptimeText.Text = FormatTimeSpan(metrics.Uptime);
        }
        catch
        {
            CpuUsageText.Text = "--%";
            MemoryUsageText.Text = "-- MB";
            EventsPerSecText.Text = "--";
            UptimeText.Text = "--";
        }
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F1} GB";
    }

    private static string FormatTimeSpan(TimeSpan ts)
    {
        if (ts.TotalDays >= 1) return $"{(int)ts.TotalDays}d {ts.Hours}h";
        if (ts.TotalHours >= 1) return $"{(int)ts.TotalHours}h {ts.Minutes}m";
        return $"{(int)ts.TotalMinutes}m";
    }
}

/// <summary>
/// Display model for preflight check results.
/// </summary>
public class PreflightCheckDisplay
{
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool Passed { get; set; }
    public string StatusIcon { get; set; } = string.Empty;
    public SolidColorBrush? StatusColor { get; set; }
}
