using System.Text.Json;
using MarketDataCollector.Application.Config;
using MarketDataCollector.Application.Logging;
using Serilog;

namespace MarketDataCollector.Application.Services;

/// <summary>
/// Service for automatic configuration based on available credentials and environment.
/// Implements user-friendly auto-detection and configuration of data providers.
/// </summary>
public sealed class AutoConfigurationService
{
    private readonly ILogger _log = LoggingSetup.ForContext<AutoConfigurationService>();

    /// <summary>
    /// Result of provider detection.
    /// </summary>
    public sealed record DetectedProvider(
        string Name,
        string DisplayName,
        bool HasCredentials,
        string[] MissingCredentials,
        bool IsRecommended,
        int SuggestedPriority,
        string[] Capabilities
    );

    /// <summary>
    /// Result of auto-configuration analysis.
    /// </summary>
    public sealed record AutoConfigResult(
        bool Success,
        AppConfig Config,
        IReadOnlyList<DetectedProvider> DetectedProviders,
        IReadOnlyList<string> AppliedFixes,
        IReadOnlyList<string> Recommendations,
        IReadOnlyList<string> Warnings
    );

    /// <summary>
    /// Environment variables for each provider.
    /// </summary>
    private static readonly Dictionary<string, ProviderCredentialInfo> ProviderCredentials = new()
    {
        ["Alpaca"] = new ProviderCredentialInfo(
            DisplayName: "Alpaca Markets",
            RequiredEnvVars: new[] { "ALPACA_KEY_ID", "ALPACA_SECRET_KEY" },
            AlternativeEnvVars: new[] { "MDC_ALPACA_KEY_ID", "MDC_ALPACA_SECRET_KEY" },
            Capabilities: new[] { "RealTime", "Historical", "Trades", "Quotes" },
            Priority: 5
        ),
        ["Polygon"] = new ProviderCredentialInfo(
            DisplayName: "Polygon.io",
            RequiredEnvVars: new[] { "POLYGON_API_KEY" },
            AlternativeEnvVars: new[] { "MDC_POLYGON_API_KEY" },
            Capabilities: new[] { "RealTime", "Historical", "Trades", "Quotes", "Aggregates" },
            Priority: 10
        ),
        ["Tiingo"] = new ProviderCredentialInfo(
            DisplayName: "Tiingo",
            RequiredEnvVars: new[] { "TIINGO_API_TOKEN" },
            AlternativeEnvVars: new[] { "TIINGO_TOKEN", "MDC_TIINGO_TOKEN" },
            Capabilities: new[] { "Historical", "Daily" },
            Priority: 15
        ),
        ["Finnhub"] = new ProviderCredentialInfo(
            DisplayName: "Finnhub",
            RequiredEnvVars: new[] { "FINNHUB_API_KEY" },
            AlternativeEnvVars: new[] { "MDC_FINNHUB_API_KEY" },
            Capabilities: new[] { "Historical", "Daily", "Fundamentals" },
            Priority: 18
        ),
        ["AlphaVantage"] = new ProviderCredentialInfo(
            DisplayName: "Alpha Vantage",
            RequiredEnvVars: new[] { "ALPHA_VANTAGE_API_KEY" },
            AlternativeEnvVars: new[] { "ALPHAVANTAGE_API_KEY", "MDC_ALPHA_VANTAGE_API_KEY" },
            Capabilities: new[] { "Historical", "Daily", "Intraday" },
            Priority: 25
        ),
        ["IB"] = new ProviderCredentialInfo(
            DisplayName: "Interactive Brokers",
            RequiredEnvVars: Array.Empty<string>(), // IB uses TWS, not API keys
            AlternativeEnvVars: Array.Empty<string>(),
            Capabilities: new[] { "RealTime", "Historical", "Trades", "Quotes", "L2Depth" },
            Priority: 1
        ),
        ["Yahoo"] = new ProviderCredentialInfo(
            DisplayName: "Yahoo Finance",
            RequiredEnvVars: Array.Empty<string>(), // No API key required
            AlternativeEnvVars: Array.Empty<string>(),
            Capabilities: new[] { "Historical", "Daily" },
            Priority: 10
        ),
        ["Stooq"] = new ProviderCredentialInfo(
            DisplayName: "Stooq",
            RequiredEnvVars: Array.Empty<string>(), // No API key required
            AlternativeEnvVars: Array.Empty<string>(),
            Capabilities: new[] { "Historical", "Daily" },
            Priority: 20
        )
    };

