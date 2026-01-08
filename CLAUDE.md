# CLAUDE.md - AI Assistant Guide for Market Data Collector

This document provides essential context for AI assistants (Claude, Copilot, etc.) working with the Market Data Collector codebase.

## Project Overview

Market Data Collector is a high-performance, cross-platform market data collection system built on **.NET 9.0** using **C# 11** and **F# 8.0**. It captures real-time and historical market microstructure data from multiple providers and persists it for downstream research, backtesting, and algorithmic trading.

**Version:** 1.5.0 | **Status:** Production Ready

### Key Capabilities
- Real-time streaming from Interactive Brokers, Alpaca, NYSE, StockSharp
- Historical backfill from 9+ providers (Yahoo Finance, Stooq, Tiingo, etc.)
- Archival-first storage with Write-Ahead Logging (WAL)
- Microservices architecture with MassTransit messaging
- Web dashboard and native UWP Windows desktop application
- QuantConnect Lean Engine integration for backtesting

---

## Quick Commands

```bash
# Build the project (from repo root)
dotnet build -c Release

# Run tests
dotnet test tests/MarketDataCollector.Tests

# Run F# tests
dotnet test tests/MarketDataCollector.FSharp.Tests

# Run with web dashboard
dotnet run --project src/MarketDataCollector/MarketDataCollector.csproj -- --ui --http-port 8080

# Run benchmarks
dotnet run --project benchmarks/MarketDataCollector.Benchmarks -c Release

# Using Makefile (from repo root)
make build       # Build the project
make test        # Run tests
make run-ui      # Run with web dashboard
make docker      # Build and run Docker container
make help        # Show all available commands
```

---

## Repository Structure

```
Market-Data-Collector/
├── .github/                          # GitHub configuration
│   ├── workflows/                    # CI/CD pipelines
│   └── prompts/                      # AI assistant prompts
│
├── docs/                             # All documentation
│   ├── architecture/                 # System architecture docs
│   ├── guides/                       # How-to guides
│   ├── providers/                    # Provider setup guides
│   ├── integrations/                 # Integration guides
│   ├── api/                          # API reference
│   ├── status/                       # Roadmap, backlog
│   ├── ai-assistants/                # Specialized AI guides
│   │   ├── CLAUDE.fsharp.md          # F# domain guide
│   │   ├── CLAUDE.microservices.md   # Microservices guide
│   │   ├── CLAUDE.providers.md       # Data providers guide
│   │   ├── CLAUDE.storage.md         # Storage architecture guide
│   │   └── CLAUDE.testing.md         # Testing strategy guide
│   ├── USAGE.md                      # Detailed usage guide
│   ├── HELP.md                       # User guide with FAQ
│   └── DEPENDENCIES.md               # Package documentation
│
├── scripts/                          # All scripts
│   ├── install/                      # Installation scripts
│   ├── publish/                      # Publishing scripts
│   ├── run/                          # Runtime scripts
│   └── diagnostics/                  # Diagnostic scripts
│
├── deploy/                           # Deployment configurations
│   ├── docker/                       # Dockerfile, docker-compose
│   ├── systemd/                      # Linux systemd service
│   └── monitoring/                   # Prometheus, Grafana configs
│
├── config/                           # Configuration files
│   ├── appsettings.json              # Runtime config
│   └── appsettings.sample.json       # Configuration template
│
├── src/                              # Source code
│   ├── MarketDataCollector/          # Core application (entry point)
│   │   ├── Domain/                   # Business logic, collectors
│   │   ├── Infrastructure/           # Provider implementations
│   │   │   ├── DataSources/          # Data source abstractions
│   │   │   └── Providers/            # IB, Alpaca, NYSE, etc.
│   │   ├── Storage/                  # JSONL/Parquet sinks
│   │   ├── Messaging/                # MassTransit publishers
│   │   └── Application/              # Startup, config, HTTP
│   ├── MarketDataCollector.FSharp/   # F# domain models
│   ├── MarketDataCollector.Contracts/# Shared DTOs
│   ├── MarketDataCollector.Ui/       # Web dashboard
│   ├── MarketDataCollector.Uwp/      # Windows desktop app
│   └── Microservices/                # Decomposed services
│       ├── Gateway/                  # API Gateway (5000)
│       ├── TradeIngestion/           # Trade service (5001)
│       ├── QuoteIngestion/           # Quote service (5002)
│       ├── OrderBookIngestion/       # Order book (5003)
│       ├── HistoricalDataIngestion/  # Historical (5004)
│       ├── DataValidation/           # Validation (5005)
│       └── Shared/                   # Shared contracts
│
├── tests/                            # Test projects
│   ├── MarketDataCollector.Tests/    # C# unit tests
│   └── MarketDataCollector.FSharp.Tests/ # F# tests
│
├── benchmarks/                       # Performance benchmarks
│   └── MarketDataCollector.Benchmarks/
│
├── MarketDataCollector.sln           # Solution file
├── Directory.Build.props             # Build settings
├── Makefile                          # Build automation
├── CLAUDE.md                         # This file
├── README.md                         # Project overview
└── LICENSE                           # License
```

