# CLAUDE.md - AI Assistant Guide for Market Data Collector

This document provides essential context for AI assistants (Claude, Copilot, etc.) working with the Market Data Collector codebase.

## Project Overview

Market Data Collector is a high-performance, cross-platform market data collection system built on **.NET 9.0** using **C# 11** and **F# 8.0**. It captures real-time and historical market microstructure data from multiple providers and persists it for downstream research, backtesting, and algorithmic trading.

**Version:** 1.6.1 | **Status:** Production Ready | **Files:** 501 source files

### Key Capabilities
- Real-time streaming from Interactive Brokers, Alpaca, NYSE, Polygon, StockSharp (90+ data sources)
- Historical backfill from 10+ providers with automatic fallback chain
- Symbol search from multiple providers (Alpaca, Finnhub, Polygon, OpenFIGI)
- Comprehensive data quality monitoring with SLA enforcement
- Archival-first storage with Write-Ahead Logging (WAL) and tiered storage
- Portable data packaging for sharing and archival
- Web dashboard and native UWP Windows desktop application
- QuantConnect Lean Engine integration for backtesting
- Scheduled maintenance and archive management

### Project Statistics
| Metric | Count |
|--------|-------|
| Total Source Files | 501 |
| C# Files | 489 |
| F# Files | 12 |
| Test Files | 48 |
| Documentation Files | 66 |
| Main Projects | 6 (+ 3 test/benchmark) |
| Provider Implementations | 5 streaming, 10 historical |
| Symbol Search Providers | 4 |
| CI/CD Workflows | 21 |
| Makefile Targets | 65 |

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

# Dry-Run Mode (validation without starting)
dotnet run --project src/MarketDataCollector/MarketDataCollector.csproj -- --dry-run         # Full validation
dotnet run --project src/MarketDataCollector/MarketDataCollector.csproj -- --dry-run --offline  # Skip connectivity checks

