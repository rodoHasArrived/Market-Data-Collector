using Microsoft.Extensions.Configuration;

namespace MarketDataCollector.Configuration;

/// <summary>
/// Loads and validates simplified configuration.
/// Supports JSON files and environment variable overrides.
/// </summary>
public static class SimplifiedConfigurationLoader
{
    /// <summary>
    /// Loads configuration from appsettings.json with environment variable overrides.
    /// </summary>
    /// <param name="configPath">Path to configuration file (default: appsettings.json)</param>
    /// <returns>Parsed and validated configuration</returns>
    public static SimplifiedAppConfiguration Load(string? configPath = null)
    {
        var basePath = Directory.GetCurrentDirectory();
        var configFile = configPath ?? "appsettings.json";

        // Support config/ subdirectory
        if (!File.Exists(Path.Combine(basePath, configFile)))
        {
            var configDirPath = Path.Combine(basePath, "config", configFile);
            if (File.Exists(configDirPath))
            {
                basePath = Path.Combine(basePath, "config");
            }
        }

        var builder = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile(configFile, optional: false, reloadOnChange: false)
            .AddJsonFile($"{Path.GetFileNameWithoutExtension(configFile)}.local.json", optional: true)
            .AddEnvironmentVariables();

        var root = builder.Build();

        // Parse application settings with env var overrides
        var appSettings = new ApplicationSettings(
            HttpPort: GetInt(root, "Application:HttpPort", "HTTP_PORT", 8080),
            LogLevel: GetString(root, "Application:LogLevel", "LOG_LEVEL", "Information")
        );

        // Parse data path with env var override
        var dataPath = GetString(root, "DataPath", "DATA_PATH", "./data");

        // Parse storage configuration
        var storage = new StorageConfiguration(
            Type: GetString(root, "Storage:Type", null, "SQLite"),
            Path: GetString(root, "Storage:Path", null, Path.Combine(dataPath, "market_data.db"))
        );

        // Parse providers
        var providers = new List<ProviderConfiguration>();
        var providersSection = root.GetSection("Providers");

        foreach (var providerSection in providersSection.GetChildren())
        {
            var provider = ParseProvider(providerSection);
            if (provider != null)
            {
                providers.Add(provider);
            }
        }

        return new SimplifiedAppConfiguration(
            Application: appSettings,
            DataPath: dataPath,
            Providers: providers,
            Storage: storage
        );
    }

    /// <summary>
    /// Validates configuration and throws if invalid.
    /// </summary>
    public static void Validate(SimplifiedAppConfiguration config)
    {
        var errors = new List<string>();

        // Must have at least one provider
        if (config.Providers.Count == 0)
        {
            errors.Add("At least one provider must be configured");
        }

        // Must have at least one enabled provider
        if (!config.EnabledProviders.Any())
        {
            errors.Add("At least one provider must be enabled");
        }

        // Validate each enabled provider
        foreach (var provider in config.EnabledProviders)
        {
            ValidateProvider(provider, errors);
        }

        // Validate data path is writable
        try
        {
            Directory.CreateDirectory(config.DataPath);
        }
        catch (Exception ex)
        {
            errors.Add($"Cannot create data directory '{config.DataPath}': {ex.Message}");
        }

        if (errors.Count > 0)
        {
            throw new ConfigurationValidationException(errors);
        }
    }

