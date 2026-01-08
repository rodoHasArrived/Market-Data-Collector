# MarketDataCollector Project Context

## Overview

MarketDataCollector is a high-performance market data collection system on .NET 9.0 that captures real-time and historical data from multiple providers with production-grade reliability.

## Critical Rules

When contributing to this project, always follow these rules:

- **ALWAYS** use `CancellationToken` on async methods
- **NEVER** store secrets in code or config files - use environment variables
- **ALWAYS** use structured logging with semantic parameters
- **PREFER** `IAsyncEnumerable<T>` for streaming data over collections
- **ALWAYS** mark classes as `sealed` unless designed for inheritance

## Architecture Principles

1. **Provider Independence**: All data providers implement `IMarketDataClient` interface, enabling seamless swapping and concurrent multi-provider operations
2. **No Vendor Lock-in**: Provider-agnostic interfaces with intelligent failover strategies
3. **Security First**: Environment variable-based credential management, no plain-text secrets
4. **Observability**: Structured logging, Prometheus metrics, health check endpoints
5. **Modularity**: Separate projects for core logic, domain models (F#), web UI, and tests

## Technology Stack

| Component | Technology |
|-----------|------------|
| Runtime | .NET 9.0 |
| Languages | C# (infrastructure), F# (domain modeling) |
| Serialization | System.Text.Json |
| Metrics | OpenTelemetry, Prometheus |
| Containerization | Docker, Docker Compose |
| Data Providers | Interactive Brokers TWS API, Alpaca Markets REST/WebSocket |

## Project Structure

```
MarketDataCollector/
├── src/
│   ├── MarketDataCollector/           # Main application, entry point
│   ├── MarketDataCollector.Domain/    # F# domain models, validation
│   └── MarketDataCollector.Ui/        # Web dashboard, WebSocket updates
├── tests/                              # Unit and integration tests
├── docs/                               # Documentation
├── deploy/                             # Kubernetes, systemd configs
└── data/                               # Runtime data storage
```

## Key Interfaces

### IMarketDataClient

Core abstraction for all real-time market data providers:

```csharp
public interface IMarketDataClient : IAsyncDisposable
{
    Task ConnectAsync(CancellationToken ct = default);
    Task DisconnectAsync();
    Task SubscribeAsync(SymbolSubscription subscription, CancellationToken ct = default);
    Task UnsubscribeAsync(string symbol, CancellationToken ct = default);
    IAsyncEnumerable<MarketDataEvent> GetEventsAsync(CancellationToken ct = default);
    ConnectionState State { get; }
    event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged;
}
```

### IHistoricalDataProvider

Historical data provider abstraction:

```csharp
public interface IHistoricalDataProvider
{
    Task<IReadOnlyList<OhlcBar>> GetHistoricalBarsAsync(
        string symbol,
        DateTime start,
        DateTime end,
        BarTimeframe timeframe,
        CancellationToken ct = default);

    IAsyncEnumerable<OhlcBar> StreamHistoricalBarsAsync(
        string symbol,
        DateTime start,
        DateTime end,
        BarTimeframe timeframe,
        CancellationToken ct = default);
}
```

## Coding Conventions

### Logging

Use structured logging with semantic parameters:

```csharp
_logger.LogInformation("Received {Count} bars for {Symbol}", bars.Count, symbol);
```

### Configuration

Configuration uses the .NET Options pattern with environment variable overrides:

```csharp
// Environment variables use double underscore for nesting
// ALPACA__KEYID maps to Alpaca:KeyId
services.Configure<AlpacaOptions>(configuration.GetSection("Alpaca"));
```

### Error Handling

- Log all errors with context
- Use exponential backoff for retries
- Throw `ArgumentException` for bad inputs, `InvalidOperationException` for state errors
- Use `Result<T, TError>` in F# code

## Anti-Patterns to Avoid

| Anti-Pattern | Why It's Bad |
|--------------|--------------|
| Swallowing exceptions silently | Hides bugs, makes debugging impossible |
| Hardcoding connection strings or credentials | Security risk, inflexible deployment |
| Using `Task.Run` for I/O-bound operations | Wastes thread pool threads |
| Blocking async code with `.Result` or `.Wait()` | Can cause deadlocks |
| Creating new `HttpClient` instances | Socket exhaustion, DNS issues |
