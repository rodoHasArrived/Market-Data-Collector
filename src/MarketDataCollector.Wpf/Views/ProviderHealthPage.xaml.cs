using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MarketDataCollector.Wpf.Contracts;
using WpfServices = MarketDataCollector.Wpf.Services;
using Timer = System.Timers.Timer;

namespace MarketDataCollector.Wpf.Views;

/// <summary>
/// Provider health monitoring page showing connection status, latency, and history.
/// </summary>
public partial class ProviderHealthPage : Page
{
    private readonly StatusService _statusService;
    private readonly ConnectionService _connectionService;
    private readonly WpfServices.LoggingService _loggingService;
    private readonly WpfServices.NotificationService _notificationService;
    private readonly ObservableCollection<ProviderStatusModel> _streamingProviders = new();
    private readonly ObservableCollection<BackfillProviderModel> _backfillProviders = new();
    private readonly ObservableCollection<ConnectionEventModel> _connectionHistory = new();
    private Timer? _refreshTimer;
    private CancellationTokenSource? _cts;
    private string _baseUrl = "http://localhost:8080";

    public ProviderHealthPage(
        StatusService statusService,
        ConnectionService connectionService,
        WpfServices.LoggingService loggingService,
        WpfServices.NotificationService notificationService)
    {
        InitializeComponent();

        _statusService = statusService;
        _connectionService = connectionService;
        _loggingService = loggingService;
        _notificationService = notificationService;

        StreamingProvidersControl.ItemsSource = _streamingProviders;
        BackfillProvidersControl.ItemsSource = _backfillProviders;
        ConnectionHistoryControl.ItemsSource = _connectionHistory;

        // Get base URL from StatusService
        _baseUrl = _statusService.BaseUrl;

        // Subscribe to connection events
        _connectionService.StateChanged += OnConnectionStateChanged;
        _connectionService.ConnectionHealthUpdated += OnConnectionHealthUpdated;

        Unloaded += OnPageUnloaded;
    }

    private void OnPageUnloaded(object sender, RoutedEventArgs e)
    {
        _connectionService.StateChanged -= OnConnectionStateChanged;
        _connectionService.ConnectionHealthUpdated -= OnConnectionHealthUpdated;
        _refreshTimer?.Stop();
        _refreshTimer?.Dispose();
        _cts?.Cancel();
        _cts?.Dispose();
    }

    private async void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        await RefreshDataAsync();

