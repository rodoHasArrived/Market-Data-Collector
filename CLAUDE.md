# CLAUDE.md - AI Assistant Guide for Market Data Collector

This document provides essential context for AI assistants (Claude, Copilot, etc.) working with the Market Data Collector codebase.

## Project Overview

Market Data Collector is a high-performance, cross-platform market data collection system built on **.NET 9.0** using **C# 11** and **F# 8.0**. It captures real-time and historical market microstructure data from multiple providers and persists it for downstream research, backtesting, and algorithmic trading.

**Version:** 1.6.0 | **Status:** Production Ready

### Key Capabilities
- Real-time streaming from Interactive Brokers, Alpaca, NYSE, Polygon, StockSharp
- Historical backfill from 9+ providers (Yahoo Finance, Stooq, Tiingo, Alpha Vantage, Finnhub, Nasdaq Data Link, etc.)
- Archival-first storage with Write-Ahead Logging (WAL)
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
make docs        # Generate documentation
make help        # Show all available commands

# Diagnostics (via Makefile)
make doctor      # Run full diagnostic check
make diagnose    # Build diagnostics
make metrics     # Show build metrics

# First-Time Setup (Auto-Configuration)
dotnet run --project src/MarketDataCollector/MarketDataCollector.csproj -- --wizard           # Interactive configuration wizard
dotnet run --project src/MarketDataCollector/MarketDataCollector.csproj -- --auto-config     # Quick auto-configuration from env vars
dotnet run --project src/MarketDataCollector/MarketDataCollector.csproj -- --detect-providers # Show available providers
dotnet run --project src/MarketDataCollector/MarketDataCollector.csproj -- --validate-credentials # Validate API credentials
```

---

## Repository Structure

```
Market-Data-Collector/
├── .github/                          # GitHub configuration
│   ├── workflows/                    # CI/CD pipelines
│   ├── agents/                       # AI agent configurations
│   ├── prompts/                      # AI assistant prompts
│   ├── copilot-instructions.md       # GitHub Copilot instructions
│   ├── dependabot.yml                # Dependency updates
│   └── labeler.yml                   # PR auto-labeling
│
├── docs/                             # All documentation
│   ├── architecture/                 # System architecture docs
│   ├── guides/                       # How-to guides
│   ├── providers/                    # Provider setup guides
│   ├── integrations/                 # Integration guides
│   ├── api/                          # API reference
│   ├── status/                       # Roadmap, backlog
│   ├── adr/                          # Architecture Decision Records
│   ├── ai-assistants/                # Specialized AI guides
│   ├── changelogs/                   # Version changelogs
│   ├── diagrams/                     # Architecture diagrams
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
├── build-system/                     # Python build tooling
│   ├── cli/                          # Command-line tools
│   ├── adapters/                     # Build adapters
│   ├── analytics/                    # Build analytics
│   ├── core/                         # Core utilities
│   └── diagnostics/                  # Diagnostic tools
│
├── tools/                            # Development tools
│   └── DocGenerator/                 # Documentation generator (.NET)
│
├── deploy/                           # Deployment configurations
│   ├── docker/                       # Dockerfile, docker-compose
│   ├── systemd/                      # Linux systemd service
│   └── monitoring/                   # Prometheus, Grafana configs
│
├── config/                           # Configuration files
│   ├── appsettings.json              # Runtime config (gitignored)
│   └── appsettings.sample.json       # Configuration template
│
├── src/                              # Source code
│   ├── MarketDataCollector/          # Core application (entry point)
│   │   ├── Domain/                   # Business logic
│   │   │   ├── Collectors/           # Data collectors
│   │   │   ├── Events/               # Domain events
│   │   │   └── Models/               # Domain models
│   │   ├── Infrastructure/           # Provider implementations
│   │   │   ├── Providers/            # Data providers
│   │   │   │   ├── Alpaca/           # Alpaca Markets
│   │   │   │   ├── Backfill/         # Historical data providers
│   │   │   │   ├── InteractiveBrokers/ # IB Gateway
│   │   │   │   ├── NYSE/             # NYSE data
│   │   │   │   ├── Polygon/          # Polygon.io
│   │   │   │   ├── StockSharp/       # StockSharp connectors
│   │   │   │   ├── MultiProvider/    # Multi-provider routing
│   │   │   │   └── SymbolSearch/     # Symbol search providers
│   │   │   ├── DataSources/          # Data source abstractions
│   │   │   ├── Resilience/           # WebSocket resilience (Polly)
│   │   │   └── IMarketDataClient.cs  # Core streaming interface
│   │   ├── Storage/                  # Data persistence
│   │   │   ├── Sinks/                # JSONL/Parquet writers
│   │   │   ├── Archival/             # Archive management
│   │   │   ├── Export/               # Data export
│   │   │   ├── Maintenance/          # Archive maintenance
│   │   │   ├── Packaging/            # Portable data packages
│   │   │   ├── Replay/               # Data replay
│   │   │   ├── Policies/             # Retention policies
│   │   │   └── Services/             # Storage services
│   │   ├── Application/              # Startup, config, HTTP
│   │   ├── Integrations/             # External integrations
│   │   │   └── Lean/                 # QuantConnect Lean
│   │   └── Tools/                    # Utility tools
│   ├── MarketDataCollector.FSharp/   # F# domain models
│   ├── MarketDataCollector.Contracts/# Shared DTOs
│   ├── MarketDataCollector.Ui/       # Web dashboard
│   └── MarketDataCollector.Uwp/      # Windows desktop app
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
- **ALWAYS** add `[ImplementsAdr]` attributes when implementing ADR contracts