    /// <summary>
    /// Detects all available providers based on environment variables.
    /// </summary>
    public IReadOnlyList<DetectedProvider> DetectAvailableProviders()
    {
        var providers = new List<DetectedProvider>();

        foreach (var (name, info) in ProviderCredentials)
        {
            var (hasCredentials, missing) = CheckCredentials(info);

            providers.Add(new DetectedProvider(
                Name: name,
                DisplayName: info.DisplayName,
                HasCredentials: hasCredentials,
                MissingCredentials: missing,
                IsRecommended: hasCredentials && info.Capabilities.Contains("RealTime"),
                SuggestedPriority: info.Priority,
                Capabilities: info.Capabilities
            ));
        }

        return providers.OrderBy(p => p.HasCredentials ? 0 : 1)
                        .ThenBy(p => p.SuggestedPriority)
                        .ToList();
    }

    /// <summary>
    /// Auto-configures the application based on detected providers and environment.
    /// </summary>
    public AutoConfigResult AutoConfigure(AppConfig? existingConfig = null)
    {
        var config = existingConfig ?? new AppConfig();
        var appliedFixes = new List<string>();
        var recommendations = new List<string>();
        var warnings = new List<string>();

        var detectedProviders = DetectAvailableProviders();
        var availableRealTime = detectedProviders
            .Where(p => p.HasCredentials && p.Capabilities.Contains("RealTime"))
            .ToList();
        var availableHistorical = detectedProviders
            .Where(p => p.HasCredentials && p.Capabilities.Contains("Historical"))
            .ToList();

        _log.Information("Auto-configuration: Detected {RealTimeCount} real-time providers, {HistoricalCount} historical providers",
            availableRealTime.Count, availableHistorical.Count);

        // Auto-configure real-time data source
        if (config.DataSource == DataSourceKind.IB && !IsIBGatewayAvailable())
        {
            if (availableRealTime.Any())
            {
                var bestProvider = availableRealTime.First();
                if (Enum.TryParse<DataSourceKind>(bestProvider.Name, out var dataSourceKind))
                {
                    config = config with { DataSource = dataSourceKind };
                    appliedFixes.Add($"Switched from IB to {bestProvider.DisplayName} (IB Gateway not detected)");
                }
            }
            else
            {
                warnings.Add("No real-time data providers configured. Set ALPACA_KEY_ID/ALPACA_SECRET_KEY or POLYGON_API_KEY environment variables.");
            }
        }

        // Auto-configure Alpaca credentials if available
        config = AutoConfigureAlpaca(config, appliedFixes);

        // Auto-configure Polygon credentials if available
        config = AutoConfigurePolygon(config, appliedFixes);

        // Auto-configure backfill providers
        config = AutoConfigureBackfill(config, availableHistorical, appliedFixes);

        // Auto-configure storage based on environment
        config = AutoConfigureStorage(config, appliedFixes);

        // Apply self-healing fixes
        config = ApplySelfHealingFixes(config, appliedFixes, warnings);

        // Generate recommendations
        GenerateRecommendations(config, detectedProviders, recommendations);

        return new AutoConfigResult(
            Success: true,
            Config: config,
            DetectedProviders: detectedProviders,
            AppliedFixes: appliedFixes,
            Recommendations: recommendations,
            Warnings: warnings
        );
    }

