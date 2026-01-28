using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using MarketDataCollector.Uwp.Services;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace MarketDataCollector.Uwp.Views;

/// <summary>
/// Storage Optimization page for analyzing and optimizing data storage.
/// Implements Feature Refinement #63 - Archive Storage Optimization Advisor.
/// </summary>
public sealed partial class StorageOptimizationPage : Page
{
    private readonly StorageOptimizationAdvisorService _optimizationService;
    private readonly NotificationService _notificationService;
    private readonly ConfigService _configService;
    private StorageOptimizationReport? _lastReport;
    private CancellationTokenSource? _analysisCts;

    public StorageOptimizationPage()
    {
        this.InitializeComponent();
        _optimizationService = StorageOptimizationAdvisorService.Instance;
        _notificationService = NotificationService.Instance;
        _configService = ConfigService.Instance;
    }

    #region Event Handlers

    private async void AnalyzeStorage_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Show progress
            ProgressSection.Visibility = Visibility.Visible;
            AnalyzeButton.IsEnabled = false;
            CancelButton.Visibility = Visibility.Visible;

            // Get data root from config
            var config = await _configService.LoadConfigAsync();
            var dataRoot = config?.DataRoot ?? "data";

            // Build analysis options from UI
            var options = new StorageAnalysisOptions
            {
                CalculateHashes = CalculateHashesCheck.IsChecked ?? true,
                FindDuplicates = FindDuplicatesCheck.IsChecked ?? true,
                AnalyzeCompression = AnalyzeCompressionCheck.IsChecked ?? true,
                FindSmallFiles = FindSmallFilesCheck.IsChecked ?? true,
                AnalyzeTiering = AnalyzeTieringCheck.IsChecked ?? true,
                ColdTierAgeDays = (int)ColdTierAgeDays.Value
            };

            // Create cancellation token
            _analysisCts = new CancellationTokenSource();

