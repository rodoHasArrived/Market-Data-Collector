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
    #region Cached Brushes (Performance Optimization)

    // Static cached brushes to avoid repeated allocations
    private static readonly SolidColorBrush s_successBrush = new(Color.FromArgb(255, 63, 185, 80));
    private static readonly SolidColorBrush s_dangerBrush = new(Color.FromArgb(255, 248, 81, 73));
    private static readonly SolidColorBrush s_warningBrush = new(Color.FromArgb(255, 210, 153, 34));
    private static readonly SolidColorBrush s_infoBrush = new(Color.FromArgb(255, 88, 166, 255));
    private static readonly SolidColorBrush s_defaultBrush = new(Color.FromArgb(255, 230, 237, 243));

    // Semi-transparent brushes for trend badge backgrounds
    private static readonly SolidColorBrush s_successBgBrush = new(Color.FromArgb(26, 63, 185, 80));
    private static readonly SolidColorBrush s_dangerBgBrush = new(Color.FromArgb(26, 248, 81, 73));
    private static readonly SolidColorBrush s_warningBgBrush = new(Color.FromArgb(26, 210, 153, 34));
    private static readonly SolidColorBrush s_infoBgBrush = new(Color.FromArgb(26, 88, 166, 255));
    private static readonly SolidColorBrush s_defaultBgBrush = new(Color.FromArgb(26, 230, 237, 243));

    #endregion

    // Reusable PointCollection for sparkline updates
    private readonly PointCollection _sparklinePoints = new();

    // Fixed-size circular buffer for sparkline data
    private const int SparklineCapacity = 20;
    private readonly double[] _sparklineBuffer = new double[SparklineCapacity];
    private int _sparklineIndex = 0;
    private int _sparklineCount = 0;
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
        // Use cached brushes based on variant - no allocations
        var (accentBrush, trendBgBrush) = Variant switch
        {
            MetricCardVariant.Success => (s_successBrush, s_successBgBrush),
            MetricCardVariant.Danger => (s_dangerBrush, s_dangerBgBrush),
            MetricCardVariant.Warning => (s_warningBrush, s_warningBgBrush),
            MetricCardVariant.Info => (s_infoBrush, s_infoBgBrush),
            _ => (s_defaultBrush, s_defaultBgBrush)
        };

        // Apply bottom border accent
        CardBorder.BorderBrush = accentBrush;

        // Apply value color
        ValueText.Foreground = accentBrush;

        // Apply trend badge styling
        TrendBadge.Background = trendBgBrush;
        TrendText.Foreground = accentBrush;

        // Apply sparkline color
        SparklinePath.Stroke = accentBrush;
    }

    private SolidColorBrush GetAccentBrush()
    {
        // Return cached brushes - no allocations
        return Variant switch
        {
            MetricCardVariant.Success => s_successBrush,
            MetricCardVariant.Danger => s_dangerBrush,
            MetricCardVariant.Warning => s_warningBrush,
            MetricCardVariant.Info => s_infoBrush,
            _ => s_defaultBrush
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

        // Clear and reuse PointCollection instead of allocating new one
        _sparklinePoints.Clear();

        for (var i = 0; i < data.Count; i++)
        {
            var x = padding + (i * stepX);
            var normalizedY = (data[i] - minValue) / range;
            var y = canvasHeight - padding - (normalizedY * availableHeight);
            _sparklinePoints.Add(new Point(x, y));
        }

        SparklinePath.Points = _sparklinePoints;
    }

    /// <summary>
    /// Updates the sparkline with new data points using O(1) circular buffer.
    /// Call this to animate new data without allocations.
    /// </summary>
    public void AddSparklinePoint(double value)
    {
        // Use internal circular buffer for O(1) operations
        _sparklineBuffer[_sparklineIndex] = value;
        _sparklineIndex = (_sparklineIndex + 1) % SparklineCapacity;
        if (_sparklineCount < SparklineCapacity) _sparklineCount++;

        // Also update the SparklineData property if it exists (for binding compatibility)
        if (SparklineData == null)
        {
            SparklineData = new List<double>();
        }

        // Only rebuild the list when necessary (for external consumers)
        // This is still needed for data binding but avoids the RemoveAt(0) O(n) operation
        if (_sparklineCount == SparklineCapacity)
        {
            // Efficient rebuild: copy from circular buffer in order
            var newData = new List<double>(_sparklineCount);
            for (int i = 0; i < _sparklineCount; i++)
            {
                var idx = (_sparklineIndex - _sparklineCount + i + SparklineCapacity) % SparklineCapacity;
                newData.Add(_sparklineBuffer[idx]);
            }
            SparklineData = newData;
        }
        else
        {
            // Still filling up the buffer
            ((List<double>)SparklineData).Add(value);
        }
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
