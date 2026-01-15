using System;
using System.Collections.Generic;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace MarketDataCollector.Uwp.Services;

/// <summary>
/// Service for managing global keyboard shortcuts.
/// </summary>
public sealed class KeyboardShortcutService
{
    private static KeyboardShortcutService? _instance;
    private static readonly object _lock = new();

    private readonly Dictionary<string, ShortcutAction> _shortcuts = new();
    private readonly Dictionary<string, KeyboardAccelerator> _accelerators = new();
    private UIElement? _rootElement;
    private bool _isEnabled = true;

    public static KeyboardShortcutService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new KeyboardShortcutService();
                }
            }
            return _instance;
        }
    }

    private KeyboardShortcutService()
    {
        InitializeDefaultShortcuts();
    }

    /// <summary>
    /// Gets or sets whether keyboard shortcuts are enabled.
    /// </summary>
    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            _isEnabled = value;
            UpdateAcceleratorsEnabled();
        }
    }

    /// <summary>
    /// Gets all registered shortcuts.
    /// </summary>
    public IReadOnlyDictionary<string, ShortcutAction> Shortcuts => _shortcuts;

    /// <summary>
    /// Initializes the keyboard shortcut service with the root element.
    /// </summary>
    public void Initialize(UIElement rootElement)
    {
        _rootElement = rootElement;
        RegisterAccelerators();
    }

    /// <summary>
    /// Registers default keyboard shortcuts.
    /// </summary>
    private void InitializeDefaultShortcuts()
    {
        // Navigation shortcuts
        RegisterShortcut("NavigateDashboard", VirtualKey.D, VirtualKeyModifiers.Control,
            "Navigate to Dashboard", ShortcutCategory.Navigation);

        RegisterShortcut("NavigateSymbols", VirtualKey.Y, VirtualKeyModifiers.Control,
            "Navigate to Symbols", ShortcutCategory.Navigation);

        RegisterShortcut("NavigateBackfill", VirtualKey.B, VirtualKeyModifiers.Control,
            "Navigate to Backfill", ShortcutCategory.Navigation);

        RegisterShortcut("NavigateSettings", VirtualKey.Number0, VirtualKeyModifiers.Control,
            "Open Settings", ShortcutCategory.Navigation);

        // Collector shortcuts
        RegisterShortcut("StartCollector", VirtualKey.S, VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift,
            "Start Collector", ShortcutCategory.Collector);

        RegisterShortcut("StopCollector", VirtualKey.Q, VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift,
            "Stop Collector", ShortcutCategory.Collector);

        RegisterShortcut("RefreshStatus", VirtualKey.F5, VirtualKeyModifiers.None,
            "Refresh Status", ShortcutCategory.Collector);

        // Backfill shortcuts
        RegisterShortcut("RunBackfill", VirtualKey.R, VirtualKeyModifiers.Control,
            "Run Backfill", ShortcutCategory.Backfill);

        RegisterShortcut("PauseBackfill", VirtualKey.P, VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift,
            "Pause/Resume Backfill", ShortcutCategory.Backfill);

        RegisterShortcut("CancelBackfill", VirtualKey.Escape, VirtualKeyModifiers.None,
            "Cancel Backfill", ShortcutCategory.Backfill);

        // Symbol shortcuts
        RegisterShortcut("AddSymbol", VirtualKey.N, VirtualKeyModifiers.Control,
            "Add New Symbol", ShortcutCategory.Symbols);

        RegisterShortcut("SearchSymbols", VirtualKey.F, VirtualKeyModifiers.Control,
            "Search Symbols", ShortcutCategory.Symbols);

        RegisterShortcut("DeleteSelected", VirtualKey.Delete, VirtualKeyModifiers.None,
            "Delete Selected", ShortcutCategory.Symbols);

        RegisterShortcut("SelectAll", VirtualKey.A, VirtualKeyModifiers.Control,
            "Select All", ShortcutCategory.Symbols);

        // View shortcuts
        RegisterShortcut("ToggleTheme", VirtualKey.T, VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift,
            "Toggle Theme", ShortcutCategory.View);

        RegisterShortcut("ViewLogs", VirtualKey.L, VirtualKeyModifiers.Control,
            "View Logs", ShortcutCategory.View);

        RegisterShortcut("ZoomIn", VirtualKey.Add, VirtualKeyModifiers.Control,
            "Zoom In", ShortcutCategory.View);

        RegisterShortcut("ZoomOut", VirtualKey.Subtract, VirtualKeyModifiers.Control,
            "Zoom Out", ShortcutCategory.View);

        // General shortcuts
        RegisterShortcut("Save", VirtualKey.S, VirtualKeyModifiers.Control,
            "Save", ShortcutCategory.General);

        RegisterShortcut("Help", VirtualKey.F1, VirtualKeyModifiers.None,
            "Show Help", ShortcutCategory.General);

        RegisterShortcut("QuickCommand", VirtualKey.K, VirtualKeyModifiers.Control,
            "Quick Command", ShortcutCategory.General);
    }

    /// <summary>
    /// Registers a keyboard shortcut.
    /// </summary>
    public void RegisterShortcut(
        string actionId,
        VirtualKey key,
        VirtualKeyModifiers modifiers,
        string description,
        ShortcutCategory category = ShortcutCategory.General)
    {
        var action = new ShortcutAction
        {
            ActionId = actionId,
            Key = key,
            Modifiers = modifiers,
            Description = description,
            Category = category,
            IsEnabled = true
        };

        _shortcuts[actionId] = action;
    }

    /// <summary>
    /// Updates an existing shortcut's key binding.
    /// </summary>
    public void UpdateShortcut(string actionId, VirtualKey key, VirtualKeyModifiers modifiers)
    {
        if (_shortcuts.TryGetValue(actionId, out var action))
        {
            action.Key = key;
            action.Modifiers = modifiers;

            // Re-register accelerator
            if (_rootElement != null)
            {
                UnregisterAccelerator(actionId);
                RegisterAccelerator(actionId, action);
            }
        }
    }

    /// <summary>
    /// Enables or disables a specific shortcut.
    /// </summary>
    public void SetShortcutEnabled(string actionId, bool enabled)
    {
        if (_shortcuts.TryGetValue(actionId, out var action))
        {
            action.IsEnabled = enabled;

            if (_accelerators.TryGetValue(actionId, out var accelerator))
            {
                accelerator.IsEnabled = enabled && _isEnabled;
            }
        }
    }

    private void RegisterAccelerators()
    {
        if (_rootElement == null) return;

        foreach (var kvp in _shortcuts)
        {
            RegisterAccelerator(kvp.Key, kvp.Value);
        }
    }

    private void RegisterAccelerator(string actionId, ShortcutAction action)
    {
        if (_rootElement == null) return;

        var accelerator = new KeyboardAccelerator
        {
            Key = action.Key,
            Modifiers = action.Modifiers,
            IsEnabled = action.IsEnabled && _isEnabled
        };

        accelerator.Invoked += (sender, args) =>
        {
            args.Handled = true;
            OnShortcutInvoked(actionId);
        };

        _accelerators[actionId] = accelerator;
        _rootElement.KeyboardAccelerators.Add(accelerator);
    }

    private void UnregisterAccelerator(string actionId)
    {
        if (_rootElement == null) return;

        if (_accelerators.TryGetValue(actionId, out var accelerator))
        {
            _rootElement.KeyboardAccelerators.Remove(accelerator);
            _accelerators.Remove(actionId);
        }
    }

    private void UpdateAcceleratorsEnabled()
    {
        foreach (var kvp in _accelerators)
        {
            if (_shortcuts.TryGetValue(kvp.Key, out var action))
            {
                kvp.Value.IsEnabled = action.IsEnabled && _isEnabled;
            }
        }
    }

    private void OnShortcutInvoked(string actionId)
    {
        if (!_isEnabled) return;
        if (!_shortcuts.TryGetValue(actionId, out var action) || !action.IsEnabled) return;

        ShortcutInvoked?.Invoke(this, new ShortcutInvokedEventArgs
        {
            ActionId = actionId,
            Action = action
        });
    }

    /// <summary>
    /// Gets a formatted shortcut string (e.g., "Ctrl+S").
    /// </summary>
    public static string FormatShortcut(VirtualKey key, VirtualKeyModifiers modifiers)
    {
        var parts = new List<string>();

        if (modifiers.HasFlag(VirtualKeyModifiers.Control))
            parts.Add("Ctrl");
        if (modifiers.HasFlag(VirtualKeyModifiers.Shift))
            parts.Add("Shift");
        if (modifiers.HasFlag(VirtualKeyModifiers.Menu))
            parts.Add("Alt");
        if (modifiers.HasFlag(VirtualKeyModifiers.Windows))
            parts.Add("Win");

        parts.Add(FormatKey(key));

        return string.Join("+", parts);
    }

    private static string FormatKey(VirtualKey key)
    {
        return key switch
        {
            VirtualKey.Number0 => "0",
            VirtualKey.Number1 => "1",
            VirtualKey.Number2 => "2",
            VirtualKey.Number3 => "3",
            VirtualKey.Number4 => "4",
            VirtualKey.Number5 => "5",
            VirtualKey.Number6 => "6",
            VirtualKey.Number7 => "7",
            VirtualKey.Number8 => "8",
            VirtualKey.Number9 => "9",
            VirtualKey.Add => "+",
            VirtualKey.Subtract => "-",
            VirtualKey.Multiply => "*",
            VirtualKey.Divide => "/",
            VirtualKey.Escape => "Esc",
            VirtualKey.Delete => "Del",
            VirtualKey.Back => "Backspace",
            VirtualKey.Enter => "Enter",
            VirtualKey.Space => "Space",
            VirtualKey.Tab => "Tab",
            _ => key.ToString()
        };
    }

    /// <summary>
    /// Gets all shortcuts grouped by category.
    /// </summary>
    public Dictionary<ShortcutCategory, List<ShortcutAction>> GetShortcutsByCategory()
    {
        var result = new Dictionary<ShortcutCategory, List<ShortcutAction>>();

        foreach (var shortcut in _shortcuts.Values)
        {
            if (!result.ContainsKey(shortcut.Category))
            {
                result[shortcut.Category] = new List<ShortcutAction>();
            }
            result[shortcut.Category].Add(shortcut);
        }

        return result;
    }

    /// <summary>
    /// Event raised when a shortcut is invoked.
    /// </summary>
    public event EventHandler<ShortcutInvokedEventArgs>? ShortcutInvoked;
}

/// <summary>
/// Shortcut action definition.
/// </summary>
public class ShortcutAction
{
    public string ActionId { get; set; } = string.Empty;
    public VirtualKey Key { get; set; }
    public VirtualKeyModifiers Modifiers { get; set; }
    public string Description { get; set; } = string.Empty;
    public ShortcutCategory Category { get; set; }
    public bool IsEnabled { get; set; } = true;

    public string FormattedShortcut => KeyboardShortcutService.FormatShortcut(Key, Modifiers);
}

/// <summary>
/// Shortcut categories.
/// </summary>
public enum ShortcutCategory
{
    General,
    Navigation,
    Collector,
    Backfill,
    Symbols,
    View
}

/// <summary>
/// Shortcut invoked event args.
/// </summary>
public class ShortcutInvokedEventArgs : EventArgs
{
    public string ActionId { get; set; } = string.Empty;
    public ShortcutAction? Action { get; set; }
}