---

## Critical Rules

When contributing to this project, **always follow these rules**:

### Must-Follow Rules
- **ALWAYS** use `CancellationToken` on async methods
- **NEVER** store secrets in code or config files - use environment variables
- **ALWAYS** use structured logging with semantic parameters: `_logger.LogInformation("Received {Count} bars for {Symbol}", count, symbol)`
- **PREFER** `IAsyncEnumerable<T>` for streaming data over collections
- **ALWAYS** mark classes as `sealed` unless designed for inheritance
- **NEVER** log sensitive data (API keys, credentials)
- **NEVER** use `Task.Run` for I/O-bound operations (wastes thread pool)
- **NEVER** block async code with `.Result` or `.Wait()` (causes deadlocks)

### Architecture Principles
1. **Provider Independence** - All providers implement `IMarketDataClient` interface
2. **No Vendor Lock-in** - Provider-agnostic interfaces with failover
3. **Security First** - Environment variables for credentials
4. **Observability** - Structured logging, Prometheus metrics, health endpoints
5. **Modularity** - Separate projects for core, domain, UI, microservices

---

## Key Interfaces

### IMarketDataClient (Streaming)
```csharp
public interface IMarketDataClient : IAsyncDisposable
{
    Task ConnectAsync(CancellationToken ct = default);
    Task DisconnectAsync();
    Task SubscribeAsync(SymbolSubscription subscription, CancellationToken ct = default);
    Task UnsubscribeAsync(string symbol, CancellationToken ct = default);
    IAsyncEnumerable<MarketDataEvent> GetEventsAsync(CancellationToken ct = default);
    ConnectionState State { get; }
}
```

### IHistoricalDataProvider (Backfill)
```csharp
public interface IHistoricalDataProvider
{
    Task<IReadOnlyList<OhlcBar>> GetHistoricalBarsAsync(
        string symbol, DateTime start, DateTime end,
        BarTimeframe timeframe, CancellationToken ct = default);

    IAsyncEnumerable<OhlcBar> StreamHistoricalBarsAsync(
        string symbol, DateTime start, DateTime end,
        BarTimeframe timeframe, CancellationToken ct = default);
}
```

---

## Testing

### Test Framework Stack
- **xUnit** - Test framework
- **FluentAssertions** - Fluent assertions
- **Moq** / **NSubstitute** - Mocking frameworks
- **MassTransit.TestFramework** - Message bus testing
- **coverlet** - Code coverage

### Running Tests
```bash
# Run all tests
dotnet test

# Run specific test project
dotnet test tests/MarketDataCollector.Tests

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run F# tests
dotnet test tests/MarketDataCollector.FSharp.Tests
```

### Test Categories
- Unit tests: `tests/MarketDataCollector.Tests/`
- F# domain tests: `tests/MarketDataCollector.FSharp.Tests/`
- Benchmarks: `benchmarks/MarketDataCollector.Benchmarks/`

