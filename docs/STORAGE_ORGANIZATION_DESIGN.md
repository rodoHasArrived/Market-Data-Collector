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
9. [File Maintenance & Health Monitoring](#file-maintenance--health-monitoring)
10. [Data Robustness & Quality Scoring](#data-robustness--quality-scoring)
11. [Search & Discovery Infrastructure](#search--discovery-infrastructure)
12. [Actionable Metadata & Insights](#actionable-metadata--insights)
13. [Implementation Roadmap](#implementation-roadmap)

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
| **File Maintenance** | Manual | Automated health checks, self-healing |
| **Data Robustness** | Basic validation | Quality scoring, best-of-breed selection |
| **Search** | File system only | Multi-level indexes, faceted search |
| **Metadata** | Minimal | Rich metadata, insights, lineage tracking |

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

## File Maintenance & Health Monitoring

### 1. Automated File Health Service

**Continuous monitoring and self-healing for storage integrity:**

```csharp
interface IFileMaintenanceService
{
    Task<HealthReport> RunHealthCheckAsync(HealthCheckOptions options, CancellationToken ct);
    Task<RepairResult> RepairAsync(RepairOptions options, CancellationToken ct);
    Task<DefragResult> DefragmentAsync(DefragOptions options, CancellationToken ct);
    Task<OrphanReport> FindOrphansAsync(CancellationToken ct);
}

record HealthCheckOptions(
    bool ValidateChecksums,          // Verify file integrity
    bool CheckSequenceContinuity,    // Detect gaps in sequences
    bool ValidateSchemas,            // Ensure JSON/Parquet valid
    bool CheckFilePermissions,       // Verify read/write access
    bool IdentifyCorruption,         // Detect partial writes
    string[] Paths,                  // Specific paths or "*" for all
    int ParallelChecks               // Concurrent validation threads
);
```

### 2. File Health Report Structure

```json
{
  "report_id": "uuid-v4",
  "generated_at": "2024-01-15T12:00:00Z",
  "scan_duration_ms": 45000,
  "summary": {
    "total_files": 15000,
    "total_bytes": 107374182400,
    "healthy_files": 14950,
    "warning_files": 35,
    "corrupted_files": 15,
    "orphaned_files": 12
  },
  "issues": [
    {
      "severity": "critical",
      "type": "checksum_mismatch",
      "path": "live/alpaca/AAPL/Trade/2024-01-10.jsonl.gz",
      "expected_checksum": "sha256:abc123...",
      "actual_checksum": "sha256:def456...",
      "recommended_action": "restore_from_backup",
      "auto_repairable": true
    },
    {
      "severity": "warning",
      "type": "sequence_gap",
      "path": "live/ib/SPY/Trade/2024-01-12.jsonl",
      "details": {
        "gap_start": 1000500,
        "gap_end": 1000750,
        "missing_events": 250
      },
      "recommended_action": "backfill_from_source",
      "auto_repairable": false
    },
    {
      "severity": "info",
      "type": "orphaned_file",
      "path": "live/alpaca/DELETED_SYMBOL/Trade/2024-01-05.jsonl",
      "reason": "symbol_not_in_registry",
      "recommended_action": "archive_or_delete"
    }
  ],
  "statistics": {
    "avg_file_size_bytes": 7158278,
    "oldest_file": "2023-01-01",
    "newest_file": "2024-01-15",
    "compression_ratio": 8.5,
    "fragmentation_pct": 12.3
  }
}
```

### 3. Self-Healing Capabilities

```csharp
record RepairOptions(
    RepairStrategy Strategy,
    bool DryRun,                     // Preview changes only
    bool BackupBeforeRepair,         // Create backup first
    string BackupPath,
    RepairScope Scope
);

enum RepairStrategy
{
    RestoreFromBackup,       // Use backup copy
    BackfillFromSource,      // Re-fetch from data provider
    TruncateCorrupted,       // Remove corrupted tail
    RebuildIndex,            // Regenerate metadata index
    MergeFragments,          // Combine small files
    RecompressOptimal        // Apply better compression
}

enum RepairScope
{
    SingleFile,
    Directory,
    Symbol,
    DateRange,
    EventType,
    All
}
```

### 4. Scheduled Maintenance Tasks

```json
{
  "Maintenance": {
    "enabled": true,
    "schedule": {
      "health_check": "0 3 * * *",
      "defragmentation": "0 4 * * 0",
      "orphan_cleanup": "0 5 1 * *",
      "index_rebuild": "0 2 * * 0",
      "backup_verification": "0 6 * * 0"
    },
    "auto_repair": {
      "enabled": true,
      "max_auto_repairs_per_run": 100,
      "require_backup": true,
      "notify_on_repair": ["admin@example.com"]
    },
    "thresholds": {
      "fragmentation_trigger_pct": 20,
      "min_file_size_for_merge_bytes": 1048576,
      "max_file_age_for_hot_tier_days": 7
    }
  }
}
```

### 5. File Compaction & Defragmentation

**Merge small files and optimize storage layout:**

```csharp
record DefragOptions(
    long MinFileSizeBytes,           // Files smaller than this get merged
    int MaxFilesPerMerge,            // Batch size for merging
    bool PreserveOriginals,          // Keep originals until verified
    CompressionLevel TargetCompression,
    TimeSpan MaxFileAge              // Only defrag files older than
);

interface IFileCompactor
{
    // Merge multiple small files into optimized larger files
    Task<CompactionResult> CompactAsync(
        string[] sourcePaths,
        string targetPath,
        CompactionOptions options,
        CancellationToken ct
    );

    // Split oversized files into manageable chunks
    Task<SplitResult> SplitAsync(
        string sourcePath,
        long maxChunkBytes,
        CancellationToken ct
    );
}

record CompactionResult(
    int FilesProcessed,
    int FilesCreated,
    long BytesBefore,
    long BytesAfter,
    double CompressionImprovement,
    TimeSpan Duration
);
```

### 6. Orphan Detection & Cleanup

```csharp
record OrphanDetectionConfig(
    bool CheckSymbolRegistry,        // Files for unknown symbols
    bool CheckSourceRegistry,        // Files from unknown sources
    bool CheckDateRanges,            // Files outside expected dates
    bool CheckManifestConsistency,   // Files not in manifest
    OrphanAction DefaultAction
);

enum OrphanAction
{
    Report,              // Just log, take no action
    Quarantine,          // Move to _quarantine folder
    Archive,             // Move to archive tier
    Delete               // Remove permanently
}
```

---

## Data Robustness & Quality Scoring

### 1. Data Quality Dimensions

**Evaluate data across multiple quality dimensions:**

```csharp
record DataQualityScore(
    string Path,
    DateTimeOffset EvaluatedAt,
    double OverallScore,             // 0.0 - 1.0
    QualityDimension[] Dimensions
);

record QualityDimension(
    string Name,
    double Score,                    // 0.0 - 1.0
    double Weight,                   // Importance factor
    string[] Issues                  // Specific problems found
);
```

**Quality Dimensions:**

| Dimension | Description | Scoring Criteria |
|-----------|-------------|------------------|
| Completeness | No missing data | % of expected events present |
| Accuracy | Data matches source | Cross-source validation |
| Timeliness | Data is current | Lag from event to storage |
| Consistency | No conflicts | Schema compliance, no duplicates |
| Integrity | Data uncorrupted | Checksum validation |
| Continuity | No sequence gaps | Sequence number analysis |

### 2. Quality Scoring Engine

```csharp
interface IDataQualityService
{
    Task<DataQualityScore> ScoreAsync(string path, CancellationToken ct);
    Task<QualityReport> GenerateReportAsync(QualityReportOptions options, CancellationToken ct);
    Task<DataQualityScore[]> GetHistoricalScoresAsync(string path, TimeSpan window, CancellationToken ct);
}

record QualityReportOptions(
    string[] Paths,
    DateTimeOffset? From,
    DateTimeOffset? To,
    double MinScoreThreshold,        // Only include if score < threshold
    bool IncludeRecommendations,
    bool CompareAcrossSources
);
```

### 3. Quality Score Calculation

```csharp
// Completeness Score
completeness = actual_events / expected_events;

// Expected events derived from:
// - Historical average for symbol/date
// - Trading hours × average events/minute
// - Cross-source comparison

// Accuracy Score (when multiple sources available)
accuracy = matching_events / total_comparable_events;

// Timeliness Score
timeliness = 1.0 - (avg_latency_ms / max_acceptable_latency_ms);

// Consistency Score
consistency = valid_schema_events / total_events
            × unique_events / total_events  // penalize duplicates
            × events_in_sequence / total_events;

// Integrity Score
integrity = verified_checksums / total_files
          × uncorrupted_files / total_files;

// Continuity Score
continuity = 1.0 - (gap_count × gap_penalty);
```

### 4. Best-of-Breed Data Selection

**When multiple sources exist, select the most robust data:**

```csharp
interface IBestOfBreedSelector
{
    Task<SourceRanking[]> RankSourcesAsync(
        string symbol,
        DateTimeOffset date,
        MarketEventType type,
        CancellationToken ct
    );

    Task<ConsolidatedDataset> CreateGoldenRecordAsync(
        string symbol,
        DateTimeOffset date,
        ConsolidationOptions options,
        CancellationToken ct
    );
}

record SourceRanking(
    string Source,
    double QualityScore,
    long EventCount,
    int GapCount,
    double Latency,
    bool IsRecommended
);

record ConsolidationOptions(
    SourceSelectionStrategy Strategy,
    bool FillGapsFromAlternates,     // Use secondary sources for missing data
    bool ValidateCrossSource,        // Cross-check prices/volumes
    decimal PriceTolerancePct,       // Max price diff before flagging
    long VolumeTolerancePct          // Max volume diff before flagging
);

enum SourceSelectionStrategy
{
    HighestQualityScore,     // Best overall quality
    MostComplete,            // Highest event count
    LowestLatency,           // Fastest data
    MostConsistent,          // Fewest anomalies
    Merge                    // Combine best of each
}
```

### 5. Quality-Aware Storage Decisions

```json
{
  "QualityPolicies": {
    "minimum_score_for_archive": 0.95,
    "minimum_score_for_research": 0.90,
    "quarantine_below_score": 0.70,
    "auto_backfill_below_score": 0.85,
    "prefer_source_above_score": 0.98,
    "consolidation": {
      "enabled": true,
      "schedule": "0 6 * * *",
      "target_directory": "consolidated",
      "strategy": "Merge",
      "min_sources_for_consolidation": 2
    }
  }
}
```

### 6. Quality Trend Monitoring

```csharp
interface IQualityTrendMonitor
{
    Task<QualityTrend> GetTrendAsync(
        string symbol,
        TimeSpan window,
        CancellationToken ct
    );

    Task<Alert[]> GetQualityAlertsAsync(CancellationToken ct);
}

record QualityTrend(
    string Symbol,
    double CurrentScore,
    double PreviousScore,
    double TrendDirection,           // -1.0 to 1.0
    string[] DegradingDimensions,
    string[] ImprovingDimensions,
    DateTimeOffset[] ScoreHistory,
    double[] ScoreValues
);
```

**Quality Dashboard Metrics:**

```json
{
  "quality_dashboard": {
    "overall_score": 0.94,
    "by_source": {
      "alpaca": 0.96,
      "ib": 0.93,
      "polygon": 0.91
    },
    "by_event_type": {
      "Trade": 0.97,
      "L2Snapshot": 0.92,
      "BboQuote": 0.95
    },
    "by_symbol": {
      "SPY": 0.98,
      "AAPL": 0.96,
      "TSLA": 0.89
    },
    "alerts": [
      {
        "symbol": "TSLA",
        "issue": "quality_degradation",
        "current_score": 0.89,
        "previous_score": 0.95,
        "recommendation": "investigate_source_ib"
      }
    ]
  }
}
```

---

## Search & Discovery Infrastructure

### 1. Multi-Level Index Architecture

**Hierarchical indexing for fast discovery:**

```
_index/
├── global/
│   ├── symbols.idx              # All symbols with metadata
│   ├── date_range.idx           # Date coverage per symbol
│   ├── sources.idx              # Source availability matrix
│   └── statistics.idx           # Aggregated stats
├── by_symbol/
│   └── {symbol}/
│       ├── files.idx            # All files for symbol
│       ├── sequences.idx        # Sequence ranges per file
│       └── quality.idx          # Quality scores history
├── by_date/
│   └── {yyyy-mm-dd}/
│       ├── symbols.idx          # Symbols with data on date
│       └── summary.idx          # Daily statistics
└── full_text/
    └── events.idx               # Full-text search index
```

### 2. Index Schema

```csharp
record GlobalSymbolIndex(
    Dictionary<string, SymbolIndexEntry> Symbols,
    DateTimeOffset LastUpdated,
    int Version
);

record SymbolIndexEntry(
    string Symbol,
    string CanonicalName,
    string[] Aliases,
    string AssetClass,
    string Exchange,
    DateTimeOffset FirstDataDate,
    DateTimeOffset LastDataDate,
    long TotalEvents,
    long TotalBytes,
    string[] AvailableSources,
    MarketEventType[] AvailableTypes,
    double QualityScore,
    Dictionary<string, SourceCoverage> SourceCoverage
);

record SourceCoverage(
    string Source,
    DateTimeOffset FirstDate,
    DateTimeOffset LastDate,
    long EventCount,
    double CoveragePct              // % of trading days with data
);
```

### 3. Search Query API

```csharp
interface IStorageSearchService
{
    // Find files matching criteria
    Task<SearchResult<FileInfo>> SearchFilesAsync(
        FileSearchQuery query,
        CancellationToken ct
    );

    // Find events within files
    Task<SearchResult<MarketEvent>> SearchEventsAsync(
        EventSearchQuery query,
        CancellationToken ct
    );

    // Discover available data
    Task<DataCatalog> DiscoverAsync(
        DiscoveryQuery query,
        CancellationToken ct
    );
}

record FileSearchQuery(
    string[]? Symbols,
    MarketEventType[]? Types,
    string[]? Sources,
    DateTimeOffset? From,
    DateTimeOffset? To,
    long? MinSize,
    long? MaxSize,
    double? MinQualityScore,
    string? PathPattern,             // Glob pattern
    SortField SortBy,
    bool Descending,
    int Skip,
    int Take
);

record EventSearchQuery(
    string Symbol,
    MarketEventType Type,
    DateTimeOffset From,
    DateTimeOffset To,
    decimal? MinPrice,
    decimal? MaxPrice,
    long? MinVolume,
    AggressorSide? Side,
    long? SequenceFrom,
    long? SequenceTo,
    int Limit
);
```

### 4. Faceted Search Support

```json
{
  "search": {
    "query": "AAPL",
    "filters": {
      "date_range": ["2024-01-01", "2024-01-31"],
      "event_types": ["Trade", "BboQuote"],
      "sources": ["alpaca"]
    }
  },
  "results": {
    "total_matches": 1250000,
    "files": 31,
    "facets": {
      "by_date": {
        "2024-01-02": 45000,
        "2024-01-03": 42000,
        "...": "..."
      },
      "by_event_type": {
        "Trade": 800000,
        "BboQuote": 450000
      },
      "by_source": {
        "alpaca": 1250000
      },
      "by_hour": {
        "09": 150000,
        "10": 180000,
        "...": "..."
      }
    }
  }
}
```

### 5. Natural Language Query Support

**Parse human-readable queries into structured searches:**

```csharp
interface INaturalLanguageQueryParser
{
    StorageQuery Parse(string naturalQuery);
}

// Example queries:
// "AAPL trades from last week"
//   → Symbol: AAPL, Type: Trade, From: -7d
//
// "all L2 snapshots for SPY on January 15th"
//   → Symbol: SPY, Type: L2Snapshot, Date: 2024-01-15
//
// "high volume trades over 1M shares"
//   → Type: Trade, MinVolume: 1000000
//
// "data gaps in TSLA for December"
//   → Symbol: TSLA, Month: 2024-12, Query: gaps
```

### 6. Real-Time Index Updates

```csharp
interface IIndexMaintainer
{
    // Called after each file write
    Task UpdateIndexAsync(
        string filePath,
        IndexUpdateType updateType,
        CancellationToken ct
    );

    // Rebuild indexes from scratch
    Task RebuildIndexAsync(
        string[] paths,
        RebuildOptions options,
        CancellationToken ct
    );
}

enum IndexUpdateType
{
    FileCreated,
    FileAppended,
    FileDeleted,
    FileMoved,
    MetadataChanged
}
```

### 7. Search Performance Optimization

```json
{
  "SearchOptimization": {
    "index_in_memory": true,
    "max_index_memory_mb": 512,
    "cache_recent_queries": true,
    "query_cache_size": 1000,
    "query_cache_ttl_seconds": 300,
    "parallel_search_threads": 4,
    "bloom_filters": {
      "enabled": true,
      "false_positive_rate": 0.01
    },
    "partitioned_indexes": {
      "by_date": true,
      "by_symbol": true
    }
  }
}
```

---

## Actionable Metadata & Insights

### 1. Rich Metadata Schema

**Comprehensive metadata for every data file:**

```csharp
record FileMetadata(
    // Identity
    string FilePath,
    string FileId,                   // UUID
    string Checksum,

    // Content Description
    string Symbol,
    MarketEventType EventType,
    string Source,
    DateTimeOffset Date,

    // Statistics
    long EventCount,
    long SizeBytes,
    long SizeCompressed,
    double CompressionRatio,

    // Temporal Coverage
    DateTimeOffset FirstEventTime,
    DateTimeOffset LastEventTime,
    TimeSpan Duration,
    double EventsPerSecond,

    // Sequence Info
    long FirstSequence,
    long LastSequence,
    int SequenceGaps,
    long[] GapRanges,

    // Quality Metrics
    double QualityScore,
    int WarningCount,
    int ErrorCount,
    string[] ValidationIssues,

    // Price Statistics (for Trade events)
    decimal? PriceMin,
    decimal? PriceMax,
    decimal? PriceOpen,
    decimal? PriceClose,
    decimal? VWAP,

    // Volume Statistics
    long? TotalVolume,
    long? BuyVolume,
    long? SellVolume,
    double? BuySellRatio,

    // Lifecycle
    DateTimeOffset CreatedAt,
    DateTimeOffset? ModifiedAt,
    DateTimeOffset? ArchivedAt,
    string CurrentTier,
    string[] TierHistory,

    // Lineage
    string? ParentFileId,            // If derived/compacted
    string[] ChildFileIds,
    string? SourceProvider,
    string? BackfillProvider
);
```

### 2. Automated Insights Generation

```csharp
interface IInsightGenerator
{
    Task<Insight[]> GenerateInsightsAsync(
        InsightScope scope,
        CancellationToken ct
    );
}

record Insight(
    InsightType Type,
    InsightSeverity Severity,
    string Title,
    string Description,
    string[] AffectedPaths,
    string[] RecommendedActions,
    Dictionary<string, object> Context,
    DateTimeOffset GeneratedAt
);

enum InsightType
{
    // Storage Insights
    StorageGrowthAnomaly,
    UnusualCompressionRatio,
    HighFragmentation,
    QuotaNearLimit,

    // Data Quality Insights
    QualityDegradation,
    SourceReliabilityIssue,
    SequenceGapPattern,
    DataLatencyIncrease,

    // Optimization Insights
    CompressionOpportunity,
    ArchivalCandidate,
    ConsolidationOpportunity,
    UnusedDataPattern,

    // Anomaly Detection
    VolumeSpike,
    PriceAnomaly,
    MissingExpectedData,
    DuplicateDataDetected
}
```

### 3. Insight Examples

```json
{
  "insights": [
    {
      "type": "CompressionOpportunity",
      "severity": "info",
      "title": "Compression savings available",
      "description": "150 files in warm tier using gzip could save 40% space with zstd",
      "affected_paths": ["warm/alpaca/equity/**/*.jsonl.gz"],
      "recommended_actions": [
        "Run: migrate-compression --from gzip --to zstd --tier warm"
      ],
      "context": {
        "current_size_gb": 50,
        "projected_size_gb": 30,
        "savings_gb": 20
      }
    },
    {
      "type": "QualityDegradation",
      "severity": "warning",
      "title": "TSLA data quality declining",
      "description": "Quality score dropped from 0.95 to 0.87 over past 7 days",
      "affected_paths": ["live/ib/equity/TSLA/**"],
      "recommended_actions": [
        "Check IB connection for TSLA subscription",
        "Compare with Alpaca data for gaps",
        "Consider switching primary source"
      ],
      "context": {
        "score_history": [0.95, 0.93, 0.91, 0.89, 0.88, 0.87, 0.87],
        "main_issues": ["sequence_gaps", "increased_latency"]
      }
    },
    {
      "type": "ArchivalCandidate",
      "severity": "info",
      "title": "30 days of data ready for archival",
      "description": "Cold tier data from November 2023 meets archival criteria",
      "affected_paths": ["cold/*/2023/11/**"],
      "recommended_actions": [
        "Review archival policy",
        "Run: archive --month 2023-11 --verify"
      ],
      "context": {
        "file_count": 2500,
        "total_size_gb": 150,
        "avg_quality_score": 0.97
      }
    }
  ]
}
```

### 4. Usage Analytics

**Track how data is accessed and used:**

```csharp
record UsageMetrics(
    string Path,
    int ReadCount,
    int QueryCount,
    DateTimeOffset LastAccessed,
    DateTimeOffset[] AccessHistory,
    string[] AccessPatterns,         // "bulk_read", "random_access", "streaming"
    Dictionary<string, int> AccessByUser,
    double HotDataScore              // How frequently accessed (0-1)
);

interface IUsageAnalytics
{
    Task RecordAccessAsync(string path, AccessType type, CancellationToken ct);
    Task<UsageReport> GetUsageReportAsync(TimeSpan window, CancellationToken ct);
    Task<string[]> GetColdDataAsync(TimeSpan threshold, CancellationToken ct);
    Task<string[]> GetHotDataAsync(int topN, CancellationToken ct);
}
```

### 5. Data Lineage Tracking

```csharp
record DataLineage(
    string FileId,
    LineageNode[] Ancestors,
    LineageNode[] Descendants,
    TransformationStep[] Transformations
);

record LineageNode(
    string FileId,
    string Path,
    string Type,                     // "raw", "processed", "consolidated", "archived"
    DateTimeOffset CreatedAt
);

record TransformationStep(
    string Operation,                // "compress", "convert", "merge", "filter"
    DateTimeOffset PerformedAt,
    string[] InputFiles,
    string[] OutputFiles,
    Dictionary<string, string> Parameters
);
```

**Lineage visualization:**
```
raw/alpaca/AAPL/Trade/2024-01-15.jsonl
    │
    ├─[compress]─→ warm/alpaca/AAPL/Trade/2024-01-15.jsonl.gz
    │                  │
    │                  └─[convert]─→ cold/parquet/AAPL/Trade/2024-01.parquet
    │                                    │
    └─[merge]────────────────────────────┴─→ consolidated/AAPL/Trade/2024-01.parquet
                                                  │
                                                  └─[archive]─→ archive/2024/Q1/AAPL_Trade.parquet.zst
```

### 6. Actionable Dashboards

```json
{
  "Dashboard": {
    "sections": [
      {
        "name": "Storage Overview",
        "widgets": [
          {"type": "gauge", "metric": "total_usage_pct", "thresholds": [70, 90]},
          {"type": "trend", "metric": "daily_growth_gb", "window": "30d"},
          {"type": "breakdown", "metric": "usage_by_tier"}
        ]
      },
      {
        "name": "Data Quality",
        "widgets": [
          {"type": "score", "metric": "overall_quality", "target": 0.95},
          {"type": "heatmap", "metric": "quality_by_symbol_date"},
          {"type": "list", "metric": "quality_alerts", "limit": 10}
        ]
      },
      {
        "name": "Insights & Actions",
        "widgets": [
          {"type": "feed", "source": "insights", "filter": "actionable"},
          {"type": "checklist", "source": "pending_maintenance"},
          {"type": "timeline", "source": "scheduled_tasks"}
        ]
      },
      {
        "name": "Search & Discovery",
        "widgets": [
          {"type": "search_bar", "scope": "global"},
          {"type": "facets", "dimensions": ["symbol", "type", "source", "date"]},
          {"type": "recent_searches", "limit": 5}
        ]
      }
    ]
  }
}
```

### 7. Metadata-Driven Automation

```csharp
record AutomationRule(
    string Name,
    MetadataCondition[] Conditions,
    AutomationAction[] Actions,
    bool Enabled,
    string Schedule                  // Cron expression or "realtime"
);

record MetadataCondition(
    string Field,                    // e.g., "QualityScore", "SizeBytes", "Age"
    ConditionOperator Operator,      // Lt, Gt, Eq, Contains, etc.
    object Value
);

record AutomationAction(
    ActionType Type,
    Dictionary<string, string> Parameters
);

enum ActionType
{
    Notify,
    MoveToTier,
    Compress,
    Archive,
    Delete,
    Backfill,
    RunRepair,
    GenerateReport
}
```

**Example automation rules:**

```json
{
  "AutomationRules": [
    {
      "name": "auto_archive_old_high_quality",
      "conditions": [
        {"field": "Age", "operator": "Gt", "value": "365d"},
        {"field": "QualityScore", "operator": "Gte", "value": 0.95},
        {"field": "CurrentTier", "operator": "Eq", "value": "cold"}
      ],
      "actions": [
        {"type": "Archive", "parameters": {"target": "glacier", "verify": "true"}}
      ],
      "schedule": "0 3 1 * *"
    },
    {
      "name": "alert_quality_drop",
      "conditions": [
        {"field": "QualityScore", "operator": "Lt", "value": 0.85}
      ],
      "actions": [
        {"type": "Notify", "parameters": {"channel": "slack", "severity": "warning"}}
      ],
      "schedule": "realtime"
    },
    {
      "name": "auto_backfill_gaps",
      "conditions": [
        {"field": "SequenceGaps", "operator": "Gt", "value": 0},
        {"field": "Age", "operator": "Lt", "value": "7d"}
      ],
      "actions": [
        {"type": "Backfill", "parameters": {"source": "alternate", "max_gaps": "10"}}
      ],
      "schedule": "0 */4 * * *"
    }
  ]
}
```

---

## Implementation Roadmap

### Phase 1: Foundation & Core Infrastructure
- [ ] Implement source registry configuration
- [ ] Add hierarchical naming convention option
- [ ] Create file manifest generation
- [ ] Implement per-source quota tracking
- [ ] Build basic file health check service

### Phase 2: Storage Policies & Lifecycle
- [ ] Define policy configuration schema
- [ ] Implement policy evaluation engine
- [ ] Add compression policy matrix
- [ ] Create backup policy executor
- [ ] Implement scheduled maintenance tasks

### Phase 3: Tiered Storage
- [ ] Define tier configuration schema
- [ ] Implement tier migration service
- [ ] Add Parquet conversion pipeline
- [ ] Create unified query interface
- [ ] Build file compaction service

### Phase 4: Perpetual Storage & Compliance
- [ ] Implement archive tier with WORM support
- [ ] Add data catalog service
- [ ] Create compliance reporting
- [ ] Implement immutability guarantees
- [ ] Build data lineage tracking

### Phase 5: Data Quality & Robustness
- [ ] Implement quality scoring engine
- [ ] Add quality dimension evaluators (completeness, accuracy, etc.)
- [ ] Build best-of-breed data selector
- [ ] Create quality trend monitoring
- [ ] Implement auto-backfill for gaps

### Phase 6: Search & Discovery
- [ ] Build multi-level index architecture
- [ ] Implement file and event search APIs
- [ ] Add faceted search support
- [ ] Create natural language query parser
- [ ] Build real-time index maintenance

### Phase 7: Metadata & Insights
- [ ] Implement rich file metadata schema
- [ ] Build automated insight generator
- [ ] Create usage analytics tracking
- [ ] Implement metadata-driven automation rules
- [ ] Build actionable dashboards

### Phase 8: Self-Healing & Advanced Features
- [ ] Implement self-healing repair capabilities
- [ ] Add orphan detection and cleanup
- [ ] Build cross-source reconciliation
- [ ] Create capacity forecasting
- [ ] Add adaptive partitioning
- [ ] Implement trading calendar integration

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
7. **Maintains** file health with automated checks, self-healing, and defragmentation
8. **Guarantees** data robustness through quality scoring and best-of-breed selection
9. **Discovers** data efficiently with multi-level indexes and faceted search
10. **Provides** actionable insights through rich metadata and automated recommendations

### Key Capabilities Matrix

| Capability | Description | Business Value |
|------------|-------------|----------------|
| **File Maintenance** | Health checks, self-healing, compaction | Reduced manual ops, fewer outages |
| **Quality Scoring** | 6-dimension quality evaluation | Trust in data, better decisions |
| **Best-of-Breed** | Auto-select highest quality source | Always use best available data |
| **Search Infrastructure** | Indexes, faceted search, NL queries | Find any data in seconds |
| **Metadata & Lineage** | Full lifecycle tracking | Audit trail, reproducibility |
| **Automated Insights** | Proactive recommendations | Prevent issues before they occur |
| **Usage Analytics** | Access pattern tracking | Optimize for actual workloads |

The modular design allows incremental adoption—start with basic naming conventions and progressively add policies, tiering, quality management, and advanced search as needs grow.
