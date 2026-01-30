namespace MarketDataCollector.Infrastructure.Providers.StockSharp.ConnectorHub;

/// <summary>
/// Configuration options for the StockSharp Connector Hub.
/// Configures which adapters to enable and how to route data requests.
/// </summary>
public sealed record ConnectorHubOptions
{
    /// <summary>
    /// Whether the connector hub is enabled.
    /// </summary>
    public bool Enabled { get; init; } = false;

    /// <summary>
    /// List of adapter IDs to enable at startup.
    /// If empty, adapters are loaded from the Adapters configuration.
    /// </summary>
    public IReadOnlyList<string> EnabledAdapters { get; init; } = [];

    /// <summary>
    /// Default adapter ID to use when no specific routing can be determined.
    /// </summary>
    public string? DefaultAdapterId { get; init; }

    /// <summary>
    /// Whether to use the hub as a fallback for native providers.
    /// When true, symbols that can't be routed to native providers will use the hub.
    /// </summary>
    public bool UseFallbackRouting { get; init; } = false;

    /// <summary>
    /// Adapter configurations.
    /// </summary>
    public IReadOnlyList<AdapterOptions> Adapters { get; init; } = [];

    /// <summary>
    /// Provider routing rules.
    /// Maps provider IDs to adapter IDs for explicit routing.
    /// </summary>
    public IReadOnlyDictionary<string, string> ProviderRouting { get; init; } =
        new Dictionary<string, string>();

    /// <summary>
    /// Exchange routing rules.
    /// Maps exchange codes to adapter IDs for explicit routing.
    /// </summary>
    public IReadOnlyDictionary<string, string> ExchangeRouting { get; init; } =
        new Dictionary<string, string>();

    /// <summary>
    /// Asset class routing rules.
    /// Maps asset classes to adapter IDs for explicit routing.
    /// </summary>
    public IReadOnlyDictionary<string, string> AssetClassRouting { get; init; } =
        new Dictionary<string, string>();

    /// <summary>
    /// Heartbeat configuration.
    /// </summary>
    public HeartbeatOptions Heartbeat { get; init; } = new();

    /// <summary>
    /// Reconnection configuration.
    /// </summary>
    public ReconnectionOptions Reconnection { get; init; } = new();

    /// <summary>
    /// Message buffering configuration.
    /// </summary>
    public BufferingOptions Buffering { get; init; } = new();
}

/// <summary>
/// Configuration for a single adapter in the hub.
/// </summary>
public sealed record AdapterOptions
{
    /// <summary>
    /// Unique adapter identifier (if different from Type).
    /// </summary>
    public string? Id { get; init; }

    /// <summary>
    /// Adapter type (e.g., "ib", "alpaca", "polygon", "custom").
    /// </summary>
    public string? Type { get; init; }

    /// <summary>
    /// Whether this adapter is enabled.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Priority for adapter selection (lower = higher priority).
    /// </summary>
    public int Priority { get; init; } = 50;

    /// <summary>
    /// Display name for the adapter.
    /// </summary>
    public string? DisplayName { get; init; }

    /// <summary>
    /// Description of the adapter.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Adapter-specific settings.
    /// </summary>
    public Dictionary<string, string> Settings { get; init; } = new();

    /// <summary>
    /// Get a typed setting value with a default.
    /// </summary>
    public T GetSetting<T>(string key, T defaultValue)
    {
        if (!Settings.TryGetValue(key, out var stringValue))
            return defaultValue;

        try
        {
            var targetType = typeof(T);
            var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

            if (underlyingType == typeof(string))
                return (T)(object)stringValue;

            if (underlyingType == typeof(int))
                return (T)(object)int.Parse(stringValue);

            if (underlyingType == typeof(bool))
                return (T)(object)bool.Parse(stringValue);

            if (underlyingType == typeof(double))
                return (T)(object)double.Parse(stringValue);

            if (underlyingType == typeof(TimeSpan))
                return (T)(object)TimeSpan.Parse(stringValue);

            if (underlyingType.IsEnum)
                return (T)Enum.Parse(underlyingType, stringValue, ignoreCase: true);

            return defaultValue;
        }
        catch
        {
            return defaultValue;
        }
    }
}

