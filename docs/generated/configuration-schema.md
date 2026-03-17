# Configuration Schema

> Generated from `config/appsettings.sample.json`
> Last updated: 2026-03-17

This document describes the configuration options available in the Market Data Collector.
Copy `config/appsettings.sample.json` to `config/appsettings.json` and adjust settings as needed.

> **Security:** Never store API keys or secrets in configuration files. Use environment variables for all credentials.

## Environment Variables

Set these before running the application:

| Variable | Provider | Required |
|----------|----------|----------|
| `ALPACA_KEY_ID` | Alpaca | Required for Alpaca streaming/backfill |
| `ALPACA_SECRET_KEY` | Alpaca | Required for Alpaca streaming/backfill |
| `POLYGON_API_KEY` | Polygon.io | Required for Polygon streaming/backfill |
| `TIINGO_API_TOKEN` | Tiingo | Required for Tiingo backfill |
| `FINNHUB_API_KEY` | Finnhub | Required for Finnhub backfill |
| `ALPHA_VANTAGE_API_KEY` | Alpha Vantage | Required for Alpha Vantage backfill |
| `NASDAQ_API_KEY` | Nasdaq Data Link | Optional (higher rate limits) |
| `OPENFIGI_API_KEY` | OpenFIGI | Optional (higher rate limits) |
| `NYSE_API_KEY` | NYSE Connect | Required for NYSE streaming |
| `NYSE_API_SECRET` | NYSE Connect | Required for NYSE streaming |
| `NYSE_CLIENT_ID` | NYSE Connect | Required for NYSE streaming |
| `MDC_STOCKSHARP_CONNECTOR` | StockSharp | Connector type (Rithmic, IQFeed, CQG, InteractiveBrokers, Custom) |
| `MDC_DEBUG` | — | Set to `true` to enable debug logging |

---

## Configuration Sections

### `DataRoot`

Root directory for all data output (relative or absolute path). Default: `"data"`.

---

### `Compress`

Enable gzip compression for JSONL files (`.jsonl.gz`). Default: `false`.

---

### `Backfill`

Controls historical data backfill behaviour.

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `Enabled` | bool | `false` | Enable backfill mode (pulls historical data instead of live streaming) |
| `Provider` | string | `"composite"` | Provider to use: `"composite"` (auto-failover), `"alpaca"`, `"yahoo"`, `"stooq"`, `"nasdaq"` |
| `Symbols` | string[] | `["SPY","QQQ","AAPL"]` | Symbols to backfill |
| `From` | string | — | Start date (inclusive), format: `YYYY-MM-DD` |
| `To` | string | — | End date (inclusive), format: `YYYY-MM-DD` |
| `Granularity` | string | `"daily"` | Data granularity: `"daily"`, `"hourly"`, `"minute1"`, `"minute5"`, `"minute15"`, `"minute30"` |
| `EnableFallback` | bool | `true` | Enable automatic failover to alternate providers on failure |
| `PreferAdjustedPrices` | bool | `true` | Use split/dividend adjusted prices when available |
| `EnableSymbolResolution` | bool | `true` | Enable OpenFIGI symbol resolution (normalises symbols across providers) |
| `ProviderPriority` | string[] \| null | `null` | Custom provider priority order (lower index = tried first) |
| `EnableRateLimitRotation` | bool | `true` | Automatically switch to alternate providers when approaching rate limits |
| `RateLimitRotationThreshold` | float | `0.8` | Threshold (0.0–1.0) at which to start rotating providers |
| `SkipExistingData` | bool | `true` | Check existing archives and skip dates that already have data |
| `FillGapsOnly` | bool | `true` | Only fill detected gaps rather than performing a full backfill |

#### `Backfill.Jobs`

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `PersistJobs` | bool | `true` | Persist job state to disk for resume after restart |
| `JobsDirectory` | string | `"_backfill_jobs"` | Directory for job state files (relative to `DataRoot`) |
| `MaxConcurrentRequests` | int | `3` | Maximum concurrent requests across all providers |
| `MaxConcurrentPerProvider` | int | `2` | Maximum concurrent requests per provider |
| `MaxRetries` | int | `3` | Maximum retries for failed requests |
| `RetryDelaySeconds` | int | `5` | Delay between retries in seconds |
| `BatchSizeDays` | int | `365` | Maximum days per request batch |
| `AutoPauseOnRateLimit` | bool | `true` | Automatically pause when all providers are rate-limited |
| `AutoResumeAfterRateLimit` | bool | `true` | Automatically resume after rate limit window expires |
| `MaxRateLimitWaitMinutes` | int | — | Maximum minutes to wait for rate limit before pausing |

