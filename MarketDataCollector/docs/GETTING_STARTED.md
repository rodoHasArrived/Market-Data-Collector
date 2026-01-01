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

Edit `appsettings.json` with your settings. See [CONFIGURATION.md](CONFIGURATION.md) for detailed options.

### 3. Run a Smoke Test

Test that everything is working without connecting to any data provider:

```bash
dotnet run --project src/MarketDataCollector/MarketDataCollector.csproj
```

This will run a simulated data test and write sample events to the `./data/` directory.

### 4. Run Self-Tests

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

- **Trade**: Tick-by-tick trade executions
- **Depth**: Level 2 market depth updates
- **Quote**: Best bid/offer (BBO) updates
- **OrderFlow**: Aggregated order flow statistics
- **Integrity**: Data quality alerts

## Replay Historical Data

Replay previously captured data for analysis:

```bash
dotnet run -- --replay ./data
```

## Next Steps

- Read [CONFIGURATION.md](CONFIGURATION.md) for all configuration options
- Check [TROUBLESHOOTING.md](TROUBLESHOOTING.md) if you encounter issues
- Review [architecture.md](architecture.md) for system design details
- See [operator-runbook.md](operator-runbook.md) for production deployment

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

For more issues, see [TROUBLESHOOTING.md](TROUBLESHOOTING.md).
