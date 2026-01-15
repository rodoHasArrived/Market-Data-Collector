using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using Windows.Foundation;

namespace MarketDataCollector.Uwp.Helpers;

/// <summary>
/// Provides responsive layout utilities for adapting UI based on window size.
/// Implements common breakpoints for responsive design.
/// </summary>
public static class ResponsiveLayoutHelper
{
    /// <summary>
    /// Breakpoint widths for responsive design.
    /// </summary>
    public static class Breakpoints
    {
        /// <summary>Compact: 0-640px (mobile, small tablets)</summary>
        public const double Compact = 640;

        /// <summary>Medium: 641-1007px (tablets, small laptops)</summary>
        public const double Medium = 1007;

        /// <summary>Expanded: 1008-1365px (laptops, small desktops)</summary>
        public const double Expanded = 1365;

        /// <summary>Wide: 1366px+ (large desktops, ultrawide)</summary>
        public const double Wide = 1366;
    }

    /// <summary>
    /// Gets the current layout size category based on window width.
    /// </summary>
    public static LayoutSize GetLayoutSize(double windowWidth)
    {
        return windowWidth switch
        {
            <= Breakpoints.Compact => LayoutSize.Compact,
            <= Breakpoints.Medium => LayoutSize.Medium,
            <= Breakpoints.Expanded => LayoutSize.Expanded,
            _ => LayoutSize.Wide
        };
    }

    /// <summary>
    /// Gets recommended column count for a grid based on window width.
    /// </summary>
    /// <param name="windowWidth">The current window width.</param>
    /// <param name="minColumnWidth">Minimum width for each column.</param>
    /// <param name="maxColumns">Maximum number of columns allowed.</param>
    public static int GetRecommendedColumnCount(double windowWidth, double minColumnWidth = 300, int maxColumns = 4)
    {
        var availableWidth = windowWidth - 48; // Account for padding
        var columns = (int)Math.Floor(availableWidth / minColumnWidth);
        return Math.Clamp(columns, 1, maxColumns);
    }

    /// <summary>
    /// Gets recommended padding based on window width.
    /// </summary>
    public static Thickness GetResponsivePadding(double windowWidth)
    {
        return GetLayoutSize(windowWidth) switch
        {
            LayoutSize.Compact => new Thickness(12),
            LayoutSize.Medium => new Thickness(16),
            LayoutSize.Expanded => new Thickness(20),
            _ => new Thickness(24)
        };
    }

    /// <summary>
    /// Gets recommended spacing between cards based on window width.
    /// </summary>
    public static double GetCardSpacing(double windowWidth)
    {
        return GetLayoutSize(windowWidth) switch
        {
            LayoutSize.Compact => 8,
            LayoutSize.Medium => 12,
            LayoutSize.Expanded => 16,
            _ => 24
        };
    }

    /// <summary>
    /// Determines if the navigation pane should be in overlay mode.
    /// </summary>
    public static bool ShouldUseOverlayNavigation(double windowWidth)
    {
        return windowWidth <= Breakpoints.Medium;
    }

    /// <summary>
    /// Gets the recommended NavigationView display mode.
    /// </summary>
    public static NavigationViewPaneDisplayMode GetNavigationDisplayMode(double windowWidth)
    {
        return GetLayoutSize(windowWidth) switch
        {
            LayoutSize.Compact => NavigationViewPaneDisplayMode.LeftMinimal,
            LayoutSize.Medium => NavigationViewPaneDisplayMode.LeftCompact,
            _ => NavigationViewPaneDisplayMode.Left
        };
    }
}

/// <summary>
/// Layout size categories for responsive design.
/// </summary>
public enum LayoutSize
{
    /// <summary>Compact layout (0-640px) - Single column, minimal UI</summary>
    Compact,

    /// <summary>Medium layout (641-1007px) - Two columns, condensed UI</summary>
    Medium,

    /// <summary>Expanded layout (1008-1365px) - Three columns, full UI</summary>
    Expanded,

    /// <summary>Wide layout (1366px+) - Four columns, spacious UI</summary>
    Wide
}

/// <summary>
/// Attached properties for responsive behavior on FrameworkElements.
/// </summary>
public static class ResponsiveProperties
{
    #region MinWidth

    public static readonly DependencyProperty MinWidthProperty =
        DependencyProperty.RegisterAttached(
            "MinWidth",
            typeof(double),
            typeof(ResponsiveProperties),
            new PropertyMetadata(0.0));

    public static void SetMinWidth(DependencyObject element, double value)
    {
        element.SetValue(MinWidthProperty, value);
    }

    public static double GetMinWidth(DependencyObject element)
    {
        return (double)element.GetValue(MinWidthProperty);
    }

    #endregion

    #region HideOnCompact

    public static readonly DependencyProperty HideOnCompactProperty =
        DependencyProperty.RegisterAttached(
            "HideOnCompact",
            typeof(bool),
            typeof(ResponsiveProperties),
            new PropertyMetadata(false));

    public static void SetHideOnCompact(DependencyObject element, bool value)
    {
        element.SetValue(HideOnCompactProperty, value);
    }

    public static bool GetHideOnCompact(DependencyObject element)
    {
        return (bool)element.GetValue(HideOnCompactProperty);
    }

    #endregion

    #region ColumnSpan

    public static readonly DependencyProperty CompactColumnSpanProperty =
        DependencyProperty.RegisterAttached(
            "CompactColumnSpan",
            typeof(int),
            typeof(ResponsiveProperties),
            new PropertyMetadata(1));

    public static void SetCompactColumnSpan(DependencyObject element, int value)
    {
        element.SetValue(CompactColumnSpanProperty, value);
    }

    public static int GetCompactColumnSpan(DependencyObject element)
    {
        return (int)element.GetValue(CompactColumnSpanProperty);
    }

    #endregion
}
