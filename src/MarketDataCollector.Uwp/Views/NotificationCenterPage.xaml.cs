using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using MarketDataCollector.Uwp.Services;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace MarketDataCollector.Uwp.Views;

/// <summary>
/// Notification Center page showing notification history, active incidents, and snooze rules.
/// Implements Feature Refinement #50 - Notification Center & Incident Timeline.
/// </summary>
public sealed partial class NotificationCenterPage : Page
{
    private readonly NotificationService _notificationService;
    private readonly IntegrityEventsService _integrityEventsService;
    private readonly ObservableCollection<IncidentViewModel> _activeIncidents = new();
    private readonly ObservableCollection<IncidentTimelineItem> _incidentTimeline = new();
    private readonly ObservableCollection<SnoozeRule> _snoozeRules = new();
    private List<NotificationHistoryItem> _allNotifications = new();

    public NotificationCenterPage()
    {
        this.InitializeComponent();
        _notificationService = NotificationService.Instance;
        _integrityEventsService = IntegrityEventsService.Instance;

        ActiveIncidentsList.ItemsSource = _activeIncidents;
        IncidentTimelineList.ItemsSource = _incidentTimeline;
        SnoozeRulesList.ItemsSource = _snoozeRules;

        // Subscribe to new notifications
        _notificationService.NotificationReceived += NotificationService_NotificationReceived;

        Loaded += NotificationCenterPage_Loaded;
        Unloaded += NotificationCenterPage_Unloaded;
    }

    private void NotificationCenterPage_Loaded(object sender, RoutedEventArgs e)
    {
        _ = LoadDataAsync();
    }

    private void NotificationCenterPage_Unloaded(object sender, RoutedEventArgs e)
    {
        _notificationService.NotificationReceived -= NotificationService_NotificationReceived;
    }

