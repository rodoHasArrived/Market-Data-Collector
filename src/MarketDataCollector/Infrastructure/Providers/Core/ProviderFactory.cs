using MarketDataCollector.Application.Config;
using MarketDataCollector.Application.Logging;
using MarketDataCollector.Infrastructure.Contracts;
using MarketDataCollector.Infrastructure.Providers.Alpaca;
using MarketDataCollector.Infrastructure.Providers.Backfill;
using MarketDataCollector.Infrastructure.Providers.InteractiveBrokers;
using MarketDataCollector.Infrastructure.Providers.Polygon;
using MarketDataCollector.Infrastructure.Providers.StockSharp;
using MarketDataCollector.Infrastructure.Providers.SymbolSearch;
using Serilog;

// Type aliases for clarity when dealing with backfill provider configs
using YahooBackfillConfig = MarketDataCollector.Application.Config.YahooFinanceConfig;
using PolygonBackfillConfig = MarketDataCollector.Application.Config.PolygonConfig;
using TiingoBackfillConfig = MarketDataCollector.Application.Config.TiingoConfig;
using FinnhubBackfillConfig = MarketDataCollector.Application.Config.FinnhubConfig;
using StooqBackfillConfig = MarketDataCollector.Application.Config.StooqConfig;
using AlphaVantageBackfillConfig = MarketDataCollector.Application.Config.AlphaVantageConfig;
using NasdaqBackfillConfig = MarketDataCollector.Application.Config.NasdaqDataLinkConfig;
using SymbolResolution = MarketDataCollector.Infrastructure.Providers.Backfill.SymbolResolution;

namespace MarketDataCollector.Infrastructure.Providers.Core;

/// <summary>
/// Unified factory for creating and registering all provider types (streaming, backfill, symbol search).
/// This replaces scattered provider creation logic with a single entry point.
/// </summary>
/// <remarks>
/// The factory uses capability-driven registration where all providers implement
/// <see cref="IProviderMetadata"/> and are registered in a unified <see cref="ProviderRegistry"/>.
/// </remarks>
[ImplementsAdr("ADR-001", "Unified provider factory for capability-driven registration")]
public sealed class ProviderFactory
{
    private readonly AppConfig _config;
    private readonly ICredentialResolver _credentialResolver;
    private readonly ILogger _log;

    public ProviderFactory(
        AppConfig config,
        ICredentialResolver credentialResolver,
        ILogger? log = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _credentialResolver = credentialResolver ?? throw new ArgumentNullException(nameof(credentialResolver));
        _log = log ?? LoggingSetup.ForContext<ProviderFactory>();
    }

    /// <summary>
    /// Creates all configured providers and registers them with the provided registry.
    /// This is the main entry point for provider initialization.
    /// </summary>
    /// <param name="registry">The registry to register providers with.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Summary of created providers.</returns>
    public async Task<ProviderCreationResult> CreateAndRegisterAllAsync(
        ProviderRegistry registry,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(registry);

        var result = new ProviderCreationResult();

        // Create and register streaming providers
        var streamingProviders = await CreateStreamingProvidersAsync(ct);
        foreach (var provider in streamingProviders)
        {
            registry.Register(provider);
            result.StreamingProviders.Add(provider.ProviderId);
        }

        // Create and register backfill providers
        var backfillProviders = CreateBackfillProviders();
        foreach (var provider in backfillProviders)
        {
            registry.Register(provider);
            result.BackfillProviders.Add(provider.ProviderId);
        }

        // Create and register symbol search providers
        var searchProviders = CreateSymbolSearchProviders();
        foreach (var provider in searchProviders)
        {
            registry.Register(provider);
            result.SymbolSearchProviders.Add(provider.ProviderId);
        }

        _log.Information(
            "Provider factory created {StreamingCount} streaming, {BackfillCount} backfill, {SearchCount} search providers",
            result.StreamingProviders.Count,
            result.BackfillProviders.Count,
            result.SymbolSearchProviders.Count);

        return result;
    }

    /// <summary>
    /// Creates streaming provider based on the configured data source.
    /// </summary>
    public async Task<IReadOnlyList<IMarketDataClient>> CreateStreamingProvidersAsync(CancellationToken ct = default)
    {
        var providers = new List<IMarketDataClient>();

        // Create primary streaming provider based on configuration
        var primaryProvider = await CreatePrimaryStreamingProviderAsync(ct);
        if (primaryProvider != null)
        {
            providers.Add(primaryProvider);
        }

        return providers;
    }

