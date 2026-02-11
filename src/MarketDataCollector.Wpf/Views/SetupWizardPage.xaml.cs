using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using MarketDataCollector.Wpf.Services;

namespace MarketDataCollector.Wpf.Views;

public partial class SetupWizardPage : Page
{
    private static readonly string[] ProviderOptions =
    {
        "NoOp",
        "Stooq",
        "NasdaqDataLink",
        "Polygon",
        "Alpaca"
    };

    private readonly ConnectionService _connectionService;
    private readonly FirstRunService _firstRunService;
    private readonly BackendServiceManager _backendServiceManager;
    private readonly HttpClient _httpClient;

    public SetupWizardPage()
    {
        InitializeComponent();
        _connectionService = ConnectionService.Instance;
        _firstRunService = FirstRunService.Instance;
        _backendServiceManager = BackendServiceManager.Instance;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
    }

    private async void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        ProviderCombo.ItemsSource = ProviderOptions;
        ConfigPathText.Text = _firstRunService.ConfigFilePath;

        LoadExistingConfiguration();
        LoadStoredApiKeys();

        await EnsureBackendAvailableAsync();
    }

    private void LoadExistingConfiguration()
    {
        try
        {
            if (!File.Exists(_firstRunService.ConfigFilePath))
            {
                ProviderCombo.SelectedItem = ProviderOptions[0];
                StorageLocationTextBox.Text = "data";
                ConfigStatusText.Text = "No configuration file found yet. A default will be created.";
                return;
            }

            var json = File.ReadAllText(_firstRunService.ConfigFilePath);
            var root = JsonNode.Parse(json) ?? new JsonObject();

            var dataSource = root["DataSource"]?.GetValue<string>() ?? ProviderOptions[0];
            if (Array.IndexOf(ProviderOptions, dataSource) < 0)
            {
                ProviderCombo.ItemsSource = new[] { dataSource }.Concat(ProviderOptions);
            }

            ProviderCombo.SelectedItem = dataSource;

            var storageBase = root["Storage"]?["BaseDirectory"]?.GetValue<string>() ?? "data";
            StorageLocationTextBox.Text = storageBase;

            ConfigStatusText.Text = "Loaded existing configuration.";
        }
        catch (Exception ex)
        {
            ConfigStatusText.Text = $"Unable to read configuration. {ex.Message}";
            ProviderCombo.SelectedItem = ProviderOptions[0];
            StorageLocationTextBox.Text = "data";
        }
    }

    private void LoadStoredApiKeys()
    {
        var nasdaqKey = Environment.GetEnvironmentVariable("NASDAQDATALINK__APIKEY", EnvironmentVariableTarget.User);
        if (!string.IsNullOrWhiteSpace(nasdaqKey))
        {
            NasdaqApiKeyTextBox.Text = nasdaqKey;
        }

        var openFigiKey = Environment.GetEnvironmentVariable("OPENFIGI__APIKEY", EnvironmentVariableTarget.User);
        if (!string.IsNullOrWhiteSpace(openFigiKey))
        {
            OpenFigiApiKeyTextBox.Text = openFigiKey;
        }
    }

    private async void TestConnection_Click(object sender, RoutedEventArgs e)
    {
        await RefreshBackendStatusAsync();
    }

    private async Task RefreshBackendStatusAsync()
    {
        var serviceStatus = await _backendServiceManager.GetStatusAsync();

        BackendStatusText.Text = "Checking backend status...";
        BackendStatusDetailText.Text = $"Testing {_connectionService.ServiceUrl}/healthz";
        BackendStatusDot.Fill = (System.Windows.Media.Brush)FindResource("WarningColorBrush");

        var result = await CheckBackendAsync();

        if (result.isHealthy)
        {
            BackendStatusDot.Fill = (System.Windows.Media.Brush)FindResource("SuccessColorBrush");
            BackendStatusText.Text = "Backend is running.";
            BackendStatusDetailText.Text = $"Latency: {result.latencyMs} ms · {serviceStatus.StatusMessage}";
        }
        else
        {
            BackendStatusDot.Fill = (System.Windows.Media.Brush)FindResource("ErrorColorBrush");
            BackendStatusText.Text = "Backend not reachable.";
            BackendStatusDetailText.Text = $"{result.message} · {serviceStatus.StatusMessage}";
        }
    }

    private async Task EnsureBackendAvailableAsync()
    {
        var status = await _backendServiceManager.GetStatusAsync();
        if (!status.IsRunning)
        {
            BackendStatusText.Text = "Starting backend service...";
            BackendStatusDetailText.Text = "First run now auto-starts the backend service.";

            var startResult = await _backendServiceManager.StartAsync();
            if (!startResult.Success)
            {
                NotificationService.Instance.NotifyWarning("Backend", startResult.Message);
            }
        }

        await RefreshBackendStatusAsync();
    }

    private async Task<(bool isHealthy, string message, int latencyMs)> CheckBackendAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(8));
            var response = await _httpClient.GetAsync($"{_connectionService.ServiceUrl}/healthz", cts.Token);
            stopwatch.Stop();
            if (response.IsSuccessStatusCode)
            {
                return (true, "Healthy", (int)stopwatch.Elapsed.TotalMilliseconds);
            }

            return (false, $"Health check returned {response.StatusCode}", 0);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return (false, ex.Message, 0);
        }
    }

    private async void StartBackend_Click(object sender, RoutedEventArgs e)
    {
        var result = await _backendServiceManager.StartAsync();
        if (result.Success)
        {
            NotificationService.Instance.NotifyInfo("Backend", result.Message);
        }
        else
        {
            NotificationService.Instance.NotifyWarning("Backend", result.Message);
        }

        await RefreshBackendStatusAsync();
    }

    private void OpenInstructions_Click(object sender, RoutedEventArgs e)
    {
        OpenUrl("https://github.com/example/market-data-collector/blob/main/docs/guides/configuration.md");
    }

    private void UseDefaultStorage_Click(object sender, RoutedEventArgs e)
    {
        StorageLocationTextBox.Text = "data";
    }

    private async void SaveAndContinue_Click(object sender, RoutedEventArgs e)
    {
        ValidationStatusText.Text = "Saving configuration...";

        if (!TryGetWizardInputs(out var provider, out var storageLocation))
        {
            return;
        }

        var saveResult = await SaveConfigurationAsync(provider, storageLocation);
        if (!saveResult)
        {
            return;
        }

        SaveApiKeys();

        var startResult = await _backendServiceManager.StartAsync();
        if (!startResult.Success)
        {
            ValidationStatusText.Text = $"Saved config, but backend start failed: {startResult.Message}";
            NotificationService.Instance.NotifyWarning("Setup Wizard", "Configuration saved, but backend is offline.");
            return;
        }

        var backendResult = await CheckBackendAsync();
        if (!backendResult.isHealthy)
        {
            ValidationStatusText.Text = $"Saved config, but backend is unreachable: {backendResult.message}";
            NotificationService.Instance.NotifyWarning("Setup Wizard", "Configuration saved, but backend is offline.");
            return;
        }

        ValidationStatusText.Text = "Configuration saved and backend verified. Redirecting to dashboard...";
        NotificationService.Instance.NotifySuccess("Setup Wizard", "Setup complete. Welcome!");

        MarketDataCollector.Wpf.Services.NavigationService.Instance.NavigateTo("Dashboard");
    }

    private bool TryGetWizardInputs(out string provider, out string storageLocation)
    {
        provider = ProviderCombo.SelectedItem as string ?? string.Empty;
        storageLocation = StorageLocationTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(provider))
        {
            ValidationStatusText.Text = "Select a default provider before continuing.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(storageLocation))
        {
            ValidationStatusText.Text = "Enter a storage location before continuing.";
            return false;
        }

        return true;
    }

    private async Task<bool> SaveConfigurationAsync(string provider, string storageLocation)
    {
        try
        {
            if (Path.IsPathRooted(storageLocation))
            {
                Directory.CreateDirectory(storageLocation);
            }

            var configDirectory = Path.GetDirectoryName(_firstRunService.ConfigFilePath);
            if (!string.IsNullOrWhiteSpace(configDirectory))
            {
                Directory.CreateDirectory(configDirectory);
            }

            JsonNode rootNode;
            if (File.Exists(_firstRunService.ConfigFilePath))
            {
                var json = await File.ReadAllTextAsync(_firstRunService.ConfigFilePath);
                rootNode = JsonNode.Parse(json) ?? new JsonObject();
            }
            else
            {
                rootNode = new JsonObject();
            }

            rootNode["DataSource"] = provider;

            var storageNode = rootNode["Storage"] as JsonObject ?? new JsonObject();
            storageNode["BaseDirectory"] = storageLocation;
            rootNode["Storage"] = storageNode;

            var backfillNode = rootNode["Backfill"] as JsonObject ?? new JsonObject();
            backfillNode["DefaultProvider"] = provider;
            rootNode["Backfill"] = backfillNode;

            var output = rootNode.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true
            });

            await File.WriteAllTextAsync(_firstRunService.ConfigFilePath, output);
            ConfigStatusText.Text = "Configuration saved.";

            return true;
        }
        catch (Exception ex)
        {
            ValidationStatusText.Text = $"Failed to save configuration: {ex.Message}";
            _ = NotificationService.Instance.NotifyErrorAsync("Setup Wizard", "Failed to save configuration.");
            return false;
        }
    }

    private void SaveApiKeys()
    {
        SaveApiKey("NASDAQDATALINK__APIKEY", NasdaqApiKeyTextBox.Text);
        SaveApiKey("OPENFIGI__APIKEY", OpenFigiApiKeyTextBox.Text);
    }

    private static void SaveApiKey(string variableName, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            Environment.SetEnvironmentVariable(variableName, null, EnvironmentVariableTarget.User);
            return;
        }

        Environment.SetEnvironmentVariable(variableName, value.Trim(), EnvironmentVariableTarget.User);
    }

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch
        {
            NotificationService.Instance.ShowNotification(
                "Error",
                "Could not open the link. Please try again.",
                NotificationType.Error);
        }
    }
}
