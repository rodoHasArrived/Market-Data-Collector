using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace MarketDataCollector.Uwp.Models;

/// <summary>
/// Display model for candlestick chart elements.
/// </summary>
public sealed class CandlestickViewModel
{
    public SolidColorBrush BodyColor { get; set; } = new(Microsoft.UI.Colors.White);
    public SolidColorBrush WickColor { get; set; } = new(Microsoft.UI.Colors.White);
    public double BodyHeight { get; set; }
    public double WickHeight { get; set; }
    public Thickness BodyMargin { get; set; }
    public Thickness WickMargin { get; set; }
    public string Tooltip { get; set; } = string.Empty;
}

/// <summary>
/// Display model for volume bars.
/// </summary>
public sealed class VolumeBarViewModel
{
    public double Height { get; set; }
    public SolidColorBrush Color { get; set; } = new(Microsoft.UI.Colors.Gray);
    public string Tooltip { get; set; } = string.Empty;
}

/// <summary>
/// Display model for volume profile bars.
/// </summary>
public sealed class VolumeProfileBarViewModel
{
    public string PriceLabel { get; set; } = string.Empty;
    public double BarWidth { get; set; }
    public SolidColorBrush BarColor { get; set; } = new(Microsoft.UI.Colors.Blue);
}

/// <summary>
/// Display model for indicator values.
/// </summary>
public sealed class IndicatorValueViewModel
{
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public SolidColorBrush ValueColor { get; set; } = new(Microsoft.UI.Colors.White);
}

/// <summary>
/// Drawing modes for chart annotations.
/// </summary>
public enum DrawingMode
{
    None,
    Line,
    TrendLine,
    HorizontalLine,
    VerticalLine,
    FibonacciRetracement,
    Rectangle,
    TextAnnotation
}

/// <summary>
/// Represents a drawing annotation on the chart.
/// </summary>
public sealed class ChartDrawing
{
    public DrawingMode Mode { get; set; }
    public Windows.Foundation.Point StartPoint { get; set; }
    public Windows.Foundation.Point EndPoint { get; set; }
    public Windows.UI.Color Color { get; set; } = Microsoft.UI.Colors.Yellow;
    public string? Text { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