            // Create progress reporter
            var progress = new Progress<StorageAnalysisProgress>(p =>
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    AnalysisStatusText.Text = p.Stage;
                    AnalysisProgressBar.Value = p.Percentage;
                });
            });

            // Run analysis
            _lastReport = await _optimizationService.AnalyzeStorageAsync(
                dataRoot, options, progress, _analysisCts.Token);

            // Display results
            DisplayResults(_lastReport);

            if (!_lastReport.WasCancelled)
            {
                await _notificationService.NotifyAsync(
                    "Analysis Complete",
                    $"Found {_lastReport.Recommendations.Count} optimization opportunities",
                    NotificationType.Success);
            }
        }
        catch (OperationCanceledException)
        {
            await _notificationService.NotifyAsync("Analysis Cancelled", "Storage analysis was cancelled", NotificationType.Info);
        }
        catch (Exception ex)
        {
            await _notificationService.NotifyErrorAsync("Analysis Failed", ex.Message);
        }
        finally
        {
            ProgressSection.Visibility = Visibility.Collapsed;
            AnalyzeButton.IsEnabled = true;
            CancelButton.Visibility = Visibility.Collapsed;
        }
    }

    private void CancelAnalysis_Click(object sender, RoutedEventArgs e)
    {
        _analysisCts?.Cancel();
    }

    private void DisplayResults(StorageOptimizationReport report)
    {
        // Show results sections
        ResultsSummarySection.Visibility = Visibility.Visible;
        RecommendationsSection.Visibility = Visibility.Visible;
        FileBreakdownSection.Visibility = Visibility.Visible;
        TopConsumersSection.Visibility = Visibility.Visible;
        QuickActionsSection.Visibility = Visibility.Visible;

        // Update summary
        CurrentUsageText.Text = FormatBytes(report.TotalBytes);
        PotentialSavingsText.Text = FormatBytes(report.TotalPotentialSavings);
        ProjectedUsageText.Text = FormatBytes(report.ProjectedUsageAfterOptimization);

        TotalFilesText.Text = report.TotalFiles.ToString("N0");
        DuplicatesFoundText.Text = report.DuplicateFilesCount.ToString("N0");
        UncompressedFilesText.Text = report.UncompressedFilesCount.ToString("N0");
        SmallFilesText.Text = report.SmallFilesCount.ToString("N0");

        EstimatedTimeText.Text = $"Estimated time to complete all optimizations: {report.EstimatedOptimizationTime.TotalMinutes:F0} minutes";

        // Update recommendations list
        var sortedRecs = report.Recommendations
            .OrderByDescending(r => r.PotentialSavingsBytes)
            .ToList();
        RecommendationsList.ItemsSource = sortedRecs;
        RecommendationCountText.Text = $"({sortedRecs.Count})";

        // Update file breakdown by data type
        var byDataType = report.AnalyzedFiles
            .GroupBy(f => f.DataType)
            .Select(g => new
            {
                DataType = g.Key,
                Size = g.Sum(f => f.Size),
                SizeFormatted = FormatBytes(g.Sum(f => f.Size)),
                Percentage = report.TotalBytes > 0 ? g.Sum(f => f.Size) * 100.0 / report.TotalBytes : 0
            })
            .OrderByDescending(x => x.Size)
            .ToList();
        ByDataTypeList.ItemsSource = byDataType;

        // Update file breakdown by tier
        var byTier = report.AnalyzedFiles
            .GroupBy(f => f.StorageTier)
            .Select(g => new
            {
                Tier = g.Key,
                Size = g.Sum(f => f.Size),
                SizeFormatted = FormatBytes(g.Sum(f => f.Size)),
                Percentage = report.TotalBytes > 0 ? g.Sum(f => f.Size) * 100.0 / report.TotalBytes : 0
            })
            .OrderByDescending(x => x.Size)
            .ToList();
        ByTierList.ItemsSource = byTier;

        // Update top consumers
        var topConsumers = report.AnalyzedFiles
            .GroupBy(f => f.Symbol)
            .Where(g => !string.IsNullOrEmpty(g.Key))
            .Select(g => new
            {
                Symbol = g.Key,
                Size = g.Sum(f => f.Size),
                SizeFormatted = FormatBytes(g.Sum(f => f.Size)),
                FileCount = g.Count(),
                Percentage = report.TotalBytes > 0 ? g.Sum(f => f.Size) * 100.0 / report.TotalBytes : 0
            })
            .OrderByDescending(x => x.Size)
            .Take(10)
            .Select((x, i) => new
            {
                Rank = $"#{i + 1}",
                x.Symbol,
                x.Size,
                x.SizeFormatted,
                x.FileCount,
                x.Percentage
            })
            .ToList();
        TopConsumersList.ItemsSource = topConsumers;

        // Enable quick action buttons based on recommendations
        RemoveAllDuplicatesButton.IsEnabled = report.DuplicateFilesCount > 0;
        CompressWarmTierButton.IsEnabled = report.UncompressedFilesCount > 0;
        MergeSmalFilesButton.IsEnabled = report.SmallFilesCount > 10;
        MoveToClodButton.IsEnabled = report.TieringCandidatesCount > 0;
    }

    private async void ExecuteRecommendation_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is OptimizationRecommendation recommendation)
        {
            // Confirm execution
            var dialog = new ContentDialog
            {
                Title = "Execute Optimization",
                Content = $"This will {recommendation.Title.ToLowerInvariant()}.\n\n" +
                         $"Affected files: {recommendation.AffectedFiles.Count}\n" +
                         $"Potential savings: {FormatBytes(recommendation.PotentialSavingsBytes)}",
                PrimaryButtonText = "Execute",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary)
            {
                return;
            }

            try
            {
                button.IsEnabled = false;

                var progress = new Progress<OptimizationProgress>(p =>
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        AnalysisStatusText.Text = $"Processing: {p.CurrentFile}";
                        AnalysisProgressBar.Value = p.Percentage;
                    });
                });

                ProgressSection.Visibility = Visibility.Visible;

                var executionResult = await _optimizationService.ExecuteOptimizationAsync(
                    recommendation, progress, CancellationToken.None);

                if (executionResult.Success)
                {
                    await _notificationService.NotifyAsync(
                        "Optimization Complete",
                        $"Processed {executionResult.FilesProcessed} files, saved {FormatBytes(executionResult.BytesSaved)}",
                        NotificationType.Success);

                    // Refresh analysis
                    AnalyzeStorage_Click(sender, e);
                }
                else
                {
                    var errors = string.Join("\n", executionResult.Errors.Take(3));
                    await _notificationService.NotifyErrorAsync("Optimization Failed", errors);
                }
            }
            finally
            {
                button.IsEnabled = true;
                ProgressSection.Visibility = Visibility.Collapsed;
            }
        }
    }

    private async void ExecuteAll_Click(object sender, RoutedEventArgs e)
    {
        if (_lastReport == null || !_lastReport.Recommendations.Any())
        {
            return;
        }

        var dialog = new ContentDialog
        {
            Title = "Execute All Optimizations",
            Content = $"This will execute {_lastReport.Recommendations.Count} optimization(s).\n\n" +
                     $"Potential savings: {FormatBytes(_lastReport.TotalPotentialSavings)}\n" +
                     $"Estimated time: {_lastReport.EstimatedOptimizationTime.TotalMinutes:F0} minutes",
            PrimaryButtonText = "Execute All",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
        {
            return;
        }

        await _notificationService.NotifyAsync(
            "Coming Soon",
            "Batch optimization execution will be available in a future update",
            NotificationType.Info);
    }

    private async void ExportReport_Click(object sender, RoutedEventArgs e)
    {
        if (_lastReport == null)
        {
            await _notificationService.NotifyWarningAsync("No Report", "Run an analysis first to generate a report");
            return;
        }

        try
        {
            var picker = new FileSavePicker();
            picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            picker.FileTypeChoices.Add("JSON", new List<string> { ".json" });
            picker.FileTypeChoices.Add("Text", new List<string> { ".txt" });
            picker.SuggestedFileName = $"storage_optimization_{DateTime.Now:yyyyMMdd_HHmmss}";

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            var file = await picker.PickSaveFileAsync();
            if (file != null)
            {
                string content;
                if (file.FileType == ".json")
                {
                    content = System.Text.Json.JsonSerializer.Serialize(_lastReport,
                        new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                }
                else
                {
                    content = _lastReport.GetSummary();
                }

                await FileIO.WriteTextAsync(file, content);
                await _notificationService.NotifyAsync("Export Complete", $"Exported to {file.Name}", NotificationType.Success);
            }
        }
        catch (Exception ex)
        {
            await _notificationService.NotifyErrorAsync("Export Failed", ex.Message);
        }
    }

    private async void RemoveAllDuplicates_Click(object sender, RoutedEventArgs e)
    {
        if (_lastReport == null) return;

        var duplicateRecs = _lastReport.Recommendations
            .Where(r => r.Type == OptimizationType.RemoveDuplicates)
            .ToList();

        if (!duplicateRecs.Any())
        {
            await _notificationService.NotifyAsync("No Duplicates", "No duplicate files to remove", NotificationType.Info);
            return;
        }

        var totalFiles = duplicateRecs.Sum(r => r.AffectedFiles.Count);
        var totalSavings = duplicateRecs.Sum(r => r.PotentialSavingsBytes);

        var dialog = new ContentDialog
        {
            Title = "Remove All Duplicates",
            Content = $"This will remove {totalFiles} duplicate files.\n\nSpace to free: {FormatBytes(totalSavings)}",
            PrimaryButtonText = "Remove All",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            // Execute each duplicate removal recommendation
            foreach (var rec in duplicateRecs)
            {
                await _optimizationService.ExecuteOptimizationAsync(rec);
            }

            await _notificationService.NotifyAsync("Duplicates Removed", $"Removed {totalFiles} duplicate files", NotificationType.Success);
            AnalyzeStorage_Click(sender, e);
        }
    }

    private async void CompressWarmTier_Click(object sender, RoutedEventArgs e)
    {
        await _notificationService.NotifyAsync(
            "Coming Soon",
            "Compression execution requires additional libraries and will be available in a future update",
            NotificationType.Info);
    }

    private async void MergeSmallFiles_Click(object sender, RoutedEventArgs e)
    {
        await _notificationService.NotifyAsync(
            "Coming Soon",
            "File merging requires format-specific logic and will be available in a future update",
            NotificationType.Info);
    }

    private async void MoveToCold_Click(object sender, RoutedEventArgs e)
    {
        await _notificationService.NotifyAsync(
            "Coming Soon",
            "Tier migration requires storage configuration and will be available in a future update",
            NotificationType.Info);
    }

    #endregion

    #region Helper Methods

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

    #endregion
}

#region Converters

/// <summary>
/// Converts bytes to formatted string.
/// </summary>
public sealed class BytesToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is long bytes)
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
        return "0 B";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        // Parse formatted string back to bytes (e.g., "1.5 GB" -> 1610612736)
        if (value is string str && !string.IsNullOrWhiteSpace(str))
        {
            var parts = str.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2 && double.TryParse(parts[0], out var num))
            {
                var multiplier = parts[1].ToUpperInvariant() switch
                {
                    "B" => 1L,
                    "KB" => 1024L,
                    "MB" => 1024L * 1024,
                    "GB" => 1024L * 1024 * 1024,
                    "TB" => 1024L * 1024 * 1024 * 1024,
                    _ => 0L
                };

                if (multiplier > 0)
                {
                    return (long)(num * multiplier);
                }
            }
        }

        return 0L;
    }
}

