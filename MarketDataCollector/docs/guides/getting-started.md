# Getting Started with MarketDataCollector

This guide walks you through setting up and running MarketDataCollector for the first time.

## Prerequisites

- **.NET 8.0 SDK** or later
- **One of the following data providers**:
  - Interactive Brokers TWS or IB Gateway (for IB data)
  - Alpaca account with API keys (for Alpaca data)

## Quick Start

### 1. Clone and Build

```bash
git clone <repository-url>
cd MarketDataCollector
dotnet build
```

### 2. Configure Your Settings

Copy the sample configuration file:

```bash
cp appsettings.sample.json appsettings.json
```

Edit `appsettings.json` with your settings. See [Configuration Guide](configuration.md) for detailed options.

### 3. Run a Smoke Test

Test that everything is working without connecting to any data provider:

```bash
dotnet run --project src/MarketDataCollector/MarketDataCollector.csproj
```

This will run a simulated data test and write sample events to the `./data/` directory.

### 4. Monitor the Dashboard

Start the collector with the built-in HTTP dashboard and config hot reload:

```bash
dotnet run --project src/MarketDataCollector/MarketDataCollector.csproj -- --watch-config --http-port 8080
```

Open `http://localhost:8080` for the live dashboard, `/metrics` for Prometheus, and `/status` for JSON status output.

### 5. Run a Historical Backfill (Optional)

Prime the data directory with historical bars before live capture begins:

```bash
dotnet run --project src/MarketDataCollector/MarketDataCollector.csproj -- --backfill \
  --backfill-provider stooq --backfill-symbols SPY,QQQ --backfill-from 2024-01-01 --backfill-to 2024-01-05
```

You can also enable `Backfill.Enabled` in `appsettings.json` to run the default backfill automatically at startup. The dashboard exposes `/api/backfill/*` endpoints and a status panel to view results and rerun jobs.

### 6. Run Self-Tests

Verify the internal components are working correctly:

```bash
dotnet run --project src/MarketDataCollector/MarketDataCollector.csproj -- --selftest
```

## Provider-Specific Setup

### Interactive Brokers (IB)

