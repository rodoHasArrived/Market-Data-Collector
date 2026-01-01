# Configuration Guide

This document describes all configuration options available in MarketDataCollector.

## Configuration Sources

Configuration is loaded from multiple sources in order of precedence (highest first):

1. **Command-line arguments** (`--key value`)
2. **Environment variables** (`MDC_KEY` or provider-specific like `ALPACA_KEY_ID`)
3. **appsettings.json** file
4. **Default values** in code

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

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| `KeyId` | string | Yes | - | Alpaca API Key ID |
| `SecretKey` | string | Yes | - | Alpaca Secret Key |
| `Feed` | string | No | `"iex"` | Data feed: `"iex"` (free) or `"sip"` (paid) |
| `UseSandbox` | boolean | No | `false` | Use paper trading environment |
| `SubscribeQuotes` | boolean | No | `false` | Subscribe to quote (BBO) data |

```json
{
  "Alpaca": {
    "KeyId": "AKXXXXXXXXXXXXXXXXXX",
    "SecretKey": "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx",
    "Feed": "sip",
    "UseSandbox": false,
    "SubscribeQuotes": true
  }
}
```

**Environment Variables**:
- `ALPACA_KEY_ID` - Overrides `Alpaca.KeyId`
- `ALPACA_SECRET_KEY` - Overrides `Alpaca.SecretKey`

**Security Best Practice**: Use environment variables for credentials rather than storing them in config files.

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

```json
{
  "DataRoot": "data",
  "Compress": false,
  "DataSource": "Alpaca",

  "Alpaca": {
    "KeyId": "AKXXXXXXXXXXXXXXXXXX",
    "SecretKey": "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx",
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

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `Enabled` | boolean | `false` | Run a backfill before the live session starts |
| `Provider` | string | `"stooq"` | Registered historical data provider name |
| `Symbols` | array | `[]` | Symbols to backfill (falls back to live symbol list when empty) |
| `From` | string (yyyy-MM-dd) | `null` | Inclusive start date |
| `To` | string (yyyy-MM-dd) | `null` | Inclusive end date |

### Example

```json
"Backfill": {
  "Enabled": true,
  "Provider": "stooq",
  "Symbols": ["SPY", "QQQ"],
  "From": "2024-01-01",
  "To": "2024-01-05"
}
```

Command-line overrides available at runtime:

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
