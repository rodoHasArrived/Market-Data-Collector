using System;
using System.Collections.Generic;
using System.IO;

namespace MarketDataCollector.Wpf.Services;

/// <summary>
/// Service for managing contextual tooltips, onboarding tips, and feature discovery.
/// Tracks which tips have been shown to avoid repetition.
/// Uses file-based storage for dismissed tips persistence (WPF replacement for ApplicationData).
/// </summary>
public sealed class TooltipService
{
    private static TooltipService? _instance;
    private static readonly object _lock = new();

    private readonly HashSet<string> _shownTips = new();
    private readonly HashSet<string> _dismissedTips = new();
    private const string DismissedTipsFileName = "dismissed-tooltips.txt";

    public static TooltipService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new TooltipService();
                }
            }
            return _instance;
        }
    }

    private TooltipService()
    {
        LoadDismissedTips();
    }

    public FeatureHelp GetFeatureHelp(string featureKey)
    {
        return _featureHelp.TryGetValue(featureKey, out var help) ? help : new FeatureHelp
        {
            Title = "Help",
            Description = "No help available for this feature.",
            LearnMoreUrl = null
        };
    }

    public bool ShouldShowTip(string tipKey)
    {
        if (_dismissedTips.Contains(tipKey)) return false;
        if (_shownTips.Contains(tipKey)) return false;
        _shownTips.Add(tipKey);
        return true;
    }

    public void DismissTip(string tipKey)
    {
        _dismissedTips.Add(tipKey);
        SaveDismissedTips();
    }

    public void ResetAllTips()
    {
        _dismissedTips.Clear();
        _shownTips.Clear();
        SaveDismissedTips();
    }

    public IReadOnlyList<OnboardingTip> GetOnboardingTips(string pageKey)
    {
        return _onboardingTips.TryGetValue(pageKey, out var tips) ? tips : Array.Empty<OnboardingTip>();
    }

    /// <summary>
    /// Gets formatted tooltip text for a feature (title + description + tips).
    /// Can be used to populate WPF ToolTip content on controls.
    /// </summary>
    public string GetTooltipContent(string featureKey)
    {
        var help = GetFeatureHelp(featureKey);
        var content = help.Description;
        if (help.Tips != null && help.Tips.Length > 0)
            content += "\n\nTips:\n" + string.Join("\n", help.Tips.Select(t => $"  - {t}"));
        return content;
    }

    private static string GetSettingsFilePath()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MarketDataCollector");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, DismissedTipsFileName);
    }

    private void LoadDismissedTips()
    {
        try
        {
            var filePath = GetSettingsFilePath();
            if (File.Exists(filePath))
            {
                var serialized = File.ReadAllText(filePath);
                foreach (var tip in serialized.Split(',', StringSplitOptions.RemoveEmptyEntries))
                    _dismissedTips.Add(tip);
            }
        }
        catch { /* Ignore settings errors */ }
    }

    private void SaveDismissedTips()
    {
        try
        {
            File.WriteAllText(GetSettingsFilePath(), string.Join(",", _dismissedTips));
        }
        catch { /* Ignore settings errors */ }
    }

    #region Feature Help Content

    private static readonly Dictionary<string, FeatureHelp> _featureHelp = new()
    {
        ["dashboard"] = new FeatureHelp
        {
            Title = "Dashboard",
            Description = "The Dashboard provides a real-time overview of your market data collection.",
            Tips = new[] { "Use Ctrl+D to navigate here", "Sparklines show 30s trends", "Click metrics for details" },
            KeyboardShortcuts = new Dictionary<string, string>
            {
                ["Ctrl+S"] = "Start collector", ["Ctrl+Shift+S"] = "Stop collector", ["F5"] = "Refresh data"
            }
        },
        ["backfill"] = new FeatureHelp
        {
            Title = "Historical Backfill",
            Description = "Download historical market data from various providers.",
            Tips = new[] { "Use 'composite' for auto multi-source", "Drag to prioritize downloads", "Schedule recurring backfills" },
            KeyboardShortcuts = new Dictionary<string, string>
            {
                ["Ctrl+B"] = "Navigate to Backfill", ["Ctrl+Enter"] = "Start backfill", ["Escape"] = "Cancel"
            }
        },
        ["symbols"] = new FeatureHelp
        {
            Title = "Symbol Management",
            Description = "Manage your symbol subscriptions, types, and exchange mappings.",
            Tips = new[] { "Comma-separate multiple symbols", "Use Index Subscription for indices", "Import from CSV via Portfolio Import" }
        },
        ["provider"] = new FeatureHelp
        {
            Title = "Data Provider Configuration",
            Description = "Configure your active data provider and connection settings.",
            Tips = new[] { "Use env vars for API keys", "Multi-Source for failover", "Check health before starting" }
        },
        ["storage"] = new FeatureHelp
        {
            Title = "Storage Management",
            Description = "Monitor storage, configure retention, and manage archival.",
            Tips = new[] { "Archive Health verifies integrity", "Auto cleanup with retention policies", "Export to Parquet for analysis" }
        },
        ["dataquality"] = new FeatureHelp
        {
            Title = "Data Quality Monitoring",
            Description = "Track gaps, anomalies, and integrity issues in collected data.",
            Tips = new[] { "Scores below 95% may indicate issues", "Set alerts for gap detection", "Integrity check for file consistency" }
        },
        ["leanintegration"] = new FeatureHelp
        {
            Title = "QuantConnect Lean Integration",
            Description = "Run backtests using Lean Engine with your collected data.",
            Tips = new[] { "Cover the backtest date range", "Configure Lean paths in settings", "Results export to output folder" }
        }
    };

    #endregion

    #region Onboarding Tips

    private static readonly Dictionary<string, OnboardingTip[]> _onboardingTips = new()
    {
        ["Dashboard"] = new[]
        {
            new OnboardingTip { Id = "dashboard_welcome", Title = "Welcome to Market Data Collector", Content = "Start by configuring a data provider.", TargetElement = "CollectorStatusBadge", Order = 1 },
            new OnboardingTip { Id = "dashboard_metrics", Title = "Monitoring Metrics", Content = "Key metrics: events, drops, integrity, bars.", TargetElement = "MetricsGrid", Order = 2 },
            new OnboardingTip { Id = "dashboard_quickadd", Title = "Quick Add Symbol", Content = "Type AAPL or SPY and press Enter.", TargetElement = "QuickAddSymbolBox", Order = 3 }
        },
        ["Backfill"] = new[]
        {
            new OnboardingTip { Id = "backfill_provider", Title = "Choose a Provider", Content = "'composite' tries multiple providers.", TargetElement = "ProviderCombo", Order = 1 },
            new OnboardingTip { Id = "backfill_daterange", Title = "Select Date Range", Content = "Use presets or pick custom dates.", TargetElement = "DatePresetPanel", Order = 2 }
        },
        ["Provider"] = new[]
        {
            new OnboardingTip { Id = "provider_apikey", Title = "API Keys", Content = "Use environment variables for security.", TargetElement = "ApiKeyInput", Order = 1 }
        }
    };

    #endregion
}

public class FeatureHelp
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string[]? Tips { get; set; }
    public string? LearnMoreUrl { get; set; }
    public Dictionary<string, string>? KeyboardShortcuts { get; set; }
}

public class OnboardingTip
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string TargetElement { get; set; } = string.Empty;
    public int Order { get; set; }
}
