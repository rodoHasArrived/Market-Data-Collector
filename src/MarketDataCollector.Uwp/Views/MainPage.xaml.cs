using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using MarketDataCollector.Uwp.ViewModels;
using MarketDataCollector.Uwp.Services;

namespace MarketDataCollector.Uwp.Views;

/// <summary>
/// Main dashboard page with navigation to different sections.
/// </summary>
public sealed partial class MainPage : Page
{
    public MainViewModel ViewModel { get; }
    private readonly SearchService _searchService;
    private readonly FirstRunService _firstRunService;
    private readonly NotificationService _notificationService;
    private readonly ConnectionService _connectionService;
    private readonly DispatcherTimer _notificationDismissTimer;
    private string? _currentNotificationAction;

    public MainPage()
    {
        this.InitializeComponent();
        ViewModel = new MainViewModel();
        DataContext = ViewModel;
        _searchService = SearchService.Instance;
        _firstRunService = new FirstRunService();
        _notificationService = NotificationService.Instance;
        _connectionService = ConnectionService.Instance;

        // Setup notification dismiss timer
        _notificationDismissTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(8) };
        _notificationDismissTimer.Tick += NotificationDismissTimer_Tick;

        // Subscribe to notification events
        _notificationService.NotificationReceived += NotificationService_NotificationReceived;
        _connectionService.StateChanged += ConnectionService_StateChanged;

