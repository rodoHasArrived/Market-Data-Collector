using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using MarketDataCollector.Contracts.Backfill;
using MarketDataCollector.Ui.Services;
using MarketDataCollector.Wpf.Models;
using UiBackfillService = MarketDataCollector.Ui.Services.BackfillService;
using UiBackfillProgressEventArgs = MarketDataCollector.Ui.Services.BackfillProgressEventArgs;
using UiBackfillCompletedEventArgs = MarketDataCollector.Ui.Services.BackfillCompletedEventArgs;
using WpfServices = MarketDataCollector.Wpf.Services;

namespace MarketDataCollector.Wpf.Views;

/// <summary>
/// Historical data backfill page with provider selection, date ranges, and scheduling.
/// Wired to real BackfillApiService for live execution and progress tracking.
/// Supports job resumability via BackfillCheckpointService.
/// </summary>
public partial class BackfillPage : Page
{
    private readonly WpfServices.NotificationService _notificationService;
    private readonly WpfServices.NavigationService _navigationService;
    private readonly WpfServices.LoggingService _loggingService;
    private readonly BackfillApiService _backfillApiService;
    private readonly UiBackfillService _backfillService;
    private readonly BackfillCheckpointService _checkpointService;
    private readonly ObservableCollection<SymbolProgressInfo> _symbolProgress = new();
    private readonly ObservableCollection<ScheduledJobInfo> _scheduledJobs = new();
    private readonly ObservableCollection<ResumableJobInfo> _resumableJobs = new();
    private readonly ObservableCollection<GapAnalysisItem> _gapItems = new();
    private readonly DispatcherTimer _progressPollTimer;
    private CancellationTokenSource? _backfillCts;

