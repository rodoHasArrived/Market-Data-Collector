// Replace "Template" with your provider name in the namespace and class names.
namespace MarketDataCollector.Infrastructure.Adapters.Template;

/// <summary>
/// HTTP / WebSocket endpoint constants for the Template provider API.
/// TODO: Replace with the actual endpoint URL(s) for your provider.
/// </summary>
internal static class TemplateEndpoints
{
    // TODO: Add your provider's base URL or WebSocket URI.
    // Example for a REST API:
    //   public const string BaseUrl = "https://api.example.com/v1";
    // Example for a WebSocket API:
    //   public const string WssUri = "wss://stream.example.com/ws";
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
    // TODO: Add message type constants.
    // Example:
    //   public const string Trade = "trade";
    //   public const string Quote = "quote";
    //   public const string Error = "error";
}