    private void NotificationService_NotificationReceived(object? sender, NotificationEventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            _ = LoadDataAsync();
        });
    }

    private async Task LoadDataAsync()
    {
        try
        {
            // Load notification history
            _allNotifications = _notificationService.GetHistory().ToList();
            ApplyFilters();

            // Load active incidents from integrity events service
            await LoadActiveIncidentsAsync();

            // Build incident timeline
            BuildIncidentTimeline();

            // Update summary counts
            UpdateSummaryCounts();

            // Load snooze rules
            LoadSnoozeRules();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NotificationCenterPage] Error loading data: {ex.Message}");
        }
    }

    private async Task LoadActiveIncidentsAsync()
    {
        _activeIncidents.Clear();

        try
        {
            var integrityEvents = await _integrityEventsService.GetRecentEventsAsync(50);
            var activeEvents = integrityEvents
                .Where(e => !e.IsAcknowledged && e.Timestamp > DateTime.Now.AddHours(-24))
                .GroupBy(e => e.Category)
                .Select(g => new IncidentViewModel
                {
                    Id = Guid.NewGuid().ToString(),
                    Title = GetIncidentTitle(g.Key),
                    Description = $"{g.Count()} related events detected",
                    Icon = GetIncidentIcon(g.Key),
                    StartedAt = $"Started: {g.Min(e => e.Timestamp):HH:mm:ss}",
                    Duration = $"Duration: {FormatDuration(DateTime.Now - g.Min(e => e.Timestamp))}",
                    RelatedEvents = $"{g.Count()} events",
                    Category = g.Key
                })
                .ToList();

            foreach (var incident in activeEvents)
            {
                _activeIncidents.Add(incident);
            }

            // Update visibility
            NoActiveIncidentsText.Visibility = _activeIncidents.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            ActiveIncidentsList.Visibility = _activeIncidents.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NotificationCenterPage] Error loading active incidents: {ex.Message}");
        }
    }

    private void BuildIncidentTimeline()
    {
        _incidentTimeline.Clear();

        // Combine notifications and integrity events into a timeline
        var timelineItems = _allNotifications
            .OrderByDescending(n => n.Timestamp)
            .Take(30)
            .Select((n, index) => new IncidentTimelineItem
            {
                Id = index.ToString(),
                Title = n.Title,
                Description = n.Message,
                Timestamp = n.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
                Source = n.Tag,
                TypeLabel = n.Type.ToString(),
                TypeBackground = GetTypeBackground(n.Type),
                StatusColor = GetStatusColor(n.Type),
                ShowConnector = index < 29 ? Visibility.Visible : Visibility.Collapsed,
                NavigationTarget = GetNavigationTarget(n.Tag)
            })
            .ToList();

        foreach (var item in timelineItems)
        {
            _incidentTimeline.Add(item);
        }
    }

    private void ApplyFilters()
    {
        var filtered = _allNotifications.AsEnumerable();

        // Severity filter
        if (SeverityFilter.SelectedItem is ComboBoxItem severityItem &&
            severityItem.Tag?.ToString() != "All")
        {
            var severityStr = severityItem.Tag?.ToString();
            if (Enum.TryParse<NotificationType>(severityStr, out var severity))
            {
                filtered = filtered.Where(n => n.Type == severity);
            }
        }

        // Source filter
        if (SourceFilter.SelectedItem is ComboBoxItem sourceItem &&
            sourceItem.Tag?.ToString() != "All")
        {
            var source = sourceItem.Tag?.ToString();
            filtered = filtered.Where(n => n.Tag.Equals(source, StringComparison.OrdinalIgnoreCase));
        }

        // Time range filter
        if (TimeRangeFilter.SelectedItem is ComboBoxItem timeItem)
        {
            var range = timeItem.Tag?.ToString();
            var cutoff = range switch
            {
                "hour" => DateTime.Now.AddHours(-1),
                "day" => DateTime.Now.AddDays(-1),
                "week" => DateTime.Now.AddDays(-7),
                _ => DateTime.MinValue
            };
            filtered = filtered.Where(n => n.Timestamp >= cutoff);
        }

        var filteredList = filtered.ToList();
        NotificationHistoryList.ItemsSource = filteredList;
        NotificationCountText.Text = $"({filteredList.Count})";
        NoNotificationsText.Visibility = filteredList.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateSummaryCounts()
    {
        var now = DateTime.Now;
        var last24Hours = _allNotifications.Where(n => n.Timestamp >= now.AddDays(-1)).ToList();

        ErrorCountText.Text = last24Hours.Count(n => n.Type == NotificationType.Error).ToString();
        WarningCountText.Text = last24Hours.Count(n => n.Type == NotificationType.Warning).ToString();
        InfoCountText.Text = last24Hours.Count(n => n.Type == NotificationType.Info || n.Type == NotificationType.Success).ToString();
        ActiveIncidentCountText.Text = _activeIncidents.Count.ToString();
    }

    private void LoadSnoozeRules()
    {
        // Load snooze rules from local settings
        _snoozeRules.Clear();

        try
        {
            var localSettings = ApplicationData.Current.LocalSettings;
            var rulesJson = localSettings.Values["SnoozeRules"] as string;

            if (!string.IsNullOrEmpty(rulesJson))
            {
                var rules = System.Text.Json.JsonSerializer.Deserialize<List<SnoozeRule>>(rulesJson);
                if (rules != null)
                {
                    foreach (var rule in rules.Where(r => r.ExpiresAt > DateTime.Now))
                    {
                        rule.ExpiresIn = $"Expires in {FormatDuration(rule.ExpiresAt - DateTime.Now)}";
                        _snoozeRules.Add(rule);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NotificationCenterPage] Error loading snooze rules: {ex.Message}");
        }
    }

    private void SaveSnoozeRules()
    {
        try
        {
            var localSettings = ApplicationData.Current.LocalSettings;
            var rulesJson = System.Text.Json.JsonSerializer.Serialize(_snoozeRules.ToList());
            localSettings.Values["SnoozeRules"] = rulesJson;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NotificationCenterPage] Error saving snooze rules: {ex.Message}");
        }
    }

    #region Event Handlers

    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        _ = LoadDataAsync();
    }

    private void ClearAll_Click(object sender, RoutedEventArgs e)
    {
        _notificationService.ClearHistory();
        _allNotifications.Clear();
        ApplyFilters();
        UpdateSummaryCounts();
    }

    private void SeverityFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ApplyFilters();
    }

    private void SourceFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ApplyFilters();
    }

    private void TimeRangeFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ApplyFilters();
    }

    private void FilterChanged(object sender, RoutedEventArgs e)
    {
        ApplyFilters();
    }

    private void ViewIncident_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string incidentId)
        {
            // Navigate to incident details or expand inline
            System.Diagnostics.Debug.WriteLine($"View incident: {incidentId}");
        }
    }

    private void AcknowledgeIncident_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string incidentId)
        {
            var incident = _activeIncidents.FirstOrDefault(i => i.Id == incidentId);
            if (incident != null)
            {
                _activeIncidents.Remove(incident);
                UpdateSummaryCounts();
            }
        }
    }

    private void ViewIncidentDetails_Click(object sender, RoutedEventArgs e)
    {
        // Show incident details in a dialog or expandable panel
    }

    private void NavigateToSource_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string navigationTarget)
        {
            // Use NavigationService or Frame to navigate
            System.Diagnostics.Debug.WriteLine($"Navigate to: {navigationTarget}");
        }
    }

    private void DismissNotification_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is NotificationHistoryItem notification)
        {
            _allNotifications.Remove(notification);
            ApplyFilters();
            UpdateSummaryCounts();
        }
    }

    private async void ExportTimeline_Click(object sender, RoutedEventArgs e)
    {
        await ExportToFileAsync("incident_timeline", _incidentTimeline);
    }

    private async void ExportNotifications_Click(object sender, RoutedEventArgs e)
    {
        await ExportToFileAsync("notification_history", _allNotifications);
    }

    private async Task ExportToFileAsync<T>(string prefix, IEnumerable<T> data)
    {
        try
        {
            var picker = new FileSavePicker();
            picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            picker.FileTypeChoices.Add("JSON", new List<string> { ".json" });
            picker.FileTypeChoices.Add("CSV", new List<string> { ".csv" });
            picker.SuggestedFileName = $"{prefix}_{DateTime.Now:yyyyMMdd_HHmmss}";

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            var file = await picker.PickSaveFileAsync();
            if (file != null)
            {
                var json = System.Text.Json.JsonSerializer.Serialize(data, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true
                });
                await FileIO.WriteTextAsync(file, json);

                await _notificationService.NotifyAsync(
                    "Export Complete",
                    $"Exported to {file.Name}",
                    NotificationType.Success);
            }
        }
        catch (Exception ex)
        {
            await _notificationService.NotifyErrorAsync("Export Failed", ex.Message);
        }
    }

    private void AddSnoozeRule_Click(object sender, RoutedEventArgs e)
    {
        if (SnoozeSourceCombo.SelectedItem is ComboBoxItem sourceItem &&
            SnoozeDurationCombo.SelectedItem is ComboBoxItem durationItem)
        {
            var source = sourceItem.Tag?.ToString() ?? "unknown";
            var durationMinutes = int.Parse(durationItem.Tag?.ToString() ?? "60");

            var rule = new SnoozeRule
            {
                Id = Guid.NewGuid().ToString(),
                Source = source,
                ExpiresAt = DateTime.Now.AddMinutes(durationMinutes),
                ExpiresIn = $"Expires in {durationMinutes} minutes",
                Reason = $"Snoozed for {durationMinutes} minutes"
            };

            _snoozeRules.Add(rule);
            SaveSnoozeRules();
        }
    }

    private void RemoveSnooze_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string ruleId)
        {
            var rule = _snoozeRules.FirstOrDefault(r => r.Id == ruleId);
            if (rule != null)
            {
                _snoozeRules.Remove(rule);
                SaveSnoozeRules();
            }
        }
    }

    private async void TestNotification_Click(object sender, RoutedEventArgs e)
    {
        await _notificationService.SendTestNotificationAsync();
    }

    private void AcknowledgeAll_Click(object sender, RoutedEventArgs e)
    {
        _activeIncidents.Clear();
        UpdateSummaryCounts();
        NoActiveIncidentsText.Visibility = Visibility.Visible;
        ActiveIncidentsList.Visibility = Visibility.Collapsed;
    }

    private void OpenSettings_Click(object sender, RoutedEventArgs e)
    {
        // Navigate to notification settings
        Frame.Navigate(typeof(SettingsPage));
    }

    private void CreateSupportBundle_Click(object sender, RoutedEventArgs e)
    {
        // Navigate to diagnostics page for support bundle creation
        Frame.Navigate(typeof(DiagnosticsPage));
    }

    #endregion

    #region Helper Methods

    private static string GetIncidentTitle(string category)
    {
        return category switch
        {
            "SequenceGap" => "Sequence Gap Detected",
            "StaleData" => "Stale Data Alert",
            "ValidationFailure" => "Validation Failure",
            "ProviderSwitch" => "Provider Failover",
            "ConnectionIssue" => "Connection Issue",
            _ => $"{category} Incident"
        };
    }

    private static string GetIncidentIcon(string category)
    {
        return category switch
        {
            "SequenceGap" => "\uE783",
            "StaleData" => "\uE823",
            "ValidationFailure" => "\uE7BA",
            "ProviderSwitch" => "\uE943",
            "ConnectionIssue" => "\uE701",
            _ => "\uE946"
        };
    }

    private static SolidColorBrush GetTypeBackground(NotificationType type)
    {
        return type switch
        {
            NotificationType.Error => new SolidColorBrush(Windows.UI.Color.FromArgb(50, 245, 101, 101)),
            NotificationType.Warning => new SolidColorBrush(Windows.UI.Color.FromArgb(50, 237, 137, 54)),
            NotificationType.Success => new SolidColorBrush(Windows.UI.Color.FromArgb(50, 72, 187, 120)),
            _ => new SolidColorBrush(Windows.UI.Color.FromArgb(50, 66, 153, 225))
        };
    }

    private static SolidColorBrush GetStatusColor(NotificationType type)
    {
        return type switch
        {
            NotificationType.Error => new SolidColorBrush(Windows.UI.Color.FromArgb(255, 245, 101, 101)),
            NotificationType.Warning => new SolidColorBrush(Windows.UI.Color.FromArgb(255, 237, 137, 54)),
            NotificationType.Success => new SolidColorBrush(Windows.UI.Color.FromArgb(255, 72, 187, 120)),
            _ => new SolidColorBrush(Windows.UI.Color.FromArgb(255, 66, 153, 225))
        };
    }

    private static string GetNavigationTarget(string tag)
    {
        return tag switch
        {
            "connection" => "page:Provider",
            "backfill" => "page:Backfill",
            "storage" => "page:Storage",
            "datagap" => "page:ArchiveHealth",
            "schedule" => "page:ServiceManager",
            _ => "page:Dashboard"
        };
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalDays >= 1)
            return $"{(int)duration.TotalDays}d {duration.Hours}h";
        if (duration.TotalHours >= 1)
            return $"{duration.Hours}h {duration.Minutes}m";
        if (duration.TotalMinutes >= 1)
            return $"{duration.Minutes}m {duration.Seconds}s";
        return $"{duration.Seconds}s";
    }

    #endregion
}

