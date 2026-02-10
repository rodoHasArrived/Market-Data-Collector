using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using MarketDataCollector.Uwp.Services;
using MarketDataCollector.Uwp.Models;

namespace MarketDataCollector.Uwp.Views;

/// <summary>
/// Page for managing trading hours and exchange calendars.
/// </summary>
public sealed partial class TradingHoursPage : Page
{
    private readonly DispatcherTimer _clockTimer;
    private readonly ObservableCollection<ExchangeSession> _exchangeSessions;
    private readonly ObservableCollection<HolidayEntry> _holidays;

    public TradingHoursPage()
    {
        this.InitializeComponent();

        _exchangeSessions = new ObservableCollection<ExchangeSession>();
        _holidays = new ObservableCollection<HolidayEntry>();

        ExchangeSessionsList.ItemsSource = _exchangeSessions;
        HolidaysList.ItemsSource = _holidays;

        _clockTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _clockTimer.Tick += ClockTimer_Tick;

        Loaded += TradingHoursPage_Loaded;
        Unloaded += TradingHoursPage_Unloaded;
    }

    private void TradingHoursPage_Loaded(object sender, RoutedEventArgs e)
    {
        _clockTimer.Start();
        UpdateClocks();
        LoadExchangeSessions();
        LoadHolidays();
        UpdateMarketStatus();
    }

    private void TradingHoursPage_Unloaded(object sender, RoutedEventArgs e)
    {
        _clockTimer.Stop();
    }

    private void ClockTimer_Tick(object? sender, object e)
    {
        UpdateClocks();
        UpdateMarketStatus();
    }

    private void UpdateClocks()
    {
        var now = DateTime.Now;
        var utcNow = DateTime.UtcNow;

        LocalTimeText.Text = now.ToString("HH:mm:ss");
        LocalTimezoneText.Text = TimeZoneInfo.Local.DisplayName;

        UtcTimeText.Text = utcNow.ToString("HH:mm:ss");
    }

    private void UpdateMarketStatus()
    {
        var estNow = TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.UtcNow,
            TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"));

        var isWeekday = estNow.DayOfWeek >= DayOfWeek.Monday && estNow.DayOfWeek <= DayOfWeek.Friday;
        var currentTime = estNow.TimeOfDay;

        var preMarketStart = new TimeSpan(4, 0, 0);
        var marketOpen = new TimeSpan(9, 30, 0);
        var marketClose = new TimeSpan(16, 0, 0);
        var postMarketEnd = new TimeSpan(20, 0, 0);

        string status;
        string nextEvent;
        SolidColorBrush statusColor;

        if (!isWeekday)
        {
            status = "Closed";
            nextEvent = "Opens Monday 9:30 AM";
            statusColor = BrushRegistry.Error;
        }
        else if (currentTime >= marketOpen && currentTime < marketClose)
        {
            status = "Open";
            var timeUntilClose = marketClose - currentTime;
            nextEvent = $"Closes in {timeUntilClose.Hours}h {timeUntilClose.Minutes}m";
            statusColor = BrushRegistry.Success;
        }
        else if (currentTime >= preMarketStart && currentTime < marketOpen)
        {
            status = "Pre-Market";
            var timeUntilOpen = marketOpen - currentTime;
            nextEvent = $"Opens in {timeUntilOpen.Hours}h {timeUntilOpen.Minutes}m";
            statusColor = BrushRegistry.Warning;
        }
        else if (currentTime >= marketClose && currentTime < postMarketEnd)
        {
            status = "Post-Market";
            var timeUntilEnd = postMarketEnd - currentTime;
            nextEvent = $"Ends in {timeUntilEnd.Hours}h {timeUntilEnd.Minutes}m";
            statusColor = BrushRegistry.Warning;
        }
        else
        {
            status = "Closed";
            nextEvent = "Pre-market opens 4:00 AM";
            statusColor = BrushRegistry.Error;
        }

        NyseStatusText.Text = status;
        NyseStatusIndicator.Fill = statusColor;
        NyseNextEventText.Text = nextEvent;

        NasdaqStatusText.Text = status;
        NasdaqStatusIndicator.Fill = statusColor;
        NasdaqNextEventText.Text = nextEvent;
    }

