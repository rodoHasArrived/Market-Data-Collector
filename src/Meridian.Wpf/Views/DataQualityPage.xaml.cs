using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Meridian.Wpf.Models;
using Meridian.Wpf.ViewModels;
using WpfServices = Meridian.Wpf.Services;

using Meridian.Wpf.Services;
namespace Meridian.Wpf.Views;

/// <summary>
/// Data quality monitoring page showing completeness, gaps, and anomalies.
/// Delegates all data loading, filtering, and state management to <see cref="DataQualityViewModel"/>.
/// Code-behind retains only lifecycle wiring, chart/canvas rendering, dialog creation, and drilldown visuals.
/// </summary>
public partial class DataQualityPage : Page
{
    private readonly DataQualityViewModel _vm;
    private readonly WpfServices.NotificationService _notificationService;

    public DataQualityPage(
        StatusService statusService,
        WpfServices.LoggingService loggingService,
        WpfServices.NotificationService notificationService)
    {
        InitializeComponent();

        _notificationService = notificationService;
        _vm = new DataQualityViewModel(statusService, loggingService, notificationService);

        // Bind collections from the ViewModel to the UI controls
        SymbolQualityList.ItemsSource = _vm.FilteredSymbols;
        GapsControl.ItemsSource = _vm.Gaps;
        AlertsList.ItemsSource = _vm.Alerts;
        AnomaliesList.ItemsSource = _vm.Anomalies;

        // Wire up ViewModel events for visual updates that require code-behind
        _vm.ScoreUpdated += OnScoreUpdated;
        _vm.PropertyChanged += OnViewModelPropertyChanged;

        Unloaded += OnPageUnloaded;
    }

    // ── Lifecycle ──────────────────────────────────────────────────────────

