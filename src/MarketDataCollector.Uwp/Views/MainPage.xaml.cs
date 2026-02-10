using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using MarketDataCollector.Uwp.ViewModels;
using MarketDataCollector.Uwp.Services;

using MarketDataCollector.Uwp.Models;

namespace MarketDataCollector.Uwp.Views;

/// <summary>
/// Main dashboard page with workspace-based navigation (Monitor, Collect, Storage, Quality, Settings).
/// Uses NavigationService for centralized page routing and supports a command palette (Ctrl+K).
/// </summary>
public sealed partial class MainPage : Page
{
    public MainViewModel ViewModel { get; }
    private readonly SearchService _searchService;
    private readonly FirstRunService _firstRunService;
    private readonly NotificationService _notificationService;
    private readonly ConnectionService _connectionService;
    private readonly NavigationService _navigationService;
    private readonly DispatcherTimer _notificationDismissTimer;
    private string? _currentNotificationAction;
    private bool _commandPaletteOpen;

    public MainPage()
    {
        this.InitializeComponent();
        ViewModel = new MainViewModel();
        DataContext = ViewModel;
        _searchService = SearchService.Instance;
        _firstRunService = new FirstRunService();
        _notificationService = NotificationService.Instance;
        _connectionService = ConnectionService.Instance;
        _navigationService = NavigationService.Instance;

        // Initialize navigation service with the content frame
        _navigationService.Initialize(ContentFrame);

        // Setup notification dismiss timer
        _notificationDismissTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(8) };
        _notificationDismissTimer.Tick += NotificationDismissTimer_Tick;

        // Subscribe to notification events
        _notificationService.NotificationReceived += NotificationService_NotificationReceived;
        _connectionService.StateChanged += ConnectionService_StateChanged;

        // Register Ctrl+K keyboard accelerator for command palette
        var ctrlK = new KeyboardAccelerator { Key = Windows.System.VirtualKey.K, Modifiers = Windows.System.VirtualKeyModifiers.Control };
        ctrlK.Invoked += CommandPaletteAccelerator_Invoked;
        this.KeyboardAccelerators.Add(ctrlK);

        // Register Escape to close command palette
        var escape = new KeyboardAccelerator { Key = Windows.System.VirtualKey.Escape };
        escape.Invoked += EscapeAccelerator_Invoked;
        this.KeyboardAccelerators.Add(escape);

