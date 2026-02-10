using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using MarketDataCollector.Uwp.Services;

namespace MarketDataCollector.Uwp.Views;

/// <summary>
/// Data completeness calendar page with heatmap visualization.
/// </summary>
public sealed partial class DataCalendarPage : Page
{
    private readonly DataCalendarService _calendarService;
    private readonly SymbolManagementService _symbolService;
    private int _selectedYear;
    private string[]? _symbols;

    public DataCalendarPage()
    {
        this.InitializeComponent();
        _calendarService = new DataCalendarService();
        _symbolService = new SymbolManagementService();

        InitializeYearSelector();
        LoadCalendarDataAsync();
    }

    private void InitializeYearSelector()
    {
        var currentYear = DateTime.Now.Year;
        for (int year = currentYear; year >= currentYear - 5; year--)
        {
            YearSelector.Items.Add(new ComboBoxItem { Content = year.ToString(), Tag = year });
        }
        YearSelector.SelectedIndex = 0;
        _selectedYear = currentYear;
    }

    private async void YearSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (YearSelector.SelectedItem is ComboBoxItem item && item.Tag is int year)
        {
            _selectedYear = year;
            await LoadCalendarDataAsync();
        }
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        await LoadCalendarDataAsync();
    }

    private async Task LoadCalendarDataAsync()
    {
        LoadingOverlay.Visibility = Visibility.Visible;

        try
        {
            // Get symbols
            var allSymbols = await _symbolService.GetAllSymbolsAsync();
            _symbols = allSymbols.Select(s => s.Symbol).ToArray();

            // Get calendar data
            var yearData = await _calendarService.GetYearCalendarAsync(_selectedYear, _symbols);

            // Update summary stats
            OverallCompletenessText.Text = $"{yearData.OverallCompleteness:F1}%";
            TradingDaysText.Text = yearData.TotalTradingDays.ToString();
            DaysWithDataText.Text = yearData.DaysWithData.ToString();
            TotalGapsText.Text = yearData.TotalGaps.ToString();

            // Build month view models
            var monthViewModels = yearData.Months.Select(m => new MonthViewModel
            {
                MonthName = m.MonthName,
                CompletenessText = $"{m.Completeness:F1}% complete, {m.GapCount} gaps",
                WeekRows = BuildWeekRows(m)
            }).ToList();

            MonthsContainer.ItemsSource = monthViewModels;

            // Load gaps
            var gapSummary = await _calendarService.GetGapSummaryAsync(
                new DateOnly(_selectedYear, 1, 1),
                new DateOnly(_selectedYear, 12, 31),
                _symbols);

            var gapViewModels = gapSummary.Gaps.Select(g => new GapViewModel
            {
                Symbol = g.Symbol,
                DateRange = g.StartDate == g.EndDate
                    ? g.StartDate.ToString("yyyy-MM-dd")
                    : $"{g.StartDate:yyyy-MM-dd} to {g.EndDate:yyyy-MM-dd}",
                GapType = g.GapType,
                MissingEvents = $"{g.ExpectedEvents - g.ActualEvents:N0} missing",
                CanRepair = g.CanRepair,
                CanRepairVisibility = g.CanRepair ? Visibility.Visible : Visibility.Collapsed,
                GapInfo = g
            }).ToList();

            GapsList.ItemsSource = gapViewModels;
            NoGapsText.Visibility = gapViewModels.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            RepairAllGapsButton.Visibility = gapViewModels.Any(g => g.CanRepair) ? Visibility.Visible : Visibility.Collapsed;

            // Load symbol coverage
            var coverageMatrix = await _calendarService.GetCoverageMatrixAsync(
                _symbols,
                new DateOnly(_selectedYear, 1, 1),
                new DateOnly(_selectedYear, 12, 31));

            var coverageViewModels = coverageMatrix.Symbols.Select(s => new SymbolCoverageViewModel
            {
                Symbol = s.Symbol,
                Completeness = s.OverallCompleteness,
                CompletenessText = $"{s.OverallCompleteness:F1}%",
                BarColor = GetCompletenessColor(s.OverallCompleteness),
                TextColor = GetCompletenessColor(s.OverallCompleteness)
            }).OrderByDescending(s => s.Completeness).ToList();

            SymbolCoverageList.ItemsSource = coverageViewModels;
        }
        catch (Exception ex)
        {
            LoggingService.Instance.LogError("Error loading calendar", ex);
        }
        finally
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;
        }
    }

    private List<WeekRowViewModel> BuildWeekRows(CalendarMonthData month)
    {
        var weeks = new List<WeekRowViewModel>();
        var currentWeek = new List<DayCellViewModel>();

        // Pad start of month
        var firstDay = month.Days.FirstOrDefault();
        if (firstDay != null)
        {
            var dayOfWeek = (int)firstDay.DayOfWeek;
            // Adjust for Monday-first week
            dayOfWeek = dayOfWeek == 0 ? 6 : dayOfWeek - 1;
            for (int i = 0; i < dayOfWeek; i++)
            {
                currentWeek.Add(new DayCellViewModel { Color = new SolidColorBrush(Microsoft.UI.Colors.Transparent), Tooltip = "" });
            }
        }

        foreach (var day in month.Days)
        {
            var color = GetDayColor(day);
            var tooltip = day.IsTradingDay
                ? $"{day.Date:MMM d}: {day.Completeness:F1}% ({day.EventCount:N0} events)"
                : $"{day.Date:MMM d}: {(day.IsHoliday ? "Holiday" : "Weekend")}";

            currentWeek.Add(new DayCellViewModel { Color = color, Tooltip = tooltip });

            var dayOfWeek = (int)day.DayOfWeek;
            dayOfWeek = dayOfWeek == 0 ? 6 : dayOfWeek - 1;

            if (dayOfWeek == 6) // Sunday (end of week)
            {
                weeks.Add(new WeekRowViewModel { Days = currentWeek.ToList() });
                currentWeek.Clear();
            }
        }

        // Add remaining days
        if (currentWeek.Count > 0)
        {
            // Pad end of month
            while (currentWeek.Count < 7)
            {
                currentWeek.Add(new DayCellViewModel { Color = new SolidColorBrush(Microsoft.UI.Colors.Transparent), Tooltip = "" });
            }
            weeks.Add(new WeekRowViewModel { Days = currentWeek.ToList() });
        }

        return weeks;
    }

    private SolidColorBrush GetDayColor(CalendarDayData day)
    {
        if (!day.IsTradingDay)
            return new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(0xFF, 0x30, 0x36, 0x3d)); // Non-trading

        return day.CompletenessLevel switch
        {
            CompletenessLevel.Complete => new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(0xFF, 0x3f, 0xb9, 0x50)), // Green
            CompletenessLevel.Good => new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(0xFF, 0x58, 0xa6, 0xff)), // Blue
            CompletenessLevel.Partial => new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(0xFF, 0xd2, 0x99, 0x22)), // Yellow
            CompletenessLevel.Poor => new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(0xFF, 0xf8, 0x51, 0x49)), // Red
            CompletenessLevel.Minimal => new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(0xFF, 0xf8, 0x51, 0x49)), // Red
            CompletenessLevel.Missing => new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(0xFF, 0x21, 0x26, 0x2d)), // Dark
            _ => new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(0xFF, 0x30, 0x36, 0x3d))
        };
    }

    private SolidColorBrush GetCompletenessColor(double completeness)
    {
        if (completeness >= 99)
            return new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(0xFF, 0x3f, 0xb9, 0x50));
        if (completeness >= 95)
            return new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(0xFF, 0x58, 0xa6, 0xff));
        if (completeness >= 80)
            return new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(0xFF, 0xd2, 0x99, 0x22));
        return new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(0xFF, 0xf8, 0x51, 0x49));
    }

    private async void RepairGap_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is GapViewModel gap)
        {
            button.IsEnabled = false;

            try
            {
                var result = await _calendarService.RepairGapsAsync(
                    new[] { gap.GapInfo },
                    new Progress<GapRepairProgress>(p =>
                    {
                        // Update progress if needed
                    }));

                if (result.Success)
                {
                    await LoadCalendarDataAsync();
                }
                else
                {
                    await ShowErrorAsync($"Failed to repair gap: {string.Join(", ", result.FailedItems)}");
                }
            }
            finally
            {
                button.IsEnabled = true;
            }
        }
    }

    private async void RepairAllGaps_Click(object sender, RoutedEventArgs e)
    {
        RepairAllGapsButton.IsEnabled = false;

        try
        {
            var gaps = (GapsList.ItemsSource as IEnumerable<GapViewModel>)?
                .Where(g => g.CanRepair)
                .Select(g => g.GapInfo)
                .ToList();

            if (gaps == null || gaps.Count == 0)
                return;

            var result = await _calendarService.RepairGapsAsync(
                gaps,
                new Progress<GapRepairProgress>(p =>
                {
                    // Update progress
                }));

            await LoadCalendarDataAsync();

            if (!result.Success)
            {
                await ShowErrorAsync($"Some gaps failed to repair: {string.Join(", ", result.FailedItems)}");
            }
        }
        finally
        {
            RepairAllGapsButton.IsEnabled = true;
        }
    }

    private async Task ShowErrorAsync(string message)
    {
        var dialog = new ContentDialog
        {
            Title = "Error",
            Content = message,
            CloseButtonText = "OK",
            XamlRoot = this.XamlRoot
        };
        await dialog.ShowAsync();
    }
}

