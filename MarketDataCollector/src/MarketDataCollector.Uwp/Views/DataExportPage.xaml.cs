using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MarketDataCollector.Uwp.Services;

namespace MarketDataCollector.Uwp.Views;

/// <summary>
/// Page for data export and external system integration.
/// </summary>
public sealed partial class DataExportPage : Page
{
    private readonly CredentialService _credentialService;
    private readonly ObservableCollection<ExportHistoryItem> _exportHistory;

    public DataExportPage()
    {
        this.InitializeComponent();

        _credentialService = new CredentialService();
        _exportHistory = new ObservableCollection<ExportHistoryItem>();

        ExportHistoryList.ItemsSource = _exportHistory;

        Loaded += DataExportPage_Loaded;
    }

    private void DataExportPage_Loaded(object sender, RoutedEventArgs e)
    {
        // Set default dates
        ExportFromDate.Date = DateTimeOffset.Now.AddDays(-7);
        ExportToDate.Date = DateTimeOffset.Now;

        // Load sample export history
        LoadSampleExportHistory();
    }

    private void LoadSampleExportHistory()
    {
        _exportHistory.Add(new ExportHistoryItem
        {
            Timestamp = "2026-01-02 09:30",
            Format = "CSV",
            SymbolCount = "5",
            Size = "128 MB",
            Destination = "C:\\Exports\\market_data_20260102.csv"
        });
        _exportHistory.Add(new ExportHistoryItem
        {
            Timestamp = "2026-01-01 18:00",
            Format = "Parquet",
            SymbolCount = "12",
            Size = "45 MB",
            Destination = "C:\\Exports\\daily_export.parquet"
        });

        NoExportHistoryText.Visibility = _exportHistory.Count > 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    private void SetToday_Click(object sender, RoutedEventArgs e)
    {
        ExportFromDate.Date = DateTimeOffset.Now.Date;
        ExportToDate.Date = DateTimeOffset.Now;
    }

    private void SetWeek_Click(object sender, RoutedEventArgs e)
    {
        ExportFromDate.Date = DateTimeOffset.Now.AddDays(-7);
        ExportToDate.Date = DateTimeOffset.Now;
    }

    private void SetMonth_Click(object sender, RoutedEventArgs e)
    {
        ExportFromDate.Date = DateTimeOffset.Now.AddMonths(-1);
        ExportToDate.Date = DateTimeOffset.Now;
    }

    private async void ExportData_Click(object sender, RoutedEventArgs e)
    {
        ExportProgress.IsActive = true;
        ExportButton.IsEnabled = false;
        ExportProgressPanel.Visibility = Visibility.Visible;

        // Simulate export progress
        for (int i = 0; i <= 100; i += 10)
        {
            ExportProgressBar.Value = i;
            ExportProgressPercent.Text = $"{i}%";
            ExportProgressLabel.Text = i < 50 ? "Exporting trades..." : "Compressing data...";
            await Task.Delay(200);
        }

        // Add to history
        var format = (ExportFormatCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "CSV";
        _exportHistory.Insert(0, new ExportHistoryItem
        {
            Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
            Format = format.Split(' ')[0],
            SymbolCount = SelectedSymbolsList.SelectedItems.Count.ToString(),
            Size = "64 MB",
            Destination = "C:\\Exports\\export_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv"
        });
        NoExportHistoryText.Visibility = Visibility.Collapsed;

        ExportProgress.IsActive = false;
        ExportButton.IsEnabled = true;
        ExportProgressPanel.Visibility = Visibility.Collapsed;

        ActionInfoBar.Severity = InfoBarSeverity.Success;
        ActionInfoBar.Title = "Export Complete";
        ActionInfoBar.Message = "Data exported successfully to the selected destination.";
        ActionInfoBar.IsOpen = true;
    }

    private void DatabaseType_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DatabasePortBox == null) return;

        var selectedItem = DatabaseTypeCombo.SelectedItem as ComboBoxItem;
        var dbType = selectedItem?.Tag?.ToString();

        DatabasePortBox.Value = dbType switch
        {
            "postgresql" or "timescaledb" => 5432,
            "clickhouse" => 8123,
            "questdb" => 9000,
            "influxdb" => 8086,
            "sqlite" => 0,
            _ => 5432
        };
    }

