# Operator Runbook

## Startup

### Headless / Test Mode
```bash
dotnet run --project src/MarketDataCollector/MarketDataCollector.csproj
```

This runs a smoke test with simulated data. Output is written to `./data/`.

### Self-Test Mode
```bash
dotnet run --project src/MarketDataCollector/MarketDataCollector.csproj -- --selftest
```

Runs built-in self-tests (e.g., `DepthBufferSelfTests`) and exits.

### Production (with Live Data)
```bash
dotnet build -p:DefineConstants=IBAPI  # Only needed if using IB provider
dotnet run --project src/MarketDataCollector/MarketDataCollector.csproj -- --watch-config --http-port 8080
```

- `--watch-config`: Enables hot reload of `appsettings.json` (handled by `ConfigWatcher`)
- `--http-port 8080`: Starts HTTP monitoring server on port 8080 (optional, omit to disable)
- `--serve-status`: Writes periodic health snapshots to `data/_status/status.json` (legacy option, use HTTP server instead)

### UI
```bash
dotnet run --project src/MarketDataCollector.Ui/MarketDataCollector.Ui.csproj
```

---

## Hot Reload

* Edit `appsettings.json`
* Or use UI
* Changes applied without restart (when `--watch-config` is enabled)

Supported:
* Add/remove symbols
* Toggle trades/depth subscriptions
* Change depth levels
* Switch between data providers (IB, Alpaca)

---

## Integrity Events

### Trade Integrity
`TradeDataCollector` validates sequence numbers for each symbol/stream:
- **OutOfOrder**: Trade rejected if sequence <= previous
- **SequenceGap**: Trade accepted but stats marked stale if sequence skips

### Depth Integrity
`MarketDepthCollector` validates order book operations:
- **Gap**: Insert position out of range
- **OutOfOrder**: Update position doesn't exist
- **InvalidPosition**: Delete position doesn't exist
- **Stale**: Stream frozen from previous error

If integrity events spike:
1. Check provider connectivity
2. Verify market data entitlements
3. Call `ResetSymbolStream(symbol)` or resubscribe affected symbol
4. Inspect JSONL output in `data/`

---

## Monitoring

### HTTP Monitoring Server

Start the built-in HTTP server for real-time monitoring:

```bash
dotnet run -- --http-port 8080
```

Access monitoring endpoints:
- **Dashboard**: http://localhost:8080/ (auto-refreshing HTML dashboard)
- **Prometheus metrics**: http://localhost:8080/metrics
- **JSON status**: http://localhost:8080/status

#### Available Metrics

Prometheus-compatible metrics exposed at `/metrics`:

| Metric | Type | Description |
|--------|------|-------------|
| `mdc_published` | counter | Total events published to pipeline |
| `mdc_dropped` | counter | Events dropped due to backpressure |
| `mdc_integrity` | counter | Integrity validation events |
| `mdc_trades` | counter | Trade events processed |
| `mdc_depth_updates` | counter | Market depth updates processed |
| `mdc_quotes` | counter | Quote updates processed |
| `mdc_events_per_second` | gauge | Current event throughput rate |
| `mdc_drop_rate` | gauge | Drop rate percentage |

#### Integration with Prometheus

Add this scrape configuration to `prometheus.yml`:

```yaml
scrape_configs:
  - job_name: 'marketdatacollector'
    static_configs:
      - targets: ['localhost:8080']
    metrics_path: '/metrics'
    scrape_interval: 5s
```

#### Dashboard Features

The HTML dashboard at `/` provides:
- Real-time metrics display with auto-refresh (2 second interval)
- Table of recent integrity events with timestamps and details
- Links to raw Prometheus and JSON endpoints

### Programmatic Access

Access counters via `Metrics` static class in code:
- `Metrics.Published`: Total events written to pipeline
- `Metrics.Dropped`: Events dropped due to backpressure
- `Metrics.Integrity`: Integrity events emitted

### Legacy Status File

When `--serve-status` is enabled, periodic health snapshots are written to:
```
data/_status/status.json
```

Note: The HTTP monitoring server is preferred over file-based status for production deployments.

---

## Shutdown

Use Ctrl+C:
* Subscriptions cancelled
* `EventPipeline` drained and flushed
* Files flushed via `JsonlStorageSink`
* Clean disconnect from providers

---

## Canonical Startup Scripts

