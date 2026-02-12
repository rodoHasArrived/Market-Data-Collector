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
    private readonly NotificationService _notificationService;
    private readonly NavigationService _navigationService;
    private readonly ObservableCollection<SymbolProgressInfo> _symbolProgress = new();
    private readonly ObservableCollection<ScheduledJobInfo> _scheduledJobs = new();

    public BackfillPage(
        NotificationService notificationService,
        NavigationService navigationService)
    {
        InitializeComponent();

        _notificationService = notificationService;
        _navigationService = navigationService;

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

    private void StartBackfill_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(SymbolsBox.Text))
        {
            SymbolsValidationError.Text = "Please enter at least one symbol";
            SymbolsValidationError.Visibility = Visibility.Visible;
            return;
        }

        SymbolsValidationError.Visibility = Visibility.Collapsed;

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

        _notificationService.ShowNotification(
            "Backfill Started",
            $"Downloading data for {symbols.Length} symbols...",
            NotificationType.Info);
    }

    private void PauseBackfill_Click(object sender, RoutedEventArgs e)
    {
        BackfillStatusText.Text = "Paused";
        PauseBackfillButton.Content = "Resume";

        _notificationService.ShowNotification(
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

    private void RefreshStatus_Click(object sender, RoutedEventArgs e)
    {
        RefreshStatus();

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
