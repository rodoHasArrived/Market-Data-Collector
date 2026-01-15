using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using System;

namespace MarketDataCollector.Uwp.Helpers;

/// <summary>
/// Provides accessibility utilities for improved screen reader support and keyboard navigation.
/// </summary>
public static class AccessibilityHelper
{
    /// <summary>
    /// Announces a message to screen readers using the AutomationPeer live region.
    /// </summary>
    /// <param name="element">The element to use as the announcement source.</param>
    /// <param name="message">The message to announce.</param>
    /// <param name="politeness">The politeness level for the announcement.</param>
    public static void Announce(FrameworkElement element, string message, AutomationNotificationProcessing politeness = AutomationNotificationProcessing.ImportantMostRecent)
    {
        if (element == null || string.IsNullOrEmpty(message))
        {
            return;
        }

        var peer = FrameworkElementAutomationPeer.FromElement(element);
        peer?.RaiseNotificationEvent(
            AutomationNotificationKind.Other,
            politeness,
            message,
            Guid.NewGuid().ToString());
    }

    /// <summary>
    /// Sets the accessible name for an element.
    /// </summary>
    public static void SetAccessibleName(DependencyObject element, string name)
    {
        AutomationProperties.SetName(element, name);
    }

    /// <summary>
    /// Sets the accessible help text for an element.
    /// </summary>
    public static void SetAccessibleHelpText(DependencyObject element, string helpText)
    {
        AutomationProperties.SetHelpText(element, helpText);
    }

    /// <summary>
    /// Sets the live setting for dynamic content updates.
    /// </summary>
    public static void SetLiveSetting(DependencyObject element, AutomationLiveSetting setting)
    {
        AutomationProperties.SetLiveSetting(element, setting);
    }

    /// <summary>
    /// Marks an element as a heading for screen reader navigation.
    /// </summary>
    public static void SetHeadingLevel(DependencyObject element, AutomationHeadingLevel level)
    {
        AutomationProperties.SetHeadingLevel(element, level);
    }

    /// <summary>
    /// Sets up a labeled-by relationship between elements.
    /// </summary>
    public static void SetLabeledBy(DependencyObject element, UIElement label)
    {
        AutomationProperties.SetLabeledBy(element, label);
    }

    /// <summary>
    /// Creates an accessible status message for metric updates.
    /// </summary>
    public static string FormatMetricAnnouncement(string metricName, string value, string? trend = null)
    {
        var message = $"{metricName}: {value}";
        if (!string.IsNullOrEmpty(trend))
        {
            message += $", {trend}";
        }
        return message;
    }

    /// <summary>
    /// Creates an accessible status message for connection state changes.
    /// </summary>
    public static string FormatConnectionStatus(bool isConnected, string? providerName = null)
    {
        var status = isConnected ? "connected" : "disconnected";
        return string.IsNullOrEmpty(providerName)
            ? $"Connection status: {status}"
            : $"{providerName} {status}";
    }

    /// <summary>
    /// Creates an accessible progress announcement.
    /// </summary>
    public static string FormatProgressAnnouncement(string taskName, int percentComplete)
    {
        return percentComplete switch
        {
            0 => $"{taskName} started",
            100 => $"{taskName} completed",
            _ => $"{taskName}: {percentComplete} percent complete"
        };
    }

    /// <summary>
    /// Creates an accessible alert announcement.
    /// </summary>
    public static string FormatAlertAnnouncement(string severity, string message)
    {
        return $"{severity} alert: {message}";
    }

    /// <summary>
    /// Checks if high contrast mode is enabled.
    /// </summary>
    public static bool IsHighContrastEnabled
    {
        get
        {
            var settings = new Windows.UI.ViewManagement.UISettings();
            return settings.AdvancedEffectsEnabled == false; // High contrast typically disables advanced effects
        }
    }

    /// <summary>
    /// Gets accessible color for status indicators.
    /// In high contrast mode, uses patterns instead of colors alone.
    /// </summary>
    public static string GetAccessibleStatusIcon(bool isSuccess)
    {
        return isSuccess ? "\uE73E" : "\uEA39"; // Checkmark or Error X
    }

    /// <summary>
    /// Creates a screen reader friendly list announcement.
    /// </summary>
    public static string FormatListAnnouncement(string listName, int itemCount, int? selectedIndex = null)
    {
        var message = $"{listName}: {itemCount} item{(itemCount != 1 ? "s" : "")}";
        if (selectedIndex.HasValue)
        {
            message += $", item {selectedIndex.Value + 1} selected";
        }
        return message;
    }

