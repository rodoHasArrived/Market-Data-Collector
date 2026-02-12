using System;
using System.Collections.Generic;
using System.IO;
using MarketDataCollector.Ui.Services.Services;

namespace MarketDataCollector.Wpf.Services;

/// <summary>
/// Service for managing contextual tooltips, onboarding tips, and feature discovery.
/// Tracks which tips have been shown to avoid repetition.
/// Uses file-based storage for dismissed tips persistence.
/// Delegates content to the shared <see cref="TooltipContent"/> in Ui.Services.
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

    public FeatureHelp GetFeatureHelp(string featureKey) => TooltipContent.GetFeatureHelp(featureKey);

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

    public IReadOnlyList<OnboardingTip> GetOnboardingTips(string pageKey) => TooltipContent.GetOnboardingTips(pageKey);

    /// <summary>
    /// Gets formatted tooltip text for a feature (title + description + tips).
    /// Can be used to populate WPF ToolTip content on controls.
    /// </summary>
    public string GetTooltipContent(string featureKey) => TooltipContent.GetTooltipText(featureKey);

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
}
