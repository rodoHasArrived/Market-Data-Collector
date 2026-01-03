using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using MarketDataCollector.Uwp.Models;
using MarketDataCollector.Uwp.Services;

namespace MarketDataCollector.Uwp.Views;

/// <summary>
/// Page for managing collection sessions (#65/27 - P0 Critical).
/// </summary>
public sealed partial class CollectionSessionPage : Page
{
    private readonly CollectionSessionService _sessionService;
    private readonly ObservableCollection<CollectionSession> _sessions;
    private CollectionSession? _activeSession;
    private DispatcherTimer? _refreshTimer;

    public CollectionSessionPage()
    {
        this.InitializeComponent();
        _sessionService = CollectionSessionService.Instance;
        _sessions = new ObservableCollection<CollectionSession>();
        SessionsList.ItemsSource = _sessions;

        Loaded += Page_Loaded;
        Unloaded += Page_Unloaded;

        // Subscribe to events
        _sessionService.SessionStarted += OnSessionStarted;
        _sessionService.SessionCompleted += OnSessionCompleted;
        _sessionService.StatisticsUpdated += OnStatisticsUpdated;
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadSessionsAsync();
        await LoadSettingsAsync();
        StartRefreshTimer();
    }

    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        StopRefreshTimer();
        _sessionService.SessionStarted -= OnSessionStarted;
        _sessionService.SessionCompleted -= OnSessionCompleted;
        _sessionService.StatisticsUpdated -= OnStatisticsUpdated;
    }

    private async Task LoadSessionsAsync()
    {
        var sessions = await _sessionService.GetSessionsAsync();
        _sessions.Clear();

        foreach (var session in sessions.OrderByDescending(s => s.CreatedAt))
        {
            _sessions.Add(session);
        }

        SessionCountText.Text = $"({_sessions.Count} sessions)";
        NoSessionsText.Visibility = _sessions.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        // Check for active session
        _activeSession = await _sessionService.GetActiveSessionAsync();
        UpdateActiveSessionCard();
    }

    private async Task LoadSettingsAsync()
    {
        var config = await _sessionService.LoadSessionsAsync();
        AutoCreateDailyToggle.IsOn = config.AutoCreateDailySessions;
        SessionNamingPatternBox.Text = config.SessionNamingPattern;
        GenerateManifestToggle.IsOn = config.GenerateManifestOnComplete;
        RetainHistoryBox.Value = config.RetainSessionHistory;
    }

    private void UpdateActiveSessionCard()
    {
        if (_activeSession != null && _activeSession.Status == "Active")
        {
            ActiveSessionCard.Visibility = Visibility.Visible;
            ActiveSessionName.Text = _activeSession.Name;
            ActiveSessionStarted.Text = $"Started: {_activeSession.StartedAt?.ToString("g") ?? "--"}";
            ActiveSessionSymbols.Text = _activeSession.Symbols.Length.ToString();

            if (_activeSession.Statistics != null)
            {
                ActiveSessionEvents.Text = FormatNumber(_activeSession.Statistics.TotalEvents);
                ActiveSessionQuality.Text = $"{_activeSession.QualityScore:F0}%";
                TradeEventsCount.Text = FormatNumber(_activeSession.Statistics.TradeEvents);
                QuoteEventsCount.Text = FormatNumber(_activeSession.Statistics.QuoteEvents);
                DepthEventsCount.Text = FormatNumber(_activeSession.Statistics.DepthEvents);
                EventsPerSecond.Text = $"{_activeSession.Statistics.EventsPerSecond:F0}/s";
            }
        }
        else
        {
            ActiveSessionCard.Visibility = Visibility.Collapsed;
        }
    }

    private void StartRefreshTimer()
    {
        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _refreshTimer.Tick += async (s, e) =>
        {
            if (_activeSession != null)
            {
                _activeSession = await _sessionService.GetActiveSessionAsync();
                UpdateActiveSessionCard();
            }
        };
        _refreshTimer.Start();
    }

    private void StopRefreshTimer()
    {
        _refreshTimer?.Stop();
        _refreshTimer = null;
    }

    private async void NewSession_Click(object sender, RoutedEventArgs e)
    {
        NewSessionNameBox.Text = $"{DateTime.Now:yyyy-MM-dd}-custom";
        NewSessionDescriptionBox.Text = "";
        NewSessionTagsBox.Text = "";
        StartImmediatelyCheck.IsChecked = true;

        await NewSessionDialog.ShowAsync();
    }

    private async void CreateDaily_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var session = await _sessionService.CreateDailySessionAsync();
            await _sessionService.StartSessionAsync(session.Id);
            await LoadSessionsAsync();
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Failed to create daily session", ex.Message);
        }
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        await LoadSessionsAsync();
    }

    private async void NewSessionDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        var name = NewSessionNameBox.Text?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            args.Cancel = true;
            return;
        }

        try
        {
            var description = NewSessionDescriptionBox.Text?.Trim();
            var tags = NewSessionTagsBox.Text?.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim()).ToArray();

            var session = await _sessionService.CreateSessionAsync(name, description, tags);

            if (StartImmediatelyCheck.IsChecked == true)
            {
                await _sessionService.StartSessionAsync(session.Id);
            }

            await LoadSessionsAsync();
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Failed to create session", ex.Message);
        }
    }

    private async void PauseSession_Click(object sender, RoutedEventArgs e)
    {
        if (_activeSession == null) return;

        try
        {
            if (_activeSession.Status == "Active")
            {
                await _sessionService.PauseSessionAsync(_activeSession.Id);
                PauseSessionButton.Content = new FontIcon { Glyph = "\uE768", FontSize = 14 };
            }
            else if (_activeSession.Status == "Paused")
            {
                await _sessionService.ResumeSessionAsync(_activeSession.Id);
                PauseSessionButton.Content = new FontIcon { Glyph = "\uE769", FontSize = 14 };
            }

            _activeSession = await _sessionService.GetActiveSessionAsync();
            UpdateActiveSessionCard();
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Failed to pause/resume session", ex.Message);
        }
    }

    private async void StopSession_Click(object sender, RoutedEventArgs e)
    {
        if (_activeSession == null) return;

        var dialog = new ContentDialog
        {
            Title = "Stop Session",
            Content = $"Are you sure you want to stop the session '{_activeSession.Name}'?\n\nA manifest will be generated for the collected data.",
            PrimaryButtonText = "Stop",
            SecondaryButtonText = "Cancel",
            XamlRoot = this.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            try
            {
                await _sessionService.StopSessionAsync(_activeSession.Id, GenerateManifestToggle.IsOn);
                await LoadSessionsAsync();
            }
            catch (Exception ex)
            {
                await ShowErrorAsync("Failed to stop session", ex.Message);
            }
        }
    }

    private async void ViewSessionDetails_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is CollectionSession session)
        {
            var summary = _sessionService.GenerateSessionSummary(session);
            DetailsSummaryText.Text = summary;
            SessionDetailsDialog.Title = $"Session: {session.Name}";
            await SessionDetailsDialog.ShowAsync();
        }
    }

    private async void ExportSession_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is CollectionSession session)
        {
            try
            {
                var manifestService = ManifestService.Instance;
                var (manifest, path) = await manifestService.GenerateManifestForSessionAsync(session);

                await ShowInfoAsync("Manifest Exported", $"Manifest saved to:\n{path}");
            }
            catch (Exception ex)
            {
                await ShowErrorAsync("Failed to export manifest", ex.Message);
            }
        }
    }

    private async void AutoCreateDaily_Toggled(object sender, RoutedEventArgs e)
    {
        var config = await _sessionService.LoadSessionsAsync();
        config.AutoCreateDailySessions = AutoCreateDailyToggle.IsOn;
        await _sessionService.SaveSessionsAsync();
    }

    private void OnSessionStarted(object? sender, CollectionSessionEventArgs e)
    {
        DispatcherQueue.TryEnqueue(async () =>
        {
            await LoadSessionsAsync();
        });
    }

    private void OnSessionCompleted(object? sender, CollectionSessionEventArgs e)
    {
        DispatcherQueue.TryEnqueue(async () =>
        {
            await LoadSessionsAsync();
        });
    }

    private void OnStatisticsUpdated(object? sender, CollectionSessionEventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (e.Session?.Id == _activeSession?.Id)
            {
                _activeSession = e.Session;
                UpdateActiveSessionCard();
            }
        });
    }

    private static string FormatNumber(long number)
    {
        if (number >= 1_000_000_000)
            return $"{number / 1_000_000_000.0:F1}B";
        if (number >= 1_000_000)
            return $"{number / 1_000_000.0:F1}M";
        if (number >= 1_000)
            return $"{number / 1_000.0:F1}K";
        return number.ToString("N0");
    }

    private async Task ShowErrorAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = "OK",
            XamlRoot = this.XamlRoot
        };
        await dialog.ShowAsync();
    }

    private async Task ShowInfoAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = "OK",
            XamlRoot = this.XamlRoot
        };
        await dialog.ShowAsync();
    }
}