1. **Install IB TWS or IB Gateway**
   - Download from [Interactive Brokers](https://www.interactivebrokers.com/en/trading/tws.php)
   - Install and log in with your account

2. **Configure API Access**
   - In TWS: File → Global Configuration → API → Settings
   - Enable "Enable ActiveX and Socket Clients"
   - Note the port number (default: 7497 for paper, 7496 for live)
   - Optional: Add trusted IPs

3. **Configure MarketDataCollector**
   ```json
   {
     "DataSource": "IB",
     "Symbols": [
       {
         "Symbol": "SPY",
         "SubscribeTrades": true,
         "SubscribeDepth": true,
         "DepthLevels": 10,
         "SecurityType": "STK",
         "Exchange": "SMART",
         "Currency": "USD"
       }
     ]
   }
   ```

4. **Run with IB**
   ```bash
   dotnet run --project src/MarketDataCollector/MarketDataCollector.csproj -- --serve-status
   ```

See [interactive-brokers-setup.md](interactive-brokers-setup.md) for detailed IB configuration.

### Alpaca

1. **Get API Keys**
   - Sign up at [Alpaca](https://alpaca.markets)
   - Go to Dashboard → Paper Trading → API Keys
   - Copy your Key ID and Secret Key

2. **Configure MarketDataCollector**

   **Option A: Environment Variables (Recommended)**
   ```bash
   export ALPACA_KEY_ID="your-key-id"
   export ALPACA_SECRET_KEY="your-secret-key"
   ```

   **Option B: Configuration File**
   ```json
   {
     "DataSource": "Alpaca",
     "Alpaca": {
       "KeyId": "your-key-id",
       "SecretKey": "your-secret-key",
       "Feed": "iex",
       "UseSandbox": false
     },
     "Symbols": [
       {
         "Symbol": "SPY",
         "SubscribeTrades": true
       }
     ]
   }
   ```

3. **Run with Alpaca**
   ```bash
   dotnet run --project src/MarketDataCollector/MarketDataCollector.csproj -- --serve-status
   ```

## Monitoring Dashboard

Start the collector with the status dashboard:

```bash
dotnet run -- --serve-status --status-port 8080
```

Then open http://localhost:8080 in your browser to see:
- Real-time metrics
- Event throughput
- Integrity events
- Memory usage

### Available Endpoints

| Endpoint | Description |
|----------|-------------|
| `/` | HTML dashboard with auto-refresh |
| `/health` | JSON health check with component status |
| `/healthz` | Kubernetes-compatible health probe |
| `/ready` | Readiness probe (200 if ready) |
| `/live` | Liveness probe (200 if alive) |
| `/metrics` | Prometheus-compatible metrics |
| `/status` | Full JSON status snapshot |
| `/api/backfill/*` | Backfill job management endpoints |

## UWP Desktop Application

For Windows users, a native desktop application provides a graphical interface for configuration and monitoring:

```bash
# Run the UWP app
dotnet run --project src/MarketDataCollector.Uwp/MarketDataCollector.Uwp.csproj
```

### Desktop App Features

- **Dashboard** - Real-time metrics with sparkline charts and data health gauges
- **Provider Management** - Configure IB, Alpaca, and other data sources with connection health monitoring
- **Storage Analytics** - Disk usage visualization with tiered storage (hot/warm/cold) configuration
- **Symbol Management** - Add, edit, and bulk import symbols with subscription templates
- **Backfill Control** - Schedule and monitor historical data backfill jobs
- **Trading Hours** - Configure market hours and session schedules
- **Data Export** - Export collected data in multiple formats (JSONL, Parquet, CSV)
- **Settings** - Theme customization, notifications, and keyboard shortcuts

## CLI Modes

The collector supports multiple operation modes via command-line arguments:

| Mode | Command | Description |
|------|---------|-------------|
| **Default** | `dotnet run` | Run smoke test with simulated data |
| **Self-Test** | `--selftest` | Run internal self-tests and exit |
| **Live Capture** | `--serve-status` | Connect to provider and capture data |
| **Web Dashboard** | `--ui [--http-port N]` | Start web-based dashboard UI |
| **HTTP Monitoring** | `--http-port N` | Start HTTP monitoring server |
| **Backfill** | `--backfill [options]` | Run historical data backfill |
| **Replay** | `--replay <path>` | Replay JSONL events for analysis |
| **Watch Config** | `--watch-config` | Enable hot-reload of configuration |

### Combined Mode Example

```bash
# Production mode with all features
dotnet run -- --serve-status --http-port 8080 --watch-config
```

## Output Data

Data is written to the `./data/` directory in JSONL (newline-delimited JSON) format:

```
data/
├── SPY/
│   ├── trade/
│   │   └── 2024-01-15.jsonl
│   ├── depth/
│   │   └── 2024-01-15.jsonl
│   └── quote/
│       └── 2024-01-15.jsonl
└── _logs/
    └── mdc-2024-01-15.log
```

### Event Types

- **Trade**: Tick-by-tick trade executions with sequence validation
- **L2Snapshot**: Level 2 market depth snapshots
- **BboQuote**: Best bid/offer (BBO) updates with spread and mid-price
- **OrderFlow**: Aggregated order flow statistics (VWAP, imbalance)
- **Integrity**: Trade sequence anomalies (gaps, out-of-order)
- **DepthIntegrity**: Order book integrity failures
- **HistoricalBar**: OHLCV bars from historical backfill
- **Heartbeat**: Connection health signals

## Replay Historical Data

Replay previously captured data for analysis:

```bash
dotnet run -- --replay ./data
```

## Next Steps

- Read [Configuration Guide](configuration.md) for all configuration options
- Check [Troubleshooting](troubleshooting.md) if you encounter issues
- Review [Architecture](../architecture/overview.md) for system design details
- See [Operator Runbook](operator-runbook.md) for production deployment

## Common Issues

### "Configuration file not found"
Copy `appsettings.sample.json` to `appsettings.json` and configure your settings.

### "Alpaca KeyId/SecretKey required"
Set your Alpaca credentials in `appsettings.json` or via environment variables.

### "IB connection failed"
1. Ensure TWS/Gateway is running
2. Check that API is enabled in TWS settings
3. Verify the port number matches
4. Check firewall settings

For more issues, see [Troubleshooting](troubleshooting.md).

---

**Version:** 1.4.0
**Last Updated:** 2026-01-04
**See Also:** [HELP.md](../../HELP.md) | [Configuration](configuration.md) | [Architecture](../architecture/overview.md) | [Lean Integration](../integrations/lean-integration.md)
