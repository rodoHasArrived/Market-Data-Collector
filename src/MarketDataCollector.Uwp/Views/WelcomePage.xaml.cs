using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using MarketDataCollector.Uwp.Services;

namespace MarketDataCollector.Uwp.Views;

/// <summary>
/// Welcome/Onboarding page for first-time users.
/// Guides users through initial setup with a step-by-step wizard.
/// </summary>
public sealed partial class WelcomePage : Page
{
    private int _currentStep = 1;
    private const int TotalSteps = 4;
    private readonly ConfigService _configService;
    private readonly ObservableCollection<WelcomeSymbolItem> _selectedSymbols;

    public WelcomePage()
    {
        this.InitializeComponent();
        _configService = new ConfigService();
        _selectedSymbols = new ObservableCollection<WelcomeSymbolItem>();
        SelectedSymbolsList.ItemsSource = _selectedSymbols;

        UpdateStepUI();
    }

    private void UpdateStepUI()
    {
        // Update progress bar
        SetupProgress.Value = (_currentStep - 1) * 100.0 / (TotalSteps - 1);

        // Update step indicators
        UpdateStepIndicator(Step1Indicator, 1);
        UpdateStepIndicator(Step2Indicator, 2);
        UpdateStepIndicator(Step3Indicator, 3);
        UpdateStepIndicator(Step4Indicator, 4);

        // Show/hide content panels
        Step1Content.Visibility = _currentStep == 1 ? Visibility.Visible : Visibility.Collapsed;
        Step2Content.Visibility = _currentStep == 2 ? Visibility.Visible : Visibility.Collapsed;
        Step3Content.Visibility = _currentStep == 3 ? Visibility.Visible : Visibility.Collapsed;
        Step4Content.Visibility = _currentStep == 4 ? Visibility.Visible : Visibility.Collapsed;

        // Update navigation buttons
        BackButton.Visibility = _currentStep > 1 ? Visibility.Visible : Visibility.Collapsed;
        SkipButton.Visibility = _currentStep < TotalSteps ? Visibility.Visible : Visibility.Collapsed;

        // Update Next button text
        UpdateNextButtonText();
    }

    private void UpdateStepIndicator(StackPanel indicator, int stepNumber)
    {
        var isCompleted = stepNumber < _currentStep;
        var isCurrent = stepNumber == _currentStep;

        indicator.Opacity = isCurrent || isCompleted ? 1.0 : 0.5;

        var border = indicator.Children[0] as Border;
        if (border != null)
        {
            if (isCompleted)
            {
                border.Background = new SolidColorBrush(Microsoft.UI.Colors.Green);
                var textBlock = border.Child as TextBlock;
                if (textBlock != null) textBlock.Text = "\uE73E"; // Checkmark
            }
            else if (isCurrent)
            {
                border.Background = (SolidColorBrush)Application.Current.Resources["SystemAccentColor"];
            }
        }
    }

    private void UpdateNextButtonText()
    {
        var textBlock = ((StackPanel)NextButton.Content).Children[0] as TextBlock;
        if (textBlock == null) return;

        textBlock.Text = _currentStep switch
        {
            1 => "Get Started",
            2 => "Continue",
            3 => "Finish Setup",
            4 => "Go to Dashboard",
            _ => "Next"
        };
    }

