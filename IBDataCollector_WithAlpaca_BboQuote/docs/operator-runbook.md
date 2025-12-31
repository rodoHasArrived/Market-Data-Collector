# Operator Runbook

## Startup

### Headless / Test Mode
```bash
dotnet run --project src/IBDataCollector/IBDataCollector.csproj
```

### Production (IB Live)
```bash
dotnet build -p:DefineConstants=IBAPI
dotnet run --project src/IBDataCollector/IBDataCollector.csproj -- --watch-config --serve-status
```

### UI
```bash
dotnet run --project src/IBDataCollector.Ui/IBDataCollector.Ui.csproj
```

---

## Hot Reload

* Edit `appsettings.json`
* Or use UI
* Changes applied without restart

Supported:
* Add/remove symbols
* Toggle trades/depth
* Change depth levels

---

## Integrity Events

If integrity events spike:
1. Check IB connectivity
2. Verify market data entitlements
3. Resubscribe affected symbol
4. Inspect JSONL output

---

## Shutdown

Use Ctrl+C:
* Subscriptions cancelled
* Files flushed
* Clean disconnect


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
`deploy/systemd/ibdatacollector.service`

Typical install (example):
```bash
sudo mkdir -p /opt/ibdatacollector
sudo rsync -a ./ /opt/ibdatacollector/
sudo cp deploy/systemd/ibdatacollector.service /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable --now ibdatacollector
sudo journalctl -u ibdatacollector -f
```


---

## Preflight Checklist (built-in)

Startup scripts run a preflight step before building/starting:

- Disk space (warn if < 2GB free)
- Directory permissions (data/logs/run writable)
- Config sanity:
  - counts of symbols with trades/depth enabled
  - note: L2 depth requires IB depth entitlements
- IB reachability (only when `USE_IBAPI=true`)
  - **Auto-detects port** by testing: `7497, 4002, 7496, 4001`
  - uses the first reachable port unless `IB_PORT` is explicitly set

If preflight fails, the startup script aborts with errors.


---

## Alternative Data Source: Alpaca Market Data

The collector can run with either Interactive Brokers ("IB") or Alpaca ("Alpaca") as the live market data provider.

### Configure Alpaca
In `appsettings.json`:

```json
{
  "DataSource": "Alpaca",
  "Alpaca": {
    "KeyId": "YOUR_KEY_ID",
    "SecretKey": "YOUR_SECRET_KEY",
    "Feed": "iex",
    "UseSandbox": false,
    "SubscribeQuotes": false
  },
  "Symbols": [
    { "Symbol": "AAPL", "SubscribeTrades": true, "SubscribeDepth": false }
  ]
}
```

Notes:
- Alpaca real-time stock data is provided via WebSocket streams with message authentication and subscribe actions. (See Alpaca docs.)
- This integration currently supports **trade prints**. Full Level-2 depth is not supported for stocks via Alpaca; you can optionally subscribe to quotes for future BBO support.

### Startup scripts
If you set `DataSource` to `Alpaca`, set `USE_IBAPI=false` in the startup scripts (or omit it). IB connectivity checks are skipped when IB is disabled.


---

## Alpaca quote context for aggressor inference

When `DataSource` is `Alpaca`, the system can ingest **quote (BBO)** updates and use them to infer trade aggressor side:

- Trade price >= Ask => Buy aggressor
- Trade price <= Bid => Sell aggressor
- Otherwise => Unknown

To enable quote ingestion in Alpaca mode, set:

```json
"Alpaca": { "SubscribeQuotes": true }
```

This will emit `MarketEventType.BboQuote` events with `BboQuotePayload` and improve `Trade` + `OrderFlow` aggressor classification.

---

## Known Issues and Limitations

### Critical Issues (Recently Fixed)
- **Subscription Bug (Fixed):** Trade subscriptions were not being registered due to a logic error where `SubscribeDepth` was checked twice instead of `SubscribeTrades` in Program.cs.

### Current Limitations

| Issue | Impact | Workaround |
|-------|--------|------------|
| No logging framework | Errors may be silently swallowed | Check console output; monitor status.json |
| No connection retry | Single connection attempt; fails immediately on error | Restart manually if connection drops |
| Alpaca quotes not wired to L2 | BBO data from Alpaca not fully utilized | Enable `SubscribeQuotes` for basic BBO events |
| No price/size validation | Potentially invalid data may be persisted | Validate data in downstream processing |

### Security Warnings

⚠️ **Credential Storage:** Alpaca API credentials are stored in plaintext in `appsettings.json`. For production use:
1. Use environment variables instead of config files
2. Consider Azure Key Vault, AWS Secrets Manager, or HashiCorp Vault
3. Use .NET User Secrets for local development
4. Never commit credentials to version control

### Deprecated Code

The file `Domain/LightweightMarketDepthCollector.cs` is deprecated and unused. It uses an old namespace and enum naming convention. Consider deleting this file to reduce confusion.

---

## Troubleshooting

### Common Issues

**Problem:** Trades not being recorded
- **Cause:** Prior to the recent fix, `SubscribeTrades` was not being evaluated correctly
- **Solution:** Ensure you have the latest code with the subscription bug fix

**Problem:** Connection drops without retry
- **Cause:** No exponential backoff retry logic implemented
- **Solution:** Restart the collector manually; consider implementing retry logic

**Problem:** Config errors not visible
- **Cause:** Bare catch blocks in config loading code
- **Solution:** Check console stderr for warning messages; consider adding structured logging

**Problem:** Alpaca WebSocket disconnects silently
- **Cause:** No automatic reconnection or heartbeat mechanism
- **Solution:** Monitor status.json for stale timestamps; restart if needed
