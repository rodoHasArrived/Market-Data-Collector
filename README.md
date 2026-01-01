# Market Data Collector

A high-performance, cross-platform market data collection system for real-time and historical market microstructure data.

## Overview

Market Data Collector is a modular, event-driven system that captures, validates, and persists high-fidelity market data from multiple providers including Interactive Brokers, Alpaca, and Polygon. The system is designed for researchers, quantitative analysts, and traders who need reliable tick-by-tick market data for backtesting, research, and live trading applications.

## Key Features

- **Multi-Provider Support**: Connect to Interactive Brokers, Alpaca, or Polygon data feeds
- **Provider-Agnostic Architecture**: Seamlessly switch between data sources without code changes
- **High-Performance Event Pipeline**: Bounded channel architecture with configurable backpressure handling
- **Flexible Storage Options**: Multiple file naming conventions, date partitioning, and retention policies
- **Real-Time Monitoring**: Built-in HTTP server with Prometheus metrics and live dashboard
- **Data Replay**: Replay historical data from stored JSONL files for backtesting
- **Integrity Validation**: Built-in sequence validation and order book integrity checking
- **Hot Configuration Reload**: Update subscriptions without restarting the collector

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

## Contributing

Contributions are welcome! Please see the documentation for architecture details before submitting pull requests.
