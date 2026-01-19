namespace MarketDataCollector.Infrastructure.Plugins.Discovery;

/// <summary>
/// Marks a class as a market data plugin for automatic discovery.
/// </summary>
/// <remarks>
/// Example usage:
/// <code>
/// [MarketDataPlugin(
///     id: "alpaca",
///     displayName: "Alpaca Markets",
///     description: "Real-time and historical data from Alpaca Markets",
///     pluginType: PluginType.Hybrid)]
/// public sealed class AlpacaPlugin : MarketDataPluginBase { ... }
/// </code>
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class MarketDataPluginAttribute : Attribute
{
    /// <summary>
    /// Unique plugin identifier (e.g., "alpaca", "yahoo", "ib").
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Human-readable display name.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Brief description of the plugin.
    /// </summary>
    public string Description { get; set; } = "";

    /// <summary>
    /// Plugin type: Realtime, Historical, or Hybrid.
    /// </summary>
    public PluginType Type { get; }

    /// <summary>
    /// Plugin category: Free, Premium, Broker, Exchange.
    /// </summary>
    public PluginCategory Category { get; set; } = PluginCategory.Free;

    /// <summary>
    /// Priority for fallback ordering (lower = higher priority).
    /// </summary>
    public int Priority { get; set; } = 100;

    /// <summary>
    /// Version string (semantic versioning recommended).
    /// </summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// Author or organization name.
    /// </summary>
    public string? Author { get; set; }

    /// <summary>
    /// URL for documentation or support.
    /// </summary>
    public string? Url { get; set; }

    /// <summary>
    /// Environment variable prefix for configuration (e.g., "ALPACA").
    /// If not set, defaults to uppercase plugin ID.
    /// </summary>
    public string? ConfigPrefix { get; set; }

    /// <summary>
    /// Creates a new MarketDataPluginAttribute.
    /// </summary>
    public MarketDataPluginAttribute(string id, string displayName, PluginType type)
    {
        Id = id;
        DisplayName = displayName;
        Type = type;
    }
}

/// <summary>
/// Plugin type classification.
/// </summary>
public enum PluginType
{
    /// <summary>Real-time streaming data only.</summary>
    Realtime,

    /// <summary>Historical data only.</summary>
    Historical,

    /// <summary>Both real-time and historical data.</summary>
    Hybrid
}

/// <summary>
/// Plugin category classification.
/// </summary>
public enum PluginCategory
{
    /// <summary>Free data source (may have rate limits).</summary>
    Free,

    /// <summary>Premium paid data source.</summary>
    Premium,

    /// <summary>Broker-provided data (requires account).</summary>
    Broker,

    /// <summary>Direct exchange feed.</summary>
    Exchange
}