#### `Backfill.Scheduling`

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `Enabled` | bool | `false` | Enable scheduled backfill service |
| `ScheduleCheckIntervalSeconds` | int | — | How often to check for due schedules (seconds) |
| `MaxExecutionDurationHours` | int | — | Maximum duration for a single execution (hours) |
| `CatchUpMissedSchedules` | bool | — | Catch up missed schedules on startup |
| `CatchUpWindowHours` | int | — | How far back to look for missed schedules (hours) |
| `MaxConcurrentExecutions` | int | — | Maximum concurrent scheduled executions |
| `PauseDuringMarketHours` | bool | — | Pause executions during market hours |

#### `Backfill.Providers`

Per-provider configuration nested under `Backfill.Providers`:

| Provider key | Relevant fields |
|--------------|-----------------|
| `Alpaca` | `Feed` (`"iex"`, `"sip"`, `"delayed_sip"`), `Adjustment` (`"raw"`, `"split"`, `"dividend"`, `"all"`) |
| `Yahoo` | No credentials required; free fallback provider |
| `Polygon` | Credentials via `POLYGON_API_KEY` |
| `Tiingo` | Credentials via `TIINGO_API_TOKEN` |
| `Finnhub` | Credentials via `FINNHUB_API_KEY` |
| `Stooq` | No credentials required |
| `AlphaVantage` | Credentials via `ALPHA_VANTAGE_API_KEY` |
| `Nasdaq` | Credentials via `NASDAQ_API_KEY` |
| `OpenFigi` | Credentials via `OPENFIGI_API_KEY` |

---

### `DataSource`

Legacy single-provider selection (use `DataSources` for multi-provider configuration).

---

### `DataSources`

Multi-provider streaming configuration.

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `Sources` | object[] | — | List of configured data sources |
| `DefaultRealTimeSourceId` | string | — | Default data source ID for real-time streaming |
| `DefaultHistoricalSourceId` | string | — | Default data source ID for historical data requests |
| `EnableFailover` | bool | — | Enable automatic failover between providers |
| `FailoverTimeoutSeconds` | int | — | Timeout before failing over to next provider |
| `HealthCheckIntervalSeconds` | int | — | Health check interval in seconds |
| `AutoRecover` | bool | — | Automatically recover to primary provider when healthy |
| `FailoverRules` | object[] | — | Failover rules defining primary/backup chains |
| `SymbolMappings.PersistencePath` | string | — | Path to persist symbol mappings |
| `SymbolMappings.Mappings` | object | — | Pre-configured mappings for common edge cases |

---

### `Alpaca`

Alpaca streaming provider configuration.

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `Feed` | string | — | SIP (real-time, paid) or IEX (free) |
| `UseSandbox` | bool | `false` | Use sandbox/paper-trading environment |
| `SubscribeQuotes` | bool | — | Subscribe to quote (BBO) data in addition to trades |

Credentials via `ALPACA_KEY_ID` / `ALPACA_SECRET_KEY` environment variables.

---

### `StockSharp`

StockSharp multi-connector configuration. Credentials via `MDC_STOCKSHARP_*` environment variables. See the header of `config/appsettings.sample.json` for the full variable list.

---

### `Storage`

Controls how collected data is persisted to disk.

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `NamingConvention` | string | `"BySymbol"` | File naming: `"Flat"`, `"BySymbol"` (recommended), `"ByDate"`, `"ByType"` |
| `DatePartition` | string | `"Daily"` | Partitioning: `"None"`, `"Daily"`, `"Hourly"`, `"Monthly"` |
| `IncludeProvider` | bool | `false` | Include provider name in file path |
| `FilePrefix` | string \| null | `null` | Optional prefix for all file names |
| `Profile` | string \| null | `null` | Storage profile preset: `"Research"`, `"LowLatency"`, `"Archival"` |
| `RetentionDays` | int \| null | `null` | Auto-delete files older than N days (`null` = keep forever) |
| `MaxTotalMegabytes` | int \| null | `null` | Maximum total storage in MB (`null` = unlimited) |
| `Sinks` | string[] \| null | `null` | Active storage sink plugin IDs. Built-in: `"jsonl"`, `"parquet"` |

**File path patterns by `NamingConvention`:**

| Convention | Pattern |
|------------|---------|
| `Flat` | `{root}/{symbol}_{type}_{date}.jsonl` |
| `BySymbol` | `{root}/{symbol}/{type}/{date}.jsonl` |
| `ByDate` | `{root}/{date}/{symbol}/{type}.jsonl` |
| `ByType` | `{root}/{type}/{symbol}/{date}.jsonl` |

---

### `Symbols`

Array of symbol subscriptions. Each entry configures a single instrument.

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `Symbol` | string | — | Ticker symbol (e.g. `"SPY"`, `"AAPL"`, `"PCG-PA"`) |
| `SubscribeTrades` | bool | `true` | Subscribe to tick-by-tick trade data |
| `SubscribeDepth` | bool | `false` | Subscribe to Level 2 order book depth |
| `DepthLevels` | int | `10` | Number of depth levels to capture |
| `SecurityType` | string | `"STK"` | IB contract type: `"STK"`, `"FUT"`, `"OPT"` |
| `Exchange` | string | `"SMART"` | Exchange routing (IB: `"SMART"`, `"NYSE"`, `"NASDAQ"`) |
| `Currency` | string | `"USD"` | Contract currency |
| `PrimaryExchange` | string | — | Primary listing exchange (IB) |
| `LocalSymbol` | string | — | Exchange-specific symbol (required for preferred shares, e.g. `"PCG PRA"`) |