/// <summary>
/// Heartbeat monitoring configuration.
/// </summary>
public sealed record HeartbeatOptions
{
    /// <summary>
    /// Interval between heartbeat checks.
    /// </summary>
    public TimeSpan Interval { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Timeout after which a connection is considered stale.
    /// </summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Whether to automatically trigger reconnection on timeout.
    /// </summary>
    public bool AutoReconnect { get; init; } = true;
}

/// <summary>
/// Reconnection configuration.
/// </summary>
public sealed record ReconnectionOptions
{
    /// <summary>
    /// Maximum number of reconnection attempts.
    /// </summary>
    public int MaxAttempts { get; init; } = 10;

    /// <summary>
    /// Initial delay before first reconnection attempt.
    /// </summary>
    public TimeSpan InitialDelay { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Maximum delay between reconnection attempts.
    /// </summary>
    public TimeSpan MaxDelay { get; init; } = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Multiplier for exponential backoff.
    /// </summary>
    public double BackoffMultiplier { get; init; } = 2.0;

    /// <summary>
    /// Whether to recover subscriptions after reconnection.
    /// </summary>
    public bool RecoverSubscriptions { get; init; } = true;
}

/// <summary>
/// Message buffering configuration.
/// </summary>
public sealed record BufferingOptions
{
    /// <summary>
    /// Maximum number of messages in the buffer.
    /// </summary>
    public int Capacity { get; init; } = 10_000;

    /// <summary>
    /// Behavior when buffer is full.
    /// </summary>
    public BufferFullMode FullMode { get; init; } = BufferFullMode.DropOldest;
}

/// <summary>
/// Behavior when the message buffer is full.
/// </summary>
public enum BufferFullMode
{
    /// <summary>Block until space is available.</summary>
    Wait,

    /// <summary>Drop the oldest messages.</summary>
    DropOldest,

    /// <summary>Drop the newest messages.</summary>
    DropNewest
}

/// <summary>
/// Factory methods for creating ConnectorHubOptions.
/// </summary>
public static class ConnectorHubOptionsFactory
{
    /// <summary>
    /// Create options for a single Interactive Brokers adapter.
    /// </summary>
    public static ConnectorHubOptions ForInteractiveBrokers(
        string host = "127.0.0.1",
        int port = 7496,
        int clientId = 1)
    {
        return new ConnectorHubOptions
        {
            Enabled = true,
            EnabledAdapters = ["ib"],
            DefaultAdapterId = "ib",
            Adapters =
            [
                new AdapterOptions
                {
                    Id = "ib",
                    Type = "ib",
                    Enabled = true,
                    Priority = 10,
                    DisplayName = "Interactive Brokers",
                    Settings = new Dictionary<string, string>
                    {
                        ["Host"] = host,
                        ["Port"] = port.ToString(),
                        ["ClientId"] = clientId.ToString()
                    }
                }
            ]
        };
    }

    /// <summary>
    /// Create options for Alpaca Markets adapter.
    /// </summary>
    public static ConnectorHubOptions ForAlpaca(
        string keyId,
        string secretKey,
        bool usePaper = true,
        string feed = "iex")
    {
        return new ConnectorHubOptions
        {
            Enabled = true,
            EnabledAdapters = ["alpaca"],
            DefaultAdapterId = "alpaca",
            Adapters =
            [
                new AdapterOptions
                {
                    Id = "alpaca",
                    Type = "alpaca",
                    Enabled = true,
                    Priority = 10,
                    DisplayName = "Alpaca Markets",
                    Settings = new Dictionary<string, string>
                    {
                        ["KeyId"] = keyId,
                        ["SecretKey"] = secretKey,
                        ["UsePaper"] = usePaper.ToString(),
                        ["Feed"] = feed
                    }
                }
            ]
        };
    }

    /// <summary>
    /// Create options for multiple adapters with automatic routing.
    /// </summary>
    public static ConnectorHubOptions MultiAdapter(params AdapterOptions[] adapters)
    {
        var enabledAdapterIds = adapters
            .Where(a => a.Enabled)
            .Select(a => a.Id ?? a.Type ?? "unknown")
            .ToList();

        return new ConnectorHubOptions
        {
            Enabled = true,
            EnabledAdapters = enabledAdapterIds,
            DefaultAdapterId = enabledAdapterIds.FirstOrDefault(),
            Adapters = adapters.ToList()
        };
    }
}
