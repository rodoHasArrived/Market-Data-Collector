# Storage Organization Design: Improvements & Best Practices

This document outlines storage organization improvements for the Market Data Collector, covering naming conventions, date partitioning, policies, capacity limits, and perpetual data management strategies.

---

## Table of Contents

1. [Executive Summary](#executive-summary)
2. [Naming Convention Improvements](#naming-convention-improvements)
3. [Date Partitioning Strategies](#date-partitioning-strategies)
4. [Storage Policies](#storage-policies)
5. [Capacity Limits & Quotas](#capacity-limits--quotas)
6. [Perpetual Data Management](#perpetual-data-management)
7. [Multi-Source Data Organization](#multi-source-data-organization)
8. [Tiered Storage Architecture](#tiered-storage-architecture)
9. [Implementation Roadmap](#implementation-roadmap)

---

## Executive Summary

### Current State
The system currently supports:
- 4 file naming conventions (Flat, BySymbol, ByDate, ByType)
- 4 date partitions (None, Daily, Hourly, Monthly)
- Time-based retention (RetentionDays)
- Capacity-based retention (MaxTotalMegabytes)
- Optional gzip compression

### Proposed Improvements
| Area | Current | Proposed Enhancement |
|------|---------|---------------------|
| Naming | 4 static patterns | Hierarchical taxonomy + metadata tags |
| Partitioning | Single dimension | Multi-dimensional partitioning |
| Policies | Delete-only | Tier-based lifecycle policies |
| Capacity | Global limit | Per-source/symbol quotas |
| Perpetual Data | Not supported | Archive tier with cold storage |
| Multi-Source | Implicit via filename | Explicit source registries |

---

## Naming Convention Improvements

### 1. Hierarchical Taxonomy Structure

**Proposed Directory Hierarchy:**
```
{root}/
├── _catalog/                    # Metadata & indices
│   ├── manifest.json            # Global catalog of all data
│   ├── sources.json             # Registered data sources
│   └── schemas/                 # Schema definitions per version
│       ├── v1.json
│       └── v2.json
├── _archive/                    # Cold storage tier
│   └── {year}/
│       └── {month}/
├── live/                        # Hot tier (current trading data)
│   └── {source}/
│       └── {asset_class}/
│           └── {symbol}/
│               └── {event_type}/
│                   └── {date}.jsonl.gz
└── historical/                  # Warm tier (backfill data)
    └── {provider}/
        └── {symbol}/
            └── {granularity}/
                └── {date_range}.parquet
```

### 2. Enhanced Naming Patterns

**Add new naming conventions:**

```csharp
enum FileNamingConvention
{
    // Existing
    Flat,           // {root}/{symbol}_{type}_{date}.jsonl
    BySymbol,       // {root}/{symbol}/{type}/{date}.jsonl
    ByDate,         // {root}/{date}/{symbol}/{type}.jsonl
    ByType,         // {root}/{type}/{symbol}/{date}.jsonl

    // NEW: Extended patterns
    BySource,       // {root}/{source}/{symbol}/{type}/{date}.jsonl
    ByAssetClass,   // {root}/{asset_class}/{symbol}/{type}/{date}.jsonl
    Hierarchical,   // {root}/{source}/{asset_class}/{symbol}/{type}/{date}.jsonl
    Canonical       // {root}/{year}/{month}/{day}/{source}/{symbol}/{type}.jsonl
}
```

### 3. Symbol Naming Standardization

**Implement canonical symbol registry:**

```json
{
  "symbols": {
    "AAPL": {
      "canonical": "AAPL",
      "aliases": ["AAPL.US", "AAPL.O", "US0378331005"],
      "asset_class": "equity",
      "exchange": "NASDAQ",
      "currency": "USD",
      "sedol": "2046251",
      "isin": "US0378331005",
      "figi": "BBG000B9XRY4"
    }
  }
}
```

**Benefits:**
- Unified symbol lookup across data sources
- Automatic alias resolution during queries
- Cross-reference with industry identifiers (ISIN, FIGI, SEDOL)

### 4. File Naming Metadata Encoding

**Embed queryable metadata in filenames:**

```
Format: {symbol}_{type}_{date}_{source}_{checksum}.jsonl.gz

Example: AAPL_Trade_2024-01-15_alpaca_a3f2b1.jsonl.gz
                                  │       │
                                  │       └── First 6 chars of SHA256
                                  └── Data source identifier
```

**Metadata index file (per directory):**
```json
{
  "files": [
    {
      "name": "AAPL_Trade_2024-01-15_alpaca_a3f2b1.jsonl.gz",
      "symbol": "AAPL",
      "type": "Trade",
      "date": "2024-01-15",
      "source": "alpaca",
      "checksum": "a3f2b1c4d5e6f7...",
      "size_bytes": 1048576,
      "event_count": 50000,
      "first_seq": 1000000,
      "last_seq": 1050000,
      "created_at": "2024-01-15T16:00:00Z"
    }
  ]
}
```

---

## Date Partitioning Strategies

### 1. Multi-Dimensional Partitioning

**Current:** Single partition dimension (date OR symbol OR type)

**Proposed:** Composite partitioning with configurable priority

```csharp
record PartitionStrategy(
    PartitionDimension Primary,      // e.g., Date
    PartitionDimension? Secondary,   // e.g., Symbol
    PartitionDimension? Tertiary,    // e.g., EventType
    DateGranularity DateFormat       // Daily, Hourly, Monthly
);

enum PartitionDimension
{
    Date,
    Symbol,
    EventType,
    Source,
    AssetClass,
    Exchange
}
```

**Example configurations:**

```json
{
  "Partitioning": {
    "Strategy": "composite",
    "Dimensions": ["Date", "Source", "Symbol"],
    "DateGranularity": "Daily"
  }
}
```

### 2. Adaptive Partitioning by Volume

**Auto-adjust partition granularity based on data volume:**

```csharp
record AdaptivePartitionConfig(
    long EventsPerHourThreshold = 100_000,  // Switch to hourly
    long EventsPerDayThreshold = 50_000,    // Stay at daily
    long EventsPerMonthThreshold = 10_000   // Switch to monthly
);
```

**Implementation logic:**
```
IF events_per_hour > 100,000 THEN use Hourly partitions
ELSE IF events_per_day > 50,000 THEN use Daily partitions
ELSE IF events_per_month < 10,000 THEN use Monthly partitions
```

### 3. Trading Calendar Awareness

**Align partitions with market calendars:**

```csharp
record TradingCalendarPartition(
    string Exchange,           // "NYSE", "NASDAQ", "CME"
    bool SkipNonTradingDays,   // Don't create files for holidays
    bool SeparatePreMarket,    // Pre-market in separate partition
    bool SeparateAfterHours,   // After-hours in separate partition
    TimeZoneInfo MarketTimeZone
);
```

**Directory structure with sessions:**
```
AAPL/Trade/
├── 2024-01-15_pre.jsonl.gz      # 04:00-09:30 ET
├── 2024-01-15_regular.jsonl.gz  # 09:30-16:00 ET
└── 2024-01-15_after.jsonl.gz    # 16:00-20:00 ET
```

### 4. Rolling Window Partitions

**For real-time analytics, maintain rolling windows:**

```csharp
record RollingPartitionConfig(
    TimeSpan WindowSize,        // e.g., 1 hour
    int WindowCount,            // Keep last N windows
    bool CompactOnRotation      // Merge into daily on rotation
);
```

**Example: 1-hour rolling windows, keep last 24:**
```
AAPL/Trade/
├── current.jsonl               # Active window (0-60 min old)
├── window_23.jsonl             # 1-2 hours old
├── window_22.jsonl             # 2-3 hours old
└── ...
└── window_00.jsonl             # 23-24 hours old → compact to daily
```

---

## Storage Policies

### 1. Lifecycle Policy Framework

**Define policies per data classification:**

```csharp
record StoragePolicy(
    string Name,
    DataClassification Classification,
    RetentionPolicy Retention,
    CompressionPolicy Compression,
    TieringPolicy Tiering,
    ReplicationPolicy? Replication
);

enum DataClassification
{
    Critical,       // Never delete (regulatory/compliance)
    Standard,       // Normal retention policies apply
    Transient,      // Short-lived, deletable quickly
    Derived         // Can be regenerated, aggressive cleanup
}
```

### 2. Tiered Retention Policies

```json
{
  "Policies": {
    "Trade": {
      "classification": "Critical",
      "hot_tier_days": 7,
      "warm_tier_days": 90,
      "cold_tier_days": 365,
      "archive_tier": "perpetual",
      "compression": {
        "hot": "none",
        "warm": "gzip",
        "cold": "zstd",
        "archive": "zstd-max"
      }
    },
    "L2Snapshot": {
      "classification": "Standard",
      "hot_tier_days": 3,
      "warm_tier_days": 30,
      "cold_tier_days": 180,
      "archive_tier": null,
      "compression": {
        "hot": "gzip",
        "warm": "zstd",
        "cold": "zstd-max"
      }
    },
    "Heartbeat": {
      "classification": "Transient",
      "hot_tier_days": 1,
      "warm_tier_days": 0,
      "cold_tier_days": 0,
      "archive_tier": null
    }
  }
}
```

### 3. Compression Policy Matrix

| Data Age | Compression | Ratio | CPU Cost | Use Case |
|----------|-------------|-------|----------|----------|
| Hot (< 7d) | None/LZ4 | 1-2x | Minimal | Real-time access |
| Warm (7-90d) | Gzip-6 | 5-8x | Low | Daily analytics |
| Cold (90-365d) | Zstd-12 | 10-15x | Medium | Monthly reports |
| Archive (> 1y) | Zstd-19 | 15-20x | High | Compliance/audit |

### 4. Integrity Policy

**Automatic data validation rules:**

```csharp
record IntegrityPolicy(
    bool ValidateOnWrite,           // Schema validation
    bool ChecksumOnWrite,           // SHA256 per file
    bool VerifyOnRead,              // Checksum verification
    int MaxSequenceGap,             // Alert threshold
    TimeSpan StaleDataThreshold,    // No data for N minutes = alert
    bool EnforceMonotonicity        // Reject out-of-order events
);
```

### 5. Backup Policy

```json
{
  "Backup": {
    "enabled": true,
    "schedule": "0 0 * * *",
    "retention_backups": 7,
    "targets": [
      {
        "type": "local",
        "path": "/backup/mdc"
      },
      {
        "type": "s3",
        "bucket": "mdc-backups",
        "region": "us-east-1",
        "storage_class": "GLACIER_IR"
      }
    ],
    "include_patterns": ["*.jsonl", "*.jsonl.gz", "*.parquet"],
    "exclude_patterns": ["**/current.jsonl", "**/_temp/*"]
  }
}
```

---

## Capacity Limits & Quotas

### 1. Hierarchical Quota System

**Multi-level capacity management:**

```csharp
record QuotaConfig(
    StorageQuota Global,
    Dictionary<string, StorageQuota> PerSource,
    Dictionary<string, StorageQuota> PerAssetClass,
    Dictionary<string, StorageQuota> PerSymbol,
    Dictionary<MarketEventType, StorageQuota> PerEventType
);

record StorageQuota(
    long MaxBytes,
    long? MaxFiles,
    long? MaxEventsPerDay,
    QuotaEnforcementPolicy Enforcement
);

enum QuotaEnforcementPolicy
{
    Warn,           // Log warning, continue writing
    SoftLimit,      // Start cleanup, continue writing
    HardLimit,      // Stop writing until cleanup completes
    DropOldest      // Delete oldest to make room
}
```

### 2. Per-Source Quotas

```json
{
  "Quotas": {
    "global": {
      "max_bytes": 107374182400,
      "enforcement": "SoftLimit"
    },
    "per_source": {
      "alpaca": {
        "max_bytes": 53687091200,
        "max_files": 100000,
        "enforcement": "DropOldest"
      },
      "ib": {
        "max_bytes": 53687091200,
        "enforcement": "SoftLimit"
      }
    },
    "per_symbol": {
      "default": {
        "max_bytes": 1073741824,
        "max_events_per_day": 10000000
      },
      "SPY": {
        "max_bytes": 10737418240,
        "max_events_per_day": 50000000
      }
    }
  }
}
```

### 3. Dynamic Quota Allocation

**Auto-adjust quotas based on usage patterns:**

```csharp
record DynamicQuotaConfig(
    bool Enabled,
    TimeSpan EvaluationPeriod,      // How often to rebalance
    double MinReservePct,            // Always keep N% free
    double OverprovisionFactor,      // Allow N% burst above quota
    bool StealFromInactive          // Reallocate from unused quotas
);
```

### 4. Capacity Forecasting

**Predict storage needs based on historical patterns:**

```csharp
interface ICapacityForecaster
{
    StorageForecast Forecast(TimeSpan horizon);
    Alert[] GetCapacityAlerts(double thresholdPct);
}

record StorageForecast(
    DateTimeOffset ForecastDate,
    long CurrentUsageBytes,
    long ProjectedUsageBytes,
    double GrowthRatePerDay,
    DateTimeOffset? EstimatedFullDate,
    Dictionary<string, long> BreakdownBySource
);
```

---

## Perpetual Data Management

### 1. Archive Tier Definition

**Data that must be kept indefinitely:**

```csharp
enum ArchiveReason
{
    Regulatory,         // SEC Rule 17a-4, MiFID II
    Compliance,         // Internal audit requirements
    Research,           // ML training datasets
    Legal,              // Litigation hold
    Historical          // Reference data
}

record ArchivePolicy(
    ArchiveReason Reason,
    string Description,
    bool Immutable,              // Write-once, never modify
    bool RequiresEncryption,
    TimeSpan? MinRetention,      // Minimum before deletion allowed
    string[] ApproversForDelete  // Required approvals
);
```

### 2. Perpetual Storage Organization

```
_archive/
├── regulatory/                  # SEC 17a-4 compliant
│   ├── manifest.json            # Cryptographically signed
│   └── 2024/
│       └── Q1/
│           ├── trades.parquet.zst
│           ├── trades.manifest.json
│           └── trades.sha256
├── research/                    # ML training datasets
│   ├── labeled/
│   │   └── market_regimes/
│   └── raw/
│       └── tick_data/
└── legal_holds/                 # Litigation preservation
    └── case_12345/
        ├── hold_notice.json
        └── preserved_data/
```

### 3. Write-Once Append-Only (WORM) Support

```csharp
record WormConfig(
    bool Enabled,
    TimeSpan LockDelay,          // Time before lock engages
    bool AllowExtend,            // Can extend retention, not shorten
    string ComplianceMode        // "governance" | "compliance"
);
```

### 4. Archive Format Optimization

**Convert to columnar format for long-term storage:**

```csharp
record ArchiveFormat(
    string Format,                // "parquet" | "orc" | "avro"
    string Compression,           // "zstd" | "snappy" | "brotli"
    int CompressionLevel,
    bool EnableBloomFilters,      // Fast existence checks
    bool EnableStatistics,        // Min/max per column
    string[] PartitionColumns,    // e.g., ["date", "symbol"]
    int RowGroupSize              // Rows per group (default 100K)
);
```

**Parquet conversion pipeline:**
```
Daily JSONL files (hot tier)
    ↓ (after 7 days)
Weekly Parquet files (warm tier)
    ↓ (after 90 days)
Monthly Parquet files (cold tier)
    ↓ (after 365 days)
Yearly Parquet files (archive tier, perpetual)
```

### 5. Data Catalog for Perpetual Data

```json
{
  "catalog": {
    "version": "1.0",
    "entries": [
      {
        "id": "uuid-v4",
        "path": "_archive/regulatory/2024/Q1/trades.parquet.zst",
        "type": "Trade",
        "symbols": ["AAPL", "GOOGL", "MSFT"],
        "date_range": {
          "start": "2024-01-01",
          "end": "2024-03-31"
        },
        "event_count": 150000000,
        "size_bytes": 2147483648,
        "checksum": "sha256:abc123...",
        "created_at": "2024-04-01T00:00:00Z",
        "archive_reason": "Regulatory",
        "retention_until": null,
        "immutable": true,
        "schema_version": 1
      }
    ]
  }
}
```

---

## Multi-Source Data Organization

### 1. Source Registry

**Centralized source management:**

```json
{
  "sources": {
    "alpaca": {
      "id": "alpaca",
      "name": "Alpaca Markets",
      "type": "live",
      "priority": 1,
      "asset_classes": ["equity"],
      "data_types": ["Trade", "BboQuote", "L2Snapshot"],
      "latency_ms": 10,
      "reliability": 0.999,
      "cost_per_event": 0.0001,
      "enabled": true
    },
    "ib": {
      "id": "ib",
      "name": "Interactive Brokers",
      "type": "live",
      "priority": 2,
      "asset_classes": ["equity", "options", "futures", "forex"],
      "data_types": ["Trade", "BboQuote", "L2Snapshot", "OrderFlow"],
      "latency_ms": 5,
      "reliability": 0.9999,
      "enabled": true
    },
    "polygon": {
      "id": "polygon",
      "name": "Polygon.io",
      "type": "live",
      "priority": 3,
      "asset_classes": ["equity", "crypto"],
      "enabled": false
    },
    "stooq": {
      "id": "stooq",
      "name": "Stooq Historical",
      "type": "historical",
      "asset_classes": ["equity"],
      "data_types": ["HistoricalBar"],
      "enabled": true
    }
  }
}
```

### 2. Source-Aware Directory Structure

```
data/
├── live/
│   ├── alpaca/
│   │   ├── equity/
│   │   │   ├── AAPL/
│   │   │   └── SPY/
│   │   └── _source_meta.json
│   └── ib/
│       ├── equity/
│       ├── options/
│       └── futures/
├── historical/
│   ├── stooq/
│   ├── yahoo/
│   └── nasdaq/
└── consolidated/               # Merged view across sources
    └── AAPL/
        └── Trade/
            └── 2024-01-15.parquet  # Best-of-breed merged
```

### 3. Source Conflict Resolution

**When multiple sources have overlapping data:**

```csharp
record ConflictResolutionPolicy(
    ConflictStrategy Strategy,
    string[] PriorityOrder,          // ["ib", "alpaca", "polygon"]
    bool KeepAll,                    // Store all versions
    bool CreateConsolidated          // Merge into golden record
);

enum ConflictStrategy
{
    FirstWins,          // Keep first source's data
    LastWins,           // Keep latest source's data
    HighestPriority,    // Use priority order
    LowestLatency,      // Prefer lowest-latency source
    MostComplete,       // Source with most fields populated
    Merge               // Combine and deduplicate
}
```

### 4. Cross-Source Reconciliation

```csharp
record ReconciliationConfig(
    bool Enabled,
    TimeSpan Window,                 // Compare events within window
    decimal PriceTolerance,          // 0.01 = 1 cent
    long VolumeTolerance,            // Acceptable volume difference
    bool GenerateDiscrepancyReport,
    string ReportPath
);
```

**Reconciliation report:**
```json
{
  "date": "2024-01-15",
  "symbol": "AAPL",
  "sources_compared": ["alpaca", "ib"],
  "total_events": {
    "alpaca": 50000,
    "ib": 50250
  },
  "matched": 49800,
  "discrepancies": [
    {
      "type": "missing_in_source",
      "source": "alpaca",
      "count": 250
    },
    {
      "type": "price_mismatch",
      "count": 50,
      "avg_difference": 0.005
    }
  ]
}
```

---

## Tiered Storage Architecture

### 1. Storage Tier Definitions

| Tier | Age | Storage Type | Access Pattern | Format | Compression |
|------|-----|--------------|----------------|--------|-------------|
| Hot | 0-7d | NVMe SSD | Real-time, random | JSONL | None/LZ4 |
| Warm | 7-90d | SSD/HDD | Daily batch | JSONL.gz | Gzip |
| Cold | 90-365d | HDD/NAS | Weekly/monthly | Parquet | Zstd |
| Archive | >1y | Object Storage | Rare, bulk | Parquet | Zstd-max |
| Glacier | >3y | Tape/Glacier | Emergency only | Parquet | Zstd-max |

### 2. Tiering Configuration

```json
{
  "Tiering": {
    "enabled": true,
    "tiers": [
      {
        "name": "hot",
        "path": "/fast-ssd/mdc/hot",
        "max_age_days": 7,
        "max_size_gb": 100,
        "format": "jsonl",
        "compression": null
      },
      {
        "name": "warm",
        "path": "/ssd/mdc/warm",
        "max_age_days": 90,
        "max_size_gb": 500,
        "format": "jsonl.gz",
        "compression": "gzip"
      },
      {
        "name": "cold",
        "path": "/hdd/mdc/cold",
        "max_age_days": 365,
        "max_size_gb": 2000,
        "format": "parquet",
        "compression": "zstd"
      },
      {
        "name": "archive",
        "path": "s3://mdc-archive",
        "max_age_days": null,
        "format": "parquet",
        "compression": "zstd",
        "storage_class": "GLACIER_IR"
      }
    ],
    "migration_schedule": "0 2 * * *",
    "parallel_migrations": 4
  }
}
```

### 3. Tier Migration Service

```csharp
interface ITierMigrationService
{
    Task<MigrationResult> MigrateAsync(
        string sourcePath,
        StorageTier targetTier,
        MigrationOptions options,
        CancellationToken ct
    );

    Task<MigrationPlan> PlanMigrationAsync(
        TimeSpan horizon,
        CancellationToken ct
    );
}

record MigrationOptions(
    bool DeleteSource,
    bool VerifyChecksum,
    bool ConvertFormat,
    int ParallelFiles,
    Action<MigrationProgress>? OnProgress
);
```

### 4. Unified Query Layer

**Query across all tiers transparently:**

```csharp
interface IUnifiedStorageReader
{
    IAsyncEnumerable<MarketEvent> ReadAsync(
        StorageQuery query,
        CancellationToken ct
    );
}

record StorageQuery(
    string[] Symbols,
    MarketEventType[] Types,
    DateTimeOffset Start,
    DateTimeOffset End,
    string[]? Sources,
    StorageTier[]? PreferredTiers,  // Hint for optimization
    int? MaxEvents
);
```

---

## Implementation Roadmap

### Phase 1: Foundation (Weeks 1-2)
- [ ] Implement source registry configuration
- [ ] Add hierarchical naming convention option
- [ ] Create file manifest generation
- [ ] Implement per-source quota tracking

### Phase 2: Policies (Weeks 3-4)
- [ ] Define policy configuration schema
- [ ] Implement policy evaluation engine
- [ ] Add compression policy matrix
- [ ] Create backup policy executor

### Phase 3: Tiering (Weeks 5-6)
- [ ] Define tier configuration schema
- [ ] Implement tier migration service
- [ ] Add Parquet conversion pipeline
- [ ] Create unified query interface

### Phase 4: Perpetual Storage (Weeks 7-8)
- [ ] Implement archive tier with WORM support
- [ ] Add data catalog service
- [ ] Create compliance reporting
- [ ] Implement immutability guarantees

### Phase 5: Advanced Features (Weeks 9-10)
- [ ] Add capacity forecasting
- [ ] Implement cross-source reconciliation
- [ ] Create trading calendar integration
- [ ] Add adaptive partitioning

---

## Configuration Examples

### Minimal Configuration (Development)

```json
{
  "Storage": {
    "NamingConvention": "BySymbol",
    "DatePartition": "Daily",
    "RetentionDays": 7,
    "Compress": false
  }
}
```

### Standard Configuration (Production)

```json
{
  "Storage": {
    "NamingConvention": "Hierarchical",
    "DatePartition": "Daily",
    "Compress": true,
    "CompressionCodec": "gzip",
    "RetentionDays": 90,
    "MaxTotalMegabytes": 102400,
    "Quotas": {
      "PerSource": {
        "alpaca": { "MaxBytes": 53687091200 }
      }
    },
    "Policies": {
      "Trade": {
        "Classification": "Critical",
        "WarmTierDays": 30,
        "ColdTierDays": 180
      }
    }
  }
}
```

### Enterprise Configuration (Compliance)

```json
{
  "Storage": {
    "NamingConvention": "Canonical",
    "DatePartition": "Daily",
    "Compress": true,
    "CompressionCodec": "zstd",
    "Tiering": {
      "Enabled": true,
      "Tiers": ["hot", "warm", "cold", "archive"]
    },
    "Archive": {
      "Enabled": true,
      "Worm": true,
      "Reason": "Regulatory",
      "MinRetentionYears": 7
    },
    "Catalog": {
      "Enabled": true,
      "SignManifests": true
    },
    "Reconciliation": {
      "Enabled": true,
      "Sources": ["alpaca", "ib"]
    }
  }
}
```

---

## Summary

This design provides a comprehensive framework for storage organization that:

1. **Scales** from development to enterprise compliance requirements
2. **Optimizes** storage costs through intelligent tiering and compression
3. **Ensures** data integrity through checksums, validation, and reconciliation
4. **Supports** perpetual data retention for regulatory compliance
5. **Enables** flexible querying across multiple sources and time ranges
6. **Manages** capacity proactively through quotas and forecasting

The modular design allows incremental adoption—start with basic naming conventions and add policies, tiering, and archival as needs grow.
