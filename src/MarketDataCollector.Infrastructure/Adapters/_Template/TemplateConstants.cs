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

    // TODO: Set the recommended minimum delay between consecutive requests.
    //   MinRequestDelay = Window / MaxRequestsPerWindow
    public static readonly TimeSpan MinRequestDelay = TimeSpan.FromSeconds(1);
}

/// <summary>
/// Message type or action string constants for the Template provider WebSocket protocol.
/// TODO: Populate with the actual message-type strings from the provider's documentation.
/// Remove this class if the provider is REST-only.
/// </summary>
internal static class TemplateMessageTypes
{
    // TODO: Add message type constants. Examples:
    public const string Trade = "trade";
    public const string Quote = "quote";
    public const string Subscribe = "subscribe";
    public const string Unsubscribe = "unsubscribe";
    public const string Error = "error";
    public const string Connected = "connected";
    public const string Authenticated = "authenticated";
}
