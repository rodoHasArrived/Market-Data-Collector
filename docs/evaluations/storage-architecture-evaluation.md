# Storage Architecture Evaluation

## Market Data Collector — Data Persistence Assessment

**Date:** 2026-02-03
**Status:** Evaluation Complete
**Author:** Architecture Review

---

## Executive Summary

This document evaluates the storage architecture of the Market Data Collector system, including file formats, organization strategies, compression, tiered storage, and the Write-Ahead Log (WAL) implementation. The evaluation assesses current design decisions against alternatives and identifies optimization opportunities.

**Key Finding:** The current storage architecture is well-designed for the primary use case of archival-first market data collection. The JSONL + Parquet dual-format approach provides an excellent balance of write performance and query efficiency. The tiered storage with WAL provides good durability guarantees.

---

## A. Current Architecture Overview

### Storage Components

| Component | Location | Purpose |
|-----------|----------|---------|
| JsonlStorageSink | `Storage/Sinks/` | Real-time JSONL file writes |
| ParquetStorageSink | `Storage/Sinks/` | Columnar archive format |
| WriteAheadLog | `Storage/Archival/` | Durability guarantee |
| TierMigrationService | `Storage/Services/` | Hot/warm/cold tier management |
| PortableDataPackager | `Storage/Packaging/` | Data export/import |
| ScheduledArchiveMaintenanceService | `Storage/Maintenance/` | Automated maintenance |

### File Organization Strategies

The system supports four naming conventions:

| Strategy | Pattern | Use Case |
|----------|---------|----------|
| **BySymbol** (default) | `{root}/{symbol}/{type}/{date}.jsonl` | Symbol-centric analysis |
| ByDate | `{root}/{date}/{symbol}/{type}.jsonl` | Date-centric queries |
| ByType | `{root}/{type}/{symbol}/{date}.jsonl` | Type-centric processing |
| Flat | `{root}/{symbol}_{type}_{date}.jsonl` | Simple deployments |

### Directory Structure

```
data/
├── live/                    # Hot tier (real-time)
│   ├── {provider}/
│   │   └── {date}/
│   │       ├── {symbol}_trades.jsonl.gz
│   │       └── {symbol}_quotes.jsonl.gz
├── historical/              # Backfill data
│   └── {provider}/
│       └── {date}/
│           └── {symbol}_bars.jsonl
├── _wal/                    # Write-ahead log
│   ├── trades/
│   └── quotes/
└── _archive/                # Cold tier
    └── parquet/
        └── {symbol}/
            └── {year}/
                └── {type}.parquet
```

---

## B. File Format Evaluation

---

### Format 1: JSONL (JSON Lines)

**Current Usage:** Primary format for real-time data ingestion

**Strengths:**

| Strength | Detail |
|----------|--------|
| Append-only writes | Perfect for streaming data |
| Human readable | Easy debugging and inspection |
| Schema flexible | Handles evolving event schemas |
| Line-oriented | Simple crash recovery (last complete line) |
| Compression friendly | Gzip/LZ4 compress well |
| Universal tooling | Every language can parse JSON |

**Weaknesses:**

| Weakness | Detail |
|----------|--------|
| Query inefficiency | Full scan required for filters |
| Storage overhead | Text format larger than binary |
| Parse overhead | JSON parsing CPU-intensive |
| No columnar access | Cannot read single fields efficiently |
| Type coercion | Numbers as strings, precision issues |

**Performance Characteristics:**

| Metric | Typical Value |
|--------|---------------|
| Write throughput | 100K+ events/second |
| Read throughput | 10-50K events/second |
| Compression ratio (gzip) | 5-10x |
| Compression ratio (lz4) | 3-5x |
| Storage per trade event | ~200-500 bytes (uncompressed) |

**Best For:**
- Real-time data ingestion
- Short-term hot storage
- Data interchange
- Debugging and validation

---

### Format 2: Parquet

**Current Usage:** Archival format for cold storage

**Strengths:**

| Strength | Detail |
|----------|--------|
| Columnar storage | Efficient single-column queries |
| Excellent compression | 10-50x typical for market data |
| Predicate pushdown | Filter at storage layer |
| Schema evolution | Add columns without rewrite |
| Industry standard | Spark, Pandas, DuckDB support |
| Statistics | Min/max/count in metadata |

**Weaknesses:**

| Weakness | Detail |
|----------|--------|
| Write complexity | Not append-friendly |
| Batch oriented | Requires buffering for writes |
| Memory overhead | Needs row group in memory |
| Small file overhead | Metadata dominates small files |
| Not streamable | Must write complete file |

