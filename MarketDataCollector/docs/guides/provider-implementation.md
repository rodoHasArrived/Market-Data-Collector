# Data Provider Implementation Guide

## Context

When implementing or modifying market data providers in MarketDataCollector, follow these patterns for consistency and reliability.

## Provider Implementation Checklist

1. Implement `IMarketDataClient` interface
1. Support both real-time streaming and snapshot queries
1. Handle reconnection with exponential backoff
1. Emit connection state change events
1. Support graceful shutdown via `CancellationToken`
1. Log all significant events with structured logging

## Interactive Brokers Provider Pattern

```csharp
public sealed class IBMarketDataClient : IMarketDataClient
{
    private readonly IBClientOptions _options;
    private readonly ILogger<IBMarketDataClient> _logger;
    private readonly EClientSocket _clientSocket;
    private readonly Channel<MarketDataEvent> _eventChannel;
    private ConnectionState _state = ConnectionState.Disconnected;

    public IBMarketDataClient(
        IOptions<IBClientOptions> options,
        ILogger<IBMarketDataClient> logger)
    {
        _options = options.Value;
        _logger = logger;
        _eventChannel = Channel.CreateBounded<MarketDataEvent>(
            new BoundedChannelOptions(10_000)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = false,
                SingleWriter = false
            });
    }

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        UpdateState(ConnectionState.Connecting);

        try
        {
            _clientSocket.eConnect(
                _options.Host,
                _options.Port,
                _options.ClientId);

            // Wait for connection confirmation
            await WaitForConnectionAsync(ct);

            UpdateState(ConnectionState.Connected);
            _logger.LogInformation(
                "Connected to IB Gateway at {Host}:{Port} with ClientId {ClientId}",
                _options.Host, _options.Port, _options.ClientId);
        }
        catch (Exception ex)
        {
            UpdateState(ConnectionState.Error);
            _logger.LogError(ex,
                "Failed to connect to IB Gateway at {Host}:{Port}",
                _options.Host, _options.Port);
            throw;
        }
    }

    public async IAsyncEnumerable<MarketDataEvent> GetEventsAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var evt in _eventChannel.Reader.ReadAllAsync(ct))
        {
            yield return evt;
        }
    }

    private void UpdateState(ConnectionState newState)
    {
        var oldState = _state;
        _state = newState;
        ConnectionStateChanged?.Invoke(this,
            new ConnectionStateChangedEventArgs(oldState, newState));
    }
}
```

## Alpaca Provider Pattern

```csharp
public sealed class AlpacaMarketDataClient : IMarketDataClient
{
    private readonly AlpacaOptions _options;
    private readonly ILogger<AlpacaMarketDataClient> _logger;
    private readonly HttpClient _httpClient;
    private IAlpacaDataStreamingClient? _streamingClient;
    private readonly Channel<MarketDataEvent> _eventChannel;

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        UpdateState(ConnectionState.Connecting);

        var environment = _options.UsePaper
            ? Environments.Paper
            : Environments.Live;

        _streamingClient = environment
            .GetAlpacaDataStreamingClient(
                new SecretKey(_options.KeyId, _options.SecretKey));

        var authStatus = await _streamingClient
            .ConnectAndAuthenticateAsync(ct);

        if (authStatus != AuthStatus.Authorized)
        {
            UpdateState(ConnectionState.Error);
            throw new InvalidOperationException(
                $"Alpaca authentication failed: {authStatus}");
        }

        UpdateState(ConnectionState.Connected);
        _logger.LogInformation(
            "Connected to Alpaca {Environment} streaming API",
            _options.UsePaper ? "Paper" : "Live");
    }

    public async Task SubscribeAsync(
        SymbolSubscription subscription,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(subscription);

        if (_streamingClient is null)
            throw new InvalidOperationException("Not connected");

        if (subscription.SubscribeTrades)
        {
            var tradeSubscription = _streamingClient
                .GetTradeSubscription(subscription.Symbol);
            tradeSubscription.Received += OnTradeReceived;
            await _streamingClient.SubscribeAsync(tradeSubscription, ct);
        }

        if (subscription.SubscribeQuotes)
        {
            var quoteSubscription = _streamingClient
                .GetQuoteSubscription(subscription.Symbol);
            quoteSubscription.Received += OnQuoteReceived;
            await _streamingClient.SubscribeAsync(quoteSubscription, ct);
        }

        _logger.LogInformation(
            "Subscribed to {Symbol} (Trades={Trades}, Quotes={Quotes})",
            subscription.Symbol,
            subscription.SubscribeTrades,
            subscription.SubscribeQuotes);
    }
}
```

## Reconnection Strategy

```csharp
public sealed class ReconnectingClientWrapper : IMarketDataClient
{
    private readonly IMarketDataClient _innerClient;
    private readonly ReconnectionOptions _options;
    private readonly ILogger _logger;

    public async Task ConnectWithRetryAsync(CancellationToken ct)
    {
        var delay = _options.InitialDelay;
        var attempt = 0;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await _innerClient.ConnectAsync(ct);
                return; // Success
            }
            catch (Exception ex) when (attempt < _options.MaxRetries)
            {
                attempt++;
                _logger.LogWarning(ex,
                    "Connection attempt {Attempt} failed, retrying in {Delay}ms",
                    attempt, delay.TotalMilliseconds);

                await Task.Delay(delay, ct);
                delay = TimeSpan.FromMilliseconds(
                    Math.Min(delay.TotalMilliseconds * 2,
                             _options.MaxDelay.TotalMilliseconds));
            }
        }
    }
}
```

## Testing Providers

Always create mock implementations for unit testing:

```csharp
public sealed class MockMarketDataClient : IMarketDataClient
{
    private readonly Channel<MarketDataEvent> _channel;
    public ConnectionState State { get; private set; }

    public void SimulateTrade(string symbol, decimal price, decimal volume)
    {
        var trade = new TradeEvent(symbol, price, volume, DateTimeOffset.UtcNow);
        _channel.Writer.TryWrite(trade);
    }

    public void SimulateDisconnection()
    {
        State = ConnectionState.Disconnected;
        ConnectionStateChanged?.Invoke(this,
            new ConnectionStateChangedEventArgs(
                ConnectionState.Connected,
                ConnectionState.Disconnected));
    }
}
```

## Performance Considerations

- Use `Channel<T>` for thread-safe event buffering
- Consider `BoundedChannelFullMode.DropOldest` for high-frequency data
- Pool `HttpClient` instances via `IHttpClientFactory`
- Use `ValueTask` when operations often complete synchronously
- Avoid allocations in hot paths - use `Span<T>` and `ArrayPool<T>`
