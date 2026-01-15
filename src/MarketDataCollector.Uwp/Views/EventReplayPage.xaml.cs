using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using MarketDataCollector.Uwp.Services;

namespace MarketDataCollector.Uwp.Views;

/// <summary>
/// Page for replaying historical market data events from JSONL files.
/// </summary>
public sealed partial class EventReplayPage : Page
{
    private readonly EventReplayService _replayService;
    private readonly DispatcherTimer _statusTimer;
    private string? _currentSessionId;
    private bool _isPlaying;
    private string? _selectedFilePath;
    private readonly ObservableCollection<ReplayEventDisplay> _eventPreview = new();

    public EventReplayPage()
    {
        this.InitializeComponent();
        _replayService = EventReplayService.Instance;
        EventPreviewList.ItemsSource = _eventPreview;

        _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _statusTimer.Tick += StatusTimer_Tick;

        SelectFileRadio.Checked += (s, e) =>
        {
            FileSelectionPanel.Visibility = Visibility.Visible;
            DateRangePanel.Visibility = Visibility.Collapsed;
        };
        SelectDateRangeRadio.Checked += (s, e) =>
        {
            FileSelectionPanel.Visibility = Visibility.Collapsed;
            DateRangePanel.Visibility = Visibility.Visible;
        };

        Loaded += EventReplayPage_Loaded;
        Unloaded += EventReplayPage_Unloaded;
    }

    private async void EventReplayPage_Loaded(object sender, RoutedEventArgs e)
    {
        // Load recent files
        var result = await _replayService.GetAvailableFilesAsync();
        if (result.Success && result.Files.Count > 0)
        {
            var recent = result.Files.Take(10).Select(f => f.FileName).ToList();
            RecentFilesCombo.ItemsSource = recent;
        }
    }

    private void EventReplayPage_Unloaded(object sender, RoutedEventArgs e)
    {
        _statusTimer.Stop();
        if (_currentSessionId != null)
        {
            _ = _replayService.StopReplayAsync(_currentSessionId);
        }
    }

