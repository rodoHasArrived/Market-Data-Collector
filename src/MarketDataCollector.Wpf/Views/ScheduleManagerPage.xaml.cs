using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using MarketDataCollector.Wpf.Services;

namespace MarketDataCollector.Wpf.Views;

public partial class ScheduleManagerPage : Page
{
    private readonly NavigationService _navigationService;
    private readonly NotificationService _notificationService;
    private readonly ScheduleManagerService _scheduleService;

    public ScheduleManagerPage(
        NavigationService navigationService,
        NotificationService notificationService)
    {
        InitializeComponent();

        _navigationService = navigationService;
        _notificationService = notificationService;
        _scheduleService = ScheduleManagerService.Instance;
    }

    private async void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        await LoadBackfillSchedulesAsync();
        await LoadMaintenanceSchedulesAsync();
        await LoadTemplatesAsync();
    }

    private async void RefreshBackfillSchedules_Click(object sender, RoutedEventArgs e)
    {
        await LoadBackfillSchedulesAsync();
    }

    private async void RefreshMaintenanceSchedules_Click(object sender, RoutedEventArgs e)
    {
        await LoadMaintenanceSchedulesAsync();
    }

    private async void ValidateCron_Click(object sender, RoutedEventArgs e)
    {
        var expression = CronExpressionInput.Text?.Trim();
        if (string.IsNullOrEmpty(expression))
        {
            CronValidationResult.Text = "Please enter a cron expression.";
            CronValidationResult.Foreground = (System.Windows.Media.Brush)FindResource("WarningColorBrush");
            return;
        }

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var result = await _scheduleService.ValidateCronExpressionAsync(expression, cts.Token);

            if (result == null)
            {
                CronValidationResult.Text = "Could not validate expression. Backend may be unavailable.";
                CronValidationResult.Foreground = (System.Windows.Media.Brush)FindResource("WarningColorBrush");
                return;
            }

            if (result.IsValid)
            {
                var text = $"Valid: {result.Description}";
                if (result.NextRuns.Count > 0)
                {
                    text += "\nNext runs:";
                    foreach (var run in result.NextRuns)
                    {
                        text += $"\n  {run:yyyy-MM-dd HH:mm:ss} UTC";
                    }
                }
                CronValidationResult.Text = text;
                CronValidationResult.Foreground = (System.Windows.Media.Brush)FindResource("SuccessColorBrush");
            }
            else
            {
                CronValidationResult.Text = $"Invalid: {result.ErrorMessage}";
                CronValidationResult.Foreground = (System.Windows.Media.Brush)FindResource("ErrorColorBrush");
            }
        }
        catch (Exception ex)
        {
            CronValidationResult.Text = $"Validation failed: {ex.Message}";
            CronValidationResult.Foreground = (System.Windows.Media.Brush)FindResource("ErrorColorBrush");
        }
    }

    private async System.Threading.Tasks.Task LoadBackfillSchedulesAsync()
    {
        try
        {
            BackfillSchedulesStatus.Text = "Loading...";
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var schedules = await _scheduleService.GetBackfillSchedulesAsync(cts.Token);

            if (schedules == null || schedules.Count == 0)
            {
                BackfillSchedulesStatus.Text = "No backfill schedules configured.";
                BackfillSchedulesList.ItemsSource = null;
                return;
            }

            BackfillSchedulesStatus.Text = $"{schedules.Count} schedule(s) found";
            BackfillSchedulesList.ItemsSource = schedules;
        }
        catch (Exception ex)
        {
            BackfillSchedulesStatus.Text = $"Failed to load: {ex.Message}";
            BackfillSchedulesStatus.Foreground = (System.Windows.Media.Brush)FindResource("ErrorColorBrush");
        }
    }

    private async System.Threading.Tasks.Task LoadMaintenanceSchedulesAsync()
    {
        try
        {
            MaintenanceSchedulesStatus.Text = "Loading...";
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var schedules = await _scheduleService.GetMaintenanceSchedulesAsync(cts.Token);

            if (schedules == null || schedules.Count == 0)
            {
                MaintenanceSchedulesStatus.Text = "No maintenance schedules configured.";
                MaintenanceSchedulesList.ItemsSource = null;
                return;
            }

            MaintenanceSchedulesStatus.Text = $"{schedules.Count} schedule(s) found";
            MaintenanceSchedulesList.ItemsSource = schedules;
        }
        catch (Exception ex)
        {
            MaintenanceSchedulesStatus.Text = $"Failed to load: {ex.Message}";
            MaintenanceSchedulesStatus.Foreground = (System.Windows.Media.Brush)FindResource("ErrorColorBrush");
        }
    }

    private async System.Threading.Tasks.Task LoadTemplatesAsync()
    {
        try
        {
            TemplatesStatus.Text = "Loading...";
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var templates = await _scheduleService.GetBackfillTemplatesAsync(cts.Token);

            if (templates == null || templates.Count == 0)
            {
                TemplatesStatus.Text = "No templates available.";
                TemplatesList.ItemsSource = null;
                return;
            }

            TemplatesStatus.Text = $"{templates.Count} template(s) available";
            TemplatesList.ItemsSource = templates;
        }
        catch (Exception ex)
        {
            TemplatesStatus.Text = $"Failed to load: {ex.Message}";
            TemplatesStatus.Foreground = (System.Windows.Media.Brush)FindResource("ErrorColorBrush");
        }
    }
}
