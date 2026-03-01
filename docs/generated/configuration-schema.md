# Configuration Schema

> Auto-generated from `config/appsettings.sample.json` on 2026-03-01

This document describes all configuration options available in the Market Data Collector.
Copy `config/appsettings.sample.json` to `config/appsettings.json` and adjust values as needed.

**Security:** Never store API keys or secrets in configuration files. Use environment variables for all credentials.

---

## Top-Level Settings

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `DataRoot` | string | `"data"` | Root directory for all data output (relative or absolute path) |
| `Compress` | bool | `false` | Enable gzip compression for JSONL files (`.jsonl.gz`) |
| `DataSource` | string | `"IB"` | Active streaming provider for single-provider mode: `"IB"`, `"Alpaca"`, `"Polygon"`, `"NYSE"`, `"StockSharp"` |

---

## Backfill

Historical data backfill with automatic multi-provider failover.

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `Backfill:Enabled` | bool | `false` | Enable backfill mode |
| `Backfill:Provider` | string | `"composite"` | Provider to use: `"composite"` (auto-failover), `"alpaca"`, `"yahoo"`, `"stooq"`, `"nasdaq"` |
| `Backfill:Symbols` | string[] | `["SPY","QQQ","AAPL"]` | Symbols to backfill |
| `Backfill:From` | string | `"2024-01-01"` | Start date (inclusive, ISO 8601) |
| `Backfill:To` | string | `"2024-12-31"` | End date (inclusive, ISO 8601) |
| `Backfill:Granularity` | string | `"daily"` | Data granularity: `"daily"`, `"hourly"`, `"minute1"`, `"minute5"`, `"minute15"`, `"minute30"` |
| `Backfill:EnableFallback` | bool | `true` | Enable automatic failover to alternate providers on failure |
| `Backfill:PreferAdjustedPrices` | bool | `true` | Use split/dividend adjusted prices when available |
| `Backfill:EnableSymbolResolution` | bool | `true` | Enable OpenFIGI symbol resolution across providers |
| `Backfill:ProviderPriority` | string[]? | `null` | Custom provider priority order (lower index = tried first). Default: `["alpaca","yahoo","stooq","nasdaq"]` |
| `Backfill:EnableRateLimitRotation` | bool | `true` | Automatically switch providers when approaching rate limits |
| `Backfill:RateLimitRotationThreshold` | double | `0.8` | Threshold (0.0–1.0) at which to start rotating providers |
| `Backfill:SkipExistingData` | bool | `true` | Check existing archives and skip dates with data |
| `Backfill:FillGapsOnly` | bool | `true` | Only fill detected gaps vs. full backfill |

### Backfill:Jobs

Job management and scheduling for backfill operations.

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `Jobs:PersistJobs` | bool | `true` | Persist job state to disk for resume after restart |
| `Jobs:JobsDirectory` | string | `"_backfill_jobs"` | Directory for job state files (relative to DataRoot) |
| `Jobs:MaxConcurrentRequests` | int | `3` | Maximum concurrent requests across all providers |
| `Jobs:MaxConcurrentPerProvider` | int | `2` | Maximum concurrent requests per provider |
| `Jobs:MaxRetries` | int | `3` | Maximum retries for failed requests |
| `Jobs:RetryDelaySeconds` | int | `5` | Delay between retries in seconds |
| `Jobs:BatchSizeDays` | int | `365` | Maximum days per request batch |
| `Jobs:AutoPauseOnRateLimit` | bool | `true` | Pause when all providers are rate-limited |
| `Jobs:AutoResumeAfterRateLimit` | bool | `true` | Resume after rate limit window expires |
| `Jobs:MaxRateLimitWaitMinutes` | int | `5` | Maximum wait for rate limit before pausing |