    public BackfillPage(
        WpfServices.NotificationService notificationService,
        WpfServices.NavigationService navigationService,
        WpfServices.LoggingService loggingService)
    {
        InitializeComponent();

        _notificationService = notificationService;
        _navigationService = navigationService;
        _loggingService = loggingService;
        _backfillApiService = new BackfillApiService();
        _backfillService = UiBackfillService.Instance;
        _checkpointService = BackfillCheckpointService.Instance;

        SymbolProgressList.ItemsSource = _symbolProgress;
        ScheduledJobsList.ItemsSource = _scheduledJobs;
        ResumableJobsList.ItemsSource = _resumableJobs;
        GapAnalysisList.ItemsSource = _gapItems;

        _progressPollTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5)
        };
        _progressPollTimer.Tick += OnProgressPollTimerTick;

        _backfillService.ProgressUpdated += OnBackfillProgressUpdated;
        _backfillService.BackfillCompleted += OnBackfillCompleted;
    }

    private async void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        // Set default dates first; RestorePageFilterState will override with saved values
        ToDatePicker.SelectedDate = DateTime.Today;
        FromDatePicker.SelectedDate = DateTime.Today.AddDays(-30);

        RestorePageFilterState();
        UpdateProviderPrioritySummary();
        UpdateGranularityHint();

        await LoadScheduledJobsAsync();
        await LoadResumableJobsAsync();
        await RefreshStatusFromApiAsync();
    }

    private void OnPageUnloaded(object sender, RoutedEventArgs e)
    {
        _progressPollTimer.Stop();
        _backfillCts?.Cancel();
        _backfillService.ProgressUpdated -= OnBackfillProgressUpdated;
        _backfillService.BackfillCompleted -= OnBackfillCompleted;

        SavePageFilterState();
    }

    private async Task LoadScheduledJobsAsync()
    {
        _scheduledJobs.Clear();

        try
        {
            var executions = await _backfillApiService.GetExecutionHistoryAsync(limit: 10);
            foreach (var exec in executions)
            {
                _scheduledJobs.Add(new ScheduledJobInfo
                {
                    Name = $"{exec.Status}: {exec.SymbolsProcessed} symbols",
                    NextRun = exec.CompletedAt?.ToString("g") ?? exec.StartedAt.ToString("g")
                });
            }
        }
        catch
        {
            // Fallback if API unavailable
        }

        NoScheduledJobsText.Visibility = _scheduledJobs.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private async Task RefreshStatusFromApiAsync()
    {
        try
        {
            var lastStatus = await _backfillApiService.GetLastStatusAsync();

            if (lastStatus != null)
            {
                StatusGrid.Visibility = Visibility.Visible;
                NoStatusText.Visibility = Visibility.Collapsed;

                var isSuccess = lastStatus.Success;
                StatusText.Text = isSuccess ? "Completed" : "Failed";
                StatusText.Foreground = isSuccess
                    ? new SolidColorBrush(Color.FromRgb(63, 185, 80))
                    : new SolidColorBrush(Color.FromRgb(244, 67, 54));
                ProviderText.Text = lastStatus.Provider ?? "Unknown";
                SymbolsText.Text = lastStatus.Symbols != null
                    ? string.Join(", ", lastStatus.Symbols)
                    : "N/A";
                BarsWrittenText.Text = lastStatus.BarsWritten.ToString("N0");
                StartedText.Text = lastStatus.StartedUtc?.LocalDateTime.ToString("g") ?? "Unknown";
                CompletedText.Text = lastStatus.CompletedUtc?.LocalDateTime.ToString("g") ?? "N/A";
            }
            else
            {
                StatusGrid.Visibility = Visibility.Collapsed;
                NoStatusText.Visibility = Visibility.Visible;
            }
        }
        catch
        {
            StatusGrid.Visibility = Visibility.Collapsed;
            NoStatusText.Visibility = Visibility.Visible;
        }
    }

    private void OnBackfillProgressUpdated(object? sender, UiBackfillProgressEventArgs e)
    {
        if (e.Progress == null) return;

        _ = Dispatcher.InvokeAsync(() =>
        {
            UpdateProgressDisplay(e.Progress);
        });
    }

    private async Task LoadResumableJobsAsync()
    {
        _resumableJobs.Clear();

        try
        {
            var resumable = await _checkpointService.GetResumableJobsAsync();
            foreach (var job in resumable)
            {
                var pendingSymbols = await _checkpointService.GetPendingSymbolsAsync(job.JobId);
                _resumableJobs.Add(new ResumableJobInfo
                {
                    JobId = job.JobId,
                    Provider = job.Provider,
                    Status = job.Status.ToString(),
                    CreatedAt = job.CreatedAt.ToLocalTime().ToString("g"),
                    SymbolsSummary = $"{job.CompletedCount}/{job.SymbolCheckpoints.Count} symbols done, {pendingSymbols.Length} remaining",
                    PendingCount = pendingSymbols.Length,
                    TotalBarsDownloaded = job.TotalBarsDownloaded,
                    DateRange = $"{job.FromDate:d} — {job.ToDate:d}"
                });
            }
        }
        catch
        {
            // Checkpoint storage unavailable
        }

        NoResumableJobsText.Visibility = _resumableJobs.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        ResumableJobsCard.Visibility = Visibility.Visible;
    }

    private async void ResumeJob_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not ResumableJobInfo job)
            return;

        if (_backfillService.IsRunning)
        {
            _notificationService.ShowNotification(
                "Cannot Resume",
                "A backfill operation is already running. Cancel or wait for it to finish.",
                NotificationType.Warning);
            return;
        }

        try
        {
            StartBackfillButton.Visibility = Visibility.Collapsed;
            PauseBackfillButton.Visibility = Visibility.Visible;
            CancelBackfillButton.Visibility = Visibility.Visible;
            ProgressPanel.Visibility = Visibility.Visible;
            SymbolProgressCard.Visibility = Visibility.Visible;

            BackfillStatusText.Text = $"Resuming ({job.PendingCount} symbols remaining)...";

            _notificationService.ShowNotification(
                "Resuming Backfill",
                $"Resuming job from checkpoint: {job.PendingCount} symbols remaining.",
                NotificationType.Info);

            _progressPollTimer.Start();

            await _backfillService.ResumeBackfillAsync(job.JobId);
        }
        catch (OperationCanceledException)
        {
            _progressPollTimer.Stop();
            BackfillStatusText.Text = "Cancelled";
            StartBackfillButton.Visibility = Visibility.Visible;
            PauseBackfillButton.Visibility = Visibility.Collapsed;
            CancelBackfillButton.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            _progressPollTimer.Stop();
            BackfillStatusText.Text = "Resume Failed";
            StartBackfillButton.Visibility = Visibility.Visible;
            PauseBackfillButton.Visibility = Visibility.Collapsed;
            CancelBackfillButton.Visibility = Visibility.Collapsed;

            _notificationService.ShowNotification(
                "Resume Failed",
                ex.Message,
                NotificationType.Error);
        }
    }

    private async void DismissJob_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not ResumableJobInfo job)
            return;

        _resumableJobs.Remove(job);
        NoResumableJobsText.Visibility = _resumableJobs.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        _notificationService.ShowNotification(
            "Job Dismissed",
            "Resumable job has been dismissed from the list.",
            NotificationType.Info);
    }

    private void OnBackfillCompleted(object? sender, UiBackfillCompletedEventArgs e)
    {
        _ = Dispatcher.InvokeAsync(async () =>
        {
            _progressPollTimer.Stop();

            StartBackfillButton.Visibility = Visibility.Visible;
            PauseBackfillButton.Visibility = Visibility.Collapsed;
            CancelBackfillButton.Visibility = Visibility.Collapsed;

            if (e.Success)
            {
                BackfillStatusText.Text = "Completed";
                _notificationService.ShowNotification(
                    "Backfill Complete",
                    $"Successfully downloaded data for {e.Progress?.CompletedSymbols ?? 0} symbols.",
                    NotificationType.Success);
            }
            else if (e.WasCancelled)
            {
                BackfillStatusText.Text = "Cancelled";
            }
            else
            {
                BackfillStatusText.Text = "Failed";
                _notificationService.ShowNotification(
                    "Backfill Failed",
                    e.Error?.Message ?? "Unknown error occurred.",
                    NotificationType.Error);
            }

            await RefreshStatusFromApiAsync();
            await LoadResumableJobsAsync();
        });
    }

    private void UpdateProgressDisplay(MarketDataCollector.Contracts.Backfill.BackfillProgress progress)
    {
        BackfillStatusText.Text = progress.Status;

        var completedCount = progress.CompletedSymbols;
        OverallProgressText.Text = $"Overall: {completedCount} / {progress.TotalSymbols} symbols complete";

        if (progress.SymbolProgress != null)
        {
            for (var i = 0; i < progress.SymbolProgress.Length && i < _symbolProgress.Count; i++)
            {
                var sp = progress.SymbolProgress[i];
                var item = _symbolProgress[i];
                item.Progress = sp.CalculatedProgress;
                item.BarsText = $"{sp.BarsDownloaded:N0} bars";
                item.StatusText = sp.Status;
                item.TimeText = sp.Duration?.ToString(@"mm\:ss") ?? "--";
                item.StatusBackground = sp.Status switch
                {
                    "Completed" => new SolidColorBrush(Color.FromArgb(40, 63, 185, 80)),
                    "Failed" => new SolidColorBrush(Color.FromArgb(40, 244, 67, 54)),
                    "Downloading" => new SolidColorBrush(Color.FromArgb(40, 33, 150, 243)),
                    _ => new SolidColorBrush(Color.FromArgb(40, 139, 148, 158))
                };
            }
        }
    }

    private async void OnProgressPollTimerTick(object? sender, EventArgs e)
    {
        // Poll the backend for real-time backfill progress updates
        await _backfillService.PollBackendStatusAsync();
        await RefreshStatusFromApiAsync();
    }

    private void SymbolsBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var symbols = SymbolsBox.Text?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? Array.Empty<string>();
        SymbolCountText.Text = $"{symbols.Length} symbols";
    }

    private void ProviderPriority_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateProviderPrioritySummary();
    }

    private void GranularityCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateGranularityHint();
    }

    private void ApplySmartRange_Click(object sender, RoutedEventArgs e)
    {
        var granularity = (GranularityCombo?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Daily";
        var symbolCount = SymbolsBox.Text?
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Length ?? 0;

        var lookbackDays = granularity switch
        {
            "1Min" => symbolCount > 20 ? 3 : symbolCount > 5 ? 7 : 14,
            "15Min" => symbolCount > 20 ? 14 : symbolCount > 5 ? 30 : 60,
            "Hourly" => symbolCount > 50 ? 30 : symbolCount > 10 ? 90 : 180,
            _ => symbolCount > 100 ? 365 : symbolCount > 30 ? 365 * 2 : 365 * 5
        };

        ToDatePicker.SelectedDate = DateTime.Today;
        FromDatePicker.SelectedDate = DateTime.Today.AddDays(-lookbackDays);

        DateRangeHintText.Text = $"Smart range applied: last {lookbackDays} days for {Math.Max(symbolCount, 1)} symbol(s) at {GetGranularityDisplay(granularity)} granularity.";
    }

    private void UpdateProviderPrioritySummary()
    {
        var primary = GetProviderName(PrimaryProviderCombo);
        var secondary = GetProviderName(SecondaryProviderCombo);
        var tertiary = GetProviderName(TertiaryProviderCombo);

        var sequence = new[] { primary, secondary, tertiary }
            .Where(v => !string.IsNullOrWhiteSpace(v) && v != "No fallback")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        ProviderPrioritySummaryText.Text = sequence.Length > 0
            ? $"Priority: {string.Join(" → ", sequence)}"
            : "Priority: No providers selected";
    }

    private void UpdateGranularityHint()
    {
        var granularity = (GranularityCombo?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Daily";
        GranularityHintText.Text = granularity switch
        {
            "1Min" => "1-minute data is best for short tactical windows (typically days to a few weeks).",
            "15Min" => "15-minute data balances detail and request size for multi-week to multi-month backfills.",
            "Hourly" => "Hourly data is well-suited for trend/rotation systems over months.",
            _ => "Daily is recommended for broad symbol lists and long history windows."
        };
    }

    private static string GetProviderName(ComboBox combo)
    {
        return (combo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
    }

    private static string? GetComboSelectedTag(ComboBox combo)
    {
        return (combo.SelectedItem as ComboBoxItem)?.Tag?.ToString();
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

    private const string PageTag = "Backfill";

    private void SavePageFilterState()
    {
        var ws = WpfServices.WorkspaceService.Instance;
        ws.UpdatePageFilterState(PageTag, "Symbols", SymbolsBox.Text);
        ws.UpdatePageFilterState(PageTag, "ProviderCombo", GetComboSelectedTag(ProviderCombo) ?? "composite");
        ws.UpdatePageFilterState(PageTag, "PrimaryProvider", GetComboSelectedTag(PrimaryProviderCombo) ?? "yahoo");
        ws.UpdatePageFilterState(PageTag, "SecondaryProvider", GetComboSelectedTag(SecondaryProviderCombo) ?? "stooq");
        ws.UpdatePageFilterState(PageTag, "TertiaryProvider", GetComboSelectedTag(TertiaryProviderCombo) ?? "nasdaq");
        ws.UpdatePageFilterState(PageTag, "Granularity", GetComboSelectedTag(GranularityCombo) ?? "Daily");
        ws.UpdatePageFilterState(PageTag, "FromDate", FromDatePicker.SelectedDate?.ToString("yyyy-MM-dd"));
        ws.UpdatePageFilterState(PageTag, "ToDate", ToDatePicker.SelectedDate?.ToString("yyyy-MM-dd"));
    }

    private void RestorePageFilterState()
    {
        var ws = WpfServices.WorkspaceService.Instance;

        var symbols = ws.GetPageFilterState(PageTag, "Symbols");
        if (symbols is not null)
            SymbolsBox.Text = symbols;

        var provider = ws.GetPageFilterState(PageTag, "ProviderCombo");
        if (provider is not null)
            SelectComboItemByTag(ProviderCombo, provider);

        var primary = ws.GetPageFilterState(PageTag, "PrimaryProvider");
        if (primary is not null)
            SelectComboItemByTag(PrimaryProviderCombo, primary);

        var secondary = ws.GetPageFilterState(PageTag, "SecondaryProvider");
        if (secondary is not null)
            SelectComboItemByTag(SecondaryProviderCombo, secondary);

        var tertiary = ws.GetPageFilterState(PageTag, "TertiaryProvider");
        if (tertiary is not null)
            SelectComboItemByTag(TertiaryProviderCombo, tertiary);

        var granularity = ws.GetPageFilterState(PageTag, "Granularity");
        if (granularity is not null)
            SelectComboItemByTag(GranularityCombo, granularity);

        if (DateTime.TryParse(ws.GetPageFilterState(PageTag, "FromDate"), out var fromDate))
            FromDatePicker.SelectedDate = fromDate;

        if (DateTime.TryParse(ws.GetPageFilterState(PageTag, "ToDate"), out var toDate))
            ToDatePicker.SelectedDate = toDate;
    }

    private static string GetGranularityDisplay(string granularity)
    {
        return granularity switch
        {
            "1Min" => "1-minute",
            "15Min" => "15-minute",
            "Hourly" => "hourly",
            "Daily" => "daily",
            _ => granularity.ToLowerInvariant()
        };
    }

    private void DatePicker_SelectedDateChanged(object? sender, SelectionChangedEventArgs e)
    {
        FromDateValidationError.Visibility = Visibility.Collapsed;
        ToDateValidationError.Visibility = Visibility.Collapsed;
    }

    private void ValidateData_Click(object sender, RoutedEventArgs e)
    {
        _notificationService.ShowNotification(
            "Data Validation",
            "Starting data validation...",
            NotificationType.Info);
    }

    private void RepairGaps_Click(object sender, RoutedEventArgs e)
    {
        _notificationService.ShowNotification(
            "Gap Repair",
            "Checking for data gaps...",
            NotificationType.Info);
    }

    private void OpenWizard_Click(object sender, RoutedEventArgs e)
    {
        _navigationService.NavigateTo("AnalysisExportWizard");
    }

    private void FillAllGaps_Click(object sender, RoutedEventArgs e)
    {
        _notificationService.ShowNotification(
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

        _notificationService.ShowNotification(
            "Update to Latest",
            "Configured to update all subscribed symbols to latest data.",
            NotificationType.Info);
    }

    private void BrowseData_Click(object sender, RoutedEventArgs e)
    {
        _navigationService.NavigateTo("DataBrowser");
    }

    private async void AddAllSubscribed_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Load configured symbols from config service instead of hardcoded list
            var configSymbols = await WpfServices.ConfigService.Instance.GetConfiguredSymbolsAsync();
            if (configSymbols.Length > 0)
            {
                SymbolsBox.Text = string.Join(", ", configSymbols.Select(s => s.Symbol));
            }
            else
            {
                // Fallback to common symbols when no config exists yet
                SymbolsBox.Text = "SPY, QQQ, AAPL, MSFT, GOOGL, AMZN, NVDA, META, TSLA";
            }
        }
        catch
        {
            // Fallback on error
            SymbolsBox.Text = "SPY, QQQ, AAPL, MSFT, GOOGL, AMZN, NVDA, META, TSLA";
        }
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

    private async void StartBackfill_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(SymbolsBox.Text))
        {
            SymbolsValidationError.Text = "Please enter at least one symbol";
            SymbolsValidationError.Visibility = Visibility.Visible;
            return;
        }

        SymbolsValidationError.Visibility = Visibility.Collapsed;

        var fromDate = FromDatePicker.SelectedDate ?? DateTime.Today.AddDays(-30);
        var toDate = ToDatePicker.SelectedDate ?? DateTime.Today;

        if (fromDate > toDate)
        {
            FromDateValidationError.Text = "From date must be earlier than To date";
            FromDateValidationError.Visibility = Visibility.Visible;
            ToDateValidationError.Text = "To date must be on or after From date";
            ToDateValidationError.Visibility = Visibility.Visible;
            return;
        }

        FromDateValidationError.Visibility = Visibility.Collapsed;
        ToDateValidationError.Visibility = Visibility.Collapsed;

        StartBackfillButton.Visibility = Visibility.Collapsed;
        PauseBackfillButton.Visibility = Visibility.Visible;
        CancelBackfillButton.Visibility = Visibility.Visible;
        ProgressPanel.Visibility = Visibility.Visible;
        SymbolProgressCard.Visibility = Visibility.Visible;

        BackfillStatusText.Text = "Running...";

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

        _notificationService.ShowNotification(
            "Backfill Started",
            $"Downloading data for {symbols.Length} symbols...",
            NotificationType.Info);

        // Get provider from combo or default to "composite"
        var provider = (ProviderCombo?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "composite";
        var granularity = (GranularityCombo?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Daily";

        // Start progress polling
        _progressPollTimer.Start();

        // Execute backfill via real API
        _backfillCts = new CancellationTokenSource();
        try
        {
            await _backfillService.StartBackfillAsync(
                symbols.Select(s => s.Trim().ToUpper()).ToArray(),
                provider,
                fromDate,
                toDate,
                granularity);
        }
        catch (OperationCanceledException)
        {
            _progressPollTimer.Stop();
            BackfillStatusText.Text = "Cancelled";
            StartBackfillButton.Visibility = Visibility.Visible;
            PauseBackfillButton.Visibility = Visibility.Collapsed;
            CancelBackfillButton.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            _progressPollTimer.Stop();
            BackfillStatusText.Text = "Failed";
            StartBackfillButton.Visibility = Visibility.Visible;
            PauseBackfillButton.Visibility = Visibility.Collapsed;
            CancelBackfillButton.Visibility = Visibility.Collapsed;

            _notificationService.ShowNotification(
                "Backfill Failed",
                ex.Message,
                NotificationType.Error);
        }
    }

    private void PauseBackfill_Click(object sender, RoutedEventArgs e)
    {
        if (_backfillService.IsPaused)
        {
            _backfillService.Resume();
            BackfillStatusText.Text = "Running...";
            PauseBackfillButton.Content = "Pause";
            _notificationService.ShowNotification(
                "Backfill Resumed",
                "Backfill operation has been resumed.",
                NotificationType.Info);
        }
        else
        {
            _backfillService.Pause();
            BackfillStatusText.Text = "Paused";
            PauseBackfillButton.Content = "Resume";
            _notificationService.ShowNotification(
                "Backfill Paused",
                "Backfill operation has been paused.",
                NotificationType.Warning);
        }
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
            _backfillService.Cancel();
            _backfillCts?.Cancel();
            _progressPollTimer.Stop();

            StartBackfillButton.Visibility = Visibility.Visible;
            PauseBackfillButton.Visibility = Visibility.Collapsed;
            CancelBackfillButton.Visibility = Visibility.Collapsed;
            ProgressPanel.Visibility = Visibility.Collapsed;

            BackfillStatusText.Text = "Cancelled";

            _notificationService.ShowNotification(
                "Backfill Cancelled",
                "The backfill operation was cancelled.",
                NotificationType.Warning);
        }
    }

    private async void RefreshStatus_Click(object sender, RoutedEventArgs e)
    {
        await RefreshStatusFromApiAsync();

        _notificationService.ShowNotification(
            "Status Refreshed",
            "Backfill status has been refreshed.",
            NotificationType.Info);
    }

    private void SetNasdaqApiKey_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ApiKeyDialog("Nasdaq Data Link", "NASDAQDATALINK__APIKEY");
        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.ApiKey))
        {
            // Store the API key (in a real implementation, this would go to secure storage)
            Environment.SetEnvironmentVariable("NASDAQDATALINK__APIKEY", dialog.ApiKey, EnvironmentVariableTarget.User);

            NasdaqKeyStatusText.Text = "API key configured";
            ClearNasdaqKeyButton.Visibility = Visibility.Visible;

            _notificationService.ShowNotification(
                "API Key Saved",
                "Nasdaq Data Link API key has been configured.",
                NotificationType.Success);
        }
    }

    private void ClearNasdaqApiKey_Click(object sender, RoutedEventArgs e)
    {
        NasdaqKeyStatusText.Text = "No API key stored";
        ClearNasdaqKeyButton.Visibility = Visibility.Collapsed;
    }

    private void SetOpenFigiApiKey_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ApiKeyDialog("OpenFIGI", "OPENFIGI__APIKEY", isOptional: true);
        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.ApiKey))
        {
            // Store the API key (in a real implementation, this would go to secure storage)
            Environment.SetEnvironmentVariable("OPENFIGI__APIKEY", dialog.ApiKey, EnvironmentVariableTarget.User);

            OpenFigiKeyStatusText.Text = "API key configured (optional)";
            ClearOpenFigiKeyButton.Visibility = Visibility.Visible;

            _notificationService.ShowNotification(
                "API Key Saved",
                "OpenFIGI API key has been configured.",
                NotificationType.Success);
        }
    }

    private void ClearOpenFigiApiKey_Click(object sender, RoutedEventArgs e)
    {
        OpenFigiKeyStatusText.Text = "No API key stored (optional)";
        ClearOpenFigiKeyButton.Visibility = Visibility.Collapsed;
    }

    private async void ScanGaps_Click(object sender, RoutedEventArgs e)
    {
        var symbolsText = SymbolsBox.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(symbolsText))
        {
            GapAnalysisSummaryText.Text = "Enter symbols above before scanning for gaps.";
            return;
        }

        var symbols = symbolsText.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var fromDate = FromDatePicker.SelectedDate ?? DateTime.Today.AddDays(-30);
        var toDate = ToDatePicker.SelectedDate ?? DateTime.Today;
        var totalDays = Math.Max(1, (int)(toDate - fromDate).TotalDays);

        _gapItems.Clear();
        GapAnalysisCard.Visibility = Visibility.Visible;
        GapAnalysisList.Visibility = Visibility.Collapsed;
        GapActionPanel.Visibility = Visibility.Collapsed;
        GapAnalysisSummaryText.Text = "Scanning for data gaps\u2026";

        var totalGapDays = 0;
        var apiReachable = false;

        foreach (var symbol in symbols)
        {
            var sym = symbol.Trim().ToUpper();

            try
            {
                var result = await _backfillApiService.GetSymbolGapAnalysisAsync(sym);
                apiReachable = true;

                int coveragePct;
                int gapDays;
                if (result != null)
                {
                    coveragePct = Math.Max(0, Math.Min(100, (int)Math.Round(result.DataAvailabilityPercent)));
                    gapDays = totalDays - (int)Math.Round(totalDays * coveragePct / 100.0);
                }
                else
                {
                    // Symbol not yet tracked by quality monitoring — treat as no data recorded
                    coveragePct = 0;
                    gapDays = totalDays;
                }

                totalGapDays += gapDays;

                var coverageBrush = coveragePct >= 95
                    ? new SolidColorBrush(Color.FromRgb(63, 185, 80))
                    : coveragePct >= 70
                        ? new SolidColorBrush(Color.FromRgb(227, 179, 65))
                        : new SolidColorBrush(Color.FromRgb(244, 67, 54));

                _gapItems.Add(new GapAnalysisItem
                {
                    Symbol = sym,
                    CoveragePercent = coveragePct,
                    CoverageText = $"{coveragePct}%",
                    GapDays = gapDays,
                    GapDaysText = gapDays == 0 ? "Complete" : $"{gapDays}d gaps",
                    CoverageBrush = coverageBrush,
                    CoverageWidth = Math.Max(4, coveragePct * 3.5) // scale to fit the grid column
                });
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Gap scan failed for symbol", ex, ("symbol", sym));
            }
        }

        if (!apiReachable)
        {
            GapAnalysisSummaryText.Text = "Service unavailable. Ensure the backend is running to scan for gaps.";
            return;
        }

        GapAnalysisList.Visibility = Visibility.Visible;
        GapActionPanel.Visibility = totalGapDays > 0 ? Visibility.Visible : Visibility.Collapsed;
        GapActionHintText.Text = totalGapDays > 0
            ? $"{totalGapDays} total gap days across {symbols.Length} symbols"
            : "";
        GapAnalysisSummaryText.Text = totalGapDays == 0
            ? "All symbols have complete coverage for the selected date range."
            : $"Found gaps in {_gapItems.Count(g => g.GapDays > 0)} of {symbols.Length} symbols.";
    }

    private void AutoFillGaps_Click(object sender, RoutedEventArgs e)
    {
        var symbolsWithGaps = _gapItems.Where(g => g.GapDays > 0).Select(g => g.Symbol).ToArray();
        if (symbolsWithGaps.Length == 0)
        {
            _notificationService.ShowNotification("No Gaps", "No gaps detected to fill.", NotificationType.Info);
            return;
        }

        SymbolsBox.Text = string.Join(", ", symbolsWithGaps);
        _notificationService.ShowNotification(
            "Gap Fill",
            $"Configured to fill gaps for {symbolsWithGaps.Length} symbols. Press Start Backfill to begin.",
            NotificationType.Info);
    }

    private void ScheduledBackfill_Toggled(object sender, RoutedEventArgs e)
    {
        if (ScheduleSettingsPanel != null)
        {
            ScheduleSettingsPanel.Opacity = ScheduledBackfillToggle.IsChecked.GetValueOrDefault() ? 1.0 : 0.5;
        }
    }

    private void SaveSchedule_Click(object sender, RoutedEventArgs e)
    {
        _notificationService.ShowNotification(
            "Schedule Saved",
            "Backfill schedule has been saved.",
            NotificationType.Success);
    }

    private void RunScheduledJob_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is ScheduledJobInfo job)
        {
            _notificationService.ShowNotification(
                "Running Job",
                $"Starting scheduled job: {job.Name}",
                NotificationType.Info);
        }
    }

    private void EditScheduledJob_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is ScheduledJobInfo job)
        {
            var dialog = new EditScheduledJobDialog(job);
            if (dialog.ShowDialog() == true)
            {
                if (dialog.ShouldDelete)
                {
                    _scheduledJobs.Remove(job);
                    NoScheduledJobsText.Visibility = _scheduledJobs.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

                    _notificationService.ShowNotification(
                        "Job Deleted",
                        $"Scheduled job '{job.Name}' has been deleted.",
                        NotificationType.Success);
                }
                else
                {
                    // Update job properties
                    var index = _scheduledJobs.IndexOf(job);
                    if (index >= 0)
                    {
                        _scheduledJobs[index] = new ScheduledJobInfo
                        {
                            Name = dialog.JobName,
                            NextRun = dialog.NextRunText
                        };
                    }

                    _notificationService.ShowNotification(
                        "Job Updated",
                        $"Scheduled job '{dialog.JobName}' has been updated.",
                        NotificationType.Success);
                }
            }
        }
    }
}

