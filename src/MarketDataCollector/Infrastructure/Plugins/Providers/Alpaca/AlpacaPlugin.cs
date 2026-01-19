using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using MarketDataCollector.Infrastructure.Plugins.Base;
using MarketDataCollector.Infrastructure.Plugins.Core;
using MarketDataCollector.Infrastructure.Plugins.Discovery;
using Microsoft.Extensions.Logging;

namespace MarketDataCollector.Infrastructure.Plugins.Providers.Alpaca;

/// <summary>
/// Alpaca Markets plugin for real-time and historical market data.
///
/// This is a migration example showing how to convert the legacy
/// AlpacaMarketDataClient to the new unified plugin architecture.
///
/// Configuration (via environment variables):
///   ALPACA__KEY_ID - Alpaca API key ID
///   ALPACA__SECRET_KEY - Alpaca API secret key
///   ALPACA__FEED - Feed type: "iex" (default), "sip", "delayed_sip"
///   ALPACA__USE_SANDBOX - Use sandbox environment (default: false)
/// </summary>
[MarketDataPlugin(
    id: "alpaca",
    displayName: "Alpaca Markets",
    type: PluginType.Hybrid,
    Category = PluginCategory.Broker,
    Priority = 10,
    Description = "Real-time streaming and historical data from Alpaca Markets",
    Author = "Market Data Collector",
    ConfigPrefix = "ALPACA",
    Version = "2.0.0")]
public sealed class AlpacaPlugin : RealtimePluginBase
{
    private const string DefaultFeed = "iex";
    private const string SandboxHost = "stream.data.sandbox.alpaca.markets";
    private const string ProductionHost = "stream.data.alpaca.markets";

    private ClientWebSocket? _ws;
    private CancellationTokenSource? _receiveCts;
    private Task? _receiveTask;

    private string _keyId = "";
    private string _secretKey = "";
    private string _feed = DefaultFeed;
    private bool _useSandbox;

    private readonly HashSet<string> _subscribedSymbols = new(StringComparer.OrdinalIgnoreCase);

    #region Identity

    public override string Id => "alpaca";
    public override string DisplayName => "Alpaca Markets";
    public override string Description => "Real-time streaming and historical data from Alpaca Markets";
    public override Version Version => new(2, 0, 0);

    #endregion

    #region Capabilities

    public override PluginCapabilities Capabilities => new()
    {
        SupportsRealtime = true,
        SupportsHistorical = true,
        SupportsTrades = true,
        SupportsQuotes = true,
        SupportsDepth = false, // Alpaca doesn't provide L2 depth for stocks
        SupportsBars = true,
        SupportsAdjustedPrices = true,
        SupportsDividends = true,
        SupportsSplits = true,
        MaxHistoricalLookback = TimeSpan.FromDays(365 * 7), // ~7 years
        SupportedBarIntervals = ["1min", "5min", "15min", "30min", "1hour", "1day"],
        SupportedAssetClasses = new HashSet<AssetClass> { AssetClass.Equity, AssetClass.Crypto },
        SupportedMarkets = new HashSet<string> { "US" },
        MaxSymbolsPerRequest = 200,
        RateLimit = RateLimitPolicy.PerMinute(200)
    };

    #endregion

    #region Lifecycle

    protected override void ValidateConfiguration(IPluginConfig config)
    {
        _keyId = config.GetRequired("key_id");
        _secretKey = config.GetRequired("secret_key");
        _feed = config.Get("feed", DefaultFeed)!;
        _useSandbox = config.Get("use_sandbox", false);

        Logger.LogInformation(
            "Alpaca plugin configured: Feed={Feed}, Sandbox={UseSandbox}",
            _feed, _useSandbox);
    }

    protected override ILogger CreateLogger()
    {
        // In production, inject ILoggerFactory via DI
        return Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
    }

    #endregion

    #region Connection

