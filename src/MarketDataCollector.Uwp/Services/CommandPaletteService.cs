namespace MarketDataCollector.Uwp.Services;

/// <summary>
/// Provides command palette functionality for quick navigation across all UWP desktop app pages.
/// Supports fuzzy matching and keyboard shortcut (Ctrl+K) activation.
/// </summary>
public sealed class CommandPaletteService
{
    private readonly List<CommandPaletteItem> _commands = new();
    private readonly INavigationService _navigationService;

    public CommandPaletteService(INavigationService navigationService)
    {
        _navigationService = navigationService;
        RegisterDefaultCommands();
    }

    /// <summary>
    /// Gets all registered commands for display.
    /// </summary>
    public IReadOnlyList<CommandPaletteItem> AllCommands => _commands.AsReadOnly();

    /// <summary>
    /// Searches commands by fuzzy matching the query against name, category, and keywords.
    /// </summary>
    public IReadOnlyList<CommandPaletteItem> Search(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return _commands;

        var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        return _commands
            .Select(cmd => new { Command = cmd, Score = CalculateMatchScore(cmd, terms) })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .Select(x => x.Command)
            .ToList();
    }

    /// <summary>
    /// Executes a command by navigating to its associated page.
    /// </summary>
    public void Execute(CommandPaletteItem item)
    {
        item.Action?.Invoke();
    }

    /// <summary>
    /// Registers a custom command.
    /// </summary>
    public void Register(CommandPaletteItem item)
    {
        _commands.Add(item);
    }

    private static int CalculateMatchScore(CommandPaletteItem cmd, string[] terms)
    {
        var score = 0;
        var searchText = $"{cmd.Name} {cmd.Category} {string.Join(" ", cmd.Keywords)}".ToLowerInvariant();

        foreach (var term in terms)
        {
            var lowerTerm = term.ToLowerInvariant();

            if (cmd.Name.StartsWith(term, StringComparison.OrdinalIgnoreCase))
                score += 100;
            else if (cmd.Name.Contains(term, StringComparison.OrdinalIgnoreCase))
                score += 50;
            else if (searchText.Contains(lowerTerm))
                score += 25;
            else if (FuzzyMatch(searchText, lowerTerm))
                score += 10;
            else
                return 0; // All terms must match
        }

        return score;
    }

    private static bool FuzzyMatch(string text, string pattern)
    {
        var patternIdx = 0;
        foreach (var ch in text)
        {
            if (patternIdx < pattern.Length && char.ToLowerInvariant(ch) == char.ToLowerInvariant(pattern[patternIdx]))
                patternIdx++;
        }
        return patternIdx == pattern.Length;
    }

