using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;

namespace MarketDataCollector.Uwp.Services;

/// <summary>
/// Service for creating and managing context menus (right-click menus) throughout the application.
/// Provides reusable menu patterns with keyboard accelerators for common actions.
/// </summary>
public sealed class ContextMenuService
{
    private static readonly Lazy<ContextMenuService> _instance = new(() => new ContextMenuService());
    public static ContextMenuService Instance => _instance.Value;

    private ContextMenuService() { }

    #region Menu Item Builders

    /// <summary>
    /// Creates a menu flyout item with an icon and optional keyboard accelerator.
    /// </summary>
    public MenuFlyoutItem CreateMenuItem(
        string text,
        string iconGlyph,
        RoutedEventHandler clickHandler,
        VirtualKey? acceleratorKey = null,
        VirtualKeyModifiers modifiers = VirtualKeyModifiers.None,
        object? tag = null)
    {
        var item = new MenuFlyoutItem
        {
            Text = text,
            Tag = tag,
            Icon = new FontIcon { Glyph = iconGlyph, FontSize = 14 }
        };

        item.Click += clickHandler;

        if (acceleratorKey.HasValue)
        {
            item.KeyboardAccelerators.Add(new KeyboardAccelerator
            {
                Key = acceleratorKey.Value,
                Modifiers = modifiers
            });
        }

        return item;
    }

    /// <summary>
    /// Creates a submenu with child items.
    /// </summary>
    public MenuFlyoutSubItem CreateSubMenu(string text, string iconGlyph, params MenuFlyoutItemBase[] items)
    {
        var subItem = new MenuFlyoutSubItem
        {
            Text = text,
            Icon = new FontIcon { Glyph = iconGlyph, FontSize = 14 }
        };

        foreach (var item in items)
        {
            subItem.Items.Add(item);
        }

        return subItem;
    }

    /// <summary>
    /// Creates a separator line.
    /// </summary>
    public MenuFlyoutSeparator CreateSeparator() => new();

    #endregion

    #region Symbol Context Menu

    /// <summary>
    /// Creates a context menu for symbol items (watchlist, symbols page, etc.)
    /// </summary>
    public MenuFlyout CreateSymbolContextMenu(
        string symbol,
        bool isFavorite,
        Func<string, Task> onToggleFavorite,
        Func<string, Task> onViewDetails,
        Func<string, Task> onViewLiveData,
        Func<string, Task> onRunBackfill,
        Func<string, Task> onCopySymbol,
        Func<string, Task> onRemove,
        Func<string, Task>? onEdit = null,
        Func<string, Task>? onAddNote = null)
    {
        var menu = new MenuFlyout();

        // View actions
        menu.Items.Add(CreateMenuItem(
            "View Details",
            "\uE8A5", // Info icon
            async (s, e) => await onViewDetails(symbol),
            VirtualKey.Enter,
            tag: symbol));

        menu.Items.Add(CreateMenuItem(
            "View Live Data",
            "\uE9D9", // Chart icon
            async (s, e) => await onViewLiveData(symbol),
            VirtualKey.L,
            VirtualKeyModifiers.Control,
            tag: symbol));

        menu.Items.Add(CreateSeparator());

        // Favorite toggle
        menu.Items.Add(CreateMenuItem(
            isFavorite ? "Remove from Favorites" : "Add to Favorites",
            isFavorite ? "\uE735" : "\uE734", // Filled/unfilled star
            async (s, e) => await onToggleFavorite(symbol),
            VirtualKey.F,
            VirtualKeyModifiers.Control,
            tag: symbol));

        // Edit action (optional)
        if (onEdit != null)
        {
            menu.Items.Add(CreateMenuItem(
                "Edit Symbol",
                "\uE70F", // Edit icon
                async (s, e) => await onEdit(symbol),
                VirtualKey.E,
                VirtualKeyModifiers.Control,
                tag: symbol));
        }

        // Add note (optional)
        if (onAddNote != null)
        {
            menu.Items.Add(CreateMenuItem(
                "Add Note",
                "\uE70B", // Note icon
                async (s, e) => await onAddNote(symbol),
                tag: symbol));
        }

        menu.Items.Add(CreateSeparator());

        // Data actions
        menu.Items.Add(CreateMenuItem(
            "Run Backfill",
            "\uE896", // Download icon
            async (s, e) => await onRunBackfill(symbol),
            VirtualKey.B,
            VirtualKeyModifiers.Control,
            tag: symbol));

        menu.Items.Add(CreateSeparator());

        // Copy and remove
        menu.Items.Add(CreateMenuItem(
            "Copy Symbol",
            "\uE8C8", // Copy icon
            async (s, e) => await onCopySymbol(symbol),
            VirtualKey.C,
            VirtualKeyModifiers.Control,
            tag: symbol));

        menu.Items.Add(CreateSeparator());

        menu.Items.Add(CreateMenuItem(
            "Remove",
            "\uE74D", // Delete icon
            async (s, e) => await onRemove(symbol),
            VirtualKey.Delete,
            tag: symbol));

        return menu;
    }

