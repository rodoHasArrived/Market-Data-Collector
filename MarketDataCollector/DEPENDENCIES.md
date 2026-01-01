# Dependencies

This document describes all the NuGet packages used in the Market Data Collector project and their purposes.

## Core Framework

- **.NET 8.0** - Target framework for all projects
- **C# 11** - Language version with modern features (nullable reference types, records, etc.)

---

## MarketDataCollector (Main Project)

### Configuration Management

| Package | Version | Purpose |
|---------|---------|---------|
| Microsoft.Extensions.Configuration | 8.0.0 | Core configuration abstractions |
| Microsoft.Extensions.Configuration.Json | 8.0.0 | JSON configuration provider (appsettings.json) |
| Microsoft.Extensions.Configuration.Binder | 8.0.2 | Bind configuration to strongly-typed objects |
| Microsoft.Extensions.Configuration.EnvironmentVariables | 8.0.0 | Environment variable configuration provider |
| Microsoft.Extensions.Configuration.CommandLine | 8.0.0 | Command-line argument configuration provider |

**Why**: The application uses `appsettings.json` for configuration with support for environment variables and command-line overrides. This allows flexible configuration across development, staging, and production environments.

### Dependency Injection

| Package | Version | Purpose |
|---------|---------|---------|
| Microsoft.Extensions.DependencyInjection | 8.0.0 | DI container implementation |
| Microsoft.Extensions.DependencyInjection.Abstractions | 8.0.1 | DI abstractions |

**Why**: Enables clean separation of concerns, testability, and modular architecture. Used in `Program.cs` to compose the application.

### Logging

| Package | Version | Purpose |
|---------|---------|---------|
| Serilog | 4.1.0 | Structured logging framework |
| Serilog.Sinks.Console | 6.0.0 | Console output for logs |
| Serilog.Sinks.File | 6.0.0 | File output for logs |
| Serilog.Extensions.Logging | 8.0.0 | Microsoft.Extensions.Logging integration |
| Serilog.Settings.Configuration | 8.0.4 | Configure Serilog from appsettings.json |

**Why**: Structured logging is critical for production systems. Serilog provides rich structured logging with minimal performance overhead. Logs can be sent to multiple sinks (console, file, Seq, Elasticsearch) and include contextual information for debugging production issues.

**Recommended Configuration** (appsettings.json):
```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "System": "Warning"
      }
    },
    "WriteTo": [
      { "Name": "Console" },
      {
        "Name": "File",
        "Args": {
          "path": "logs/marketdata-.log",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 7
        }
      }
    ],
    "Enrich": ["FromLogContext", "WithMachineName", "WithThreadId"]
  }
}
```

### Monitoring and Metrics

| Package | Version | Purpose |
|---------|---------|---------|
| prometheus-net | 8.2.1 | Prometheus metrics client |
| prometheus-net.AspNetCore | 8.2.1 | ASP.NET Core integration for metrics |

**Why**: Production observability requires metrics. The application already has a `StatusHttpServer` that exposes Prometheus-formatted metrics. This package provides:
- Counter metrics (events published, dropped, integrity violations)
- Gauge metrics (current event rate, drop rate)
- Histogram metrics (latency tracking)
- Built-in runtime metrics (GC, thread pool, etc.)

**Usage**:
```csharp
// In Metrics.cs - replace manual counter tracking with:
private static readonly Counter PublishedCounter = Metrics.CreateCounter(
    "mdc_published_total",
    "Total events published"
);

// Increment:
PublishedCounter.Inc();
```

### Resilience and Retry

| Package | Version | Purpose |
|---------|---------|---------|
| Polly | 8.4.2 | Resilience and transient fault handling |
| Polly.Extensions | 8.4.2 | Microsoft.Extensions integration |

**Why**: Network connections to IB, Alpaca, and Polygon are unreliable. Polly provides:
- Retry policies for transient failures
- Circuit breakers to prevent cascading failures
- Timeout policies to prevent hanging connections
- Fallback strategies