        // Start auto-refresh timer (every 30 seconds)
        _refreshTimer = new Timer(30000);
        _refreshTimer.Elapsed += async (_, _) => await Dispatcher.InvokeAsync(RefreshDataAsync);
        _refreshTimer.Start();
    }

    private void OnConnectionStateChanged(object? sender, ConnectionStateChangedEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            AddConnectionEvent(
                e.NewState == ConnectionState.Connected ? "Connected" :
                e.NewState == ConnectionState.Disconnected ? "Disconnected" :
                e.NewState == ConnectionState.Reconnecting ? "Reconnecting" : "Unknown",
                e.Provider,
                e.NewState == ConnectionState.Connected ? EventType.Success :
                e.NewState == ConnectionState.Disconnected ? EventType.Error : EventType.Warning);
        });
    }

    private void OnConnectionHealthUpdated(object? sender, ConnectionHealthEventArgs e)
    {
        if (!e.IsHealthy)
        {
            Dispatcher.Invoke(() =>
            {
                AddConnectionEvent(
                    $"Health check failed: {e.ErrorMessage}",
                    _connectionService.CurrentProvider,
                    EventType.Warning);
            });
        }
    }

    private async Task RefreshDataAsync()
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();

        try
        {
            // Load streaming providers
            await LoadStreamingProvidersAsync(_cts.Token);

            // Load backfill providers
            await LoadBackfillProvidersAsync(_cts.Token);

            // Update summary stats
            UpdateSummaryStats();

            // Update last refresh time
            LastUpdateText.Text = $"Last updated: {DateTime.Now:HH:mm:ss}";
        }
        catch (OperationCanceledException)
        {
            // Cancelled - ignore
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Failed to refresh provider health", ex);
        }
    }

    private async Task LoadStreamingProvidersAsync(CancellationToken ct)
    {
        _streamingProviders.Clear();

        // Get provider catalog
        var providers = await _statusService.GetAvailableProvidersAsync(ct);
        var streamingProviders = providers.Where(p =>
            p.ProviderType == "Streaming" || p.ProviderType == "Hybrid").ToList();

        // Get current connection status
        var connectionState = _connectionService.State;
        var currentProvider = _connectionService.CurrentProvider;
        var latency = _connectionService.LastLatencyMs;
        var uptime = _connectionService.Uptime;

        foreach (var provider in streamingProviders)
        {
            var isActive = provider.ProviderId.Equals(currentProvider, StringComparison.OrdinalIgnoreCase);
            var isConnected = isActive && connectionState == ConnectionState.Connected;
            var isReconnecting = isActive && connectionState == ConnectionState.Reconnecting;

            _streamingProviders.Add(new ProviderStatusModel
            {
                ProviderId = provider.ProviderId,
                Name = provider.DisplayName,
                StatusText = isConnected ? "Connected" :
                            isReconnecting ? "Reconnecting..." :
                            isActive ? "Disconnected" : "Not Active",
                StatusColor = new SolidColorBrush(
                    isConnected ? Color.FromRgb(63, 185, 80) :
                    isReconnecting ? Color.FromRgb(255, 193, 7) :
                    Color.FromRgb(139, 148, 158)),
                LatencyText = isConnected ? $"Latency: {latency:F0}ms" : "Latency: --",
                UptimeText = isConnected && uptime.HasValue ?
                    $"Uptime: {FormatUptime(uptime.Value)}" : "Uptime: --",
                ActionText = isConnected ? "Disconnect" : "Connect",
                IsConnected = isConnected
            });
        }

        // If no streaming providers found, add defaults
        if (_streamingProviders.Count == 0)
        {
            _streamingProviders.Add(CreateDefaultProvider("alpaca", "Alpaca Markets"));
            _streamingProviders.Add(CreateDefaultProvider("ib", "Interactive Brokers"));
            _streamingProviders.Add(CreateDefaultProvider("polygon", "Polygon.io"));
        }
    }

    private ProviderStatusModel CreateDefaultProvider(string id, string name) => new()
    {
        ProviderId = id,
        Name = name,
        StatusText = "Not Configured",
        StatusColor = new SolidColorBrush(Color.FromRgb(139, 148, 158)),
        LatencyText = "Latency: --",
        UptimeText = "Uptime: --",
        ActionText = "Configure",
        IsConnected = false
    };

    private async Task LoadBackfillProvidersAsync(CancellationToken ct)
    {
        _backfillProviders.Clear();

        // Get provider catalog
        var providers = await _statusService.GetAvailableProvidersAsync(ct);
        var backfillProviders = providers.Where(p =>
            p.ProviderType == "Backfill" || p.ProviderType == "Hybrid").ToList();

        foreach (var provider in backfillProviders)
        {
            var hasCredentials = !provider.RequiresCredentials ||
                CheckCredentialsConfigured(provider.ProviderId);

            _backfillProviders.Add(new BackfillProviderModel
            {
                ProviderId = provider.ProviderId,
                Name = provider.DisplayName,
                StatusText = hasCredentials ? "Available" : "Not Configured",
                StatusColor = new SolidColorBrush(
                    hasCredentials ? Color.FromRgb(63, 185, 80) : Color.FromRgb(139, 148, 158)),
                RateLimitText = GetRateLimitText(provider.ProviderId),
                LastUsedText = "Last used: --"
            });
        }

        // If no backfill providers found, add defaults
        if (_backfillProviders.Count == 0)
        {
            _backfillProviders.Add(new BackfillProviderModel
            {
                ProviderId = "stooq",
                Name = "Stooq",
                StatusText = "Available",
                StatusColor = new SolidColorBrush(Color.FromRgb(63, 185, 80)),
                RateLimitText = "30 req/min",
                LastUsedText = "Last used: --"
            });
            _backfillProviders.Add(new BackfillProviderModel
            {
                ProviderId = "yahoo",
                Name = "Yahoo Finance",
                StatusText = "Available",
                StatusColor = new SolidColorBrush(Color.FromRgb(63, 185, 80)),
                RateLimitText = "100 req/min",
                LastUsedText = "Last used: --"
            });
        }
    }

    private static bool CheckCredentialsConfigured(string providerId)
    {
        var envVarName = providerId.ToUpperInvariant() switch
        {
            "ALPACA" => "ALPACA__KEYID",
            "POLYGON" => "POLYGON__APIKEY",
            "TIINGO" => "TIINGO__TOKEN",
            "FINNHUB" => "FINNHUB__APIKEY",
            "ALPHAVANTAGE" => "ALPHAVANTAGE__APIKEY",
            _ => null
        };

        if (envVarName == null) return true;
        return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(envVarName));
    }

    private static string GetRateLimitText(string providerId)
    {
        return providerId.ToLowerInvariant() switch
        {
            "alpaca" => "200 req/min",
            "polygon" => "5 req/min (free)",
            "tiingo" => "500 req/hour",
            "finnhub" => "60 req/min",
            "alphavantage" => "5 req/min",
            "stooq" => "30 req/min",
            "yahoo" => "100 req/min",
            _ => "Unknown"
        };
    }

    private void UpdateSummaryStats()
    {
        var connected = _streamingProviders.Count(p => p.IsConnected);
        var disconnected = _streamingProviders.Count(p => !p.IsConnected);
        var totalProviders = _streamingProviders.Count + _backfillProviders.Count;

        ConnectedCountText.Text = connected.ToString();
        DisconnectedCountText.Text = disconnected.ToString();
        TotalProvidersText.Text = totalProviders.ToString();

        if (_connectionService.State == ConnectionState.Connected)
        {
            AvgLatencyText.Text = $"{_connectionService.LastLatencyMs:F0}";
        }
        else
        {
            AvgLatencyText.Text = "--";
        }
    }

    private void AddConnectionEvent(string message, string provider, EventType eventType)
    {
        _connectionHistory.Insert(0, new ConnectionEventModel
        {
            Message = message,
            Provider = provider,
            Timestamp = DateTime.Now,
            TimeText = "Just now",
            EventColor = new SolidColorBrush(eventType switch
            {
                EventType.Success => Color.FromRgb(63, 185, 80),
                EventType.Warning => Color.FromRgb(255, 193, 7),
                EventType.Error => Color.FromRgb(244, 67, 54),
                _ => Color.FromRgb(139, 148, 158)
            })
        });

        // Keep only last 50 events
        while (_connectionHistory.Count > 50)
        {
            _connectionHistory.RemoveAt(_connectionHistory.Count - 1);
        }

        NoHistoryText.Visibility = _connectionHistory.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        // Update time text for existing events
        foreach (var evt in _connectionHistory)
        {
            evt.TimeText = FormatTimeAgo(evt.Timestamp);
        }
    }

    private static string FormatUptime(TimeSpan uptime)
    {
        if (uptime.TotalMinutes < 1) return "< 1m";
        if (uptime.TotalHours < 1) return $"{(int)uptime.TotalMinutes}m";
        if (uptime.TotalDays < 1) return $"{(int)uptime.TotalHours}h {uptime.Minutes}m";
        return $"{(int)uptime.TotalDays}d {uptime.Hours}h";
    }

    private static string FormatTimeAgo(DateTime timestamp)
    {
        var diff = DateTime.Now - timestamp;
        if (diff.TotalSeconds < 60) return "Just now";
        if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
        if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
        return timestamp.ToString("MMM d HH:mm");
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        await RefreshDataAsync();
        _notificationService.ShowNotification(
            "Refreshed",
            "Provider health data has been refreshed.",
            NotificationType.Info);
    }

    private async void ProviderAction_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string providerId) return;

        var provider = _streamingProviders.FirstOrDefault(p => p.ProviderId == providerId);
        if (provider == null) return;

        if (provider.IsConnected)
        {
            // Disconnect
            await _connectionService.DisconnectAsync();
            AddConnectionEvent("Disconnected by user", provider.Name, EventType.Info);
            _notificationService.ShowNotification(
                "Disconnected",
                $"Disconnected from {provider.Name}.",
                NotificationType.Info);
        }
        else
        {
            // Connect
            await _connectionService.ConnectAsync(providerId);
            AddConnectionEvent("Connected by user", provider.Name, EventType.Success);
            _notificationService.ShowNotification(
                "Connected",
                $"Connected to {provider.Name}.",
                NotificationType.Success);
        }

        await RefreshDataAsync();
    }

    private void ProviderDetails_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string providerId) return;

        var provider = _streamingProviders.FirstOrDefault(p => p.ProviderId == providerId);
        if (provider == null) return;

        var details = $"Provider: {provider.Name}\n" +
                     $"Status: {provider.StatusText}\n" +
                     $"{provider.LatencyText}\n" +
                     $"{provider.UptimeText}\n" +
                     $"Reconnects: {_connectionService.TotalReconnects}";

        MessageBox.Show(details, $"{provider.Name} Details", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ClearHistory_Click(object sender, RoutedEventArgs e)
    {
        _connectionHistory.Clear();
        NoHistoryText.Visibility = Visibility.Visible;

        _notificationService.ShowNotification(
            "History Cleared",
            "Connection history has been cleared.",
            NotificationType.Info);
    }
}

/// <summary>
/// Model for streaming provider status display.
/// </summary>
public class ProviderStatusModel
{
    public string ProviderId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string StatusText { get; set; } = string.Empty;
    public SolidColorBrush StatusColor { get; set; } = new(Colors.Gray);
    public string LatencyText { get; set; } = string.Empty;
    public string UptimeText { get; set; } = string.Empty;
    public string ActionText { get; set; } = string.Empty;
    public bool IsConnected { get; set; }
}

/// <summary>
/// Model for backfill provider status display.
/// </summary>
public class BackfillProviderModel
{
    public string ProviderId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string StatusText { get; set; } = string.Empty;
    public SolidColorBrush StatusColor { get; set; } = new(Colors.Gray);
    public string RateLimitText { get; set; } = string.Empty;
    public string LastUsedText { get; set; } = string.Empty;
}

/// <summary>
/// Model for connection history events.
/// </summary>
public class ConnectionEventModel
{
    public string Message { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string TimeText { get; set; } = string.Empty;
    public SolidColorBrush EventColor { get; set; } = new(Colors.Gray);
}

/// <summary>
/// Event type for connection history.
/// </summary>
public enum EventType
{
    Info,
    Success,
    Warning,
    Error
}
