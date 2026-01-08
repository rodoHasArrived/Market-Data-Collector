# Market Data Collector

A high-performance, cross-platform market data collection system for real-time and historical market microstructure data.

[![.NET](https://img.shields.io/badge/.NET-8.0-blue)](https://dotnet.microsoft.com/)
[![C#](https://img.shields.io/badge/C%23-11-blue)](https://docs.microsoft.com/en-us/dotnet/csharp/)
[![F#](https://img.shields.io/badge/F%23-8.0-blue)](https://fsharp.org/)
[![Docker](https://img.shields.io/badge/Docker-Ready-blue)](https://www.docker.com/)
[![License](https://img.shields.io/badge/license-See%20LICENSE-green)](LICENSE)

**Status**: Production Ready | **Version**: 1.5.0 | **Last Updated**: 2026-01-04

---

## Installation

### Option 1: Docker (Recommended)

```bash
cd MarketDataCollector

# Quick install with interactive script
./install.sh --docker

# Or manually with Docker Compose
cp appsettings.sample.json appsettings.json
docker compose up -d
```

Access the dashboard at **http://localhost:8080**

### Option 2: Native .NET

```bash
cd MarketDataCollector

# Quick install with interactive script
./install.sh --native

# Or manually
cp appsettings.sample.json appsettings.json
dotnet run --project src/MarketDataCollector/MarketDataCollector.csproj -- --ui
```

### Option 3: Using Make

```bash
cd MarketDataCollector

# Show all available commands
make help

# Docker installation
make docker

# Native installation
make run-ui
```

### Windows Installation

```powershell
cd MarketDataCollector

# Interactive installation
.\install.ps1

# Or specify mode directly
.\install.ps1 -Mode Docker
.\install.ps1 -Mode Native
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
# Clone the repository and enter the solution root
git clone https://github.com/rodoHasArrived/Test.git
cd Test/MarketDataCollector

# Copy the sample settings and edit as needed
cp appsettings.sample.json appsettings.json

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

- **[HELP.md](HELP.md)** - Comprehensive user guide with troubleshooting and FAQ
- **[DEPENDENCIES.md](DEPENDENCIES.md)** - Dependencies and implementation recommendations
- **[docs/guides/getting-started.md](docs/guides/getting-started.md)** - End-to-end setup for local development
- **[docs/guides/configuration.md](docs/guides/configuration.md)** - Detailed explanation of every setting including backfill
- **[docs/architecture/overview.md](docs/architecture/overview.md)** - System architecture and design
- **[docs/guides/operator-runbook.md](docs/guides/operator-runbook.md)** - Operations guide and production deployment
- **[docs/architecture/domains.md](docs/architecture/domains.md)** - Event contracts and domain models
- **[docs/architecture/c4-diagrams.md](docs/architecture/c4-diagrams.md)** - System diagrams
- **[docs/integrations/lean-integration.md](docs/integrations/lean-integration.md)** - QuantConnect Lean integration guide and examples
- **[docs/architecture/storage-design.md](docs/architecture/storage-design.md)** - Advanced storage organization and data management strategies

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
docker compose up -d

# With monitoring stack (Prometheus + Grafana)
docker compose --profile monitoring up -d

# View logs
docker compose logs -f marketdatacollector

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

## License

See LICENSE file for details.

## Roadmap and Future Enhancements

### Recently Completed

**F# Domain Library (Completed 2026-01-03):**
- ✅ Type-safe domain models using discriminated unions with exhaustive pattern matching
- ✅ Railway-Oriented validation with error accumulation (no more exceptions)
- ✅ Pure functional calculations (spread, imbalance, VWAP, TWAP, microprice)
- ✅ Pipeline transforms for declarative stream processing
- ✅ C# interop layer with wrapper classes and extension methods
- ✅ Comprehensive test suite with 50+ unit tests

**Storage Organization Design (Completed 2026-01-02):**
- ✅ Comprehensive storage organization design document with best practices
- ✅ Hierarchical taxonomy structure for data organization
- ✅ Tiered storage architecture (hot/warm/cold)
- ✅ File maintenance and health monitoring strategies
- ✅ Data quality scoring and best-of-breed selection
- ✅ Search and discovery infrastructure design
- ✅ Operational scheduling for off-hours maintenance

**UWP Desktop Application (Completed 2026-01-02):**
- ✅ Native Windows desktop app using UWP/XAML with WinUI 3 styling
- ✅ Full-featured dashboard with real-time status monitoring
- ✅ Integrated configuration pages for providers, storage, symbols, and backfill
- ✅ Secure credential management using Windows CredentialPicker

**Code Quality (Completed 2026-01-01):**
- ✅ Extracted shared subscription management into `SymbolSubscriptionTracker` base class
- ✅ Standardized logger initialization across all components using `LoggingSetup.ForContext<T>()`
- ✅ Added comprehensive `.gitignore` for credential protection
- ✅ Cleaned up consumer classes by removing boilerplate code

### Near-Term Improvements

**Resilience and Reliability:**
- Connection retry with exponential backoff for all providers (Polly integration)
- Automatic WebSocket reconnection on connection loss
- Heartbeat/keep-alive mechanism to detect stale connections
- Circuit breakers to prevent cascading failures

**Security:**
- ✅ Windows CredentialPicker for secure API credential management (UWP app)
- Move API credentials from config files to environment variables or secure vault
- Support for Azure Key Vault, AWS Secrets Manager, HashiCorp Vault

**Observability:**
- ✅ Structured Serilog logging implemented throughout codebase
- Comprehensive error logging for connection failures and parse errors
- Distributed tracing with OpenTelemetry

**Data Quality:**
- Use decimal instead of double for price fields to avoid floating-point precision issues
- Cross-validation of bid/ask price ordering in order books
- Enhanced Alpaca quote integration (wire "T":"q" messages to QuoteCollector)

### Long-Term Enhancements

**Testing:**
- Comprehensive unit test suite with xUnit
- Integration tests for all provider implementations
- Mock data generation with Bogus
- Benchmark suite with BenchmarkDotNet

**Performance:**
- Adopt System.IO.Pipelines for zero-copy WebSocket message parsing
- Memory allocation optimization in hot paths
- Enhanced backpressure handling

**Storage:**
- Alternative storage backends (QuestDB, InfluxDB, TimescaleDB)
- Apache Parquet for archival storage (10-20x better compression)
- Data validation and schema evolution support

**Advanced Features:**
- Automated recovery policies (auto-resubscribe on integrity events)
- Multi-provider reconciliation and feed divergence alarms
- Order book analytics (microprice, liquidity imbalance)
- Custom alert rules and webhook notifications

See [DEPENDENCIES.md](DEPENDENCIES.md) for detailed implementation recommendations.

## Contributing

Contributions are welcome! Please see the documentation for architecture details before submitting pull requests.