    /// <summary>
    /// Creates the primary streaming provider based on DataSource configuration.
    /// </summary>
    private Task<IMarketDataClient?> CreatePrimaryStreamingProviderAsync(CancellationToken ct)
    {
        IMarketDataClient? client = _config.DataSource switch
        {
            DataSourceKind.Alpaca => CreateAlpacaStreamingClient(),
            DataSourceKind.Polygon => CreatePolygonStreamingClient(),
            DataSourceKind.StockSharp => CreateStockSharpStreamingClient(),
            DataSourceKind.IB or DataSourceKind.None => CreateIBStreamingClient(),
            _ => CreateIBStreamingClient()
        };

        return Task.FromResult(client);
    }

    private IMarketDataClient? CreateAlpacaStreamingClient()
    {
        var (keyId, secretKey) = _credentialResolver.ResolveAlpacaCredentials(
            _config.Alpaca?.KeyId,
            _config.Alpaca?.SecretKey);

        if (string.IsNullOrEmpty(keyId) || string.IsNullOrEmpty(secretKey))
        {
            _log.Warning("Alpaca credentials not configured, skipping Alpaca streaming provider");
            return null;
        }

        return new AlpacaMarketDataClient(
            tradeCollector: null!, // Will be set during initialization
            quoteCollector: null!, // Will be set during initialization
            opt: _config.Alpaca! with { KeyId = keyId, SecretKey = secretKey });
    }

    private IMarketDataClient CreatePolygonStreamingClient()
    {
        return new PolygonMarketDataClient(
            publisher: null!, // Will be set during initialization
            tradeCollector: null!,
            quoteCollector: null!);
    }

    private IMarketDataClient CreateStockSharpStreamingClient()
    {
        return new StockSharpMarketDataClient(
            tradeCollector: null!,
            depthCollector: null!,
            quoteCollector: null!,
            config: _config.StockSharp ?? new StockSharpConfig());
    }

    private IMarketDataClient CreateIBStreamingClient()
    {
        return new IBMarketDataClient(
            publisher: null!,
            tradeCollector: null!,
            depthCollector: null!);
    }

    /// <summary>
    /// Creates all configured backfill providers.
    /// </summary>
    public IReadOnlyList<IHistoricalDataProvider> CreateBackfillProviders()
    {
        var providers = new List<IHistoricalDataProvider>();
        var backfillCfg = _config.Backfill;
        var providersCfg = backfillCfg?.Providers;

        // Alpaca Markets (highest priority when configured)
        TryAddBackfillProvider(providers, () => CreateAlpacaBackfillProvider(providersCfg?.Alpaca));

        // Yahoo Finance (broad free coverage)
        TryAddBackfillProvider(providers, () => CreateYahooBackfillProvider(providersCfg?.Yahoo));

        // Polygon.io
        TryAddBackfillProvider(providers, () => CreatePolygonBackfillProvider(providersCfg?.Polygon));

        // Tiingo
        TryAddBackfillProvider(providers, () => CreateTiingoBackfillProvider(providersCfg?.Tiingo));

        // Finnhub
        TryAddBackfillProvider(providers, () => CreateFinnhubBackfillProvider(providersCfg?.Finnhub));

        // Stooq
        TryAddBackfillProvider(providers, () => CreateStooqBackfillProvider(providersCfg?.Stooq));

        // Alpha Vantage
        TryAddBackfillProvider(providers, () => CreateAlphaVantageBackfillProvider(providersCfg?.AlphaVantage));

        // Nasdaq Data Link
        TryAddBackfillProvider(providers, () => CreateNasdaqBackfillProvider(providersCfg?.Nasdaq));

        return providers
            .OrderBy(p => p.Priority)
            .ToList();
    }