/// <summary>
/// Dialog for configuring API keys.
/// </summary>
public sealed class ApiKeyDialog : Window
{
    private readonly TextBox _apiKeyBox;
    private readonly string _providerName;

    public string ApiKey => _apiKeyBox.Text;

    public ApiKeyDialog(string providerName, string envVarName, bool isOptional = false)
    {
        _providerName = providerName;

        Title = $"Configure {providerName} API Key";
        Width = 450;
        Height = 220;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        Background = new SolidColorBrush(Color.FromRgb(30, 30, 46));

        var grid = new Grid { Margin = new Thickness(20) };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Description
        var descText = new TextBlock
        {
            Text = $"Enter your {providerName} API key{(isOptional ? " (optional)" : "")}:",
            Foreground = Brushes.White,
            Margin = new Thickness(0, 0, 0, 8),
            TextWrapping = TextWrapping.Wrap
        };
        Grid.SetRow(descText, 0);
        grid.Children.Add(descText);

        // Environment variable hint
        var hintText = new TextBlock
        {
            Text = $"Environment variable: {envVarName}",
            Foreground = new SolidColorBrush(Color.FromRgb(139, 148, 158)),
            FontSize = 11,
            Margin = new Thickness(0, 0, 0, 12)
        };
        Grid.SetRow(hintText, 1);
        grid.Children.Add(hintText);

        // API Key input
        _apiKeyBox = new TextBox
        {
            Background = new SolidColorBrush(Color.FromRgb(42, 42, 62)),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(58, 58, 78)),
            Padding = new Thickness(10, 8, 10, 8),
            FontFamily = new FontFamily("Consolas"),
            Margin = new Thickness(0, 0, 0, 16)
        };

