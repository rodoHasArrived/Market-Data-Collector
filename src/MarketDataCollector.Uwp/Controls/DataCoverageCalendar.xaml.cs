using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace MarketDataCollector.Uwp.Controls;

/// <summary>
/// A calendar control that visualizes data coverage for backfilled historical data.
/// Shows which days have complete, partial, or missing data.
/// </summary>
public sealed partial class DataCoverageCalendar : UserControl
{
    private DateTime _currentMonth;
    private readonly Dictionary<DateTime, DataCoverageInfo> _coverageData = new();
    private readonly List<string> _availableSymbols = new();
    private string _selectedSymbol = "*";

    /// <summary>
    /// Event raised when a date is clicked.
    /// </summary>
    public event EventHandler<DateClickedEventArgs>? DateClicked;

    /// <summary>
    /// Event raised when a gap date is clicked for repair.
    /// </summary>
    public event EventHandler<DateClickedEventArgs>? RepairRequested;

    public DataCoverageCalendar()
    {
        this.InitializeComponent();
        _currentMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
        RenderCalendar();
    }

    /// <summary>
    /// Sets the data coverage information.
    /// </summary>
    public void SetCoverageData(IEnumerable<DataCoverageInfo> coverage)
    {
        _coverageData.Clear();
        _availableSymbols.Clear();
        var symbols = new HashSet<string>();

        foreach (var item in coverage)
        {
            _coverageData[item.Date.Date] = item;
            if (!string.IsNullOrEmpty(item.Symbol))
            {
                symbols.Add(item.Symbol);
            }
        }

        _availableSymbols.AddRange(symbols.OrderBy(s => s));

        // Update symbol filter
        SymbolFilterCombo.Items.Clear();
        SymbolFilterCombo.Items.Add(new ComboBoxItem { Content = "All Symbols", Tag = "*", IsSelected = true });
        foreach (var symbol in _availableSymbols)
        {
            SymbolFilterCombo.Items.Add(new ComboBoxItem { Content = symbol, Tag = symbol });
        }

        RenderCalendar();
    }

    /// <summary>
    /// Adds coverage data for a specific date.
    /// </summary>
    public void AddCoverage(DateTime date, DataCoverageStatus status, string? symbol = null, int? barCount = null)
    {
        _coverageData[date.Date] = new DataCoverageInfo
        {
            Date = date.Date,
            Status = status,
            Symbol = symbol,
            BarCount = barCount
        };
        RenderCalendar();
    }

    /// <summary>
    /// Clears all coverage data.
    /// </summary>
    public void ClearCoverage()
    {
        _coverageData.Clear();
        _availableSymbols.Clear();
        RenderCalendar();
    }

    /// <summary>
    /// Navigates to a specific month.
    /// </summary>
    public void NavigateToMonth(int year, int month)
    {
        _currentMonth = new DateTime(year, month, 1);
        RenderCalendar();
    }

    private void RenderCalendar()
    {
        // Update month/year text
        MonthYearText.Text = _currentMonth.ToString("MMMM yyyy");

        // Clear existing day cells
        CalendarGrid.Children.Clear();

        var firstDayOfMonth = new DateTime(_currentMonth.Year, _currentMonth.Month, 1);
        var daysInMonth = DateTime.DaysInMonth(_currentMonth.Year, _currentMonth.Month);
        var startDayOfWeek = (int)firstDayOfMonth.DayOfWeek;

        // Create day cells
        for (int day = 1; day <= daysInMonth; day++)
        {
            var date = new DateTime(_currentMonth.Year, _currentMonth.Month, day);
            var row = (startDayOfWeek + day - 1) / 7;
            var col = (startDayOfWeek + day - 1) % 7;

            var cell = CreateDayCell(date);
            Grid.SetRow(cell, row);
            Grid.SetColumn(cell, col);
            CalendarGrid.Children.Add(cell);
        }
    }

    private Border CreateDayCell(DateTime date)
    {
        var isWeekend = date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday;
        var isToday = date.Date == DateTime.Today;
        var isFuture = date.Date > DateTime.Today;

        // Get coverage status
        var coverage = GetCoverageForDate(date);

        // Determine background color
        var bgColor = GetBackgroundColor(coverage, isWeekend, isFuture);

        var border = new Border
        {
            Background = new SolidColorBrush(bgColor),
            CornerRadius = new CornerRadius(4),
            Margin = new Thickness(2),
            Padding = new Thickness(4),
            Tag = date
        };

        // Add today indicator
        if (isToday)
        {
            border.BorderBrush = Application.Current.Resources["SystemAccentColor"] as SolidColorBrush
                ?? new SolidColorBrush(Color.FromArgb(255, 0, 120, 212));
            border.BorderThickness = new Thickness(2);
        }

        var content = new StackPanel
        {
            Spacing = 2,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        // Day number
        var dayText = new TextBlock
        {
            Text = date.Day.ToString(),
            FontWeight = isToday ? Microsoft.UI.Text.FontWeights.Bold : Microsoft.UI.Text.FontWeights.Normal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Foreground = GetTextColor(coverage, isWeekend, isFuture)
        };
        content.Children.Add(dayText);

        // Bar count indicator (if available)
        if (coverage != null && coverage.BarCount.HasValue && coverage.BarCount > 0)
        {
            var barText = new TextBlock
            {
                Text = coverage.BarCount > 1000 ? $"{coverage.BarCount / 1000.0:F1}k" : coverage.BarCount.ToString(),
                FontSize = 9,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255))
            };
            content.Children.Add(barText);
        }