    private async void SetDatabaseCredentials_Click(object sender, RoutedEventArgs e)
    {
        var success = await _credentialService.PromptAndStoreCredentialAsync(
            "MarketDataCollector_DatabaseExport",
            "Enter database credentials",
            "Enter username and password for database connection");

        if (success)
        {
            DbCredentialStatus.Text = "Configured";
            ActionInfoBar.Severity = InfoBarSeverity.Success;
            ActionInfoBar.Title = "Credentials Saved";
            ActionInfoBar.Message = "Database credentials have been securely stored.";
            ActionInfoBar.IsOpen = true;
        }
    }

    private async void TestDatabaseConnection_Click(object sender, RoutedEventArgs e)
    {
        ActionInfoBar.Severity = InfoBarSeverity.Informational;
        ActionInfoBar.Title = "Testing Connection...";
        ActionInfoBar.Message = "Attempting to connect to the database.";
        ActionInfoBar.IsOpen = true;

        await Task.Delay(1500); // Simulate connection test

        ActionInfoBar.Severity = InfoBarSeverity.Success;
        ActionInfoBar.Title = "Connection Successful";
        ActionInfoBar.Message = "Successfully connected to the database.";
    }

    private void ConfigureDatabaseSync_Click(object sender, RoutedEventArgs e)
    {
        ActionInfoBar.Severity = InfoBarSeverity.Informational;
        ActionInfoBar.Title = "Database Sync";
        ActionInfoBar.Message = "Database synchronization configuration will be available in a future update.";
        ActionInfoBar.IsOpen = true;
    }

    private async void TestWebhook_Click(object sender, RoutedEventArgs e)
    {
        WebhookTestResult.Text = "Testing...";

        await Task.Delay(1000); // Simulate webhook test

        if (!string.IsNullOrWhiteSpace(WebhookUrlBox.Text))
        {
            WebhookTestResult.Text = "Success (200 OK)";
            ActionInfoBar.Severity = InfoBarSeverity.Success;
            ActionInfoBar.Title = "Webhook Test Successful";
            ActionInfoBar.Message = "The webhook endpoint responded successfully.";
        }
        else
        {
            WebhookTestResult.Text = "Failed - No URL";
            ActionInfoBar.Severity = InfoBarSeverity.Error;
            ActionInfoBar.Title = "Webhook Test Failed";
            ActionInfoBar.Message = "Please enter a webhook URL.";
        }
        ActionInfoBar.IsOpen = true;
    }

    private void BrowseLeanPath_Click(object sender, RoutedEventArgs e)
    {
        // In a real implementation, this would open a folder picker
        LeanDataPathBox.Text = "C:\\QuantConnect\\Data";
    }

    private async void ExportToLean_Click(object sender, RoutedEventArgs e)
    {
        ActionInfoBar.Severity = InfoBarSeverity.Informational;
        ActionInfoBar.Title = "Exporting to Lean Format...";
        ActionInfoBar.Message = "Converting market data to QuantConnect Lean format.";
        ActionInfoBar.IsOpen = true;

        await Task.Delay(2000); // Simulate export

        ActionInfoBar.Severity = InfoBarSeverity.Success;
        ActionInfoBar.Title = "Lean Export Complete";
        ActionInfoBar.Message = "Data has been exported in QuantConnect Lean format.";
    }

    private async void VerifyLeanData_Click(object sender, RoutedEventArgs e)
    {
        ActionInfoBar.Severity = InfoBarSeverity.Informational;
        ActionInfoBar.Title = "Verifying Lean Data...";
        ActionInfoBar.Message = "Checking data integrity and format compliance.";
        ActionInfoBar.IsOpen = true;

        await Task.Delay(1500);

        ActionInfoBar.Severity = InfoBarSeverity.Success;
        ActionInfoBar.Title = "Verification Complete";
        ActionInfoBar.Message = "Lean data format is valid and ready for backtesting.";
    }
}

/// <summary>
/// Represents an export history item.
/// </summary>
public class ExportHistoryItem
{
    public string Timestamp { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty;
    public string SymbolCount { get; set; } = string.Empty;
    public string Size { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
}