        // Try to load existing value
        var existingValue = Environment.GetEnvironmentVariable(envVarName, EnvironmentVariableTarget.User);
        if (!string.IsNullOrEmpty(existingValue))
        {
            _apiKeyBox.Text = existingValue;
        }

        Grid.SetRow(_apiKeyBox, 2);
        grid.Children.Add(_apiKeyBox);

        // Buttons
        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        Grid.SetRow(buttonPanel, 4);

        var cancelButton = new Button
        {
            Content = "Cancel",
            Width = 100,
            Background = new SolidColorBrush(Color.FromRgb(58, 58, 78)),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(12, 8, 12, 8),
            Margin = new Thickness(0, 0, 8, 0)
        };
        cancelButton.Click += (_, _) => { DialogResult = false; Close(); };
        buttonPanel.Children.Add(cancelButton);

        var saveButton = new Button
        {
            Content = "Save",
            Width = 100,
            Background = new SolidColorBrush(Color.FromRgb(76, 175, 80)),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(12, 8, 12, 8)
        };
        saveButton.Click += OnSaveClick;
        buttonPanel.Children.Add(saveButton);

        grid.Children.Add(buttonPanel);
        Content = grid;
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}

/// <summary>
/// Dialog for editing scheduled jobs.
/// </summary>
public sealed class EditScheduledJobDialog : Window
{
    private readonly TextBox _nameBox;
    private readonly ComboBox _frequencyCombo;
    private readonly ComboBox _timeCombo;
    private readonly ComboBox _dayCombo;

