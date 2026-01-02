# Market Data Collector

A high-performance, cross-platform market data collection system for real-time and historical market microstructure data.

[![.NET](https://img.shields.io/badge/.NET-8.0-blue)](https://dotnet.microsoft.com/)
[![C#](https://img.shields.io/badge/C%23-11-blue)](https://docs.microsoft.com/en-us/dotnet/csharp/)
[![License](https://img.shields.io/badge/license-See%20LICENSE-green)](LICENSE)

**Status**: Production Ready | **Version**: 1.1.0 | **Last Updated**: 2026-01-02

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

Comprehensive documentation is available in the `MarketDataCollector/docs/` directory:

- **[MarketDataCollector/README.md](MarketDataCollector/README.md)** - Product overview, CLI/UI usage, and configuration highlights
- **[docs/GETTING_STARTED.md](MarketDataCollector/docs/GETTING_STARTED.md)** - End-to-end setup for local development
- **[docs/CONFIGURATION.md](MarketDataCollector/docs/CONFIGURATION.md)** - Detailed explanation of every setting including backfill
- **[docs/architecture.md](MarketDataCollector/docs/architecture.md)** - System architecture and design
- **[docs/operator-runbook.md](MarketDataCollector/docs/operator-runbook.md)** - Operations guide and production deployment
- **[docs/domains.md](MarketDataCollector/docs/domains.md)** - Event contracts and domain models
- **[docs/c4-diagrams.md](MarketDataCollector/docs/c4-diagrams.md)** - System diagrams
- **[docs/lean-integration.md](MarketDataCollector/docs/lean-integration.md)** - QuantConnect Lean integration guide and examples

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

See [`MarketDataCollector/src/MarketDataCollector/Integrations/Lean/README.md`](MarketDataCollector/src/MarketDataCollector/Integrations/Lean/README.md) for integration details and examples.

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
- **Backfill status and controls** at `/api/backfill/*`

Monitor event throughput, drop rates, integrity events, and pipeline statistics in real-time. Initiate or review historical backfill jobs directly from the dashboard without restarting the collector.

## License

See LICENSE file for details.

## Roadmap and Future Enhancements

### Recently Completed

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

See [DEPENDENCIES.md](MarketDataCollector/DEPENDENCIES.md) for detailed implementation recommendations.

## Contributing

Contributions are welcome! Please see the documentation for architecture details before submitting pull requests.