#region View Models

/// <summary>
/// View model for active incidents.
/// </summary>
public sealed class IncidentViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = "\uE946";
    public string StartedAt { get; set; } = string.Empty;
    public string Duration { get; set; } = string.Empty;
    public string RelatedEvents { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
}

/// <summary>
/// View model for incident timeline items.
/// </summary>
public sealed class IncidentTimelineItem
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Timestamp { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string TypeLabel { get; set; } = string.Empty;
    public SolidColorBrush TypeBackground { get; set; } = new SolidColorBrush(Colors.Gray);
    public SolidColorBrush StatusColor { get; set; } = new SolidColorBrush(Colors.Gray);
    public Visibility ShowConnector { get; set; } = Visibility.Visible;
    public string NavigationTarget { get; set; } = string.Empty;
}

/// <summary>
/// Model for snooze rules.
/// </summary>
public sealed class SnoozeRule
{
    public string Id { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public string ExpiresIn { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}

#endregion

#region Converters

/// <summary>
/// Converts NotificationType to color brush.
/// </summary>
public sealed class NotificationTypeToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is NotificationType type)
        {
            return type switch
            {
                NotificationType.Error => new SolidColorBrush(Windows.UI.Color.FromArgb(255, 245, 101, 101)),
                NotificationType.Warning => new SolidColorBrush(Windows.UI.Color.FromArgb(255, 237, 137, 54)),
                NotificationType.Success => new SolidColorBrush(Windows.UI.Color.FromArgb(255, 72, 187, 120)),
                _ => new SolidColorBrush(Windows.UI.Color.FromArgb(255, 66, 153, 225))
            };
        }
        return new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts NotificationType to icon glyph.
/// </summary>
public sealed class NotificationTypeToIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is NotificationType type)
        {
            return type switch
            {
                NotificationType.Error => "\uEA39",
                NotificationType.Warning => "\uE7BA",
                NotificationType.Success => "\uE73E",
                _ => "\uE946"
            };
        }
        return "\uE946";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

#endregion