# Deployment Modes
dotnet run --project src/MarketDataCollector/MarketDataCollector.csproj -- --mode web        # Web dashboard mode
dotnet run --project src/MarketDataCollector/MarketDataCollector.csproj -- --mode headless   # Headless/service mode
dotnet run --project src/MarketDataCollector/MarketDataCollector.csproj -- --mode desktop    # Desktop UI mode
```

---

## Command-Line Reference

### Symbol Management
```bash
--symbols                    # Show all symbols (monitored + archived)
--symbols-monitored          # List symbols configured for monitoring
--symbols-archived           # List symbols with archived data
--symbols-add SPY,AAPL       # Add symbols to configuration
--symbols-remove TSLA        # Remove symbols from configuration
--symbol-status SPY          # Detailed status for a symbol
--no-trades                  # Don't subscribe to trade data
--no-depth                   # Don't subscribe to depth/L2 data
--depth-levels 10            # Number of depth levels to capture
```

### Configuration & Validation
```bash
--quick-check                # Fast configuration health check
--test-connectivity          # Test connectivity to all providers
--show-config                # Display current configuration
--error-codes                # Show error code reference guide
--check-schemas              # Check stored data schema compatibility
--validate-schemas           # Run schema check during startup
--strict-schemas             # Exit if schema incompatibilities found
--watch-config               # Enable hot-reload of configuration
```

### Data Packaging
```bash
--package                    # Create a portable data package
--import-package pkg.zip     # Import a package into storage
--list-package pkg.zip       # List package contents
--validate-package pkg.zip   # Validate package integrity
--package-symbols SPY,AAPL   # Symbols to include
--package-from 2024-01-01    # Start date
--package-to 2024-12-31      # End date
--package-format zip         # Format: zip, tar.gz
```

### Backfill Operations
```bash
--backfill                   # Run historical data backfill
--backfill-provider stooq    # Provider to use
--backfill-symbols SPY,AAPL  # Symbols to backfill
--backfill-from 2024-01-01   # Start date
--backfill-to 2024-01-05     # End date
```

---

## Repository Structure

```
Market-Data-Collector/
├── .github/                          # GitHub configuration
│   ├── workflows/                    # CI/CD pipelines (21 workflows)
│   ├── agents/                       # AI agent configurations
│   │   └── documentation-agent.md   # Documentation specialist guide
│   ├── prompts/                      # AI assistant prompts
│   ├── QUICKSTART.md                 # Workflow quick start guide
│   ├── dependabot.yml                # Dependency updates
│   └── labeler.yml                   # PR auto-labeling
│
├── docs/                             # All documentation
│   ├── getting-started/              # Onboarding guides
│   │   ├── setup.md                  # Setup and first run
│   │   ├── configuration.md          # Configuration options
│   │   └── troubleshooting.md        # Common issues and solutions
│   ├── architecture/                 # System architecture docs
│   │   ├── overview.md               # System architecture
│   │   ├── domains.md                # Event contracts
│   │   ├── storage-design.md         # Storage organization
│   │   ├── consolidation.md          # UI layer consolidation
│   │   ├── crystallized-storage-format.md # Storage format spec
│   │   ├── c4-diagrams.md            # C4 model visualizations
│   │   └── why-this-architecture.md  # Design rationale
│   ├── operations/                   # Production operations
│   │   ├── operator-runbook.md       # Operations guide
│   │   ├── portable-data-packager.md # Data packaging guide
│   │   └── msix-packaging.md         # Desktop packaging
│   ├── development/                  # Developer guides
│   │   ├── provider-implementation.md # Adding new providers
│   │   ├── uwp-development-roadmap.md # UWP development status
│   │   ├── uwp-release-checklist.md  # UWP release process
│   │   ├── github-actions-summary.md # CI/CD overview
│   │   ├── github-actions-testing.md # Testing workflows
│   │   └── project-context.md        # Project context
│   ├── providers/                    # Provider setup guides
│   │   ├── backfill-guide.md         # Historical data guide
│   │   ├── data-sources.md           # Available data sources
│   │   └── provider-comparison.md    # Feature comparison matrix
│   ├── integrations/                 # Integration guides
│   │   ├── lean-integration.md       # QuantConnect Lean guide
│   │   ├── fsharp-integration.md     # F# domain library
│   │   └── language-strategy.md      # Polyglot architecture
│   ├── ai/                           # AI assistant guides
│   │   ├── claude/                   # Claude-specific guides
│   │   │   ├── CLAUDE.providers.md   # Provider implementation
│   │   │   ├── CLAUDE.storage.md     # Storage system
│   │   │   ├── CLAUDE.fsharp.md      # F# domain library
│   │   │   └── CLAUDE.testing.md     # Testing guide
│   │   └── copilot/                  # Copilot guides
│   │       └── instructions.md       # GitHub Copilot instructions
│   ├── api/                          # API reference
│   ├── status/                       # Roadmap, backlog, changelogs
│   ├── adr/                          # Architecture Decision Records
│   ├── reference/                    # Reference materials
│   │   ├── data-dictionary.md        # Field definitions
│   │   └── data-uniformity.md        # Consistency guidelines
│   ├── diagrams/                     # Architecture diagrams
│   ├── uml/                          # UML diagrams
│   ├── USAGE.md                      # Detailed usage guide
│   ├── HELP.md                       # User guide with FAQ
│   └── DEPENDENCIES.md               # Package documentation
│
├── build/                            # All build tooling (consolidated)
│   ├── python/                       # Python build tooling
│   │   ├── cli/                      # Command-line tools (buildctl.py)
│   │   ├── adapters/                 # Build adapters
│   │   ├── analytics/                # Build analytics
│   │   ├── core/                     # Core utilities
│   │   ├── diagnostics/              # Diagnostic tools
│   │   └── knowledge/                # Error pattern catalogs
│   ├── scripts/                      # Shell scripts
│   │   ├── install/                  # Installation scripts
│   │   ├── publish/                  # Publishing scripts
│   │   ├── run/                      # Runtime scripts
│   │   ├── lib/                      # Script utilities
│   │   └── docs/                     # Documentation scripts
│   ├── node/                         # Node.js tooling
│   │   ├── generate-diagrams.mjs     # Diagram generation
│   │   └── generate-icons.mjs        # Icon generation
│   └── dotnet/                       # .NET tools
│       ├── DocGenerator/             # Documentation generator
│       └── FSharpInteropGenerator/   # F# interop generator
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
│   │   │   ├── Collectors/           # Data collectors (5 files)
│   │   │   ├── Events/               # Domain events (7 files)
│   │   │   └── Models/               # Domain models (21 files)
│   │   ├── Infrastructure/           # Provider implementations
│   │   │   ├── Contracts/            # Core interfaces
│   │   │   ├── Providers/            # Data providers
│   │   │   │   ├── Core/             # Provider infrastructure
│   │   │   │   ├── Streaming/        # Real-time streaming providers
│   │   │   │   │   ├── Alpaca/       # Alpaca Markets
│   │   │   │   │   ├── InteractiveBrokers/ # IB Gateway
│   │   │   │   │   ├── NYSE/         # NYSE data
│   │   │   │   │   ├── Polygon/      # Polygon.io
│   │   │   │   │   └── StockSharp/   # StockSharp (90+ sources)
│   │   │   │   ├── Historical/       # Historical data providers
│   │   │   │   │   ├── Alpaca/       # Alpaca historical
│   │   │   │   │   ├── AlphaVantage/ # Alpha Vantage
│   │   │   │   │   ├── Finnhub/      # Finnhub
│   │   │   │   │   ├── InteractiveBrokers/ # IB historical
│   │   │   │   │   ├── NasdaqDataLink/    # Nasdaq Data Link
│   │   │   │   │   ├── Polygon/      # Polygon historical
│   │   │   │   │   ├── StockSharp/   # StockSharp historical
│   │   │   │   │   ├── Stooq/        # Stooq
│   │   │   │   │   ├── Tiingo/       # Tiingo
│   │   │   │   │   ├── YahooFinance/ # Yahoo Finance
│   │   │   │   │   ├── RateLimiting/ # Rate limit tracking
│   │   │   │   │   ├── Queue/        # Backfill job queue
│   │   │   │   │   ├── GapAnalysis/  # Gap detection/repair
│   │   │   │   │   └── SymbolResolution/ # Symbol resolvers
│   │   │   │   ├── SymbolSearch/     # Symbol search providers
│   │   │   │   └── MultiProvider/    # Multi-provider routing
│   │   │   ├── DataSources/          # Data source abstractions
│   │   │   ├── Resilience/           # WebSocket resilience (Polly)
│   │   │   └── IMarketDataClient.cs  # Core streaming interface
│   │   ├── Storage/                  # Data persistence (~35 files)
│   │   │   ├── Sinks/                # JSONL/Parquet writers
│   │   │   ├── Archival/             # Archive management, WAL
│   │   │   ├── Export/               # Data export, quality reports
│   │   │   ├── Maintenance/          # Scheduled archive maintenance
│   │   │   ├── Packaging/            # Portable data packages
│   │   │   ├── Replay/               # Data replay, memory-mapped readers
│   │   │   ├── Policies/             # Retention policies
│   │   │   └── Services/             # Storage services
│   │   ├── Application/              # Startup, config, services (~90 files)
│   │   │   ├── Backfill/             # Backfill service, requests, results
│   │   │   ├── Config/               # Configuration management
│   │   │   │   └── Credentials/      # Credential providers, OAuth
│   │   │   ├── Exceptions/           # Custom exception types
│   │   │   ├── Filters/              # Event filtering
│   │   │   ├── Indicators/           # Technical indicators
│   │   │   ├── Logging/              # Structured logging setup
│   │   │   ├── Monitoring/           # Metrics, health checks
│   │   │   │   └── DataQuality/      # Quality monitoring (~12 files)
│   │   │   ├── Pipeline/             # Event pipeline
│   │   │   ├── Results/              # Result<T, TError> types
│   │   │   ├── Scheduling/           # Backfill scheduling (cron)
│   │   │   ├── Serialization/        # JSON serialization
│   │   │   ├── Services/             # Application services (~25 files)
│   │   │   ├── Subscriptions/        # Symbol subscription management
│   │   │   │   ├── Models/           # Watchlists, portfolios
│   │   │   │   └── Services/         # Subscription services
│   │   │   └── Http/                 # HTTP endpoints
│   │   │       └── Endpoints/        # All HTTP endpoint handlers
│   │   ├── Integrations/             # External integrations
│   │   │   └── Lean/                 # QuantConnect Lean
│   │   └── Tools/                    # Utility tools
│   ├── MarketDataCollector.FSharp/   # F# domain models (12 files)
│   │   ├── Domain/                   # F# domain types
│   │   ├── Validation/               # Railway-oriented validation
│   │   ├── Calculations/             # Spread, imbalance, VWAP
│   │   └── Pipeline/                 # Data transforms
│   ├── MarketDataCollector.Contracts/# Shared DTOs, contracts
│   │   ├── Api/                      # HTTP API contracts
│   │   ├── Domain/                   # Shared domain contracts
│   │   └── Configuration/            # Configuration schema
│   ├── MarketDataCollector.Ui/       # Web dashboard (10 files)
│   │   ├── Endpoints/                # HTTP endpoints
│   │   └── wwwroot/                  # Static assets
│   ├── MarketDataCollector.Ui.Shared/ # Shared UI services & endpoints
│   │   ├── Endpoints/                # Consolidated HTTP endpoints
│   │   └── Services/                 # Shared UI services
│   └── MarketDataCollector.Uwp/      # Windows desktop app (WinUI 3)
│       ├── Views/                    # XAML UI pages
│       ├── ViewModels/               # MVVM view models
│       ├── Services/                 # Windows services
│       ├── Models/                   # UWP-specific models + SharedModelAliases.cs
│       └── SharedModels/             # Linked source files from Contracts (compile-time)
│
├── tests/                            # Test projects (mirrors src structure)
│   ├── MarketDataCollector.Tests/    # C# unit tests
│   │   ├── Application/              # Application layer tests
│   │   │   ├── Backfill/             # Backfill tests
│   │   │   ├── Config/               # Config tests
│   │   │   ├── Credentials/          # Credential tests
│   │   │   ├── Indicators/           # Indicator tests
│   │   │   ├── Monitoring/           # Monitoring tests
│   │   │   │   └── DataQuality/      # Data quality tests
│   │   │   ├── Pipeline/             # Pipeline tests
│   │   │   └── Services/             # Service tests
│   │   ├── Domain/                   # Domain layer tests
│   │   │   ├── Collectors/           # Collector tests
│   │   │   └── Models/               # Model tests
│   │   ├── Infrastructure/           # Infrastructure tests
│   │   │   ├── Providers/            # Provider tests
│   │   │   ├── Resilience/           # Resilience tests
│   │   │   └── Shared/               # Shared utilities
│   │   ├── Integration/              # Integration tests
│   │   ├── Serialization/            # Serialization tests
│   │   ├── Storage/                  # Storage tests
│   │   └── SymbolSearch/             # Symbol search tests
│   └── MarketDataCollector.FSharp.Tests/ # F# tests (5 files)
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

