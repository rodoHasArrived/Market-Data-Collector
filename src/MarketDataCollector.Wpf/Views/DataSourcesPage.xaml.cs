using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MarketDataCollector.Wpf.Services;

namespace MarketDataCollector.Wpf.Views;

public partial class DataSourcesPage : Page
{
    private readonly ObservableCollection<ProviderCatalogItem> _providers = new();
    private CancellationTokenSource? _cts;

    public DataSourcesPage()
    {
        InitializeComponent();
        ProvidersList.ItemsSource = _providers;
    }

    private async void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        await RefreshAsync();
    }

    private void OnPageUnloaded(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();

        try
        {
            var catalog = await StatusService.Instance.GetAvailableProvidersAsync(_cts.Token);
            var status = await StatusService.Instance.GetProviderStatusAsync(_cts.Token);

            var providers = catalog.ToList();
            if (providers.Count == 0)
            {
                providers = GetDemoProviders();
            }

            UpdateProviderList(providers, status);
            UpdateSummary(providers);

            LastUpdatedText.Text = $"Last updated: {DateTime.Now:HH:mm:ss}";
        }
        catch (OperationCanceledException)
        {
            // ignore
        }
        catch (Exception ex)
        {
            LoggingService.Instance.LogError("Failed to refresh data sources", ex);
        }
    }

    private void UpdateProviderList(IReadOnlyList<ProviderInfo> providers, ProviderStatusInfo? status)
    {
        _providers.Clear();

        var activeProvider = status?.ActiveProvider ?? string.Empty;
        var isConnected = status?.IsConnected == true;

        foreach (var provider in providers)
        {
            var isActive = !string.IsNullOrWhiteSpace(activeProvider) &&
                           provider.ProviderId.Equals(activeProvider, StringComparison.OrdinalIgnoreCase);

            var statusText = isActive
                ? (isConnected ? "Connected" : "Disconnected")
                : "Idle";

            _providers.Add(new ProviderCatalogItem
            {
                ProviderId = provider.ProviderId,
                DisplayName = provider.DisplayName,
                ProviderType = provider.ProviderType,
                Description = provider.Description,
                CredentialsText = provider.RequiresCredentials ? "Credentials Required" : "No Credentials",
                StatusText = statusText,
                StatusBrush = new SolidColorBrush(isActive
                    ? (isConnected ? Color.FromRgb(63, 185, 80) : Color.FromRgb(244, 67, 54))
                    : Color.FromRgb(139, 148, 158))
            });
        }

        ProviderCountText.Text = $"{_providers.Count} provider{(_providers.Count == 1 ? "" : "s")}";
        NoProvidersText.Visibility = _providers.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateSummary(IReadOnlyList<ProviderInfo> providers)
    {
        StreamingCountText.Text = providers.Count(p => p.ProviderType == "Streaming").ToString();
        BackfillCountText.Text = providers.Count(p => p.ProviderType == "Backfill").ToString();
        HybridCountText.Text = providers.Count(p => p.ProviderType == "Hybrid").ToString();
    }

    private static List<ProviderInfo> GetDemoProviders()
    {
        return new List<ProviderInfo>
        {
            new()
            {
                ProviderId = "alpaca",
                DisplayName = "Alpaca Markets",
                ProviderType = "Streaming",
                Description = "Commission-free stock and crypto data feed.",
                RequiresCredentials = true
            },
            new()
            {
                ProviderId = "polygon",
                DisplayName = "Polygon.io",
                ProviderType = "Hybrid",
                Description = "Realtime equities with robust backfill support.",
                RequiresCredentials = true
            },
            new()
            {
                ProviderId = "yahoo",
                DisplayName = "Yahoo Finance",
                ProviderType = "Backfill",
                Description = "Free historical EOD data source.",
                RequiresCredentials = false
            }
        };
    }

    private sealed class ProviderCatalogItem
    {
        public string ProviderId { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public string ProviderType { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public string CredentialsText { get; init; } = string.Empty;
        public string StatusText { get; init; } = string.Empty;
        public Brush StatusBrush { get; init; } = new SolidColorBrush(Color.FromRgb(139, 148, 158));
    }
}