**Recommended Usage**:
```csharp
// In AlpacaMarketDataClient.cs - wrap WebSocket connection:
var retryPolicy = Policy
    .Handle<WebSocketException>()
    .Or<HttpRequestException>()
    .WaitAndRetryAsync(
        retryCount: 5,
        sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
        onRetry: (exception, timeSpan, attempt, context) =>
        {
            _logger.Warning("WebSocket connection failed, retry {Attempt} after {Delay}ms",
                attempt, timeSpan.TotalMilliseconds);
        }
    );

await retryPolicy.ExecuteAsync(async () =>
{
    await _ws.ConnectAsync(uri, ct);
});
```

### Performance

| Package | Version | Purpose |
|---------|---------|---------|
| System.Threading.Channels | 8.0.0 | Bounded channels for event pipeline |
| System.IO.Pipelines | 8.0.0 | High-performance I/O operations |

**Why**:
- **Channels**: Already used in `EventPipeline.cs` for bounded queues with backpressure. Explicit reference ensures latest version.
- **Pipelines**: Recommended for zero-copy WebSocket message parsing in `AlpacaMarketDataClient.cs` to reduce allocations.

### Validation

| Package | Version | Purpose |
|---------|---------|---------|
| FluentValidation | 11.10.0 | Fluent validation for configuration |

**Why**: Configuration errors are a common source of runtime failures. FluentValidation provides:
- Expressive validation rules
- Clear error messages
- Validation on startup (fail-fast)

**Recommended Usage**:
```csharp
public class AlpacaOptionsValidator : AbstractValidator<AlpacaOptions>
{
    public AlpacaOptionsValidator()
    {
        RuleFor(x => x.KeyId).NotEmpty().WithMessage("Alpaca KeyId is required");
        RuleFor(x => x.SecretKey).NotEmpty().WithMessage("Alpaca SecretKey is required");
        RuleFor(x => x.Feed).Must(f => f == "iex" || f == "sip")
            .WithMessage("Feed must be 'iex' or 'sip'");
    }
}
```

### Compression

| Package | Version | Purpose |
|---------|---------|---------|
| System.IO.Compression | 4.3.0 | Gzip compression for JSONL files |

**Why**: Already used in `JsonlStorageSink.cs` for optional JSONL compression. Reduces storage costs for tick data (typical compression ratio: 5-10x).

### HTTP Client

| Package | Version | Purpose |
|---------|---------|---------|
| System.Net.Http.Json | 8.0.0 | JSON extension methods for HttpClient |

**Why**: Simplifies REST API calls for Polygon and other HTTP-based providers. Provides `GetFromJsonAsync<T>()` and `PostAsJsonAsync<T>()` extension methods.

### WebSocket Client

| Package | Version | Purpose |
|---------|---------|---------|
| System.Net.WebSockets.Client | 4.3.2 | WebSocket client implementation |

**Why**: Already used in `AlpacaMarketDataClient.cs`. Explicit reference ensures availability across all platforms.

### Interactive Brokers API

| Package | Version | Purpose |
|---------|---------|---------|
| IBApi (Manual) | 10.19+ | Interactive Brokers TWS API |

**Why**: Required for IB connectivity. Not available as standard NuGet package - must be installed manually. See `docs/interactive-brokers-setup.md` for installation instructions.

**Note**: Code uses `#if IBAPI` conditional compilation so the project builds without it.

### MassTransit

| Package | Version | Purpose |
|---------|---------|---------|
| MassTransit | 8.2.5 | Distributed application framework for messaging |
| MassTransit.RabbitMQ | 8.2.5 | RabbitMQ transport for MassTransit |
| MassTransit.Azure.ServiceBus.Core | 8.2.5 | Azure Service Bus transport |
| Microsoft.Extensions.Hosting | 8.0.0 | Hosting abstractions for background services |