    private void Next_Click(object sender, RoutedEventArgs e)
    {
        if (_currentStep < TotalSteps)
        {
            // Validate current step before proceeding
            if (!ValidateCurrentStep()) return;

            _currentStep++;

            // Special handling when entering certain steps
            if (_currentStep == 2)
            {
                UpdateProviderSelection();
            }
            else if (_currentStep == 4)
            {
                UpdateSummary();
            }

            UpdateStepUI();
        }
        else
        {
            // Final step - save and navigate to dashboard
            _ = SaveConfigurationAndNavigate();
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

    private void Skip_Click(object sender, RoutedEventArgs e)
    {
        NavigateToDashboard();
    }

    private bool ValidateCurrentStep()
    {
        switch (_currentStep)
        {
            case 3:
                // Ensure at least one symbol is selected
                if (_selectedSymbols.Count == 0)
                {
                    // Show warning but allow proceeding
                    return true;
                }
                break;
        }
        return true;
    }

    private void UpdateProviderSelection()
    {
        // Show credentials panel
        CredentialsPanel.Visibility = Visibility.Visible;

        if (IBRadio.IsChecked == true)
        {
            CredentialsHelpText.Text = "Connect to Interactive Brokers TWS or IB Gateway running on your machine.";
            AlpacaCredentials.Visibility = Visibility.Collapsed;
            PolygonCredentials.Visibility = Visibility.Collapsed;
            IBCredentials.Visibility = Visibility.Visible;
        }
        else if (AlpacaRadio.IsChecked == true)
        {
            CredentialsHelpText.Text = "Enter your Alpaca API credentials. Get them from your Alpaca dashboard.";
            AlpacaCredentials.Visibility = Visibility.Visible;
            PolygonCredentials.Visibility = Visibility.Collapsed;
            IBCredentials.Visibility = Visibility.Collapsed;
        }
        else if (PolygonRadio.IsChecked == true)
        {
            CredentialsHelpText.Text = "Enter your Polygon.io API key from your Polygon dashboard.";
            AlpacaCredentials.Visibility = Visibility.Collapsed;
            PolygonCredentials.Visibility = Visibility.Visible;
            IBCredentials.Visibility = Visibility.Collapsed;
        }

        CredentialsInfoBar.IsOpen = true;
    }

    private void ProviderCard_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Border border && border.Tag is string provider)
        {
            switch (provider)
            {
                case "IB":
                    IBRadio.IsChecked = true;
                    break;
                case "Alpaca":
                    AlpacaRadio.IsChecked = true;
                    break;
                case "Polygon":
                    PolygonRadio.IsChecked = true;
                    break;
            }
            UpdateProviderSelection();
        }
    }