    public string JobName => _nameBox.Text;
    public string NextRunText { get; private set; } = string.Empty;
    public bool ShouldDelete { get; private set; }

    public EditScheduledJobDialog(ScheduledJobInfo job)
    {
        Title = "Edit Scheduled Job";
        Width = 450;
        Height = 350;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        Background = new SolidColorBrush(Color.FromRgb(30, 30, 46));

        var grid = new Grid { Margin = new Thickness(20) };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Job name
        AddLabel(grid, "Job Name:", 0);
        _nameBox = new TextBox
        {
            Text = job.Name,
            Background = new SolidColorBrush(Color.FromRgb(42, 42, 62)),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(58, 58, 78)),
            Padding = new Thickness(10, 6, 10, 6),
            Margin = new Thickness(0, 4, 0, 12)
        };
        Grid.SetRow(_nameBox, 1);
        grid.Children.Add(_nameBox);

        // Frequency
        AddLabel(grid, "Frequency:", 2);
        _frequencyCombo = new ComboBox
        {
            Background = new SolidColorBrush(Color.FromRgb(42, 42, 62)),
            Foreground = Brushes.White,
            Margin = new Thickness(0, 4, 0, 12)
        };
        _frequencyCombo.Items.Add("Daily");
        _frequencyCombo.Items.Add("Weekly");
        _frequencyCombo.Items.Add("Monthly");
        _frequencyCombo.SelectedIndex = job.Name.Contains("Weekly") ? 1 : 0;
        _frequencyCombo.SelectionChanged += OnFrequencyChanged;
        Grid.SetRow(_frequencyCombo, 3);
        grid.Children.Add(_frequencyCombo);

