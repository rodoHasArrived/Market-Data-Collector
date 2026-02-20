# Configuration Schema

> Auto-generated from `config/appsettings.sample.json`

This document describes the configuration options available in the Market Data Collector.

## Configuration Sections

### DataRoot

Type: `str`

### Compress

Type: `bool`

### Backfill

| Setting | Type | Description |
|---------|------|-------------|
| `Enabled` | bool | - |
| `Provider` | str | - |
| `Symbols` | array | - |
| `From` | str | - |
| `To` | str | - |
| `Granularity` | str | - |
| `EnableFallback` | bool | - |
| `PreferAdjustedPrices` | bool | - |
| `EnableSymbolResolution` | bool | - |
| `ProviderPriority` | NoneType | - |
| `EnableRateLimitRotation` | bool | - |
| `RateLimitRotationThreshold` | float | - |
| `SkipExistingData` | bool | - |
| `FillGapsOnly` | bool | - |
| `Jobs` | object | - |
| `Providers` | object | - |

### DataSource

Type: `str`

### DataSources

| Setting | Type | Description |
|---------|------|-------------|
| `Sources` | array | - |
| `DefaultRealTimeSourceId` | str | - |
| `DefaultHistoricalSourceId` | str | - |
| `EnableFailover` | bool | - |
| `FailoverTimeoutSeconds` | int | - |
| `HealthCheckIntervalSeconds` | int | - |
| `AutoRecover` | bool | - |
| `FailoverRules` | array | - |
| `SymbolMappings` | object | - |

### Alpaca

| Setting | Type | Description |
|---------|------|-------------|
| `Feed` | str | - |
| `UseSandbox` | bool | - |
| `SubscribeQuotes` | bool | - |

### StockSharp

| Setting | Type | Description |
|---------|------|-------------|
| `Enabled` | bool | - |
| `ConnectorType` | str | - |
| `AdapterType` | str | - |
| `AdapterAssembly` | str | - |
| `EnableRealTime` | bool | - |
| `EnableHistorical` | bool | - |
| `UseBinaryStorage` | bool | - |
| `StoragePath` | str | - |
| `Rithmic` | object | - |
| `IQFeed` | object | - |
| `CQG` | object | - |
| `InteractiveBrokers` | object | - |

### Storage

| Setting | Type | Description |
|---------|------|-------------|
| `NamingConvention` | str | - |
| `DatePartition` | str | - |
| `IncludeProvider` | bool | - |
| `FilePrefix` | NoneType | - |
| `Profile` | NoneType | - |
| `RetentionDays` | NoneType | - |
| `MaxTotalMegabytes` | NoneType | - |

### Symbols

Type: `list`

### Derivatives

| Setting | Type | Description |
|---------|------|-------------|
| `Enabled` | bool | - |
| `Underlyings` | array | - |
| `MaxDaysToExpiration` | int | - |
| `StrikeRange` | int | - |
| `CaptureGreeks` | bool | - |
| `CaptureChainSnapshots` | bool | - |
| `ChainSnapshotIntervalSeconds` | int | - |
| `CaptureOpenInterest` | bool | - |
| `ExpirationFilter` | array | - |
| `IndexOptions` | object | - |

### Settings

| Setting | Type | Description |
|---------|------|-------------|
| `Theme` | str | - |
| `AccentColor` | str | - |
| `CompactMode` | bool | - |
| `NotificationsEnabled` | bool | - |
| `NotifyConnectionStatus` | bool | - |
| `NotifyErrors` | bool | - |
| `NotifyBackfillComplete` | bool | - |
| `NotifyDataGaps` | bool | - |
| `NotifyStorageWarnings` | bool | - |
| `QuietHoursEnabled` | bool | - |
| `QuietHoursStart` | str | - |
| `QuietHoursEnd` | str | - |
| `AutoReconnectEnabled` | bool | - |
| `MaxReconnectAttempts` | int | - |
| `StatusRefreshIntervalSeconds` | int | - |
| `ServiceUrl` | str | - |
| `ServiceTimeoutSeconds` | int | - |
| `BackfillTimeoutMinutes` | int | - |

### Serilog

| Setting | Type | Description |
|---------|------|-------------|
| `MinimumLevel` | object | - |
| `WriteTo` | array | - |

---
*This file is auto-generated. Do not edit manually.*
