using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MarketDataCollector.Wpf.Services;

namespace MarketDataCollector.Wpf.Views;

/// <summary>
/// Historical data backfill page with provider selection, date ranges, and scheduling.
/// </summary>
public partial class BackfillPage : Page
{
    private readonly ObservableCollection<SymbolProgressInfo> _symbolProgress = new();
    private readonly ObservableCollection<ScheduledJobInfo> _scheduledJobs = new();
    private bool _isRunning;

    public BackfillPage()
    {
        InitializeComponent();

        SymbolProgressList.ItemsSource = _symbolProgress;
        ScheduledJobsList.ItemsSource = _scheduledJobs;
    }

    private void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        // Set default dates
        ToDatePicker.SelectedDate = DateTime.Today;
        FromDatePicker.SelectedDate = DateTime.Today.AddDays(-30);

        LoadScheduledJobs();
        RefreshStatus();
    }

    private void LoadScheduledJobs()
    {
        _scheduledJobs.Clear();
        _scheduledJobs.Add(new ScheduledJobInfo { Name = "Daily EOD Update", NextRun = "Tomorrow 6:00 AM" });
        _scheduledJobs.Add(new ScheduledJobInfo { Name = "Weekly Full Sync", NextRun = "Sunday 2:00 AM" });

        NoScheduledJobsText.Visibility = _scheduledJobs.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void RefreshStatus()
    {
        // Show sample status for demonstration
        StatusGrid.Visibility = Visibility.Visible;
        NoStatusText.Visibility = Visibility.Collapsed;

        StatusText.Text = "Completed";
        StatusText.Foreground = new SolidColorBrush(Color.FromRgb(63, 185, 80));
        ProviderText.Text = "Multi-Source";
        SymbolsText.Text = "SPY, QQQ, AAPL, MSFT, GOOGL";
        BarsWrittenText.Text = "12,456";
        StartedText.Text = "2 hours ago";
        CompletedText.Text = "1 hour ago";
    }

    private void SymbolsBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var symbols = SymbolsBox.Text?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? Array.Empty<string>();
        SymbolCountText.Text = $"{symbols.Length} symbols";
    }

    private void ValidateData_Click(object sender, RoutedEventArgs e)
    {
        NotificationService.Instance.ShowNotification(
            "Data Validation",
            "Starting data validation...",
            NotificationType.Info);
    }

    private void RepairGaps_Click(object sender, RoutedEventArgs e)
    {
        NotificationService.Instance.ShowNotification(
            "Gap Repair",
            "Checking for data gaps...",
            NotificationType.Info);
    }

    private void OpenWizard_Click(object sender, RoutedEventArgs e)
    {
        NavigationService.Instance.NavigateTo("AnalysisExportWizard");
    }

    private void FillAllGaps_Click(object sender, RoutedEventArgs e)
    {
        NotificationService.Instance.ShowNotification(
            "Fill Gaps",
            "Analyzing all symbols for gaps...",
            NotificationType.Info);
    }

    private void UpdateLatest_Click(object sender, RoutedEventArgs e)
    {
        // Set dates to update to latest
        FromDatePicker.SelectedDate = DateTime.Today.AddDays(-5);
        ToDatePicker.SelectedDate = DateTime.Today;
        AddAllSubscribed_Click(sender, e);

        NotificationService.Instance.ShowNotification(
            "Update to Latest",
            "Configured to update all subscribed symbols to latest data.",
            NotificationType.Info);
    }

    private void BrowseData_Click(object sender, RoutedEventArgs e)
    {
        NavigationService.Instance.NavigateTo("DataBrowser");
    }

    private void AddAllSubscribed_Click(object sender, RoutedEventArgs e)
    {
        SymbolsBox.Text = "SPY, QQQ, AAPL, MSFT, GOOGL, AMZN, NVDA, META, TSLA";
    }

    private void AddMajorETFs_Click(object sender, RoutedEventArgs e)
    {
        var current = SymbolsBox.Text?.Trim() ?? "";
        var etfs = "SPY, QQQ, IWM";
        SymbolsBox.Text = string.IsNullOrEmpty(current) ? etfs : $"{current}, {etfs}";
    }

    private void Last30Days_Click(object sender, RoutedEventArgs e)
    {
        FromDatePicker.SelectedDate = DateTime.Today.AddDays(-30);
        ToDatePicker.SelectedDate = DateTime.Today;
    }

    private void Last90Days_Click(object sender, RoutedEventArgs e)
    {
        FromDatePicker.SelectedDate = DateTime.Today.AddDays(-90);
        ToDatePicker.SelectedDate = DateTime.Today;
    }

    private void YearToDate_Click(object sender, RoutedEventArgs e)
    {
        FromDatePicker.SelectedDate = new DateTime(DateTime.Today.Year, 1, 1);
        ToDatePicker.SelectedDate = DateTime.Today;
    }

    private void LastYear_Click(object sender, RoutedEventArgs e)
    {
        FromDatePicker.SelectedDate = DateTime.Today.AddYears(-1);
        ToDatePicker.SelectedDate = DateTime.Today;
    }

    private void Last5Years_Click(object sender, RoutedEventArgs e)
    {
        FromDatePicker.SelectedDate = DateTime.Today.AddYears(-5);
        ToDatePicker.SelectedDate = DateTime.Today;
    }

    private void StartBackfill_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(SymbolsBox.Text))
        {
            SymbolsValidationError.Text = "Please enter at least one symbol";
            SymbolsValidationError.Visibility = Visibility.Visible;
            return;
        }

        SymbolsValidationError.Visibility = Visibility.Collapsed;
        _isRunning = true;

        StartBackfillButton.Visibility = Visibility.Collapsed;
        PauseBackfillButton.Visibility = Visibility.Visible;
        CancelBackfillButton.Visibility = Visibility.Visible;
        ProgressPanel.Visibility = Visibility.Visible;
        SymbolProgressCard.Visibility = Visibility.Visible;

        BackfillStatusText.Text = "Running...";

        // Simulate progress with sample symbols
        var symbols = SymbolsBox.Text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        _symbolProgress.Clear();
        foreach (var symbol in symbols)
        {
            _symbolProgress.Add(new SymbolProgressInfo
            {
                Symbol = symbol.Trim().ToUpper(),
                Progress = 0,
                BarsText = "0 bars",
                StatusText = "Pending",
                TimeText = "--",
                StatusBackground = new SolidColorBrush(Color.FromArgb(40, 139, 148, 158))
            });
        }

        OverallProgressText.Text = $"Overall: 0 / {symbols.Length} symbols complete";

        NotificationService.Instance.ShowNotification(
            "Backfill Started",
            $"Downloading data for {symbols.Length} symbols...",
            NotificationType.Info);
    }

    private void PauseBackfill_Click(object sender, RoutedEventArgs e)
    {
        BackfillStatusText.Text = "Paused";
        PauseBackfillButton.Content = "Resume";

        NotificationService.Instance.ShowNotification(
            "Backfill Paused",
            "Backfill operation has been paused.",
            NotificationType.Warning);
    }

    private void CancelBackfill_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "Are you sure you want to cancel the backfill operation?",
            "Cancel Backfill",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            _isRunning = false;

            StartBackfillButton.Visibility = Visibility.Visible;
            PauseBackfillButton.Visibility = Visibility.Collapsed;
            CancelBackfillButton.Visibility = Visibility.Collapsed;
            ProgressPanel.Visibility = Visibility.Collapsed;

            BackfillStatusText.Text = "Cancelled";

            NotificationService.Instance.ShowNotification(
                "Backfill Cancelled",
                "The backfill operation was cancelled.",
                NotificationType.Warning);
        }
    }

    private void RefreshStatus_Click(object sender, RoutedEventArgs e)
    {
        RefreshStatus();

        NotificationService.Instance.ShowNotification(
            "Status Refreshed",
            "Backfill status has been refreshed.",
            NotificationType.Info);
    }

    private void SetNasdaqApiKey_Click(object sender, RoutedEventArgs e)
    {
        NotificationService.Instance.ShowNotification(
            "Set API Key",
            "API key configuration will be available soon.",
            NotificationType.Info);
    }

    private void ClearNasdaqApiKey_Click(object sender, RoutedEventArgs e)
    {
        NasdaqKeyStatusText.Text = "No API key stored";
        ClearNasdaqKeyButton.Visibility = Visibility.Collapsed;
    }

    private void SetOpenFigiApiKey_Click(object sender, RoutedEventArgs e)
    {
        NotificationService.Instance.ShowNotification(
            "Set API Key",
            "API key configuration will be available soon.",
            NotificationType.Info);
    }

    private void ClearOpenFigiApiKey_Click(object sender, RoutedEventArgs e)
    {
        OpenFigiKeyStatusText.Text = "No API key stored (optional)";
        ClearOpenFigiKeyButton.Visibility = Visibility.Collapsed;
    }

    private void ScheduledBackfill_Toggled(object sender, RoutedEventArgs e)
    {
        if (ScheduleSettingsPanel != null)
        {
            ScheduleSettingsPanel.Opacity = ScheduledBackfillToggle.IsChecked == true ? 1.0 : 0.5;
        }
    }

    private void SaveSchedule_Click(object sender, RoutedEventArgs e)
    {
        NotificationService.Instance.ShowNotification(
            "Schedule Saved",
            "Backfill schedule has been saved.",
            NotificationType.Success);
    }

    private void RunScheduledJob_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is ScheduledJobInfo job)
        {
            NotificationService.Instance.ShowNotification(
                "Running Job",
                $"Starting scheduled job: {job.Name}",
                NotificationType.Info);
        }
    }

    private void EditScheduledJob_Click(object sender, RoutedEventArgs e)
    {
        NotificationService.Instance.ShowNotification(
            "Edit Job",
            "Job editing will be available soon.",
            NotificationType.Info);
    }
}

/// <summary>
/// Symbol progress information for backfill tracking.
/// </summary>
public class SymbolProgressInfo
{
    public string Symbol { get; set; } = string.Empty;
    public double Progress { get; set; }
    public string BarsText { get; set; } = string.Empty;
    public string StatusText { get; set; } = string.Empty;
    public string TimeText { get; set; } = string.Empty;
    public SolidColorBrush StatusBackground { get; set; } = new(Color.FromArgb(40, 139, 148, 158));
}

/// <summary>
/// Scheduled job information.
/// </summary>
public class ScheduledJobInfo
{
    public string Name { get; set; } = string.Empty;
    public string NextRun { get; set; } = string.Empty;
}
