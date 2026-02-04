using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MarketDataCollector.Wpf.Services;

namespace MarketDataCollector.Wpf.Views;

public partial class CollectionSessionPage : Page
{
    private readonly ObservableCollection<SessionHistoryItem> _recentSessions = new();
    private bool _sessionActive;
    private DateTime? _sessionStart;
    private int _symbolsTracked;
    private long _eventsCollected;

    public CollectionSessionPage()
    {
        InitializeComponent();
        SessionHistoryList.ItemsSource = _recentSessions;
    }

    private void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        LoadSessionData();
    }

    private void OnPageUnloaded(object sender, RoutedEventArgs e)
    {
        _recentSessions.Clear();
    }

    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        LoadSessionData();
    }

    private void StartSession_Click(object sender, RoutedEventArgs e)
    {
        if (_sessionActive)
        {
            NotificationService.Instance.ShowNotification(
                "Collection Session",
                "A session is already running.",
                NotificationType.Warning);
            return;
        }

        _sessionActive = true;
        _sessionStart = DateTime.Now;
        _symbolsTracked = 0;
        _eventsCollected = 0;
        UpdateSessionDisplay();

        NotificationService.Instance.ShowNotification(
            "Collection Session",
            "Collection session started.",
            NotificationType.Success);
    }

    private void EndSession_Click(object sender, RoutedEventArgs e)
    {
        if (!_sessionActive || _sessionStart == null)
        {
            NotificationService.Instance.ShowNotification(
                "Collection Session",
                "No active session to end.",
                NotificationType.Info);
            return;
        }

        var duration = DateTime.Now - _sessionStart.Value;
        _recentSessions.Insert(0, new SessionHistoryItem
        {
            StartTime = _sessionStart.Value.ToString("HH:mm"),
            Duration = FormatDuration(duration),
            Symbols = $"{_symbolsTracked} symbols",
            Status = "Completed",
            StatusBrush = new SolidColorBrush(Color.FromRgb(63, 185, 80))
        });

        _sessionActive = false;
        _sessionStart = null;
        UpdateSessionDisplay();

        NotificationService.Instance.ShowNotification(
            "Collection Session",
            "Collection session ended.",
            NotificationType.Info);
    }

    private void LoadSessionData()
    {
        var now = DateTime.Now;
        _sessionActive = true;
        _sessionStart = now.AddHours(-2).AddMinutes(-18);
        _symbolsTracked = 124;
        _eventsCollected = 1_248_900;

        _recentSessions.Clear();
        _recentSessions.Add(new SessionHistoryItem
        {
            StartTime = now.AddHours(-6).ToString("HH:mm"),
            Duration = "1h 12m",
            Symbols = "98 symbols",
            Status = "Completed",
            StatusBrush = new SolidColorBrush(Color.FromRgb(63, 185, 80))
        });
        _recentSessions.Add(new SessionHistoryItem
        {
            StartTime = now.AddDays(-1).AddHours(-3).ToString("HH:mm"),
            Duration = "45m",
            Symbols = "72 symbols",
            Status = "Completed",
            StatusBrush = new SolidColorBrush(Color.FromRgb(63, 185, 80))
        });
        _recentSessions.Add(new SessionHistoryItem
        {
            StartTime = now.AddDays(-1).AddHours(-7).ToString("HH:mm"),
            Duration = "30m",
            Symbols = "50 symbols",
            Status = "Stopped",
            StatusBrush = new SolidColorBrush(Color.FromRgb(255, 193, 7))
        });

        UpdateSessionDisplay();
    }

    private void UpdateSessionDisplay()
    {
        if (_sessionActive && _sessionStart.HasValue)
        {
            var duration = DateTime.Now - _sessionStart.Value;
            SessionStatusText.Text = "Running";
            SessionStatusText.Foreground = new SolidColorBrush(Color.FromRgb(63, 185, 80));
            SessionDurationText.Text = FormatDuration(duration);
            SymbolsTrackedText.Text = _symbolsTracked.ToString("N0");
            EventsCollectedText.Text = _eventsCollected.ToString("N0");

            CurrentStatusText.Text = "Active";
            CurrentStatusText.Foreground = new SolidColorBrush(Color.FromRgb(63, 185, 80));
            CurrentStartText.Text = _sessionStart.Value.ToString("MMM dd, HH:mm");
            CurrentProviderText.Text = "Polygon.io (Streaming)";
            CurrentModeText.Text = "Live + Backfill";
            CurrentNotesText.Text = "Collecting trades and quotes across active watchlists.";

            StartSessionButton.Visibility = Visibility.Collapsed;
            EndSessionButton.Visibility = Visibility.Visible;
        }
        else
        {
            SessionStatusText.Text = "Idle";
            SessionStatusText.Foreground = new SolidColorBrush(Color.FromRgb(139, 148, 158));
            SessionDurationText.Text = "0m";
            SymbolsTrackedText.Text = "0";
            EventsCollectedText.Text = "0";

            CurrentStatusText.Text = "Idle";
            CurrentStatusText.Foreground = new SolidColorBrush(Color.FromRgb(139, 148, 158));
            CurrentStartText.Text = "--";
            CurrentProviderText.Text = "--";
            CurrentModeText.Text = "--";
            CurrentNotesText.Text = "No active collection session.";

            StartSessionButton.Visibility = Visibility.Visible;
            EndSessionButton.Visibility = Visibility.Collapsed;
        }

        NoSessionsText.Visibility = _recentSessions.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        LastUpdatedText.Text = $"Last updated: {DateTime.Now:HH:mm:ss}";
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalHours >= 1)
        {
            return $"{(int)duration.TotalHours}h {duration.Minutes}m";
        }

        return $"{duration.Minutes}m";
    }

    private sealed class SessionHistoryItem
    {
        public string StartTime { get; init; } = string.Empty;
        public string Duration { get; init; } = string.Empty;
        public string Symbols { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
        public Brush StatusBrush { get; init; } = new SolidColorBrush(Color.FromRgb(139, 148, 158));
    }
}