---

### `Derivatives`

Options chain tracking for underlying instruments.

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `Enabled` | bool | `false` | Enable derivatives tracking |
| `Underlyings` | string[] | — | Underlying symbols to track options for |
| `MaxDaysToExpiration` | int | `90` | Only track contracts expiring within N days (0 = no limit) |
| `StrikeRange` | int | `20` | Number of strikes above and below ATM to track (0 = all) |
| `CaptureGreeks` | bool | `true` | Capture delta, gamma, theta, vega, rho, IV |
| `CaptureChainSnapshots` | bool | `false` | Capture periodic full chain snapshots |
| `ChainSnapshotIntervalSeconds` | int | `300` | Interval between chain snapshots |
| `CaptureOpenInterest` | bool | `true` | Capture daily open interest updates |
| `ExpirationFilter` | string[] | `["Weekly","Monthly"]` | Filter by expiration type |
| `IndexOptions.Enabled` | bool | `false` | Enable index option tracking |
| `IndexOptions.Indices` | string[] | — | Index symbols (e.g. `["SPX","NDX","VIX"]`) |

---

### `Canonicalization`

Controls deterministic canonicalization of market events (ADR-011).

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `Enabled` | bool | `false` | Master switch — enables the canonicalization pipeline |
| `Version` | int | `1` | Mapping version stamped on enriched events; bump when mapping tables change |
| `PilotSymbols` | string[] | `[]` | Canonicalize only these symbols; empty = all symbols |
| `EnableDualWrite` | bool | `false` | Persist both raw and enriched events for parity validation (doubles write volume) |
| `UnresolvedAlertThresholdPercent` | float | `0.1` | Alert threshold (%) for unresolved mapping rate per provider |
| `ConditionCodesPath` | string | — | Override path for condition-codes mapping JSON |
| `VenueMappingPath` | string | — | Override path for venue-mapping JSON |

**Rollout phases:**

- **Phase 2 (Dual-Write):** `Enabled=true`, set `PilotSymbols`, `EnableDualWrite=true` — validates parity between raw and canonical paths.
- **Phase 3 (Default):** `Enabled=true`, `EnableDualWrite=false`, clear `PilotSymbols` — writes only canonical events.

---

### `Settings`

Desktop application settings.

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `Theme` | string | `"System"` | UI theme: `"System"`, `"Light"`, `"Dark"` |
| `AccentColor` | string | `"System"` | Accent colour: `"System"` or hex (e.g. `"#0078D4"`) |
| `CompactMode` | bool | `false` | Use compact mode for UI elements |
| `NotificationsEnabled` | bool | `true` | Enable Windows toast notifications |
| `NotifyConnectionStatus` | bool | `true` | Notify on connection status changes |
| `NotifyErrors` | bool | `true` | Notify on errors |
| `NotifyBackfillComplete` | bool | `true` | Notify when backfill completes |
| `NotifyDataGaps` | bool | `true` | Notify when data gaps are detected |
| `NotifyStorageWarnings` | bool | `true` | Notify on storage warnings |
| `QuietHoursEnabled` | bool | `false` | Suppress notifications during quiet hours |
| `QuietHoursStart` | string | `"22:00"` | Start of quiet hours (HH:mm) |
| `QuietHoursEnd` | string | `"07:00"` | End of quiet hours (HH:mm) |
| `AutoReconnectEnabled` | bool | `true` | Automatically reconnect on connection loss |
| `MaxReconnectAttempts` | int | `10` | Maximum reconnection attempts |
| `StatusRefreshIntervalSeconds` | int | `2` | Status display refresh interval |
| `ServiceUrl` | string | `"http://localhost:8080"` | URL of the Market Data Collector service |
| `ServiceTimeoutSeconds` | int | `30` | Timeout in seconds for API requests |
| `BackfillTimeoutMinutes` | int | `60` | Timeout for long-running backfill operations |

---

### `Serilog`

Logging configuration via Serilog. Controls log levels and output sinks.

| Field | Description |
|-------|-------------|
| `MinimumLevel.Default` | Global minimum log level (e.g. `"Information"`) |
| `MinimumLevel.Override` | Per-namespace overrides (e.g. `"Microsoft": "Warning"`) |
| `WriteTo` | Log sinks — `Console` and `File` are configured by default |

The default file sink writes to `data/_logs/mdc-.log` with daily rolling and 30-day retention.

---

*For the full annotated reference, see `config/appsettings.sample.json`.*