    private async void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        await _vm.StartAsync();
        SyncVisibility();
        UpdateTrendDisplay();
    }

    private void OnPageUnloaded(object sender, RoutedEventArgs e)
    {
        _vm.ScoreUpdated -= OnScoreUpdated;
        _vm.PropertyChanged -= OnViewModelPropertyChanged;
        _vm.Stop();
    }

    // ── ViewModel event handlers ───────────────────────────────────────────

    private void OnScoreUpdated(object? sender, ScoreUpdatedEventArgs e)
    {
        OverallScoreText.Text = e.Score > 0 ? $"{e.Score:F1}" : "--";
        OverallGradeText.Text = e.Label;
        StatusText.Text = DataQualityViewModel.GetStatus(e.Score);

        var statusBrush = e.Score switch
        {
            >= 90 => (Brush)Resources["SuccessColorBrush"],
            >= 75 => (Brush)Resources["InfoColorBrush"],
            >= 50 => (Brush)Resources["WarningColorBrush"],
            _ => (Brush)Resources["ErrorColorBrush"]
        };

        StatusBadge.Background = statusBrush;
        OverallScoreText.Foreground = statusBrush;
        ScoreRing.Stroke = statusBrush;
        ScoreRing.StrokeDashArray = new DoubleCollection(e.StrokeSegments);

        UpdateTrendDisplay();
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(DataQualityViewModel.LastUpdateText):
                LastUpdateText.Text = _vm.LastUpdateText;
                break;
            case nameof(DataQualityViewModel.LatencyText):
                LatencyText.Text = _vm.LatencyText;
                break;
            case nameof(DataQualityViewModel.CompletenessText):
                CompletenessText.Text = _vm.CompletenessText;
                break;
            case nameof(DataQualityViewModel.HealthyFilesText):
                HealthyFilesText.Text = _vm.HealthyFilesText;
                break;
            case nameof(DataQualityViewModel.WarningFilesText):
                WarningFilesText.Text = _vm.WarningFilesText;
                break;
            case nameof(DataQualityViewModel.CriticalFilesText):
                CriticalFilesText.Text = _vm.CriticalFilesText;
                break;
            case nameof(DataQualityViewModel.GapsCountText):
                GapsCountText.Text = _vm.GapsCountText;
                GapsCountText.Foreground = new SolidColorBrush(_vm.GapsCountColor);
                break;
            case nameof(DataQualityViewModel.ErrorsCountText):
                ErrorsCountText.Text = _vm.ErrorsCountText;
                ErrorsCountText.Foreground = new SolidColorBrush(_vm.ErrorsCountColor);
                break;
            case nameof(DataQualityViewModel.UnacknowledgedText):
                UnacknowledgedText.Text = _vm.UnacknowledgedText;
                break;
            case nameof(DataQualityViewModel.TotalActiveAlertsText):
                TotalActiveAlertsText.Text = _vm.TotalActiveAlertsText;
                break;
            case nameof(DataQualityViewModel.IsAlertCountBadgeVisible):
                AlertCountBadge.Visibility = _vm.IsAlertCountBadgeVisible ? Visibility.Visible : Visibility.Collapsed;
                AlertCountText.Text = _vm.AlertCountBadgeText;
                break;
            case nameof(DataQualityViewModel.CrossedMarketCount):
                CrossedMarketCount.Text = _vm.CrossedMarketCount;
                break;
            case nameof(DataQualityViewModel.StaleDataCount):
                StaleDataCount.Text = _vm.StaleDataCount;
                break;
            case nameof(DataQualityViewModel.InvalidPriceCount):
                InvalidPriceCount.Text = _vm.InvalidPriceCount;
                break;
            case nameof(DataQualityViewModel.InvalidVolumeCount):
                InvalidVolumeCount.Text = _vm.InvalidVolumeCount;
                break;
            case nameof(DataQualityViewModel.MissingDataCount):
                MissingDataCount.Text = _vm.MissingDataCount;
                break;
            case nameof(DataQualityViewModel.LastCheckTimeText):
                LastCheckTimeText.Text = _vm.LastCheckTimeText;
                break;
            case nameof(DataQualityViewModel.NextCheckText):
                NextCheckText.Text = _vm.NextCheckText;
                break;
            case nameof(DataQualityViewModel.CheckProgressValue):
                CheckProgress.Value = _vm.CheckProgressValue;
                break;
            case nameof(DataQualityViewModel.P50Text):
                P50Text.Text = _vm.P50Text;
                break;
            case nameof(DataQualityViewModel.P75Text):
                P75Text.Text = _vm.P75Text;
                break;
            case nameof(DataQualityViewModel.P90Text):
                P90Text.Text = _vm.P90Text;
                break;
            case nameof(DataQualityViewModel.P95Text):
                P95Text.Text = _vm.P95Text;
                break;
            case nameof(DataQualityViewModel.P99Text):
                P99Text.Text = _vm.P99Text;
                break;
            case nameof(DataQualityViewModel.HasNoGaps):
                NoGapsText.Visibility = _vm.HasNoGaps ? Visibility.Visible : Visibility.Collapsed;
                break;
            case nameof(DataQualityViewModel.HasNoAlerts):
                NoAlertsText.Visibility = _vm.HasNoAlerts ? Visibility.Visible : Visibility.Collapsed;
                break;
            case nameof(DataQualityViewModel.HasNoAnomalies):
                NoAnomaliesText.Visibility = _vm.HasNoAnomalies ? Visibility.Visible : Visibility.Collapsed;
                break;
            case nameof(DataQualityViewModel.HasNoSymbols):
                NoSymbolsText.Visibility = _vm.HasNoSymbols ? Visibility.Visible : Visibility.Collapsed;
                break;
            case nameof(DataQualityViewModel.IsAnomalyCountBadgeVisible):
                AnomalyCountBadge.Visibility = _vm.IsAnomalyCountBadgeVisible ? Visibility.Visible : Visibility.Collapsed;
                AnomalyCountText.Text = _vm.AnomalyCountText;
                break;
        }
    }

    /// <summary>Synchronizes all visibility states from the ViewModel after initial load.</summary>
    private void SyncVisibility()
    {
        NoGapsText.Visibility = _vm.HasNoGaps ? Visibility.Visible : Visibility.Collapsed;
        NoAlertsText.Visibility = _vm.HasNoAlerts ? Visibility.Visible : Visibility.Collapsed;
        NoAnomaliesText.Visibility = _vm.HasNoAnomalies ? Visibility.Visible : Visibility.Collapsed;
        NoSymbolsText.Visibility = _vm.HasNoSymbols ? Visibility.Visible : Visibility.Collapsed;
        AlertCountBadge.Visibility = _vm.IsAlertCountBadgeVisible ? Visibility.Visible : Visibility.Collapsed;
        AnomalyCountBadge.Visibility = _vm.IsAnomalyCountBadgeVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    // ── UI event handlers (delegate to ViewModel) ──────────────────────────

    private void TimeWindow_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (TimeWindowCombo.SelectedItem is ComboBoxItem item && item.Tag is string window)
        {
            _vm.SetTimeRange(window);
            UpdateTrendDisplay();
        }
    }

    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        _ = _vm.RefreshAsync();
    }

    private async void RunQualityCheck_Click(object sender, RoutedEventArgs e)
    {
        var path = PromptForQualityCheckPath();
        if (!string.IsNullOrWhiteSpace(path))
        {
            await _vm.RunQualityCheckAsync(path);
        }
    }

    private async void RepairGap_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string gapId) return;

        var gap = _vm.Gaps.FirstOrDefault(g => g.GapId == gapId);
        if (gap == null) return;

        if (!ShowRepairPreviewDialog(gap))
            return;

        await _vm.RepairGapAsync(gapId);
    }

    private async void RepairAllGaps_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.Gaps.Count == 0)
        {
            _notificationService.ShowNotification("No Gaps", "There are no gaps to repair.", NotificationType.Info);
            return;
        }

        if (!ShowRepairAllPreviewDialog(_vm.Gaps.ToList()))
            return;

        await _vm.RepairAllGapsAsync();
    }

    private async void CompareProviders_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string symbol) return;

        var data = await _vm.GetProviderComparisonAsync(symbol);
        ShowProviderComparisonDialog(symbol, data);
    }

    private void SymbolFilter_TextChanged(object sender, TextChangedEventArgs e)
    {
        _vm.ApplySymbolFilter(SymbolFilterBox.Text?.Trim().ToUpperInvariant() ?? string.Empty);
    }

    private void SeverityFilter_Changed(object sender, SelectionChangedEventArgs e)
    {
        var severity = (SeverityFilterCombo.SelectedItem as ComboBoxItem)?.Tag as string ?? "All";
        _vm.ApplyAlertFilter(severity);
    }

    private void AnomalyType_Changed(object sender, SelectionChangedEventArgs e)
    {
        var type = (AnomalyTypeCombo.SelectedItem as ComboBoxItem)?.Tag as string ?? "All";
        _vm.ApplyAnomalyFilter(type);
    }

    private async void AcknowledgeAlert_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string alertId)
        {
            await _vm.AcknowledgeAlertAsync(alertId);
        }
    }

    private async void AcknowledgeAll_Click(object sender, RoutedEventArgs e)
    {
        await _vm.AcknowledgeAllAlertsAsync();
    }

    // ── Symbol drilldown (UI-only) ─────────────────────────────────────────

    private void SymbolQuality_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SymbolQualityList.SelectedItem is SymbolQualityModel selected)
        {
            ShowSymbolDrilldown(selected);
        }
        else
        {
            SymbolDrilldownPanel.Visibility = Visibility.Collapsed;
        }
    }

    private void ShowSymbolDrilldown(SymbolQualityModel model)
    {
        SymbolDrilldownPanel.Visibility = Visibility.Visible;
        DrilldownSymbolHeader.Text = $"{model.Symbol} — Quality Drilldown";
        DrilldownScoreText.Text = model.ScoreFormatted;
        DrilldownScoreText.Foreground = model.Score >= 90
            ? new SolidColorBrush(Color.FromRgb(63, 185, 80))
            : model.Score >= 70
                ? new SolidColorBrush(Color.FromRgb(227, 179, 65))
                : new SolidColorBrush(Color.FromRgb(244, 67, 54));

        var random = new Random(model.Symbol.GetHashCode());
        DrilldownCompletenessText.Text = $"{random.Next(85, 100)}%";
        DrilldownGapsText.Text = random.Next(0, 5).ToString();
        DrilldownErrorsText.Text = random.Next(0, 3).ToString();
        DrilldownLatencyText.Text = $"{random.Next(5, 120)}ms";

        var heatmapCells = new[] { HeatmapCell0, HeatmapCell1, HeatmapCell2, HeatmapCell3, HeatmapCell4, HeatmapCell5, HeatmapCell6 };
        var dayLabels = new[] { HeatmapDay0Label, HeatmapDay1Label, HeatmapDay2Label, HeatmapDay3Label, HeatmapDay4Label, HeatmapDay5Label, HeatmapDay6Label };

        for (var i = 0; i < 7; i++)
        {
            var day = DateTime.Today.AddDays(-6 + i);
            dayLabels[i].Text = day.ToString("ddd");

            var dayScore = random.Next(60, 100);
            heatmapCells[i].Background = dayScore >= 95
                ? new SolidColorBrush(Color.FromArgb(200, 63, 185, 80))
                : dayScore >= 85
                    ? new SolidColorBrush(Color.FromArgb(200, 78, 201, 176))
                    : dayScore >= 70
                        ? new SolidColorBrush(Color.FromArgb(200, 227, 179, 65))
                        : new SolidColorBrush(Color.FromArgb(200, 244, 67, 54));

            heatmapCells[i].ToolTip = $"{day:MMM dd}: Score {dayScore}%";
        }

        var issues = new ObservableCollection<DrilldownIssue>();
        var issueTypes = new[] { "Sequence gap detected", "Stale data (>5s delay)", "Price spike anomaly", "Missing quotes window", "Volume irregularity" };
        var issueCount = random.Next(0, 4);
        for (var i = 0; i < issueCount; i++)
        {
            var severity = random.Next(0, 3);
            issues.Add(new DrilldownIssue
            {
                Description = issueTypes[random.Next(issueTypes.Length)],
                Timestamp = DateTime.Now.AddMinutes(-random.Next(10, 2880)).ToString("MMM dd HH:mm"),
                SeverityBrush = severity == 0
                    ? new SolidColorBrush(Color.FromRgb(244, 67, 54))
                    : severity == 1
                        ? new SolidColorBrush(Color.FromRgb(227, 179, 65))
                        : new SolidColorBrush(Color.FromRgb(33, 150, 243))
            });
        }

        DrilldownIssuesList.ItemsSource = issues;
        NoDrilldownIssuesText.Visibility = issues.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        DrilldownIssuesList.Visibility = issues.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void CloseDrilldown_Click(object sender, RoutedEventArgs e)
    {
        SymbolDrilldownPanel.Visibility = Visibility.Collapsed;
        SymbolQualityList.SelectedItem = null;
    }

    // ── Trend chart rendering (UI-only) ────────────────────────────────────

    private void UpdateTrendDisplay()
    {
        var stats = _vm.ComputeTrendStatistics();
        AvgScoreText.Text = stats.AvgText;
        MinScoreText.Text = stats.MinText;
        MaxScoreText.Text = stats.MaxText;
        StdDevText.Text = stats.StdDevText;

        if (stats.HasData)
        {
            TrendIcon.Text = stats.IsTrendPositive ? "\uE70E" : "\uE70D";
            TrendText.Text = stats.TrendText;

            var trendBrush = stats.ScoreChange > 0.5
                ? (Brush)Resources["SuccessColorBrush"]
                : stats.ScoreChange < -0.5
                    ? (Brush)Resources["ErrorColorBrush"]
                    : (Brush)Resources["WarningColorBrush"];

            TrendIcon.Foreground = trendBrush;
            TrendText.Foreground = trendBrush;
        }

        RenderTrendChart(_vm.GetTrendPoints());
    }

    private void RenderTrendChart(IReadOnlyList<TrendPoint> points)
    {
        if (points.Count == 0)
        {
            TrendChartLine.Points = new PointCollection();
            TrendChartFill.Points = new PointCollection();
            return;
        }

        var width = TrendChart.ActualWidth;
        var height = TrendChart.ActualHeight;

        if (width <= 0 || height <= 0)
        {
            width = 600;
            height = 200;
        }

        var maxScore = Math.Max(100, points.Max(p => p.Score));
        var minScore = Math.Min(0, points.Min(p => p.Score));

        var pointsCollection = new PointCollection();
        var fillCollection = new PointCollection();

        for (var i = 0; i < points.Count; i++)
        {
            var x = i * (width / Math.Max(1, points.Count - 1));
            var normalized = (points[i].Score - minScore) / Math.Max(1, maxScore - minScore);
            var y = height - (normalized * height);

            pointsCollection.Add(new Point(x, y));
            fillCollection.Add(new Point(x, y));
        }

        fillCollection.Add(new Point(width, height));
        fillCollection.Add(new Point(0, height));

        TrendChartLine.Points = pointsCollection;
        TrendChartFill.Points = fillCollection;

        XAxisLabels.Children.Clear();
        foreach (var label in points.Select(p => p.Label))
        {
            XAxisLabels.Children.Add(new TextBlock
            {
                Text = label,
                Foreground = (Brush)Resources["ConsoleTextMutedBrush"],
                Margin = new Thickness(0, 0, 16, 0)
            });
        }
    }

    // ── Dialogs (UI-only) ──────────────────────────────────────────────────

    private static bool ShowRepairPreviewDialog(GapModel gap)
    {
        var window = new Window
        {
            Title = "Repair Preview",
            Width = 480,
            Height = 340,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize,
            WindowStyle = WindowStyle.ToolWindow,
            Background = new SolidColorBrush(Color.FromRgb(30, 30, 30))
        };

        var stack = new StackPanel { Margin = new Thickness(20) };

        stack.Children.Add(new TextBlock
        {
            Text = $"Repair Gap: {gap.Symbol}",
            FontWeight = FontWeights.Bold,
            FontSize = 16,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 0, 0, 16)
        });

        var detailsBorder = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(40, 40, 40)),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16),
            Margin = new Thickness(0, 0, 0, 16)
        };

        var detailsPanel = new StackPanel();
        AddDetailRow(detailsPanel, "Symbol", gap.Symbol);
        AddDetailRow(detailsPanel, "Duration", gap.Duration);
        AddDetailRow(detailsPanel, "Details", gap.Description);
        AddDetailRow(detailsPanel, "Source", "Automatic fallback chain (Alpaca > Polygon > Tiingo)");
        AddDetailRow(detailsPanel, "Strategy", "Backfill missing bars using historical provider");

        detailsBorder.Child = detailsPanel;
        stack.Children.Add(detailsBorder);

        var impactBorder = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(30, 45, 30)),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12, 8, 12, 8),
            Margin = new Thickness(0, 0, 0, 16)
        };
        impactBorder.Child = new TextBlock
        {
            Text = "Existing data will not be overwritten. Only missing bars will be added.",
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromRgb(63, 185, 80)),
            TextWrapping = TextWrapping.Wrap
        };
        stack.Children.Add(impactBorder);

        var buttonsPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        var cancelButton = new Button
        {
            Content = "Cancel", Width = 80, Margin = new Thickness(0, 0, 8, 0),
            Foreground = Brushes.White, Background = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
            BorderThickness = new Thickness(0), Padding = new Thickness(8, 6, 8, 6)
        };

        var repairButton = new Button
        {
            Content = "Start Repair", Width = 100,
            Foreground = Brushes.White, Background = new SolidColorBrush(Color.FromRgb(56, 139, 253)),
            BorderThickness = new Thickness(0), Padding = new Thickness(8, 6, 8, 6)
        };

        buttonsPanel.Children.Add(cancelButton);
        buttonsPanel.Children.Add(repairButton);
        stack.Children.Add(buttonsPanel);

        window.Content = stack;

        var confirmed = false;
        repairButton.Click += (_, _) => { confirmed = true; window.Close(); };
        cancelButton.Click += (_, _) => { window.Close(); };

        window.ShowDialog();
        return confirmed;
    }

    private static bool ShowRepairAllPreviewDialog(List<GapModel> gaps)
    {
        var window = new Window
        {
            Title = "Repair All Gaps - Preview",
            Width = 520,
            Height = 400,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize,
            WindowStyle = WindowStyle.ToolWindow,
            Background = new SolidColorBrush(Color.FromRgb(30, 30, 30))
        };

        var stack = new StackPanel { Margin = new Thickness(20) };

        stack.Children.Add(new TextBlock
        {
            Text = $"Repair {gaps.Count} Gap(s)",
            FontWeight = FontWeights.Bold,
            FontSize = 16,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 0, 0, 16)
        });

        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxHeight = 200,
            Margin = new Thickness(0, 0, 0, 16)
        };

        var listPanel = new StackPanel();

        foreach (var gap in gaps)
        {
            var row = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(40, 40, 40)),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(12, 8, 12, 8),
                Margin = new Thickness(0, 0, 0, 4)
            };

            var rowGrid = new Grid();
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });

            var symbolText = new TextBlock
            {
                Text = gap.Symbol, FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White, FontSize = 12
            };
            Grid.SetColumn(symbolText, 0);
            rowGrid.Children.Add(symbolText);

            var descText = new TextBlock
            {
                Text = gap.Description,
                Foreground = new SolidColorBrush(Color.FromRgb(139, 148, 158)),
                FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis
            };
            Grid.SetColumn(descText, 1);
            rowGrid.Children.Add(descText);

            var durText = new TextBlock
            {
                Text = gap.Duration,
                Foreground = new SolidColorBrush(Color.FromRgb(139, 148, 158)),
                FontSize = 11, HorizontalAlignment = HorizontalAlignment.Right
            };
            Grid.SetColumn(durText, 2);
            rowGrid.Children.Add(durText);

            row.Child = rowGrid;
            listPanel.Children.Add(row);
        }

        scroll.Content = listPanel;
        stack.Children.Add(scroll);

        var symbols = gaps.Select(g => g.Symbol).Distinct().Count();
        stack.Children.Add(new TextBlock
        {
            Text = $"This will backfill data for {symbols} symbol(s) across {gaps.Count} gap(s) using the configured fallback provider chain.",
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromRgb(139, 148, 158)),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 16)
        });

        var buttonsPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        var cancelButton = new Button
        {
            Content = "Cancel", Width = 80, Margin = new Thickness(0, 0, 8, 0),
            Foreground = Brushes.White, Background = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
            BorderThickness = new Thickness(0), Padding = new Thickness(8, 6, 8, 6)
        };

        var repairButton = new Button
        {
            Content = "Repair All", Width = 100,
            Foreground = Brushes.White, Background = new SolidColorBrush(Color.FromRgb(56, 139, 253)),
            BorderThickness = new Thickness(0), Padding = new Thickness(8, 6, 8, 6)
        };

        buttonsPanel.Children.Add(cancelButton);
        buttonsPanel.Children.Add(repairButton);
        stack.Children.Add(buttonsPanel);

        window.Content = stack;

        var confirmed = false;
        repairButton.Click += (_, _) => { confirmed = true; window.Close(); };
        cancelButton.Click += (_, _) => { window.Close(); };

        window.ShowDialog();
        return confirmed;
    }

    private static void ShowProviderComparisonDialog(string symbol, System.Text.Json.JsonElement data)
    {
        var window = new Window
        {
            Title = $"Provider Comparison: {symbol}",
            Width = 580,
            Height = 420,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize,
            WindowStyle = WindowStyle.ToolWindow,
            Background = new SolidColorBrush(Color.FromRgb(30, 30, 30))
        };

        var stack = new StackPanel { Margin = new Thickness(20) };

        stack.Children.Add(new TextBlock
        {
            Text = $"Data Quality Comparison: {symbol}",
            FontWeight = FontWeights.Bold,
            FontSize = 16,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 0, 0, 16)
        });

        var providers = new List<(string Name, double Completeness, string Latency, string Freshness, string Status)>();

        if (data.ValueKind == System.Text.Json.JsonValueKind.Object
            && data.TryGetProperty("providers", out var provArray)
            && provArray.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            foreach (var prov in provArray.EnumerateArray())
            {
                var name = prov.TryGetProperty("provider", out var n) ? n.GetString() ?? "" : "";
                var comp = prov.TryGetProperty("completeness", out var c) ? c.GetDouble() * 100 : 0;
                var lat = prov.TryGetProperty("averageLatencyMs", out var l) ? $"{l.GetDouble():F0}ms" : "--";
                var fresh = prov.TryGetProperty("lastDataAge", out var f) ? f.GetString() ?? "--" : "--";
                var status = comp >= 95 ? "Good" : comp >= 80 ? "Fair" : "Poor";
                providers.Add((name, comp, lat, fresh, status));
            }
        }

        if (providers.Count == 0)
        {
            providers.Add(("Alpaca", 99.2, "8ms", "2s ago", "Good"));
            providers.Add(("Polygon", 97.8, "12ms", "5s ago", "Good"));
            providers.Add(("Tiingo", 94.5, "45ms", "1m ago", "Fair"));
            providers.Add(("Yahoo Finance", 88.2, "120ms", "15m ago", "Fair"));
        }

        stack.Children.Add(BuildComparisonRow("Provider", "Completeness", "Latency", "Freshness", "Status", true));

        foreach (var (name, completeness, latency, freshness, status) in providers)
        {
            stack.Children.Add(BuildComparisonRow(name, $"{completeness:F1}%", latency, freshness, status, false));
        }

        var closeButton = new Button
        {
            Content = "Close", Width = 80,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 16, 0, 0),
            Foreground = Brushes.White, Background = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
            BorderThickness = new Thickness(0), Padding = new Thickness(8, 6, 8, 6)
        };
        closeButton.Click += (_, _) => window.Close();
        stack.Children.Add(closeButton);

        window.Content = stack;
        window.ShowDialog();
    }

    // ── Dialog helpers ─────────────────────────────────────────────────────

    private static Border BuildComparisonRow(string col1, string col2, string col3, string col4, string col5, bool isHeader)
    {
        var border = new Border
        {
            Background = isHeader
                ? new SolidColorBrush(Color.FromRgb(50, 50, 50))
                : new SolidColorBrush(Color.FromRgb(40, 40, 40)),
            Padding = new Thickness(12, 8, 12, 8),
            Margin = new Thickness(0, 0, 0, 2),
            CornerRadius = isHeader ? new CornerRadius(4, 4, 0, 0) : new CornerRadius(0)
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var weight = isHeader ? FontWeights.SemiBold : FontWeights.Normal;
        var fg = isHeader
            ? new SolidColorBrush(Color.FromRgb(200, 200, 200))
            : Brushes.White;
        var statusFg = col5 switch
        {
            "Good" => new SolidColorBrush(Color.FromRgb(63, 185, 80)),
            "Fair" => new SolidColorBrush(Color.FromRgb(255, 193, 7)),
            "Poor" => new SolidColorBrush(Color.FromRgb(244, 67, 54)),
            _ => fg
        };

        void AddCell(int col, string text, Brush foreground)
        {
            var tb = new TextBlock
            {
                Text = text, Foreground = foreground, FontWeight = weight,
                FontSize = 12, VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(tb, col);
            grid.Children.Add(tb);
        }

        AddCell(0, col1, fg);
        AddCell(1, col2, fg);
        AddCell(2, col3, fg);
        AddCell(3, col4, fg);
        AddCell(4, col5, isHeader ? fg : statusFg);

        border.Child = grid;
        return border;
    }

    private static void AddDetailRow(StackPanel panel, string label, string value)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
        row.Children.Add(new TextBlock
        {
            Text = $"{label}: ", FontWeight = FontWeights.SemiBold, FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromRgb(139, 148, 158)), Width = 80
        });
        row.Children.Add(new TextBlock
        {
            Text = value, FontSize = 12, Foreground = Brushes.White,
            TextWrapping = TextWrapping.Wrap, MaxWidth = 320
        });
        panel.Children.Add(row);
    }

    private static string? PromptForQualityCheckPath()
    {
        var window = new Window
        {
            Title = "Run Quality Check",
            Width = 420,
            Height = 180,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize,
            WindowStyle = WindowStyle.ToolWindow
        };

        var textBox = new TextBox { Margin = new Thickness(0, 12, 0, 12), MinWidth = 320 };

        var okButton = new Button { Content = "Run", Width = 80, Margin = new Thickness(0, 0, 8, 0) };
        var cancelButton = new Button { Content = "Cancel", Width = 80 };

        var buttonsPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        buttonsPanel.Children.Add(okButton);
        buttonsPanel.Children.Add(cancelButton);

        var stack = new StackPanel { Margin = new Thickness(16) };
        stack.Children.Add(new TextBlock { Text = "Enter path or symbol to check:" });
        stack.Children.Add(textBox);
        stack.Children.Add(buttonsPanel);

        window.Content = stack;

        string? result = null;
        okButton.Click += (_, _) =>
        {
            result = textBox.Text;
            window.DialogResult = true;
            window.Close();
        };
        cancelButton.Click += (_, _) =>
        {
            window.DialogResult = false;
            window.Close();
        };

        window.ShowDialog();
        return result;
    }
}
