using MarketDataCollector.Providers.FreeData.Stooq;
using MarketDataCollector.Providers.FreeData.Tiingo;
using MarketDataCollector.Providers.FreeData.YahooFinance;
using MarketDataCollector.ProviderSdk;
using MarketDataCollector.ProviderSdk.Providers;
using Microsoft.Extensions.DependencyInjection;

namespace MarketDataCollector.Providers.FreeData;

/// <summary>
/// Plugin entry point for free historical data providers.
/// Registers Stooq, Tiingo, and Yahoo Finance providers that require
/// no paid subscription for basic daily OHLCV data.
/// </summary>
public sealed class FreeDataPlugin : IProviderPlugin
{
    public ProviderPluginInfo Info { get; } = new(
        PluginId: "free-data",
        DisplayName: "Free Data Providers",
        Version: "1.0.0",
        Description: "Historical data from free APIs: Stooq, Tiingo, Yahoo Finance",
        Author: "Market Data Collector");

    public void Register(IProviderRegistration registration)
    {
        // Register all free data historical providers
        registration.AddHistoricalProvider<StooqProvider>();
        registration.AddHistoricalProvider<TiingoProvider>();
        registration.AddHistoricalProvider<YahooFinanceProvider>();

        // Register named HTTP clients for each provider
        registration.AddServices(services =>
        {
            services.AddHttpClient("stooq-historical", client =>
            {
                client.DefaultRequestHeaders.Add("User-Agent", "MarketDataCollector/1.0");
                client.Timeout = TimeSpan.FromSeconds(30);
            });

            services.AddHttpClient("tiingo-historical", client =>
            {
                client.DefaultRequestHeaders.Add("Content-Type", "application/json");
                client.Timeout = TimeSpan.FromSeconds(30);
            });

            services.AddHttpClient("yahoo-historical", client =>
            {
                client.DefaultRequestHeaders.Add("User-Agent", "MarketDataCollector/1.0");
                client.Timeout = TimeSpan.FromSeconds(30);
            });
        });

        // Declare credentials (only Tiingo requires one)
        registration.DeclareCredentials(
            new ProviderCredentialField(
                Name: "TiingoToken",
                EnvironmentVariable: "TIINGO_API_TOKEN",
                DisplayName: "Tiingo API Token",
                Required: false,
                IsSensitive: true));
    }
}
