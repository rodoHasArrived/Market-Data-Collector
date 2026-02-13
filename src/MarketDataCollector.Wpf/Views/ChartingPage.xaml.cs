using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MarketDataCollector.Ui.Services;
using MarketDataCollector.Wpf.ViewModels;

namespace MarketDataCollector.Wpf.Views;

public partial class ChartingPage : Page
{
    private readonly ChartingPageViewModel _viewModel = new();

    public ChartingPage()
    {
        InitializeComponent();
    }

    private void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        _viewModel.Initialize(
            SymbolCombo, CandlestickChart, VolumeChart, VolumeProfileChart,
            IndicatorValuesList, CurrentPriceText, PriceChangeText, PriceChangePercentText,
            OpenText, HighText, LowText, VolumeText,
            PocText, VahText, ValText,
            PeriodHighText, PeriodLowText, PeriodVolumeText, CandleCountText,
            NoChartDataText, NoVolumeProfileText, NoIndicatorsText,
            ActiveIndicatorsText, LoadingOverlay);
    }

    private void Symbol_SelectionChanged(object sender, SelectionChangedEventArgs e) => _viewModel.OnSymbolChanged();
    private void Timeframe_SelectionChanged(object sender, SelectionChangedEventArgs e) => _viewModel.OnTimeframeChanged(TimeframeCombo);
    private void DatePicker_Changed(object? sender, EventArgs e) => _viewModel.OnDateChanged(FromDatePicker, ToDatePicker);
    private void Refresh_Click(object sender, RoutedEventArgs e) => _viewModel.RefreshChart();
    private void Indicator_Click(object sender, RoutedEventArgs e) => _viewModel.OnIndicatorToggled(sender);
}

public sealed class ChartingPageViewModel : BindableBase
{
    private readonly ChartingService _chartingService = new();
    private readonly SymbolManagementService _symbolService = SymbolManagementService.Instance;
    private CandlestickData? _chartData;
    private string? _selectedSymbol;
    private ChartTimeframe _selectedTimeframe = ChartTimeframe.Daily;
    private DateOnly? _fromDate;
    private DateOnly? _toDate;
    private readonly List<string> _activeIndicators = new();

    private const double ChartHeight = 400;
    private const double VolumeChartHeight = 100;

    private ComboBox? _symbolCombo;
    private ItemsControl? _candlestickChart;
    private ItemsControl? _volumeChart;
    private ItemsControl? _volumeProfileChart;
    private ItemsControl? _indicatorValuesList;
    private TextBlock? _currentPriceText, _priceChangeText, _priceChangePercentText;
    private TextBlock? _openText, _highText, _lowText, _volumeText;
    private TextBlock? _pocText, _vahText, _valText;
    private TextBlock? _periodHighText, _periodLowText, _periodVolumeText, _candleCountText;
    private TextBlock? _noChartDataText, _noVolumeProfileText, _noIndicatorsText, _activeIndicatorsText;
    private Border? _loadingOverlay;

    private static readonly SolidColorBrush BullishBrush = new(Color.FromRgb(63, 185, 80));
    private static readonly SolidColorBrush BearishBrush = new(Color.FromRgb(248, 81, 73));
    private static readonly SolidColorBrush BullishVolumeBrush = new(Color.FromArgb(128, 63, 185, 80));
    private static readonly SolidColorBrush BearishVolumeBrush = new(Color.FromArgb(128, 248, 81, 73));

    public void Initialize(
        ComboBox symbolCombo, ItemsControl candlestickChart, ItemsControl volumeChart,
        ItemsControl volumeProfileChart, ItemsControl indicatorValuesList,
        TextBlock currentPrice, TextBlock priceChange, TextBlock priceChangePercent,
        TextBlock open, TextBlock high, TextBlock low, TextBlock volume,
        TextBlock poc, TextBlock vah, TextBlock val,
        TextBlock periodHigh, TextBlock periodLow, TextBlock periodVolume, TextBlock candleCount,
        TextBlock noChartData, TextBlock noVolumeProfile, TextBlock noIndicators,
        TextBlock activeIndicators, Border loadingOverlay)
    {
        _symbolCombo = symbolCombo;
        _candlestickChart = candlestickChart;
        _volumeChart = volumeChart;
        _volumeProfileChart = volumeProfileChart;
        _indicatorValuesList = indicatorValuesList;
        _currentPriceText = currentPrice;
        _priceChangeText = priceChange;
        _priceChangePercentText = priceChangePercent;
        _openText = open; _highText = high; _lowText = low; _volumeText = volume;
        _pocText = poc; _vahText = vah; _valText = val;
        _periodHighText = periodHigh; _periodLowText = periodLow;
        _periodVolumeText = periodVolume; _candleCountText = candleCount;
        _noChartDataText = noChartData; _noVolumeProfileText = noVolumeProfile;
        _noIndicatorsText = noIndicators; _activeIndicatorsText = activeIndicators;
        _loadingOverlay = loadingOverlay;

        _fromDate = DateOnly.FromDateTime(DateTime.Now.AddMonths(-3));
        _toDate = DateOnly.FromDateTime(DateTime.Now);

        LoadSymbolsAsync();
    }

