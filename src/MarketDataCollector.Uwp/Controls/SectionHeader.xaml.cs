using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace MarketDataCollector.Uwp.Controls;

/// <summary>
/// Reusable section header control with icon, title, description, badge, and action slot.
/// Provides consistent header styling across all pages.
/// </summary>
public sealed partial class SectionHeader : UserControl
{
    public SectionHeader()
    {
        this.InitializeComponent();
        UpdateIconAppearance();
    }

    #region Dependency Properties

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(SectionHeader),
            new PropertyMetadata(string.Empty));

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public static readonly DependencyProperty DescriptionProperty =
        DependencyProperty.Register(nameof(Description), typeof(string), typeof(SectionHeader),
            new PropertyMetadata(string.Empty));

    public string Description
    {
        get => (string)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public static readonly DependencyProperty IconGlyphProperty =
        DependencyProperty.Register(nameof(IconGlyph), typeof(string), typeof(SectionHeader),
            new PropertyMetadata(string.Empty, OnIconGlyphChanged));

    public string IconGlyph
    {
        get => (string)GetValue(IconGlyphProperty);
        set => SetValue(IconGlyphProperty, value);
    }

    public static readonly DependencyProperty BadgeProperty =
        DependencyProperty.Register(nameof(Badge), typeof(string), typeof(SectionHeader),
            new PropertyMetadata(string.Empty));

    public string Badge
    {
        get => (string)GetValue(BadgeProperty);
        set => SetValue(BadgeProperty, value);
    }

    public static readonly DependencyProperty VariantProperty =
        DependencyProperty.Register(nameof(Variant), typeof(SectionHeaderVariant), typeof(SectionHeader),
            new PropertyMetadata(SectionHeaderVariant.Default, OnVariantChanged));

    public SectionHeaderVariant Variant
    {
        get => (SectionHeaderVariant)GetValue(VariantProperty);
        set => SetValue(VariantProperty, value);
    }

    public static readonly DependencyProperty ActionsProperty =
        DependencyProperty.Register(nameof(Actions), typeof(object), typeof(SectionHeader),
            new PropertyMetadata(null));

    public object? Actions
    {
        get => GetValue(ActionsProperty);
        set => SetValue(ActionsProperty, value);
    }

    public static readonly DependencyProperty TitleFontSizeProperty =
        DependencyProperty.Register(nameof(TitleFontSize), typeof(double), typeof(SectionHeader),
            new PropertyMetadata(18.0));

    public double TitleFontSize
    {
        get => (double)GetValue(TitleFontSizeProperty);
        set => SetValue(TitleFontSizeProperty, value);
    }

    #endregion

    #region Computed Properties

    public Visibility IconVisibility => string.IsNullOrEmpty(IconGlyph) ? Visibility.Collapsed : Visibility.Visible;
    public Visibility DescriptionVisibility => string.IsNullOrEmpty(Description) ? Visibility.Collapsed : Visibility.Visible;
    public Visibility BadgeVisibility => string.IsNullOrEmpty(Badge) ? Visibility.Collapsed : Visibility.Visible;

    #endregion

    #region Property Changed Handlers

    private static void OnIconGlyphChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SectionHeader header)
        {
            header.UpdateIconAppearance();
        }
    }

    private static void OnVariantChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SectionHeader header)
        {
            header.UpdateIconAppearance();
        }
    }

    private void UpdateIconAppearance()
    {
        var (background, foreground) = Variant switch
        {
            SectionHeaderVariant.Success => (GetBrush("#1A3fb950"), GetBrush("#3fb950")),
            SectionHeaderVariant.Warning => (GetBrush("#1Ad29922"), GetBrush("#d29922")),
            SectionHeaderVariant.Danger => (GetBrush("#1Af85149"), GetBrush("#f85149")),
            SectionHeaderVariant.Info => (GetBrush("#1A58a6ff"), GetBrush("#58a6ff")),
            SectionHeaderVariant.Purple => (GetBrush("#1Aa371f7"), GetBrush("#a371f7")),
            SectionHeaderVariant.Cyan => (GetBrush("#1A39c5cf"), GetBrush("#39c5cf")),
            _ => (GetBrush("#1A8b949e"), GetBrush("#8b949e"))
        };

        IconContainer.Background = background;
        HeaderIcon.Foreground = foreground;

        // Update badge colors based on variant
        var (badgeBg, badgeFg) = Variant switch
        {
            SectionHeaderVariant.Success => (GetBrush("#3fb950"), GetBrush("#FFFFFF")),
            SectionHeaderVariant.Warning => (GetBrush("#d29922"), GetBrush("#FFFFFF")),
            SectionHeaderVariant.Danger => (GetBrush("#f85149"), GetBrush("#FFFFFF")),
            SectionHeaderVariant.Info => (GetBrush("#58a6ff"), GetBrush("#FFFFFF")),
            SectionHeaderVariant.Purple => (GetBrush("#a371f7"), GetBrush("#FFFFFF")),
            SectionHeaderVariant.Cyan => (GetBrush("#39c5cf"), GetBrush("#FFFFFF")),
            _ => (GetBrush("#21262d"), GetBrush("#8b949e"))
        };

        BadgeContainer.Background = badgeBg;
        BadgeText.Foreground = badgeFg;
    }

    private static SolidColorBrush GetBrush(string hex)
    {
        var color = ParseHexColor(hex);
        return new SolidColorBrush(color);
    }

    private static Windows.UI.Color ParseHexColor(string hex)
    {
        hex = hex.TrimStart('#');
        return hex.Length switch
        {
            6 => Windows.UI.Color.FromArgb(255,
                byte.Parse(hex[..2], System.Globalization.NumberStyles.HexNumber),
                byte.Parse(hex[2..4], System.Globalization.NumberStyles.HexNumber),
                byte.Parse(hex[4..6], System.Globalization.NumberStyles.HexNumber)),
            8 => Windows.UI.Color.FromArgb(
                byte.Parse(hex[..2], System.Globalization.NumberStyles.HexNumber),
                byte.Parse(hex[2..4], System.Globalization.NumberStyles.HexNumber),
                byte.Parse(hex[4..6], System.Globalization.NumberStyles.HexNumber),
                byte.Parse(hex[6..8], System.Globalization.NumberStyles.HexNumber)),
            _ => Colors.Gray
        };
    }

    #endregion
}

/// <summary>
/// Visual variants for section headers.
/// </summary>
public enum SectionHeaderVariant
{
    Default,
    Success,
    Warning,
    Danger,
    Info,
    Purple,
    Cyan
}