---

## Configuration

### Environment Variables
API credentials should be set via environment variables:
```bash
export ALPACA__KEYID=your-key-id
export ALPACA__SECRETKEY=your-secret-key
export NYSE__APIKEY=your-api-key
```

Note: Use double underscore (`__`) for nested configuration (maps to `Alpaca:KeyId`).

### appsettings.json
Configuration file should be copied from template:
```bash
cp config/appsettings.sample.json config/appsettings.json
```

Key sections:
- `DataSource` - Active provider (IB, Alpaca, NYSE)
- `Symbols` - List of symbols to subscribe
- `Storage` - File organization, retention, compression
- `Backfill` - Historical data settings

---

## Coding Conventions

### Logging
Use structured logging with semantic parameters:
```csharp
// Good
_logger.LogInformation("Received {Count} bars for {Symbol}", bars.Count, symbol);

// Bad - don't interpolate
_logger.LogInformation($"Received {bars.Count} bars for {symbol}");
```

### Error Handling
- Log all errors with context (symbol, provider, timestamp)
- Use exponential backoff for retries
- Throw `ArgumentException` for bad inputs
- Throw `InvalidOperationException` for state errors
- Use `Result<T, TError>` in F# code

### Naming Conventions
- Async methods end with `Async`
- Cancellation token parameter named `ct` or `cancellationToken`
- Private fields prefixed with `_`
- Interfaces prefixed with `I`

### Performance
- Avoid allocations in hot paths
- Use object pooling for frequently created objects
- Prefer `Span<T>` and `Memory<T>` for buffer operations
- Use `System.Threading.Channels` for producer-consumer patterns
- Consider lock-free alternatives for high-contention scenarios

---

## Domain Models

### Core Event Types
- `Trade` - Tick-by-tick trade prints with sequence validation
- `LOBSnapshot` - Full L2 order book state
- `BboQuote` - Best bid/offer with spread and mid-price
- `OrderFlowStatistics` - Rolling VWAP, imbalance, volume splits
- `IntegrityEvent` - Sequence anomalies (gaps, out-of-order)
- `HistoricalBar` - OHLCV bars from backfill providers

### Key Classes
| Class | Location | Purpose |
|-------|----------|---------|
| `TradeDataCollector` | `src/MarketDataCollector/Domain/Collectors/` | Tick-by-tick trade processing |
| `MarketDepthCollector` | `src/MarketDataCollector/Domain/Collectors/` | L2 order book maintenance |
| `QuoteCollector` | `src/MarketDataCollector/Domain/Collectors/` | BBO state tracking |
| `EventPipeline` | `src/MarketDataCollector/Storage/` | Bounded channel event routing |
| `JsonlStorageSink` | `src/MarketDataCollector/Storage/Sinks/` | JSONL file persistence |
| `AlpacaMarketDataClient` | `src/MarketDataCollector/Infrastructure/Providers/` | Alpaca WebSocket client |

---

## Storage Architecture

### File Organization
```
data/
├── live/                    # Real-time data (hot tier)
│   ├── {provider}/
│   │   └── {date}/
│   │       ├── {symbol}_trades.jsonl.gz
│   │       └── {symbol}_quotes.jsonl.gz
├── historical/              # Backfill data
│   └── {provider}/
│       └── {date}/
│           └── {symbol}_bars.jsonl
└── _archive/                # Compressed archives (cold tier)
    └── parquet/
```

### Naming Conventions
- **BySymbol** (default): `{root}/{symbol}/{type}/{date}.jsonl`
- **ByDate**: `{root}/{date}/{symbol}/{type}.jsonl`
- **ByType**: `{root}/{type}/{symbol}/{date}.jsonl`
- **Flat**: `{root}/{symbol}_{type}_{date}.jsonl`

