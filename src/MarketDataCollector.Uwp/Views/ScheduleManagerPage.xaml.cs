using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using MarketDataCollector.Uwp.Services;

namespace MarketDataCollector.Uwp.Views;

/// <summary>
/// Page for managing scheduled backfill and maintenance tasks.
/// </summary>
public sealed partial class ScheduleManagerPage : Page
{
    private readonly ScheduleManagerService _scheduleService;
    private List<BackfillSchedule> _backfillSchedules = new();
    private List<MaintenanceSchedule> _maintenanceSchedules = new();
    private List<ScheduleExecutionLog> _executionHistory = new();

    public ScheduleManagerPage()
    {
        InitializeComponent();
        _scheduleService = ScheduleManagerService.Instance;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        LoadingRing.IsActive = true;
        LoadingRing.Visibility = Visibility.Visible;

        try
        {
            await Task.WhenAll(
                LoadBackfillSchedulesAsync(),
                LoadMaintenanceSchedulesAsync()
            );
        }
        catch (Exception ex)
        {
            ShowError($"Failed to load schedules: {ex.Message}");
        }
        finally
        {
            LoadingRing.IsActive = false;
            LoadingRing.Visibility = Visibility.Collapsed;
        }
    }

    private async Task LoadBackfillSchedulesAsync()
    {
        _backfillSchedules = await _scheduleService.GetBackfillSchedulesAsync() ?? new List<BackfillSchedule>();

        BackfillSchedulesList.ItemsSource = _backfillSchedules;
        NoBackfillSchedulesText.Visibility = _backfillSchedules.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        // Update stats
        TotalBackfillSchedulesText.Text = _backfillSchedules.Count.ToString();
        ActiveBackfillSchedulesText.Text = _backfillSchedules.Count(s => s.IsEnabled).ToString();

        var nextRun = _backfillSchedules
            .Where(s => s.IsEnabled && s.NextRunAt.HasValue)
            .OrderBy(s => s.NextRunAt)
            .FirstOrDefault();

        NextBackfillRunText.Text = nextRun?.NextRunAt?.ToString("g") ?? "--";
    }

    private async Task LoadMaintenanceSchedulesAsync()
    {
        _maintenanceSchedules = await _scheduleService.GetMaintenanceSchedulesAsync() ?? new List<MaintenanceSchedule>();

        MaintenanceSchedulesList.ItemsSource = _maintenanceSchedules;
        NoMaintenanceSchedulesText.Visibility = _maintenanceSchedules.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        // Update stats
        TotalMaintenanceSchedulesText.Text = _maintenanceSchedules.Count.ToString();
        ActiveMaintenanceSchedulesText.Text = _maintenanceSchedules.Count(s => s.IsEnabled).ToString();

        var nextRun = _maintenanceSchedules
            .Where(s => s.IsEnabled && s.NextRunAt.HasValue)
            .OrderBy(s => s.NextRunAt)
            .FirstOrDefault();

        NextMaintenanceRunText.Text = nextRun?.NextRunAt?.ToString("g") ?? "--";
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        await LoadDataAsync();
    }

    #region Backfill Schedule Actions