## Data Providers

### Streaming Providers (IMarketDataClient)

| Provider | Class | Trades | Quotes | Depth | Features |
|----------|-------|--------|--------|-------|----------|
| Alpaca | `AlpacaMarketDataClient` | Yes | Yes | No | WebSocket streaming |
| Polygon | `PolygonMarketDataClient` | Yes | Yes | Yes | Circuit breaker, retry |
| Interactive Brokers | `IBMarketDataClient` | Yes | Yes | Yes | TWS/Gateway, conditional |
| StockSharp | `StockSharpMarketDataClient` | Yes | Yes | Yes | 90+ data sources |
| NYSE | `NYSEDataSource` | Yes | Yes | L1/L2 | Hybrid streaming + historical |
| NoOp | `NoOpMarketDataClient` | - | - | - | Placeholder |

### Historical Providers (IHistoricalDataProvider)

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
| Interactive Brokers | Yes (with account) | All types | IB pacing rules |

**CompositeHistoricalDataProvider** provides automatic multi-provider routing with:
- Priority-based fallback chain
- Rate limit tracking
- Provider health monitoring
- Symbol resolution across providers

### Symbol Search Providers (ISymbolSearchProvider)

| Provider | Class | Exchanges | Rate Limit |
|----------|-------|-----------|------------|
| Alpaca | `AlpacaSymbolSearchProviderRefactored` | US, Crypto | 200/min |
| Finnhub | `FinnhubSymbolSearchProviderRefactored` | US, International | 60/min |
| Polygon | `PolygonSymbolSearchProvider` | US | 5/min (free) |
| OpenFIGI | `OpenFigiClient` | Global (ID mapping) | - |

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

