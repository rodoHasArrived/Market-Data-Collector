# Market Data Collector - User Guide

Welcome to the Market Data Collector! This comprehensive guide will help you get started and make the most of the system.

## Table of Contents

- [Overview](#overview)
- [Quick Start](#quick-start)
- [Installation](#installation)
- [Configuration](#configuration)
- [Data Providers](#data-providers)
- [Multi-Provider Support](#multi-provider-support)
- [Storage Settings](#storage-settings)
- [Symbol Management](#symbol-management)
- [Historical Backfill](#historical-backfill)
- [Offline Storage & Archival](#offline-storage--archival)
- [Web Dashboard](#web-dashboard)
- [Windows Desktop App](#windows-desktop-app)
- [Command Line Usage](#command-line-usage)
- [Troubleshooting](#troubleshooting)
- [FAQ](#faq)

## Overview

Market Data Collector is a high-performance system for capturing real-time and historical market data. It supports multiple data providers and offers flexible storage options for researchers, traders, and quantitative analysts.

### Key Features

- **Multi-Provider Support**: Interactive Brokers, Alpaca, and Polygon
- **Simultaneous Connections**: Connect to multiple providers at once for comparison and failover
- **Real-Time Data Collection**: Tick-by-tick trades, Level 2 order books, and quotes
- **Historical Backfill**: Download historical data to fill gaps
- **Flexible Storage**: Multiple file organization strategies
- **Offline Storage & Archival**: Portable packages, data completeness calendar, archive browser
- **Batch Export Scheduler**: Automate recurring exports with format conversion
- **Automatic Failover**: Configure failover rules between providers
- **Web Dashboard**: Easy-to-use interface for configuration and monitoring
- **Windows Desktop App**: Native UWP/XAML application with secure credential management
- **High Performance**: Event-driven architecture with backpressure handling
- **Production Ready**: Comprehensive error handling, logging, and monitoring
- **Secure Credentials**: Windows CredentialPicker integration for API keys

## Quick Start

### 1. Start the Application

**Option A: Web Dashboard (Cross-platform)**

The easiest way to get started is with the web dashboard:

```bash
./MarketDataCollector --ui
```

Then open your browser to `http://localhost:8080`

**Option B: Windows Desktop App**

For secure credential management on Windows:

```bash
dotnet run --project src/MarketDataCollector.Uwp/MarketDataCollector.Uwp.csproj
```

### 2. Configure Your Data Provider

Choose between:
- **Interactive Brokers**: Best for Level 2 market depth data
- **Alpaca**: Best for real-time US equities with free tier available

### 3. Add Symbols to Track

Add the stock symbols you want to collect data for (e.g., AAPL, MSFT, TSLA)

### 4. Configure Storage

Choose where and how you want data to be stored

### 5. Start Collecting

Run the collector in production mode:

```bash
./MarketDataCollector --serve-status --watch-config
```

## Installation

### Prerequisites

- **Operating System**: Windows, Linux, or macOS
- **.NET Runtime**: Included in the single executable (self-contained)
- **Disk Space**: Depends on the number of symbols and data retention
- **Network**: Internet connection for data providers

### Download and Install

1. Download the appropriate executable for your platform:
   - Windows: `MarketDataCollector-win-x64.exe`
   - Linux: `MarketDataCollector-linux-x64`
   - macOS: `MarketDataCollector-osx-x64`

2. Make executable (Linux/macOS):
   ```bash
   chmod +x MarketDataCollector-linux-x64
   ```

3. Create configuration file:
   ```bash
   cp appsettings.sample.json appsettings.json
   ```

4. Edit `appsettings.json` with your settings

## Configuration

### Configuration File Location

The application looks for `appsettings.json` in the same directory as the executable.

### Basic Configuration

```json
{
  "DataRoot": "data",
  "Compress": false,
  "DataSource": "IB",
  "Symbols": [
    {
      "Symbol": "AAPL",
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

### Configuration Options

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `DataRoot` | string | "data" | Directory where market data will be stored |
| `Compress` | boolean | false | Enable gzip compression for storage files |
| `DataSource` | string | "IB" | Data provider: "IB", "Alpaca", or "Polygon" |
| `Symbols` | array | [] | List of symbols to collect data for |

### Hot Reload

When running with `--watch-config`, the application will automatically reload configuration changes without restarting. This allows you to:
- Add or remove symbols
- Change storage settings
- Update provider credentials

## Data Providers

### Interactive Brokers (IB)

**Requirements:**
- TWS (Trader Workstation) or IB Gateway running
- API connections enabled in TWS/Gateway settings
- Valid IB account

**Setup:**

1. Start TWS or IB Gateway
2. Enable API connections:
   - Go to File > Global Configuration > API > Settings
   - Check "Enable ActiveX and Socket Clients"
   - Note the Socket Port (default: 7497 for TWS, 4001 for Gateway)

3. Configure in `appsettings.json`:
   ```json
   {
     "DataSource": "IB"
   }
   ```

4. Set environment variables (optional):
   ```bash
   export IB_HOST=127.0.0.1
   export IB_PORT=7497
   export IB_CLIENT_ID=17
   ```

**Supported Data Types:**
- ✅ Tick-by-tick trades
- ✅ Level 2 market depth (order book)
- ✅ Best bid/offer quotes
- ✅ Market microstructure data

### Alpaca

**Requirements:**
- Alpaca account (free tier available)
- API credentials from Alpaca dashboard

**Setup:**

1. Sign up at https://alpaca.markets
2. Get your API Key ID and Secret Key from the dashboard
3. Configure in the web UI or `appsettings.json`:

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

**Feed Options:**
- `iex`: Free IEX feed (delayed)
- `sip`: Paid SIP feed (real-time)
- `delayed_sip`: Delayed SIP feed

**Supported Data Types:**
- ✅ Real-time trades
- ✅ Real-time quotes
- ✅ Bar/candlestick data

### Polygon

**Note:** Polygon support is in development. IB and Alpaca are recommended for production use.

## Multi-Provider Support

Market Data Collector v1.2 introduces the ability to connect to multiple data providers simultaneously for enhanced data quality and reliability.

### Simultaneous Connections

Connect to multiple providers at the same time to:
- Compare data quality across sources
- Implement automatic failover
- Collect from multiple sources for reconciliation

**Via Web Dashboard:**
1. Navigate to "Multi-Provider Connections" section
2. Click "Add Provider Connection"
3. Configure provider ID, type, and credentials
4. Repeat for additional providers

**Configuration Example:**
```json
{
  "DataSources": {
    "Sources": [
      {
        "Id": "ib_primary",
        "Name": "Interactive Brokers Primary",
        "Provider": "IB",
        "Priority": 1,
        "Enabled": true
      },
      {
        "Id": "alpaca_backup",
        "Name": "Alpaca Backup",
        "Provider": "Alpaca",
        "Priority": 2,
        "Enabled": true,
        "Alpaca": {
          "KeyId": "YOUR_KEY",
          "SecretKey": "YOUR_SECRET"
        }
      }
    ],
    "EnableFailover": true,
    "FailoverTimeoutSeconds": 30
  }
}
```

### Provider Comparison

Compare data quality metrics side-by-side across all connected providers:

| Metric | Description |
|--------|-------------|
| Data Quality Score | Overall score (0-100%) based on connection stability, latency, and drop rate |
| Trades Received | Total trade events received |
| Depth Updates | Total order book updates |
| Average Latency | Mean message processing latency |
| Messages Dropped | Events dropped due to backpressure |
| Connection Success Rate | Percentage of successful connection attempts |

Access via the "Provider Comparison" section in the web dashboard or `/api/multiprovider/comparison` endpoint.

### Automatic Failover

Configure automatic failover rules to maintain data collection when providers fail:

**Failover Rule Configuration:**
```json
{
  "FailoverRules": [
    {
      "Id": "primary_failover",
      "PrimaryProviderId": "ib_primary",
      "BackupProviderIds": ["alpaca_backup", "polygon_tertiary"],
      "FailoverThreshold": 3,
      "RecoveryThreshold": 5,
      "DataQualityThreshold": 70,
      "MaxLatencyMs": 1000
    }
  ]
}
```

**Failover Triggers:**
- **Consecutive Failures**: Failover after N consecutive connection/data failures
- **Data Quality**: Failover when quality score drops below threshold
- **Latency**: Failover when latency exceeds maximum acceptable value

**Auto-Recovery**: When the primary provider recovers, subscriptions automatically migrate back.

### Provider Symbol Mapping

Different providers may use different symbols for the same security. Configure mappings to normalize symbols:

**Via Web Dashboard:**
1. Navigate to "Provider Symbol Mapping" section
2. Add canonical symbol and provider-specific variants
3. Optionally include FIGI, ISIN, or CUSIP identifiers

**Example Mappings:**
| Canonical | IB | Alpaca | Polygon | FIGI |
|-----------|-----|--------|---------|------|
| BRK.B | BRK B | BRK.B | BRK.B | BBG000DWG505 |
| PCG.PRA | PCG PRA | PCG-A | PCG/A | BBG00123ABC |

**Import/Export:** Use CSV for bulk symbol mapping management.

## Storage Settings

### Naming Conventions

The naming convention determines how files are organized in directories:

#### 1. Flat
All files in one directory: `{root}/{prefix}{symbol}_{type}_{date}.jsonl`

**Example:** `data/market_AAPL_Trade_2024-01-15.jsonl`

**Use When:** You have a small number of symbols and want simple organization

#### 2. By Symbol (Recommended)
Organized by symbol, then data type: `{root}/{symbol}/{type}/{prefix}{date}.jsonl`

**Example:** `data/AAPL/Trade/2024-01-15.jsonl`

**Use When:** You want to easily access all data for a specific symbol

#### 3. By Date
Organized by date, then symbol: `{root}/{date}/{symbol}/{prefix}{type}.jsonl`

**Example:** `data/2024-01-15/AAPL/Trade.jsonl`

**Use When:** You want to process data by time periods

#### 4. By Type
Organized by data type, then symbol: `{root}/{type}/{symbol}/{prefix}{date}.jsonl`

**Example:** `data/Trade/AAPL/2024-01-15.jsonl`

**Use When:** You want to analyze specific data types across all symbols

### Date Partitioning

Controls how data is split across time periods:

- **None**: Single file per symbol/type combination
- **Daily** (Recommended): New file each day
- **Hourly**: New file each hour (for high-frequency trading)
- **Monthly**: New file each month (for long-term storage)

### Compression

Enable gzip compression to reduce disk space usage:

```json
{
  "Compress": true
}
```

**Savings:** Typically 80-90% reduction in file size

**Trade-off:** Slightly slower write performance (usually negligible)

### Data Format

All data is stored in **JSON Lines (JSONL)** format:
- One JSON object per line
- Easy to stream and process
- Human-readable
- Compatible with many tools (pandas, jq, etc.)

**Example Trade Event:**
```json
{"timestamp":"2024-01-15T14:30:00.123Z","symbol":"AAPL","type":"Trade","price":150.25,"size":100,"aggressorSide":"Buy"}
```

## Symbol Management

### Adding Symbols

#### Via Web Dashboard:
1. Navigate to the "Subscribed Symbols" section
2. Fill in symbol details
3. Click "Add Symbol"

#### Via Configuration File:
```json
{
  "Symbols": [
    {
      "Symbol": "AAPL",
      "SubscribeTrades": true,
      "SubscribeDepth": false,
      "DepthLevels": 10,
      "SecurityType": "STK",
      "Exchange": "SMART",
      "Currency": "USD"
    }
  ]
}
```

### Symbol Options

| Option | Type | Description |
|--------|------|-------------|
| `Symbol` | string | Ticker symbol (e.g., "AAPL") |
| `SubscribeTrades` | boolean | Collect trade/tick data |
| `SubscribeDepth` | boolean | Collect Level 2 order book (IB only) |
| `DepthLevels` | integer | Number of price levels to track (5-20 typical) |
| `SecurityType` | string | "STK", "OPT", "FUT", etc. |
| `Exchange` | string | Exchange routing (IB: "SMART" recommended) |
| `Currency` | string | "USD", "EUR", etc. |
| `LocalSymbol` | string | IB local symbol for specific securities |
| `PrimaryExchange` | string | Primary listing exchange (e.g., "NYSE") |

### Removing Symbols

#### Via Web Dashboard:
Click the "Delete" button next to the symbol

#### Via Configuration File:
Remove the symbol object from the `Symbols` array

## Historical Backfill

Download historical data to fill gaps or get initial dataset.

### Using the Web Dashboard

1. Navigate to "Historical Backfill" section
2. Select provider (currently: Stooq)
3. Enter comma-separated symbols: `AAPL,MSFT,TSLA`
4. Select date range
5. Click "Start Backfill"

### Using Command Line

```bash
./MarketDataCollector --backfill \
  --backfill-provider stooq \
  --backfill-symbols AAPL,MSFT \
  --backfill-from 2024-01-01 \
  --backfill-to 2024-12-31
```

### Backfill Providers

#### Stooq
- **Type:** Free end-of-day data
- **Coverage:** US equities, indices, ETFs
- **Resolution:** Daily bars
- **Delay:** End of day

### Backfill Data Format

Historical bars are converted to the same JSONL format as real-time data for consistency:

```json
{"timestamp":"2024-01-15T21:00:00Z","symbol":"AAPL","type":"HistoricalBar","open":150.0,"high":152.5,"low":149.5,"close":151.75,"volume":50000000}
```

## Offline Storage & Archival

Market Data Collector v1.2 includes comprehensive tools for managing archived data offline.

### Portable Data Packager

Create self-contained archive packages for data portability and backup:

**Features:**
- Package data by symbol, date range, or event type
- Include manifests and schemas for self-documentation
- SHA256 checksums for integrity verification
- Optional encryption for sensitive data
- Multiple formats: ZIP, TAR.GZ, 7Z

**Via Web Dashboard:**
1. Navigate to "Archive Browser" section
2. Select files/folders to package
3. Choose package format and options
4. Click "Create Package"

**Package Structure:**
```
MarketData_2026-01.zip
├── manifest.json         # Package metadata
├── README.md             # Usage documentation
├── schemas/              # JSON schemas for event types
│   ├── Trade_schema.json
│   └── Quote_schema.json
├── data/                 # Market data files
│   ├── AAPL/
│   └── MSFT/
├── verification/
│   └── checksums.sha256  # File integrity checksums
```

### Data Completeness Calendar

Visualize data coverage and identify gaps across your archive:

**Features:**
- Calendar heatmap showing completeness by date
- Per-symbol completeness tracking
- Gap detection with trading calendar awareness
- One-click backfill for missing dates
- Completeness scoring (0-100%)

**Completeness Status Colors:**
- 🟢 **Green (>99%)**: Complete data
- 🟡 **Yellow (95-99%)**: Minor gaps
- 🟠 **Orange (80-95%)**: Significant gaps
- 🔴 **Red (<80%)**: Major issues
- ⚫ **Gray**: Non-trading day (weekend/holiday)

**Via UWP Desktop App:**
1. Navigate to "Data Completeness" page
2. Select date range and symbols
3. View calendar heatmap
4. Click any day for drill-down details
5. Use "Backfill Gaps" to queue missing data

### Archive Browser

Browse and inspect archived data files:

**Navigation:**
- Tree view: Year → Month → Day → Symbol → Event Type
- File metadata: size, checksum, event count, timestamps
- Quick preview: first/last 100 events without full load

**File Operations:**
- **Preview**: View sample events
- **Verify**: Check file integrity
- **Compare**: Detect duplicates or changes
- **Export**: Copy files to another location
- **Search**: Find events by timestamp or content

**Via UWP Desktop App:**
1. Navigate to "Archive Browser" page
2. Browse the hierarchical tree
3. Right-click files for context menu
4. Use search bar for filtering

### Batch Export Scheduler

Automate recurring data exports:

**Job Configuration:**
```json
{
  "Name": "Daily Python Export",
  "SourcePath": "/data",
  "DestinationPath": "/exports/{year}/{month}/",
  "Symbols": ["AAPL", "MSFT", "GOOGL"],
  "EventTypes": ["Trade", "BboQuote"],
  "DateRange": "yesterday",
  "Format": "parquet",
  "Schedule": {
    "Frequency": "Daily",
    "TimeOfDay": "06:00"
  },
  "IncrementalMode": true
}
```

**Export Formats:**
- **Raw**: Original JSONL files (optionally decompressed)
- **CSV**: Comma-separated values for Excel/spreadsheets
- **Parquet**: Columnar format for Python/pandas
- **JSON Lines**: Decompressed JSONL

**Schedule Frequencies:**
- Hourly
- Daily (with specific time)
- Weekly (with day of week)
- Monthly (with day of month)

**Via Web Dashboard:**
1. Navigate to "Batch Export" section
2. Create new export job
3. Configure schedule and format
4. Monitor job status and history

## Web Dashboard

### Starting the Dashboard

```bash
./MarketDataCollector --ui
```

Access at: `http://localhost:8080`

### Custom Port

```bash
./MarketDataCollector --ui --http-port 9000
```

### Dashboard Features

1. **System Status**
   - Connection state
   - Real-time metrics
   - Last update timestamp

2. **Data Provider**
   - Switch between providers
   - Configure credentials
   - View provider-specific settings

3. **Storage Settings**
   - Configure data directory
   - Set naming convention
   - Enable compression
   - Preview file paths

4. **Historical Backfill**
   - Download historical data
   - View backfill status
   - Check progress

5. **Symbol Management**
   - Add/remove symbols
   - Configure subscription options
   - Manage IB-specific settings

### Dashboard Notifications

The dashboard shows toast notifications for:
- ✅ Successful operations
- ❌ Errors and failures
- ℹ️ Informational messages

## Windows Desktop App

The UWP/XAML desktop application provides a native Windows experience for configuring and monitoring Market Data Collector.

### Starting the Desktop App

```bash
dotnet run --project src/MarketDataCollector.Uwp/MarketDataCollector.Uwp.csproj
```

### Desktop App Features

The application includes dedicated pages for all collector functions:

1. **Dashboard Page**
   - Real-time system status
   - Live metrics and statistics
   - Connection state indicator
   - Event throughput monitoring

2. **Provider Page**
   - Select data provider (IB, Alpaca, Polygon)
   - Configure provider-specific settings
   - Test connection status

3. **Storage Page**
   - Configure data directory
   - Select naming convention
   - Set date partitioning options
   - Enable/disable compression
   - Preview file path structure

4. **Symbols Page**
   - Add and remove symbols
   - Configure subscription options
   - Set security type and exchange
   - Manage depth levels

5. **Backfill Page**
   - Select backfill provider
   - Enter symbols to backfill
   - Configure date range
   - Start and monitor backfill progress

6. **Settings Page**
   - General application settings
   - Logging configuration
   - Advanced options

### Secure Credential Management

The desktop app uses Windows CredentialPicker for secure API key management:

**How it works:**
1. Navigate to the Provider page
2. Click "Set Credentials" for your chosen provider
3. Windows CredentialPicker dialog appears
4. Enter your API credentials securely
5. Credentials are stored in Windows Credential Manager

**Benefits:**
- Credentials never stored in plain text files
- Protected by Windows security
- Integrated with Windows Hello (biometric auth)
- Separate from application data

**Supported Credentials:**
- Interactive Brokers: User ID (no password required for API)
- Alpaca: API Key ID and Secret Key
- Polygon: API Key

### Desktop App vs Web Dashboard

| Feature | Desktop App | Web Dashboard |
|---------|-------------|---------------|
| Platform | Windows only | Any browser |
| Credential Storage | Windows Credential Manager | appsettings.json |
| UI Framework | UWP/XAML + WinUI 3 | HTML/CSS/JavaScript |
| Offline Use | Yes | Requires collector running |
| Native Integration | Full Windows features | Browser sandboxed |

**When to use Desktop App:**
- Windows-only deployment
- Secure credential management is critical
- Native Windows experience preferred
- Integration with Windows ecosystem

**When to use Web Dashboard:**
- Cross-platform access
- Remote monitoring
- Quick configuration changes
- Existing browser workflow

## Command Line Usage

### Basic Modes

#### Production Mode (Recommended)
```bash
./MarketDataCollector --serve-status --watch-config
```

- `--serve-status`: Enable monitoring endpoint at `/status`
- `--watch-config`: Auto-reload configuration changes

#### Web Dashboard Mode
```bash
./MarketDataCollector --ui
```

#### Backfill Mode
```bash
./MarketDataCollector --backfill
```

#### Replay Mode (Testing)
```bash
./MarketDataCollector --replay /path/to/data.jsonl
```

### All Command Line Options

| Option | Description |
|--------|-------------|
| `--ui` | Start web dashboard interface |
| `--serve-status` | Enable HTTP status endpoint |
| `--watch-config` | Enable hot-reload of configuration |
| `--backfill` | Run historical data backfill |
| `--replay <path>` | Replay events from JSONL file |
| `--selftest` | Run system self-tests |
| `--http-port <port>` | Set HTTP server port (default: 8080) |
| `--status-port <port>` | Set status endpoint port |
| `--backfill-provider <name>` | Backfill provider to use |
| `--backfill-symbols <list>` | Comma-separated symbols to backfill |
| `--backfill-from <date>` | Backfill start date (YYYY-MM-DD) |
| `--backfill-to <date>` | Backfill end date (YYYY-MM-DD) |

### Examples

**Start with web UI and monitoring:**
```bash
./MarketDataCollector --ui --serve-status
```

**Run backfill for specific symbols:**
```bash
./MarketDataCollector --backfill \
  --backfill-symbols AAPL,MSFT,GOOGL \
  --backfill-from 2024-01-01 \
  --backfill-to 2024-12-31
```

**Production deployment with custom port:**
```bash
./MarketDataCollector --serve-status --watch-config --http-port 9090
```

## Troubleshooting

### Common Issues

#### 1. "Configuration file not found"

**Cause:** Missing `appsettings.json`

**Solution:**
```bash
cp appsettings.sample.json appsettings.json
# Edit appsettings.json with your settings
```

#### 2. "Connection failed to Interactive Brokers"

**Causes:**
- TWS/Gateway not running
- API connections not enabled
- Wrong port number

**Solution:**
1. Ensure TWS or IB Gateway is running
2. Check File > Global Configuration > API > Settings
3. Verify "Enable ActiveX and Socket Clients" is checked
4. Confirm port matches your configuration (7497 for TWS, 4001 for Gateway)

#### 3. "Alpaca authentication failed"

**Causes:**
- Invalid API credentials
- Wrong environment (sandbox vs production)

**Solution:**
1. Verify credentials in Alpaca dashboard
2. Check `UseSandbox` setting matches your credentials
3. Ensure KeyId and SecretKey are correct

#### 4. "Permission denied writing to data directory"

**Cause:** Insufficient file system permissions

**Solution:**
```bash
# Linux/macOS
chmod 755 ./data
# Or specify a different directory you have access to
```

#### 5. "High CPU usage"

**Causes:**
- Too many symbols with high-frequency updates
- Insufficient system resources

**Solution:**
1. Reduce number of subscribed symbols
2. Disable market depth for some symbols (depth is more resource-intensive)
3. Increase system resources

#### 6. "Data files not being created"

**Causes:**
- Collector not receiving data from provider
- Storage path issues
- No active symbols configured

**Solution:**
1. Check system status in dashboard
2. Verify provider connection is established
3. Confirm symbols are correctly configured
4. Check logs for errors

### Logging

Logs are stored in the data directory under `_logs/`:

```
data/
  _logs/
    collector-2024-01-15.log
```

**Log Levels:**
- **Debug**: Detailed diagnostic information
- **Information**: General informational messages
- **Warning**: Warning messages for non-critical issues
- **Error**: Error messages for failures
- **Fatal**: Critical errors that cause shutdown

**Viewing Logs:**
```bash
# View latest log
tail -f data/_logs/collector-$(date +%Y-%m-%d).log

# Search for errors
grep ERROR data/_logs/*.log
```

### Status Endpoint

When running with `--serve-status`, check system status:

```bash
curl http://localhost:8080/status | jq .
```

**Response:**
```json
{
  "timestampUtc": "2024-01-15T14:30:00Z",
  "isConnected": true,
  "metrics": {
    "published": 150000,
    "dropped": 0,
    "integrity": 5,
    "historicalBars": 1000
  }
}
```

### Prometheus Metrics

Metrics available at `/metrics` endpoint:

```bash
curl http://localhost:8080/metrics
```

Integrate with Prometheus/Grafana for monitoring.

## FAQ

### Q: How much disk space do I need?

**A:** It depends on:
- Number of symbols
- Data types (trades, depth, quotes)
- Trading hours and activity
- Compression enabled

**Estimates** (per symbol, per day, with compression):
- Trades only: 10-50 MB
- Trades + Depth: 100-500 MB
- Very active stocks: 1+ GB

### Q: Can I run multiple instances?

**A:** Yes, but:
- Each instance needs its own configuration file
- Each instance needs a unique data directory OR different symbols
- For IB: Each instance needs a unique Client ID

### Q: Is data collection real-time?

**A:** Yes! Data is captured as it arrives from providers:
- **IB**: True tick-by-tick real-time (with market data subscription)
- **Alpaca**: Real-time WebSocket streaming
- **Latency**: Typically <100ms from exchange to disk

### Q: Can I use the collected data with other tools?

**A:** Absolutely! Data is in JSONL format, easily loaded by:
- **Python pandas**: `pd.read_json(path, lines=True)`
- **QuantConnect LEAN**: Built-in integration
- **Command line tools**: `jq`, `grep`, etc.
- **Custom tools**: Any JSON parser

### Q: Do I need an IB subscription for market data?

**A:** Depends:
- **Real-time data**: Requires IB market data subscription
- **Delayed data**: Available without subscription (15-20 min delay)
- **Paper trading account**: Can access delayed data

### Q: Can I collect options, futures, or forex data?

**A:** Yes! Set the `SecurityType` in symbol configuration:
- `STK`: Stocks
- `OPT`: Options
- `FUT`: Futures
- `CASH`: Forex
- `IND`: Indices

**Example:**
```json
{
  "Symbol": "ESZ4",
  "SecurityType": "FUT",
  "Exchange": "CME",
  "Currency": "USD"
}
```

### Q: How do I backup my data?

**A:** Simply copy the data directory:

```bash
# Create backup
tar -czf backup-2024-01-15.tar.gz data/

# Restore backup
tar -xzf backup-2024-01-15.tar.gz
```

Consider:
- Cloud storage (AWS S3, Azure Blob, Google Cloud Storage)
- Regular automated backups
- Version control for configuration files

### Q: Can I run this in Docker?

**A:** Yes! Example Dockerfile:

```dockerfile
FROM mcr.microsoft.com/dotnet/runtime:8.0
COPY MarketDataCollector /app/MarketDataCollector
COPY appsettings.json /app/appsettings.json
WORKDIR /app
RUN chmod +x MarketDataCollector
EXPOSE 8080
CMD ["./MarketDataCollector", "--serve-status", "--watch-config"]
```

### Q: How do I stop the collector gracefully?

**A:** Press `Ctrl+C` or send SIGTERM:

```bash
# The application will:
# 1. Stop accepting new data
# 2. Flush all pending events to disk
# 3. Close connections gracefully
# 4. Exit with status 0
```

### Q: Can I change symbols without restarting?

**A:** Yes! If running with `--watch-config`:
1. Edit `appsettings.json`
2. Save the file
3. Application will reload automatically
4. New symbols will be subscribed
5. Removed symbols will be unsubscribed

## Support and Resources

### Documentation

- **README.md**: Project overview and quick start
- **HELP.md**: This comprehensive user guide (includes Windows Desktop App section)
- **docs/CONFIGURATION.md**: Detailed configuration reference
- **docs/GETTING_STARTED.md**: Step-by-step setup guide
- **docs/TROUBLESHOOTING.md**: Common issues and solutions
- **docs/architecture.md**: System design and architecture
- **docs/lean-integration.md**: QuantConnect Lean Engine integration
- **../docs/STORAGE_ORGANIZATION_DESIGN.md**: Advanced storage organization strategies

### Getting Help

1. **Check the logs**: Most issues are logged with detailed error messages
2. **Review documentation**: Comprehensive docs cover most scenarios
3. **GitHub Issues**: Report bugs or request features
4. **Community**: Share knowledge with other users

### Best Practices

1. **Start Small**: Begin with 1-3 symbols, then scale up
2. **Monitor Resources**: Watch CPU, memory, and disk usage
3. **Regular Backups**: Automate data backups
4. **Test First**: Use paper trading or sandbox accounts initially
5. **Update Regularly**: Keep software up to date for bug fixes and features
6. **Review Logs**: Periodically check logs for warnings or errors
7. **Validate Data**: Spot-check collected data for accuracy

### Contributing

We welcome contributions! If you've found a bug or have a feature request, please open an issue on GitHub.

---

**Version:** 1.2
**Last Updated:** 2026-01-03
**License:** See LICENSE file