    private void RegisterDefaultCommands()
    {
        // Primary Navigation
        AddNavCommand("Dashboard", "Navigation", "home overview summary", typeof(Views.DashboardPage));
        AddNavCommand("Watchlist", "Navigation", "favorites watch symbols", typeof(Views.WatchlistPage));

        // Data Sources
        AddNavCommand("Provider Configuration", "Data Sources", "provider setup connection", typeof(Views.ProviderPage));
        AddNavCommand("Provider Health", "Data Sources", "provider status health latency", typeof(Views.ProviderHealthPage));
        AddNavCommand("Data Sources", "Data Sources", "sources feeds connections", typeof(Views.DataSourcesPage));

        // Data Management
        AddNavCommand("Live Data Viewer", "Data", "realtime streaming trades quotes", typeof(Views.LiveDataViewerPage));
        AddNavCommand("Symbols", "Data", "symbols tickers manage add remove", typeof(Views.SymbolsPage));
        AddNavCommand("Symbol Mapping", "Data", "mapping cross-provider figi", typeof(Views.SymbolMappingPage));
        AddNavCommand("Symbol Storage", "Data", "storage per-symbol files", typeof(Views.SymbolStoragePage));
        AddNavCommand("Storage", "Data", "storage files disk usage", typeof(Views.StoragePage));
        AddNavCommand("Backfill", "Data", "historical backfill download bars", typeof(Views.BackfillPage));
        AddNavCommand("Portfolio Import", "Data", "import portfolio csv file", typeof(Views.PortfolioImportPage));
        AddNavCommand("Index Subscriptions", "Data", "index subscribe SPX NDX", typeof(Views.IndexSubscriptionPage));
        AddNavCommand("Schedule Manager", "Data", "schedule timer recurring", typeof(Views.ScheduleManagerPage));

        // Monitoring
        AddNavCommand("Data Quality", "Monitoring", "quality gaps anomalies validation", typeof(Views.DataQualityPage));
        AddNavCommand("Collection Sessions", "Monitoring", "sessions history tracking", typeof(Views.CollectionSessionPage));
        AddNavCommand("Archive Health", "Monitoring", "archive integrity checksum", typeof(Views.ArchiveHealthPage));
        AddNavCommand("Service Manager", "Monitoring", "services background tasks", typeof(Views.ServiceManagerPage));
        AddNavCommand("System Health", "Monitoring", "system cpu memory resources", typeof(Views.SystemHealthPage));
        AddNavCommand("Diagnostics", "Monitoring", "diagnostics debug logs errors", typeof(Views.DiagnosticsPage));

        // Tools
        AddNavCommand("Data Export", "Tools", "export csv parquet json", typeof(Views.DataExportPage));
        AddNavCommand("Data Sampling", "Tools", "sampling preview inspect", typeof(Views.DataSamplingPage));
        AddNavCommand("Time Series Alignment", "Tools", "alignment sync time series", typeof(Views.TimeSeriesAlignmentPage));
        AddNavCommand("Export Presets", "Tools", "presets template export format", typeof(Views.ExportPresetsPage));
        AddNavCommand("Analysis Export", "Tools", "analysis report export", typeof(Views.AnalysisExportPage));
        AddNavCommand("Analysis Export Wizard", "Tools", "wizard guided export analysis", typeof(Views.AnalysisExportWizardPage));
        AddNavCommand("Event Replay", "Tools", "replay events playback", typeof(Views.EventReplayPage));
        AddNavCommand("Package Manager", "Tools", "package bundle archive share", typeof(Views.PackageManagerPage));
        AddNavCommand("Trading Hours", "Tools", "hours calendar holidays market", typeof(Views.TradingHoursPage));

        // Analytics & Visualization
        AddNavCommand("Advanced Analytics", "Analytics", "analytics statistics advanced", typeof(Views.AdvancedAnalyticsPage));
        AddNavCommand("Charting", "Analytics", "chart plot visualize graph", typeof(Views.ChartingPage));
        AddNavCommand("Order Book", "Analytics", "orderbook depth L2 bid ask", typeof(Views.OrderBookPage));
        AddNavCommand("Data Calendar", "Analytics", "calendar heatmap coverage", typeof(Views.DataCalendarPage));

        // Storage & Maintenance
        AddNavCommand("Storage Optimization", "Maintenance", "optimize compress cleanup", typeof(Views.StorageOptimizationPage));
        AddNavCommand("Retention Assurance", "Maintenance", "retention policy lifecycle", typeof(Views.RetentionAssurancePage));
        AddNavCommand("Admin Maintenance", "Maintenance", "admin maintenance tasks", typeof(Views.AdminMaintenancePage));

        // Integrations
        AddNavCommand("Lean Integration", "Integrations", "lean quantconnect backtest", typeof(Views.LeanIntegrationPage));
        AddNavCommand("Messaging Hub", "Integrations", "messaging alerts notifications slack", typeof(Views.MessagingHubPage));

        // Workspaces & Notifications
        AddNavCommand("Workspaces", "Workspaces", "workspace layout save restore", typeof(Views.WorkspacePage));
        AddNavCommand("Notification Center", "Notifications", "notifications alerts messages", typeof(Views.NotificationCenterPage));

        // Support & Setup
        AddNavCommand("Help", "Support", "help documentation faq guide", typeof(Views.HelpPage));
        AddNavCommand("Welcome", "Support", "welcome getting started onboarding", typeof(Views.WelcomePage));
        AddNavCommand("Settings", "Support", "settings preferences configuration", typeof(Views.SettingsPage));
        AddNavCommand("Keyboard Shortcuts", "Support", "keyboard shortcuts hotkeys bindings", typeof(Views.KeyboardShortcutsPage));
        AddNavCommand("Setup Wizard", "Support", "setup wizard configure first-run", typeof(Views.SetupWizardPage));

        // Data Browser
        AddNavCommand("Data Browser", "Data", "browse files explore data", typeof(Views.DataBrowserPage));
    }

    private void AddNavCommand(string name, string category, string keywords, Type pageType)
    {
        _commands.Add(new CommandPaletteItem(
            Name: name,
            Category: category,
            Keywords: keywords.Split(' '),
            Icon: GetCategoryIcon(category),
            Action: () => _navigationService.NavigateTo(pageType)
        ));
    }

    private static string GetCategoryIcon(string category) => category switch
    {
        "Navigation" => "\uE80F",
        "Data Sources" => "\uE839",
        "Data" => "\uE8A5",
        "Monitoring" => "\uE9D9",
        "Tools" => "\uE90F",
        "Analytics" => "\uE9D2",
        "Maintenance" => "\uE90F",
        "Integrations" => "\uE71B",
        "Workspaces" => "\uE8A0",
        "Notifications" => "\uEA8F",
        "Support" => "\uE897",
        _ => "\uE7C3"
    };
}

/// <summary>
/// Represents a single command palette item.
/// </summary>
public sealed record CommandPaletteItem(
    string Name,
    string Category,
    string[] Keywords,
    string Icon,
    Action? Action = null
);