// View Models
public class MonthViewModel
{
    public string MonthName { get; set; } = string.Empty;
    public string CompletenessText { get; set; } = string.Empty;
    public List<WeekRowViewModel> WeekRows { get; set; } = new();
}

public class WeekRowViewModel
{
    public List<DayCellViewModel> Days { get; set; } = new();
}

public class DayCellViewModel
{
    public SolidColorBrush Color { get; set; } = new(Microsoft.UI.Colors.Transparent);
    public string Tooltip { get; set; } = string.Empty;
}

public class GapViewModel
{
    public string Symbol { get; set; } = string.Empty;
    public string DateRange { get; set; } = string.Empty;
    public string GapType { get; set; } = string.Empty;
    public string MissingEvents { get; set; } = string.Empty;
    public bool CanRepair { get; set; }
    public Visibility CanRepairVisibility { get; set; }
    public GapInfo GapInfo { get; set; } = new();
}

public class SymbolCoverageViewModel
{
    public string Symbol { get; set; } = string.Empty;
    public double Completeness { get; set; }
    public string CompletenessText { get; set; } = string.Empty;
    public SolidColorBrush BarColor { get; set; } = new(Microsoft.UI.Colors.Green);
    public SolidColorBrush TextColor { get; set; } = new(Microsoft.UI.Colors.Green);
}