### Compression Profiles
| Profile | Algorithm | Use Case |
|---------|-----------|----------|
| RealTime | LZ4 | Live streaming data |
| Standard | Gzip | General purpose |
| Archive | ZSTD-19 | Long-term storage |

---

## HTTP Endpoints

When running with `--ui` flag:
- `/` - HTML dashboard (auto-refreshing)
- `/status` - JSON status with metrics
- `/metrics` - Prometheus metrics
- `/health`, `/ready`, `/live` - Kubernetes health probes
- `/api/backfill/*` - Backfill management API

---

## Common Tasks

### Adding a New Data Provider
1. Create client class in `src/MarketDataCollector/Infrastructure/Providers/{ProviderName}/`
2. Implement `IMarketDataClient` interface
3. Add `[DataSource("provider-name")]` attribute
4. Register in DI container in `Program.cs`
5. Add configuration section in `config/appsettings.sample.json`
6. Add tests in `tests/MarketDataCollector.Tests/`

### Adding a New Historical Provider
1. Create provider in `src/MarketDataCollector/Infrastructure/Providers/Backfill/`
2. Implement `IHistoricalDataProvider` interface
3. Register in `CompositeHistoricalDataProvider`
4. Add to provider priority list

### Running Backfill
```bash
# Via command line
dotnet run --project src/MarketDataCollector -- \
  --backfill --backfill-provider stooq \
  --backfill-symbols SPY,AAPL \
  --backfill-from 2024-01-01 --backfill-to 2024-01-05

# Via Makefile
make run-backfill SYMBOLS=SPY,AAPL
```

---

## CI/CD

### GitHub Actions Workflow
Located at `.github/workflows/dotnet-desktop.yml`:
- Builds on push to `main` and PRs
- Runs tests on Ubuntu
- Publishes for Linux, Windows, macOS (x64 + ARM64)
- Creates releases on version tags (`v*`)

### Build Requirements
- .NET 9.0 SDK
- `EnableWindowsTargeting=true` for cross-platform builds (set in `Directory.Build.props`)

---

## Anti-Patterns to Avoid

| Anti-Pattern | Why It's Bad |
|--------------|--------------|
| Swallowing exceptions silently | Hides bugs, makes debugging impossible |
| Hardcoding credentials | Security risk, inflexible deployment |
| Using `Task.Run` for I/O | Wastes thread pool threads |
| Blocking async with `.Result` | Causes deadlocks |
| Creating new `HttpClient` instances | Socket exhaustion, DNS issues |
| Logging with string interpolation | Loses structured logging benefits |
| Missing CancellationToken | Prevents graceful shutdown |

---

## Documentation

Key documentation files in `docs/`:
- `guides/getting-started.md` - Setup and first run
- `guides/configuration.md` - All configuration options
- `guides/operator-runbook.md` - Production operations
- `architecture/overview.md` - System architecture
- `architecture/domains.md` - Event contracts
- `providers/backfill-guide.md` - Historical data guide
- `integrations/lean-integration.md` - QuantConnect Lean guide
- `ai-assistants/CLAUDE.*.md` - Specialized AI assistant guides

---

## Troubleshooting

### Build Issues
```bash
# Run diagnostic script
./scripts/diagnostics/diagnose-build.sh

# Manual restore with diagnostics
dotnet restore /p:EnableWindowsTargeting=true -v diag
```

### Common Issues
1. **NETSDK1100 error** - Ensure `EnableWindowsTargeting=true` is set
2. **Credential errors** - Check environment variables are set
3. **Connection failures** - Verify API keys and network connectivity
4. **High memory** - Check channel capacity in `EventPipeline`

---

## Related Resources

- [README.md](README.md) - Project overview
- [docs/USAGE.md](docs/USAGE.md) - Detailed usage guide
- [docs/HELP.md](docs/HELP.md) - User guide with FAQ
- [docs/DEPENDENCIES.md](docs/DEPENDENCIES.md) - Package documentation
- [docs/ai-assistants/](docs/ai-assistants/) - Specialized AI guides

---

*Last Updated: 2026-01-08*
