# Configuration Schema

> Auto-generated from `config/appsettings.sample.json`

This document describes the configuration options available in the Market Data Collector.

**Version:** 1.6.2
**Last Updated:** 2026-03-17
**Audience:** Operators and contributors configuring a Market Data Collector deployment.

## Configuration Sections

---

## Root Keys

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `DataRoot` | `string` | `"data"` | Root directory for all data output (relative or absolute path). |
| `Compress` | `bool` | `false` | Enable gzip compression for JSONL files (`.jsonl.gz`). |
| `DataSource` | `string` | `"IB"` | Active real-time provider: `"IB"`, `"Alpaca"`, `"Polygon"`, `"NYSE"`, `"StockSharp"`. For multi-provider mode use `DataSources`. |

---

## Backfill

Historical backfill settings. Pull free end-of-day bars from multiple providers with automatic failover.

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `Backfill.Enabled` | `bool` | `false` | Enable backfill mode (pulls historical data instead of live streaming). |
| `Backfill.Provider` | `string` | `"composite"` | Provider: `"composite"` (auto-failover), `"alpaca"`, `"yahoo"`, `"stooq"`, `"nasdaq"`. |
| `Backfill.Symbols` | `string[]` | `["SPY","QQQ","AAPL"]` | Symbols to backfill. |
| `Backfill.From` | `string` | `"2024-01-01"` | Start date for backfill range (inclusive, `YYYY-MM-DD`). |
| `Backfill.To` | `string` | `"2024-12-31"` | End date for backfill range (inclusive, `YYYY-MM-DD`). |
| `Backfill.Granularity` | `string` | `"daily"` | Data granularity: `"daily"`, `"hourly"`, `"minute1"`, `"minute5"`, `"minute15"`, `"minute30"`. |
| `Backfill.EnableFallback` | `bool` | `true` | Enable automatic failover to alternate providers on failure. |
| `Backfill.PreferAdjustedPrices` | `bool` | `true` | Use split/dividend adjusted prices when available. |
| `Backfill.EnableSymbolResolution` | `bool` | `true` | Enable OpenFIGI symbol resolution to normalise symbols across providers. |
| `Backfill.ProviderPriority` | `string[]` \| `null` | `null` | Custom provider priority order (lower index = tried first). `null` uses built-in defaults. |
| `Backfill.EnableRateLimitRotation` | `bool` | `true` | Automatically switch providers when approaching rate limits. |
| `Backfill.RateLimitRotationThreshold` | `float` | `0.8` | Fraction of rate limit (0–1) at which provider rotation begins. |
| `Backfill.SkipExistingData` | `bool` | `true` | Check archives and skip dates that already have data. |
| `Backfill.FillGapsOnly` | `bool` | `true` | Only fill detected gaps in existing data (vs. full re-backfill). |
| `Backfill.Jobs` | `object` | — | Job management sub-configuration (see below). |

### Backfill.Jobs

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `Backfill.Jobs.PersistJobs` | `bool` | `true` | Persist job state to disk for resume after restart. |
| `Backfill.Jobs.JobsDirectory` | `string` | `"_backfill_jobs"` | Directory for job state files (relative to `DataRoot`). |
| `Backfill.Jobs.MaxConcurrentRequests` | `int` | `3` | Maximum concurrent requests across all providers. |
| `Backfill.Jobs.MaxConcurrentPerProvider` | `int` | `2` | Maximum concurrent requests per individual provider. |
| `Backfill.Jobs.MaxRetries` | `int` | `3` | Maximum retries for failed requests. |
| `Backfill.Jobs.RetryDelaySeconds` | `int` | `5` | Delay between retries in seconds. |
| `Backfill.Jobs.BatchSizeDays` | `int` | `365` | Maximum days per request batch. |

---

## DataSources

Multi-provider real-time source configuration (replaces single `DataSource` key).

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `DataSources.Sources` | `object[]` | — | Array of provider source definitions. Each entry has `Id`, `Name`, `Provider`, `Enabled`, `Priority`. |
| `DataSources.DefaultRealTimeSourceId` | `string` | — | ID of the default real-time source from `DataSources.Sources`. |
| `DataSources.DefaultHistoricalSourceId` | `string` | — | ID of the default historical source from `DataSources.Sources`. |
| `DataSources.EnableFailover` | `bool` | `false` | Enable automatic failover between sources when the primary becomes unhealthy. |
| `DataSources.FailoverTimeoutSeconds` | `int` | `30` | Seconds to wait before triggering failover after a provider health degradation. |
| `DataSources.HealthCheckIntervalSeconds` | `int` | `30` | Interval in seconds for provider health checks. |
| `DataSources.AutoRecover` | `bool` | `true` | Automatically switch back to the primary source once it recovers. |
| `DataSources.FailoverRules` | `object[]` | `[]` | Custom failover rules that override the default policy. |
| `DataSources.SymbolMappings` | `object[]` | `[]` | Per-symbol provider overrides (e.g., `BRK.B` → `BRK B` on Alpaca). |

