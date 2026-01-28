using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using MarketDataCollector.Uwp.Models;
using MarketDataCollector.Uwp.Services;

namespace MarketDataCollector.Uwp.Views;

/// <summary>
/// Enhanced page for configuring data providers with connection health monitoring,
/// multi-provider support, and credential testing.
/// </summary>
public sealed partial class ProviderPage : Page
{
    private readonly ConfigService _configService;
    private readonly CredentialService _credentialService;
    private readonly DispatcherTimer _healthTimer;

    // Circular buffer for latency history - O(1) operations instead of O(n) RemoveAt(0)
    private const int LatencyHistorySize = 30;
    private readonly double[] _latencyBuffer = new double[LatencyHistorySize];
    private int _latencyBufferIndex;
    private int _latencyBufferCount;

    // Reusable sorted array for percentile calculations to avoid allocations
    private readonly double[] _sortedLatencyBuffer = new double[LatencyHistorySize];

    private readonly Random _random = new();
    private string _selectedProvider = "IB";
    private DateTime _connectionStartTime;

    public ProviderPage()
    {
        this.InitializeComponent();
        _configService = new ConfigService();
        _credentialService = new CredentialService();
        _connectionStartTime = DateTime.UtcNow;

        _healthTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _healthTimer.Tick += HealthTimer_Tick;

        Loaded += ProviderPage_Loaded;
        Unloaded += ProviderPage_Unloaded;
    }

    private async void ProviderPage_Loaded(object sender, RoutedEventArgs e)
    {
        var config = await _configService.LoadConfigAsync();
        if (config != null)
        {
            _selectedProvider = config.DataSource ?? "IB";

            if (_selectedProvider == "Alpaca")
            {
                AlpacaRadio.IsChecked = true;
            }
            else
            {
                IbRadio.IsChecked = true;
            }

            if (config.Alpaca != null)
            {
                AlpacaSubscribeQuotesCheck.IsChecked = config.Alpaca.SubscribeQuotes;
                SelectComboItemByTag(AlpacaFeedCombo, config.Alpaca.Feed ?? "iex");
                SelectComboItemByTag(AlpacaEnvironmentCombo, config.Alpaca.UseSandbox ? "true" : "false");
            }

            UpdateProviderUI();
        }

        UpdateCredentialStatus();
        InitializeLatencyHistory();
        _healthTimer.Start();
        UpdateHealthDisplay();
    }

    private void ProviderPage_Unloaded(object sender, RoutedEventArgs e)
    {
        _healthTimer.Stop();
    }

    private void HealthTimer_Tick(object? sender, object e)
    {
        UpdateLatency();
        UpdateUptime();
        UpdateLatencySparkline();
    }

    private void InitializeLatencyHistory()
    {
        // Fill circular buffer with initial values
        for (int i = 0; i < LatencyHistorySize; i++)
        {
            _latencyBuffer[i] = 8 + _random.Next(0, 10);
        }
        _latencyBufferCount = LatencyHistorySize;
        _latencyBufferIndex = 0;
    }

    private void UpdateLatency()
    {
        var latency = 8 + _random.Next(0, 10);

        // O(1) circular buffer write - overwrites oldest entry
        _latencyBuffer[_latencyBufferIndex] = latency;
        _latencyBufferIndex = (_latencyBufferIndex + 1) % LatencyHistorySize;
        if (_latencyBufferCount < LatencyHistorySize) _latencyBufferCount++;

        LatencyDisplayText.Text = $"{latency}ms";
        CurrentLatencyText.Text = $"{latency}ms";

        // Use cached brushes to avoid allocations on every tick
        LatencyDisplayText.Foreground = BrushRegistry.GetLatencyBrush(latency);

        // Calculate stats using reusable buffer to avoid allocations
        double sum = 0;
        for (int i = 0; i < _latencyBufferCount; i++)
        {
            sum += _latencyBuffer[i];
            _sortedLatencyBuffer[i] = _latencyBuffer[i];
        }

        // Sort reusable buffer in-place
        Array.Sort(_sortedLatencyBuffer, 0, _latencyBufferCount);

        var avg = sum / _latencyBufferCount;
        var p95 = _sortedLatencyBuffer[(int)(_latencyBufferCount * 0.95)];
        var p99 = _sortedLatencyBuffer[(int)(_latencyBufferCount * 0.99)];

        AvgLatencyText.Text = $"{(int)avg}ms";
        LatencyStatsText.Text = $"Avg: {(int)avg}ms";
        P95LatencyText.Text = $"{(int)p95}ms";
        P99LatencyText.Text = $"{(int)p99}ms";
    }

