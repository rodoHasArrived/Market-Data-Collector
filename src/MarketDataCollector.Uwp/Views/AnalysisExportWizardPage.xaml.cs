using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using MarketDataCollector.Uwp.Services;
using Windows.Storage.Pickers;

namespace MarketDataCollector.Uwp.Views;

/// <summary>
/// Analysis export wizard page for guided data export.
/// </summary>
public sealed partial class AnalysisExportWizardPage : Page
{
    private int _currentStep = 1;
    private const int TotalSteps = 4;
    private readonly AnalysisExportWizardService _exportService;
    private readonly SymbolManagementService _symbolService;
    private string _selectedProfile = "python-pandas";
    private ExportEstimate? _estimate;
    private PreExportQualityReport? _qualityReport;
    private CancellationTokenSource? _exportCts;

    public AnalysisExportWizardPage()
    {
        this.InitializeComponent();
        _exportService = new AnalysisExportWizardService();
        _symbolService = new SymbolManagementService();

        InitializeDefaultDates();
        LoadSymbolsAsync();
    }

    private void InitializeDefaultDates()
    {
        ToDatePicker.Date = DateTimeOffset.Now;
        FromDatePicker.Date = DateTimeOffset.Now.AddDays(-30);
    }

    private async void LoadSymbolsAsync()
    {
        try
        {
            var symbols = await _symbolService.GetAllSymbolsAsync();
            var items = symbols.Select(s => new { Symbol = s.Symbol, Description = s.Description ?? "" }).ToList();
            SymbolsList.ItemsSource = items;

            // Select all by default
            foreach (var item in items)
            {
                SymbolsList.SelectedItems.Add(item);
            }
        }
        catch
        {
            // Use default symbols if service fails
            LoggingService.Instance.LogWarning("Failed to load symbols for export wizard");
            var defaultSymbols = new[]
            {
                new { Symbol = "SPY", Description = "S&P 500 ETF" },
                new { Symbol = "QQQ", Description = "Nasdaq 100 ETF" },
                new { Symbol = "AAPL", Description = "Apple Inc" }
            };
            SymbolsList.ItemsSource = defaultSymbols;
        }
    }

    private void UpdateStepUI()
    {
        WizardProgress.Value = (_currentStep - 1) * 100.0 / (TotalSteps - 1);

        UpdateStepIndicator(Step1Indicator, 1);
        UpdateStepIndicator(Step2Indicator, 2);
        UpdateStepIndicator(Step3Indicator, 3);
        UpdateStepIndicator(Step4Indicator, 4);

        Step1Content.Visibility = _currentStep == 1 ? Visibility.Visible : Visibility.Collapsed;
        Step2Content.Visibility = _currentStep == 2 ? Visibility.Visible : Visibility.Collapsed;
        Step3Content.Visibility = _currentStep == 3 ? Visibility.Visible : Visibility.Collapsed;
        Step4Content.Visibility = _currentStep == 4 ? Visibility.Visible : Visibility.Collapsed;

        BackButton.Visibility = _currentStep > 1 && _currentStep < 4 ? Visibility.Visible : Visibility.Collapsed;
        NextButton.Visibility = _currentStep < 4 ? Visibility.Visible : Visibility.Collapsed;

        NextButtonText.Text = _currentStep switch
        {
            1 => "Continue",
            2 => "Preview Export",
            3 => "Start Export",
            _ => "Next"
        };
    }

    private void UpdateStepIndicator(StackPanel indicator, int stepNumber)
    {
        var isCompleted = stepNumber < _currentStep;
        var isCurrent = stepNumber == _currentStep;

        indicator.Opacity = isCurrent || isCompleted ? 1.0 : 0.5;

        if (indicator.Children[0] is Border border)
        {
            if (isCompleted)
            {
                border.Background = new SolidColorBrush(Microsoft.UI.Colors.Green);
                if (border.Child is TextBlock textBlock)
                    textBlock.Text = "\uE73E";
            }
            else if (isCurrent)
            {
                border.Background = (SolidColorBrush)Application.Current.Resources["SystemAccentColor"];
            }
        }
    }

