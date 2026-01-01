# Market Data Collector

**Version**: 1.0.0 (Production Ready) | **Last Updated**: 2026-01-01

A cross-platform, production-ready market data collector with an intuitive web dashboard. Ingests real-time market data from multiple sources (Interactive Brokers, Alpaca, Polygon), normalizes them into domain events, and persists them as JSONL for downstream research. Features comprehensive error handling, single-executable deployment, and built-in help system.

## ✨ New in v1.0

- **🎨 Modern Web Dashboard** - Full-featured UI for configuration and monitoring
- **📦 Single Executable** - Deploy as one file, no dependencies
- **🛡️ Enhanced Error Handling** - Comprehensive error detection and user-friendly messages
- **📚 Complete Documentation** - Built-in help system with detailed HELP.md guide
- **🎯 Production Ready** - Robust deployment with systemd support

## Supported Data Providers

- **Interactive Brokers (IB)** – L2 depth, tick-by-tick trades, quotes
- **Alpaca** – Real-time trades and quotes via WebSocket
- **Polygon** – Stub implementation ready for expansion
- Extensible architecture for adding additional providers

## Quick start

### **🚀 Easiest Way: Web Dashboard** (New!)

Start the intuitive web dashboard for easy configuration and monitoring:

```bash
./MarketDataCollector --ui
```

Then open your browser to `http://localhost:8080` for a full-featured dashboard with:
- 📊 Real-time system status and metrics
- ⚙️ Point-and-click configuration
- 📈 Symbol management
- 📅 Historical backfill interface
- 💡 Built-in help and tooltips
- 🎨 Modern, responsive UI

### Command Line Modes

**Run local smoke test** (no provider connectivity required):
```bash
dotnet run --project src/MarketDataCollector/MarketDataCollector.csproj
```

**Production mode** with status endpoint and hot-reload:
```bash
./MarketDataCollector --serve-status --watch-config
```

**Self-tests:**
```bash
./MarketDataCollector --selftest
```

**Historical backfill:**
```bash
./MarketDataCollector --backfill \
  --backfill-provider stooq \
  --backfill-symbols SPY,QQQ \
  --backfill-from 2024-01-01 \
  --backfill-to 2024-01-05
```

**Get help:**
```bash
./MarketDataCollector --help
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
- **`/api/backfill/*`** - REST endpoints surfaced by the dashboard to list providers, run backfill, and read the latest backfill status

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

## Historical Backfill

The collector can prime disk with historical daily bars before live capture begins. The feature ships with a Stooq provider and can be extended with additional implementations under `Infrastructure/Providers/Backfill`.

### Configuration

`appsettings.json` accepts a `Backfill` section to seed defaults:

```json
"Backfill": {
  "Enabled": false,
  "Provider": "stooq",
  "Symbols": ["SPY", "QQQ"],
  "From": "2024-01-01",
  "To": "2024-01-05"
}
```

Command-line arguments override the config file when present:

- `--backfill` enables a run at startup (even if config is disabled)
- `--backfill-provider <name>` selects a specific provider
- `--backfill-symbols <CSV>` overrides the symbol list
- `--backfill-from <yyyy-MM-dd>` / `--backfill-to <yyyy-MM-dd>` bound the date range

Results are written alongside the live data under `DataRoot` and surfaced through `/api/backfill/status` and the dashboard.

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

## Lean Engine Integration

Market Data Collector integrates with **QuantConnect's Lean Engine** to enable algorithmic trading and backtesting:

### Key Features
- **Custom BaseData Types**: `MarketDataCollectorTradeData` and `MarketDataCollectorQuoteData` for Lean algorithms
- **Data Provider**: `MarketDataCollectorDataProvider` implements Lean's `IDataProvider` interface
- **Tick-Level Backtesting**: Use collected market microstructure data for strategy development
- **Sample Algorithms**: Pre-built examples for spread analysis, order flow, and microstructure strategies

### Quick Start with Lean

```csharp
using QuantConnect.Algorithm;
using MarketDataCollector.Integrations.Lean;

public class MyAlgorithm : QCAlgorithm
{
    public override void Initialize()
    {
        AddData<MarketDataCollectorTradeData>("SPY", Resolution.Tick);
        AddData<MarketDataCollectorQuoteData>("SPY", Resolution.Tick);
    }

    public override void OnData(Slice data)
    {
        // Access high-fidelity market data
    }
}
```

See [`src/MarketDataCollector/Integrations/Lean/README.md`](src/MarketDataCollector/Integrations/Lean/README.md) for comprehensive integration guide.

## Architecture and design docs

Detailed diagrams and domain notes live in `./docs`:

* `architecture.md` – layered architecture and event flow
* `c4-diagrams.md` – rendered system, container, and component diagrams
* `domains.md` – event contracts and invariants
* `operator-runbook.md` – operational guidance and startup scripts
* `why-this-architecture.md` – non-technical overview of design decisions
* `interactive-brokers-setup.md` – IB API installation and configuration
* `open-source-references.md` – catalog of related projects and resources
* `lean-integration.md` – QuantConnect Lean Engine integration guide

## Recent Improvements

### Code Quality (2026-01-01)
- **Subscription Management**: New `SymbolSubscriptionTracker` base class provides thread-safe subscription handling for depth collectors
- **Logging Standardization**: All components now use `LoggingSetup.ForContext<T>()` for consistent logging
- **Consumer Cleanup**: Removed boilerplate from MassTransit consumer classes
- **Security**: Added `.gitignore` to protect credentials from version control

## Known Limitations and Roadmap

### Current Limitations

**Provider Integration:**
- Alpaca quote messages ("T":"q") not yet wired to QuoteCollector (trade-only currently)
- IB connection does not auto-retry on failure (manual restart required)
- No heartbeat/keep-alive for WebSocket connections

**Security:**
- API credentials stored in `appsettings.json` (should use environment variables or secrets manager)
- ✅ `.gitignore` now excludes credential files from version control
- No built-in authentication for HTTP monitoring endpoints

**Observability:**
- ✅ Structured Serilog logging implemented throughout codebase
- Some error paths may still need additional logging (parse errors, connection failures)

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