    #endregion

    #region Subscription Context Menu (Symbols Page)

    /// <summary>
    /// Creates a context menu for subscription items on the symbols page.
    /// </summary>
    public MenuFlyout CreateSubscriptionContextMenu(
        string symbol,
        bool tradesEnabled,
        bool depthEnabled,
        Func<string, Task> onEdit,
        Func<string, bool, Task> onToggleTrades,
        Func<string, bool, Task> onToggleDepth,
        Func<string, Task> onViewLiveData,
        Func<string, Task> onRunBackfill,
        Func<string, Task> onCopySymbol,
        Func<string, Task> onDelete)
    {
        var menu = new MenuFlyout();

        // Edit action
        menu.Items.Add(CreateMenuItem(
            "Edit Subscription",
            "\uE70F", // Edit icon
            async (s, e) => await onEdit(symbol),
            VirtualKey.E,
            VirtualKeyModifiers.Control,
            tag: symbol));

        menu.Items.Add(CreateSeparator());

        // Subscription toggles submenu
        var subscriptionMenu = CreateSubMenu("Subscription", "\uE9D9",
            CreateMenuItem(
                tradesEnabled ? "Disable Trades" : "Enable Trades",
                tradesEnabled ? "\uE73B" : "\uE73A", // Checkbox checked/unchecked
                async (s, e) => await onToggleTrades(symbol, !tradesEnabled),
                tag: symbol),
            CreateMenuItem(
                depthEnabled ? "Disable Depth" : "Enable Depth",
                depthEnabled ? "\uE73B" : "\uE73A", // Checkbox checked/unchecked
                async (s, e) => await onToggleDepth(symbol, !depthEnabled),
                tag: symbol));

        menu.Items.Add(subscriptionMenu);

        menu.Items.Add(CreateSeparator());

        // View and data actions
        menu.Items.Add(CreateMenuItem(
            "View Live Data",
            "\uE9D9", // Chart icon
            async (s, e) => await onViewLiveData(symbol),
            VirtualKey.L,
            VirtualKeyModifiers.Control,
            tag: symbol));

        menu.Items.Add(CreateMenuItem(
            "Run Backfill",
            "\uE896", // Download icon
            async (s, e) => await onRunBackfill(symbol),
            VirtualKey.B,
            VirtualKeyModifiers.Control,
            tag: symbol));

        menu.Items.Add(CreateSeparator());

        // Copy
        menu.Items.Add(CreateMenuItem(
            "Copy Symbol",
            "\uE8C8", // Copy icon
            async (s, e) => await onCopySymbol(symbol),
            VirtualKey.C,
            VirtualKeyModifiers.Control,
            tag: symbol));

        menu.Items.Add(CreateSeparator());

        // Delete
        menu.Items.Add(CreateMenuItem(
            "Delete",
            "\uE74D", // Delete icon
            async (s, e) => await onDelete(symbol),
            VirtualKey.Delete,
            tag: symbol));

        return menu;
    }

    #endregion

    #region Schedule Context Menu

    /// <summary>
    /// Creates a context menu for schedule items (backfill/maintenance).
    /// </summary>
    public MenuFlyout CreateScheduleContextMenu(
        string scheduleId,
        string scheduleName,
        bool isEnabled,
        Func<string, Task> onRunNow,
        Func<string, Task> onEdit,
        Func<string, bool, Task> onToggleEnabled,
        Func<string, Task> onViewHistory,
        Func<string, Task> onClone,
        Func<string, Task> onDelete)
    {
        var menu = new MenuFlyout();

        // Run now
        menu.Items.Add(CreateMenuItem(
            "Run Now",
            "\uE768", // Play icon
            async (s, e) => await onRunNow(scheduleId),
            VirtualKey.R,
            VirtualKeyModifiers.Control,
            tag: scheduleId));

        menu.Items.Add(CreateSeparator());

        // Edit
        menu.Items.Add(CreateMenuItem(
            "Edit Schedule",
            "\uE70F", // Edit icon
            async (s, e) => await onEdit(scheduleId),
            VirtualKey.E,
            VirtualKeyModifiers.Control,
            tag: scheduleId));

        // Enable/Disable toggle
        menu.Items.Add(CreateMenuItem(
            isEnabled ? "Disable Schedule" : "Enable Schedule",
            isEnabled ? "\uE8FB" : "\uE768", // Pause/Play icon
            async (s, e) => await onToggleEnabled(scheduleId, !isEnabled),
            tag: scheduleId));

        menu.Items.Add(CreateSeparator());

        // View history
        menu.Items.Add(CreateMenuItem(
            "View History",
            "\uE81C", // History icon
            async (s, e) => await onViewHistory(scheduleId),
            VirtualKey.H,
            VirtualKeyModifiers.Control,
            tag: scheduleId));

        // Clone
        menu.Items.Add(CreateMenuItem(
            "Clone Schedule",
            "\uE8C8", // Copy icon
            async (s, e) => await onClone(scheduleId),
            tag: scheduleId));

        menu.Items.Add(CreateSeparator());

        // Delete
        menu.Items.Add(CreateMenuItem(
            "Delete",
            "\uE74D", // Delete icon
            async (s, e) => await onDelete(scheduleId),
            VirtualKey.Delete,
            tag: scheduleId));

        return menu;
    }

