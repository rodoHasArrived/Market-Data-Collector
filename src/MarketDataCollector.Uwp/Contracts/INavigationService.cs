using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml.Controls;

namespace MarketDataCollector.Uwp.Contracts;

/// <summary>
/// Interface for managing navigation throughout the application.
/// Enables testability and dependency injection.
/// </summary>
public interface INavigationService
{
    /// <summary>
    /// Gets whether navigation can go back.
    /// </summary>
    bool CanGoBack { get; }

    /// <summary>
    /// Initializes the navigation service with the main frame.
    /// </summary>
    void Initialize(Frame frame);

    /// <summary>
    /// Navigates to a page by tag name.
    /// </summary>
    bool NavigateTo(string pageTag, object? parameter = null);

    /// <summary>
    /// Navigates to a page type directly.
    /// </summary>
    bool NavigateTo(Type pageType, object? parameter = null);

    /// <summary>
    /// Navigates back.
    /// </summary>
    void GoBack();

    /// <summary>
    /// Gets the page type for a given tag.
    /// </summary>
    Type? GetPageType(string pageTag);

    /// <summary>
    /// Gets navigation breadcrumbs.
    /// </summary>
    IReadOnlyList<NavigationEntry> GetBreadcrumbs();

    /// <summary>
    /// Event raised when navigation occurs.
    /// </summary>
    event EventHandler<NavigationEventArgs>? Navigated;
}

/// <summary>
/// Represents a navigation history entry.
/// </summary>
public sealed class NavigationEntry
{
    public string PageTag { get; init; } = string.Empty;
    public object? Parameter { get; init; }
    public DateTime Timestamp { get; init; }
}

/// <summary>
/// Navigation event arguments.
/// </summary>
public sealed class NavigationEventArgs : EventArgs
{
    public string PageTag { get; init; } = string.Empty;
    public object? Parameter { get; init; }
}
