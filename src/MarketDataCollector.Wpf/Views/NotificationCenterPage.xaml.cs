using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WpfServices = MarketDataCollector.Wpf.Services;

namespace MarketDataCollector.Wpf.Views;

/// <summary>
/// Notification Center page for viewing and managing all application notifications.
/// Displays notification history from the NotificationService with filtering,
/// mark-as-read, and preference management.
/// </summary>
public partial class NotificationCenterPage : Page
{
    private readonly WpfServices.NotificationService _notificationService;
    private readonly ObservableCollection<NotificationItem> _allNotifications = new();
    private readonly ObservableCollection<NotificationItem> _filteredNotifications = new();
    private bool _suppressFilterEvents;

    public NotificationCenterPage(WpfServices.NotificationService notificationService)
    {
        InitializeComponent();

        _notificationService = notificationService;
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
        LoadNotifications();
    }

    private void OnPageUnloaded(object sender, RoutedEventArgs e)
    {
        _notificationService.NotificationReceived -= OnNotificationReceived;
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

        // Also mark all as read in the service
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

        // If "All" checkbox changed, sync all other checkboxes
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
            // Update "All" checkbox based on individual filter state
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

        // Toggle empty state visibility
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