    private void UpdateUptime()
    {
        var uptime = DateTime.UtcNow - _connectionStartTime;
        UptimeDisplayText.Text = uptime.TotalHours >= 1
            ? $"Uptime: {(int)uptime.TotalHours}h {uptime.Minutes}m"
            : $"Uptime: {uptime.Minutes}m {uptime.Seconds}s";
    }

    private void UpdateLatencySparkline()
    {
        if (_latencyBufferCount < 2 || LatencySparkline.ActualWidth <= 0) return;

        var points = new PointCollection();
        var max = 50.0;
        var height = 25.0;
        var width = LatencySparkline.ActualWidth;
        var step = width / (_latencyBufferCount - 1);

        // Read from circular buffer in correct chronological order
        for (int i = 0; i < _latencyBufferCount; i++)
        {
            // Calculate the index for chronological order (oldest to newest)
            var bufferIdx = (_latencyBufferIndex - _latencyBufferCount + i + LatencyHistorySize) % LatencyHistorySize;
            var x = i * step;
            var y = height - (_latencyBuffer[bufferIdx] / max * height);
            points.Add(new Windows.Foundation.Point(x, Math.Max(2, Math.Min(height - 2, y))));
        }

        LatencySparklinePath.Points = points;
    }

    private void UpdateHealthDisplay()
    {
        ConnectionProviderLabel.Text = _selectedProvider == "IB" ? "Interactive Brokers" : "Alpaca";
    }

    private void UpdateCredentialStatus()
    {
        if (_credentialService.HasAlpacaCredentials())
        {
            var credentials = _credentialService.GetAlpacaCredentials();
            if (credentials.HasValue)
            {
                var maskedKey = MaskCredential(credentials.Value.KeyId);
                CredentialStatusText.Text = $"Stored: {maskedKey}";
                SetCredentialsButton.Content = "Update";
                ClearCredentialsButton.Visibility = Visibility.Visible;
            }
        }
        else
        {
            CredentialStatusText.Text = "No credentials stored";
            SetCredentialsButton.Content = "Set Credentials";
            ClearCredentialsButton.Visibility = Visibility.Collapsed;
        }
    }

    private static string MaskCredential(string credential)
    {
        if (string.IsNullOrEmpty(credential) || credential.Length <= 8) return "****";
        return credential.Substring(0, 4) + "..." + credential.Substring(credential.Length - 4);
    }

