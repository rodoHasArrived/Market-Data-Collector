using System;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Storage.Pickers;
using Windows.System;
using MarketDataCollector.Uwp.Services;

namespace MarketDataCollector.Uwp.Views;

/// <summary>
/// Page for QuantConnect Lean integration and backtesting.
/// </summary>
public sealed partial class LeanIntegrationPage : Page
{
    private readonly LeanIntegrationService _leanService;
    private readonly DispatcherTimer _backtestTimer;
    private string? _currentBacktestId;

    public LeanIntegrationPage()
    {
        this.InitializeComponent();
        _leanService = LeanIntegrationService.Instance;

        _backtestTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _backtestTimer.Tick += BacktestTimer_Tick;

        Loaded += LeanIntegrationPage_Loaded;
        Unloaded += LeanIntegrationPage_Unloaded;
    }

    private async void LeanIntegrationPage_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadStatusAsync();
        await LoadConfigurationAsync();
        await LoadAlgorithmsAsync();
        await LoadBacktestHistoryAsync();
    }

    private void LeanIntegrationPage_Unloaded(object sender, RoutedEventArgs e)
    {
        _backtestTimer.Stop();
    }

    private async System.Threading.Tasks.Task LoadStatusAsync()
    {
        try
        {
            var status = await _leanService.GetStatusAsync();

            StatusIndicator.Fill = new SolidColorBrush(
                status.IsConfigured ? Windows.UI.Color.FromArgb(255, 72, 187, 120) : Windows.UI.Color.FromArgb(255, 245, 101, 101));

            StatusText.Text = status.IsConfigured
                ? (status.IsInstalled ? "Lean Integration Active" : "Lean Not Found")
                : "Not Configured";

            StatusDetailsText.Text = $"Last sync: {(status.LastSync?.ToString("g") ?? "Never")} | Symbols synced: {status.SymbolsSynced}";

            SymbolsSyncedText.Text = status.SymbolsSynced.ToString();
            LastSyncText.Text = status.LastSync?.ToString("g") ?? "Never";
        }
        catch (Exception ex)
        {
            StatusText.Text = "Connection Error";
            StatusDetailsText.Text = ex.Message;
        }
    }

    private async System.Threading.Tasks.Task LoadConfigurationAsync()
    {
        try
        {
            var config = await _leanService.GetConfigurationAsync();

            LeanPathBox.Text = config.LeanPath ?? "";
            DataPathBox.Text = config.DataPath ?? "";
            AutoSyncToggle.IsOn = config.AutoSync;
        }
        catch
        {
            // Use defaults
        }
    }

    private async System.Threading.Tasks.Task LoadAlgorithmsAsync()
    {
        try
        {
            var result = await _leanService.GetAlgorithmsAsync();
            if (result.Success && result.Algorithms.Count > 0)
            {
                AlgorithmCombo.ItemsSource = result.Algorithms.Select(a => new ComboBoxItem
                {
                    Content = a.Name,
                    Tag = a.Path
                }).ToList();
            }
        }
        catch
        {
            // Algorithms not available
        }
    }

    private async System.Threading.Tasks.Task LoadBacktestHistoryAsync()
    {
        try
        {
            var result = await _leanService.GetBacktestHistoryAsync();
            if (result.Success && result.Backtests.Count > 0)
            {
                RecentBacktestsList.ItemsSource = result.Backtests.Select(b => new BacktestDisplayInfo
                {
                    BacktestId = b.BacktestId,
                    AlgorithmName = b.AlgorithmName,
                    DateText = b.StartedAt.ToString("g"),
                    ReturnText = b.TotalReturn.HasValue ? $"{b.TotalReturn:+0.0%;-0.0%}" : "N/A",
                    ReturnColor = new SolidColorBrush(
                        b.TotalReturn >= 0 ? Windows.UI.Color.FromArgb(255, 72, 187, 120) : Windows.UI.Color.FromArgb(255, 245, 101, 101))
                }).ToList();
                NoBacktestsText.Visibility = Visibility.Collapsed;
            }
            else
            {
                NoBacktestsText.Visibility = Visibility.Visible;
            }
        }
        catch
        {
            NoBacktestsText.Visibility = Visibility.Visible;
        }
    }

    private async void VerifyInstallation_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var result = await _leanService.VerifyInstallationAsync();

            var dialog = new ContentDialog
            {
                Title = result.Success ? "Lean Installation Valid" : "Lean Installation Issues",
                Content = result.Success
                    ? $"Lean {result.Version} found at {result.LeanPath}"
                    : string.Join("\n", result.Errors),
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };

            await dialog.ShowAsync();
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Verification Failed", ex.Message);
        }
    }

    private async void BrowseLeanPath_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FolderPicker();
        picker.SuggestedStartLocation = PickerLocationId.ComputerFolder;
        picker.FileTypeFilter.Add("*");

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        var folder = await picker.PickSingleFolderAsync();
        if (folder != null)
        {
            LeanPathBox.Text = folder.Path;
        }
    }

    private async void BrowseDataPath_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FolderPicker();
        picker.SuggestedStartLocation = PickerLocationId.ComputerFolder;
        picker.FileTypeFilter.Add("*");

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        var folder = await picker.PickSingleFolderAsync();
        if (folder != null)
        {
            DataPathBox.Text = folder.Path;
        }
    }

    private async void SaveConfig_Click(object sender, RoutedEventArgs e)
    {
        var config = new LeanConfigurationUpdate
        {
            LeanPath = LeanPathBox.Text,
            DataPath = DataPathBox.Text,
            AutoSync = AutoSyncToggle.IsOn
        };

        var success = await _leanService.UpdateConfigurationAsync(config);
        if (success)
        {
            await LoadStatusAsync();
        }
    }

    private async void SyncData_Click(object sender, RoutedEventArgs e)
    {
        SyncProgress.IsActive = true;
        SyncDataButton.IsEnabled = false;
        SyncStatusText.Text = "Syncing...";

        try
        {
            var options = new DataSyncOptions
            {
                Symbols = string.IsNullOrWhiteSpace(SyncSymbolsBox.Text)
                    ? null
                    : SyncSymbolsBox.Text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
                FromDate = SyncFromDate.Date?.Date is DateTime from ? DateOnly.FromDateTime(from) : null,
                ToDate = SyncToDate.Date?.Date is DateTime to ? DateOnly.FromDateTime(to) : null,
                Resolution = (ResolutionCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Minute",
                Overwrite = OverwriteSyncCheck.IsChecked == true
            };

            var result = await _leanService.SyncDataAsync(options);

            SyncStatusText.Text = result.Success
                ? $"Synced {result.SymbolsSynced} symbols, {result.FilesCreated} files"
                : string.Join(", ", result.Errors);

            await LoadStatusAsync();
        }
        catch (Exception ex)
        {
            SyncStatusText.Text = $"Error: {ex.Message}";
        }
        finally
        {
            SyncProgress.IsActive = false;
            SyncDataButton.IsEnabled = true;
        }
    }

    private async void RunBacktest_Click(object sender, RoutedEventArgs e)
    {
        var selectedAlgorithm = AlgorithmCombo.SelectedItem as ComboBoxItem;
        if (selectedAlgorithm?.Tag is not string algorithmPath)
        {
            await ShowErrorAsync("Error", "Please select an algorithm");
            return;
        }

        BacktestProgress.IsActive = true;
        RunBacktestButton.IsEnabled = false;
        StopBacktestButton.Visibility = Visibility.Visible;
        BacktestProgressPanel.Visibility = Visibility.Visible;

        try
        {
            var options = new BacktestOptions
            {
                AlgorithmPath = algorithmPath,
                AlgorithmName = selectedAlgorithm.Content?.ToString(),
                StartDate = BacktestStartDate.Date?.Date is DateTime start ? DateOnly.FromDateTime(start) : null,
                EndDate = BacktestEndDate.Date?.Date is DateTime end ? DateOnly.FromDateTime(end) : null,
                InitialCapital = (decimal)InitialCapitalBox.Value
            };

            var result = await _leanService.StartBacktestAsync(options);

            if (result.Success)
            {
                _currentBacktestId = result.BacktestId;
                _backtestTimer.Start();
            }
            else
            {
                await ShowErrorAsync("Backtest Failed", result.Error ?? "Unknown error");
                ResetBacktestUI();
            }
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Backtest Failed", ex.Message);
            ResetBacktestUI();
        }
    }

    private async void StopBacktest_Click(object sender, RoutedEventArgs e)
    {
        if (_currentBacktestId != null)
        {
            await _leanService.StopBacktestAsync(_currentBacktestId);
            _backtestTimer.Stop();
            ResetBacktestUI();
        }
    }

    private async void BacktestTimer_Tick(object? sender, object e)
    {
        if (_currentBacktestId == null) return;

        try
        {
            var status = await _leanService.GetBacktestStatusAsync(_currentBacktestId);

            BacktestProgressText.Text = $"Processing {status.CurrentDate:d}...";
            BacktestProgressPercent.Text = $"{status.Progress:F0}%";
            BacktestProgressBar.Value = status.Progress;

            if (status.State == BacktestState.Completed)
            {
                _backtestTimer.Stop();
                await ShowBacktestResultsAsync(_currentBacktestId);
                ResetBacktestUI();
            }
            else if (status.State == BacktestState.Failed)
            {
                _backtestTimer.Stop();
                await ShowErrorAsync("Backtest Failed", status.Error ?? "Unknown error");
                ResetBacktestUI();
            }
        }
        catch
        {
            // Ignore status errors
        }
    }

    private async System.Threading.Tasks.Task ShowBacktestResultsAsync(string backtestId)
    {
        var results = await _leanService.GetBacktestResultsAsync(backtestId);

        TotalReturnText.Text = $"{results.TotalReturn:+0.0%;-0.0%}";
        TotalReturnText.Foreground = new SolidColorBrush(
            results.TotalReturn >= 0 ? Windows.UI.Color.FromArgb(255, 72, 187, 120) : Windows.UI.Color.FromArgb(255, 245, 101, 101));

        SharpeRatioText.Text = $"{results.SharpeRatio:F2}";
        MaxDrawdownText.Text = $"{results.MaxDrawdown:0.0%}";
        TotalTradesText.Text = results.TotalTrades.ToString();

        ResultsCard.Visibility = Visibility.Visible;
        await LoadBacktestHistoryAsync();
    }

    private void ResetBacktestUI()
    {
        BacktestProgress.IsActive = false;
        RunBacktestButton.IsEnabled = true;
        StopBacktestButton.Visibility = Visibility.Collapsed;
        BacktestProgressPanel.Visibility = Visibility.Collapsed;
        _currentBacktestId = null;
    }

    private void ViewResults_Click(object sender, RoutedEventArgs e)
    {
        // Navigate to detailed results view
    }

    private async void OpenLeanFolder_Click(object sender, RoutedEventArgs e)
    {
        var path = LeanPathBox.Text;
        if (!string.IsNullOrEmpty(path))
        {
            await Launcher.LaunchFolderPathAsync(path);
        }
    }

    private async void RefreshAlgorithms_Click(object sender, RoutedEventArgs e)
    {
        await LoadAlgorithmsAsync();
    }

    private async void ViewDocs_Click(object sender, RoutedEventArgs e)
    {
        await Launcher.LaunchUriAsync(new Uri("https://www.quantconnect.com/docs"));
    }

    private async System.Threading.Tasks.Task ShowErrorAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = "OK",
            XamlRoot = this.XamlRoot
        };
        await dialog.ShowAsync();
    }
}

public class BacktestDisplayInfo
{
    public string BacktestId { get; set; } = string.Empty;
    public string AlgorithmName { get; set; } = string.Empty;
    public string DateText { get; set; } = string.Empty;
    public string ReturnText { get; set; } = string.Empty;
    public SolidColorBrush? ReturnColor { get; set; }
}
