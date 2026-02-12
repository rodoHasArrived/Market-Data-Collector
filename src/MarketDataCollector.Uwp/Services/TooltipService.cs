using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage;
using MarketDataCollector.Ui.Services.Services;

namespace MarketDataCollector.Uwp.Services;

/// <summary>
/// Service for managing contextual tooltips, onboarding tips, and feature discovery.
/// Tracks which tips have been shown to avoid repetition.
/// Delegates content to the shared <see cref="TooltipContent"/> in Ui.Services.
/// </summary>
public sealed class TooltipService : ITooltipService
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

    public FeatureHelp GetFeatureHelp(string featureKey) => TooltipContent.GetFeatureHelp(featureKey);

    public bool ShouldShowTip(string tipKey)
    {
        if (_dismissedTips.Contains(tipKey))
            return false;

        if (_shownTips.Contains(tipKey))
            return false;

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
}
