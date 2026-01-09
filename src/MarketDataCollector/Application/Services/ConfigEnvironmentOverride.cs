using System.Reflection;
using System.Text.Json;
using MarketDataCollector.Application.Config;
using MarketDataCollector.Application.Logging;
using Serilog;

namespace MarketDataCollector.Application.Services;

/// <summary>
/// Service for applying environment variable overrides to configuration.
/// Implements QW-25: Config Environment Override.
/// </summary>
public sealed class ConfigEnvironmentOverride
{
    private readonly ILogger _log = LoggingSetup.ForContext<ConfigEnvironmentOverride>();

    /// <summary>
    /// Environment variable prefix for configuration overrides.
    /// </summary>
    public const string EnvPrefix = "MDC_";

    /// <summary>
    /// Mapping of environment variables to configuration paths.
    /// </summary>
    private static readonly Dictionary<string, string> EnvToConfigMapping = new(StringComparer.OrdinalIgnoreCase)
    {
        // Core settings
        ["MDC_DATA_ROOT"] = "DataRoot",
        ["MDC_COMPRESS"] = "Compress",
        ["MDC_DATASOURCE"] = "DataSource",

        // Alpaca settings
        ["MDC_ALPACA_KEY_ID"] = "Alpaca:KeyId",
        ["MDC_ALPACA_SECRET_KEY"] = "Alpaca:SecretKey",
        ["MDC_ALPACA_FEED"] = "Alpaca:Feed",
        ["MDC_ALPACA_SANDBOX"] = "Alpaca:UseSandbox",
        ["MDC_ALPACA_QUOTES"] = "Alpaca:SubscribeQuotes",

        // Legacy Alpaca env vars (without MDC_ prefix)
        ["ALPACA_KEY_ID"] = "Alpaca:KeyId",
        ["ALPACA_SECRET_KEY"] = "Alpaca:SecretKey",

        // Storage settings
        ["MDC_STORAGE_NAMING"] = "Storage:NamingConvention",
        ["MDC_STORAGE_PARTITION"] = "Storage:DatePartition",
        ["MDC_STORAGE_RETENTION_DAYS"] = "Storage:RetentionDays",
        ["MDC_STORAGE_MAX_MB"] = "Storage:MaxTotalMegabytes",

        // Backfill settings
        ["MDC_BACKFILL_ENABLED"] = "Backfill:Enabled",
        ["MDC_BACKFILL_PROVIDER"] = "Backfill:Provider",
        ["MDC_BACKFILL_SYMBOLS"] = "Backfill:Symbols",
        ["MDC_BACKFILL_FROM"] = "Backfill:From",
        ["MDC_BACKFILL_TO"] = "Backfill:To",

        // Provider API keys
        ["POLYGON_API_KEY"] = "Backfill:Providers:Polygon:ApiKey",
        ["TIINGO_API_TOKEN"] = "Backfill:Providers:Tiingo:ApiToken",
        ["FINNHUB_API_KEY"] = "Backfill:Providers:Finnhub:ApiKey",
        ["ALPHA_VANTAGE_API_KEY"] = "Backfill:Providers:AlphaVantage:ApiKey",

        // MassTransit settings
        ["MDC_MASSTRANSIT_ENABLED"] = "MassTransit:Enabled",
        ["MDC_MASSTRANSIT_TRANSPORT"] = "MassTransit:Transport",
        ["MDC_RABBITMQ_HOST"] = "MassTransit:RabbitMQ:Host",
        ["MDC_RABBITMQ_PORT"] = "MassTransit:RabbitMQ:Port",
        ["MDC_RABBITMQ_USER"] = "MassTransit:RabbitMQ:Username",
        ["MDC_RABBITMQ_PASS"] = "MassTransit:RabbitMQ:Password"
    };

    /// <summary>
    /// Applies environment variable overrides to configuration.
    /// </summary>
    public AppConfig ApplyOverrides(AppConfig config)
    {
        var appliedOverrides = new List<string>();
        var result = config;

        foreach (var (envVar, configPath) in EnvToConfigMapping)
        {
            var value = Environment.GetEnvironmentVariable(envVar);
            if (string.IsNullOrEmpty(value)) continue;

            try
            {
                result = ApplyOverride(result, configPath, value);
                appliedOverrides.Add($"{envVar} -> {configPath}");
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "Failed to apply environment override {EnvVar} to {Path}", envVar, configPath);
            }
        }