    protected override async Task ConnectAsync(CancellationToken ct)
    {
        var host = _useSandbox ? SandboxHost : ProductionHost;
        var uri = new Uri($"wss://{host}/v2/{_feed}");

        Logger.LogInformation("Connecting to Alpaca WebSocket at {Uri}", uri);

        _ws = new ClientWebSocket();

        try
        {
            await _ws.ConnectAsync(uri, ct).ConfigureAwait(false);

            // Authenticate immediately after connection
            var authMsg = JsonSerializer.Serialize(new
            {
                action = "auth",
                key = _keyId,
                secret = _secretKey
            });

            await SendAsync(authMsg, ct).ConfigureAwait(false);

            // Start receive loop
            _receiveCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            _receiveTask = ReceiveLoopAsync(_receiveCts.Token);

            Logger.LogInformation("Connected to Alpaca WebSocket");
        }
        catch
        {
            _ws?.Dispose();
            _ws = null;
            throw;
        }
    }

    protected override async Task DisconnectAsync()
    {
        Logger.LogInformation("Disconnecting from Alpaca WebSocket");

        _receiveCts?.Cancel();

        if (_ws != null)
        {
            try
            {
                if (_ws.State == WebSocketState.Open)
                {
                    await _ws.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Shutting down",
                        CancellationToken.None).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Error closing WebSocket");
            }

            _ws.Dispose();
            _ws = null;
        }

        if (_receiveTask != null)
        {
            try
            {
                await _receiveTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
        }

        _receiveCts?.Dispose();
        _receiveCts = null;
        _receiveTask = null;

        Logger.LogInformation("Disconnected from Alpaca WebSocket");
    }

    #endregion

    #region Subscription

    protected override async Task SubscribeAsync(IReadOnlyList<string> symbols, CancellationToken ct)
    {
        foreach (var symbol in symbols)
        {
            _subscribedSymbols.Add(symbol);
        }

        await SendSubscriptionUpdateAsync(ct).ConfigureAwait(false);
    }

    protected override async Task UnsubscribeAsync(IReadOnlyList<string> symbols, CancellationToken ct)
    {
        foreach (var symbol in symbols)
        {
            _subscribedSymbols.Remove(symbol);
        }

        await SendSubscriptionUpdateAsync(ct).ConfigureAwait(false);
    }

    private async Task SendSubscriptionUpdateAsync(CancellationToken ct)
    {
        if (_ws == null || _ws.State != WebSocketState.Open)
            return;

        var symbols = _subscribedSymbols.ToArray();

        var msg = JsonSerializer.Serialize(new
        {
            action = "subscribe",
            trades = symbols,
            quotes = symbols
        });

        await SendAsync(msg, ct).ConfigureAwait(false);

        Logger.LogDebug("Subscribed to {Count} symbols", symbols.Length);
    }

    #endregion

    #region Receive Loop

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        if (_ws == null) return;

        var buffer = new byte[64 * 1024];
        var messageBuilder = new StringBuilder(128 * 1024);

        try
        {
            while (!ct.IsCancellationRequested && _ws.State == WebSocketState.Open)
            {
                messageBuilder.Clear();

                WebSocketReceiveResult result;
                do
                {
                    result = await _ws.ReceiveAsync(buffer, ct).ConfigureAwait(false);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        Logger.LogWarning("WebSocket closed by server");
                        return;
                    }

                    messageBuilder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                }
                while (!result.EndOfMessage);

                var json = messageBuilder.ToString();
                if (!string.IsNullOrWhiteSpace(json))
                {
                    ProcessMessage(json);
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Normal shutdown
        }
        catch (WebSocketException ex)
        {
            Logger.LogError(ex, "WebSocket error in receive loop");
            RecordFailure($"WebSocket error: {ex.Message}");
        }
    }

    private void ProcessMessage(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in doc.RootElement.EnumerateArray())
                {
                    ProcessElement(element);
                }
            }
            else if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                ProcessElement(doc.RootElement);
            }
        }
        catch (JsonException ex)
        {
            Logger.LogWarning(ex, "Failed to parse message");
        }
    }

    private void ProcessElement(JsonElement el)
    {
        if (!el.TryGetProperty("T", out var typeProp))
            return;

        var messageType = typeProp.GetString();

        switch (messageType)
        {
            case "t": // Trade
                ProcessTrade(el);
                break;

            case "q": // Quote
                ProcessQuote(el);
                break;

            case "success":
                var msg = el.TryGetProperty("msg", out var msgProp) ? msgProp.GetString() : "";
                Logger.LogDebug("Alpaca success: {Message}", msg);
                if (msg == "authenticated")
                {
                    RecordSuccess();
                }
                break;

            case "error":
                var errorMsg = el.TryGetProperty("msg", out var errProp) ? errProp.GetString() : "Unknown error";
                Logger.LogError("Alpaca error: {Message}", errorMsg);
                RecordFailure(errorMsg ?? "Unknown error");
                break;

            case "subscription":
                Logger.LogDebug("Subscription confirmed");
                break;
        }
    }

    private void ProcessTrade(JsonElement el)
    {
        var symbol = el.TryGetProperty("S", out var sProp) ? sProp.GetString() : null;
        if (string.IsNullOrWhiteSpace(symbol)) return;

        var price = el.TryGetProperty("p", out var pProp) ? (decimal)pProp.GetDouble() : 0m;
        var size = el.TryGetProperty("s", out var szProp) ? szProp.GetInt32() : 0;
        var timestamp = ParseTimestamp(el);
        var exchange = el.TryGetProperty("x", out var xProp) ? xProp.GetString() : null;
        var tradeId = el.TryGetProperty("i", out var iProp) ? iProp.GetInt64().ToString() : null;

        // Get trade conditions if present
        var conditions = el.TryGetProperty("c", out var cProp) && cProp.ValueKind == JsonValueKind.Array
            ? cProp.EnumerateArray().Select(c => c.GetString()!).Where(c => c != null).ToList()
            : null;

        var trade = new TradeEvent
        {
            Symbol = symbol!,
            Timestamp = timestamp,
            Source = Id,
            Price = price,
            Size = size,
            Exchange = exchange,
            TradeId = tradeId,
            Conditions = conditions
        };

        TryEnqueueEvent(trade);
    }

    private void ProcessQuote(JsonElement el)
    {
        var symbol = el.TryGetProperty("S", out var sProp) ? sProp.GetString() : null;
        if (string.IsNullOrWhiteSpace(symbol)) return;

        var bidPrice = el.TryGetProperty("bp", out var bpProp) ? (decimal)bpProp.GetDouble() : 0m;
        var bidSize = el.TryGetProperty("bs", out var bsProp) ? (decimal)bsProp.GetInt64() : 0m;
        var askPrice = el.TryGetProperty("ap", out var apProp) ? (decimal)apProp.GetDouble() : 0m;
        var askSize = el.TryGetProperty("as", out var asProp) ? (decimal)asProp.GetInt64() : 0m;
        var timestamp = ParseTimestamp(el);
        var bidExchange = el.TryGetProperty("bx", out var bxProp) ? bxProp.GetString() : null;
        var askExchange = el.TryGetProperty("ax", out var axProp) ? axProp.GetString() : null;

        var quote = new QuoteEvent
        {
            Symbol = symbol!,
            Timestamp = timestamp,
            Source = Id,
            BidPrice = bidPrice,
            BidSize = bidSize,
            AskPrice = askPrice,
            AskSize = askSize,
            BidExchange = bidExchange,
            AskExchange = askExchange
        };

        TryEnqueueEvent(quote);
    }

    private static DateTimeOffset ParseTimestamp(JsonElement el)
    {
        if (el.TryGetProperty("t", out var tsProp))
        {
            var tsString = tsProp.GetString();
            if (DateTimeOffset.TryParse(tsString, out var dto))
            {
                return dto;
            }
        }
        return DateTimeOffset.UtcNow;
    }

    #endregion

    #region Helpers

    private async Task SendAsync(string message, CancellationToken ct)
    {
        if (_ws == null || _ws.State != WebSocketState.Open)
            throw new InvalidOperationException("WebSocket is not connected");

        var bytes = Encoding.UTF8.GetBytes(message);
        await _ws.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, ct).ConfigureAwait(false);
    }

    #endregion
}