## HTTP API Reference

The application exposes a comprehensive REST API when running with `--ui` or `--mode web`.

### Core Endpoints
| Endpoint | Method | Purpose |
|----------|--------|---------|
| `/` | GET | HTML dashboard (auto-refreshing) |
| `/api/status` | GET | Full status with metrics |
| `/api/health` | GET | Comprehensive health status |
| `/healthz`, `/readyz`, `/livez` | GET | Kubernetes health probes |
| `/api/metrics` | GET | Prometheus metrics |
| `/api/errors` | GET | Error log with filtering |
| `/api/backpressure` | GET | Backpressure status |

### Configuration API (`/api/config/`)
| Endpoint | Method | Purpose |
|----------|--------|---------|
| `/api/config` | GET | Full configuration |
| `/api/config/data-source` | POST | Update active data source |
| `/api/config/symbols` | POST | Add/update symbol |
| `/api/config/symbols/{symbol}` | DELETE | Remove symbol |
| `/api/config/data-sources` | GET/POST | Manage data sources |
| `/api/config/data-sources/{id}/toggle` | POST | Toggle source enabled |

### Provider API (`/api/providers/`)
| Endpoint | Method | Purpose |
|----------|--------|---------|
| `/api/providers/status` | GET | All provider status |
| `/api/providers/metrics` | GET | Provider metrics |
| `/api/providers/latency` | GET | Latency metrics |
| `/api/providers/catalog` | GET | Provider catalog with metadata |
| `/api/providers/comparison` | GET | Feature comparison |
| `/api/connections` | GET | Connection health |

