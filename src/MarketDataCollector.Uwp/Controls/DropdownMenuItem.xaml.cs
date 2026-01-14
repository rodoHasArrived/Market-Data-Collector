using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.UI;

namespace MarketDataCollector.Uwp.Controls;

/// <summary>
/// Individual menu item component for the dropdown navigation menu.
/// Supports icons, descriptions, keyboard shortcuts, and submenus.
/// </summary>
public sealed partial class DropdownMenuItem : UserControl
{
    private static readonly SolidColorBrush s_hoverBrush = new(Color.FromArgb(24, 88, 166, 255));
    private static readonly SolidColorBrush s_pressedBrush = new(Color.FromArgb(40, 88, 166, 255));
    private static readonly SolidColorBrush s_transparentBrush = new(Colors.Transparent);

    private bool _isPressed;
    private Storyboard? _hoverStoryboard;
    private Storyboard? _unhoverStoryboard;

    #region Dependency Properties

    public static readonly DependencyProperty IconProperty =
        DependencyProperty.Register(nameof(Icon), typeof(string), typeof(DropdownMenuItem),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(nameof(Text), typeof(string), typeof(DropdownMenuItem),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty DescriptionProperty =
        DependencyProperty.Register(nameof(Description), typeof(string), typeof(DropdownMenuItem),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ShortcutProperty =
        DependencyProperty.Register(nameof(Shortcut), typeof(string), typeof(DropdownMenuItem),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty IsSeparatorProperty =
        DependencyProperty.Register(nameof(IsSeparator), typeof(bool), typeof(DropdownMenuItem),
            new PropertyMetadata(false, OnTypeChanged));

    public static readonly DependencyProperty IsHeaderProperty =
        DependencyProperty.Register(nameof(IsHeader), typeof(bool), typeof(DropdownMenuItem),
            new PropertyMetadata(false, OnTypeChanged));

    public static readonly DependencyProperty HasSubmenuProperty =
        DependencyProperty.Register(nameof(HasSubmenu), typeof(bool), typeof(DropdownMenuItem),
            new PropertyMetadata(false));

    #endregion

    #region Properties

    /// <summary>
    /// Gets or sets the icon glyph.
    /// </summary>
    public string Icon
    {
        get => (string)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <summary>
    /// Gets or sets the menu item text.
    /// </summary>
    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>
    /// Gets or sets the description text.
    /// </summary>
    public string Description
    {
        get => (string)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    /// <summary>
    /// Gets or sets the keyboard shortcut text.
    /// </summary>
    public string Shortcut
    {
        get => (string)GetValue(ShortcutProperty);
        set => SetValue(ShortcutProperty, value);
    }

    /// <summary>
    /// Gets or sets whether this is a separator.
    /// </summary>
    public bool IsSeparator
    {
        get => (bool)GetValue(IsSeparatorProperty);
        set => SetValue(IsSeparatorProperty, value);
    }

    /// <summary>
    /// Gets or sets whether this is a section header.
    /// </summary>
    public bool IsHeader
    {
        get => (bool)GetValue(IsHeaderProperty);
        set => SetValue(IsHeaderProperty, value);
    }

    /// <summary>
    /// Gets or sets whether this item has a submenu.
    /// </summary>
    public bool HasSubmenu
    {
        get => (bool)GetValue(HasSubmenuProperty);
        set => SetValue(HasSubmenuProperty, value);
    }

    // Computed visibility properties
    public Visibility IconVisibility => string.IsNullOrEmpty(Icon) ? Visibility.Collapsed : Visibility.Visible;
    public Visibility DescriptionVisibility => string.IsNullOrEmpty(Description) ? Visibility.Collapsed : Visibility.Visible;
    public Visibility ShortcutVisibility => string.IsNullOrEmpty(Shortcut) ? Visibility.Collapsed : Visibility.Visible;
    public Visibility SubmenuVisibility => HasSubmenu ? Visibility.Visible : Visibility.Collapsed;

    #endregion

    public DropdownMenuItem()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateVisualState();
        CreateAnimations();
    }

    private static void OnTypeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DropdownMenuItem item)
        {
            item.UpdateVisualState();
        }
    }

    private void UpdateVisualState()
    {
        // Hide all states
        SeparatorBorder.Visibility = Visibility.Collapsed;
        HeaderBorder.Visibility = Visibility.Collapsed;
        ItemBorder.Visibility = Visibility.Collapsed;
        DisabledOverlay.Visibility = Visibility.Collapsed;

        if (IsSeparator)
        {
            SeparatorBorder.Visibility = Visibility.Visible;
        }
        else if (IsHeader)
        {
            HeaderBorder.Visibility = Visibility.Visible;
        }
        else
        {
            ItemBorder.Visibility = Visibility.Visible;

            if (!IsEnabled)
            {
                DisabledOverlay.Visibility = Visibility.Visible;
            }
        }
    }

    private void CreateAnimations()
    {
        // Hover animation - slide item slightly right
        _hoverStoryboard = new Storyboard();
        var hoverTranslate = new DoubleAnimation
        {
            To = 4,
            Duration = new Duration(TimeSpan.FromMilliseconds(100)),
            EasingFunction = new CubicBezierEasingFunction
            {
                ControlPoint1 = new Windows.Foundation.Point(0.4, 0),
                ControlPoint2 = new Windows.Foundation.Point(0.6, 1)
            }
        };
        Storyboard.SetTarget(hoverTranslate, ItemTranslateTransform);
        Storyboard.SetTargetProperty(hoverTranslate, "X");
        _hoverStoryboard.Children.Add(hoverTranslate);

        // Also animate the submenu arrow
        if (HasSubmenu)
        {
            var arrowBounce = new DoubleAnimation
            {
                To = 3,
                Duration = new Duration(TimeSpan.FromMilliseconds(100)),
                EasingFunction = new CubicBezierEasingFunction
                {
                    ControlPoint1 = new Windows.Foundation.Point(0.4, 0),
                    ControlPoint2 = new Windows.Foundation.Point(0.6, 1)
                }
            };
            Storyboard.SetTarget(arrowBounce, ArrowTranslateTransform);
            Storyboard.SetTargetProperty(arrowBounce, "X");
            _hoverStoryboard.Children.Add(arrowBounce);
        }

        // Unhover animation - slide back
        _unhoverStoryboard = new Storyboard();
        var unhoverTranslate = new DoubleAnimation
        {
            To = 0,
            Duration = new Duration(TimeSpan.FromMilliseconds(100)),
            EasingFunction = new CubicBezierEasingFunction
            {
                ControlPoint1 = new Windows.Foundation.Point(0.4, 0),
                ControlPoint2 = new Windows.Foundation.Point(0.6, 1)
            }
        };
        Storyboard.SetTarget(unhoverTranslate, ItemTranslateTransform);
        Storyboard.SetTargetProperty(unhoverTranslate, "X");
        _unhoverStoryboard.Children.Add(unhoverTranslate);

        if (HasSubmenu)
        {
            var arrowReturn = new DoubleAnimation
            {
                To = 0,
                Duration = new Duration(TimeSpan.FromMilliseconds(100))
            };
            Storyboard.SetTarget(arrowReturn, ArrowTranslateTransform);
            Storyboard.SetTargetProperty(arrowReturn, "X");
            _unhoverStoryboard.Children.Add(arrowReturn);
        }
    }

    #region Event Handlers

    private void RootControl_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (IsSeparator || IsHeader || !IsEnabled)
            return;

        ItemBorder.Background = s_hoverBrush;
        _hoverStoryboard?.Begin();
    }

    private void RootControl_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (IsSeparator || IsHeader)
            return;

        ItemBorder.Background = s_transparentBrush;
        _isPressed = false;
        _unhoverStoryboard?.Begin();
    }

    private void RootControl_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (IsSeparator || IsHeader || !IsEnabled)
            return;

        _isPressed = true;
        ItemBorder.Background = s_pressedBrush;
    }

    private void RootControl_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (IsSeparator || IsHeader || !IsEnabled)
            return;

        if (_isPressed)
        {
            ItemBorder.Background = s_hoverBrush;
        }
        _isPressed = false;
    }

    private void RootControl_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (IsSeparator || IsHeader || !IsEnabled)
            return;

        // Raise the clicked event
        ItemClicked?.Invoke(this, new DropdownMenuItemClickedEventArgs
        {
            Tag = Tag?.ToString() ?? string.Empty,
            Text = Text,
            HasSubmenu = HasSubmenu
        });
    }

    #endregion

    #region Events

    /// <summary>
    /// Raised when the menu item is clicked.
    /// </summary>
    public event EventHandler<DropdownMenuItemClickedEventArgs>? ItemClicked;

    #endregion
}