    private async void LoadSymbolsAsync()
    {
        try
        {
            var result = await _symbolService.GetAllSymbolsAsync();
            if (_symbolCombo == null || !result.Success) return;
            foreach (var symbol in result.Symbols)
                _symbolCombo.Items.Add(new ComboBoxItem { Content = symbol.Symbol, Tag = symbol.Symbol });
            if (_symbolCombo.Items.Count > 0) _symbolCombo.SelectedIndex = 0;
        }
        catch
        {
            // Symbols not available
        }
    }

    public void OnSymbolChanged()
    {
        if (_symbolCombo?.SelectedItem is ComboBoxItem item && item.Tag is string symbol)
        {
            _selectedSymbol = symbol;
            LoadChartDataAsync();
        }
    }

    public void OnTimeframeChanged(ComboBox combo)
    {
        if (combo.SelectedItem is ComboBoxItem item && item.Tag is string tf && Enum.TryParse<ChartTimeframe>(tf, out var timeframe))
        {
            _selectedTimeframe = timeframe;
            LoadChartDataAsync();
        }
    }

    public void OnDateChanged(DatePicker from, DatePicker to)
    {
        if (from.SelectedDate.HasValue) _fromDate = DateOnly.FromDateTime(from.SelectedDate.Value);
        if (to.SelectedDate.HasValue) _toDate = DateOnly.FromDateTime(to.SelectedDate.Value);
        if (_fromDate.HasValue && _toDate.HasValue) LoadChartDataAsync();
    }

    public void RefreshChart() => LoadChartDataAsync();

    public void OnIndicatorToggled(object sender)
    {
        if (sender is CheckBox cb && cb.Tag is string id)
        {
            if (cb.IsChecked == true) { if (!_activeIndicators.Contains(id)) _activeIndicators.Add(id); }
            else _activeIndicators.Remove(id);
            UpdateIndicatorDisplay();
        }
    }

    private async void LoadChartDataAsync()
    {
        if (string.IsNullOrEmpty(_selectedSymbol) || !_fromDate.HasValue || !_toDate.HasValue) return;
        if (_loadingOverlay != null) _loadingOverlay.Visibility = Visibility.Visible;
        if (_noChartDataText != null) _noChartDataText.Visibility = Visibility.Collapsed;

        try
        {
            _chartData = await _chartingService.GetCandlestickDataAsync(_selectedSymbol, _selectedTimeframe, _fromDate.Value, _toDate.Value);
            if (_chartData.Candles.Count == 0) { if (_noChartDataText != null) _noChartDataText.Visibility = Visibility.Visible; return; }
            RenderCandlestickChart(); RenderVolumeChart(); UpdatePriceInfo(); UpdateVolumeProfile(); UpdateIndicatorDisplay(); UpdateStatistics();
        }
        catch (Exception ex)
        {
            if (_noChartDataText != null) { _noChartDataText.Text = $"Error: {ex.Message}"; _noChartDataText.Visibility = Visibility.Visible; }
        }
        finally { if (_loadingOverlay != null) _loadingOverlay.Visibility = Visibility.Collapsed; }
    }