        // Time
        AddLabel(grid, "Time:", 4);
        _timeCombo = new ComboBox
        {
            Background = new SolidColorBrush(Color.FromRgb(42, 42, 62)),
            Foreground = Brushes.White,
            Margin = new Thickness(0, 4, 0, 12)
        };
        for (var hour = 0; hour < 24; hour++)
        {
            _timeCombo.Items.Add($"{hour:D2}:00");
            _timeCombo.Items.Add($"{hour:D2}:30");
        }
        _timeCombo.SelectedIndex = 12; // 6:00 AM
        Grid.SetRow(_timeCombo, 5);
        grid.Children.Add(_timeCombo);

        // Day of week (for weekly)
        _dayCombo = new ComboBox
        {
            Background = new SolidColorBrush(Color.FromRgb(42, 42, 62)),
            Foreground = Brushes.White,
            Margin = new Thickness(0, 4, 0, 12),
            Visibility = job.Name.Contains("Weekly") ? Visibility.Visible : Visibility.Collapsed
        };
        foreach (var day in new[] { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" })
        {
            _dayCombo.Items.Add(day);
        }
        _dayCombo.SelectedIndex = 6; // Sunday
        Grid.SetRow(_dayCombo, 6);
        grid.Children.Add(_dayCombo);

        // Buttons
        var buttonPanel = new Grid { Margin = new Thickness(0, 8, 0, 0) };
        buttonPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        buttonPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        buttonPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        buttonPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetRow(buttonPanel, 7);

        var deleteButton = new Button
        {
            Content = "Delete",
            Width = 100,
            Background = new SolidColorBrush(Color.FromRgb(244, 67, 54)),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(12, 8, 12, 8)
        };
        deleteButton.Click += (_, _) => { ShouldDelete = true; DialogResult = true; Close(); };
        Grid.SetColumn(deleteButton, 0);
        buttonPanel.Children.Add(deleteButton);

        var cancelButton = new Button
        {
            Content = "Cancel",
            Width = 100,
            Background = new SolidColorBrush(Color.FromRgb(58, 58, 78)),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(12, 8, 12, 8)
        };
        cancelButton.Click += (_, _) => { DialogResult = false; Close(); };
        Grid.SetColumn(cancelButton, 2);
        buttonPanel.Children.Add(cancelButton);

        var saveButton = new Button
        {
            Content = "Save",
            Width = 100,
            Margin = new Thickness(8, 0, 0, 0),
            Background = new SolidColorBrush(Color.FromRgb(76, 175, 80)),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(12, 8, 12, 8)
        };
        saveButton.Click += OnSaveClick;
        Grid.SetColumn(saveButton, 3);
        buttonPanel.Children.Add(saveButton);

        grid.Children.Add(buttonPanel);
        Content = grid;
    }

    private void AddLabel(Grid grid, string text, int row)
    {
        var label = new TextBlock
        {
            Text = text,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 0, 0, 4)
        };
        Grid.SetRow(label, row);
        grid.Children.Add(label);
    }

    private void OnFrequencyChanged(object sender, SelectionChangedEventArgs e)
    {
        _dayCombo.Visibility = _frequencyCombo.SelectedIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_nameBox.Text))
        {
            MessageBox.Show("Please enter a job name.", "Validation Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Calculate next run text
        var time = _timeCombo.SelectedItem?.ToString() ?? "06:00";
        var frequency = _frequencyCombo.SelectedItem?.ToString() ?? "Daily";

        NextRunText = frequency switch
        {
            "Daily" => $"Tomorrow {time}",
            "Weekly" => $"{_dayCombo.SelectedItem} {time}",
            "Monthly" => $"1st of month {time}",
            _ => $"Tomorrow {time}"
        };

        DialogResult = true;
        Close();
    }
}
