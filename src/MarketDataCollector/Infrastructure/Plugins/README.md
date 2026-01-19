# Unified Plugin Architecture

This directory contains the redesigned plugin architecture for Market Data Collector.

## Design Philosophy

**"Simplest thing that works, designed to scale"**

This architecture replaces the previous complex setup with:
- Multiple registration systems (DataProvider, DataSource)
- 3+ separate interfaces (IMarketDataClient, IHistoricalDataProvider, IDataSource)
- 640+ lines of configuration with 100+ options
- 8 naming conventions, 5 compression codecs, 5 storage tiers

With a simplified approach:
- **One interface** for all data sources (`IMarketDataPlugin`)
- **One streaming model** for both real-time and historical data
- **Environment variables** for configuration (no complex JSON)
- **Simple storage** that just works (JSONL files by default)

## Quick Start

### 1. Register Plugins

```csharp
// In Program.cs
builder.Services.AddMarketDataPlugins(options =>
{
    options.DataPath = "./data";
    options.EnableStorage = true;
});
```

### 2. Set Environment Variables

```bash
export ALPACA__KEY_ID=your-key
export ALPACA__SECRET_KEY=your-secret
export ALPACA__FEED=iex
```

### 3. Stream Data

```csharp
var orchestrator = services.GetRequiredService<PluginOrchestrator>();

// Real-time streaming
await foreach (var evt in orchestrator.StreamAsync(
    DataStreamRequest.Realtime("AAPL", "MSFT"),
    cancellationToken))
{
    Console.WriteLine($"{evt.Symbol}: {evt}");
}

// Historical backfill
await orchestrator.BackfillAsync(
    symbols: ["AAPL", "MSFT"],
    from: new DateOnly(2024, 1, 1),
    to: DateOnly.FromDateTime(DateTime.Today));
```

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│                     PluginOrchestrator                       │
│  (Routes requests to plugins, handles failover)              │
└──────────────────────────┬──────────────────────────────────┘
                           │
        ┌──────────────────┼──────────────────┐
        │                  │                  │
        ▼                  ▼                  ▼
┌───────────────┐  ┌───────────────┐  ┌───────────────┐
│ AlpacaPlugin  │  │  YahooPlugin  │  │  OtherPlugin  │
│  (Realtime)   │  │ (Historical)  │  │   (Hybrid)    │
└───────────────┘  └───────────────┘  └───────────────┘
        │                  │                  │
        └──────────────────┼──────────────────┘
                           │
                           ▼
                ┌─────────────────────┐
                │  IMarketDataStore   │
                │  (FileSystemStore)  │
                └─────────────────────┘
```

## Directory Structure

```
Infrastructure/Plugins/
├── Core/                   # Core interfaces and types
│   ├── IMarketDataPlugin.cs       # Main plugin interface
│   ├── PluginCapabilities.cs      # Capability model
│   ├── PluginHealth.cs            # Health status model
│   ├── MarketDataEvent.cs         # Unified event types
│   ├── IPluginConfig.cs           # Configuration interface
│   └── PluginOrchestrator.cs      # Request routing
│
├── Base/                   # Base classes for plugins
│   ├── MarketDataPluginBase.cs    # Common functionality
│   ├── RealtimePluginBase.cs      # WebSocket/streaming
│   └── HistoricalPluginBase.cs    # REST/batch
│
├── Discovery/              # Plugin discovery & DI
│   ├── MarketDataPluginAttribute.cs
│   └── PluginRegistry.cs
│
├── Storage/                # Simplified storage
│   ├── IMarketDataStore.cs
│   └── FileSystemStore.cs
│
├── Providers/              # Plugin implementations
│   ├── Alpaca/
│   │   └── AlpacaPlugin.cs
│   └── Yahoo/
│       └── YahooFinancePlugin.cs
│
└── ServiceCollectionExtensions.cs  # DI registration
```

## Creating a New Plugin

### 1. Choose a Base Class

- `RealtimePluginBase` - For WebSocket/streaming sources
- `HistoricalPluginBase` - For REST API/batch sources
- `MarketDataPluginBase` - For custom implementations

### 2. Add the Plugin Attribute

```csharp
[MarketDataPlugin(
    id: "myprovider",
    displayName: "My Provider",
    type: PluginType.Historical,
    Category = PluginCategory.Free,
    Priority = 50)]
