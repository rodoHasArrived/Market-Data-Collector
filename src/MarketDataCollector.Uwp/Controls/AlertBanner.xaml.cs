using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;

namespace MarketDataCollector.Uwp.Controls;

/// <summary>
/// Reusable alert banner control for inline notifications and alerts.
/// Supports multiple severity levels, dismissibility, and optional action buttons.
/// </summary>
public sealed partial class AlertBanner : UserControl
{
    public AlertBanner()
    {
        this.InitializeComponent();
        UpdateSeverityAppearance();
    }

    #region Events

    /// <summary>
    /// Raised when the action button is clicked.
    /// </summary>
    public event EventHandler? ActionClicked;

    /// <summary>
    /// Raised when the alert is dismissed.
    /// </summary>
    public event EventHandler? Dismissed;

    #endregion

    #region Dependency Properties

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(AlertBanner),
            new PropertyMetadata(string.Empty));

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public static readonly DependencyProperty MessageProperty =
        DependencyProperty.Register(nameof(Message), typeof(string), typeof(AlertBanner),
            new PropertyMetadata(string.Empty));

    public string Message
    {
        get => (string)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public static readonly DependencyProperty DetailsProperty =
        DependencyProperty.Register(nameof(Details), typeof(string), typeof(AlertBanner),
            new PropertyMetadata(string.Empty));

    public string Details
    {
        get => (string)GetValue(DetailsProperty);
        set => SetValue(DetailsProperty, value);
    }

    public static readonly DependencyProperty SeverityProperty =
        DependencyProperty.Register(nameof(Severity), typeof(AlertSeverity), typeof(AlertBanner),
            new PropertyMetadata(AlertSeverity.Info, OnSeverityChanged));

    public AlertSeverity Severity
    {
        get => (AlertSeverity)GetValue(SeverityProperty);
        set => SetValue(SeverityProperty, value);
    }

    public static readonly DependencyProperty ActionTextProperty =
        DependencyProperty.Register(nameof(ActionText), typeof(string), typeof(AlertBanner),
            new PropertyMetadata(string.Empty));

    public string ActionText
    {
        get => (string)GetValue(ActionTextProperty);
        set => SetValue(ActionTextProperty, value);
    }

    public static readonly DependencyProperty IsDismissibleProperty =
        DependencyProperty.Register(nameof(IsDismissible), typeof(bool), typeof(AlertBanner),
            new PropertyMetadata(true));

    public bool IsDismissible
    {
        get => (bool)GetValue(IsDismissibleProperty);
        set => SetValue(IsDismissibleProperty, value);
    }

    public static readonly DependencyProperty IconGlyphProperty =
        DependencyProperty.Register(nameof(IconGlyph), typeof(string), typeof(AlertBanner),
            new PropertyMetadata(string.Empty, OnIconGlyphChanged));

    public string IconGlyph
    {
        get => (string)GetValue(IconGlyphProperty);
        set => SetValue(IconGlyphProperty, value);
    }

    #endregion

    #region Computed Properties

    public Visibility TitleVisibility => string.IsNullOrEmpty(Title) ? Visibility.Collapsed : Visibility.Visible;
    public Visibility MessageVisibility => string.IsNullOrEmpty(Message) ? Visibility.Collapsed : Visibility.Visible;
    public Visibility DetailsVisibility => string.IsNullOrEmpty(Details) ? Visibility.Collapsed : Visibility.Visible;
    public Visibility ActionVisibility => string.IsNullOrEmpty(ActionText) ? Visibility.Collapsed : Visibility.Visible;
    public Visibility DismissVisibility => IsDismissible ? Visibility.Visible : Visibility.Collapsed;

    #endregion

    #region Event Handlers

    private void ActionButton_Click(object sender, RoutedEventArgs e)
    {
        ActionClicked?.Invoke(this, EventArgs.Empty);
    }

    private void DismissButton_Click(object sender, RoutedEventArgs e)
    {
        Visibility = Visibility.Collapsed;
        Dismissed?.Invoke(this, EventArgs.Empty);
    }

    #endregion

    #region Property Changed Handlers

    private static void OnSeverityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AlertBanner banner)
        {
            banner.UpdateSeverityAppearance();
        }
    }

    private static void OnIconGlyphChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AlertBanner banner)
        {
            banner.UpdateSeverityAppearance();
        }
    }

    private void UpdateSeverityAppearance()
    {
        var (background, border, iconBg, foreground, icon) = Severity switch
        {
            AlertSeverity.Success => (
                GetBrush("#0D3fb950"),
                GetBrush("#333fb950"),
                GetBrush("#1A3fb950"),
                GetBrush("#3fb950"),
                "\uE73E" // Checkmark
            ),
            AlertSeverity.Warning => (
                GetBrush("#0Dd29922"),
                GetBrush("#33d29922"),
                GetBrush("#1Ad29922"),
                GetBrush("#d29922"),
                "\uE7BA" // Warning
            ),
            AlertSeverity.Error => (
                GetBrush("#0Df85149"),
                GetBrush("#33f85149"),
                GetBrush("#1Af85149"),
                GetBrush("#f85149"),
                "\uE783" // Error
            ),
            _ => (
                GetBrush("#0D58a6ff"),
                GetBrush("#3358a6ff"),
                GetBrush("#1A58a6ff"),
                GetBrush("#58a6ff"),
                "\uE946" // Info
            )
        };

        AlertBorder.Background = background;
        AlertBorder.BorderBrush = border;
        IconContainer.Background = iconBg;
        SeverityIcon.Foreground = foreground;
        TitleText.Foreground = foreground;

        // Use custom icon if provided, otherwise use severity default
        SeverityIcon.Glyph = string.IsNullOrEmpty(IconGlyph) ? icon : IconGlyph;

        // Style action button based on severity
        ActionButton.Background = iconBg;
        ActionButton.Foreground = foreground;
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

    #region Public Methods

    /// <summary>
    /// Shows the alert banner.
    /// </summary>
    public void Show()
    {
        Visibility = Visibility.Visible;
    }

    /// <summary>
    /// Hides the alert banner.
    /// </summary>
    public void Hide()
    {
        Visibility = Visibility.Collapsed;
    }

    #endregion
}

/// <summary>
/// Alert severity levels.
/// </summary>
public enum AlertSeverity
{
    Info,
    Success,
    Warning,
    Error
}
