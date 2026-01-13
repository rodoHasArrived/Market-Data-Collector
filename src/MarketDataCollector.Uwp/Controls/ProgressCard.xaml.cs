using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace MarketDataCollector.Uwp.Controls;

/// <summary>
/// Reusable progress card control for showing operation progress with stats.
/// Ideal for backfill operations, data imports, and long-running tasks.
/// </summary>
public sealed partial class ProgressCard : UserControl
{
    public ProgressCard()
    {
        this.InitializeComponent();
        UpdateStatusAppearance();
    }

    #region Dependency Properties

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(ProgressCard),
            new PropertyMetadata("Progress"));

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public static readonly DependencyProperty IconGlyphProperty =
        DependencyProperty.Register(nameof(IconGlyph), typeof(string), typeof(ProgressCard),
            new PropertyMetadata(string.Empty));

    public string IconGlyph
    {
        get => (string)GetValue(IconGlyphProperty);
        set => SetValue(IconGlyphProperty, value);
    }

    public static readonly DependencyProperty ProgressProperty =
        DependencyProperty.Register(nameof(Progress), typeof(double), typeof(ProgressCard),
            new PropertyMetadata(0.0, OnProgressChanged));

    public double Progress
    {
        get => (double)GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    public static readonly DependencyProperty MaximumProperty =
        DependencyProperty.Register(nameof(Maximum), typeof(double), typeof(ProgressCard),
            new PropertyMetadata(100.0));

    public double Maximum
    {
        get => (double)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public static readonly DependencyProperty IsIndeterminateProperty =
        DependencyProperty.Register(nameof(IsIndeterminate), typeof(bool), typeof(ProgressCard),
            new PropertyMetadata(false));

    public bool IsIndeterminate
    {
        get => (bool)GetValue(IsIndeterminateProperty);
        set => SetValue(IsIndeterminateProperty, value);
    }

    public static readonly DependencyProperty StatusProperty =
        DependencyProperty.Register(nameof(Status), typeof(ProgressStatus), typeof(ProgressCard),
            new PropertyMetadata(ProgressStatus.Idle, OnStatusChanged));

    public ProgressStatus Status
    {
        get => (ProgressStatus)GetValue(StatusProperty);
        set => SetValue(StatusProperty, value);
    }

    public static readonly DependencyProperty StatusLabelProperty =
        DependencyProperty.Register(nameof(StatusLabel), typeof(string), typeof(ProgressCard),
            new PropertyMetadata("Idle"));

    public string StatusLabel
    {
        get => (string)GetValue(StatusLabelProperty);
        set => SetValue(StatusLabelProperty, value);
    }

    public static readonly DependencyProperty ProgressTextProperty =
        DependencyProperty.Register(nameof(ProgressText), typeof(string), typeof(ProgressCard),
            new PropertyMetadata("0 / 0 items"));

    public string ProgressText
    {
        get => (string)GetValue(ProgressTextProperty);
        set => SetValue(ProgressTextProperty, value);
    }

    public static readonly DependencyProperty ElapsedTimeProperty =
        DependencyProperty.Register(nameof(ElapsedTime), typeof(string), typeof(ProgressCard),
            new PropertyMetadata("--:--"));

    public string ElapsedTime
    {
        get => (string)GetValue(ElapsedTimeProperty);
        set => SetValue(ElapsedTimeProperty, value);
    }

    public static readonly DependencyProperty RateTextProperty =
        DependencyProperty.Register(nameof(RateText), typeof(string), typeof(ProgressCard),
            new PropertyMetadata("-- /s"));

    public string RateText
    {
        get => (string)GetValue(RateTextProperty);
        set => SetValue(RateTextProperty, value);
    }

    public static readonly DependencyProperty RemainingTimeProperty =
        DependencyProperty.Register(nameof(RemainingTime), typeof(string), typeof(ProgressCard),
            new PropertyMetadata("--:--"));

    public string RemainingTime
    {
        get => (string)GetValue(RemainingTimeProperty);
        set => SetValue(RemainingTimeProperty, value);
    }

    public static readonly DependencyProperty DetailsProperty =
        DependencyProperty.Register(nameof(Details), typeof(string), typeof(ProgressCard),
            new PropertyMetadata(string.Empty));

    public string Details
    {
        get => (string)GetValue(DetailsProperty);
        set => SetValue(DetailsProperty, value);
    }

    public static readonly DependencyProperty ShowStatsProperty =
        DependencyProperty.Register(nameof(ShowStats), typeof(bool), typeof(ProgressCard),
            new PropertyMetadata(true));

    public bool ShowStats
    {
        get => (bool)GetValue(ShowStatsProperty);
        set => SetValue(ShowStatsProperty, value);
    }

    #endregion

    #region Computed Properties

    public Visibility IconVisibility => string.IsNullOrEmpty(IconGlyph) ? Visibility.Collapsed : Visibility.Visible;
    public Visibility DetailsVisibility => string.IsNullOrEmpty(Details) ? Visibility.Collapsed : Visibility.Visible;
    public Visibility StatsVisibility => ShowStats ? Visibility.Visible : Visibility.Collapsed;
    public Visibility SpinnerVisibility => Status == ProgressStatus.Running ? Visibility.Visible : Visibility.Collapsed;

    public string PercentageText
    {
        get
        {
            if (IsIndeterminate) return "...";
            var percent = Maximum > 0 ? (Progress / Maximum) * 100 : 0;
            return $"{percent:F1}%";
        }
    }

    #endregion

    #region Property Changed Handlers

    private static void OnProgressChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ProgressCard card)
        {
            card.Bindings.Update();
        }
    }

    private static void OnStatusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ProgressCard card)
        {
            card.UpdateStatusAppearance();
        }
    }

    private void UpdateStatusAppearance()
    {
        var (background, foreground, progressForeground) = Status switch
        {
            ProgressStatus.Running => (GetBrush("#1A58a6ff"), GetBrush("#58a6ff"), GetBrush("#58a6ff")),
            ProgressStatus.Completed => (GetBrush("#1A3fb950"), GetBrush("#3fb950"), GetBrush("#3fb950")),
            ProgressStatus.Failed => (GetBrush("#1Af85149"), GetBrush("#f85149"), GetBrush("#f85149")),
            ProgressStatus.Paused => (GetBrush("#1Ad29922"), GetBrush("#d29922"), GetBrush("#d29922")),
            ProgressStatus.Cancelled => (GetBrush("#1A8b949e"), GetBrush("#8b949e"), GetBrush("#8b949e")),
            _ => (GetBrush("#21262d"), GetBrush("#8b949e"), GetBrush("#8b949e"))
        };

        StatusBadge.Background = background;
        StatusText.Foreground = foreground;
        MainProgressBar.Foreground = progressForeground;

        // Auto-set status label if not explicitly set
        if (string.IsNullOrEmpty(StatusLabel) || StatusLabel == "Idle")
        {
            StatusLabel = Status switch
            {
                ProgressStatus.Running => "Running",
                ProgressStatus.Completed => "Completed",
                ProgressStatus.Failed => "Failed",
                ProgressStatus.Paused => "Paused",
                ProgressStatus.Cancelled => "Cancelled",
                _ => "Idle"
            };
        }
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
    /// Updates progress with calculated time statistics.
    /// </summary>
    public void UpdateProgress(double current, double total, TimeSpan elapsed)
    {
        Progress = current;
        Maximum = total;
        ProgressText = $"{current:N0} / {total:N0} items";
        ElapsedTime = FormatTimeSpan(elapsed);

        if (current > 0 && elapsed.TotalSeconds > 0)
        {
            var rate = current / elapsed.TotalSeconds;
            RateText = $"{rate:N1} /s";

            var remaining = (total - current) / rate;
            RemainingTime = FormatTimeSpan(TimeSpan.FromSeconds(remaining));
        }
    }

    private static string FormatTimeSpan(TimeSpan ts)
    {
        if (ts.TotalHours >= 1)
            return $"{(int)ts.TotalHours}:{ts.Minutes:D2}:{ts.Seconds:D2}";
        return $"{ts.Minutes:D2}:{ts.Seconds:D2}";
    }

    #endregion
}

/// <summary>
/// Progress operation status.
/// </summary>
public enum ProgressStatus
{
    Idle,
    Running,
    Completed,
    Failed,
    Paused,
    Cancelled
}