    /// <summary>
    /// Creates a screen reader friendly table announcement.
    /// </summary>
    public static string FormatTableAnnouncement(string tableName, int rowCount, int columnCount)
    {
        return $"{tableName}: {rowCount} row{(rowCount != 1 ? "s" : "")}, {columnCount} column{(columnCount != 1 ? "s" : "")}";
    }

    /// <summary>
    /// Creates a screen reader friendly time/duration announcement.
    /// </summary>
    public static string FormatDurationAnnouncement(TimeSpan duration)
    {
        if (duration.TotalHours >= 1)
            return $"{(int)duration.TotalHours} hour{((int)duration.TotalHours != 1 ? "s" : "")} {duration.Minutes} minute{(duration.Minutes != 1 ? "s" : "")}";
        if (duration.TotalMinutes >= 1)
            return $"{duration.Minutes} minute{(duration.Minutes != 1 ? "s" : "")} {duration.Seconds} second{(duration.Seconds != 1 ? "s" : "")}";
        return $"{duration.Seconds} second{(duration.Seconds != 1 ? "s" : "")}";
    }

    /// <summary>
    /// Sets focus to an element with screen reader announcement.
    /// </summary>
    public static void SetFocusWithAnnouncement(Control control, string announcement)
    {
        if (control == null) return;

        control.Focus(FocusState.Programmatic);

        if (!string.IsNullOrEmpty(announcement))
        {
            Announce(control, announcement);
        }
    }

    /// <summary>
    /// Creates keyboard shortcut description for screen readers.
    /// </summary>
    public static string FormatKeyboardShortcut(string key, bool ctrl = false, bool shift = false, bool alt = false)
    {
        var parts = new System.Collections.Generic.List<string>();
        if (ctrl) parts.Add("Control");
        if (alt) parts.Add("Alt");
        if (shift) parts.Add("Shift");
        parts.Add(key);
        return string.Join(" plus ", parts);
    }
}

/// <summary>
/// Attached properties for accessibility attributes.
/// </summary>
public static class A11yProperties
{
    #region ScreenReaderText

    /// <summary>
    /// Alternative text for screen readers that differs from visible text.
    /// </summary>
    public static readonly DependencyProperty ScreenReaderTextProperty =
        DependencyProperty.RegisterAttached(
            "ScreenReaderText",
            typeof(string),
            typeof(A11yProperties),
            new PropertyMetadata(string.Empty, OnScreenReaderTextChanged));

    public static void SetScreenReaderText(DependencyObject element, string value)
    {
        element.SetValue(ScreenReaderTextProperty, value);
    }

    public static string GetScreenReaderText(DependencyObject element)
    {
        return (string)element.GetValue(ScreenReaderTextProperty);
    }

    private static void OnScreenReaderTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is string text && !string.IsNullOrEmpty(text))
        {
            AutomationProperties.SetName(d, text);
        }
    }

    #endregion

    #region IsImportant

    /// <summary>
    /// Marks content as important for screen readers (polite announcement).
    /// </summary>
    public static readonly DependencyProperty IsImportantProperty =
        DependencyProperty.RegisterAttached(
            "IsImportant",
            typeof(bool),
            typeof(A11yProperties),
            new PropertyMetadata(false, OnIsImportantChanged));

    public static void SetIsImportant(DependencyObject element, bool value)
    {
        element.SetValue(IsImportantProperty, value);
    }

    public static bool GetIsImportant(DependencyObject element)
    {
        return (bool)element.GetValue(IsImportantProperty);
    }

    private static void OnIsImportantChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
        {
            AutomationProperties.SetLiveSetting(d, AutomationLiveSetting.Polite);
        }
    }

    #endregion

    #region IsCritical

    /// <summary>
    /// Marks content as critical for screen readers (assertive announcement).
    /// </summary>
    public static readonly DependencyProperty IsCriticalProperty =
        DependencyProperty.RegisterAttached(
            "IsCritical",
            typeof(bool),
            typeof(A11yProperties),
            new PropertyMetadata(false, OnIsCriticalChanged));

    public static void SetIsCritical(DependencyObject element, bool value)
    {
        element.SetValue(IsCriticalProperty, value);
    }

    public static bool GetIsCritical(DependencyObject element)
    {
        return (bool)element.GetValue(IsCriticalProperty);
    }

    private static void OnIsCriticalChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
        {
            AutomationProperties.SetLiveSetting(d, AutomationLiveSetting.Assertive);
        }
    }

    #endregion
}
