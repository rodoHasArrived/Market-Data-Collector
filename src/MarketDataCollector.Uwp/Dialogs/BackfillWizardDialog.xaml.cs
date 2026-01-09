using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using MarketDataCollector.Uwp.Services;
using Windows.UI;

namespace MarketDataCollector.Uwp.Dialogs;

/// <summary>
/// A step-by-step wizard for configuring and starting a backfill operation.
/// </summary>
public sealed partial class BackfillWizardDialog : ContentDialog
{
    private readonly ConfigService _configService;
    private readonly ObservableCollection<string> _selectedSymbols = new();
    private int _currentStep = 1;
    private const int TotalSteps = 4;

    private static readonly string[] PopularSymbols = new[]
    {
        "AAPL", "MSFT", "GOOGL", "AMZN", "NVDA", "META", "TSLA", "BRK.B",
        "JPM", "V", "UNH", "XOM", "JNJ", "WMT", "MA", "PG"
    };

    private static readonly string[] MajorETFs = new[] { "SPY", "QQQ", "IWM", "DIA", "VTI" };
    private static readonly string[] TechGiants = new[] { "AAPL", "MSFT", "GOOGL", "AMZN", "META" };
    private static readonly string[] Magnificent7 = new[] { "AAPL", "MSFT", "GOOGL", "AMZN", "NVDA", "META", "TSLA" };

    /// <summary>
    /// Gets the selected symbols for backfill.
    /// </summary>
    public IReadOnlyList<string> SelectedSymbols => _selectedSymbols.ToList();

    /// <summary>
    /// Gets the selected from date.
    /// </summary>
    public DateTimeOffset? FromDate { get; private set; }

    /// <summary>
    /// Gets the selected to date.
    /// </summary>
    public DateTimeOffset? ToDate { get; private set; }

    /// <summary>
    /// Gets the selected provider.
    /// </summary>
    public string Provider { get; private set; } = "composite";

    /// <summary>
    /// Gets whether adjusted prices should be used.
    /// </summary>
    public bool UseAdjustedPrices { get; private set; } = true;

    /// <summary>
    /// Gets whether existing data should be skipped.
    /// </summary>
    public bool SkipExistingData { get; private set; } = true;

    /// <summary>
    /// Gets whether the wizard was completed successfully.
    /// </summary>
    public bool WasCompleted { get; private set; }

    public BackfillWizardDialog()
    {
        this.InitializeComponent();
        _configService = new ConfigService();

        SelectedSymbolsPanel.ItemsSource = _selectedSymbols;
        PopularSymbolsList.ItemsSource = PopularSymbols;

        // Initialize dates
        ToDate = DateTimeOffset.Now;
        FromDate = DateTimeOffset.Now.AddYears(-1);
        WizardToDate.Date = ToDate;
        WizardFromDate.Date = FromDate;

        UpdateYtdText();
        UpdateDateSummary();
        UpdateUI();
    }

    private void UpdateYtdText()
    {
        var startOfYear = new DateTime(DateTime.Now.Year, 1, 1);
        var tradingDays = EstimateTradingDays(startOfYear, DateTime.Now);
        YtdDaysText.Text = $"~{tradingDays} trading days";
    }

    private void UpdateUI()
    {
        // Update step circles and text
        UpdateStepVisual(Step1Circle, Step1Text, 1);
        UpdateStepVisual(Step2Circle, Step2Text, 2);
        UpdateStepVisual(Step3Circle, Step3Text, 3);
        UpdateStepVisual(Step4Circle, Step4Text, 4);

        // Update panel visibility
        Step1Panel.Visibility = _currentStep == 1 ? Visibility.Visible : Visibility.Collapsed;
        Step2Panel.Visibility = _currentStep == 2 ? Visibility.Visible : Visibility.Collapsed;
        Step3Panel.Visibility = _currentStep == 3 ? Visibility.Visible : Visibility.Collapsed;
        Step4Panel.Visibility = _currentStep == 4 ? Visibility.Visible : Visibility.Collapsed;

        // Update buttons
        BackButton.Visibility = _currentStep > 1 ? Visibility.Visible : Visibility.Collapsed;
        PrimaryButtonText = _currentStep == TotalSteps ? "Start Download" : "Next";

        // Update no symbols text
        NoSymbolsText.Visibility = _selectedSymbols.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        // Update review panel if on last step
        if (_currentStep == TotalSteps)
        {
            UpdateReviewPanel();
        }
    }

