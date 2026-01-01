using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using MarketDataCollector.Application.Config;
using MarketDataCollector.Application.Logging;
using MarketDataCollector.Domain.Collectors;
using MarketDataCollector.Domain.Models;
using Serilog;

namespace MarketDataCollector.Infrastructure.Providers.Alpaca;

/// <summary>
/// Alpaca Market Data client (WebSocket) that implements the IMarketDataClient abstraction.
///
/// Current support:
/// - Trades: YES (streams "t" messages and forwards to TradeDataCollector)
/// - Depth (L2): NO (Alpaca stock stream provides quotes/BBO, not full L2 updates; method returns -1)
///
/// Notes:
/// - Alpaca typically limits to 1 active stream connection per user per endpoint.
/// - Authentication is performed by sending an "auth" message immediately after connect.
/// </summary>
public sealed class AlpacaMarketDataClient : IMarketDataClient
{
    private readonly ILogger _log = LoggingSetup.ForContext<AlpacaMarketDataClient>();
    private readonly TradeDataCollector _tradeCollector;
    private readonly QuoteCollector _quoteCollector;
    private readonly AlpacaOptions _opt;

    private ClientWebSocket? _ws;
    private Task? _recvLoop;
    private CancellationTokenSource? _cts;

    private readonly object _gate = new();
    private readonly HashSet<string> _tradeSymbols = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _quoteSymbols = new(StringComparer.OrdinalIgnoreCase);

    // Cached serializer options to avoid allocations in hot path
    private static readonly JsonSerializerOptions s_serializerOptions = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private int _nextSubId = 100_000; // keep away from IB ids
    private readonly Dictionary<int, (string Symbol, string Kind)> _subs = new();

    public AlpacaMarketDataClient(TradeDataCollector tradeCollector, QuoteCollector quoteCollector, AlpacaOptions opt)
    {
        _tradeCollector = tradeCollector;
        _quoteCollector = quoteCollector ?? throw new ArgumentNullException(nameof(quoteCollector));
        _opt = opt ?? throw new ArgumentNullException(nameof(opt));
        if (string.IsNullOrWhiteSpace(_opt.KeyId) || string.IsNullOrWhiteSpace(_opt.SecretKey))
            throw new ArgumentException("Alpaca KeyId/SecretKey required.");
    }

    public bool IsEnabled => true;

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        if (_ws != null) return;

        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _ws = new ClientWebSocket();

        var host = _opt.UseSandbox ? "stream.data.sandbox.alpaca.markets" : "stream.data.alpaca.markets";
        var uri = new Uri($"wss://{host}/v2/{_opt.Feed}");

        _log.Information("Connecting to Alpaca WebSocket at {Uri} (Sandbox: {UseSandbox})", uri, _opt.UseSandbox);

