using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage;

namespace MarketDataCollector.Uwp.Services;

/// <summary>
/// Service for managing contextual tooltips, onboarding tips, and feature discovery.
/// Tracks which tips have been shown to avoid repetition.
/// </summary>
public sealed class TooltipService
{
    private static TooltipService? _instance;
    private static readonly object _lock = new();

    private readonly HashSet<string> _shownTips = new();
    private readonly HashSet<string> _dismissedTips = new();
    private const string DismissedTipsKey = "DismissedTooltips";

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

    /// <summary>
    /// Gets contextual help content for a feature.
    /// </summary>
    public FeatureHelp GetFeatureHelp(string featureKey)
    {
        return _featureHelp.TryGetValue(featureKey, out var help) ? help : new FeatureHelp
        {
            Title = "Help",
            Description = "No help available for this feature.",
            LearnMoreUrl = null
        };
    }

    /// <summary>
    /// Shows a teaching tip for first-time feature discovery.
    /// </summary>
    public bool ShouldShowTip(string tipKey)
    {
        if (_dismissedTips.Contains(tipKey))
            return false;

        if (_shownTips.Contains(tipKey))
            return false;

        _shownTips.Add(tipKey);
        return true;
    }

    /// <summary>
    /// Permanently dismisses a tip.
    /// </summary>
    public void DismissTip(string tipKey)
    {
        _dismissedTips.Add(tipKey);
        SaveDismissedTips();
    }

    /// <summary>
    /// Resets all dismissed tips (useful for testing or user preference).
    /// </summary>
    public void ResetAllTips()
    {
        _dismissedTips.Clear();
        _shownTips.Clear();
        SaveDismissedTips();
    }

    /// <summary>
    /// Gets onboarding tips for a page.
    /// </summary>
    public IReadOnlyList<OnboardingTip> GetOnboardingTips(string pageKey)
    {
        return _onboardingTips.TryGetValue(pageKey, out var tips) ? tips : Array.Empty<OnboardingTip>();
    }

    /// <summary>
    /// Creates and configures a teaching tip for an element.
    /// </summary>
    public TeachingTip CreateTeachingTip(
        FrameworkElement target,
        string title,
        string subtitle,
        string? actionContent = null,
        Action? actionCallback = null)
    {
        var tip = new TeachingTip
        {
            Title = title,
            Subtitle = subtitle,
            Target = target,
            IsLightDismissEnabled = true,
            PreferredPlacement = TeachingTipPlacementMode.Auto,
            ActionButtonContent = actionContent,
            CloseButtonContent = "Got it"
        };

        if (actionCallback != null && !string.IsNullOrEmpty(actionContent))
        {
            tip.ActionButtonClick += (s, e) => actionCallback();
        }

        return tip;
    }

    private void LoadDismissedTips()
    {
        try
        {
            var localSettings = ApplicationData.Current.LocalSettings;
            if (localSettings.Values.TryGetValue(DismissedTipsKey, out var value) && value is string serialized)
            {
                foreach (var tip in serialized.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    _dismissedTips.Add(tip);
                }
            }
        }
        catch
        {
            // Ignore settings errors
        }
    }

    private void SaveDismissedTips()
    {
        try
        {
            var localSettings = ApplicationData.Current.LocalSettings;
            localSettings.Values[DismissedTipsKey] = string.Join(",", _dismissedTips);
        }
        catch
        {
            // Ignore settings errors
        }
    }

    #region Feature Help Content

