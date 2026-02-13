using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using MarketDataCollector.Ui.Services;
using MarketDataCollector.Wpf.ViewModels;

namespace MarketDataCollector.Wpf.Views;

public partial class LeanIntegrationPage : Page
{
    private readonly LeanIntegrationService _leanService = LeanIntegrationService.Instance;
    private readonly DispatcherTimer _backtestTimer;
    private string? _currentBacktestId;

    public LeanIntegrationPage()
    {
        InitializeComponent();
        _backtestTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _backtestTimer.Tick += BacktestTimer_Tick;
    }

    private async void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        await LoadStatusAsync();
        await LoadConfigurationAsync();
        await LoadAlgorithmsAsync();
        await LoadBacktestHistoryAsync();
    }

    private async System.Threading.Tasks.Task LoadStatusAsync()
    {
        try
        {
            var status = await _leanService.GetStatusAsync();
            StatusIndicator.Fill = new SolidColorBrush(
                status.IsConfigured ? Color.FromRgb(72, 187, 120) : Color.FromRgb(245, 101, 101));
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
            AutoSyncToggle.IsChecked = config.AutoSync;
        }
        catch { /* Use defaults */ }
    }

    private async System.Threading.Tasks.Task LoadAlgorithmsAsync()
    {
        try
        {
            var result = await _leanService.GetAlgorithmsAsync();
            if (result.Success && result.Algorithms.Count > 0)
            {
                AlgorithmCombo.ItemsSource = result.Algorithms.Select(a => new ComboBoxItem { Content = a.Name, Tag = a.Path }).ToList();
            }
        }
        catch { /* Algorithms not available */ }
    }

    private async System.Threading.Tasks.Task LoadBacktestHistoryAsync()
    {
        try
        {
            var result = await _leanService.GetBacktestHistoryAsync();
            if (result.Success && result.Backtests.Count > 0)
            {
                RecentBacktestsList.ItemsSource = result.Backtests.Select(b => new WpfBacktestDisplayInfo
                {
                    AlgorithmName = b.AlgorithmName,
                    DateText = b.StartedAt.ToString("g"),
                    ReturnText = b.TotalReturn.HasValue ? $"{b.TotalReturn:+0.0%;-0.0%}" : "N/A",
                    ReturnBrush = new SolidColorBrush(b.TotalReturn >= 0 ? Color.FromRgb(72, 187, 120) : Color.FromRgb(245, 101, 101))
                }).ToList();
                NoBacktestsText.Visibility = Visibility.Collapsed;
            }
            else
            {
                NoBacktestsText.Visibility = Visibility.Visible;
            }
        }
        catch { NoBacktestsText.Visibility = Visibility.Visible; }
    }

    private async void VerifyInstallation_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var result = await _leanService.VerifyInstallationAsync();
            MessageBox.Show(
                result.Success ? $"Lean {result.Version} found at {result.LeanPath}" : string.Join("\n", result.Errors),
                result.Success ? "Lean Installation Valid" : "Lean Installation Issues",
                MessageBoxButton.OK,
                result.Success ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Verification Failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BrowseLeanPath_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "Select Lean Engine Path" };
        if (dialog.ShowDialog() == true)
            LeanPathBox.Text = dialog.FolderName;
    }

    private void BrowseDataPath_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "Select Data Path" };
        if (dialog.ShowDialog() == true)
            DataPathBox.Text = dialog.FolderName;
    }

    private async void SaveConfig_Click(object sender, RoutedEventArgs e)
    {
        var config = new LeanConfigurationUpdate
        {
            LeanPath = LeanPathBox.Text,
            DataPath = DataPathBox.Text,
            AutoSync = AutoSyncToggle.IsChecked == true
        };
        var success = await _leanService.UpdateConfigurationAsync(config);
        if (success) await LoadStatusAsync();
    }

    private async void SyncData_Click(object sender, RoutedEventArgs e)
    {
        SyncDataButton.IsEnabled = false;
        SyncStatusText.Text = "Syncing...";
        try
        {
            var options = new DataSyncOptions
            {
                Symbols = string.IsNullOrWhiteSpace(SyncSymbolsBox.Text)
                    ? null
                    : SyncSymbolsBox.Text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
                FromDate = SyncFromDate.SelectedDate is DateTime from ? DateOnly.FromDateTime(from) : null,
                ToDate = SyncToDate.SelectedDate is DateTime to ? DateOnly.FromDateTime(to) : null,
                Resolution = (ResolutionCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Minute",
                Overwrite = OverwriteSyncCheck.IsChecked == true
            };
            var result = await _leanService.SyncDataAsync(options);
            SyncStatusText.Text = result.Success
                ? $"Synced {result.SymbolsSynced} symbols, {result.FilesCreated} files"
                : string.Join(", ", result.Errors);
            await LoadStatusAsync();
        }
        catch (Exception ex) { SyncStatusText.Text = $"Error: {ex.Message}"; }
        finally { SyncDataButton.IsEnabled = true; }
    }

    private async void RunBacktest_Click(object sender, RoutedEventArgs e)
    {
        var selectedAlgorithm = AlgorithmCombo.SelectedItem as ComboBoxItem;
        if (selectedAlgorithm?.Tag is not string algorithmPath)
        {
            MessageBox.Show("Please select an algorithm.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        RunBacktestButton.IsEnabled = false;
        StopBacktestButton.Visibility = Visibility.Visible;
        BacktestProgressPanel.Visibility = Visibility.Visible;

        try
        {
            _ = decimal.TryParse(InitialCapitalBox.Text, out var capital);
            if (capital < 1000) capital = 100000;

            var options = new BacktestOptions
            {
                AlgorithmPath = algorithmPath,
                AlgorithmName = selectedAlgorithm.Content?.ToString(),
                StartDate = BacktestStartDate.SelectedDate is DateTime start ? DateOnly.FromDateTime(start) : null,
                EndDate = BacktestEndDate.SelectedDate is DateTime end ? DateOnly.FromDateTime(end) : null,
                InitialCapital = capital
            };
            var result = await _leanService.StartBacktestAsync(options);
            if (result.Success) { _currentBacktestId = result.BacktestId; _backtestTimer.Start(); }
            else { MessageBox.Show(result.Error ?? "Unknown error", "Backtest Failed"); ResetBacktestUI(); }
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Backtest Failed"); ResetBacktestUI(); }
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

    private async void BacktestTimer_Tick(object? sender, EventArgs e)
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
                MessageBox.Show(status.Error ?? "Unknown error", "Backtest Failed");
                ResetBacktestUI();
            }
        }
        catch { /* Ignore status errors */ }
    }

    private async System.Threading.Tasks.Task ShowBacktestResultsAsync(string backtestId)
    {
        var results = await _leanService.GetBacktestResultsAsync(backtestId);
        TotalReturnText.Text = $"{results.TotalReturn:+0.0%;-0.0%}";
        TotalReturnText.Foreground = new SolidColorBrush(results.TotalReturn >= 0 ? Color.FromRgb(72, 187, 120) : Color.FromRgb(245, 101, 101));
        SharpeRatioText.Text = $"{results.SharpeRatio:F2}";
        MaxDrawdownText.Text = $"{results.MaxDrawdown:0.0%}";
        TotalTradesText.Text = results.TotalTrades.ToString();
        ResultsCard.Visibility = Visibility.Visible;
        await LoadBacktestHistoryAsync();
    }

    private void ResetBacktestUI()
    {
        RunBacktestButton.IsEnabled = true;
        StopBacktestButton.Visibility = Visibility.Collapsed;
        BacktestProgressPanel.Visibility = Visibility.Collapsed;
        _currentBacktestId = null;
    }

    private async void RefreshAlgorithms_Click(object sender, RoutedEventArgs e) => await LoadAlgorithmsAsync();
}

public sealed class WpfBacktestDisplayInfo
{
    public string AlgorithmName { get; set; } = string.Empty;
    public string DateText { get; set; } = string.Empty;
    public string ReturnText { get; set; } = string.Empty;
    public Brush? ReturnBrush { get; set; }
}