### Architecture Principles
1. **Provider Independence** - All providers implement `IMarketDataClient` interface
2. **No Vendor Lock-in** - Provider-agnostic interfaces with failover
3. **Security First** - Environment variables for credentials
4. **Observability** - Structured logging, Prometheus metrics, health endpoints
5. **Simplicity** - Monolithic core with optional UI projects
6. **ADR Compliance** - Follow Architecture Decision Records in `docs/adr/`

---

## Key Interfaces

### IMarketDataClient (Streaming)
Location: `src/MarketDataCollector/Infrastructure/IMarketDataClient.cs`

```csharp
[ImplementsAdr("ADR-001", "Core streaming data provider contract")]
[ImplementsAdr("ADR-004", "All async methods support CancellationToken")]
public interface IMarketDataClient : IAsyncDisposable
{
    bool IsEnabled { get; }
    Task ConnectAsync(CancellationToken ct = default);
    Task DisconnectAsync(CancellationToken ct = default);
    int SubscribeMarketDepth(SymbolConfig cfg);
    void UnsubscribeMarketDepth(int subscriptionId);
    int SubscribeTrades(SymbolConfig cfg);
    void UnsubscribeTrades(int subscriptionId);
}
```

### IHistoricalDataProvider (Backfill)
Location: `src/MarketDataCollector/Infrastructure/Providers/Backfill/IHistoricalDataProvider.cs`

```csharp
[ImplementsAdr("ADR-001", "Core historical data provider contract")]
[ImplementsAdr("ADR-004", "All async methods support CancellationToken")]
public interface IHistoricalDataProvider
{
    string Name { get; }
    string DisplayName { get; }
    string Description { get; }

    // Capabilities
    HistoricalDataCapabilities Capabilities { get; }
    int Priority { get; }

    Task<IReadOnlyList<HistoricalBar>> GetDailyBarsAsync(
        string symbol, DateOnly? from, DateOnly? to, CancellationToken ct = default);

    // Extended methods for tick data, quotes, trades, auctions
}
```

---

## Architecture Decision Records (ADRs)

ADRs document significant architectural decisions. Located in `docs/adr/`:

| ADR | Title | Key Points |
|-----|-------|------------|
| ADR-001 | Provider Abstraction | Interface contracts for data providers |
| ADR-002 | Tiered Storage | Hot/cold storage architecture |
| ADR-004 | Async Streaming Patterns | CancellationToken, IAsyncEnumerable |
| ADR-005 | Attribute-Based Discovery | `[DataSource]`, `[ImplementsAdr]` attributes |

Use `[ImplementsAdr("ADR-XXX", "reason")]` attribute when implementing ADR contracts.

---

## Testing