    private void UpdateStepVisual(Border circle, TextBlock text, int stepNumber)
    {
        var isActive = stepNumber == _currentStep;
        var isCompleted = stepNumber < _currentStep;

        if (isActive)
        {
            circle.Background = Application.Current.Resources["SystemAccentColor"] as SolidColorBrush
                ?? new SolidColorBrush(Color.FromArgb(255, 0, 120, 212));
            text.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
            text.Foreground = Application.Current.Resources["TextFillColorPrimaryBrush"] as SolidColorBrush;
        }
        else if (isCompleted)
        {
            circle.Background = new SolidColorBrush(Color.FromArgb(255, 72, 187, 120));
            text.FontWeight = Microsoft.UI.Text.FontWeights.Normal;
            text.Foreground = Application.Current.Resources["SystemBaseMediumColor"] as SolidColorBrush;
        }
        else
        {
            circle.Background = Application.Current.Resources["SystemBaseMediumLowColor"] as SolidColorBrush
                ?? new SolidColorBrush(Color.FromArgb(255, 160, 160, 160));
            text.FontWeight = Microsoft.UI.Text.FontWeights.Normal;
            text.Foreground = Application.Current.Resources["SystemBaseMediumColor"] as SolidColorBrush;
        }
    }

    private void UpdateDateSummary()
    {
        if (FromDate.HasValue)
        {
            SummaryFromDate.Text = FromDate.Value.ToString("MMM d, yyyy");
        }
        if (ToDate.HasValue)
        {
            SummaryToDate.Text = ToDate.Value.ToString("MMM d, yyyy");
        }
        if (FromDate.HasValue && ToDate.HasValue)
        {
            var tradingDays = EstimateTradingDays(FromDate.Value.DateTime, ToDate.Value.DateTime);
            SummaryTradingDays.Text = $"~{tradingDays}";
        }
    }

    private void UpdateReviewPanel()
    {
        ReviewSymbols.Text = _selectedSymbols.Count > 0
            ? string.Join(", ", _selectedSymbols)
            : "None selected";

        if (FromDate.HasValue && ToDate.HasValue)
        {
            ReviewDateRange.Text = $"{FromDate.Value:MMM d, yyyy} to {ToDate.Value:MMM d, yyyy}";
        }

        Provider = GetSelectedProvider();
        ReviewProvider.Text = GetProviderDisplayName(Provider);

        UseAdjustedPrices = AdjustedPricesCheck.IsChecked == true;
        SkipExistingData = SkipExistingCheck.IsChecked == true;

        var options = new List<string>();
        if (UseAdjustedPrices) options.Add("Adjusted prices");
        if (SkipExistingData) options.Add("Skip existing data");
        ReviewOptions.Text = options.Count > 0 ? string.Join(", ", options) : "None";

        // Estimate download
        if (FromDate.HasValue && ToDate.HasValue)
        {
            var tradingDays = EstimateTradingDays(FromDate.Value.DateTime, ToDate.Value.DateTime);
            var totalBars = _selectedSymbols.Count * tradingDays;
            var estimatedSizeMb = totalBars * 0.0001; // ~100 bytes per bar
            ReviewEstimate.Text = $"~{totalBars:N0} bars ({estimatedSizeMb:F1} MB)";

            // Estimate time (assume ~500 bars/second)
            var estimatedSeconds = totalBars / 500.0;
            if (estimatedSeconds < 60)
                EstimatedTime.Text = $"~{Math.Max(5, (int)estimatedSeconds)} seconds";
            else if (estimatedSeconds < 3600)
                EstimatedTime.Text = $"~{(int)(estimatedSeconds / 60)} minutes";
            else
                EstimatedTime.Text = $"~{estimatedSeconds / 3600:F1} hours";
        }
    }

    private string GetSelectedProvider()
    {
        if (ProviderMultiSource.IsChecked == true) return "composite";
        if (ProviderYahoo.IsChecked == true) return "yahoo";
        if (ProviderStooq.IsChecked == true) return "stooq";
        if (ProviderAlpaca.IsChecked == true) return "alpaca";
        return "composite";
    }

    private static string GetProviderDisplayName(string provider) => provider switch
    {
        "composite" => "Multi-Source (Auto-Failover)",
        "yahoo" => "Yahoo Finance",
        "stooq" => "Stooq",
        "alpaca" => "Alpaca Markets",
        _ => provider
    };

    private static int EstimateTradingDays(DateTime from, DateTime to)
    {
        var totalDays = (to - from).TotalDays;
        return (int)(totalDays * 252 / 365); // ~252 trading days per year
    }