    private void QuickAddSymbol_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string symbol)
        {
            AddSymbolToList(symbol);
            button.IsEnabled = false;
            button.Opacity = 0.5;
        }
    }

    private void CustomSymbol_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (!string.IsNullOrWhiteSpace(args.QueryText))
        {
            AddSymbolToList(args.QueryText.ToUpperInvariant());
            sender.Text = string.Empty;
        }
    }

    private void AddCustomSymbol_Click(object sender, RoutedEventArgs e)
    {
        var symbol = CustomSymbolBox.Text?.Trim().ToUpperInvariant();
        if (!string.IsNullOrWhiteSpace(symbol))
        {
            AddSymbolToList(symbol);
            CustomSymbolBox.Text = string.Empty;
        }
    }

    private void AddSymbolToList(string symbol)
    {
        // Check if already added
        if (_selectedSymbols.Any(s => s.Symbol == symbol))
        {
            return;
        }

        var subscriptions = new List<string>();
        if (SubscribeTradesCheck.IsChecked == true) subscriptions.Add("Trades");
        if (SubscribeDepthCheck.IsChecked == true) subscriptions.Add("Depth");
        if (SubscribeQuotesCheck.IsChecked == true) subscriptions.Add("Quotes");

        _selectedSymbols.Add(new WelcomeSymbolItem
        {
            Symbol = symbol,
            SubscribeTrades = SubscribeTradesCheck.IsChecked == true,
            SubscribeDepth = SubscribeDepthCheck.IsChecked == true,
            SubscribeQuotes = SubscribeQuotesCheck.IsChecked == true,
            SubscriptionText = string.Join(", ", subscriptions)
        });

        UpdateSymbolsUI();
    }

    private void RemoveSymbol_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string symbol)
        {
            var item = _selectedSymbols.FirstOrDefault(s => s.Symbol == symbol);
            if (item != null)
            {
                _selectedSymbols.Remove(item);
                UpdateSymbolsUI();

                // Re-enable quick add button if applicable
                var quickAddButton = FindQuickAddButton(symbol);
                if (quickAddButton != null)
                {
                    quickAddButton.IsEnabled = true;
                    quickAddButton.Opacity = 1.0;
                }
            }
        }
    }

    private Button? FindQuickAddButton(string symbol)
    {
        return symbol switch
        {
            "SPY" => AddSPYButton,
            "QQQ" => AddQQQButton,
            "AAPL" => AddAAPLButton,
            "MSFT" => AddMSFTButton,
            "TSLA" => AddTSLAButton,
            "AMZN" => AddAMZNButton,
            _ => null
        };
    }

    private void UpdateSymbolsUI()
    {
        SelectedSymbolsCount.Text = _selectedSymbols.Count.ToString();
        NoSymbolsText.Visibility = _selectedSymbols.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateSummary()
    {
        // Provider
        string provider = "Interactive Brokers";
        if (AlpacaRadio.IsChecked == true) provider = "Alpaca Markets";
        else if (PolygonRadio.IsChecked == true) provider = "Polygon.io";
        SummaryProvider.Text = provider;

        // Symbols
        var symbols = _selectedSymbols.Select(s => s.Symbol).ToList();
        SummarySymbols.Text = symbols.Count > 0
            ? string.Join(", ", symbols)
            : "No symbols configured";

        // Data types
        var dataTypes = new List<string>();
        if (_selectedSymbols.Any(s => s.SubscribeTrades)) dataTypes.Add("Trades");
        if (_selectedSymbols.Any(s => s.SubscribeDepth)) dataTypes.Add("Market Depth");
        if (_selectedSymbols.Any(s => s.SubscribeQuotes)) dataTypes.Add("Quotes");
        SummaryDataTypes.Text = dataTypes.Count > 0 ? string.Join(", ", dataTypes) : "None selected";
    }

    private async Task SaveConfigurationAndNavigate()
    {
        try
        {
            var config = await _configService.LoadConfigAsync() ?? new AppConfig();

            // Save provider
            if (IBRadio.IsChecked == true)
            {
                config.DataSource = "IB";
            }
            else if (AlpacaRadio.IsChecked == true)
            {
                config.DataSource = "Alpaca";
                config.Alpaca = new AlpacaOptions
                {
                    KeyId = AlpacaKeyIdBox.Text,
                    SecretKey = AlpacaSecretBox.Password,
                    UseSandbox = AlpacaSandboxCheck.IsChecked == true,
                    Feed = "iex"
                };
            }
            else if (PolygonRadio.IsChecked == true)
            {
                config.DataSource = "Polygon";
            }

            // Save symbols
            var symbolConfigs = _selectedSymbols.Select(s => new SymbolConfig
            {
                Symbol = s.Symbol,
                SubscribeTrades = s.SubscribeTrades,
                SubscribeDepth = s.SubscribeDepth,
                DepthLevels = 10,
                SecurityType = "STK",
                Exchange = "SMART",
                Currency = "USD"
            }).ToArray();

            if (symbolConfigs.Length > 0)
            {
                config.Symbols = symbolConfigs;
            }

            // Initialize settings if not present
            config.Settings ??= new AppSettings();

            await _configService.SaveConfigAsync(config);
        }
        catch
        {
            // Ignore errors during save - don't block navigation
        }

        NavigateToDashboard();
    }

    private void NavigateToDashboard()
    {
        if (Frame.CanGoBack)
        {
            Frame.GoBack();
        }
        else
        {
            Frame.Navigate(typeof(MainPage));
        }
    }
}

/// <summary>
/// Simple model for symbols in the welcome wizard.
/// </summary>
public class WelcomeSymbolItem
{
    public string Symbol { get; set; } = string.Empty;
    public bool SubscribeTrades { get; set; }
    public bool SubscribeDepth { get; set; }
    public bool SubscribeQuotes { get; set; }
    public string SubscriptionText { get; set; } = string.Empty;
}