---

## Alpaca

Settings for the Alpaca Markets streaming and historical provider.

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `Alpaca.Feed` | `string` | `"iex"` | Data feed: `"iex"` (15-min delayed, free) or `"sip"` (real-time consolidated, paid). |
| `Alpaca.UseSandbox` | `bool` | `false` | Use Alpaca paper-trading sandbox environment. |
| `Alpaca.SubscribeQuotes` | `bool` | `false` | Subscribe to BBO quote updates in addition to trades. |

---

## StockSharp

Settings for the StockSharp multi-connector provider.

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `StockSharp.Enabled` | `bool` | `true` | Enable the StockSharp connector. |
| `StockSharp.ConnectorType` | `string` | `"Rithmic"` | Connector type: `"Rithmic"`, `"IQFeed"`, `"CQG"`, `"InteractiveBrokers"`, `"Custom"`. |
| `StockSharp.AdapterType` | `string` | `""` | Fully-qualified adapter class name for `"Custom"` connectors. |
| `StockSharp.AdapterAssembly` | `string` | `""` | Assembly path for custom adapters. |
| `StockSharp.EnableRealTime` | `bool` | `true` | Enable real-time streaming via StockSharp. |
| `StockSharp.EnableHistorical` | `bool` | `true` | Enable historical data retrieval via StockSharp. |
| `StockSharp.UseBinaryStorage` | `bool` | `false` | Use StockSharp binary storage format instead of JSONL. |
| `StockSharp.StoragePath` | `string` | `"data/stocksharp/{connector}"` | Path template for StockSharp binary storage. |
| `StockSharp.Rithmic` | `object` | — | Rithmic-specific settings (`Server`, `UserName`, `Password`, `CertFile`, `UsePaperTrading`). |
| `StockSharp.IQFeed` | `object` | — | IQFeed-specific settings (`Host`, `Level1Port`, `Level2Port`, `LookupPort`, `ProductId`). |
| `StockSharp.CQG` | `object` | — | CQG-specific settings (`UserName`, `Password`, `UseDemoServer`). |

---

## Storage

File storage and data persistence configuration.

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `Storage.NamingConvention` | `string` | `"BySymbol"` | File naming layout: `"BySymbol"` (recommended), `"ByDate"`, `"ByType"`, `"Flat"`. |
| `Storage.DatePartition` | `string` | `"Daily"` | Date partitioning: `"None"`, `"Daily"`, `"Hourly"`, `"Monthly"`. |
| `Storage.IncludeProvider` | `bool` | `false` | Include provider name in the file path. |
| `Storage.FilePrefix` | `string` \| `null` | `null` | Optional prefix prepended to all file names. |
| `Storage.Profile` | `string` \| `null` | `null` | Storage profile preset: `"Research"`, `"LowLatency"`, `"Archival"`. |
| `Storage.RetentionDays` | `int` \| `null` | `null` | Auto-delete files older than this many days (`null` = keep forever). |
| `Storage.MaxTotalMegabytes` | `int` \| `null` | `null` | Maximum total storage in MB; oldest files removed first when exceeded (`null` = unlimited). |
| `Storage.Sinks` | `string[]` \| `null` | `null` | Active storage sink plugin IDs: `"jsonl"`, `"parquet"`, or custom IDs. |

---

## Derivatives