    private void SymbolSearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
        {
            var query = sender.Text?.ToUpper().Trim();
            if (!string.IsNullOrEmpty(query))
            {
                var suggestions = PopularSymbols
                    .Where(s => s.StartsWith(query) && !_selectedSymbols.Contains(s))
                    .Take(5)
                    .ToList();
                sender.ItemsSource = suggestions;
            }
            else
            {
                sender.ItemsSource = null;
            }
        }
    }

    private void SymbolSearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        var symbol = (args.ChosenSuggestion as string ?? args.QueryText)?.ToUpper().Trim();
        if (!string.IsNullOrEmpty(symbol) && !_selectedSymbols.Contains(symbol))
        {
            _selectedSymbols.Add(symbol);
            sender.Text = string.Empty;
            UpdateUI();
        }
    }

    private void RemoveSymbol_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string symbol)
        {
            _selectedSymbols.Remove(symbol);
            UpdateUI();
        }
    }

    private void PopularSymbol_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string symbol && !_selectedSymbols.Contains(symbol))
        {
            _selectedSymbols.Add(symbol);
            UpdateUI();
        }
    }

    private void AddSymbolsFromArray(string[] symbols)
    {
        foreach (var symbol in symbols.Where(s => !_selectedSymbols.Contains(s)))
        {
            _selectedSymbols.Add(symbol);
        }
        UpdateUI();
    }

    private void AddMajorIndices_Click(object sender, RoutedEventArgs e) => AddSymbolsFromArray(MajorETFs);
    private void AddTechStocks_Click(object sender, RoutedEventArgs e) => AddSymbolsFromArray(TechGiants);
    private void AddMagnificent7_Click(object sender, RoutedEventArgs e) => AddSymbolsFromArray(Magnificent7);

    private async void AddSubscribed_Click(object sender, RoutedEventArgs e)
    {
        var config = await _configService.LoadConfigAsync();
        if (config?.Symbols != null)
        {
            var symbols = config.Symbols
                .Where(s => !string.IsNullOrEmpty(s.Symbol))
                .Select(s => s.Symbol!)
                .ToArray();
            AddSymbolsFromArray(symbols);
        }
    }

    private void DatePreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tag)
        {
            ToDate = DateTimeOffset.Now;
            WizardToDate.Date = ToDate;

            if (tag == "YTD")
            {
                FromDate = new DateTimeOffset(new DateTime(DateTime.Now.Year, 1, 1));
            }
            else if (int.TryParse(tag, out var days))
            {
                FromDate = DateTimeOffset.Now.AddDays(-days);
            }

            WizardFromDate.Date = FromDate;
            UpdateDateSummary();
        }
    }

    private void CustomDate_Changed(CalendarDatePicker sender, CalendarDatePickerDateChangedEventArgs args)
    {
        if (sender == WizardFromDate)
        {
            FromDate = args.NewDate;
        }
        else if (sender == WizardToDate)
        {
            ToDate = args.NewDate;
        }
        UpdateDateSummary();
    }

    private void PrimaryButton_Click(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        args.Cancel = true; // Prevent dialog from closing

        if (_currentStep < TotalSteps)
        {
            // Validate current step
            if (!ValidateCurrentStep())
            {
                return;
            }

            _currentStep++;
            UpdateUI();
        }
        else
        {
            // Final step - start download
            WasCompleted = true;
            Hide();
        }
    }

    private bool ValidateCurrentStep()
    {
        switch (_currentStep)
        {
            case 1:
                if (_selectedSymbols.Count == 0)
                {
                    ShowValidationError("Please select at least one symbol.");
                    return false;
                }
                return true;
            case 2:
                if (!FromDate.HasValue || !ToDate.HasValue)
                {
                    ShowValidationError("Please select both a start and end date.");
                    return false;
                }
                if (FromDate > ToDate)
                {
                    ShowValidationError("Start date must be before end date.");
                    return false;
                }
                return true;
            case 3:
                return true; // Provider always has a selection
            default:
                return true;
        }
    }

    private async void ShowValidationError(string message)
    {
        var dialog = new ContentDialog
        {
            Title = "Validation Error",
            Content = message,
            CloseButtonText = "OK",
            XamlRoot = this.XamlRoot
        };
        await dialog.ShowAsync();
    }

    private void SecondaryButton_Click(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        WasCompleted = false;
        // Allow dialog to close
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentStep > 1)
        {
            _currentStep--;
            UpdateUI();
        }
    }
}