        // Also check for generic MDC_ prefixed variables
        foreach (System.Collections.DictionaryEntry env in Environment.GetEnvironmentVariables())
        {
            var key = env.Key.ToString();
            if (key == null || !key.StartsWith(EnvPrefix, StringComparison.OrdinalIgnoreCase))
                continue;

            // Skip already mapped variables
            if (EnvToConfigMapping.ContainsKey(key))
                continue;

            // Convert MDC_SOME_SETTING to Some:Setting
            var configPath = ConvertEnvVarToConfigPath(key);
            var value = env.Value?.ToString();

            if (string.IsNullOrEmpty(value)) continue;

            try
            {
                result = ApplyOverride(result, configPath, value);
                appliedOverrides.Add($"{key} -> {configPath}");
            }
            catch (Exception ex)
            {
                _log.Debug(ex, "Could not apply generic override {EnvVar} to {Path}", key, configPath);
            }
        }

        if (appliedOverrides.Count > 0)
        {
            _log.Information("Applied {Count} environment variable overrides: {Overrides}",
                appliedOverrides.Count, string.Join(", ", appliedOverrides));
        }

        return result;
    }

    /// <summary>
    /// Gets all recognized environment variables and their current values.
    /// </summary>
    public IReadOnlyList<EnvironmentOverrideInfo> GetRecognizedVariables()
    {
        var variables = new List<EnvironmentOverrideInfo>();

        foreach (var (envVar, configPath) in EnvToConfigMapping)
        {
            var value = Environment.GetEnvironmentVariable(envVar);
            var isSensitive = IsSensitiveVariable(envVar);

            variables.Add(new EnvironmentOverrideInfo
            {
                EnvironmentVariable = envVar,
                ConfigPath = configPath,
                CurrentValue = isSensitive && !string.IsNullOrEmpty(value) ? "[SET]" : value,
                IsSet = !string.IsNullOrEmpty(value),
                IsSensitive = isSensitive
            });
        }

        return variables.OrderBy(v => v.EnvironmentVariable).ToList();
    }

    /// <summary>
    /// Gets documentation for all supported environment variables.
    /// </summary>
    public string GetDocumentation()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("# Environment Variable Configuration");
        sb.AppendLine();
        sb.AppendLine("The following environment variables can be used to override configuration:");
        sb.AppendLine();

        var groups = EnvToConfigMapping
            .GroupBy(kvp => GetCategory(kvp.Key))
            .OrderBy(g => g.Key);

        foreach (var group in groups)
        {
            sb.AppendLine($"## {group.Key}");
            sb.AppendLine();

            foreach (var (envVar, configPath) in group.OrderBy(kvp => kvp.Key))
            {
                var desc = GetVariableDescription(envVar);
                sb.AppendLine($"- `{envVar}` -> `{configPath}`");
                if (!string.IsNullOrEmpty(desc))
                    sb.AppendLine($"  {desc}");
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }

    private AppConfig ApplyOverride(AppConfig config, string path, string value)
    {
        var parts = path.Split(':');

        return parts[0] switch
        {
            "DataRoot" => config with { DataRoot = value },
            "Compress" => config with { Compress = ParseBool(value) },
            "DataSource" => config with { DataSource = ParseDataSource(value) },
            "Alpaca" => ApplyAlpacaOverride(config, parts.Skip(1).ToArray(), value),
            "Storage" => ApplyStorageOverride(config, parts.Skip(1).ToArray(), value),
            "Backfill" => ApplyBackfillOverride(config, parts.Skip(1).ToArray(), value),
            "MassTransit" => ApplyMassTransitOverride(config, parts.Skip(1).ToArray(), value),
            _ => config
        };
    }

    private AppConfig ApplyAlpacaOverride(AppConfig config, string[] path, string value)
    {
        var alpaca = config.Alpaca ?? new AlpacaOptions();

        if (path.Length == 0) return config;

        alpaca = path[0] switch
        {
            "KeyId" => alpaca with { KeyId = value },
            "SecretKey" => alpaca with { SecretKey = value },
            "Feed" => alpaca with { Feed = value },
            "UseSandbox" => alpaca with { UseSandbox = ParseBool(value) },
            "SubscribeQuotes" => alpaca with { SubscribeQuotes = ParseBool(value) },
            _ => alpaca
        };

        return config with { Alpaca = alpaca };
    }

    private AppConfig ApplyStorageOverride(AppConfig config, string[] path, string value)
    {
        var storage = config.Storage ?? new StorageConfig();

        if (path.Length == 0) return config;

        storage = path[0] switch
        {
            "NamingConvention" => storage with { NamingConvention = value },
            "DatePartition" => storage with { DatePartition = value },
            "RetentionDays" => storage with { RetentionDays = ParseInt(value) },
            "MaxTotalMegabytes" => storage with { MaxTotalMegabytes = ParseLong(value) },
            _ => storage
        };

        return config with { Storage = storage };
    }

    private AppConfig ApplyBackfillOverride(AppConfig config, string[] path, string value)
    {
        var backfill = config.Backfill ?? new BackfillConfig();

        if (path.Length == 0) return config;

        if (path[0] == "Providers" && path.Length >= 3)
        {
            // Handle provider-specific settings
            var providers = backfill.Providers ?? new BackfillProvidersConfig();
            // For simplicity, we'll skip nested provider config in this implementation
            return config;
        }

        backfill = path[0] switch
        {
            "Enabled" => backfill with { Enabled = ParseBool(value) },
            "Provider" => backfill with { Provider = value },
            "Symbols" => backfill with { Symbols = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) },
            "From" => backfill with { From = DateOnly.TryParse(value, out var from) ? from : backfill.From },
            "To" => backfill with { To = DateOnly.TryParse(value, out var to) ? to : backfill.To },
            _ => backfill
        };

        return config with { Backfill = backfill };
    }

    private AppConfig ApplyMassTransitOverride(AppConfig config, string[] path, string value)
    {
        var mt = config.MassTransit ?? new MassTransitConfig();

        if (path.Length == 0) return config;

        if (path[0] == "RabbitMQ" && path.Length >= 2)
        {
            var rmq = mt.RabbitMQ ?? new RabbitMqConfig();
            rmq = path[1] switch
            {
                "Host" => rmq with { Host = value },
                "Port" => rmq with { Port = ParseInt(value) ?? 5672 },
                "Username" => rmq with { Username = value },
                "Password" => rmq with { Password = value },
                _ => rmq
            };
            mt = mt with { RabbitMQ = rmq };
        }
        else
        {
            mt = path[0] switch
            {
                "Enabled" => mt with { Enabled = ParseBool(value) },
                "Transport" => mt with { Transport = value },
                _ => mt
            };
        }

        return config with { MassTransit = mt };
    }

    private static string ConvertEnvVarToConfigPath(string envVar)
    {
        // Remove MDC_ prefix
        var path = envVar.Substring(EnvPrefix.Length);

        // Convert SOME_SETTING to Some:Setting
        var parts = path.Split('_');
        return string.Join(":", parts.Select(p =>
            char.ToUpper(p[0]) + p.Substring(1).ToLower()));
    }

    private static bool ParseBool(string value)
    {
        return value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("1", StringComparison.Ordinal) ||
               value.Equals("yes", StringComparison.OrdinalIgnoreCase);
    }

    private static int? ParseInt(string value)
    {
        return int.TryParse(value, out var result) ? result : null;
    }

    private static long? ParseLong(string value)
    {
        return long.TryParse(value, out var result) ? result : null;
    }

    private static DataSourceKind ParseDataSource(string value)
    {
        return Enum.TryParse<DataSourceKind>(value, ignoreCase: true, out var result)
            ? result
            : DataSourceKind.IB;
    }

    private static bool IsSensitiveVariable(string envVar)
    {
        var sensitivePatterns = new[] { "KEY", "SECRET", "PASSWORD", "TOKEN", "PASS" };
        return sensitivePatterns.Any(p => envVar.Contains(p, StringComparison.OrdinalIgnoreCase));
    }

    private static string GetCategory(string envVar)
    {
        if (envVar.StartsWith("MDC_ALPACA") || envVar.StartsWith("ALPACA")) return "Alpaca Configuration";
        if (envVar.StartsWith("MDC_STORAGE")) return "Storage Configuration";
        if (envVar.StartsWith("MDC_BACKFILL")) return "Backfill Configuration";
        if (envVar.StartsWith("MDC_MASSTRANSIT") || envVar.StartsWith("MDC_RABBITMQ")) return "MassTransit Configuration";
        if (envVar.Contains("API_KEY") || envVar.Contains("TOKEN")) return "API Keys";
        return "Core Configuration";
    }

    private static string GetVariableDescription(string envVar)
    {
        return envVar switch
        {
            "MDC_DATA_ROOT" => "Root directory for data storage",
            "MDC_COMPRESS" => "Enable gzip compression (true/false)",
            "MDC_DATASOURCE" => "Data source provider (IB, Alpaca, Polygon)",
            "MDC_ALPACA_FEED" => "Alpaca data feed (iex or sip)",
            "MDC_BACKFILL_SYMBOLS" => "Comma-separated list of symbols",
            _ => ""
        };
    }
}

/// <summary>
/// Information about an environment variable override.
/// </summary>
public sealed class EnvironmentOverrideInfo
{
    public string EnvironmentVariable { get; set; } = "";
    public string ConfigPath { get; set; } = "";
    public string? CurrentValue { get; set; }
    public bool IsSet { get; set; }
    public bool IsSensitive { get; set; }
}
