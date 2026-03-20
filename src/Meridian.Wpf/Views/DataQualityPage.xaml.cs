using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Meridian.Wpf.Models;
using Meridian.Wpf.ViewModels;

namespace Meridian.Wpf.Views;

/// <summary>
/// Data quality monitoring page showing completeness, gaps, and anomalies.
/// The page is responsible only for DI-backed viewmodel binding, lifecycle hooks, chart rendering, and dialogs.
/// </summary>
public partial class DataQualityPage : Page
{
    private readonly DataQualityViewModel _viewModel;

    public DataQualityPage(DataQualityViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;

        Loaded += OnPageLoaded;
        Unloaded += OnPageUnloaded;
        SizeChanged += (_, _) => RenderTrendChart(_viewModel.TrendPoints);
        _viewModel.TrendChartChanged += (_, _) => RenderTrendChart(_viewModel.TrendPoints);
        _viewModel.DrilldownChanged += (_, _) => ApplyDrilldownHeatmap();
    }

    private async void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.StartAsync();
        RenderTrendChart(_viewModel.TrendPoints);
        ApplyDrilldownHeatmap();
    }

    private void OnPageUnloaded(object sender, RoutedEventArgs e)
    {
        _viewModel.Stop();
    }

    private void TimeWindow_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (TimeWindowCombo.SelectedItem is ComboBoxItem item && item.Tag is string window)
        {
            _viewModel.SetTimeRange(window);
        }
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.RefreshAsync();
    }

    private async void RunQualityCheck_Click(object sender, RoutedEventArgs e)
    {
        var path = PromptForQualityCheckPath();
        if (!string.IsNullOrWhiteSpace(path))
        {
            await _viewModel.RunQualityCheckAsync(path);
        }
    }

    private async void RepairGap_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string gapId)
        {
            return;
        }

        var gap = _viewModel.Gaps.FirstOrDefault(g => g.GapId == gapId);
        if (gap != null && ShowRepairPreviewDialog(gap))
        {
            await _viewModel.RepairGapAsync(gapId);
        }
    }

    private async void RepairAllGaps_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.Gaps.Count == 0)
        {
            return;
        }

        if (ShowRepairAllPreviewDialog(_viewModel.Gaps.ToList()))
        {
            await _viewModel.RepairAllGapsAsync();
        }
    }

    private async void CompareProviders_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string symbol)
        {
            return;
        }

        var comparison = await _viewModel.GetProviderComparisonAsync(symbol);
        ShowProviderComparisonDialog(comparison.Symbol, comparison.Providers);
    }

    private void SymbolFilter_TextChanged(object sender, TextChangedEventArgs e)
    {
        _viewModel.ApplySymbolFilter(SymbolFilterBox.Text?.Trim() ?? string.Empty);
    }

    private void SymbolQuality_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SymbolQualityList.SelectedItem is SymbolQualityModel selected)
        {
            _viewModel.ShowSymbolDrilldown(selected);
        }
        else
        {
            _viewModel.HideSymbolDrilldown();
        }
    }

    private void CloseDrilldown_Click(object sender, RoutedEventArgs e)
    {
        SymbolQualityList.SelectedItem = null;
        _viewModel.HideSymbolDrilldown();
    }

    private void SeverityFilter_Changed(object sender, SelectionChangedEventArgs e)
    {
        var severity = (SeverityFilterCombo.SelectedItem as ComboBoxItem)?.Tag as string ?? "All";
        _viewModel.ApplyAlertFilter(severity);
    }

    private async void AcknowledgeAlert_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string alertId)
        {
            await _viewModel.AcknowledgeAlertAsync(alertId);
        }
    }

    private async void AcknowledgeAll_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.AcknowledgeAllAlertsAsync();
    }

    private void AnomalyType_Changed(object sender, SelectionChangedEventArgs e)
    {
        var type = (AnomalyTypeCombo.SelectedItem as ComboBoxItem)?.Tag as string ?? "All";
        _viewModel.ApplyAnomalyFilter(type);
    }

    private void RenderTrendChart(IReadOnlyList<TrendPoint> points)
    {
        if (points.Count == 0)
        {
            TrendChartLine.Points = new PointCollection();
            TrendChartFill.Points = new PointCollection();
            XAxisLabels.Children.Clear();
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
            var y = height - normalized * height;

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

    private void ApplyDrilldownHeatmap()
    {
        var heatmapCells = new[] { HeatmapCell0, HeatmapCell1, HeatmapCell2, HeatmapCell3, HeatmapCell4, HeatmapCell5, HeatmapCell6 };
        var dayLabels = new[] { HeatmapDay0Label, HeatmapDay1Label, HeatmapDay2Label, HeatmapDay3Label, HeatmapDay4Label, HeatmapDay5Label, HeatmapDay6Label };

        for (var i = 0; i < heatmapCells.Length; i++)
        {
            if (i >= _viewModel.DrilldownHeatmapCells.Count)
            {
                heatmapCells[i].Background = Brushes.Transparent;
                heatmapCells[i].ToolTip = null;
                dayLabels[i].Text = string.Empty;
                continue;
            }

            var cell = _viewModel.DrilldownHeatmapCells[i];
            dayLabels[i].Text = cell.Label;
            heatmapCells[i].Background = cell.Tone switch
            {
                Meridian.Ui.Services.DataQuality.DataQualityVisualTones.Success => new SolidColorBrush(Color.FromArgb(200, 63, 185, 80)),
                Meridian.Ui.Services.DataQuality.DataQualityVisualTones.Info => new SolidColorBrush(Color.FromArgb(200, 78, 201, 176)),
                Meridian.Ui.Services.DataQuality.DataQualityVisualTones.Warning => new SolidColorBrush(Color.FromArgb(200, 227, 179, 65)),
                _ => new SolidColorBrush(Color.FromArgb(200, 244, 67, 54))
            };
            heatmapCells[i].ToolTip = cell.Tooltip;
        }
    }

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
            Content = "Cancel",
            Width = 80,
            Margin = new Thickness(0, 0, 8, 0),
            Foreground = Brushes.White,
            Background = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(8, 6, 8, 6)
        };

        var repairButton = new Button
        {
            Content = "Start Repair",
            Width = 100,
            Foreground = Brushes.White,
            Background = new SolidColorBrush(Color.FromRgb(56, 139, 253)),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(8, 6, 8, 6)
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

            var symbolText = new TextBlock { Text = gap.Symbol, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White, FontSize = 12 };
            Grid.SetColumn(symbolText, 0);
            rowGrid.Children.Add(symbolText);

            var descText = new TextBlock
            {
                Text = gap.Description,
                Foreground = new SolidColorBrush(Color.FromRgb(139, 148, 158)),
                FontSize = 11,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Grid.SetColumn(descText, 1);
            rowGrid.Children.Add(descText);

            var durText = new TextBlock
            {
                Text = gap.Duration,
                Foreground = new SolidColorBrush(Color.FromRgb(139, 148, 158)),
                FontSize = 11,
                HorizontalAlignment = HorizontalAlignment.Right
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

        var buttonsPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var cancelButton = new Button
        {
            Content = "Cancel",
            Width = 80,
            Margin = new Thickness(0, 0, 8, 0),
            Foreground = Brushes.White,
            Background = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(8, 6, 8, 6)
        };

        var repairButton = new Button
        {
            Content = "Repair All",
            Width = 100,
            Foreground = Brushes.White,
            Background = new SolidColorBrush(Color.FromRgb(56, 139, 253)),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(8, 6, 8, 6)
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

    private static void ShowProviderComparisonDialog(string symbol, IReadOnlyList<Meridian.Ui.Services.DataQuality.DataQualityProviderComparisonItem> providers)
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

        stack.Children.Add(BuildComparisonRow("Provider", "Completeness", "Latency", "Freshness", "Status", true));
        foreach (var provider in providers)
        {
            stack.Children.Add(BuildComparisonRow(
                provider.Name,
                provider.CompletenessText,
                provider.LatencyText,
                provider.FreshnessText,
                provider.Status,
                false));
        }

        var closeButton = new Button
        {
            Content = "Close",
            Width = 80,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 16, 0, 0),
            Foreground = Brushes.White,
            Background = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(8, 6, 8, 6)
        };
        closeButton.Click += (_, _) => window.Close();
        stack.Children.Add(closeButton);

        window.Content = stack;
        window.ShowDialog();
    }

    private static Border BuildComparisonRow(string col1, string col2, string col3, string col4, string col5, bool isHeader)
    {
        var border = new Border
        {
            Background = isHeader ? new SolidColorBrush(Color.FromRgb(50, 50, 50)) : new SolidColorBrush(Color.FromRgb(40, 40, 40)),
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
        var fg = isHeader ? new SolidColorBrush(Color.FromRgb(200, 200, 200)) : Brushes.White;
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
                Text = text,
                Foreground = foreground,
                FontWeight = weight,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center
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
            Text = $"{label}: ",
            FontWeight = FontWeights.SemiBold,
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromRgb(139, 148, 158)),
            Width = 80
        });
        row.Children.Add(new TextBlock
        {
            Text = value,
            FontSize = 12,
            Foreground = Brushes.White,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 320
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

        var buttonsPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
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