        try
        {
            await _ws.ConnectAsync(uri, ct).ConfigureAwait(false);
            _log.Information("Successfully connected to Alpaca WebSocket");
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Failed to connect to Alpaca WebSocket at {Uri}. " +
                "Troubleshooting: 1) Check your internet connection. 2) Verify Alpaca service status at status.alpaca.markets. " +
                "3) Ensure your API keys are valid and not expired.", uri);
            throw;
        }

        // Authenticate via message (must be within ~10 seconds of connection)
        var authMsg = JsonSerializer.Serialize(new { action = "auth", key = _opt.KeyId, secret = _opt.SecretKey });
        await SendTextAsync(authMsg, ct).ConfigureAwait(false);
        _log.Debug("Authentication message sent to Alpaca");

        _recvLoop = Task.Run(() => ReceiveLoopAsync(_cts.Token), _cts.Token);
    }

    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        _log.Information("Disconnecting from Alpaca WebSocket");

        var ws = _ws;
        var cts = _cts;

        _ws = null;
        _cts = null;

        if (cts != null)
        {
            try { cts.Cancel(); } catch { }
            try { cts.Dispose(); } catch { }
        }

        if (ws != null)
        {
            try
            {
                if (ws.State is WebSocketState.Open or WebSocketState.CloseReceived)
                    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "shutdown", ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "Error during WebSocket close, connection may have been lost");
            }
            try { ws.Dispose(); } catch { }
        }

        if (_recvLoop != null)
        {
            try { await _recvLoop.ConfigureAwait(false); } catch { }
        }
        _recvLoop = null;

        _log.Information("Disconnected from Alpaca WebSocket");
    }

    public int SubscribeTrades(SymbolConfig cfg)
    {
        if (cfg is null) throw new ArgumentNullException(nameof(cfg));
        var symbol = cfg.Symbol.Trim();
        if (symbol.Length == 0) return -1;

        var id = Interlocked.Increment(ref _nextSubId);
        lock (_gate)
        {
            _tradeSymbols.Add(symbol);
            _subs[id] = (symbol, "trades");
        }

        _ = TrySendSubscribeAsync();
        return id;
    }

    public void UnsubscribeTrades(int subscriptionId)
    {
        (string Symbol, string Kind) sub;
        lock (_gate)
        {
            if (!_subs.TryGetValue(subscriptionId, out sub)) return;
            _subs.Remove(subscriptionId);
            if (sub.Kind == "trades")
            {
                // remove only if no remaining trade subs for this symbol
                if (!_subs.Values.Any(v => v.Kind == "trades" && v.Symbol.Equals(sub.Symbol, StringComparison.OrdinalIgnoreCase)))
                    _tradeSymbols.Remove(sub.Symbol);
            }
        }

        _ = TrySendSubscribeAsync(); // send updated subscription set
    }

    public int SubscribeMarketDepth(SymbolConfig cfg)
    {
        // Not supported for stocks: Alpaca provides quotes, not full L2 depth updates.
        // If you later add QuoteCollector -> L2Snapshot mapping, wire it here.
        if (_opt.SubscribeQuotes)
        {
            var symbol = cfg.Symbol.Trim();
            if (symbol.Length == 0) return -1;

            var id = Interlocked.Increment(ref _nextSubId);
            lock (_gate)
            {
                _quoteSymbols.Add(symbol);
                _subs[id] = (symbol, "quotes");
            }
            _ = TrySendSubscribeAsync();
            return id;
        }

        return -1;
    }

    public void UnsubscribeMarketDepth(int subscriptionId)
    {
        (string Symbol, string Kind) sub;
        lock (_gate)
        {
            if (!_subs.TryGetValue(subscriptionId, out sub)) return;
            _subs.Remove(subscriptionId);
            if (sub.Kind == "quotes")
            {
                if (!_subs.Values.Any(v => v.Kind == "quotes" && v.Symbol.Equals(sub.Symbol, StringComparison.OrdinalIgnoreCase)))
                    _quoteSymbols.Remove(sub.Symbol);
            }
        }

        _ = TrySendSubscribeAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync().ConfigureAwait(false);
    }

    private async Task TrySendSubscribeAsync()
    {
        try
        {
            var ws = _ws;
            if (ws == null || ws.State != WebSocketState.Open) return;

            string[] trades;
            string[] quotes;
            lock (_gate)
            {
                trades = _tradeSymbols.ToArray();
                quotes = _quoteSymbols.ToArray();
            }

            var msg = new Dictionary<string, object?>
            {
                ["action"] = "subscribe",
                ["trades"] = trades.Length == 0 ? null : trades
            };

            if (_opt.SubscribeQuotes && quotes.Length > 0)
                msg["quotes"] = quotes;

            var json = JsonSerializer.Serialize(msg, s_serializerOptions);
            await SendTextAsync(json, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Failed to send subscription update to Alpaca WebSocket. " +
                "This may indicate a connection issue. Check network connectivity and Alpaca service status.");
        }
    }

    private async Task SendTextAsync(string text, CancellationToken ct)
    {
        var ws = _ws ?? throw new InvalidOperationException("Not connected.");
        var bytes = Encoding.UTF8.GetBytes(text);
        await ws.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, ct).ConfigureAwait(false);
    }

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        if (_ws == null) return;

        var ws = _ws;
        var buf = new byte[64 * 1024];
        var sb = new StringBuilder(128 * 1024);

        while (!ct.IsCancellationRequested && ws.State == WebSocketState.Open)
        {
            sb.Clear();

            WebSocketReceiveResult? res;
            do
            {
                res = await ws.ReceiveAsync(buf, ct).ConfigureAwait(false);
                if (res.MessageType == WebSocketMessageType.Close) return;
                sb.Append(Encoding.UTF8.GetString(buf, 0, res.Count));
            }
            while (!res.EndOfMessage);

            var json = sb.ToString();
            if (string.IsNullOrWhiteSpace(json)) continue;

            // Alpaca sends arrays of objects: [{"T":"success",...}, {"T":"t",...}]
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var el in doc.RootElement.EnumerateArray())
                        HandleMessage(el);
                }
                else if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    HandleMessage(doc.RootElement);
                }
            }
            catch (JsonException ex)
            {
                _log.Warning(ex, "Failed to parse Alpaca WebSocket message. Raw JSON: {RawJson}. " +
                    "This may indicate a protocol change or malformed message.",
                    json.Length > 500 ? json[..500] + "..." : json);
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Unexpected error processing Alpaca WebSocket message");
            }
        }
    }

    private void HandleMessage(JsonElement el)
    {
        // Trades: "T":"t" (per Alpaca docs)
        if (!el.TryGetProperty("T", out var tProp)) return;
        var t = tProp.GetString();
        if (t == "t")
        {
            var sym = el.TryGetProperty("S", out var sProp) ? sProp.GetString() : null;
            if (string.IsNullOrWhiteSpace(sym)) return;

            var price = el.TryGetProperty("p", out var pProp) ? (decimal)pProp.GetDouble() : 0m;
            var size = el.TryGetProperty("s", out var szProp) ? szProp.GetInt32() : 0;
            var ts = el.TryGetProperty("t", out var tsProp) ? tsProp.GetString() : null;
            var venue = el.TryGetProperty("x", out var xProp) ? xProp.GetString() : null;
            var tradeId = el.TryGetProperty("i", out var iProp) ? iProp.GetInt64() : 0;

            DateTimeOffset dto;
            if (!DateTimeOffset.TryParse(ts, out dto))
                dto = DateTimeOffset.UtcNow;

            var update = new MarketTradeUpdate(
                Timestamp: dto,
                Symbol: sym!,
                Price: price,
                Size: size,
                Aggressor: AggressorSide.Unknown,
                SequenceNumber: tradeId <= 0 ? null : tradeId,
                StreamId: "ALPACA",
                Venue: venue ?? "ALPACA"
            );

            _tradeCollector.OnTrade(update);
        }

        // TODO: Wire Alpaca quotes ("T":"q") into the L2 collector
        // Alpaca quotes contain bid/ask price and size which can be converted to BboQuotePayload
        // Steps to implement:
        // 1. Parse quote messages (T="q") with fields: S=symbol, bp=bidPrice, bs=bidSize, ap=askPrice, as=askSize, t=timestamp
        // 2. Create MarketQuoteUpdate from the parsed data
        // 3. Forward to _quoteCollector.OnQuote(update) to publish BBO events
        // 4. Consider creating L2SnapshotPayload from aggregated quotes if full depth is needed
        if (t == "q")
        {
            // Quote parsing is available but not yet wired to collectors
            // Uncomment and complete implementation when ready:
            // var sym = el.TryGetProperty("S", out var sProp) ? sProp.GetString() : null;
            // var bidPrice = el.TryGetProperty("bp", out var bp) ? (decimal)bp.GetDouble() : 0m;
            // var askPrice = el.TryGetProperty("ap", out var ap) ? (decimal)ap.GetDouble() : 0m;
            // ... wire to _quoteCollector
        }
    }
}
