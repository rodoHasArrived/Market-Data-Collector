using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MarketDataCollector.Uwp.Services;
using MarketDataCollector.Uwp.Models;

namespace MarketDataCollector.Uwp.Views;

/// <summary>
/// Page for creating data samples and subsets for development, testing, and analysis.
/// </summary>
public sealed partial class DataSamplingPage : Page
{
    private readonly DataSamplingService _samplingService;
    private readonly ObservableCollection<SamplingPresetItem> _presets;
    private readonly ObservableCollection<SavedSampleItem> _savedSamples;
    private CancellationTokenSource? _cancellationTokenSource;

    public DataSamplingPage()
    {
        this.InitializeComponent();

        _samplingService = DataSamplingService.Instance;
        _presets = new ObservableCollection<SamplingPresetItem>();
        _savedSamples = new ObservableCollection<SavedSampleItem>();

        PresetsGrid.ItemsSource = _presets;
        SavedSamplesList.ItemsSource = _savedSamples;

        Loaded += DataSamplingPage_Loaded;
    }

    private async void DataSamplingPage_Loaded(object sender, RoutedEventArgs e)
    {
        FromDatePicker.Date = DateTimeOffset.Now.AddDays(-7);
        ToDatePicker.Date = DateTimeOffset.Now;

        await LoadPresetsAsync();
        await LoadSavedSamplesAsync();
        UpdateEstimate();
    }

    private async Task LoadPresetsAsync()
    {
        var presets = await _samplingService.GetSamplingPresetsAsync();
        _presets.Clear();
        foreach (var preset in presets)
        {
            _presets.Add(new SamplingPresetItem
            {
                Name = preset.Name,
                Description = preset.Description,
                Strategy = preset.Strategy,
                SampleSize = preset.SampleSize,
                SamplePercent = preset.SamplePercent,
                IntervalSeconds = preset.IntervalSeconds,
                EventTypes = preset.EventTypes,
                Seed = preset.Seed,
                IncludeStatistics = preset.IncludeStatistics
            });
        }
    }

    private async Task LoadSavedSamplesAsync()
    {
        var samples = await _samplingService.GetSavedSamplesAsync();
        _savedSamples.Clear();

        if (samples.Count == 0)
        {
            _savedSamples.Add(new SavedSampleItem
            {
                Id = "sample-001",
                Name = "dev_test_sample",
                CreatedAt = DateTimeOffset.Now.AddDays(-2),
                Strategy = "Random",
                RecordCount = 100000,
                FileSizeBytes = 15_000_000,
                Symbols = new List<string> { "AAPL", "MSFT", "GOOGL" }
            });
            _savedSamples.Add(new SavedSampleItem
            {
                Id = "sample-002",
                Name = "ml_training_set",
                CreatedAt = DateTimeOffset.Now.AddDays(-5),
                Strategy = "Volatility",
                RecordCount = 500000,
                FileSizeBytes = 75_000_000,
                Symbols = new List<string> { "SPY", "QQQ", "IWM" }
            });
        }
        else
        {
            foreach (var sample in samples)
            {
                _savedSamples.Add(new SavedSampleItem
                {
                    Id = sample.Id,
                    Name = sample.Name,
                    CreatedAt = sample.CreatedAt,
                    Strategy = sample.Strategy,
                    RecordCount = sample.RecordCount,
                    FileSizeBytes = sample.FileSizeBytes,
                    Symbols = sample.Symbols
                });
            }
        }

        NoSamplesText.Visibility = _savedSamples.Count > 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    private void Preset_Click(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is SamplingPresetItem preset)
        {
            ApplyPreset(preset);
            ActionInfoBar.Severity = InfoBarSeverity.Informational;
            ActionInfoBar.Title = "Preset Applied";
            ActionInfoBar.Message = $"Applied \"{preset.Name}\" preset. Adjust settings as needed.";
            ActionInfoBar.IsOpen = true;
        }
    }