    private void TryAddBackfillProvider(
        List<IHistoricalDataProvider> providers,
        Func<IHistoricalDataProvider?> factory)
    {
        try
        {
            var provider = factory();
            if (provider != null)
            {
                providers.Add(provider);
            }
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "Failed to create backfill provider");
        }
    }

    private IHistoricalDataProvider? CreateAlpacaBackfillProvider(AlpacaBackfillConfig? cfg)
    {
        if (!(cfg?.Enabled ?? true)) return null;

        var (keyId, secretKey) = _credentialResolver.ResolveAlpacaCredentials(cfg?.KeyId, cfg?.SecretKey);
        if (string.IsNullOrEmpty(keyId) || string.IsNullOrEmpty(secretKey)) return null;

        return new AlpacaHistoricalDataProvider(
            keyId: keyId,
            secretKey: secretKey,
            feed: cfg?.Feed ?? "iex",
            adjustment: cfg?.Adjustment ?? "all",
            priority: cfg?.Priority ?? 5,
            rateLimitPerMinute: cfg?.RateLimitPerMinute ?? 200,
            log: _log);
    }

    private IHistoricalDataProvider? CreateYahooBackfillProvider(YahooBackfillConfig? cfg)
    {
        if (!(cfg?.Enabled ?? true)) return null;
        return new YahooFinanceHistoricalDataProvider(log: _log);
    }

    private IHistoricalDataProvider? CreatePolygonBackfillProvider(PolygonBackfillConfig? cfg)
    {
        if (!(cfg?.Enabled ?? true)) return null;

        var apiKey = _credentialResolver.ResolvePolygonCredentials(cfg?.ApiKey);
        if (string.IsNullOrEmpty(apiKey)) return null;

        return new PolygonHistoricalDataProvider(apiKey: apiKey, log: _log);
    }

    private IHistoricalDataProvider? CreateTiingoBackfillProvider(TiingoBackfillConfig? cfg)
    {
        if (!(cfg?.Enabled ?? true)) return null;

        var token = _credentialResolver.ResolveTiingoCredentials(cfg?.ApiToken);
        if (string.IsNullOrEmpty(token)) return null;

        return new TiingoHistoricalDataProvider(apiToken: token, log: _log);
    }

    private IHistoricalDataProvider? CreateFinnhubBackfillProvider(FinnhubBackfillConfig? cfg)
    {
        if (!(cfg?.Enabled ?? true)) return null;

        var apiKey = _credentialResolver.ResolveFinnhubCredentials(cfg?.ApiKey);
        if (string.IsNullOrEmpty(apiKey)) return null;

        return new FinnhubHistoricalDataProvider(apiKey: apiKey, log: _log);
    }

    private IHistoricalDataProvider? CreateStooqBackfillProvider(StooqBackfillConfig? cfg)
    {
        if (!(cfg?.Enabled ?? true)) return null;
        return new StooqHistoricalDataProvider(log: _log);
    }

    private IHistoricalDataProvider? CreateAlphaVantageBackfillProvider(AlphaVantageBackfillConfig? cfg)
    {
        // Disabled by default due to very limited free tier
        if (!(cfg?.Enabled ?? false)) return null;

        var apiKey = _credentialResolver.ResolveAlphaVantageCredentials(cfg?.ApiKey);
        if (string.IsNullOrEmpty(apiKey)) return null;

        return new AlphaVantageHistoricalDataProvider(apiKey: apiKey, log: _log);
    }

    private IHistoricalDataProvider? CreateNasdaqBackfillProvider(NasdaqBackfillConfig? cfg)
    {
        if (!(cfg?.Enabled ?? true)) return null;

        var apiKey = _credentialResolver.ResolveNasdaqCredentials(cfg?.ApiKey);
        return new NasdaqDataLinkHistoricalDataProvider(
            apiKey: apiKey,
            database: cfg?.Database ?? "WIKI",
            log: _log);
    }

    /// <summary>
    /// Creates all configured symbol search providers.
    /// Symbol search uses the same credentials as backfill providers.
    /// </summary>
    public IReadOnlyList<ISymbolSearchProvider> CreateSymbolSearchProviders()
    {
        var providers = new List<ISymbolSearchProvider>();
        var backfillProviders = _config.Backfill?.Providers;

        // Alpaca Symbol Search (uses same credentials as Alpaca backfill)
        TryAddSearchProvider(providers, () => CreateAlpacaSearchProvider(backfillProviders?.Alpaca));

        // Finnhub Symbol Search (uses same credentials as Finnhub backfill)
        TryAddSearchProvider(providers, () => CreateFinnhubSearchProvider(backfillProviders?.Finnhub));

        // Polygon Symbol Search (uses same credentials as Polygon backfill)
        TryAddSearchProvider(providers, () => CreatePolygonSearchProvider(backfillProviders?.Polygon));

        return providers
            .OrderBy(p => p.Priority)
            .ToList();
    }

    private void TryAddSearchProvider(
        List<ISymbolSearchProvider> providers,
        Func<ISymbolSearchProvider?> factory)
    {
        try
        {
            var provider = factory();
            if (provider != null)
            {
                providers.Add(provider);
            }
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "Failed to create symbol search provider");
        }
    }

    private ISymbolSearchProvider? CreateAlpacaSearchProvider(AlpacaBackfillConfig? cfg)
    {
        // Enabled by default if config is null (credential-based activation)
        if (cfg != null && !cfg.Enabled) return null;

        var (keyId, secretKey) = _credentialResolver.ResolveAlpacaCredentials(cfg?.KeyId, cfg?.SecretKey);
        if (string.IsNullOrEmpty(keyId) || string.IsNullOrEmpty(secretKey)) return null;

        return new AlpacaSymbolSearchProviderRefactored(keyId, secretKey, httpClient: null, log: _log);
    }

    private ISymbolSearchProvider? CreateFinnhubSearchProvider(FinnhubBackfillConfig? cfg)
    {
        // Enabled by default if config is null (credential-based activation)
        if (cfg != null && !cfg.Enabled) return null;

        var apiKey = _credentialResolver.ResolveFinnhubCredentials(cfg?.ApiKey);
        if (string.IsNullOrEmpty(apiKey)) return null;

        return new FinnhubSymbolSearchProviderRefactored(apiKey, httpClient: null, log: _log);
    }

    private ISymbolSearchProvider? CreatePolygonSearchProvider(PolygonBackfillConfig? cfg)
    {
        // Enabled by default if config is null (credential-based activation)
        if (cfg != null && !cfg.Enabled) return null;

        var apiKey = _credentialResolver.ResolvePolygonCredentials(cfg?.ApiKey);
        if (string.IsNullOrEmpty(apiKey)) return null;

        return new PolygonSymbolSearchProvider(apiKey, httpClient: null, log: _log);
    }

    /// <summary>
    /// Creates a composite backfill provider with automatic failover.
    /// </summary>
    public CompositeHistoricalDataProvider CreateCompositeBackfillProvider(
        IReadOnlyList<IHistoricalDataProvider> providers)
    {
        var openFigiApiKey = _config.Backfill?.Providers?.OpenFigi?.ApiKey;
        var enableSymbolResolution = _config.Backfill?.EnableSymbolResolution ?? true;

        SymbolResolution.OpenFigiSymbolResolver? symbolResolver = null;
        if (enableSymbolResolution)
        {
            symbolResolver = new SymbolResolution.OpenFigiSymbolResolver(openFigiApiKey, log: _log);
        }

        return new CompositeHistoricalDataProvider(
            providers,
            symbolResolver,
            enableCrossValidation: false,
            log: _log);
    }
}

