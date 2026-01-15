# Crystallized Storage Format Specification

Version: 1.0
Status: Stable
Last Updated: 2026-01-09

## Overview

The Crystallized Storage Format is a standardized, intuitive data organization system designed for market data. It supports multiple data providers, various data types (bars, trades, quotes, order books), and different time granularities while remaining accessible to both casual Excel users and advanced ML practitioners.

### Design Goals

1. **Provider-agnostic**: Consistent structure regardless of data source
2. **Time granularity support**: From tick data to monthly bars
3. **Self-documenting**: File names and manifests explain the contents
4. **Excel-friendly**: CSV export with standard columns
5. **ML-optimized**: Efficient bulk access with consistent schemas
6. **Discoverable**: Catalog and manifest files for automated exploration

---

## Directory Structure

```
data/
├── _catalog.json                              # Root catalog (all symbols/providers)
├── {provider}/                                # Data source (alpaca, polygon, yahoo, etc.)
│   └── {symbol}/                              # Trading symbol (AAPL, SPY, etc.)
│       ├── _manifest.json                     # Symbol metadata and data summary
│       ├── bars/                              # OHLCV price bars
│       │   ├── tick/                          # Tick bars (rare)
│       │   ├── 1s/                            # 1-second bars
│       │   ├── 5s/                            # 5-second bars
│       │   ├── 1m/                            # 1-minute bars
│       │   ├── 5m/                            # 5-minute bars
│       │   ├── 15m/                           # 15-minute bars
│       │   ├── 30m/                           # 30-minute bars
│       │   ├── 1h/                            # 1-hour bars
│       │   ├── 4h/                            # 4-hour bars
│       │   ├── daily/                         # Daily bars (end-of-day)
│       │   ├── weekly/                        # Weekly bars
│       │   └── monthly/                       # Monthly bars
│       ├── trades/                            # Tick-by-tick trade prints
│       ├── quotes/                            # Best bid/offer snapshots
│       ├── orderbook/                         # Level 2 order book
│       ├── orderflow/                         # Pre-computed order flow stats
│       │   ├── 1m/
│       │   ├── 5m/
│       │   └── ...
│       ├── auctions/                          # Opening/closing auction data
│       └── corporate_actions/                 # Dividends, splits, etc.
└── _system/                                   # System events (global)
    └── events_{date}.jsonl
```

---

## Time Granularities

| Granularity | File Suffix | Use Case | Typical Partition |
|-------------|-------------|----------|-------------------|
| Tick | `tick` | Market microstructure, precise fills | Hourly |
| 1 Second | `1s` | High-frequency analysis | Hourly |
| 5 Seconds | `5s` | Scalping strategies | Hourly |
| 15 Seconds | `15s` | Short-term momentum | Hourly |
| 30 Seconds | `30s` | Short-term momentum | Hourly |
| 1 Minute | `1m` | Intraday trading | Daily |
| 5 Minutes | `5m` | Day trading | Daily |
| 15 Minutes | `15m` | Swing trading | Daily |
| 30 Minutes | `30m` | Swing trading | Daily |
| 1 Hour | `1h` | Position trading | Daily |
| 4 Hours | `4h` | Multi-day positions | Daily |
| Daily | `daily` | End-of-day analysis | Monthly |
| Weekly | `weekly` | Long-term trends | Single file |
| Monthly | `monthly` | Macro analysis | Single file |

---

## Data Categories

### Bars (OHLCV)
OHLCV price bars aggregated at various time intervals.

**Columns:**
| Column | Type | Description |
|--------|------|-------------|
| timestamp | datetime | Bar open time (UTC) |
| open | decimal | Opening price |
| high | decimal | Highest price |
| low | decimal | Lowest price |
| close | decimal | Closing price |
| volume | long | Total volume |
| vwap | decimal? | Volume-weighted average price |
| trades_count | int? | Number of trades in bar |

**Path Pattern:** `{root}/{provider}/{symbol}/bars/{granularity}/{date}.{ext}`

**Example Files:**
- `data/alpaca/AAPL/bars/daily/2024-01.jsonl`
- `data/polygon/SPY/bars/1m/2024-01-15.jsonl`

---

### Trades
Individual trade executions (tick data).

**Columns:**
| Column | Type | Description |
|--------|------|-------------|
| timestamp | datetime | Trade time (UTC, microsecond precision) |
| price | decimal | Execution price |
| size | long | Trade size |
| side | string | Aggressor side: "buy", "sell", "unknown" |
| sequence | long | Sequence number |
| venue | string? | Execution venue/exchange |
| conditions | string? | Trade condition codes |

**Path Pattern:** `{root}/{provider}/{symbol}/trades/{date}.{ext}`

**Example:** `data/alpaca/AAPL/trades/2024-01-15.jsonl`

---

### Quotes (BBO)
Best bid/offer snapshots.