        Loaded += MainPage_Loaded;
        Unloaded += MainPage_Unloaded;
    }

    private async void MainPage_Loaded(object sender, RoutedEventArgs e)
    {
        // Check for first run and show welcome wizard
        if (await _firstRunService.IsFirstRunAsync())
        {
            ContentFrame.Navigate(typeof(WelcomePage));
            await _firstRunService.InitializeAsync();
            return;
        }

        await ViewModel.LoadAsync();

        // Start connection monitoring
        _connectionService.StartMonitoring();
    }

    private void MainPage_Unloaded(object sender, RoutedEventArgs e)
    {
        _notificationService.NotificationReceived -= NotificationService_NotificationReceived;
        _connectionService.StateChanged -= ConnectionService_StateChanged;
        _notificationDismissTimer.Stop();
    }

    #region In-App Notification Banner

    private void NotificationService_NotificationReceived(object? sender, NotificationEventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            ShowInAppNotification(e.Title, e.Message, e.Type, GetNotificationAction(e.Tag));
        });
    }

    private void ConnectionService_StateChanged(object? sender, ConnectionStateChangedEventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (e.NewState == ConnectionState.Error || e.NewState == ConnectionState.Disconnected)
            {
                if (e.OldState == ConnectionState.Connected)
                {
                    ShowInAppNotification(
                        "Connection Lost",
                        $"Lost connection to {e.Provider}. Attempting to reconnect...",
                        NotificationType.Error,
                        "action:logs");
                }
            }
            else if (e.NewState == ConnectionState.Connected && e.OldState != ConnectionState.Connected)
            {
                ShowInAppNotification(
                    "Connected",
                    $"Successfully connected to {e.Provider}",
                    NotificationType.Success);
            }
        });
    }

    private void ShowInAppNotification(string title, string message, NotificationType type, string? action = null)
    {
        NotificationTitle.Text = title;
        NotificationMessage.Text = message;
        _currentNotificationAction = action;

        // Set background color and icon based on type
        (NotificationBanner.Background, NotificationIcon.Glyph) = type switch
        {
            NotificationType.Success => (new SolidColorBrush(Windows.UI.Color.FromArgb(255, 72, 187, 120)), "\uE73E"),
            NotificationType.Warning => (new SolidColorBrush(Windows.UI.Color.FromArgb(255, 237, 137, 54)), "\uE7BA"),
            NotificationType.Error => (new SolidColorBrush(Windows.UI.Color.FromArgb(255, 245, 101, 101)), "\uEA39"),
            _ => (new SolidColorBrush(Windows.UI.Color.FromArgb(255, 88, 166, 255)), "\uE946")
        };

        // Show action button if there's an action
        NotificationActionButton.Visibility = !string.IsNullOrEmpty(action)
            ? Visibility.Visible
            : Visibility.Collapsed;

        NotificationBanner.Visibility = Visibility.Visible;

        // Start auto-dismiss timer
        _notificationDismissTimer.Stop();
        _notificationDismissTimer.Start();
    }

    private void DismissNotification_Click(object sender, RoutedEventArgs e)
    {
        _notificationDismissTimer.Stop();
        NotificationBanner.Visibility = Visibility.Collapsed;
    }

    private void NotificationDismissTimer_Tick(object? sender, object e)
    {
        _notificationDismissTimer.Stop();
        NotificationBanner.Visibility = Visibility.Collapsed;
    }

    private void NotificationAction_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_currentNotificationAction))
        {
            HandleSearchNavigation(_currentNotificationAction);
        }
        DismissNotification_Click(sender, e);
    }

    private static string? GetNotificationAction(string tag)
    {
        return tag switch
        {
            "connection" => "page:Provider",
            "reconnect" => "page:Provider",
            "error" => "action:logs",
            "backfill" => "page:Backfill",
            "datagap" => "page:ArchiveHealth",
            "storage" => "page:Storage",
            "schedule" => "page:ServiceManager",
            _ => null
        };
    }

    #endregion

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected)
        {
            ContentFrame.Navigate(typeof(SettingsPage));
            return;
        }

        if (args.SelectedItem is NavigationViewItem item)
        {
            var tag = item.Tag?.ToString();
            NavigateToPage(tag);
        }
    }

    private void NavigateToPage(string? tag)
    {
        switch (tag)
        {
            case "Dashboard":
                ContentFrame.Navigate(typeof(DashboardPage));
                break;
            case "Watchlist":
                ContentFrame.Navigate(typeof(WatchlistPage));
                break;
            case "Provider":
                ContentFrame.Navigate(typeof(ProviderPage));
                break;
            case "DataSources":
                ContentFrame.Navigate(typeof(DataSourcesPage));
                break;
            case "Plugins":
                ContentFrame.Navigate(typeof(PluginsPage));
                break;
            case "Storage":
                ContentFrame.Navigate(typeof(StoragePage));
                break;
            case "Symbols":
                ContentFrame.Navigate(typeof(SymbolsPage));
                break;
            case "SymbolMapping":
                ContentFrame.Navigate(typeof(SymbolMappingPage));
                break;
            case "Backfill":
                ContentFrame.Navigate(typeof(BackfillPage));
                break;
            case "Schedules":
                ContentFrame.Navigate(typeof(ScheduleManagerPage));
                break;
            case "CollectionSessions":
                ContentFrame.Navigate(typeof(CollectionSessionPage));
                break;
            case "ArchiveHealth":
                ContentFrame.Navigate(typeof(ArchiveHealthPage));
                break;
            case "ServiceManager":
                ContentFrame.Navigate(typeof(ServiceManagerPage));
                break;
            case "DataExport":
                ContentFrame.Navigate(typeof(DataExportPage));
                break;
            case "TradingHours":
                ContentFrame.Navigate(typeof(TradingHoursPage));
                break;
            case "Help":
                ContentFrame.Navigate(typeof(HelpPage));
                break;
            case "Welcome":
                ContentFrame.Navigate(typeof(WelcomePage));
                break;
            case "DataQuality":
                ContentFrame.Navigate(typeof(DataQualityPage));
                break;
            case "LiveData":
                ContentFrame.Navigate(typeof(LiveDataViewerPage));
                break;
            case "SystemHealth":
                ContentFrame.Navigate(typeof(SystemHealthPage));
                break;
            case "PortfolioImport":
                ContentFrame.Navigate(typeof(PortfolioImportPage));
                break;
            case "IndexSubscription":
                ContentFrame.Navigate(typeof(IndexSubscriptionPage));
                break;
            case "Diagnostics":
                ContentFrame.Navigate(typeof(DiagnosticsPage));
                break;
            case "EventReplay":
                ContentFrame.Navigate(typeof(EventReplayPage));
                break;
            case "PackageManager":
                ContentFrame.Navigate(typeof(PackageManagerPage));
                break;
            case "AnalysisExport":
                ContentFrame.Navigate(typeof(AnalysisExportPage));
                break;
            case "LeanIntegration":
                ContentFrame.Navigate(typeof(LeanIntegrationPage));
                break;
            case "MessagingHub":
                ContentFrame.Navigate(typeof(MessagingHubPage));
                break;
        }
    }

    private async void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
        {
            var suggestions = await _searchService.GetSuggestionsAsync(sender.Text);
            sender.ItemsSource = suggestions.Select(s => new SearchSuggestionDisplay
            {
                Text = s.Text,
                Category = s.Category,
                Icon = s.Icon,
                NavigationTarget = s.NavigationTarget
            }).ToList();
        }
    }

    private void SearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (args.ChosenSuggestion is SearchSuggestionDisplay suggestion)
        {
            HandleSearchNavigation(suggestion.NavigationTarget);
        }
        else if (!string.IsNullOrWhiteSpace(args.QueryText))
        {
            // If no suggestion chosen, try to navigate to symbol or show search results
            var query = args.QueryText.Trim().ToUpperInvariant();
            HandleSearchNavigation($"symbol:{query}");
        }
        sender.Text = string.Empty;
    }

    private void SearchBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        if (args.SelectedItem is SearchSuggestionDisplay suggestion)
        {
            sender.Text = suggestion.Text;
        }
    }

    private void HandleSearchNavigation(string target)
    {
        var parts = target.Split(':');
        if (parts.Length < 2) return;

        var type = parts[0];
        var value = parts[1];

        switch (type)
        {
            case "page":
                NavigateToPage(value);
                break;
            case "symbol":
                // Navigate to symbol storage page or watchlist
                ContentFrame.Navigate(typeof(SymbolStoragePage), value);
                break;
            case "provider":
                ContentFrame.Navigate(typeof(ProviderPage), value);
                break;
            case "action":
                HandleAction(value);
                break;
            case "help":
                ContentFrame.Navigate(typeof(HelpPage), value);
                break;
        }
    }

    private void HandleAction(string action)
    {
        switch (action)
        {
            case "start":
                // Trigger collector start
                break;
            case "stop":
                // Trigger collector stop
                break;
            case "backfill":
                NavigateToPage("Backfill");
                break;
            case "addsymbol":
                NavigateToPage("Symbols");
                break;
            case "export":
                NavigateToPage("DataExport");
                break;
            case "verify":
                NavigateToPage("ArchiveHealth");
                break;
            case "logs":
                NavigateToPage("ServiceManager");
                break;
            case "settings":
                ContentFrame.Navigate(typeof(SettingsPage));
                break;
            case "refresh":
                _ = ViewModel.RefreshStatusCommand.ExecuteAsync(null);
                break;
        }
    }
}

/// <summary>
/// Display model for search suggestions in the UI.
/// </summary>
public class SearchSuggestionDisplay
{
    public string Text { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string NavigationTarget { get; set; } = string.Empty;

    public override string ToString() => $"{Text} ({Category})";
}