    private static readonly Dictionary<string, FeatureHelp> _featureHelp = new()
    {
        ["dashboard"] = new FeatureHelp
        {
            Title = "Dashboard",
            Description = "The Dashboard provides a real-time overview of your market data collection. Monitor connection status, event throughput, data quality, and active symbol subscriptions at a glance.",
            Tips = new[]
            {
                "Use keyboard shortcut Ctrl+D to quickly navigate here",
                "The sparklines show trends over the last 30 seconds",
                "Click on any metric card to see detailed analysis"
            },
            KeyboardShortcuts = new Dictionary<string, string>
            {
                ["Ctrl+S"] = "Start collector",
                ["Ctrl+Shift+S"] = "Stop collector",
                ["F5"] = "Refresh data"
            }
        },

        ["backfill"] = new FeatureHelp
        {
            Title = "Historical Backfill",
            Description = "Download historical market data from various providers. Use this to fill gaps in your data or bootstrap a new symbol with historical bars.",
            Tips = new[]
            {
                "Use 'composite' provider to automatically try multiple sources",
                "Drag and drop symbols to prioritize download order",
                "Schedule recurring backfills for automatic gap filling"
            },
            KeyboardShortcuts = new Dictionary<string, string>
            {
                ["Ctrl+B"] = "Navigate to Backfill",
                ["Ctrl+Enter"] = "Start backfill",
                ["Escape"] = "Cancel backfill"
            }
        },

        ["symbols"] = new FeatureHelp
        {
            Title = "Symbol Management",
            Description = "Manage your symbol subscriptions. Add new symbols, configure subscription types (trades, depth, quotes), and set exchange mappings.",
            Tips = new[]
            {
                "Enter multiple symbols separated by commas",
                "Use the Index Subscription page to add market indices",
                "Import from CSV files using Portfolio Import"
            }
        },

        ["provider"] = new FeatureHelp
        {
            Title = "Data Provider Configuration",
            Description = "Configure your active data provider and connection settings. Each provider has different capabilities and rate limits.",
            Tips = new[]
            {
                "Store API keys in environment variables for security",
                "Use Multi-Source for automatic failover between providers",
                "Check provider health status before starting collection"
            }
        },

        ["storage"] = new FeatureHelp
        {
            Title = "Storage Management",
            Description = "Monitor storage usage, configure retention policies, and manage data archival. Keep your data organized and disk space optimized.",
            Tips = new[]
            {
                "Use Archive Health to verify data integrity",
                "Configure automatic cleanup with retention policies",
                "Export data to Parquet for efficient analysis"
            }
        },

        ["dataquality"] = new FeatureHelp
        {
            Title = "Data Quality Monitoring",
            Description = "Monitor the quality of your collected data. Track gaps, anomalies, and integrity issues to ensure reliable data for analysis.",
            Tips = new[]
            {
                "Quality scores below 95% may indicate issues",
                "Set up alerts for automatic gap detection",
                "Use the integrity check to verify file consistency"
            }
        },

        ["leanintegration"] = new FeatureHelp
        {
            Title = "QuantConnect Lean Integration",
            Description = "Run backtests using QuantConnect's Lean Engine with your collected data. Test trading strategies against historical market data.",
            Tips = new[]
            {
                "Ensure your data covers the backtest date range",
                "Configure Lean paths in the integration settings",
                "Results are exported to the configured output folder"
            }
        }
    };

    #endregion

    #region Onboarding Tips

    private static readonly Dictionary<string, OnboardingTip[]> _onboardingTips = new()
    {
        ["Dashboard"] = new[]
        {
            new OnboardingTip
            {
                Id = "dashboard_welcome",
                Title = "Welcome to Market Data Collector",
                Content = "This dashboard shows your real-time data collection status. Start by configuring a data provider.",
                TargetElement = "CollectorStatusBadge",
                Order = 1
            },
            new OnboardingTip
            {
                Id = "dashboard_metrics",
                Title = "Monitoring Metrics",
                Content = "These cards show key metrics: events published, dropped, integrity issues, and historical bars.",
                TargetElement = "MetricsGrid",
                Order = 2
            },
            new OnboardingTip
            {
                Id = "dashboard_quickadd",
                Title = "Quick Add Symbol",
                Content = "Quickly add a symbol to your subscription list. Type a symbol like AAPL or SPY and press Enter.",
                TargetElement = "QuickAddSymbolBox",
                Order = 3
            }
        },

        ["Backfill"] = new[]
        {
            new OnboardingTip
            {
                Id = "backfill_provider",
                Title = "Choose a Provider",
                Content = "Select a historical data provider. 'composite' will try multiple providers automatically.",
                TargetElement = "ProviderCombo",
                Order = 1
            },
            new OnboardingTip
            {
                Id = "backfill_daterange",
                Title = "Select Date Range",
                Content = "Use the preset buttons (30d, 90d, YTD) or pick custom dates. Longer ranges take more time.",
                TargetElement = "DatePresetPanel",
                Order = 2
            }
        },

        ["Provider"] = new[]
        {
            new OnboardingTip
            {
                Id = "provider_apikey",
                Title = "API Keys",
                Content = "Enter your API credentials. For security, consider using environment variables instead of storing keys directly.",
                TargetElement = "ApiKeyInput",
                Order = 1
            }
        }
    };

    #endregion
}

/// <summary>
/// Contextual help information for a feature.
/// </summary>
public class FeatureHelp
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string[]? Tips { get; set; }
    public string? LearnMoreUrl { get; set; }
    public Dictionary<string, string>? KeyboardShortcuts { get; set; }
}

/// <summary>
/// An onboarding tip to show new users.
/// </summary>
public class OnboardingTip
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string TargetElement { get; set; } = string.Empty;
    public int Order { get; set; }
}
