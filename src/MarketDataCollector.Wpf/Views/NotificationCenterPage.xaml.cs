using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MarketDataCollector.Ui.Services;
using WpfServices = MarketDataCollector.Wpf.Services;

namespace MarketDataCollector.Wpf.Views;

/// <summary>
/// Notification Center page for viewing and managing all application notifications.
/// Integrates with AlertService for grouped alert display, playbook-based remediation,
/// and snooze/suppress controls to reduce alert fatigue.
/// </summary>
public partial class NotificationCenterPage : Page
{
    private readonly WpfServices.NotificationService _notificationService;
    private readonly AlertService _alertService;
    private readonly ObservableCollection<NotificationItem> _allNotifications = new();
    private readonly ObservableCollection<NotificationItem> _filteredNotifications = new();
    private bool _suppressFilterEvents;
    private System.Timers.Timer? _alertRefreshTimer;

    public NotificationCenterPage(WpfServices.NotificationService notificationService)
    {
        InitializeComponent();

        _notificationService = notificationService;
        _alertService = AlertService.Instance;
        NotificationsList.ItemsSource = _filteredNotifications;

        // Sync preference checkboxes with current settings
        var settings = _notificationService.GetSettings();
        EnableDesktopNotificationsCheck.IsChecked = settings.Enabled;
        PlayNotificationSoundCheck.IsChecked = settings.SoundType != "None";
        ShowNotificationBadgeCheck.IsChecked = true;
    }

    private void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        _notificationService.NotificationReceived += OnNotificationReceived;
        _alertService.AlertRaised += OnAlertChanged;
        _alertService.AlertResolved += OnAlertChanged;
        LoadNotifications();
        RefreshGroupedAlerts();
        RefreshAlertSummary();

