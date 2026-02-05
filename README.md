# Market Data Collector

A high-performance, cross-platform market data collection system for real-time and historical market microstructure data.

[![.NET](https://img.shields.io/badge/.NET-9.0-blue)](https://dotnet.microsoft.com/)
[![C#](https://img.shields.io/badge/C%23-13-blue)](https://docs.microsoft.com/en-us/dotnet/csharp/)
[![F#](https://img.shields.io/badge/F%23-8.0-blue)](https://fsharp.org/)
[![Docker](https://img.shields.io/badge/Docker-Ready-blue)](https://www.docker.com/)
[![License](https://img.shields.io/badge/license-See%20LICENSE-green)](LICENSE)

[![Build and Release](https://github.com/rodoHasArrived/Market-Data-Collector/actions/workflows/dotnet-desktop.yml/badge.svg)](https://github.com/rodoHasArrived/Market-Data-Collector/actions/workflows/dotnet-desktop.yml)
[![CodeQL](https://github.com/rodoHasArrived/Market-Data-Collector/actions/workflows/codeql-analysis.yml/badge.svg)](https://github.com/rodoHasArrived/Market-Data-Collector/actions/workflows/codeql-analysis.yml)
[![Docker Build](https://github.com/rodoHasArrived/Market-Data-Collector/actions/workflows/docker-build.yml/badge.svg)](https://github.com/rodoHasArrived/Market-Data-Collector/actions/workflows/docker-build.yml)
[![Code Quality](https://github.com/rodoHasArrived/Market-Data-Collector/actions/workflows/code-quality.yml/badge.svg)](https://github.com/rodoHasArrived/Market-Data-Collector/actions/workflows/code-quality.yml)

**Status**: Development / Pilot Ready

---

## What This Project Does

Market Data Collector is a complete solution for **building your own market data archive**. It connects to financial data providers, captures market data in real-time, and stores everything locally so you have full ownership and offline access to your data.

### Core Capabilities

| Capability | What It Does For You |
|------------|---------------------|
| **📡 Real-Time Streaming** | Capture live trades, quotes, and order book depth as they happen from Interactive Brokers, Alpaca, NYSE, Polygon, or StockSharp |
| **📥 Historical Backfill** | Download years of historical price data from 10+ providers (Yahoo Finance, Tiingo, Polygon, Alpaca, and more) with automatic failover |
| **💾 Local Data Storage** | Own your data—everything is stored in structured JSONL or Parquet files on your machine, not locked in a vendor's cloud |
| **🔍 Data Quality Monitoring** | Automatic validation catches missing data, sequence gaps, and anomalies before they corrupt your analysis |
| **📦 Data Packaging** | Export and package your data for sharing, backup, or use in other tools |
| **📊 Live Dashboards** | Monitor collection status, throughput, and data quality through a web dashboard or Windows desktop app |
| **🔬 Backtesting Integration** | Feed your collected data directly into QuantConnect Lean for algorithmic strategy development |

### Who Is This For?

- **Quantitative researchers** who need tick-level market microstructure data for analysis
- **Algorithmic traders** building strategies that require historical and real-time market data
- **Data engineers** who want to build a reliable market data pipeline
- **Hobbyist traders** who want to collect and own their own market data archive
- **Students and academics** studying market microstructure, price formation, or trading systems

### The Problem It Solves

Commercial market data is expensive, vendor APIs change without notice, and cloud-only solutions mean you never truly own your data. Market Data Collector gives you:

1. **Data independence** — Switch providers without losing your archive or rewriting code
2. **Cost control** — Use free-tier APIs strategically, pay only for premium data you actually need
3. **Reliability** — Automatic reconnection, failover between providers, and data integrity checks
4. **Flexibility** — Collect exactly the symbols and data types you need, store them how you want

---

## Installation

### Golden Path (Recommended)

Use the installation orchestrator script for all setups. It keeps Docker and native installs consistent across platforms.

```bash
# Interactive installer (Docker or Native)
./scripts/install/install.sh

# Or choose a mode explicitly
./scripts/install/install.sh --docker
./scripts/install/install.sh --native
```

Access the dashboard at **http://localhost:8080**

### Windows Installation

The PowerShell installer mirrors the same workflow on Windows.

```powershell
# Interactive installation
.\scripts\install\install.ps1

# Or specify mode directly
.\scripts\install\install.ps1 -Mode Docker
.\scripts\install\install.ps1 -Mode Native
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

### Windows Desktop App Install

**Two Options Available:**

1. **WPF Desktop App (Recommended)** - Modern WPF application with maximum Windows stability
   - Works on Windows 7+
   - Simple .exe deployment
   - Direct assembly references (no WinRT limitations)
   - See [WPF README](src/MarketDataCollector.Wpf/README.md) for details

2. **UWP Desktop App (Legacy)** - Windows 10+ only
   - MSIX packaging required
   - Windows 10 build 19041+ required
   - See instructions below

**WPF Installation:**
```bash
# Build from source
dotnet build src/MarketDataCollector.Wpf/MarketDataCollector.Wpf.csproj -c Release

# Run
dotnet run --project src/MarketDataCollector.Wpf/MarketDataCollector.Wpf.csproj
```

**UWP Installation (AppInstaller):**
1. Download the `.appinstaller` file from the release assets.
2. Double-click it to launch App Installer.
3. Select **Install** to complete setup.

**Install (MSIX)**
1. Download the MSIX bundle (`.msix`/`.msixbundle`) from the release assets.
2. Double-click the file and follow the prompts.

**CI artifacts (preview builds)**
- GitHub Actions Desktop App Build workflow artifacts include an MSIX archive named `MarketDataCollector.Desktop-msix-x64.zip` for tag/manual runs. Download it from the workflow run artifacts and extract to get the MSIX package.  
  https://github.com/rodoHasArrived/Market-Data-Collector/actions/workflows/desktop-app.yml

**Upgrade**
- Re-open the latest `.appinstaller` file; App Installer detects updates automatically.
- For MSIX, install the newer package version over the existing one.

**Uninstall**
- Open **Settings → Apps → Installed apps**, select **Market Data Collector Desktop**, and choose **Uninstall**.

**Troubleshooting install**
- **Missing signing certificate**: Install the signing cert or use a release-signed package; unsigned MSIX packages cannot be installed.
- **Blocked package**: If Windows SmartScreen blocks the installer, choose **More info → Run anyway**, or unblock the downloaded file in **Properties**.

---

## Technical Overview

Market Data Collector is built on **.NET 9.0** using **C# 13** and **F# 8.0**. It uses a modular, event-driven architecture with bounded channels for high-throughput data processing. The system supports deployment as a single self-contained executable, a Docker container, or a systemd service.

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
- **Native Windows apps**: WPF desktop application (recommended) and a legacy UWP app for Windows-only configuration and monitoring
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
git clone https://github.com/rodoHasArrived/Market-Data-Collector.git
cd Market-Data-Collector

# Run the interactive configuration wizard (recommended for new users)
dotnet run --project src/MarketDataCollector/MarketDataCollector.csproj -- --wizard
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
dotnet run --project src/MarketDataCollector/MarketDataCollector.csproj -- --auto-config

# Check what providers are available
dotnet run --project src/MarketDataCollector/MarketDataCollector.csproj -- --detect-providers

# Validate your API credentials
dotnet run --project src/MarketDataCollector/MarketDataCollector.csproj -- --validate-credentials
```

### Manual Setup

```bash
# Clone the repository
git clone https://github.com/rodoHasArrived/Market-Data-Collector.git
cd Market-Data-Collector

# Copy the sample settings and edit as needed
cp config/appsettings.sample.json config/appsettings.json

# Option 1: Launch the web dashboard (serves HTML + Prometheus + JSON status)
dotnet run --project src/MarketDataCollector/MarketDataCollector.csproj -- --ui --watch-config --http-port 8080

# Option 2: Launch the UWP desktop application (Windows only)
dotnet run --project src/MarketDataCollector.Uwp/MarketDataCollector.Uwp.csproj

# Run smoke test (no provider connectivity required)
dotnet run --project src/MarketDataCollector/MarketDataCollector.csproj

# Run self-tests
dotnet run --project src/MarketDataCollector/MarketDataCollector.csproj -- --selftest

# Historical backfill with overrides
dotnet run --project src/MarketDataCollector/MarketDataCollector.csproj -- \
  --backfill --backfill-provider stooq --backfill-symbols SPY,AAPL \
  --backfill-from 2024-01-01 --backfill-to 2024-01-05
```

Access the monitoring dashboard at `http://localhost:8080`, JSON status at `http://localhost:8080/status`, and Prometheus metrics at `http://localhost:8080/metrics`.

## Documentation

Comprehensive documentation is available in the `docs/` directory:

- **[docs/USAGE.md](docs/USAGE.md)** - Product overview, CLI/UI usage, and configuration highlights
- **[docs/HELP.md](docs/HELP.md)** - Comprehensive user guide with troubleshooting and FAQ
- **[docs/guides/getting-started.md](docs/guides/getting-started.md)** - End-to-end setup for local development
- **[docs/guides/configuration.md](docs/guides/configuration.md)** - Detailed explanation of every setting including backfill
- **[docs/architecture/overview.md](docs/architecture/overview.md)** - System architecture and design
- **[docs/operations/operator-runbook.md](docs/operations/operator-runbook.md)** - Operations guide and production deployment
- **[docs/architecture/domains.md](docs/architecture/domains.md)** - Event contracts and domain models
- **[docs/architecture/c4-diagrams.md](docs/architecture/c4-diagrams.md)** - System diagrams
- **[docs/integrations/lean-integration.md](docs/integrations/lean-integration.md)** - QuantConnect Lean integration guide and examples
- **[docs/architecture/storage-design.md](docs/architecture/storage-design.md)** - Advanced storage organization and data management strategies
- **[docs/development/build-observability.md](docs/development/build-observability.md)** - Build observability toolkit and CLI reference

### AI Assistant Guides
- **[CLAUDE.md](CLAUDE.md)** - Main AI assistant guide
- **[docs/ai-assistants/](docs/ai-assistants/)** - Specialized guides for subsystems (F#, providers, storage, testing)

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
| Finnhub | Yes | Daily bars | 60/min |
| Alpha Vantage | Yes | Daily bars | 5/min |
| Nasdaq Data Link | Limited | Various | Varies |

Configure fallback chains in `appsettings.json` under `Backfill.ProviderPriority` for automatic failover between providers.

### Symbol Search Providers
- **Alpaca** - Symbol search with exchange info
- **Finnhub** - Global symbol search
- **Polygon** - Ticker search with market data
- **OpenFIGI** - FIGI-based instrument resolution

## Lean Engine Integration

Market Data Collector now integrates with **QuantConnect's Lean Engine**, enabling sophisticated algorithmic trading strategies:

- **Custom Data Types**: Trade and quote data exposed as Lean `BaseData` types
- **Backtesting Support**: Use collected tick data for algorithm backtesting
- **Data Provider**: Custom `IDataProvider` implementation for JSONL files
- **Sample Algorithms**: Ready-to-use examples for microstructure-aware trading

See [`src/MarketDataCollector/Integrations/Lean/README.md`](src/MarketDataCollector/Integrations/Lean/README.md) for integration details and examples.

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
sudo cp deploy/systemd/marketdatacollector.service /etc/systemd/system/

# Enable and start
sudo systemctl enable marketdatacollector
sudo systemctl start marketdatacollector
```

### Environment Variables

API credentials can be set via environment variables:
```bash
export ALPACA__KEYID=your-key-id
export ALPACA__SECRETKEY=your-secret-key
```

## CI/CD and Automation

The repository includes 21 comprehensive GitHub Actions workflows for automated testing, security, and deployment:

- **🔨 Build & Release** - Automated builds and cross-platform releases
- **🔒 CodeQL Analysis** - Security vulnerability scanning (weekly + on changes)
- **📦 Docker Build** - Automated container image builds to GitHub Container Registry
- **⚡ Performance Benchmarks** - Track performance metrics over time
- **✨ Code Quality** - Linting, formatting, and link checking
- **🏷️ Auto Labeling** - Intelligent PR and issue labeling
- **🔍 Dependency Review** - Security checks for dependencies in PRs
- **🗑️ Stale Management** - Automatic issue/PR lifecycle management
- **📊 Build Observability** - Build metrics and diagnostic capture
- **📖 Documentation Auto-Update** - Automatically update docs on provider changes
- **🔄 Documentation Sync** - Keep documentation structure in sync with codebase

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
docker pull ghcr.io/rodohasarrived/market-data-collector:latest

# Run the container
docker run -d -p 8080:8080 \
  -v $(pwd)/data:/app/data \
  -v $(pwd)/appsettings.json:/app/appsettings.json:ro \
  ghcr.io/rodohasarrived/market-data-collector:latest
```

## Repository Structure

**501 source files** | **489 C#** | **12 F#** | **48 test files** | **66 documentation files**

```
Market-Data-Collector/
├── .github/              # CI/CD workflows (21), AI prompts, Dependabot
├── docs/                 # Documentation (61 files), ADRs, AI assistant guides
├── scripts/              # Install, publish, run, and diagnostic scripts
├── deploy/               # Docker, systemd, and monitoring configs
├── config/               # Configuration files (appsettings.json)
├── build-system/         # Python build tooling and diagnostics
├── tools/                # Development tools (DocGenerator)
├── src/
│   ├── MarketDataCollector/        # Core application (entry point)
│   │   ├── Domain/                 # Business logic, collectors, events, models
│   │   ├── Infrastructure/         # Provider implementations (~50 files)
│   │   ├── Storage/                # Data persistence (~35 files)
│   │   └── Application/            # Startup, config, services (~90 files)
│   ├── MarketDataCollector.FSharp/ # F# domain models (12 files)
│   ├── MarketDataCollector.Contracts/ # Shared DTOs and contracts
│   ├── MarketDataCollector.Ui/     # Web dashboard (10 files)
│   └── MarketDataCollector.Uwp/    # Windows desktop app (WinUI 3)
├── tests/                # C# and F# test projects (50 files)
├── benchmarks/           # Performance benchmarks (BenchmarkDotNet)
├── MarketDataCollector.sln
├── Makefile              # Build automation (65 targets)
└── CLAUDE.md             # AI assistant guide
```

## License

See LICENSE file for details.

## Contributing

Contributions are welcome! Please see the [CLAUDE.md](CLAUDE.md) for architecture details and coding guidelines before submitting pull requests.
