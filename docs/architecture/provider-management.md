# Provider Management & Data Quality Architecture

**Version:** 2.0 | **Last Updated:** 2026-01-28

This document describes the unified data provider management system, historical data archival capabilities, and data quality monitoring infrastructure. See also [ADR-001: Provider Abstraction](../adr/001-provider-abstraction.md) and [ADR-005: Attribute-Based Discovery](../adr/005-attribute-based-discovery.md).

## Overview

The Market Data Collector now features a provider-agnostic, extensible architecture that supports:

- **Unified Provider Abstraction** - Single interface for all data providers
- **Capability Discovery** - Runtime capability checking without provider-specific code
- **Concurrent Operations** - Parallel execution across multiple providers
- **Circuit Breaker Pattern** - Intelligent failover with automatic recovery
- **Priority-Based Job Queue** - Sophisticated backfill job scheduling
- **Gap Detection & Repair** - Automatic identification and repair of data gaps
- **Data Quality Monitoring** - Multi-dimensional quality scoring

## Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────────────┐
│                          Application Layer                               │
│  ┌─────────────────────────────────────────────────────────────────┐   │
│  │  DataSourceManager (Orchestration)                              │   │
│  │  ├── Provider Selection                                          │   │
│  │  ├── Failover Coordination                                       │   │
│  │  └── Result Aggregation                                          │   │
│  └─────────────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                      Provider Abstraction Layer                          │
│  ┌────────────────┐ ┌────────────────┐ ┌────────────────────────────┐  │
│  │ IDataProvider  │ │ IStreaming     │ │ IHistoricalDataProvider    │  │
│  │                │ │ DataProvider   │ │                            │  │
│  └───────┬────────┘ └───────┬────────┘ └───────────────┬────────────┘  │
│          │                  │                          │                │
│          └──────────────────┴──────────────────────────┘                │
│                             │                                           │
│  ┌──────────────────────────▼──────────────────────────────────────┐   │
│  │              ProviderCapabilityInfo                              │   │
│  │  ├── ProviderCapabilities (flags)                                │   │
│  │  ├── Rate Limiting Info                                          │   │
│  │  ├── Historical Data Limits                                      │   │
│  │  └── Data Quality Indicators                                     │   │
│  └─────────────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                       Resilience Layer                                   │
│  ┌────────────────────┐ ┌────────────────────┐ ┌──────────────────┐    │
│  │ CircuitBreaker     │ │ ConcurrentProvider │ │ RateLimiter      │    │
│  │ Registry           │ │ Executor           │ │ Registry         │    │
│  └────────────────────┘ └────────────────────┘ └──────────────────┘    │
└─────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                       Provider Registry                                  │
│  ┌─────────────────────────────────────────────────────────────────┐   │
│  │ [DataProvider] Attribute-Based Discovery                        │   │
│  │ ├── Auto-scan assemblies                                         │   │
│  │ ├── Capability matching                                          │   │
│  │ └── Priority ordering                                            │   │
│  └─────────────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                    Concrete Provider Implementations                     │
│  ┌─────────────┐ ┌─────────────┐ ┌─────────────┐ ┌─────────────────┐   │
│  │   Alpaca    │ │    IBKR     │ │   Polygon   │ │  Yahoo Finance  │   │
│  │  Provider   │ │  Provider   │ │  Provider   │ │    Provider     │   │
│  └─────────────┘ └─────────────┘ └─────────────┘ └─────────────────┘   │
└─────────────────────────────────────────────────────────────────────────┘
```

## Core Components

### 1. Provider Capabilities System

The `ProviderCapabilities` enum provides declarative capability flags:

```csharp
[Flags]
public enum ProviderCapabilities : long
{
    // Real-time streaming
    RealTimeTrades = 1L << 0,
    RealTimeQuotes = 1L << 1,
    Level2Depth = 1L << 2,

    // Historical data
    HistoricalDailyBars = 1L << 10,
    HistoricalIntradayBars = 1L << 11,
    AdjustedPrices = 1L << 14,

    // Asset classes
    Equities = 1L << 20,
    Crypto = 1L << 24,

