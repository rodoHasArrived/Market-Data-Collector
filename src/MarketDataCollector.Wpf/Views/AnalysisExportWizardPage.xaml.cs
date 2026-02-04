using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using MarketDataCollector.Wpf.ViewModels;

namespace MarketDataCollector.Wpf.Views;

public partial class AnalysisExportWizardPage : Page
{
    private readonly AnalysisExportWizardViewModel _viewModel = new();

    public AnalysisExportWizardPage()
    {
        InitializeComponent();
        DataContext = _viewModel;
    }

    private void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        _viewModel.Initialize();
    }

    private void AddSymbol_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.AddSymbol();
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.GoBack();
    }

    private void Next_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.GoNext();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.CancelWizard();
    }
}

public sealed class AnalysisExportWizardViewModel : BindableBase, IDataErrorInfo
{
    private int _currentStep = 1;
    private string _symbolInput = string.Empty;
    private DateTime? _fromDate;
    private DateTime? _toDate;
    private string _selectedFormat = "CSV";
    private string _destination = string.Empty;
    private bool _includeCharts = true;
    private bool _includeSummary = true;
    private string _validationSummary = string.Empty;
    private string _statusMessage = string.Empty;

    public AnalysisExportWizardViewModel()
    {
        SelectedSymbols = new ObservableCollection<string>();
        Formats = new ObservableCollection<string> { "CSV", "Parquet", "JSON", "Excel" };
        Metrics = new ObservableCollection<MetricOption>
        {
            new("Volatility"),
            new("Skew"),
            new("Spread"),
            new("Liquidity"),
            new("Gap Analysis"),
            new("Performance Attribution")
        };
    }

    public ObservableCollection<string> SelectedSymbols { get; }

    public ObservableCollection<string> Formats { get; }

    public ObservableCollection<MetricOption> Metrics { get; }

    public int CurrentStep
    {
        get => _currentStep;
        private set
        {
            if (SetProperty(ref _currentStep, value))
            {
                RaisePropertyChanged(nameof(CanGoBack));
                RaisePropertyChanged(nameof(PrimaryActionLabel));
                UpdateReviewSummary();
            }
        }
    }

    public string SymbolInput
    {
        get => _symbolInput;
        set => SetProperty(ref _symbolInput, value);
    }

    public DateTime? FromDate
    {
        get => _fromDate;
        set
        {
            if (SetProperty(ref _fromDate, value))
            {
                UpdateValidationSummary();
                UpdateReviewSummary();
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
                UpdateReviewSummary();
            }
        }
    }

    public string SelectedFormat
    {
        get => _selectedFormat;
        set
        {
            if (SetProperty(ref _selectedFormat, value))
            {
                UpdateReviewSummary();
            }
        }
    }

    public string Destination
    {
        get => _destination;
        set
        {
            if (SetProperty(ref _destination, value))
            {
                UpdateValidationSummary();
            }
        }
    }

    public bool IncludeCharts
    {
        get => _includeCharts;
        set => SetProperty(ref _includeCharts, value);
    }

    public bool IncludeSummary
    {
        get => _includeSummary;
        set => SetProperty(ref _includeSummary, value);
    }

    public bool CanGoBack => CurrentStep > 1;

    public string PrimaryActionLabel => CurrentStep < 3 ? "Next" : "Queue Export";

    public string ReviewSummary { get; private set; } = string.Empty;

    public string ValidationSummary
    {
        get => _validationSummary;
        private set => SetProperty(ref _validationSummary, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string Error => string.Empty;

    public string this[string columnName]
    {
        get
        {
            return columnName switch
            {
                nameof(ToDate) when FromDate.HasValue && ToDate.HasValue && FromDate > ToDate => "Start date must be before the end date.",
                nameof(Destination) when CurrentStep >= 2 && string.IsNullOrWhiteSpace(Destination) => "Destination is required.",
                _ => string.Empty
            };
        }
    }

    public void Initialize()
    {
        if (SelectedSymbols.Count == 0)
        {
            SelectedSymbols.Add("AAPL");
            SelectedSymbols.Add("MSFT");
        }

        UpdateReviewSummary();
    }

    public void AddSymbol()
    {
        var symbol = SymbolInput.Trim().ToUpperInvariant();
        if (!string.IsNullOrWhiteSpace(symbol) && !SelectedSymbols.Contains(symbol))
        {
            SelectedSymbols.Add(symbol);
            SymbolInput = string.Empty;
            UpdateReviewSummary();
        }
    }

    public void GoBack()
    {
        if (CurrentStep > 1)
        {
            CurrentStep--;
            StatusMessage = string.Empty;
        }
    }

    public void GoNext()
    {
        UpdateValidationSummary();
        if (!string.IsNullOrEmpty(ValidationSummary))
        {
            StatusMessage = "Resolve validation issues before continuing.";
            return;
        }

        if (CurrentStep == 2 && !Metrics.Any(metric => metric.IsSelected))
        {
            ValidationSummary = "Select at least one metric.";
            StatusMessage = "Pick metrics to continue.";
            return;
        }

        if (CurrentStep < 3)
        {
            CurrentStep++;
            StatusMessage = string.Empty;
            return;
        }

        StatusMessage = "Analysis export queued successfully.";
    }

    public void CancelWizard()
    {
        CurrentStep = 1;
        StatusMessage = "Wizard reset.";
    }

    private void UpdateReviewSummary()
    {
        var symbols = SelectedSymbols.Count == 0 ? "No symbols selected" : string.Join(", ", SelectedSymbols.Take(5));
        if (SelectedSymbols.Count > 5)
        {
            symbols += $" +{SelectedSymbols.Count - 5} more";
        }

        var range = FromDate.HasValue || ToDate.HasValue
            ? $"{FromDate:MMM dd, yyyy} - {ToDate:MMM dd, yyyy}"
            : "Open range";

        ReviewSummary = $"Symbols: {symbols}\nDate Range: {range}\nFormat: {SelectedFormat}\nDestination: {Destination}";
        RaisePropertyChanged(nameof(ReviewSummary));
    }

    private void UpdateValidationSummary()
    {
        var errors = new[]
        {
            this[nameof(ToDate)],
            this[nameof(Destination)]
        };

        ValidationSummary = string.Join(" ", errors.Where(error => !string.IsNullOrWhiteSpace(error)));
    }
}
