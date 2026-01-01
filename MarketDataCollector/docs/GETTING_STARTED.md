# Getting Started with Market Data Collector

This guide walks you through setting up and running Market Data Collector for the first time, from installation to capturing your first market data.

## 🚀 Quickest Start (Recommended)

**Download, unzip, and run in 3 steps:**

1. **Download** the pre-built executable for your platform from [Releases](https://github.com/rodoHasArrived/Test/releases)
2. **Unzip** the archive
3. **Run** the web dashboard:
   ```bash
   ./MarketDataCollector --ui
   ```
4. **Open** your browser to `http://localhost:8080`

That's it! The web dashboard will guide you through configuration.

---

## Installation Options

### Option 1: One-Click Installer (Easiest)

**Windows:**
```powershell
# Download and run the installer
curl -O https://github.com/rodoHasArrived/Test/releases/latest/download/install.ps1
powershell -ExecutionPolicy Bypass -File install.ps1
```

**Linux/macOS:**
```bash
# Download and run the installer
curl -fsSL https://github.com/rodoHasArrived/Test/releases/latest/download/install.sh | bash
```

The installer will:
- Download the latest version
- Extract to a convenient location
- Set up configuration files
- Create desktop shortcuts
- Add to system PATH (optional)

### Option 2: Download Pre-Built Executable

1. Go to [Releases](https://github.com/rodoHasArrived/Test/releases/latest)
2. Download for your platform:
   - **Windows**: `MarketDataCollector-win-x64.zip`
   - **Linux**: `MarketDataCollector-linux-x64.tar.gz`
   - **macOS**: `MarketDataCollector-osx-x64.tar.gz`
3. Extract the archive
4. Run `./MarketDataCollector --ui`

### Option 3: Build from Source

**Prerequisites:**
- .NET 8.0 SDK or later
- Git

**Steps:**

```bash
# Clone the repository
git clone https://github.com/rodoHasArrived/Test.git
cd Test/MarketDataCollector

# Build the project
dotnet build

# Run from source
dotnet run --project src/MarketDataCollector/MarketDataCollector.csproj -- --ui
```

**Or publish as single executable:**

```bash
# Windows
./publish.ps1

# Linux/macOS
./publish.sh

# Executable will be in ./publish/<platform>/
```

---

## 🎯 First-Time Setup

### Step 1: Start the Web Dashboard

The easiest way to configure everything is through the web dashboard:

```bash
./MarketDataCollector --ui
```

Then open `http://localhost:8080` in your browser.

### Step 2: Choose Your Data Provider

In the dashboard, select your data provider:

**Option A: Alpaca (Recommended for Beginners)**
- ✅ Free tier available
- ✅ Real-time US equity data
- ✅ Easy API setup
- ❌ No Level 2 depth data

1. Sign up at [alpaca.markets](https://alpaca.markets)
2. Get your API keys from the dashboard
3. Enter them in the web UI under "Data Provider → Alpaca Settings"

**Option B: Interactive Brokers**
- ✅ Level 2 market depth
- ✅ Global markets
- ✅ Professional-grade data
- ❌ Requires TWS/Gateway running
- ❌ More complex setup

See [Interactive Brokers Setup](#interactive-brokers-setup) below.

### Step 3: Configure Storage

In the dashboard, go to "Storage Settings":

1. **Data Root Path**: Where data will be saved (default: `./data`)
2. **Naming Convention**: How files are organized (recommended: "By Symbol")
3. **Date Partitioning**: File splitting strategy (recommended: "Daily")
4. **Compression**: Enable to save disk space (recommended: Enabled)

The preview will show you exactly where files will be saved.

### Step 4: Add Symbols to Track

In the dashboard, scroll to "Subscribed Symbols":

1. Enter a symbol (e.g., `AAPL`, `SPY`, `TSLA`)
2. Choose what data to collect:
   - ✅ **Trades**: Tick-by-tick trade data (recommended)
   - **Depth**: Level 2 order book (IB only, resource-intensive)
3. Click "Add Symbol"

Start with 1-3 symbols to test, then scale up.

### Step 5: Run the Collector

Now you have two options:

**Option A: Continue in Web Dashboard**

The web dashboard includes status monitoring, so you can keep it running:
- Real-time metrics
- Connection status
- Error notifications

**Option B: Run in Production Mode**

For production deployment, run with status endpoint and config hot-reload:

```bash
./MarketDataCollector --serve-status --watch-config
```

You can still access the dashboard at `http://localhost:8080/status` to monitor.

---

## 📊 Verify Data Collection

### Check Files

Data is written to JSONL files in your configured directory:

```bash
# View the data directory structure
ls -R ./data

# View recent trades for a symbol
tail -f ./data/AAPL/Trade/2024-01-15.jsonl
```

### Check Dashboard

Open `http://localhost:8080` (if running with `--ui` or `--serve-status`) to see:
- **Published**: Events successfully written
- **Dropped**: Events lost due to backpressure (should be 0)
- **Integrity**: Data quality issues detected
- **Connection Status**: Connected/Disconnected

### Check Logs

Logs are saved in the data directory:

```bash
# View today's log
tail -f ./data/_logs/collector-$(date +%Y-%m-%d).log

# Search for errors
grep ERROR ./data/_logs/*.log
```

---

## 🔧 Provider-Specific Setup

### Interactive Brokers Setup

**Prerequisites:**
- IB account (live or paper trading)
- TWS or IB Gateway installed

**Steps:**

1. **Install IB TWS or Gateway**
   - Download from [Interactive Brokers](https://www.interactivebrokers.com/en/trading/tws.php)
   - Install and log in

2. **Enable API Access**
   - In TWS: **File → Global Configuration → API → Settings**
   - ✅ Check "Enable ActiveX and Socket Clients"
   - ✅ Check "Read-Only API" (recommended for data collection)
   - Note the **Socket Port**:
     - 7497 for paper trading (TWS)
     - 7496 for live trading (TWS)
     - 4002 for paper trading (Gateway)
     - 4001 for live trading (Gateway)
   - Optional: Add trusted IPs (127.0.0.1 for local)

3. **Configure in Web Dashboard**
   - Select "Interactive Brokers" as data provider
   - Or manually edit `appsettings.json`:
     ```json
     {
       "DataSource": "IB"
     }
     ```

4. **Set Environment Variables** (Optional)
   ```bash
   export IB_HOST=127.0.0.1
   export IB_PORT=7497
   export IB_CLIENT_ID=17
   ```

5. **Start Collecting**
   ```bash
   ./MarketDataCollector --serve-status
   ```

**IB-Specific Symbol Configuration:**

For IB, you can specify additional symbol details:

```json
{
  "Symbol": "SPY",
  "SubscribeTrades": true,
  "SubscribeDepth": true,
  "DepthLevels": 10,
  "SecurityType": "STK",
  "Exchange": "SMART",
  "Currency": "USD",
  "PrimaryExchange": "ARCA"
}
```

**For preferred stocks:**
```json
{
  "Symbol": "PCG",
  "LocalSymbol": "PCG PRA",
  "SecurityType": "STK",
  "Exchange": "NYSE"
}
```

See [interactive-brokers-setup.md](interactive-brokers-setup.md) for advanced IB configuration.

### Alpaca Setup

**Prerequisites:**
- Alpaca account (free tier available)

**Steps:**

1. **Sign Up**
   - Go to [alpaca.markets](https://alpaca.markets)
   - Create a free account

2. **Get API Keys**
   - Dashboard → Paper Trading → API Keys
   - Click "Generate New Key"
   - Copy your **Key ID** and **Secret Key**
   - ⚠️ Keep your secret key secure!

3. **Configure in Web Dashboard**
   - Select "Alpaca" as data provider
   - Enter your Key ID and Secret Key
   - Choose feed type:
     - **IEX** (free) - 15-minute delayed
     - **SIP** (paid subscription) - Real-time
   - Choose environment:
     - **Sandbox** for testing
     - **Production** for live data

   Or edit `appsettings.json`:
   ```json
   {
     "DataSource": "Alpaca",
     "Alpaca": {
       "KeyId": "YOUR_KEY_ID",
       "SecretKey": "YOUR_SECRET_KEY",
       "Feed": "iex",
       "UseSandbox": false,
       "SubscribeQuotes": true
     }
   }
   ```

4. **Start Collecting**
   ```bash
   ./MarketDataCollector --serve-status
   ```

**Using Environment Variables** (More Secure):

```bash
export ALPACA_KEY_ID="your-key-id"
export ALPACA_SECRET_KEY="your-secret-key"
./MarketDataCollector --serve-status
```

---

## 📅 Historical Backfill (Optional)

Before collecting real-time data, you may want to download historical data to fill gaps.

### Via Web Dashboard

1. Open `http://localhost:8080`
2. Scroll to "Historical Backfill"
3. Select provider (currently: Stooq)
4. Enter symbols: `AAPL,MSFT,GOOGL`
5. Select date range
6. Click "Start Backfill"

### Via Command Line

```bash
./MarketDataCollector --backfill \
  --backfill-provider stooq \
  --backfill-symbols AAPL,MSFT,GOOGL \
  --backfill-from 2024-01-01 \
  --backfill-to 2024-12-31
```

### Via Configuration File

Enable automatic backfill on startup:

```json
{
  "Backfill": {
    "Enabled": true,
    "Provider": "stooq",
    "Symbols": ["AAPL", "MSFT", "GOOGL"],
    "From": "2024-01-01",
    "To": "2024-12-31"
  }
}
```

---

## 🧪 Run Self-Tests

Verify everything is working correctly:

```bash
./MarketDataCollector --selftest
```

This tests:
- ✅ Order book integrity checking
- ✅ Event pipeline
- ✅ Storage system
- ✅ Configuration validation

---

## 📖 Next Steps

### Learn More
- **[HELP.md](../HELP.md)** - Comprehensive user guide
- **[CONFIGURATION.md](CONFIGURATION.md)** - All configuration options
- **[TROUBLESHOOTING.md](TROUBLESHOOTING.md)** - Common issues and solutions
- **[architecture.md](architecture.md)** - System design and architecture

### Production Deployment
- **[operator-runbook.md](operator-runbook.md)** - Production operations guide
- **Deploy as systemd service** (Linux)
- **Run as Windows service**
- **Docker deployment**

### Advanced Features
- **MassTransit Integration** - Distributed messaging
- **QuantConnect LEAN Integration** - Algorithmic trading
- **Prometheus Monitoring** - Metrics and alerting
- **Custom Data Providers** - Extend the system

---

## ❓ Common Issues

### "Configuration file not found"

**Solution:**
```bash
# Option 1: Copy sample configuration
cp appsettings.sample.json appsettings.json

# Option 2: Use web dashboard to create config
./MarketDataCollector --ui
```

### "Permission denied"

**Linux/macOS:**
```bash
chmod +x MarketDataCollector
```

**Windows:**
Right-click → Properties → Unblock

### "Port already in use"

Change the port:
```bash
./MarketDataCollector --ui --http-port 9000
```

### "Alpaca authentication failed"

- Verify API keys are correct
- Check that you're using the right environment (sandbox vs production)
- Ensure keys match the environment

### "IB connection failed"

1. Ensure TWS/Gateway is running
2. Check API is enabled in TWS settings
3. Verify port number (7497 for TWS paper, 4001 for Gateway live)
4. Check firewall isn't blocking connection
5. Try increasing connection timeout in config

### "No data being written"

1. Check connection status in dashboard
2. Verify symbols are correctly configured
3. Check market hours (markets must be open for live data)
4. Review logs for errors: `./data/_logs/*.log`
5. Ensure data directory has write permissions

For more issues, see **[TROUBLESHOOTING.md](TROUBLESHOOTING.md)**.

---

## 🎓 Quick Reference

### Command Line Options

```bash
# Start web dashboard
./MarketDataCollector --ui

# Production mode
./MarketDataCollector --serve-status --watch-config

# Run backfill
./MarketDataCollector --backfill

# Run self-tests
./MarketDataCollector --selftest

# Show help
./MarketDataCollector --help
```

### File Locations

```
MarketDataCollector/
├── MarketDataCollector       # Executable
├── appsettings.json          # Configuration
├── data/                     # Market data
│   ├── AAPL/
│   │   ├── Trade/
│   │   └── Quote/
│   └── _logs/                # Application logs
└── HELP.md                   # User guide
```

### Web Dashboard URLs

- Dashboard: `http://localhost:8080`
- Status API: `http://localhost:8080/api/status`
- Metrics: `http://localhost:8080/metrics`

---

## 📞 Getting Help

- **Documentation**: See [HELP.md](../HELP.md)
- **Issues**: [GitHub Issues](https://github.com/rodoHasArrived/Test/issues)
- **Examples**: Check the `examples/` directory
- **Logs**: Review `./data/_logs/` for detailed error messages

---

**You're all set!** Start capturing market data with confidence. 🚀