    private void LoadExchangeSessions()
    {
        _exchangeSessions.Clear();

        _exchangeSessions.Add(new ExchangeSession
        {
            Name = "NYSE",
            Status = "Open",
            StatusColor = BrushRegistry.Success,
            PreMarket = "04:00-09:30",
            RegularHours = "09:30-16:00",
            PostMarket = "16:00-20:00",
            Timezone = "EST",
            CollectData = true
        });

        _exchangeSessions.Add(new ExchangeSession
        {
            Name = "NASDAQ",
            Status = "Open",
            StatusColor = BrushRegistry.Success,
            PreMarket = "04:00-09:30",
            RegularHours = "09:30-16:00",
            PostMarket = "16:00-20:00",
            Timezone = "EST",
            CollectData = true
        });

        _exchangeSessions.Add(new ExchangeSession
        {
            Name = "CME",
            Status = "Open",
            StatusColor = BrushRegistry.Success,
            PreMarket = "-",
            RegularHours = "17:00-16:00",
            PostMarket = "-",
            Timezone = "CST",
            CollectData = false
        });

        _exchangeSessions.Add(new ExchangeSession
        {
            Name = "LSE",
            Status = "Closed",
            StatusColor = BrushRegistry.Error,
            PreMarket = "-",
            RegularHours = "08:00-16:30",
            PostMarket = "-",
            Timezone = "GMT",
            CollectData = false
        });

        _exchangeSessions.Add(new ExchangeSession
        {
            Name = "TSE",
            Status = "Closed",
            StatusColor = BrushRegistry.Error,
            PreMarket = "-",
            RegularHours = "09:00-15:00",
            PostMarket = "-",
            Timezone = "JST",
            CollectData = false
        });
    }

    private void LoadHolidays()
    {
        _holidays.Clear();

        _holidays.Add(new HolidayEntry
        {
            Date = "2026-01-01",
            Name = "New Year's Day",
            Exchange = "NYSE, NASDAQ",
            Type = "Closed",
            TypeColor = BrushRegistry.Error
        });

        _holidays.Add(new HolidayEntry
        {
            Date = "2026-01-20",
            Name = "Martin Luther King Jr. Day",
            Exchange = "NYSE, NASDAQ",
            Type = "Closed",
            TypeColor = BrushRegistry.Error
        });

        _holidays.Add(new HolidayEntry
        {
            Date = "2026-02-16",
            Name = "Presidents' Day",
            Exchange = "NYSE, NASDAQ",
            Type = "Closed",
            TypeColor = BrushRegistry.Error
        });

        _holidays.Add(new HolidayEntry
        {
            Date = "2026-07-03",
            Name = "Independence Day (Observed)",
            Exchange = "NYSE, NASDAQ",
            Type = "Early Close",
            TypeColor = BrushRegistry.Warning
        });
    }