    // Connection features
    WebSocketStreaming = 1L << 50,
    AutoReconnect = 1L << 53,
}
```

**Usage:**

```csharp
// Check if provider supports specific capabilities
var caps = provider.CapabilityInfo.Capabilities;
if (caps.HasAll(ProviderCapabilities.HistoricalDailyBars | ProviderCapabilities.AdjustedPrices))
{
    // Provider supports adjusted daily bars
}

// Find providers with streaming capabilities
var streamingProviders = registry.FindProviders(ProviderCapabilities.WebSocketStreaming);
```

### 2. Provider Registry

Automatic provider discovery using attributes:

```csharp
[DataProvider(
    id: "alpaca",
    displayName: "Alpaca Markets",
    capabilities: ProviderCapabilities.RealTimeTrades |
                  ProviderCapabilities.HistoricalDailyBars |
                  ProviderCapabilities.Equities,
    DefaultPriority = 10)]
public sealed class AlpacaDataProvider : IUnifiedDataProvider
{
    // Implementation
}
```

**Registration:**

```csharp
// In Program.cs
services.AddProviderRegistry(
    Assembly.GetExecutingAssembly(),
    typeof(AlpacaDataProvider).Assembly
);

// Or manual registration
registry.Register(new ProviderRegistration(
    "custom-provider",
    "Custom Provider",
    ProviderCapabilities.HistoricalDailyBars,
    typeof(CustomProvider),
    null,
    50
));
```

### 3. Circuit Breaker Pattern

Prevents cascading failures with automatic recovery:

```csharp
var options = new CircuitBreakerOptions
{
    FailureThreshold = 5,        // Open after 5 failures
    OpenDuration = TimeSpan.FromMinutes(1),
    SuccessThreshold = 1,        // Close after 1 success
    SlidingWindow = TimeSpan.FromMinutes(5)
};

var breaker = new CircuitBreaker("alpaca", options);

// Execute with protection
try
{
    var result = await breaker.ExecuteAsync(async ct =>
    {
        return await provider.GetBarsAsync(request, ct);
    }, cancellationToken);
}
catch (CircuitBreakerOpenException ex)
{
    // Provider temporarily unavailable
    _log.Warning("Provider {Name} circuit open until {Until}",
        ex.CircuitName, ex.OpenUntil);
}
```

**Circuit States:**

| State | Description | Behavior |
|-------|-------------|----------|
| Closed | Normal operation | All requests flow through |
| Open | Failure threshold exceeded | Requests immediately rejected |
| HalfOpen | Testing recovery | Single request allowed |

### 4. Concurrent Provider Executor

Execute operations across multiple providers in parallel:

```csharp
var executor = new ConcurrentProviderExecutor(circuitBreakers);

// Execute on all providers concurrently
var results = await executor.ExecuteAsync(
    providers,
    async (provider, ct) => await provider.GetBarsAsync(request, ct),
    new ConcurrentExecutionOptions
    {
        MaxConcurrency = 4,
        PerProviderTimeout = TimeSpan.FromSeconds(30),
        ContinueOnError = true,
        StopOnFirstSuccess = false
    },
    cancellationToken
);

// Process results
foreach (var result in results.SuccessfulResults)
{
    Console.WriteLine($"{result.ProviderId}: {result.Value.Bars.Count} bars in {result.Duration}");
}
```

**Execution Strategies:**

| Strategy | Description |
|----------|-------------|
| `All` | Return all results |
| `FirstSuccess` | Stop on first success |
| `HighestPriority` | Return result from highest priority provider |
| `Merge` | Combine results from all providers |
| `BestQuality` | Return highest quality result |

### 5. Priority Backfill Queue

Sophisticated job scheduling with dependencies:

```csharp
var queue = new PriorityBackfillQueue();

// Enqueue with priority
var job = await queue.EnqueueAsync(new BackfillJobRequest
{
    Symbol = "AAPL",
    StartDate = new DateOnly(2024, 1, 1),
    EndDate = new DateOnly(2024, 12, 31),
    Priority = BackfillPriority.High,
    PreferredProviders = new[] { "alpaca", "yahoo" },
    FillGapsOnly = true
});