    private void RenderCandlestickChart()
    {
        if (_chartData == null || _chartData.Candles.Count == 0 || _candlestickChart == null) return;
        var range = _chartData.HighestPrice - _chartData.LowestPrice;
        if (range == 0) range = 1;

        _candlestickChart.ItemsSource = _chartData.Candles.Select(c =>
        {
            var highY = (double)((_chartData.HighestPrice - c.High) / range) * ChartHeight;
            var lowY = (double)((_chartData.HighestPrice - c.Low) / range) * ChartHeight;
            var openY = (double)((_chartData.HighestPrice - c.Open) / range) * ChartHeight;
            var closeY = (double)((_chartData.HighestPrice - c.Close) / range) * ChartHeight;
            var bodyTop = Math.Min(openY, closeY);
            var bull = c.Close >= c.Open;
            var brush = bull ? BullishBrush : BearishBrush;
            return new WpfCandlestickVm
            {
                BodyBrush = brush, WickBrush = brush,
                BodyHeight = Math.Max(1, Math.Abs(openY - closeY)),
                WickHeight = lowY - highY,
                BodyMargin = new Thickness(2, bodyTop, 2, 0),
                WickMargin = new Thickness(5.5, highY, 5.5, 0),
                Tooltip = $"{c.Timestamp:yyyy-MM-dd}\nO: {c.Open:F2}  H: {c.High:F2}\nL: {c.Low:F2}  C: {c.Close:F2}\nVol: {c.Volume:N0}"
            };
        }).ToList();
    }

    private void RenderVolumeChart()
    {
        if (_chartData == null || _chartData.Candles.Count == 0 || _volumeChart == null) return;
        var max = _chartData.Candles.Max(c => c.Volume);
        if (max == 0) max = 1;

        _volumeChart.ItemsSource = _chartData.Candles.Select(c => new WpfVolumeBarVm
        {
            Height = Math.Max(1, (double)(c.Volume / max) * VolumeChartHeight),
            BarBrush = c.Close >= c.Open ? BullishVolumeBrush : BearishVolumeBrush,
            Tooltip = $"{c.Timestamp:yyyy-MM-dd}: {c.Volume:N0}"
        }).ToList();
    }

    private void UpdatePriceInfo()
    {
        if (_chartData == null || _chartData.Candles.Count == 0) return;
        var last = _chartData.Candles.Last();
        var first = _chartData.Candles.First();
        if (_currentPriceText != null) _currentPriceText.Text = $"{last.Close:F2}";
        var change = last.Close - first.Close;
        var pct = first.Close > 0 ? (change / first.Close) * 100 : 0;
        var brush = change >= 0 ? BullishBrush : BearishBrush;
        if (_priceChangeText != null) { _priceChangeText.Text = $"{(change >= 0 ? "+" : "")}{change:F2}"; _priceChangeText.Foreground = brush; }
        if (_priceChangePercentText != null) { _priceChangePercentText.Text = $"({(pct >= 0 ? "+" : "")}{pct:F2}%)"; _priceChangePercentText.Foreground = brush; }
        if (_openText != null) _openText.Text = $"{last.Open:F2}";
        if (_highText != null) _highText.Text = $"{last.High:F2}";
        if (_lowText != null) _lowText.Text = $"{last.Low:F2}";
        if (_volumeText != null) _volumeText.Text = $"{last.Volume:N0}";
    }

    private void UpdateVolumeProfile()
    {
        if (_chartData == null || _chartData.Candles.Count == 0)
        { if (_noVolumeProfileText != null) _noVolumeProfileText.Visibility = Visibility.Visible; return; }

        var profile = _chartingService.CalculateVolumeProfile(_chartData, 15);
        if (profile.Levels.Count == 0) { if (_noVolumeProfileText != null) _noVolumeProfileText.Visibility = Visibility.Visible; return; }
        if (_noVolumeProfileText != null) _noVolumeProfileText.Visibility = Visibility.Collapsed;

        var pocBrush = new SolidColorBrush(Color.FromRgb(210, 153, 34));
        var normalBrush = new SolidColorBrush(Color.FromArgb(128, 88, 166, 255));
        if (_volumeProfileChart != null)
            _volumeProfileChart.ItemsSource = profile.Levels.OrderByDescending(l => l.PriceLevel).Select(l =>
            {
                var isPoc = Math.Abs(l.PriceLevel - profile.PointOfControl) < 0.01m * profile.PointOfControl;
                return new WpfVolumeProfileBarVm { PriceLabel = $"{l.PriceLevel:F2}", BarWidth = l.Intensity * 150, BarBrush = isPoc ? pocBrush : normalBrush };
            }).ToList();
        if (_pocText != null) _pocText.Text = $"{profile.PointOfControl:F2}";
        if (_vahText != null) _vahText.Text = $"{profile.ValueAreaHigh:F2}";
        if (_valText != null) _valText.Text = $"{profile.ValueAreaLow:F2}";
    }

