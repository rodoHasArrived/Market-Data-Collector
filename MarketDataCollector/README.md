# Market Data Collector

**Version**: 0.9.0 (Pre-Release) | **Last Updated**: 2026-01-01

A cross-platform, provider-agnostic market data collector that ingests real-time market data from multiple sources (Interactive Brokers, Alpaca, Polygon, and more), normalizes them into domain events, and persists them as JSONL for downstream research. The collector emits best bid/offer (BBO) snapshots to support trade aggressor inference and quote-aware analytics regardless of data provider.

## Supported Data Providers

- **Interactive Brokers (IB)** – L2 depth, tick-by-tick trades, quotes
- **Alpaca** – Real-time trades and quotes via WebSocket
- **Polygon** – Stub implementation ready for expansion
- Extensible architecture for adding additional providers

## Quick start

Run a local smoke test (no provider connectivity required):

```bash
dotnet run --project src/MarketDataCollector/MarketDataCollector.csproj
```

To exercise built-in self tests:

```bash
dotnet run --project src/MarketDataCollector/MarketDataCollector.csproj -- --selftest
```

See `docs/operator-runbook.md` for production startup scripts, including the systemd unit and PowerShell helpers.

## Configuration highlights

* `appsettings.json` drives symbol subscriptions (trades/depth), provider settings, and API credentials.
* Hot reload is enabled by default: edits to `appsettings.json` apply without restarting when `--watch-config` is set.
* Set `DataSource` to `IB`, `Alpaca`, or `Polygon` to select the active data provider; BBO snapshots keep recording with stream IDs preserved for reconciliation.
* Configure storage options including naming conventions, date partitioning, retention policies, and capacity limits.

## Outputs

Events are written under `./data/` as newline-delimited JSON. The storage system supports multiple file organization strategies:

### File Naming Conventions
- **BySymbol** (default): `{root}/{symbol}/{type}/{date}.jsonl` - Best for analyzing individual symbols over time
- **ByDate**: `{root}/{date}/{symbol}/{type}.jsonl` - Best for daily batch processing
- **ByType**: `{root}/{type}/{symbol}/{date}.jsonl` - Best for analyzing specific event types across symbols
- **Flat**: `{root}/{symbol}_{type}_{date}.jsonl` - Simple flat structure for small datasets

### Date Partitioning
- **Daily** (default): One file per day
- **Hourly**: One file per hour (high-volume scenarios)
- **Monthly**: One file per month (long-term storage)
- **None**: Single file per symbol/type

### Retention Management
- **RetentionDays**: Automatically delete files older than specified days
- **MaxTotalMegabytes**: Cap total storage size, removing oldest files first

Example configuration in `appsettings.json`:
```json
{
  "Storage": {
    "NamingConvention": "BySymbol",
    "DatePartition": "Daily",
    "IncludeProvider": false,
    "RetentionDays": 30,
    "MaxTotalMegabytes": 10240
  }
}
```

Integrity events are stored alongside trade/depth/quote streams so data quality issues are easy to correlate.

## Monitoring and Observability

The collector includes a built-in HTTP server for real-time monitoring without requiring ASP.NET:

```bash
dotnet run -- --http-port 8080
```

### Available Endpoints

- **`/`** - Live HTML dashboard with auto-refreshing metrics and integrity events
- **`/metrics`** - Prometheus-compatible metrics endpoint
- **`/status`** - JSON status with metrics, pipeline statistics, and integrity events

### Metrics Exposed

- `mdc_published` - Total events published
- `mdc_dropped` - Events dropped due to backpressure
- `mdc_integrity` - Integrity validation events
- `mdc_trades` - Trade events processed
- `mdc_depth_updates` - Market depth updates
- `mdc_quotes` - Quote updates
- `mdc_events_per_second` - Current event throughput rate
- `mdc_drop_rate` - Drop rate percentage

Integrate with Prometheus, Grafana, or simply monitor the dashboard in your browser.

## Data Replay

Replay previously captured JSONL files for backtesting and analysis:

```csharp
var replayer = new JsonlReplayer("./data");
await foreach (var evt in replayer.ReadEventsAsync(cancellationToken))
{
    // Process historical events
    Console.WriteLine($"{evt.EventType}: {evt.Timestamp}");
}
```

The replayer supports:
- Automatic gzip decompression (`.jsonl.gz` files)
- Chronological playback across multiple files
- All event types (trades, depth, quotes, integrity events)

## Architecture and design docs

Detailed diagrams and domain notes live in `./docs`:

* `FAQ.md` – frequently asked questions (What is...? Where can I find...?)
* `GETTING_STARTED.md` – quick start guide and initial setup
* `CONFIGURATION.md` – configuration reference
* `TROUBLESHOOTING.md` – troubleshooting guide and solutions
* `architecture.md` – layered architecture and event flow
* `c4-diagrams.md` – rendered system, container, and component diagrams
* `domains.md` – event contracts and invariants
* `operator-runbook.md` – operational guidance and startup scripts
* `why-this-architecture.md` – non-technical overview of design decisions
* `interactive-brokers-setup.md` – IB API installation and configuration
* `open-source-references.md` – catalog of related projects and resources

## Known Limitations and Roadmap

### Current Limitations

**Provider Integration:**
- Alpaca quote messages ("T":"q") not yet wired to QuoteCollector (trade-only currently)
- IB connection does not auto-retry on failure (manual restart required)
- No heartbeat/keep-alive for WebSocket connections

**Security:**
- API credentials stored in `appsettings.json` (should use environment variables or secrets manager)
- No built-in authentication for HTTP monitoring endpoints

**Observability:**
- Some error paths lack structured logging (parse errors, connection failures)
- Monitoring loop disposal errors not logged

**Data Precision:**
- Order book uses `double` for prices (consider `decimal` to avoid floating-point precision issues)

### Planned Enhancements

See the project [DEPENDENCIES.md](DEPENDENCIES.md) for detailed recommendations on:
- **Serilog** integration for structured logging
- **Polly** for retry policies and circuit breakers
- **FluentValidation** for configuration validation
- **prometheus-net** for enhanced metrics
- **xUnit/Moq** for comprehensive testing
- **Parquet.Net** for columnar storage

The main project README includes a detailed roadmap for near-term and long-term improvements.