### Failover API (`/api/failover/`)
| Endpoint | Method | Purpose |
|----------|--------|---------|
| `/api/failover/config` | GET/POST | Failover configuration |
| `/api/failover/rules` | GET/POST | Failover rules |
| `/api/failover/health` | GET | Provider health status |
| `/api/failover/force/{ruleId}` | POST | Force failover |

### Backfill API (`/api/backfill/`)
| Endpoint | Method | Purpose |
|----------|--------|---------|
| `/api/backfill/providers` | GET | Available providers |
| `/api/backfill/status` | GET | Last backfill status |
| `/api/backfill/run` | POST | Execute backfill |
| `/api/backfill/run/preview` | POST | Preview backfill |
| `/api/backfill/schedules` | GET/POST | Manage schedules |
| `/api/backfill/schedules/{id}/trigger` | POST | Trigger schedule |
| `/api/backfill/executions` | GET | Execution history |
| `/api/backfill/gap-fill` | POST | Immediate gap fill |
| `/api/backfill/statistics` | GET | Backfill statistics |

### Data Quality API (`/api/quality/`)
| Endpoint | Method | Purpose |
|----------|--------|---------|
| `/api/quality/dashboard` | GET | Quality dashboard |
| `/api/quality/metrics` | GET | Real-time metrics |
| `/api/quality/completeness` | GET | Completeness scores |
| `/api/quality/gaps` | GET | Gap analysis |
| `/api/quality/gaps/{symbol}` | GET | Symbol gaps |
| `/api/quality/errors` | GET | Sequence errors |
| `/api/quality/anomalies` | GET | Detected anomalies |
| `/api/quality/latency` | GET | Latency distributions |
| `/api/quality/comparison/{symbol}` | GET | Cross-provider comparison |
| `/api/quality/health` | GET | Quality health status |
| `/api/quality/reports/daily` | GET | Daily quality report |

### SLA Monitoring API (`/api/sla/`)
| Endpoint | Method | Purpose |
|----------|--------|---------|
| `/api/sla/status` | GET | SLA compliance status |
| `/api/sla/violations` | GET | SLA violations |
| `/api/sla/health` | GET | SLA health |
| `/api/sla/metrics` | GET | SLA metrics |

### Maintenance API (`/api/maintenance/`)
| Endpoint | Method | Purpose |
|----------|--------|---------|
| `/api/maintenance/schedules` | GET/POST | Manage schedules |
| `/api/maintenance/schedules/{id}/trigger` | POST | Trigger maintenance |
| `/api/maintenance/executions` | GET | Execution history |
| `/api/maintenance/execute` | POST | Immediate execution |
| `/api/maintenance/task-types` | GET | Available task types |

### Packaging API (`/api/packaging/`)
| Endpoint | Method | Purpose |
|----------|--------|---------|
| `/api/packaging/create` | POST | Create package |
| `/api/packaging/import` | POST | Import package |
| `/api/packaging/validate` | POST | Validate package |
| `/api/packaging/list` | GET | List packages |
| `/api/packaging/download/{fileName}` | GET | Download package |

---

## Data Quality Monitoring

The system includes comprehensive data quality monitoring in `Application/Monitoring/DataQuality/`:

### Quality Services
| Service | Purpose |
|---------|---------|
| `DataQualityMonitoringService` | Orchestrates all quality checks |
| `CompletenessScoreCalculator` | Calculates data completeness scores |
| `GapAnalyzer` | Detects and analyzes data gaps |
| `SequenceErrorTracker` | Tracks sequence/integrity errors |
| `AnomalyDetector` | Detects data anomalies |
| `LatencyHistogram` | Tracks latency distribution |
| `CrossProviderComparisonService` | Compares data across providers |
| `PriceContinuityChecker` | Checks price continuity |
| `DataFreshnessSlaMonitor` | Monitors data freshness SLA |
| `DataQualityReportGenerator` | Generates quality reports |