    private void UpdateIndicatorDisplay()
    {
        if (_chartData == null) return;
        var values = new List<WpfIndicatorValueVm>();
        foreach (var id in _activeIndicators)
        {
            switch (id)
            {
                case "sma": AddSimple(values, _chartingService.CalculateSma(_chartData, 20), "SMA(20)", Colors.Orange); break;
                case "ema": AddSimple(values, _chartingService.CalculateEma(_chartData, 20), "EMA(20)", Colors.Cyan); break;
                case "vwap": AddSimple(values, _chartingService.CalculateVwap(_chartData), "VWAP", Colors.Purple); break;
                case "atr": AddSimple(values, _chartingService.CalculateAtr(_chartData, 14), "ATR(14)", Colors.Yellow); break;
                case "rsi":
                    var rsi = _chartingService.CalculateRsi(_chartData, 14);
                    if (rsi.Values.Count > 0) { var v = rsi.Values.Last().Value; values.Add(new WpfIndicatorValueVm { Name = "RSI(14)", Value = $"{v:F1}", ValueBrush = new SolidColorBrush(v > 70 ? Colors.Red : v < 30 ? Colors.Green : Colors.White) }); }
                    break;
                case "macd":
                    var macd = _chartingService.CalculateMacd(_chartData);
                    if (macd.MacdLine.Count > 0)
                    {
                        var mv = macd.MacdLine.Last().Value; var sv = macd.SignalLine.LastOrDefault()?.Value ?? 0;
                        values.Add(new WpfIndicatorValueVm { Name = "MACD", Value = $"{mv:F3}", ValueBrush = new SolidColorBrush(mv > sv ? Colors.Green : Colors.Red) });
                        values.Add(new WpfIndicatorValueVm { Name = "Signal", Value = $"{sv:F3}", ValueBrush = new SolidColorBrush(Colors.Orange) });
                    }
                    break;
                case "bb":
                    var bb = _chartingService.CalculateBollingerBands(_chartData);
                    if (bb.UpperBand.Count > 0)
                    {
                        var b = new SolidColorBrush(Colors.LightBlue);
                        values.Add(new WpfIndicatorValueVm { Name = "BB Upper", Value = $"{bb.UpperBand.Last().Value:F2}", ValueBrush = b });
                        values.Add(new WpfIndicatorValueVm { Name = "BB Middle", Value = $"{bb.MiddleBand.Last().Value:F2}", ValueBrush = b });
                        values.Add(new WpfIndicatorValueVm { Name = "BB Lower", Value = $"{bb.LowerBand.Last().Value:F2}", ValueBrush = b });
                    }
                    break;
            }
        }
        if (_indicatorValuesList != null) _indicatorValuesList.ItemsSource = values;
        if (_noIndicatorsText != null) _noIndicatorsText.Visibility = values.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (_activeIndicatorsText != null) _activeIndicatorsText.Text = _activeIndicators.Count > 0 ? $"Active: {string.Join(", ", _activeIndicators.Select(i => i.ToUpper()))}" : "";
    }

    private static void AddSimple(List<WpfIndicatorValueVm> list, IndicatorData data, string name, Color color)
    { if (data.Values.Count > 0) list.Add(new WpfIndicatorValueVm { Name = name, Value = $"{data.Values.Last().Value:F2}", ValueBrush = new SolidColorBrush(color) }); }

    private void UpdateStatistics()
    {
        if (_chartData == null || _chartData.Candles.Count == 0) return;
        if (_periodHighText != null) _periodHighText.Text = $"{_chartData.HighestPrice:F2}";
        if (_periodLowText != null) _periodLowText.Text = $"{_chartData.LowestPrice:F2}";
        if (_periodVolumeText != null) _periodVolumeText.Text = $"{_chartData.TotalVolume:N0}";
        if (_candleCountText != null) _candleCountText.Text = $"{_chartData.Candles.Count}";
    }
}

public sealed class WpfCandlestickVm
{
    public Brush BodyBrush { get; set; } = Brushes.White;
    public Brush WickBrush { get; set; } = Brushes.White;
    public double BodyHeight { get; set; }
    public double WickHeight { get; set; }
    public Thickness BodyMargin { get; set; }
    public Thickness WickMargin { get; set; }
    public string Tooltip { get; set; } = string.Empty;
}

public sealed class WpfVolumeBarVm
{
    public double Height { get; set; }
    public Brush BarBrush { get; set; } = Brushes.Gray;
    public string Tooltip { get; set; } = string.Empty;
}

public sealed class WpfVolumeProfileBarVm
{
    public string PriceLabel { get; set; } = string.Empty;
    public double BarWidth { get; set; }
    public Brush BarBrush { get; set; } = Brushes.Blue;
}

public sealed class WpfIndicatorValueVm
{
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public Brush ValueBrush { get; set; } = Brushes.White;
}