**Performance Characteristics:**

| Metric | Typical Value |
|--------|---------------|
| Write throughput | Batch-dependent |
| Read throughput (full scan) | 1M+ rows/second |
| Read throughput (columnar) | 10M+ values/second |
| Compression ratio | 10-50x |
| Query speedup vs JSONL | 10-100x for filtered queries |

**Best For:**
- Long-term archival
- Analytical queries
- Data sharing/distribution
- Research and backtesting

---

### Format 3: Alternatives Considered

#### Binary Formats

| Format | Pros | Cons | Verdict |
|--------|------|------|---------|
| **Protobuf** | Compact, fast, schema-enforced | Requires schema compilation | Good for internal, not archival |
| **MessagePack** | JSON-like but binary | Less tooling than JSON | Minor benefit for added complexity |
| **FlatBuffers** | Zero-copy access | Complex setup | Overkill for this use case |
| **Avro** | Schema evolution, Hadoop native | Less common outside Hadoop | Consider for Spark integration |

#### Time-Series Databases

| Database | Pros | Cons | Verdict |
|----------|------|------|---------|
| **TimescaleDB** | SQL interface, mature | Postgres dependency | Good alternative, more operational overhead |
| **InfluxDB** | Purpose-built for time-series | Proprietary query language | Vendor lock-in concern |
| **QuestDB** | Excellent performance | Younger ecosystem | Worth evaluating |
| **ClickHouse** | Exceptional query speed | Operational complexity | Consider for large-scale deployment |

**Current Decision Rationale:**

The JSONL + Parquet combination was chosen because:

1. **Simplicity** - File-based storage requires no database operations
2. **Portability** - Files can be copied, shared, backed up trivially
3. **Tooling** - Both formats have universal tool support
4. **Separation of concerns** - Write-optimized (JSONL) vs read-optimized (Parquet)
5. **Cost** - No database licensing or operational costs

---

## C. Compression Evaluation

### Current Compression Profiles

| Profile | Algorithm | Level | Use Case |
|---------|-----------|-------|----------|
| RealTime | LZ4 | Default | Live streaming data |
| Standard | Gzip | 6 | General purpose |
| Archive | ZSTD | 19 | Long-term storage |

### Compression Comparison (Market Data)

| Algorithm | Ratio | Compress Speed | Decompress Speed | CPU Usage |
|-----------|-------|----------------|------------------|-----------|
| None | 1x | N/A | N/A | None |
| LZ4 | 3-5x | 500 MB/s | 1500 MB/s | Very Low |
| Gzip-6 | 5-10x | 50 MB/s | 200 MB/s | Medium |
| Gzip-9 | 6-12x | 20 MB/s | 200 MB/s | High |
| ZSTD-3 | 6-10x | 200 MB/s | 500 MB/s | Low |
| ZSTD-19 | 8-15x | 10 MB/s | 500 MB/s | Very High |
| Brotli-11 | 10-18x | 5 MB/s | 300 MB/s | Very High |

### Recommendations

| Tier | Recommended | Rationale |
|------|-------------|-----------|
| Hot (real-time) | LZ4 | Minimal latency impact |
| Warm (recent) | ZSTD-3 | Good balance |
| Cold (archive) | ZSTD-19 | Maximum compression |

**Current Implementation Assessment:** The compression profiles are well-chosen. LZ4 for real-time is correct; ZSTD-19 for archive is optimal.

---

## D. Tiered Storage Evaluation

### Current Tier Configuration

| Tier | Purpose | Default Retention | Compression | Location |
|------|---------|-------------------|-------------|----------|
| Hot | Real-time access | 7 days | LZ4 | `data/live/` |
| Warm | Recent history | 30 days | Gzip | `data/live/` (older) |
| Cold | Long-term archive | Indefinite | ZSTD-19/Parquet | `data/_archive/` |

### Tier Migration Process

```
1. Data arrives → Hot tier (JSONL + LZ4)
   ↓ (after 7 days)
2. TierMigrationService → Warm tier (recompress to Gzip)
   ↓ (after 30 days)
3. TierMigrationService → Cold tier (convert to Parquet + ZSTD)
```

### Evaluation

**Strengths:**

| Strength | Detail |
|----------|--------|
| Automatic lifecycle | No manual intervention needed |
| Cost optimization | Cold tier highly compressed |
| Query optimization | Parquet enables fast analysis |
| Configurable | Retention periods adjustable |
| Scheduled | Runs during off-hours |