**Why**: MassTransit provides enterprise-grade message bus capabilities for:
- Publishing market events to external consumers
- Distributed system integration
- Reliable message delivery with retry policies
- Support for multiple transport backends (RabbitMQ, Azure Service Bus, etc.)

**Usage**:
```csharp
// Configure MassTransit in DI container
services.AddMassTransit(x =>
{
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host("localhost", "/", h =>
        {
            h.Username("guest");
            h.Password("guest");
        });
    });
});
```

### QuantConnect Lean Engine

| Package | Version | Purpose |
|---------|---------|---------|
| QuantConnect.Lean | 2.5.17315 | Main Lean algorithmic trading engine |
| QuantConnect.Lean.Engine | 2.5.17269 | Core engine and datafeed implementation |
| QuantConnect.Common | 2.5.17315 | Common utilities and interfaces |
| QuantConnect.Indicators | 2.5.17212 | Technical indicators library |

**Why**: Integration with QuantConnect's Lean Engine enables:
- **Algorithmic Trading**: Use collected market data for live trading and backtesting
- **Custom Data Types**: Expose MarketDataCollector events as Lean BaseData types
- **Backtesting**: Test strategies on high-fidelity tick data
- **Indicators**: Access 200+ technical indicators for analysis
- **Research**: Jupyter notebook integration for data exploration

**Key Integration Components**:

1. **Custom BaseData Types**:
   - `MarketDataCollectorTradeData` - Tick-by-tick trade events
   - `MarketDataCollectorQuoteData` - BBO quote events

2. **Custom Data Provider**:
   - `MarketDataCollectorDataProvider` - Reads JSONL files for Lean

3. **Sample Algorithms**:
   - Spread arbitrage strategies
   - Order flow imbalance detection
   - Microstructure-aware trading

**Usage Example**:
```csharp
using QuantConnect.Algorithm;
using MarketDataCollector.Integrations.Lean;

public class MyAlgorithm : QCAlgorithm
{
    public override void Initialize()
    {
        SetStartDate(2024, 1, 1);
        SetCash(100000);

        // Subscribe to MarketDataCollector data
        AddData<MarketDataCollectorTradeData>("SPY", Resolution.Tick);
        AddData<MarketDataCollectorQuoteData>("SPY", Resolution.Tick);
    }

    public override void OnData(Slice data)
    {
        if (data.ContainsKey("SPY") && data["SPY"] is MarketDataCollectorTradeData trade)
        {
            Debug($"Trade: {trade.TradePrice:F2} x {trade.TradeSize}");
            Debug($"Aggressor: {trade.AggressorSide}");
        }
    }
}
```

**Documentation**: See `src/MarketDataCollector/Integrations/Lean/README.md` for comprehensive integration guide.

---

## MarketDataCollector.Ui (Web Dashboard)

### ASP.NET Core

| Package | Version | Purpose |
|---------|---------|---------|
| Microsoft.AspNetCore.OpenApi | 8.0.0 | OpenAPI/Swagger support |

**Why**: Provides API documentation and interactive testing UI for the monitoring dashboard.

### Logging

| Package | Version | Purpose |
|---------|---------|---------|
| Serilog.AspNetCore | 8.0.3 | Serilog integration for ASP.NET Core |

**Why**: Provides request logging, enrichment with HTTP context, and ASP.NET Core integration.

### Monitoring

| Package | Version | Purpose |
|---------|---------|---------|
| prometheus-net.AspNetCore | 8.2.1 | Prometheus metrics for ASP.NET Core |

**Why**: Exposes HTTP metrics (request count, duration, status codes) alongside custom business metrics.

---

## Recommended Additional Packages

The following packages are **not yet added** but are recommended for future enhancements:

### Testing

| Package | Purpose |
|---------|---------|
| xunit (2.6.6) | Testing framework |
| xunit.runner.visualstudio (2.5.6) | Visual Studio test runner |
| Moq (4.20.70) | Mocking framework |
| FluentAssertions (6.12.0) | Fluent assertion library |
| Bogus (35.4.0) | Fake data generator |