    private async void BrowseFile_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();
        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        picker.FileTypeFilter.Add(".jsonl");
        picker.FileTypeFilter.Add(".jsonl.gz");

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        var file = await picker.PickSingleFileAsync();
        if (file != null)
        {
            _selectedFilePath = file.Path;
            FilePathBox.Text = file.Path;
            await LoadFileInfoAsync(file.Path);
        }
    }

    private async void RecentFiles_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (RecentFilesCombo.SelectedItem is string fileName)
        {
            // Find the full path from available files
            var result = await _replayService.GetAvailableFilesAsync();
            var file = result.Files.FirstOrDefault(f => f.FileName == fileName);
            if (file != null)
            {
                _selectedFilePath = file.Path;
                FilePathBox.Text = file.Path;
                await LoadFileInfoAsync(file.Path);
            }
        }
    }

    private async void FindFiles_Click(object sender, RoutedEventArgs e)
    {
        var symbol = SymbolBox.Text?.Trim().ToUpperInvariant();
        DateOnly? fromDate = FromDatePicker.Date?.Date is DateTime from ? DateOnly.FromDateTime(from) : null;
        DateOnly? toDate = ToDatePicker.Date?.Date is DateTime to ? DateOnly.FromDateTime(to) : null;

        var result = await _replayService.GetAvailableFilesAsync(symbol, fromDate, toDate);
        if (result.Success && result.Files.Count > 0)
        {
            var displayFiles = result.Files.Select(f => new ReplayFileDisplay
            {
                Path = f.Path,
                Symbol = f.Symbol,
                EventType = f.EventType,
                DateText = f.Date.ToString("yyyy-MM-dd"),
                EventCountText = f.EventCount.ToString("N0"),
                SizeText = FormatBytes(f.FileSizeBytes)
            }).ToList();

            AvailableFilesList.ItemsSource = displayFiles;
            AvailableFilesCard.Visibility = Visibility.Visible;
        }
        else
        {
            AvailableFilesCard.Visibility = Visibility.Collapsed;
        }
    }

    private async void AvailableFiles_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (AvailableFilesList.SelectedItem is ReplayFileDisplay file)
        {
            _selectedFilePath = file.Path;
            await LoadFileInfoAsync(file.Path);
        }
    }

    private async System.Threading.Tasks.Task LoadFileInfoAsync(string filePath)
    {
        var stats = await _replayService.GetFileStatsAsync(filePath);

        FileInfoCard.Visibility = Visibility.Visible;
        InfoFileName.Text = System.IO.Path.GetFileName(filePath);
        InfoEventCount.Text = stats.EventCount.ToString("N0");
        InfoDuration.Text = FormatTimeSpan(stats.Duration);
        InfoFirstEvent.Text = stats.FirstEventTime.ToString("g");
        InfoLastEvent.Text = stats.LastEventTime.ToString("g");

        if (stats.EventTypeCounts?.Count > 0)
        {
            EventBreakdownCard.Visibility = Visibility.Visible;
            EventBreakdownList.ItemsSource = stats.EventTypeCounts.Select(kvp => new EventTypeBreakdown
            {
                EventType = kvp.Key,
                CountText = kvp.Value.ToString("N0")
            }).ToList();
        }

        TotalTimeText.Text = FormatTimeSpan(stats.Duration);
        ProgressSlider.Maximum = stats.Duration.TotalSeconds;
    }

    private async void Play_Click(object sender, RoutedEventArgs e)
    {
        if (_isPlaying && _currentSessionId != null)
        {
            // Pause
            await _replayService.PauseReplayAsync(_currentSessionId);
            _isPlaying = false;
            _statusTimer.Stop();
            PlayIcon.Glyph = "\uE768";
            StatusText.Text = "Paused";
        }
        else if (_currentSessionId != null)
        {
            // Resume
            await _replayService.ResumeReplayAsync(_currentSessionId);
            _isPlaying = true;
            _statusTimer.Start();
            PlayIcon.Glyph = "\uE769";
            StatusText.Text = "Playing";
        }
        else if (!string.IsNullOrEmpty(_selectedFilePath))
        {
            // Start new replay
            await StartReplayAsync();
        }
    }

    private async System.Threading.Tasks.Task StartReplayAsync()
    {
        var speedItem = SpeedCombo.SelectedItem as ComboBoxItem;
        var speedMultiplier = double.Parse(speedItem?.Tag?.ToString() ?? "1");

        var eventTypeFilter = EventTypeFilterCombo.SelectedItem as ComboBoxItem;
        string[]? eventTypes = eventTypeFilter?.Tag is string type ? new[] { type } : null;

        var options = new ReplayOptions
        {
            FilePath = _selectedFilePath,
            SpeedMultiplier = speedMultiplier,
            PreserveTiming = PreserveTimingCheck.IsChecked == true,
            PublishToEventBus = PublishToEventBusCheck.IsChecked == true,
            EventTypes = eventTypes
        };

        var result = await _replayService.StartReplayAsync(options);
        if (result.Success)
        {
            _currentSessionId = result.SessionId;
            _isPlaying = true;
            _statusTimer.Start();
            PlayIcon.Glyph = "\uE769";
            StatusText.Text = "Playing";

            TotalTimeText.Text = FormatTimeSpan(result.EstimatedDuration);
        }
        else
        {
            StatusText.Text = "Failed";
        }
    }

    private async void Stop_Click(object sender, RoutedEventArgs e)
    {
        if (_currentSessionId != null)
        {
            await _replayService.StopReplayAsync(_currentSessionId);
            _currentSessionId = null;
            _isPlaying = false;
            _statusTimer.Stop();
            PlayIcon.Glyph = "\uE768";
            StatusText.Text = "Stopped";
            ProgressSlider.Value = 0;
            CurrentTimeText.Text = "00:00:00";
        }
    }

    private async void Rewind_Click(object sender, RoutedEventArgs e)
    {
        if (_currentSessionId != null)
        {
            var newPosition = TimeSpan.FromSeconds(Math.Max(0, ProgressSlider.Value - 10));
            await _replayService.SeekAsync(_currentSessionId, newPosition);
        }
    }

    private async void Forward_Click(object sender, RoutedEventArgs e)
    {
        if (_currentSessionId != null)
        {
            var newPosition = TimeSpan.FromSeconds(Math.Min(ProgressSlider.Maximum, ProgressSlider.Value + 10));
            await _replayService.SeekAsync(_currentSessionId, newPosition);
        }
    }

    private void ProgressSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        CurrentTimeText.Text = FormatTimeSpan(TimeSpan.FromSeconds(e.NewValue));
    }

    private async void StatusTimer_Tick(object? sender, object e)
    {
        if (_currentSessionId == null) return;

        try
        {
            var status = await _replayService.GetStatusAsync(_currentSessionId);

            EventsReplayedText.Text = status.EventsReplayed.ToString("N0");
            ProgressSlider.Value = status.Elapsed.TotalSeconds;

            if (status.State == ReplayState.Completed)
            {
                _statusTimer.Stop();
                _isPlaying = false;
                PlayIcon.Glyph = "\uE768";
                StatusText.Text = "Completed";
                _currentSessionId = null;
            }
        }
        catch
        {
            // Ignore status update errors
        }
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F1} GB";
    }

    private static string FormatTimeSpan(TimeSpan ts)
    {
        if (ts.TotalHours >= 1)
            return $"{(int)ts.TotalHours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}";
        return $"{ts.Minutes:D2}:{ts.Seconds:D2}";
    }
}

public class ReplayFileDisplay
{
    public string Path { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string DateText { get; set; } = string.Empty;
    public string EventCountText { get; set; } = string.Empty;
    public string SizeText { get; set; } = string.Empty;
}

public class ReplayEventDisplay
{
    public string TimestampText { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
}

public class EventTypeBreakdown
{
    public string EventType { get; set; } = string.Empty;
    public string CountText { get; set; } = string.Empty;
}