    /// <summary>
    /// Generates configuration for first-time users with interactive defaults.
    /// </summary>
    public AppConfig GenerateFirstTimeConfig(FirstTimeConfigOptions options)
    {
        var config = new AppConfig();
        var detectedProviders = DetectAvailableProviders();

        // Select data source
        if (options.UseCase == UseCase.RealTimeTrading)
        {
            var realTimeProvider = detectedProviders
                .FirstOrDefault(p => p.HasCredentials && p.Capabilities.Contains("RealTime"));

            if (realTimeProvider != null && Enum.TryParse<DataSourceKind>(realTimeProvider.Name, out var kind))
            {
                config = config with { DataSource = kind };
            }
        }

        // Configure Alpaca if credentials are available
        var alpacaKeyId = GetEnvVar("ALPACA_KEY_ID", "MDC_ALPACA_KEY_ID");
        var alpacaSecret = GetEnvVar("ALPACA_SECRET_KEY", "MDC_ALPACA_SECRET_KEY");
        if (!string.IsNullOrEmpty(alpacaKeyId) && !string.IsNullOrEmpty(alpacaSecret))
        {
            config = config with
            {
                Alpaca = new AlpacaOptions(
                    KeyId: alpacaKeyId,
                    SecretKey: alpacaSecret,
                    Feed: options.UseCase == UseCase.RealTimeTrading ? "sip" : "iex",
                    UseSandbox: options.UseCase == UseCase.Development
                )
            };
        }

        // Set up symbols based on use case
        config = config with
        {
            Symbols = GetDefaultSymbols(options.SymbolPreset)
        };

        // Configure storage
        config = config with
        {
            Storage = new StorageConfig(
                NamingConvention: "BySymbol",
                DatePartition: "Daily",
                RetentionDays: options.UseCase == UseCase.Development ? 30 : null
            ),
            Compress = options.UseCase != UseCase.Development
        };

        // Configure backfill
        if (options.EnableBackfill)
        {
            config = config with
            {
                Backfill = GenerateBackfillConfig(detectedProviders)
            };
        }

        return config;
    }

    private AppConfig AutoConfigureAlpaca(AppConfig config, List<string> appliedFixes)
    {
        var keyId = GetEnvVar("ALPACA_KEY_ID", "MDC_ALPACA_KEY_ID");
        var secretKey = GetEnvVar("ALPACA_SECRET_KEY", "MDC_ALPACA_SECRET_KEY");

        if (string.IsNullOrEmpty(keyId) || string.IsNullOrEmpty(secretKey))
            return config;

        // Only auto-configure if not already set or using placeholder
        if (config.Alpaca == null ||
            string.IsNullOrEmpty(config.Alpaca.KeyId) ||
            config.Alpaca.KeyId.StartsWith("YOUR_") ||
            config.Alpaca.KeyId.StartsWith("${"))
        {
            var feed = GetEnvVar("ALPACA_FEED", "MDC_ALPACA_FEED") ?? "iex";
            var sandbox = GetEnvVar("ALPACA_SANDBOX", "MDC_ALPACA_SANDBOX");

            config = config with
            {
                Alpaca = new AlpacaOptions(
                    KeyId: keyId,
                    SecretKey: secretKey,
                    Feed: feed,
                    UseSandbox: sandbox?.Equals("true", StringComparison.OrdinalIgnoreCase) ?? false
                )
            };
            appliedFixes.Add("Auto-configured Alpaca credentials from environment variables");
        }

        return config;
    }

    private AppConfig AutoConfigurePolygon(AppConfig config, List<string> appliedFixes)
    {
        var apiKey = GetEnvVar("POLYGON_API_KEY", "MDC_POLYGON_API_KEY");

        if (string.IsNullOrEmpty(apiKey))
            return config;

        if (config.Polygon == null || string.IsNullOrEmpty(config.Polygon.ApiKey))
        {
            config = config with
            {
                Polygon = new PolygonOptions(
                    ApiKey: apiKey,
                    Feed: "stocks",
                    SubscribeTrades: true
                )
            };
            appliedFixes.Add("Auto-configured Polygon credentials from environment variables");
        }

        return config;
    }

