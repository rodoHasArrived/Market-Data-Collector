using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using MarketDataCollector.Uwp.Services;
using MarketDataCollector.Contracts.Configuration;

namespace MarketDataCollector.Uwp.Views;

/// <summary>
/// Guided setup wizard with preflight checks.
/// </summary>
public sealed partial class SetupWizardPage : Page
{
    private int _currentStep = 1;
    private const int TotalSteps = 4;
    private readonly SetupWizardService _wizardService;
    private string _selectedPreset = "minimal";
    private string _selectedProvider = "Alpaca";

    public SetupWizardPage()
    {
        this.InitializeComponent();
        _wizardService = new SetupWizardService();
        UpdateStepUI();
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

        BackButton.Visibility = _currentStep > 1 ? Visibility.Visible : Visibility.Collapsed;
        SkipButton.Visibility = _currentStep < TotalSteps ? Visibility.Visible : Visibility.Collapsed;

        NextButtonText.Text = _currentStep switch
        {
            1 => "Continue",
            2 => "Run Preflight Checks",
            3 => "Complete Setup",
            4 => "Go to Dashboard",
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

    private void PresetCard_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Border border && border.Tag is string preset)
        {
            _selectedPreset = preset;
            switch (preset)
            {
                case "day-trader":
                    DayTraderRadio.IsChecked = true;
                    break;
                case "researcher":
                    ResearcherRadio.IsChecked = true;
                    break;
                case "data-archivist":
                    ArchivistRadio.IsChecked = true;
                    break;
                case "minimal":
                    MinimalRadio.IsChecked = true;
                    break;
            }
        }
    }