public sealed class MyProviderPlugin : HistoricalPluginBase
{
    // Implementation
}
```

### 3. Implement Required Methods

For historical plugins:
```csharp
protected override Task<IReadOnlyList<BarEvent>> FetchBarsAsync(
    string symbol,
    DateOnly from,
    DateOnly to,
    string interval,
    bool adjusted,
    CancellationToken ct)
{
    // Fetch data from your API
}
```

For real-time plugins:
```csharp
protected override Task ConnectAsync(CancellationToken ct);
protected override Task DisconnectAsync();
protected override Task SubscribeAsync(IReadOnlyList<string> symbols, CancellationToken ct);
protected override Task UnsubscribeAsync(IReadOnlyList<string> symbols, CancellationToken ct);
```

### 4. Set Capabilities

```csharp
public override PluginCapabilities Capabilities => new()
{
    SupportsRealtime = false,
    SupportsHistorical = true,
    SupportsBars = true,
    SupportsAdjustedPrices = true,
    MaxSymbolsPerRequest = 100,
    RateLimit = RateLimitPolicy.PerMinute(60)
};
```

## Configuration

Plugins are configured via environment variables:

```bash
# Pattern: PLUGINID__SETTING
ALPACA__KEY_ID=xxx
ALPACA__SECRET_KEY=xxx
ALPACA__FEED=iex
ALPACA__USE_SANDBOX=false

YAHOO__USER_AGENT=MyApp/1.0
```

Access in your plugin:
```csharp
protected override void ValidateConfiguration(IPluginConfig config)
{
    _apiKey = config.GetRequired("key_id");
    _secretKey = config.GetRequired("secret_key");
    _feed = config.Get("feed", "iex");
}
```

## Event Types

All plugins emit `MarketDataEvent` subclasses:

- `TradeEvent` - Trade executions (tick-by-tick)
- `QuoteEvent` - Best bid/offer quotes
- `DepthEvent` - Order book depth
- `BarEvent` - OHLCV bars
- `DividendEvent` - Dividend announcements
- `SplitEvent` - Stock splits
- `HeartbeatEvent` - Connection keepalive
- `ErrorEvent` - Errors

## Migration from Legacy Architecture

### Old Way (Multiple Interfaces)

```csharp
// Had to implement IMarketDataClient for real-time
public class MyRealtimeProvider : IMarketDataClient { ... }

// AND IHistoricalDataProvider for historical
public class MyHistoricalProvider : IHistoricalDataProvider { ... }

// Complex configuration
services.Configure<MyProviderOptions>(config.GetSection("MyProvider"));
```

### New Way (Unified Plugin)

```csharp
// Single class handles both
[MarketDataPlugin("myprovider", "My Provider", PluginType.Hybrid)]
public class MyProviderPlugin : MarketDataPluginBase
{
    // One implementation for everything
}

// Simple configuration via environment variables
export MYPROVIDER__API_KEY=xxx
```

## Best Practices

1. **Use base classes** - Don't implement `IMarketDataPlugin` directly
2. **Declare capabilities accurately** - The orchestrator uses them for routing
3. **Handle rate limits** - Use the built-in rate limiting in `HistoricalPluginBase`
4. **Emit health updates** - Call `RecordSuccess()` and `RecordFailure()` appropriately
5. **Support cancellation** - Pass `CancellationToken` through all async operations
6. **Log with context** - Use structured logging with semantic parameters

## Comparison: Old vs New

| Aspect | Old Architecture | New Architecture |
|--------|------------------|------------------|
| Interfaces | 3+ (IMarketDataClient, IHistoricalDataProvider, IDataSource) | 1 (IMarketDataPlugin) |
| Registration | Two parallel systems | Single registry |
| Configuration | 640+ lines JSON | Environment variables |
| Storage options | 8 naming × 4 partition × 5 compression | JSONL by default |
| Event types | Multiple domain events | Unified MarketDataEvent |
| Streaming model | Observables + callbacks | IAsyncEnumerable |
| Plugin creation | Implement multiple interfaces | Extend one base class |

## Future Extensions

The architecture is designed to scale when needed:

- **Cloud storage**: Implement `IMarketDataStore` for S3/Azure Blob
- **Parquet format**: Create `ParquetStore` for columnar analytics
- **Multi-datacenter**: Add geographic routing to orchestrator
- **Rate limit pooling**: Share rate limits across plugin instances
- **Plugin hot-reload**: Use `AssemblyLoadContext` for dynamic loading

Start simple, add complexity only when required.
