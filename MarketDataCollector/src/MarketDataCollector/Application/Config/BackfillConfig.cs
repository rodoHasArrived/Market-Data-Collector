namespace MarketDataCollector.Application.Config;

/// <summary>
/// Configuration for historical backfill operations.
/// </summary>
/// <param name="Enabled">When true, the collector will run a backfill instead of live collection.</param>
/// <param name="Provider">Primary historical data provider (e.g. "stooq", "yahoo", "nasdaq", "composite").</param>
/// <param name="Symbols">Symbols to backfill; defaults to configured live symbols.</param>
/// <param name="From">Optional inclusive start date (UTC).</param>
/// <param name="To">Optional inclusive end date (UTC).</param>
/// <param name="Granularity">Data granularity: "daily", "hourly", "minute1", "minute5", etc.</param>
/// <param name="EnableFallback">When true and using composite provider, automatically try alternative providers on failure.</param>
/// <param name="PreferAdjustedPrices">Prefer providers that return adjusted prices (for splits/dividends).</param>
/// <param name="EnableSymbolResolution">Use OpenFIGI to normalize symbols across providers.</param>
/// <param name="ProviderPriority">Custom provider priority order for fallback (overrides defaults).</param>
/// <param name="EnableRateLimitRotation">Automatically rotate to next provider when approaching rate limits.</param>
/// <param name="RateLimitRotationThreshold">Usage threshold (0.0-1.0) at which to start rotating providers.</param>
/// <param name="SkipExistingData">Check existing archives and skip dates that already have data.</param>
/// <param name="FillGapsOnly">Only fill detected gaps in existing data (vs. full backfill).</param>
/// <param name="Jobs">Job management configuration.</param>
/// <param name="Providers">Configuration for individual data providers.</param>
public sealed record BackfillConfig(
    bool Enabled = false,
    string Provider = "composite",
    string[]? Symbols = null,
    DateOnly? From = null,
    DateOnly? To = null,
    string Granularity = "daily",
    bool EnableFallback = true,
    bool PreferAdjustedPrices = true,
    bool EnableSymbolResolution = true,
    string[]? ProviderPriority = null,
    bool EnableRateLimitRotation = true,
    double RateLimitRotationThreshold = 0.8,
    bool SkipExistingData = true,
    bool FillGapsOnly = true,
    BackfillJobsConfig? Jobs = null,
    BackfillProvidersConfig? Providers = null
);

/// <summary>
/// Configuration for backfill job management.
/// </summary>
/// <param name="PersistJobs">Persist job state to disk for resume after restart.</param>
/// <param name="JobsDirectory">Directory for job state files (relative to DataRoot).</param>
/// <param name="MaxConcurrentRequests">Maximum concurrent requests across all providers.</param>
/// <param name="MaxConcurrentPerProvider">Maximum concurrent requests per provider.</param>
/// <param name="MaxRetries">Maximum retries for failed requests.</param>
/// <param name="RetryDelaySeconds">Delay between retries in seconds.</param>
/// <param name="BatchSizeDays">Maximum days per request batch.</param>
/// <param name="AutoPauseOnRateLimit">Automatically pause job when all providers are rate-limited.</param>
/// <param name="AutoResumeAfterRateLimit">Automatically resume after rate limit window expires.</param>
/// <param name="MaxRateLimitWaitMinutes">Maximum minutes to wait for rate limit before pausing.</param>
public sealed record BackfillJobsConfig(
    bool PersistJobs = true,
    string JobsDirectory = "_backfill_jobs",
    int MaxConcurrentRequests = 3,
    int MaxConcurrentPerProvider = 2,
    int MaxRetries = 3,
    int RetryDelaySeconds = 5,
    int BatchSizeDays = 365,
    bool AutoPauseOnRateLimit = true,
    bool AutoResumeAfterRateLimit = true,
    int MaxRateLimitWaitMinutes = 5
);

/// <summary>
/// Configuration for individual backfill data providers.
/// </summary>
public sealed record BackfillProvidersConfig(
    AlpacaBackfillConfig? Alpaca = null,
    YahooFinanceConfig? Yahoo = null,
    NasdaqDataLinkConfig? Nasdaq = null,
    StooqConfig? Stooq = null,
    OpenFigiConfig? OpenFigi = null
);

/// <summary>
/// Yahoo Finance provider configuration.
/// </summary>
/// <param name="Enabled">Enable this provider.</param>
/// <param name="Priority">Priority in fallback chain (lower = tried first).</param>
/// <param name="RateLimitPerHour">Maximum requests per hour.</param>
public sealed record YahooFinanceConfig(
    bool Enabled = true,
    int Priority = 10,
    int RateLimitPerHour = 2000
);

/// <summary>
/// Nasdaq Data Link (Quandl) provider configuration.
/// </summary>
/// <param name="Enabled">Enable this provider.</param>
/// <param name="ApiKey">API key for higher rate limits (optional).</param>
/// <param name="Database">Database to query (e.g., "WIKI", "EOD").</param>
/// <param name="Priority">Priority in fallback chain (lower = tried first).</param>
public sealed record NasdaqDataLinkConfig(
    bool Enabled = true,
    string? ApiKey = null,
    string Database = "WIKI",
    int Priority = 30
);

/// <summary>
/// Stooq provider configuration.
/// </summary>
/// <param name="Enabled">Enable this provider.</param>
/// <param name="Priority">Priority in fallback chain (lower = tried first).</param>
/// <param name="DefaultMarket">Default market suffix (e.g., "us" for US equities).</param>
public sealed record StooqConfig(
    bool Enabled = true,
    int Priority = 20,
    string DefaultMarket = "us"
);

/// <summary>
/// OpenFIGI symbol resolver configuration.
/// </summary>
/// <param name="Enabled">Enable symbol resolution via OpenFIGI.</param>
/// <param name="ApiKey">Optional API key for higher rate limits.</param>
/// <param name="CacheResults">Cache resolution results in memory.</param>
public sealed record OpenFigiConfig(
    bool Enabled = true,
    string? ApiKey = null,
    bool CacheResults = true
);

/// <summary>
/// Alpaca Markets historical data provider configuration.
/// </summary>
/// <param name="Enabled">Enable this provider.</param>
/// <param name="KeyId">API Key ID (falls back to ALPACA_KEY_ID env var).</param>
/// <param name="SecretKey">API Secret Key (falls back to ALPACA_SECRET_KEY env var).</param>
/// <param name="Feed">Data feed: "iex" (free), "sip" (paid), or "delayed_sip" (free, 15-min delay).</param>
/// <param name="Adjustment">Price adjustment: "raw", "split", "dividend", or "all".</param>
/// <param name="Priority">Priority in fallback chain (lower = tried first).</param>
/// <param name="RateLimitPerMinute">Maximum requests per minute.</param>
public sealed record AlpacaBackfillConfig(
    bool Enabled = true,
    string? KeyId = null,
    string? SecretKey = null,
    string Feed = "iex",
    string Adjustment = "all",
    int Priority = 5,
    int RateLimitPerMinute = 200
);
