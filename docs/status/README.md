# Project Status Documentation

This folder contains project status, roadmap, and changelog documentation for the Market Data Collector.

## Documents

| Document | Description |
|----------|-------------|
| [ROADMAP.md](ROADMAP.md) | Feature backlog, development priorities, and sprint plans |
| [production-status.md](production-status.md) | Architecture assessment and production readiness |
| [CHANGELOG.md](CHANGELOG.md) | Version history and implemented improvements |

## Quick Links

### Current Status
- **Version:** 1.6.1
- **Status:** Production Ready
- **Last Updated:** 2026-01-27

### Key Metrics
- 116+ features implemented
- 9 historical data providers (production ready)
- 2 streaming providers (Alpaca, Interactive Brokers)
- UWP Desktop App with 17+ pages and full CLI-equivalent services
- 370+ total backlog items

### What's New in v1.5.0
- Apache Parquet storage (10-20x compression)
- OpenTelemetry distributed tracing
- 200+ technical indicators
- Write-Ahead Logging for crash-safe persistence
- Daily summary webhooks
- Data quality validation (crossed markets, timestamp monotonicity)
- UWP Desktop App with Admin/Maintenance and Advanced Analytics pages
- CLI symbol management commands
- Auto-configuration and user-friendly onboarding

## Related Documentation

- [Architecture Overview](../architecture/overview.md)
- [Getting Started Guide](../guides/getting-started.md)
- [Configuration Reference](../guides/configuration.md)
- [UWP Development Roadmap](../guides/uwp-development-roadmap.md)

---

*For the main project documentation, see the [root README](../../README.md).*
