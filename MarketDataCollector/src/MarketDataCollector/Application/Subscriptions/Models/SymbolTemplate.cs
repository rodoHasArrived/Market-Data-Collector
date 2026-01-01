namespace MarketDataCollector.Application.Subscriptions.Models;

/// <summary>
/// Predefined template for bulk symbol subscriptions.
/// Supports equity groups, sectors, and indices.
/// </summary>
public sealed record SymbolTemplate(
    /// <summary>Unique template identifier.</summary>
    string Id,

    /// <summary>Display name for the template.</summary>
    string Name,

    /// <summary>Template description.</summary>
    string Description,

    /// <summary>Category: Sector, Index, Custom, MarketCap.</summary>
    TemplateCategory Category,

    /// <summary>List of symbols included in this template.</summary>
    string[] Symbols,

    /// <summary>Default subscription settings for symbols in this template.</summary>
    TemplateSubscriptionDefaults Defaults
);

/// <summary>
/// Default subscription settings applied when using a template.
/// </summary>
public sealed record TemplateSubscriptionDefaults(
    bool SubscribeTrades = true,
    bool SubscribeDepth = true,
    int DepthLevels = 10,
    string SecurityType = "STK",
    string Exchange = "SMART",
    string Currency = "USD"
);

/// <summary>
/// Categories for organizing subscription templates.
/// </summary>
public enum TemplateCategory
{
    /// <summary>Industry sector (e.g., Technology, Healthcare).</summary>
    Sector,

    /// <summary>Market index components (e.g., S&P 500, NASDAQ 100).</summary>
    Index,

    /// <summary>Market capitalization groups (e.g., Large Cap, Small Cap).</summary>
    MarketCap,

    /// <summary>Custom user-defined template.</summary>
    Custom
}

/// <summary>
/// Request to apply a template to the current subscriptions.
/// </summary>
public sealed record ApplyTemplateRequest(
    string TemplateId,
    bool ReplaceExisting = false,
    TemplateSubscriptionDefaults? OverrideDefaults = null
);
