using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using MarketDataCollector.Uwp.Services;
using Windows.Storage;
using Windows.System;

namespace MarketDataCollector.Uwp.Views;

/// <summary>
/// Page for viewing storage details and data quality for a specific symbol.
/// Provides data file management, gap detection, and quick actions.
/// </summary>
public sealed partial class SymbolStoragePage : Page
{
    private readonly StorageService _storageService;
    private readonly BackfillService _backfillService;
    private readonly DataQualityService _dataQualityService;
    private readonly NotificationService _notificationService;
    private string _currentSymbol = string.Empty;
    private bool _isLoading;

    public ObservableCollection<DataFileInfo> DataFiles { get; } = new();
    public ObservableCollection<DataGapInfo> DataGaps { get; } = new();

    public SymbolStoragePage()
    {
        this.InitializeComponent();
        _storageService = StorageService.Instance;
        _backfillService = BackfillService.Instance;
        _dataQualityService = DataQualityService.Instance;
        _notificationService = NotificationService.Instance;

        FilesListView.ItemsSource = DataFiles;
        GapsListView.ItemsSource = DataGaps;

        Loaded += SymbolStoragePage_Loaded;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is string symbol && !string.IsNullOrWhiteSpace(symbol))
        {
            _currentSymbol = symbol.ToUpperInvariant();
        }
    }

    private async void SymbolStoragePage_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadSymbolDataAsync();
    }

    private async Task LoadSymbolDataAsync()
    {
        if (string.IsNullOrWhiteSpace(_currentSymbol))
        {
            ShowError("No symbol specified", "Please select a symbol to view its storage details.");
            return;
        }

        SetLoading(true, $"Loading data for {_currentSymbol}...");

        try
        {
            // Update UI with symbol name
            SymbolTitle.Text = $"{_currentSymbol} Storage";
            SymbolBadgeText.Text = _currentSymbol;

            // Load symbol info
            var symbolInfo = await _storageService.GetSymbolInfoAsync(_currentSymbol);
            if (symbolInfo != null)
            {
                ExchangeText.Text = symbolInfo.Exchange ?? "Unknown";
                ProviderText.Text = symbolInfo.Provider ?? "Unknown";
                FirstDataText.Text = symbolInfo.FirstDataPoint?.ToString("yyyy-MM-dd HH:mm") ?? "--";
                LastDataText.Text = symbolInfo.LastDataPoint?.ToString("yyyy-MM-dd HH:mm") ?? "--";

                if (symbolInfo.FirstDataPoint.HasValue && symbolInfo.LastDataPoint.HasValue)
                {
                    var duration = symbolInfo.LastDataPoint.Value - symbolInfo.FirstDataPoint.Value;
                    CollectionDurationText.Text = FormatDuration(duration);
                }

                UpdateSubscriptionStatus(symbolInfo.IsSubscribed);
            }

            // Load storage stats
            var stats = await _storageService.GetSymbolStorageStatsAsync(_currentSymbol);
            if (stats != null)
            {
                TotalEventsText.Text = FormatNumber(stats.TotalEvents);
                StorageSizeText.Text = FormatBytes(stats.TotalSizeBytes);
                DataQualityText.Text = $"{stats.DataQuality:F1}%";

                // Update quality color
                DataQualityText.Foreground = stats.DataQuality >= 99
                    ? (Brush)Resources["SuccessColorBrush"]
                    : stats.DataQuality >= 95
                        ? (Brush)Resources["WarningColorBrush"]
                        : (Brush)Resources["ErrorColorBrush"];
            }

            // Load data files
            await LoadDataFilesAsync();

            // Load data gaps
            await LoadDataGapsAsync();

            // Update timeline
            UpdateTimeline();

            ClearError();
        }
        catch (Exception ex)
        {
            ShowError("Failed to load symbol data", ex.Message);
        }
        finally
        {
            SetLoading(false);
        }
    }

    private async Task LoadDataFilesAsync()
    {
        DataFiles.Clear();

        var files = await _storageService.GetSymbolFilesAsync(_currentSymbol);
        foreach (var file in files)
        {
            DataFiles.Add(file);
        }

        FileCountText.Text = $"{DataFiles.Count} file{(DataFiles.Count != 1 ? "s" : "")}";

        // Show/hide empty state
        NoFilesPanel.Visibility = DataFiles.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        FilesListView.Visibility = DataFiles.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private async Task LoadDataGapsAsync()
    {
        DataGaps.Clear();

        var gaps = await _dataQualityService.GetDataGapsAsync(_currentSymbol);
        foreach (var gap in gaps)
        {
            DataGaps.Add(gap);
        }

        DataGapsText.Text = DataGaps.Count.ToString();
        GapsPanel.Visibility = DataGaps.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        // Update gap count color
        DataGapsText.Foreground = DataGaps.Count == 0
            ? (Brush)Resources["SuccessColorBrush"]
            : DataGaps.Count <= 2
                ? (Brush)Resources["WarningColorBrush"]
                : (Brush)Resources["ErrorColorBrush"];
    }

    private void UpdateTimeline()
    {
        // This would calculate actual coverage and update the timeline bar
        // For now, show placeholder
        TimelineStartText.Text = "2024-01-01";
        TimelineEndText.Text = DateTime.Now.ToString("yyyy-MM-dd");
        CoverageBar.Width = 200; // Would be calculated based on actual coverage
    }

    private void UpdateSubscriptionStatus(bool isSubscribed)
    {
        SubscriptionStatusText.Text = isSubscribed ? "Active" : "Inactive";
        SubscriptionStatusIndicator.Fill = isSubscribed
            ? (Brush)Resources["SuccessColorBrush"]
            : (Brush)Resources["ErrorColorBrush"];
    }

    #region Event Handlers

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        await LoadSymbolDataAsync();
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        // Navigate to export page with symbol pre-selected
        Frame.Navigate(typeof(DataExportPage), _currentSymbol);
    }

    private void Backfill_Click(object sender, RoutedEventArgs e)
    {
        // Navigate to backfill page with symbol pre-selected
        Frame.Navigate(typeof(BackfillPage), _currentSymbol);
    }

    private async void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var folderPath = await _storageService.GetSymbolFolderPathAsync(_currentSymbol);
            if (!string.IsNullOrEmpty(folderPath))
            {
                var folder = await StorageFolder.GetFolderFromPathAsync(folderPath);
                await Launcher.LaunchFolderAsync(folder);
            }
            else
            {
                await _notificationService.NotifyWarningAsync(
                    "Folder Not Found",
                    $"No data folder exists for {_currentSymbol}");
            }
        }
        catch (Exception ex)
        {
            await _notificationService.NotifyErrorAsync(
                "Failed to Open Folder",
                ex.Message);
        }
    }

    private void ViewLiveData_Click(object sender, RoutedEventArgs e)
    {
        Frame.Navigate(typeof(LiveDataViewerPage), _currentSymbol);
    }

    private async void VerifyIntegrity_Click(object sender, RoutedEventArgs e)
    {
        SetLoading(true, "Verifying data integrity...");

        try
        {
            var result = await _dataQualityService.VerifySymbolIntegrityAsync(_currentSymbol);

            if (result.IsValid)
            {
                await _notificationService.NotifyAsync(
                    "Integrity Check Passed",
                    $"All data for {_currentSymbol} is valid",
                    NotificationType.Success);
            }
            else
            {
                await _notificationService.NotifyAsync(
                    "Integrity Issues Found",
                    $"Found {result.Issues.Count} issue(s) with {_currentSymbol} data",
                    NotificationType.Warning);
            }

            // Reload gaps
            await LoadDataGapsAsync();
        }
        catch (Exception ex)
        {
            ShowError("Integrity check failed", ex.Message);
        }
        finally
        {
            SetLoading(false);
        }
    }

    private async void FillGap_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is DataGapInfo gap)
        {
            SetLoading(true, $"Filling gap from {gap.StartDate:yyyy-MM-dd} to {gap.EndDate:yyyy-MM-dd}...");

            try
            {
                await _backfillService.FillGapAsync(_currentSymbol, gap.StartDate, gap.EndDate);

                await _notificationService.NotifyAsync(
                    "Gap Fill Started",
                    $"Backfilling {_currentSymbol} from {gap.StartDate:yyyy-MM-dd} to {gap.EndDate:yyyy-MM-dd}",
                    NotificationType.Info);

                // Reload after a delay to show progress
                await Task.Delay(1000);
                await LoadDataGapsAsync();
            }
            catch (Exception ex)
            {
                ShowError("Failed to fill gap", ex.Message);
            }
            finally
            {
                SetLoading(false);
            }
        }
    }

    #endregion

    #region UI Helpers

    private void SetLoading(bool isLoading, string? message = null)
    {
        _isLoading = isLoading;
        LoadingOverlay.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;

        if (!string.IsNullOrEmpty(message))
        {
            LoadingText.Text = message;
        }

        // Disable action buttons while loading
        RefreshButton.IsEnabled = !isLoading;
        ExportButton.IsEnabled = !isLoading;
        BackfillButton.IsEnabled = !isLoading;
    }

    private void ShowError(string title, string message)
    {
        ErrorInfoBar.Title = title;
        ErrorInfoBar.Message = message;
        ErrorInfoBar.IsOpen = true;
    }

    private void ClearError()
    {
        ErrorInfoBar.IsOpen = false;
    }

    private static string FormatNumber(long number)
    {
        return number >= 1_000_000
            ? $"{number / 1_000_000.0:N1}M"
            : number >= 1_000
                ? $"{number / 1_000.0:N1}K"
                : number.ToString("N0");
    }

    private static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:F1} {sizes[order]}";
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalDays >= 365)
            return $"{duration.TotalDays / 365:F1} years";
        if (duration.TotalDays >= 30)
            return $"{duration.TotalDays / 30:F1} months";
        if (duration.TotalDays >= 1)
            return $"{duration.TotalDays:F0} days";
        if (duration.TotalHours >= 1)
            return $"{duration.TotalHours:F1} hours";
        return $"{duration.TotalMinutes:F0} minutes";
    }

    #endregion
}

/// <summary>
/// Information about a data file for a symbol.
/// </summary>
public class DataFileInfo
{
    public string FileName { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public string FileSize { get; set; } = string.Empty;
    public string ModifiedDate { get; set; } = string.Empty;
    public string EventCount { get; set; } = string.Empty;
    public string FileIcon { get; set; } = "\uE7C3"; // Document icon
    public Brush? TypeBackground { get; set; }
}

/// <summary>
/// Information about a data gap.
/// </summary>
public class DataGapInfo
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int MissingBars { get; set; }
    public string Description => $"{StartDate:yyyy-MM-dd} to {EndDate:yyyy-MM-dd} ({MissingBars} bars missing)";
}
