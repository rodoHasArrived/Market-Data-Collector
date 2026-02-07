using System.Buffers;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using PolygonOptions = MarketDataCollector.Application.Config.PolygonOptions;
using MarketDataCollector.Application.Exceptions;
using MarketDataCollector.Application.Logging;
using MarketDataCollector.Domain.Collectors;
using MarketDataCollector.Domain.Events;
using MarketDataCollector.Contracts.Domain.Models;
using MarketDataCollector.Domain.Models;
using MarketDataCollector.Infrastructure.Contracts;
using MarketDataCollector.Infrastructure.DataSources;
using MarketDataCollector.Infrastructure.Providers;
using MarketDataCollector.Infrastructure.Providers.Core;
using MarketDataCollector.Infrastructure.Resilience;
using MarketDataCollector.Infrastructure.Shared;
using Serilog;

namespace MarketDataCollector.Infrastructure.Providers.Polygon;

/// <summary>
/// Polygon.io market data adapter implementing the IMarketDataClient abstraction.
/// Supports full WebSocket streaming for trades, quotes, and aggregates.
///
/// Current support:
/// - Trades: YES (streams "T" messages and forwards to TradeDataCollector)
/// - Quotes: YES (streams "Q" messages and forwards to QuoteCollector)
/// - Aggregates: YES (streams "A" and "AM" messages for second/minute bars)
///
/// Connection Resilience:
/// - Uses Polly-based WebSocketResiliencePolicy for connection retry with exponential backoff
/// - Implements circuit breaker pattern to prevent cascading failures
/// - Automatic reconnection on connection loss with jitter
/// - Configurable retry attempts (default: 5) with 2s base delay, max 30s between retries
///
/// Polygon WebSocket Protocol:
/// - Endpoint: wss://socket.polygon.io/{feed} (stocks, options, forex, crypto)
/// - Auth: Send {"action":"auth","params":"{apiKey}"} after connect
/// - Subscribe: {"action":"subscribe","params":"T.AAPL,Q.AAPL"}
/// - Message types: T=trade, Q=quote, A=aggregate, AM=minute aggregate
/// </summary>
[DataSource("polygon", "Polygon.io", DataSourceType.Realtime, DataSourceCategory.Aggregator,
    Priority = 15, Description = "WebSocket streaming from Polygon.io for trades, quotes, and aggregates")]
[ImplementsAdr("ADR-001", "Polygon.io streaming data provider implementation")]
[ImplementsAdr("ADR-004", "All async methods support CancellationToken")]
[ImplementsAdr("ADR-005", "Attribute-based provider discovery")]
public sealed class PolygonMarketDataClient : IMarketDataClient
{
    private readonly ILogger _log = LoggingSetup.ForContext<PolygonMarketDataClient>();
    private readonly IMarketEventPublisher _publisher;
    private readonly TradeDataCollector _tradeCollector;
    private readonly QuoteCollector _quoteCollector;
    private readonly PolygonOptions _options;

    // Use centralized configuration for resilience settings
    private readonly WebSocketConnectionConfig _connectionConfig = WebSocketConnectionConfig.Default;

    // Centralized subscription management with provider-specific ID range
    private readonly Infrastructure.Shared.SubscriptionManager _subscriptionManager = new(startingId: ProviderSubscriptionRanges.PolygonStart);

    // WebSocket connection - kept for protocol-specific handshake operations
    private ClientWebSocket? _ws;
    private CancellationTokenSource? _cts;
    private Task? _receiveLoop;
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    // Connection state
    private bool _isConnected;
    private bool _isAuthenticated;
    private long _messageSequence;
    private volatile bool _isDisposing;

    // Reconnection state
    private readonly SemaphoreSlim _reconnectGate = new(1, 1);
    private int _reconnectAttempts;
    private const int MaxReconnectAttempts = 10;
    private static readonly TimeSpan ReconnectBaseDelay = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan ReconnectMaxDelay = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Creates a new Polygon market data client.
    /// </summary>
    /// <param name="publisher">Event publisher for heartbeats and status.</param>
    /// <param name="tradeCollector">Collector for trade data.</param>
    /// <param name="quoteCollector">Collector for quote data.</param>
    /// <param name="options">Polygon configuration options. If null or missing ApiKey, runs in stub mode.</param>
    /// <exception cref="ArgumentNullException">If publisher, tradeCollector, or quoteCollector is null.</exception>
    public PolygonMarketDataClient(
        IMarketEventPublisher publisher,
        TradeDataCollector tradeCollector,
        QuoteCollector quoteCollector,
        PolygonOptions? options = null)
    {
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        _tradeCollector = tradeCollector ?? throw new ArgumentNullException(nameof(tradeCollector));
        _quoteCollector = quoteCollector ?? throw new ArgumentNullException(nameof(quoteCollector));
        _options = options ?? new PolygonOptions();

        // Validate API key format if provided
        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            ValidateApiKeyFormat(_options.ApiKey);
        }