/// <summary>
/// Converts priority to color.
/// </summary>
public sealed class PriorityToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is RecommendationPriority priority)
        {
            return priority switch
            {
                RecommendationPriority.Critical => Windows.UI.Color.FromArgb(255, 245, 101, 101),
                RecommendationPriority.High => Windows.UI.Color.FromArgb(255, 237, 137, 54),
                RecommendationPriority.Medium => Windows.UI.Color.FromArgb(255, 66, 153, 225),
                _ => Windows.UI.Color.FromArgb(255, 160, 174, 192)
            };
        }
        return Windows.UI.Color.FromArgb(255, 160, 174, 192);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        // Map color back to priority based on RGB values
        if (value is Windows.UI.Color color)
        {
            // Critical: #F56565 (RGB: 245, 101, 101)
            if (color.R >= 240 && color.G >= 95 && color.G <= 110 && color.B >= 95 && color.B <= 110)
                return RecommendationPriority.Critical;

            // High: #ED8936 (RGB: 237, 137, 54)
            if (color.R >= 230 && color.G >= 130 && color.G <= 145 && color.B >= 50 && color.B <= 60)
                return RecommendationPriority.High;

            // Medium: #4299E1 (RGB: 66, 153, 225)
            if (color.R >= 60 && color.R <= 75 && color.G >= 148 && color.G <= 160 && color.B >= 220)
                return RecommendationPriority.Medium;
        }

        // Default to Low priority
        return RecommendationPriority.Low;
    }
}

#endregion