    private AppConfig AutoConfigureBackfill(AppConfig config, IReadOnlyList<DetectedProvider> availableHistorical, List<string> appliedFixes)
    {
        if (config.Backfill?.Providers != null)
            return config;

        var providers = new BackfillProvidersConfig();
        var priorityOrder = new List<string>();

        // Auto-enable providers with credentials
        foreach (var provider in availableHistorical.OrderBy(p => p.SuggestedPriority))
        {
            switch (provider.Name)
            {
                case "Alpaca":
                    var alpacaKey = GetEnvVar("ALPACA_KEY_ID", "MDC_ALPACA_KEY_ID");
                    var alpacaSecret = GetEnvVar("ALPACA_SECRET_KEY", "MDC_ALPACA_SECRET_KEY");
                    providers = providers with
                    {
                        Alpaca = new AlpacaBackfillConfig(
                            Enabled: true,
                            KeyId: alpacaKey,
                            SecretKey: alpacaSecret,
                            Priority: provider.SuggestedPriority
                        )
                    };
                    priorityOrder.Add("alpaca");
                    break;

                case "Tiingo":
                    var tiingoToken = GetEnvVar("TIINGO_API_TOKEN", "TIINGO_TOKEN", "MDC_TIINGO_TOKEN");
                    providers = providers with
                    {
                        Tiingo = new TiingoConfig(
                            Enabled: true,
                            ApiToken: tiingoToken,
                            Priority: provider.SuggestedPriority
                        )
                    };
                    priorityOrder.Add("tiingo");
                    break;

                case "Polygon":
                    var polygonKey = GetEnvVar("POLYGON_API_KEY", "MDC_POLYGON_API_KEY");
                    providers = providers with
                    {
                        Polygon = new PolygonConfig(
                            Enabled: true,
                            ApiKey: polygonKey,
                            Priority: provider.SuggestedPriority
                        )
                    };
                    priorityOrder.Add("polygon");
                    break;

                case "Finnhub":
                    var finnhubKey = GetEnvVar("FINNHUB_API_KEY", "MDC_FINNHUB_API_KEY");
                    providers = providers with
                    {
                        Finnhub = new FinnhubConfig(
                            Enabled: true,
                            ApiKey: finnhubKey,
                            Priority: provider.SuggestedPriority
                        )
                    };
                    priorityOrder.Add("finnhub");
                    break;

                case "AlphaVantage":
                    var avKey = GetEnvVar("ALPHA_VANTAGE_API_KEY", "ALPHAVANTAGE_API_KEY", "MDC_ALPHA_VANTAGE_API_KEY");
                    providers = providers with
                    {
                        AlphaVantage = new AlphaVantageConfig(
                            Enabled: true,
                            ApiKey: avKey,
                            Priority: provider.SuggestedPriority
                        )
                    };
                    priorityOrder.Add("alphavantage");
                    break;

                case "Yahoo":
                    providers = providers with
                    {
                        Yahoo = new YahooFinanceConfig(Enabled: true, Priority: provider.SuggestedPriority)
                    };
                    priorityOrder.Add("yahoo");
                    break;

                case "Stooq":
                    providers = providers with
                    {
                        Stooq = new StooqConfig(Enabled: true, Priority: provider.SuggestedPriority)
                    };
                    priorityOrder.Add("stooq");
                    break;
            }
        }

        // Always enable free providers as fallback
        if (providers.Yahoo == null)
        {
            providers = providers with { Yahoo = new YahooFinanceConfig(Enabled: true, Priority: 100) };
            priorityOrder.Add("yahoo");
        }
        if (providers.Stooq == null)
        {
            providers = providers with { Stooq = new StooqConfig(Enabled: true, Priority: 110) };
            priorityOrder.Add("stooq");
        }

        var backfill = config.Backfill ?? new BackfillConfig();
        config = config with
        {
            Backfill = backfill with
            {
                Provider = "composite",
                Providers = providers,
                ProviderPriority = priorityOrder.ToArray(),
                EnableFallback = true,
                EnableRateLimitRotation = true
            }
        };

        if (availableHistorical.Any())
        {
            appliedFixes.Add($"Auto-configured {availableHistorical.Count} backfill providers based on environment credentials");
        }

        return config;
    }

    private AppConfig AutoConfigureStorage(AppConfig config, List<string> appliedFixes)
    {
        if (config.Storage != null)
            return config;

        // Detect available disk space and set sensible defaults
        var dataRoot = config.DataRoot;
        long? maxMegabytes = null;

        try
        {
            var rootPath = Path.GetFullPath(dataRoot);
            var drive = new DriveInfo(Path.GetPathRoot(rootPath) ?? "/");

            // Reserve 10% of available space, max 100GB
            var availableBytes = drive.AvailableFreeSpace;
            var reserveBytes = Math.Min(availableBytes / 10, 100L * 1024 * 1024 * 1024);
            maxMegabytes = reserveBytes / (1024 * 1024);

            _log.Debug("Auto-detected storage: {AvailableGB} GB available, reserving {ReserveGB} GB",
                availableBytes / (1024 * 1024 * 1024), reserveBytes / (1024 * 1024 * 1024));
        }
        catch (Exception ex)
        {
            _log.Debug(ex, "Could not auto-detect storage space");
        }

        config = config with
        {
            Storage = new StorageConfig(
                NamingConvention: "BySymbol",
                DatePartition: "Daily",
                MaxTotalMegabytes: maxMegabytes,
                RetentionDays: null
            )
        };

        appliedFixes.Add("Auto-configured storage with default naming convention");

        return config;
    }