        _log.Information(
            "Polygon client initialized (Mode: {Mode}, Feed: {Feed}, Trades: {Trades}, Quotes: {Quotes}, Aggregates: {Aggregates})",
            IsStubMode ? "Stub" : "Live",
            _options.Feed,
            _options.SubscribeTrades,
            _options.SubscribeQuotes,
            _options.SubscribeAggregates);
    }

    /// <summary>
    /// Minimum length for a valid Polygon API key.
    /// Polygon API keys are typically 32 characters, but we accept 20+ for flexibility.
    /// </summary>
    private const int MinApiKeyLength = 20;

    /// <summary>
    /// Gets whether the client has a valid API key configured.
    /// A valid API key must be non-empty and at least <see cref="MinApiKeyLength"/> characters.
    /// When false, the client operates in stub mode with synthetic data.
    /// </summary>
    public bool HasValidCredentials =>
        !string.IsNullOrWhiteSpace(_options.ApiKey) && _options.ApiKey.Length >= MinApiKeyLength;

    /// <summary>
    /// Gets whether the client is operating in stub mode (no real connection).
    /// </summary>
    public bool IsStubMode => !HasValidCredentials;

    /// <summary>
    /// Gets whether the client is enabled and ready to receive subscriptions.
    /// Returns true only when a valid API key (20+ characters) is configured.
    /// </summary>
    public bool IsEnabled => HasValidCredentials;

    #region IProviderMetadata

    /// <inheritdoc/>
    public string ProviderId => "polygon";

    /// <inheritdoc/>
    public string ProviderDisplayName => "Polygon.io Streaming";

    /// <inheritdoc/>
    public string ProviderDescription => "Real-time trades, quotes, and aggregates via Polygon.io WebSocket API";

    /// <inheritdoc/>
    public int ProviderPriority => 15;

    /// <inheritdoc/>
    public ProviderCapabilities ProviderCapabilities { get; } = ProviderCapabilities.Streaming(
        trades: true,
        quotes: true,
        depth: false) with
    {
        SupportedMarkets = new[] { "US" },
        MaxRequestsPerWindow = 5,
        RateLimitWindow = TimeSpan.FromMinutes(1),
        MinRequestDelay = TimeSpan.FromMilliseconds(12000)
    };

    /// <inheritdoc/>
    public ProviderCredentialField[] ProviderCredentialFields => new[]
    {
        new ProviderCredentialField("ApiKey", "POLYGON__APIKEY", "Polygon API Key", true)
    };

    /// <inheritdoc/>
    public string[] ProviderNotes => new[]
    {
        "Polygon provides comprehensive market data.",
        "Free tier has limited rate limits; paid plans offer more.",
        "Supports stocks, options, forex, and crypto."
    };

    /// <inheritdoc/>
    public string[] ProviderWarnings => new[]
    {
        "Free tier has 15-minute delayed data for most feeds."
    };

    #endregion

    /// <summary>
    /// Gets the current connection state.
    /// </summary>
    public bool IsConnected => _isConnected;

    /// <summary>
    /// Gets the configured feed type (stocks, options, forex, crypto).
    /// </summary>
    public string Feed => _options.Feed;

    /// <summary>
    /// Gets whether using delayed (15-minute) data.
    /// </summary>
    public bool UseDelayed => _options.UseDelayed;

    /// <summary>
    /// Validates the API key format.
    /// Polygon API keys are typically 32-character alphanumeric strings.
    /// </summary>
    private void ValidateApiKeyFormat(string apiKey)
    {
        // Polygon API keys are typically alphanumeric, 32 characters
        // But we'll be lenient and just check for reasonable length and no whitespace
        if (apiKey.Length < 10)
        {
            _log.Warning("Polygon API key appears too short ({Length} chars). Expected ~32 characters.", apiKey.Length);
        }

        if (apiKey.Any(char.IsWhiteSpace))
        {
            throw new ArgumentException("Polygon API key contains whitespace characters", nameof(apiKey));
        }

        _log.Debug("Polygon API key format validated (length: {Length})", apiKey.Length);
    }

    /// <summary>
    /// Connects to Polygon WebSocket stream.
    /// In stub mode, emits a synthetic heartbeat. In live mode, connects to the actual WebSocket.
    /// </summary>
    public async Task ConnectAsync(CancellationToken ct = default)
    {
        if (IsStubMode)
        {
            _log.Information(
                "Polygon client connecting in STUB mode (no API key configured). " +
                "Set Polygon:ApiKey in configuration or POLYGON__APIKEY environment variable for live data.");

            _isConnected = true;
            _publisher.TryPublish(MarketEvent.Heartbeat(DateTimeOffset.UtcNow, source: "PolygonStub"));
            return;
        }

        // Live mode - connect to Polygon WebSocket
        var endpoint = _options.UseDelayed
            ? $"wss://delayed.polygon.io/{_options.Feed}"
            : $"wss://socket.polygon.io/{_options.Feed}";

        _log.Information(
            "Polygon client connecting to {Endpoint} (Delayed: {UseDelayed})",
            endpoint,
            _options.UseDelayed);

        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        // Use centralized resilience configuration
        var connectionPipeline = WebSocketResiliencePolicy.CreateComprehensivePipeline(
            maxRetries: _connectionConfig.MaxRetries,
            retryBaseDelay: _connectionConfig.RetryBaseDelay,
            circuitBreakerFailureThreshold: _connectionConfig.CircuitBreakerFailureThreshold,
            circuitBreakerDuration: _connectionConfig.CircuitBreakerDuration,
            operationTimeout: _connectionConfig.OperationTimeout);

        await connectionPipeline.ExecuteAsync(async token =>
        {
            _ws = new ClientWebSocket();
            _ws.Options.KeepAliveInterval = TimeSpan.FromSeconds(30);

            await _ws.ConnectAsync(new Uri(endpoint), token).ConfigureAwait(false);

            _log.Debug("WebSocket connected, waiting for connection message");

            // Wait for initial connection message from Polygon
            await WaitForConnectionMessageAsync(token).ConfigureAwait(false);

            // Authenticate with API key
            await AuthenticateAsync(token).ConfigureAwait(false);

            _isConnected = true;
            _isAuthenticated = true;
            _reconnectAttempts = 0;

            // Start receive loop
            _receiveLoop = Task.Run(() => ReceiveLoopAsync(_cts.Token), CancellationToken.None);

            _publisher.TryPublish(MarketEvent.Heartbeat(DateTimeOffset.UtcNow, source: "Polygon"));

            _log.Information("Polygon WebSocket connected and authenticated successfully");

            // Re-subscribe to existing symbols after reconnection
            await ResubscribeAllAsync(token).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Waits for the initial connection message from Polygon.
    /// </summary>
    private async Task WaitForConnectionMessageAsync(CancellationToken ct)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(4096);
        try
        {
            var result = await _ws!.ReceiveAsync(buffer.AsMemory(), ct).ConfigureAwait(false);
            if (result.MessageType == WebSocketMessageType.Text)
            {
                var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                _log.Debug("Received connection message: {Message}", message);

                // Parse the message array
                using var doc = JsonDocument.Parse(message);
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var elem in doc.RootElement.EnumerateArray())
                    {
                        if (elem.TryGetProperty("ev", out var evProp) &&
                            evProp.GetString() == "status" &&
                            elem.TryGetProperty("status", out var statusProp) &&
                            statusProp.GetString() == "connected")
                        {
                            _log.Debug("Connection status confirmed");
                            return;
                        }
                    }
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>
    /// Authenticates with Polygon using the API key.
    /// </summary>
    private async Task AuthenticateAsync(CancellationToken ct)
    {
        var authMessage = JsonSerializer.Serialize(new { action = "auth", @params = _options.ApiKey });
        await SendMessageAsync(authMessage, ct).ConfigureAwait(false);

        _log.Debug("Sent authentication message, waiting for response");

        // Wait for auth response
        var buffer = ArrayPool<byte>.Shared.Rent(4096);
        try
        {
            var result = await _ws!.ReceiveAsync(buffer.AsMemory(), ct).ConfigureAwait(false);
            if (result.MessageType == WebSocketMessageType.Text)
            {
                var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                _log.Debug("Received auth response: {Message}", message);

                using var doc = JsonDocument.Parse(message);
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var elem in doc.RootElement.EnumerateArray())
                    {
                        if (elem.TryGetProperty("ev", out var evProp) && evProp.GetString() == "status")
                        {
                            var status = elem.TryGetProperty("status", out var statusProp) ? statusProp.GetString() : null;
                            var authMessage2 = elem.TryGetProperty("message", out var msgProp) ? msgProp.GetString() : null;

                            if (status == "auth_success")
                            {
                                _log.Information("Polygon authentication successful");
                                return;
                            }
                            else if (status == "auth_failed")
                            {
                                throw new ConnectionException(
                                    $"Polygon authentication failed: {authMessage2}",
                                    provider: "Polygon");
                            }
                        }
                    }
                }
            }

            throw new ConnectionException(
                "Did not receive valid authentication response from Polygon",
                provider: "Polygon");
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>
    /// Sends a message to the WebSocket.
    /// </summary>
    private async Task SendMessageAsync(string message, CancellationToken ct)
    {
        await _sendLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_ws?.State != WebSocketState.Open)
            {
                _log.Warning("Cannot send message - WebSocket not open (state: {State})", _ws?.State);
                return;
            }

            var bytes = Encoding.UTF8.GetBytes(message);
            await _ws.SendAsync(bytes.AsMemory(), WebSocketMessageType.Text, true, ct).ConfigureAwait(false);
            _log.Debug("Sent message: {Message}", message);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    /// <summary>
    /// Main receive loop for processing incoming WebSocket messages.
    /// </summary>
    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(65536);
        var messageBuilder = new List<byte>();

        try
        {
            while (!ct.IsCancellationRequested && _ws?.State == WebSocketState.Open)
            {
                ValueWebSocketReceiveResult result;
                messageBuilder.Clear();

                do
                {
                    result = await _ws.ReceiveAsync(buffer.AsMemory(), ct).ConfigureAwait(false);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        _log.Warning("Polygon WebSocket closed by server: {Status} {Description}",
                            _ws.CloseStatus, _ws.CloseStatusDescription);
                        _isConnected = false;
                        return;
                    }

                    messageBuilder.AddRange(buffer.Take(result.Count));
                }
                while (!result.EndOfMessage);

                if (result.MessageType == WebSocketMessageType.Text && messageBuilder.Count > 0)
                {
                    var message = Encoding.UTF8.GetString(messageBuilder.ToArray());
                    ProcessMessage(message);
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            _log.Debug("Polygon receive loop cancelled");
        }
        catch (WebSocketException ex)
        {
            _log.Error(ex, "Polygon WebSocket error in receive loop");
            _isConnected = false;

            // Trigger automatic reconnection on WebSocket errors
            if (!_isDisposing && !ct.IsCancellationRequested)
            {
                TryReconnectAsync()
                    .ObserveException(_log, "Polygon reconnection after WebSocket error");
            }
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Unexpected error in Polygon receive loop");
            _isConnected = false;

            // Trigger automatic reconnection on unexpected errors
            if (!_isDisposing && !ct.IsCancellationRequested)
            {
                TryReconnectAsync()
                    .ObserveException(_log, "Polygon reconnection after unexpected error");
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);

            // Handle server-initiated close (CloseReceived) with reconnection
            if (!_isDisposing && !ct.IsCancellationRequested && !_isConnected)
            {
                TryReconnectAsync()
                    .ObserveException(_log, "Polygon reconnection after server-initiated close");
            }
        }
    }

    /// <summary>
    /// Processes an incoming WebSocket message.
    /// </summary>
    private void ProcessMessage(string message)
    {
        try
        {
            using var doc = JsonDocument.Parse(message);

            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                _log.Warning("Unexpected message format (not array): {Message}", message.Length > 200 ? message[..200] : message);
                return;
            }

            foreach (var elem in doc.RootElement.EnumerateArray())
            {
                if (!elem.TryGetProperty("ev", out var evProp))
                    continue;

                var eventType = evProp.GetString();
                switch (eventType)
                {
                    case "T": // Trade
                        ProcessTrade(elem);
                        break;

                    case "Q": // Quote
                        ProcessQuote(elem);
                        break;

                    case "A": // Second aggregate
                        ProcessAggregate(elem, Domain.Models.AggregateTimeframe.Second);
                        break;

                    case "AM": // Minute aggregate
                        ProcessAggregate(elem, Domain.Models.AggregateTimeframe.Minute);
                        break;

                    case "status":
                        ProcessStatus(elem);
                        break;

                    default:
                        _log.Debug("Unhandled event type: {EventType}", eventType);
                        break;
                }
            }
        }
        catch (JsonException ex)
        {
            _log.Warning(ex, "Failed to parse Polygon message: {Message}", message.Length > 200 ? message[..200] : message);
        }
    }

    /// <summary>
    /// Processes a trade message from Polygon.
    /// Trade format: { "ev":"T", "sym":"AAPL", "p":150.25, "s":100, "t":1234567890000, "c":[12,37], "i":"trade_id", "x":4, "z":1 }
    /// </summary>
    private void ProcessTrade(JsonElement elem)
    {
        try
        {
            var symbol = elem.TryGetProperty("sym", out var symProp) ? symProp.GetString() : null;
            if (string.IsNullOrEmpty(symbol)) return;

            if (!_subscriptionManager.HasSubscription(symbol, "trades"))
                return;

            var price = elem.TryGetProperty("p", out var priceProp) ? priceProp.GetDecimal() : 0m;
            var size = elem.TryGetProperty("s", out var sizeProp) ? sizeProp.GetInt64() : 0L;
            var timestamp = elem.TryGetProperty("t", out var tsProp) ? tsProp.GetInt64() : 0L;
            var tradeId = elem.TryGetProperty("i", out var idProp) ? idProp.GetString() : null;
            var exchange = elem.TryGetProperty("x", out var xProp) ? xProp.GetInt32() : 0;

            // Parse conditions to determine aggressor side using comprehensive mapping
            var aggressor = AggressorSide.Unknown;
            if (elem.TryGetProperty("c", out var conditions) && conditions.ValueKind == JsonValueKind.Array)
            {
                var conditionCodes = conditions.EnumerateArray().Select(c => c.GetInt32());
                aggressor = MapConditionCodesToAggressor(conditionCodes);
            }

            var seq = Interlocked.Increment(ref _messageSequence);
            var trade = new MarketTradeUpdate(
                Timestamp: timestamp > 0
                    ? DateTimeOffset.FromUnixTimeMilliseconds(timestamp)
                    : DateTimeOffset.UtcNow,
                Symbol: symbol,
                Price: price,
                Size: size,
                Aggressor: aggressor,
                SequenceNumber: seq,
                StreamId: tradeId ?? $"POLYGON_{seq}",
                Venue: MapExchangeCode(exchange));

            _tradeCollector.OnTrade(trade);
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "Failed to process Polygon trade message");
        }
    }

    /// <summary>
    /// Processes a quote message from Polygon.
    /// Quote format: { "ev":"Q", "sym":"AAPL", "bp":150.20, "bs":100, "ap":150.25, "as":200, "t":1234567890000, "x":4 }
    /// </summary>
    private void ProcessQuote(JsonElement elem)
    {
        try
        {
            var symbol = elem.TryGetProperty("sym", out var symProp) ? symProp.GetString() : null;
            if (string.IsNullOrEmpty(symbol)) return;

            if (!_subscriptionManager.HasSubscription(symbol, "quotes"))
                return;

            var bidPrice = elem.TryGetProperty("bp", out var bpProp) ? bpProp.GetDecimal() : 0m;
            var bidSize = elem.TryGetProperty("bs", out var bsProp) ? bsProp.GetInt64() : 0L;
            var askPrice = elem.TryGetProperty("ap", out var apProp) ? apProp.GetDecimal() : 0m;
            var askSize = elem.TryGetProperty("as", out var asProp) ? asProp.GetInt64() : 0L;
            var timestamp = elem.TryGetProperty("t", out var tsProp) ? tsProp.GetInt64() : 0L;
            var exchange = elem.TryGetProperty("x", out var xProp) ? xProp.GetInt32() : 0;

            var ts = timestamp > 0
                ? DateTimeOffset.FromUnixTimeMilliseconds(timestamp)
                : DateTimeOffset.UtcNow;

            var quote = new MarketQuoteUpdate(
                Timestamp: ts,
                Symbol: symbol,
                BidPrice: bidPrice,
                BidSize: bidSize,
                AskPrice: askPrice,
                AskSize: askSize,
                SequenceNumber: null,
                StreamId: "POLYGON",
                Venue: MapExchangeCode(exchange));

            _quoteCollector.OnQuote(quote);
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "Failed to process Polygon quote message");
        }
    }

    /// <summary>
    /// Processes an aggregate bar message from Polygon.
    /// Aggregate format: { "ev":"A/AM", "sym":"AAPL", "o":150.25, "h":150.50, "l":150.00, "c":150.40, "v":1000, "vw":150.30, "s":1234567890000, "e":1234567891000, "n":50 }
    /// </summary>
    /// <param name="elem">The JSON element containing the aggregate data.</param>
    /// <param name="timeframe">The aggregate timeframe (Second or Minute).</param>
    private void ProcessAggregate(JsonElement elem, Domain.Models.AggregateTimeframe timeframe)
    {
        try
        {
            var symbol = elem.TryGetProperty("sym", out var symProp) ? symProp.GetString() : null;
            if (string.IsNullOrEmpty(symbol)) return;

            if (!_subscriptionManager.HasSubscription(symbol, "aggregates"))
                return;

            // Parse OHLCV data
            var open = elem.TryGetProperty("o", out var oProp) ? oProp.GetDecimal() : 0m;
            var high = elem.TryGetProperty("h", out var hProp) ? hProp.GetDecimal() : 0m;
            var low = elem.TryGetProperty("l", out var lProp) ? lProp.GetDecimal() : 0m;
            var close = elem.TryGetProperty("c", out var cProp) ? cProp.GetDecimal() : 0m;
            var volume = elem.TryGetProperty("v", out var vProp) ? vProp.GetInt64() : 0L;

            // Skip aggregates with invalid OHLC data
            if (open <= 0 || high <= 0 || low <= 0 || close <= 0)
            {
                _log.Debug("Skipping aggregate for {Symbol} with invalid OHLC data", symbol);
                return;
            }

            // Parse additional fields
            var vwap = elem.TryGetProperty("vw", out var vwProp) ? vwProp.GetDecimal() : 0m;
            var startTimestamp = elem.TryGetProperty("s", out var sProp) ? sProp.GetInt64() : 0L;
            var endTimestamp = elem.TryGetProperty("e", out var eProp) ? eProp.GetInt64() : 0L;
            var tradeCount = elem.TryGetProperty("n", out var nProp) ? nProp.GetInt32() : 0;

            // Convert timestamps
            var startTime = startTimestamp > 0
                ? DateTimeOffset.FromUnixTimeMilliseconds(startTimestamp)
                : DateTimeOffset.UtcNow;
            var endTime = endTimestamp > 0
                ? DateTimeOffset.FromUnixTimeMilliseconds(endTimestamp)
                : startTime.AddSeconds(timeframe == Domain.Models.AggregateTimeframe.Second ? 1 : 60);

            var seq = Interlocked.Increment(ref _messageSequence);
            var aggregateBar = new AggregateBar(
                Symbol: symbol,
                StartTime: startTime,
                EndTime: endTime,
                Open: open,
                High: high,
                Low: low,
                Close: close,
                Volume: volume,
                Vwap: vwap,
                TradeCount: tradeCount,
                Timeframe: timeframe,
                Source: "Polygon",
                SequenceNumber: seq);

            // Publish the aggregate bar event
            _publisher.TryPublish(MarketEvent.AggregateBar(endTime, symbol, aggregateBar, seq, "Polygon"));

            _log.Debug(
                "Processed {Timeframe} aggregate for {Symbol}: O={Open} H={High} L={Low} C={Close} V={Volume}",
                timeframe, symbol, open, high, low, close, volume);
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "Failed to process Polygon aggregate message");
        }
    }

    /// <summary>
    /// Processes a status message from Polygon.
    /// </summary>
    private void ProcessStatus(JsonElement elem)
    {
        var status = elem.TryGetProperty("status", out var statusProp) ? statusProp.GetString() : null;
        var message = elem.TryGetProperty("message", out var msgProp) ? msgProp.GetString() : null;

        _log.Debug("Polygon status: {Status} - {Message}", status, message);

        if (status == "success" && message?.Contains("subscribed") == true)
        {
            _log.Information("Polygon subscription confirmed: {Message}", message);
        }
    }

    /// <summary>
    /// Maps Polygon exchange codes to exchange names.
    /// </summary>
    private static string MapExchangeCode(int code)
    {
        return code switch
        {
            1 => "NYSE",
            2 => "AMEX",
            3 => "ARCA",
            4 => "NASDAQ",
            5 => "NASDAQ_BX",
            6 => "NASDAQ_PSX",
            7 => "BATS_Y",
            8 => "BATS",
            9 => "IEX",
            10 => "EDGX",
            11 => "EDGA",
            12 => "CHX",
            13 => "NSX",
            14 => "FINRA_ADF",
            15 => "CBOE",
            16 => "MEMX",
            17 => "MIAX",
            19 => "LTSE",
            _ => $"EX_{code}"
        };
    }

    /// <summary>
    /// Re-subscribes to all symbols after reconnection.
    /// </summary>
    private async Task ResubscribeAllAsync(CancellationToken ct)
    {
        var tradeSyms = _subscriptionManager.GetSymbolsByKind("trades");
        var quoteSyms = _subscriptionManager.GetSymbolsByKind("quotes");
        var aggregateSyms = _subscriptionManager.GetSymbolsByKind("aggregates");

        if (tradeSyms.Length > 0)
        {
            var channels = string.Join(",", tradeSyms.Select(s => $"T.{s}"));
            var subMessage = JsonSerializer.Serialize(new { action = "subscribe", @params = channels });
            await SendMessageAsync(subMessage, ct).ConfigureAwait(false);
            _log.Information("Re-subscribed to {Count} trade channels", tradeSyms.Length);
        }

        if (quoteSyms.Length > 0)
        {
            var channels = string.Join(",", quoteSyms.Select(s => $"Q.{s}"));
            var subMessage = JsonSerializer.Serialize(new { action = "subscribe", @params = channels });
            await SendMessageAsync(subMessage, ct).ConfigureAwait(false);
            _log.Information("Re-subscribed to {Count} quote channels", quoteSyms.Length);
        }

        if (aggregateSyms.Length > 0)
        {
            // Subscribe to both second (A) and minute (AM) aggregates
            var channels = string.Join(",", aggregateSyms.SelectMany(s => new[] { $"A.{s}", $"AM.{s}" }));
            var subMessage = JsonSerializer.Serialize(new { action = "subscribe", @params = channels });
            await SendMessageAsync(subMessage, ct).ConfigureAwait(false);
            _log.Information("Re-subscribed to {Count} aggregate channels", aggregateSyms.Length);
        }
    }

    /// <summary>
    /// Attempts automatic reconnection with exponential backoff and gating.
    /// On success, re-authenticates and resubscribes to all active symbols.
    /// </summary>
    private async Task TryReconnectAsync()
    {
        if (_isDisposing) return;

        if (!await _reconnectGate.WaitAsync(0))
        {
            _log.Debug("Polygon reconnection already in progress, skipping duplicate attempt");
            return;
        }

        try
        {
            // Cleanup old connection state
            _isConnected = false;
            _isAuthenticated = false;
            _ws?.Dispose();
            _ws = null;
            _cts?.Dispose();
            _cts = null;
            _receiveLoop = null;

            while (_reconnectAttempts < MaxReconnectAttempts && !_isDisposing)
            {
                _reconnectAttempts++;
                var delay = CalculateReconnectDelay(_reconnectAttempts);

                _log.Information("Polygon reconnection attempt {Attempt}/{Max} in {Delay}ms",
                    _reconnectAttempts, MaxReconnectAttempts, delay.TotalMilliseconds);

                await Task.Delay(delay).ConfigureAwait(false);

                if (_isDisposing) return;

                try
                {
                    await ConnectAsync(CancellationToken.None).ConfigureAwait(false);

                    if (_isConnected)
                    {
                        _log.Information("Polygon successfully reconnected after {Attempts} attempts. " +
                            "Resubscribed to {SubCount} active subscriptions.",
                            _reconnectAttempts, _subscriptionManager.Count);
                        _reconnectAttempts = 0;
                        return;
                    }
                }
                catch (Exception ex)
                {
                    _log.Warning(ex, "Polygon reconnection attempt {Attempt} failed", _reconnectAttempts);
                }
            }

            _log.Error("Polygon failed to reconnect after {Attempts} attempts. Manual intervention may be required.",
                _reconnectAttempts);
        }
        finally
        {
            _reconnectGate.Release();
        }
    }

    private static TimeSpan CalculateReconnectDelay(int attempt)
    {
        var baseMs = ReconnectBaseDelay.TotalMilliseconds;
        var maxMs = ReconnectMaxDelay.TotalMilliseconds;
        var delay = Math.Min(baseMs * Math.Pow(2, attempt - 1), maxMs);
        // Add jitter (±20%)
        var jitter = delay * 0.2 * (Random.Shared.NextDouble() * 2 - 1);
        return TimeSpan.FromMilliseconds(delay + jitter);
    }

    /// <summary>
    /// Disconnects from Polygon WebSocket stream.
    /// </summary>
    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        _log.Information("Polygon client disconnecting (Mode: {Mode})", IsStubMode ? "Stub" : "Live");

        _isDisposing = true;
        _isConnected = false;
        _isAuthenticated = false;

        // Cancel receive loop
        _cts?.Cancel();

        // Wait for receive loop to finish
        if (_receiveLoop != null)
        {
            try
            {
                await _receiveLoop.WaitAsync(TimeSpan.FromSeconds(5), ct).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                _log.Warning("Receive loop did not terminate within timeout");
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
        }

        // Close WebSocket connection gracefully
        if (_ws?.State == WebSocketState.Open)
        {
            try
            {
                await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client disconnecting", ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "Error closing WebSocket connection");
            }
        }

        _ws?.Dispose();
        _ws = null;
        _cts?.Dispose();
        _cts = null;
        _receiveLoop = null;

        _subscriptionManager.Clear();
    }

    /// <summary>
    /// Subscribes to market depth (L2) for the specified symbol.
    /// </summary>
    /// <returns>Subscription ID, or -1 if not supported/not subscribed.</returns>
    /// <remarks>
    /// Polygon provides BBO quotes, not full L2 order book depth.
    /// </remarks>
    public int SubscribeMarketDepth(SymbolConfig cfg)
    {
        if (cfg is null) throw new ArgumentNullException(nameof(cfg));

        // Polygon provides quotes (BBO), not full L2 depth
        if (!_options.SubscribeQuotes)
        {
            _log.Debug("Quote subscription disabled in Polygon options, skipping depth for {Symbol}", cfg.Symbol);
            return -1;
        }

        var symbol = cfg.Symbol.Trim().ToUpperInvariant();
        var isNewSymbol = !_subscriptionManager.HasSubscription(symbol, "quotes");

        var id = _subscriptionManager.Subscribe(symbol, "quotes");
        if (id == -1) return -1;

        _log.Debug("Subscribed to Polygon quotes for {Symbol} (SubId: {SubId}, Mode: {Mode})",
            symbol, id, IsStubMode ? "Stub" : "Live");

        // In live mode, send subscribe message to WebSocket
        if (!IsStubMode && _isConnected && _isAuthenticated && isNewSymbol)
        {
            SendSubscribeAsync($"Q.{symbol}")
                .ObserveException(_log, $"Polygon subscribe quotes for {symbol}");
        }

        return id;
    }

    /// <summary>
    /// Unsubscribes from market depth for the specified subscription.
    /// </summary>
    public void UnsubscribeMarketDepth(int subscriptionId)
    {
        var subscription = _subscriptionManager.Unsubscribe(subscriptionId);
        if (subscription == null) return;

        // Only send unsubscribe if no other subscriptions exist for this symbol+kind
        if (!_subscriptionManager.HasSubscription(subscription.Symbol, "quotes"))
        {
            _log.Debug("Unsubscribed from Polygon quotes for {Symbol}", subscription.Symbol);
            if (!IsStubMode && _isConnected)
            {
                SendUnsubscribeAsync($"Q.{subscription.Symbol}")
                    .ObserveException(_log, $"Polygon unsubscribe quotes for {subscription.Symbol}");
            }
        }
    }

    /// <summary>
    /// Subscribes to tick-by-tick trades for the specified symbol.
    /// </summary>
    /// <returns>Subscription ID, or -1 if not supported/not subscribed.</returns>
    public int SubscribeTrades(SymbolConfig cfg)
    {
        if (cfg is null) throw new ArgumentNullException(nameof(cfg));

        if (!_options.SubscribeTrades)
        {
            _log.Debug("Trade subscription disabled in Polygon options, skipping trades for {Symbol}", cfg.Symbol);
            return -1;
        }

        var symbol = cfg.Symbol.Trim().ToUpperInvariant();
        var isNewSymbol = !_subscriptionManager.HasSubscription(symbol, "trades");

        var id = _subscriptionManager.Subscribe(symbol, "trades");
        if (id == -1) return -1;

        _log.Debug("Subscribed to Polygon trades for {Symbol} (SubId: {SubId}, Mode: {Mode})",
            symbol, id, IsStubMode ? "Stub" : "Live");

        // In stub mode, emit a synthetic trade for testing
        if (IsStubMode)
        {
            _tradeCollector.OnTrade(new MarketTradeUpdate(
                Timestamp: DateTimeOffset.UtcNow,
                Symbol: symbol,
                Price: 100m,  // Use positive price for valid trade
                Size: 1,
                Aggressor: AggressorSide.Unknown,
                SequenceNumber: 0,
                StreamId: "POLYGON_STUB",
                Venue: "POLYGON"));
        }
        else if (_isConnected && _isAuthenticated && isNewSymbol)
        {
            // In live mode, send subscribe message to WebSocket
            SendSubscribeAsync($"T.{symbol}")
                .ObserveException(_log, $"Polygon subscribe trades for {symbol}");
        }

        return id;
    }

    /// <summary>
    /// Unsubscribes from trades for the specified subscription.
    /// </summary>
    public void UnsubscribeTrades(int subscriptionId)
    {
        var subscription = _subscriptionManager.Unsubscribe(subscriptionId);
        if (subscription == null) return;

        // Only send unsubscribe if no other subscriptions exist for this symbol+kind
        if (!_subscriptionManager.HasSubscription(subscription.Symbol, "trades"))
        {
            _log.Debug("Unsubscribed from Polygon trades for {Symbol}", subscription.Symbol);
            if (!IsStubMode && _isConnected)
            {
                SendUnsubscribeAsync($"T.{subscription.Symbol}")
                    .ObserveException(_log, $"Polygon unsubscribe trades for {subscription.Symbol}");
            }
        }
    }

    /// <summary>
    /// Subscribes to aggregate bars (second and minute) for the specified symbol.
    /// </summary>
    /// <param name="cfg">Symbol configuration.</param>
    /// <returns>Subscription ID, or -1 if not supported/not subscribed.</returns>
    public int SubscribeAggregates(SymbolConfig cfg)
    {
        if (cfg is null) throw new ArgumentNullException(nameof(cfg));

        if (!_options.SubscribeAggregates)
        {
            _log.Debug("Aggregate subscription disabled in Polygon options, skipping aggregates for {Symbol}", cfg.Symbol);
            return -1;
        }

        var symbol = cfg.Symbol.Trim().ToUpperInvariant();
        var isNewSymbol = !_subscriptionManager.HasSubscription(symbol, "aggregates");

        var id = _subscriptionManager.Subscribe(symbol, "aggregates");
        if (id == -1) return -1;

        _log.Debug("Subscribed to Polygon aggregates for {Symbol} (SubId: {SubId}, Mode: {Mode})",
            symbol, id, IsStubMode ? "Stub" : "Live");

        // In live mode, send subscribe message for both A (second) and AM (minute) channels
        if (!IsStubMode && _isConnected && _isAuthenticated && isNewSymbol)
        {
            SendSubscribeAsync($"A.{symbol},AM.{symbol}")
                .ObserveException(_log, $"Polygon subscribe aggregates for {symbol}");
        }

        return id;
    }

    /// <summary>
    /// Unsubscribes from aggregate bars for the specified subscription.
    /// </summary>
    /// <param name="subscriptionId">The subscription ID returned from SubscribeAggregates.</param>
    public void UnsubscribeAggregates(int subscriptionId)
    {
        var subscription = _subscriptionManager.Unsubscribe(subscriptionId);
        if (subscription == null) return;

        // Only send unsubscribe if no other subscriptions exist for this symbol+kind
        if (!_subscriptionManager.HasSubscription(subscription.Symbol, "aggregates"))
        {
            _log.Debug("Unsubscribed from Polygon aggregates for {Symbol}", subscription.Symbol);
            if (!IsStubMode && _isConnected)
            {
                SendUnsubscribeAsync($"A.{subscription.Symbol},AM.{subscription.Symbol}")
                    .ObserveException(_log, $"Polygon unsubscribe aggregates for {subscription.Symbol}");
            }
        }
    }

    /// <summary>
    /// Sends a subscribe message to the WebSocket.
    /// </summary>
    private async Task SendSubscribeAsync(string channel)
    {
        try
        {
            var message = JsonSerializer.Serialize(new { action = "subscribe", @params = channel });
            await SendMessageAsync(message, CancellationToken.None).ConfigureAwait(false);
            _log.Debug("Sent subscribe request for {Channel}", channel);
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "Failed to send subscribe message for {Channel}", channel);
        }
    }

    /// <summary>
    /// Sends an unsubscribe message to the WebSocket.
    /// </summary>
    private async Task SendUnsubscribeAsync(string channel)
    {
        try
        {
            var message = JsonSerializer.Serialize(new { action = "unsubscribe", @params = channel });
            await SendMessageAsync(message, CancellationToken.None).ConfigureAwait(false);
            _log.Debug("Sent unsubscribe request for {Channel}", channel);
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "Failed to send unsubscribe message for {Channel}", channel);
        }
    }

    /// <summary>
    /// Gets the current subscription count.
    /// </summary>
    public int SubscriptionCount => _subscriptionManager.Count;

    /// <summary>
    /// Gets the list of currently subscribed trade symbols.
    /// </summary>
    public IReadOnlyList<string> SubscribedTradeSymbols => _subscriptionManager.GetSymbolsByKind("trades");

    /// <summary>
    /// Gets the list of currently subscribed quote symbols.
    /// </summary>
    public IReadOnlyList<string> SubscribedQuoteSymbols => _subscriptionManager.GetSymbolsByKind("quotes");

    /// <summary>
    /// Gets the list of currently subscribed aggregate symbols.
    /// </summary>
    public IReadOnlyList<string> SubscribedAggregateSymbols => _subscriptionManager.GetSymbolsByKind("aggregates");

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        _log.Information("Disposing Polygon client");

        _isDisposing = true;
        _isConnected = false;
        _isAuthenticated = false;

        // Cancel receive loop
        _cts?.Cancel();

        // Wait for receive loop to complete
        if (_receiveLoop != null)
        {
            try
            {
                await _receiveLoop.WaitAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
            }
            catch
            {
                // Ignore timeout
            }
        }

        // Close WebSocket
        if (_ws?.State == WebSocketState.Open)
        {
            try
            {
                await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disposing", CancellationToken.None)
                    .ConfigureAwait(false);
            }
            catch
            {
                // Ignore close errors during dispose
            }
        }

        _ws?.Dispose();
        _cts?.Dispose();
        _sendLock.Dispose();
        _reconnectGate.Dispose();
        _subscriptionManager.Dispose();
    }

    /// <summary>
    /// Maps Polygon CTA/UTP trade condition codes to aggressor side.
    /// Polygon uses the Consolidated Tape Association (CTA) and UTP Plan condition codes.
    ///
    /// Condition codes that indicate seller-initiated trades:
    /// - 29: Seller - Trade was seller-initiated
    /// - 30: Sold Last - Last sale was seller-initiated
    /// - 31: Sold Last and Stopped Stock - Seller-initiated with stopped stock
    /// - 32: Sold (Out of Sequence) - Seller-initiated trade reported late
    /// - 33: Sold (Out of Sequence) and Stopped Stock - Late seller trade with stopped stock
    /// - 37: Odd Lot Trade - Often seller-initiated for small lot sizes
    ///
    /// Condition codes that indicate buyer-initiated trades:
    /// - 14: Intermarket Sweep Order (ISO) - Aggressive buy sweeping multiple venues
    ///
    /// Most other condition codes don't definitively indicate trade direction.
    ///
    /// Reference: https://polygon.io/docs/stocks/get_v3_reference_conditions
    /// </summary>
    /// <param name="conditionCodes">Array of condition codes from the trade message.</param>
    /// <returns>The determined aggressor side, or Unknown if not determinable.</returns>
    private static AggressorSide MapConditionCodesToAggressor(IEnumerable<int> conditionCodes)
    {
        foreach (var code in conditionCodes)
        {
            switch (code)
            {
                // Seller-initiated condition codes
                case 29: // Seller
                case 30: // Sold Last
                case 31: // Sold Last and Stopped Stock
                case 32: // Sold (Out of Sequence)
                case 33: // Sold (Out of Sequence) and Stopped Stock
                    return AggressorSide.Sell;

                    // Buyer-initiated condition codes
                    // Intermarket Sweep (14) can be buy or sell, but is typically
                    // used for aggressive buying. We'll keep it as Unknown for accuracy.

                    // The following codes are informational and don't indicate direction:
                    // 0: Regular Sale
                    // 1: Acquisition
                    // 2: Average Price Trade
                    // 4: Bunched Trade
                    // 5: Bunched Sold Trade (despite name, indicates bunched reporting)
                    // 7: Cash Sale
                    // 8: Closing Prints
                    // 9: Cross Trade
                    // 10: Derivatively Priced
                    // 11: Distribution
                    // 12: Form T (Extended Hours)
                    // 13: Extended Hours (Sold Out of Sequence)
                    // 15: Market Center Official Close
                    // 16: Market Center Official Open
                    // 17: Market Center Opening Trade
                    // 18: Market Center Reopening Trade
                    // 19: Market Center Closing Trade
                    // 20: Next Day
                    // 21: Price Variation Trade
                    // 22: Prior Reference Price
                    // 25: Opening Prints
                    // 37: Odd Lot Trade - ambiguous, could be either side
                    // 41: Trade Thru Exempt
                    // 52: Contingent Trade
                    // 53: Qualified Contingent Trade (QCT)
            }
        }

        return AggressorSide.Unknown;
    }
}
