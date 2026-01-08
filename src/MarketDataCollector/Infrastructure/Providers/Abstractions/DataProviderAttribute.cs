namespace MarketDataCollector.Infrastructure.Providers.Abstractions;

/// <summary>
/// Attribute for automatic provider discovery via reflection.
/// Enables plugin-style architecture for adding new providers.
/// Apply this attribute to provider classes to register them with the provider registry.
/// </summary>
/// <example>
/// <code>
/// [DataProvider(
///     id: "alpaca",
///     displayName: "Alpaca Markets",
///     capabilities: ProviderCapabilities.RealTimeTrades | ProviderCapabilities.HistoricalDailyBars,
///     DefaultPriority = 10)]
/// public sealed class AlpacaDataProvider : IUnifiedDataProvider { }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class DataProviderAttribute : Attribute
{
    /// <summary>
    /// Unique identifier for this provider type (lowercase, no spaces).
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Human-readable display name.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Provider capabilities.
    /// </summary>
    public ProviderCapabilities Capabilities { get; }

    /// <summary>
    /// Optional configuration type for this provider.
    /// </summary>
    public Type? ConfigurationType { get; set; }

    /// <summary>
    /// Default priority for this provider (lower = higher priority).
    /// </summary>
    public int DefaultPriority { get; set; } = 100;

    /// <summary>
    /// Brief description of this provider.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Documentation URL for this provider.
    /// </summary>
    public string? DocumentationUrl { get; set; }

    /// <summary>
    /// Whether this provider requires authentication.
    /// </summary>
    public bool RequiresAuthentication { get; set; } = true;

    /// <summary>
    /// Supported regions/markets (comma-separated, e.g., "US,EU,APAC").
    /// </summary>
    public string? SupportedRegions { get; set; }

    public DataProviderAttribute(
        string id,
        string displayName,
        ProviderCapabilities capabilities)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Provider ID is required", nameof(id));

        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Display name is required", nameof(displayName));

        Id = id.ToLowerInvariant().Trim();
        DisplayName = displayName.Trim();
        Capabilities = capabilities;
    }

    /// <summary>
    /// Get supported regions as a list.
    /// </summary>
    public IReadOnlyList<string> GetSupportedRegionsList() =>
        string.IsNullOrWhiteSpace(SupportedRegions)
            ? Array.Empty<string>()
            : SupportedRegions.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

/// <summary>
/// Registration information for a discovered provider.
/// </summary>
public sealed record ProviderRegistration(
    string ProviderId,
    string DisplayName,
    ProviderCapabilities Capabilities,
    Type ProviderType,
    Type? ConfigurationType,
    int DefaultPriority,
    string? Description = null,
    bool RequiresAuthentication = true,
    IReadOnlyList<string>? SupportedRegions = null
)
{
    /// <summary>
    /// Create from a DataProviderAttribute and Type.
    /// </summary>
    public static ProviderRegistration FromAttribute(DataProviderAttribute attr, Type providerType)
    {
        return new ProviderRegistration(
            attr.Id,
            attr.DisplayName,
            attr.Capabilities,
            providerType,
            attr.ConfigurationType,
            attr.DefaultPriority,
            attr.Description,
            attr.RequiresAuthentication,
            attr.GetSupportedRegionsList()
        );
    }
}
