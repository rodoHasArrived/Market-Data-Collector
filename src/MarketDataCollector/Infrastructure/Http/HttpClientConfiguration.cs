using System.Net.Http.Headers;
using MarketDataCollector.Infrastructure.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Extensions.Http;

namespace MarketDataCollector.Infrastructure.Http;

/// <summary>
/// Named HttpClient identifiers for IHttpClientFactory.
/// Using constants ensures consistency across the codebase.
/// </summary>
/// <remarks>
/// Implements TD-10: Replace instance HttpClient with IHttpClientFactory.
/// </remarks>
public static class HttpClientNames
{
    // Streaming/Trading providers
    public const string Alpaca = "alpaca";
    public const string AlpacaData = "alpaca-data";
    public const string Polygon = "polygon";
    public const string NYSE = "nyse";

    // Backfill providers
    public const string AlpacaHistorical = "alpaca-historical";
    public const string PolygonHistorical = "polygon-historical";
    public const string TiingoHistorical = "tiingo-historical";
    public const string YahooFinanceHistorical = "yahoo-finance-historical";
    public const string StooqHistorical = "stooq-historical";
    public const string FinnhubHistorical = "finnhub-historical";
    public const string AlphaVantageHistorical = "alpha-vantage-historical";
    public const string NasdaqDataLinkHistorical = "nasdaq-data-link-historical";

    // Symbol search providers
    public const string AlpacaSymbolSearch = "alpaca-symbol-search";
    public const string PolygonSymbolSearch = "polygon-symbol-search";
    public const string FinnhubSymbolSearch = "finnhub-symbol-search";
    public const string OpenFigi = "openfigi";

    // Application services
    public const string CredentialValidation = "credential-validation";
    public const string ConnectivityTest = "connectivity-test";
    public const string DailySummaryWebhook = "daily-summary-webhook";
    public const string OAuthTokenRefresh = "oauth-token-refresh";
    public const string CredentialTesting = "credential-testing";
    public const string PortfolioImport = "portfolio-import";
    public const string DryRun = "dry-run";
    public const string PreflightChecker = "preflight-checker";

    // Default client for general purpose
    public const string Default = "default";
}

