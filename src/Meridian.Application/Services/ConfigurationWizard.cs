using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Meridian.Application.Config;
using Meridian.Application.Logging;
using Meridian.Storage;
using Serilog;

namespace Meridian.Application.Services;

/// <summary>
/// Interactive configuration wizard for first-time setup.
/// Guides users through configuration with a step-by-step process.
/// </summary>
public sealed class ConfigurationWizard
{
    private readonly ILogger _log = LoggingSetup.ForContext<ConfigurationWizard>();
    private readonly AutoConfigurationService _autoConfig;
    private readonly TextWriter _output;
    private readonly TextReader _input;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// Provider signup URLs for credential guidance.
    /// </summary>
    private static readonly Dictionary<string, (string SignupUrl, string DocsUrl, string FreeTier)> ProviderSignupInfo = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Alpaca"] = (
            "https://app.alpaca.markets/signup",
            "https://docs.alpaca.markets/docs/getting-started",
            "Free: IEX feed (real-time, ~10% of trades), unlimited paper trading"
        ),
        ["Polygon"] = (
            "https://polygon.io/dashboard/signup",
            "https://polygon.io/docs/stocks/getting-started",
            "Free: 5 API calls/min, end-of-day data"
        ),
        ["Tiingo"] = (
            "https://www.tiingo.com/account/api/token",
            "https://www.tiingo.com/documentation/general/overview",
            "Free: 500 requests/hour, daily historical data"
        ),
        ["Finnhub"] = (
            "https://finnhub.io/register",
            "https://finnhub.io/docs/api",
            "Free: 60 API calls/min, US stock data"
        ),
        ["AlphaVantage"] = (
            "https://www.alphavantage.co/support/#api-key",
            "https://www.alphavantage.co/documentation/",
            "Free: 25 requests/day, daily historical data"
        ),
    };

    public ConfigurationWizard(TextWriter? output = null, TextReader? input = null)
    {
        _autoConfig = new AutoConfigurationService();
        _output = output ?? Console.Out;
        _input = input ?? Console.In;
    }

    /// <summary>
    /// Runs the interactive configuration wizard.
    /// </summary>
    public async Task<WizardResult> RunAsync(CancellationToken ct = default)
    {
        try
        {
            PrintHeader();

            // Step 1: Detect available providers
            var detectedProviders = await DetectProvidersStepAsync(ct);

            // Step 2: Credential guidance - help users get API keys
            await CredentialGuidanceStepAsync(detectedProviders, ct);

            // Step 3: Select use case
            var useCase = await SelectUseCaseStepAsync(ct);

            // Step 4: Configure data source
            var dataSource = await ConfigureDataSourceStepAsync(detectedProviders, useCase, ct);

            // Step 5: Validate credentials for selected provider
            await ValidateCredentialsStepAsync(dataSource, ct);

            // Step 6: Configure symbols
            var symbols = await ConfigureSymbolsStepAsync(useCase, ct);

            // Step 7: Configure storage
            var storage = await ConfigureStorageStepAsync(useCase, ct);

            // Step 8: Configure backfill
            var backfill = await ConfigureBackfillStepAsync(detectedProviders, useCase, ct);

            // Build final configuration
            var config = BuildConfiguration(dataSource, symbols, storage, backfill);

            // Step 9: Review and confirm
            var confirmed = await ReviewConfigurationStepAsync(config, ct);

            if (!confirmed)
            {
                PrintLine("\nConfiguration cancelled. No changes made.");
                return new WizardResult(Success: false, Config: null, ConfigPath: null);
            }

            // Step 10: Save configuration
            var savedPath = await SaveConfigurationStepAsync(config, ct);

            PrintNextSteps(savedPath, dataSource.DataSource);

            return new WizardResult(Success: true, Config: config, ConfigPath: savedPath);
        }
        catch (OperationCanceledException)
        {
            PrintLine("\nWizard cancelled.");
            return new WizardResult(Success: false, Config: null, ConfigPath: null);
        }
    }

    /// <summary>
    /// Runs a quick auto-configuration without interactive prompts.
    /// </summary>
    public WizardResult RunQuickSetup()
    {
        PrintHeader();
        PrintLine("Running quick auto-configuration...\n");

        var result = _autoConfig.AutoConfigure();

        if (result.AppliedFixes.Count > 0)
        {
            PrintLine("Applied automatic fixes:");
            foreach (var fix in result.AppliedFixes)
            {
                PrintLine($"  - {fix}");
            }
            PrintLine();
        }

        if (result.Warnings.Count > 0)
        {
            PrintWarning("Warnings:");
            foreach (var warning in result.Warnings)
            {
                PrintLine($"  - {warning}");
            }
            PrintLine();
        }

        PrintLine("Detected providers:");
        foreach (var provider in result.DetectedProviders)
        {
            var status = provider.HasCredentials ? "[OK]" : "[--]";
            PrintLine($"  {status} {provider.DisplayName}");
        }
        PrintLine();

        // Save configuration
        var configPath = Path.Combine("config", "appsettings.json");
        SaveConfiguration(result.Config, configPath);

        PrintSuccess($"Configuration saved to: {configPath}");

        if (result.Recommendations.Count > 0)
        {
            PrintLine("\nRecommendations:");
            foreach (var rec in result.Recommendations)
            {
                PrintLine($"  - {rec}");
            }
        }

        PrintLine("\nNext steps:");
        PrintLine("  1. Validate:  dotnet run -- --dry-run");
        PrintLine("  2. Start:     dotnet run -- --mode web");
        PrintLine("  3. Dashboard: http://localhost:8080");
        PrintLine();

        return new WizardResult(Success: true, Config: result.Config, ConfigPath: configPath);
    }

    private void PrintHeader()
    {
        PrintLine("=".PadRight(60, '='));
        PrintLine("  Market Data Collector - Configuration Wizard");
        PrintLine("=".PadRight(60, '='));
        PrintLine();
    }

    private async Task<IReadOnlyList<AutoConfigurationService.DetectedProvider>> DetectProvidersStepAsync(CancellationToken ct)
    {
        PrintStep(1, "Detecting Available Providers");

        var providers = _autoConfig.DetectAvailableProviders();

        PrintLine("\nDetected providers:\n");

        foreach (var provider in providers)
        {
            var statusIcon = provider.HasCredentials ? "[OK]" : "[  ]";
            var statusText = provider.HasCredentials ? "Configured" : "Not configured";

            PrintLine($"  {statusIcon} {provider.DisplayName,-25} {statusText}");

            if (!provider.HasCredentials && provider.MissingCredentials.Length > 0)
            {
                PrintLine($"        Missing: {string.Join(", ", provider.MissingCredentials)}");
            }
        }

        var configuredCount = providers.Count(p => p.HasCredentials);
        PrintLine($"\n  {configuredCount}/{providers.Count} providers configured");

        if (configuredCount == 0)
        {
            PrintWarning("\n  No API credentials detected. You can still use free providers (Yahoo, Stooq)");
            PrintLine("  or configure credentials via environment variables:\n");
            PrintLine("    export ALPACA_KEY_ID=your-key-id");
            PrintLine("    export ALPACA_SECRET_KEY=your-secret-key");
        }

        await Task.CompletedTask;
        return providers;
    }

    private async Task<UseCase> SelectUseCaseStepAsync(CancellationToken ct)
    {
        PrintStep(3, "Select Your Use Case");

        PrintLine("\nHow will you use Market Data Collector?\n");
        PrintLine("  1. Development/Testing - Local development with sample data");
        PrintLine("  2. Research - Historical data analysis and backtesting");
        PrintLine("  3. Real-Time Trading - Live market data streaming");
        PrintLine("  4. Backfill Only - Historical data collection only");
        PrintLine("  5. Production - Full production deployment");

        var choice = await PromptChoiceAsync("Select option", 1, 5, defaultValue: 1, ct: ct);

        return choice switch
        {
            1 => UseCase.Development,
            2 => UseCase.Research,
            3 => UseCase.RealTimeTrading,
            4 => UseCase.BackfillOnly,
            5 => UseCase.Production,
            _ => UseCase.Development
        };
    }

    private async Task<DataSourceSelection> ConfigureDataSourceStepAsync(
        IReadOnlyList<AutoConfigurationService.DetectedProvider> providers,
        UseCase useCase,
        CancellationToken ct)
    {
        PrintStep(4, "Configure Data Source");

        var selection = new DataSourceSelection();

        // For backfill-only, skip real-time provider selection
        if (useCase == UseCase.BackfillOnly)
        {
            PrintLine("\nBackfill mode selected - skipping real-time data source configuration.");
            selection.DataSource = DataSourceKind.IB; // Default, won't be used
            return selection;
        }

        var realTimeProviders = providers
            .Where(p => p.Capabilities.Contains("RealTime"))
            .ToList();

        var configuredRealTime = realTimeProviders.Where(p => p.HasCredentials).ToList();

        PrintLine("\nSelect your primary real-time data source:\n");

        var options = new List<(DataSourceKind Kind, string Name, bool Available)>();

        foreach (var provider in realTimeProviders)
        {
            if (Enum.TryParse<DataSourceKind>(provider.Name, out var kind))
            {
                options.Add((kind, provider.DisplayName, provider.HasCredentials));
            }
        }

        for (int i = 0; i < options.Count; i++)
        {
            var (kind, name, available) = options[i];
            var status = available ? "[OK]" : "[--]";
            PrintLine($"  {i + 1}. {status} {name}");
        }

        var defaultChoice = configuredRealTime.Any()
            ? options.FindIndex(o => o.Name == configuredRealTime.First().DisplayName) + 1
            : 1;

        if (defaultChoice < 1)
            defaultChoice = 1;

        var choice = await PromptChoiceAsync("Select data source", 1, options.Count, defaultChoice, ct);
        selection.DataSource = options[choice - 1].Kind;

        // Configure provider-specific options
        switch (selection.DataSource)
        {
            case DataSourceKind.Alpaca:
                selection.Alpaca = await ConfigureAlpacaOptionsAsync(ct);
                break;
            case DataSourceKind.Polygon:
                selection.Polygon = await ConfigurePolygonOptionsAsync(ct);
                break;
            case DataSourceKind.IB:
                selection.IB = await ConfigureIBOptionsAsync(ct);
                break;
            case DataSourceKind.Synthetic:
                PrintLine("\n  Synthetic offline dataset selected. No credentials are required.");
                break;
        }

        return selection;
    }

    private async Task<AlpacaOptions?> ConfigureAlpacaOptionsAsync(CancellationToken ct)
    {
        PrintLine("\n  Alpaca Configuration:");

        var keyId = Environment.GetEnvironmentVariable("ALPACA_KEY_ID") ??
                    Environment.GetEnvironmentVariable("MDC_ALPACA_KEY_ID");
        var secretKey = Environment.GetEnvironmentVariable("ALPACA_SECRET_KEY") ??
                        Environment.GetEnvironmentVariable("MDC_ALPACA_SECRET_KEY");

        if (string.IsNullOrEmpty(keyId))
        {
            PrintLine("\n  No Alpaca credentials found in environment.");
            PrintLine("  You can set them now or configure later in appsettings.json\n");

            keyId = await PromptStringAsync("  Alpaca Key ID (or press Enter to skip)", required: false, ct: ct);
            if (string.IsNullOrEmpty(keyId))
            {
                PrintWarning("  Skipping Alpaca configuration. Set ALPACA_KEY_ID environment variable later.");
                return null;
            }

            secretKey = await PromptStringAsync("  Alpaca Secret Key", required: true, ct: ct);
        }
        else
        {
            PrintLine($"  Using credentials from environment (Key ID: {keyId[..Math.Min(8, keyId.Length)]}...)");
        }

        PrintLine("\n  Select data feed:");
        PrintLine("    1. IEX (free, ~10% of trades)");
        PrintLine("    2. SIP (paid, full market data)");
        PrintLine("    3. Delayed SIP (free, 15-minute delay)");

        var feedChoice = await PromptChoiceAsync("  Feed", 1, 3, 1, ct);
        var feed = feedChoice switch
        {
            1 => "iex",
            2 => "sip",
            3 => "delayed_sip",
            _ => "iex"
        };

        var useSandbox = await PromptYesNoAsync("  Use sandbox/paper trading", defaultValue: false, ct: ct);

        return new AlpacaOptions(
            KeyId: keyId ?? "",
            SecretKey: secretKey ?? "",
            Feed: feed,
            UseSandbox: useSandbox
        );
    }

    private async Task<PolygonOptions?> ConfigurePolygonOptionsAsync(CancellationToken ct)
    {
        PrintLine("\n  Polygon Configuration:");

        var apiKey = Environment.GetEnvironmentVariable("POLYGON_API_KEY") ??
                     Environment.GetEnvironmentVariable("MDC_POLYGON_API_KEY");

        if (string.IsNullOrEmpty(apiKey))
        {
            apiKey = await PromptStringAsync("  Polygon API Key", required: true, ct: ct);
        }
        else
        {
            PrintLine($"  Using API key from environment ({apiKey[..Math.Min(8, apiKey.Length)]}...)");
        }

        var subscribeTrades = await PromptYesNoAsync("  Subscribe to trades", defaultValue: true, ct: ct);
        var subscribeQuotes = await PromptYesNoAsync("  Subscribe to quotes", defaultValue: false, ct: ct);

        return new PolygonOptions(
            ApiKey: apiKey,
            SubscribeTrades: subscribeTrades,
            SubscribeQuotes: subscribeQuotes
        );
    }

    private async Task<IBOptions?> ConfigureIBOptionsAsync(CancellationToken ct)
    {
        PrintLine("\n  Interactive Brokers Configuration:");
        PrintLine("  Note: IB requires TWS or IB Gateway to be running.\n");

        var host = await PromptStringAsync("  TWS/Gateway Host", defaultValue: "127.0.0.1", ct: ct);
        var portStr = await PromptStringAsync("  Port (7496=live, 7497=paper)", defaultValue: "7497", ct: ct);
        var port = int.TryParse(portStr, out var p) ? p : 7497;

        var clientIdStr = await PromptStringAsync("  Client ID", defaultValue: "0", ct: ct);
        var clientId = int.TryParse(clientIdStr, out var c) ? c : 0;

        var subscribeDepth = await PromptYesNoAsync("  Subscribe to market depth (Level 2)", defaultValue: true, ct: ct);

        return new IBOptions(
            Host: host ?? "127.0.0.1",
            Port: port,
            ClientId: clientId,
            UsePaperTrading: port == 7497,
            SubscribeDepth: subscribeDepth
        );
    }

    private async Task<SymbolConfig[]> ConfigureSymbolsStepAsync(UseCase useCase, CancellationToken ct)
    {
        PrintStep(6, "Configure Symbols");

        PrintLine("\nSelect symbol preset or enter custom symbols:\n");
        PrintLine("  1. US Major Indices (SPY, QQQ, DIA, IWM)");
        PrintLine("  2. Tech Giants (AAPL, MSFT, GOOGL, AMZN, META, NVDA)");
        PrintLine("  3. S&P 500 Top 20");
        PrintLine("  4. Crypto (BTC/USD, ETH/USD, SOL/USD)");
        PrintLine("  5. Custom symbols");

        var choice = await PromptChoiceAsync("Select preset", 1, 5, 1, ct);

        if (choice == 5)
        {
            return await ConfigureCustomSymbolsAsync(ct);
        }

        var preset = choice switch
        {
            1 => SymbolPreset.USMajorIndices,
            2 => SymbolPreset.TechGiants,
            3 => SymbolPreset.SP500Top20,
            4 => SymbolPreset.Crypto,
            _ => SymbolPreset.USMajorIndices
        };

        var symbols = GetPresetSymbols(preset);

        PrintLine($"\nSelected {symbols.Length} symbols:");
        PrintLine($"  {string.Join(", ", symbols.Take(10).Select(s => s.Symbol))}");
        if (symbols.Length > 10)
            PrintLine($"  ... and {symbols.Length - 10} more");

        var subscribeDepth = useCase != UseCase.BackfillOnly &&
                             await PromptYesNoAsync("\nSubscribe to market depth for these symbols", defaultValue: false, ct: ct);

        if (subscribeDepth)
        {
            var depthStr = await PromptStringAsync("Depth levels (1-50)", defaultValue: "10", ct: ct);
            var depthLevels = int.TryParse(depthStr, out var d) ? Math.Clamp(d, 1, 50) : 10;

            symbols = symbols.Select(s => s with { SubscribeDepth = true, DepthLevels = depthLevels }).ToArray();
        }

        return symbols;
    }

    private async Task<SymbolConfig[]> ConfigureCustomSymbolsAsync(CancellationToken ct)
    {
        PrintLine("\n  Enter symbols separated by commas (e.g., SPY,AAPL,MSFT):");
        var input = await PromptStringAsync("Symbols", required: true, ct: ct);

        var symbolNames = input?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            ?? new[] { "SPY" };

        var subscribeTrades = await PromptYesNoAsync("Subscribe to trades", defaultValue: true, ct: ct);
        var subscribeDepth = await PromptYesNoAsync("Subscribe to market depth", defaultValue: false, ct: ct);

        var depthLevels = 10;
        if (subscribeDepth)
        {
            var depthStr = await PromptStringAsync("Depth levels (1-50)", defaultValue: "10", ct: ct);
            depthLevels = int.TryParse(depthStr, out var d) ? Math.Clamp(d, 1, 50) : 10;
        }

        return symbolNames.Select(s => new SymbolConfig(
            Symbol: s.ToUpperInvariant(),
            SubscribeTrades: subscribeTrades,
            SubscribeDepth: subscribeDepth,
            DepthLevels: depthLevels
        )).ToArray();
    }

    private async Task<StorageConfig> ConfigureStorageStepAsync(UseCase useCase, CancellationToken ct)
    {
        PrintStep(7, "Configure Storage");

        PrintLine("\n  Storage profiles provide pre-configured settings for common use cases.\n");

        // Display available profiles
        var presets = StorageProfilePresets.GetPresets();
        PrintLine("  Available storage profiles:");
        for (int i = 0; i < presets.Count; i++)
        {
            var preset = presets[i];
            var isDefault = preset.Id == StorageProfilePresets.DefaultProfile;
            var marker = isDefault ? " (recommended)" : "";
            PrintLine($"    {i + 1}. {preset.Label}{marker}");
            PrintLine($"       {preset.Description}");
        }
        PrintLine($"    {presets.Count + 1}. Custom - configure individual settings manually");

        // Get default choice based on use case
        var defaultChoice = useCase switch
        {
            UseCase.Development => 1, // Research
            UseCase.Research => 1, // Research
            UseCase.RealTimeTrading => 2, // LowLatency
            UseCase.Production => 3, // Archival
            _ => 1 // Research as fallback
        };

        var profileChoice = await PromptChoiceAsync("Select profile", 1, presets.Count + 1, defaultChoice, ct);

        // Custom configuration path
        if (profileChoice > presets.Count)
        {
            return await ConfigureStorageAdvancedAsync(useCase, ct);
        }

        // Use selected profile
        var selectedProfile = presets[profileChoice - 1];
        PrintLine($"\n  Selected profile: {selectedProfile.Label}");

        // Offer advanced overrides
        var customize = await PromptYesNoAsync("\nCustomize advanced settings", defaultValue: false, ct: ct);
        if (customize)
        {
            return await ConfigureStorageAdvancedAsync(useCase, ct, selectedProfile.Id);
        }

        // Use profile defaults with use-case-specific retention
        int? retentionDays = useCase switch
        {
            UseCase.Development => 30,
            UseCase.Research => null,
            UseCase.Production => 365,
            _ => null
        };

        return new StorageConfig(
            Profile: selectedProfile.Id,
            RetentionDays: retentionDays
        );
    }

    private async Task<StorageConfig> ConfigureStorageAdvancedAsync(UseCase useCase, CancellationToken ct, string? baseProfile = null)
    {
        PrintLine("\n  Advanced storage configuration:\n");

        PrintLine("  Naming convention:");
        PrintLine("    1. BySymbol - data/SPY/trades/2024-01-15.jsonl");
        PrintLine("    2. ByDate - data/2024-01-15/SPY/trades.jsonl");
        PrintLine("    3. ByType - data/trades/SPY/2024-01-15.jsonl");
        PrintLine("    4. Flat - data/SPY_trades_2024-01-15.jsonl");

        var namingChoice = await PromptChoiceAsync("Select naming", 1, 4, 1, ct);
        var naming = namingChoice switch
        {
            1 => "BySymbol",
            2 => "ByDate",
            3 => "ByType",
            4 => "Flat",
            _ => "BySymbol"
        };

        PrintLine("\n  Date partitioning:");
        PrintLine("    1. Daily - new file each day");
        PrintLine("    2. Hourly - new file each hour");
        PrintLine("    3. Monthly - new file each month");
        PrintLine("    4. None - single file per symbol/type");

        var partitionChoice = await PromptChoiceAsync("Select partitioning", 1, 4, 1, ct);
        var partition = partitionChoice switch
        {
            1 => "Daily",
            2 => "Hourly",
            3 => "Monthly",
            4 => "None",
            _ => "Daily"
        };

        // Retention policy based on use case
        int? retentionDays = useCase switch
        {
            UseCase.Development => 30,
            UseCase.Research => null,
            UseCase.Production => 365,
            _ => null
        };

        if (useCase != UseCase.Development)
        {
            var setRetention = await PromptYesNoAsync("\nSet data retention policy", defaultValue: false, ct: ct);
            if (setRetention)
            {
                var daysStr = await PromptStringAsync("Retention days (e.g., 365)", defaultValue: "365", ct: ct);
                retentionDays = int.TryParse(daysStr, out var d) ? d : null;
            }
        }
        else
        {
            PrintLine($"\n  Using {retentionDays}-day retention for development.");
        }

        return new StorageConfig(
            NamingConvention: naming,
            DatePartition: partition,
            RetentionDays: retentionDays,
            Profile: baseProfile // Preserve base profile if user wanted to customize on top of it
        );
    }

    private async Task<BackfillConfig?> ConfigureBackfillStepAsync(
        IReadOnlyList<AutoConfigurationService.DetectedProvider> providers,
        UseCase useCase,
        CancellationToken ct)
    {
        PrintStep(8, "Configure Historical Data (Backfill)");

        if (useCase == UseCase.RealTimeTrading)
        {
            var enableBackfill = await PromptYesNoAsync("\nEnable historical data backfill", defaultValue: false, ct: ct);
            if (!enableBackfill)
            {
                PrintLine("  Skipping backfill configuration.");
                return null;
            }
        }

        PrintLine("\n  Backfill providers allow fetching historical market data.");

        var historicalProviders = providers
            .Where(p => p.Capabilities.Contains("Historical"))
            .OrderBy(p => p.SuggestedPriority)
            .ToList();

        PrintLine("\n  Available providers (in priority order):");
        foreach (var provider in historicalProviders)
        {
            var status = provider.HasCredentials ? "[OK]" : "[--]";
            PrintLine($"    {status} {provider.DisplayName}");
        }

        var configuredProviders = historicalProviders.Where(p => p.HasCredentials).ToList();

        if (configuredProviders.Count == 0)
        {
            PrintLine("\n  No premium providers configured. Using free providers (Yahoo, Stooq).");
        }

        var priority = configuredProviders.Select(p => p.Name.ToLowerInvariant()).ToList();
        priority.Add("yahoo");
        priority.Add("stooq");

        var enableRotation = await PromptYesNoAsync("\nEnable automatic provider rotation on rate limits", defaultValue: true, ct: ct);

        return new BackfillConfig(
            Enabled: useCase == UseCase.BackfillOnly || useCase == UseCase.Research,
            Provider: "composite",
            EnableFallback: true,
            EnableRateLimitRotation: enableRotation,
            ProviderPriority: priority.Distinct().ToArray()
        );
    }

    private AppConfig BuildConfiguration(
        DataSourceSelection dataSource,
        SymbolConfig[] symbols,
        StorageConfig storage,
        BackfillConfig? backfill)
    {
        return new AppConfig(
            DataRoot: "data",
            Compress: true,
            DataSource: dataSource.DataSource,
            Alpaca: dataSource.Alpaca,
            IB: dataSource.IB,
            Polygon: dataSource.Polygon,
            Storage: storage,
            Symbols: symbols,
            Backfill: backfill
        );
    }

    private async Task<bool> ReviewConfigurationStepAsync(AppConfig config, CancellationToken ct)
    {
        PrintStep(9, "Review Configuration");

        var json = JsonSerializer.Serialize(config, JsonOptions);

        PrintLine("\nGenerated configuration:\n");
        PrintLine("```json");
        PrintLine(json);
        PrintLine("```");

        return await PromptYesNoAsync("\nSave this configuration", defaultValue: true, ct: ct);
    }

    private async Task<string> SaveConfigurationStepAsync(AppConfig config, CancellationToken ct)
    {
        PrintStep(10, "Save Configuration");

        var configDir = "config";
        if (!Directory.Exists(configDir))
        {
            Directory.CreateDirectory(configDir);
        }

        var configPath = Path.Combine(configDir, "appsettings.json");

        // Check for existing config
        if (File.Exists(configPath))
        {
            var overwrite = await PromptYesNoAsync($"\n{configPath} already exists. Overwrite", defaultValue: false, ct: ct);
            if (!overwrite)
            {
                var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
                configPath = Path.Combine(configDir, $"appsettings.{timestamp}.json");
                PrintLine($"  Saving to: {configPath}");
            }
        }

        SaveConfiguration(config, configPath);
        return configPath;
    }

    private void SaveConfiguration(AppConfig config, string path)
    {
        var json = JsonSerializer.Serialize(config, JsonOptions);
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        File.WriteAllText(path, json);
        _log.Information("Configuration saved to {Path}", path);
    }

    private static SymbolConfig[] GetPresetSymbols(SymbolPreset preset)
    {
        return preset switch
        {
            SymbolPreset.USMajorIndices => new[]
            {
                new SymbolConfig("SPY", SubscribeTrades: true, SubscribeDepth: false),
                new SymbolConfig("QQQ", SubscribeTrades: true, SubscribeDepth: false),
                new SymbolConfig("DIA", SubscribeTrades: true, SubscribeDepth: false),
                new SymbolConfig("IWM", SubscribeTrades: true, SubscribeDepth: false)
            },
            SymbolPreset.TechGiants => new[]
            {
                new SymbolConfig("AAPL", SubscribeTrades: true, SubscribeDepth: false),
                new SymbolConfig("MSFT", SubscribeTrades: true, SubscribeDepth: false),
                new SymbolConfig("GOOGL", SubscribeTrades: true, SubscribeDepth: false),
                new SymbolConfig("AMZN", SubscribeTrades: true, SubscribeDepth: false),
                new SymbolConfig("META", SubscribeTrades: true, SubscribeDepth: false),
                new SymbolConfig("NVDA", SubscribeTrades: true, SubscribeDepth: false)
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
                new SymbolConfig("BTC/USD", SubscribeTrades: true, SubscribeDepth: false),
                new SymbolConfig("ETH/USD", SubscribeTrades: true, SubscribeDepth: false),
                new SymbolConfig("SOL/USD", SubscribeTrades: true, SubscribeDepth: false)
            },
            _ => new[]
            {
                new SymbolConfig("SPY", SubscribeTrades: true, SubscribeDepth: false)
            }
        };
    }

    private async Task CredentialGuidanceStepAsync(
        IReadOnlyList<AutoConfigurationService.DetectedProvider> providers,
        CancellationToken ct)
    {
        var configuredCount = providers.Count(p => p.HasCredentials);

        // Skip if user already has credentials configured
        if (configuredCount > 0)
        {
            return;
        }

        PrintStep(2, "Credential Setup Guide");

        PrintLine("\n  No API credentials detected. Here's how to get started:");
        PrintLine("  Most providers offer free tiers that are perfect for getting started.\n");

        PrintLine("  Popular providers with free tiers:");

        foreach (var (name, info) in ProviderSignupInfo.Take(3))
        {
            PrintLine($"\n    {name}:");
            PrintLine($"      {info.FreeTier}");
            PrintLine($"      Sign up:  {info.SignupUrl}");
        }

        PrintLine("\n  Once you have an API key, set it as an environment variable:");
        PrintLine("  (Copy and paste the relevant lines below)\n");
        PrintLine("    # Alpaca (recommended for real-time data)");
        PrintLine("    export ALPACA_KEY_ID=your-key-id");
        PrintLine("    export ALPACA_SECRET_KEY=your-secret-key\n");
        PrintLine("    # Polygon");
        PrintLine("    export POLYGON_API_KEY=your-api-key\n");
        PrintLine("    # Tiingo (historical data)");
        PrintLine("    export TIINGO_API_TOKEN=your-token\n");

        PrintLine("  Tip: Add these to your ~/.bashrc or ~/.zshrc to persist them.");

        var continueSetup = await PromptYesNoAsync(
            "\n  Continue setup without credentials? (You can add them later)",
            defaultValue: true, ct: ct);

        if (!continueSetup)
        {
            PrintLine("\n  Set your environment variables and re-run: Meridian --wizard");
            throw new OperationCanceledException();
        }
    }

    private async Task ValidateCredentialsStepAsync(DataSourceSelection dataSource, CancellationToken ct)
    {
        PrintStep(5, "Validate Credentials");

        var hasCredentials = dataSource.DataSource switch
        {
            DataSourceKind.Alpaca => !string.IsNullOrWhiteSpace(dataSource.Alpaca?.KeyId) &&
                                     !string.IsNullOrWhiteSpace(dataSource.Alpaca?.SecretKey),
            DataSourceKind.Polygon => !string.IsNullOrWhiteSpace(dataSource.Polygon?.ApiKey),
            DataSourceKind.IB => true, // IB uses local connection, no API key
            _ => true
        };

        if (!hasCredentials)
        {
            PrintWarning("  No credentials configured for the selected provider.");
            PrintLine("  You can still save the configuration and add credentials later.");
            PrintLine("  The collector will not be able to connect until credentials are set.\n");

            ShowCredentialHelp(dataSource.DataSource.ToString());
            return;
        }

        // Skip validation for IB (requires TWS running) and StockSharp
        if (dataSource.DataSource == DataSourceKind.IB ||
            dataSource.DataSource == DataSourceKind.StockSharp)
        {
            PrintLine($"  {dataSource.DataSource} uses a local connection - skipping API validation.");
            return;
        }

        PrintLine("  Validating credentials with provider API...\n");

        try
        {
            await using var validator = new CredentialValidationService();

            CredentialValidationService.ValidationResult? result = dataSource.DataSource switch
            {
                DataSourceKind.Alpaca when dataSource.Alpaca != null =>
                    await validator.ValidateAlpacaAsync(dataSource.Alpaca, ct),
                DataSourceKind.Polygon when dataSource.Polygon != null =>
                    await validator.ValidatePolygonAsync(dataSource.Polygon, ct),
                _ => null
            };

            if (result == null)
            {
                PrintLine("  Skipped - no validation available for this provider.");
                return;
            }

            if (result.IsValid)
            {
                PrintSuccess($"  [OK] {result.Provider}: {result.Message} ({result.ResponseTime.TotalMilliseconds:F0}ms)");
                if (!string.IsNullOrEmpty(result.AccountInfo))
                {
                    PrintLine($"       {result.AccountInfo}");
                }
            }
            else
            {
                PrintWarning($"  [FAIL] {result.Provider}: {result.Message}");
                PrintLine("\n  Your credentials appear to be invalid.");

                var retry = await PromptYesNoAsync("  Would you like to re-enter credentials", defaultValue: false, ct: ct);
                if (retry)
                {
                    // Re-configure the provider
                    switch (dataSource.DataSource)
                    {
                        case DataSourceKind.Alpaca:
                            dataSource.Alpaca = await ConfigureAlpacaOptionsAsync(ct);
                            break;
                        case DataSourceKind.Polygon:
                            dataSource.Polygon = await ConfigurePolygonOptionsAsync(ct);
                            break;
                        case DataSourceKind.Synthetic:
                            PrintLine("\n  Synthetic offline dataset selected. No credentials are required.");
                            break;
                    }

                    // Retry validation
                    await ValidateCredentialsStepAsync(dataSource, ct);
                    return;
                }

                PrintLine("\n  Continuing with current credentials. You can fix them later in config/appsettings.json.");
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            PrintWarning($"  Could not validate credentials: {ex.Message}");
            PrintLine("  This may be a network issue. Continuing with setup...");
        }
    }

    private void ShowCredentialHelp(string providerName)
    {
        if (ProviderSignupInfo.TryGetValue(providerName, out var info))
        {
            PrintLine($"  To get {providerName} credentials:");
            PrintLine($"    1. Sign up at: {info.SignupUrl}");
            PrintLine($"    2. Set environment variables before running the collector");
            PrintLine($"    3. Docs: {info.DocsUrl}");
        }
    }

    private void PrintNextSteps(string savedPath, DataSourceKind dataSource)
    {
        PrintLine();
        PrintSuccess("=".PadRight(60, '='));
        PrintSuccess("  Configuration Complete!");
        PrintSuccess("=".PadRight(60, '='));
        PrintLine();
        PrintLine($"  Configuration saved to: {savedPath}");
        PrintLine();
        PrintLine("  Next steps:");
        PrintLine("  -".PadRight(40, '-'));
        PrintLine();
        PrintLine("  1. Validate your setup (recommended):");
        PrintLine("     dotnet run --project src/Meridian -- --dry-run");
        PrintLine();
        PrintLine("  2. Start collecting data with the web dashboard:");
        PrintLine("     dotnet run --project src/Meridian -- --mode web");
        PrintLine("     Then open http://localhost:8080 in your browser");
        PrintLine();
        PrintLine("  3. Or use quickstart (auto-validates and starts):");
        PrintLine("     dotnet run --project src/Meridian -- --quickstart");
        PrintLine();
        PrintLine("  4. Backfill historical data:");
        PrintLine("     dotnet run --project src/Meridian -- --backfill \\");
        PrintLine("       --backfill-symbols SPY,AAPL --backfill-from 2024-01-01");
        PrintLine();

        // Show credential reminder if needed
        var providerName = dataSource.ToString();
        if (ProviderSignupInfo.ContainsKey(providerName))
        {
            var envVarPrefix = providerName.ToUpperInvariant();
            PrintLine("  Credentials (for production, use environment variables):");
            PrintLine($"    See: docs/providers/{providerName.ToLowerInvariant()}-setup.md");
        }

        PrintLine();
        PrintLine("  Full documentation: docs/HELP.md");
        PrintLine("  Provider setup:     docs/providers/");
        PrintLine("  Troubleshooting:    dotnet run -- --quick-check");
        PrintLine();
    }

    /// <summary>
    /// Runs a quickstart flow: auto-configures, validates, and returns a config ready to launch.
    /// </summary>
    public async Task<WizardResult> RunQuickstartAsync(CancellationToken ct = default)
    {
        PrintLine("=".PadRight(60, '='));
        PrintLine("  Market Data Collector - Quickstart");
        PrintLine("=".PadRight(60, '='));
        PrintLine();

        // Step 1: Auto-detect providers from environment
        PrintLine("[1/4] Detecting providers from environment...");
        var autoResult = _autoConfig.AutoConfigure();

        var configuredProviders = autoResult.DetectedProviders.Where(p => p.HasCredentials).ToList();
        var freeProviders = autoResult.DetectedProviders.Where(p => p.Name is "Yahoo" or "Stooq").ToList();

        PrintLine($"  Found {configuredProviders.Count} configured provider(s)");
        foreach (var p in configuredProviders)
        {
            PrintLine($"    [OK] {p.DisplayName}");
        }

        if (configuredProviders.Count == 0)
        {
            PrintLine("  No API credentials found - using free providers (Yahoo, Stooq) for backfill.");
            PrintLine("  Tip: Set ALPACA_KEY_ID + ALPACA_SECRET_KEY for real-time streaming.\n");
        }

        // Step 2: Generate config
        PrintLine("\n[2/4] Generating configuration...");
        var config = autoResult.Config;
        PrintLine($"  Data source: {config.DataSource}");
        PrintLine($"  Symbols: {string.Join(", ", (config.Symbols ?? []).Select(s => s.Symbol))}");
        PrintLine($"  Storage: {config.Storage?.Profile ?? "default"}");

        // Step 3: Validate credentials if any
        if (configuredProviders.Count > 0)
        {
            PrintLine("\n[3/4] Validating credentials...");
            try
            {
                await using var validator = new CredentialValidationService();
                var validationSummary = await validator.ValidateAllAsync(config, ct);

                foreach (var result in validationSummary.Results)
                {
                    var status = result.IsValid ? "[OK]" : "[FAIL]";
                    PrintLine($"  {status} {result.Provider}: {result.Message} ({result.ResponseTime.TotalMilliseconds:F0}ms)");
                }

                if (!validationSummary.AllValid)
                {
                    PrintWarning("\n  Some credentials failed validation. Check the warnings above.");
                    PrintLine("  The collector may not be able to connect to all providers.\n");
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                PrintWarning($"  Credential validation failed: {ex.Message}");
                PrintLine("  Continuing anyway - this may be a network issue.");
            }
        }
        else
        {
            PrintLine("\n[3/4] Skipping credential validation (no API keys configured).");
        }

        // Step 4: Save config
        PrintLine("\n[4/4] Saving configuration...");
        var configPath = Path.Combine("config", "appsettings.json");
        var configDir = Path.GetDirectoryName(configPath);
        if (!string.IsNullOrEmpty(configDir) && !Directory.Exists(configDir))
        {
            Directory.CreateDirectory(configDir);
        }

        // Back up existing config
        if (File.Exists(configPath))
        {
            var backupPath = configPath + $".backup-{DateTime.Now:yyyyMMdd-HHmmss}";
            File.Copy(configPath, backupPath, overwrite: true);
            PrintLine($"  Backed up existing config to: {backupPath}");
        }

        SaveConfiguration(config, configPath);
        PrintSuccess($"  Configuration saved to: {configPath}");

        PrintLine();
        PrintSuccess("  Quickstart complete! Starting with --mode web will launch the dashboard.");
        PrintLine("  Open http://localhost:8080 in your browser after starting.");
        PrintLine();

        return new WizardResult(Success: true, Config: config, ConfigPath: configPath);
    }

    #region Console Helpers

    private void PrintStep(int step, string title)
    {
        PrintLine();
        PrintLine($"Step {step}: {title}");
        PrintLine("-".PadRight(40, '-'));
    }

    private void PrintLine(string text = "")
    {
        _output.WriteLine(text);
    }

    private void PrintSuccess(string text)
    {
        var originalColor = Console.ForegroundColor;
        try
        {
            Console.ForegroundColor = ConsoleColor.Green;
            _output.WriteLine(text);
        }
        finally
        {
            Console.ForegroundColor = originalColor;
        }
    }

    private void PrintWarning(string text)
    {
        var originalColor = Console.ForegroundColor;
        try
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            _output.WriteLine(text);
        }
        finally
        {
            Console.ForegroundColor = originalColor;
        }
    }

    private async Task<int> PromptChoiceAsync(string prompt, int min, int max, int defaultValue, CancellationToken ct)
    {
        while (true)
        {
            ct.ThrowIfCancellationRequested();

            _output.Write($"\n{prompt} [{min}-{max}] (default: {defaultValue}): ");
            var input = await ReadLineAsync(ct);

            if (string.IsNullOrWhiteSpace(input))
                return defaultValue;

            if (int.TryParse(input, out var value) && value >= min && value <= max)
                return value;

            PrintLine($"  Please enter a number between {min} and {max}");
        }
    }

    private async Task<bool> PromptYesNoAsync(string prompt, bool defaultValue, CancellationToken ct)
    {
        var defaultText = defaultValue ? "Y/n" : "y/N";

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            _output.Write($"{prompt} [{defaultText}]: ");
            var input = await ReadLineAsync(ct);

            if (string.IsNullOrWhiteSpace(input))
                return defaultValue;

            if (input.Equals("y", StringComparison.OrdinalIgnoreCase) ||
                input.Equals("yes", StringComparison.OrdinalIgnoreCase))
                return true;

            if (input.Equals("n", StringComparison.OrdinalIgnoreCase) ||
                input.Equals("no", StringComparison.OrdinalIgnoreCase))
                return false;

            PrintLine("  Please enter 'y' or 'n'");
        }
    }

    private async Task<string?> PromptStringAsync(string prompt, bool required = false, string? defaultValue = null, CancellationToken ct = default)
    {
        var defaultText = defaultValue != null ? $" (default: {defaultValue})" : "";

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            _output.Write($"{prompt}{defaultText}: ");
            var input = await ReadLineAsync(ct);

            if (string.IsNullOrWhiteSpace(input))
            {
                if (defaultValue != null)
                    return defaultValue;
                if (!required)
                    return null;
                PrintLine("  This field is required");
                continue;
            }

            return input;
        }
    }

    private async Task<string?> ReadLineAsync(CancellationToken ct)
    {
        // Simple async readline wrapper
        return await Task.Run(() => _input.ReadLine(), ct);
    }

    #endregion
}

/// <summary>
/// Result of running the configuration wizard.
/// </summary>
public sealed record WizardResult(
    bool Success,
    AppConfig? Config,
    string? ConfigPath
);

/// <summary>
/// Data source selection from wizard.
/// </summary>
internal sealed class DataSourceSelection
{
    public DataSourceKind DataSource { get; set; } = DataSourceKind.IB;
    public AlpacaOptions? Alpaca { get; set; }
    public PolygonOptions? Polygon { get; set; }
    public IBOptions? IB { get; set; }
}