### Test Framework Stack
- **xUnit** - Test framework
- **FluentAssertions** - Fluent assertions
- **Moq** / **NSubstitute** - Mocking frameworks
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
export POLYGON__APIKEY=your-api-key
export TIINGO__TOKEN=your-token
```

Note: Use double underscore (`__`) for nested configuration (maps to `Alpaca:KeyId`).

### appsettings.json
Configuration file should be copied from template:
```bash
cp config/appsettings.sample.json config/appsettings.json
```

Key sections:
- `DataSource` - Active provider (IB, Alpaca, NYSE, Polygon)
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
| `EventPipeline` | `src/MarketDataCollector/Storage/Services/` | Bounded channel event routing |
| `JsonlStorageSink` | `src/MarketDataCollector/Storage/Sinks/` | JSONL file persistence |
| `AlpacaMarketDataClient` | `src/MarketDataCollector/Infrastructure/Providers/Alpaca/` | Alpaca WebSocket client |
| `CompositeHistoricalDataProvider` | `src/MarketDataCollector/Infrastructure/Providers/Backfill/` | Multi-provider backfill with fallback |

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
4. Add `[ImplementsAdr("ADR-001", "reason")]` attribute
5. Register in DI container in `Program.cs`
6. Add configuration section in `config/appsettings.sample.json`
7. Add tests in `tests/MarketDataCollector.Tests/`

### Adding a New Historical Provider
1. Create provider in `src/MarketDataCollector/Infrastructure/Providers/Backfill/`
2. Implement `IHistoricalDataProvider`
3. Add `[ImplementsAdr]` attributes
4. Register in `CompositeHistoricalDataProvider`
5. Add to provider priority list

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

## Build Requirements

- .NET 9.0 SDK
- `EnableWindowsTargeting=true` for cross-platform builds (set in `Directory.Build.props`)
- Python 3 for build tooling (`build-system/`)
- Node.js for diagram generation (optional)

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
| Missing `[ImplementsAdr]` attribute | Loses ADR traceability |

---

## Backfill Providers

Available historical data providers (in priority order):

| Provider | Free Tier | Data Types | Rate Limits |
|----------|-----------|------------|-------------|
| Alpaca | Yes (with account) | Bars, trades, quotes | 200/min |
| Polygon | Limited | Bars, trades, quotes, aggregates | Varies |
| Tiingo | Yes | Daily bars | 500/hour |
| Yahoo Finance | Yes | Daily bars | Unofficial |
| Stooq | Yes | Daily bars | Low |
| Finnhub | Yes | Daily bars | 60/min |
| Alpha Vantage | Yes | Daily bars | 5/min |
| Nasdaq Data Link | Limited | Various | Varies |

Configure fallback chain in `appsettings.json` under `Backfill.ProviderPriority`.

---

## Documentation

Key documentation files:
- `docs/guides/getting-started.md` - Setup and first run
- `docs/guides/configuration.md` - All configuration options
- `docs/guides/operator-runbook.md` - Production operations
- `docs/architecture/overview.md` - System architecture
- `docs/architecture/domains.md` - Event contracts
- `docs/providers/backfill-guide.md` - Historical data guide
- `docs/integrations/lean-integration.md` - QuantConnect Lean guide
- `docs/adr/` - Architecture Decision Records
- `docs/ai-assistants/CLAUDE.*.md` - Specialized AI assistant guides

---

## Troubleshooting

### Build Issues
```bash
# Run diagnostic script
./scripts/diagnostics/diagnose-build.sh

# Use build control CLI
make doctor

# Manual restore with diagnostics
dotnet restore /p:EnableWindowsTargeting=true -v diag
```

### Common Issues
1. **NETSDK1100 error** - Ensure `EnableWindowsTargeting=true` is set
2. **Credential errors** - Check environment variables are set
3. **Connection failures** - Verify API keys and network connectivity
4. **High memory** - Check channel capacity in `EventPipeline`
5. **Provider rate limits** - Check `ProviderRateLimitTracker` logs

---

## Related Resources

- [README.md](README.md) - Project overview
- [docs/USAGE.md](docs/USAGE.md) - Detailed usage guide
- [docs/HELP.md](docs/HELP.md) - User guide with FAQ
- [docs/DEPENDENCIES.md](docs/DEPENDENCIES.md) - Package documentation
- [docs/adr/](docs/adr/) - Architecture Decision Records
- [docs/ai-assistants/](docs/ai-assistants/) - Specialized AI guides
- [.github/copilot-instructions.md](.github/copilot-instructions.md) - Copilot instructions

---

*Last Updated: 2026-01-19*