### Backfill:Jobs:Scheduling

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `Scheduling:Enabled` | bool | `false` | Enable scheduled backfill service |
| `Scheduling:ScheduleCheckIntervalSeconds` | int | `60` | How often to check for due schedules |
| `Scheduling:MaxExecutionDurationHours` | int | `6` | Maximum duration for a single execution |
| `Scheduling:CatchUpMissedSchedules` | bool | `true` | Catch up missed schedules on startup |
| `Scheduling:CatchUpWindowHours` | int | `24` | How far back to look for missed schedules |
| `Scheduling:MaxConcurrentExecutions` | int | `1` | Maximum concurrent scheduled executions |
| `Scheduling:PauseDuringMarketHours` | bool | `false` | Pause executions during market hours |
| `Scheduling:DefaultSchedules` | array | `[]` | Default schedules. Presets: `"daily"`, `"weekly"`, `"eod"`, `"monthly"` |

### Backfill:Providers

Per-provider configuration for historical data sources.

| Provider | Key | Type | Default | Description |
|----------|-----|------|---------|-------------|
| **Alpaca** | `Enabled` | bool | `true` | Enable Alpaca historical data |
| | `Feed` | string | `"iex"` | Feed type: `"iex"` (free), `"sip"` (paid), `"delayed_sip"` (free, 15-min delay) |
| | `Adjustment` | string | `"all"` | Price adjustment: `"raw"`, `"split"`, `"dividend"`, `"all"` |
| | `Priority` | int | `5` | Provider priority (lower = tried first) |
| | `RateLimitPerMinute` | int | `200` | API rate limit per minute |
| **Yahoo** | `Enabled` | bool | `true` | Enable Yahoo Finance (no API key required) |
| | `Priority` | int | `22` | Provider priority |
| | `RateLimitPerHour` | int | `2000` | Rate limit per hour |
| **Polygon** | `Enabled` | bool | `true` | Enable Polygon.io (free tier: 5 calls/min) |
| | `Priority` | int | `12` | Provider priority |
| | `RateLimitPerMinute` | int | `5` | Rate limit per minute |
| **Tiingo** | `Enabled` | bool | `true` | Enable Tiingo (free tier: 50 req/hour) |
| | `Priority` | int | `15` | Provider priority |
| | `RateLimitPerHour` | int | `50` | Rate limit per hour |
| **Finnhub** | `Enabled` | bool | `true` | Enable Finnhub (free tier: 60 calls/min) |
| | `Priority` | int | `18` | Provider priority |
| | `RateLimitPerMinute` | int | `60` | Rate limit per minute |
| **Stooq** | `Enabled` | bool | `true` | Enable Stooq (no API key required) |
| | `Priority` | int | `20` | Provider priority |
| | `DefaultMarket` | string | `"us"` | Default market |
| **AlphaVantage** | `Enabled` | bool | `false` | Enable Alpha Vantage (very limited free tier: 25/day) |
| | `Priority` | int | `25` | Provider priority |
| | `RateLimitPerMinute` | int | `5` | Rate limit per minute |
| | `RateLimitPerDay` | int | `25` | Rate limit per day |
| **Nasdaq** | `Enabled` | bool | `true` | Enable Nasdaq Data Link (formerly Quandl) |
| | `Database` | string | `"WIKI"` | Default dataset |
| | `Priority` | int | `30` | Provider priority |
| **OpenFigi** | `Enabled` | bool | `true` | Enable OpenFIGI symbol resolution |
| | `CacheResults` | bool | `true` | Cache resolution results |

---

## DataSources (Multi-Provider)

Configure multiple simultaneous data providers with automatic failover. Replaces `DataSource` when enabled.

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `DataSources:DefaultRealTimeSourceId` | string | `"alpaca-primary"` | Default source for real-time streaming |
| `DataSources:DefaultHistoricalSourceId` | string | `"alpaca-primary"` | Default source for historical data |
| `DataSources:EnableFailover` | bool | `true` | Enable automatic failover between providers |
| `DataSources:FailoverTimeoutSeconds` | int | `30` | Timeout before failover to next provider |
| `DataSources:HealthCheckIntervalSeconds` | int | `10` | Health check interval |
| `DataSources:AutoRecover` | bool | `true` | Auto-recover to primary when healthy |

### DataSources:Sources[] (array)

Each source entry has:

| Key | Type | Description |
|-----|------|-------------|
| `Id` | string | Unique identifier (e.g., `"alpaca-primary"`) |
| `Name` | string | Display name |
| `Provider` | string | Provider type: `"Alpaca"`, `"IB"`, `"Polygon"`, `"NYSE"`, `"StockSharp"` |
| `Enabled` | bool | Whether this source is active |
| `Type` | string | Source type: `"RealTime"`, `"Historical"` |
| `Priority` | int | Priority for failover ordering (lower = preferred) |
| `Description` | string | Human-readable description |
| `Tags` | string[] | Optional tags for filtering |

### DataSources:FailoverRules[] (array)

| Key | Type | Description |
|-----|------|-------------|
| `Id` | string | Rule identifier |
| `PrimaryProviderId` | string | Primary source ID |
| `BackupProviderIds` | string[] | Backup source IDs in priority order |
| `FailoverThreshold` | int | Failures before switching |
| `RecoveryThreshold` | int | Successes before switching back |
| `DataQualityThreshold` | int | Minimum quality score (0–100) |
| `MaxLatencyMs` | int | Maximum acceptable latency in milliseconds |

### DataSources:SymbolMappings

| Key | Type | Description |
|-----|------|-------------|
| `PersistencePath` | string | Path to persist symbol mappings JSON |
| `Mappings[]` | array | Pre-configured canonical-to-provider symbol mappings |

Each mapping: `CanonicalSymbol`, `IbSymbol`, `AlpacaSymbol`, `PolygonSymbol`, `YahooSymbol`, `Name`.

---

## Alpaca

Settings for Alpaca real-time streaming.

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `Alpaca:Feed` | string | `"iex"` | Data feed: `"iex"` (free, delayed) or `"sip"` (paid, real-time) |
| `Alpaca:UseSandbox` | bool | `false` | Use sandbox/paper trading environment |
| `Alpaca:SubscribeQuotes` | bool | `false` | Subscribe to BBO quote updates in addition to trades |

---

## StockSharp

Settings for StockSharp multi-exchange streaming.

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `StockSharp:Enabled` | bool | `true` | Enable StockSharp provider |
| `StockSharp:ConnectorType` | string | `"Rithmic"` | Connector type: `"Rithmic"`, `"IQFeed"`, `"CQG"`, `"InteractiveBrokers"`, `"Custom"` |
| `StockSharp:AdapterType` | string | `""` | Custom adapter type |
| `StockSharp:AdapterAssembly` | string | `""` | Custom adapter assembly |
| `StockSharp:EnableRealTime` | bool | `true` | Enable real-time streaming |
| `StockSharp:EnableHistorical` | bool | `true` | Enable historical data |
| `StockSharp:UseBinaryStorage` | bool | `false` | Use StockSharp binary storage format |
| `StockSharp:StoragePath` | string | `"data/stocksharp/{connector}"` | Storage path for connector-specific data |

Sub-sections: `Rithmic`, `IQFeed`, `CQG`, `InteractiveBrokers` — each with connector-specific host/port/credentials.

---

## Storage

File organization, retention, and compression settings.

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `Storage:NamingConvention` | string | `"BySymbol"` | File naming: `"Flat"`, `"BySymbol"` (recommended), `"ByDate"`, `"ByType"` |
| `Storage:DatePartition` | string | `"Daily"` | Partitioning: `"None"`, `"Daily"` (default), `"Hourly"`, `"Monthly"` |
| `Storage:IncludeProvider` | bool | `false` | Include provider name in file path |
| `Storage:FilePrefix` | string? | `null` | Optional prefix for all file names |
| `Storage:Profile` | string? | `null` | Optional preset: `"Research"`, `"LowLatency"`, `"Archival"` |
| `Storage:RetentionDays` | int? | `null` | Auto-delete files older than N days (`null` = keep forever) |
| `Storage:MaxTotalMegabytes` | int? | `null` | Maximum total storage in MB (`null` = unlimited) |

### File Path Examples

| Convention | Pattern | Example |
|-----------|---------|---------|
| `BySymbol` | `{root}/{symbol}/{type}/{date}.jsonl` | `data/SPY/trades/2024-06-15.jsonl` |
| `ByDate` | `{root}/{date}/{symbol}/{type}.jsonl` | `data/2024-06-15/SPY/trades.jsonl` |
| `ByType` | `{root}/{type}/{symbol}/{date}.jsonl` | `data/trades/SPY/2024-06-15.jsonl` |
| `Flat` | `{root}/{symbol}_{type}_{date}.jsonl` | `data/SPY_trades_2024-06-15.jsonl` |

