using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI;
using MarketDataCollector.Uwp.Services;

namespace MarketDataCollector.Uwp.Views;

/// <summary>
/// Page for archive health monitoring (#26/57 - P0 Critical).
/// </summary>
public sealed partial class ArchiveHealthPage : Page
{
    private readonly ArchiveHealthService _healthService;
    private readonly SchemaService _schemaService;
    private readonly ObservableCollection<ArchiveIssue> _issues;
    private readonly ObservableCollection<string> _recommendations;

    public ArchiveHealthPage()
    {
        this.InitializeComponent();
        _healthService = ArchiveHealthService.Instance;
        _schemaService = SchemaService.Instance;
        _issues = new ObservableCollection<ArchiveIssue>();
        _recommendations = new ObservableCollection<string>();

        IssuesList.ItemsSource = _issues;
        RecommendationsList.ItemsSource = _recommendations;

        Loaded += Page_Loaded;

        // Subscribe to events
        _healthService.HealthStatusUpdated += OnHealthStatusUpdated;
        _healthService.VerificationCompleted += OnVerificationCompleted;
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadHealthStatusAsync();
        await LoadDictionaryStatusAsync();
    }

    private async Task LoadHealthStatusAsync()
    {
        var status = await _healthService.GetHealthStatusAsync();
        UpdateHealthDisplay(status);
    }

