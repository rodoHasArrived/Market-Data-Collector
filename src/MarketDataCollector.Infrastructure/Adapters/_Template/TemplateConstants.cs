// Replace "Template" with your provider name in the namespace and class names.
namespace MarketDataCollector.Infrastructure.Adapters.Template;

/// <summary>
/// HTTP / WebSocket endpoint constants for the Template provider API.
/// TODO: Replace with the actual endpoint URL(s) for your provider.
/// </summary>
internal static class TemplateEndpoints
{
    // TODO: Add your provider's base URL or WebSocket URI. Examples:

    // REST API base URL (used by TemplateHistoricalDataProvider and TemplateSymbolSearchProvider):
    public const string BaseUrl = "https://api.example.com/v1";

    // WebSocket streaming URI (used by TemplateMarketDataClient):
    // public const string WssUri = "wss://stream.example.com/ws";

    // Specific endpoint paths can also be defined here, e.g.:
    // public const string HistoricalBars = "/bars";
    // public const string SymbolSearch   = "/search";
}

/// <summary>
/// Rate limit constants for the Template provider.
/// Replace these values with the actual limits from the provider's API documentation.
/// </summary>
internal static class TemplateRateLimits
{
    /// <summary>Maximum requests per rate-limit window.</summary>
    public const int MaxRequestsPerWindow = 60;

    /// <summary>Rate-limit window duration.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    /// <summary>Recommended minimum delay between consecutive requests (Window / MaxRequestsPerWindow).</summary>
    public static readonly TimeSpan MinRequestDelay = TimeSpan.FromSeconds(1);
}

/// <summary>
/// Message type or action string constants for the Template provider WebSocket protocol.
/// TODO: Populate with the actual message-type strings from the provider's documentation.
/// Remove this class if the provider is REST-only.
/// </summary>
internal static class TemplateMessageTypes
{
    /// <summary>Trade event message type.</summary>
    public const string Trade = "trade";

    /// <summary>Quote (BBO) event message type.</summary>
    public const string Quote = "quote";

    /// <summary>Subscribe action sent to the WebSocket server.</summary>
    public const string Subscribe = "subscribe";

    /// <summary>Unsubscribe action sent to the WebSocket server.</summary>
    public const string Unsubscribe = "unsubscribe";

    /// <summary>Error response message type.</summary>
    public const string Error = "error";

    /// <summary>Connection established message type.</summary>
    public const string Connected = "connected";

    /// <summary>Authentication success message type.</summary>
    public const string Authenticated = "authenticated";
}
