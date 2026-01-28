using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MarketDataCollector.Uwp.Services;

namespace MarketDataCollector.Uwp.Views;

/// <summary>
/// Page displaying all available keyboard shortcuts for the application.
/// Dynamically loads shortcuts from the KeyboardShortcutService for accurate documentation.
/// </summary>
public sealed partial class KeyboardShortcutsPage : Page
{
    public KeyboardShortcutsPage()
    {
        this.InitializeComponent();
        Loaded += KeyboardShortcutsPage_Loaded;
    }

    private void KeyboardShortcutsPage_Loaded(object sender, RoutedEventArgs e)
    {
        LoadShortcuts();
    }

    /// <summary>
    /// Loads shortcuts from the KeyboardShortcutService and populates the dynamic shortcuts panel.
    /// </summary>
    private void LoadShortcuts()
    {
        var shortcutService = KeyboardShortcutService.Instance;
        var shortcutsByCategory = shortcutService.GetShortcutsByCategory();

        // Find the dynamic shortcuts panel in the XAML
        if (FindName("DynamicShortcutsPanel") is StackPanel panel)
        {
            panel.Children.Clear();

            foreach (var category in shortcutsByCategory.OrderBy(c => GetCategoryOrder(c.Key)))
            {
                var categoryPanel = CreateCategoryPanel(category.Key, category.Value);
                panel.Children.Add(categoryPanel);
            }
        }
    }

    /// <summary>
    /// Creates a panel displaying shortcuts for a specific category.
    /// </summary>
    private static Border CreateCategoryPanel(ShortcutCategory category, List<ShortcutAction> shortcuts)
    {
        var border = new Border
        {
            Style = Application.Current.Resources["CardStyle"] as Style,
            Margin = new Thickness(0, 0, 0, 16)
        };

        var stack = new StackPanel { Spacing = 16 };

        // Category title
        var titleText = new TextBlock
        {
            Text = GetCategoryDisplayName(category),
            Style = Application.Current.Resources["SubtitleTextBlockStyle"] as Style
        };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetHeadingLevel(titleText, Microsoft.UI.Xaml.Automation.Peers.AutomationHeadingLevel.Level2);
        stack.Children.Add(titleText);

        // Grid for shortcuts
        var grid = new Grid
        {
            RowSpacing = 12,
            ColumnSpacing = 24
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var row = 0;
        foreach (var shortcut in shortcuts.OrderBy(s => s.Description))
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Shortcut key badge
            var keyBorder = new Border
            {
                Background = Application.Current.Resources["SystemFillColorNeutralBackgroundBrush"] as Microsoft.UI.Xaml.Media.Brush,
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 4, 8, 4),
                HorizontalAlignment = HorizontalAlignment.Left
            };

            var keyText = new TextBlock
            {
                Text = shortcut.FormattedShortcut,
                FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Cascadia Code"),
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            };
            keyBorder.Child = keyText;

            Grid.SetRow(keyBorder, row);
            Grid.SetColumn(keyBorder, 0);
            grid.Children.Add(keyBorder);

            // Description
            var descText = new TextBlock
            {
                Text = shortcut.Description,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(descText, row);
            Grid.SetColumn(descText, 1);
            grid.Children.Add(descText);

            row++;
        }

        stack.Children.Add(grid);
        border.Child = stack;

        return border;
    }

    /// <summary>
    /// Gets display name for a shortcut category.
    /// </summary>
    private static string GetCategoryDisplayName(ShortcutCategory category)
    {
        return category switch
        {
            ShortcutCategory.General => "General",
            ShortcutCategory.Navigation => "Navigation",
            ShortcutCategory.Collector => "Collector Control",
            ShortcutCategory.Backfill => "Backfill Operations",
            ShortcutCategory.Symbols => "Symbol Management",
            ShortcutCategory.View => "View & Display",
            _ => category.ToString()
        };
    }

    /// <summary>
    /// Gets the display order for a category.
    /// </summary>
    private static int GetCategoryOrder(ShortcutCategory category)
    {
        return category switch
        {
            ShortcutCategory.Collector => 0,
            ShortcutCategory.Navigation => 1,
            ShortcutCategory.Symbols => 2,
            ShortcutCategory.Backfill => 3,
            ShortcutCategory.View => 4,
            ShortcutCategory.General => 5,
            _ => 99
        };
    }
}
