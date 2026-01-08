namespace MarketDataCollector.Application.Subscriptions.Models;

/// <summary>
/// Components of a market index.
/// </summary>
public sealed record IndexComponents(
    /// <summary>Index identifier (e.g., "SPX", "NDX").</summary>
    string IndexId,

    /// <summary>Index display name.</summary>
    string Name,

    /// <summary>Component symbols.</summary>
    IndexComponent[] Components,

    /// <summary>When the components were last updated.</summary>
    DateTimeOffset LastUpdated,

    /// <summary>Data source for component list.</summary>
    string Source
);

/// <summary>
/// Individual component of an index.
/// </summary>
public sealed record IndexComponent(
    /// <summary>Symbol ticker.</summary>
    string Symbol,

    /// <summary>Company name.</summary>
    string Name,

    /// <summary>Weight in the index (0-1).</summary>
    decimal? Weight = null,

    /// <summary>Sector classification.</summary>
    string? Sector = null
);

/// <summary>
/// Available indices for auto-subscription.
/// </summary>
public static class KnownIndices
{
    public static readonly IndexDefinition SP500 = new("SPX", "S&P 500", "Large-cap US equities");
    public static readonly IndexDefinition Nasdaq100 = new("NDX", "NASDAQ 100", "Top 100 NASDAQ non-financial stocks");
    public static readonly IndexDefinition DowJones = new("DJI", "Dow Jones Industrial Average", "30 blue-chip US stocks");
    public static readonly IndexDefinition Russell2000 = new("RUT", "Russell 2000", "Small-cap US equities");
    public static readonly IndexDefinition SP400 = new("MID", "S&P 400 MidCap", "Mid-cap US equities");

    public static IReadOnlyList<IndexDefinition> All => new[]
    {
        SP500, Nasdaq100, DowJones, Russell2000, SP400
    };
}

/// <summary>
/// Definition of an available index.
/// </summary>
public sealed record IndexDefinition(
    string Id,
    string Name,
    string Description
);

/// <summary>
/// Request to auto-subscribe to index components.
/// </summary>
public sealed record IndexSubscribeRequest(
    /// <summary>Index identifier to subscribe to.</summary>
    string IndexId,

    /// <summary>Maximum number of components to subscribe (by weight).</summary>
    int? MaxComponents = null,

    /// <summary>Custom subscription settings for the components.</summary>
    TemplateSubscriptionDefaults? Defaults = null,

    /// <summary>Whether to replace existing subscriptions.</summary>
    bool ReplaceExisting = false,

    /// <summary>Only subscribe to components in these sectors.</summary>
    string[]? FilterSectors = null
);

/// <summary>
/// Result of an index subscription operation.
/// </summary>
public sealed record IndexSubscribeResult(
    string IndexId,
    int ComponentsSubscribed,
    int ComponentsSkipped,
    string[] SubscribedSymbols,
    string[] SkippedSymbols,
    string? Message = null
);
