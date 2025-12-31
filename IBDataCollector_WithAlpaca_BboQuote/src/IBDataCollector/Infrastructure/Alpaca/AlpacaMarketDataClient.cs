\
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using IBDataCollector.Application.Config;
using IBDataCollector.Domain.Collectors;
using IBDataCollector.Domain.Models;
using IBDataCollector.Infrastructure.IB;

namespace IBDataCollector.Infrastructure.Alpaca;

// TODO: Implement connection retry with exponential backoff for production resilience
// TODO: Add automatic reconnection when WebSocket connection is lost
// TODO: Add heartbeat/keep-alive mechanism to detect stale connections
// TODO: Consider moving API credentials to environment variables or secure vault instead of config file

/// <summary>
/// Alpaca Market Data client (WebSocket) that plugs into the existing IIBMarketDataClient abstraction.
///
/// Current support:
/// - Trades: YES (streams "t" messages and forwards to TradeDataCollector)
/// - Depth (L2): NO (Alpaca stock stream provides quotes/BBO, not full L2 updates; method returns -1)
///
/// Notes:
/// - Alpaca typically limits to 1 active stream connection per user per endpoint.
/// - Authentication is performed by sending an "auth" message immediately after connect.
/// </summary>
public sealed class AlpacaMarketDataClient : IIBMarketDataClient
{
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

        await _ws.ConnectAsync(uri, ct).ConfigureAwait(false);

        // Authenticate via message (must be within ~10 seconds of connection)
        var authMsg = JsonSerializer.Serialize(new { action = "auth", key = _opt.KeyId, secret = _opt.SecretKey });
        await SendTextAsync(authMsg, ct).ConfigureAwait(false);

        _recvLoop = Task.Run(() => ReceiveLoopAsync(_cts.Token), _cts.Token);
    }

    public async Task DisconnectAsync(CancellationToken ct = default)
    {
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
            catch { }
            try { ws.Dispose(); } catch { }
        }

        if (_recvLoop != null)
        {
            try { await _recvLoop.ConfigureAwait(false); } catch { }
        }
        _recvLoop = null;
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
            // TODO: Add proper logging - caller is fire-and-forget but errors should still be observable
            // Consider publishing connection status events for monitoring
            _ = ex; // Suppress unused variable warning until logging is added
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
            catch (Exception ex)
            {
                // TODO: Log JSON parse errors - silent failures can hide protocol issues
                _ = ex; // Suppress unused variable warning until logging is added
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

        if (t == "q")
        {
            var sym = el.TryGetProperty("S", out var sProp) ? sProp.GetString() : null;
            if (string.IsNullOrWhiteSpace(sym)) return;

            var bidPrice = el.TryGetProperty("bp", out var bpProp) ? (decimal)bpProp.GetDouble() : 0m;
            var askPrice = el.TryGetProperty("ap", out var apProp) ? (decimal)apProp.GetDouble() : 0m;
            var bidSize = el.TryGetProperty("bs", out var bsProp) ? bsProp.GetInt64() : 0L;
            var askSize = el.TryGetProperty("as", out var asProp) ? asProp.GetInt64() : 0L;
            var ts = el.TryGetProperty("t", out var tsProp) ? tsProp.GetString() : null;
            var venue = el.TryGetProperty("x", out var xProp) ? xProp.GetString() : null;

            if (bidPrice <= 0m && askPrice <= 0m)
                return; // ignore empty quotes

            DateTimeOffset dto;
            if (!DateTimeOffset.TryParse(ts, out dto))
                dto = DateTimeOffset.UtcNow;

            var update = new MarketQuoteUpdate(
                Timestamp: dto,
                Symbol: sym!,
                BidPrice: bidPrice,
                BidSize: bidSize,
                AskPrice: askPrice,
                AskSize: askSize,
                SequenceNumber: null,
                StreamId: "ALPACA",
                Venue: venue ?? "ALPACA"
            );

            _quoteCollector.OnQuote(update);
        }
    }
}
