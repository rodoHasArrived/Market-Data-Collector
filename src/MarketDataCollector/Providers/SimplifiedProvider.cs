using MarketDataCollector.Configuration;

namespace MarketDataCollector.Providers;

/// <summary>
/// Simplified market data provider interface.
/// Replaces complex IMarketDataClient hierarchy with minimal contract.
/// </summary>
public interface ISimplifiedMarketDataProvider : IAsyncDisposable
{
    /// <summary>
    /// Provider name (e.g., "alpaca", "polygon", "ib").
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Connects to the data source.
    /// </summary>
    Task ConnectAsync(CancellationToken ct = default);

    /// <summary>
    /// Disconnects from the data source.
    /// </summary>
    Task DisconnectAsync(CancellationToken ct = default);

    /// <summary>
    /// Streams market data for the specified symbols.
    /// Returns an async enumerable that yields data as it arrives.
    /// </summary>
    IAsyncEnumerable<SimplifiedMarketData> StreamAsync(
        IEnumerable<string> symbols,
        CancellationToken ct = default);

    /// <summary>
    /// Gets the current health status of the provider.
    /// </summary>
    Task<ProviderHealthStatus> GetHealthAsync(CancellationToken ct = default);
}

/// <summary>
/// Provider health status.
/// </summary>
public sealed record ProviderHealthStatus(
    bool IsHealthy,
    string? ErrorMessage = null,
    DateTime? LastSuccessfulOperation = null,
    int? PendingOperations = null
)
{
    public static ProviderHealthStatus Healthy(DateTime? lastSuccess = null) =>
        new(true, null, lastSuccess ?? DateTime.UtcNow);

    public static ProviderHealthStatus Unhealthy(string error) =>
        new(false, error);
}

/// <summary>
/// Factory for creating providers from configuration.
/// </summary>
public interface IProviderFactory
{
    /// <summary>
    /// Creates a provider from configuration.
    /// </summary>
    ISimplifiedMarketDataProvider Create(ProviderConfiguration config);

    /// <summary>
    /// Gets supported provider types.
    /// </summary>
    IEnumerable<string> SupportedTypes { get; }
}

/// <summary>
/// Default provider factory implementation.
/// </summary>
public sealed class SimplifiedProviderFactory : IProviderFactory
{
    private readonly IServiceProvider _services;

    public SimplifiedProviderFactory(IServiceProvider services)
    {
        _services = services;
    }

    public IEnumerable<string> SupportedTypes => new[]
    {
        "PythonSubprocess",
        "InteractiveBrokers",
        "Native"
    };

    public ISimplifiedMarketDataProvider Create(ProviderConfiguration config)
    {
        return config.Type switch
        {
            "PythonSubprocess" => CreatePythonSubprocessProvider(config),
            "InteractiveBrokers" => CreateIBProvider(config),
            "Native" => CreateNativeProvider(config),
            _ => throw new ArgumentException($"Unknown provider type: {config.Type}")
        };
    }

    private ISimplifiedMarketDataProvider CreatePythonSubprocessProvider(ProviderConfiguration config)
    {
        var scriptPath = config.ScriptPath
            ?? throw new ArgumentException($"ScriptPath required for provider '{config.Name}'");

        var providerConfig = new Dictionary<string, object>();

        // Add provider-specific credentials from environment
        switch (config.Name.ToLowerInvariant())
        {
            case "alpaca":
                providerConfig["key_id"] = GetRequiredEnv("ALPACA_KEY_ID");
                providerConfig["secret_key"] = GetRequiredEnv("ALPACA_SECRET_KEY");
                providerConfig["use_paper"] = GetEnvBool("ALPACA_USE_PAPER", true);
                break;

            case "polygon":
                providerConfig["api_key"] = GetRequiredEnv("POLYGON_API_KEY");
                break;

            case "tiingo":
                providerConfig["api_token"] = GetRequiredEnv("TIINGO_API_TOKEN");
                break;

            case "finnhub":
                providerConfig["api_key"] = GetRequiredEnv("FINNHUB_API_KEY");
                break;
        }

        providerConfig["symbols"] = config.GetAllSymbols().ToList();

        return new PythonSubprocessProvider(config.Name, scriptPath, providerConfig);
    }

    private ISimplifiedMarketDataProvider CreateIBProvider(ProviderConfiguration config)
    {
        var host = Environment.GetEnvironmentVariable("IB_HOST") ?? "127.0.0.1";
        var port = int.Parse(Environment.GetEnvironmentVariable("IB_PORT") ?? "7497");
        var clientId = int.Parse(Environment.GetEnvironmentVariable("IB_CLIENT_ID") ?? "1");

        // For now, return a stub - IB requires significant integration
        throw new NotImplementedException("IB provider not yet implemented in simplified architecture");
    }

    private ISimplifiedMarketDataProvider CreateNativeProvider(ProviderConfiguration config)
    {
        // For native .NET providers (if any)
        throw new NotImplementedException($"Native provider '{config.Name}' not yet implemented");
    }

    private static string GetRequiredEnv(string name)
    {
        return Environment.GetEnvironmentVariable(name)
            ?? throw new InvalidOperationException($"Environment variable '{name}' not set");
    }

    private static bool GetEnvBool(string name, bool defaultValue)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return bool.TryParse(value, out var result) ? result : defaultValue;
    }
}