Options chain tracking configuration for equity/index underlyings.

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `Derivatives.Enabled` | `bool` | `false` | Enable derivatives (options chain) tracking. |
| `Derivatives.Underlyings` | `string[]` | `["SPY","QQQ","AAPL"]` | Underlying symbols to track options for. |
| `Derivatives.MaxDaysToExpiration` | `int` | `90` | Only track contracts expiring within this many days (`0` = no limit). |
| `Derivatives.StrikeRange` | `int` | `20` | Number of strikes above and below ATM to track (`0` = all). |
| `Derivatives.CaptureGreeks` | `bool` | `true` | Capture Greeks (delta, gamma, theta, vega, rho, IV). |
| `Derivatives.CaptureChainSnapshots` | `bool` | `false` | Capture periodic full-chain snapshots. |
| `Derivatives.ChainSnapshotIntervalSeconds` | `int` | `300` | Interval between chain snapshots in seconds. |
| `Derivatives.CaptureOpenInterest` | `bool` | `true` | Capture daily open interest updates. |
| `Derivatives.ExpirationFilter` | `string[]` | `["Weekly","Monthly"]` | Expiration types to track: `"Weekly"`, `"Monthly"`, `"Quarterly"`, `"LEAPS"`, `"All"`. |
| `Derivatives.IndexOptions` | `object` | — | Index options sub-config (`Enabled`, `Indices`, `IncludeWeeklies`, `IncludeAmSettled`, `IncludePmSettled`). |

---

## Canonicalization

Deterministic canonicalization of market events (symbol normalisation, ISO MIC venue codes, condition codes).

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `Canonicalization.Enabled` | `bool` | `false` | Enable the canonicalization pipeline. |
| `Canonicalization.Version` | `int` | `1` | Mapping version stamped on enriched events. Increment when mapping tables change. |
| `Canonicalization.PilotSymbols` | `string[]` | `[]` | Canonicalize only these symbols. Empty/`null` = all symbols. Used for incremental rollout. |
| `Canonicalization.EnableDualWrite` | `bool` | `false` | Persist both raw and canonicalized events for parity validation (doubles write volume). |
| `Canonicalization.UnresolvedAlertThresholdPercent` | `float` | `0.1` | Alert threshold (%) for unresolved mapping rate per provider. |
| `Canonicalization.ConditionCodesPath` | `string` | — | Path to custom condition-codes JSON override (optional). |
| `Canonicalization.VenueMappingPath` | `string` | — | Path to custom venue-mapping JSON override (optional). |

---

## Settings (Desktop App)

Windows desktop application-specific settings.

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `Settings.Theme` | `string` | `"System"` | UI theme: `"System"`, `"Light"`, or `"Dark"`. |
| `Settings.AccentColor` | `string` | `"System"` | Accent colour: `"System"` or a hex value (e.g., `"#0078D4"`). |
| `Settings.CompactMode` | `bool` | `false` | Enable compact mode for UI elements. |
| `Settings.NotificationsEnabled` | `bool` | `true` | Enable Windows toast notifications. |
| `Settings.NotifyConnectionStatus` | `bool` | `true` | Notify when the provider connection status changes. |
| `Settings.NotifyErrors` | `bool` | `true` | Notify on errors. |
| `Settings.NotifyBackfillComplete` | `bool` | `true` | Notify when a backfill operation completes. |
| `Settings.NotifyDataGaps` | `bool` | `true` | Notify when data gaps are detected. |
| `Settings.NotifyStorageWarnings` | `bool` | `true` | Notify when storage warnings are raised. |
| `Settings.QuietHoursEnabled` | `bool` | `false` | Enable quiet hours (suppress notifications during a time range). |
| `Settings.QuietHoursStart` | `string` | `"22:00"` | Start of quiet hours (24-hour `HH:mm` format). |
| `Settings.QuietHoursEnd` | `string` | `"07:00"` | End of quiet hours (24-hour `HH:mm` format). |
| `Settings.AutoReconnectEnabled` | `bool` | `true` | Automatically reconnect to the service on connection loss. |
| `Settings.MaxReconnectAttempts` | `int` | `10` | Maximum number of reconnection attempts before giving up. |
| `Settings.StatusRefreshIntervalSeconds` | `int` | `2` | Interval in seconds for polling the status endpoint. |
| `Settings.ServiceUrl` | `string` | `"http://localhost:8080"` | URL of the Market Data Collector service backend. |
| `Settings.ServiceTimeoutSeconds` | `int` | `30` | Timeout in seconds for API requests to the service. |
| `Settings.BackfillTimeoutMinutes` | `int` | `60` | Timeout in minutes for long-running backfill operations. |

---

## Serilog

Structured logging configuration via [Serilog](https://serilog.net/).

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `Serilog.MinimumLevel` | `object` | — | Minimum log level settings. Sub-keys: `Default` (`"Information"`), `Override` (per-namespace overrides). |
| `Serilog.WriteTo` | `object[]` | — | Array of sink definitions. Each entry has `Name` (e.g., `"Console"`, `"File"`) and `Args`. |

---

*This file is auto-generated. Do not edit manually.*