### Quality Metrics
- **Completeness Score** - Percentage of expected data received
- **Gap Analysis** - Missing data periods with duration
- **Sequence Errors** - Out-of-order or duplicate events
- **Anomaly Detection** - Unusual price/volume patterns
- **Latency Distribution** - End-to-end latency percentiles
- **Cross-Provider Comparison** - Data consistency across providers
- **SLA Compliance** - Data freshness within thresholds

---

## Application Services

### Core Services
| Service | Location | Purpose |
|---------|----------|---------|
| `ConfigurationService` | `Application/Config/` | Configuration loading with self-healing |
| `ConfigurationWizard` | `Application/Services/` | Interactive configuration setup |
| `AutoConfigurationService` | `Application/Services/` | Auto-config from environment |
| `PreflightChecker` | `Application/Services/` | Pre-startup validation |
| `GracefulShutdownService` | `Application/Services/` | Graceful shutdown coordination |
| `DryRunService` | `Application/Services/` | Dry-run validation mode |
| `DiagnosticBundleService` | `Application/Services/` | Comprehensive diagnostics |
| `TradingCalendar` | `Application/Services/` | Market hours and holidays |

### Monitoring Services
| Service | Location | Purpose |
|---------|----------|---------|
| `ConnectionHealthMonitor` | `Application/Monitoring/` | Provider connection health |
| `ProviderLatencyService` | `Application/Monitoring/` | Latency tracking |
| `SpreadMonitor` | `Application/Monitoring/` | Bid-ask spread monitoring |
| `BackpressureAlertService` | `Application/Monitoring/` | Backpressure alerts |
| `ErrorTracker` | `Application/Monitoring/` | Error categorization |
| `PrometheusMetrics` | `Application/Monitoring/` | Metrics export |

### Storage Services
| Service | Location | Purpose |
|---------|----------|---------|
| `WriteAheadLog` | `Storage/Archival/` | WAL for durability |
| `PortableDataPackager` | `Storage/Packaging/` | Data package creation |
| `TierMigrationService` | `Storage/Services/` | Hot/warm/cold tier migration |
| `ScheduledArchiveMaintenanceService` | `Storage/Maintenance/` | Scheduled maintenance |
| `HistoricalDataQueryService` | `Application/Services/` | Query stored data |

---

## Architecture Decision Records (ADRs)

ADRs document significant architectural decisions. Located in `docs/adr/`:

| ADR | Title | Key Points |
|-----|-------|------------|
| ADR-001 | Provider Abstraction | Interface contracts for data providers |
| ADR-002 | Tiered Storage | Hot/cold storage architecture |
| ADR-003 | Microservices Decomposition | Rejected in favor of monolith |
| ADR-004 | Async Streaming Patterns | CancellationToken, IAsyncEnumerable |
| ADR-005 | Attribute-Based Discovery | `[DataSource]`, `[ImplementsAdr]` attributes |
| ADR-010 | HttpClient Factory | HttpClientFactory lifecycle management |

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

### Test Organization (53 test files total)
| Directory | Purpose | Files |
|-----------|---------|-------|
| `tests/MarketDataCollector.Tests/Backfill/` | Backfill provider tests | 4 |
| `tests/MarketDataCollector.Tests/Credentials/` | Credential provider tests | 3 |
| `tests/MarketDataCollector.Tests/Indicators/` | Technical indicator tests | 1 |
| `tests/MarketDataCollector.Tests/Infrastructure/` | Infrastructure tests | 2 |
| `tests/MarketDataCollector.Tests/Integration/` | End-to-end tests | 1 |
| `tests/MarketDataCollector.Tests/Models/` | Domain model tests | 2 |
| `tests/MarketDataCollector.Tests/Monitoring/` | Monitoring/quality tests | 9 |
| `tests/MarketDataCollector.Tests/Pipeline/` | Event pipeline tests | 1 |
| `tests/MarketDataCollector.Tests/Providers/` | Provider-specific tests | 1 |
| `tests/MarketDataCollector.Tests/Serialization/` | JSON serialization tests | 1 |
| `tests/MarketDataCollector.Tests/Storage/` | Storage and archival tests | 4 |
| `tests/MarketDataCollector.Tests/SymbolSearch/` | Symbol resolution tests | 2 |
| `tests/MarketDataCollector.FSharp.Tests/` | F# domain tests | 5 |

### Benchmarks
Located in `benchmarks/MarketDataCollector.Benchmarks/` using BenchmarkDotNet.

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
export FINNHUB__TOKEN=your-token
export ALPHAVANTAGE__APIKEY=your-api-key
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
- `Storage` - File organization, retention, compression, tiers
- `Backfill` - Historical data settings, provider priority
- `DataQuality` - Quality monitoring thresholds
- `Sla` - Data freshness SLA configuration
- `Maintenance` - Archive maintenance schedules

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