---

## Symbols[] (array)

Symbol subscription configuration. Each entry defines a symbol and its data subscriptions.

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `Symbol` | string | — | Ticker symbol (e.g., `"SPY"`, `"AAPL"`, `"PCG-PA"`) |
| `SubscribeTrades` | bool | `true` | Subscribe to tick-by-tick trades |
| `SubscribeDepth` | bool | `false` | Subscribe to L2 depth/order book data |
| `DepthLevels` | int | `10` | Number of depth levels to capture |
| `SecurityType` | string | `"STK"` | IB security type: `"STK"`, `"FUT"`, `"OPT"`, `"IND"` |
| `Exchange` | string | `"SMART"` | IB exchange (e.g., `"SMART"`, `"CME"`, `"CBOE"`) |
| `Currency` | string | `"USD"` | Currency |
| `PrimaryExchange` | string | — | Primary exchange (e.g., `"NASDAQ"`, `"NYSE"`, `"ARCA"`) |
| `LocalSymbol` | string? | — | IB local symbol for special cases (e.g., `"PCG PRA"` for preferred shares) |

### Options Contract Fields (SecurityType = "OPT")

| Key | Type | Description |
|-----|------|-------------|
| `InstrumentType` | string | `"EquityOption"` or `"IndexOption"` |
| `Strike` | decimal | Strike price |
| `Right` | string | `"Call"` or `"Put"` |
| `LastTradeDateOrContractMonth` | string | Expiry date (`"YYYYMMDD"`) or contract month |
| `OptionStyle` | string | `"American"` or `"European"` |
| `Multiplier` | int | Contract multiplier (typically `100`) |
| `UnderlyingSymbol` | string | Underlying ticker |

---

## Derivatives

Options chain discovery and tracking.

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `Derivatives:Enabled` | bool | `false` | Enable derivatives tracking |
| `Derivatives:Underlyings` | string[] | `["SPY","QQQ","AAPL"]` | Underlying symbols to track |
| `Derivatives:MaxDaysToExpiration` | int | `90` | Max DTE filter (0 = no limit) |
| `Derivatives:StrikeRange` | int | `20` | Strikes above/below ATM to track (0 = all) |
| `Derivatives:CaptureGreeks` | bool | `true` | Capture delta, gamma, theta, vega, rho, IV |
| `Derivatives:CaptureChainSnapshots` | bool | `false` | Enable periodic chain snapshots |
| `Derivatives:ChainSnapshotIntervalSeconds` | int | `300` | Interval between snapshots |
| `Derivatives:CaptureOpenInterest` | bool | `true` | Capture daily open interest |
| `Derivatives:ExpirationFilter` | string[] | `["Weekly","Monthly"]` | Filter: `"Weekly"`, `"Monthly"`, `"Quarterly"`, `"LEAPS"`, `"All"` |

### Derivatives:IndexOptions

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `IndexOptions:Enabled` | bool | `false` | Enable index options tracking |
| `IndexOptions:Indices` | string[] | `["SPX","NDX","RUT","VIX"]` | Indices to track |
| `IndexOptions:IncludeWeeklies` | bool | `true` | Include weekly expirations |
| `IndexOptions:IncludeAmSettled` | bool | `true` | Include AM-settled contracts |
| `IndexOptions:IncludePmSettled` | bool | `true` | Include PM-settled contracts |

---

## Canonicalization

Deterministic symbol, venue, and condition-code normalization for market events.

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `Canonicalization:Enabled` | bool | `false` | Master switch for canonicalization pipeline |
| `Canonicalization:Version` | int | `1` | Mapping version stamped on enriched events |
| `Canonicalization:PilotSymbols` | string[] | `[]` | Canonicalize only these symbols (empty = all) |
| `Canonicalization:EnableDualWrite` | bool | `false` | Persist both raw and enriched events for parity validation |
| `Canonicalization:UnresolvedAlertThresholdPercent` | double | `0.1` | Alert threshold for unresolved mapping rate per provider |
| `Canonicalization:ConditionCodesPath` | string? | — | Override path for condition code mapping JSON |
| `Canonicalization:VenueMappingPath` | string? | — | Override path for venue mapping JSON |

