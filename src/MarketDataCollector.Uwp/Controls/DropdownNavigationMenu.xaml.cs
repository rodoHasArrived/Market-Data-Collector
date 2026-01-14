using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.Foundation;
using Windows.System;
using MarketDataCollector.Uwp.Services;

namespace MarketDataCollector.Uwp.Controls;

/// <summary>
/// A dropdown navigation menu triggered by right-flick gestures.
/// Provides quick navigation access with keyboard shortcuts and categorized menu items.
/// </summary>
public sealed partial class DropdownNavigationMenu : UserControl
{
    private bool _isOpen;
    private Point _menuPosition;
    private readonly GestureService _gestureService;
    private Storyboard? _openStoryboard;
    private Storyboard? _closeStoryboard;

    #region Dependency Properties

    public static readonly DependencyProperty HeaderProperty =
        DependencyProperty.Register(nameof(Header), typeof(string), typeof(DropdownNavigationMenu),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty HeaderIconProperty =
        DependencyProperty.Register(nameof(HeaderIcon), typeof(string), typeof(DropdownNavigationMenu),
            new PropertyMetadata("\uE700"));

    public static readonly DependencyProperty FooterProperty =
        DependencyProperty.Register(nameof(Footer), typeof(string), typeof(DropdownNavigationMenu),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty MenuItemsProperty =
        DependencyProperty.Register(nameof(MenuItems), typeof(ObservableCollection<DropdownMenuItemData>),
            typeof(DropdownNavigationMenu),
            new PropertyMetadata(null));

    public static readonly DependencyProperty IsOpenProperty =
        DependencyProperty.Register(nameof(IsOpen), typeof(bool), typeof(DropdownNavigationMenu),
            new PropertyMetadata(false, OnIsOpenChanged));

    #endregion

    #region Properties

    /// <summary>
    /// Gets or sets the header text for the menu.
    /// </summary>
    public string Header
    {
        get => (string)GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    /// <summary>
    /// Gets or sets the header icon glyph.
    /// </summary>
    public string HeaderIcon
    {
        get => (string)GetValue(HeaderIconProperty);
        set => SetValue(HeaderIconProperty, value);
    }

    /// <summary>
    /// Gets or sets the footer hint text.
    /// </summary>
    public string Footer
    {
        get => (string)GetValue(FooterProperty);
        set => SetValue(FooterProperty, value);
    }

    /// <summary>
    /// Gets or sets the collection of menu items.
    /// </summary>
    public ObservableCollection<DropdownMenuItemData> MenuItems
    {
        get => (ObservableCollection<DropdownMenuItemData>)GetValue(MenuItemsProperty);
        set => SetValue(MenuItemsProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the menu is open.
    /// </summary>
    public bool IsOpen
    {
        get => (bool)GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    // Computed visibility properties
    public Visibility HeaderVisibility => string.IsNullOrEmpty(Header) ? Visibility.Collapsed : Visibility.Visible;
    public Visibility FooterVisibility => string.IsNullOrEmpty(Footer) ? Visibility.Collapsed : Visibility.Visible;

    #endregion

    public DropdownNavigationMenu()
    {
        InitializeComponent();
        MenuItems = new ObservableCollection<DropdownMenuItemData>();
        _gestureService = GestureService.Instance;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Subscribe to gesture events
        _gestureService.RightFlickDetected += GestureService_RightFlickDetected;
        _gestureService.LeftFlickDetected += GestureService_LeftFlickDetected;
        _gestureService.FlickProgress += GestureService_FlickProgress;
        _gestureService.FlickCanceled += GestureService_FlickCanceled;

        // Setup backdrop click handler
        BackdropOverlay.PointerPressed += BackdropOverlay_PointerPressed;

        // Setup keyboard handler for Escape key
        if (XamlRoot?.Content is UIElement root)
        {
            root.KeyDown += Root_KeyDown;
        }

        // Create animation storyboards
        CreateAnimations();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _gestureService.RightFlickDetected -= GestureService_RightFlickDetected;
        _gestureService.LeftFlickDetected -= GestureService_LeftFlickDetected;
        _gestureService.FlickProgress -= GestureService_FlickProgress;
        _gestureService.FlickCanceled -= GestureService_FlickCanceled;

        BackdropOverlay.PointerPressed -= BackdropOverlay_PointerPressed;

        if (XamlRoot?.Content is UIElement root)
        {
            root.KeyDown -= Root_KeyDown;
        }
    }

    #region Animation Setup

    private void CreateAnimations()
    {
        // Open animation
        _openStoryboard = new Storyboard();

        var scaleYOpen = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = new Duration(TimeSpan.FromMilliseconds(200)),
            EasingFunction = new CubicBezierEasingFunction { ControlPoint1 = new Point(0, 0), ControlPoint2 = new Point(0.2, 1) }
        };
        Storyboard.SetTarget(scaleYOpen, MenuContainer);
        Storyboard.SetTargetProperty(scaleYOpen, "(UIElement.RenderTransform).(CompositeTransform.ScaleY)");

        var scaleXOpen = new DoubleAnimation
        {
            From = 0.95,
            To = 1,
            Duration = new Duration(TimeSpan.FromMilliseconds(200)),
            EasingFunction = new CubicBezierEasingFunction { ControlPoint1 = new Point(0, 0), ControlPoint2 = new Point(0.2, 1) }
        };
        Storyboard.SetTarget(scaleXOpen, MenuContainer);
        Storyboard.SetTargetProperty(scaleXOpen, "(UIElement.RenderTransform).(CompositeTransform.ScaleX)");

        var opacityOpen = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = new Duration(TimeSpan.FromMilliseconds(150))
        };
        Storyboard.SetTarget(opacityOpen, MenuContainer);
        Storyboard.SetTargetProperty(opacityOpen, "Opacity");

        var backdropOpen = new DoubleAnimation
        {
            From = 0,
            To = 0.4,
            Duration = new Duration(TimeSpan.FromMilliseconds(250))
        };
        Storyboard.SetTarget(backdropOpen, BackdropOverlay);
        Storyboard.SetTargetProperty(backdropOpen, "Opacity");

        _openStoryboard.Children.Add(scaleYOpen);
        _openStoryboard.Children.Add(scaleXOpen);
        _openStoryboard.Children.Add(opacityOpen);
        _openStoryboard.Children.Add(backdropOpen);

        // Close animation
        _closeStoryboard = new Storyboard();

        var scaleYClose = new DoubleAnimation
        {
            From = 1,
            To = 0,
            Duration = new Duration(TimeSpan.FromMilliseconds(150)),
            EasingFunction = new CubicBezierEasingFunction { ControlPoint1 = new Point(0.4, 0), ControlPoint2 = new Point(1, 1) }
        };
        Storyboard.SetTarget(scaleYClose, MenuContainer);
        Storyboard.SetTargetProperty(scaleYClose, "(UIElement.RenderTransform).(CompositeTransform.ScaleY)");

        var opacityClose = new DoubleAnimation
        {
            From = 1,
            To = 0,
            Duration = new Duration(TimeSpan.FromMilliseconds(100))
        };
        Storyboard.SetTarget(opacityClose, MenuContainer);
        Storyboard.SetTargetProperty(opacityClose, "Opacity");

        var backdropClose = new DoubleAnimation
        {
            From = 0.4,
            To = 0,
            Duration = new Duration(TimeSpan.FromMilliseconds(200))
        };
        Storyboard.SetTarget(backdropClose, BackdropOverlay);
        Storyboard.SetTargetProperty(backdropClose, "Opacity");

        _closeStoryboard.Children.Add(scaleYClose);
        _closeStoryboard.Children.Add(opacityClose);
        _closeStoryboard.Children.Add(backdropClose);

        _closeStoryboard.Completed += (s, e) =>
        {
            MenuContainer.Visibility = Visibility.Collapsed;
            BackdropOverlay.Visibility = Visibility.Collapsed;
            _isOpen = false;
        };
    }

    #endregion

    #region Gesture Handlers

    private void GestureService_RightFlickDetected(object? sender, FlickDetectedEventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            _menuPosition = e.Position;
            Open(_menuPosition);
        });
    }

    private void GestureService_LeftFlickDetected(object? sender, FlickDetectedEventArgs e)
    {
        if (_isOpen)
        {
            DispatcherQueue.TryEnqueue(Close);
        }
    }

    private void GestureService_FlickProgress(object? sender, FlickProgressEventArgs e)
    {
        if (e.Direction == FlickDirection.Right && !_isOpen)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                // Show flick indicator
                FlickIndicator.Visibility = Visibility.Visible;
                FlickIndicator.Opacity = Math.Min(1.0, e.Progress * 1.5);

                // Position indicator at gesture location
                FlickIndicator.Margin = new Thickness(0, e.Position.Y - 20, 0, 0);
            });
        }
    }

    private void GestureService_FlickCanceled(object? sender, FlickCanceledEventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            FlickIndicator.Opacity = 0;
            FlickIndicator.Visibility = Visibility.Collapsed;
        });
    }

    #endregion

    #region Open/Close Methods

    /// <summary>
    /// Opens the dropdown menu at the specified position.
    /// </summary>
    /// <param name="position">The position to open the menu at.</param>
    public void Open(Point position)
    {
        if (_isOpen)
            return;

        _isOpen = true;
        _menuPosition = position;

        // Hide flick indicator
        FlickIndicator.Opacity = 0;
        FlickIndicator.Visibility = Visibility.Collapsed;

        // Position the menu
        PositionMenu(position);

        // Show elements
        BackdropOverlay.Visibility = Visibility.Visible;
        MenuContainer.Visibility = Visibility.Visible;

        // Run animation
        _openStoryboard?.Begin();

        // Raise event
        Opened?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Opens the dropdown menu at the default position (top-left of container).
    /// </summary>
    public void Open()
    {
        Open(new Point(20, 80));
    }

    /// <summary>
    /// Closes the dropdown menu.
    /// </summary>
    public void Close()
    {
        if (!_isOpen)
            return;

        // Run close animation
        _closeStoryboard?.Begin();

        // Raise event
        Closed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Toggles the dropdown menu open/closed state.
    /// </summary>
    public void Toggle()
    {
        if (_isOpen)
            Close();
        else
            Open();
    }

    private void PositionMenu(Point position)
    {
        // Get available space
        var containerWidth = RootGrid.ActualWidth;
        var containerHeight = RootGrid.ActualHeight;

        // Default position (slight offset from flick position)
        var menuX = Math.Max(8, position.X - 20);
        var menuY = Math.Max(8, position.Y - 40);

        // Ensure menu doesn't go off-screen (estimate menu size)
        var estimatedMenuWidth = 260.0;
        var estimatedMenuHeight = Math.Min(400.0, MenuItems.Count * 44 + 100);

        if (menuX + estimatedMenuWidth > containerWidth - 8)
        {
            menuX = containerWidth - estimatedMenuWidth - 8;
        }

        if (menuY + estimatedMenuHeight > containerHeight - 8)
        {
            menuY = containerHeight - estimatedMenuHeight - 8;
        }

        // Apply position
        MenuContainer.Margin = new Thickness(menuX, menuY, 0, 0);
    }

    #endregion

    #region Event Handlers

    private static void OnIsOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DropdownNavigationMenu menu)
        {
            if ((bool)e.NewValue)
                menu.Open();
            else
                menu.Close();
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void BackdropOverlay_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        Close();
    }

    private void Root_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (_isOpen && e.Key == VirtualKey.Escape)
        {
            e.Handled = true;
            Close();
        }
    }

    private void MenuItem_Clicked(object sender, DropdownMenuItemClickedEventArgs e)
    {
        // Close menu on item click (unless it has a submenu)
        if (!e.HasSubmenu)
        {
            Close();
        }

        // Raise the navigation event
        ItemClicked?.Invoke(this, e);
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Adds a menu item to the dropdown.
    /// </summary>
    public void AddItem(DropdownMenuItemData item)
    {
        MenuItems.Add(item);
    }

    /// <summary>
    /// Adds a separator to the menu.
    /// </summary>
    public void AddSeparator()
    {
        MenuItems.Add(new DropdownMenuItemData { IsSeparator = true });
    }

    /// <summary>
    /// Adds a section header to the menu.
    /// </summary>
    public void AddHeader(string text)
    {
        MenuItems.Add(new DropdownMenuItemData { Text = text, IsHeader = true });
    }

    /// <summary>
    /// Clears all menu items.
    /// </summary>
    public void ClearItems()
    {
        MenuItems.Clear();
    }

    /// <summary>
    /// Registers the root element for gesture detection.
    /// Call this from the page that hosts this control.
    /// </summary>
    /// <param name="element">The root element to monitor for gestures.</param>
    public void RegisterGestureElement(UIElement element)
    {
        _gestureService.RegisterElement(element);
    }

    #endregion

    #region Events

    /// <summary>
    /// Raised when the menu is opened.
    /// </summary>
    public event EventHandler? Opened;

    /// <summary>
    /// Raised when the menu is closed.
    /// </summary>
    public event EventHandler? Closed;

    /// <summary>
    /// Raised when a menu item is clicked.
    /// </summary>
    public event EventHandler<DropdownMenuItemClickedEventArgs>? ItemClicked;

    #endregion
}

