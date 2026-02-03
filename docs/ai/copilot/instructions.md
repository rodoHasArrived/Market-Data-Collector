# Market Data Collector - Copilot Instructions

> **Note:** For comprehensive project context, see [CLAUDE.md](../CLAUDE.md) in the repository root.

## Repository Overview

**Market Data Collector** is a high-performance, cross-platform market data collection system for real-time and historical market microstructure data. It's a production-ready .NET 9.0 solution with F# domain libraries, supporting multiple data providers (Interactive Brokers, Alpaca, NYSE, Polygon, StockSharp) and offering flexible storage options.

**Project Type:** .NET Solution (C# and F#)
**Target Framework:** .NET 9.0
**Languages:** C# 11, F# 8.0
**Size:** 478 source files (466 C#, 12 F#) across 6 main projects
**Architecture:** Event-driven, monolithic core with optional UI projects

## Build & Test Commands

### Prerequisites
- .NET SDK 9.0 or later (SDK 10.0.101 confirmed working)
- Docker and Docker Compose (optional, for containerized deployment)

### Key Build Commands

**IMPORTANT:** Always use `/p:EnableWindowsTargeting=true` flag on non-Windows systems to avoid NETSDK1100 errors.

```bash
# Navigate to project root
cd MarketDataCollector

# Restore dependencies (ALWAYS run first)
dotnet restore /p:EnableWindowsTargeting=true

# Build
dotnet build -c Release --no-restore /p:EnableWindowsTargeting=true

# Run tests
dotnet test tests/MarketDataCollector.Tests/MarketDataCollector.Tests.csproj -c Release --verbosity normal /p:EnableWindowsTargeting=true

# Clean build artifacts
dotnet clean
rm -rf bin/ obj/ publish/
```

### Test Framework
- **Framework:** xUnit
- **Test Projects:**
  - `tests/MarketDataCollector.Tests/` (C#)
  - `tests/MarketDataCollector.FSharp.Tests/` (F#)
- **Mocking:** Moq, NSubstitute, MassTransit.TestFramework
- **Assertions:** FluentAssertions

### Running the Application

```bash
# Basic run (smoke test with no provider)
dotnet run --project src/MarketDataCollector/MarketDataCollector.csproj

# Run with web dashboard
dotnet run --project src/MarketDataCollector/MarketDataCollector.csproj -- --ui --http-port 8080

# Run with config hot reload
dotnet run --project src/MarketDataCollector/MarketDataCollector.csproj -- --ui --watch-config

# Run self-tests
dotnet run --project src/MarketDataCollector/MarketDataCollector.csproj -- --selftest

# Historical backfill
dotnet run --project src/MarketDataCollector/MarketDataCollector.csproj -- --backfill --backfill-provider stooq --backfill-symbols SPY,AAPL
```

### Make Commands (Alternative)

The repository includes a comprehensive Makefile for common tasks:

```bash
make help           # Show all available commands
make install        # Interactive installation
make build          # Build the project
make test           # Run tests
make run-ui         # Run with web dashboard
make docker         # Build and start Docker container
make clean          # Clean build artifacts
```

## Project Structure

### Solution Layout

```
MarketDataCollector/
├── src/
│   ├── MarketDataCollector/              # Main console application (C#)
│   ├── MarketDataCollector.Ui/           # Web dashboard UI (C#)
│   ├── MarketDataCollector.Ui.Shared/    # Shared UI services & endpoints
│   ├── MarketDataCollector.Uwp/          # Windows UWP desktop app (WinUI 3)
│   ├── MarketDataCollector.Contracts/    # Shared contracts and DTOs
│   └── MarketDataCollector.FSharp/       # F# domain library (12 files)
├── tests/
│   ├── MarketDataCollector.Tests/        # C# unit tests (50 files)
│   └── MarketDataCollector.FSharp.Tests/ # F# unit tests
├── benchmarks/
│   └── MarketDataCollector.Benchmarks/   # BenchmarkDotNet performance tests
├── docs/                                 # Comprehensive documentation (61 files)
├── scripts/                              # Build and diagnostic scripts
├── build-system/                         # Python build tooling
└── deploy/                               # Deployment configurations
```

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

### Key Configuration Files

- **`Directory.Build.props`** - Root MSBuild properties, enables `EnableWindowsTargeting=true` for cross-platform builds
- **`appsettings.sample.json`** - Sample configuration (copy to `appsettings.json`)
- **`appsettings.json`** - Actual configuration (gitignored, contains credentials)
- **`MarketDataCollector.sln`** - Solution file for all projects
- **`Makefile`** - Common development tasks
- **`docker-compose.yml`** - Docker deployment configuration

### Main Application Architecture

The main application (`src/MarketDataCollector/`) follows a layered architecture:

- **Infrastructure Layer:** Provider-specific code (IB, Alpaca, NYSE, Polygon, StockSharp clients)
- **Domain Layer:** Core collectors (TradeDataCollector, MarketDepthCollector, QuoteCollector)
- **Event Pipeline Layer:** Bounded channel event processing, CompositePublisher
- **Storage Layer:** JSONL, Parquet, tiered storage, WAL (Write-Ahead Logging)
- **Application Layer:** Program.cs, ConfigWatcher, StatusHttpServer, BackfillService

**Key Interfaces:**
- `IMarketDataClient` - Core streaming interface for real-time data
- `IHistoricalDataProvider` - Historical/backfill data interface

### Important Directories

- **`src/MarketDataCollector/Infrastructure/Providers/`** - Data provider implementations
- **`src/MarketDataCollector/Domain/`** - Core domain models
- **`src/MarketDataCollector/Storage/`** - Storage implementations
- **`src/MarketDataCollector/Application/`** - Application services

## CI/CD Workflow

**GitHub Actions:** 21 workflows in `.github/workflows/`

Key workflows include:
- `test-matrix.yml` - Multi-platform test matrix
- `pr-checks.yml` - PR validation checks
- `security.yml` - Security scanning (CodeQL, Trivy)
- `docker.yml` - Docker image building
- `release.yml` - Release automation

The main CI pipeline runs on pushes to `main` and pull requests:

1. **Build Job** (ubuntu-latest):
   - Checkout code
   - Setup .NET 9.0.x
   - `dotnet restore /p:EnableWindowsTargeting=true`
   - `dotnet build -c Release --no-restore /p:EnableWindowsTargeting=true`
   - `dotnet test -c Release --no-build --verbosity normal /p:EnableWindowsTargeting=true`

2. **Publish Jobs** (multi-platform):
   - Linux x64, Windows x64, macOS x64, macOS ARM64
   - Publishes both `MarketDataCollector` and `MarketDataCollector.Ui` as single-file executables
   - Creates archives (.tar.gz for Unix, .zip for Windows)

3. **Release Job** (on git tags starting with 'v'):
   - Downloads all platform artifacts
   - Creates GitHub release with all platform builds

## Development Practices

### Configuration Management

- **NEVER commit credentials:** `appsettings.json` is gitignored
- **Use environment variables for secrets:** `ALPACA_KEY_ID`, `ALPACA_SECRET_KEY`, etc.
- **Copy sample config:** Always start with `cp appsettings.sample.json appsettings.json`

### Logging

- **Framework:** Serilog with structured logging
- **Initialization:** Use `LoggingSetup.ForContext<T>()` for logger instances
- **Configuration:** Defined in `appsettings.json` under `Serilog` section

### Testing Best Practices

- Use xUnit for test framework
- Use FluentAssertions for readable assertions
- Use Moq or NSubstitute for mocking
- Test files follow naming convention: `<ClassUnderTest>Tests.cs`

### Code Style

- C# 11 with nullable reference types enabled
- Implicit usings enabled
- Follow existing conventions in the codebase
- Use `async`/`await` for I/O operations
- Prefer dependency injection over static classes

## Common Issues & Workarounds

### Issue: Build fails with NETSDK1100 error on Linux/macOS
**Solution:** Always use `/p:EnableWindowsTargeting=true` flag (already set in `Directory.Build.props`)

### Issue: appsettings.json not found
**Solution:** Copy `appsettings.sample.json` to `appsettings.json` and configure

### Issue: Data or logs directories don't exist
**Solution:** Run `mkdir -p data logs` or use `make setup-config`

### Issue: Docker build fails
**Solution:** Ensure `appsettings.json` exists before building: `cp appsettings.sample.json appsettings.json`

### Issue: Tests fail due to missing configuration
**Solution:** Tests should mock configuration or use in-memory configuration. Check test setup.

## Important Files Reference

### Root Directory Files
- `README.md` - Main project documentation
- `HELP.md` - Comprehensive user guide (38KB)
- `DEPENDENCIES.md` - Complete NuGet package documentation
- `Makefile` - Development commands
- `Dockerfile` - Container build definition
- `docker-compose.yml` - Multi-container orchestration
- `install.sh` / `install.ps1` - Installation scripts
- `publish.sh` / `publish.ps1` - Publishing scripts

### Documentation
- `docs/guides/getting-started.md` - Setup guide
- `docs/guides/configuration.md` - Configuration reference
- `docs/architecture/overview.md` - System architecture (detailed)
- `docs/guides/operator-runbook.md` - Operations guide
- `docs/status/improvements.md` - Implementation status and roadmap

### Scripts
- `scripts/diagnose-build.sh` - Build diagnostics with verbose logging
- `scripts/validate-data.sh` - Data validation scripts

## Trust These Instructions

These instructions are comprehensive and accurate as of the last documentation date. Only search the codebase if:
- You need to understand implementation details not covered here
- Information appears outdated or contradictory
- You need to verify behavior of a specific component

When in doubt, refer to the extensive documentation in the `docs/` directory, particularly:
- Architecture diagrams: `docs/architecture/`
- Configuration details: `docs/guides/configuration.md`
- Troubleshooting: `HELP.md`

## Quick Decision Tree

**Adding new functionality?**
→ Add to appropriate layer in `src/MarketDataCollector/`, follow existing patterns

**Fixing a bug?**
→ Add test first in `tests/MarketDataCollector.Tests/`, then fix

**Working with providers?**
→ Look in `src/MarketDataCollector/Infrastructure/Providers/`

**Storage changes?**
→ Check `src/MarketDataCollector/Storage/`

**Need to run tests?**
→ `dotnet test tests/MarketDataCollector.Tests/` (C# tests only)

**Need to build?**
→ `dotnet restore /p:EnableWindowsTargeting=true` then `dotnet build -c Release /p:EnableWindowsTargeting=true`

**Starting the app?**
→ `dotnet run --project src/MarketDataCollector/MarketDataCollector.csproj -- --ui` for web dashboard