    private void ProfileCard_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Border border && border.Tag is string profile)
        {
            _selectedProfile = profile;
            switch (profile)
            {
                case "python-pandas":
                    PythonPandasRadio.IsChecked = true;
                    break;
                case "quantconnect-lean":
                    LeanRadio.IsChecked = true;
                    break;
                case "r-dataframe":
                    RRadio.IsChecked = true;
                    break;
                case "sql-postgres":
                    PostgresRadio.IsChecked = true;
                    break;
                case "excel":
                    ExcelRadio.IsChecked = true;
                    break;
            }
        }
    }

    private void SelectAllSymbols_Click(object sender, RoutedEventArgs e)
    {
        SymbolsList.SelectAll();
    }

    private void ClearSymbols_Click(object sender, RoutedEventArgs e)
    {
        SymbolsList.SelectedItems.Clear();
    }

    private void QuickDateRange_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string daysStr && int.TryParse(daysStr, out var days))
        {
            ToDatePicker.Date = DateTimeOffset.Now;
            FromDatePicker.Date = DateTimeOffset.Now.AddDays(-days);
        }
    }

    private async void BrowseOutput_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FolderPicker();
        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        picker.FileTypeFilter.Add("*");

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        var folder = await picker.PickSingleFolderAsync();
        if (folder != null)
        {
            OutputPathBox.Text = folder.Path;
        }
    }

    private async void Next_Click(object sender, RoutedEventArgs e)
    {
        if (_currentStep == 2)
        {
            // Calculate estimate before moving to preview
            await CalculateEstimateAsync();
        }

        if (_currentStep == 3)
        {
            // Validate output path
            if (string.IsNullOrWhiteSpace(OutputPathBox.Text))
            {
                await ShowErrorAsync("Please select an output folder.");
                return;
            }

            // Start export
            _currentStep++;
            UpdateStepUI();
            await ExecuteExportAsync();
            return;
        }

        if (_currentStep < TotalSteps)
        {
            _currentStep++;
            UpdateStepUI();
        }
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (_currentStep > 1)
        {
            _currentStep--;
            UpdateStepUI();
        }
    }

    private void GoBack_Click(object sender, RoutedEventArgs e)
    {
        if (Frame.CanGoBack)
            Frame.GoBack();
    }

    private async Task CalculateEstimateAsync()
    {
        var config = BuildConfiguration();

        try
        {
            _estimate = await _exportService.EstimateExportAsync(config);
            _qualityReport = await _exportService.GenerateQualityReportAsync(config);

            // Update UI
            EstimatedSizeText.Text = _estimate.EstimatedSizeFormatted;
            RecordCountText.Text = $"{_estimate.AvailableRecords:N0}";
            EstimatedTimeText.Text = _estimate.EstimatedDurationFormatted;
            CompletenessText.Text = $"{_estimate.CompletenessPercent:F1}%";
            CompletenessBar.Value = _estimate.CompletenessPercent;

            // Update quality report
            QualityGradeText.Text = GetQualityGrade(_qualityReport.OverallCompleteness);
            QualityGradeText.Foreground = GetGradeColor(_qualityReport.OverallCompleteness);
            TradingDaysText.Text = _qualityReport.TotalTradingDays.ToString();
            DaysWithDataText.Text = _qualityReport.DaysWithData.ToString();
            GapsFoundText.Text = _qualityReport.GapCount.ToString();
            SuitabilityText.Text = _qualityReport.SuitabilityAssessment;

            // Show warnings
            if (_estimate.Warnings.Count > 0)
            {
                WarningsPanel.Visibility = Visibility.Visible;
                WarningsList.ItemsSource = _estimate.Warnings;
            }
            else
            {
                WarningsPanel.Visibility = Visibility.Collapsed;
            }
        }
        catch (Exception ex)
        {
            await ShowErrorAsync($"Failed to calculate estimate: {ex.Message}");
        }
    }

    private ExportConfiguration BuildConfiguration()
    {
        var profiles = _exportService.GetExportProfiles();
        var profile = profiles.FirstOrDefault(p => p.Id == _selectedProfile) ?? profiles[0];

        var symbols = SymbolsList.SelectedItems
            .Cast<dynamic>()
            .Select(x => (string)x.Symbol)
            .ToArray();

        var dataTypes = new List<string>();
        if (TradesCheck.IsChecked == true) dataTypes.Add("trades");
        if (QuotesCheck.IsChecked == true) dataTypes.Add("quotes");
        if (BarsCheck.IsChecked == true) dataTypes.Add("bars_1d");
        if (DepthCheck.IsChecked == true) dataTypes.Add("depth");

        return new ExportConfiguration
        {
            Profile = profile,
            Symbols = symbols,
            DataTypes = dataTypes.ToArray(),
            FromDate = FromDatePicker.Date.HasValue ? DateOnly.FromDateTime(FromDatePicker.Date.Value.DateTime) : DateOnly.FromDateTime(DateTime.Now.AddDays(-30)),
            ToDate = ToDatePicker.Date.HasValue ? DateOnly.FromDateTime(ToDatePicker.Date.Value.DateTime) : DateOnly.FromDateTime(DateTime.Now),
            OutputPath = OutputPathBox.Text ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MarketDataExport"),
            IncludeQualityReport = IncludeQualityReportCheck.IsChecked == true,
            IncludeSchema = IncludeSchemaCheck.IsChecked == true
        };
    }

    private async Task ExecuteExportAsync()
    {
        var config = BuildConfiguration();
        _exportCts = new CancellationTokenSource();

        ExportProgressRing.IsActive = true;
        ExportResultPanel.Visibility = Visibility.Collapsed;

        var progress = new Progress<ExportProgress>(p =>
        {
            CurrentSymbolText.Text = $"Processing {p.CurrentSymbol}...";
            ProgressPercentText.Text = $"{p.PercentComplete:F0}%";
            ExportProgressBar.Value = p.PercentComplete;
        });

        try
        {
            var result = await _exportService.ExecuteExportAsync(config, progress, _exportCts.Token);

            ExportProgressRing.IsActive = false;
            ExportStatusText.Text = result.Success ? "Export Complete!" : "Export Failed";
            ExportResultPanel.Visibility = Visibility.Visible;

            if (result.Success)
            {
                ExportResultInfoBar.Severity = InfoBarSeverity.Success;
                ExportResultInfoBar.Title = "Export Successful";
                ExportResultInfoBar.Message = $"Exported {result.ProcessedSymbols} symbols in {result.Duration.TotalSeconds:F1} seconds";
                GeneratedFilesList.ItemsSource = result.GeneratedFiles;
            }
            else
            {
                ExportResultInfoBar.Severity = InfoBarSeverity.Error;
                ExportResultInfoBar.Title = "Export Failed";
                ExportResultInfoBar.Message = result.ErrorMessage;
            }
        }
        catch (OperationCanceledException)
        {
            ExportProgressRing.IsActive = false;
            ExportStatusText.Text = "Export Cancelled";
            ExportResultPanel.Visibility = Visibility.Visible;
            ExportResultInfoBar.Severity = InfoBarSeverity.Warning;
            ExportResultInfoBar.Title = "Export Cancelled";
            ExportResultInfoBar.Message = "The export was cancelled by the user.";
        }
        catch (Exception ex)
        {
            ExportProgressRing.IsActive = false;
            ExportStatusText.Text = "Export Failed";
            ExportResultPanel.Visibility = Visibility.Visible;
            ExportResultInfoBar.Severity = InfoBarSeverity.Error;
            ExportResultInfoBar.Title = "Export Error";
            ExportResultInfoBar.Message = ex.Message;
        }
    }

    private async void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(OutputPathBox.Text))
        {
            try
            {
                await Windows.System.Launcher.LaunchFolderPathAsync(OutputPathBox.Text);
            }
            catch { LoggingService.Instance.LogWarning("Analysis export wizard operation failed"); }
        }
    }

    private void ExportAnother_Click(object sender, RoutedEventArgs e)
    {
        _currentStep = 1;
        UpdateStepUI();
    }

    private string GetQualityGrade(double completeness)
    {
        return completeness switch
        {
            >= 99 => "A+",
            >= 95 => "A",
            >= 90 => "B",
            >= 80 => "C",
            >= 70 => "D",
            _ => "F"
        };
    }

    private SolidColorBrush GetGradeColor(double completeness)
    {
        if (completeness >= 95)
            return new SolidColorBrush(Microsoft.UI.Colors.Green);
        if (completeness >= 80)
            return new SolidColorBrush(Microsoft.UI.Colors.Orange);
        return new SolidColorBrush(Microsoft.UI.Colors.Red);
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
}
