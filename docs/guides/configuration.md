# Configuration Guide

This document describes all configuration options available in MarketDataCollector.

## Configuration Sources

Configuration is loaded from multiple sources in order of precedence (highest first):

1. **Command-line arguments** (`--key value`)
2. **Environment variables** (`MDC_KEY` or provider-specific like `ALPACA_KEY_ID`)
3. **appsettings.json** file
4. **Default values** in code

## Credential Management

> **SECURITY WARNING**: Never store API keys, secrets, or passwords in configuration files.
> Always use environment variables for sensitive credentials.

### Required Environment Variables

Set these environment variables based on which providers you use:

#### Data Providers

| Provider | Environment Variable | Description |
|----------|---------------------|-------------|
| Alpaca | `ALPACA_KEY_ID` | Alpaca API Key ID |
| Alpaca | `ALPACA_SECRET_KEY` | Alpaca Secret Key |
| Polygon.io | `POLYGON_API_KEY` | Polygon API Key |
| Tiingo | `TIINGO_API_TOKEN` | Tiingo API Token |
| Finnhub | `FINNHUB_API_KEY` | Finnhub API Key |
| Alpha Vantage | `ALPHA_VANTAGE_API_KEY` | Alpha Vantage API Key |
| Nasdaq Data Link | `NASDAQ_API_KEY` | Nasdaq Data Link API Key |
| OpenFIGI | `OPENFIGI_API_KEY` | OpenFIGI API Key (optional, higher rate limits) |
| NYSE | `NYSE_API_KEY` | NYSE Connect API Key |
| NYSE | `NYSE_API_SECRET` | NYSE Connect API Secret |
| NYSE | `NYSE_CLIENT_ID` | NYSE OAuth Client ID |

### Setting Environment Variables

#### Linux/macOS (bash)

```bash
# Add to ~/.bashrc or ~/.zshrc for persistence
export ALPACA_KEY_ID="your-key-id"
export ALPACA_SECRET_KEY="your-secret-key"
export POLYGON_API_KEY="your-polygon-key"
export TIINGO_API_TOKEN="your-tiingo-token"
```

#### Windows (PowerShell)

```powershell
# Session only
$env:ALPACA_KEY_ID = "your-key-id"
$env:ALPACA_SECRET_KEY = "your-secret-key"

# Permanent (user scope)
[Environment]::SetEnvironmentVariable("ALPACA_KEY_ID", "your-key-id", "User")
[Environment]::SetEnvironmentVariable("ALPACA_SECRET_KEY", "your-secret-key", "User")
```

#### Windows (Command Prompt)

```cmd
set ALPACA_KEY_ID=your-key-id
set ALPACA_SECRET_KEY=your-secret-key

:: Permanent (use setx)
setx ALPACA_KEY_ID "your-key-id"
setx ALPACA_SECRET_KEY "your-secret-key"
```

#### Docker

```bash
# Using docker run
docker run -e ALPACA_KEY_ID=your-key-id \
           -e ALPACA_SECRET_KEY=your-secret-key \
           market-data-collector

# Using docker-compose.yml
services:
  collector:
    environment:
      - ALPACA_KEY_ID=${ALPACA_KEY_ID}
      - ALPACA_SECRET_KEY=${ALPACA_SECRET_KEY}
```

#### Kubernetes

```yaml
apiVersion: v1
kind: Secret
metadata:
  name: mdc-credentials
type: Opaque
stringData:
  ALPACA_KEY_ID: your-key-id
  ALPACA_SECRET_KEY: your-secret-key
---
apiVersion: apps/v1
kind: Deployment
spec:
  template:
    spec:
      containers:
        - name: collector
          envFrom:
            - secretRef:
                name: mdc-credentials
```

### .NET User Secrets (Local Development)

For local development, you can use .NET User Secrets:

```bash
cd src/MarketDataCollector
dotnet user-secrets init
dotnet user-secrets set "Alpaca:KeyId" "your-key-id"
dotnet user-secrets set "Alpaca:SecretKey" "your-secret-key"
```

### Secret Vault Integration

For production deployments, consider using a secrets management solution:

- **Azure Key Vault** - Use managed identity for Azure deployments
- **AWS Secrets Manager** - Use IAM roles for AWS deployments
- **HashiCorp Vault** - For multi-cloud or on-premises deployments

### Verifying Credentials

Check which credentials are configured:

```bash
# Run with --preflight to check configuration
dotnet run --project src/MarketDataCollector -- --preflight
```

The preflight check reports which providers have valid credentials configured.

## Core Settings

### DataRoot

**Type**: `string`
**Default**: `"data"`

Root directory for all data output. Can be relative or absolute path.

```json
{
  "DataRoot": "/var/lib/mdc/data"
}
```

### Compress

**Type**: `boolean`
**Default**: `false`

Enable gzip compression for JSONL output files. Files will have `.jsonl.gz` extension.

```json
{
  "Compress": true
}
```

### DataSource

**Type**: `string`
**Default**: `"IB"`
**Options**: `"IB"`, `"Alpaca"`, `"Polygon"`

Selects the market data provider.

```json
{
  "DataSource": "Alpaca"
}
```

## Provider Settings

### Interactive Brokers (IB)

IB configuration is primarily done through symbol settings. No additional root-level configuration is required.

Ensure TWS/Gateway is running with API enabled before starting the collector.

### Alpaca

**Section**: `Alpaca`