    private AppConfig ApplySelfHealingFixes(AppConfig config, List<string> appliedFixes, List<string> warnings)
    {
        // Fix: Alpaca selected but no credentials
        if (config.DataSource == DataSourceKind.Alpaca)
        {
            if (config.Alpaca == null || string.IsNullOrEmpty(config.Alpaca.KeyId))
            {
                // Try to get from environment
                var keyId = GetEnvVar("ALPACA_KEY_ID", "MDC_ALPACA_KEY_ID");
                var secretKey = GetEnvVar("ALPACA_SECRET_KEY", "MDC_ALPACA_SECRET_KEY");

                if (!string.IsNullOrEmpty(keyId) && !string.IsNullOrEmpty(secretKey))
                {
                    config = config with
                    {
                        Alpaca = new AlpacaOptions(keyId, secretKey)
                    };
                    appliedFixes.Add("Fixed: Added Alpaca credentials from environment variables");
                }
                else
                {
                    // Fall back to free provider
                    warnings.Add("Alpaca selected but no credentials found. Consider setting ALPACA_KEY_ID and ALPACA_SECRET_KEY environment variables.");
                }
            }
        }

        // Fix: Invalid naming convention
        if (config.Storage != null)
        {
            var validConventions = new[] { "flat", "bysymbol", "bydate", "bytype", "bysource", "byassetclass", "hierarchical", "canonical" };
            if (!validConventions.Contains(config.Storage.NamingConvention.ToLowerInvariant()))
            {
                config = config with
                {
                    Storage = config.Storage with { NamingConvention = "BySymbol" }
                };
                appliedFixes.Add($"Fixed: Invalid naming convention '{config.Storage.NamingConvention}' changed to 'BySymbol'");
            }

            // Fix: Invalid date partition
            var validPartitions = new[] { "none", "daily", "hourly", "monthly" };
            if (!validPartitions.Contains(config.Storage.DatePartition.ToLowerInvariant()))
            {
                config = config with
                {
                    Storage = config.Storage with { DatePartition = "Daily" }
                };
                appliedFixes.Add($"Fixed: Invalid date partition '{config.Storage.DatePartition}' changed to 'Daily'");
            }
        }

        // Fix: Empty symbols list
        if (config.Symbols == null || config.Symbols.Length == 0)
        {
            config = config with
            {
                Symbols = new[]
                {
                    new SymbolConfig("SPY", SubscribeTrades: true, SubscribeDepth: true, DepthLevels: 10)
                }
            };
            appliedFixes.Add("Fixed: Added default symbol (SPY) since none were configured");
        }

        // Fix: Invalid depth levels
        if (config.Symbols != null)
        {
            var fixedSymbols = config.Symbols.Select(s =>
            {
                if (s.SubscribeDepth && (s.DepthLevels < 1 || s.DepthLevels > 50))
                {
                    return s with { DepthLevels = Math.Clamp(s.DepthLevels, 1, 50) };
                }
                return s;
            }).ToArray();

            if (!config.Symbols.SequenceEqual(fixedSymbols))
            {
                config = config with { Symbols = fixedSymbols };
                appliedFixes.Add("Fixed: Adjusted depth levels to valid range (1-50)");
            }
        }

        // Fix: Backfill date range issues
        if (config.Backfill != null)
        {
            var backfill = config.Backfill;
            var needsFix = false;

            // Fix: From date after To date
            if (backfill.From.HasValue && backfill.To.HasValue && backfill.From > backfill.To)
            {
                backfill = backfill with { From = backfill.To, To = backfill.From };
                needsFix = true;
                appliedFixes.Add("Fixed: Swapped backfill From/To dates (From was after To)");
            }

            // Fix: Future end date
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            if (backfill.To.HasValue && backfill.To > today)
            {
                backfill = backfill with { To = today };
                needsFix = true;
                appliedFixes.Add("Fixed: Adjusted backfill To date to today (was in the future)");
            }

            if (needsFix)
            {
                config = config with { Backfill = backfill };
            }
        }

        return config;
    }