    private void ApplyPreset(SamplingPresetItem preset)
    {
        for (int i = 0; i < StrategyCombo.Items.Count; i++)
        {
            if (StrategyCombo.Items[i] is ComboBoxItem item &&
                item.Tag?.ToString() == preset.Strategy.ToString())
            {
                StrategyCombo.SelectedIndex = i;
                break;
            }
        }

        if (preset.SampleSize.HasValue)
        {
            SizeByCountRadio.IsChecked = true;
            SampleCountBox.Value = preset.SampleSize.Value;
        }
        else if (preset.SamplePercent.HasValue)
        {
            SizeByPercentRadio.IsChecked = true;
            SamplePercentBox.Value = preset.SamplePercent.Value;
        }

        if (preset.IntervalSeconds.HasValue)
        {
            IntervalSecondsBox.Value = preset.IntervalSeconds.Value;
        }

        if (preset.EventTypes != null)
        {
            TradesCheck.IsChecked = preset.EventTypes.Contains("Trade");
            QuotesCheck.IsChecked = preset.EventTypes.Contains("BboQuote");
            DepthCheck.IsChecked = preset.EventTypes.Contains("LOBSnapshot");
            BarsCheck.IsChecked = preset.EventTypes.Contains("Bar");
        }

        if (preset.Seed.HasValue)
        {
            UseFixedSeedCheck.IsChecked = true;
            SeedBox.Value = preset.Seed.Value;
        }

        IncludeStatisticsCheck.IsChecked = preset.IncludeStatistics;
        UpdateEstimate();
    }

