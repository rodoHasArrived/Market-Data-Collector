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
using UiBackfillService = MarketDataCollector.Ui.Services.BackfillService;
using UiBackfillProgressEventArgs = MarketDataCollector.Ui.Services.BackfillProgressEventArgs;
using UiBackfillCompletedEventArgs = MarketDataCollector.Ui.Services.BackfillCompletedEventArgs;
using WpfServices = MarketDataCollector.Wpf.Services;
using NotificationType = MarketDataCollector.Wpf.Services.NotificationType;

namespace MarketDataCollector.Wpf.Views;

/// <summary>
/// Historical data backfill page with provider selection, date ranges, and scheduling.
/// Wired to real BackfillApiService for live execution and progress tracking.
/// </summary>
public partial class BackfillPage : Page
{
    private readonly WpfServices.NotificationService _notificationService;
    private readonly WpfServices.NavigationService _navigationService;
    private readonly BackfillApiService _backfillApiService;
    private readonly UiBackfillService _backfillService;
    private readonly ObservableCollection<SymbolProgressInfo> _symbolProgress = new();
    private readonly ObservableCollection<ScheduledJobInfo> _scheduledJobs = new();
    private readonly DispatcherTimer _progressPollTimer;
    private CancellationTokenSource? _backfillCts;

    public BackfillPage(
        WpfServices.NotificationService notificationService,
        WpfServices.NavigationService navigationService)
    {
        InitializeComponent();

        _notificationService = notificationService;
        _navigationService = navigationService;
        _backfillApiService = new BackfillApiService();
        _backfillService = UiBackfillService.Instance;

        SymbolProgressList.ItemsSource = _symbolProgress;
        ScheduledJobsList.ItemsSource = _scheduledJobs;

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
        // Set default dates
        ToDatePicker.SelectedDate = DateTime.Today;
        FromDatePicker.SelectedDate = DateTime.Today.AddDays(-30);

        UpdateProviderPrioritySummary();
        UpdateGranularityHint();

        await LoadScheduledJobsAsync();
        await RefreshStatusFromApiAsync();
    }

    private void OnPageUnloaded(object sender, RoutedEventArgs e)
    {
        _progressPollTimer.Stop();
        _backfillCts?.Cancel();
        _backfillService.ProgressUpdated -= OnBackfillProgressUpdated;
        _backfillService.BackfillCompleted -= OnBackfillCompleted;
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

        Dispatcher.Invoke(() =>
        {
            UpdateProgressDisplay(e.Progress);
        });
    }

    private void OnBackfillCompleted(object? sender, UiBackfillCompletedEventArgs e)
    {
        Dispatcher.Invoke(async () =>
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
        await RefreshStatusFromApiAsync();
    }

    private void SymbolsBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var symbols = SymbolsBox.Text?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? Array.Empty<string>();
        SymbolCountText.Text = $"{symbols.Length} symbols";
        DateRangeHintText.Text = "Smart range uses symbol count + granularity to keep request sizes practical.";
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

    private static string GetGranularityDisplay(string granularity)
    {
        return granularity switch
        {
            "1Min" => "1 minute",
            "15Min" => "15 minute",
            _ => granularity
        };
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

/// <summary>
/// Dialog for configuring API keys.
/// </summary>
public class ApiKeyDialog : Window
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
public class EditScheduledJobDialog : Window
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
