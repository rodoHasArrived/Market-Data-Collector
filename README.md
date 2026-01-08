# Market Data Collector

A high-performance, cross-platform market data collection system for real-time and historical market microstructure data.

[![.NET](https://img.shields.io/badge/.NET-8.0-blue)](https://dotnet.microsoft.com/)
[![C#](https://img.shields.io/badge/C%23-11-blue)](https://docs.microsoft.com/en-us/dotnet/csharp/)
[![License](https://img.shields.io/badge/license-See%20LICENSE-green)](LICENSE)

**Status**: Active Development | **Version**: 0.9.0 (Pre-Release)

## Overview

Market Data Collector is a modular, event-driven system that captures, validates, and persists high-fidelity market data from multiple providers including Interactive Brokers, Alpaca, and Polygon. The system is designed for researchers, quantitative analysts, and traders who need reliable tick-by-tick market data for backtesting, research, and live trading applications.

## Key Features

### Data Collection
- **Multi-Provider Support**: Connect to Interactive Brokers, Alpaca, or Polygon data feeds
- **Provider-Agnostic Architecture**: Seamlessly switch between data sources without code changes
- **Tick-by-Tick Trades**: Capture every trade with sequence validation and aggressor inference
- **Level 2 Order Book**: Maintain full market depth with integrity checking
- **BBO Quotes**: Best bid/offer snapshots with spread and mid-price calculation
- **Order Flow Statistics**: Real-time VWAP, buy/sell volume, and imbalance metrics

### Performance and Reliability
- **High-Performance Event Pipeline**: Bounded channel architecture (default 50,000 events) with configurable backpressure
- **Integrity Validation**: Built-in sequence validation and order book integrity checking with detailed event emission
- **Hot Configuration Reload**: Update subscriptions without restarting the collector
- **Graceful Shutdown**: Ensures all events are flushed before termination

### Storage and Data Management
- **Flexible Storage Options**: Multiple file naming conventions (BySymbol, ByDate, ByType, Flat)
- **Date Partitioning**: Daily, hourly, monthly, or no partitioning strategies
- **Retention Policies**: Automatic cleanup based on time (RetentionDays) or capacity (MaxTotalMegabytes)
- **Compression Support**: Optional gzip compression for JSONL files
- **Data Replay**: Replay historical data from stored JSONL files for backtesting and analysis

### Monitoring and Observability
- **Real-Time Monitoring**: Built-in HTTP server with Prometheus metrics and live dashboard
- **Comprehensive Metrics**: Track events published, dropped, integrity violations, and throughput rates
- **JSON Status Endpoint**: Machine-readable status for integration with monitoring tools
- **HTML Dashboard**: Auto-refreshing browser-based dashboard with integrity event tracking

## Quick Start

```bash
# Clone the repository
cd MarketDataCollector

# Run smoke test (no provider connectivity required)
dotnet run --project src/MarketDataCollector/MarketDataCollector.csproj

# Run self-tests
dotnet run --project src/MarketDataCollector/MarketDataCollector.csproj -- --selftest

# Start with monitoring and config hot reload
dotnet run --project src/MarketDataCollector/MarketDataCollector.csproj -- --watch-config --http-port 8080
```

Access the monitoring dashboard at `http://localhost:8080`

## Documentation

Comprehensive documentation is available in the `MarketDataCollector/docs/` directory:

- **[docs/FAQ.md](MarketDataCollector/docs/FAQ.md)** - Frequently asked questions (What is...? Where can I find...?)
- **[MarketDataCollector/README.md](MarketDataCollector/README.md)** - Quick start guide and configuration overview
- **[docs/architecture.md](MarketDataCollector/docs/architecture.md)** - System architecture and design
- **[docs/operator-runbook.md](MarketDataCollector/docs/operator-runbook.md)** - Operations guide and production deployment
- **[docs/domains.md](MarketDataCollector/docs/domains.md)** - Event contracts and domain models
- **[docs/c4-diagrams.md](MarketDataCollector/docs/c4-diagrams.md)** - System diagrams

## Supported Data Sources

- **Interactive Brokers** - L2 market depth, tick-by-tick trades, quotes
- **Alpaca** - Real-time trades and quotes via WebSocket
- **Polygon** - Stub implementation for future expansion

## Output Data

Market data is stored as newline-delimited JSON (JSONL) files with:
- Configurable naming conventions (by symbol, date, or type)
- Optional gzip compression
- Automatic retention management
- Data integrity events alongside market data

## Monitoring

The built-in HTTP server provides:
- **Prometheus metrics** at `/metrics`
- **JSON status** at `/status`
- **Live HTML dashboard** at `/`

Monitor event throughput, drop rates, integrity events, and pipeline statistics in real-time.

## License

See LICENSE file for details.

## Roadmap and Future Enhancements

### Near-Term Improvements

**Resilience and Reliability:**
- Connection retry with exponential backoff for all providers (Polly integration)
- Automatic WebSocket reconnection on connection loss
- Heartbeat/keep-alive mechanism to detect stale connections
- Circuit breakers to prevent cascading failures

**Security:**
- Move API credentials from config files to environment variables or secure vault
- Support for Azure Key Vault, AWS Secrets Manager, HashiCorp Vault

**Observability:**
- Replace manual logging with structured Serilog throughout codebase
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
