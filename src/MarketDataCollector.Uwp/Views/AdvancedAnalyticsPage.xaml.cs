using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using MarketDataCollector.Uwp.Services;
using MarketDataCollector.Uwp.Models;

namespace MarketDataCollector.Uwp.Views;

/// <summary>
/// Page for advanced analytics including gap analysis, cross-provider comparison,
/// latency histograms, anomaly detection, and detailed quality reports.
/// </summary>
public sealed partial class AdvancedAnalyticsPage : Page
{
    private readonly AdvancedAnalyticsService _analyticsService;
    private readonly SymbolManagementService _symbolService;
    private GapAnalysisResult? _lastGapAnalysis;

    public AdvancedAnalyticsPage()
    {
        this.InitializeComponent();
        _analyticsService = AdvancedAnalyticsService.Instance;
        _symbolService = SymbolManagementService.Instance;

        Loaded += AdvancedAnalyticsPage_Loaded;
    }

    private async void AdvancedAnalyticsPage_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadSymbolsAsync();
        await RefreshAllAsync();
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        await RefreshAllAsync();
    }

    private async System.Threading.Tasks.Task RefreshAllAsync()
    {
        RefreshButton.IsEnabled = false;
        try
        {
            await LoadQualityReportAsync();
            await LoadLatencyDataAsync();
            await LoadRateLimitsAsync();
        }
        finally
        {
            RefreshButton.IsEnabled = true;
        }
    }

    private async System.Threading.Tasks.Task LoadSymbolsAsync()
    {
        try
        {
            var result = await _symbolService.GetAllSymbolsAsync();
            if (result.Success)
            {
                GapSymbolCombo.Items.Clear();
                GapSymbolCombo.Items.Add(new ComboBoxItem { Content = "All Symbols", Tag = "" });

                foreach (var symbol in result.Symbols.Take(50))
                {
                    GapSymbolCombo.Items.Add(new ComboBoxItem { Content = symbol.Symbol, Tag = symbol.Symbol });
                }

                GapSymbolCombo.SelectedIndex = 0;
            }
        }
        catch
        {
            // Keep default items
        }
    }

    #region Quality Report

    private async System.Threading.Tasks.Task LoadQualityReportAsync()
    {
        try
        {
            var result = await _analyticsService.GetQualityReportAsync(new DataQualityReportOptions
            {
                IncludeDetails = true
            });

            if (result.Success)
            {
                // Update summary cards
                OverallScoreText.Text = $"{result.OverallScore:F0}%";
                GradeText.Text = result.Grade;
                UpdateGradeBadge(result.Grade);

                if (result.Metrics != null)
                {
                    CompletenessText.Text = $"{result.Metrics.CompletenessScore:F0}%";
                }

                // Update symbol reports
                var items = result.SymbolReports.Select(r => new SymbolQualityDisplayItem
                {
                    Symbol = r.Symbol,
                    Grade = r.Grade,
                    GradeBackground = GetGradeBackground(r.Grade),
                    OverallScore = r.OverallScore,
                    CompletenessText = $"{r.CompletenessScore:F0}%",
                    IntegrityText = $"{r.IntegrityScore:F0}%",
                    IssueCount = r.Issues.Count > 0 ? r.Issues.Count.ToString() : "-"
                }).ToList();

                SymbolQualityList.ItemsSource = items;

                // Show recommendations
                if (result.Recommendations.Count > 0)
                {
                    RecommendationsCard.Visibility = Visibility.Visible;
                    RecommendationsList.ItemsSource = result.Recommendations;
                }
                else
                {
                    RecommendationsCard.Visibility = Visibility.Collapsed;
                }
            }
        }
        catch (Exception ex)
        {
            ShowError("Failed to load quality report", ex.Message);
        }
    }

    private void UpdateGradeBadge(string grade)
    {
        var background = grade switch
        {
            "A" or "A+" => new SolidColorBrush(Windows.UI.Color.FromArgb(40, 72, 187, 120)),
            "B" or "B+" => new SolidColorBrush(Windows.UI.Color.FromArgb(40, 88, 166, 255)),
            "C" or "C+" => new SolidColorBrush(Windows.UI.Color.FromArgb(40, 237, 137, 54)),
            _ => new SolidColorBrush(Windows.UI.Color.FromArgb(40, 245, 101, 101))
        };
        GradeBadge.Background = background;
    }

    private static SolidColorBrush GetGradeBackground(string grade)
    {
        return grade switch
        {
            "A" or "A+" => new SolidColorBrush(Windows.UI.Color.FromArgb(40, 72, 187, 120)),
            "B" or "B+" => new SolidColorBrush(Windows.UI.Color.FromArgb(40, 88, 166, 255)),
            "C" or "C+" => new SolidColorBrush(Windows.UI.Color.FromArgb(40, 237, 137, 54)),
            _ => new SolidColorBrush(Windows.UI.Color.FromArgb(40, 245, 101, 101))
        };
    }

    private async void GenerateReport_Click(object sender, RoutedEventArgs e)
    {
        await LoadQualityReportAsync();
        ShowSuccess("Quality report generated.");
    }

    #endregion

    #region Gap Analysis

    private async void AnalyzeGaps_Click(object sender, RoutedEventArgs e)
    {
        GapAnalysisProgress.Visibility = Visibility.Visible;
        try
        {
            var symbol = (GapSymbolCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString();

            var result = await _analyticsService.AnalyzeGapsAsync(new GapAnalysisOptions
            {
                Symbol = string.IsNullOrEmpty(symbol) ? null : symbol,
                MinGapMinutes = 5
            });

            _lastGapAnalysis = result;

            if (result.Success)
            {
                GapSummaryCard.Visibility = Visibility.Visible;
                TotalGapsText.Text = result.TotalGaps.ToString();
                TotalGapDurationText.Text = FormatDuration(result.TotalGapDuration);
                GapCountText.Text = result.TotalGaps.ToString();

                var repairableCount = result.Gaps.Count(g => g.IsRepairable);
                RepairableGapsText.Text = repairableCount.ToString();
                RepairGapsButton.IsEnabled = repairableCount > 0;

                var items = result.Gaps.Select(g => new GapDisplayItem
                {
                    Symbol = g.Symbol,
                    EventType = g.EventType,
                    TimeRange = $"{g.StartTime:HH:mm} - {g.EndTime:HH:mm}",
                    DurationText = FormatDuration(g.Duration),
                    RepairableText = g.IsRepairable ? "Repairable" : "Manual",
                    RepairableBackground = new SolidColorBrush(
                        g.IsRepairable
                            ? Windows.UI.Color.FromArgb(40, 72, 187, 120)
                            : Windows.UI.Color.FromArgb(40, 160, 160, 160))
                }).ToList();

                GapsList.ItemsSource = items;
            }
            else
            {
                ShowError("Gap analysis failed", result.Error ?? "Unknown error");
            }
        }
        catch (Exception ex)
        {
            ShowError("Gap analysis failed", ex.Message);
        }
        finally
        {
            GapAnalysisProgress.Visibility = Visibility.Collapsed;
        }
    }

    private async void RepairGaps_Click(object sender, RoutedEventArgs e)
    {
        if (_lastGapAnalysis == null) return;

        var dialog = new ContentDialog
        {
            Title = "Repair Data Gaps",
            Content = "This will attempt to fetch missing data from alternative providers. Continue?",
            PrimaryButtonText = "Repair",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.XamlRoot
        };

        var dialogResult = await dialog.ShowAsync();
        if (dialogResult != ContentDialogResult.Primary) return;

        try
        {
            var result = await _analyticsService.RepairGapsAsync(new GapRepairOptions
            {
                UseAlternativeProviders = true
            });

            if (result.Success)
            {
                ShowSuccess($"Repair complete. {result.GapsRepaired} gaps repaired, {result.RecordsRecovered} records recovered.");
                // Refresh gap analysis
                AnalyzeGaps_Click(sender, e);
            }
            else
            {
                ShowError("Repair failed", result.Error ?? "Unknown error");
            }
        }
        catch (Exception ex)
        {
            ShowError("Repair failed", ex.Message);
        }
    }

    #endregion

    #region Cross-Provider Comparison

    private async void CompareProviders_Click(object sender, RoutedEventArgs e)
    {
        var symbol = CompareSymbolBox.Text?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(symbol))
        {
            ShowError("Validation", "Please enter a symbol to compare.");
            return;
        }

        ComparisonProgress.Visibility = Visibility.Visible;
        try
        {
            var date = CompareDatePicker.Date?.Date ?? DateTime.Today;

            var result = await _analyticsService.CompareProvidersAsync(new CrossProviderComparisonOptions
            {
                Symbol = symbol,
                Date = DateOnly.FromDateTime(date)
            });

            if (result.Success)
            {
                ComparisonResultsCard.Visibility = Visibility.Visible;
                ConsistencyScoreText.Text = $"{result.OverallConsistencyScore:F1}%";
                DiscrepancyCountText.Text = result.Discrepancies.Count.ToString();

                var items = result.Discrepancies.Select(d => new DiscrepancyDisplayItem
                {
                    TimestampText = d.Timestamp.ToString("HH:mm:ss"),
                    DiscrepancyType = d.DiscrepancyType,
                    Values = $"{d.Provider1}: {d.Value1} | {d.Provider2}: {d.Value2}",
                    DifferenceText = $"{d.Difference:F2}%"
                }).ToList();

                DiscrepanciesList.ItemsSource = items;
            }
            else
            {
                ShowError("Comparison failed", result.Error ?? "Unknown error");
            }
        }
        catch (Exception ex)
        {
            ShowError("Comparison failed", ex.Message);
        }
        finally
        {
            ComparisonProgress.Visibility = Visibility.Collapsed;
        }
    }

    #endregion

    #region Latency Analysis

    private async System.Threading.Tasks.Task LoadLatencyDataAsync()
    {
        try
        {
            var result = await _analyticsService.GetLatencyHistogramAsync();

            if (result.Success)
            {
                var maxP99 = result.Providers.Max(p => p.P99Ms);
                if (maxP99 < 1) maxP99 = 100;

                var items = result.Providers.Select(p => new LatencyDisplayItem
                {
                    Provider = p.Provider,
                    P50Text = $"{p.P50Ms:F0}ms",
                    P95Text = $"{p.P95Ms:F0}ms",
                    P99Text = $"{p.P99Ms:F0}ms",
                    LatencyPercent = (p.P50Ms / maxP99) * 100
                }).ToList();

                LatencyList.ItemsSource = items;
            }
        }
        catch
        {
            // Show placeholder data
            LatencyList.ItemsSource = new List<LatencyDisplayItem>
            {
                new() { Provider = "Alpaca", P50Text = "--ms", P95Text = "--ms", P99Text = "--ms" },
                new() { Provider = "Polygon", P50Text = "--ms", P95Text = "--ms", P99Text = "--ms" }
            };
        }
    }

    #endregion

    #region Rate Limits

    private async System.Threading.Tasks.Task LoadRateLimitsAsync()
    {
        try
        {
            var result = await _analyticsService.GetRateLimitStatusAsync();

            if (result.Success)
            {
                var items = result.Providers.Select(p => new RateLimitDisplayItem
                {
                    Provider = p.Provider,
                    UsagePercent = p.UsagePercent,
                    UsageText = $"{p.RequestsUsed}/{p.RequestsPerMinute}",
                    StatusText = p.IsThrottled ? "Throttled" : (p.UsagePercent > 80 ? "High" : "OK"),
                    UsageColor = new SolidColorBrush(
                        p.IsThrottled
                            ? Windows.UI.Color.FromArgb(255, 245, 101, 101)
                            : (p.UsagePercent > 80
                                ? Windows.UI.Color.FromArgb(255, 237, 137, 54)
                                : Windows.UI.Color.FromArgb(255, 72, 187, 120))),
                    StatusBackground = new SolidColorBrush(
                        p.IsThrottled
                            ? Windows.UI.Color.FromArgb(40, 245, 101, 101)
                            : (p.UsagePercent > 80
                                ? Windows.UI.Color.FromArgb(40, 237, 137, 54)
                                : Windows.UI.Color.FromArgb(40, 72, 187, 120)))
                }).ToList();

                RateLimitsList.ItemsSource = items;
            }
        }
        catch
        {
            // Show placeholder data
            RateLimitsList.ItemsSource = new List<RateLimitDisplayItem>
            {
                new() { Provider = "Alpaca", UsageText = "--/--", StatusText = "--", UsagePercent = 0 },
                new() { Provider = "Polygon", UsageText = "--/--", StatusText = "--", UsagePercent = 0 }
            };
        }
    }

    #endregion

    #region Helpers

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalDays >= 1) return $"{(int)duration.TotalDays}d {duration.Hours}h";
        if (duration.TotalHours >= 1) return $"{(int)duration.TotalHours}h {duration.Minutes}m";
        return $"{(int)duration.TotalMinutes}m";
    }

    private void ShowSuccess(string message)
    {
        StatusInfoBar.Severity = InfoBarSeverity.Success;
        StatusInfoBar.Title = "Success";
        StatusInfoBar.Message = message;
        StatusInfoBar.IsOpen = true;
    }

    private void ShowError(string title, string message)
    {
        StatusInfoBar.Severity = InfoBarSeverity.Error;
        StatusInfoBar.Title = title;
        StatusInfoBar.Message = message;
        StatusInfoBar.IsOpen = true;
    }

    #endregion
}
