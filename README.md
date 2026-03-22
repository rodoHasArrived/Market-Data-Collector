# Meridian

A high-performance, self-hosted algorithmic trading platform — **collect**, **backtest**, and **execute** strategies over real-time and historical market data.

[![.NET](https://img.shields.io/badge/.NET-9.0-blue)](https://dotnet.microsoft.com/)
[![C#](https://img.shields.io/badge/C%23-13-blue)](https://docs.microsoft.com/en-us/dotnet/csharp/)
[![F#](https://img.shields.io/badge/F%23-8.0-blue)](https://fsharp.org/)
[![Docker](https://img.shields.io/badge/Docker-Ready-blue)](https://www.docker.com/)
[![License](https://img.shields.io/badge/license-See%20LICENSE-green)](LICENSE)

[![Build and Release](https://github.com/rodoHasArrived/Meridian/actions/workflows/dotnet-desktop.yml/badge.svg)](https://github.com/rodoHasArrived/Meridian/actions/workflows/dotnet-desktop.yml)
[![Security](https://github.com/rodoHasArrived/Meridian/actions/workflows/security.yml/badge.svg)](https://github.com/rodoHasArrived/Meridian/actions/workflows/security.yml)
[![Docker Build](https://github.com/rodoHasArrived/Meridian/actions/workflows/docker.yml/badge.svg)](https://github.com/rodoHasArrived/Meridian/actions/workflows/docker.yml)
[![Code Quality](https://github.com/rodoHasArrived/Meridian/actions/workflows/code-quality.yml/badge.svg)](https://github.com/rodoHasArrived/Meridian/actions/workflows/code-quality.yml)
[![Copilot SWE Agent](https://github.com/rodoHasArrived/Meridian/actions/workflows/copilot-swe-agent-copilot.yml/badge.svg)](https://github.com/rodoHasArrived/Meridian/actions/workflows/copilot-swe-agent-copilot.yml)

**Status**: Development / Pilot Ready (trading workstation migration planning active)

> **Migration direction:** Meridian is actively migrating toward a workflow-centric **Trading Workstation** experience that unifies research, backtesting, paper trading, portfolio analysis, and ledger auditability. See [docs/plans/trading-workstation-migration-blueprint.md](docs/plans/trading-workstation-migration-blueprint.md).

---

## What This Project Does

Meridian is a self-hosted, multi-pillar algorithmic trading platform. It provides everything needed to collect market data at scale, develop and validate trading strategies through backtesting, execute strategies with zero financial risk using paper trading, and manage the complete strategy lifecycle from research to production deployment. All pillars—data collection, backtesting, execution, and strategy management—are built on a unified, event-driven architecture and fully integrated.

### Platform Pillars

| Pillar | Projects | Status |
|--------|----------|--------|
| **📡 Data Collection** | `Meridian.*` core projects | ✅ Production-ready — 90+ streaming and 10+ backfill providers |
| **🔬 Backtesting** | `Meridian.Backtesting`, `.Backtesting.Sdk` | ✅ Production-ready — tick-level engine with fill models and metrics |
| **⚡ Execution** | `Meridian.Execution`, `.Execution.Sdk` | ✅ Paper trading gateway active — ready for live integration |
| **🗂️ Strategies** | `Meridian.Strategies` | ✅ Lifecycle management — register, backtest, paper-trade, promote to live |

### Core Capabilities

| Capability | What It Does For You |
|------------|---------------------|
| **📡 Real-Time Streaming** | Capture live trades, quotes, and order book depth as they happen from Interactive Brokers, Alpaca, NYSE, Polygon, or StockSharp |
| **📥 Historical Backfill** | Download years of historical price data from 10+ providers (Yahoo Finance, Tiingo, Polygon, Alpaca, and more) with automatic failover |
| **💾 Local Data Storage** | Own your data — everything is stored in structured JSONL or Parquet files on your machine, not locked in a vendor's cloud |
| **🔍 Data Quality Monitoring** | Automatic validation catches missing data, sequence gaps, and anomalies before they corrupt your analysis |
| **📦 Data Packaging** | Export and package your data for sharing, backup, or use in other tools |
| **📊 Live Dashboards** | Monitor collection status, throughput, and data quality through a web dashboard or Windows desktop app |
| **🔬 Backtesting Engine** | Replay tick-level historical data through strategy plugins; compute Sharpe, drawdown, XIRR, and full fill tape |
| **📒 Portfolio Ledger** | Record cash, positions, commissions, financing, and realized P&L through double-entry accounting for audit and analysis |
| **⚡ Paper Trading** | Run strategies against a live feed with zero financial risk using `PaperTradingGateway` (ADR-015); execution UX is being migrated into a dedicated trading cockpit |
| **🗂️ Strategy Lifecycle** | Register, start, pause, stop, and archive strategy runs with full audit trail and promotion workflow |

### Who Is This For?

- **Quantitative researchers** who need tick-level market microstructure data for analysis
- **Algorithmic traders** building and validating strategies before committing real capital
- **Data engineers** who want to build a reliable market data pipeline
- **Hobbyist traders** who want to collect, backtest, and paper-trade their own strategies
- **Students and academics** studying market microstructure, price formation, or trading systems

### The Problem It Solves

Commercial market data is expensive, vendor APIs change without notice, and cloud-only
solutions mean you never truly own your data or your strategy infrastructure. Meridian gives you:

1. **Data independence** — Switch providers without losing your archive or rewriting code
2. **Cost control** — Use free-tier APIs strategically, pay only for premium data you actually need
3. **Reliability** — Automatic reconnection, failover between providers, and data integrity checks
4. **Flexibility** — Collect exactly the symbols and data types you need, store them how you want
5. **Paper-first safety** — Validate every strategy in simulation before any real capital is committed

---

## Installation

### Golden Path (Recommended)

Use the installation orchestrator script for all setups. It keeps Docker and native installs consistent across platforms.

```bash
# Interactive installer (Docker or Native)
./build/scripts/install/install.sh

# Or choose a mode explicitly
./build/scripts/install/install.sh --docker
./build/scripts/install/install.sh --native
```

Access the dashboard at **http://localhost:8080**

### Windows Installation

The PowerShell installer mirrors the same workflow on Windows.

```powershell
# Interactive installation
.\build\scripts\install\install.ps1

# Or specify mode directly
.\build\scripts\install\install.ps1 -Mode Docker
.\build\scripts\install\install.ps1 -Mode Native
```

### Optional Make Wrappers

Make targets remain available as thin wrappers around the installer and runtime commands.

```bash
make help
make docker
make run-ui
make test
make doctor
```

Desktop-focused helpers:

```bash
make desktop-dev-bootstrap   # Validate desktop development environment (PowerShell)
make build-wpf               # Build WPF desktop app (Windows only)
make test-desktop-services   # Run desktop-focused tests (includes WPF service tests on Windows)
```

### Windows Desktop App Install

**WPF Desktop App (Recommended)** - Modern WPF application with maximum Windows stability
- Works on Windows 7+
- Simple .exe deployment
- Direct assembly references (no WinRT limitations)
- See [WPF README](src/Meridian.Wpf/README.md) for details

**Installation:**
```bash
# Build from source
dotnet build src/Meridian.Wpf/Meridian.Wpf.csproj -c Release

# Run
dotnet run --project src/Meridian.Wpf/Meridian.Wpf.csproj
```

---

## Desktop Development

### Setting Up Desktop Development Environment

For contributors working on the WPF desktop application, use the desktop development bootstrap script to validate your environment:

```bash
make desktop-dev-bootstrap
```

This script validates:
- ✅ .NET 9 SDK installation
- ✅ Windows SDK presence (Windows only)
- ✅ Visual Studio Build Tools
- ✅ XAML tooling support
- ✅ Desktop project restore and smoke build

The script provides actionable fix messages for any missing components.

### Desktop Testing

The repository includes comprehensive tests for desktop services:

```bash
# Run all desktop-focused tests (platform-aware)
make test-desktop-services

# On Windows, this runs:
# - WPF-specific tests (binding/navigation/desktop host wiring)
# - Shared desktop UI service tests
#
# On non-Windows platforms, use Meridian.Tests for startup/composition/contracts/endpoint-shape coverage
```

**Test Projects:**
- `tests/Meridian.Tests` - cross-platform startup, composition, contract, endpoint-shape, and core/backend tests
- `tests/Meridian.Wpf.Tests` - Windows-only WPF-specific binding, navigation, and desktop host wiring
  - NavigationServiceTests (14 tests)
  - ConfigServiceTests (13 tests)
  - StatusServiceTests (13 tests)
  - ConnectionServiceTests (18 tests)
- `tests/Meridian.Ui.Tests` - shared desktop UI service logic under the existing Windows target
  - ApiClientServiceTests, BackfillServiceTests, FixtureDataServiceTests
  - FormValidationServiceTests, SystemHealthServiceTests, WatchlistServiceTests
  - BoundedObservableCollectionTests, CircularBufferTests

### Desktop Build Commands

```bash
# Build WPF application (Windows only)
make build-wpf
```

---

**Troubleshooting install**
- **Missing signing certificate**: Install the signing cert or use a release-signed package; unsigned MSIX packages cannot be installed.
- **Blocked package**: If Windows SmartScreen blocks the installer, choose **More info → Run anyway**, or unblock the downloaded file in **Properties**.

---

## Technical Overview

Meridian is built on **.NET 9.0** using **C# 13** and **F# 8.0** across **704 source files** (692 C# + 12 F# in `src/`), with **273 test files** (~4,100 test methods). It uses a modular, event-driven architecture with bounded channels (System.Threading.Channels) for high-throughput data processing. The unified architecture spans all four pillars: data collection, backtesting, execution, and strategy management. The system supports deployment as a single self-contained executable, a Docker container, or a systemd service.

### Implementation Status Snapshot

**Version:** 1.7.x | **Status:** Development / Pilot Ready (workflow-centric trading workstation migration in planning)

- ✅ **Core event pipeline** — bounded channels, backpressure, 100K capacity, nanosecond timing
- ✅ **Multi-provider ingest** — 90+ streaming sources (Alpaca, Interactive Brokers, Polygon, NYSE, StockSharp, etc.)
- ✅ **Historical backfill** — 10+ providers with automatic failover and rate limiting
- ✅ **Storage layer** — JSONL/Parquet with tiered retention, packaging, and export
- ✅ **Backtesting engine** — Tick-by-tick replay with multiple fill models and performance metrics (Sharpe, drawdown, XIRR)
- ✅ **Paper trading gateway** — Risk-free strategy validation against live feeds
- ✅ **Strategy lifecycle** — Register, backtest, paper-trade, promote workflows with full audit trail
- 🔄 **WPF desktop app** — Windows-native client with broad feature coverage; next migration phase consolidates pages into Research, Trading, Data Operations, and Governance workspaces
- 🔄 **Web dashboard** — Real-time monitoring, backfill controls, HTML + Prometheus + JSON status; planned to gain run, portfolio, and ledger visibility as part of the trading workstation migration
- ✅ **Data quality monitoring** — Completeness, gap analysis, anomaly detection, SLA enforcement
- ⚠️ Some providers require credentials or build flags (Interactive Brokers needs `IBAPI` constant; NYSE/Polygon need API keys)
- 📝 End-to-end OpenTelemetry trace propagation and multi-instance coordination (planned for Phase 13–15)

**For detailed status:** See [docs/status/production-status.md](docs/status/production-status.md), [docs/status/FEATURE_INVENTORY.md](docs/status/FEATURE_INVENTORY.md), and [docs/status/ROADMAP.md](docs/status/ROADMAP.md). Historical and deprecated documentation is centralized in [docs/archived/INDEX.md](docs/archived/INDEX.md).

## Pillar Details

### 📡 Data Collection (Production-Ready)
Meridian's data collection foundation is mature and production-ready:
- **Real-time streaming** from 90+ sources with automatic failover and health monitoring
- **Historical backfill** from 10+ providers with composite fallback chains and rate limiting
- **Symbol search** across 5 global providers (Alpaca, Finnhub, Polygon, OpenFIGI, StockSharp)
- **Multi-format storage** (JSONL + Parquet) with tiered retention and portable packaging
- **Data quality monitoring** with SLA enforcement and anomaly detection

### 🔬 Backtesting (Production-Ready)
Full backtesting capabilities for strategy research and validation:
- **Tick-by-tick replay engine** — Process historical data at nanosecond precision
- **Multiple fill models** — Bar midpoint, order book depth, and custom implementations
- **Performance metrics** — Sharpe ratio, max drawdown, XIRR, cumulative returns, win rate, profit factor
- **Strategy SDK** — Plug-and-play interface for implementing custom backtesting logic
- **Integration with data collection** — Backtest against collected real-world data or imported datasets

### ⚡ Execution (Paper Trading Active)
Strategy execution framework with paper trading ready for live integration:
- **Paper trading gateway** — Risk-free validation of strategies against live market feeds
- **Order management system** — Limit/market orders, position tracking, cash management
- **Portfolio state tracking** — Real-time position and performance metrics
- **Execution migration** — Current paper-trading infrastructure is being elevated into a full trading cockpit with positions, orders, fills, risk, and ledger surfaces
- **Integration pathway** — Designed for live broker adapter implementation (e.g., Interactive Brokers, Alpaca)

### 🗂️ Strategy Lifecycle (Fully Implemented)
End-to-end strategy management from development to production:
- **Registration & versioning** — Store and version control strategies
- **Backtest → Paper-Trade → Live promotion workflow** — Formalized promotion path with audit trail; being standardized around a shared `StrategyRun` model
- **Multi-run comparison** — Compare performance across multiple backtest and paper-trading runs
- **Portfolio + ledger auditability** — Migration target promotes journals, trial balances, and P&L attribution to first-class user workflows
- **Performance analytics** — Aggregate metrics and performance tracking across strategy versions
- **Audit trail** — Complete history of strategy changes, runs, and promotions

---

## Key Features

### Data Collection
- **Multi-provider ingest**: Interactive Brokers (L2 depth, tick-by-tick), Alpaca (WebSocket streaming), NYSE (direct feed), Polygon (aggregates), StockSharp (multi-exchange)
- **Historical backfill**: 10+ providers (Yahoo Finance, Stooq, Tiingo, Alpha Vantage, Finnhub, Nasdaq Data Link, Polygon, IB) with automatic failover
- **Provider-agnostic architecture**: Swap feeds without code changes and preserve stream IDs for reconciliation
- **Microstructure detail**: Tick-by-tick trades, Level 2 order book, BBO quotes, and order-flow statistics

### Performance and Reliability
- **High-performance pipeline**: Bounded channel architecture (default 50,000 events) with configurable backpressure
- **Integrity validation**: Sequence checks and order book integrity enforcement with dedicated event emission
- **Hot configuration reload**: Apply subscription changes without restarting the collector
- **Graceful shutdown**: Flushes all events and metrics before exit

### Storage and Data Management
- **Flexible JSONL storage**: Default BySymbol naming convention `{root}/{symbol}/{type}/{date}.jsonl` for optimal organization (also supports ByDate, ByType, Flat) with optional gzip compression
- **Partitioning and retention**: Daily/hourly/monthly/none plus retention by age or total capacity
- **Data replay**: Stream historical JSONL files for backtesting and research

### Monitoring and Observability
- **Web dashboard**: Modern HTML dashboard for live monitoring, integrity event tracking, and backfill controls
- **Native Windows app**: WPF desktop application for Windows-only configuration and monitoring
- **Metrics and status**: Prometheus metrics at `/metrics`, JSON status at `/status`, HTML dashboard at `/`
- **Logging**: Structured logging via Serilog with ready-to-use sinks

### Security
- **Secure credential management**: Windows CredentialPicker integration for API keys and secrets
- **Credential protection**: `.gitignore` excludes sensitive configuration files from version control
- **Environment variable support**: API credentials via environment variables for production deployments

### Architecture
- **Monolithic core**: Simple, maintainable architecture with optional UI components
- **Provider abstraction**: Clean interfaces for adding new data providers
- **ADR compliance**: Architecture Decision Records for consistent design patterns

## Quick Start

### New User? Use the Configuration Wizard

For first-time users, the interactive wizard guides you through setup:

```bash
# Clone the repository
git clone https://github.com/rodoHasArrived/Meridian.git
cd Meridian

# Run the interactive configuration wizard (recommended for new users)
dotnet run --project src/Meridian/Meridian.csproj -- --wizard
```

The wizard will:
- Detect available data providers from your environment
- Guide you through provider selection and configuration
- Set up symbols, storage, and backfill options
- Generate your `appsettings.json` automatically

### Quick Auto-Configuration

If you have environment variables set, use quick auto-configuration:

```bash
# Auto-detect providers from environment variables
dotnet run --project src/Meridian/Meridian.csproj -- --auto-config

# Check what providers are available
dotnet run --project src/Meridian/Meridian.csproj -- --detect-providers

# Validate your API credentials
dotnet run --project src/Meridian/Meridian.csproj -- --validate-credentials
```

### Manual Setup

```bash
# Clone the repository
git clone https://github.com/rodoHasArrived/Meridian.git
cd Meridian

# Copy the sample settings and edit as needed
cp config/appsettings.sample.json config/appsettings.json

# Option 1: Launch the web dashboard (serves HTML + Prometheus + JSON status)
dotnet run --project src/Meridian/Meridian.csproj -- --ui --watch-config --http-port 8080

# Run smoke test (no provider connectivity required)
dotnet run --project src/Meridian/Meridian.csproj

# Run self-tests
dotnet run --project src/Meridian/Meridian.csproj -- --selftest

# Historical backfill with overrides
dotnet run --project src/Meridian/Meridian.csproj -- \
  --backfill --backfill-provider stooq --backfill-symbols SPY,AAPL \
  --backfill-from 2024-01-01 --backfill-to 2024-01-05
```

Access the monitoring dashboard at `http://localhost:8080`, JSON status at `http://localhost:8080/status`, and Prometheus metrics at `http://localhost:8080/metrics`.

## Documentation

Comprehensive documentation is available in the `docs/` directory:

- **[docs/README.md](docs/README.md)** - Product overview, CLI/UI usage, and configuration highlights
- **[docs/HELP.md](docs/HELP.md)** - Comprehensive user guide with troubleshooting and FAQ
- **[docs/getting-started/README.md](docs/getting-started/README.md)** - End-to-end setup for local development
- **[docs/HELP.md#configuration](docs/HELP.md#configuration)** - Detailed explanation of every setting including backfill
- **[docs/architecture/overview.md](docs/architecture/overview.md)** - System architecture and design
- **[docs/operations/operator-runbook.md](docs/operations/operator-runbook.md)** - Operations guide and production deployment
- **[docs/architecture/domains.md](docs/architecture/domains.md)** - Event contracts and domain models
- **[docs/architecture/c4-diagrams.md](docs/architecture/c4-diagrams.md)** - System diagrams
- **[docs/integrations/lean-integration.md](docs/integrations/lean-integration.md)** - QuantConnect Lean integration guide and examples
- **[docs/architecture/storage-design.md](docs/architecture/storage-design.md)** - Advanced storage organization and data management strategies
- **[docs/development/github-actions-summary.md](docs/development/github-actions-summary.md)** - Build observability toolkit and CLI reference

### AI Assistant Guides
- **[CLAUDE.md](CLAUDE.md)** - Main AI assistant guide
- **[docs/ai/](docs/ai/)** - Specialized guides for subsystems (F#, providers, storage, testing)

## Supported Data Sources

### Real-Time Streaming Providers
- **Interactive Brokers** - L2 market depth, tick-by-tick trades, quotes via IB Gateway
- **Alpaca** - Real-time trades and quotes via WebSocket
- **NYSE** - Direct NYSE market data feed
- **Polygon** - Real-time trades, quotes, and aggregates
- **StockSharp** - Multi-exchange connectors

### Historical Backfill Providers
| Provider | Free Tier | Data Types | Rate Limits |
|----------|-----------|------------|-------------|
| Alpaca | Yes (with account) | Bars, trades, quotes | 200/min |
| Polygon | Limited | Bars, trades, quotes, aggregates | Varies |
| Tiingo | Yes | Daily bars | 500/hour |
| Yahoo Finance | Yes | Daily bars | Unofficial |
| Stooq | Yes | Daily bars | Low |
| StockSharp | Yes (with account) | Various | Varies |
| Finnhub | Yes | Daily bars | 60/min |
| Alpha Vantage | Yes | Daily bars | 5/min |
| Nasdaq Data Link | Limited | Various | Varies |
| Interactive Brokers | Yes (with account) | All types | IB pacing rules |

Configure fallback chains in `appsettings.json` under `Backfill.ProviderPriority` for automatic failover between providers.

### Symbol Search Providers
- **Alpaca** - Symbol search with exchange info
- **Finnhub** - Global symbol search
- **Polygon** - Ticker search with market data
- **OpenFIGI** - FIGI-based instrument resolution
- **StockSharp** - Multi-exchange symbol search

## Lean Engine Integration

Meridian now integrates with **QuantConnect's Lean Engine**, enabling sophisticated algorithmic trading strategies:

- **Custom Data Types**: Trade and quote data exposed as Lean `BaseData` types
- **Backtesting Support**: Use collected tick data for algorithm backtesting
- **Data Provider**: Custom `IDataProvider` implementation for JSONL files
- **Sample Algorithms**: Ready-to-use examples for microstructure-aware trading

See [`src/Meridian/Integrations/Lean/README.md`](src/Meridian/Integrations/Lean/README.md) for integration details and examples.

## Output Data

Market data is stored as newline-delimited JSON (JSONL) files with:
- Configurable naming conventions (by symbol, date, or type)
- Optional gzip compression
- Automatic retention management
- Data integrity events alongside market data

## Monitoring and Backfill Control

The built-in HTTP server provides:
- **Prometheus metrics** at `/metrics`
- **JSON status** at `/status`
- **Live HTML dashboard** at `/`
- **Health checks** at `/health`, `/ready`, `/live` (Kubernetes-compatible)
- **Backfill status and controls** at `/api/backfill/*`

Monitor event throughput, drop rates, integrity events, and pipeline statistics in real-time. Initiate or review historical backfill jobs directly from the dashboard without restarting the collector.

## Production Deployment

### Docker Deployment

```bash
# Production deployment with Docker Compose
make docker

# With monitoring stack (Prometheus + Grafana)
make docker-monitoring

# View logs
make docker-logs

# Health check
curl http://localhost:8080/health
```

### Kubernetes Deployment

The application supports Kubernetes-style health probes:
- **Liveness**: `/live` or `/livez`
- **Readiness**: `/ready` or `/readyz`
- **Health**: `/health` or `/healthz` (detailed JSON response)

### Systemd Service (Linux)

```bash
# Copy service file
sudo cp deploy/systemd/meridian.service /etc/systemd/system/

# Enable and start
sudo systemctl enable meridian
sudo systemctl start meridian
```

### Environment Variables

API credentials can be set via environment variables:
```bash
export ALPACA__KEYID=your-key-id
export ALPACA__SECRETKEY=your-secret-key
```

## CI/CD and Automation

The repository includes **33 GitHub Actions workflow files** for automated testing, security, documentation, and deployment:

- **Build & Release** - Automated builds, cross-platform releases, and reusable build workflow
- **Security** - CodeQL analysis, dependency auditing, and vulnerability scanning
- **Docker** - Container image builds and publishing
- **Performance Benchmarks** - Track performance metrics over time
- **Code Quality** - Linting, formatting, and code analysis
- **Test Matrix** - Multi-platform testing (Windows, Linux, macOS)
- **Desktop Builds** - WPF desktop application builds
- **PR Checks** - Pull request validation
- **Auto Labeling** - Intelligent PR and issue labeling
- **Stale Management** - Automatic issue/PR lifecycle management
- **Build Observability** - Build metrics and diagnostic capture
- **Documentation** - Auto-update docs, AI instruction sync, TODO scanning
- **Diagrams** - Architecture and UML diagram generation
- **Scheduled Maintenance** - Automated maintenance tasks
- **Nightly** - Nightly builds and extended checks
- **Workflow Validation** - Self-validating workflow correctness

See [`.github/workflows/README.md`](.github/workflows/README.md) for detailed documentation.

## Troubleshooting

### Quick Diagnostics

```bash
# Run full environment health check
make doctor

# Quick environment check
make doctor-quick

# Run build diagnostics
make diagnose

# Show build metrics
make metrics
```

### Common Issues

| Issue | Solution |
|-------|----------|
| Build fails with NETSDK1100 | Ensure `EnableWindowsTargeting=true` is set in `Directory.Build.props` |
| Provider connection errors | Run `make doctor` and verify API credentials with `--validate-credentials` |
| Missing configuration | Run `make setup-config` to create `appsettings.json` from template |
| High memory usage | Check channel capacity settings in `EventPipeline` configuration |
| Rate limit errors | Review `ProviderRateLimitTracker` logs and adjust request intervals |

### Debug Information

```bash
# Collect debug bundle for issue reporting
make collect-debug

# Build with binary log for detailed analysis
make build-binlog

# Validate JSONL data integrity
make validate-data
```

For more troubleshooting details, see [docs/HELP.md](docs/HELP.md).

### Docker Images

Pre-built Docker images are available from GitHub Container Registry:

```bash
# Pull the latest image
docker pull ghcr.io/rodohasarrived/meridian:latest

# Run the container
docker run -d -p 8080:8080 \
  -v $(pwd)/data:/app/data \
  -v $(pwd)/appsettings.json:/app/appsettings.json:ro \
  ghcr.io/rodohasarrived/meridian:latest
```

## Repository Structure

**704 source files** | **692 C#** | **12 F#** | **273 test files** | **~4,100 test methods** | **214 documentation files**

This section is auto-updated by the `readme-tree.yml` workflow on pushes to `main`.

<details>
<summary>Show live repository tree</summary>

<!-- readme-tree start -->
```
.
├── .claude
│   ├── agents
│   │   ├── meridian-blueprint.md
│   │   ├── meridian-cleanup.md
│   │   └── meridian-docs.md
│   ├── settings.json
│   ├── settings.local.json
│   └── skills
│       ├── _shared
│       │   └── project-context.md
│       ├── meridian-blueprint
│       │   ├── CHANGELOG.md
│       │   ├── SKILL.md
│       │   └── references
│       │       ├── blueprint-patterns.md
│       │       └── pipeline-position.md
│       ├── meridian-brainstorm
│       │   ├── CHANGELOG.md
│       │   ├── SKILL.md
│       │   ├── brainstorm-history.jsonl
│       │   └── references
│       │       ├── competitive-landscape.md
│       │       └── idea-dimensions.md
│       ├── meridian-code-review
│       │   ├── CHANGELOG.md
│       │   ├── SKILL.md
│       │   ├── agents
│       │   │   └── grader.md
│       │   ├── eval-viewer
│       │   │   ├── generate_review.py
│       │   │   └── viewer.html
│       │   ├── evals
│       │   │   ├── benchmark_baseline.json
│       │   │   └── evals.json
│       │   ├── references
│       │   │   ├── architecture.md
│       │   │   └── schemas.md
│       │   └── scripts
│       │       ├── __init__.py
│       │       ├── aggregate_benchmark.py
│       │       ├── package_skill.py
│       │       ├── quick_validate.py
│       │       ├── run_eval.py
│       │       └── utils.py
│       ├── meridian-provider-builder
│       │   ├── CHANGELOG.md
│       │   ├── SKILL.md
│       │   └── references
│       │       └── provider-patterns.md
│       ├── meridian-test-writer
│       │   ├── CHANGELOG.md
│       │   ├── SKILL.md
│       │   └── references
│       │       └── test-patterns.md
│       └── skills_provider.py
├── .devcontainer
│   └── devcontainer.json
├── .editorconfig
├── .flake8
├── .github
│   ├── ISSUE_TEMPLATE
│   │   ├── .gitkeep
│   │   ├── bug_report.yml
│   │   ├── config.yml
│   │   └── feature_request.yml
│   ├── PULL_REQUEST_TEMPLATE.md
│   ├── actions
│   │   └── setup-dotnet-cache
│   │       └── action.yml
│   ├── agents
│   │   ├── adr-generator.agent.md
│   │   ├── blueprint-agent.md
│   │   ├── brainstorm-agent.md
│   │   ├── bug-fix-agent.md
│   │   ├── cleanup-agent.md
│   │   ├── cleanup-specialist.agent.md
│   │   ├── code-review-agent.md
│   │   ├── documentation-agent.md
│   │   ├── performance-agent.md
│   │   ├── provider-builder-agent.md
│   │   └── test-writer-agent.md
│   ├── copilot-instructions.md
│   ├── dependabot.yml
│   ├── instructions
│   │   ├── csharp.instructions.md
│   │   ├── docs.instructions.md
│   │   ├── dotnet-tests.instructions.md
│   │   └── wpf.instructions.md
│   ├── labeler.yml
│   ├── labels.yml
│   ├── markdown-link-check-config.json
│   ├── prompts
│   │   ├── README.md
│   │   ├── add-data-provider.prompt.yml
│   │   ├── add-export-format.prompt.yml
│   │   ├── code-review.prompt.yml
│   │   ├── configure-deployment.prompt.yml
│   │   ├── explain-architecture.prompt.yml
│   │   ├── fix-build-errors.prompt.yml
│   │   ├── fix-code-quality.prompt.yml
│   │   ├── fix-test-failures.prompt.yml
│   │   ├── optimize-performance.prompt.yml
│   │   ├── project-context.prompt.yml
│   │   ├── provider-implementation-guide.prompt.yml
│   │   ├── troubleshoot-issue.prompt.yml
│   │   ├── workflow-results-code-quality.prompt.yml
│   │   ├── workflow-results-test-matrix.prompt.yml
│   │   ├── wpf-debug-improve.prompt.yml
│   │   └── write-unit-tests.prompt.yml
│   ├── pull_request_template_desktop.md
│   ├── spellcheck-config.yml
│   └── workflows
│       ├── README.md
│       ├── SKIPPED_JOBS_EXPLAINED.md
│       ├── benchmark.yml
│       ├── bottleneck-detection.yml
│       ├── build-observability.yml
│       ├── close-duplicate-issues.yml
│       ├── code-quality.yml
│       ├── copilot-pull-request-reviewer.yml
│       ├── copilot-setup-steps.yml
│       ├── copilot-swe-agent-copilot.yml
│       ├── desktop-builds.yml
│       ├── docker.yml
│       ├── documentation.yml
│       ├── export-project-artifact.yml
│       ├── golden-path-validation.yml
│       ├── labeling.yml
│       ├── maintenance-self-test.yml
│       ├── maintenance.yml
│       ├── makefile.yml
│       ├── nightly.yml
│       ├── pr-checks.yml
│       ├── prompt-generation.yml
│       ├── python-package-conda.yml
│       ├── readme-tree.yml
│       ├── release.yml
│       ├── repo-health.yml
│       ├── reusable-dotnet-build.yml
│       ├── scheduled-maintenance.yml
│       ├── security.yml
│       ├── skill-evals.yml
│       ├── stale.yml
│       ├── static.yml
│       ├── test-matrix.yml
│       ├── ticker-data-collection.yml
│       ├── update-diagrams.yml
│       └── validate-workflows.yml
├── .gitignore
├── .globalconfig
├── .markdownlint.json
├── AUDIT_REPORT.md
├── AUDIT_REPORT_2026_03_20.md
├── CLAUDE.md
├── Directory.Build.props
├── Directory.Packages.props
├── LICENSE
├── Makefile
├── Meridian.sln
├── README.md
├── audit-architecture-results.txt
├── audit-code-results.json
├── audit-results-full.json
├── benchmarks
│   ├── BOTTLENECK_REPORT.md
│   ├── Meridian.Benchmarks
│   │   ├── CollectorBenchmarks.cs
│   │   ├── EndToEndPipelineBenchmarks.cs
│   │   ├── EventPipelineBenchmarks.cs
│   │   ├── IndicatorBenchmarks.cs
│   │   ├── JsonSerializationBenchmarks.cs
│   │   ├── Meridian.Benchmarks.csproj
│   │   ├── Program.cs
│   │   ├── StorageSinkBenchmarks.cs
│   │   └── WalChecksumBenchmarks.cs
│   └── run-bottleneck-benchmarks.sh
├── build
│   ├── dotnet
│   │   ├── DocGenerator
│   │   │   ├── DocGenerator.csproj
│   │   │   └── Program.cs
│   │   └── FSharpInteropGenerator
│   │       ├── FSharpInteropGenerator.csproj
│   │       └── Program.cs
│   ├── node
│   │   ├── generate-diagrams.mjs
│   │   └── generate-icons.mjs
│   ├── python
│   │   ├── __init__.py
│   │   ├── adapters
│   │   │   ├── __init__.py
│   │   │   └── dotnet.py
│   │   ├── analytics
│   │   │   ├── __init__.py
│   │   │   ├── history.py
│   │   │   ├── metrics.py
│   │   │   └── profile.py
│   │   ├── cli
│   │   │   └── buildctl.py
│   │   ├── core
│   │   │   ├── __init__.py
│   │   │   ├── events.py
│   │   │   ├── fingerprint.py
│   │   │   ├── graph.py
│   │   │   └── utils.py
│   │   ├── diagnostics
│   │   │   ├── __init__.py
│   │   │   ├── doctor.py
│   │   │   ├── env_diff.py
│   │   │   ├── error_matcher.py
│   │   │   ├── preflight.py
│   │   │   └── validate_data.py
│   │   └── knowledge
│   │       └── errors
│   │           ├── msbuild.json
│   │           └── nuget.json
│   ├── rules
│   │   └── doc-rules.yaml
│   └── scripts
│       ├── ai-architecture-check.py
│       ├── ai-repo-updater.py
│       ├── docs
│       │   ├── README.md
│       │   ├── add-todos.py
│       │   ├── ai-docs-maintenance.py
│       │   ├── create-todo-issues.py
│       │   ├── generate-changelog.py
│       │   ├── generate-coverage.py
│       │   ├── generate-dependency-graph.py
│       │   ├── generate-health-dashboard.py
│       │   ├── generate-metrics-dashboard.py
│       │   ├── generate-prompts.py
│       │   ├── generate-structure-docs.py
│       │   ├── repair-links.py
│       │   ├── rules-engine.py
│       │   ├── run-docs-automation.py
│       │   ├── scan-todos.py
│       │   ├── sync-readme-badges.py
│       │   ├── test-scripts.py
│       │   ├── update-claude-md.py
│       │   ├── validate-api-docs.py
│       │   ├── validate-docs-structure.py
│       │   ├── validate-examples.py
│       │   ├── validate-golden-path.sh
│       │   └── validate-skill-packages.py
│       ├── hooks
│       │   ├── commit-msg
│       │   ├── install-hooks.sh
│       │   └── pre-commit
│       ├── install
│       │   ├── install.ps1
│       │   └── install.sh
│       ├── lib
│       │   └── BuildNotification.psm1
│       ├── publish
│       │   ├── publish.ps1
│       │   └── publish.sh
│       ├── run
│       │   ├── start-collector.ps1
│       │   ├── start-collector.sh
│       │   ├── stop-collector.ps1
│       │   └── stop-collector.sh
│       └── validate-tooling-metadata.py
├── config
│   ├── appsettings.json
│   ├── appsettings.sample.json
│   ├── appsettings.schema.json
│   ├── condition-codes.json
│   └── venue-mapping.json
├── deploy
│   ├── docker
│   │   ├── .dockerignore
│   │   ├── Dockerfile
│   │   ├── docker-compose.override.yml
│   │   └── docker-compose.yml
│   ├── k8s
│   │   ├── configmap.yaml
│   │   ├── deployment.yaml
│   │   ├── kustomization.yaml
│   │   ├── namespace.yaml
│   │   ├── pvc.yaml
│   │   ├── secret.yaml
│   │   ├── service.yaml
│   │   └── serviceaccount.yaml
│   ├── monitoring
│   │   ├── alert-rules.yml
│   │   ├── grafana
│   │   │   └── provisioning
│   │   │       ├── dashboards
│   │   │       │   ├── dashboards.yml
│   │   │       │   └── json
│   │   │       │       ├── meridian-overview.json
│   │   │       │       └── meridian-trades.json
│   │   │       └── datasources
│   │   │           └── datasources.yml
│   │   └── prometheus.yml
│   └── systemd
│       └── meridian.service
├── docs
│   ├── DEPENDENCIES.md
│   ├── HELP.md
│   ├── README.md
│   ├── adr
│   │   ├── 001-provider-abstraction.md
│   │   ├── 002-tiered-storage-architecture.md
│   │   ├── 003-microservices-decomposition.md
│   │   ├── 004-async-streaming-patterns.md
│   │   ├── 005-attribute-based-discovery.md
│   │   ├── 006-domain-events-polymorphic-payload.md
│   │   ├── 007-write-ahead-log-durability.md
│   │   ├── 008-multi-format-composite-storage.md
│   │   ├── 009-fsharp-interop.md
│   │   ├── 010-httpclient-factory.md
│   │   ├── 011-centralized-configuration-and-credentials.md
│   │   ├── 012-monitoring-and-alerting-pipeline.md
│   │   ├── 013-bounded-channel-policy.md
│   │   ├── 014-json-source-generators.md
│   │   ├── 015-strategy-execution-contract.md
│   │   ├── 016-platform-architecture-migration.md
│   │   ├── ADR-015-platform-restructuring.md
│   │   ├── README.md
│   │   └── _template.md
│   ├── ai
│   │   ├── README.md
│   │   ├── agents
│   │   │   └── README.md
│   │   ├── ai-known-errors.md
│   │   ├── claude
│   │   │   ├── CLAUDE.actions.md
│   │   │   ├── CLAUDE.api.md
│   │   │   ├── CLAUDE.fsharp.md
│   │   │   ├── CLAUDE.providers.md
│   │   │   ├── CLAUDE.repo-updater.md
│   │   │   ├── CLAUDE.storage.md
│   │   │   ├── CLAUDE.structure.md
│   │   │   └── CLAUDE.testing.md
│   │   ├── copilot
│   │   │   ├── ai-sync-workflow.md
│   │   │   └── instructions.md
│   │   ├── instructions
│   │   │   └── README.md
│   │   ├── prompts
│   │   │   └── README.md
│   │   └── skills
│   │       └── README.md
│   ├── architecture
│   │   ├── README.md
│   │   ├── c4-diagrams.md
│   │   ├── crystallized-storage-format.md
│   │   ├── desktop-layers.md
│   │   ├── deterministic-canonicalization.md
│   │   ├── domains.md
│   │   ├── layer-boundaries.md
│   │   ├── overview.md
│   │   ├── provider-management.md
│   │   ├── storage-design.md
│   │   ├── ui-redesign.md
│   │   └── why-this-architecture.md
│   ├── archived
│   │   ├── 2026-02_PR_SUMMARY.md
│   │   ├── 2026-02_UI_IMPROVEMENTS_SUMMARY.md
│   │   ├── 2026-02_VISUAL_CODE_EXAMPLES.md
│   │   ├── ARTIFACT_ACTIONS_DOWNGRADE.md
│   │   ├── CHANGES_SUMMARY.md
│   │   ├── CLEANUP_OPPORTUNITIES.md
│   │   ├── CLEANUP_SUMMARY.md
│   │   ├── CONFIG_CONSOLIDATION_REPORT.md
│   │   ├── CS0101_FIX_SUMMARY.md
│   │   ├── DUPLICATE_CODE_ANALYSIS.md
│   │   ├── H3_DEBUG_CODE_ANALYSIS.md
│   │   ├── IMPROVEMENTS_2026-02.md
│   │   ├── INDEX.md
│   │   ├── QUICKSTART_2026-01-08.md
│   │   ├── README.md
│   │   ├── REDESIGN_IMPROVEMENTS.md
│   │   ├── REPOSITORY_REORGANIZATION_PLAN.md
│   │   ├── ROADMAP_UPDATE_SUMMARY.md
│   │   ├── STRUCTURAL_IMPROVEMENTS_2026-02.md
│   │   ├── TEST_MATRIX_FIX_SUMMARY.md
│   │   ├── UWP_COMPREHENSIVE_AUDIT.md
│   │   ├── WORKFLOW_IMPROVEMENTS_2026-01-08.md
│   │   ├── c4-context-legacy.png
│   │   ├── c4-context-legacy.puml
│   │   ├── consolidation.md
│   │   ├── desktop-app-xaml-compiler-errors.md
│   │   ├── desktop-devex-high-value-improvements.md
│   │   ├── desktop-end-user-improvements-shortlist.md
│   │   ├── desktop-end-user-improvements.md
│   │   ├── desktop-ui-alternatives-evaluation.md
│   │   ├── repository-cleanup-action-plan.md
│   │   ├── uwp-development-roadmap.md
│   │   ├── uwp-release-checklist.md
│   │   └── uwp-to-wpf-migration.md
│   ├── audits
│   │   ├── CODE_REVIEW_2026-03-16.md
│   │   ├── FURTHER_SIMPLIFICATION_OPPORTUNITIES.md
│   │   └── README.md
│   ├── development
│   │   ├── README.md
│   │   ├── adding-custom-rules.md
│   │   ├── build-observability.md
│   │   ├── central-package-management.md
│   │   ├── desktop-testing-guide.md
│   │   ├── documentation-automation.md
│   │   ├── documentation-contribution-guide.md
│   │   ├── expanding-scripts.md
│   │   ├── fsharp-decision-rule.md
│   │   ├── github-actions-summary.md
│   │   ├── github-actions-testing.md
│   │   ├── policies
│   │   │   └── desktop-support-policy.md
│   │   ├── provider-implementation.md
│   │   ├── refactor-map.md
│   │   ├── repository-organization-guide.md
│   │   ├── tooling-workflow-backlog.md
│   │   ├── ui-fixture-mode-guide.md
│   │   └── wpf-implementation-notes.md
│   ├── diagrams
│   │   ├── README.md
│   │   ├── c4-level1-context.dot
│   │   ├── c4-level1-context.png
│   │   ├── c4-level1-context.svg
│   │   ├── c4-level2-containers.dot
│   │   ├── c4-level2-containers.png
│   │   ├── c4-level2-containers.svg
│   │   ├── c4-level3-components.dot
│   │   ├── c4-level3-components.png
│   │   ├── c4-level3-components.svg
│   │   ├── cli-commands.dot
│   │   ├── cli-commands.png
│   │   ├── cli-commands.svg
│   │   ├── data-flow.dot
│   │   ├── data-flow.png
│   │   ├── data-flow.svg
│   │   ├── deployment-options.dot
│   │   ├── deployment-options.png
│   │   ├── deployment-options.svg
│   │   ├── event-pipeline-sequence.dot
│   │   ├── event-pipeline-sequence.png
│   │   ├── event-pipeline-sequence.svg
│   │   ├── onboarding-flow.dot
│   │   ├── onboarding-flow.png
│   │   ├── onboarding-flow.svg
│   │   ├── project-dependencies.dot
│   │   ├── project-dependencies.png
│   │   ├── project-dependencies.svg
│   │   ├── provider-architecture.dot
│   │   ├── provider-architecture.png
│   │   ├── provider-architecture.svg
│   │   ├── resilience-patterns.dot
│   │   ├── resilience-patterns.png
│   │   ├── resilience-patterns.svg
│   │   ├── storage-architecture.dot
│   │   ├── storage-architecture.png
│   │   ├── storage-architecture.svg
│   │   ├── ui-implementation-flow.dot
│   │   ├── ui-implementation-flow.svg
│   │   ├── ui-navigation-map.dot
│   │   ├── ui-navigation-map.svg
│   │   └── uml
│   │       ├── README.md
│   │       ├── activity-diagram-backfill.png
│   │       ├── activity-diagram-backfill.puml
│   │       ├── activity-diagram.png
│   │       ├── activity-diagram.puml
│   │       ├── communication-diagram.png
│   │       ├── communication-diagram.puml
│   │       ├── interaction-overview-diagram.png
│   │       ├── interaction-overview-diagram.puml
│   │       ├── sequence-diagram-backfill.png
│   │       ├── sequence-diagram-backfill.puml
│   │       ├── sequence-diagram.png
│   │       ├── sequence-diagram.puml
│   │       ├── state-diagram-backfill.png
│   │       ├── state-diagram-backfill.puml
│   │       ├── state-diagram-orderbook.png
│   │       ├── state-diagram-orderbook.puml
│   │       ├── state-diagram-trade-sequence.png
│   │       ├── state-diagram-trade-sequence.puml
│   │       ├── state-diagram.png
│   │       ├── state-diagram.puml
│   │       ├── timing-diagram-backfill.png
│   │       ├── timing-diagram-backfill.puml
│   │       ├── timing-diagram.png
│   │       ├── timing-diagram.puml
│   │       ├── use-case-diagram.png
│   │       └── use-case-diagram.puml
│   ├── docfx
│   │   ├── README.md
│   │   └── docfx.json
│   ├── evaluations
│   │   ├── 2026-03-brainstorm-next-frontier.md
│   │   ├── README.md
│   │   ├── assembly-performance-opportunities.md
│   │   ├── data-quality-monitoring-evaluation.md
│   │   ├── desktop-improvements-executive-summary.md
│   │   ├── desktop-platform-improvements-implementation-guide.md
│   │   ├── high-impact-improvement-brainstorm-2026-03.md
│   │   ├── high-impact-improvements-brainstorm.md
│   │   ├── high-value-low-cost-improvements-brainstorm.md
│   │   ├── historical-data-providers-evaluation.md
│   │   ├── ingestion-orchestration-evaluation.md
│   │   ├── nautilus-inspired-restructuring-proposal.md
│   │   ├── operational-readiness-evaluation.md
│   │   ├── quant-script-blueprint-brainstorm.md
│   │   ├── realtime-streaming-architecture-evaluation.md
│   │   ├── storage-architecture-evaluation.md
│   │   └── windows-desktop-provider-configurability-assessment.md
│   ├── examples
│   │   └── provider-template
│   │       ├── README.md
│   │       ├── TemplateConfig.cs
│   │       ├── TemplateConstants.cs
│   │       ├── TemplateFactory.cs
│   │       ├── TemplateHistoricalDataProvider.cs
│   │       ├── TemplateMarketDataClient.cs
│   │       └── TemplateSymbolSearchProvider.cs
│   ├── generated
│   │   ├── README.md
│   │   ├── adr-index.md
│   │   ├── configuration-schema.md
│   │   ├── documentation-coverage.md
│   │   ├── project-context.md
│   │   ├── provider-registry.md
│   │   ├── repository-structure.md
│   │   └── workflows-overview.md
│   ├── getting-started
│   │   └── README.md
│   ├── integrations
│   │   ├── README.md
│   │   ├── fsharp-integration.md
│   │   ├── language-strategy.md
│   │   └── lean-integration.md
│   ├── operations
│   │   ├── README.md
│   │   ├── deployment.md
│   │   ├── high-availability.md
│   │   ├── msix-packaging.md
│   │   ├── operator-runbook.md
│   │   ├── performance-tuning.md
│   │   ├── portable-data-packager.md
│   │   └── service-level-objectives.md
│   ├── plans
│   │   ├── assembly-performance-roadmap.md
│   │   ├── codebase-audit-cleanup-roadmap.md
│   │   ├── l3-inference-implementation-plan.md
│   │   ├── ledger
│   │   ├── quant-script-environment-blueprint.md
│   │   ├── readability-refactor-baseline.md
│   │   ├── readability-refactor-roadmap.md
│   │   ├── readability-refactor-technical-design-pack.md
│   │   └── trading-workstation-migration-blueprint.md
│   ├── providers
│   │   ├── README.md
│   │   ├── alpaca-setup.md
│   │   ├── backfill-guide.md
│   │   ├── data-sources.md
│   │   ├── interactive-brokers-free-equity-reference.md
│   │   ├── interactive-brokers-setup.md
│   │   └── provider-comparison.md
│   ├── reference
│   │   ├── README.md
│   │   ├── api-reference.md
│   │   ├── data-dictionary.md
│   │   ├── data-uniformity.md
│   │   ├── design-review-memo.md
│   │   ├── environment-variables.md
│   │   └── open-source-references.md
│   ├── security
│   │   ├── README.md
│   │   └── known-vulnerabilities.md
│   ├── status
│   │   ├── CHANGELOG.md
│   │   ├── EVALUATIONS_AND_AUDITS.md
│   │   ├── FEATURE_INVENTORY.md
│   │   ├── FULL_IMPLEMENTATION_TODO_2026_03_20.md
│   │   ├── IMPROVEMENTS.md
│   │   ├── README.md
│   │   ├── ROADMAP.md
│   │   ├── TODO.md
│   │   ├── health-dashboard.md
│   │   └── production-status.md
│   └── toc.yml
├── environment.yml
├── global.json
├── package-lock.json
├── package.json
├── prompt-generation-results.json
├── scripts
│   ├── ai
│   │   ├── common.sh
│   │   ├── maintenance-full.sh
│   │   ├── maintenance-light.sh
│   │   ├── maintenance.sh
│   │   ├── route-maintenance.sh
│   │   └── setup-ai-agent.sh
│   ├── compare_benchmarks.py
│   ├── dev
│   │   ├── desktop-dev.ps1
│   │   └── diagnose-uwp-xaml.ps1
│   ├── generate-diagrams.mjs
│   └── lib
│       ├── ui-diagram-generator.mjs
│       └── ui-diagram-generator.test.mjs
├── src
│   ├── Meridian
│   │   ├── DashboardServerBridge.cs
│   │   ├── GlobalUsings.cs
│   │   ├── Integrations
│   │   │   └── Lean
│   │   │       ├── MeridianDataProvider.cs
│   │   │       ├── MeridianQuoteData.cs
│   │   │       ├── MeridianTradeData.cs
│   │   │       ├── README.md
│   │   │       └── SampleLeanAlgorithm.cs
│   │   ├── Meridian.csproj
│   │   ├── Program.cs
│   │   ├── Tools
│   │   │   └── DataValidator.cs
│   │   ├── UiServer.cs
│   │   ├── app.manifest
│   │   ├── runtimeconfig.template.json
│   │   └── wwwroot
│   │       └── templates
│   │           ├── credentials.html
│   │           ├── index.html
│   │           └── index.js
│   ├── Meridian.Application
│   │   ├── Backfill
│   │   │   ├── BackfillCostEstimator.cs
│   │   │   ├── BackfillRequest.cs
│   │   │   ├── BackfillResult.cs
│   │   │   ├── BackfillStatusStore.cs
│   │   │   ├── GapBackfillService.cs
│   │   │   └── HistoricalBackfillService.cs
│   │   ├── Canonicalization
│   │   │   ├── CanonicalizationMetrics.cs
│   │   │   ├── CanonicalizingPublisher.cs
│   │   │   ├── ConditionCodeMapper.cs
│   │   │   ├── EventCanonicalizer.cs
│   │   │   ├── IEventCanonicalizer.cs
│   │   │   └── VenueMicMapper.cs
│   │   ├── Commands
│   │   │   ├── CatalogCommand.cs
│   │   │   ├── CliArguments.cs
│   │   │   ├── CommandDispatcher.cs
│   │   │   ├── ConfigCommands.cs
│   │   │   ├── ConfigPresetCommand.cs
│   │   │   ├── DiagnosticsCommands.cs
│   │   │   ├── DryRunCommand.cs
│   │   │   ├── GenerateLoaderCommand.cs
│   │   │   ├── HelpCommand.cs
│   │   │   ├── ICliCommand.cs
│   │   │   ├── PackageCommands.cs
│   │   │   ├── QueryCommand.cs
│   │   │   ├── SchemaCheckCommand.cs
│   │   │   ├── SelfTestCommand.cs
│   │   │   ├── SymbolCommands.cs
│   │   │   ├── ValidateConfigCommand.cs
│   │   │   └── WalRepairCommand.cs
│   │   ├── Composition
│   │   │   ├── CircuitBreakerCallbackRouter.cs
│   │   │   ├── Features
│   │   │   │   ├── BackfillFeatureRegistration.cs
│   │   │   │   ├── CanonicalizationFeatureRegistration.cs
│   │   │   │   ├── CollectorFeatureRegistration.cs
│   │   │   │   ├── ConfigurationFeatureRegistration.cs
│   │   │   │   ├── CredentialFeatureRegistration.cs
│   │   │   │   ├── DiagnosticsFeatureRegistration.cs
│   │   │   │   ├── HttpClientFeatureRegistration.cs
│   │   │   │   ├── IServiceFeatureRegistration.cs
│   │   │   │   ├── MaintenanceFeatureRegistration.cs
│   │   │   │   ├── PipelineFeatureRegistration.cs
│   │   │   │   ├── ProviderFeatureRegistration.cs
│   │   │   │   ├── StorageFeatureRegistration.cs
│   │   │   │   └── SymbolManagementFeatureRegistration.cs
│   │   │   ├── HostAdapters.cs
│   │   │   ├── HostStartup.cs
│   │   │   ├── ServiceCompositionRoot.cs
│   │   │   └── Startup
│   │   │       └── SharedStartupBootstrapper.cs
│   │   ├── Config
│   │   │   ├── AppConfigJsonOptions.cs
│   │   │   ├── ConfigDtoMapper.cs
│   │   │   ├── ConfigJsonSchemaGenerator.cs
│   │   │   ├── ConfigValidationHelper.cs
│   │   │   ├── ConfigValidatorCli.cs
│   │   │   ├── ConfigWatcher.cs
│   │   │   ├── ConfigurationPipeline.cs
│   │   │   ├── Credentials
│   │   │   │   ├── CredentialStatus.cs
│   │   │   │   ├── CredentialTestingService.cs
│   │   │   │   ├── OAuthToken.cs
│   │   │   │   ├── OAuthTokenRefreshService.cs
│   │   │   │   └── ProviderCredentialResolver.cs
│   │   │   ├── DeploymentContext.cs
│   │   │   ├── IConfigValidator.cs
│   │   │   ├── SensitiveValueMasker.cs
│   │   │   └── StorageConfigExtensions.cs
│   │   ├── Credentials
│   │   │   └── ICredentialStore.cs
│   │   ├── Filters
│   │   │   └── MarketEventFilter.cs
│   │   ├── GlobalUsings.cs
│   │   ├── Http
│   │   │   ├── BackfillCoordinator.cs
│   │   │   ├── ConfigStore.cs
│   │   │   ├── Endpoints
│   │   │   │   ├── ArchiveMaintenanceEndpoints.cs
│   │   │   │   ├── DataQualityEndpoints.cs
│   │   │   │   ├── PackagingEndpoints.cs
│   │   │   │   └── StatusEndpointHandlers.cs
│   │   │   ├── HtmlTemplateLoader.cs
│   │   │   └── HtmlTemplates.cs
│   │   ├── Indicators
│   │   │   └── TechnicalIndicatorService.cs
│   │   ├── Meridian.Application.csproj
│   │   ├── Monitoring
│   │   │   ├── BackpressureAlertService.cs
│   │   │   ├── BadTickFilter.cs
│   │   │   ├── CircuitBreakerStatusService.cs
│   │   │   ├── ClockSkewEstimator.cs
│   │   │   ├── ConnectionHealthMonitor.cs
│   │   │   ├── ConnectionStatusWebhook.cs
│   │   │   ├── Core
│   │   │   │   ├── AlertDispatcher.cs
│   │   │   │   ├── AlertRunbookRegistry.cs
│   │   │   │   ├── HealthCheckAggregator.cs
│   │   │   │   └── SloDefinitionRegistry.cs
│   │   │   ├── DataLossAccounting.cs
│   │   │   ├── DataQuality
│   │   │   │   ├── AnomalyDetector.cs
│   │   │   │   ├── CompletenessScoreCalculator.cs
│   │   │   │   ├── CrossProviderComparisonService.cs
│   │   │   │   ├── DataFreshnessSlaMonitor.cs
│   │   │   │   ├── DataQualityModels.cs
│   │   │   │   ├── DataQualityMonitoringService.cs
│   │   │   │   ├── DataQualityReportGenerator.cs
│   │   │   │   ├── GapAnalyzer.cs
│   │   │   │   ├── IQualityAnalyzer.cs
│   │   │   │   ├── LatencyHistogram.cs
│   │   │   │   ├── LiquidityProfileProvider.cs
│   │   │   │   ├── PriceContinuityChecker.cs
│   │   │   │   └── SequenceErrorTracker.cs
│   │   │   ├── DetailedHealthCheck.cs
│   │   │   ├── ErrorRingBuffer.cs
│   │   │   ├── IEventMetrics.cs
│   │   │   ├── Metrics.cs
│   │   │   ├── PrometheusMetrics.cs
│   │   │   ├── ProviderDegradationScorer.cs
│   │   │   ├── ProviderLatencyService.cs
│   │   │   ├── ProviderMetricsStatus.cs
│   │   │   ├── SchemaValidationService.cs
│   │   │   ├── SpreadMonitor.cs
│   │   │   ├── StatusHttpServer.cs
│   │   │   ├── StatusSnapshot.cs
│   │   │   ├── StatusWriter.cs
│   │   │   ├── SystemHealthChecker.cs
│   │   │   ├── TickSizeValidator.cs
│   │   │   ├── TimestampMonotonicityChecker.cs
│   │   │   └── ValidationMetrics.cs
│   │   ├── Pipeline
│   │   │   ├── DeadLetterSink.cs
│   │   │   ├── DroppedEventAuditTrail.cs
│   │   │   ├── DualPathEventPipeline.cs
│   │   │   ├── EventPipeline.cs
│   │   │   ├── FSharpEventValidator.cs
│   │   │   ├── HotPathBatchSerializer.cs
│   │   │   ├── IEventValidator.cs
│   │   │   ├── IngestionJobService.cs
│   │   │   ├── PersistentDedupLedger.cs
│   │   │   └── SchemaUpcasterRegistry.cs
│   │   ├── Results
│   │   │   ├── ErrorCode.cs
│   │   │   ├── OperationError.cs
│   │   │   └── Result.cs
│   │   ├── Scheduling
│   │   │   ├── BackfillExecutionLog.cs
│   │   │   ├── BackfillSchedule.cs
│   │   │   ├── BackfillScheduleManager.cs
│   │   │   ├── IOperationalScheduler.cs
│   │   │   ├── OperationalScheduler.cs
│   │   │   └── ScheduledBackfillService.cs
│   │   ├── Services
│   │   │   ├── ApiDocumentationService.cs
│   │   │   ├── AutoConfigurationService.cs
│   │   │   ├── CanonicalSymbolRegistry.cs
│   │   │   ├── CliModeResolver.cs
│   │   │   ├── ConfigEnvironmentOverride.cs
│   │   │   ├── ConfigTemplateGenerator.cs
│   │   │   ├── ConfigurationService.cs
│   │   │   ├── ConfigurationServiceCredentialAdapter.cs
│   │   │   ├── ConfigurationWizard.cs
│   │   │   ├── ConnectivityTestService.cs
│   │   │   ├── CredentialValidationService.cs
│   │   │   ├── DailySummaryWebhook.cs
│   │   │   ├── DiagnosticBundleService.cs
│   │   │   ├── DryRunService.cs
│   │   │   ├── ErrorTracker.cs
│   │   │   ├── FriendlyErrorFormatter.cs
│   │   │   ├── GracefulShutdownHandler.cs
│   │   │   ├── GracefulShutdownService.cs
│   │   │   ├── HistoricalDataQueryService.cs
│   │   │   ├── OptionsChainService.cs
│   │   │   ├── PreflightChecker.cs
│   │   │   ├── ProgressDisplayService.cs
│   │   │   ├── SampleDataGenerator.cs
│   │   │   ├── ServiceRegistry.cs
│   │   │   ├── StartupSummary.cs
│   │   │   └── TradingCalendar.cs
│   │   ├── Subscriptions
│   │   │   ├── Services
│   │   │   │   ├── AutoResubscribePolicy.cs
│   │   │   │   ├── BatchOperationsService.cs
│   │   │   │   ├── IndexSubscriptionService.cs
│   │   │   │   ├── MetadataEnrichmentService.cs
│   │   │   │   ├── PortfolioImportService.cs
│   │   │   │   ├── SchedulingService.cs
│   │   │   │   ├── SymbolImportExportService.cs
│   │   │   │   ├── SymbolManagementService.cs
│   │   │   │   ├── SymbolSearchService.cs
│   │   │   │   ├── TemplateService.cs
│   │   │   │   └── WatchlistService.cs
│   │   │   └── SubscriptionOrchestrator.cs
│   │   ├── Testing
│   │   │   └── DepthBufferSelfTests.cs
│   │   ├── Tracing
│   │   │   ├── OpenTelemetrySetup.cs
│   │   │   └── TracedEventMetrics.cs
│   │   └── Wizard
│   │       ├── Core
│   │       │   ├── IWizardStep.cs
│   │       │   ├── WizardContext.cs
│   │       │   ├── WizardCoordinator.cs
│   │       │   ├── WizardStepId.cs
│   │       │   ├── WizardStepResult.cs
│   │       │   ├── WizardStepStatus.cs
│   │       │   ├── WizardSummary.cs
│   │       │   └── WizardTransition.cs
│   │       ├── Metadata
│   │       │   ├── ProviderDescriptor.cs
│   │       │   └── ProviderRegistry.cs
│   │       ├── Steps
│   │       │   ├── ConfigureBackfillStep.cs
│   │       │   ├── ConfigureDataSourceStep.cs
│   │       │   ├── ConfigureStorageStep.cs
│   │       │   ├── ConfigureSymbolsStep.cs
│   │       │   ├── CredentialGuidanceStep.cs
│   │       │   ├── DetectProvidersStep.cs
│   │       │   ├── ReviewConfigurationStep.cs
│   │       │   ├── SaveConfigurationStep.cs
│   │       │   ├── SelectUseCaseStep.cs
│   │       │   └── ValidateCredentialsStep.cs
│   │       └── WizardWorkflowFactory.cs
│   ├── Meridian.Backtesting
│   │   ├── Engine
│   │   │   ├── BacktestContext.cs
│   │   │   ├── BacktestEngine.cs
│   │   │   ├── MultiSymbolMergeEnumerator.cs
│   │   │   └── UniverseDiscovery.cs
│   │   ├── FillModels
│   │   │   ├── BarMidpointFillModel.cs
│   │   │   ├── IFillModel.cs
│   │   │   ├── OrderBookFillModel.cs
│   │   │   └── OrderFillResult.cs
│   │   ├── GlobalUsings.cs
│   │   ├── Meridian.Backtesting.csproj
│   │   ├── Metrics
│   │   │   ├── BacktestMetricsEngine.cs
│   │   │   └── XirrCalculator.cs
│   │   ├── Plugins
│   │   │   └── StrategyPluginLoader.cs
│   │   └── Portfolio
│   │       ├── ICommissionModel.cs
│   │       └── SimulatedPortfolio.cs
│   ├── Meridian.Backtesting.Sdk
│   │   ├── AssetEvent.cs
│   │   ├── BacktestProgressEvent.cs
│   │   ├── BacktestRequest.cs
│   │   ├── BacktestResult.cs
│   │   ├── CashFlowEntry.cs
│   │   ├── FillEvent.cs
│   │   ├── FinancialAccount.cs
│   │   ├── FinancialAccountSnapshot.cs
│   │   ├── GlobalUsings.cs
│   │   ├── IBacktestContext.cs
│   │   ├── IBacktestStrategy.cs
│   │   ├── Ledger
│   │   │   ├── BacktestLedger.cs
│   │   │   ├── JournalEntry.cs
│   │   │   ├── LedgerAccount.cs
│   │   │   ├── LedgerAccountType.cs
│   │   │   ├── LedgerAccounts.cs
│   │   │   └── LedgerEntry.cs
│   │   ├── Meridian.Backtesting.Sdk.csproj
│   │   ├── Order.cs
│   │   ├── PortfolioSnapshot.cs
│   │   ├── Position.cs
│   │   └── StrategyParameterAttribute.cs
│   ├── Meridian.Contracts
│   │   ├── Api
│   │   │   ├── BackfillApiModels.cs
│   │   │   ├── ClientModels.cs
│   │   │   ├── ErrorResponse.cs
│   │   │   ├── LiveDataModels.cs
│   │   │   ├── OptionsModels.cs
│   │   │   ├── ProviderCatalog.cs
│   │   │   ├── Quality
│   │   │   │   └── QualityApiModels.cs
│   │   │   ├── StatusEndpointModels.cs
│   │   │   ├── StatusModels.cs
│   │   │   ├── UiApiClient.cs
│   │   │   ├── UiApiRoutes.cs
│   │   │   └── UiDashboardModels.cs
│   │   ├── Archive
│   │   │   └── ArchiveHealthModels.cs
│   │   ├── Backfill
│   │   │   └── BackfillProgress.cs
│   │   ├── Catalog
│   │   │   ├── DirectoryIndex.cs
│   │   │   ├── ICanonicalSymbolRegistry.cs
│   │   │   ├── StorageCatalog.cs
│   │   │   └── SymbolRegistry.cs
│   │   ├── Configuration
│   │   │   ├── AppConfigDto.cs
│   │   │   ├── DerivativesConfigDto.cs
│   │   │   └── SymbolConfig.cs
│   │   ├── Credentials
│   │   │   ├── CredentialModels.cs
│   │   │   └── ISecretProvider.cs
│   │   ├── Domain
│   │   │   ├── CanonicalSymbol.cs
│   │   │   ├── Enums
│   │   │   │   ├── AggressorSide.cs
│   │   │   │   ├── CanonicalTradeCondition.cs
│   │   │   │   ├── ConnectionStatus.cs
│   │   │   │   ├── DepthIntegrityKind.cs
│   │   │   │   ├── DepthOperation.cs
│   │   │   │   ├── InstrumentType.cs
│   │   │   │   ├── IntegritySeverity.cs
│   │   │   │   ├── LiquidityProfile.cs
│   │   │   │   ├── MarketEventTier.cs
│   │   │   │   ├── MarketEventType.cs
│   │   │   │   ├── MarketState.cs
│   │   │   │   ├── OptionRight.cs
│   │   │   │   ├── OptionStyle.cs
│   │   │   │   ├── OrderBookSide.cs
│   │   │   │   └── OrderSide.cs
│   │   │   ├── Events
│   │   │   │   ├── IMarketEventPayload.cs
│   │   │   │   ├── MarketEvent.cs
│   │   │   │   └── MarketEventPayload.cs
│   │   │   ├── MarketDataModels.cs
│   │   │   ├── Models
│   │   │   │   ├── AdjustedHistoricalBar.cs
│   │   │   │   ├── AggregateBarPayload.cs
│   │   │   │   ├── BboQuotePayload.cs
│   │   │   │   ├── DepthIntegrityEvent.cs
│   │   │   │   ├── GreeksSnapshot.cs
│   │   │   │   ├── HistoricalAuction.cs
│   │   │   │   ├── HistoricalBar.cs
│   │   │   │   ├── HistoricalQuote.cs
│   │   │   │   ├── HistoricalTrade.cs
│   │   │   │   ├── IntegrityEvent.cs
│   │   │   │   ├── L2SnapshotPayload.cs
│   │   │   │   ├── LOBSnapshot.cs
│   │   │   │   ├── MarketQuoteUpdate.cs
│   │   │   │   ├── OpenInterestUpdate.cs
│   │   │   │   ├── OptionChainSnapshot.cs
│   │   │   │   ├── OptionContractSpec.cs
│   │   │   │   ├── OptionQuote.cs
│   │   │   │   ├── OptionTrade.cs
│   │   │   │   ├── OrderAdd.cs
│   │   │   │   ├── OrderBookLevel.cs
│   │   │   │   ├── OrderCancel.cs
│   │   │   │   ├── OrderExecute.cs
│   │   │   │   ├── OrderFlowStatistics.cs
│   │   │   │   ├── OrderModify.cs
│   │   │   │   ├── OrderReplace.cs
│   │   │   │   └── Trade.cs
│   │   │   ├── ProviderId.cs
│   │   │   ├── StreamId.cs
│   │   │   ├── SubscriptionId.cs
│   │   │   ├── SymbolId.cs
│   │   │   └── VenueCode.cs
│   │   ├── Export
│   │   │   ├── AnalysisExportModels.cs
│   │   │   ├── ExportPreset.cs
│   │   │   └── StandardPresets.cs
│   │   ├── Manifest
│   │   │   └── DataManifest.cs
│   │   ├── Meridian.Contracts.csproj
│   │   ├── Pipeline
│   │   │   ├── IngestionJob.cs
│   │   │   └── PipelinePolicyConstants.cs
│   │   ├── Schema
│   │   │   ├── EventSchema.cs
│   │   │   └── ISchemaUpcaster.cs
│   │   ├── Session
│   │   │   └── CollectionSession.cs
│   │   └── Store
│   │       └── MarketDataQuery.cs
│   ├── Meridian.Core
│   │   ├── Config
│   │   │   ├── AlpacaOptions.cs
│   │   │   ├── AppConfig.cs
│   │   │   ├── BackfillConfig.cs
│   │   │   ├── CanonicalizationConfig.cs
│   │   │   ├── DataSourceConfig.cs
│   │   │   ├── DataSourceKind.cs
│   │   │   ├── DataSourceKindConverter.cs
│   │   │   ├── DerivativesConfig.cs
│   │   │   ├── IConfigurationProvider.cs
│   │   │   ├── StockSharpConfig.cs
│   │   │   ├── SyntheticMarketDataConfig.cs
│   │   │   └── ValidatedConfig.cs
│   │   ├── Exceptions
│   │   │   ├── ConfigurationException.cs
│   │   │   ├── ConnectionException.cs
│   │   │   ├── DataProviderException.cs
│   │   │   ├── MeridianException.cs
│   │   │   ├── OperationTimeoutException.cs
│   │   │   ├── RateLimitException.cs
│   │   │   ├── SequenceValidationException.cs
│   │   │   ├── StorageException.cs
│   │   │   └── ValidationException.cs
│   │   ├── GlobalUsings.cs
│   │   ├── Logging
│   │   │   └── LoggingSetup.cs
│   │   ├── Meridian.Core.csproj
│   │   ├── Monitoring
│   │   │   ├── Core
│   │   │   │   ├── IAlertDispatcher.cs
│   │   │   │   └── IHealthCheckProvider.cs
│   │   │   ├── EventSchemaValidator.cs
│   │   │   ├── IConnectionHealthMonitor.cs
│   │   │   ├── IReconnectionMetrics.cs
│   │   │   └── MigrationDiagnostics.cs
│   │   ├── Performance
│   │   │   └── Performance
│   │   │       ├── ConnectionWarmUp.cs
│   │   │       ├── RawQuoteEvent.cs
│   │   │       ├── RawTradeEvent.cs
│   │   │       ├── SpscRingBuffer.cs
│   │   │       ├── SymbolTable.cs
│   │   │       └── ThreadingUtilities.cs
│   │   ├── Pipeline
│   │   │   └── EventPipelinePolicy.cs
│   │   ├── Scheduling
│   │   │   └── CronExpressionParser.cs
│   │   ├── Serialization
│   │   │   └── MarketDataJsonContext.cs
│   │   ├── Services
│   │   │   └── IFlushable.cs
│   │   └── Subscriptions
│   │       └── Models
│   │           ├── BatchOperations.cs
│   │           ├── BulkImportExport.cs
│   │           ├── IndexComponents.cs
│   │           ├── PortfolioImport.cs
│   │           ├── ResubscriptionMetrics.cs
│   │           ├── SubscriptionSchedule.cs
│   │           ├── SymbolMetadata.cs
│   │           ├── SymbolSearchResult.cs
│   │           ├── SymbolTemplate.cs
│   │           └── Watchlist.cs
│   ├── Meridian.Domain
│   │   ├── BannedReferences.txt
│   │   ├── Collectors
│   │   │   ├── IQuoteStateStore.cs
│   │   │   ├── L3OrderBookCollector.cs
│   │   │   ├── MarketDepthCollector.cs
│   │   │   ├── OptionDataCollector.cs
│   │   │   ├── QuoteCollector.cs
│   │   │   ├── SymbolSubscriptionTracker.cs
│   │   │   └── TradeDataCollector.cs
│   │   ├── Events
│   │   │   ├── IBackpressureSignal.cs
│   │   │   ├── IMarketEventPublisher.cs
│   │   │   ├── MarketEvent.cs
│   │   │   ├── MarketEventPayload.cs
│   │   │   ├── PublishResult.cs
│   │   │   └── Publishers
│   │   │       └── CompositePublisher.cs
│   │   ├── GlobalUsings.cs
│   │   ├── Meridian.Domain.csproj
│   │   └── Models
│   │       ├── AggregateBar.cs
│   │       ├── MarketDepthUpdate.cs
│   │       └── MarketTradeUpdate.cs
│   ├── Meridian.Execution
│   │   ├── Adapters
│   │   │   └── PaperTradingGateway.cs
│   │   ├── Exceptions
│   │   │   └── UnsupportedOrderRequestException.cs
│   │   ├── GlobalUsings.cs
│   │   ├── IRiskValidator.cs
│   │   ├── Interfaces
│   │   │   ├── IExecutionContext.cs
│   │   │   ├── ILiveFeedAdapter.cs
│   │   │   └── IOrderGateway.cs
│   │   ├── Meridian.Execution.csproj
│   │   ├── Models
│   │   │   ├── ExecutionMode.cs
│   │   │   ├── ExecutionPosition.cs
│   │   │   ├── IPortfolioState.cs
│   │   │   ├── OrderAcknowledgement.cs
│   │   │   ├── OrderGatewayCapabilities.cs
│   │   │   ├── OrderStatus.cs
│   │   │   └── OrderStatusUpdate.cs
│   │   ├── OrderManagementSystem.cs
│   │   ├── PaperTradingGateway.cs
│   │   └── Services
│   │       └── OrderLifecycleManager.cs
│   ├── Meridian.Execution.Sdk
│   │   ├── IExecutionGateway.cs
│   │   ├── IOrderManager.cs
│   │   ├── IPositionTracker.cs
│   │   ├── Meridian.Execution.Sdk.csproj
│   │   └── Models.cs
│   ├── Meridian.FSharp
│   │   ├── Calculations
│   │   │   ├── Aggregations.fs
│   │   │   ├── Imbalance.fs
│   │   │   └── Spread.fs
│   │   ├── Canonicalization
│   │   │   └── MappingRules.fs
│   │   ├── Domain
│   │   │   ├── Integrity.fs
│   │   │   ├── MarketEvents.fs
│   │   │   └── Sides.fs
│   │   ├── Generated
│   │   │   └── Meridian.FSharp.Interop.g.cs
│   │   ├── Interop.fs
│   │   ├── Meridian.FSharp.fsproj
│   │   ├── Pipeline
│   │   │   └── Transforms.fs
│   │   └── Validation
│   │       ├── QuoteValidator.fs
│   │       ├── TradeValidator.fs
│   │       ├── ValidationPipeline.fs
│   │       └── ValidationTypes.fs
│   ├── Meridian.Infrastructure
│   │   ├── Adapters
│   │   │   ├── Alpaca
│   │   │   │   ├── AlpacaConstants.cs
│   │   │   │   ├── AlpacaHistoricalDataProvider.cs
│   │   │   │   ├── AlpacaMarketDataClient.cs
│   │   │   │   └── AlpacaSymbolSearchProviderRefactored.cs
│   │   │   ├── AlphaVantage
│   │   │   │   └── AlphaVantageHistoricalDataProvider.cs
│   │   │   ├── Core
│   │   │   │   ├── Backfill
│   │   │   │   │   ├── BackfillJob.cs
│   │   │   │   │   ├── BackfillJobManager.cs
│   │   │   │   │   ├── BackfillRequestQueue.cs
│   │   │   │   │   ├── BackfillWorkerService.cs
│   │   │   │   │   └── PriorityBackfillQueue.cs
│   │   │   │   ├── BackfillProgressTracker.cs
│   │   │   │   ├── BaseHistoricalDataProvider.cs
│   │   │   │   ├── BaseSymbolSearchProvider.cs
│   │   │   │   ├── CompositeHistoricalDataProvider.cs
│   │   │   │   ├── GapAnalysis
│   │   │   │   │   ├── DataGapAnalyzer.cs
│   │   │   │   │   ├── DataGapRepair.cs
│   │   │   │   │   └── DataQualityMonitor.cs
│   │   │   │   ├── IHistoricalDataProvider.cs
│   │   │   │   ├── ISymbolSearchProvider.cs
│   │   │   │   ├── ProviderFactory.cs
│   │   │   │   ├── ProviderRegistry.cs
│   │   │   │   ├── ProviderServiceExtensions.cs
│   │   │   │   ├── ProviderSubscriptionRanges.cs
│   │   │   │   ├── ProviderTemplate.cs
│   │   │   │   ├── RateLimiting
│   │   │   │   │   ├── ProviderRateLimitTracker.cs
│   │   │   │   │   └── RateLimiter.cs
│   │   │   │   ├── ResponseHandler.cs
│   │   │   │   ├── SymbolResolution
│   │   │   │   │   └── ISymbolResolver.cs
│   │   │   │   ├── SymbolSearchUtility.cs
│   │   │   │   └── WebSocketProviderBase.cs
│   │   │   ├── Failover
│   │   │   │   ├── FailoverAwareMarketDataClient.cs
│   │   │   │   ├── StreamingFailoverRegistry.cs
│   │   │   │   └── StreamingFailoverService.cs
│   │   │   ├── Finnhub
│   │   │   │   ├── FinnhubConstants.cs
│   │   │   │   ├── FinnhubHistoricalDataProvider.cs
│   │   │   │   └── FinnhubSymbolSearchProviderRefactored.cs
│   │   │   ├── InteractiveBrokers
│   │   │   │   ├── ContractFactory.cs
│   │   │   │   ├── EnhancedIBConnectionManager.IBApi.cs
│   │   │   │   ├── EnhancedIBConnectionManager.cs
│   │   │   │   ├── IBApiLimits.cs
│   │   │   │   ├── IBCallbackRouter.cs
│   │   │   │   ├── IBConnectionManager.cs
│   │   │   │   ├── IBHistoricalDataProvider.cs
│   │   │   │   ├── IBMarketDataClient.cs
│   │   │   │   └── IBSimulationClient.cs
│   │   │   ├── NYSE
│   │   │   │   ├── NYSEDataSource.cs
│   │   │   │   ├── NYSEOptions.cs
│   │   │   │   └── NYSEServiceExtensions.cs
│   │   │   ├── NasdaqDataLink
│   │   │   │   └── NasdaqDataLinkHistoricalDataProvider.cs
│   │   │   ├── OpenFigi
│   │   │   │   ├── OpenFigiClient.cs
│   │   │   │   └── OpenFigiSymbolResolver.cs
│   │   │   ├── Polygon
│   │   │   │   ├── PolygonConstants.cs
│   │   │   │   ├── PolygonHistoricalDataProvider.cs
│   │   │   │   ├── PolygonMarketDataClient.cs
│   │   │   │   └── PolygonSymbolSearchProvider.cs
│   │   │   ├── StockSharp
│   │   │   │   ├── Converters
│   │   │   │   │   ├── MessageConverter.cs
│   │   │   │   │   └── SecurityConverter.cs
│   │   │   │   ├── StockSharpConnectorCapabilities.cs
│   │   │   │   ├── StockSharpConnectorFactory.cs
│   │   │   │   ├── StockSharpHistoricalDataProvider.cs
│   │   │   │   ├── StockSharpMarketDataClient.cs
│   │   │   │   └── StockSharpSymbolSearchProvider.cs
│   │   │   ├── Stooq
│   │   │   │   └── StooqHistoricalDataProvider.cs
│   │   │   ├── Synthetic
│   │   │   │   ├── SyntheticHistoricalDataProvider.cs
│   │   │   │   ├── SyntheticMarketDataClient.cs
│   │   │   │   └── SyntheticReferenceDataCatalog.cs
│   │   │   ├── Tiingo
│   │   │   │   └── TiingoHistoricalDataProvider.cs
│   │   │   ├── TwelveData
│   │   │   │   └── TwelveDataHistoricalDataProvider.cs
│   │   │   └── YahooFinance
│   │   │       └── YahooFinanceHistoricalDataProvider.cs
│   │   ├── Contracts
│   │   │   ├── ContractVerificationExtensions.cs
│   │   │   └── ContractVerificationService.cs
│   │   ├── DataSources
│   │   │   ├── DataSourceBase.cs
│   │   │   └── DataSourceConfiguration.cs
│   │   ├── GlobalUsings.cs
│   │   ├── Http
│   │   │   ├── HttpClientConfiguration.cs
│   │   │   └── SharedResiliencePolicies.cs
│   │   ├── Meridian.Infrastructure.csproj
│   │   ├── NoOpMarketDataClient.cs
│   │   ├── Resilience
│   │   │   ├── HttpResiliencePolicy.cs
│   │   │   ├── WebSocketConnectionConfig.cs
│   │   │   ├── WebSocketConnectionManager.cs
│   │   │   └── WebSocketResiliencePolicy.cs
│   │   ├── Shared
│   │   │   ├── ISymbolStateStore.cs
│   │   │   ├── SubscriptionManager.cs
│   │   │   ├── TaskSafetyExtensions.cs
│   │   │   └── WebSocketReconnectionHelper.cs
│   │   └── Utilities
│   │       ├── HttpResponseHandler.cs
│   │       ├── JsonElementExtensions.cs
│   │       └── SymbolNormalization.cs
│   ├── Meridian.Ledger
│   │   ├── GlobalUsings.cs
│   │   ├── IReadOnlyLedger.cs
│   │   ├── JournalEntry.cs
│   │   ├── JournalEntryMetadata.cs
│   │   ├── Ledger.cs
│   │   ├── LedgerAccount.cs
│   │   ├── LedgerAccountSummary.cs
│   │   ├── LedgerAccountType.cs
│   │   ├── LedgerAccounts.cs
│   │   ├── LedgerBalancePoint.cs
│   │   ├── LedgerEntry.cs
│   │   ├── LedgerQuery.cs
│   │   ├── LedgerSnapshot.cs
│   │   ├── LedgerValidationException.cs
│   │   └── Meridian.Ledger.csproj
│   ├── Meridian.Mcp
│   │   ├── GlobalUsings.cs
│   │   ├── Meridian.Mcp.csproj
│   │   ├── Program.cs
│   │   ├── Prompts
│   │   │   ├── CodeReviewPrompts.cs
│   │   │   ├── ProviderPrompts.cs
│   │   │   └── TestWriterPrompts.cs
│   │   ├── Resources
│   │   │   ├── AdrResources.cs
│   │   │   ├── ConventionResources.cs
│   │   │   └── TemplateResources.cs
│   │   ├── Services
│   │   │   └── RepoPathService.cs
│   │   └── Tools
│   │       ├── AdrTools.cs
│   │       ├── AuditTools.cs
│   │       ├── ConventionTools.cs
│   │       ├── KnownErrorTools.cs
│   │       └── ProviderTools.cs
│   ├── Meridian.McpServer
│   │   ├── GlobalUsings.cs
│   │   ├── Meridian.McpServer.csproj
│   │   ├── Program.cs
│   │   ├── Prompts
│   │   │   └── MarketDataPrompts.cs
│   │   ├── Resources
│   │   │   └── MarketDataResources.cs
│   │   └── Tools
│   │       ├── BackfillTools.cs
│   │       ├── ProviderTools.cs
│   │       ├── StorageTools.cs
│   │       └── SymbolTools.cs
│   ├── Meridian.ProviderSdk
│   │   ├── CredentialValidator.cs
│   │   ├── DataSourceAttribute.cs
│   │   ├── DataSourceRegistry.cs
│   │   ├── HistoricalDataCapabilities.cs
│   │   ├── IDataSource.cs
│   │   ├── IHistoricalBarWriter.cs
│   │   ├── IHistoricalDataSource.cs
│   │   ├── IMarketDataClient.cs
│   │   ├── IOptionsChainProvider.cs
│   │   ├── IProviderMetadata.cs
│   │   ├── IProviderModule.cs
│   │   ├── IRealtimeDataSource.cs
│   │   ├── ImplementsAdrAttribute.cs
│   │   ├── Meridian.ProviderSdk.csproj
│   │   └── ProviderHttpUtilities.cs
│   ├── Meridian.Risk
│   │   ├── CompositeRiskValidator.cs
│   │   ├── IRiskRule.cs
│   │   ├── Meridian.Risk.csproj
│   │   └── Rules
│   │       ├── DrawdownCircuitBreaker.cs
│   │       ├── OrderRateThrottle.cs
│   │       └── PositionLimitRule.cs
│   ├── Meridian.Storage
│   │   ├── Archival
│   │   │   ├── ArchivalStorageService.cs
│   │   │   ├── AtomicFileWriter.cs
│   │   │   ├── CompressionProfileManager.cs
│   │   │   ├── SchemaVersionManager.cs
│   │   │   └── WriteAheadLog.cs
│   │   ├── Export
│   │   │   ├── AnalysisExportService.Features.cs
│   │   │   ├── AnalysisExportService.Formats.Arrow.cs
│   │   │   ├── AnalysisExportService.Formats.Parquet.cs
│   │   │   ├── AnalysisExportService.Formats.Xlsx.cs
│   │   │   ├── AnalysisExportService.Formats.cs
│   │   │   ├── AnalysisExportService.IO.cs
│   │   │   ├── AnalysisExportService.cs
│   │   │   ├── AnalysisQualityReport.cs
│   │   │   ├── ExportProfile.cs
│   │   │   ├── ExportRequest.cs
│   │   │   ├── ExportResult.cs
│   │   │   ├── ExportValidator.cs
│   │   │   └── ExportVerificationReport.cs
│   │   ├── GlobalUsings.cs
│   │   ├── Interfaces
│   │   │   ├── IMarketDataStore.cs
│   │   │   ├── ISourceRegistry.cs
│   │   │   ├── IStorageCatalogService.cs
│   │   │   ├── IStoragePolicy.cs
│   │   │   ├── IStorageSink.cs
│   │   │   └── ISymbolRegistryService.cs
│   │   ├── Maintenance
│   │   │   ├── ArchiveMaintenanceModels.cs
│   │   │   ├── ArchiveMaintenanceScheduleManager.cs
│   │   │   ├── IArchiveMaintenanceScheduleManager.cs
│   │   │   ├── IArchiveMaintenanceService.cs
│   │   │   ├── IMaintenanceExecutionHistory.cs
│   │   │   └── ScheduledArchiveMaintenanceService.cs
│   │   ├── Meridian.Storage.csproj
│   │   ├── Packaging
│   │   │   ├── PackageManifest.cs
│   │   │   ├── PackageOptions.cs
│   │   │   ├── PackageResult.cs
│   │   │   ├── PortableDataPackager.Creation.cs
│   │   │   ├── PortableDataPackager.Scripts.Import.cs
│   │   │   ├── PortableDataPackager.Scripts.Sql.cs
│   │   │   ├── PortableDataPackager.Scripts.cs
│   │   │   ├── PortableDataPackager.Validation.cs
│   │   │   └── PortableDataPackager.cs
│   │   ├── Policies
│   │   │   └── JsonlStoragePolicy.cs
│   │   ├── Replay
│   │   │   ├── JsonlReplayer.cs
│   │   │   └── MemoryMappedJsonlReader.cs
│   │   ├── Services
│   │   │   ├── DataLineageService.cs
│   │   │   ├── DataQualityScoringService.cs
│   │   │   ├── DataQualityService.cs
│   │   │   ├── EventBuffer.cs
│   │   │   ├── FileMaintenanceService.cs
│   │   │   ├── FilePermissionsService.cs
│   │   │   ├── LifecyclePolicyEngine.cs
│   │   │   ├── MaintenanceScheduler.cs
│   │   │   ├── MetadataTagService.cs
│   │   │   ├── ParquetConversionService.cs
│   │   │   ├── QuotaEnforcementService.cs
│   │   │   ├── RetentionComplianceReporter.cs
│   │   │   ├── SourceRegistry.cs
│   │   │   ├── StorageCatalogService.cs
│   │   │   ├── StorageChecksumService.cs
│   │   │   ├── StorageSearchService.cs
│   │   │   ├── SymbolRegistryService.cs
│   │   │   └── TierMigrationService.cs
│   │   ├── Sinks
│   │   │   ├── CatalogSyncSink.cs
│   │   │   ├── CompositeSink.cs
│   │   │   ├── JsonlStorageSink.cs
│   │   │   └── ParquetStorageSink.cs
│   │   ├── StorageOptions.cs
│   │   ├── StorageProfiles.cs
│   │   ├── StorageSinkAttribute.cs
│   │   ├── StorageSinkRegistry.cs
│   │   └── Store
│   │       ├── CompositeMarketDataStore.cs
│   │       └── JsonlMarketDataStore.cs
│   ├── Meridian.Strategies
│   │   ├── GlobalUsings.cs
│   │   ├── Interfaces
│   │   │   ├── ILiveStrategy.cs
│   │   │   ├── IStrategyLifecycle.cs
│   │   │   └── IStrategyRepository.cs
│   │   ├── Meridian.Strategies.csproj
│   │   ├── Models
│   │   │   ├── RunType.cs
│   │   │   ├── StrategyRunEntry.cs
│   │   │   └── StrategyStatus.cs
│   │   ├── Promotions
│   │   │   └── BacktestToLivePromoter.cs
│   │   ├── Services
│   │   │   └── StrategyLifecycleManager.cs
│   │   └── Storage
│   │       └── StrategyRunStore.cs
│   ├── Meridian.Ui
│   │   ├── Meridian.Ui.csproj
│   │   ├── Program.cs
│   │   ├── app.manifest
│   │   └── wwwroot
│   │       └── static
│   │           └── dashboard.css
│   ├── Meridian.Ui.Services
│   │   ├── Collections
│   │   │   ├── BoundedObservableCollection.cs
│   │   │   └── CircularBuffer.cs
│   │   ├── Contracts
│   │   │   ├── ConnectionTypes.cs
│   │   │   ├── IAdminMaintenanceService.cs
│   │   │   ├── IArchiveHealthService.cs
│   │   │   ├── IBackgroundTaskSchedulerService.cs
│   │   │   ├── IConfigService.cs
│   │   │   ├── ICredentialService.cs
│   │   │   ├── ILoggingService.cs
│   │   │   ├── IMessagingService.cs
│   │   │   ├── INotificationService.cs
│   │   │   ├── IOfflineTrackingPersistenceService.cs
│   │   │   ├── IPendingOperationsQueueService.cs
│   │   │   ├── IRefreshScheduler.cs
│   │   │   ├── ISchemaService.cs
│   │   │   ├── IStatusService.cs
│   │   │   ├── IThemeService.cs
│   │   │   ├── IWatchlistService.cs
│   │   │   └── NavigationTypes.cs
│   │   ├── GlobalUsings.cs
│   │   ├── Meridian.Ui.Services.csproj
│   │   └── Services
│   │       ├── ActivityFeedService.cs
│   │       ├── AdminMaintenanceModels.cs
│   │       ├── AdminMaintenanceServiceBase.cs
│   │       ├── AdvancedAnalyticsModels.cs
│   │       ├── AdvancedAnalyticsServiceBase.cs
│   │       ├── AlertService.cs
│   │       ├── AnalysisExportService.cs
│   │       ├── AnalysisExportWizardService.cs
│   │       ├── ApiClientService.cs
│   │       ├── ArchiveBrowserService.cs
│   │       ├── ArchiveHealthService.cs
│   │       ├── BackendServiceManagerBase.cs
│   │       ├── BackfillApiService.cs
│   │       ├── BackfillCheckpointService.cs
│   │       ├── BackfillProviderConfigService.cs
│   │       ├── BackfillService.cs
│   │       ├── BatchExportSchedulerService.cs
│   │       ├── ChartingService.cs
│   │       ├── CollectionSessionService.cs
│   │       ├── ColorPalette.cs
│   │       ├── CommandPaletteService.cs
│   │       ├── ConfigService.cs
│   │       ├── ConfigServiceBase.cs
│   │       ├── ConnectionServiceBase.cs
│   │       ├── CredentialService.cs
│   │       ├── DataCalendarService.cs
│   │       ├── DataCompletenessService.cs
│   │       ├── DataQuality
│   │       │   ├── DataQualityApiClient.cs
│   │       │   ├── DataQualityModels.cs
│   │       │   ├── DataQualityPresentationService.cs
│   │       │   ├── DataQualityRefreshService.cs
│   │       │   ├── IDataQualityApiClient.cs
│   │       │   ├── IDataQualityPresentationService.cs
│   │       │   └── IDataQualityRefreshService.cs
│   │       ├── DataQualityRefreshCoordinator.cs
│   │       ├── DataQualityServiceBase.cs
│   │       ├── DataSamplingService.cs
│   │       ├── DesktopJsonOptions.cs
│   │       ├── DiagnosticsService.cs
│   │       ├── ErrorHandlingService.cs
│   │       ├── ErrorMessages.cs
│   │       ├── EventReplayService.cs
│   │       ├── ExportPresetServiceBase.cs
│   │       ├── FixtureDataService.cs
│   │       ├── FixtureModeDetector.cs
│   │       ├── FormValidationRules.cs
│   │       ├── FormatHelpers.cs
│   │       ├── HttpClientConfiguration.cs
│   │       ├── InfoBarConstants.cs
│   │       ├── IntegrityEventsService.cs
│   │       ├── LeanIntegrationService.cs
│   │       ├── LiveDataService.cs
│   │       ├── LoggingService.cs
│   │       ├── LoggingServiceBase.cs
│   │       ├── ManifestService.cs
│   │       ├── NavigationServiceBase.cs
│   │       ├── NotificationService.cs
│   │       ├── NotificationServiceBase.cs
│   │       ├── OAuthRefreshService.cs
│   │       ├── OnboardingTourService.cs
│   │       ├── OperationResult.cs
│   │       ├── OrderBookVisualizationService.cs
│   │       ├── PeriodicRefreshScheduler.cs
│   │       ├── PortablePackagerService.cs
│   │       ├── PortfolioImportService.cs
│   │       ├── ProviderHealthService.cs
│   │       ├── ProviderManagementService.cs
│   │       ├── RetentionAssuranceModels.cs
│   │       ├── ScheduleManagerService.cs
│   │       ├── ScheduledMaintenanceService.cs
│   │       ├── SchemaService.cs
│   │       ├── SchemaServiceBase.cs
│   │       ├── SearchService.cs
│   │       ├── SettingsConfigurationService.cs
│   │       ├── SetupWizardService.cs
│   │       ├── SmartRecommendationsService.cs
│   │       ├── StatusServiceBase.cs
│   │       ├── StorageAnalyticsService.cs
│   │       ├── StorageModels.cs
│   │       ├── StorageOptimizationAdvisorService.cs
│   │       ├── StorageServiceBase.cs
│   │       ├── SymbolGroupService.cs
│   │       ├── SymbolManagementService.cs
│   │       ├── SymbolMappingService.cs
│   │       ├── SystemHealthService.cs
│   │       ├── ThemeServiceBase.cs
│   │       ├── TimeSeriesAlignmentService.cs
│   │       ├── TooltipContent.cs
│   │       ├── WatchlistService.cs
│   │       └── WorkspaceModels.cs
│   ├── Meridian.Ui.Shared
│   │   ├── DtoExtensions.cs
│   │   ├── Endpoints
│   │   │   ├── AdminEndpoints.cs
│   │   │   ├── AnalyticsEndpoints.cs
│   │   │   ├── ApiKeyMiddleware.cs
│   │   │   ├── AuthEndpoints.cs
│   │   │   ├── AuthenticationMode.cs
│   │   │   ├── BackfillEndpoints.cs
│   │   │   ├── BackfillScheduleEndpoints.cs
│   │   │   ├── CalendarEndpoints.cs
│   │   │   ├── CanonicalizationEndpoints.cs
│   │   │   ├── CatalogEndpoints.cs
│   │   │   ├── CheckpointEndpoints.cs
│   │   │   ├── ConfigEndpoints.cs
│   │   │   ├── CronEndpoints.cs
│   │   │   ├── DiagnosticsEndpoints.cs
│   │   │   ├── EndpointHelpers.cs
│   │   │   ├── ExportEndpoints.cs
│   │   │   ├── FailoverEndpoints.cs
│   │   │   ├── HealthEndpoints.cs
│   │   │   ├── HistoricalEndpoints.cs
│   │   │   ├── IBEndpoints.cs
│   │   │   ├── IngestionJobEndpoints.cs
│   │   │   ├── LeanEndpoints.cs
│   │   │   ├── LiveDataEndpoints.cs
│   │   │   ├── LoginSessionMiddleware.cs
│   │   │   ├── MaintenanceScheduleEndpoints.cs
│   │   │   ├── MessagingEndpoints.cs
│   │   │   ├── OptionsEndpoints.cs
│   │   │   ├── PathValidation.cs
│   │   │   ├── ProviderEndpoints.cs
│   │   │   ├── ProviderExtendedEndpoints.cs
│   │   │   ├── ReplayEndpoints.cs
│   │   │   ├── ResilienceEndpoints.cs
│   │   │   ├── SamplingEndpoints.cs
│   │   │   ├── StatusEndpoints.cs
│   │   │   ├── StorageEndpoints.cs
│   │   │   ├── StorageQualityEndpoints.cs
│   │   │   ├── SubscriptionEndpoints.cs
│   │   │   ├── SymbolEndpoints.cs
│   │   │   ├── SymbolMappingEndpoints.cs
│   │   │   └── UiEndpoints.cs
│   │   ├── HtmlTemplateGenerator.Login.cs
│   │   ├── HtmlTemplateGenerator.Scripts.cs
│   │   ├── HtmlTemplateGenerator.Styles.cs
│   │   ├── HtmlTemplateGenerator.cs
│   │   ├── LeanAutoExportService.cs
│   │   ├── LeanSymbolMapper.cs
│   │   ├── LoginSessionService.cs
│   │   ├── Meridian.Ui.Shared.csproj
│   │   └── Services
│   │       ├── BackfillCoordinator.cs
│   │       └── ConfigStore.cs
│   └── Meridian.Wpf
│       ├── App.xaml
│       ├── App.xaml.cs
│       ├── Contracts
│       │   ├── IConnectionService.cs
│       │   └── INavigationService.cs
│       ├── Converters
│       │   └── BoolToVisibilityConverter.cs
│       ├── GlobalUsings.cs
│       ├── MainWindow.xaml
│       ├── MainWindow.xaml.cs
│       ├── Meridian.Wpf.csproj
│       ├── Models
│       │   ├── ActivityLogModels.cs
│       │   ├── AppConfig.cs
│       │   ├── BackfillModels.cs
│       │   ├── DashboardModels.cs
│       │   ├── DataQualityModels.cs
│       │   ├── LeanModels.cs
│       │   ├── LiveDataModels.cs
│       │   ├── NotificationModels.cs
│       │   ├── OrderBookModels.cs
│       │   ├── ProviderHealthModels.cs
│       │   ├── StorageDisplayModels.cs
│       │   └── SymbolsModels.cs
│       ├── README.md
│       ├── Services
│       │   ├── ArchiveHealthService.cs
│       │   ├── BackendServiceManager.cs
│       │   ├── BackgroundTaskSchedulerService.cs
│       │   ├── BacktestService.cs
│       │   ├── BrushRegistry.cs
│       │   ├── ConfigService.cs
│       │   ├── ConnectionService.cs
│       │   ├── ContextMenuService.cs
│       │   ├── CredentialService.cs
│       │   ├── ExportFormat.cs
│       │   ├── ExportPresetService.cs
│       │   ├── FirstRunService.cs
│       │   ├── FormValidationService.cs
│       │   ├── InfoBarService.cs
│       │   ├── KeyboardShortcutService.cs
│       │   ├── LoggingService.cs
│       │   ├── MessagingService.cs
│       │   ├── NavigationService.cs
│       │   ├── NotificationService.cs
│       │   ├── OfflineTrackingPersistenceService.cs
│       │   ├── PendingOperationsQueueService.cs
│       │   ├── RetentionAssuranceService.cs
│       │   ├── SchemaService.cs
│       │   ├── StatusService.cs
│       │   ├── StorageService.cs
│       │   ├── ThemeService.cs
│       │   ├── TooltipService.cs
│       │   ├── TypeForwards.cs
│       │   ├── WatchlistService.cs
│       │   └── WorkspaceService.cs
│       ├── Styles
│       │   ├── Animations.xaml
│       │   ├── AppStyles.xaml
│       │   └── IconResources.xaml
│       ├── ViewModels
│       │   ├── ActivityLogViewModel.cs
│       │   ├── BackfillViewModel.cs
│       │   ├── BacktestViewModel.cs
│       │   ├── BindableBase.cs
│       │   ├── ChartingPageViewModel.cs
│       │   ├── DashboardViewModel.cs
│       │   ├── DataQualityViewModel.cs
│       │   ├── LeanIntegrationViewModel.cs
│       │   ├── LiveDataViewerViewModel.cs
│       │   ├── NotificationCenterViewModel.cs
│       │   ├── OrderBookViewModel.cs
│       │   ├── ProviderHealthViewModel.cs
│       │   ├── ProviderPageModels.cs
│       │   └── SymbolsPageViewModel.cs
│       └── Views
│           ├── ActivityLogPage.xaml
│           ├── ActivityLogPage.xaml.cs
│           ├── AddProviderWizardPage.xaml
│           ├── AddProviderWizardPage.xaml.cs
│           ├── AdminMaintenancePage.xaml
│           ├── AdminMaintenancePage.xaml.cs
│           ├── AdvancedAnalyticsPage.xaml
│           ├── AdvancedAnalyticsPage.xaml.cs
│           ├── AnalysisExportPage.xaml
│           ├── AnalysisExportPage.xaml.cs
│           ├── AnalysisExportWizardPage.xaml
│           ├── AnalysisExportWizardPage.xaml.cs
│           ├── ArchiveHealthPage.xaml
│           ├── ArchiveHealthPage.xaml.cs
│           ├── BackfillPage.xaml
│           ├── BackfillPage.xaml.cs
│           ├── BacktestPage.xaml
│           ├── BacktestPage.xaml.cs
│           ├── ChartingPage.xaml
│           ├── ChartingPage.xaml.cs
│           ├── CollectionSessionPage.xaml
│           ├── CollectionSessionPage.xaml.cs
│           ├── CommandPaletteWindow.xaml
│           ├── CommandPaletteWindow.xaml.cs
│           ├── DashboardPage.xaml
│           ├── DashboardPage.xaml.cs
│           ├── DataBrowserPage.xaml
│           ├── DataBrowserPage.xaml.cs
│           ├── DataCalendarPage.xaml
│           ├── DataCalendarPage.xaml.cs
│           ├── DataExportPage.xaml
│           ├── DataExportPage.xaml.cs
│           ├── DataQualityPage.xaml
│           ├── DataQualityPage.xaml.cs
│           ├── DataSamplingPage.xaml
│           ├── DataSamplingPage.xaml.cs
│           ├── DataSourcesPage.xaml
│           ├── DataSourcesPage.xaml.cs
│           ├── DiagnosticsPage.xaml
│           ├── DiagnosticsPage.xaml.cs
│           ├── EventReplayPage.xaml
│           ├── EventReplayPage.xaml.cs
│           ├── ExportPresetsPage.xaml
│           ├── ExportPresetsPage.xaml.cs
│           ├── HelpPage.xaml
│           ├── HelpPage.xaml.cs
│           ├── IndexSubscriptionPage.xaml
│           ├── IndexSubscriptionPage.xaml.cs
│           ├── KeyboardShortcutsPage.xaml
│           ├── KeyboardShortcutsPage.xaml.cs
│           ├── LeanIntegrationPage.xaml
│           ├── LeanIntegrationPage.xaml.cs
│           ├── LiveDataViewerPage.xaml
│           ├── LiveDataViewerPage.xaml.cs
│           ├── MainPage.xaml
│           ├── MainPage.xaml.cs
│           ├── MessagingHubPage.xaml
│           ├── MessagingHubPage.xaml.cs
│           ├── NotificationCenterPage.xaml
│           ├── NotificationCenterPage.xaml.cs
│           ├── OptionsPage.xaml
│           ├── OptionsPage.xaml.cs
│           ├── OrderBookPage.xaml
│           ├── OrderBookPage.xaml.cs
│           ├── PackageManagerPage.xaml
│           ├── PackageManagerPage.xaml.cs
│           ├── Pages.cs
│           ├── PortfolioImportPage.xaml
│           ├── PortfolioImportPage.xaml.cs
│           ├── ProviderHealthPage.xaml
│           ├── ProviderHealthPage.xaml.cs
│           ├── ProviderPage.xaml
│           ├── ProviderPage.xaml.cs
│           ├── RetentionAssurancePage.xaml
│           ├── RetentionAssurancePage.xaml.cs
│           ├── ScheduleManagerPage.xaml
│           ├── ScheduleManagerPage.xaml.cs
│           ├── ServiceManagerPage.xaml
│           ├── ServiceManagerPage.xaml.cs
│           ├── SettingsPage.xaml
│           ├── SettingsPage.xaml.cs
│           ├── SetupWizardPage.xaml
│           ├── SetupWizardPage.xaml.cs
│           ├── StorageOptimizationPage.xaml
│           ├── StorageOptimizationPage.xaml.cs
│           ├── StoragePage.xaml
│           ├── StoragePage.xaml.cs
│           ├── SymbolMappingPage.xaml
│           ├── SymbolMappingPage.xaml.cs
│           ├── SymbolStoragePage.xaml
│           ├── SymbolStoragePage.xaml.cs
│           ├── SymbolsPage.xaml
│           ├── SymbolsPage.xaml.cs
│           ├── SystemHealthPage.xaml
│           ├── SystemHealthPage.xaml.cs
│           ├── TimeSeriesAlignmentPage.xaml
│           ├── TimeSeriesAlignmentPage.xaml.cs
│           ├── TradingHoursPage.xaml
│           ├── TradingHoursPage.xaml.cs
│           ├── WatchlistPage.xaml
│           ├── WatchlistPage.xaml.cs
│           ├── WelcomePage.xaml
│           ├── WelcomePage.xaml.cs
│           ├── WorkspacePage.xaml
│           └── WorkspacePage.xaml.cs
├── tests
│   ├── Directory.Build.props
│   ├── Meridian.Backtesting.Tests
│   │   ├── FillModelTests.cs
│   │   ├── GlobalUsings.cs
│   │   ├── LedgerQueryTests.cs
│   │   ├── Meridian.Backtesting.Tests.csproj
│   │   ├── SimulatedPortfolioTests.cs
│   │   └── XirrCalculatorTests.cs
│   ├── Meridian.FSharp.Tests
│   │   ├── CalculationTests.fs
│   │   ├── CanonicalizationTests.fs
│   │   ├── DomainTests.fs
│   │   ├── Meridian.FSharp.Tests.fsproj
│   │   ├── PipelineTests.fs
│   │   └── ValidationTests.fs
│   ├── Meridian.McpServer.Tests
│   │   ├── GlobalUsings.cs
│   │   ├── Meridian.McpServer.Tests.csproj
│   │   └── Tools
│   │       ├── BackfillToolsTests.cs
│   │       └── StorageToolsTests.cs
│   ├── Meridian.Tests
│   │   ├── Application
│   │   │   ├── Backfill
│   │   │   │   ├── AdditionalProviderContractTests.cs
│   │   │   │   ├── BackfillStatusStoreTests.cs
│   │   │   │   ├── BackfillWorkerServiceTests.cs
│   │   │   │   ├── CompositeHistoricalDataProviderTests.cs
│   │   │   │   ├── HistoricalProviderContractTests.cs
│   │   │   │   ├── ParallelBackfillServiceTests.cs
│   │   │   │   ├── PriorityBackfillQueueTests.cs
│   │   │   │   ├── RateLimiterTests.cs
│   │   │   │   └── ScheduledBackfillTests.cs
│   │   │   ├── Canonicalization
│   │   │   │   ├── CanonicalizationGoldenFixtureTests.cs
│   │   │   │   └── Fixtures
│   │   │   │       ├── alpaca_trade_extended_hours.json
│   │   │   │       ├── alpaca_trade_odd_lot.json
│   │   │   │       ├── alpaca_trade_regular.json
│   │   │   │       ├── alpaca_xnas_identity.json
│   │   │   │       ├── polygon_trade_extended_hours.json
│   │   │   │       ├── polygon_trade_odd_lot.json
│   │   │   │       ├── polygon_trade_regular.json
│   │   │   │       └── polygon_xnas_identity.json
│   │   │   ├── Commands
│   │   │   │   ├── CliArgumentsTests.cs
│   │   │   │   ├── CommandDispatcherTests.cs
│   │   │   │   ├── DryRunCommandTests.cs
│   │   │   │   ├── HelpCommandTests.cs
│   │   │   │   ├── PackageCommandsTests.cs
│   │   │   │   ├── SelfTestCommandTests.cs
│   │   │   │   ├── SymbolCommandsTests.cs
│   │   │   │   └── ValidateConfigCommandTests.cs
│   │   │   ├── Composition
│   │   │   │   └── Startup
│   │   │   │       └── SharedStartupBootstrapperTests.cs
│   │   │   ├── Config
│   │   │   │   ├── ConfigJsonSchemaGeneratorTests.cs
│   │   │   │   ├── ConfigValidationPipelineTests.cs
│   │   │   │   ├── ConfigValidatorTests.cs
│   │   │   │   └── ConfigurationUnificationTests.cs
│   │   │   ├── Credentials
│   │   │   │   ├── CredentialStatusTests.cs
│   │   │   │   ├── CredentialTestingServiceTests.cs
│   │   │   │   └── OAuthTokenTests.cs
│   │   │   ├── Indicators
│   │   │   │   └── TechnicalIndicatorServiceTests.cs
│   │   │   ├── Monitoring
│   │   │   │   ├── BackpressureAlertServiceTests.cs
│   │   │   │   ├── BadTickFilterTests.cs
│   │   │   │   ├── DataQuality
│   │   │   │   │   ├── DataFreshnessSlaMonitorTests.cs
│   │   │   │   │   ├── DataQualityTests.cs
│   │   │   │   │   └── LiquidityProfileTests.cs
│   │   │   │   ├── ErrorRingBufferTests.cs
│   │   │   │   ├── PriceContinuityCheckerTests.cs
│   │   │   │   ├── PrometheusMetricsTests.cs
│   │   │   │   ├── ProviderDegradationScorerTests.cs
│   │   │   │   ├── ProviderLatencyServiceTests.cs
│   │   │   │   ├── SchemaValidationServiceTests.cs
│   │   │   │   ├── SloDefinitionRegistryTests.cs
│   │   │   │   ├── SpreadMonitorTests.cs
│   │   │   │   ├── TickSizeValidatorTests.cs
│   │   │   │   └── TracedEventMetricsTests.cs
│   │   │   ├── Pipeline
│   │   │   │   ├── BackfillProgressTrackerTests.cs
│   │   │   │   ├── BackpressureSignalTests.cs
│   │   │   │   ├── CompositePublisherTests.cs
│   │   │   │   ├── DroppedEventAuditTrailTests.cs
│   │   │   │   ├── DualPathEventPipelineTests.cs
│   │   │   │   ├── EventPipelineMetricsTests.cs
│   │   │   │   ├── EventPipelineTests.cs
│   │   │   │   ├── FSharpEventValidatorTests.cs
│   │   │   │   ├── GoldenMasterPipelineReplayTests.cs
│   │   │   │   ├── HotPathBatchSerializerTests.cs
│   │   │   │   ├── IngestionJobServiceTests.cs
│   │   │   │   ├── IngestionJobTests.cs
│   │   │   │   ├── MarketDataClientFactoryTests.cs
│   │   │   │   ├── SpscRingBufferTests.cs
│   │   │   │   └── WalEventPipelineTests.cs
│   │   │   └── Services
│   │   │       ├── CanonicalizingPublisherTests.cs
│   │   │       ├── CliModeResolverTests.cs
│   │   │       ├── ConditionCodeMapperTests.cs
│   │   │       ├── ConfigurationPresetsTests.cs
│   │   │       ├── ConfigurationServiceTests.cs
│   │   │       ├── CronExpressionParserTests.cs
│   │   │       ├── DataQuality
│   │   │       │   ├── AnomalyDetectorTests.cs
│   │   │       │   ├── CompletenessScoreCalculatorTests.cs
│   │   │       │   ├── GapAnalyzerTests.cs
│   │   │       │   └── SequenceErrorTrackerTests.cs
│   │   │       ├── ErrorCodeMappingTests.cs
│   │   │       ├── EventCanonicalizerTests.cs
│   │   │       ├── GracefulShutdownTests.cs
│   │   │       ├── OperationalSchedulerTests.cs
│   │   │       ├── OptionsChainServiceTests.cs
│   │   │       ├── PreflightCheckerTests.cs
│   │   │       ├── TradingCalendarTests.cs
│   │   │       └── VenueMicMapperTests.cs
│   │   ├── Architecture
│   │   │   └── LayerBoundaryTests.cs
│   │   ├── Domain
│   │   │   ├── Collectors
│   │   │   │   ├── L3OrderBookCollectorTests.cs
│   │   │   │   ├── LiveDataAccessTests.cs
│   │   │   │   ├── MarketDepthCollectorTests.cs
│   │   │   │   ├── OptionDataCollectorTests.cs
│   │   │   │   ├── QuoteCollectorTests.cs
│   │   │   │   └── TradeDataCollectorTests.cs
│   │   │   ├── Models
│   │   │   │   ├── AdjustedHistoricalBarTests.cs
│   │   │   │   ├── AggregateBarTests.cs
│   │   │   │   ├── BboQuotePayloadTests.cs
│   │   │   │   ├── EffectiveSymbolTests.cs
│   │   │   │   ├── GreeksSnapshotTests.cs
│   │   │   │   ├── HistoricalBarTests.cs
│   │   │   │   ├── OpenInterestUpdateTests.cs
│   │   │   │   ├── OptionChainSnapshotTests.cs
│   │   │   │   ├── OptionContractSpecTests.cs
│   │   │   │   ├── OptionQuoteTests.cs
│   │   │   │   ├── OptionTradeTests.cs
│   │   │   │   ├── OrderBookLevelTests.cs
│   │   │   │   ├── OrderEventPayloadTests.cs
│   │   │   │   └── TradeModelTests.cs
│   │   │   └── StrongDomainTypeTests.cs
│   │   ├── Execution
│   │   │   └── PaperTradingGatewayTests.cs
│   │   ├── GlobalUsings.cs
│   │   ├── Infrastructure
│   │   │   ├── DataSources
│   │   │   │   └── CredentialConfigTests.cs
│   │   │   ├── Providers
│   │   │   │   ├── AlpacaCredentialAndReconnectTests.cs
│   │   │   │   ├── AlpacaMessageParsingTests.cs
│   │   │   │   ├── AlpacaQuotePipelineGoldenTests.cs
│   │   │   │   ├── AlpacaQuoteRoutingTests.cs
│   │   │   │   ├── BackfillRetryAfterTests.cs
│   │   │   │   ├── FailoverAwareMarketDataClientTests.cs
│   │   │   │   ├── Fixtures
│   │   │   │   │   └── InteractiveBrokers
│   │   │   │   │       ├── ib_order_limit_buy_day.json
│   │   │   │   │       ├── ib_order_limit_sell_fok.json
│   │   │   │   │       ├── ib_order_market_sell_gtc.json
│   │   │   │   │       ├── ib_order_moc_sell_day.json
│   │   │   │   │       └── ib_order_stop_buy_ioc.json
│   │   │   │   ├── FreeProviderContractTests.cs
│   │   │   │   ├── HistoricalDataProviderContractTests.cs
│   │   │   │   ├── IBOrderSampleTests.cs
│   │   │   │   ├── IBSimulationClientContractTests.cs
│   │   │   │   ├── IBSimulationClientTests.cs
│   │   │   │   ├── MarketDataClientContractTests.cs
│   │   │   │   ├── NYSEMessageParsingTests.cs
│   │   │   │   ├── PolygonMarketDataClientTests.cs
│   │   │   │   ├── PolygonMessageParsingTests.cs
│   │   │   │   ├── PolygonSubscriptionTests.cs
│   │   │   │   ├── ProviderResilienceTests.cs
│   │   │   │   ├── StockSharpMessageConversionTests.cs
│   │   │   │   ├── StockSharpSubscriptionTests.cs
│   │   │   │   ├── StreamingFailoverServiceTests.cs
│   │   │   │   └── SyntheticMarketDataProviderTests.cs
│   │   │   ├── Resilience
│   │   │   │   ├── WebSocketConnectionManagerTests.cs
│   │   │   │   └── WebSocketResiliencePolicyTests.cs
│   │   │   └── Shared
│   │   │       ├── SymbolNormalizationTests.cs
│   │   │       └── TempDirectoryFixture.cs
│   │   ├── Integration
│   │   │   ├── ConfigurableTickerDataCollectionTests.cs
│   │   │   ├── ConnectionRetryIntegrationTests.cs
│   │   │   ├── EndpointStubDetectionTests.cs
│   │   │   ├── EndpointTests
│   │   │   │   ├── AuthEndpointTests.cs
│   │   │   │   ├── BackfillEndpointTests.cs
│   │   │   │   ├── CatalogEndpointTests.cs
│   │   │   │   ├── ConfigEndpointTests.cs
│   │   │   │   ├── EndpointIntegrationTestBase.cs
│   │   │   │   ├── EndpointTestCollection.cs
│   │   │   │   ├── EndpointTestFixture.cs
│   │   │   │   ├── FailoverEndpointTests.cs
│   │   │   │   ├── HealthEndpointTests.cs
│   │   │   │   ├── HistoricalEndpointTests.cs
│   │   │   │   ├── IBEndpointTests.cs
│   │   │   │   ├── LeanEndpointTests.cs
│   │   │   │   ├── LiveDataEndpointTests.cs
│   │   │   │   ├── MaintenanceEndpointTests.cs
│   │   │   │   ├── NegativePathEndpointTests.cs
│   │   │   │   ├── OptionsEndpointTests.cs
│   │   │   │   ├── ProviderEndpointTests.cs
│   │   │   │   ├── QualityDropsEndpointTests.cs
│   │   │   │   ├── QualityEndpointContractTests.cs
│   │   │   │   ├── ResponseSchemaSnapshotTests.cs
│   │   │   │   ├── ResponseSchemaValidationTests.cs
│   │   │   │   ├── StatusEndpointTests.cs
│   │   │   │   ├── StorageEndpointTests.cs
│   │   │   │   └── SymbolEndpointTests.cs
│   │   │   ├── FixtureProviderTests.cs
│   │   │   ├── GracefulShutdownIntegrationTests.cs
│   │   │   └── YahooFinancePcgPreferredIntegrationTests.cs
│   │   ├── Meridian.Tests.csproj
│   │   ├── ProviderSdk
│   │   │   ├── CredentialValidatorTests.cs
│   │   │   ├── DataSourceAttributeTests.cs
│   │   │   ├── DataSourceRegistryTests.cs
│   │   │   └── ExceptionTypeTests.cs
│   │   ├── Serialization
│   │   │   └── HighPerformanceJsonTests.cs
│   │   ├── Storage
│   │   │   ├── AnalysisExportServiceTests.cs
│   │   │   ├── AtomicFileWriterTests.cs
│   │   │   ├── CanonicalSymbolRegistryTests.cs
│   │   │   ├── CompositeSinkTests.cs
│   │   │   ├── DataLineageServiceTests.cs
│   │   │   ├── DataQualityScoringServiceTests.cs
│   │   │   ├── DataValidatorTests.cs
│   │   │   ├── EventBufferTests.cs
│   │   │   ├── ExportValidatorTests.cs
│   │   │   ├── FilePermissionsServiceTests.cs
│   │   │   ├── JsonlBatchWriteTests.cs
│   │   │   ├── LifecyclePolicyEngineTests.cs
│   │   │   ├── MemoryMappedJsonlReaderTests.cs
│   │   │   ├── MetadataTagServiceTests.cs
│   │   │   ├── ParquetConversionServiceTests.cs
│   │   │   ├── PortableDataPackagerTests.cs
│   │   │   ├── QuotaEnforcementServiceTests.cs
│   │   │   ├── StorageCatalogServiceTests.cs
│   │   │   ├── StorageChecksumServiceTests.cs
│   │   │   ├── StorageOptionsDefaultsTests.cs
│   │   │   ├── StorageSinkRegistryTests.cs
│   │   │   ├── SymbolRegistryServiceTests.cs
│   │   │   ├── WriteAheadLogCorruptionModeTests.cs
│   │   │   ├── WriteAheadLogFuzzTests.cs
│   │   │   └── WriteAheadLogTests.cs
│   │   ├── SymbolSearch
│   │   │   ├── OpenFigiClientTests.cs
│   │   │   └── SymbolSearchServiceTests.cs
│   │   ├── TestCollections.cs
│   │   ├── TestData
│   │   │   └── Golden
│   │   │       └── alpaca-quote-pipeline.json
│   │   └── TestHelpers
│   │       ├── PolygonStubClient.cs
│   │       └── TestMarketEventPublisher.cs
│   ├── Meridian.Ui.Tests
│   │   ├── Collections
│   │   │   ├── BoundedObservableCollectionTests.cs
│   │   │   └── CircularBufferTests.cs
│   │   ├── Meridian.Ui.Tests.csproj
│   │   ├── README.md
│   │   └── Services
│   │       ├── ActivityFeedServiceTests.cs
│   │       ├── AlertServiceTests.cs
│   │       ├── AnalysisExportServiceBaseTests.cs
│   │       ├── ApiClientServiceTests.cs
│   │       ├── ArchiveBrowserServiceTests.cs
│   │       ├── BackendServiceManagerBaseTests.cs
│   │       ├── BackfillApiServiceTests.cs
│   │       ├── BackfillCheckpointServiceTests.cs
│   │       ├── BackfillProviderConfigServiceTests.cs
│   │       ├── BackfillServiceTests.cs
│   │       ├── ChartingServiceTests.cs
│   │       ├── CollectionSessionServiceTests.cs
│   │       ├── CommandPaletteServiceTests.cs
│   │       ├── ConfigServiceBaseTests.cs
│   │       ├── ConfigServiceTests.cs
│   │       ├── ConnectionServiceBaseTests.cs
│   │       ├── CredentialServiceTests.cs
│   │       ├── DataCalendarServiceTests.cs
│   │       ├── DataCompletenessServiceTests.cs
│   │       ├── DataQualityRefreshCoordinatorTests.cs
│   │       ├── DataQualityServiceBaseTests.cs
│   │       ├── DataSamplingServiceTests.cs
│   │       ├── DiagnosticsServiceTests.cs
│   │       ├── ErrorHandlingServiceTests.cs
│   │       ├── EventReplayServiceTests.cs
│   │       ├── FixtureDataServiceTests.cs
│   │       ├── FormValidationServiceTests.cs
│   │       ├── IntegrityEventsServiceTests.cs
│   │       ├── LeanIntegrationServiceTests.cs
│   │       ├── LiveDataServiceTests.cs
│   │       ├── LoggingServiceBaseTests.cs
│   │       ├── ManifestServiceTests.cs
│   │       ├── NotificationServiceBaseTests.cs
│   │       ├── NotificationServiceTests.cs
│   │       ├── OrderBookVisualizationServiceTests.cs
│   │       ├── PortfolioImportServiceTests.cs
│   │       ├── ProviderHealthServiceTests.cs
│   │       ├── ProviderManagementServiceTests.cs
│   │       ├── ScheduleManagerServiceTests.cs
│   │       ├── ScheduledMaintenanceServiceTests.cs
│   │       ├── SchemaServiceTests.cs
│   │       ├── SearchServiceTests.cs
│   │       ├── SmartRecommendationsServiceTests.cs
│   │       ├── StatusServiceBaseTests.cs
│   │       ├── StorageAnalyticsServiceTests.cs
│   │       ├── SymbolGroupServiceTests.cs
│   │       ├── SymbolManagementServiceTests.cs
│   │       ├── SymbolMappingServiceTests.cs
│   │       ├── SystemHealthServiceTests.cs
│   │       ├── TimeSeriesAlignmentServiceTests.cs
│   │       └── WatchlistServiceTests.cs
│   ├── Meridian.Wpf.Tests
│   │   ├── GlobalUsings.cs
│   │   ├── Meridian.Wpf.Tests.csproj
│   │   ├── Services
│   │   │   ├── AdminMaintenanceServiceTests.cs
│   │   │   ├── BackgroundTaskSchedulerServiceTests.cs
│   │   │   ├── ConfigServiceTests.cs
│   │   │   ├── ConnectionServiceTests.cs
│   │   │   ├── ExportPresetServiceTests.cs
│   │   │   ├── FirstRunServiceTests.cs
│   │   │   ├── InfoBarServiceTests.cs
│   │   │   ├── KeyboardShortcutServiceTests.cs
│   │   │   ├── MessagingServiceTests.cs
│   │   │   ├── NavigationServiceTests.cs
│   │   │   ├── NotificationServiceTests.cs
│   │   │   ├── OfflineTrackingPersistenceServiceTests.cs
│   │   │   ├── PendingOperationsQueueServiceTests.cs
│   │   │   ├── RetentionAssuranceServiceTests.cs
│   │   │   ├── StatusServiceTests.cs
│   │   │   ├── StorageServiceTests.cs
│   │   │   ├── TooltipServiceTests.cs
│   │   │   ├── WatchlistServiceTests.cs
│   │   │   └── WorkspaceServiceTests.cs
│   │   └── ViewModels
│   │       └── DataQualityViewModelCharacterizationTests.cs
│   ├── coverlet.runsettings
│   ├── scripts
│   │   └── setup-verification.sh
│   ├── setup-script-tests.md
│   └── xunit.runner.json
└── tree.bak

317 directories, 1829 files
```
<!-- readme-tree end -->

</details>

## License

See LICENSE file for details.

## Contributing

Contributions are welcome! Please see the [CLAUDE.md](CLAUDE.md) for architecture details and coding guidelines before submitting pull requests.
