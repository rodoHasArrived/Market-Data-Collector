using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using MarketDataCollector.Infrastructure.Plugins.Core;
using MarketDataCollector.Infrastructure.Plugins.Base;
using MarketDataCollector.Infrastructure.Plugins.Discovery;
using Serilog;

namespace MarketDataCollector.Infrastructure.Plugins.Providers.Polygon;

/// <summary>
/// Polygon.io market data plugin supporting both real-time streaming and historical data.
/// Provides trades, quotes, and aggregates for US stocks, options, forex, and crypto.
/// </summary>
[MarketDataPlugin(
    id: "polygon",
    displayName: "Polygon.io",
    type: PluginType.Hybrid,
    Category = PluginCategory.DataVendor,
    Priority = 10)]
public sealed class PolygonPlugin : MarketDataPluginBase
{
    private const string LiveEndpoint = "wss://socket.polygon.io";
    private const string DelayedEndpoint = "wss://delayed.polygon.io";
    private const string RestBaseUrl = "https://api.polygon.io";

    private readonly HttpClient _httpClient;
    private ClientWebSocket? _webSocket;
    private CancellationTokenSource? _receiveCts;
    private Task? _receiveTask;

    private string _apiKey = string.Empty;
    private string _feed = "stocks";
    private bool _useDelayed;
    private bool _subscribeTrades = true;
    private bool _subscribeQuotes;
    private bool _subscribeAggregates;

    private readonly SemaphoreSlim _rateLimiter = new(1, 1);
    private readonly Queue<DateTimeOffset> _requestTimes = new();
    private int _maxRequestsPerMinute = 5; // Free tier default

    public override string Id => "polygon";
    public override string DisplayName => "Polygon.io";
    public override string Description => "Real-time and historical market data from Polygon.io";
    public override string Version => "1.0.0";

    public override PluginCapabilities Capabilities { get; protected set; } = new()
    {
        SupportsRealtime = true,
        SupportsHistorical = true,
        SupportsTrades = true,
        SupportsQuotes = true,
        SupportsBars = true,
        SupportsAdjustedPrices = true,
        SupportsDividends = true,
        SupportsSplits = true,
        SupportedMarkets = new[] { "US" },
        SupportedAssetClasses = new[] { "Equity", "Option", "Forex", "Crypto" },
        SupportedBarIntervals = new[] { "1min", "5min", "15min", "30min", "1hour", "1day", "1week", "1month" },
        MaxSymbolsPerSubscription = 1000,
        RequiresAuthentication = true
    };