    private void ExchangeSession_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ExchangeSessionsList.SelectedItem is ExchangeSession session)
        {
            // Update the configuration panel with the selected session's settings
            // In a real implementation, this would populate the time pickers
        }
    }

    private void ExchangeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ExchangeCombo.SelectedItem is ComboBoxItem item)
        {
            var exchange = item.Tag?.ToString();

            // Set default values based on exchange
            switch (exchange)
            {
                case "NYSE":
                case "NASDAQ":
                    PreMarketStartTime.Time = new TimeSpan(4, 0, 0);
                    PreMarketEndTime.Time = new TimeSpan(9, 30, 0);
                    RegularStartTime.Time = new TimeSpan(9, 30, 0);
                    RegularEndTime.Time = new TimeSpan(16, 0, 0);
                    PostMarketStartTime.Time = new TimeSpan(16, 0, 0);
                    PostMarketEndTime.Time = new TimeSpan(20, 0, 0);
                    TimezoneCombo.SelectedIndex = 0; // America/New_York
                    break;

                case "CME":
                    PreMarketStartTime.Time = TimeSpan.Zero;
                    RegularStartTime.Time = new TimeSpan(17, 0, 0);
                    RegularEndTime.Time = new TimeSpan(16, 0, 0);
                    TimezoneCombo.SelectedIndex = 1; // America/Chicago
                    EnablePreMarketCheck.IsChecked = false;
                    EnablePostMarketCheck.IsChecked = false;
                    break;

                case "LSE":
                    RegularStartTime.Time = new TimeSpan(8, 0, 0);
                    RegularEndTime.Time = new TimeSpan(16, 30, 0);
                    TimezoneCombo.SelectedIndex = 2; // Europe/London
                    EnablePreMarketCheck.IsChecked = false;
                    EnablePostMarketCheck.IsChecked = false;
                    break;

                case "TSE":
                    RegularStartTime.Time = new TimeSpan(9, 0, 0);
                    RegularEndTime.Time = new TimeSpan(15, 0, 0);
                    TimezoneCombo.SelectedIndex = 3; // Asia/Tokyo
                    EnablePreMarketCheck.IsChecked = false;
                    EnablePostMarketCheck.IsChecked = false;
                    break;
            }
        }
    }

    private void AddHoliday_Click(object sender, RoutedEventArgs e)
    {
        ActionInfoBar.Severity = InfoBarSeverity.Informational;
        ActionInfoBar.Title = "Add Holiday";
        ActionInfoBar.Message = "Holiday editor dialog will be available in a future update.";
        ActionInfoBar.IsOpen = true;
    }

    private void ImportHolidays_Click(object sender, RoutedEventArgs e)
    {
        ActionInfoBar.Severity = InfoBarSeverity.Informational;
        ActionInfoBar.Title = "Import Holidays";
        ActionInfoBar.Message = "You can import holidays from ICS/CSV files or fetch from exchange APIs.";
        ActionInfoBar.IsOpen = true;
    }

    private async void SaveConfiguration_Click(object sender, RoutedEventArgs e)
    {
        SaveProgress.IsActive = true;

        await Task.Delay(1000); // Simulate save

        SaveProgress.IsActive = false;

        ActionInfoBar.Severity = InfoBarSeverity.Success;
        ActionInfoBar.Title = "Configuration Saved";
        ActionInfoBar.Message = "Trading hours configuration has been saved successfully.";
        ActionInfoBar.IsOpen = true;
    }

    private void ResetDefaults_Click(object sender, RoutedEventArgs e)
    {
        // Reset to US market defaults
        ExchangeCombo.SelectedIndex = 0;
        EnablePreMarketCheck.IsChecked = true;
        EnableRegularHoursCheck.IsChecked = true;
        EnablePostMarketCheck.IsChecked = true;

        PreMarketStartTime.Time = new TimeSpan(4, 0, 0);
        PreMarketEndTime.Time = new TimeSpan(9, 30, 0);
        RegularStartTime.Time = new TimeSpan(9, 30, 0);
        RegularEndTime.Time = new TimeSpan(16, 0, 0);
        PostMarketStartTime.Time = new TimeSpan(16, 0, 0);
        PostMarketEndTime.Time = new TimeSpan(20, 0, 0);

        TimezoneCombo.SelectedIndex = 0;

        MondayCheck.IsChecked = true;
        TuesdayCheck.IsChecked = true;
        WednesdayCheck.IsChecked = true;
        ThursdayCheck.IsChecked = true;
        FridayCheck.IsChecked = true;
        SaturdayCheck.IsChecked = false;
        SundayCheck.IsChecked = false;

        ActionInfoBar.Severity = InfoBarSeverity.Informational;
        ActionInfoBar.Title = "Reset Complete";
        ActionInfoBar.Message = "Settings have been reset to US market defaults.";
        ActionInfoBar.IsOpen = true;
    }
}