/// <summary>
/// Data model for dropdown menu items.
/// </summary>
public sealed class DropdownMenuItemData
{
    /// <summary>
    /// Icon glyph for the menu item.
    /// </summary>
    public string Icon { get; set; } = string.Empty;

    /// <summary>
    /// Display text for the menu item.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Optional description text shown below the main text.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Keyboard shortcut text to display.
    /// </summary>
    public string Shortcut { get; set; } = string.Empty;

    /// <summary>
    /// Navigation tag used to identify the action.
    /// </summary>
    public string Tag { get; set; } = string.Empty;

    /// <summary>
    /// Whether this item is a separator line.
    /// </summary>
    public bool IsSeparator { get; set; }

    /// <summary>
    /// Whether this item is a section header.
    /// </summary>
    public bool IsHeader { get; set; }

    /// <summary>
    /// Whether the item is enabled and clickable.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Whether this item has a submenu.
    /// </summary>
    public bool HasSubmenu { get; set; }

    /// <summary>
    /// Submenu items if HasSubmenu is true.
    /// </summary>
    public IList<DropdownMenuItemData>? SubmenuItems { get; set; }
}

/// <summary>
/// Event arguments for when a dropdown menu item is clicked.
/// </summary>
public sealed class DropdownMenuItemClickedEventArgs : EventArgs
{
    /// <summary>
    /// The navigation tag of the clicked item.
    /// </summary>
    public string Tag { get; init; } = string.Empty;

    /// <summary>
    /// The text of the clicked item.
    /// </summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>
    /// Whether the clicked item has a submenu.
    /// </summary>
    public bool HasSubmenu { get; init; }
}
