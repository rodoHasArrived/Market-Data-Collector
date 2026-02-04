using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using MarketDataCollector.Wpf.ViewModels;

namespace MarketDataCollector.Wpf.Views;

public partial class DataBrowserPage : Page
{
    private readonly DataBrowserViewModel _viewModel = new();

    public DataBrowserPage()
    {
        InitializeComponent();
        DataContext = _viewModel;
    }

    private void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        _viewModel.RefreshResults();
    }

    private void ApplyFilters_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.RefreshResults();
    }

    private void ResetFilters_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.ResetFilters();
    }

    private void PreviousPage_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.GoToPreviousPage();
    }

    private void NextPage_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.GoToNextPage();
    }
}

public sealed class DataBrowserViewModel : BindableBase, IDataErrorInfo
{
    private readonly List<DataBrowserRecord> _allRecords;
    private readonly ObservableCollection<DataBrowserRecord> _pagedRecords = new();
    private string _symbolFilter = string.Empty;
    private string _selectedDataType = "All";
    private string _selectedVenue = "All";
    private DateTime? _fromDate;
    private DateTime? _toDate;
    private int _pageSize = 25;
    private int _currentPage = 1;
    private string _validationSummary = string.Empty;

    public DataBrowserViewModel()
    {
        _allRecords = BuildSampleData();
        DataTypes = new ObservableCollection<string> { "All", "Trades", "Quotes", "Depth" };
        Venues = new ObservableCollection<string> { "All", "NYSE", "NASDAQ", "ARCA", "SMART" };
        PageSizes = new ObservableCollection<int> { 25, 50, 100, 250 };
    }

    public ObservableCollection<string> DataTypes { get; }

    public ObservableCollection<string> Venues { get; }

    public ObservableCollection<int> PageSizes { get; }

    public ObservableCollection<DataBrowserRecord> PagedRecords => _pagedRecords;

    public string SymbolFilter
    {
        get => _symbolFilter;
        set => SetProperty(ref _symbolFilter, value);
    }

    public string SelectedDataType
    {
        get => _selectedDataType;
        set => SetProperty(ref _selectedDataType, value);
    }

    public string SelectedVenue
    {
        get => _selectedVenue;
        set => SetProperty(ref _selectedVenue, value);
    }

    public DateTime? FromDate
    {
        get => _fromDate;
        set
        {
            if (SetProperty(ref _fromDate, value))
            {
                UpdateValidationSummary();
            }
        }
    }

    public DateTime? ToDate
    {
        get => _toDate;
        set
        {
            if (SetProperty(ref _toDate, value))
            {
                UpdateValidationSummary();
            }
        }
    }

    public int PageSize
    {
        get => _pageSize;
        set
        {
            if (SetProperty(ref _pageSize, value))
            {
                _currentPage = 1;
                RefreshResults();
            }
        }
    }

    public string PageSummary => $"Page {_currentPage} of {TotalPages} · {FilteredCount} records";

    public bool CanGoPrevious => _currentPage > 1;

    public bool CanGoNext => _currentPage < TotalPages;

    public string ValidationSummary
    {
        get => _validationSummary;
        private set => SetProperty(ref _validationSummary, value);
    }

    private int FilteredCount { get; set; }

    private int TotalPages => PageSize <= 0 ? 1 : Math.Max(1, (int)Math.Ceiling(FilteredCount / (double)PageSize));

    public string Error => string.Empty;

    public string this[string columnName]
    {
        get
        {
            if ((columnName == nameof(FromDate) || columnName == nameof(ToDate)) &&
                FromDate.HasValue && ToDate.HasValue && FromDate > ToDate)
            {
                return "Start date must be before the end date.";
            }

            return string.Empty;
        }
    }

    public void RefreshResults()
    {
        var query = _allRecords.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(SymbolFilter))
        {
            query = query.Where(record => record.Symbol.Contains(SymbolFilter.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        if (!string.Equals(SelectedDataType, "All", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(record => record.DataType.Equals(SelectedDataType, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.Equals(SelectedVenue, "All", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(record => record.Venue.Equals(SelectedVenue, StringComparison.OrdinalIgnoreCase));
        }

        if (FromDate.HasValue)
        {
            query = query.Where(record => record.Timestamp >= FromDate.Value);
        }

        if (ToDate.HasValue)
        {
            query = query.Where(record => record.Timestamp <= ToDate.Value);
        }

        var filtered = query.OrderByDescending(record => record.Timestamp).ToList();
        FilteredCount = filtered.Count;

        var paged = filtered.Skip((_currentPage - 1) * PageSize).Take(PageSize).ToList();
        _pagedRecords.Clear();
        foreach (var record in paged)
        {
            _pagedRecords.Add(record);
        }

        RaisePropertyChanged(nameof(PageSummary));
        RaisePropertyChanged(nameof(CanGoPrevious));
        RaisePropertyChanged(nameof(CanGoNext));
        UpdateValidationSummary();
    }

    public void GoToPreviousPage()
    {
        if (!CanGoPrevious)
        {
            return;
        }

        _currentPage--;
        RefreshResults();
    }

    public void GoToNextPage()
    {
        if (!CanGoNext)
        {
            return;
        }

        _currentPage++;
        RefreshResults();
    }

    public void ResetFilters()
    {
        SymbolFilter = string.Empty;
        SelectedDataType = "All";
        SelectedVenue = "All";
        FromDate = null;
        ToDate = null;
        _currentPage = 1;
        RefreshResults();
    }

    private void UpdateValidationSummary()
    {
        ValidationSummary = this[nameof(FromDate)];
    }

    private static List<DataBrowserRecord> BuildSampleData()
    {
        var random = new Random(42);
        var symbols = new[] { "AAPL", "MSFT", "NVDA", "SPY", "QQQ", "TSLA", "AMZN" };
        var dataTypes = new[] { "Trades", "Quotes", "Depth" };
        var venues = new[] { "NYSE", "NASDAQ", "ARCA", "SMART" };
        var records = new List<DataBrowserRecord>();

        for (var i = 0; i < 240; i++)
        {
            var symbol = symbols[random.Next(symbols.Length)];
            var dataType = dataTypes[random.Next(dataTypes.Length)];
            var venue = venues[random.Next(venues.Length)];
            var timestamp = DateTime.Today.AddMinutes(-random.Next(0, 7200));
            records.Add(new DataBrowserRecord
            {
                Symbol = symbol,
                DataType = dataType,
                Venue = venue,
                Timestamp = timestamp,
                Price = Math.Round(50 + random.NextDouble() * 250, 2),
                Size = random.Next(10, 1000)
            });
        }

        return records;
    }
}

public sealed class DataBrowserRecord
{
    public DateTime Timestamp { get; init; }

    public string Symbol { get; init; } = string.Empty;

    public string DataType { get; init; } = string.Empty;

    public string Venue { get; init; } = string.Empty;

    public double Price { get; init; }

    public int Size { get; init; }
}
