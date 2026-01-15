using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using MarketDataCollector.Uwp.Services;
using MarketDataCollector.Uwp.Controls;
using MarketDataCollector.Uwp.Dialogs;
using Windows.UI;

namespace MarketDataCollector.Uwp.Views;

/// <summary>
/// A comprehensive data browser page for viewing, searching, and managing backfilled historical data.
/// </summary>
public sealed partial class DataBrowserPage : Page
{
    private readonly ObservableCollection<DataTreeNode> _treeNodes = new();
    private readonly ObservableCollection<DataFileItem> _dataItems = new();
    private readonly ObservableCollection<ChartBarData> _chartBars = new();
    private readonly StorageAnalyticsService _storageService;
    private readonly ArchiveBrowserService _archiveBrowserService;

    private string? _currentSymbol;
    private DateTime? _currentDate;
    private int _currentPage = 1;
    private const int PageSize = 50;

    public DataBrowserPage()
    {
        this.InitializeComponent();
        _storageService = new StorageAnalyticsService();
        _archiveBrowserService = new ArchiveBrowserService();

        DataListView.ItemsSource = _dataItems;
        ChartBarsControl.ItemsSource = _chartBars;

        Loaded += DataBrowserPage_Loaded;
    }

    private async void DataBrowserPage_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadTreeViewAsync();
        await UpdateStorageSummaryAsync();
        UpdateEmptyState();
    }

    private async Task LoadTreeViewAsync()
    {
        _treeNodes.Clear();

        // Root nodes for different data categories
        var historicalNode = new DataTreeNode
        {
            Name = "Historical Data",
            Icon = "\uE787",
            IsExpanded = true,
            NodeType = "category"
        };

        var liveNode = new DataTreeNode
        {
            Name = "Live Data",
            Icon = "\uE9D9",
            NodeType = "category"
        };

        var archiveNode = new DataTreeNode
        {
            Name = "Archives",
            Icon = "\uE8B7",
            NodeType = "category"
        };

        // Load symbols from storage
        var symbols = await GetAvailableSymbolsAsync();
        foreach (var symbol in symbols.Take(20)) // Limit for performance
        {
            var symbolNode = new DataTreeNode
            {
                Name = symbol,
                Icon = "\uE8D2",
                NodeType = "symbol",
                Tag = symbol,
                Badge = await GetSymbolBadgeAsync(symbol)
            };

            // Add date range children
            var dates = await GetDatesForSymbolAsync(symbol);
            foreach (var date in dates.Take(10))
            {
                symbolNode.Children.Add(new DataTreeNode
                {
                    Name = date.ToString("yyyy-MM-dd"),
                    Icon = "\uE787",
                    NodeType = "date",
                    Tag = $"{symbol}|{date:yyyy-MM-dd}"
                });
            }

            historicalNode.Children.Add(symbolNode);
        }

        _treeNodes.Add(historicalNode);
        _treeNodes.Add(liveNode);
        _treeNodes.Add(archiveNode);

        DataTreeView.ItemsSource = _treeNodes;
    }

    private async Task UpdateStorageSummaryAsync()
    {
        var analytics = await _storageService.GetStorageAnalyticsAsync();
        if (analytics != null)
        {
            var sizeGb = analytics.TotalSizeBytes / (1024.0 * 1024.0 * 1024.0);
            var sizeMb = analytics.TotalSizeBytes / (1024.0 * 1024.0);

            TotalStorageText.Text = sizeGb >= 1 ? $"{sizeGb:F1} GB" : $"{sizeMb:F1} MB";
            StorageUsageBar.Value = Math.Min(100, sizeGb / 50.0 * 100); // Assume 50GB max
            StorageDetailsText.Text = $"{analytics.TotalFileCount:N0} files | {analytics.SymbolBreakdown?.Length ?? 0} symbols";
        }
    }

    private void UpdateEmptyState()
    {
        var hasData = _dataItems.Count > 0;
        EmptyStatePanel.Visibility = hasData ? Visibility.Collapsed : Visibility.Visible;
        GridViewPanel.Visibility = hasData ? Visibility.Visible : Visibility.Collapsed;
    }

    private async Task<List<string>> GetAvailableSymbolsAsync()
    {
        // In real implementation, this would query the storage service
        await Task.Delay(10);
        return new List<string> { "AAPL", "MSFT", "GOOGL", "AMZN", "NVDA", "META", "TSLA", "SPY", "QQQ", "IWM" };
    }

    private async Task<string> GetSymbolBadgeAsync(string symbol)
    {
        await Task.Delay(5);
        var random = new Random(symbol.GetHashCode());
        var days = random.Next(100, 1000);
        return $"{days}d";
    }

    private async Task<List<DateTime>> GetDatesForSymbolAsync(string symbol)
    {
        await Task.Delay(5);
        var dates = new List<DateTime>();
        var today = DateTime.Today;
        for (int i = 0; i < 30; i++)
        {
            var date = today.AddDays(-i);
            if (date.DayOfWeek != DayOfWeek.Saturday && date.DayOfWeek != DayOfWeek.Sunday)
            {
                dates.Add(date);
            }
        }
        return dates;
    }

    private async Task LoadDataItemsAsync(string? symbol = null, DateTime? date = null)
    {
        _dataItems.Clear();

        // Generate sample data items
        var symbols = symbol != null ? new[] { symbol } : new[] { "AAPL", "MSFT", "GOOGL", "AMZN", "NVDA" };
        var random = new Random();

        foreach (var sym in symbols)
        {
            for (int i = 0; i < 10; i++)
            {
                var itemDate = date ?? DateTime.Today.AddDays(-i);
                if (itemDate.DayOfWeek == DayOfWeek.Saturday || itemDate.DayOfWeek == DayOfWeek.Sunday)
                    continue;

                _dataItems.Add(new DataFileItem
                {
                    Symbol = sym,
                    Date = itemDate,
                    DateText = itemDate.ToString("yyyy-MM-dd"),
                    DataType = "Daily",
                    TypeColor = new SolidColorBrush(Color.FromArgb(40, 72, 187, 120)),
                    Provider = "Yahoo",
                    BarCount = random.Next(1, 5),
                    BarCountText = random.Next(1, 5).ToString(),
                    SizeBytes = random.Next(100, 5000),
                    SizeText = $"{random.Next(100, 5000) / 1024.0:F1} KB"
                });
            }
        }

        UpdateStats();
        UpdatePagination();
        UpdateEmptyState();
        await Task.CompletedTask;
    }

    private void UpdateStats()
    {
        StatsBarsText.Text = _dataItems.Sum(d => d.BarCount).ToString("N0");
        StatsFilesText.Text = _dataItems.Count.ToString("N0");
        StatsSizeText.Text = $"{_dataItems.Sum(d => d.SizeBytes) / 1024.0:F1} KB";
    }

    private void UpdatePagination()
    {
        var total = _dataItems.Count;
        var start = (_currentPage - 1) * PageSize + 1;
        var end = Math.Min(_currentPage * PageSize, total);
        PaginationText.Text = $"Showing {start}-{end} of {total:N0} items";
        PrevPageButton.IsEnabled = _currentPage > 1;
        NextPageButton.IsEnabled = end < total;
    }

    private void UpdateBreadcrumb()
    {
        if (_currentSymbol != null)
        {
            Breadcrumb1.Visibility = Visibility.Visible;
            BreadcrumbLevel1.Visibility = Visibility.Visible;
            BreadcrumbLevel1.Content = _currentSymbol;
            BreadcrumbLevel1.Tag = _currentSymbol;

            if (_currentDate.HasValue)
            {
                Breadcrumb2.Visibility = Visibility.Visible;
                BreadcrumbLevel2.Visibility = Visibility.Visible;
                BreadcrumbLevel2.Content = _currentDate.Value.ToString("yyyy-MM-dd");
                BreadcrumbLevel2.Tag = $"{_currentSymbol}|{_currentDate.Value:yyyy-MM-dd}";
            }
            else
            {
                Breadcrumb2.Visibility = Visibility.Collapsed;
                BreadcrumbLevel2.Visibility = Visibility.Collapsed;
            }

            SelectedItemTitle.Text = _currentDate.HasValue
                ? $"{_currentSymbol} - {_currentDate.Value:yyyy-MM-dd}"
                : _currentSymbol;
            SelectedItemSubtitle.Text = _currentDate.HasValue
                ? "Daily OHLCV data"
                : "All historical data for this symbol";
        }
        else
        {
            Breadcrumb1.Visibility = Visibility.Collapsed;
            BreadcrumbLevel1.Visibility = Visibility.Collapsed;
            Breadcrumb2.Visibility = Visibility.Collapsed;
            BreadcrumbLevel2.Visibility = Visibility.Collapsed;

            SelectedItemTitle.Text = "All Data";
            SelectedItemSubtitle.Text = "Browse your historical market data";
        }
    }

    private void DataTreeView_ItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
    {
        if (args.InvokedItem is DataTreeNode node)
        {
            if (node.NodeType == "symbol")
            {
                _currentSymbol = node.Tag;
                _currentDate = null;
            }
            else if (node.NodeType == "date" && node.Tag != null)
            {
                var parts = node.Tag.Split('|');
                if (parts.Length == 2)
                {
                    _currentSymbol = parts[0];
                    if (DateTime.TryParse(parts[1], out var date))
                    {
                        _currentDate = date;
                    }
                }
            }
            else
            {
                _currentSymbol = null;
                _currentDate = null;
            }

            UpdateBreadcrumb();
            _ = LoadDataItemsAsync(_currentSymbol, _currentDate);
        }
    }

    private void Breadcrumb_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn)
        {
            var tag = btn.Tag?.ToString();
            if (tag == "root")
            {
                _currentSymbol = null;
                _currentDate = null;
            }
            else if (tag != null && tag.Contains('|'))
            {
                var parts = tag.Split('|');
                _currentSymbol = parts[0];
                if (parts.Length > 1 && DateTime.TryParse(parts[1], out var date))
                {
                    _currentDate = date;
                }
            }
            else
            {
                _currentSymbol = tag;
                _currentDate = null;
            }

            UpdateBreadcrumb();
            _ = LoadDataItemsAsync(_currentSymbol, _currentDate);
        }
    }

    private void SearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        var query = args.QueryText?.ToUpper().Trim();
        if (!string.IsNullOrEmpty(query))
        {
            _currentSymbol = query;
            _currentDate = null;
            UpdateBreadcrumb();
            _ = LoadDataItemsAsync(_currentSymbol);
        }
    }

    private void Filter_Click(object sender, RoutedEventArgs e)
    {
        _ = LoadDataItemsAsync(_currentSymbol, _currentDate);
    }

    private void ViewMode_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (ViewModeCombo.SelectedItem is ComboBoxItem item && item.Tag is string mode)
        {
            GridViewPanel.Visibility = mode == "Grid" ? Visibility.Visible : Visibility.Collapsed;
            CalendarViewPanel.Visibility = mode == "Calendar" ? Visibility.Visible : Visibility.Collapsed;
            ChartViewPanel.Visibility = mode == "Chart" ? Visibility.Visible : Visibility.Collapsed;

            if (mode == "Calendar")
            {
                LoadCalendarData();
            }
            else if (mode == "Chart")
            {
                LoadChartData();
            }
        }
    }

    private void LoadCalendarData()
    {
        var coverageData = new List<DataCoverageInfo>();
        var random = new Random();

        // Generate sample coverage data for the current month
        var today = DateTime.Today;
        for (int i = 0; i < 60; i++)
        {
            var date = today.AddDays(-i);
            if (date.DayOfWeek != DayOfWeek.Saturday && date.DayOfWeek != DayOfWeek.Sunday)
            {
                var rand = random.NextDouble();
                coverageData.Add(new DataCoverageInfo
                {
                    Date = date,
                    Status = rand > 0.2 ? DataCoverageStatus.Complete
                        : rand > 0.1 ? DataCoverageStatus.Partial
                        : DataCoverageStatus.Missing,
                    BarCount = random.Next(100, 5000),
                    Symbol = _currentSymbol
                });
            }
        }

        CoverageCalendar.SetCoverageData(coverageData);
    }

    private void LoadChartData()
    {
        _chartBars.Clear();
        var random = new Random();

        // Generate sample monthly data
        var months = new[] { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };
        foreach (var month in months)
        {
            _chartBars.Add(new ChartBarData
            {
                Label = month,
                Value = random.Next(1000, 10000),
                Height = random.Next(20, 180)
            });
        }
    }

    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        _ = LoadDataItemsAsync(_currentSymbol, _currentDate);
    }

    private async void Export_Click(object sender, RoutedEventArgs e)
    {
        var selectedItems = DataListView.SelectedItems.Cast<DataFileItem>().ToList();
        if (selectedItems.Count == 0)
        {
            await ShowMessageAsync("No Selection", "Please select items to export.");
            return;
        }

        await ShowMessageAsync("Export", $"Exporting {selectedItems.Count} item(s)...");
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        var selectedItems = DataListView.SelectedItems.Cast<DataFileItem>().ToList();
        if (selectedItems.Count == 0)
        {
            await ShowMessageAsync("No Selection", "Please select items to delete.");
            return;
        }

        var dialog = new ContentDialog
        {
            Title = "Confirm Delete",
            Content = $"Are you sure you want to delete {selectedItems.Count} item(s)? This cannot be undone.",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.XamlRoot
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            foreach (var item in selectedItems)
            {
                _dataItems.Remove(item);
            }
            UpdateStats();
        }
    }

    private void DataListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var count = DataListView.SelectedItems.Count;
        ExportButton.IsEnabled = count > 0;
        DeleteButton.IsEnabled = count > 0;
    }

    private void Preview_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is DataFileItem item)
        {
            ShowPreview(item);
        }
    }

    private void ShowPreview(DataFileItem item)
    {
        PreviewPanel.Visibility = Visibility.Visible;
        GridViewPanel.Visibility = Visibility.Collapsed;

        PreviewTitle.Text = $"Data Preview - {item.Symbol}";
        PreviewSubtitle.Text = item.DateText;

        // Sample OHLCV data
        var random = new Random(item.Symbol.GetHashCode());
        var basePrice = random.Next(50, 500);
        PreviewOpenText.Text = $"${basePrice:F2}";
        PreviewHighText.Text = $"${basePrice * 1.02:F2}";
        PreviewLowText.Text = $"${basePrice * 0.98:F2}";
        PreviewCloseText.Text = $"${basePrice * 1.01:F2}";

        // Sample raw data
        var rawData = $@"{{""symbol"":""{item.Symbol}"",""date"":""{item.DateText}"",""open"":{basePrice:F2},""high"":{basePrice * 1.02:F2},""low"":{basePrice * 0.98:F2},""close"":{basePrice * 1.01:F2},""volume"":{random.Next(1000000, 50000000)}}}";
        PreviewRawData.Text = rawData;
    }

    private void ClosePreview_Click(object sender, RoutedEventArgs e)
    {
        PreviewPanel.Visibility = Visibility.Collapsed;
        GridViewPanel.Visibility = Visibility.Visible;
    }

    private void PrevPage_Click(object sender, RoutedEventArgs e)
    {
        if (_currentPage > 1)
        {
            _currentPage--;
            UpdatePagination();
        }
    }

    private void NextPage_Click(object sender, RoutedEventArgs e)
    {
        _currentPage++;
        UpdatePagination();
    }

    private async void StartWizard_Click(object sender, RoutedEventArgs e)
    {
        var wizard = new BackfillWizardDialog
        {
            XamlRoot = this.XamlRoot
        };

        await wizard.ShowAsync();

        if (wizard.WasCompleted)
        {
            // Navigate to backfill page or start backfill
            await ShowMessageAsync("Wizard Complete",
                $"Starting backfill for {wizard.SelectedSymbols.Count} symbols...");
        }
    }

    private async Task ShowMessageAsync(string title, string message)
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

/// <summary>
/// Tree view node for data browser.
/// </summary>
public class DataTreeNode
{
    public string Name { get; set; } = string.Empty;
    public string Icon { get; set; } = "\uE8B7";
    public string NodeType { get; set; } = string.Empty;
    public string? Tag { get; set; }
    public string? Badge { get; set; }
    public bool HasBadge => !string.IsNullOrEmpty(Badge);
    public bool IsExpanded { get; set; }
    public ObservableCollection<DataTreeNode> Children { get; } = new();
}

/// <summary>
/// Data file item for the list view.
/// </summary>
public class DataFileItem
{
    public string Symbol { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string DateText { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public SolidColorBrush TypeColor { get; set; } = new(Color.FromArgb(40, 72, 187, 120));
    public string Provider { get; set; } = string.Empty;
    public int BarCount { get; set; }
    public string BarCountText { get; set; } = "0";
    public long SizeBytes { get; set; }
    public string SizeText { get; set; } = "0 KB";
}

/// <summary>
/// Chart bar data for the chart view.
/// </summary>
public class ChartBarData
{
    public string Label { get; set; } = string.Empty;
    public int Value { get; set; }
    public int Height { get; set; }
}