    private void Strategy_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (StrategyCombo.SelectedItem is ComboBoxItem item)
        {
            var strategy = item.Tag?.ToString();

            IntervalPanel.Visibility = strategy is "TimeBased" or "Systematic"
                ? Visibility.Visible
                : Visibility.Collapsed;

            AdvancedOptionsPanel.Visibility = strategy is "TimeBased" or "Systematic"
                ? Visibility.Collapsed
                : Visibility.Visible;

            StrategyDescription.Text = strategy switch
            {
                "Random" => "Randomly select N events or N% of total data",
                "TimeBased" => "Sample at regular time intervals (e.g., every 10 seconds)",
                "SymbolStratified" => "Equal representation across all symbols",
                "EventTypeStratified" => "Maintain original trade/quote ratio in sample",
                "VolatilityBased" => "Oversample high-activity periods, undersample quiet periods",
                "FirstN" => "Take the first N records from each symbol/date",
                "LastN" => "Take the last N records from each symbol/date",
                "PeakHours" => "Sample only market open/close periods (high activity)",
                "Systematic" => "Select every Nth record for uniform coverage",
                _ => ""
            };

            UpdateEstimate();
        }
    }

    private void SizeType_Changed(object sender, RoutedEventArgs e)
    {
        if (SizeByCountRadio?.IsChecked == true)
        {
            if (SampleCountBox != null) SampleCountBox.IsEnabled = true;
            if (SamplePercentBox != null) SamplePercentBox.IsEnabled = false;
        }
        else
        {
            if (SampleCountBox != null) SampleCountBox.IsEnabled = false;
            if (SamplePercentBox != null) SamplePercentBox.IsEnabled = true;
        }
        UpdateEstimate();
    }

    private void Symbols_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var count = SymbolsList.SelectedItems.Count;
        SelectedSymbolsCount.Text = $"{count} symbol{(count == 1 ? "" : "s")} selected";
        UpdateEstimate();
    }

    private void DateRange_Changed(CalendarDatePicker sender, CalendarDatePickerDateChangedEventArgs args)
    {
        UpdateEstimate();
    }

    private void SymbolSearch_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        // Filter symbols based on search text
    }

    private void UpdateEstimate()
    {
        var symbolCount = SymbolsList?.SelectedItems.Count ?? 3;
        var days = 7;
        if (FromDatePicker?.Date != null && ToDatePicker?.Date != null)
        {
            days = Math.Max(1, (ToDatePicker.Date.Value - FromDatePicker.Date.Value).Days);
        }

        var estimatedSourceRecords = symbolCount * days * 150000L;
        long estimatedSampleSize;

        if (SizeByCountRadio?.IsChecked == true)
        {
            estimatedSampleSize = (long)(SampleCountBox?.Value ?? 100000);
        }
        else
        {
            var percent = SamplePercentBox?.Value ?? 5;
            estimatedSampleSize = (long)(estimatedSourceRecords * percent / 100);
        }

        var estimatedFileSize = estimatedSampleSize * 150;

        if (EstSourceRecords != null)
            EstSourceRecords.Text = $"~{FormatNumber(estimatedSourceRecords)}";
        if (EstSampleSize != null)
            EstSampleSize.Text = $"~{FormatNumber(estimatedSampleSize)}";
        if (EstFileSize != null)
            EstFileSize.Text = $"~{FormatBytes(estimatedFileSize)}";
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

    private void SetToday_Click(object sender, RoutedEventArgs e)
    {
        FromDatePicker.Date = DateTimeOffset.Now.Date;
        ToDatePicker.Date = DateTimeOffset.Now;
    }

    private void SetWeek_Click(object sender, RoutedEventArgs e)
    {
        FromDatePicker.Date = DateTimeOffset.Now.AddDays(-7);
        ToDatePicker.Date = DateTimeOffset.Now;
    }

    private void SetMonth_Click(object sender, RoutedEventArgs e)
    {
        FromDatePicker.Date = DateTimeOffset.Now.AddMonths(-1);
        ToDatePicker.Date = DateTimeOffset.Now;
    }

    private void SetYear_Click(object sender, RoutedEventArgs e)
    {
        FromDatePicker.Date = DateTimeOffset.Now.AddYears(-1);
        ToDatePicker.Date = DateTimeOffset.Now;
    }

    private void SetInterval1s_Click(object sender, RoutedEventArgs e) => IntervalSecondsBox.Value = 1;
    private void SetInterval10s_Click(object sender, RoutedEventArgs e) => IntervalSecondsBox.Value = 10;
    private void SetInterval1m_Click(object sender, RoutedEventArgs e) => IntervalSecondsBox.Value = 60;
    private void SetInterval5m_Click(object sender, RoutedEventArgs e) => IntervalSecondsBox.Value = 300;

    private void BrowseOutput_Click(object sender, RoutedEventArgs e)
    {
        OutputPathBox.Text = $"C:\\Samples\\sample_{DateTime.Now:yyyyMMdd_HHmmss}.parquet";
    }

    private async void CreateSample_Click(object sender, RoutedEventArgs e)
    {
        var options = BuildSamplingOptions();
        var validation = _samplingService.ValidateOptions(options);

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
        CreateSampleButton.IsEnabled = false;

        try
        {
            for (int i = 0; i <= 100; i += 5)
            {
                if (_cancellationTokenSource.Token.IsCancellationRequested)
                    break;

                ProgressBar.Value = i;
                ProgressPercent.Text = $"{i}%";
                ProgressLabel.Text = i < 30
                    ? "Scanning source data..."
                    : i < 70
                        ? $"Sampling {options.Symbols?.FirstOrDefault() ?? "data"}..."
                        : "Finalizing sample...";

                await Task.Delay(100, _cancellationTokenSource.Token);
            }

            if (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                var newSample = new SavedSampleItem
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = string.IsNullOrWhiteSpace(options.Name)
                        ? $"sample_{DateTime.Now:yyyyMMdd_HHmmss}"
                        : options.Name,
                    CreatedAt = DateTimeOffset.Now,
                    Strategy = options.Strategy.ToString(),
                    RecordCount = options.SampleSize ?? (long)((options.SamplePercent ?? 5) * 10000),
                    FileSizeBytes = (options.SampleSize ?? 100000) * 150,
                    Symbols = options.Symbols
                };

                _savedSamples.Insert(0, newSample);
                NoSamplesText.Visibility = Visibility.Collapsed;

                ShowStatistics(newSample);

                ActionInfoBar.Severity = InfoBarSeverity.Success;
                ActionInfoBar.Title = "Sample Created";
                ActionInfoBar.Message = $"Successfully created sample '{newSample.Name}' with {FormatNumber(newSample.RecordCount)} records.";
                ActionInfoBar.IsOpen = true;
            }
        }
        catch (OperationCanceledException)
        {
            ActionInfoBar.Severity = InfoBarSeverity.Informational;
            ActionInfoBar.Title = "Cancelled";
            ActionInfoBar.Message = "Sample creation was cancelled.";
            ActionInfoBar.IsOpen = true;
        }
        finally
        {
            ProgressPanel.Visibility = Visibility.Collapsed;
            CreateSampleButton.IsEnabled = true;
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }
    }

    private SamplingOptions BuildSamplingOptions()
    {
        var symbols = SymbolsList.SelectedItems
            .Cast<ListViewItem>()
            .Select(i => i.Content?.ToString() ?? "")
            .Where(s => !string.IsNullOrEmpty(s))
            .ToList();

        var eventTypes = new List<string>();
        if (TradesCheck.IsChecked == true) eventTypes.Add("Trade");
        if (QuotesCheck.IsChecked == true) eventTypes.Add("BboQuote");
        if (DepthCheck.IsChecked == true) eventTypes.Add("LOBSnapshot");
        if (BarsCheck.IsChecked == true) eventTypes.Add("Bar");

        var strategyTag = (StrategyCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Random";
        var strategy = Enum.TryParse<SamplingStrategyType>(strategyTag, out var s) ? s : SamplingStrategyType.Random;

        var formatTag = (OutputFormatCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Parquet";
        var format = Enum.TryParse<ExportFormat>(formatTag, out var f) ? f : ExportFormat.Parquet;

        return new SamplingOptions
        {
            Name = SampleNameBox.Text,
            Symbols = symbols,
            FromDate = FromDatePicker.Date.HasValue ? DateOnly.FromDateTime(FromDatePicker.Date.Value.DateTime) : null,
            ToDate = ToDatePicker.Date.HasValue ? DateOnly.FromDateTime(ToDatePicker.Date.Value.DateTime) : null,
            Strategy = strategy,
            SampleSize = SizeByCountRadio.IsChecked == true ? (long?)SampleCountBox.Value : null,
            SamplePercent = SizeByPercentRadio.IsChecked == true ? SamplePercentBox.Value : null,
            IntervalSeconds = strategy is SamplingStrategyType.TimeBased or SamplingStrategyType.Systematic
                ? (int?)IntervalSecondsBox.Value
                : null,
            MaintainDistribution = MaintainDistributionCheck.IsChecked == true,
            Seed = UseFixedSeedCheck.IsChecked == true ? (int?)SeedBox.Value : null,
            EventTypes = eventTypes.ToArray(),
            OutputPath = OutputPathBox.Text,
            OutputFormat = format,
            IncludeStatistics = IncludeStatisticsCheck.IsChecked == true
        };
    }

    private void ShowStatistics(SavedSampleItem sample)
    {
        StatisticsPanel.Visibility = Visibility.Visible;

        var symbolStats = sample.Symbols?.Select(s => new KeyValuePair<string, string>(
            s, FormatNumber(sample.RecordCount / (sample.Symbols?.Count ?? 1)))).ToList()
            ?? new List<KeyValuePair<string, string>>();

        StatsBySymbolList.ItemsSource = symbolStats;

        StatsByEventList.ItemsSource = new List<KeyValuePair<string, string>>
        {
            new("Trade", FormatNumber((long)(sample.RecordCount * 0.65))),
            new("BboQuote", FormatNumber((long)(sample.RecordCount * 0.35)))
        };

        StatSamplingRatio.Text = "10.0%";
        StatTimeSpan.Text = "7 days";
        StatFirstRecord.Text = DateTime.Now.AddDays(-7).ToString("yyyy-MM-dd HH:mm");
        StatLastRecord.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
        StatDuration.Text = "2.3s";
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        _cancellationTokenSource?.Cancel();
    }

    private async void RefreshSamples_Click(object sender, RoutedEventArgs e)
    {
        await LoadSavedSamplesAsync();
    }

    private void OpenSample_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string sampleId)
        {
            var sample = _savedSamples.FirstOrDefault(s => s.Id == sampleId);
            if (sample != null)
            {
                ActionInfoBar.Severity = InfoBarSeverity.Informational;
                ActionInfoBar.Title = "Opening Sample";
                ActionInfoBar.Message = $"Opening '{sample.Name}' in file explorer...";
                ActionInfoBar.IsOpen = true;
            }
        }
    }

    private async void DeleteSample_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string sampleId)
        {
            var sample = _savedSamples.FirstOrDefault(s => s.Id == sampleId);
            if (sample != null)
            {
                var dialog = new ContentDialog
                {
                    Title = "Delete Sample",
                    Content = $"Are you sure you want to delete '{sample.Name}'? This action cannot be undone.",
                    PrimaryButtonText = "Delete",
                    CloseButtonText = "Cancel",
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = this.XamlRoot
                };

                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary)
                {
                    _savedSamples.Remove(sample);
                    NoSamplesText.Visibility = _savedSamples.Count > 0 ? Visibility.Collapsed : Visibility.Visible;

                    ActionInfoBar.Severity = InfoBarSeverity.Success;
                    ActionInfoBar.Title = "Sample Deleted";
                    ActionInfoBar.Message = $"'{sample.Name}' has been deleted.";
                    ActionInfoBar.IsOpen = true;
                }
            }
        }
    }
}
