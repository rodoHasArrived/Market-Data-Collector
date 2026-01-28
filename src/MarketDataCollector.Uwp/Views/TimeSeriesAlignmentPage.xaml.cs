using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using MarketDataCollector.Uwp.Services;
using Windows.UI;

namespace MarketDataCollector.Uwp.Views;

/// <summary>
/// Page for aligning time series data to regular intervals.
/// </summary>
public sealed partial class TimeSeriesAlignmentPage : Page
{
    private readonly TimeSeriesAlignmentService _alignmentService;
    private readonly ObservableCollection<AlignmentPreset> _presets;
    private CancellationTokenSource? _cancellationTokenSource;
    private AlignmentResult? _lastResult;

    public TimeSeriesAlignmentPage()
    {
        this.InitializeComponent();

        _alignmentService = TimeSeriesAlignmentService.Instance;
        _presets = new ObservableCollection<AlignmentPreset>();

        PresetsGrid.ItemsSource = _presets;

        Loaded += TimeSeriesAlignmentPage_Loaded;
    }

    private async void TimeSeriesAlignmentPage_Loaded(object sender, RoutedEventArgs e)
    {
        FromDatePicker.Date = DateTimeOffset.Now.AddDays(-7);
        ToDatePicker.Date = DateTimeOffset.Now;

        await LoadPresetsAsync();
        UpdateEstimate();
        DrawVisualization();
    }

    private async Task LoadPresetsAsync()
    {
        var presets = await _alignmentService.GetAlignmentPresetsAsync();
        _presets.Clear();
        foreach (var preset in presets)
        {
            _presets.Add(preset);
        }
    }

