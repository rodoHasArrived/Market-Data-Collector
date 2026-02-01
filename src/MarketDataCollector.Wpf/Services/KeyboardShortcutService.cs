using System.Windows.Input;

namespace MarketDataCollector.Wpf.Services;

public sealed class KeyboardShortcutService : IKeyboardShortcutService
{
    private readonly ILoggingService _logger;
    private readonly Dictionary<(Key, ModifierKeys), Action> _shortcuts = new();

    public KeyboardShortcutService(ILoggingService logger)
    {
        _logger = logger;
    }

    public void RegisterShortcut(Key key, ModifierKeys modifiers, Action action)
    {
        var shortcut = (key, modifiers);
        _shortcuts[shortcut] = action;
        _logger.Log($"Registered shortcut: {modifiers}+{key}");
    }

    public void UnregisterShortcut(Key key, ModifierKeys modifiers)
    {
        var shortcut = (key, modifiers);
        if (_shortcuts.Remove(shortcut))
        {
            _logger.Log($"Unregistered shortcut: {modifiers}+{key}");
        }
    }

    public bool HandleKeyPress(Key key, ModifierKeys modifiers)
    {
        var shortcut = (key, modifiers);
        if (_shortcuts.TryGetValue(shortcut, out var action))
        {
            _logger.Log($"Executing shortcut: {modifiers}+{key}");
            action.Invoke();
            return true;
        }
        return false;
    }
}