> **Credentials**: Set `ALPACA_KEY_ID` and `ALPACA_SECRET_KEY` environment variables.
> See [Credential Management](#credential-management) for details.

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| `Feed` | string | No | `"iex"` | Data feed: `"iex"` (free) or `"sip"` (paid) |
| `UseSandbox` | boolean | No | `false` | Use paper trading environment |
| `SubscribeQuotes` | boolean | No | `false` | Subscribe to quote (BBO) data |

```json
{
  "Alpaca": {
    "Feed": "sip",
    "UseSandbox": false,
    "SubscribeQuotes": true
  }
}
```

## Storage Settings

**Section**: `Storage`

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `NamingConvention` | string | `"BySymbol"` | File organization strategy |
| `DatePartition` | string | `"Daily"` | Date-based file splitting |
| `IncludeProvider` | boolean | `false` | Include provider name in paths |
| `FilePrefix` | string | `null` | Optional prefix for file names |
| `RetentionDays` | integer | `null` | Auto-delete files older than N days |
| `MaxTotalMegabytes` | integer | `null` | Cap total storage size in MB |

### NamingConvention Options

| Value | Structure | Best For |
|-------|-----------|----------|
| `Flat` | `{root}/{symbol}_{type}_{date}.jsonl` | Small datasets |
| `BySymbol` | `{root}/{symbol}/{type}/{date}.jsonl` | Per-symbol analysis |
| `ByDate` | `{root}/{date}/{symbol}/{type}.jsonl` | Daily batch processing |
| `ByType` | `{root}/{type}/{symbol}/{date}.jsonl` | Event type analysis |

### DatePartition Options

| Value | Description |
|-------|-------------|
| `None` | Single file per symbol/type |
| `Daily` | One file per day (default) |
| `Hourly` | One file per hour |
| `Monthly` | One file per month |

### Example

```json
{
  "Storage": {
    "NamingConvention": "ByDate",
    "DatePartition": "Daily",
    "IncludeProvider": true,
    "RetentionDays": 30,
    "MaxTotalMegabytes": 10240
  }
}
```

## Symbol Configuration

**Section**: `Symbols` (array)

Each symbol entry configures what data to collect and how to identify the instrument.

### Common Fields

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `Symbol` | string | Required | Ticker symbol |
| `SubscribeTrades` | boolean | `true` | Collect trade data |
| `SubscribeDepth` | boolean | `true` | Collect L2 depth data |
| `DepthLevels` | integer | `10` | Number of depth levels (1-50) |

### IB-Specific Fields

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `SecurityType` | string | `"STK"` | Contract type (STK, OPT, FUT, etc.) |
| `Exchange` | string | `"SMART"` | Exchange or routing destination |
| `Currency` | string | `"USD"` | Currency |
| `PrimaryExchange` | string | `null` | Primary listing exchange |
| `LocalSymbol` | string | `null` | Local symbol (recommended for preferreds) |
| `TradingClass` | string | `null` | Trading class |
| `ConId` | integer | `null` | IB Contract ID (overrides other fields) |

### Security Types

| Value | Description |
|-------|-------------|
| `STK` | Stock |
| `OPT` | Option |
| `FUT` | Future |
| `CASH` | Forex pair |
| `IND` | Index |
| `CFD` | Contract for Difference |
| `BOND` | Bond |
| `CMDTY` | Commodity |
| `FUND` | Mutual Fund |
| `WAR` | Warrant |

### Examples

**Standard Stock**:
```json
{
  "Symbol": "AAPL",
  "SubscribeTrades": true,
  "SubscribeDepth": true,
  "DepthLevels": 10,
  "SecurityType": "STK",
  "Exchange": "SMART",
  "Currency": "USD",
  "PrimaryExchange": "NASDAQ"
}
```

**Preferred Share** (IB requires LocalSymbol):
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

**Futures Contract**:
```json
{
  "Symbol": "ES",
  "SubscribeTrades": true,
  "SubscribeDepth": true,
  "DepthLevels": 10,
  "SecurityType": "FUT",
  "Exchange": "CME",
  "Currency": "USD",
  "LocalSymbol": "ESH5"
}
```

**Forex Pair**:
```json
{
  "Symbol": "EUR.USD",
  "SubscribeTrades": true,
  "SubscribeDepth": true,
  "SecurityType": "CASH",
  "Exchange": "IDEALPRO",
  "Currency": "USD"
}
```

## Logging Configuration

**Section**: `Serilog`

The collector uses Serilog for structured logging. You can customize log levels and outputs.

### Basic Configuration

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "System": "Warning"
      }
    }
  }
}
```

### Log Levels

| Level | Use Case |
|-------|----------|
| `Verbose` | Extremely detailed tracing |
| `Debug` | Developer diagnostics |
| `Information` | General operational events (default) |
| `Warning` | Potential issues |
| `Error` | Failures that need attention |
| `Fatal` | Critical failures |

### Environment Variables

- `MDC_DEBUG=true` - Enable debug logging

## Command-Line Arguments

| Argument | Description |
|----------|-------------|
| `--selftest` | Run internal self-tests and exit |
| `--serve-status` | Enable HTTP status server |
| `--status-port <port>` | HTTP server port (default: 8080) |
| `--replay <path>` | Replay JSONL files from path |

## Complete Example

> **Note**: Credentials are provided via environment variables, not in the config file.
> Before running, set: `ALPACA_KEY_ID` and `ALPACA_SECRET_KEY`

```json
{
  "DataRoot": "data",
  "Compress": false,
  "DataSource": "Alpaca",

  "Alpaca": {
    "Feed": "sip",
    "UseSandbox": false,
    "SubscribeQuotes": true
  },

  "Storage": {
    "NamingConvention": "BySymbol",
    "DatePartition": "Daily",
    "RetentionDays": 30,
    "MaxTotalMegabytes": 10240
  },

  "Symbols": [
    {
      "Symbol": "SPY",
      "SubscribeTrades": true,
      "SubscribeDepth": false
    },
    {
      "Symbol": "AAPL",
      "SubscribeTrades": true,
      "SubscribeDepth": false
    }
  ],

  "Serilog": {
    "MinimumLevel": {
      "Default": "Information"
    }
  }
}
```

## Historical Backfill

Backfill options seed historical daily bars before live capture begins. They can be defined in `appsettings.json` and overridden via command-line arguments.

### Basic Settings

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `Enabled` | boolean | `false` | Run a backfill before the live session starts |
| `Provider` | string | `"stooq"` | Registered historical data provider name |
| `Symbols` | array | `[]` | Symbols to backfill (falls back to live symbol list when empty) |
| `From` | string (yyyy-MM-dd) | `null` | Inclusive start date |
| `To` | string (yyyy-MM-dd) | `null` | Inclusive end date |

### Advanced Settings

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `EnableFallback` | boolean | `true` | Auto-failover to alternate providers |
| `EnableSymbolResolution` | boolean | `false` | Use OpenFIGI for symbol normalization |
| `RateLimitRotation` | boolean | `true` | Switch providers when approaching limits |
| `SkipExistingData` | boolean | `true` | Skip dates with existing data (gap-fill mode) |
| `RetryCount` | integer | `3` | Number of retries per request |
| `BatchSize` | integer | `100` | Symbols per batch |

### Available Providers

| Provider | ID | Description | Free | Notes |
|----------|-----|-------------|------|-------|
| Alpaca | `alpaca` | Historical bars, trades, quotes, auctions | Yes (IEX) | Requires API keys |
| Yahoo Finance | `yahoo` | EOD OHLCV bars | Yes | 50K+ global securities |
| Stooq | `stooq` | EOD bars | Yes | US equities |
| Nasdaq Data Link | `nasdaq` | Alternative datasets | Yes (limited) | Requires API key |
| Composite | `composite` | Automatic failover | - | Uses all above with priority |

### Alpaca-Specific Settings

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `Feed` | string | `"iex"` | Data feed: `"iex"` (free), `"sip"` (paid), `"delayed_sip"` |
| `Adjustment` | string | `"raw"` | Price adjustment: `"raw"`, `"split"`, `"dividend"`, `"all"` |

### Example

```json
"Backfill": {
  "Enabled": true,
  "Provider": "composite",
  "EnableFallback": true,
  "RateLimitRotation": true,
  "SkipExistingData": true,
  "Symbols": ["SPY", "QQQ", "AAPL"],
  "From": "2024-01-01",
  "To": "2024-12-31",
  "Alpaca": {
    "Feed": "iex",
    "Adjustment": "split"
  }
}
```

### Command-Line Overrides

- `--backfill` forces a backfill run even if disabled in config
- `--backfill-provider <name>` chooses a provider
- `--backfill-symbols <CSV>` overrides symbols
- `--backfill-from <yyyy-MM-dd>` / `--backfill-to <yyyy-MM-dd>` override date bounds

## Validation

Configuration is validated on startup. Invalid configurations will:
1. Log detailed error messages
2. Exit with a non-zero status code

Common validation errors:
- Empty `DataRoot`
- Missing Alpaca credentials when `DataSource` is `Alpaca`
- Invalid `Feed` value (must be `iex` or `sip`)
- Empty `Symbol` in symbol configuration
- `DepthLevels` outside 1-50 range
- Invalid `SecurityType` or `Currency`

## Hot Reload

Pass `--watch-config` to enable hot reload. The collector automatically reloads `appsettings.json` when it changes and applies symbol or provider updates without a restart.

## Multiple Data Sources

The `DataSources` section allows you to configure multiple data sources for both real-time streaming and historical data collection. This is useful for:

- Combining real-time data from one provider with historical data from another
- Setting up failover between providers
- Running multiple provider connections simultaneously
- Organizing data collection by data type or use case

**Section**: `DataSources`

### DataSources Configuration

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `Sources` | array | `[]` | Array of data source configurations |
| `DefaultRealTimeSourceId` | string | `null` | ID of the default source for real-time data |
| `DefaultHistoricalSourceId` | string | `null` | ID of the default source for historical data |
| `EnableFailover` | boolean | `true` | Automatically switch to next source on failure |
| `FailoverTimeoutSeconds` | integer | `30` | Timeout before triggering failover |

### Data Source Entry

Each entry in the `Sources` array represents a configured data source:

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| `Id` | string | Yes | - | Unique identifier for the data source |
| `Name` | string | Yes | - | Display name |
| `Provider` | string | No | `"IB"` | Provider type: `"IB"`, `"Alpaca"`, `"Polygon"` |
| `Enabled` | boolean | No | `true` | Whether this source is active |
| `Type` | string | No | `"RealTime"` | Data type: `"RealTime"`, `"Historical"`, `"Both"` |
| `Priority` | integer | No | `100` | Priority for failover (lower = higher priority) |
| `Description` | string | No | `null` | Optional description |
| `Symbols` | array | No | `null` | Symbol list (uses global if not specified) |
| `Tags` | array | No | `null` | Tags for categorization |
| `Alpaca` | object | No | `null` | Alpaca-specific settings |
| `Polygon` | object | No | `null` | Polygon-specific settings |
| `IB` | object | No | `null` | Interactive Brokers-specific settings |

### IB Options (per data source)

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `Host` | string | `"127.0.0.1"` | TWS/Gateway host address |
| `Port` | integer | `7496` | Port (7496 live, 7497 paper) |
| `ClientId` | integer | `0` | Client ID for connection |
| `UsePaperTrading` | boolean | `false` | Use paper trading account |
| `SubscribeDepth` | boolean | `true` | Subscribe to L2 market depth |
| `DepthLevels` | integer | `10` | Number of depth levels |
| `TickByTick` | boolean | `true` | Request tick-by-tick data |

### Polygon Options (per data source)

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `ApiKey` | string | `null` | Polygon API key |
| `UseDelayed` | boolean | `false` | Use 15-minute delayed data |
| `Feed` | string | `"stocks"` | Feed type: `"stocks"`, `"options"`, `"forex"`, `"crypto"` |
| `SubscribeTrades` | boolean | `true` | Subscribe to trades |
| `SubscribeQuotes` | boolean | `false` | Subscribe to quotes |
| `SubscribeAggregates` | boolean | `false` | Subscribe to per-minute aggregates |

### Example: Multiple Data Sources

> **Note**: Credentials are provided via environment variables, not in the config file.
> Set `ALPACA_KEY_ID`, `ALPACA_SECRET_KEY`, and `POLYGON_API_KEY` before running.

```json
{
  "DataSources": {
    "EnableFailover": true,
    "FailoverTimeoutSeconds": 30,
    "DefaultRealTimeSourceId": "alpaca-primary",
    "DefaultHistoricalSourceId": "polygon-historical",
    "Sources": [
      {
        "Id": "alpaca-primary",
        "Name": "Alpaca Real-Time",
        "Provider": "Alpaca",
        "Type": "RealTime",
        "Priority": 1,
        "Enabled": true,
        "Description": "Primary real-time feed from Alpaca",
        "Alpaca": {
          "Feed": "sip",
          "UseSandbox": false,
          "SubscribeQuotes": true
        }
      },
      {
        "Id": "ib-backup",
        "Name": "IB Backup",
        "Provider": "IB",
        "Type": "RealTime",
        "Priority": 2,
        "Enabled": true,
        "Description": "Backup real-time feed via Interactive Brokers",
        "IB": {
          "Host": "127.0.0.1",
          "Port": 7496,
          "ClientId": 1,
          "SubscribeDepth": true,
          "DepthLevels": 10
        }
      },
      {
        "Id": "polygon-historical",
        "Name": "Polygon Historical",
        "Provider": "Polygon",
        "Type": "Historical",
        "Priority": 1,
        "Enabled": true,
        "Description": "Historical data from Polygon.io",
        "Polygon": {
          "Feed": "stocks",
          "UseDelayed": false
        }
      }
    ]
  }
}
```

### Using the Desktop App

The Windows desktop application provides a visual interface for managing data sources:

1. Navigate to **Data Sources** in the left navigation menu
2. Configure failover settings at the top of the page
3. View and manage existing data sources in the list
4. Click **Add Data Source** to create a new configuration
5. Fill in the required fields and provider-specific settings
6. Click **Save Data Source** to persist changes

### Using the Web Dashboard

The web dashboard also provides data source management:

1. Access the dashboard at the configured URL (default: http://localhost:8080)
2. Scroll to the **Data Sources** section
3. Use the form to add or edit data sources
4. Toggle individual sources on/off using the checkbox in the list
5. Configure failover settings using the controls at the top

### API Endpoints

The following API endpoints are available for programmatic configuration:

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/api/config/datasources` | Get all data sources and settings |
| `POST` | `/api/config/datasources` | Add or update a data source |
| `DELETE` | `/api/config/datasources/{id}` | Delete a data source |
| `POST` | `/api/config/datasources/{id}/toggle` | Enable/disable a data source |
| `POST` | `/api/config/datasources/defaults` | Set default source IDs |
| `POST` | `/api/config/datasources/failover` | Update failover settings |

---

**Version:** 1.5.0
**Last Updated:** 2026-01-08
**See Also:** [Getting Started](getting-started.md) | [Troubleshooting](troubleshooting.md) | [Operator Runbook](operator-runbook.md)