    private void GenerateRecommendations(AppConfig config, IReadOnlyList<DetectedProvider> providers, List<string> recommendations)
    {
        var configuredProviders = providers.Where(p => p.HasCredentials).ToList();
        var unconfiguredProviders = providers.Where(p => !p.HasCredentials && p.MissingCredentials.Length > 0).ToList();

        // Recommend missing providers
        if (!configuredProviders.Any(p => p.Capabilities.Contains("RealTime")))
        {
            recommendations.Add("Consider setting up a real-time data provider (Alpaca or Polygon) for live trading data.");
        }

        if (!configuredProviders.Any(p => p.Name == "Tiingo" || p.Name == "Polygon"))
        {
            recommendations.Add("Consider adding Tiingo (TIINGO_API_TOKEN) or Polygon (POLYGON_API_KEY) for better historical data quality.");
        }

        // Recommend compression for production
        if (!config.Compress && configuredProviders.Count > 0)
        {
            recommendations.Add("Enable compression (Compress: true) to reduce storage usage in production.");
        }

        // Recommend retention policy
        if (config.Storage?.RetentionDays == null && config.Storage?.MaxTotalMegabytes == null)
        {
            recommendations.Add("Consider setting RetentionDays or MaxTotalMegabytes to prevent unbounded storage growth.");
        }

        // Provider-specific recommendations
        if (config.DataSource == DataSourceKind.Alpaca && config.Alpaca?.Feed == "iex")
        {
            recommendations.Add("You're using the IEX feed (free). Consider upgrading to 'sip' for consolidated market data.");
        }
    }

    private BackfillConfig GenerateBackfillConfig(IReadOnlyList<DetectedProvider> providers)
    {
        var historical = providers.Where(p => p.HasCredentials && p.Capabilities.Contains("Historical")).ToList();
        var priority = historical.Select(p => p.Name.ToLowerInvariant()).ToList();

        // Always add free providers
        if (!priority.Contains("yahoo")) priority.Add("yahoo");
        if (!priority.Contains("stooq")) priority.Add("stooq");

        return new BackfillConfig(
            Enabled: false,
            Provider: "composite",
            EnableFallback: true,
            EnableRateLimitRotation: true,
            ProviderPriority: priority.ToArray()
        );
    }

    private static SymbolConfig[] GetDefaultSymbols(SymbolPreset preset)
    {
        return preset switch
        {
            SymbolPreset.USMajorIndices => new[]
            {
                new SymbolConfig("SPY", SubscribeTrades: true, SubscribeDepth: true, DepthLevels: 10),
                new SymbolConfig("QQQ", SubscribeTrades: true, SubscribeDepth: true, DepthLevels: 10),
                new SymbolConfig("DIA", SubscribeTrades: true, SubscribeDepth: true, DepthLevels: 10),
                new SymbolConfig("IWM", SubscribeTrades: true, SubscribeDepth: true, DepthLevels: 10)
            },
            SymbolPreset.TechGiants => new[]
            {
                new SymbolConfig("AAPL", SubscribeTrades: true, SubscribeDepth: true, DepthLevels: 10),
                new SymbolConfig("MSFT", SubscribeTrades: true, SubscribeDepth: true, DepthLevels: 10),
                new SymbolConfig("GOOGL", SubscribeTrades: true, SubscribeDepth: true, DepthLevels: 10),
                new SymbolConfig("AMZN", SubscribeTrades: true, SubscribeDepth: true, DepthLevels: 10),
                new SymbolConfig("META", SubscribeTrades: true, SubscribeDepth: true, DepthLevels: 10),
                new SymbolConfig("NVDA", SubscribeTrades: true, SubscribeDepth: true, DepthLevels: 10)
            },
            SymbolPreset.SP500Top20 => new[]
            {
                new SymbolConfig("AAPL", SubscribeTrades: true, SubscribeDepth: false),
                new SymbolConfig("MSFT", SubscribeTrades: true, SubscribeDepth: false),
                new SymbolConfig("GOOGL", SubscribeTrades: true, SubscribeDepth: false),
                new SymbolConfig("AMZN", SubscribeTrades: true, SubscribeDepth: false),
                new SymbolConfig("NVDA", SubscribeTrades: true, SubscribeDepth: false),
                new SymbolConfig("META", SubscribeTrades: true, SubscribeDepth: false),
                new SymbolConfig("BRK.B", SubscribeTrades: true, SubscribeDepth: false),
                new SymbolConfig("TSLA", SubscribeTrades: true, SubscribeDepth: false),
                new SymbolConfig("UNH", SubscribeTrades: true, SubscribeDepth: false),
                new SymbolConfig("JNJ", SubscribeTrades: true, SubscribeDepth: false),
                new SymbolConfig("JPM", SubscribeTrades: true, SubscribeDepth: false),
                new SymbolConfig("V", SubscribeTrades: true, SubscribeDepth: false),
                new SymbolConfig("PG", SubscribeTrades: true, SubscribeDepth: false),
                new SymbolConfig("MA", SubscribeTrades: true, SubscribeDepth: false),
                new SymbolConfig("HD", SubscribeTrades: true, SubscribeDepth: false),
                new SymbolConfig("CVX", SubscribeTrades: true, SubscribeDepth: false),
                new SymbolConfig("MRK", SubscribeTrades: true, SubscribeDepth: false),
                new SymbolConfig("ABBV", SubscribeTrades: true, SubscribeDepth: false),
                new SymbolConfig("LLY", SubscribeTrades: true, SubscribeDepth: false),
                new SymbolConfig("PEP", SubscribeTrades: true, SubscribeDepth: false)
            },
            SymbolPreset.Crypto => new[]
            {
                new SymbolConfig("BTC/USD", SubscribeTrades: true, SubscribeDepth: true, DepthLevels: 10),
                new SymbolConfig("ETH/USD", SubscribeTrades: true, SubscribeDepth: true, DepthLevels: 10),
                new SymbolConfig("SOL/USD", SubscribeTrades: true, SubscribeDepth: false)
            },
            SymbolPreset.Custom or _ => new[]
            {
                new SymbolConfig("SPY", SubscribeTrades: true, SubscribeDepth: true, DepthLevels: 10)
            }
        };
    }