### Linux/macOS
From repo root:
```bash
chmod +x START_COLLECTOR.exp STOP_COLLECTOR.exp
./START_COLLECTOR.exp
```

Stop:
```bash
./STOP_COLLECTOR.exp
```

Environment toggles:
- `USE_IBAPI=true|false`
- `START_UI=true|false`
- `BUILD=true|false`
- `DOTNET_CONFIGURATION=Release|Debug`
- `IB_HOST`, `IB_PORT`, `IB_CLIENT_ID`

### Windows (PowerShell)
Start:
```powershell
powershell -ExecutionPolicy Bypass -File .\START_COLLECTOR.ps1
```

Stop:
```powershell
powershell -ExecutionPolicy Bypass -File .\STOP_COLLECTOR.ps1
```

### systemd (Linux service)
Unit file included at:
`deploy/systemd/marketdatacollector.service`

Typical install (example):
```bash
sudo mkdir -p /opt/marketdatacollector
sudo rsync -a ./ /opt/marketdatacollector/
sudo cp deploy/systemd/marketdatacollector.service /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable --now marketdatacollector
sudo journalctl -u marketdatacollector -f
```

---

## Preflight Checklist (built-in)

Startup scripts run a preflight step before building/starting:

- Disk space (warn if < 2GB free)
- Directory permissions (data/logs/run writable)
- Config sanity:
  - counts of symbols with trades/depth enabled
  - note: L2 depth requires provider depth entitlements
- Provider reachability (only when using IB with `USE_IBAPI=true`)
  - **Auto-detects port** by testing: `7497, 4002, 7496, 4001`
  - uses the first reachable port unless `IB_PORT` is explicitly set

If preflight fails, the startup script aborts with errors.

---

## Data Provider Configuration

The collector supports multiple data providers through the `DataSource` configuration option.

### Interactive Brokers (IB)

Set `DataSource` to `IB` in `appsettings.json`:

```json
{
  "DataSource": "IB",
  "Symbols": [
    { "Symbol": "AAPL", "SubscribeTrades": true, "SubscribeDepth": true, "DepthLevels": 10 }
  ]
}
```

Build with IBAPI support:
```bash
dotnet build -p:DefineConstants=IBAPI
```

### Alpaca

Set `DataSource` to `Alpaca` in `appsettings.json`:

```json
{
  "DataSource": "Alpaca",
  "Alpaca": {
    "KeyId": "YOUR_KEY_ID",
    "SecretKey": "YOUR_SECRET_KEY",
    "Feed": "iex",
    "UseSandbox": false,
    "SubscribeQuotes": true
  },
  "Symbols": [
    { "Symbol": "AAPL", "SubscribeTrades": true, "SubscribeDepth": false }
  ]
}
```

Notes:
- Alpaca real-time stock data is provided via WebSocket streams with message authentication and subscribe actions. (See Alpaca docs.)
- This integration supports **trade prints** (`T:"t"` messages).
- Quote support (`T:"q"` messages) requires `SubscribeQuotes: true` and is wired to `QuoteCollector`.
- Full Level-2 depth is not supported for stocks via Alpaca.

### Polygon

Set `DataSource` to `Polygon` in `appsettings.json`:

```json
{
  "DataSource": "Polygon",
  "Symbols": [
    { "Symbol": "AAPL", "SubscribeTrades": true }
  ]
}
```

Notes:
- Current implementation is a stub that validates the provider abstraction
- Emits synthetic heartbeat events to verify connectivity
- Ready for expansion to full Polygon WebSocket integration

### Startup scripts
If you set `DataSource` to `Alpaca` or `Polygon`, set `USE_IBAPI=false` in the startup scripts (or omit it). IB connectivity checks are skipped when IB is disabled.

---

## Quote Context for Aggressor Inference

When using quote-capable providers, the system can ingest **quote (BBO)** updates and use them to infer trade aggressor side:

- Trade price >= Ask => Buy aggressor
- Trade price <= Bid => Sell aggressor
- Otherwise => Unknown

To enable quote ingestion in Alpaca mode, set:

```json
"Alpaca": { "SubscribeQuotes": true }
```

This will emit `MarketEventType.BboQuote` events with `BboQuotePayload` and improve `Trade` + `OrderFlow` aggressor classification.

To confirm quotes are flowing:

```bash
ls data/AAPL.BboQuote.jsonl
tail -n 5 data/AAPL.BboQuote.jsonl
```