// Batch enqueue with dependency chain
var batch = await queue.EnqueueBatchAsync(
    symbols.Select(s => new BackfillJobRequest { Symbol = s, ... }),
    new BatchEnqueueOptions { CreateDependencyChain = true }
);

// Process jobs
while (queue.DequeueNext() is { } job)
{
    await ProcessJobAsync(job);
    queue.MarkCompleted(job.JobId, success: true);
}
```

**Priority Levels:**

| Priority | Value | Description |
|----------|-------|-------------|
| Critical | 0 | System-critical gaps |
| High | 10 | User-requested immediate |
| Normal | 50 | Standard backfill |
| Low | 100 | Background fill |
| Deferred | 200 | Fill when idle |

### 6. Data Gap Detection & Repair

Automatic gap identification and repair:

```csharp
var repairService = new DataGapRepairService(
    gapAnalyzer,
    providers,
    dataRoot
);

// Detect gaps
var report = await repairService.DetectGapsAsync(
    "AAPL",
    new DateOnly(2024, 1, 1),
    new DateOnly(2024, 12, 31)
);

Console.WriteLine($"Coverage: {report.CoveragePercent:F1}%");
Console.WriteLine($"Gaps found: {report.Gaps.Count}");

// Repair gaps
var repairResult = await repairService.RepairGapsAsync(
    report,
    new GapRepairOptions
    {
        PreferredProviders = new[] { "alpaca" },
        TryAllProviders = true,
        ContinueOnError = true,
        RequestDelay = TimeSpan.FromMilliseconds(500)
    }
);

Console.WriteLine($"Repaired: {repairResult.RepairedGaps}/{repairResult.TotalGaps}");
```

**Gap Types:**

| Type | Severity | Description |
|------|----------|-------------|
| Missing | Critical | No data for date |
| Partial | Warning | Incomplete data |
| Holiday | Info | Expected market closure |
| Suspicious | Warning | Anomalous data |

### 7. Data Quality Monitoring

Multi-dimensional quality scoring:

```csharp
var monitor = new DataQualityMonitor(gapAnalyzer, dataRoot);

// Calculate quality score
var score = await monitor.CalculateScoreAsync(
    "AAPL",
    new DateOnly(2024, 1, 1),
    new DateOnly(2024, 12, 31)
);

Console.WriteLine($"Overall Score: {score.OverallScore:P0} ({score.Grade})");
Console.WriteLine("Dimensions:");
foreach (var dim in score.Dimensions)
{
    Console.WriteLine($"  {dim.Name}: {dim.Score:P0} (weight: {dim.Weight:P0})");
}

// Get alerts for low-quality data
var alerts = await monitor.GetAlertsAsync(minScoreThreshold: 0.80);
foreach (var alert in alerts)
{
    Console.WriteLine($"ALERT: {alert.Symbol} - {alert.Score:P0} - {alert.Message}");
}
```

**Quality Dimensions:**

| Dimension | Weight | Description |
|-----------|--------|-------------|
| Completeness | 30% | Percentage of expected data present |
| Accuracy | 25% | Data correctness (price ranges, etc.) |
| Timeliness | 20% | Data freshness |
| Consistency | 15% | Cross-source agreement |
| Validity | 10% | Format and constraint validation |

**Issue Types Detected:**

- Invalid High/Low relationships
- Prices outside expected range
- Suspicious price movements (>50% daily change)
- Invalid/negative volume
- Missing data gaps
- Duplicate data
- Stale data
- Future timestamps

## Configuration

### Provider Configuration

```json
{
  "Providers": {
    "DefaultTimeout": "00:00:30",
    "MaxConcurrentOperations": 4,
    "CircuitBreaker": {
      "FailureThreshold": 5,
      "OpenDuration": "00:01:00",
      "SlidingWindow": "00:05:00"
    }
  },
  "Backfill": {
    "Queue": {
      "MaxConcurrentJobs": 4,
      "MaxRetries": 3,
      "RetryBackoff": "00:01:00"
    },
    "Quality": {
      "AlertThreshold": 0.80,
      "MaxDailyPriceChangePercent": 50
    }
  }
}
```

### Rate Limiting

Each provider has configurable rate limits:

```csharp
var rateLimiter = new RateLimiter(
    maxRequestsPerWindow: 200,    // Alpaca default
    window: TimeSpan.FromMinutes(1),
    minDelayBetweenRequests: TimeSpan.FromMilliseconds(100)
);