**Columns:**
| Column | Type | Description |
|--------|------|-------------|
| timestamp | datetime | Quote time (UTC) |
| bid_price | decimal | Best bid price |
| bid_size | long | Size at best bid |
| ask_price | decimal | Best ask price |
| ask_size | long | Size at best ask |
| spread | decimal | Ask - Bid |
| mid_price | decimal | (Bid + Ask) / 2 |
| sequence | long | Sequence number |

**Path Pattern:** `{root}/{provider}/{symbol}/quotes/{date}.{ext}`

---

### Order Book (Level 2)
Full order book with multiple price levels.

**Columns:**
| Column | Type | Description |
|--------|------|-------------|
| timestamp | datetime | Snapshot time (UTC) |
| level | int | Price level (0 = best) |
| bid_price | decimal | Bid price at level |
| bid_size | long | Size at bid level |
| ask_price | decimal | Ask price at level |
| ask_size | long | Size at ask level |
| sequence | long | Sequence number |

**Path Pattern:** `{root}/{provider}/{symbol}/orderbook/{date}.{ext}`

---

### Order Flow
Pre-computed order flow statistics.

**Columns:**
| Column | Type | Description |
|--------|------|-------------|
| timestamp | datetime | Period timestamp |
| imbalance | decimal | Order flow imbalance |
| vwap | decimal | Volume-weighted average price |
| buy_volume | long | Buy-side volume |
| sell_volume | long | Sell-side volume |
| total_volume | long | Total volume |
| sequence | long | Sequence number |

**Path Pattern:** `{root}/{provider}/{symbol}/orderflow/{granularity}/{date}.{ext}`

---

## File Naming Conventions

### Standard Naming (within directory structure)
Files are named by their date partition:
```
2024-01-15.jsonl      # Daily partition
2024-01-15_14.jsonl   # Hourly partition (14:00 UTC)
2024-01.jsonl         # Monthly partition
all.jsonl             # No partition (weekly/monthly bars)
```

### Self-Documenting Naming (portable)
For files that may be moved or shared, the full context is embedded:
```
AAPL_alpaca_bars_daily_2024-01-15.csv
SPY_polygon_trades_2024-01-15.jsonl.gz
MSFT_yahoo_bars_1h_2024-01-15.jsonl
```

Format: `{symbol}_{provider}_{category}_{granularity}_{date}.{ext}`

---

## File Formats

### JSONL (JSON Lines)
- One JSON object per line
- Best for: Streaming writes, flexible schema, nested data
- Extension: `.jsonl` (compressed: `.jsonl.gz`, `.jsonl.zst`, `.jsonl.lz4`)

**Example (bar):**
```json
{"timestamp":"2024-01-15T09:30:00Z","open":185.50,"high":186.20,"low":185.40,"close":186.00,"volume":1250000}
{"timestamp":"2024-01-15T09:31:00Z","open":186.00,"high":186.50,"low":185.90,"close":186.30,"volume":890000}
```

### CSV
- Standard comma-separated values
- Best for: Excel, simple analysis, pandas
- Extension: `.csv` (compressed: `.csv.gz`)

**Example (bar):**
```csv
timestamp,open,high,low,close,volume
2024-01-15,185.50,186.20,185.40,186.00,1250000
2024-01-16,186.00,187.10,185.80,186.90,1100000
```

### Parquet
- Columnar format for analytics
- Best for: ML workloads, large datasets, cloud storage
- Extension: `.parquet`

---

## Manifest Files

### Symbol Manifest (`_manifest.json`)
Located in each symbol directory. Describes available data.

```json
{
  "schema_version": 1,
  "symbol": "AAPL",
  "provider": "alpaca",
  "description": "Apple Inc.",
  "asset_class": "equity",
  "exchange": "NASDAQ",
  "currency": "USD",
  "categories": {
    "bars": {
      "display_name": "Price Bars (OHLCV)",
      "granularities": ["1m", "5m", "15m", "1h", "daily"],
      "earliest_date": "2020-01-02",
      "latest_date": "2024-01-15",
      "file_count": 1248,
      "total_bytes": 524288000,
      "columns": ["timestamp", "open", "high", "low", "close", "volume", "vwap", "trades_count"]
    },
    "trades": {
      "display_name": "Trade Prints",
      "earliest_date": "2024-01-02",
      "latest_date": "2024-01-15",
      "file_count": 10,
      "total_bytes": 2147483648,
      "row_count": 45000000,
      "columns": ["timestamp", "price", "size", "side", "sequence", "venue", "conditions"]
    }
  },
  "earliest_date": "2020-01-02",
  "latest_date": "2024-01-15",
  "total_files": 1258,
  "total_bytes": 2671771648,
  "updated_at": "2024-01-15T23:59:59Z"
}
```

### Root Catalog (`_catalog.json`)
Located in the root data directory. Index of all data.