Each record includes `SequenceNumber`, `StreamId`, and `Venue` fields so you can reconcile feeds across providers.

---

## Storage Configuration

### File Organization

The storage system supports multiple file naming conventions and partitioning strategies. Configure in `appsettings.json`:

```json
{
  "DataRoot": "data",
  "Compress": true,
  "Storage": {
    "NamingConvention": "BySymbol",
    "DatePartition": "Daily",
    "IncludeProvider": false,
    "FilePrefix": null,
    "RetentionDays": 30,
    "MaxTotalMegabytes": 10240
  }
}
```

### Naming Conventions

Choose the organization strategy that matches your workflow:

| Convention | Path Pattern | Best For |
|------------|-------------|----------|
| `BySymbol` | `{root}/{symbol}/{type}/{date}.jsonl` | Analyzing individual symbols over time (default) |
| `ByDate` | `{root}/{date}/{symbol}/{type}.jsonl` | Daily batch processing and archival |
| `ByType` | `{root}/{type}/{symbol}/{date}.jsonl` | Analyzing event types across symbols |
| `Flat` | `{root}/{symbol}_{type}_{date}.jsonl` | Small datasets, simple browsing |

### Date Partitioning

Control file granularity with `DatePartition`:

- **`None`** – Single file per symbol/type (continuous append)
- **`Daily`** – One file per day (default, balanced)
- **`Hourly`** – One file per hour (high-volume scenarios)
- **`Monthly`** – One file per month (long-term storage)

### Retention Policies

Automatic cleanup to manage disk usage:

**Time-based retention** (`RetentionDays`):
- Deletes files older than specified days
- Runs during each write operation
- Example: `"RetentionDays": 30` keeps last 30 days of data

**Capacity-based retention** (`MaxTotalMegabytes`):
- Enforces storage cap by removing oldest files first
- Measured across all files in data root
- Example: `"MaxTotalMegabytes": 10240` limits storage to 10 GB

**Both policies can be combined** – whichever limit is hit first triggers cleanup.

### Compression

Enable gzip compression to reduce disk usage:

```json
{
  "Compress": true
}
```

Files are written with `.jsonl.gz` extension. The replayer automatically decompresses during playback.

### Provider Tagging

Include data source in file paths for multi-provider deployments:

```json
{
  "Storage": {
    "IncludeProvider": true
  }
}
```

Results in paths like: `data/IB/AAPL/Trade/2024-01-15.jsonl`

---

## Data Replay

Replay historical data for backtesting and analysis using `JsonlReplayer`:

```csharp
using MarketDataCollector.Storage.Replay;

var replayer = new JsonlReplayer("./data");
await foreach (var evt in replayer.ReadEventsAsync(cancellationToken))
{
    Console.WriteLine($"[{evt.Timestamp:O}] {evt.EventType}: {evt.Symbol}");

    // Process event (trades, quotes, depth, integrity)
    switch (evt.EventType)
    {
        case MarketEventType.Trade:
            // Handle trade
            break;
        case MarketEventType.BboQuote:
            // Handle quote
            break;
    }
}
```

Features:
- Automatically discovers and reads all `.jsonl` and `.jsonl.gz` files in directory tree
- Events are returned in chronological order based on file names
- Supports filtering with LINQ (`.Where()`, `.Take()`, etc.)
- Handles decompression transparently

Example: Replay only trades for specific symbol:

```csharp
var trades = replayer.ReadEventsAsync(ct)
    .Where(e => e.EventType == MarketEventType.Trade && e.Symbol == "AAPL");
```

---

## Preferred Stock Configuration (IB-specific)

For IB preferred shares (e.g., PCG-PA, PCG-PB), use explicit `LocalSymbol` to avoid ambiguity:

```json
{
  "Symbol": "PCG-PA",
  "SubscribeTrades": true,
  "SubscribeDepth": true,
  "DepthLevels": 10,
  "SecurityType": "STK",
  "Exchange": "SMART",
  "Currency": "USD",
  "PrimaryExchange": "NYSE",
  "LocalSymbol": "PCG PRA"
}
```

This ensures `ContractFactory` resolves to the correct IB contract.

---

**Version:** 1.1.0
**Last Updated:** 2026-01-02
**See Also:** [CONFIGURATION.md](CONFIGURATION.md) | [TROUBLESHOOTING.md](TROUBLESHOOTING.md) | [architecture.md](architecture.md)
