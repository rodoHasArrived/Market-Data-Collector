using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using MarketDataCollector.Uwp.Services;

namespace MarketDataCollector.Uwp.Views;

/// <summary>
/// Advanced charting page with candlesticks and technical indicators.
/// </summary>
public sealed partial class ChartingPage : Page
{
    private readonly ChartingService _chartingService;
    private readonly SymbolManagementService _symbolService;
    private string? _selectedSymbol;
    private ChartTimeframe _selectedTimeframe = ChartTimeframe.Daily;
    private CandlestickData? _chartData;
    private readonly List<string> _activeIndicators = new();
    private readonly Dictionary<string, ToggleMenuFlyoutItem> _indicatorMenuItems = new();

    private const double ChartHeight = 400;
    private const double VolumeChartHeight = 100;

    public ChartingPage()
    {
        this.InitializeComponent();
        _chartingService = new ChartingService();
        _symbolService = new SymbolManagementService();

        InitializeDatePickers();
        InitializeIndicatorMenu();
        LoadSymbolsAsync();
    }

    private void InitializeDatePickers()
    {
        var today = DateTimeOffset.Now;
        ToDatePicker.Date = today;
        FromDatePicker.Date = today.AddMonths(-3);
    }

    private void InitializeIndicatorMenu()
    {
        var indicators = _chartingService.GetAvailableIndicators();
        var groupedIndicators = indicators.GroupBy(i => i.Category);

        foreach (var group in groupedIndicators)
        {
            var subItem = new MenuFlyoutSubItem { Text = group.Key };

            foreach (var indicator in group)
            {
                var toggleItem = new ToggleMenuFlyoutItem
                {
                    Text = indicator.Name,
                    Tag = indicator.Id
                };
                toggleItem.Click += IndicatorToggle_Click;
                subItem.Items.Add(toggleItem);
                _indicatorMenuItems[indicator.Id] = toggleItem;
            }

            IndicatorsFlyout.Items.Add(subItem);
        }
    }

