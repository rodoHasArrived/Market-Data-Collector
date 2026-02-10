using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MarketDataCollector.Uwp.Contracts;

/// <summary>
/// Interface for tooltip and teaching tip management.
/// Provides feature help content, onboarding tips, and teaching tip creation.
/// </summary>
public interface ITooltipService
{
    Services.FeatureHelp GetFeatureHelp(string featureKey);
    bool ShouldShowTip(string tipKey);
    void DismissTip(string tipKey);
    void ResetAllTips();
    IReadOnlyList<Services.OnboardingTip> GetOnboardingTips(string pageKey);
    TeachingTip CreateTeachingTip(
        FrameworkElement target,
        string title,
        string subtitle,
        string? actionContent = null,
        Action? actionCallback = null);
}
