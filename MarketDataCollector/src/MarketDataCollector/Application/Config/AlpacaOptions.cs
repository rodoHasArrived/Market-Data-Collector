namespace MarketDataCollector.Application.Config;

// TODO: SECURITY - API credentials should NOT be stored in config files
// Recommendations:
// 1. Use environment variables: Environment.GetEnvironmentVariable("ALPACA_KEY_ID")
// 2. Use a secure vault service (Azure Key Vault, AWS Secrets Manager, HashiCorp Vault)
// 3. Use .NET User Secrets for local development (dotnet user-secrets)
// 4. Ensure appsettings.json with real credentials is in .gitignore
// 5. Never commit __SET_ME__ placeholders with actual values

/// <summary>
/// Alpaca Market Data configuration.
/// Docs: WebSocket stream + authentication. Uses Trading API keys with message auth.
/// </summary>
public sealed record AlpacaOptions(
    string KeyId,
    string SecretKey,
    string Feed = "iex",            // v2/{feed}: iex, sip, delayed_sip
    bool UseSandbox = false,        // stream.data.sandbox.alpaca.markets
    bool SubscribeQuotes = false    // if true, subscribes to quotes too
)
{
    // Resilience Configuration - Connection Retry
    /// <summary>Maximum number of connection retry attempts (-1 for unlimited).</summary>
    public int MaxConnectionRetries { get; init; } = 5;

    /// <summary>Initial delay in seconds for exponential backoff.</summary>
    public double InitialRetryDelaySeconds { get; init; } = 1.0;

    /// <summary>Maximum delay in seconds between retries.</summary>
    public double MaxRetryDelaySeconds { get; init; } = 120.0;

    /// <summary>Timeout in seconds for individual connection attempts.</summary>
    public double ConnectionTimeoutSeconds { get; init; } = 30.0;

    // Resilience Configuration - Heartbeat/Keep-Alive
    /// <summary>Enable heartbeat monitoring to detect stale connections.</summary>
    public bool EnableHeartbeat { get; init; } = true;

    /// <summary>Interval in seconds between heartbeat checks.</summary>
    public double HeartbeatIntervalSeconds { get; init; } = 30.0;

    /// <summary>Timeout in seconds for heartbeat responses.</summary>
    public double HeartbeatTimeoutSeconds { get; init; } = 10.0;

    /// <summary>Number of consecutive failures before triggering reconnection.</summary>
    public int ConsecutiveFailuresBeforeReconnect { get; init; } = 3;

    // Resilience Configuration - Auto-Reconnect
    /// <summary>Enable automatic reconnection on connection loss.</summary>
    public bool EnableAutoReconnect { get; init; } = true;
}