    private void UpdateHealthDisplay(ArchiveHealthStatus status)
    {
        // Health Score
        HealthScoreText.Text = $"{status.OverallHealthScore:F0}%";

        // Status badge
        HealthStatusText.Text = status.Status;
        HealthStatusBadge.Background = status.Status switch
        {
            "Healthy" => new SolidColorBrush(Color.FromArgb(255, 72, 187, 120)),
            "Warning" => new SolidColorBrush(Color.FromArgb(255, 237, 137, 54)),
            "Critical" => new SolidColorBrush(Color.FromArgb(255, 245, 101, 101)),
            _ => new SolidColorBrush(Color.FromArgb(255, 128, 128, 128))
        };

        // File counts
        TotalFilesText.Text = status.TotalFiles.ToString("N0");
        VerifiedFilesText.Text = status.VerifiedFiles.ToString("N0");
        PendingFilesText.Text = status.PendingFiles.ToString("N0");
        FailedFilesText.Text = status.FailedFiles.ToString("N0");

        // Last verification
        if (status.LastFullVerificationAt.HasValue)
        {
            var elapsed = DateTime.UtcNow - status.LastFullVerificationAt.Value;
            var elapsedText = elapsed.TotalDays >= 1
                ? $"{(int)elapsed.TotalDays} days ago"
                : elapsed.TotalHours >= 1
                    ? $"{(int)elapsed.TotalHours} hours ago"
                    : $"{(int)elapsed.TotalMinutes} minutes ago";

            LastVerificationText.Text = $"Last full verification: {elapsedText}";

            if (status.LastVerificationDurationMinutes.HasValue)
            {
                LastVerificationText.Text += $" (took {status.LastVerificationDurationMinutes}m)";
            }
        }
        else
        {
            LastVerificationText.Text = "Last full verification: Never";
        }

        // Storage health
        if (status.StorageHealthInfo != null)
        {
            var storage = status.StorageHealthInfo;
            TotalCapacityText.Text = FormatBytes(storage.TotalCapacity);
            UsedSpaceText.Text = FormatBytes(storage.TotalCapacity - storage.FreeSpace);
            FreeSpaceText.Text = FormatBytes(storage.FreeSpace);
            DaysUntilFullText.Text = storage.DaysUntilFull?.ToString() ?? "--";
            DriveTypeText.Text = $"Drive Type: {storage.DriveType}";

            StorageUsageBar.Value = storage.UsedPercent;
            StorageUsagePercent.Text = $"{storage.UsedPercent:F1}%";

            // Color the progress bar based on usage
            StorageUsageBar.Foreground = storage.UsedPercent switch
            {
                >= 95 => new SolidColorBrush(Color.FromArgb(255, 245, 101, 101)),
                >= 85 => new SolidColorBrush(Color.FromArgb(255, 237, 137, 54)),
                _ => new SolidColorBrush(Color.FromArgb(255, 72, 187, 120))
            };
        }

        // Issues
        _issues.Clear();
        if (status.Issues != null)
        {
            foreach (var issue in status.Issues.Where(i => i.ResolvedAt == null))
            {
                _issues.Add(issue);
            }
        }

        IssueCountBadge.Visibility = _issues.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        IssueCountText.Text = _issues.Count.ToString();
        NoIssuesText.Visibility = _issues.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        // Recommendations
        _recommendations.Clear();
        if (status.Recommendations != null)
        {
            foreach (var rec in status.Recommendations)
            {
                _recommendations.Add(rec);
            }
        }

        NoRecommendationsText.Visibility = _recommendations.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private async Task LoadDictionaryStatusAsync()
    {
        try
        {
            var dictionary = await _schemaService.GetDataDictionaryAsync();
            DictionaryStatusText.Text = $"Last generated: {dictionary.GeneratedAt:g} UTC ({dictionary.Schemas.Count} schemas)";
        }
        catch
        {
            DictionaryStatusText.Text = "Last generated: Never";
        }
    }

    private async void VerifyAll_Click(object sender, RoutedEventArgs e)
    {
        VerifyAllButton.IsEnabled = false;
        QuickCheckButton.IsEnabled = false;
        VerificationProgress.Visibility = Visibility.Visible;
        VerificationStatusText.Visibility = Visibility.Visible;

        var progress = new Progress<VerificationProgress>(p =>
        {
            VerificationProgress.Value = p.ProgressPercent;
            VerificationStatusText.Text = $"Verifying {p.CurrentFile} ({p.ProcessedFiles}/{p.TotalFiles}) - {p.FilesPerSecond:F1} files/s";

            if (p.EstimatedTimeRemainingSeconds.HasValue)
            {
                var eta = TimeSpan.FromSeconds(p.EstimatedTimeRemainingSeconds.Value);
                VerificationStatusText.Text += $" - ETA: {eta:mm\\:ss}";
            }
        });

        try
        {
            var job = await _healthService.StartFullVerificationAsync(progress);

            if (job.FailedFiles > 0)
            {
                await ShowInfoAsync("Verification Complete",
                    $"Verified {job.VerifiedFiles:N0} files.\n{job.FailedFiles:N0} files failed verification.");
            }
            else
            {
                await ShowInfoAsync("Verification Complete",
                    $"All {job.VerifiedFiles:N0} files verified successfully!");
            }
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Verification Failed", ex.Message);
        }
        finally
        {
            VerifyAllButton.IsEnabled = true;
            QuickCheckButton.IsEnabled = true;
            VerificationProgress.Visibility = Visibility.Collapsed;
            VerificationStatusText.Visibility = Visibility.Collapsed;
            await LoadHealthStatusAsync();
        }
    }

    private async void QuickCheck_Click(object sender, RoutedEventArgs e)
    {
        QuickCheckButton.IsEnabled = false;
        VerificationProgress.Visibility = Visibility.Visible;
        VerificationProgress.IsIndeterminate = true;
        VerificationStatusText.Visibility = Visibility.Visible;
        VerificationStatusText.Text = "Running quick verification on recent files...";

        try
        {
            var since = DateTime.UtcNow.AddDays(-7);
            var job = await _healthService.StartIncrementalVerificationAsync(since);

            await ShowInfoAsync("Quick Check Complete",
                $"Checked {job.TotalFiles:N0} recent files.\n" +
                $"Verified: {job.VerifiedFiles:N0}, Failed: {job.FailedFiles:N0}");
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Quick Check Failed", ex.Message);
        }
        finally
        {
            QuickCheckButton.IsEnabled = true;
            VerificationProgress.Visibility = Visibility.Collapsed;
            VerificationProgress.IsIndeterminate = false;
            VerificationStatusText.Visibility = Visibility.Collapsed;
            await LoadHealthStatusAsync();
        }
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        await LoadHealthStatusAsync();
    }

    private async void ResolveIssue_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is ArchiveIssue issue)
        {
            try
            {
                await _healthService.ResolveIssueAsync(issue.Id);
                await LoadHealthStatusAsync();
            }
            catch (Exception ex)
            {
                await ShowErrorAsync("Failed to resolve issue", ex.Message);
            }
        }
    }

    private async void GenerateDictionary_Click(object sender, RoutedEventArgs e)
    {
        GenerateDictionaryButton.IsEnabled = false;

        try
        {
            var dictionary = await _schemaService.GenerateDataDictionaryAsync();
            DictionaryStatusText.Text = $"Generated: {dictionary.GeneratedAt:g} UTC ({dictionary.Schemas.Count} schemas)";
            await ShowInfoAsync("Dictionary Generated",
                $"Data dictionary generated successfully with {dictionary.Schemas.Count} schemas.");
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Generation Failed", ex.Message);
        }
        finally
        {
            GenerateDictionaryButton.IsEnabled = true;
        }
    }

    private async void ExportMarkdown_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var markdown = await _schemaService.GenerateMarkdownDocumentationAsync();

            var picker = new Windows.Storage.Pickers.FileSavePicker();
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
            picker.FileTypeChoices.Add("Markdown", new[] { ".md" });
            picker.SuggestedFileName = "DATA_DICTIONARY";

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            var file = await picker.PickSaveFileAsync();
            if (file != null)
            {
                await Windows.Storage.FileIO.WriteTextAsync(file, markdown);
                await ShowInfoAsync("Export Complete", $"Data dictionary saved to {file.Path}");
            }
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Export Failed", ex.Message);
        }
    }

    private async void ExportJson_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var json = await _schemaService.ExportDataDictionaryAsync("json");

            var picker = new Windows.Storage.Pickers.FileSavePicker();
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
            picker.FileTypeChoices.Add("JSON", new[] { ".json" });
            picker.SuggestedFileName = "data_dictionary";

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            var file = await picker.PickSaveFileAsync();
            if (file != null)
            {
                await Windows.Storage.FileIO.WriteTextAsync(file, json);
                await ShowInfoAsync("Export Complete", $"Data dictionary saved to {file.Path}");
            }
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Export Failed", ex.Message);
        }
    }

    private void OnHealthStatusUpdated(object? sender, ArchiveHealthEventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (e.Status != null)
            {
                UpdateHealthDisplay(e.Status);
            }
        });
    }

    private void OnVerificationCompleted(object? sender, VerificationJobEventArgs e)
    {
        DispatcherQueue.TryEnqueue(async () =>
        {
            await LoadHealthStatusAsync();
        });
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

    private async Task ShowErrorAsync(string title, string message)
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

    private async Task ShowInfoAsync(string title, string message)
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