        // Refresh alert display periodically to update snooze expiry
        _alertRefreshTimer = new System.Timers.Timer(10000);
        _alertRefreshTimer.Elapsed += (_, _) => Dispatcher.InvokeAsync(() =>
        {
            RefreshGroupedAlerts();
            RefreshAlertSummary();
        });
        _alertRefreshTimer.Start();
    }

    private void OnPageUnloaded(object sender, RoutedEventArgs e)
    {
        _notificationService.NotificationReceived -= OnNotificationReceived;
        _alertService.AlertRaised -= OnAlertChanged;
        _alertService.AlertResolved -= OnAlertChanged;
        _alertRefreshTimer?.Stop();
        _alertRefreshTimer?.Dispose();
    }

    private void OnAlertChanged(object? sender, AlertEventArgs e)
    {
        Dispatcher.InvokeAsync(() =>
        {
            RefreshGroupedAlerts();
            RefreshAlertSummary();
        });
    }

    private void OnNotificationReceived(object? sender, WpfServices.NotificationEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            var item = CreateNotificationItem(
                e.Title,
                e.Message,
                e.Type,
                DateTime.Now);

            _allNotifications.Insert(0, item);
            ApplyFilters();
            UpdateCounters();
        });
    }

    private void LoadNotifications()
    {
        _allNotifications.Clear();

        var history = _notificationService.GetHistory();

        if (history.Count > 0)
        {
            foreach (var historyItem in history)
            {
                var item = CreateNotificationItem(
                    historyItem.Title,
                    historyItem.Message,
                    historyItem.Type,
                    historyItem.Timestamp);
                item.IsRead = historyItem.IsRead;

                _allNotifications.Add(item);
            }
        }

        ApplyFilters();
        UpdateCounters();
    }

    // ─── Alert Grouping ────────────────────────────────────────────

    private void RefreshGroupedAlerts()
    {
        GroupedAlertsPanel.Children.Clear();

        var groups = _alertService.GetGroupedAlerts();

        if (groups.Count == 0)
        {
            NoGroupedAlertsPanel.Visibility = Visibility.Visible;
            GroupedAlertsPanel.Visibility = Visibility.Collapsed;
            return;
        }

        NoGroupedAlertsPanel.Visibility = Visibility.Collapsed;
        GroupedAlertsPanel.Visibility = Visibility.Visible;

        foreach (var group in groups)
        {
            var card = BuildAlertGroupCard(group);
            GroupedAlertsPanel.Children.Add(card);
        }
    }

    private void RefreshAlertSummary()
    {
        var summary = _alertService.GetSummary();

        AlertTotalText.Text = $"{summary.TotalActive} active";

        CriticalBadge.Visibility = summary.CriticalCount > 0 ? Visibility.Visible : Visibility.Collapsed;
        CriticalCountText.Text = $"{summary.CriticalCount} Critical";

        ErrorBadge.Visibility = summary.ErrorCount > 0 ? Visibility.Visible : Visibility.Collapsed;
        ErrorCountText.Text = $"{summary.ErrorCount} Error{(summary.ErrorCount != 1 ? "s" : "")}";

        WarningBadge.Visibility = summary.WarningCount > 0 ? Visibility.Visible : Visibility.Collapsed;
        WarningCountText.Text = $"{summary.WarningCount} Warning{(summary.WarningCount != 1 ? "s" : "")}";

        SnoozedCountText.Text = summary.SnoozedCount > 0
            ? $"{summary.SnoozedCount} snoozed"
            : string.Empty;
    }

    private Border BuildAlertGroupCard(AlertGroup group)
    {
        var severityColor = group.Severity switch
        {
            AlertSeverity.Critical or AlertSeverity.Emergency => (Brush)FindResource("ErrorColorBrush"),
            AlertSeverity.Error => new SolidColorBrush(Color.FromRgb(255, 87, 34)),
            AlertSeverity.Warning => (Brush)FindResource("WarningColorBrush"),
            _ => (Brush)FindResource("InfoColorBrush")
        };

        var card = new Border
        {
            Background = (Brush)FindResource("ConsoleBackgroundLightBrush"),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12, 10, 12, 10),
            BorderBrush = (Brush)FindResource("ConsoleBorderBrush"),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 0, 0, 8)
        };

        var outerGrid = new Grid();
        outerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        outerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        outerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Severity indicator
        var indicator = new Border
        {
            Width = 4,
            CornerRadius = new CornerRadius(2),
            Background = severityColor,
            Margin = new Thickness(0, 0, 12, 0),
            VerticalAlignment = VerticalAlignment.Stretch
        };
        Grid.SetColumn(indicator, 0);
        outerGrid.Children.Add(indicator);

        // Content
        var contentPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(contentPanel, 1);

        // Title row
        var titlePanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
        titlePanel.Children.Add(new TextBlock
        {
            Text = group.Title,
            FontWeight = FontWeights.SemiBold,
            FontSize = 13,
            Foreground = (Brush)FindResource("ConsoleTextPrimaryBrush")
        });

        // Category badge
        titlePanel.Children.Add(new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(30, 139, 148, 158)),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(6, 2, 6, 2),
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = group.Category,
                FontSize = 10,
                Foreground = (Brush)FindResource("ConsoleTextMutedBrush")
            }
        });

        // Occurrence count
        if (group.Count > 1)
        {
            titlePanel.Children.Add(new Border
            {
                Background = severityColor,
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(6, 1, 6, 1),
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = $"x{group.Count}",
                    FontSize = 10,
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.SemiBold
                }
            });
        }

        contentPanel.Children.Add(titlePanel);

        // Affected resources
        if (group.AffectedResources.Count > 0)
        {
            var resources = string.Join(", ", group.AffectedResources.Take(5));
            if (group.AffectedResources.Count > 5)
                resources += $" +{group.AffectedResources.Count - 5} more";

            contentPanel.Children.Add(new TextBlock
            {
                Text = $"Affected: {resources}",
                FontSize = 12,
                Foreground = (Brush)FindResource("ConsoleTextSecondaryBrush"),
                Margin = new Thickness(0, 0, 0, 2)
            });
        }

        // Time info
        contentPanel.Children.Add(new TextBlock
        {
            Text = $"First: {FormatTimestamp(group.FirstOccurred)}  |  Last: {FormatTimestamp(group.LastOccurred)}",
            FontSize = 11,
            Foreground = (Brush)FindResource("ConsoleTextMutedBrush")
        });

        outerGrid.Children.Add(contentPanel);

        // Action buttons
        var actionsPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(actionsPanel, 2);

        // Show Playbook button (only if a playbook exists)
        if (group.RepresentativeAlert.Playbook != null)
        {
            var playbookButton = new Button
            {
                Content = "Playbook",
                Style = (Style)FindResource("SecondaryButtonStyle"),
                Margin = new Thickness(4, 0, 0, 4),
                Padding = new Thickness(8, 4, 8, 4),
                FontSize = 11,
                Tag = group.RepresentativeAlert.Id
            };
            playbookButton.Click += ShowPlaybook_Click;
            actionsPanel.Children.Add(playbookButton);
        }

        // Snooze button
        var snoozeButton = new Button
        {
            Content = "Snooze 1h",
            Style = (Style)FindResource("GhostButtonStyle"),
            Margin = new Thickness(4, 0, 0, 4),
            Padding = new Thickness(8, 4, 8, 4),
            FontSize = 11,
            Tag = group.RepresentativeAlert.Id
        };
        snoozeButton.Click += SnoozeAlert_Click;
        actionsPanel.Children.Add(snoozeButton);

        // Suppress button
        var suppressButton = new Button
        {
            Content = "Suppress",
            Style = (Style)FindResource("GhostButtonStyle"),
            Margin = new Thickness(4, 0, 0, 0),
            Padding = new Thickness(8, 4, 8, 4),
            FontSize = 11,
            Tag = $"{group.Category}|{group.Title}"
        };
        suppressButton.Click += SuppressAlert_Click;
        actionsPanel.Children.Add(suppressButton);

        outerGrid.Children.Add(actionsPanel);
        card.Child = outerGrid;
        return card;
    }

    // ─── Playbook Display ──────────────────────────────────────────

    private void ShowPlaybook_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string alertId)
            return;

        var alert = _alertService.GetActiveAlerts().FirstOrDefault(a => a.Id == alertId);
        if (alert?.Playbook == null)
            return;

        var playbook = alert.Playbook;
        PlaybookTitle.Text = playbook.Title;
        PlaybookWhatHappened.Text = playbook.WhatHappened;

        // Possible causes
        PlaybookCausesPanel.Children.Clear();
        foreach (var cause in playbook.PossibleCauses)
        {
            var causePanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
            causePanel.Children.Add(new TextBlock
            {
                Text = "\u2022",
                FontSize = 13,
                Foreground = (Brush)FindResource("ConsoleTextMutedBrush"),
                Margin = new Thickness(8, 0, 8, 0)
            });
            causePanel.Children.Add(new TextBlock
            {
                Text = cause,
                FontSize = 12,
                Foreground = (Brush)FindResource("ConsoleTextSecondaryBrush"),
                TextWrapping = TextWrapping.Wrap
            });
            PlaybookCausesPanel.Children.Add(causePanel);
        }

        // Remediation steps
        PlaybookStepsPanel.Children.Clear();
        foreach (var step in playbook.RemediationSteps)
        {
            var stepBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(15, 88, 166, 255)),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12, 8, 12, 8),
                Margin = new Thickness(0, 0, 0, 6)
            };

            var stepPanel = new StackPanel { Orientation = Orientation.Horizontal };
            stepPanel.Children.Add(new Border
            {
                Width = 24,
                Height = 24,
                CornerRadius = new CornerRadius(12),
                Background = (Brush)FindResource("InfoColorBrush"),
                Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = step.Priority.ToString(),
                    FontSize = 11,
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.SemiBold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            });

            var stepContent = new StackPanel();
            stepContent.Children.Add(new TextBlock
            {
                Text = step.Title,
                FontWeight = FontWeights.SemiBold,
                FontSize = 12,
                Foreground = (Brush)FindResource("ConsoleTextPrimaryBrush")
            });
            stepContent.Children.Add(new TextBlock
            {
                Text = step.Description,
                FontSize = 11,
                Foreground = (Brush)FindResource("ConsoleTextSecondaryBrush"),
                TextWrapping = TextWrapping.Wrap
            });
            stepPanel.Children.Add(stepContent);

            stepBorder.Child = stepPanel;
            PlaybookStepsPanel.Children.Add(stepBorder);
        }

        PlaybookIgnoredText.Text = playbook.WhatHappensIfIgnored;
        PlaybookPanel.Visibility = Visibility.Visible;
    }

    private void ClosePlaybook_Click(object sender, RoutedEventArgs e)
    {
        PlaybookPanel.Visibility = Visibility.Collapsed;
    }

    // ─── Snooze / Suppress ─────────────────────────────────────────

    private void SnoozeAlert_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string alertId)
            return;

        _alertService.SnoozeAlert(alertId, TimeSpan.FromHours(1));
        _notificationService.ShowNotification(
            "Alert Snoozed",
            "Alert snoozed for 1 hour.",
            WpfServices.NotificationType.Info);

        RefreshGroupedAlerts();
        RefreshAlertSummary();
    }

    private void SuppressAlert_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string tagValue)
            return;

        var parts = tagValue.Split('|', 2);
        if (parts.Length < 2)
            return;

        var category = parts[0];
        var titlePattern = parts[1];

        _alertService.AddSuppressionRule(category, titlePattern, TimeSpan.FromHours(24));
        _notificationService.ShowNotification(
            "Alert Suppressed",
            $"Similar alerts in \"{category}\" will be suppressed for 24 hours.",
            WpfServices.NotificationType.Info);

        RefreshGroupedAlerts();
        RefreshAlertSummary();
    }

    // ─── Existing Notification Logic ───────────────────────────────

    private NotificationItem CreateNotificationItem(
        string title,
        string message,
        WpfServices.NotificationType type,
        DateTime timestamp)
    {
        var (icon, iconColor, iconBackground, typeBackground, typeName) = type switch
        {
            WpfServices.NotificationType.Error => (
                (string)FindResource("IconError"),
                (Brush)FindResource("ErrorColorBrush"),
                (Brush)FindResource("ErrorColorBrush"),
                (Brush)FindResource("ConsoleAccentRedAlpha10Brush"),
                "Error"),
            WpfServices.NotificationType.Warning => (
                (string)FindResource("IconWarning"),
                (Brush)FindResource("WarningColorBrush"),
                (Brush)FindResource("WarningColorBrush"),
                (Brush)FindResource("ConsoleAccentOrangeAlpha10Brush"),
                "Warning"),
            WpfServices.NotificationType.Success => (
                (string)FindResource("IconSuccess"),
                (Brush)FindResource("SuccessColorBrush"),
                (Brush)FindResource("SuccessColorBrush"),
                (Brush)FindResource("ConsoleAccentGreenAlpha10Brush"),
                "Success"),
            _ => (
                (string)FindResource("IconInfo"),
                (Brush)FindResource("InfoColorBrush"),
                (Brush)FindResource("InfoColorBrush"),
                (Brush)FindResource("ConsoleAccentBlueAlpha10Brush"),
                "Info")
        };

        return new NotificationItem
        {
            Icon = icon,
            IconColor = iconColor,
            IconBackground = iconBackground,
            TypeBackground = typeBackground,
            Title = title,
            Message = message,
            Timestamp = FormatTimestamp(timestamp),
            Type = typeName,
            RawTimestamp = timestamp,
            NotificationType = type
        };
    }

    private void MarkAllRead_Click(object sender, RoutedEventArgs e)
    {
        foreach (var item in _allNotifications)
        {
            item.IsRead = true;
        }

        var history = _notificationService.GetHistory();
        for (var i = 0; i < history.Count; i++)
        {
            _notificationService.MarkAsRead(i);
        }

        UpdateCounters();
    }

    private void ClearAll_Click(object sender, RoutedEventArgs e)
    {
        _allNotifications.Clear();
        _notificationService.ClearHistory();
        ApplyFilters();
        UpdateCounters();
    }

    private void FilterChanged(object sender, RoutedEventArgs e)
    {
        if (_suppressFilterEvents)
        {
            return;
        }

        if (sender == FilterAllCheck)
        {
            var isChecked = FilterAllCheck.IsChecked == true;
            _suppressFilterEvents = true;
            FilterErrorsCheck.IsChecked = isChecked;
            FilterWarningsCheck.IsChecked = isChecked;
            FilterInfoCheck.IsChecked = isChecked;
            FilterSuccessCheck.IsChecked = isChecked;
            _suppressFilterEvents = false;
        }
        else
        {
            _suppressFilterEvents = true;
            var allChecked = FilterErrorsCheck.IsChecked == true
                && FilterWarningsCheck.IsChecked == true
                && FilterInfoCheck.IsChecked == true
                && FilterSuccessCheck.IsChecked == true;
            FilterAllCheck.IsChecked = allChecked;
            _suppressFilterEvents = false;
        }

        ApplyFilters();
        UpdateCounters();
    }

    private void ApplyFilters()
    {
        var showErrors = FilterErrorsCheck.IsChecked == true;
        var showWarnings = FilterWarningsCheck.IsChecked == true;
        var showInfo = FilterInfoCheck.IsChecked == true;
        var showSuccess = FilterSuccessCheck.IsChecked == true;

        _filteredNotifications.Clear();

        foreach (var item in _allNotifications)
        {
            var shouldShow = item.NotificationType switch
            {
                WpfServices.NotificationType.Error => showErrors,
                WpfServices.NotificationType.Warning => showWarnings,
                WpfServices.NotificationType.Success => showSuccess,
                WpfServices.NotificationType.Info => showInfo,
                _ => showInfo
            };

            if (shouldShow)
            {
                _filteredNotifications.Add(item);
            }
        }

        EmptyStatePanel.Visibility = _filteredNotifications.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;
        NotificationsList.Visibility = _filteredNotifications.Count > 0
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void UpdateCounters()
    {
        var totalFiltered = _filteredNotifications.Count;
        NotificationCountText.Text = totalFiltered == 1
            ? "1 notification"
            : $"{totalFiltered} notifications";

        var unreadCount = _allNotifications.Count(n => !n.IsRead);
        if (unreadCount > 0)
        {
            UnreadBadge.Visibility = Visibility.Visible;
            UnreadCountText.Text = unreadCount.ToString("N0");
        }
        else
        {
            UnreadBadge.Visibility = Visibility.Collapsed;
        }
    }

    private static string FormatTimestamp(DateTime timestamp)
    {
        var elapsed = DateTime.Now - timestamp;

        return elapsed.TotalSeconds switch
        {
            < 60 => "Just now",
            < 3600 => $"{(int)elapsed.TotalMinutes}m ago",
            < 86400 => $"{(int)elapsed.TotalHours}h ago",
            < 172800 => "Yesterday",
            _ => timestamp.ToString("MMM dd, HH:mm")
        };
    }

    /// <summary>
    /// Display model for a single notification item in the list.
    /// </summary>
    public sealed class NotificationItem
    {
        public string Icon { get; set; } = string.Empty;
        public Brush IconColor { get; set; } = Brushes.Transparent;
        public Brush IconBackground { get; set; } = Brushes.Transparent;
        public Brush TypeBackground { get; set; } = Brushes.Transparent;
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Timestamp { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public DateTime RawTimestamp { get; set; }
        public WpfServices.NotificationType NotificationType { get; set; }
        public bool IsRead { get; set; }
    }
}
