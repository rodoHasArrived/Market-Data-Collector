using System;

namespace MarketDataCollector.Ui.Services.Contracts;

/// <summary>
/// Platform-agnostic keyboard shortcut service contract used for DI registration.
/// Platform implementations expose additional members for framework-specific key models.
/// </summary>
public interface IKeyboardShortcutService
{
    /// <summary>
    /// Gets or sets whether keyboard shortcuts are enabled globally.
    /// </summary>
    bool IsEnabled { get; set; }
}