/// <summary>
/// Result of provider creation operation.
/// </summary>
public sealed class ProviderCreationResult
{
    public List<string> StreamingProviders { get; } = new();
    public List<string> BackfillProviders { get; } = new();
    public List<string> SymbolSearchProviders { get; } = new();

    public int TotalProviders =>
        StreamingProviders.Count + BackfillProviders.Count + SymbolSearchProviders.Count;

    public bool HasStreamingProviders => StreamingProviders.Count > 0;
    public bool HasBackfillProviders => BackfillProviders.Count > 0;
    public bool HasSymbolSearchProviders => SymbolSearchProviders.Count > 0;
}

/// <summary>
/// Interface for resolving provider credentials from configuration or environment.
/// </summary>
public interface ICredentialResolver
{
    (string? KeyId, string? SecretKey) ResolveAlpacaCredentials(string? configKeyId, string? configSecretKey);
    string? ResolvePolygonCredentials(string? configApiKey);
    string? ResolveTiingoCredentials(string? configToken);
    string? ResolveFinnhubCredentials(string? configApiKey);
    string? ResolveAlphaVantageCredentials(string? configApiKey);
    string? ResolveNasdaqCredentials(string? configApiKey);
}

/// <summary>
/// Credential resolver that reads from environment variables.
/// Follows the same pattern as ConfigurationService.
/// </summary>
public sealed class EnvironmentCredentialResolver : ICredentialResolver
{
    public (string? KeyId, string? SecretKey) ResolveAlpacaCredentials(string? configKeyId, string? configSecretKey)
    {
        var keyId = configKeyId ?? Environment.GetEnvironmentVariable("ALPACA__KEYID")
                                 ?? Environment.GetEnvironmentVariable("ALPACA_KEY_ID");
        var secretKey = configSecretKey ?? Environment.GetEnvironmentVariable("ALPACA__SECRETKEY")
                                        ?? Environment.GetEnvironmentVariable("ALPACA_SECRET_KEY");
        return (keyId, secretKey);
    }

    public string? ResolvePolygonCredentials(string? configApiKey)
        => configApiKey ?? Environment.GetEnvironmentVariable("POLYGON__APIKEY")
                        ?? Environment.GetEnvironmentVariable("POLYGON_API_KEY");

    public string? ResolveTiingoCredentials(string? configToken)
        => configToken ?? Environment.GetEnvironmentVariable("TIINGO__TOKEN")
                       ?? Environment.GetEnvironmentVariable("TIINGO_API_TOKEN");

    public string? ResolveFinnhubCredentials(string? configApiKey)
        => configApiKey ?? Environment.GetEnvironmentVariable("FINNHUB__APIKEY")
                        ?? Environment.GetEnvironmentVariable("FINNHUB_API_KEY");

    public string? ResolveAlphaVantageCredentials(string? configApiKey)
        => configApiKey ?? Environment.GetEnvironmentVariable("ALPHAVANTAGE__APIKEY")
                        ?? Environment.GetEnvironmentVariable("ALPHA_VANTAGE_API_KEY");

    public string? ResolveNasdaqCredentials(string? configApiKey)
        => configApiKey ?? Environment.GetEnvironmentVariable("NASDAQ__APIKEY")
                        ?? Environment.GetEnvironmentVariable("NASDAQ_DATA_LINK_API_KEY");
}
