using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MarketDataCollector.Ui.Services;
using MarketDataCollector.Wpf.ViewModels;
using WpfServices = MarketDataCollector.Wpf.Services;

namespace MarketDataCollector.Wpf.Views;

public partial class DataQualityPage : Page
{
    private readonly DataQualityViewModel _viewModel;

    public DataQualityPage(
        StatusService statusService,
        WpfServices.LoggingService loggingService,
        WpfServices.NotificationService notificationService)
    {
        InitializeComponent();
        _viewModel = new DataQualityViewModel(statusService, loggingService, notificationService);
        DataContext = _viewModel;

        SymbolQualityList.ItemsSource = _viewModel.FilteredSymbols;
        GapsControl.ItemsSource = _viewModel.Gaps;
        AlertsList.ItemsSource = _viewModel.Alerts;
        AnomaliesList.ItemsSource = _viewModel.Anomalies;
    }

    private void OnPageLoaded(object sender, RoutedEventArgs e) => _viewModel.Start();
    private void OnPageUnloaded(object sender, RoutedEventArgs e) => _viewModel.Dispose();

    private void TimeWindow_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox combo && combo.SelectedItem is ComboBoxItem item)
            _viewModel.SetTimeRangeCommand.Execute(item.Tag?.ToString() ?? "7d");
    }

    private void TrendChart_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (sender is FrameworkElement el)
            _viewModel.SetChartDimensions(el.ActualWidth, el.ActualHeight);
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) =>
        await _viewModel.RefreshCommand.ExecuteAsync(null);

    private async void RunQualityCheck_Click(object sender, RoutedEventArgs e)
    {
        var path = PromptForQualityCheckPath();
        if (!string.IsNullOrWhiteSpace(path))
            await _viewModel.RunQualityCheckAsync(path);
    }

    private async void RepairGap_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string gapId) return;
        var gap = _viewModel.Gaps.FirstOrDefault(g => g.GapId == gapId);
        if (gap == null) return;
        if (ShowRepairPreviewDialog(gap))
            await _viewModel.RepairGapAsync(gapId);
    }

    private async void RepairAllGaps_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.Gaps.Count == 0) return;
        if (ShowRepairAllPreviewDialog(_viewModel.Gaps.ToList()))
            await _viewModel.RepairAllGapsAsync();
    }

    private async void CompareProviders_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string symbol) return;
        var data = await _viewModel.GetProviderComparisonDataAsync(symbol);
        ShowProviderComparisonDialog(symbol, data);
    }

    private async void AcknowledgeAlert_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string alertId)
            await _viewModel.AcknowledgeAlertCommand.ExecuteAsync(alertId);
    }

    private async void AcknowledgeAll_Click(object sender, RoutedEventArgs e) =>
        await _viewModel.AcknowledgeAllAlertsCommand.ExecuteAsync(null);

    private void SymbolFilter_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox tb) _viewModel.SymbolFilter = tb.Text;
    }

    private void SymbolQuality_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _viewModel.SelectSymbolCommand.Execute(SymbolQualityList.SelectedItem as SymbolQualityModel);
    }

    private void CloseDrilldown_Click(object sender, RoutedEventArgs e) =>
        _viewModel.CloseDrilldownCommand.Execute(null);

    private void SeverityFilter_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox combo && combo.SelectedItem is ComboBoxItem item)
            _viewModel.AlertSeverityFilter = item.Tag?.ToString() ?? "All";
    }

    private void AnomalyType_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox combo && combo.SelectedItem is ComboBoxItem item)
            _viewModel.AnomalyTypeFilter = item.Tag?.ToString() ?? "All";
    }

    // ── Dialog builders (UI code — stays in code-behind) ─────────────────────────

    private static bool ShowRepairPreviewDialog(GapModel gap)
    {
        var window = new Window
        {
            Title = "Repair Preview", Width = 480, Height = 340,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize, WindowStyle = WindowStyle.ToolWindow,
            Background = new SolidColorBrush(Color.FromRgb(30, 30, 30))
        };
        var stack = new StackPanel { Margin = new Thickness(20) };
        stack.Children.Add(new TextBlock { Text = $"Repair Gap: {gap.Symbol}", FontWeight = FontWeights.Bold, FontSize = 16, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 16) });
        var detailsBorder = new Border { Background = new SolidColorBrush(Color.FromRgb(40, 40, 40)), CornerRadius = new CornerRadius(8), Padding = new Thickness(16), Margin = new Thickness(0, 0, 0, 16) };
        var detailsPanel = new StackPanel();
        AddDetailRow(detailsPanel, "Symbol", gap.Symbol);
        AddDetailRow(detailsPanel, "Duration", gap.Duration);
        AddDetailRow(detailsPanel, "Details", gap.Description);
        AddDetailRow(detailsPanel, "Source", "Automatic fallback chain (Alpaca > Polygon > Tiingo)");
        detailsBorder.Child = detailsPanel;
        stack.Children.Add(detailsBorder);
        var impactBorder = new Border { Background = new SolidColorBrush(Color.FromRgb(30, 45, 30)), CornerRadius = new CornerRadius(6), Padding = new Thickness(12, 8, 12, 8), Margin = new Thickness(0, 0, 0, 16) };
        impactBorder.Child = new TextBlock { Text = "Existing data will not be overwritten. Only missing bars will be added.", FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(63, 185, 80)), TextWrapping = TextWrapping.Wrap };
        stack.Children.Add(impactBorder);
        var buttonsPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var cancelButton = new Button { Content = "Cancel", Width = 80, Margin = new Thickness(0, 0, 8, 0), Foreground = Brushes.White, Background = new SolidColorBrush(Color.FromRgb(60, 60, 60)), BorderThickness = new Thickness(0), Padding = new Thickness(8, 6, 8, 6) };
        var repairButton = new Button { Content = "Start Repair", Width = 100, Foreground = Brushes.White, Background = new SolidColorBrush(Color.FromRgb(56, 139, 253)), BorderThickness = new Thickness(0), Padding = new Thickness(8, 6, 8, 6) };
        buttonsPanel.Children.Add(cancelButton); buttonsPanel.Children.Add(repairButton);
        stack.Children.Add(buttonsPanel);
        window.Content = stack;
        var confirmed = false;
        repairButton.Click += (_, _) => { confirmed = true; window.Close(); };
        cancelButton.Click += (_, _) => window.Close();
        window.ShowDialog();
        return confirmed;
    }

    private static bool ShowRepairAllPreviewDialog(List<GapModel> gaps)
    {
        var window = new Window { Title = "Repair All Gaps - Preview", Width = 520, Height = 400, WindowStartupLocation = WindowStartupLocation.CenterOwner, ResizeMode = ResizeMode.NoResize, WindowStyle = WindowStyle.ToolWindow, Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)) };
        var stack = new StackPanel { Margin = new Thickness(20) };
        stack.Children.Add(new TextBlock { Text = $"Repair {gaps.Count} Gap(s)", FontWeight = FontWeights.Bold, FontSize = 16, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 16) });
        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = 200, Margin = new Thickness(0, 0, 0, 16) };
        var listPanel = new StackPanel();
        foreach (var gap in gaps)
        {
            var row = new Border { Background = new SolidColorBrush(Color.FromRgb(40, 40, 40)), CornerRadius = new CornerRadius(4), Padding = new Thickness(12, 8, 12, 8), Margin = new Thickness(0, 0, 0, 4) };
            var rowGrid = new Grid();
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
            var st = new TextBlock { Text = gap.Symbol, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White, FontSize = 12 };
            Grid.SetColumn(st, 0); rowGrid.Children.Add(st);
            var dt = new TextBlock { Text = gap.Description, Foreground = new SolidColorBrush(Color.FromRgb(139, 148, 158)), FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis };
            Grid.SetColumn(dt, 1); rowGrid.Children.Add(dt);
            var dur = new TextBlock { Text = gap.Duration, Foreground = new SolidColorBrush(Color.FromRgb(139, 148, 158)), FontSize = 11, HorizontalAlignment = HorizontalAlignment.Right };
            Grid.SetColumn(dur, 2); rowGrid.Children.Add(dur);
            row.Child = rowGrid; listPanel.Children.Add(row);
        }
        scroll.Content = listPanel; stack.Children.Add(scroll);
        var symbols = gaps.Select(g => g.Symbol).Distinct().Count();
        stack.Children.Add(new TextBlock { Text = $"This will backfill data for {symbols} symbol(s) across {gaps.Count} gap(s).", FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(139, 148, 158)), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 16) });
        var buttonsPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var cancelButton = new Button { Content = "Cancel", Width = 80, Margin = new Thickness(0, 0, 8, 0), Foreground = Brushes.White, Background = new SolidColorBrush(Color.FromRgb(60, 60, 60)), BorderThickness = new Thickness(0), Padding = new Thickness(8, 6, 8, 6) };
        var repairButton = new Button { Content = "Repair All", Width = 100, Foreground = Brushes.White, Background = new SolidColorBrush(Color.FromRgb(56, 139, 253)), BorderThickness = new Thickness(0), Padding = new Thickness(8, 6, 8, 6) };
        buttonsPanel.Children.Add(cancelButton); buttonsPanel.Children.Add(repairButton);
        stack.Children.Add(buttonsPanel);
        window.Content = stack;
        var confirmed = false;
        repairButton.Click += (_, _) => { confirmed = true; window.Close(); };
        cancelButton.Click += (_, _) => window.Close();
        window.ShowDialog();
        return confirmed;
    }

    private static void ShowProviderComparisonDialog(string symbol, JsonElement data)
    {
        var window = new Window { Title = $"Provider Comparison: {symbol}", Width = 580, Height = 420, WindowStartupLocation = WindowStartupLocation.CenterOwner, ResizeMode = ResizeMode.NoResize, WindowStyle = WindowStyle.ToolWindow, Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)) };
        var stack = new StackPanel { Margin = new Thickness(20) };
        stack.Children.Add(new TextBlock { Text = $"Data Quality Comparison: {symbol}", FontWeight = FontWeights.Bold, FontSize = 16, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 16) });
        var providers = new List<(string Name, double Completeness, string Latency, string Freshness, string Status)>();
        if (data.ValueKind == JsonValueKind.Object && data.TryGetProperty("providers", out var provArray) && provArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var prov in provArray.EnumerateArray())
            {
                var name = prov.TryGetProperty("provider", out var n) ? n.GetString() ?? "" : "";
                var comp = prov.TryGetProperty("completeness", out var c) ? c.GetDouble() * 100 : 0;
                var lat = prov.TryGetProperty("averageLatencyMs", out var l) ? $"{l.GetDouble():F0}ms" : "--";
                var fresh = prov.TryGetProperty("lastDataAge", out var f) ? f.GetString() ?? "--" : "--";
                providers.Add((name, comp, lat, fresh, comp >= 95 ? "Good" : comp >= 80 ? "Fair" : "Poor"));
            }
        }
        if (providers.Count == 0)
        {
            providers.Add(("Alpaca", 99.2, "8ms", "2s ago", "Good"));
            providers.Add(("Polygon", 97.8, "12ms", "5s ago", "Good"));
            providers.Add(("Tiingo", 94.5, "45ms", "1m ago", "Fair"));
        }
        stack.Children.Add(BuildComparisonRow("Provider", "Completeness", "Latency", "Freshness", "Status", true));
        foreach (var (name, comp, lat, fresh, status) in providers)
            stack.Children.Add(BuildComparisonRow(name, $"{comp:F1}%", lat, fresh, status, false));
        var closeButton = new Button { Content = "Close", Width = 80, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0), Foreground = Brushes.White, Background = new SolidColorBrush(Color.FromRgb(60, 60, 60)), BorderThickness = new Thickness(0), Padding = new Thickness(8, 6, 8, 6) };
        closeButton.Click += (_, _) => window.Close();
        stack.Children.Add(closeButton);
        window.Content = stack;
        window.ShowDialog();
    }

    private static Border BuildComparisonRow(string col1, string col2, string col3, string col4, string col5, bool isHeader)
    {
        var border = new Border { Background = isHeader ? new SolidColorBrush(Color.FromRgb(50, 50, 50)) : new SolidColorBrush(Color.FromRgb(40, 40, 40)), Padding = new Thickness(12, 8, 12, 8), Margin = new Thickness(0, 0, 0, 2), CornerRadius = isHeader ? new CornerRadius(4, 4, 0, 0) : new CornerRadius(0) };
        var grid = new Grid();
        foreach (var w in new[] { 120.0, 100.0, 80.0, 80.0 }) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(w) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var weight = isHeader ? FontWeights.SemiBold : FontWeights.Normal;
        Brush fg = isHeader ? new SolidColorBrush(Color.FromRgb(200, 200, 200)) : Brushes.White;
        Brush statusFg = col5 switch { "Good" => new SolidColorBrush(Color.FromRgb(63, 185, 80)), "Fair" => new SolidColorBrush(Color.FromRgb(255, 193, 7)), "Poor" => new SolidColorBrush(Color.FromRgb(244, 67, 54)), _ => fg };
        int idx = 0;
        foreach (var (text, brush) in new[] { (col1, fg), (col2, fg), (col3, fg), (col4, fg), (col5, isHeader ? fg : statusFg) })
        {
            var tb = new TextBlock { Text = text, Foreground = brush, FontWeight = weight, FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(tb, idx++); grid.Children.Add(tb);
        }
        border.Child = grid; return border;
    }

    private static void AddDetailRow(StackPanel panel, string label, string value)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
        row.Children.Add(new TextBlock { Text = $"{label}: ", FontWeight = FontWeights.SemiBold, FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(139, 148, 158)), Width = 80 });
        row.Children.Add(new TextBlock { Text = value, FontSize = 12, Foreground = Brushes.White, TextWrapping = TextWrapping.Wrap, MaxWidth = 320 });
        panel.Children.Add(row);
    }

    private static string? PromptForQualityCheckPath()
    {
        var window = new Window { Title = "Run Quality Check", Width = 420, Height = 180, WindowStartupLocation = WindowStartupLocation.CenterOwner, ResizeMode = ResizeMode.NoResize, WindowStyle = WindowStyle.ToolWindow };
        var textBox = new TextBox { Margin = new Thickness(0, 12, 0, 12), MinWidth = 320 };
        var okButton = new Button { Content = "Run", Width = 80, Margin = new Thickness(0, 0, 8, 0) };
        var cancelButton = new Button { Content = "Cancel", Width = 80 };
        var buttonsPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        buttonsPanel.Children.Add(okButton); buttonsPanel.Children.Add(cancelButton);
        var stack = new StackPanel { Margin = new Thickness(16) };
        stack.Children.Add(new TextBlock { Text = "Enter path or symbol to check:" });
        stack.Children.Add(textBox); stack.Children.Add(buttonsPanel);
        window.Content = stack;
        string? result = null;
        okButton.Click += (_, _) => { result = textBox.Text; window.DialogResult = true; window.Close(); };
        cancelButton.Click += (_, _) => { window.DialogResult = false; window.Close(); };
        window.ShowDialog();
        return result;
    }
}
