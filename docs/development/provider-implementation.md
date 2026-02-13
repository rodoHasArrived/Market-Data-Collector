# Data Provider Implementation Guide

**Last Updated:** 2026-02-12

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
    private readonly TradeDataCollector _tradeCollector;
    private readonly MarketDepthCollector _depthCollector;

    // 2. Internal event channel for buffering (not exposed via IAsyncEnumerable)
    // Use EventPipelinePolicy for consistent backpressure settings
    private readonly Channel<MarketDataEvent> _eventChannel =
        EventPipelinePolicy.HighThroughput.CreateChannel<MarketDataEvent>();

    // 3. Connection state management
    private ConnectionState _state = ConnectionState.Disconnected;
    public ConnectionState State => _state;
    public event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged;

    public {Provider}MarketDataClient(
        {Provider}Options options,
        ILogger<{Provider}MarketDataClient> logger,
        TradeDataCollector tradeCollector,
        MarketDepthCollector depthCollector)
    {
        _options = options;
        _logger = logger;
        _tradeCollector = tradeCollector;
        _depthCollector = depthCollector;
    }

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

## Data Flow Pattern

> **Note:** `IMarketDataClient` does *not* expose a `GetEventsAsync` method.
> Real implementations use dependency-injected collectors to receive data.
> The provider pushes events into these collectors (and/or internal channels)
> instead of exposing an async enumerable stream.

When the provider receives data (e.g., from a WebSocket), push it to the collectors:

```csharp
// In WebSocket message handler or data receive loop
private void HandleTradeMessage(JsonElement msg)
{
    var update = new MarketTradeUpdate(
        Timestamp: ParseTimestamp(msg),
        Symbol: msg.GetProperty("symbol").GetString()!,
        Price: msg.GetProperty("price").GetDecimal(),
        Size: msg.GetProperty("size").GetInt64(),
        Aggressor: ParseAggressor(msg),
        SequenceNumber: msg.GetProperty("seq").GetInt64(),
        StreamId: _streamId,
        Venue: _options.Venue);

    // Push to collector - collector handles event publishing internally
    _tradeCollector.OnTrade(update);
}

private void HandleDepthMessage(JsonElement msg)
{
    var update = new MarketDepthUpdate(
        Timestamp: ParseTimestamp(msg),
        Symbol: msg.GetProperty("symbol").GetString()!,
        Bids: ParseLevels(msg, "bids"),
        Asks: ParseLevels(msg, "asks"),
        SequenceNumber: msg.GetProperty("seq").GetInt64(),
        StreamId: _streamId);

    // Push to collector - collector handles event publishing internally
    _depthCollector.OnDepth(update);
}
```

The internal channel can still be used for buffering within the provider, but data flows outward through the collectors, not via an exposed `IAsyncEnumerable`.

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

## Related Documentation

- [Repository Organization Guide](repository-organization-guide.md) — Project boundaries and where provider code belongs
- [Refactor Map](refactor-map.md) — Phase 1 (provider registry) and Phase 3 (WebSocket consolidation)
- [ADR-001: Provider Abstraction](../adr/001-provider-abstraction.md) — Interface contracts
- [ADR-004: Async Streaming Patterns](../adr/004-async-streaming-patterns.md) — CancellationToken, IAsyncEnumerable
- [ADR-005: Attribute-Based Discovery](../adr/005-attribute-based-discovery.md) — `[DataSource]` attribute
- [Provider Comparison](../providers/provider-comparison.md) — Feature matrix across providers