    private void ProviderCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ProviderCombo.SelectedItem is ComboBoxItem item && item.Tag is string provider)
        {
            _selectedProvider = provider;

            AlpacaCredentialsPanel.Visibility = Visibility.Collapsed;
            PolygonCredentialsPanel.Visibility = Visibility.Collapsed;
            IBCredentialsPanel.Visibility = Visibility.Collapsed;
            StockSharpCredentialsPanel.Visibility = Visibility.Collapsed;
            GenericCredentialsPanel.Visibility = Visibility.Collapsed;

            switch (provider)
            {
                case "Alpaca":
                    AlpacaCredentialsPanel.Visibility = Visibility.Visible;
                    break;
                case "Polygon":
                    PolygonCredentialsPanel.Visibility = Visibility.Visible;
                    break;
                case "IB":
                    IBCredentialsPanel.Visibility = Visibility.Visible;
                    break;
                case "StockSharp":
                    StockSharpCredentialsPanel.Visibility = Visibility.Visible;
                    if (StockSharpConnectorCombo.SelectedItem == null)
                    {
                        StockSharpConnectorCombo.SelectedIndex = 0;
                    }
                    break;
                default:
                    GenericCredentialsPanel.Visibility = Visibility.Visible;
                    break;
            }
        }
    }

    private void StockSharpConnector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (StockSharpConnectorCombo.SelectedItem is not ComboBoxItem item || item.Tag is not string connector)
        {
            return;
        }

        StockSharpRithmicPanel.Visibility = Visibility.Collapsed;
        StockSharpIqFeedPanel.Visibility = Visibility.Collapsed;
        StockSharpCqgPanel.Visibility = Visibility.Collapsed;
        StockSharpIbPanel.Visibility = Visibility.Collapsed;
        StockSharpAdapterPanel.Visibility = Visibility.Collapsed;

        switch (connector)
        {
            case "Rithmic":
                StockSharpRithmicPanel.Visibility = Visibility.Visible;
                break;
            case "IQFeed":
                StockSharpIqFeedPanel.Visibility = Visibility.Visible;
                break;
            case "CQG":
                StockSharpCqgPanel.Visibility = Visibility.Visible;
                break;
            case "InteractiveBrokers":
                StockSharpIbPanel.Visibility = Visibility.Visible;
                break;
            case "Custom":
                StockSharpAdapterPanel.Visibility = Visibility.Visible;
                break;
        }
    }

    private void TestConnection_Click(object sender, RoutedEventArgs e)
    {
        _ = SafeTestConnectionAsync();
    }

    private async Task SafeTestConnectionAsync()
    {
        TestConnectionButton.IsEnabled = false;
        TestConnectionProgress.IsActive = true;
        ConnectionTestInfoBar.IsOpen = false;

        try
        {
            var credentials = GetCredentials();
            var result = await _wizardService.TestProviderConnectivityAsync(_selectedProvider, credentials);

            if (result.Success)
            {
                ConnectionTestInfoBar.Severity = InfoBarSeverity.Success;
                ConnectionTestInfoBar.Title = "Connection Successful";
                ConnectionTestInfoBar.Message = $"{result.StatusMessage}. Latency: {result.LatencyMs}ms";
            }
            else
            {
                ConnectionTestInfoBar.Severity = InfoBarSeverity.Error;
                ConnectionTestInfoBar.Title = "Connection Failed";
                ConnectionTestInfoBar.Message = result.ErrorMessage;
            }

            ConnectionTestInfoBar.IsOpen = true;
        }
        catch (Exception ex)
        {
            ConnectionTestInfoBar.Severity = InfoBarSeverity.Error;
            ConnectionTestInfoBar.Title = "Connection Test Error";
            ConnectionTestInfoBar.Message = ex.Message;
            ConnectionTestInfoBar.IsOpen = true;
            System.Diagnostics.Debug.WriteLine($"[SetupWizardPage] Error testing connection: {ex.Message}");
        }
        finally
        {
            TestConnectionButton.IsEnabled = true;
            TestConnectionProgress.IsActive = false;
        }
    }

    private Dictionary<string, string> GetCredentials()
    {
        var credentials = new Dictionary<string, string>();

        switch (_selectedProvider)
        {
            case "Alpaca":
                credentials["keyId"] = AlpacaKeyIdBox.Text ?? string.Empty;
                credentials["secretKey"] = AlpacaSecretKeyBox.Password ?? string.Empty;
                credentials["useSandbox"] = AlpacaSandboxCheck.IsChecked?.ToString() ?? "true";
                break;
            case "Polygon":
                credentials["apiKey"] = PolygonApiKeyBox.Text ?? string.Empty;
                break;
            case "IB":
                credentials["host"] = IBHostBox.Text ?? "127.0.0.1";
                credentials["port"] = IBPortBox.Value.ToString();
                break;
            case "StockSharp":
                return GetStockSharpCredentials();
            default:
                credentials["apiKey"] = GenericApiKeyBox.Text ?? string.Empty;
                break;
        }

        return credentials;
    }

    private Dictionary<string, string> GetStockSharpCredentials()
    {
        var credentials = new Dictionary<string, string>
        {
            ["connectorType"] = (StockSharpConnectorCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Rithmic",
            ["adapterType"] = StockSharpAdapterTypeBox.Text ?? string.Empty,
            ["adapterAssembly"] = StockSharpAdapterAssemblyBox.Text ?? string.Empty,
            ["connectionParams"] = StockSharpConnectionParamsBox.Text ?? string.Empty,
            ["storagePath"] = StockSharpStoragePathBox.Text ?? "data/stocksharp/{connector}",
            ["enableRealTime"] = StockSharpRealTimeCheck.IsChecked?.ToString() ?? "true",
            ["enableHistorical"] = StockSharpHistoricalCheck.IsChecked?.ToString() ?? "true",
            ["useBinaryStorage"] = StockSharpBinaryCheck.IsChecked?.ToString() ?? "false"
        };

        credentials["rithmicServer"] = RithmicServerBox.Text ?? "Rithmic Test";
        credentials["rithmicUsername"] = RithmicUserBox.Text ?? string.Empty;
        credentials["rithmicPassword"] = RithmicPasswordBox.Password ?? string.Empty;
        credentials["rithmicCertFile"] = RithmicCertBox.Text ?? string.Empty;
        credentials["rithmicPaper"] = RithmicPaperCheck.IsChecked?.ToString() ?? "true";

        credentials["iqfeedHost"] = IqFeedHostBox.Text ?? "127.0.0.1";
        credentials["iqfeedLevel1Port"] = IqFeedLevel1PortBox.Value.ToString();
        credentials["iqfeedLevel2Port"] = IqFeedLevel2PortBox.Value.ToString();
        credentials["iqfeedLookupPort"] = IqFeedLookupPortBox.Value.ToString();
        credentials["iqfeedProductId"] = IqFeedProductIdBox.Text ?? string.Empty;
        credentials["iqfeedProductVersion"] = IqFeedProductVersionBox.Text ?? "1.0";

        credentials["cqgUsername"] = CqgUserBox.Text ?? string.Empty;
        credentials["cqgPassword"] = CqgPasswordBox.Password ?? string.Empty;
        credentials["cqgDemo"] = CqgDemoCheck.IsChecked?.ToString() ?? "true";

        credentials["ibHost"] = StockSharpIbHostBox.Text ?? "127.0.0.1";
        credentials["ibPort"] = StockSharpIbPortBox.Value.ToString();
        credentials["ibClientId"] = StockSharpIbClientIdBox.Value.ToString();

        return credentials;
    }

    private void RunPreflight_Click(object sender, RoutedEventArgs e)
    {
        _ = SafeRunPreflightAsync();
    }

    private async Task SafeRunPreflightAsync()
    {
        RunPreflightButton.IsEnabled = false;
        PreflightProgress.IsActive = true;
        PreflightInfoBar.IsOpen = false;

        try
        {
            var result = await _wizardService.RunPreflightChecksAsync();

            UpdateCheckResult(NetworkCheckIcon, NetworkCheckMessage, NetworkCheckBadge, NetworkCheckStatus, result.NetworkCheck);
            UpdateCheckResult(DiskCheckIcon, DiskCheckMessage, DiskCheckBadge, DiskCheckStatus, result.DiskSpaceCheck);
            UpdateCheckResult(PermissionsCheckIcon, PermissionsCheckMessage, PermissionsCheckBadge, PermissionsCheckStatus, result.StoragePermissionCheck);
            UpdateCheckResult(ServiceCheckIcon, ServiceCheckMessage, ServiceCheckBadge, ServiceCheckStatus, result.CollectorServiceCheck);

            // Test provider connection
            var credentials = GetCredentials();
            var providerResult = await _wizardService.TestProviderConnectivityAsync(_selectedProvider, credentials);
            UpdateCheckResult(ProviderCheckIcon, ProviderCheckMessage, ProviderCheckBadge, ProviderCheckStatus,
                new CheckResult
                {
                    Name = "Provider Connection",
                    Success = providerResult.Success,
                    Message = providerResult.Success ? providerResult.StatusMessage : providerResult.ErrorMessage
                });

            if (result.AllPassed && providerResult.Success)
            {
                PreflightInfoBar.Severity = InfoBarSeverity.Success;
                PreflightInfoBar.Title = "All Checks Passed";
                PreflightInfoBar.Message = "Your system is ready for data collection.";
            }
            else
            {
                PreflightInfoBar.Severity = InfoBarSeverity.Warning;
                PreflightInfoBar.Title = "Some Checks Need Attention";
                PreflightInfoBar.Message = "Review the results above and address any issues.";
            }

            PreflightInfoBar.IsOpen = true;
        }
        catch (Exception ex)
        {
            PreflightInfoBar.Severity = InfoBarSeverity.Error;
            PreflightInfoBar.Title = "Preflight Check Error";
            PreflightInfoBar.Message = ex.Message;
            PreflightInfoBar.IsOpen = true;
            System.Diagnostics.Debug.WriteLine($"[SetupWizardPage] Error during preflight: {ex.Message}");
        }
        finally
        {
            RunPreflightButton.IsEnabled = true;
            PreflightProgress.IsActive = false;
        }
    }

    private void UpdateCheckResult(FontIcon icon, TextBlock message, Border badge, TextBlock status, CheckResult result)
    {
        message.Text = result.Message;

        if (result.Success)
        {
            icon.Glyph = "\uE73E";
            icon.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Green);
            badge.Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(0x1A, 0x3f, 0xb9, 0x50));
            status.Text = "Passed";
            status.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Green);
        }
        else
        {
            icon.Glyph = "\uE711";
            icon.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Red);
            badge.Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(0x1A, 0xf8, 0x51, 0x49));
            status.Text = "Failed";
            status.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Red);
        }
    }

    private void Next_Click(object sender, RoutedEventArgs e)
    {
        _ = SafeNextClickAsync(sender, e);
    }

    private async Task SafeNextClickAsync(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_currentStep < TotalSteps)
            {
                if (_currentStep == 2)
                {
                    // Auto-run preflight when moving to step 3
                    _currentStep++;
                    UpdateStepUI();
                    RunPreflight_Click(sender, e);
                    return;
                }

                if (_currentStep == 3)
                {
                    // Save configuration before completing
                    await SaveConfigurationAsync();
                    UpdateSummary();
                }

                _currentStep++;
                UpdateStepUI();

                if (_currentStep == 2)
                {
                    // Set default provider based on preset
                    var presets = _wizardService.GetSetupPresets();
                    var preset = presets.FirstOrDefault(p => p.Id == _selectedPreset);
                    if (preset?.RecommendedProviders.Length > 0)
                    {
                        var recommendedProvider = preset.RecommendedProviders[0];
                        foreach (ComboBoxItem item in ProviderCombo.Items)
                        {
                            if (item.Content?.ToString()?.Contains(recommendedProvider.Split(' ')[0]) == true)
                            {
                                ProviderCombo.SelectedItem = item;
                                break;
                            }
                        }
                    }
                }
            }
            else
            {
                NavigateToDashboard();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SetupWizardPage] Error during next click: {ex.Message}");
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

    private async Task SaveConfigurationAsync()
    {
        var presets = _wizardService.GetSetupPresets();
        var preset = presets.FirstOrDefault(p => p.Id == _selectedPreset);

        if (preset != null)
        {
            await _wizardService.ApplyPresetAsync(preset, _selectedProvider);
        }

        var credentials = GetCredentials();
        await _wizardService.SaveCredentialsAsync(_selectedProvider, credentials);

        if (_selectedProvider == "StockSharp")
        {
            var stockSharpOptions = BuildStockSharpOptions(credentials);
            await _wizardService.ApplyStockSharpConfigAsync(stockSharpOptions);
        }
    }

    private static StockSharpOptionsDto BuildStockSharpOptions(Dictionary<string, string> credentials)
    {
        var connectorType = credentials.GetValueOrDefault("connectorType", "Rithmic");
        var options = new StockSharpOptionsDto
        {
            Enabled = true,
            ConnectorType = connectorType,
            AdapterType = credentials.GetValueOrDefault("adapterType"),
            AdapterAssembly = credentials.GetValueOrDefault("adapterAssembly"),
            ConnectionParams = ParseConnectionParams(credentials.GetValueOrDefault("connectionParams")),
            StoragePath = credentials.GetValueOrDefault("storagePath", "data/stocksharp/{connector}"),
            EnableRealTime = bool.TryParse(credentials.GetValueOrDefault("enableRealTime", "true"), out var enableRealtime) && enableRealtime,
            EnableHistorical = bool.TryParse(credentials.GetValueOrDefault("enableHistorical", "true"), out var enableHistorical) && enableHistorical,
            UseBinaryStorage = bool.TryParse(credentials.GetValueOrDefault("useBinaryStorage", "false"), out var useBinary) && useBinary
        };

        options.Rithmic = new RithmicOptionsDto
        {
            Server = credentials.GetValueOrDefault("rithmicServer", "Rithmic Test"),
            UserName = credentials.GetValueOrDefault("rithmicUsername"),
            Password = credentials.GetValueOrDefault("rithmicPassword"),
            CertFile = credentials.GetValueOrDefault("rithmicCertFile"),
            UsePaperTrading = bool.TryParse(credentials.GetValueOrDefault("rithmicPaper", "true"), out var rithmicPaper) && rithmicPaper
        };

        options.IQFeed = new IQFeedOptionsDto
        {
            Host = credentials.GetValueOrDefault("iqfeedHost", "127.0.0.1"),
            Level1Port = ParseInt(credentials.GetValueOrDefault("iqfeedLevel1Port"), 9100),
            Level2Port = ParseInt(credentials.GetValueOrDefault("iqfeedLevel2Port"), 9200),
            LookupPort = ParseInt(credentials.GetValueOrDefault("iqfeedLookupPort"), 9300),
            ProductId = credentials.GetValueOrDefault("iqfeedProductId"),
            ProductVersion = credentials.GetValueOrDefault("iqfeedProductVersion", "1.0") ?? "1.0"
        };

        options.CQG = new CQGOptionsDto
        {
            UserName = credentials.GetValueOrDefault("cqgUsername"),
            Password = credentials.GetValueOrDefault("cqgPassword"),
            UseDemoServer = bool.TryParse(credentials.GetValueOrDefault("cqgDemo", "true"), out var cqgDemo) && cqgDemo
        };

        options.InteractiveBrokers = new StockSharpIBOptionsDto
        {
            Host = credentials.GetValueOrDefault("ibHost", "127.0.0.1"),
            Port = ParseInt(credentials.GetValueOrDefault("ibPort"), 7496),
            ClientId = ParseInt(credentials.GetValueOrDefault("ibClientId"), 1)
        };

        return options;
    }

    private static Dictionary<string, string>? ParseConnectionParams(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var items = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in items)
        {
            var parts = item.Split('=', 2, StringSplitOptions.TrimEntries);
            if (parts.Length == 2)
            {
                result[parts[0]] = parts[1];
            }
        }

        return result.Count > 0 ? result : null;
    }

    private static int ParseInt(string? value, int fallback)
        => int.TryParse(value, out var parsed) ? parsed : fallback;

    private void UpdateSummary()
    {
        var presets = _wizardService.GetSetupPresets();
        var preset = presets.FirstOrDefault(p => p.Id == _selectedPreset);

        SummaryPreset.Text = preset?.Name ?? _selectedPreset;
        SummaryProvider.Text = _selectedProvider;
        SummarySymbols.Text = preset != null ? string.Join(", ", preset.DefaultSymbols) : "SPY";

        var dataTypes = new List<string>();
        if (preset?.SubscribeTrades == true) dataTypes.Add("Trades");
        if (preset?.SubscribeDepth == true) dataTypes.Add("Market Depth");
        if (preset?.SubscribeQuotes == true) dataTypes.Add("Quotes");
        SummaryDataTypes.Text = dataTypes.Count > 0 ? string.Join(", ", dataTypes) : "Trades";
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