/// <summary>
/// Extension methods for configuring HttpClient instances via IHttpClientFactory.
/// </summary>
/// <remarks>
/// Implements TD-10: Replace instance HttpClient with IHttpClientFactory.
/// Benefits:
/// - Proper connection pooling and DNS refresh
/// - Prevents socket exhaustion
/// - Centralized configuration for timeouts, headers, retry policies
/// - Better testability through DI
/// </remarks>
[ImplementsAdr("ADR-010", "HttpClientFactory for proper HTTP client lifecycle management")]
public static class HttpClientConfiguration
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ShortTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan LongTimeout = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Registers all named HttpClient configurations with the DI container.
    /// </summary>
    public static IServiceCollection AddMarketDataHttpClients(this IServiceCollection services)
    {
        // Default client
        services.AddHttpClient(HttpClientNames.Default)
            .ConfigureHttpClient(client =>
            {
                client.Timeout = DefaultTimeout;
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            })
            .AddStandardResiliencePolicy();

        // Alpaca Trading API client
        services.AddHttpClient(HttpClientNames.Alpaca)
            .ConfigureHttpClient(client =>
            {
                client.Timeout = DefaultTimeout;
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            })
            .AddStandardResiliencePolicy();

        // Alpaca Data API client
        services.AddHttpClient(HttpClientNames.AlpacaData)
            .ConfigureHttpClient(client =>
            {
                client.BaseAddress = new Uri("https://data.alpaca.markets/v2/stocks/");
                client.Timeout = DefaultTimeout;
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            })
            .AddStandardResiliencePolicy();

        // Alpaca Historical Data client
        services.AddHttpClient(HttpClientNames.AlpacaHistorical)
            .ConfigureHttpClient(client =>
            {
                client.BaseAddress = new Uri("https://data.alpaca.markets/v2/stocks/");
                client.Timeout = DefaultTimeout;
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            })
            .AddStandardResiliencePolicy();

        // Alpaca Symbol Search client
        services.AddHttpClient(HttpClientNames.AlpacaSymbolSearch)
            .ConfigureHttpClient(client =>
            {
                client.BaseAddress = new Uri("https://api.alpaca.markets/v2/");
                client.Timeout = DefaultTimeout;
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            })
            .AddStandardResiliencePolicy();

        // Polygon clients
        services.AddHttpClient(HttpClientNames.Polygon)
            .ConfigureHttpClient(client =>
            {
                client.BaseAddress = new Uri("https://api.polygon.io/");
                client.Timeout = DefaultTimeout;
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            })
            .AddStandardResiliencePolicy();

        services.AddHttpClient(HttpClientNames.PolygonHistorical)
            .ConfigureHttpClient(client =>
            {
                client.BaseAddress = new Uri("https://api.polygon.io/");
                client.Timeout = DefaultTimeout;
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            })
            .AddStandardResiliencePolicy();

        services.AddHttpClient(HttpClientNames.PolygonSymbolSearch)
            .ConfigureHttpClient(client =>
            {
                client.BaseAddress = new Uri("https://api.polygon.io/");
                client.Timeout = DefaultTimeout;
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            })
            .AddStandardResiliencePolicy();

        // Tiingo Historical client
        services.AddHttpClient(HttpClientNames.TiingoHistorical)
            .ConfigureHttpClient(client =>
            {
                client.BaseAddress = new Uri("https://api.tiingo.com/");
                client.Timeout = DefaultTimeout;
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            })
            .AddStandardResiliencePolicy();

        // Yahoo Finance Historical client
        services.AddHttpClient(HttpClientNames.YahooFinanceHistorical)
            .ConfigureHttpClient(client =>
            {
                client.BaseAddress = new Uri("https://query1.finance.yahoo.com/");
                client.Timeout = LongTimeout;
                client.DefaultRequestHeaders.Add("User-Agent", "MarketDataCollector/1.0");
            })
            .AddStandardResiliencePolicy();

        // Stooq Historical client
        services.AddHttpClient(HttpClientNames.StooqHistorical)
            .ConfigureHttpClient(client =>
            {
                client.BaseAddress = new Uri("https://stooq.com/");
                client.Timeout = DefaultTimeout;
            })
            .AddStandardResiliencePolicy();

        // Finnhub clients
        services.AddHttpClient(HttpClientNames.FinnhubHistorical)
            .ConfigureHttpClient(client =>
            {
                client.BaseAddress = new Uri("https://finnhub.io/api/v1/");
                client.Timeout = DefaultTimeout;
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            })
            .AddStandardResiliencePolicy();

        services.AddHttpClient(HttpClientNames.FinnhubSymbolSearch)
            .ConfigureHttpClient(client =>
            {
                client.BaseAddress = new Uri("https://finnhub.io/api/v1/");
                client.Timeout = DefaultTimeout;
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            })
            .AddStandardResiliencePolicy();

        // Alpha Vantage Historical client
        services.AddHttpClient(HttpClientNames.AlphaVantageHistorical)
            .ConfigureHttpClient(client =>
            {
                client.BaseAddress = new Uri("https://www.alphavantage.co/");
                client.Timeout = DefaultTimeout;
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            })
            .AddStandardResiliencePolicy();

        // Nasdaq Data Link Historical client
        services.AddHttpClient(HttpClientNames.NasdaqDataLinkHistorical)
            .ConfigureHttpClient(client =>
            {
                client.BaseAddress = new Uri("https://data.nasdaq.com/api/v3/");
                client.Timeout = DefaultTimeout;
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            })
            .AddStandardResiliencePolicy();

        // OpenFIGI client
        services.AddHttpClient(HttpClientNames.OpenFigi)
            .ConfigureHttpClient(client =>
            {
                client.BaseAddress = new Uri("https://api.openfigi.com/v3/");
                client.Timeout = DefaultTimeout;
                client.DefaultRequestHeaders.Add("Accept", "application/json");
            })
            .AddStandardResiliencePolicy();

        // NYSE client
        services.AddHttpClient(HttpClientNames.NYSE)
            .ConfigureHttpClient(client =>
            {
                client.Timeout = DefaultTimeout;
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            })
            .AddStandardResiliencePolicy();

        // Credential validation client (short timeout)
        services.AddHttpClient(HttpClientNames.CredentialValidation)
            .ConfigureHttpClient(client =>
            {
                client.Timeout = ShortTimeout;
            })
            .AddStandardResiliencePolicy();

        // Connectivity test client (short timeout)
        services.AddHttpClient(HttpClientNames.ConnectivityTest)
            .ConfigureHttpClient(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(15);
            })
            .AddStandardResiliencePolicy();

        // Daily summary webhook client
        services.AddHttpClient(HttpClientNames.DailySummaryWebhook)
            .ConfigureHttpClient(client =>
            {
                client.Timeout = ShortTimeout;
            })
            .AddStandardResiliencePolicy();

        // OAuth token refresh client
        services.AddHttpClient(HttpClientNames.OAuthTokenRefresh)
            .ConfigureHttpClient(client =>
            {
                client.Timeout = DefaultTimeout;
                client.DefaultRequestHeaders.Add("User-Agent", "MarketDataCollector/1.6.1");
            })
            .AddStandardResiliencePolicy();

        // Credential testing client
        services.AddHttpClient(HttpClientNames.CredentialTesting)
            .ConfigureHttpClient(client =>
            {
                client.Timeout = ShortTimeout;
            })
            .AddStandardResiliencePolicy();

        // Portfolio import client
        services.AddHttpClient(HttpClientNames.PortfolioImport)
            .ConfigureHttpClient(client =>
            {
                client.Timeout = DefaultTimeout;
            })
            .AddStandardResiliencePolicy();

        // Dry run client
        services.AddHttpClient(HttpClientNames.DryRun)
            .ConfigureHttpClient(client =>
            {
                client.Timeout = ShortTimeout;
            })
            .AddStandardResiliencePolicy();

        // Preflight checker client
        services.AddHttpClient(HttpClientNames.PreflightChecker)
            .ConfigureHttpClient(client =>
            {
                client.Timeout = ShortTimeout;
            })
            .AddStandardResiliencePolicy();

        return services;
    }

    /// <summary>
    /// Adds standard resilience policies (retry with exponential backoff, circuit breaker).
    /// </summary>
    private static IHttpClientBuilder AddStandardResiliencePolicy(this IHttpClientBuilder builder)
    {
        return builder
            .AddPolicyHandler(GetRetryPolicy())
            .AddPolicyHandler(GetCircuitBreakerPolicy());
    }

    /// <summary>
    /// Creates a retry policy with exponential backoff for transient HTTP errors.
    /// </summary>
    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (outcome, timespan, retryAttempt, _) =>
                {
                    // Log retry attempts (can be enhanced with ILogger if needed)
                    System.Diagnostics.Debug.WriteLine(
                        $"Retry {retryAttempt} after {timespan.TotalSeconds}s due to {outcome.Exception?.Message ?? outcome.Result?.StatusCode.ToString()}");
                });
    }

    /// <summary>
    /// Creates a circuit breaker policy to prevent cascading failures.
    /// </summary>
    private static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromSeconds(30));
    }
}

