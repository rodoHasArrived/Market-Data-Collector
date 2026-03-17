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
/// TODO: Fill in the values from the provider's API documentation.
/// </summary>
internal static class TemplateRateLimits
{
    // TODO: Set the maximum number of requests allowed per window.
    public const int MaxRequestsPerWindow = 60;

    // TODO: Set the rate-limit window duration.
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    /// <summary>Recommended minimum delay between consecutive requests (<see cref="Window"/> / <see cref="MaxRequestsPerWindow"/>).</summary>
    public static readonly TimeSpan MinRequestDelay = Window / MaxRequestsPerWindow;
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
