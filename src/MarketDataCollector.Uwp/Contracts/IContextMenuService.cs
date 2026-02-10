using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace MarketDataCollector.Uwp.Contracts;

/// <summary>
/// Interface for context menu creation and management.
/// Provides factory methods for creating pre-configured context menus
/// for symbols, subscriptions, schedules, data files, and bulk actions.
/// </summary>
public interface IContextMenuService
{
    MenuFlyoutItem CreateMenuItem(
        string text,
        string iconGlyph,
        RoutedEventHandler clickHandler,
        Windows.System.VirtualKey? acceleratorKey = null,
        Windows.System.VirtualKeyModifiers modifiers = Windows.System.VirtualKeyModifiers.None,
        object? tag = null);

    MenuFlyoutSubItem CreateSubMenu(string text, string iconGlyph, params MenuFlyoutItemBase[] items);

    MenuFlyoutSeparator CreateSeparator();

    MenuFlyout CreateSymbolContextMenu(
        string symbol,
        bool isFavorite,
        Func<string, Task> onToggleFavorite,
        Func<string, Task> onViewDetails,
        Func<string, Task> onViewLiveData,
        Func<string, Task> onRunBackfill,
        Func<string, Task> onCopySymbol,
        Func<string, Task> onRemove,
        Func<string, Task>? onEdit = null,
        Func<string, Task>? onAddNote = null);

    MenuFlyout CreateSubscriptionContextMenu(
        string symbol,
        bool tradesEnabled,
        bool depthEnabled,
        Func<string, Task> onEdit,
        Func<string, bool, Task> onToggleTrades,
        Func<string, bool, Task> onToggleDepth,
        Func<string, Task> onViewLiveData,
        Func<string, Task> onRunBackfill,
        Func<string, Task> onCopySymbol,
        Func<string, Task> onDelete);

    MenuFlyout CreateScheduleContextMenu(
        string scheduleId,
        string scheduleName,
        bool isEnabled,
        Func<string, Task> onRunNow,
        Func<string, Task> onEdit,
        Func<string, bool, Task> onToggleEnabled,
        Func<string, Task> onViewHistory,
        Func<string, Task> onClone,
        Func<string, Task> onDelete);

    MenuFlyout CreateGenericListItemMenu(
        object item,
        Func<object, Task>? onView = null,
        Func<object, Task>? onEdit = null,
        Func<object, Task>? onCopy = null,
        Func<object, Task>? onDelete = null,
        Func<object, Task>? onRefresh = null,
        IEnumerable<(string text, string icon, Func<object, Task> action)>? customActions = null);

    MenuFlyout CreateDataFileContextMenu(
        string filePath,
        Func<string, Task> onView,
        Func<string, Task> onExport,
        Func<string, Task> onCompress,
        Func<string, Task> onVerifyIntegrity,
        Func<string, Task> onDelete,
        Func<string, Task>? onReplay = null);

    MenuFlyout CreateBulkActionsMenu(
        int selectedCount,
        Func<Task>? onSelectAll = null,
        Func<Task>? onDeselectAll = null,
        Func<Task>? onEnableSelected = null,
        Func<Task>? onDisableSelected = null,
        Func<Task>? onDeleteSelected = null,
        Func<Task>? onExportSelected = null);

    void CopyToClipboard(string text);
    void ShowAtPointer(MenuFlyout menu, UIElement element, RightTappedRoutedEventArgs e);
    void ShowAtPointer(MenuFlyout menu, UIElement element, PointerRoutedEventArgs e);
    void ShowAt(MenuFlyout menu, FrameworkElement element);
}