#### Custom Exception Types (in `Application/Exceptions/`)
| Exception | Purpose |
|-----------|---------|
| `ConfigurationException` | Invalid configuration |
| `ConnectionException` | Connection failures |
| `DataProviderException` | Provider errors |
| `RateLimitException` | Rate limit exceeded |
| `SequenceValidationException` | Data sequence issues |
| `StorageException` | Storage/persistence errors |
| `ValidationException` | Data validation failures |
| `OperationTimeoutException` | Operation timeouts |

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
| `TradeDataCollector` | `Domain/Collectors/` | Tick-by-tick trade processing |
| `MarketDepthCollector` | `Domain/Collectors/` | L2 order book maintenance |
| `QuoteCollector` | `Domain/Collectors/` | BBO state tracking |
| `EventPipeline` | `Application/Pipeline/` | Bounded channel event routing |
| `JsonlStorageSink` | `Storage/Sinks/` | JSONL file persistence |
| `ParquetStorageSink` | `Storage/Sinks/` | Parquet file persistence |
| `AlpacaMarketDataClient` | `Infrastructure/Providers/Alpaca/` | Alpaca WebSocket client |
| `CompositeHistoricalDataProvider` | `Infrastructure/Providers/Backfill/` | Multi-provider backfill with fallback |
| `BackfillWorkerService` | `Infrastructure/Providers/Backfill/` | Background backfill service |
| `DataQualityMonitoringService` | `Application/Monitoring/DataQuality/` | Data quality monitoring |
| `GracefulShutdownService` | `Application/Services/` | Graceful shutdown handling |
| `ConfigurationWizard` | `Application/Services/` | Interactive configuration setup |
| `TechnicalIndicatorService` | `Application/Indicators/` | Technical indicators (via Skender) |
| `WriteAheadLog` | `Storage/Archival/` | WAL for data durability |
| `PortableDataPackager` | `Storage/Packaging/` | Data package creation |
| `TradingCalendar` | `Application/Services/` | Market hours and holidays |

*All locations relative to `src/MarketDataCollector/`*

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
├── _wal/                    # Write-ahead log
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

### Tiered Storage
| Tier | Purpose | Retention |
|------|---------|-----------|
| Hot | Recent data, fast access | 7 days default |
| Warm | Older data, compressed | 30 days default |
| Cold | Archive, maximum compression | Indefinite |

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

See `docs/guides/provider-implementation.md` for detailed patterns.

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

### Creating Data Packages
```bash
# Create a portable package
dotnet run --project src/MarketDataCollector -- \
  --package \
  --package-symbols SPY,AAPL \
  --package-from 2024-01-01 --package-to 2024-12-31 \
  --package-output ./packages \
  --package-name "2024-equities"

# Import a package
dotnet run --project src/MarketDataCollector -- \
  --import-package ./packages/2024-equities.zip \
  --merge
```

See `docs/guides/portable-data-packager.md` for details.

---

## CI/CD Pipelines

The project uses GitHub Actions with 21 workflows in `.github/workflows/`:

| Workflow | Purpose |
|----------|---------|
| `test-matrix.yml` | Multi-platform test matrix (Windows, Linux, macOS) |
| `code-quality.yml` | Code quality checks (formatting, analyzers) |
| `security.yml` | Security scanning (CodeQL, dependency audit) |
| `benchmark.yml` | Performance benchmarks |
| `docker.yml` | Docker image building and publishing |
| `dotnet-desktop.yml` | Desktop application builds |
| `desktop-app.yml` | UWP app builds |
| `documentation.yml` | Documentation generation |
| `docs-auto-update.yml` | Auto-update docs on changes |
| `docs-structure-sync.yml` | Sync documentation structure |
| `release.yml` | Release automation |
| `pr-checks.yml` | PR validation checks |
| `dependency-review.yml` | Dependency review |
| `labeling.yml` | PR auto-labeling |
| `nightly.yml` | Nightly builds |
| `scheduled-maintenance.yml` | Scheduled maintenance tasks |
| `stale.yml` | Stale issue management |
| `cache-management.yml` | Build cache management |
| `validate-workflows.yml` | Workflow validation |
| `build-observability.yml` | Build metrics collection |
| `reusable-dotnet-build.yml` | Reusable .NET build workflow |

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

## UWP Desktop Application Architecture