    private async void LoadSymbolsAsync()
    {
        try
        {
            var symbols = await _symbolService.GetAllSymbolsAsync();
            foreach (var symbol in symbols)
            {
                SymbolSelector.Items.Add(new ComboBoxItem { Content = symbol.Symbol, Tag = symbol.Symbol });
            }

            if (SymbolSelector.Items.Count > 0)
            {
                SymbolSelector.SelectedIndex = 0;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading symbols: {ex.Message}");
        }
    }

    private async void SymbolSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SymbolSelector.SelectedItem is ComboBoxItem item && item.Tag is string symbol)
        {
            _selectedSymbol = symbol;
            await LoadChartDataAsync();
        }
    }

    private async void TimeframeSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TimeframeSelector.SelectedItem is ComboBoxItem item && item.Tag is string timeframeStr)
        {
            _selectedTimeframe = Enum.Parse<ChartTimeframe>(timeframeStr);
            await LoadChartDataAsync();
        }
    }

    private async void DatePicker_DateChanged(CalendarDatePicker sender, CalendarDatePickerDateChangedEventArgs args)
    {
        if (FromDatePicker.Date.HasValue && ToDatePicker.Date.HasValue)
        {
            await LoadChartDataAsync();
        }
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        await LoadChartDataAsync();
    }

    private async void IndicatorToggle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleMenuFlyoutItem item && item.Tag is string indicatorId)
        {
            if (item.IsChecked)
            {
                if (!_activeIndicators.Contains(indicatorId))
                    _activeIndicators.Add(indicatorId);
            }
            else
            {
                _activeIndicators.Remove(indicatorId);
            }

            UpdateIndicatorDisplay();
            await Task.CompletedTask;
        }
    }

    private void DrawingTool_Click(object sender, RoutedEventArgs e)
    {
        // Drawing tools would require more complex canvas interaction
        // Placeholder for future implementation
    }

    private void ClearDrawings_Click(object sender, RoutedEventArgs e)
    {
        // Clear any drawings
    }

    private async Task LoadChartDataAsync()
    {
        if (string.IsNullOrEmpty(_selectedSymbol) || !FromDatePicker.Date.HasValue || !ToDatePicker.Date.HasValue)
            return;

        LoadingOverlay.Visibility = Visibility.Visible;
        NoChartDataText.Visibility = Visibility.Collapsed;

        try
        {
            var fromDate = DateOnly.FromDateTime(FromDatePicker.Date.Value.DateTime);
            var toDate = DateOnly.FromDateTime(ToDatePicker.Date.Value.DateTime);

            _chartData = await _chartingService.GetCandlestickDataAsync(
                _selectedSymbol, _selectedTimeframe, fromDate, toDate);

            if (_chartData.Candles.Count == 0)
            {
                NoChartDataText.Visibility = Visibility.Visible;
                return;
            }

            RenderCandlestickChart();
            RenderVolumeChart();
            UpdatePriceInfo();
            UpdateVolumeProfile();
            UpdateIndicatorDisplay();
            UpdateStatistics();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading chart: {ex.Message}");
            NoChartDataText.Text = $"Error loading data: {ex.Message}";
            NoChartDataText.Visibility = Visibility.Visible;
        }
        finally
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;
        }
    }

    private void RenderCandlestickChart()
    {
        if (_chartData == null || _chartData.Candles.Count == 0)
            return;

        var priceRange = _chartData.HighestPrice - _chartData.LowestPrice;
        if (priceRange == 0) priceRange = 1;

        var candles = _chartData.Candles.Select(c =>
        {
            var highY = (double)((_chartData.HighestPrice - c.High) / priceRange) * ChartHeight;
            var lowY = (double)((_chartData.HighestPrice - c.Low) / priceRange) * ChartHeight;
            var openY = (double)((_chartData.HighestPrice - c.Open) / priceRange) * ChartHeight;
            var closeY = (double)((_chartData.HighestPrice - c.Close) / priceRange) * ChartHeight;

            var bodyTop = Math.Min(openY, closeY);
            var bodyHeight = Math.Max(1, Math.Abs(openY - closeY));
            var wickHeight = lowY - highY;

            var isBullish = c.Close >= c.Open;
            var bodyColor = isBullish
                ? (SolidColorBrush)Application.Current.Resources["SuccessColorBrush"]
                : (SolidColorBrush)Application.Current.Resources["ErrorColorBrush"];

            return new CandlestickViewModel
            {
                BodyColor = bodyColor,
                WickColor = bodyColor,
                BodyHeight = bodyHeight,
                WickHeight = wickHeight,
                BodyMargin = new Thickness(2, bodyTop, 2, 0),
                WickMargin = new Thickness(5.5, highY, 5.5, 0),
                Tooltip = $"{c.Timestamp:yyyy-MM-dd}\nO: {c.Open:F2}\nH: {c.High:F2}\nL: {c.Low:F2}\nC: {c.Close:F2}\nV: {c.Volume:N0}"
            };
        }).ToList();

        CandlestickChart.ItemsSource = candles;

        // Update price axis
        var priceLabels = new List<string>();
        var steps = 10;
        var stepSize = priceRange / steps;
        for (int i = 0; i <= steps; i++)
        {
            var price = _chartData.HighestPrice - i * stepSize;
            priceLabels.Add($"{price:F2}");
        }
        PriceAxisLabels.ItemsSource = priceLabels;
    }

    private void RenderVolumeChart()
    {
        if (_chartData == null || _chartData.Candles.Count == 0)
            return;

        var maxVolume = _chartData.Candles.Max(c => c.Volume);
        if (maxVolume == 0) maxVolume = 1;

        var volumeBars = _chartData.Candles.Select(c =>
        {
            var height = (double)(c.Volume / maxVolume) * VolumeChartHeight;
            var isBullish = c.Close >= c.Open;

            return new VolumeBarViewModel
            {
                Height = Math.Max(1, height),
                Color = isBullish
                    ? new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(0x80, 0x3f, 0xb9, 0x50))
                    : new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(0x80, 0xf8, 0x51, 0x49)),
                Tooltip = $"{c.Timestamp:yyyy-MM-dd}: {c.Volume:N0}"
            };
        }).ToList();

        VolumeChart.ItemsSource = volumeBars;
    }

    private void UpdatePriceInfo()
    {
        if (_chartData == null || _chartData.Candles.Count == 0)
            return;

        var lastCandle = _chartData.Candles.Last();
        var firstCandle = _chartData.Candles.First();

        CurrentPriceText.Text = $"{lastCandle.Close:F2}";

        var priceChange = lastCandle.Close - firstCandle.Close;
        var priceChangePercent = firstCandle.Close > 0
            ? (priceChange / firstCandle.Close) * 100
            : 0;

        PriceChangeText.Text = $"{(priceChange >= 0 ? "+" : "")}{priceChange:F2}";
        PriceChangePercentText.Text = $"({(priceChangePercent >= 0 ? "+" : "")}{priceChangePercent:F2}%)";

        var changeColor = priceChange >= 0
            ? (SolidColorBrush)Application.Current.Resources["SuccessColorBrush"]
            : (SolidColorBrush)Application.Current.Resources["ErrorColorBrush"];
        PriceChangeText.Foreground = changeColor;
        PriceChangePercentText.Foreground = changeColor;

        OpenText.Text = $"{lastCandle.Open:F2}";
        HighText.Text = $"{lastCandle.High:F2}";
        LowText.Text = $"{lastCandle.Low:F2}";
        CloseText.Text = $"{lastCandle.Close:F2}";

        VolumeText.Text = $"{lastCandle.Volume:N0}";
        AvgVolumeText.Text = $"{_chartData.AverageVolume:N0}";
    }

    private void UpdateVolumeProfile()
    {
        if (_chartData == null || _chartData.Candles.Count == 0)
        {
            NoVolumeProfileText.Visibility = Visibility.Visible;
            VolumeProfileChart.ItemsSource = null;
            return;
        }

        var profile = _chartingService.CalculateVolumeProfile(_chartData, 15);

        if (profile.Levels.Count == 0)
        {
            NoVolumeProfileText.Visibility = Visibility.Visible;
            return;
        }

        NoVolumeProfileText.Visibility = Visibility.Collapsed;

        var maxIntensity = profile.Levels.Max(l => l.Intensity);
        var profileBars = profile.Levels
            .OrderByDescending(l => l.PriceLevel)
            .Select(l =>
            {
                var isPoc = Math.Abs(l.PriceLevel - profile.PointOfControl) < 0.01m * profile.PointOfControl;
                return new VolumeProfileBarViewModel
                {
                    PriceLabel = $"{l.PriceLevel:F2}",
                    BarWidth = l.Intensity * 150,
                    BarColor = isPoc
                        ? new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(0xFF, 0xd2, 0x99, 0x22))
                        : new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(0x80, 0x58, 0xa6, 0xff))
                };
            })
            .ToList();

        VolumeProfileChart.ItemsSource = profileBars;

        PocText.Text = $"{profile.PointOfControl:F2}";
        VahText.Text = $"{profile.ValueAreaHigh:F2}";
        ValText.Text = $"{profile.ValueAreaLow:F2}";
    }

    private void UpdateIndicatorDisplay()
    {
        if (_chartData == null)
            return;

        var indicatorValues = new List<IndicatorValueViewModel>();

        foreach (var indicatorId in _activeIndicators)
        {
            switch (indicatorId)
            {
                case "sma":
                    var sma20 = _chartingService.CalculateSma(_chartData, 20);
                    if (sma20.Values.Count > 0)
                    {
                        indicatorValues.Add(new IndicatorValueViewModel
                        {
                            Name = "SMA(20)",
                            Value = $"{sma20.Values.Last().Value:F2}",
                            ValueColor = new SolidColorBrush(Microsoft.UI.Colors.Orange)
                        });
                    }
                    break;

                case "ema":
                    var ema20 = _chartingService.CalculateEma(_chartData, 20);
                    if (ema20.Values.Count > 0)
                    {
                        indicatorValues.Add(new IndicatorValueViewModel
                        {
                            Name = "EMA(20)",
                            Value = $"{ema20.Values.Last().Value:F2}",
                            ValueColor = new SolidColorBrush(Microsoft.UI.Colors.Cyan)
                        });
                    }
                    break;

                case "vwap":
                    var vwap = _chartingService.CalculateVwap(_chartData);
                    if (vwap.Values.Count > 0)
                    {
                        indicatorValues.Add(new IndicatorValueViewModel
                        {
                            Name = "VWAP",
                            Value = $"{vwap.Values.Last().Value:F2}",
                            ValueColor = new SolidColorBrush(Microsoft.UI.Colors.Purple)
                        });
                    }
                    break;

                case "rsi":
                    var rsi = _chartingService.CalculateRsi(_chartData, 14);
                    if (rsi.Values.Count > 0)
                    {
                        var rsiValue = rsi.Values.Last().Value;
                        indicatorValues.Add(new IndicatorValueViewModel
                        {
                            Name = "RSI(14)",
                            Value = $"{rsiValue:F1}",
                            ValueColor = rsiValue > 70
                                ? (SolidColorBrush)Application.Current.Resources["ErrorColorBrush"]
                                : rsiValue < 30
                                    ? (SolidColorBrush)Application.Current.Resources["SuccessColorBrush"]
                                    : new SolidColorBrush(Microsoft.UI.Colors.White)
                        });
                    }
                    break;

                case "macd":
                    var macd = _chartingService.CalculateMacd(_chartData);
                    if (macd.MacdLine.Count > 0)
                    {
                        var macdValue = macd.MacdLine.Last().Value;
                        var signalValue = macd.SignalLine.LastOrDefault()?.Value ?? 0;
                        indicatorValues.Add(new IndicatorValueViewModel
                        {
                            Name = "MACD",
                            Value = $"{macdValue:F3}",
                            ValueColor = macdValue > signalValue
                                ? (SolidColorBrush)Application.Current.Resources["SuccessColorBrush"]
                                : (SolidColorBrush)Application.Current.Resources["ErrorColorBrush"]
                        });
                        indicatorValues.Add(new IndicatorValueViewModel
                        {
                            Name = "Signal",
                            Value = $"{signalValue:F3}",
                            ValueColor = new SolidColorBrush(Microsoft.UI.Colors.Orange)
                        });
                    }
                    break;

                case "bb":
                    var bb = _chartingService.CalculateBollingerBands(_chartData);
                    if (bb.UpperBand.Count > 0)
                    {
                        indicatorValues.Add(new IndicatorValueViewModel
                        {
                            Name = "BB Upper",
                            Value = $"{bb.UpperBand.Last().Value:F2}",
                            ValueColor = new SolidColorBrush(Microsoft.UI.Colors.LightBlue)
                        });
                        indicatorValues.Add(new IndicatorValueViewModel
                        {
                            Name = "BB Middle",
                            Value = $"{bb.MiddleBand.Last().Value:F2}",
                            ValueColor = new SolidColorBrush(Microsoft.UI.Colors.LightBlue)
                        });
                        indicatorValues.Add(new IndicatorValueViewModel
                        {
                            Name = "BB Lower",
                            Value = $"{bb.LowerBand.Last().Value:F2}",
                            ValueColor = new SolidColorBrush(Microsoft.UI.Colors.LightBlue)
                        });
                    }
                    break;

                case "atr":
                    var atr = _chartingService.CalculateAtr(_chartData, 14);
                    if (atr.Values.Count > 0)
                    {
                        indicatorValues.Add(new IndicatorValueViewModel
                        {
                            Name = "ATR(14)",
                            Value = $"{atr.Values.Last().Value:F2}",
                            ValueColor = new SolidColorBrush(Microsoft.UI.Colors.Yellow)
                        });
                    }
                    break;
            }
        }

        IndicatorValuesList.ItemsSource = indicatorValues;
        NoIndicatorsText.Visibility = indicatorValues.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        ActiveIndicatorsText.Text = _activeIndicators.Count > 0
            ? $"Active: {string.Join(", ", _activeIndicators.Select(i => i.ToUpper()))}"
            : "";
    }

    private void UpdateStatistics()
    {
        if (_chartData == null || _chartData.Candles.Count == 0)
            return;

        PeriodHighText.Text = $"{_chartData.HighestPrice:F2}";
        PeriodLowText.Text = $"{_chartData.LowestPrice:F2}";
        PeriodVolumeText.Text = $"{_chartData.TotalVolume:N0}";
        CandleCountText.Text = $"{_chartData.Candles.Count}";
    }
}

// View Models
public class CandlestickViewModel
{
    public SolidColorBrush BodyColor { get; set; } = new(Microsoft.UI.Colors.White);
    public SolidColorBrush WickColor { get; set; } = new(Microsoft.UI.Colors.White);
    public double BodyHeight { get; set; }
    public double WickHeight { get; set; }
    public Thickness BodyMargin { get; set; }
    public Thickness WickMargin { get; set; }
    public string Tooltip { get; set; } = string.Empty;
}

public class VolumeBarViewModel
{
    public double Height { get; set; }
    public SolidColorBrush Color { get; set; } = new(Microsoft.UI.Colors.Gray);
    public string Tooltip { get; set; } = string.Empty;
}

public class VolumeProfileBarViewModel
{
    public string PriceLabel { get; set; } = string.Empty;
    public double BarWidth { get; set; }
    public SolidColorBrush BarColor { get; set; } = new(Microsoft.UI.Colors.Blue);
}

public class IndicatorValueViewModel
{
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public SolidColorBrush ValueColor { get; set; } = new(Microsoft.UI.Colors.White);
}