    public PolygonPlugin() : base(Log.ForContext<PolygonPlugin>())
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(RestBaseUrl),
            Timeout = TimeSpan.FromSeconds(30)
        };
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "MarketDataCollector/1.0");
    }

    protected override async Task OnInitializeAsync(IPluginConfig config, CancellationToken ct)
    {
        _apiKey = config.Get("POLYGON__APIKEY", string.Empty);
        if (string.IsNullOrEmpty(_apiKey))
        {
            _apiKey = Environment.GetEnvironmentVariable("POLYGON_API_KEY") ?? string.Empty;
        }

        _feed = config.Get("POLYGON__FEED", "stocks");
        _useDelayed = config.Get("POLYGON__USE_DELAYED", false);
        _subscribeTrades = config.Get("POLYGON__SUBSCRIBE_TRADES", true);
        _subscribeQuotes = config.Get("POLYGON__SUBSCRIBE_QUOTES", false);
        _subscribeAggregates = config.Get("POLYGON__SUBSCRIBE_AGGREGATES", false);
        _maxRequestsPerMinute = config.Get("POLYGON__RATE_LIMIT", 5);

        // Validate API key if provided
        if (!string.IsNullOrEmpty(_apiKey) && _apiKey.Length >= 10)
        {
            Logger.Information("Polygon plugin initialized with API key");
        }
        else
        {
            Logger.Warning("Polygon plugin running in stub mode (no valid API key)");
        }

        await Task.CompletedTask;
    }

    public override async IAsyncEnumerable<MarketDataEvent> StreamAsync(
        DataStreamRequest request,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (request.Type == DataType.Bar)
        {
            // Historical data via REST API
            await foreach (var evt in StreamHistoricalAsync(request, ct))
            {
                yield return evt;
            }
        }
        else
        {
            // Real-time streaming via WebSocket
            await foreach (var evt in StreamRealtimeAsync(request, ct))
            {
                yield return evt;
            }
        }
    }

    private async IAsyncEnumerable<MarketDataEvent> StreamHistoricalAsync(
        DataStreamRequest request,
        [EnumeratorCancellation] CancellationToken ct)
    {
        foreach (var symbol in request.Symbols)
        {
            await WaitForRateLimitAsync(ct);

            var from = request.From?.ToString("yyyy-MM-dd") ?? DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-1)).ToString("yyyy-MM-dd");
            var to = request.To?.ToString("yyyy-MM-dd") ?? DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");
            var multiplier = GetMultiplier(request.Interval);
            var timespan = GetTimespan(request.Interval);

            var url = $"/v2/aggs/ticker/{symbol}/range/{multiplier}/{timespan}/{from}/{to}?adjusted=true&sort=asc&limit=50000&apiKey={_apiKey}";

            using var response = await _httpClient.GetAsync(url, ct);

            if ((int)response.StatusCode == 429)
            {
                Logger.Warning("Polygon rate limit exceeded for {Symbol}", symbol);
                RecordHealth(false, "Rate limit exceeded");
                continue;
            }

            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync(ct);
            var data = JsonSerializer.Deserialize<PolygonAggregateResponse>(json);

            if (data?.Results == null)
            {
                Logger.Warning("No data returned for {Symbol}", symbol);
                continue;
            }

            RecordHealth(true);

            foreach (var bar in data.Results)
            {
                var timestamp = DateTimeOffset.FromUnixTimeMilliseconds(bar.Timestamp);
                yield return MarketDataEvent.Bar(
                    symbol: symbol,
                    timestamp: timestamp,
                    open: bar.Open,
                    high: bar.High,
                    low: bar.Low,
                    close: bar.Close,
                    volume: bar.Volume,
                    interval: request.Interval ?? BarInterval.Daily
                );
            }

            Logger.Information("Retrieved {Count} bars for {Symbol}", data.Results.Length, symbol);
        }
    }

    private async IAsyncEnumerable<MarketDataEvent> StreamRealtimeAsync(
        DataStreamRequest request,
        [EnumeratorCancellation] CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_apiKey))
        {
            Logger.Warning("Polygon WebSocket streaming requires API key - running in stub mode");
            yield return MarketDataEvent.Status("polygon", "stub_mode", "No API key configured");
            yield break;
        }

        var endpoint = _useDelayed ? DelayedEndpoint : LiveEndpoint;
        var wsUrl = $"{endpoint}/{_feed}";

        _webSocket = new ClientWebSocket();
        _receiveCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        try
        {
            await _webSocket.ConnectAsync(new Uri(wsUrl), ct);
            Logger.Information("Connected to Polygon WebSocket: {Url}", wsUrl);

            // Authenticate
            await SendMessageAsync(new { action = "auth", @params = _apiKey }, ct);

            // Wait for auth confirmation
            var authResponse = await ReceiveMessageAsync(ct);
            if (authResponse?.Contains("\"status\":\"auth_success\"") != true)
            {
                Logger.Error("Polygon authentication failed");
                RecordHealth(false, "Authentication failed");
                yield break;
            }

            Logger.Information("Polygon authentication successful");
            RecordHealth(true);

            // Subscribe to requested symbols
            var subscriptions = new List<string>();
            foreach (var symbol in request.Symbols)
            {
                if (_subscribeTrades || request.Type == DataType.Trade)
                    subscriptions.Add($"T.{symbol}");
                if (_subscribeQuotes || request.Type == DataType.Quote)
                    subscriptions.Add($"Q.{symbol}");
                if (_subscribeAggregates)
                    subscriptions.Add($"AM.{symbol}");
            }

            if (subscriptions.Count > 0)
            {
                await SendMessageAsync(new { action = "subscribe", @params = string.Join(",", subscriptions) }, ct);
                Logger.Information("Subscribed to {Count} channels", subscriptions.Count);
            }

            // Stream messages
            var buffer = new byte[8192];
            while (!ct.IsCancellationRequested && _webSocket.State == WebSocketState.Open)
            {
                var result = await _webSocket.ReceiveAsync(buffer, ct);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    Logger.Information("Polygon WebSocket closed by server");
                    break;
                }

                var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                var events = ParseWebSocketMessage(message);

                foreach (var evt in events)
                {
                    yield return evt;
                }
            }
        }
        finally
        {
            if (_webSocket?.State == WebSocketState.Open)
            {
                await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
            }
            _webSocket?.Dispose();
            _webSocket = null;
        }
    }

    private IEnumerable<MarketDataEvent> ParseWebSocketMessage(string message)
    {
        try
        {
            using var doc = JsonDocument.Parse(message);

            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                yield break;

            foreach (var element in doc.RootElement.EnumerateArray())
            {
                var eventType = element.GetProperty("ev").GetString();
                var symbol = element.TryGetProperty("sym", out var symProp) ? symProp.GetString() : null;

                if (string.IsNullOrEmpty(symbol))
                    continue;

                switch (eventType)
                {
                    case "T": // Trade
                        yield return MarketDataEvent.Trade(
                            symbol: symbol,
                            timestamp: DateTimeOffset.FromUnixTimeMilliseconds(element.GetProperty("t").GetInt64()),
                            price: element.GetProperty("p").GetDecimal(),
                            size: element.GetProperty("s").GetDecimal(),
                            exchange: element.TryGetProperty("x", out var xProp) ? xProp.GetInt32().ToString() : null
                        );
                        break;

                    case "Q": // Quote
                        yield return MarketDataEvent.Quote(
                            symbol: symbol,
                            timestamp: DateTimeOffset.FromUnixTimeMilliseconds(element.GetProperty("t").GetInt64()),
                            bidPrice: element.GetProperty("bp").GetDecimal(),
                            bidSize: element.GetProperty("bs").GetDecimal(),
                            askPrice: element.GetProperty("ap").GetDecimal(),
                            askSize: element.GetProperty("as").GetDecimal()
                        );
                        break;

                    case "AM": // Aggregate Minute
                        yield return MarketDataEvent.Bar(
                            symbol: symbol,
                            timestamp: DateTimeOffset.FromUnixTimeMilliseconds(element.GetProperty("s").GetInt64()),
                            open: element.GetProperty("o").GetDecimal(),
                            high: element.GetProperty("h").GetDecimal(),
                            low: element.GetProperty("l").GetDecimal(),
                            close: element.GetProperty("c").GetDecimal(),
                            volume: element.GetProperty("v").GetDecimal(),
                            interval: BarInterval.OneMinute
                        );
                        break;
                }
            }
        }
        catch (JsonException ex)
        {
            Logger.Warning(ex, "Failed to parse Polygon WebSocket message");
        }
    }

    private async Task SendMessageAsync(object message, CancellationToken ct)
    {
        if (_webSocket?.State != WebSocketState.Open)
            return;

        var json = JsonSerializer.Serialize(message);
        var bytes = Encoding.UTF8.GetBytes(json);
        await _webSocket.SendAsync(bytes, WebSocketMessageType.Text, true, ct);
    }

    private async Task<string?> ReceiveMessageAsync(CancellationToken ct)
    {
        if (_webSocket?.State != WebSocketState.Open)
            return null;

        var buffer = new byte[4096];
        var result = await _webSocket.ReceiveAsync(buffer, ct);
        return Encoding.UTF8.GetString(buffer, 0, result.Count);
    }

    private async Task WaitForRateLimitAsync(CancellationToken ct)
    {
        await _rateLimiter.WaitAsync(ct);
        try
        {
            var now = DateTimeOffset.UtcNow;
            var windowStart = now.AddMinutes(-1);

            // Remove old requests
            while (_requestTimes.Count > 0 && _requestTimes.Peek() < windowStart)
            {
                _requestTimes.Dequeue();
            }

            // Wait if at limit
            if (_requestTimes.Count >= _maxRequestsPerMinute)
            {
                var oldestRequest = _requestTimes.Peek();
                var waitTime = oldestRequest.AddMinutes(1) - now;
                if (waitTime > TimeSpan.Zero)
                {
                    Logger.Debug("Rate limit reached, waiting {WaitMs}ms", waitTime.TotalMilliseconds);
                    await Task.Delay(waitTime, ct);
                }
            }

            _requestTimes.Enqueue(DateTimeOffset.UtcNow);
        }
        finally
        {
            _rateLimiter.Release();
        }
    }

    private static int GetMultiplier(BarInterval? interval) => interval switch
    {
        BarInterval.OneMinute => 1,
        BarInterval.FiveMinute => 5,
        BarInterval.FifteenMinute => 15,
        BarInterval.ThirtyMinute => 30,
        BarInterval.OneHour => 1,
        BarInterval.Daily => 1,
        BarInterval.Weekly => 1,
        BarInterval.Monthly => 1,
        _ => 1
    };

    private static string GetTimespan(BarInterval? interval) => interval switch
    {
        BarInterval.OneMinute or BarInterval.FiveMinute or BarInterval.FifteenMinute or BarInterval.ThirtyMinute => "minute",
        BarInterval.OneHour => "hour",
        BarInterval.Weekly => "week",
        BarInterval.Monthly => "month",
        _ => "day"
    };

    protected override async ValueTask OnDisposeAsync()
    {
        _receiveCts?.Cancel();
        _receiveCts?.Dispose();

        if (_receiveTask != null)
        {
            try { await _receiveTask; } catch { /* ignore */ }
        }

        if (_webSocket?.State == WebSocketState.Open)
        {
            await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disposing", CancellationToken.None);
        }
        _webSocket?.Dispose();

        _httpClient.Dispose();
        _rateLimiter.Dispose();
    }

    // Response DTOs
    private sealed class PolygonAggregateResponse
    {
        public string? Ticker { get; set; }
        public int QueryCount { get; set; }
        public int ResultsCount { get; set; }
        public bool Adjusted { get; set; }
        public PolygonBar[]? Results { get; set; }
        public string? Status { get; set; }
        public string? RequestId { get; set; }
        public int Count { get; set; }
    }

    private sealed class PolygonBar
    {
        public decimal Open { get => o; set => o = value; }
        public decimal High { get => h; set => h = value; }
        public decimal Low { get => l; set => l = value; }
        public decimal Close { get => c; set => c = value; }
        public decimal Volume { get => v; set => v = value; }
        public decimal Vwap { get => vw; set => vw = value; }
        public long Timestamp { get => t; set => t = value; }
        public int Transactions { get => n; set => n = value; }

        // JSON property names
        public decimal o { get; set; }
        public decimal h { get; set; }
        public decimal l { get; set; }
        public decimal c { get; set; }
        public decimal v { get; set; }
        public decimal vw { get; set; }
        public long t { get; set; }
        public int n { get; set; }
    }
}