**Weaknesses:**

| Weakness | Detail |
|----------|--------|
| Migration overhead | CPU spike during conversion |
| Storage spike | Temporary 2x storage during migration |
| No cloud tiering | Local storage only (no S3/Azure Blob) |
| Single-threaded | Migration could be parallelized |

**Recommendations:**

1. **Add cloud tier support** - S3/Azure Blob for cold storage would reduce local storage costs
2. **Parallelize migration** - Process multiple symbols concurrently
3. **Add migration scheduling** - Run during market closed hours
4. **Implement incremental migration** - Migrate daily instead of batch

---

## E. Write-Ahead Log (WAL) Evaluation

### Current Implementation

Location: `Storage/Archival/WriteAheadLog.cs`

**Purpose:** Ensure data durability by writing to WAL before primary storage

**Process:**
```
1. Event received
2. Write to WAL (fsync)
3. Acknowledge receipt
4. Write to primary storage (async)
5. Mark WAL entry as committed
6. Periodic WAL cleanup
```

### Evaluation

**Strengths:**

| Strength | Detail |
|----------|--------|
| Data durability | Survives process crash |
| Recovery support | Replay uncommitted entries on startup |
| Ordered writes | Maintains event sequence |
| Configurable sync | Trade durability vs performance |

**Weaknesses:**

| Weakness | Detail |
|----------|--------|
| Write amplification | 2x writes (WAL + primary) |
| Disk I/O increase | fsync on every batch |
| Recovery complexity | Must handle partial writes |
| No replication | Single-node durability only |

**Performance Impact:**

| Configuration | Throughput | Durability |
|---------------|------------|------------|
| fsync every event | 10K events/sec | Maximum |
| fsync every 100 events | 50K events/sec | High |
| fsync every second | 100K+ events/sec | Moderate |
| No fsync (OS buffered) | 200K+ events/sec | Low |

**Current Setting Assessment:** The default batch fsync (every 100 events) provides a good balance. For mission-critical deployments, per-event fsync is available.

### Alternatives Considered

| Alternative | Pros | Cons | Verdict |
|-------------|------|------|---------|
| SQLite WAL | Proven, ACID | Overhead for simple append | Overkill |
| Memory-mapped files | Fast | Crash recovery complex | Too risky |
| Kafka | Distributed, replicated | Operational complexity | Consider for scale |
| No WAL | Simpler, faster | Data loss on crash | Unacceptable |

**Recommendation:** Current WAL implementation is appropriate. For high-availability requirements, consider adding replication to secondary node.

---

## F. Storage Capacity Planning

### Typical Data Volumes

| Data Type | Size per Event | Events per Day (active symbol) | Daily Size |
|-----------|----------------|--------------------------------|------------|
| Trade | 150-300 bytes | 50,000-500,000 | 15-150 MB |
| Quote | 200-400 bytes | 100,000-1,000,000 | 40-400 MB |
| L2 Depth | 500-2000 bytes | 10,000-100,000 | 10-200 MB |
| Daily Bar | 100-200 bytes | 1 | ~150 bytes |

### Storage Projections (100 symbols)

| Timeframe | Raw JSONL | Compressed (ZSTD) | Parquet |
|-----------|-----------|-------------------|---------|
| 1 day | 5-50 GB | 0.5-5 GB | 0.3-3 GB |
| 1 month | 150-1500 GB | 15-150 GB | 9-90 GB |
| 1 year | 1.8-18 TB | 180-1800 GB | 108-1080 GB |

### Recommendations

1. **Plan for 10-20 GB/day** compressed for active trading
2. **Cold storage reduces** this by 60-70% via Parquet conversion
3. **Consider cloud tiering** for data older than 90 days
4. **Implement retention policies** to auto-delete if storage constrained

---

## G. Query Performance Evaluation

### Current Query Patterns

| Query Type | Format | Performance |
|------------|--------|-------------|
| Recent data (< 7 days) | JSONL + LZ4 | Sequential scan, fast for small ranges |
| Historical analysis | Parquet | Columnar scan, excellent for aggregations |
| Specific event lookup | JSONL | Sequential scan, slow |
| Cross-symbol analysis | Parquet | Good with predicate pushdown |

### Performance Benchmarks (Typical)

| Query | JSONL Time | Parquet Time | Improvement |
|-------|------------|--------------|-------------|
| Full day scan (1 symbol) | 2-5 seconds | 0.2-0.5 seconds | 10x |
| VWAP calculation | 5-10 seconds | 0.5-1 second | 10x |
| Price filter (< threshold) | 10-30 seconds | 0.1-0.3 seconds | 100x |
| Multi-symbol aggregation | Minutes | Seconds | 50-100x |

