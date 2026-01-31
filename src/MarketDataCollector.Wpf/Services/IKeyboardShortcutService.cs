using System.Windows.Input;

namespace MarketDataCollector.Wpf.Services;

public interface IKeyboardShortcutService
{
    void RegisterShortcut(Key key, ModifierKeys modifiers, Action action);
    void UnregisterShortcut(Key key, ModifierKeys modifiers);
    bool HandleKeyPress(Key key, ModifierKeys modifiers);
}
