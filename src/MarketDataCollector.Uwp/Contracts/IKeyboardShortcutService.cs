using System;
using System.Collections.Generic;
using MarketDataCollector.Uwp.Services;
using Microsoft.UI.Xaml;
using Windows.System;

namespace MarketDataCollector.Uwp.Contracts;

/// <summary>
/// Interface for managing global keyboard shortcuts.
/// </summary>
public interface IKeyboardShortcutService
{
    bool IsEnabled { get; set; }
    IReadOnlyDictionary<string, ShortcutAction> Shortcuts { get; }

    void Initialize(UIElement rootElement);
    void RegisterShortcut(string actionId, VirtualKey key, VirtualKeyModifiers modifiers, string description, ShortcutCategory category = ShortcutCategory.General);
    void UpdateShortcut(string actionId, VirtualKey key, VirtualKeyModifiers modifiers);
    void SetShortcutEnabled(string actionId, bool enabled);
    Dictionary<ShortcutCategory, List<ShortcutAction>> GetShortcutsByCategory();

    event EventHandler<ShortcutInvokedEventArgs>? ShortcutInvoked;
}