The UWP desktop application (`MarketDataCollector.Uwp`) uses **WinUI 3** and has a special architecture requirement:

### Shared Source Files (Not Assembly Reference)

The WinUI 3 XAML compiler rejects assemblies without WinRT metadata with the error:
> "Assembly is not allowed in type universe"

This prevents using a standard `<ProjectReference>` to `MarketDataCollector.Contracts`.

**Solution:** Include Contracts source files directly during compilation:

```xml
<!-- In MarketDataCollector.Uwp.csproj -->
<ItemGroup Condition="'$(IsWindows)' == 'true'">
  <Compile Include="..\MarketDataCollector.Contracts\Configuration\*.cs"
           Link="SharedModels\Configuration\%(Filename)%(Extension)" />
  <!-- Similar for Api, Credentials, Backfill, Session, etc. -->
</ItemGroup>
```

**Key Files:**
- `Models/SharedModelAliases.cs` - Global using directives and type aliases for backwards compatibility
- `Models/AppConfig.cs` - UWP-specific types only (e.g., `KeyboardShortcut`)
- `SharedModels/` - Virtual folder containing linked source files from Contracts

**Benefits:**
- Eliminates ~1,300 lines of duplicated DTOs
- Single source of truth in Contracts project
- Type aliases maintain backwards compatibility (`AppConfig` → `AppConfigDto`)

See `docs/guides/uwp-development-roadmap.md` for development status.

---

## Documentation

### Core Documentation
| File | Purpose |
|------|---------|
| `docs/guides/getting-started.md` | Setup and first run |
| `docs/guides/configuration.md` | All configuration options |
| `docs/guides/operator-runbook.md` | Production operations |
| `docs/guides/troubleshooting.md` | Common issues and solutions |
| `docs/guides/provider-implementation.md` | Adding new providers |
| `docs/guides/portable-data-packager.md` | Data packaging guide |
| `docs/USAGE.md` | Detailed usage guide |
| `docs/HELP.md` | User guide with FAQ |

### Architecture Documentation
| File | Purpose |
|------|---------|
| `docs/architecture/overview.md` | System architecture |
| `docs/architecture/domains.md` | Event contracts |
| `docs/architecture/storage-design.md` | Storage organization |
| `docs/architecture/why-this-architecture.md` | Design rationale |
| `docs/adr/` | Architecture Decision Records |

### Provider Documentation
| File | Purpose |
|------|---------|
| `docs/providers/backfill-guide.md` | Historical data guide |
| `docs/providers/data-sources.md` | Available data sources |
| `docs/providers/provider-comparison.md` | Feature comparison |

### AI Assistant Guides
| File | Purpose |
|------|---------|
| `docs/ai-assistants/CLAUDE.providers.md` | Provider implementation |
| `docs/ai-assistants/CLAUDE.storage.md` | Storage system |
| `docs/ai-assistants/CLAUDE.fsharp.md` | F# domain library |
| `docs/ai-assistants/CLAUDE.testing.md` | Testing guide |
| `.github/agents/documentation-agent.md` | Documentation maintenance |

### Reference Materials
| File | Purpose |
|------|---------|
| `docs/reference/data-dictionary.md` | Field definitions |
| `docs/reference/data-uniformity.md` | Consistency guidelines |
| `docs/DEPENDENCIES.md` | Package documentation |

---

## Troubleshooting

### Build Issues
```bash
# Run build diagnostics
make diagnose

# Or call the buildctl CLI directly
python3 build-system/cli/buildctl.py build --project src/MarketDataCollector/MarketDataCollector.csproj --configuration Release

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

See `docs/guides/troubleshooting.md` for detailed solutions.

---

## Related Resources

- [README.md](README.md) - Project overview
- [docs/USAGE.md](docs/USAGE.md) - Detailed usage guide
- [docs/HELP.md](docs/HELP.md) - User guide with FAQ
- [docs/DEPENDENCIES.md](docs/DEPENDENCIES.md) - Package documentation
- [docs/adr/](docs/adr/) - Architecture Decision Records
- [docs/ai-assistants/](docs/ai-assistants/) - Specialized AI guides
- [docs/guides/troubleshooting.md](docs/guides/troubleshooting.md) - Troubleshooting guide
- [docs/providers/provider-comparison.md](docs/providers/provider-comparison.md) - Provider comparison
- [.github/copilot-instructions.md](.github/copilot-instructions.md) - Copilot instructions
- [.github/agents/documentation-agent.md](.github/agents/documentation-agent.md) - Documentation agent

---

*Last Updated: 2026-02-01*
