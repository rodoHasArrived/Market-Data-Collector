# Market Data Collector

A high-performance, cross-platform market data collection system for real-time and historical market microstructure data.

[![.NET](https://img.shields.io/badge/.NET-9.0-blue)](https://dotnet.microsoft.com/)
[![C#](https://img.shields.io/badge/C%23-11-blue)](https://docs.microsoft.com/en-us/dotnet/csharp/)
[![F#](https://img.shields.io/badge/F%23-8.0-blue)](https://fsharp.org/)
[![Docker](https://img.shields.io/badge/Docker-Ready-blue)](https://www.docker.com/)
[![License](https://img.shields.io/badge/license-See%20LICENSE-green)](LICENSE)

[![Build and Release](https://github.com/rodoHasArrived/Market-Data-Collector/actions/workflows/dotnet-desktop.yml/badge.svg)](https://github.com/rodoHasArrived/Market-Data-Collector/actions/workflows/dotnet-desktop.yml)
[![CodeQL](https://github.com/rodoHasArrived/Market-Data-Collector/actions/workflows/codeql-analysis.yml/badge.svg)](https://github.com/rodoHasArrived/Market-Data-Collector/actions/workflows/codeql-analysis.yml)
[![Docker Build](https://github.com/rodoHasArrived/Market-Data-Collector/actions/workflows/docker-build.yml/badge.svg)](https://github.com/rodoHasArrived/Market-Data-Collector/actions/workflows/docker-build.yml)
[![Code Quality](https://github.com/rodoHasArrived/Market-Data-Collector/actions/workflows/code-quality.yml/badge.svg)](https://github.com/rodoHasArrived/Market-Data-Collector/actions/workflows/code-quality.yml)

**Status**: Production Ready | **Version**: 1.6.1 | **Last Updated**: 2026-01-29

---

## Installation

### Option 1: Docker (Recommended)

```bash
# Quick install with interactive script
./scripts/install/install.sh --docker

# Or manually with Docker Compose
cp config/appsettings.sample.json config/appsettings.json
make docker
```

Access the dashboard at **http://localhost:8080**

### Option 2: Native .NET

```bash
# Quick install with interactive script
./scripts/install/install.sh --native

# Or manually
cp config/appsettings.sample.json config/appsettings.json
dotnet run --project src/MarketDataCollector/MarketDataCollector.csproj -- --ui
```

### Option 3: Using Make

```bash
# Show all available commands
make help

# Docker installation
make docker

# Native installation with web UI
make run-ui

# Run tests
make test

# Run diagnostics (troubleshooting)
make doctor
```

### Windows Installation

```powershell
# Interactive installation
.\scripts\install\install.ps1

# Or specify mode directly
.\scripts\install\install.ps1 -Mode Docker
.\scripts\install\install.ps1 -Mode Native
```

### Windows Desktop App Install

Install the signed Windows desktop app using MSIX/AppInstaller for a native setup/upgrade experience.

**Prerequisites**
- Windows 10/11 (build 19041 or newer recommended).
- [App Installer](https://www.microsoft.com/store/productId/9NBLGGH4NNS1) from the Microsoft Store (required for `.appinstaller`).
- If installing an unpackaged build, install the .NET 9 Desktop Runtime.

**Install (AppInstaller)**
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

## Overview

Market Data Collector is a modular, event-driven system that captures, validates, and persists high-fidelity market data from multiple providers including Interactive Brokers, Alpaca, NYSE, Polygon, and StockSharp. It features a modern web dashboard, a native Windows desktop application (UWP/XAML), structured logging, and supports deployment as a single self-contained executable.

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
- **Flexible JSONL storage**: Naming conventions (BySymbol, ByDate, ByType, Flat) with optional gzip compression
- **Partitioning and retention**: Daily/hourly/monthly/none plus retention by age or total capacity
- **Data replay**: Stream historical JSONL files for backtesting and research

### Monitoring and Observability
- **Web dashboard**: Modern HTML dashboard for live monitoring, integrity event tracking, and backfill controls
- **Native Windows app**: UWP/XAML desktop application with full configuration and monitoring capabilities
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
- **[docs/guides/operator-runbook.md](docs/guides/operator-runbook.md)** - Operations guide and production deployment
- **[docs/architecture/domains.md](docs/architecture/domains.md)** - Event contracts and domain models
- **[docs/architecture/c4-diagrams.md](docs/architecture/c4-diagrams.md)** - System diagrams
- **[docs/integrations/lean-integration.md](docs/integrations/lean-integration.md)** - QuantConnect Lean integration guide and examples
- **[docs/architecture/storage-design.md](docs/architecture/storage-design.md)** - Advanced storage organization and data management strategies
- **[docs/build-observability.md](docs/build-observability.md)** - Build observability toolkit and CLI reference

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

The repository includes 20 comprehensive GitHub Actions workflows for automated testing, security, and deployment:

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

**447 source files** | **413 C#** | **12 F#** | **45 test files** | **56+ documentation files**

```
Market-Data-Collector/
├── .github/              # CI/CD workflows (20), AI prompts, Dependabot
├── docs/                 # Documentation (56+ files), ADRs, AI assistant guides
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
├── tests/                # C# and F# test projects (45 files)
├── benchmarks/           # Performance benchmarks (BenchmarkDotNet)
├── MarketDataCollector.sln
├── Makefile              # Build automation (60+ targets)
└── CLAUDE.md             # AI assistant guide
```

## License

See LICENSE file for details.

## Contributing

Contributions are welcome! Please see the [CLAUDE.md](CLAUDE.md) for architecture details and coding guidelines before submitting pull requests.