    #endregion

    #region Generic List Item Context Menu

    /// <summary>
    /// Creates a generic context menu for list items with common actions.
    /// </summary>
    public MenuFlyout CreateGenericListItemMenu(
        object item,
        Func<object, Task>? onView = null,
        Func<object, Task>? onEdit = null,
        Func<object, Task>? onCopy = null,
        Func<object, Task>? onDelete = null,
        Func<object, Task>? onRefresh = null,
        IEnumerable<(string text, string icon, Func<object, Task> action)>? customActions = null)
    {
        var menu = new MenuFlyout();

        // View
        if (onView != null)
        {
            menu.Items.Add(CreateMenuItem(
                "View Details",
                "\uE8A5", // Info icon
                async (s, e) => await onView(item),
                VirtualKey.Enter,
                tag: item));
        }

        // Edit
        if (onEdit != null)
        {
            menu.Items.Add(CreateMenuItem(
                "Edit",
                "\uE70F", // Edit icon
                async (s, e) => await onEdit(item),
                VirtualKey.E,
                VirtualKeyModifiers.Control,
                tag: item));
        }

        // Custom actions
        if (customActions != null && customActions.Any())
        {
            if (menu.Items.Count > 0)
                menu.Items.Add(CreateSeparator());

            foreach (var (text, icon, action) in customActions)
            {
                menu.Items.Add(CreateMenuItem(
                    text,
                    icon,
                    async (s, e) => await action(item),
                    tag: item));
            }
        }

        // Refresh
        if (onRefresh != null)
        {
            if (menu.Items.Count > 0)
                menu.Items.Add(CreateSeparator());

            menu.Items.Add(CreateMenuItem(
                "Refresh",
                "\uE72C", // Refresh icon
                async (s, e) => await onRefresh(item),
                VirtualKey.F5,
                tag: item));
        }

        // Copy
        if (onCopy != null)
        {
            if (menu.Items.Count > 0)
                menu.Items.Add(CreateSeparator());

            menu.Items.Add(CreateMenuItem(
                "Copy",
                "\uE8C8", // Copy icon
                async (s, e) => await onCopy(item),
                VirtualKey.C,
                VirtualKeyModifiers.Control,
                tag: item));
        }

        // Delete
        if (onDelete != null)
        {
            if (menu.Items.Count > 0)
                menu.Items.Add(CreateSeparator());

            menu.Items.Add(CreateMenuItem(
                "Delete",
                "\uE74D", // Delete icon
                async (s, e) => await onDelete(item),
                VirtualKey.Delete,
                tag: item));
        }

        return menu;
    }

    #endregion

    #region Data/Archive Context Menu