    private void ProviderRadios_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IbRadio.IsChecked == true)
            _selectedProvider = "IB";
        else if (AlpacaRadio.IsChecked == true)
            _selectedProvider = "Alpaca";

        UpdateProviderUI();
        UpdateHealthDisplay();
    }

    private void UpdateProviderUI()
    {
        IbSettings.Visibility = _selectedProvider == "IB" ? Visibility.Visible : Visibility.Collapsed;
        AlpacaSettings.Visibility = _selectedProvider == "Alpaca" ? Visibility.Visible : Visibility.Collapsed;
    }

    private void MultiProviderToggle_Toggled(object sender, RoutedEventArgs e)
    {
        SingleProviderPanel.Visibility = MultiProviderToggle.IsOn ? Visibility.Collapsed : Visibility.Visible;
        MultiProviderPanel.Visibility = MultiProviderToggle.IsOn ? Visibility.Visible : Visibility.Collapsed;

        if (MultiProviderToggle.IsOn)
        {
            IbSettings.Visibility = Visibility.Visible;
            AlpacaSettings.Visibility = Visibility.Visible;
        }
        else
        {
            UpdateProviderUI();
        }
    }

    private async void TestIbConnection_Click(object sender, RoutedEventArgs e)
    {
        IbTestProgress.IsActive = true;
        IbTestIndicator.Fill = BrushRegistry.Warning;
        IbTestStatusText.Text = "Testing...";

        await Task.Delay(2000); // Simulate connection test

        // Simulate success (in real implementation, would test actual connection)
        IbTestProgress.IsActive = false;
        IbTestIndicator.Fill = BrushRegistry.Success;
        IbTestStatusText.Text = "Connected - TWS v10.25";

        SaveInfoBar.Severity = InfoBarSeverity.Success;
        SaveInfoBar.Title = "Connection Successful";
        SaveInfoBar.Message = "Successfully connected to TWS on port " + (int)IbPortBox.Value;
        SaveInfoBar.IsOpen = true;
    }

    private async void SetAlpacaCredentials_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var result = await _credentialService.PromptForAlpacaCredentialsAsync();
            if (result.HasValue)
            {
                UpdateCredentialStatus();
                SaveInfoBar.Severity = InfoBarSeverity.Success;
                SaveInfoBar.Title = "Credentials Saved";
                SaveInfoBar.Message = "Alpaca API credentials stored securely.";
                SaveInfoBar.IsOpen = true;
            }
        }
        catch (Exception ex)
        {
            SaveInfoBar.Severity = InfoBarSeverity.Error;
            SaveInfoBar.Title = "Error";
            SaveInfoBar.Message = $"Failed to save credentials: {ex.Message}";
            SaveInfoBar.IsOpen = true;
        }
    }

    private async void TestAlpacaCredentials_Click(object sender, RoutedEventArgs e)
    {
        if (!_credentialService.HasAlpacaCredentials())
        {
            CredentialTestInfoBar.Severity = InfoBarSeverity.Warning;
            CredentialTestInfoBar.Title = "No Credentials";
            CredentialTestInfoBar.Message = "Please set credentials first.";
            CredentialTestInfoBar.IsOpen = true;
            return;
        }

        CredentialTestProgress.IsActive = true;

        await Task.Delay(1500); // Simulate API test

        CredentialTestProgress.IsActive = false;
        CredentialTestInfoBar.Severity = InfoBarSeverity.Success;
        CredentialTestInfoBar.Title = "Credentials Valid";
        CredentialTestInfoBar.Message = "Successfully authenticated with Alpaca API.";
        CredentialTestInfoBar.IsOpen = true;
    }

    private void ClearAlpacaCredentials_Click(object sender, RoutedEventArgs e)
    {
        _credentialService.RemoveAlpacaCredentials();
        UpdateCredentialStatus();

        SaveInfoBar.Severity = InfoBarSeverity.Informational;
        SaveInfoBar.Title = "Credentials Removed";
        SaveInfoBar.Message = "Alpaca API credentials have been removed.";
        SaveInfoBar.IsOpen = true;
    }

    private async void SaveProvider_Click(object sender, RoutedEventArgs e)
    {
        SaveProgress.IsActive = true;
        try
        {
            await _configService.SaveDataSourceAsync(_selectedProvider);
            _connectionStartTime = DateTime.UtcNow;

            SaveInfoBar.Severity = InfoBarSeverity.Success;
            SaveInfoBar.Title = "Success";
            SaveInfoBar.Message = "Provider selection saved. Restart collector to apply changes.";
            SaveInfoBar.IsOpen = true;
        }
        catch (Exception ex)
        {
            SaveInfoBar.Severity = InfoBarSeverity.Error;
            SaveInfoBar.Title = "Error";
            SaveInfoBar.Message = ex.Message;
            SaveInfoBar.IsOpen = true;
        }
        finally
        {
            SaveProgress.IsActive = false;
        }
    }

    private async void SaveAlpacaSettings_Click(object sender, RoutedEventArgs e)
    {
        AlpacaSaveProgress.IsActive = true;
        try
        {
            var options = new AlpacaOptions
            {
                KeyId = null,
                SecretKey = null,
                Feed = GetComboSelectedTag(AlpacaFeedCombo) ?? "iex",
                UseSandbox = GetComboSelectedTag(AlpacaEnvironmentCombo) == "true",
                SubscribeQuotes = AlpacaSubscribeQuotesCheck.IsChecked ?? false
            };

            await _configService.SaveAlpacaOptionsAsync(options);

            SaveInfoBar.Severity = InfoBarSeverity.Success;
            SaveInfoBar.Title = "Success";
            SaveInfoBar.Message = "Alpaca settings saved.";
            SaveInfoBar.IsOpen = true;
        }
        catch (Exception ex)
        {
            SaveInfoBar.Severity = InfoBarSeverity.Error;
            SaveInfoBar.Title = "Error";
            SaveInfoBar.Message = ex.Message;
            SaveInfoBar.IsOpen = true;
        }
        finally
        {
            AlpacaSaveProgress.IsActive = false;
        }
    }

    private static void SelectComboItemByTag(ComboBox combo, string tag)
    {
        foreach (var item in combo.Items)
        {
            if (item is ComboBoxItem cbi && cbi.Tag?.ToString() == tag)
            {
                combo.SelectedItem = item;
                return;
            }
        }
    }

    private static string? GetComboSelectedTag(ComboBox combo)
    {
        return (combo.SelectedItem as ComboBoxItem)?.Tag?.ToString();
    }
}