await rateLimiter.WaitForSlotAsync(cancellationToken);
// Make request
```

## Best Practices

### 1. Provider Selection

```csharp
// Use capability-based selection
var providers = registry.FindProviders(
    ProviderCapabilities.HistoricalDailyBars |
    ProviderCapabilities.AdjustedPrices
);

// Sort by priority and health
var bestProvider = providers
    .Where(p => circuitBreakers.IsProviderAvailable(p.ProviderId))
    .OrderBy(p => p.DefaultPriority)
    .FirstOrDefault();
```

### 2. Error Handling

```csharp
try
{
    var result = await executor.ExecuteAsync(...);

    if (!result.HasSuccessfulResults)
    {
        foreach (var error in result.Errors)
        {
            _log.Warning("Provider failed: {Error}", error.Message);
        }
        throw new AggregateException(result.Errors);
    }
}
catch (CircuitBreakerOpenException)
{
    // Wait for circuit to recover
    await Task.Delay(circuitBreaker.OpenUntil - DateTimeOffset.UtcNow);
}
```

### 3. Quality Monitoring

```csharp
// Schedule regular quality checks
foreach (var symbol in symbols)
{
    var score = await monitor.CalculateScoreAsync(symbol);

    if (score.OverallScore < 0.80)
    {
        // Trigger gap repair
        var report = await repairService.DetectGapsAsync(symbol, ...);
        await repairService.RepairGapsAsync(report, options);

        // Recalculate score
        score = await monitor.CalculateScoreAsync(symbol);
    }

    metrics.SetGauge($"quality_{symbol}", score.OverallScore);
}
```

## File Structure

```
Infrastructure/Providers/
├── Abstractions/
│   ├── ProviderCapabilities.cs      # Capability flags enum
│   ├── IDataProvider.cs             # Core provider interfaces
│   ├── DataProviderAttribute.cs     # Registration attribute
│   └── ProviderRegistry.cs          # Provider discovery/management
├── Resilience/
│   ├── CircuitBreaker.cs            # Circuit breaker implementation
│   └── ConcurrentProviderExecutor.cs # Parallel execution
├── Backfill/
│   ├── PriorityBackfillQueue.cs     # Priority job queue
│   ├── DataGapRepair.cs             # Gap detection & repair
│   ├── DataQualityMonitor.cs        # Quality scoring
│   └── ... (existing files)
└── ... (provider implementations)
```

## Migration Guide

### From v1.x to v2.x

1. **Update provider implementations** to implement `IDataProvider` interface
2. **Add `[DataProvider]` attribute** to each provider class
3. **Replace manual registration** with `services.AddProviderRegistry()`
4. **Wrap operations** with circuit breaker protection
5. **Update backfill code** to use `PriorityBackfillQueue`

```csharp
// Before (v1.x)
var provider = new AlpacaHistoricalDataProvider(config);
var bars = await provider.GetDailyBarsAsync(symbol, from, to);

// After (v2.x)
var registry = services.GetRequiredService<IProviderRegistry>();
var provider = registry.CreateInstance("alpaca", services);
var executor = new ConcurrentProviderExecutor(circuitBreakers);

var result = await executor.ExecuteAsync(
    new[] { provider },
    async (p, ct) => await ((IHistoricalDataProvider)p).GetBarsAsync(request, ct),
    ConcurrentExecutionOptions.Default,
    cancellationToken
);
```

## API Reference

See the XML documentation in each source file for detailed API documentation.

## Related Documentation

- [Architecture Overview](overview.md)
- [Configuration Guide](../guides/configuration.md)
- [Storage Organization](storage-design.md)
- [Operator Runbook](../guides/operator-runbook.md)
- [ADR-001: Provider Abstraction](../adr/001-provider-abstraction.md)
- [ADR-005: Attribute-Based Discovery](../adr/005-attribute-based-discovery.md)
- [ADR-010: HttpClientFactory](../adr/010-httpclient-factory.md)