    /// <summary>
    /// Creates a context menu for data/archive file items.
    /// </summary>
    public MenuFlyout CreateDataFileContextMenu(
        string filePath,
        Func<string, Task> onView,
        Func<string, Task> onExport,
        Func<string, Task> onCompress,
        Func<string, Task> onVerifyIntegrity,
        Func<string, Task> onDelete,
        Func<string, Task>? onReplay = null)
    {
        var menu = new MenuFlyout();

        // View
        menu.Items.Add(CreateMenuItem(
            "View Contents",
            "\uE8A5", // Info icon
            async (s, e) => await onView(filePath),
            VirtualKey.Enter,
            tag: filePath));

        menu.Items.Add(CreateSeparator());

        // Export submenu
        menu.Items.Add(CreateMenuItem(
            "Export",
            "\uEDE1", // Export icon
            async (s, e) => await onExport(filePath),
            VirtualKey.S,
            VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift,
            tag: filePath));

        // Replay (optional)
        if (onReplay != null)
        {
            menu.Items.Add(CreateMenuItem(
                "Replay Events",
                "\uE768", // Play icon
                async (s, e) => await onReplay(filePath),
                tag: filePath));
        }

        menu.Items.Add(CreateSeparator());

        // Maintenance actions
        menu.Items.Add(CreateMenuItem(
            "Compress",
            "\uE7B8", // Compress icon
            async (s, e) => await onCompress(filePath),
            tag: filePath));

        menu.Items.Add(CreateMenuItem(
            "Verify Integrity",
            "\uE9D5", // Shield icon
            async (s, e) => await onVerifyIntegrity(filePath),
            tag: filePath));

        menu.Items.Add(CreateSeparator());

        // Delete
        menu.Items.Add(CreateMenuItem(
            "Delete",
            "\uE74D", // Delete icon
            async (s, e) => await onDelete(filePath),
            VirtualKey.Delete,
            tag: filePath));

        return menu;
    }

    #endregion

    #region Bulk Actions Context Menu

    /// <summary>
    /// Creates a context menu for bulk operations on selected items.
    /// </summary>
    public MenuFlyout CreateBulkActionsMenu(
        int selectedCount,
        Func<Task>? onSelectAll = null,
        Func<Task>? onDeselectAll = null,
        Func<Task>? onEnableSelected = null,
        Func<Task>? onDisableSelected = null,
        Func<Task>? onDeleteSelected = null,
        Func<Task>? onExportSelected = null)
    {
        var menu = new MenuFlyout();

        // Selection actions
        if (onSelectAll != null)
        {
            menu.Items.Add(CreateMenuItem(
                "Select All",
                "\uE8B3", // Select all icon
                async (s, e) => await onSelectAll(),
                VirtualKey.A,
                VirtualKeyModifiers.Control));
        }

        if (onDeselectAll != null)
        {
            menu.Items.Add(CreateMenuItem(
                "Deselect All",
                "\uE8E6", // Deselect icon
                async (s, e) => await onDeselectAll(),
                VirtualKey.D,
                VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift));
        }

        if ((onSelectAll != null || onDeselectAll != null) &&
            (onEnableSelected != null || onDisableSelected != null || onDeleteSelected != null || onExportSelected != null))
        {
            menu.Items.Add(CreateSeparator());
        }

        // Bulk actions
        if (onEnableSelected != null)
        {
            menu.Items.Add(CreateMenuItem(
                $"Enable Selected ({selectedCount})",
                "\uE73A", // Checkbox icon
                async (s, e) => await onEnableSelected()));
        }

        if (onDisableSelected != null)
        {
            menu.Items.Add(CreateMenuItem(
                $"Disable Selected ({selectedCount})",
                "\uE739", // Unchecked icon
                async (s, e) => await onDisableSelected()));
        }

        if (onExportSelected != null)
        {
            menu.Items.Add(CreateMenuItem(
                $"Export Selected ({selectedCount})",
                "\uEDE1", // Export icon
                async (s, e) => await onExportSelected()));
        }

        if (onDeleteSelected != null)
        {
            if (menu.Items.Count > 0)
                menu.Items.Add(CreateSeparator());

            menu.Items.Add(CreateMenuItem(
                $"Delete Selected ({selectedCount})",
                "\uE74D", // Delete icon
                async (s, e) => await onDeleteSelected(),
                VirtualKey.Delete,
                VirtualKeyModifiers.Shift));
        }

        return menu;
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Copies text to the clipboard.
    /// </summary>
    public void CopyToClipboard(string text)
    {
        var dataPackage = new DataPackage();
        dataPackage.SetText(text);
        Clipboard.SetContent(dataPackage);
    }

    /// <summary>
    /// Shows a context menu at the pointer position.
    /// </summary>
    public void ShowAtPointer(MenuFlyout menu, UIElement element, RightTappedRoutedEventArgs e)
    {
        menu.ShowAt(element, e.GetPosition(element));
    }

    /// <summary>
    /// Shows a context menu at the pointer position (from PointerRoutedEventArgs).
    /// </summary>
    public void ShowAtPointer(MenuFlyout menu, UIElement element, PointerRoutedEventArgs e)
    {
        menu.ShowAt(element, e.GetCurrentPoint(element).Position);
    }

    /// <summary>
    /// Shows a context menu relative to a specific element.
    /// </summary>
    public void ShowAt(MenuFlyout menu, FrameworkElement element)
    {
        menu.ShowAt(element);
    }

    #endregion
}
