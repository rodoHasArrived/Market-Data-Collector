using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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

    public MainPage()
    {
        this.InitializeComponent();
        ViewModel = new MainViewModel();
        DataContext = ViewModel;
        _searchService = SearchService.Instance;
        _firstRunService = new FirstRunService();

        Loaded += MainPage_Loaded;
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
    }

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
            case "Backfill":
                ContentFrame.Navigate(typeof(BackfillPage));
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
