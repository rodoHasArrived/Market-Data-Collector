# Data Provider Implementation Guide

## Overview

This guide covers patterns for implementing market data providers in MarketDataCollector. Follow these patterns for consistency and reliability.

## Implementation Checklist

Before writing code, ensure your provider will:

- [ ] Implement `IMarketDataClient` interface fully
- [ ] Use `Channel<T>` for thread-safe event buffering
- [ ] Handle reconnection with exponential backoff
- [ ] Emit `ConnectionStateChanged` events on all state transitions
- [ ] Support graceful shutdown via `CancellationToken`
- [ ] Log all connection events with structured logging

## Required Class Structure

Every provider must follow this structure:

```csharp
public sealed class {Provider}MarketDataClient : IMarketDataClient
{
    // 1. Dependencies via constructor injection
    private readonly {Provider}Options _options;
    private readonly ILogger<{Provider}MarketDataClient> _logger;

    // 2. Event channel with bounded capacity
    private readonly Channel<MarketDataEvent> _eventChannel =
        Channel.CreateBounded<MarketDataEvent>(new BoundedChannelOptions(10_000)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });

    // 3. Connection state management
    private ConnectionState _state = ConnectionState.Disconnected;
    public ConnectionState State => _state;
    public event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged;

    // 4. Always update state through this method
    private void UpdateState(ConnectionState newState)
    {
        var oldState = _state;
        _state = newState;
        ConnectionStateChanged?.Invoke(this, new(oldState, newState));
    }
}
```

## Connection Pattern

```csharp
public async Task ConnectAsync(CancellationToken ct = default)
{
    UpdateState(ConnectionState.Connecting);
    try
    {
        // Provider-specific connection logic
        await EstablishConnectionAsync(ct);
        UpdateState(ConnectionState.Connected);
        _logger.LogInformation("Connected to {Provider}", nameof(Provider));
    }
    catch (Exception ex)
    {
        UpdateState(ConnectionState.Error);
        _logger.LogError(ex, "Failed to connect to {Provider}", nameof(Provider));
        throw;
    }
}
```

## Event Streaming Pattern

```csharp
public async IAsyncEnumerable<MarketDataEvent> GetEventsAsync(
    [EnumeratorCancellation] CancellationToken ct = default)
{
    await foreach (var evt in _eventChannel.Reader.ReadAllAsync(ct))
        yield return evt;
}
```

## Reconnection Strategy

Implement exponential backoff for resilient connections:

```csharp
public async Task ConnectWithRetryAsync(CancellationToken ct)
{
    var delay = TimeSpan.FromSeconds(1);
    const int maxRetries = 5;

    for (int attempt = 1; attempt <= maxRetries && !ct.IsCancellationRequested; attempt++)
    {
        try
        {
            await ConnectAsync(ct);
            return;
        }
        catch (Exception ex) when (attempt < maxRetries)
        {
            _logger.LogWarning(ex, "Attempt {Attempt}/{MaxRetries} failed, retry in {Delay}s",
                attempt, maxRetries, delay.TotalSeconds);
            await Task.Delay(delay, ct);
            delay = TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 2, 30));
        }
    }
}
```

## Testing Providers

Always create mock implementations for unit testing:

```csharp
public sealed class Mock{Provider}Client : IMarketDataClient
{
    private readonly Channel<MarketDataEvent> _channel;
    public ConnectionState State { get; private set; }

    public void SimulateTrade(string symbol, decimal price, decimal volume)
        => _channel.Writer.TryWrite(new TradeEvent(symbol, price, volume, DateTimeOffset.UtcNow));

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

| Technique | When to Use |
|-----------|-------------|
| `Channel<T>` with bounded capacity | Thread-safe event buffering |
| `BoundedChannelFullMode.DropOldest` | High-frequency data where latest matters |
| `IHttpClientFactory` | Any HTTP-based provider |
| `ValueTask` | Operations that often complete synchronously |
| `Span<T>` and `ArrayPool<T>` | Hot paths with frequent allocations |

## Common Mistakes to Avoid

| Mistake | Consequence |
|---------|-------------|
| Forgetting to call `UpdateState()` on connection failure | Consumers don't know connection died |
| Not using `CancellationToken` throughout async chain | Graceful shutdown fails |
| Creating unbounded channels for high-frequency data | Memory exhaustion |
| Swallowing exceptions in event handlers | Silent data loss |
| Not disposing resources in `DisposeAsync()` | Resource leaks |