```json
{
  "schema_version": 1,
  "title": "Market Data Collection",
  "description": "Historical market data for US equities",
  "providers": [
    {
      "name": "alpaca",
      "display_name": "Alpaca Markets",
      "symbol_count": 150,
      "categories": ["bars", "trades", "quotes"]
    },
    {
      "name": "yahoo",
      "display_name": "Yahoo Finance",
      "symbol_count": 500,
      "categories": ["bars"]
    }
  ],
  "symbols": [
    {
      "symbol": "AAPL",
      "provider": "alpaca",
      "asset_class": "equity",
      "categories": ["bars", "trades", "quotes"],
      "earliest_date": "2020-01-02",
      "latest_date": "2024-01-15",
      "manifest_path": "alpaca/AAPL/_manifest.json"
    }
  ],
  "date_range": {
    "earliest": "2020-01-02",
    "latest": "2024-01-15",
    "trading_days": 1005
  },
  "storage": {
    "total_files": 15000,
    "total_bytes": 107374182400,
    "total_bytes_human": "100 GB"
  },
  "updated_at": "2024-01-15T23:59:59Z",
  "format": {
    "version": "1.0",
    "file_format": "jsonl",
    "compression": "gzip",
    "self_documenting_names": true
  }
}
```

---

## Usage Examples

### Excel Users

1. **Finding data:**
   - Open `_catalog.json` to see available symbols
   - Navigate to `data/{provider}/{symbol}/_manifest.json` for details

2. **Opening daily bars:**
   ```
   data/yahoo/AAPL/bars/daily/2024-01.csv
   ```
   - Double-click to open in Excel
   - Columns: date, open, high, low, close, volume

3. **Combining files:**
   - Use Excel's Power Query to combine monthly files
   - Or use the CSV exporter to create a single file

### Python/Pandas Users

```python
import pandas as pd
from pathlib import Path

# Read daily bars
bars = pd.read_json(
    'data/alpaca/AAPL/bars/daily/2024-01.jsonl',
    lines=True
)

# Read all daily bars for a symbol
files = Path('data/alpaca/AAPL/bars/daily').glob('*.jsonl')
bars = pd.concat([pd.read_json(f, lines=True) for f in files])

# Read trades
trades = pd.read_json(
    'data/alpaca/AAPL/trades/2024-01-15.jsonl.gz',
    lines=True,
    compression='gzip'
)
```

### Machine Learning Pipeline

```python
import json
from pathlib import Path

# Discover available data
with open('data/_catalog.json') as f:
    catalog = json.load(f)

# Get all symbols with daily bars
symbols_with_bars = [
    s['symbol'] for s in catalog['symbols']
    if 'bars' in s['categories']
]

# Build feature matrix from multiple granularities
granularities = ['1m', '5m', '15m', '1h', 'daily']
for gran in granularities:
    path = Path(f'data/alpaca/AAPL/bars/{gran}')
    if path.exists():
        # Process files...
        pass
```

---

## Configuration Options

### For Excel Users
```csharp
var options = CrystallizedStorageOptions.ForExcel();
// - CSV format
// - Self-documenting file names
// - No compression
// - Manifests enabled
```

### For Machine Learning
```csharp
var options = CrystallizedStorageOptions.ForMachineLearning();
// - JSONL format
// - Short file names (rely on directory structure)
// - ZSTD compression
// - Manifests enabled
```

### For Real-Time Collection
```csharp
var options = CrystallizedStorageOptions.ForRealTimeCollection();
// - JSONL format
// - Short file names
// - LZ4 compression (fast)
// - Manifests disabled (performance)
```

### For Archival
```csharp
var options = CrystallizedStorageOptions.ForArchival();
// - JSONL format
// - Self-documenting file names
// - ZSTD compression (best ratio)
// - Manifests enabled
```

---

## Migration from Legacy Formats

### From Flat Structure
```bash
# Old: data/AAPL_Trade_2024-01-15.jsonl
# New: data/{provider}/AAPL/trades/2024-01-15.jsonl
```

### From BySymbol Structure
```bash
# Old: data/AAPL/Trade/2024-01-15.jsonl
# New: data/{provider}/AAPL/trades/2024-01-15.jsonl
```

The main changes:
1. Provider is now first-level directory
2. Category names are lowercase
3. Granularity subfolder for bars

---

## Best Practices

1. **Always include provider**: Different providers may have different data quality
2. **Use appropriate granularity**: Don't store tick data if you only need daily bars
3. **Compress older data**: Use ZSTD for archived data, LZ4 for recent
4. **Update manifests**: Run manifest scan after bulk imports
5. **Validate on read**: Check schema_version in manifests for compatibility
6. **Use date partitions**: Easier to manage retention and backups

---

## Schema Versioning

The format uses schema versioning for forward compatibility:

- `schema_version: 1` - Current stable version
- Files with unknown versions should be readable but may have extra fields
- Breaking changes require major version bump

---

## Related Documentation

- [Storage Architecture Overview](./storage-architecture.md)
- [Data Providers Guide](../providers/backfill-guide.md)
- [AI Assistant Storage Guide](../ai-assistants/CLAUDE.storage.md)
- [API Reference: CrystallizedStorageFormat](../api/storage.md)
