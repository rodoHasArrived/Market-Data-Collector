using System.Text.Json;
using MarketDataCollector.Ui.Services.Contracts;

namespace MarketDataCollector.Ui.Services;

/// <summary>
/// Default schema service for the shared UI services layer.
/// Platform-specific projects (WPF, UWP) override this with their own implementations
/// by setting the Instance property during app startup.
/// </summary>
public class SchemaService : ISchemaService
{
    private static SchemaService? _instance;
    private static readonly object _lock = new();

    public static SchemaService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new SchemaService();
                }
            }
            return _instance;
        }
        set
        {
            lock (_lock)
            {
                _instance = value;
            }
        }
    }

    /// <summary>
    /// Gets a JSON schema for the specified event type.
    /// This base implementation returns basic schemas for common event types.
    /// Platform-specific implementations can override this for more detailed schemas.
    /// </summary>
    public virtual string? GetJsonSchema(string eventType)
    {
        var schemas = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["Trade"] = new
            {
                type = "object",
                properties = new
                {
                    symbol = new { type = "string" },
                    price = new { type = "number" },
                    size = new { type = "number" },
                    timestamp = new { type = "string", format = "date-time" },
                    exchange = new { type = "string" },
                    condition = new { type = "string" },
                    sequenceNumber = new { type = "integer" },
                    side = new { type = "string", @enum = new[] { "Buy", "Sell", "Unknown" } }
                },
                required = new[] { "symbol", "price", "size", "timestamp" }
            },
            ["BboQuote"] = new
            {
                type = "object",
                properties = new
                {
                    symbol = new { type = "string" },
                    bidPrice = new { type = "number" },
                    bidSize = new { type = "number" },
                    askPrice = new { type = "number" },
                    askSize = new { type = "number" },
                    spread = new { type = "number" },
                    spreadBps = new { type = "number" },
                    midPrice = new { type = "number" },
                    timestamp = new { type = "string", format = "date-time" }
                },
                required = new[] { "symbol", "bidPrice", "bidSize", "askPrice", "askSize", "timestamp" }
            },
            ["LOBSnapshot"] = new
            {
                type = "object",
                properties = new
                {
                    symbol = new { type = "string" },
                    timestamp = new { type = "string", format = "date-time" },
                    bids = new { type = "array", items = new { type = "object" } },
                    asks = new { type = "array", items = new { type = "object" } },
                    bestBid = new { type = "number" },
                    bestAsk = new { type = "number" },
                    spread = new { type = "number" },
                    midPrice = new { type = "number" }
                },
                required = new[] { "symbol", "timestamp", "bids", "asks" }
            },
            ["HistoricalBar"] = new
            {
                type = "object",
                properties = new
                {
                    symbol = new { type = "string" },
                    timestamp = new { type = "string", format = "date-time" },
                    open = new { type = "number" },
                    high = new { type = "number" },
                    low = new { type = "number" },
                    close = new { type = "number" },
                    volume = new { type = "number" },
                    vwap = new { type = "number" },
                    tradeCount = new { type = "integer" }
                },
                required = new[] { "symbol", "timestamp", "open", "high", "low", "close", "volume" }
            }
        };

        if (schemas.TryGetValue(eventType, out var schema))
        {
            return JsonSerializer.Serialize(new { schema = eventType, @namespace = "MarketDataCollector.Domain.Events", definition = schema }, 
                new JsonSerializerOptions { WriteIndented = true });
        }

        return null;
    }
}