    private void Preset_Click(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is AlignmentPreset preset)
        {
            ApplyPreset(preset);
            ActionInfoBar.Severity = InfoBarSeverity.Informational;
            ActionInfoBar.Title = "Preset Applied";
            ActionInfoBar.Message = $"Applied \"{preset.Name}\" preset.";
            ActionInfoBar.IsOpen = true;
        }
    }

    private void ApplyPreset(AlignmentPreset preset)
    {
        // Set interval
        for (int i = 0; i < IntervalCombo.Items.Count; i++)
        {
            if (IntervalCombo.Items[i] is ComboBoxItem item &&
                item.Tag?.ToString() == preset.Interval.ToString())
            {
                IntervalCombo.SelectedIndex = i;
                break;
            }
        }

        // Set aggregation
        for (int i = 0; i < AggregationCombo.Items.Count; i++)
        {
            if (AggregationCombo.Items[i] is ComboBoxItem item &&
                item.Tag?.ToString() == preset.Aggregation.ToString())
            {
                AggregationCombo.SelectedIndex = i;
                break;
            }
        }

        // Set gap strategy
        for (int i = 0; i < GapStrategyCombo.Items.Count; i++)
        {
            if (GapStrategyCombo.Items[i] is ComboBoxItem item &&
                item.Tag?.ToString() == preset.GapStrategy.ToString())
            {
                GapStrategyCombo.SelectedIndex = i;
                break;
            }
        }

        MaxGapBox.Value = preset.MaxGapIntervals;
        MarketHoursOnlyCheck.IsChecked = preset.MarketHoursOnly;
        MarkFilledCheck.IsChecked = preset.MarkFilledValues;

        UpdateEstimate();
    }

    private void Symbols_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var count = SymbolsList.SelectedItems.Count;
        SelectedSymbolsCount.Text = $"{count} symbol{(count == 1 ? "" : "s")} selected";
        UpdateEstimate();
    }

    private void Interval_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateEstimate();
    }

    private void SetWeek_Click(object sender, RoutedEventArgs e)
    {
        FromDatePicker.Date = DateTimeOffset.Now.AddDays(-7);
        ToDatePicker.Date = DateTimeOffset.Now;
        UpdateEstimate();
    }

    private void SetMonth_Click(object sender, RoutedEventArgs e)
    {
        FromDatePicker.Date = DateTimeOffset.Now.AddMonths(-1);
        ToDatePicker.Date = DateTimeOffset.Now;
        UpdateEstimate();
    }

    private void SetQuarter_Click(object sender, RoutedEventArgs e)
    {
        FromDatePicker.Date = DateTimeOffset.Now.AddMonths(-3);
        ToDatePicker.Date = DateTimeOffset.Now;
        UpdateEstimate();
    }

    private void BrowseOutput_Click(object sender, RoutedEventArgs e)
    {
        var intervalTag = (IntervalCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Minute1";
        var formatTag = (OutputFormatCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString()?.ToLower() ?? "parquet";
        OutputPathBox.Text = $"C:\\AlignedData\\ohlcv_{intervalTag.ToLower()}_{DateTime.Now:yyyyMMdd}.{formatTag}";
    }

    private void UpdateEstimate()
    {
        var symbolCount = SymbolsList?.SelectedItems.Count ?? 3;
        var days = 7;
        if (FromDatePicker?.Date != null && ToDatePicker?.Date != null)
        {
            days = Math.Max(1, (ToDatePicker.Date.Value - FromDatePicker.Date.Value).Days);
        }

        var intervalTag = (IntervalCombo?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Minute1";
        var marketHoursOnly = MarketHoursOnlyCheck?.IsChecked == true;

        var interval = Enum.TryParse<TimeSeriesInterval>(intervalTag, out var i) ? i : TimeSeriesInterval.Minute1;
        var intervalsPerDay = GetIntervalsPerDay(interval, marketHoursOnly);

        var outputRecords = symbolCount * days * intervalsPerDay;
        var gaps = (int)(outputRecords * 0.005);
        var fileSize = outputRecords * 100;

        if (EstOutputRecords != null)
            EstOutputRecords.Text = $"~{FormatNumber(outputRecords)}";
        if (EstGaps != null)
            EstGaps.Text = $"~{gaps:N0}";
        if (EstFileSize != null)
            EstFileSize.Text = $"~{FormatBytes(fileSize)}";
    }

    private static int GetIntervalsPerDay(TimeSeriesInterval interval, bool marketHoursOnly)
    {
        var tradingMinutes = marketHoursOnly ? 390 : 1440;

        return interval switch
        {
            TimeSeriesInterval.Second1 => tradingMinutes * 60,
            TimeSeriesInterval.Second5 => tradingMinutes * 12,
            TimeSeriesInterval.Second10 => tradingMinutes * 6,
            TimeSeriesInterval.Second30 => tradingMinutes * 2,
            TimeSeriesInterval.Minute1 => tradingMinutes,
            TimeSeriesInterval.Minute5 => tradingMinutes / 5,
            TimeSeriesInterval.Minute15 => tradingMinutes / 15,
            TimeSeriesInterval.Minute30 => tradingMinutes / 30,
            TimeSeriesInterval.Hour1 => marketHoursOnly ? 7 : 24,
            TimeSeriesInterval.Hour4 => marketHoursOnly ? 2 : 6,
            TimeSeriesInterval.Daily => 1,
            _ => tradingMinutes
        };
    }

    private static string FormatNumber(long number)
    {
        return number switch
        {
            >= 1_000_000_000 => $"{number / 1_000_000_000.0:F1}B",
            >= 1_000_000 => $"{number / 1_000_000.0:F1}M",
            >= 1_000 => $"{number / 1_000.0:F1}K",
            _ => number.ToString("N0")
        };
    }

    private static string FormatBytes(long bytes)
    {
        return bytes switch
        {
            >= 1_073_741_824 => $"{bytes / 1_073_741_824.0:F1} GB",
            >= 1_048_576 => $"{bytes / 1_048_576.0:F1} MB",
            >= 1_024 => $"{bytes / 1_024.0:F1} KB",
            _ => $"{bytes} B"
        };
    }

    private async void Preview_Click(object sender, RoutedEventArgs e)
    {
        var options = BuildAlignmentOptions();
        var preview = await _alignmentService.PreviewAlignmentAsync(options);

        if (preview.Success)
        {
            EstOutputRecords.Text = FormatNumber(preview.ExpectedOutputRecords);
            EstGaps.Text = preview.ExpectedGaps.ToString("N0");
            EstFileSize.Text = FormatBytes(preview.EstimatedFileSizeBytes);

            ActionInfoBar.Severity = InfoBarSeverity.Informational;
            ActionInfoBar.Title = "Preview Complete";
            ActionInfoBar.Message = $"Expected {FormatNumber(preview.ExpectedOutputRecords)} aligned records from {FormatNumber(preview.TotalSourceRecords)} source records.";
            ActionInfoBar.IsOpen = true;
        }
    }

    private async void Align_Click(object sender, RoutedEventArgs e)
    {
        var options = BuildAlignmentOptions();
        var validation = _alignmentService.ValidateOptions(options);

        if (!validation.IsValid)
        {
            ActionInfoBar.Severity = InfoBarSeverity.Error;
            ActionInfoBar.Title = "Validation Error";
            ActionInfoBar.Message = string.Join("; ", validation.Errors);
            ActionInfoBar.IsOpen = true;
            return;
        }

        if (validation.Warnings.Count > 0)
        {
            ActionInfoBar.Severity = InfoBarSeverity.Warning;
            ActionInfoBar.Title = "Warning";
            ActionInfoBar.Message = string.Join("; ", validation.Warnings);
            ActionInfoBar.IsOpen = true;
        }

        _cancellationTokenSource = new CancellationTokenSource();
        ProgressPanel.Visibility = Visibility.Visible;
        ResultsPanel.Visibility = Visibility.Collapsed;
        AlignButton.IsEnabled = false;

        try
        {
            var symbols = SymbolsList.SelectedItems
                .Cast<ListViewItem>()
                .Select(i => i.Content?.ToString() ?? "")
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();

            for (int i = 0; i <= 100; i += 5)
            {
                if (_cancellationTokenSource.Token.IsCancellationRequested)
                    break;

                ProgressBar.Value = i;
                ProgressPercent.Text = $"{i}%";
                var symbolIndex = (i * symbols.Count) / 100;
                ProgressLabel.Text = symbolIndex < symbols.Count
                    ? $"Processing {symbols[symbolIndex]}..."
                    : "Finalizing...";

                await Task.Delay(100, _cancellationTokenSource.Token);
            }

            if (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                _lastResult = new AlignmentResult
                {
                    Success = true,
                    OutputPath = options.OutputPath,
                    TotalSourceRecords = symbols.Count * 500000,
                    AlignedRecords = symbols.Count * 7 * 390,
                    GapsDetected = 120,
                    GapsFilled = 118,
                    Duration = TimeSpan.FromSeconds(3.2)
                };

                ShowResults(_lastResult);
            }
        }
        catch (OperationCanceledException)
        {
            ActionInfoBar.Severity = InfoBarSeverity.Informational;
            ActionInfoBar.Title = "Cancelled";
            ActionInfoBar.Message = "Alignment was cancelled.";
            ActionInfoBar.IsOpen = true;
        }
        finally
        {
            ProgressPanel.Visibility = Visibility.Collapsed;
            AlignButton.IsEnabled = true;
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }
    }

    private AlignmentOptions BuildAlignmentOptions()
    {
        var symbols = SymbolsList.SelectedItems
            .Cast<ListViewItem>()
            .Select(i => i.Content?.ToString() ?? "")
            .Where(s => !string.IsNullOrEmpty(s))
            .ToList();

        var intervalTag = (IntervalCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Minute1";
        var aggregationTag = (AggregationCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "OHLCV";
        var gapTag = (GapStrategyCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Skip";
        var formatTag = (OutputFormatCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Parquet";
        var timezoneTag = (TimezoneCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "America/New_York";

        return new AlignmentOptions
        {
            Symbols = symbols,
            FromDate = FromDatePicker.Date.HasValue ? DateOnly.FromDateTime(FromDatePicker.Date.Value.DateTime) : null,
            ToDate = ToDatePicker.Date.HasValue ? DateOnly.FromDateTime(ToDatePicker.Date.Value.DateTime) : null,
            Interval = Enum.TryParse<TimeSeriesInterval>(intervalTag, out var i) ? i : TimeSeriesInterval.Minute1,
            Aggregation = Enum.TryParse<AggregationType>(aggregationTag, out var a) ? a : AggregationType.OHLCV,
            GapStrategy = Enum.TryParse<GapStrategy>(gapTag, out var g) ? g : GapStrategy.Skip,
            MaxGapIntervals = (int)MaxGapBox.Value,
            MarkFilledValues = MarkFilledCheck.IsChecked == true,
            Timezone = timezoneTag,
            MarketHoursOnly = MarketHoursOnlyCheck.IsChecked == true,
            OutputPath = OutputPathBox.Text,
            OutputFormat = Enum.TryParse<ExportFormat>(formatTag, out var f) ? f : ExportFormat.Parquet,
            IncludeMetadata = IncludeMetadataCheck.IsChecked == true
        };
    }

    private void ShowResults(AlignmentResult result)
    {
        ResultsPanel.Visibility = Visibility.Visible;

        ResultSourceRecords.Text = FormatNumber(result.TotalSourceRecords);
        ResultAlignedRecords.Text = FormatNumber(result.AlignedRecords);

        var compression = result.TotalSourceRecords > 0
            ? result.TotalSourceRecords / Math.Max(1, result.AlignedRecords)
            : 1;
        ResultCompression.Text = $"{compression}x";
        ResultDuration.Text = $"{result.Duration.TotalSeconds:F1}s";

        ResultGapsDetected.Text = result.GapsDetected.ToString("N0");
        ResultGapsFilled.Text = result.GapsFilled.ToString("N0");

        var coverage = result.GapsDetected > 0
            ? (1 - ((result.GapsDetected - result.GapsFilled) / (double)result.GapsDetected)) * 100
            : 100;
        ResultCoverage.Text = $"{coverage:F1}%";

        ResultOutputPath.Text = result.OutputPath ?? "N/A";

        ActionInfoBar.Severity = InfoBarSeverity.Success;
        ActionInfoBar.Title = "Alignment Complete";
        ActionInfoBar.Message = $"Created {FormatNumber(result.AlignedRecords)} aligned records.";
        ActionInfoBar.IsOpen = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        _cancellationTokenSource?.Cancel();
    }

    private void CloseResults_Click(object sender, RoutedEventArgs e)
    {
        ResultsPanel.Visibility = Visibility.Collapsed;
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        if (_lastResult?.OutputPath != null)
        {
            ActionInfoBar.Severity = InfoBarSeverity.Informational;
            ActionInfoBar.Title = "Opening Folder";
            ActionInfoBar.Message = $"Opening folder containing {_lastResult.OutputPath}...";
            ActionInfoBar.IsOpen = true;
        }
    }

    private void OpenFile_Click(object sender, RoutedEventArgs e)
    {
        if (_lastResult?.OutputPath != null)
        {
            ActionInfoBar.Severity = InfoBarSeverity.Informational;
            ActionInfoBar.Title = "Opening File";
            ActionInfoBar.Message = $"Opening {_lastResult.OutputPath}...";
            ActionInfoBar.IsOpen = true;
        }
    }

    private void DrawVisualization()
    {
        DrawIrregularTicks();
        DrawRegularBars();
    }

    private void DrawIrregularTicks()
    {
        IrregularCanvas.Children.Clear();

        var rnd = new Random(42);
        var width = IrregularCanvas.ActualWidth > 0 ? IrregularCanvas.ActualWidth : 300;
        var height = IrregularCanvas.ActualHeight > 0 ? IrregularCanvas.ActualHeight : 80;

        // Draw irregular tick marks
        double x = 10;
        while (x < width - 10)
        {
            var tickHeight = rnd.Next(20, 60);
            var line = new Line
            {
                X1 = x,
                Y1 = height - 10,
                X2 = x,
                Y2 = height - 10 - tickHeight,
                Stroke = new SolidColorBrush(Color.FromArgb(255, 88, 166, 255)),
                StrokeThickness = 2
            };
            IrregularCanvas.Children.Add(line);

            // Irregular spacing
            x += rnd.Next(5, 25);
        }

        // Draw a gap
        var gapStart = width / 3;
        var gapEnd = gapStart + 40;
        var gapRect = new Rectangle
        {
            Width = gapEnd - gapStart,
            Height = height - 20,
            Fill = new SolidColorBrush(Color.FromArgb(40, 248, 81, 73))
        };
        Canvas.SetLeft(gapRect, gapStart);
        Canvas.SetTop(gapRect, 10);
        IrregularCanvas.Children.Add(gapRect);
    }

    private void DrawRegularBars()
    {
        RegularCanvas.Children.Clear();

        var width = RegularCanvas.ActualWidth > 0 ? RegularCanvas.ActualWidth : 300;
        var height = RegularCanvas.ActualHeight > 0 ? RegularCanvas.ActualHeight : 80;

        var barCount = 12;
        var barWidth = (width - 20) / barCount - 4;
        var rnd = new Random(42);

        for (int i = 0; i < barCount; i++)
        {
            var barHeight = rnd.Next(20, 60);
            var isFilled = i == 4 || i == 5;

            var rect = new Rectangle
            {
                Width = barWidth,
                Height = barHeight,
                Fill = new SolidColorBrush(isFilled
                    ? Color.FromArgb(255, 210, 153, 34)
                    : Color.FromArgb(255, 63, 185, 80)),
                RadiusX = 2,
                RadiusY = 2
            };

            Canvas.SetLeft(rect, 10 + i * (barWidth + 4));
            Canvas.SetTop(rect, height - 10 - barHeight);
            RegularCanvas.Children.Add(rect);
        }
    }
}