    private static void ValidateProvider(ProviderConfiguration provider, List<string> errors)
    {
        // Must have symbols
        if (!provider.GetAllSymbols().Any())
        {
            errors.Add($"Provider '{provider.Name}' has no symbols configured");
        }

        // Validate provider-specific requirements
        switch (provider.Type)
        {
            case "PythonSubprocess":
                if (string.IsNullOrEmpty(provider.ScriptPath))
                {
                    errors.Add($"Provider '{provider.Name}' requires ScriptPath for PythonSubprocess type");
                }
                else if (!File.Exists(provider.ScriptPath))
                {
                    errors.Add($"Provider '{provider.Name}' script not found: {provider.ScriptPath}");
                }
                break;

            case "InteractiveBrokers":
                // Validate IB environment variables
                if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("IB_HOST")))
                {
                    // Warning, not error - has default
                }
                break;
        }

        // Validate credentials based on provider name
        switch (provider.Name.ToLowerInvariant())
        {
            case "alpaca":
                if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ALPACA_KEY_ID")))
                {
                    errors.Add("Alpaca enabled but ALPACA_KEY_ID environment variable not set");
                }
                if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ALPACA_SECRET_KEY")))
                {
                    errors.Add("Alpaca enabled but ALPACA_SECRET_KEY environment variable not set");
                }
                break;

            case "polygon":
                if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("POLYGON_API_KEY")))
                {
                    errors.Add("Polygon enabled but POLYGON_API_KEY environment variable not set");
                }
                break;

            case "tiingo":
                if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TIINGO_API_TOKEN")))
                {
                    errors.Add("Tiingo enabled but TIINGO_API_TOKEN environment variable not set");
                }
                break;

            case "finnhub":
                if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("FINNHUB_API_KEY")))
                {
                    errors.Add("Finnhub enabled but FINNHUB_API_KEY environment variable not set");
                }
                break;
        }
    }

    private static ProviderConfiguration? ParseProvider(IConfigurationSection section)
    {
        var name = section["Name"];
        var type = section["Type"];

        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(type))
            return null;

        // Parse simple symbols
        var symbols = section.GetSection("Symbols")
            .GetChildren()
            .Select(s => s.Value)
            .Where(s => !string.IsNullOrEmpty(s))
            .Cast<string>()
            .ToList();

        // Parse detailed symbols (for IB, etc.)
        var symbolDetails = section.GetSection("Symbols")
            .GetChildren()
            .Where(s => s.GetSection("Symbol").Exists())
            .Select(s => new SymbolConfiguration(
                Symbol: s["Symbol"] ?? "",
                SecurityType: s["SecurityType"] ?? "STK",
                Exchange: s["Exchange"] ?? "SMART",
                Currency: s["Currency"] ?? "USD",
                Expiry: s["Expiry"],
                Strike: decimal.TryParse(s["Strike"], out var strike) ? strike : null,
                Right: s["Right"]
            ))
            .Where(s => !string.IsNullOrEmpty(s.Symbol))
            .ToList();

        return new ProviderConfiguration
        {
            Name = name,
            Type = type,
            Enabled = bool.TryParse(section["Enabled"], out var enabled) && enabled,
            ScriptPath = section["ScriptPath"],
            Symbols = symbols.Count > 0 ? symbols : null,
            SymbolDetails = symbolDetails.Count > 0 ? symbolDetails : null
        };
    }

    private static string GetString(IConfiguration config, string key, string? envVar, string defaultValue)
    {
        // Environment variable takes precedence
        if (!string.IsNullOrEmpty(envVar))
        {
            var envValue = Environment.GetEnvironmentVariable(envVar);
            if (!string.IsNullOrEmpty(envValue))
                return envValue;
        }

        return config[key] ?? defaultValue;
    }

    private static int GetInt(IConfiguration config, string key, string? envVar, int defaultValue)
    {
        var strValue = GetString(config, key, envVar, defaultValue.ToString());
        return int.TryParse(strValue, out var result) ? result : defaultValue;
    }
}

/// <summary>
/// Exception thrown when configuration validation fails.
/// </summary>
public sealed class ConfigurationValidationException : Exception
{
    public IReadOnlyList<string> Errors { get; }

    public ConfigurationValidationException(IEnumerable<string> errors)
        : base($"Configuration validation failed:\n  - {string.Join("\n  - ", errors)}")
    {
        Errors = errors.ToList();
    }
}