    private async void CreateBackfillSchedule_Click(object sender, RoutedEventArgs e)
    {
        var dialog = CreateBackfillScheduleDialog();
        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            var request = GetBackfillScheduleFromDialog(dialog);
            if (request != null)
            {
                var schedule = await _scheduleService.CreateBackfillScheduleAsync(request);
                if (schedule != null)
                {
                    await LoadBackfillSchedulesAsync();
                    ShowSuccess("Backfill schedule created");
                }
            }
        }
    }

    private ContentDialog CreateBackfillScheduleDialog(BackfillSchedule? existing = null)
    {
        var nameBox = new TextBox { Header = "Name *", PlaceholderText = "Daily SPY Backfill", Text = existing?.Name ?? "" };
        var descBox = new TextBox { Header = "Description", PlaceholderText = "Optional description", Text = existing?.Description ?? "" };
        var cronBox = new TextBox { Header = "Cron Expression *", PlaceholderText = "0 6 * * *", FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"), Text = existing?.CronExpression ?? "" };
        var symbolsBox = new TextBox { Header = "Symbols (comma-separated) *", PlaceholderText = "SPY, QQQ, AAPL", Text = existing != null ? string.Join(", ", existing.Symbols) : "" };
        var providerCombo = new ComboBox { Header = "Provider", Items = { "Alpaca", "Polygon", "Yahoo", "Stooq", "Tiingo" }, SelectedItem = existing?.Provider ?? "Alpaca" };
        var granularityCombo = new ComboBox { Header = "Granularity", Items = { "Minute", "Hourly", "Daily" }, SelectedItem = existing?.Granularity ?? "Daily" };
        var lookbackBox = new NumberBox { Header = "Lookback Days", Value = existing?.LookbackDays ?? 7, Minimum = 1, Maximum = 365 };
        var priorityCombo = new ComboBox { Header = "Priority", Items = { "Low", "Normal", "High" }, SelectedItem = existing?.Priority ?? "Normal" };

        var content = new StackPanel
        {
            Spacing = 12,
            Children = { nameBox, descBox, cronBox, symbolsBox, providerCombo, granularityCombo, lookbackBox, priorityCombo }
        };

        return new ContentDialog
        {
            Title = existing == null ? "Create Backfill Schedule" : "Edit Backfill Schedule",
            Content = new ScrollViewer { Content = content, MaxHeight = 500 },
            PrimaryButtonText = existing == null ? "Create" : "Save",
            CloseButtonText = "Cancel",
            XamlRoot = XamlRoot
        };
    }

    private CreateBackfillScheduleRequest? GetBackfillScheduleFromDialog(ContentDialog dialog)
    {
        var content = (dialog.Content as ScrollViewer)?.Content as StackPanel;
        if (content == null) return null;

        var nameBox = content.Children.OfType<TextBox>().FirstOrDefault();
        var descBox = content.Children.OfType<TextBox>().Skip(1).FirstOrDefault();
        var cronBox = content.Children.OfType<TextBox>().Skip(2).FirstOrDefault();
        var symbolsBox = content.Children.OfType<TextBox>().Skip(3).FirstOrDefault();
        var providerCombo = content.Children.OfType<ComboBox>().FirstOrDefault();
        var granularityCombo = content.Children.OfType<ComboBox>().Skip(1).FirstOrDefault();
        var lookbackBox = content.Children.OfType<NumberBox>().FirstOrDefault();
        var priorityCombo = content.Children.OfType<ComboBox>().Skip(2).FirstOrDefault();

        if (string.IsNullOrWhiteSpace(nameBox?.Text) || string.IsNullOrWhiteSpace(cronBox?.Text) || string.IsNullOrWhiteSpace(symbolsBox?.Text))
            return null;

        return new CreateBackfillScheduleRequest
        {
            Name = nameBox.Text,
            Description = descBox?.Text ?? "",
            CronExpression = cronBox.Text,
            Symbols = symbolsBox.Text.Split(',').Select(s => s.Trim().ToUpperInvariant()).Where(s => !string.IsNullOrEmpty(s)).ToList(),
            Provider = providerCombo?.SelectedItem?.ToString() ?? "Alpaca",
            Granularity = granularityCombo?.SelectedItem?.ToString() ?? "Daily",
            LookbackDays = (int)(lookbackBox?.Value ?? 7),
            Priority = priorityCombo?.SelectedItem?.ToString() ?? "Normal"
        };
    }

    private async void EditBackfillSchedule_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string id) return;

        var schedule = _backfillSchedules.FirstOrDefault(s => s.Id == id);
        if (schedule == null) return;

        var dialog = CreateBackfillScheduleDialog(schedule);
        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            var request = GetBackfillScheduleFromDialog(dialog);
            if (request != null)
            {
                var updateRequest = new UpdateBackfillScheduleRequest
                {
                    Name = request.Name,
                    Description = request.Description,
                    CronExpression = request.CronExpression,
                    Symbols = request.Symbols,
                    Provider = request.Provider,
                    Granularity = request.Granularity,
                    LookbackDays = request.LookbackDays,
                    Priority = request.Priority
                };

                await _scheduleService.UpdateBackfillScheduleAsync(id, updateRequest);
                await LoadBackfillSchedulesAsync();
                ShowSuccess("Schedule updated");
            }
        }
    }

    private async void DeleteBackfillSchedule_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string id) return;

        var dialog = new ContentDialog
        {
            Title = "Delete Schedule",
            Content = "Are you sure you want to delete this schedule? This action cannot be undone.",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            XamlRoot = XamlRoot
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            await _scheduleService.DeleteBackfillScheduleAsync(id);
            await LoadBackfillSchedulesAsync();
            ShowSuccess("Schedule deleted");
        }
    }

    private async void RunBackfillNow_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string id) return;

        var result = await _scheduleService.RunBackfillScheduleNowAsync(id);
        if (result?.Success == true)
        {
            ShowSuccess("Backfill started");
        }
        else
        {
            ShowError("Failed to start backfill");
        }
    }

    private async void ViewBackfillHistory_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string id) return;

        var history = await _scheduleService.GetBackfillExecutionHistoryAsync(id);
        if (history != null)
        {
            _executionHistory = history;
            ExecutionHistoryList.ItemsSource = history;
        }
    }

    private async void BackfillScheduleToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleSwitch toggle) return;

        var schedule = toggle.DataContext as BackfillSchedule;
        if (schedule != null)
        {
            await _scheduleService.SetBackfillScheduleEnabledAsync(schedule.Id, toggle.IsOn);
        }
    }

    private void BackfillSchedule_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Handle selection if needed
    }

    #endregion

    #region Maintenance Schedule Actions

    private async void CreateMaintenanceSchedule_Click(object sender, RoutedEventArgs e)
    {
        var dialog = CreateMaintenanceScheduleDialog();
        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            var request = GetMaintenanceScheduleFromDialog(dialog);
            if (request != null)
            {
                var schedule = await _scheduleService.CreateMaintenanceScheduleAsync(request);
                if (schedule != null)
                {
                    await LoadMaintenanceSchedulesAsync();
                    ShowSuccess("Maintenance schedule created");
                }
            }
        }
    }

    private ContentDialog CreateMaintenanceScheduleDialog(MaintenanceSchedule? existing = null)
    {
        var nameBox = new TextBox { Header = "Name *", PlaceholderText = "Daily Health Check", Text = existing?.Name ?? "" };
        var descBox = new TextBox { Header = "Description", PlaceholderText = "Optional description", Text = existing?.Description ?? "" };
        var typeCombo = new ComboBox { Header = "Maintenance Type *", Items = { "HealthCheck", "Cleanup", "DataMigration", "Defragmentation", "Archival" }, SelectedItem = existing?.MaintenanceType ?? "HealthCheck" };
        var cronBox = new TextBox { Header = "Cron Expression *", PlaceholderText = "0 3 * * *", FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"), Text = existing?.CronExpression ?? "" };
        var targetPathBox = new TextBox { Header = "Target Path (optional)", PlaceholderText = "/data/live", Text = existing?.TargetPath ?? "" };
        var maxDurationBox = new NumberBox { Header = "Max Duration (minutes)", Value = existing?.MaxDurationMinutes ?? 60, Minimum = 1, Maximum = 480 };
        var maxRetriesBox = new NumberBox { Header = "Max Retries", Value = existing?.MaxRetries ?? 3, Minimum = 0, Maximum = 10 };
        var priorityCombo = new ComboBox { Header = "Priority", Items = { "Low", "Normal", "High" }, SelectedItem = existing?.Priority ?? "Normal" };

        var content = new StackPanel
        {
            Spacing = 12,
            Children = { nameBox, descBox, typeCombo, cronBox, targetPathBox, maxDurationBox, maxRetriesBox, priorityCombo }
        };

        return new ContentDialog
        {
            Title = existing == null ? "Create Maintenance Schedule" : "Edit Maintenance Schedule",
            Content = new ScrollViewer { Content = content, MaxHeight = 500 },
            PrimaryButtonText = existing == null ? "Create" : "Save",
            CloseButtonText = "Cancel",
            XamlRoot = XamlRoot
        };
    }

    private CreateMaintenanceScheduleRequest? GetMaintenanceScheduleFromDialog(ContentDialog dialog)
    {
        var content = (dialog.Content as ScrollViewer)?.Content as StackPanel;
        if (content == null) return null;

        var nameBox = content.Children.OfType<TextBox>().FirstOrDefault();
        var descBox = content.Children.OfType<TextBox>().Skip(1).FirstOrDefault();
        var typeCombo = content.Children.OfType<ComboBox>().FirstOrDefault();
        var cronBox = content.Children.OfType<TextBox>().Skip(2).FirstOrDefault();
        var targetPathBox = content.Children.OfType<TextBox>().Skip(3).FirstOrDefault();
        var maxDurationBox = content.Children.OfType<NumberBox>().FirstOrDefault();
        var maxRetriesBox = content.Children.OfType<NumberBox>().Skip(1).FirstOrDefault();
        var priorityCombo = content.Children.OfType<ComboBox>().Skip(1).FirstOrDefault();

        if (string.IsNullOrWhiteSpace(nameBox?.Text) || string.IsNullOrWhiteSpace(cronBox?.Text))
            return null;

        return new CreateMaintenanceScheduleRequest
        {
            Name = nameBox.Text,
            Description = descBox?.Text ?? "",
            MaintenanceType = typeCombo?.SelectedItem?.ToString() ?? "HealthCheck",
            CronExpression = cronBox.Text,
            TargetPath = string.IsNullOrWhiteSpace(targetPathBox?.Text) ? null : targetPathBox.Text,
            MaxDurationMinutes = (int)(maxDurationBox?.Value ?? 60),
            MaxRetries = (int)(maxRetriesBox?.Value ?? 3),
            Priority = priorityCombo?.SelectedItem?.ToString() ?? "Normal"
        };
    }

    private async void EditMaintenanceSchedule_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string id) return;

        var schedule = _maintenanceSchedules.FirstOrDefault(s => s.Id == id);
        if (schedule == null) return;

        var dialog = CreateMaintenanceScheduleDialog(schedule);
        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            var request = GetMaintenanceScheduleFromDialog(dialog);
            if (request != null)
            {
                var updateRequest = new UpdateMaintenanceScheduleRequest
                {
                    Name = request.Name,
                    Description = request.Description,
                    CronExpression = request.CronExpression,
                    TargetPath = request.TargetPath,
                    MaxDurationMinutes = request.MaxDurationMinutes,
                    MaxRetries = request.MaxRetries,
                    Priority = request.Priority
                };

                await _scheduleService.UpdateMaintenanceScheduleAsync(id, updateRequest);
                await LoadMaintenanceSchedulesAsync();
                ShowSuccess("Schedule updated");
            }
        }
    }

    private async void DeleteMaintenanceSchedule_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string id) return;

        var dialog = new ContentDialog
        {
            Title = "Delete Schedule",
            Content = "Are you sure you want to delete this schedule? This action cannot be undone.",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            XamlRoot = XamlRoot
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            await _scheduleService.DeleteMaintenanceScheduleAsync(id);
            await LoadMaintenanceSchedulesAsync();
            ShowSuccess("Schedule deleted");
        }
    }

    private async void RunMaintenanceNow_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string id) return;

        var result = await _scheduleService.RunMaintenanceScheduleNowAsync(id);
        if (result?.Success == true)
        {
            ShowSuccess("Maintenance task started");
        }
        else
        {
            ShowError("Failed to start maintenance task");
        }
    }

    private async void ViewMaintenanceHistory_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string id) return;

        var history = await _scheduleService.GetMaintenanceExecutionHistoryAsync(id);
        if (history != null)
        {
            _executionHistory = history;
            ExecutionHistoryList.ItemsSource = history;
        }
    }

    private async void MaintenanceScheduleToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleSwitch toggle) return;

        var schedule = toggle.DataContext as MaintenanceSchedule;
        if (schedule != null)
        {
            await _scheduleService.SetMaintenanceScheduleEnabledAsync(schedule.Id, toggle.IsOn);
        }
    }

    private void MaintenanceSchedule_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Handle selection if needed
    }

    #endregion

    #region History and Templates

    private void HistoryFilter_Changed(object sender, object e)
    {
        // Apply filters to execution history
        var filtered = _executionHistory.AsEnumerable();

        if (HistoryTypeFilter.SelectedItem is ComboBoxItem typeItem && typeItem.Tag?.ToString() != "All")
        {
            // Filter by type
        }

        if (HistoryStatusFilter.SelectedItem is ComboBoxItem statusItem && statusItem.Tag?.ToString() != "All")
        {
            filtered = filtered.Where(h => h.Status == statusItem.Tag?.ToString());
        }

        ExecutionHistoryList.ItemsSource = filtered.ToList();
        NoHistoryText.Visibility = !filtered.Any() ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ClearHistoryFilters_Click(object sender, RoutedEventArgs e)
    {
        HistoryTypeFilter.SelectedIndex = 0;
        HistoryStatusFilter.SelectedIndex = 0;
        HistoryDateFrom.Date = null;
        HistoryDateTo.Date = null;
        ExecutionHistoryList.ItemsSource = _executionHistory;
    }

    private async void ViewExecutionDetails_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string id) return;

        var log = _executionHistory.FirstOrDefault(h => h.Id == id);
        if (log == null) return;

        var content = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                new TextBlock { Text = $"Schedule: {log.ScheduleName}", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold },
                new TextBlock { Text = $"Started: {log.StartedAt:g}" },
                new TextBlock { Text = $"Completed: {log.CompletedAt?.ToString("g") ?? "Running"}" },
                new TextBlock { Text = $"Duration: {log.Duration}" },
                new TextBlock { Text = $"Status: {log.Status}" },
                new TextBlock { Text = $"Records Processed: {log.RecordsProcessed}" },
                new TextBlock { Text = $"Records Failed: {log.RecordsFailed}" }
            }
        };

        if (!string.IsNullOrEmpty(log.ErrorMessage))
        {
            content.Children.Add(new TextBlock { Text = $"Error: {log.ErrorMessage}", Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Red), TextWrapping = TextWrapping.Wrap });
        }

        await new ContentDialog
        {
            Title = "Execution Details",
            Content = content,
            CloseButtonText = "Close",
            XamlRoot = XamlRoot
        }.ShowAsync();
    }

    private void ApplyTemplate_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string templateId) return;

        var (name, cron, lookback) = templateId switch
        {
            "hourly-backfill" => ("Hourly Backfill", "0 * * * *", 1),
            "daily-backfill" => ("Daily Backfill", "0 6 * * *", 1),
            "weekly-backfill" => ("Weekly Full Backfill", "0 0 * * 0", 7),
            _ => ("Custom", "", 1)
        };

        // Pre-fill the create dialog
        CreateBackfillSchedule_Click(sender, e);
    }

    private void ApplyMaintenanceTemplate_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string templateId) return;

        // Pre-fill the create dialog with template values
        CreateMaintenanceSchedule_Click(sender, e);
    }

    private void CronExpression_TextChanged(object sender, TextChangedEventArgs e)
    {
        CronValidationPanel.Visibility = Visibility.Collapsed;
        CronErrorBar.IsOpen = false;
    }

    private async void ValidateCron_Click(object sender, RoutedEventArgs e)
    {
        var expression = CronExpressionInput.Text;
        if (string.IsNullOrWhiteSpace(expression)) return;

        var result = await _scheduleService.ValidateCronExpressionAsync(expression);
        if (result == null) return;

        if (result.IsValid)
        {
            CronDescriptionText.Text = result.Description;
            NextRunsList.ItemsSource = result.NextRuns.Take(5).Select(d => d.ToString("g"));
            CronValidationPanel.Visibility = Visibility.Visible;
            CronErrorBar.IsOpen = false;
        }
        else
        {
            CronValidationPanel.Visibility = Visibility.Collapsed;
            CronErrorBar.Message = result.ErrorMessage ?? "Invalid cron expression";
            CronErrorBar.IsOpen = true;
        }
    }

    #endregion

    private void ShowSuccess(string message)
    {
        PageInfoBar.Message = message;
        PageInfoBar.Severity = InfoBarSeverity.Success;
        PageInfoBar.IsOpen = true;
    }

    private void ShowError(string message)
    {
        PageInfoBar.Message = message;
        PageInfoBar.Severity = InfoBarSeverity.Error;
        PageInfoBar.IsOpen = true;
    }
}