    private static (bool HasCredentials, string[] Missing) CheckCredentials(ProviderCredentialInfo info)
    {
        if (info.RequiredEnvVars.Length == 0)
            return (true, Array.Empty<string>());

        var missing = new List<string>();

        foreach (var envVar in info.RequiredEnvVars)
        {
            var value = Environment.GetEnvironmentVariable(envVar);

            // Check alternatives if main not set
            if (string.IsNullOrEmpty(value))
            {
                var altFound = info.AlternativeEnvVars.Any(alt =>
                    !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(alt)));

                if (!altFound)
                    missing.Add(envVar);
            }
        }

        return (missing.Count == 0, missing.ToArray());
    }

    private static string? GetEnvVar(params string[] names)
    {
        foreach (var name in names)
        {
            var value = Environment.GetEnvironmentVariable(name);
            if (!string.IsNullOrEmpty(value))
                return value;
        }
        return null;
    }

    private static bool IsIBGatewayAvailable()
    {
        // Check if IB Gateway/TWS is running on default ports
        try
        {
            var ports = new[] { 7496, 7497, 4001, 4002 };
            foreach (var port in ports)
            {
                using var client = new System.Net.Sockets.TcpClient();
                var result = client.BeginConnect("127.0.0.1", port, null, null);
                var success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(100));
                if (success && client.Connected)
                {
                    client.EndConnect(result);
                    return true;
                }
            }
        }
        catch
        {
            // Ignore connection errors
        }
        return false;
    }

    private sealed record ProviderCredentialInfo(
        string DisplayName,
        string[] RequiredEnvVars,
        string[] AlternativeEnvVars,
        string[] Capabilities,
        int Priority
    );
}

/// <summary>
/// Options for first-time configuration generation.
/// </summary>
public sealed record FirstTimeConfigOptions(
    UseCase UseCase = UseCase.Development,
    SymbolPreset SymbolPreset = SymbolPreset.USMajorIndices,
    bool EnableBackfill = true,
    bool EnableCompression = true
);

/// <summary>
/// Use case for configuration.
/// </summary>
public enum UseCase
{
    Development,
    Research,
    RealTimeTrading,
    BackfillOnly,
    Production
}

/// <summary>
/// Preset symbol lists.
/// </summary>
public enum SymbolPreset
{
    Custom,
    USMajorIndices,
    TechGiants,
    SP500Top20,
    Crypto
}
