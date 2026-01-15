using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.UI;

namespace MarketDataCollector.Uwp.Controls;

/// <summary>
/// A reusable status badge component for displaying stream status, connection state,
/// and other status indicators with optional pulse animation.
/// </summary>
public sealed partial class StatusBadge : UserControl
{
    private Storyboard? _pulseStoryboard;

    #region Dependency Properties

    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(nameof(Text), typeof(string), typeof(StatusBadge),
            new PropertyMetadata("Status"));

    public static readonly DependencyProperty StatusProperty =
        DependencyProperty.Register(nameof(Status), typeof(BadgeStatus), typeof(StatusBadge),
            new PropertyMetadata(BadgeStatus.Default, OnStatusChanged));

    public static readonly DependencyProperty IconGlyphProperty =
        DependencyProperty.Register(nameof(IconGlyph), typeof(string), typeof(StatusBadge),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty CountProperty =
        DependencyProperty.Register(nameof(Count), typeof(int?), typeof(StatusBadge),
            new PropertyMetadata(null));

    public static readonly DependencyProperty ShowPulseProperty =
        DependencyProperty.Register(nameof(ShowPulse), typeof(bool), typeof(StatusBadge),
            new PropertyMetadata(false, OnShowPulseChanged));

    public static readonly DependencyProperty SizeProperty =
        DependencyProperty.Register(nameof(Size), typeof(BadgeSize), typeof(StatusBadge),
            new PropertyMetadata(BadgeSize.Medium, OnSizeChanged));

    #endregion

    #region Properties

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public BadgeStatus Status
    {
        get => (BadgeStatus)GetValue(StatusProperty);
        set => SetValue(StatusProperty, value);
    }

    public string IconGlyph
    {
        get => (string)GetValue(IconGlyphProperty);
        set => SetValue(IconGlyphProperty, value);
    }

    public int? Count
    {
        get => (int?)GetValue(CountProperty);
        set => SetValue(CountProperty, value);
    }

    public bool ShowPulse
    {
        get => (bool)GetValue(ShowPulseProperty);
        set => SetValue(ShowPulseProperty, value);
    }

    public BadgeSize Size
    {
        get => (BadgeSize)GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    // Computed properties for UI bindings
    public Visibility IconVisibility => string.IsNullOrEmpty(IconGlyph) ? Visibility.Collapsed : Visibility.Visible;
    public Visibility CountVisibility => Count.HasValue ? Visibility.Visible : Visibility.Collapsed;
    public Visibility PulseVisibility => ShowPulse ? Visibility.Visible : Visibility.Collapsed;
    public string CountDisplay => Count.HasValue ? $"({Count})" : string.Empty;

    #endregion

    public StatusBadge()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ApplyStatusStyle();
        ApplySizeStyle();
        if (ShowPulse)
        {
            StartPulseAnimation();
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        StopPulseAnimation();
    }

    private static void OnStatusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is StatusBadge badge)
        {
            badge.ApplyStatusStyle();
        }
    }

    private static void OnShowPulseChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is StatusBadge badge)
        {
            if ((bool)e.NewValue)
            {
                badge.StartPulseAnimation();
            }
            else
            {
                badge.StopPulseAnimation();
            }
        }
    }

    private static void OnSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is StatusBadge badge)
        {
            badge.ApplySizeStyle();
        }
    }

    private void ApplyStatusStyle()
    {
        var backgroundColor = Status switch
        {
            BadgeStatus.Active => Color.FromArgb(255, 63, 185, 80),      // Green
            BadgeStatus.Success => Color.FromArgb(255, 63, 185, 80),     // Green
            BadgeStatus.Warning => Color.FromArgb(255, 210, 153, 34),    // Orange
            BadgeStatus.Error => Color.FromArgb(255, 248, 81, 73),       // Red
            BadgeStatus.Info => Color.FromArgb(255, 88, 166, 255),       // Blue
            BadgeStatus.Inactive => Color.FromArgb(255, 110, 118, 129),  // Gray
            BadgeStatus.Pending => Color.FromArgb(255, 163, 113, 247),   // Purple
            _ => Color.FromArgb(255, 110, 118, 129)                      // Default gray
        };

        BadgeBorder.Background = new SolidColorBrush(backgroundColor);
    }

    private void ApplySizeStyle()
    {
        var (padding, fontSize, iconSize) = Size switch
        {
            BadgeSize.Small => (new Thickness(6, 2, 6, 2), 10.0, 10.0),
            BadgeSize.Large => (new Thickness(12, 6, 12, 6), 14.0, 16.0),
            _ => (new Thickness(8, 4, 8, 4), 12.0, 12.0)  // Medium
        };

        BadgeBorder.Padding = padding;
        BadgeText.FontSize = fontSize;
        CountText.FontSize = fontSize - 1;
        BadgeIcon.FontSize = iconSize;
    }

    private void StartPulseAnimation()
    {
        if (_pulseStoryboard != null)
        {
            return;
        }

        _pulseStoryboard = new Storyboard
        {
            RepeatBehavior = RepeatBehavior.Forever
        };

        // Scale animation
        var scaleXAnimation = new DoubleAnimation
        {
            From = 1.0,
            To = 1.5,
            Duration = new Duration(System.TimeSpan.FromMilliseconds(1000)),
            AutoReverse = true,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };

        var scaleYAnimation = new DoubleAnimation
        {
            From = 1.0,
            To = 1.5,
            Duration = new Duration(System.TimeSpan.FromMilliseconds(1000)),
            AutoReverse = true,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };

        // Opacity animation
        var opacityAnimation = new DoubleAnimation
        {
            From = 0.4,
            To = 0.0,
            Duration = new Duration(System.TimeSpan.FromMilliseconds(1000)),
            AutoReverse = true,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };

        Storyboard.SetTarget(scaleXAnimation, PulseScale);
        Storyboard.SetTargetProperty(scaleXAnimation, "ScaleX");

        Storyboard.SetTarget(scaleYAnimation, PulseScale);
        Storyboard.SetTargetProperty(scaleYAnimation, "ScaleY");

        Storyboard.SetTarget(opacityAnimation, PulseRing);
        Storyboard.SetTargetProperty(opacityAnimation, "Opacity");

        _pulseStoryboard.Children.Add(scaleXAnimation);
        _pulseStoryboard.Children.Add(scaleYAnimation);
        _pulseStoryboard.Children.Add(opacityAnimation);

        _pulseStoryboard.Begin();
    }

    private void StopPulseAnimation()
    {
        _pulseStoryboard?.Stop();
        _pulseStoryboard = null;
    }
}

/// <summary>
/// Status variant for the StatusBadge component.
/// </summary>
public enum BadgeStatus
{
    Default,
    Active,
    Success,
    Warning,
    Error,
    Info,
    Inactive,
    Pending
}

/// <summary>
/// Size variant for the StatusBadge component.
/// </summary>
public enum BadgeSize
{
    Small,
    Medium,
    Large
}