        border.Child = content;

        // Add click handler
        if (!isFuture && !isWeekend)
        {
            border.Tapped += DayCell_Tapped;
            border.PointerEntered += DayCell_PointerEntered;
            border.PointerExited += DayCell_PointerExited;
        }

        return border;
    }

    private DataCoverageInfo? GetCoverageForDate(DateTime date)
    {
        if (_selectedSymbol == "*")
        {
            // Aggregate all symbols for this date
            var coverageForDate = _coverageData.Values
                .Where(c => c.Date.Date == date.Date)
                .ToList();

            if (!coverageForDate.Any())
                return null;

            // Return aggregate status
            var hasComplete = coverageForDate.Any(c => c.Status == DataCoverageStatus.Complete);
            var hasPartial = coverageForDate.Any(c => c.Status == DataCoverageStatus.Partial);
            var hasMissing = coverageForDate.Any(c => c.Status == DataCoverageStatus.Missing);

            return new DataCoverageInfo
            {
                Date = date,
                Status = hasComplete && !hasMissing ? DataCoverageStatus.Complete
                    : hasPartial || (hasComplete && hasMissing) ? DataCoverageStatus.Partial
                    : DataCoverageStatus.Missing,
                BarCount = coverageForDate.Sum(c => c.BarCount ?? 0)
            };
        }
        else
        {
            // Filter by selected symbol
            return _coverageData.Values
                .FirstOrDefault(c => c.Date.Date == date.Date && c.Symbol == _selectedSymbol);
        }
    }

    private static Color GetBackgroundColor(DataCoverageInfo? coverage, bool isWeekend, bool isFuture)
    {
        if (isFuture)
            return Color.FromArgb(20, 128, 128, 128);

        if (isWeekend)
            return Color.FromArgb(40, 128, 128, 128);

        if (coverage == null)
            return Color.FromArgb(60, 245, 101, 101); // Missing (light red)

        return coverage.Status switch
        {
            DataCoverageStatus.Complete => Color.FromArgb(255, 72, 187, 120), // Green
            DataCoverageStatus.Partial => Color.FromArgb(255, 237, 137, 54), // Orange
            DataCoverageStatus.Missing => Color.FromArgb(255, 245, 101, 101), // Red
            _ => Color.FromArgb(40, 128, 128, 128)
        };
    }

    private static SolidColorBrush GetTextColor(DataCoverageInfo? coverage, bool isWeekend, bool isFuture)
    {
        if (isFuture || isWeekend || coverage == null)
            return new SolidColorBrush(Color.FromArgb(180, 128, 128, 128));

        return coverage.Status switch
        {
            DataCoverageStatus.Complete or DataCoverageStatus.Partial or DataCoverageStatus.Missing
                => new SolidColorBrush(Colors.White),
            _ => new SolidColorBrush(Color.FromArgb(180, 128, 128, 128))
        };
    }

    private void DayCell_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is Border border && border.Tag is DateTime date)
        {
            var coverage = GetCoverageForDate(date);
            DateClicked?.Invoke(this, new DateClickedEventArgs
            {
                Date = date,
                Coverage = coverage
            });
        }
    }

    private void DayCell_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Border border)
        {
            border.Opacity = 0.8;
        }
    }

    private void DayCell_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Border border)
        {
            border.Opacity = 1.0;
        }
    }

    private void PrevMonth_Click(object sender, RoutedEventArgs e)
    {
        _currentMonth = _currentMonth.AddMonths(-1);
        RenderCalendar();
    }

    private void NextMonth_Click(object sender, RoutedEventArgs e)
    {
        _currentMonth = _currentMonth.AddMonths(1);
        RenderCalendar();
    }

    private void Today_Click(object sender, RoutedEventArgs e)
    {
        _currentMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
        RenderCalendar();
    }

    private void SymbolFilter_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (SymbolFilterCombo.SelectedItem is ComboBoxItem item && item.Tag is string symbol)
        {
            _selectedSymbol = symbol;
            RenderCalendar();
        }
    }
}

/// <summary>
/// Data coverage status for a specific date.
/// </summary>
public enum DataCoverageStatus
{
    /// <summary>No data present.</summary>
    Missing,
    /// <summary>Partial data (some symbols or incomplete bars).</summary>
    Partial,
    /// <summary>Complete data coverage.</summary>
    Complete
}

/// <summary>
/// Coverage information for a specific date.
/// </summary>
public class DataCoverageInfo
{
    /// <summary>The date.</summary>
    public DateTime Date { get; set; }

    /// <summary>Coverage status.</summary>
    public DataCoverageStatus Status { get; set; }

    /// <summary>Optional symbol (for per-symbol filtering).</summary>
    public string? Symbol { get; set; }

    /// <summary>Number of bars on this date.</summary>
    public int? BarCount { get; set; }

    /// <summary>Optional error message if data is missing.</summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Event args for date clicked events.
/// </summary>
public class DateClickedEventArgs : EventArgs
{
    /// <summary>The clicked date.</summary>
    public DateTime Date { get; set; }

    /// <summary>Coverage info for the date.</summary>
    public DataCoverageInfo? Coverage { get; set; }
}
