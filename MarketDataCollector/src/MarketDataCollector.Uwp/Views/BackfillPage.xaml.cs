using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MarketDataCollector.Uwp.Services;

namespace MarketDataCollector.Uwp.Views;

/// <summary>
/// Page for historical data backfill operations.
/// </summary>
public sealed partial class BackfillPage : Page
{
    private readonly BackfillService _backfillService;
    private CancellationTokenSource? _cts;

    public BackfillPage()
    {
        this.InitializeComponent();
        _backfillService = new BackfillService();

        Loaded += BackfillPage_Loaded;
    }

    private async void BackfillPage_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadLastStatusAsync();
    }

    private async Task LoadLastStatusAsync()
    {
        var status = await _backfillService.GetLastStatusAsync();
        if (status != null)
        {
            StatusGrid.Visibility = Visibility.Visible;
            NoStatusText.Visibility = Visibility.Collapsed;

            StatusText.Text = status.Success ? "Success" : "Failed";
            StatusText.Foreground = status.Success
                ? new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.LimeGreen)
                : new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Red);

            ProviderText.Text = status.Provider ?? "Unknown";
            SymbolsText.Text = status.Symbols != null ? string.Join(", ", status.Symbols) : "N/A";
            BarsWrittenText.Text = status.BarsWritten.ToString("N0");
            StartedText.Text = status.StartedUtc.ToString("yyyy-MM-dd HH:mm:ss UTC");
            CompletedText.Text = status.CompletedUtc.ToString("yyyy-MM-dd HH:mm:ss UTC");

            if (!string.IsNullOrEmpty(status.Error))
            {
                ErrorText.Text = status.Error;
                ErrorText.Visibility = Visibility.Visible;
            }
            else
            {
                ErrorText.Visibility = Visibility.Collapsed;
            }
        }
        else
        {
            StatusGrid.Visibility = Visibility.Collapsed;
            NoStatusText.Visibility = Visibility.Visible;
            ErrorText.Visibility = Visibility.Collapsed;
        }
    }

    private async void StartBackfill_Click(object sender, RoutedEventArgs e)
    {
        var symbolsText = SymbolsBox.Text?.Trim();
        if (string.IsNullOrEmpty(symbolsText))
        {
            await ShowErrorAsync("Please enter at least one symbol.");
            return;
        }

        var symbols = symbolsText.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (symbols.Length == 0)
        {
            await ShowErrorAsync("Please enter at least one valid symbol.");
            return;
        }

        var provider = GetComboSelectedTag(ProviderCombo) ?? "stooq";
        var from = FromDatePicker.Date?.ToString("yyyy-MM-dd");
        var to = ToDatePicker.Date?.ToString("yyyy-MM-dd");

        // Update UI
        StartBackfillButton.IsEnabled = false;
        CancelBackfillButton.Visibility = Visibility.Visible;
        BackfillProgress.IsActive = true;
        ProgressPanel.Visibility = Visibility.Visible;
        ProgressLabel.Text = "Starting backfill...";
        ProgressPercent.Text = "0%";
        ProgressBar.Value = 0;

        _cts = new CancellationTokenSource();

        try
        {
            // Simulate progress (the actual service doesn't provide progress updates)
            ProgressLabel.Text = $"Downloading data for {symbols.Length} symbol(s)...";
            ProgressBar.Value = 20;
            ProgressPercent.Text = "20%";

            var result = await _backfillService.RunBackfillAsync(provider, symbols, from, to);

            ProgressBar.Value = 100;
            ProgressPercent.Text = "100%";
            ProgressLabel.Text = "Complete";

            if (result != null)
            {
                if (result.Success)
                {
                    await ShowSuccessAsync($"Backfill completed successfully. {result.BarsWritten:N0} bars downloaded.");
                }
                else
                {
                    await ShowErrorAsync(result.Error ?? "Backfill failed with unknown error.");
                }
            }

            await LoadLastStatusAsync();
        }
        catch (OperationCanceledException)
        {
            ProgressLabel.Text = "Cancelled";
        }
        catch (Exception ex)
        {
            await ShowErrorAsync(ex.Message);
        }
        finally
        {
            StartBackfillButton.IsEnabled = true;
            CancelBackfillButton.Visibility = Visibility.Collapsed;
            BackfillProgress.IsActive = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    private void CancelBackfill_Click(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
    }

    private async void RefreshStatus_Click(object sender, RoutedEventArgs e)
    {
        await LoadLastStatusAsync();
    }

    private void Last30Days_Click(object sender, RoutedEventArgs e)
    {
        ToDatePicker.Date = DateTimeOffset.Now;
        FromDatePicker.Date = DateTimeOffset.Now.AddDays(-30);
    }

    private void Last90Days_Click(object sender, RoutedEventArgs e)
    {
        ToDatePicker.Date = DateTimeOffset.Now;
        FromDatePicker.Date = DateTimeOffset.Now.AddDays(-90);
    }

    private void YearToDate_Click(object sender, RoutedEventArgs e)
    {
        ToDatePicker.Date = DateTimeOffset.Now;
        FromDatePicker.Date = new DateTimeOffset(DateTimeOffset.Now.Year, 1, 1, 0, 0, 0, TimeSpan.Zero);
    }

    private void LastYear_Click(object sender, RoutedEventArgs e)
    {
        var lastYear = DateTimeOffset.Now.Year - 1;
        FromDatePicker.Date = new DateTimeOffset(lastYear, 1, 1, 0, 0, 0, TimeSpan.Zero);
        ToDatePicker.Date = new DateTimeOffset(lastYear, 12, 31, 0, 0, 0, TimeSpan.Zero);
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

    private async Task ShowSuccessAsync(string message)
    {
        var dialog = new ContentDialog
        {
            Title = "Success",
            Content = message,
            CloseButtonText = "OK",
            XamlRoot = this.XamlRoot
        };
        await dialog.ShowAsync();
    }

    private static string? GetComboSelectedTag(ComboBox combo)
    {
        return (combo.SelectedItem as ComboBoxItem)?.Tag?.ToString();
    }
}