/// <summary>
/// Static factory for creating HttpClient instances from IHttpClientFactory.
/// Provides backward-compatible static access for services not yet fully converted to DI.
/// </summary>
/// <remarks>
/// This is a transitional pattern. New code should inject IHttpClientFactory directly.
/// </remarks>
public static class HttpClientFactoryProvider
{
    private static IHttpClientFactory? _factory;
    private static IServiceProvider? _serviceProvider;

    /// <summary>
    /// Initializes the provider with the service provider.
    /// Call this during application startup after ConfigureServices.
    /// </summary>
    public static void Initialize(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _factory = serviceProvider.GetService<IHttpClientFactory>();
    }

    /// <summary>
    /// Gets an HttpClient for the specified named client.
    /// Falls back to creating a new HttpClient if factory is not initialized.
    /// </summary>
    public static HttpClient CreateClient(string name)
    {
        if (_factory != null)
        {
            return _factory.CreateClient(name);
        }

        // Fallback for non-DI scenarios (e.g., CLI tools, tests)
        // This maintains backward compatibility during transition
        return new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    /// <summary>
    /// Gets an HttpClient for the specified named client with header configuration.
    /// </summary>
    public static HttpClient CreateClient(string name, Action<HttpClient> configure)
    {
        var client = CreateClient(name);
        configure(client);
        return client;
    }

    /// <summary>
    /// Checks if the factory has been initialized.
    /// </summary>
    public static bool IsInitialized => _factory != null;
}