        Loaded += MainPage_Loaded;
        Unloaded += MainPage_Unloaded;
    }

    private void MainPage_Loaded(object sender, RoutedEventArgs e)
    {
        // Fire-and-forget with proper exception handling
        _ = SafeMainPageLoadedAsync();
    }

    private async Task SafeMainPageLoadedAsync()
    {
        try
        {
            // Check for first run and show welcome wizard
            if (await _firstRunService.IsFirstRunAsync())
            {
                _navigationService.NavigateTo("Welcome");
                await _firstRunService.InitializeAsync();
                return;
            }

            await ViewModel.LoadAsync();

            // Start connection monitoring
            _connectionService.StartMonitoring();
        }
        catch (Exception ex)
        {
            LoggingService.Instance.LogError("MainPage", "Error during page load", ex);
        }
    }

    private void MainPage_Unloaded(object sender, RoutedEventArgs e)
    {
        _notificationService.NotificationReceived -= NotificationService_NotificationReceived;
        _connectionService.StateChanged -= ConnectionService_StateChanged;
        _notificationDismissTimer.Stop();
    }

    #region Command Palette

    private void CommandPaletteAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        ToggleCommandPalette();
    }

    private void EscapeAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (_commandPaletteOpen)
        {
            args.Handled = true;
            CloseCommandPalette();
        }
    }

    private void ToggleCommandPalette()
    {
        if (_commandPaletteOpen)
        {
            CloseCommandPalette();
        }
        else
        {
            OpenCommandPalette();
        }
    }

    private void OpenCommandPalette()
    {
        _commandPaletteOpen = true;
        CommandPaletteOverlay.Visibility = Visibility.Visible;
        CommandPaletteBox.Text = string.Empty;
        CommandPaletteBox.Focus(FocusState.Programmatic);
    }

    private void CloseCommandPalette()
    {
        _commandPaletteOpen = false;
        CommandPaletteOverlay.Visibility = Visibility.Collapsed;
        CommandPaletteBox.Text = string.Empty;
    }

    private void CommandPalette_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
        {
            _ = SafeCommandPaletteSearchAsync(sender);
        }
    }

    private async Task SafeCommandPaletteSearchAsync(AutoSuggestBox sender)
    {
        try
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
        catch (Exception ex)
        {
            LoggingService.Instance.LogWarning("Command palette search error", ("error", ex.Message));
            sender.ItemsSource = null;
        }
    }

    private void CommandPalette_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (args.ChosenSuggestion is SearchSuggestionDisplay suggestion)
        {
            HandleSearchNavigation(suggestion.NavigationTarget);
        }
        else if (!string.IsNullOrWhiteSpace(args.QueryText))
        {
            var query = args.QueryText.Trim().ToUpperInvariant();
            HandleSearchNavigation($"symbol:{query}");
        }
        CloseCommandPalette();
    }

    private void CommandPalette_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        if (args.SelectedItem is SearchSuggestionDisplay suggestion)
        {
            sender.Text = suggestion.Text;
        }
    }

    private void CommandPalette_GotFocus(object sender, RoutedEventArgs e)
    {
        // Show all pages when command palette first opens with no text
        if (sender is AutoSuggestBox box && string.IsNullOrEmpty(box.Text))
        {
            _ = SafeCommandPaletteSearchAsync(box);
        }
    }

    #endregion

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

        // Set background color and icon based on type using cached brushes
        (NotificationBanner.Background, NotificationIcon.Glyph) = type switch
        {
            NotificationType.Success => (BrushRegistry.Success, "\uE73E"),
            NotificationType.Warning => (BrushRegistry.Warning, "\uE7BA"),
            NotificationType.Error => (BrushRegistry.Error, "\uEA39"),
            _ => (BrushRegistry.Info, "\uE946")
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
            _navigationService.NavigateTo("Settings");
            return;
        }

        if (args.SelectedItem is NavigationViewItem item)
        {
            var tag = item.Tag?.ToString();
            NavigateToPage(tag);
        }
    }

    /// <summary>
    /// Navigates to a page by tag using the centralized NavigationService.
    /// </summary>
    private void NavigateToPage(string? tag)
    {
        if (string.IsNullOrEmpty(tag))
            return;

        // Use NavigationService for centralized page routing
        if (!_navigationService.NavigateTo(tag))
        {
            LoggingService.Instance.LogWarning("Unknown navigation tag", ("tag", tag));
        }
    }

    private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
        {
            // Fire-and-forget with proper exception handling
            _ = SafeSearchBoxTextChangedAsync(sender);
        }
    }

    private async Task SafeSearchBoxTextChangedAsync(AutoSuggestBox sender)
    {
        try
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
        catch (Exception ex)
        {
            LoggingService.Instance.LogWarning("Search error", ("error", ex.Message));
            sender.ItemsSource = null;
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
                // Navigate to symbol storage page with symbol parameter
                _navigationService.NavigateTo("SymbolStorage", value);
                break;
            case "provider":
                _navigationService.NavigateTo("Provider", value);
                break;
            case "action":
                HandleAction(value);
                break;
            case "help":
                _navigationService.NavigateTo("Help", value);
                break;
        }
    }

    private void HandleAction(string action)
    {
        switch (action)
        {
            case "start":
                // Trigger collector start via service
                break;
            case "stop":
                // Trigger collector stop via service
                break;
            case "backfill":
                _navigationService.NavigateTo("Backfill");
                break;
            case "addsymbol":
                _navigationService.NavigateTo("Symbols");
                break;
            case "export":
                _navigationService.NavigateTo("DataExport");
                break;
            case "verify":
                _navigationService.NavigateTo("ArchiveHealth");
                break;
            case "logs":
                _navigationService.NavigateTo("ServiceManager");
                break;
            case "settings":
                _navigationService.NavigateTo("Settings");
                break;
            case "refresh":
                _ = ViewModel.RefreshStatusCommand.ExecuteAsync(null);
                break;
        }
    }
}
