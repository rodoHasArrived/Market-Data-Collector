using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;
using Windows.Foundation;
using Windows.UI;

namespace MarketDataCollector.Uwp.Controls;

/// <summary>
/// A reusable metric card component for displaying key performance indicators
/// with optional sparkline visualization and trend indicators.
/// </summary>
public sealed partial class MetricCard : UserControl
{
    #region Dependency Properties

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(string), typeof(MetricCard),
            new PropertyMetadata("0", OnValueChanged));

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(MetricCard),
            new PropertyMetadata("METRIC"));

    public static readonly DependencyProperty DescriptionProperty =
        DependencyProperty.Register(nameof(Description), typeof(string), typeof(MetricCard),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty TrendValueProperty =
        DependencyProperty.Register(nameof(TrendValue), typeof(string), typeof(MetricCard),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty VariantProperty =
        DependencyProperty.Register(nameof(Variant), typeof(MetricCardVariant), typeof(MetricCard),
            new PropertyMetadata(MetricCardVariant.Default, OnVariantChanged));

    public static readonly DependencyProperty IconGlyphProperty =
        DependencyProperty.Register(nameof(IconGlyph), typeof(string), typeof(MetricCard),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ShowSparklineProperty =
        DependencyProperty.Register(nameof(ShowSparkline), typeof(bool), typeof(MetricCard),
            new PropertyMetadata(false));

    public static readonly DependencyProperty SparklineDataProperty =
        DependencyProperty.Register(nameof(SparklineData), typeof(IList<double>), typeof(MetricCard),
            new PropertyMetadata(null, OnSparklineDataChanged));

    public static readonly DependencyProperty ValueFontSizeProperty =
        DependencyProperty.Register(nameof(ValueFontSize), typeof(double), typeof(MetricCard),
            new PropertyMetadata(32.0));

    #endregion

    #region Properties

    public string Value
    {
        get => (string)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Description
    {
        get => (string)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public string TrendValue
    {
        get => (string)GetValue(TrendValueProperty);
        set => SetValue(TrendValueProperty, value);
    }

    public MetricCardVariant Variant
    {
        get => (MetricCardVariant)GetValue(VariantProperty);
        set => SetValue(VariantProperty, value);
    }

    public string IconGlyph
    {
        get => (string)GetValue(IconGlyphProperty);
        set => SetValue(IconGlyphProperty, value);
    }

    public bool ShowSparkline
    {
        get => (bool)GetValue(ShowSparklineProperty);
        set => SetValue(ShowSparklineProperty, value);
    }

    public IList<double> SparklineData
    {
        get => (IList<double>)GetValue(SparklineDataProperty);
        set => SetValue(SparklineDataProperty, value);
    }

    public double ValueFontSize
    {
        get => (double)GetValue(ValueFontSizeProperty);
        set => SetValue(ValueFontSizeProperty, value);
    }

    // Computed properties for UI bindings
    public SolidColorBrush AccentBrush => GetAccentBrush();
    public Visibility TrendVisibility => string.IsNullOrEmpty(TrendValue) ? Visibility.Collapsed : Visibility.Visible;
    public Visibility IconVisibility => string.IsNullOrEmpty(IconGlyph) ? Visibility.Collapsed : Visibility.Visible;
    public Visibility DescriptionVisibility => string.IsNullOrEmpty(Description) ? Visibility.Collapsed : Visibility.Visible;
    public Visibility SparklineVisibility => ShowSparkline ? Visibility.Visible : Visibility.Collapsed;

    #endregion

    public MetricCard()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        SizeChanged += OnSizeChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ApplyVariantStyle();
        UpdateSparkline();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateSparkline();
    }

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        // Value changed - could trigger animation here
    }

    private static void OnVariantChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MetricCard card)
        {
            card.ApplyVariantStyle();
        }
    }

    private static void OnSparklineDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MetricCard card)
        {
            card.UpdateSparkline();
        }
    }

    private void ApplyVariantStyle()
    {
        var (accentColor, trendBackground) = Variant switch
        {
            MetricCardVariant.Success => (Color.FromArgb(255, 63, 185, 80), Color.FromArgb(26, 63, 185, 80)),
            MetricCardVariant.Danger => (Color.FromArgb(255, 248, 81, 73), Color.FromArgb(26, 248, 81, 73)),
            MetricCardVariant.Warning => (Color.FromArgb(255, 210, 153, 34), Color.FromArgb(26, 210, 153, 34)),
            MetricCardVariant.Info => (Color.FromArgb(255, 88, 166, 255), Color.FromArgb(26, 88, 166, 255)),
            _ => (Color.FromArgb(255, 230, 237, 243), Color.FromArgb(26, 230, 237, 243))
        };

        var accentBrush = new SolidColorBrush(accentColor);

        // Apply bottom border accent
        CardBorder.BorderBrush = accentBrush;

        // Apply value color
        ValueText.Foreground = accentBrush;

        // Apply trend badge styling
        TrendBadge.Background = new SolidColorBrush(trendBackground);
        TrendText.Foreground = accentBrush;

        // Apply sparkline color
        SparklinePath.Stroke = accentBrush;
    }

    private SolidColorBrush GetAccentBrush()
    {
        return Variant switch
        {
            MetricCardVariant.Success => new SolidColorBrush(Color.FromArgb(255, 63, 185, 80)),
            MetricCardVariant.Danger => new SolidColorBrush(Color.FromArgb(255, 248, 81, 73)),
            MetricCardVariant.Warning => new SolidColorBrush(Color.FromArgb(255, 210, 153, 34)),
            MetricCardVariant.Info => new SolidColorBrush(Color.FromArgb(255, 88, 166, 255)),
            _ => new SolidColorBrush(Color.FromArgb(255, 230, 237, 243))
        };
    }

    private void UpdateSparkline()
    {
        if (!ShowSparkline || SparklineData == null || SparklineData.Count < 2)
        {
            return;
        }

        var canvasWidth = SparklineCanvas.ActualWidth;
        var canvasHeight = SparklineCanvas.ActualHeight;

        if (canvasWidth <= 0 || canvasHeight <= 0)
        {
            return;
        }

        var data = SparklineData;
        var minValue = double.MaxValue;
        var maxValue = double.MinValue;

        foreach (var value in data)
        {
            if (value < minValue) minValue = value;
            if (value > maxValue) maxValue = value;
        }

        var range = maxValue - minValue;
        if (range == 0) range = 1; // Avoid division by zero

        var padding = 2.0;
        var availableWidth = canvasWidth - (padding * 2);
        var availableHeight = canvasHeight - (padding * 2);
        var stepX = availableWidth / (data.Count - 1);

        var points = new PointCollection();

        for (var i = 0; i < data.Count; i++)
        {
            var x = padding + (i * stepX);
            var normalizedY = (data[i] - minValue) / range;
            var y = canvasHeight - padding - (normalizedY * availableHeight);
            points.Add(new Point(x, y));
        }

        SparklinePath.Points = points;
    }

    /// <summary>
    /// Updates the sparkline with new data points. Call this to animate new data.
    /// </summary>
    public void AddSparklinePoint(double value)
    {
        if (SparklineData == null)
        {
            SparklineData = new List<double>();
        }

        var data = new List<double>(SparklineData) { value };

        // Keep only the last 20 points for performance
        while (data.Count > 20)
        {
            data.RemoveAt(0);
        }

        SparklineData = data;
    }
}

/// <summary>
/// Visual variant for the MetricCard component.
/// </summary>
public enum MetricCardVariant
{
    Default,
    Success,
    Danger,
    Warning,
    Info
}