**Why**: Currently no test project exists. Unit testing is critical for:
- Validating collectors (TradeDataCollector, MarketDepthCollector)
- Testing order book state transitions
- Verifying integrity event detection
- Mocking provider clients

**To Add**:
```bash
dotnet new xunit -n MarketDataCollector.Tests -o tests/MarketDataCollector.Tests
dotnet sln add tests/MarketDataCollector.Tests/MarketDataCollector.Tests.csproj
cd tests/MarketDataCollector.Tests
dotnet add package xunit
dotnet add package xunit.runner.visualstudio
dotnet add package Moq
dotnet add package FluentAssertions
dotnet add package Bogus
dotnet add reference ../../src/MarketDataCollector/MarketDataCollector.csproj
```

### Benchmarking

| Package | Purpose |
|---------|---------|
| BenchmarkDotNet (0.13.12) | Micro-benchmarking framework |

**Why**: Performance is critical for market data collection. Benchmark:
- Event pipeline throughput
- Order book update latency
- JSON serialization performance
- Memory allocation in hot paths

**To Add**:
```bash
dotnet new console -n MarketDataCollector.Benchmarks -o benchmarks
dotnet add package BenchmarkDotNet
dotnet add reference ../src/MarketDataCollector/MarketDataCollector.csproj
```

### Advanced Time Series Storage

| Package | Purpose |
|---------|---------|
| Parquet.Net (4.x) | Apache Parquet columnar storage |

**Why**: JSONL is simple but not optimal for analytics. Parquet provides:
- 10-20x better compression
- Columnar storage for fast aggregations
- Integration with Pandas, Spark, DuckDB
- Schema evolution support

---

## Dependency Management Best Practices

### Version Policy

- **Major versions**: Always use latest stable .NET 8.x packages
- **Security updates**: Update immediately when CVEs are announced
- **Breaking changes**: Pin versions and test thoroughly before upgrading

### Monitoring for Updates

```bash
# Check for outdated packages
dotnet list package --outdated

# Update all packages to latest minor/patch versions
dotnet list package --outdated | grep ">" | awk '{print $2}' | xargs -I {} dotnet add package {}
```

### Vulnerability Scanning

```bash
# Check for known vulnerabilities
dotnet list package --vulnerable --include-transitive
```

### License Compliance

All packages use permissive licenses compatible with commercial use:
- **MIT**: Serilog, FluentValidation, Polly, prometheus-net
- **Apache 2.0**: Microsoft.Extensions.*, System.*

**Exception**: IBApi is proprietary - review IB's license agreement.

---

## Restore and Build

### Initial Setup

```bash
# Restore all packages
dotnet restore

# Build solution
dotnet build

# Build with IB API support (requires IBApi DLL)
dotnet build -p:DefineConstants="IBAPI"
```

### CI/CD Considerations

NuGet packages are restored automatically during build. For CI/CD:

```yaml
# .github/workflows/build.yml
- name: Restore dependencies
  run: dotnet restore

- name: Build
  run: dotnet build --no-restore --configuration Release

- name: Test
  run: dotnet test --no-build --configuration Release
```

---

## Future Enhancements

1. **Add OpenTelemetry**
   - Distributed tracing across pipeline
   - Replace prometheus-net for unified observability

2. **Add MessagePack**
   - Binary serialization for internal events
   - Faster than JSON, smaller than JSON

3. **Add FASTER.KV** (Microsoft Research)
   - Ultra-fast persistent key-value store
   - Alternative to in-memory order book state

4. **Add Apache Arrow**
   - In-memory columnar format
   - Zero-copy data sharing with analytics tools

---

**Last Updated**: 2026-01-01
**NuGet Package Count**: 26 (main project) + 3 (UI project)
**Total Download Size**: ~15 MB
