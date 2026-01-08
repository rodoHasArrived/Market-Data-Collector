# Market Data Collector

A high-performance, cross-platform market data collection system for real-time and historical market microstructure data.

[![.NET](https://img.shields.io/badge/.NET-9.0-blue)](https://dotnet.microsoft.com/)
[![C#](https://img.shields.io/badge/C%23-11-blue)](https://docs.microsoft.com/en-us/dotnet/csharp/)
[![F#](https://img.shields.io/badge/F%23-8.0-blue)](https://fsharp.org/)
[![Docker](https://img.shields.io/badge/Docker-Ready-blue)](https://www.docker.com/)
[![License](https://img.shields.io/badge/license-See%20LICENSE-green)](LICENSE)

**Status**: Production Ready | **Version**: 1.5.0 | **Last Updated**: 2026-01-08

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
```

### Windows Installation

```powershell
# Interactive installation
.\scripts\install\install.ps1

# Or specify mode directly
.\scripts\install\install.ps1 -Mode Docker
.\scripts\install\install.ps1 -Mode Native
```

---

## Overview

Market Data Collector is a modular, event-driven system that captures, validates, and persists high-fidelity market data from multiple providers including Interactive Brokers, Alpaca, and Polygon. It ships with a modern web dashboard, a native Windows desktop application (UWP/XAML), structured logging, and a single self-contained executable for streamlined production deployments.

## Key Features

### Data Collection
- **Multi-provider ingest**: Interactive Brokers (L2 depth, tick-by-tick trades/quotes), Alpaca (real-time trades/quotes), Polygon (stub ready for expansion)
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

## Quick Start

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

### AI Assistant Guides
- **[CLAUDE.md](CLAUDE.md)** - Main AI assistant guide
- **[docs/ai-assistants/](docs/ai-assistants/)** - Specialized guides for subsystems (F#, microservices, providers, storage, testing)

## Supported Data Sources

- **Interactive Brokers** - L2 market depth, tick-by-tick trades, quotes
- **Alpaca** - Real-time trades and quotes via WebSocket
- **Polygon** - Stub implementation for future expansion

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

## Repository Structure

```
Market-Data-Collector/
├── .github/              # CI/CD workflows and AI prompts
├── docs/                 # All documentation
├── scripts/              # Install, publish, run, and diagnostic scripts
├── deploy/               # Docker, systemd, and monitoring configs
├── config/               # Configuration files (appsettings.json)
├── src/                  # Source code
├── tests/                # Unit tests
├── benchmarks/           # Performance benchmarks
├── MarketDataCollector.sln
├── Makefile
└── CLAUDE.md             # AI assistant guide
```

## License

See LICENSE file for details.

## Contributing

Contributions are welcome! Please see the [CLAUDE.md](CLAUDE.md) for architecture details and coding guidelines before submitting pull requests.
