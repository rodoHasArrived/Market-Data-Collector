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
dotnet run --project src/MarketDataCollector/MarketDataCollector.csproj -- --watch-config --serve-status
```

- `--serve-status`: Writes periodic health snapshots to `data/_status/status.json`
- `--watch-config`: Enables hot reload of `appsettings.json` (handled by `ConfigWatcher`)

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

### Metrics
Access counters via `Metrics` static class:
- `Metrics.Published`: Total events written to pipeline
- `Metrics.Dropped`: Events dropped due to backpressure
- `Metrics.Integrity`: Integrity events emitted

### Status Endpoint
When `--serve-status` is enabled, health snapshots are written to:
```
data/_status/status.json
```

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

### Startup scripts
If you set `DataSource` to `Alpaca`, set `USE_IBAPI=false` in the startup scripts (or omit it). IB connectivity checks are skipped when IB is disabled.

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