---

## Settings (Desktop App)

Configuration for the WPF desktop application.

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `Settings:Theme` | string | `"System"` | Theme: `"System"`, `"Light"`, `"Dark"` |
| `Settings:AccentColor` | string | `"System"` | Accent color: `"System"` or hex (e.g., `"#0078D4"`) |
| `Settings:CompactMode` | bool | `false` | Use compact UI mode |
| `Settings:NotificationsEnabled` | bool | `true` | Enable Windows toast notifications |
| `Settings:NotifyConnectionStatus` | bool | `true` | Notify on connection status changes |
| `Settings:NotifyErrors` | bool | `true` | Notify on errors |
| `Settings:NotifyBackfillComplete` | bool | `true` | Notify when backfill completes |
| `Settings:NotifyDataGaps` | bool | `true` | Notify on detected data gaps |
| `Settings:NotifyStorageWarnings` | bool | `true` | Notify on storage warnings |
| `Settings:QuietHoursEnabled` | bool | `false` | Enable notification quiet hours |
| `Settings:QuietHoursStart` | string | `"22:00"` | Quiet hours start time |
| `Settings:QuietHoursEnd` | string | `"07:00"` | Quiet hours end time |
| `Settings:AutoReconnectEnabled` | bool | `true` | Enable auto-reconnection |
| `Settings:MaxReconnectAttempts` | int | `10` | Maximum reconnect attempts |
| `Settings:StatusRefreshIntervalSeconds` | int | `2` | Status polling interval |
| `Settings:ServiceUrl` | string | `"http://localhost:8080"` | URL of the Market Data Collector service |
| `Settings:ServiceTimeoutSeconds` | int | `30` | API request timeout |
| `Settings:BackfillTimeoutMinutes` | int | `60` | Timeout for long-running backfill operations |

---

## Serilog

Logging configuration using Serilog structured logging.

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `Serilog:MinimumLevel:Default` | string | `"Information"` | Default log level |
| `Serilog:MinimumLevel:Override:Microsoft` | string | `"Warning"` | Microsoft namespace log level |
| `Serilog:MinimumLevel:Override:System` | string | `"Warning"` | System namespace log level |

### Console Sink

```json
{
  "Name": "Console",
  "Args": {
    "outputTemplate": "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"
  }
}
```

### File Sink

```json
{
  "Name": "File",
  "Args": {
    "path": "data/_logs/mdc-.log",
    "rollingInterval": "Day",
    "retainedFileCountLimit": 30
  }
}
```

---

## Environment Variables

All credentials must be set via environment variables. Use double-underscore (`__`) for nested configuration.

| Variable | Provider | Required |
|----------|----------|----------|
| `ALPACA_KEY_ID` / `ALPACA__KEYID` | Alpaca | When using Alpaca |
| `ALPACA_SECRET_KEY` / `ALPACA__SECRETKEY` | Alpaca | When using Alpaca |
| `POLYGON_API_KEY` / `POLYGON__APIKEY` | Polygon.io | When using Polygon |
| `TIINGO_API_TOKEN` / `TIINGO__TOKEN` | Tiingo | When using Tiingo |
| `FINNHUB_API_KEY` / `FINNHUB__TOKEN` | Finnhub | When using Finnhub |
| `ALPHA_VANTAGE_API_KEY` / `ALPHAVANTAGE__APIKEY` | Alpha Vantage | When using Alpha Vantage |
| `NASDAQ_API_KEY` | Nasdaq Data Link | Optional (higher rate limits) |
| `OPENFIGI_API_KEY` | OpenFIGI | Optional (higher rate limits) |
| `NYSE_API_KEY` | NYSE Connect | When using NYSE |
| `NYSE_API_SECRET` | NYSE Connect | When using NYSE |
| `NYSE_CLIENT_ID` | NYSE Connect | When using NYSE |
| `MDC_STOCKSHARP_CONNECTOR` | StockSharp | When using StockSharp |
| `MDC_DEBUG` | System | Optional (enable debug logging) |

---

*Generated from `config/appsettings.sample.json` — 2026-03-01*