### Query Optimization Recommendations

1. **Add indexing for JSONL** - Consider SQLite index files for hot data
2. **Partition Parquet by date** - Enable date-range pruning
3. **Add bloom filters** - For symbol/type filtering in Parquet
4. **Consider DuckDB** - For ad-hoc analytical queries

---

## H. Data Integrity Evaluation

### Current Integrity Measures

| Measure | Implementation | Effectiveness |
|---------|----------------|---------------|
| WAL | Write-ahead logging | High |
| Checksums | Per-file checksums | Medium |
| Sequence validation | Gap detection | High |
| Schema validation | JSON schema checks | Medium |

### Recommendations

1. **Add end-to-end checksums** - Hash chains for tamper detection
2. **Implement repair tools** - Automated gap filling from backfill
3. **Add data validation** - Price/volume sanity checks
4. **Consider versioning** - Immutable storage with version history

---

## I. Comparative Analysis: File-Based vs Database

### File-Based (Current Approach)

**Advantages:**

| Advantage | Detail |
|-----------|--------|
| Simplicity | No database to operate |
| Portability | Copy files to share data |
| Cost | No licensing fees |
| Backup | Standard file backup tools |
| Performance | Excellent write throughput |

**Disadvantages:**

| Disadvantage | Detail |
|--------------|--------|
| Query flexibility | Limited ad-hoc queries |
| Indexing | Must build custom indexes |
| Transactions | No ACID guarantees (except WAL) |
| Concurrency | File locking complexity |

### Time-Series Database Alternative

**Advantages:**

| Advantage | Detail |
|-----------|--------|
| Query flexibility | SQL or specialized query language |
| Indexing | Automatic time-based indexing |
| Aggregation | Built-in downsampling |
| Concurrency | Handle multiple readers/writers |

**Disadvantages:**

| Disadvantage | Detail |
|--------------|--------|
| Operational overhead | Database administration |
| Cost | Licensing and infrastructure |
| Vendor lock-in | Data format proprietary |
| Learning curve | Query language, tuning |

### Verdict

The file-based approach is appropriate for Market Data Collector because:

1. **Primary use case is archival** - Write-heavy, read-occasional
2. **Portability matters** - Data sharing and backup are simple
3. **Team size** - No dedicated DBA required
4. **Query patterns** - Mostly sequential scans or batch analysis
5. **Cost sensitivity** - No database licensing costs

**Consider database if:**
- Real-time analytical queries become primary use case
- Multi-user concurrent access is required
- Sub-second query latency is critical
- Data volume exceeds local storage capacity

---

## J. Summary Recommendations

### Retain Current Architecture

The storage architecture is well-designed. Retain:

1. **JSONL for hot tier** - Optimal for streaming ingestion
2. **Parquet for cold tier** - Optimal for analysis
3. **WAL for durability** - Appropriate for data integrity
4. **Tiered storage** - Good cost/performance balance
5. **BySymbol organization** - Matches primary access patterns

### Recommended Improvements

| Priority | Improvement | Benefit |
|----------|-------------|---------|
| High | Add cloud storage tier (S3/Azure Blob) | Reduce local storage costs |
| High | Parallelize tier migration | Reduce maintenance window |
| Medium | Add SQLite index for hot data | Improve recent data queries |
| Medium | Implement retention policy automation | Prevent storage exhaustion |
| Low | Add DuckDB integration | Ad-hoc analytical queries |
| Low | Add data validation pipeline | Catch quality issues early |

### Architecture Evolution Path

```
Current State
    ├── Add cloud cold tier (S3/Azure Blob)
    ├── Add query layer (DuckDB for analytics)
    └── Add replication (for high availability)
        ↓
Future State: Hybrid file + cloud + optional database
```

---

## Key Insight

The storage architecture follows the principle of **separation of concerns**: write-optimized format (JSONL) for ingestion, read-optimized format (Parquet) for analysis, with tiered storage managing the lifecycle.

This is the correct architecture for an archival-first market data system. The primary investment should be in:

1. **Cloud tiering** - For cost optimization at scale
2. **Query optimization** - For research and backtesting workflows
3. **Monitoring** - For storage capacity and integrity tracking

Major architectural changes (e.g., time-series database) are not justified given current requirements.

---

*Evaluation Date: 2026-02-03*
