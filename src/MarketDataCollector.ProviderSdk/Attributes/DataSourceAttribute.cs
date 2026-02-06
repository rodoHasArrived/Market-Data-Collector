namespace MarketDataCollector.ProviderSdk.Attributes;

/// <summary>
/// Marks a class as a data source provider for automatic discovery and registration.
/// This attribute enables attribute-based provider discovery in plugin assemblies.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class DataSourceAttribute : Attribute
{
    /// <summary>
    /// Unique identifier for this data source (e.g., "alpaca", "stooq").
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Human-readable display name.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Type of data provided.
    /// </summary>
    public DataSourceType Type { get; }

    /// <summary>
    /// Category of the data source.
    /// </summary>
    public DataSourceCategory Category { get; }

    /// <summary>
    /// Priority for source selection (lower = higher priority).
    /// </summary>
    public int Priority { get; set; } = 100;

    /// <summary>
    /// Whether this source should be enabled by default.
    /// </summary>
    public bool EnabledByDefault { get; set; } = true;

    /// <summary>
    /// Optional description.
    /// </summary>
    public string? Description { get; set; }

    public DataSourceAttribute(
        string id,
        string displayName,
        DataSourceType type,
        DataSourceCategory category)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
        Type = type;
        Category = category;
    }
}

/// <summary>
/// Type of data a source provides.
/// </summary>
public enum DataSourceType
{
    Realtime,
    Historical,
    Hybrid
}

/// <summary>
/// Category of a data source.
/// </summary>
public enum DataSourceCategory
{
    Broker,
    Exchange,
    DataVendor,
    FreeApi,
    Aggregator
}
