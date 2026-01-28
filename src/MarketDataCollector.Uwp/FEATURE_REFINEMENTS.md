# UWP Desktop App - Feature Refinements

This document outlines proposed feature refinements to enhance the Market Data Collector UWP Desktop Application. Refinements are organized by priority and category.

> **Primary Focus: Data Collection & Archival**
>
> The Market Data Collector is designed primarily for **offline data storage, collection, and archival**. Analysis of collected data is intended to be performed externally using specialized tools (Python, R, QuantConnect Lean, etc.). This focus shapes our feature priorities:
>
> - **Collection Excellence**: Reliable, gap-free data capture from multiple sources
> - **Archival Integrity**: Long-term storage with verification, checksums, and format preservation
> - **Export Flexibility**: Easy extraction in formats suitable for external analysis tools
> - **Storage Efficiency**: Optimal compression, tiering, and organization for archival workloads
>
> While the system maintains flexibility for future integration with cloud storage, real-time streaming, and online analysis platforms, the current priority is building a robust, self-contained offline data archive.

---

## Table of Contents

1. [High Priority Refinements](#high-priority-refinements)
2. [Offline Storage & Archival Excellence](#offline-storage--archival-excellence)
3. [Dashboard Enhancements](#dashboard-enhancements)
4. [Provider Management Improvements](#provider-management-improvements)
5. [Symbol Management Refinements](#symbol-management-refinements)
6. [Backfill & Historical Data](#backfill--historical-data)
7. [Storage & Performance](#storage--performance)
8. [UI/UX Improvements](#uiux-improvements)
9. [Security Enhancements](#security-enhancements)
10. [Integration & Automation](#integration--automation)
11. [Accessibility & Localization](#accessibility--localization)
12. [External Analysis Preparation](#external-analysis-preparation)

---

## High Priority Refinements

### 1. Real-Time Notification System
**Current State**: Status updates require manual refresh or polling.

**Refinement**:
- Implement toast notifications for critical events (connection loss, data gaps, backfill completion)
- Add system tray integration with notification badges
- Support Windows Action Center integration
- Allow users to configure notification preferences per event type

**Implementation Notes**:
```csharp
// Use Microsoft.Toolkit.Uwp.Notifications
new ToastContentBuilder()
    .AddText("Connection Lost")
    .AddText("Interactive Brokers connection dropped. Failover activated.")
    .Show();
```

### 2. Data Integrity Alerts Dashboard Widget
**Current State**: Integrity events shown as a simple counter.

**Refinement**:
- Add expandable panel showing last 10 integrity events with details
- Color-coded severity (warning/critical)
- One-click navigation to affected symbol configuration
- Export integrity report functionality

### 3. Auto-Reconnection with Exponential Backoff
**Current State**: Manual reconnection required after failures.

**Refinement**:
- Implement automatic reconnection with configurable max attempts
- Exponential backoff strategy (2s, 4s, 8s, 16s, max 5 minutes)
- Visual indicator showing reconnection attempts
- Option to pause auto-reconnection for maintenance

---

## Offline Storage & Archival Excellence

> **Core Philosophy**: Build a reliable, self-contained data archive that serves as the authoritative source for all collected market data. External tools perform analysis; this system ensures data is collected, preserved, and accessible.

### 55. Archival-First Storage Pipeline
**Current State**: Storage focuses on real-time collection with basic retention.

**Refinement**:
- Implement write-ahead logging (WAL) for crash-safe data persistence
- Add transactional file writes with atomic commit/rollback
- Create immediate on-disk persistence with configurable sync intervals
- Implement file-level checksums computed during write for instant verification
- Add pre-flight storage validation before starting collection sessions

**Implementation Notes**:
```csharp
record ArchivalWriteConfig(
    bool EnableWriteAheadLog,
    TimeSpan SyncInterval,           // How often to fsync (default: 5s)
    bool ComputeChecksumOnWrite,
    bool ValidateAfterWrite,
    int WriteBufferSizeKb            // Buffer before flush (default: 64KB)
);
```

### 56. Offline Data Catalog & Manifest System
**Current State**: Files organized by convention but no unified catalog.

**Refinement**:
- Generate comprehensive manifest files for each collection session
- Include file paths, checksums, event counts, date ranges, and quality scores
- Support manifest verification for archive integrity audits
- Enable manifest-based data discovery without scanning all files
- Create human-readable summary reports alongside machine-readable JSON manifests

**Manifest Structure**:
```json
{
  "manifest_version": "1.0",
  "collection_session": "2026-01-03T09:30:00Z",
  "total_files": 150,
  "total_events": 5000000,
  "total_bytes_raw": 2147483648,
  "total_bytes_compressed": 268435456,
  "symbols": ["AAPL", "MSFT", "SPY"],
  "date_range": { "start": "2026-01-03", "end": "2026-01-03" },
  "files": [
    {
      "path": "AAPL/Trade/2026-01-03.jsonl.gz",
      "checksum_sha256": "abc123...",
      "event_count": 50000,
      "first_timestamp": "2026-01-03T09:30:00.001Z",
      "last_timestamp": "2026-01-03T16:00:00.999Z"
    }
  ]
}
```

### 57. Archive Verification & Integrity Dashboard
**Current State**: No dedicated archive verification UI.

**Refinement**:
- Create dedicated "Archive Health" dashboard page
- Show verification status for all archived data (verified, pending, failed)
- Display checksum validation results with last-verified timestamps
- Enable on-demand full archive verification with progress tracking
- Alert on any integrity issues with one-click repair options
- Generate archive integrity reports for compliance documentation

**UI Components**:
- Archive health score gauge (0-100%)
- Files verified vs. pending vs. failed breakdown
- Last full verification date and duration
- Scheduled verification configuration
- Verification log with issue details

### 58. Long-Term Format Preservation
**Current State**: JSONL and Parquet formats supported.

**Refinement**:
- Add format migration tools for future-proofing (JSONL v1 → v2, etc.)
- Implement schema versioning with backward compatibility
- Create format conversion pipelines (JSONL → Parquet → Arrow)
- Document data schemas alongside archived files
- Include self-describing metadata in all archive files
- Support format validation against published schemas

**Schema Registry**:
```json
{
  "schemas": {
    "Trade_v1": {
      "version": "1.0.0",
      "introduced": "2025-01-01",
      "deprecated": null,
      "fields": ["Timestamp", "Symbol", "Price", "Size", "Side", "Exchange"]
    },
    "Trade_v2": {
      "version": "2.0.0",
      "introduced": "2026-01-01",
      "deprecated": null,
      "fields": ["Timestamp", "Symbol", "Price", "Size", "Side", "Exchange", "TradeId", "Conditions"],
      "migration_from_v1": "automatic"
    }
  }
}
```

### 59. Portable Archive Packages
**Current State**: Data stored in directory hierarchies.

**Refinement**:
- Create self-contained archive packages for data portability
- Include manifest, schemas, and all related files in single archive
- Support multiple package formats (ZIP, TAR.GZ, 7Z)
- Add package verification tool for integrity checking after transfer
- Include embedded viewer/documentation for package contents
- Enable selective packaging (by symbol, date range, event type)

**Package Structure**:
```
MarketData_2026-01.tar.gz
├── manifest.json
├── schemas/
│   ├── Trade_v2.json
│   └── Quote_v1.json
├── data/
│   ├── AAPL/
│   ├── MSFT/
│   └── SPY/
├── checksums.sha256
└── README.txt
```

### 60. Offline-First Collection Mode
**Current State**: Assumes online connectivity for provider APIs.

**Refinement**:
- Implement robust offline queueing for delayed writes
- Add local event buffering with configurable capacity
- Create seamless resume after connectivity restoration
- Implement local time synchronization verification
- Add clock drift detection and correction logging
- Support fully disconnected operation with manual data import

**Offline Queue Configuration**:
```json
{
  "OfflineMode": {
    "max_buffer_size_mb": 1024,
    "flush_on_reconnect": true,
    "preserve_order": true,
    "timestamp_tolerance_ms": 100,
    "clock_sync_check_interval_min": 15
  }
}
```

### 61. Archive Browsing & Inspection Tools
**Current State**: File-system based navigation only.

**Refinement**:
- Create in-app archive browser with tree navigation
- Support quick preview of file contents without full load
- Enable file metadata inspection (checksums, event counts, date ranges)
- Add search within archived files by timestamp or sequence
- Implement file comparison tool for detecting duplicates or changes
- Support direct file export from archive browser

**Browser Features**:
- Tree view: Year → Month → Day → Symbol → Type
- File preview with first/last 100 events
- Metadata panel with statistics
- Right-click context menu for export, verify, repair
- Search bar with date range picker

### 62. Data Completeness Dashboard
**Current State**: Basic gap detection in quality reports.

**Refinement**:
- Create dedicated completeness monitoring page
- Visual calendar heatmap showing data coverage by date/symbol
- Expected vs. actual data volume comparisons
- Trading calendar integration for accurate gap identification
- Completeness score by symbol, date, event type
- One-click backfill for identified gaps

**Completeness Metrics**:
- Trading days with data: 252/252 (100%)
- Expected events vs. received events ratio
- Pre-market/regular/after-hours coverage breakdown
- Gap summary with duration and estimated missing events

### 63. Archive Storage Optimization Advisor
**Current State**: Basic compression and tiering configuration.

**Refinement**:
- Analyze current archive for optimization opportunities
- Recommend compression changes based on data characteristics
- Identify duplicate or redundant files
- Suggest tiering adjustments based on access patterns
- Calculate storage savings from recommended actions
- Provide one-click optimization execution

**Advisor Output**:
```
Storage Optimization Report - 2026-01-03
─────────────────────────────────────────
Current Usage: 150 GB
Recommended Actions:
  1. Compress 45 warm-tier files with zstd → Save 12 GB
  2. Remove 3 duplicate files → Save 0.5 GB
  3. Merge 120 small files (< 1MB) → Improve access performance
  4. Move 30 files to cold tier → Reduce SSD usage by 8 GB

Projected Usage After Optimization: 129.5 GB
Estimated Time to Complete: 45 minutes
```

### 64. Scheduled Archive Maintenance
**Current State**: Manual maintenance tasks.

**Refinement**:
- Create maintenance scheduler with calendar integration
- Configure recurring tasks: verification, optimization, cleanup
- Set maintenance windows to avoid collection conflicts
- Generate maintenance reports with actions taken
- Support maintenance dry-run mode for preview
- Alert on maintenance failures or blocked tasks

**Maintenance Schedule Example**:
```json
{
  "MaintenanceTasks": [
    {
      "name": "Daily Verification",
      "schedule": "0 3 * * *",
      "action": "verify_recent",
      "scope": "last_7_days"
    },
    {
      "name": "Weekly Optimization",
      "schedule": "0 4 * * 0",
      "action": "optimize_storage",
      "scope": "warm_tier"
    },
    {
      "name": "Monthly Full Audit",
      "schedule": "0 2 1 * *",
      "action": "full_archive_verification",
      "scope": "all"
    }
  ]
}
```

### 65. Collection Session Management
**Current State**: Continuous collection without session boundaries.

**Refinement**:
- Implement explicit collection sessions with start/end timestamps
- Create session summary reports upon completion
- Track per-session statistics (events, bytes, symbols, gaps)
- Enable session-based data export and verification
- Support session tagging for organization (e.g., "Q1-2026", "Earnings-Week")
- Allow session replay configuration for testing

**Session Summary**:
```
Collection Session: 2026-01-03-regular-hours
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Started: 2026-01-03 09:30:00 ET
Ended: 2026-01-03 16:00:00 ET
Duration: 6h 30m 00s

Symbols Collected: 50
Total Events: 12,500,000
  - Trades: 8,000,000
  - Quotes: 4,000,000
  - L2 Snapshots: 500,000

Data Volume: 2.5 GB (compressed: 320 MB)
Compression Ratio: 7.8x

Data Quality:
  - Gaps Detected: 0
  - Sequence Errors: 0
  - Quality Score: 99.8%

Session Files: 150
Verification: ✓ All checksums valid
```

### 66. Archival-Optimized Compression Profiles
**Current State**: Single compression setting for all data.

**Refinement**:
- Create compression profiles optimized for archival use cases
- Implement symbol-specific compression (high-volume symbols get faster codec)
- Add event-type-specific compression (trades vs. L2 data)
- Support progressive compression (faster during collection, deeper for archive)
- Include decompression speed benchmarks for export planning
- Enable compression profile A/B testing with size comparisons

**Compression Profiles**:
```json
{
  "CompressionProfiles": {
    "RealTimeCollection": {
      "codec": "lz4",
      "level": 1,
      "priority": "speed"
    },
    "WarmArchive": {
      "codec": "zstd",
      "level": 6,
      "priority": "balanced"
    },
    "ColdArchive": {
      "codec": "zstd",
      "level": 19,
      "priority": "size"
    },
    "HighVolumeSymbols": {
      "codec": "zstd",
      "level": 3,
      "priority": "speed",
      "applies_to": ["SPY", "QQQ", "AAPL"]
    }
  }
}
```

### 67. Offline Storage Health Monitoring
**Current State**: Basic disk space warnings.

**Refinement**:
- Implement comprehensive storage health dashboard
- Monitor disk health indicators (SMART status, reallocated sectors)
- Track write performance over time for degradation detection
- Alert on storage media issues before data loss
- Support RAID status monitoring for redundant setups
- Include NAS/network storage latency monitoring

**Health Metrics**:
- Disk health score (SMART-based)
- Average write latency (ms)
- Failed write attempts (should be 0)
- Storage throughput trend
- Free space projection (days until full)
- Last backup verification status

### 68. Data Deduplication System
**Current State**: No deduplication.

**Refinement**:
- Implement content-based deduplication for multi-source data
- Detect and merge duplicate events from different providers
- Create deduplication reports showing space savings
- Support configurable deduplication strategies (first-wins, best-quality)
- Maintain provenance tracking for deduplicated records
- Enable deduplication dry-run for impact assessment

### 69. Archive Export Presets
**Current State**: Manual export configuration each time.

**Refinement**:
- Create saveable export presets for common workflows
- Support format-specific presets (CSV for Excel, Parquet for Python, etc.)
- Include destination path templates with date/symbol variables
- Enable preset scheduling for automated exports
- Share presets across users via import/export
- Include post-export hooks (run script, send notification)

**Export Preset Example**:
```json
{
  "name": "Daily Python Analysis Export",
  "format": "parquet",
  "compression": "snappy",
  "destination": "D:/Analysis/{year}/{month}/",
  "filename_pattern": "{symbol}_{date}.parquet",
  "filters": {
    "event_types": ["Trade", "BboQuote"],
    "symbols": "all",
    "date_range": "yesterday"
  },
  "post_export_hook": "python analyze.py --date {date}",
  "schedule": "0 6 * * *"
}
```

---

## Dashboard Enhancements

### 4. Customizable Dashboard Layouts
**Current State**: Fixed dashboard layout with predefined widgets.

**Refinement**:
- Drag-and-drop widget repositioning
- Resizable widget panels
- Save/load custom dashboard layouts
- Hide unused widgets option
- Add new widget types:
  - Market status widget (pre-market, market hours, after-hours)
  - Provider comparison chart
  - Data volume heatmap by symbol

### 5. Enhanced Throughput Visualization
**Current State**: Basic line chart for event throughput.

**Refinement**:
- Add area chart option with fill gradient
- Support multiple series (trades vs quotes vs depth)
- Zoom and pan capabilities
- Export chart as PNG/SVG
- Add benchmark overlay showing expected throughput

### 6. Quick Actions Keyboard Shortcuts
**Current State**: Quick actions only accessible via mouse click.

**Refinement**:
- Add keyboard shortcuts for common actions:
  - `Ctrl+S` - Start service
  - `Ctrl+Shift+S` - Stop service
  - `F5` - Refresh dashboard
  - `Ctrl+L` - Open logs
  - `Ctrl+B` - Open backfill
- Display shortcut hints on button hover
- Allow custom keybinding configuration

### 7. Symbol Performance Drill-Down
**Current State**: Symbol table shows basic metrics.

**Refinement**:
- Click symbol to open detailed performance panel
- Show intraday tick chart for selected symbol
- Display statistics: avg latency, message rate, gaps detected
- Compare symbol against provider average
- Quick toggle subscription directly from table

---

## Provider Management Improvements

### 8. Provider Health Score Algorithm
**Current State**: Quality score shown but algorithm not transparent.

**Refinement**:
- Display breakdown of health score components:
  - Connection stability (30%)
  - Latency consistency (25%)
  - Data completeness (25%)
  - Reconnection frequency (20%)
- Historical health score trend chart
- Threshold configuration for failover triggers

### 9. Multi-Provider Comparison View
**Current State**: Providers configured individually.

**Refinement**:
- Side-by-side comparison table of all configured providers
- Real-time latency comparison chart
- Data coverage matrix by symbol
- Automatic provider ranking based on performance
- One-click failover promotion

### 10. Connection Test Improvements
**Current State**: Basic connection test with pass/fail result.

**Refinement**:
- Detailed test results showing:
  - DNS resolution time
  - TCP connection time
  - Authentication time
  - First data received time
- Test historical data retrieval capability
- Test real-time subscription capability
- Save test results history

### 11. Provider-Specific Advanced Settings
**Current State**: Limited provider configuration options.

**Refinement for Interactive Brokers**:
- Market data type selection (Live, Frozen, Delayed, Delayed Frozen)
- Account selection for multi-account setups
- TWS vs IB Gateway toggle
- Automatic market data farm selection

**Refinement for Alpaca**:
- WebSocket connection pooling options
- Batch subscription size configuration
- Rate limiting visualization

**Refinement for Polygon**:
- Tier/subscription plan indicator
- API call usage meter
- WebSocket vs REST preference

---

## Symbol Management Refinements

### 12. Smart Symbol Search
**Current State**: Basic text search with autocomplete.

**Refinement**:
- Fuzzy search support (e.g., "MSFT" matches "Microsoft")
- Search by CUSIP, ISIN, or FIGI
- Recent searches history
- Popular symbols suggestions
- Sector/industry filter integration

### 13. Bulk Symbol Operations Enhancement
**Current State**: Basic select-all and bulk delete.

**Refinement**:
- Bulk edit depth levels
- Bulk change exchange
- Apply template to selected symbols
- Copy configuration between symbols
- Regex-based selection (e.g., select all `^SPY.*`)

### 14. Symbol Groups & Portfolios
**Current State**: Flat symbol list with watchlists.

**Refinement**:
- Hierarchical symbol organization (folders/groups)
- Portfolio-based grouping with position tracking
- Tag-based categorization with multiple tags per symbol
- Smart groups based on criteria (e.g., "All tech stocks > $100")
- Drag-drop symbol organization

### 15. Real-Time Symbol Status
**Current State**: Subscription status not shown in list.

**Refinement**:
- Live status indicator per symbol:
  - Green: Receiving data
  - Yellow: Subscribed, no recent data
  - Red: Subscription error
  - Gray: Not subscribed
- Last tick timestamp display
- Message rate per symbol (ticks/sec)
- Quick unsubscribe/resubscribe from context menu

### 16. Symbol Import Enhancements
**Current State**: CSV import only.

**Refinement**:
- Support additional formats: Excel (.xlsx), JSON, XML
- Import from external sources:
  - SEC EDGAR (company tickers)
  - NASDAQ listed symbols API
  - NYSE listed symbols
- Preview before import with conflict resolution
- Scheduled import refresh

---

## Backfill & Historical Data

### 17. Backfill Progress Visualization
**Current State**: Per-symbol progress tracking.

**Refinement**:
- Overall progress bar with ETA
- Download speed indicator (bars/second)
- Pause/resume individual symbols
- Priority queue management (drag to reorder)
- Estimated storage requirement before start

### 18. Data Gap Detection & Repair
**Current State**: Basic validation functionality.

**Refinement**:
- Automatic gap detection after backfill
- Visual gap report with calendar view
- One-click gap fill from alternative sources
- Gap severity classification (holidays vs actual gaps)
- Historical gap repair tracking

### 19. Intelligent Date Range Suggestions
**Current State**: Manual date selection with presets.

**Refinement**:
- Detect existing data and suggest only missing ranges
- IPO date detection for new symbols
- Split/dividend adjustment recommendations
- Corporate action calendar integration
- Avoid weekends and holidays automatically

### 20. Backfill Queue Management
**Current State**: Basic scheduled jobs.

**Refinement**:
- Job queue with drag-drop priority
- Dependency chains (backfill B after A completes)
- Resource throttling (max concurrent downloads)
- Provider rate limit awareness
- Queue persistence across app restarts

### 21. Data Quality Report
**Current State**: No post-backfill quality assessment.

**Refinement**:
- Generate quality report after backfill:
  - Missing bars count
  - Suspicious price movements
  - Volume anomalies
  - Corporate action gaps
- Compare against reference data
- Quality score per symbol

---

## Storage & Performance

### 22. Storage Analytics Dashboard
**Current State**: Basic storage health metrics.

**Refinement**:
- Detailed breakdown by:
  - Symbol (top 10 storage consumers)
  - Data type (trades vs quotes vs bars)
  - Time period (this month vs historical)
- Compression ratio statistics
- Projected storage growth
- Cleanup recommendations

### 23. Data Retention Policies Enhancement
**Current State**: Basic retention configuration.

**Refinement**:
- Tiered retention (keep 1-min bars for 1 year, daily forever)
- Archive to cold storage option (compress + move)
- Automatic cleanup scheduler
- Retention policy templates
- Pre-deletion preview and confirmation
- Retention guardrails that block overly aggressive settings (e.g., minimum 7 days for tick data)
- Legal hold toggle to freeze deletes for incident investigations or audits
- Hash-based verification before deletion to ensure archived copies are intact
- Per-provider retention dry run that shows which files would be removed, grouped by symbol and date
- Post-cleanup audit report with file counts, sizes, and any skipped items for compliance evidence

**File Retention Assurance (New)**:
- Combine guardrails, legal holds, and dry-run previews into a unified retention safety center
- Schedule periodic checksum audits to confirm archives remain readable and uncorrupted
- Generate retention attestation reports for compliance stakeholders with export to PDF/CSV

### 24. Storage Path Validation
**Current State**: Path configuration without validation.

**Refinement**:
- Real-time path validation (exists, writable, space available)
- Network path latency warning
- SSD vs HDD detection with recommendations
- Minimum free space alerts
- Automatic path migration tool
- Safe-move workflow that validates source and destination parity (counts + checksums) before switching paths
- Background retention watchdog that alerts if expected files disappear or shrink unexpectedly
- Disk health signals (SMART status, reallocated sectors) surfaced in storage settings

### 25. Performance Monitoring
**Current State**: Basic latency display.

**Refinement**:
- CPU/memory usage by component
- Disk I/O metrics
- Network bandwidth utilization
- Queue depths and processing times
- Performance degradation alerts
- Historical performance comparison

---

## UI/UX Improvements

### 26. Dark/Light Theme Toggle
**Current State**: Fixed theme (assumed light).

**Refinement**:
- Dark mode with proper color palette
- System theme auto-detection
- Scheduled theme switching
- Per-page theme preview
- High contrast mode support

### 27. Responsive Layout Improvements
**Current State**: Fixed grid layouts.

**Refinement**:
- Adaptive layouts for different window sizes
- Collapsible sidebar for more workspace
- Floating panel mode for multi-monitor
- Remember window position and size
- Snap layout support (Windows 11)

### 28. Loading States & Skeletons
**Current State**: Progress rings for loading.

**Refinement**:
- Skeleton loaders for content areas
- Partial loading (show cached data while fetching updates)
- Loading progress percentage where applicable
- Cancel long-running operations
- Offline mode indicators

### 29. Form Validation Improvements
**Current State**: Basic input validation.

**Refinement**:
- Real-time validation feedback
- Field-level error messages
- Validation summary panel
- Unsaved changes warning
- Form auto-save draft

### 30. Context Menus
**Current State**: Limited right-click functionality.

**Refinement**:
- Rich context menus on:
  - Symbols (edit, delete, view chart, copy)
  - Providers (test, disable, view logs)
  - Dashboard widgets (refresh, configure, remove)
- Consistent menu design across app
- Keyboard navigation support

### 31. Breadcrumb Navigation
**Current State**: Sidebar navigation only.

**Refinement**:
- Breadcrumb trail for deep navigation
- Quick jump to parent sections
- Recent pages history
- Bookmark frequently visited pages

---

## Security Enhancements

### 32. Credential Audit Log
**Current State**: Credentials stored securely but no access tracking.

**Refinement**:
- Log credential access events
- Alert on suspicious access patterns
- Credential expiration reminders
- Integration with Windows Security Center

### 33. API Key Rotation Reminders
**Current State**: Static API key storage.

**Refinement**:
- Track API key age
- Configurable rotation reminders (30/60/90 days)
- One-click rotation workflow
- Key versioning support

### 34. Role-Based Access (Multi-User)
**Current State**: Single-user application.

**Refinement**:
- Optional multi-user support via Windows accounts
- Admin vs Viewer roles
- Audit trail of configuration changes
- Lock configuration during trading hours

### 35. Secure Configuration Backup
**Current State**: Local configuration only.

**Refinement**:
- Encrypted configuration export
- Secure cloud backup option (OneDrive integration)
- Configuration versioning with rollback
- Exclude credentials from backup option

---

## Integration & Automation

### 36. PowerShell/CLI Integration
**Current State**: GUI-only operation.

**Refinement**:
- PowerShell module for automation:
  ```powershell
  Start-MDCService
  Add-MDCSymbol -Symbol "AAPL" -Trades -Depth -Levels 10
  Invoke-MDCBackfill -Symbols "MSFT,GOOGL" -Days 30
  ```
- Scheduled task integration
- CI/CD pipeline support

### 37. REST API for Remote Management
**Current State**: Local app only.

**Refinement**:
- Optional embedded REST API
- Remote status monitoring
- Configuration management endpoints
- Webhook support for events
- OpenAPI/Swagger documentation

### 38. External Alert Integrations
**Current State**: Windows notifications only.

**Refinement**:
- Email alerts (SMTP configuration)
- Slack/Teams webhook integration
- PagerDuty integration for critical alerts
- Custom webhook support
- Alert routing rules

### 39. Data Export Automation
**Current State**: Manual export triggers.

**Refinement**:
- Scheduled export jobs
- Export to cloud storage (S3, Azure Blob, GCS)
- FTP/SFTP upload support
- Post-export webhook triggers
- Export templates (recurring configurations)

### 40. Trading Platform Integrations
**Current State**: Data collection focus only.

**Refinement**:
- Real-time data streaming to:
  - TradeStation
  - NinjaTrader
  - AmiBroker
  - Custom TCP/WebSocket endpoints
- Format compatibility modes
- Latency optimization for trading use

---

## Accessibility & Localization

### 41. Screen Reader Support
**Current State**: Unknown accessibility status.

**Refinement**:
- ARIA labels for all controls
- Keyboard-only navigation
- Screen reader announcements for status changes
- High contrast theme
- Focus indicators

### 42. Localization Framework
**Current State**: English only (assumed).

**Refinement**:
- Resource file based localization
- Support for:
  - Spanish
  - German
  - Japanese
  - Chinese (Simplified)
- Date/time format localization
- Number format localization
- Right-to-left layout support

### 43. Font Size & Display Scaling
**Current State**: Fixed font sizes.

**Refinement**:
- User-configurable font size
- Respect Windows display scaling
- Compact vs comfortable spacing modes
- Icon size scaling

---

## Data Visualization Enhancements

### 44. Advanced Charting
**Current State**: Basic sparklines and simple charts.

**Refinement**:
- Interactive candlestick charts
- Multiple chart types (line, bar, area, OHLC)
- Technical indicator overlays (MA, VWAP)
- Drawing tools (trend lines, annotations)
- Chart export and sharing

### 45. Real-Time Data Ticker
**Current State**: Table-based display.

**Refinement**:
- Scrolling ticker tape widget
- Color-coded price changes
- Customizable ticker symbols
- Click to expand details
- Multiple ticker speeds

### 46. Heat Map Visualizations
**Current State**: No heat map support.

**Refinement**:
- Symbol performance heat map
- Sector heat map
- Time-based activity heat map
- Customizable color scales
- Interactive drill-down

---

## Service Management Improvements

### 47. Service Health Deep Dive
**Current State**: Basic start/stop controls.

**Refinement**:
- Process resource usage (CPU, memory, handles)
- Thread pool status
- Connection pool metrics
- Garbage collection statistics
- Restart history log

### 48. Log Management Enhancement
**Current State**: Basic log viewing.

**Refinement**:
- Log level filtering (Debug, Info, Warning, Error)
- Full-text search in logs
- Log export (filtered or full)
- Log rotation configuration
- Real-time log tail with auto-scroll
- Syntax highlighting for structured logs

### 49. Diagnostic Tools
**Current State**: Limited diagnostics.

**Refinement**:
- Network connectivity diagnostics
- Provider endpoint health check
- Configuration validation tool
- Performance profiler
- Generate support bundle

---

## Operational Workflow Enhancements

### 50. Notification Center & Incident Timeline
**Current State**: Toasts surface critical events but historical context is limited to logs.

**Refinement**:
- Persistent notification center panel with filter by severity and source (provider, storage, backfill)
- Incident timeline that stitches related events (disconnect → retry → failover → recovery) into one thread
- Quick actions on each incident (retry, open logs, mute, create support bundle)
- Snooze rules for noisy alerts and auto-expiry after acknowledgement

**Implementation Notes**:
- Backed by a lightweight local queue (e.g., SQLite) with retention controls
- Reuse existing Serilog event IDs to map incidents to logs and telemetry
- Provide deep links into the relevant page (provider details, symbol, backfill job)

### 51. Workspace Templates & Session Restore
**Current State**: Users rebuild layouts and filters when switching tasks or restarting the app.

**Refinement**:
- Saveable workspace templates (e.g., Monitoring, Backfill Ops, Storage Admin) capturing open pages, filters, and widget layout
- Session auto-restore on startup with last active workspace and scroll positions
- Multi-monitor workspace presets with remembered window bounds and snapped panels
- Workspace switching shortcuts with transition hints

**Implementation Notes**:
- Persist workspace definitions in app data with versioning for forward compatibility
- Provide export/import of templates for team sharing
- Guard restores with capability checks (e.g., hide provider widgets if provider disabled)

### 52. Offline Cache Mode for Air-Gapped Environments
**Current State**: UI assumes continuous connectivity to the collector service.

**Refinement**:
- Read-only offline mode that loads last-known metrics, storage summaries, and configuration snapshots
- Background cache refresher that opportunistically syncs when connectivity returns
- Visual connectivity banner with "retry now" and cache age indicators
- Explicit offline export of configuration and symbol lists for change review in disconnected networks

**Implementation Notes**:
- Use local storage for cached API responses with staleness metadata
- Gracefully degrade charts (sparkline placeholders, cached ranges) while offline
- Provide an "offline diff" that queues edits for approval once reconnected

### 53. Guided Setup & Preflight Sanity Checks
**Current State**: New users rely on documentation to configure providers and storage correctly.

**Refinement**:
- Step-by-step guided setup wizard covering provider credentials, storage paths, and retention defaults
- Preflight validation that runs connectivity tests, disk-space checks, and sample subscription validation before enabling services
- Contextual tips drawn from recent incidents ("last run failed due to rate limits—consider smaller batch size")
- Summary screen with exportable setup report for compliance evidence

**Implementation Notes**:
- Reuse existing connection test hooks and storage validators to avoid duplicate logic
- Store completed steps to allow resuming the wizard mid-way
- Offer presets for common setups (IB-only, Alpaca + S3 archive, local-only sandbox)

### 54. Support Bundle Composer
**Current State**: Support bundles must be assembled manually from logs and configuration files.

**Refinement**:
- Guided wizard to select time range, providers, symbols, and data types to include
- Automatic redaction of secrets plus user-visible preview of what will be packaged
- Include environment summary (app version, OS build, hardware footprint) and recent incident timeline
- One-click upload to a configurable secure endpoint or save-to-disk option

**Implementation Notes**:
- Leverage existing logging paths and diagnostics tooling to gather artifacts
- Generate bundles asynchronously with progress feedback and size estimates
- Provide checksum and signature for integrity verification when sharing externally

## External Analysis Preparation

> **Design Principle**: The collector's role is to provide clean, well-organized data that external tools can consume efficiently. These features focus on making data analysis-ready without building analysis capabilities into the collector itself.

### 70. Analysis-Ready Export Formats
**Current State**: JSONL and Parquet exports available.

**Refinement**:
- Add pre-configured export profiles for common analysis tools:
  - **Python/Pandas**: Parquet with appropriate dtypes, datetime handling
  - **R**: CSV with proper NA handling, date formats
  - **QuantConnect Lean**: Native Lean data format with zip packaging
  - **Excel**: XLSX with multiple sheets by symbol/date
  - **Database Import**: SQL INSERT statements, PostgreSQL COPY format
- Include data dictionary documentation with each export
- Generate sample code snippets for loading exported data

**Export Profiles**:
```json
{
  "ExportProfiles": {
    "PythonPandas": {
      "format": "parquet",
      "engine": "pyarrow",
      "timestamp_format": "datetime64[ns]",
      "include_loader_script": true
    },
    "QuantConnectLean": {
      "format": "lean_zip",
      "resolution": "tick",
      "market": "usa",
      "security_type": "equity"
    },
    "PostgreSQL": {
      "format": "csv",
      "delimiter": ",",
      "include_headers": true,
      "include_ddl": true,
      "copy_command": true
    }
  }
}
```

### 71. Data Sampling & Subset Creation
**Current State**: Full data export only.

**Refinement**:
- Create representative data samples for development/testing
- Support stratified sampling (preserve distribution of symbols, times)
- Enable time-based downsampling (e.g., every 10th second)
- Create small test datasets for algorithm development
- Generate sample statistics alongside sampled data
- Support reproducible sampling with seed configuration

**Sampling Options**:
- Random sample: N events or N% of total
- Time-based: Every N seconds, first N minutes of each hour
- Symbol-stratified: Equal representation across symbols
- Event-type-stratified: Maintain trade/quote ratio
- Volatility-based: Oversample high-activity periods

### 72. Data Dictionary & Schema Documentation
**Current State**: Schema defined in code but not exported with data.

**Refinement**:
- Auto-generate data dictionaries for all event types
- Include field descriptions, data types, valid ranges
- Document exchange-specific codes and conditions
- Create versioned schema documentation
- Export schema alongside data in multiple formats (JSON Schema, Avro, Protobuf)
- Include example records for each event type

**Data Dictionary Example**:
```markdown
# Trade Event Schema v2.0

| Field | Type | Description | Example |
|-------|------|-------------|---------|
| Timestamp | datetime64[ns] | Event timestamp in UTC | 2026-01-03T14:30:00.123456789Z |
| Symbol | string | Ticker symbol | AAPL |
| Price | decimal(18,8) | Trade price | 185.2500 |
| Size | int64 | Trade size in shares | 100 |
| Side | enum | Aggressor side (Buy/Sell/Unknown) | Buy |
| Exchange | string | Exchange code | XNAS |
| TradeId | string | Unique trade identifier | T123456789 |
| Conditions | string[] | Trade condition codes | ["@", "F"] |
```

### 73. Time Series Alignment Tools
**Current State**: Raw event data with irregular timestamps.

**Refinement**:
- Provide tools to align data to regular intervals (1s, 1m, 5m, etc.)
- Generate OHLCV bars from tick data during export
- Support multiple aggregation methods (last, mean, VWAP)
- Handle gaps with configurable fill strategies (ffill, null, interpolate)
- Create aligned multi-symbol datasets for correlation analysis
- Export alignment metadata (original event counts, gap locations)

**Alignment Configuration**:
```json
{
  "TimeSeriesAlignment": {
    "interval": "1min",
    "aggregation": {
      "price": "ohlc",
      "volume": "sum",
      "trade_count": "count"
    },
    "gap_handling": {
      "strategy": "forward_fill",
      "max_gap_intervals": 5,
      "mark_filled": true
    },
    "timezone": "America/New_York",
    "market_hours_only": true
  }
}
```

### 74. Feature Engineering Presets
**Current State**: Raw data only, no derived features.

**Refinement**:
- Generate common derived features during export:
  - Returns (log, simple, at various horizons)
  - Rolling statistics (mean, std, min, max)
  - Technical indicators (SMA, EMA, RSI, MACD)
  - Microstructure features (spread, imbalance, VWAP)
- Allow custom feature definitions via configuration
- Include feature documentation and formulas
- Support feature normalization and scaling

**Feature Presets**:
```json
{
  "FeaturePresets": {
    "BasicReturns": {
      "features": ["log_return_1m", "log_return_5m", "log_return_1h"],
      "include_raw": true
    },
    "TechnicalIndicators": {
      "features": ["sma_20", "ema_12", "ema_26", "macd", "rsi_14"],
      "lookback_periods": [5, 10, 20, 50]
    },
    "Microstructure": {
      "features": ["bid_ask_spread", "order_imbalance", "trade_intensity", "kyle_lambda"],
      "requires": ["BboQuote", "Trade"]
    }
  }
}
```

### 75. External Analysis Workspace Setup
**Current State**: No guidance for external tool integration.

**Refinement**:
- Generate project templates for analysis environments:
  - Python: requirements.txt, data loader module, Jupyter notebooks
  - R: R project with data import scripts
  - QuantConnect: Lean project structure with data links
- Create environment setup documentation
- Provide sample analysis scripts demonstrating data access
- Include common analysis patterns and best practices
- Support workspace generation for selected date ranges/symbols

**Generated Python Workspace**:
```
analysis_workspace/
├── requirements.txt
├── config.yaml
├── data/
│   └── -> symlink to exported data
├── notebooks/
│   ├── 01_data_exploration.ipynb
│   ├── 02_quality_check.ipynb
│   └── 03_basic_analysis.ipynb
├── src/
│   ├── __init__.py
│   ├── data_loader.py
│   └── utils.py
└── README.md
```

### 76. Analysis-Ready Data Quality Report
**Current State**: Quality reports focused on collection metrics.

**Refinement**:
- Generate analysis-focused quality reports with each export:
  - Missing data summary with timestamps
  - Outlier detection results
  - Data distribution statistics
  - Stationarity test results
  - Autocorrelation analysis
- Include warnings about data issues relevant to analysis
- Provide recommendations for handling detected issues
- Generate machine-readable quality metadata

**Quality Report for Analysis**:
```
Data Quality Report for External Analysis
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Export: AAPL_2026-01.parquet

Data Completeness:
  Trading Days: 21/21 (100%)
  Expected Bars (1min): 8190
  Actual Bars: 8185 (99.94%)
  Missing: 5 bars (see gaps.csv)

Distribution Statistics:
  Price Range: $180.25 - $195.80
  Daily Volume Range: 45M - 120M
  Mean Daily Trades: 850,000

Potential Issues:
  ⚠ 3 price outliers detected (>4σ) - see outliers.csv
  ⚠ Volume spike on 2026-01-15 (earnings) - 3x normal
  ✓ No structural breaks detected
  ✓ Stationarity: Returns are stationary (ADF p<0.01)

Recommendations:
  - Consider winsorizing outliers for ML training
  - Earnings date (01-15) may require special handling
  - Data suitable for: backtesting, ML training, research
```

### 77. Batch Export Automation
**Current State**: Single export operations.

**Refinement**:
- Create batch export jobs for multiple symbols/date ranges
- Support incremental exports (only new/changed data)
- Enable parallel export processing for large datasets
- Generate export completion reports with statistics
- Implement export queuing with priority
- Support export to multiple destinations simultaneously

**Batch Export Configuration**:
```json
{
  "BatchExport": {
    "name": "Monthly Research Export",
    "schedule": "0 6 1 * *",
    "jobs": [
      {
        "symbols": ["AAPL", "MSFT", "GOOGL", "AMZN", "META"],
        "date_range": "last_month",
        "format": "parquet",
        "destination": "/research/monthly/"
      },
      {
        "symbols": "sp500",
        "date_range": "last_month",
        "format": "lean_zip",
        "destination": "/lean/data/"
      }
    ],
    "parallel_jobs": 4,
    "notify_on_complete": true
  }
}
```

### 78. Data Versioning for Analysis
**Current State**: No versioning of exported datasets.

**Refinement**:
- Version exported datasets for reproducibility
- Track export configuration and parameters
- Generate dataset fingerprints for verification
- Support dataset comparison across versions
- Maintain export history with metadata
- Enable rollback to previous export versions

**Version Manifest**:
```json
{
  "dataset_id": "AAPL_2026-01_v3",
  "created_at": "2026-02-01T06:00:00Z",
  "source_data_version": "archive_2026-01-31",
  "export_config_hash": "sha256:abc123...",
  "record_count": 8500000,
  "file_checksum": "sha256:def456...",
  "changes_from_v2": [
    "Added TradeConditions field",
    "Fixed timezone handling for pre-market"
  ]
}
```

### 79. Analysis Integration Documentation
**Current State**: Limited integration guidance.

**Refinement**:
- Comprehensive documentation for common analysis workflows:
  - Backtesting setup with popular frameworks
  - Machine learning pipeline integration
  - Real-time replay for strategy testing
  - Database import procedures
- Troubleshooting guides for common issues
- Performance optimization tips for large datasets
- Sample code repository with analysis examples

---

## Implementation Priority Matrix

### Core Offline Storage & Archival (Primary Focus)

| Priority | Refinement | Effort | Impact | Category | Status |
|----------|-----------|--------|--------|----------|--------|
| P0 | Archival-First Storage Pipeline (#55) | Medium | Critical | Offline Storage | **IMPLEMENTED** (2026-01-04) |
| P0 | Offline Data Catalog & Manifest System (#56) | Medium | Critical | Offline Storage | **IMPLEMENTED** (2026-01-03) |
| P0 | Archive Verification & Integrity Dashboard (#57) | Medium | High | Offline Storage | **IMPLEMENTED** (2026-01-03) |
| P0 | Collection Session Management (#65) | Low | High | Offline Storage | **IMPLEMENTED** (2026-01-03) |
| P1 | Data Completeness Dashboard (#62) | Medium | High | Offline Storage | **IMPLEMENTED** (2026-01-03) |
| P1 | Archive Browsing & Inspection Tools (#61) | Medium | High | Offline Storage | **IMPLEMENTED** (2026-01-03) |
| P1 | Portable Archive Packages (#59) | Medium | High | Offline Storage | **IMPLEMENTED** (2026-01-03) |
| P1 | Long-Term Format Preservation (#58) | Medium | High | Offline Storage | **IMPLEMENTED** (2026-01-04) |
| P1 | Archival-Optimized Compression Profiles (#66) | Low | Medium | Offline Storage | **IMPLEMENTED** (2026-01-04) |
| P2 | Scheduled Archive Maintenance (#64) | Medium | Medium | Offline Storage | **IMPLEMENTED** (2026-01-11) |
| P2 | Archive Storage Optimization Advisor (#63) | Medium | Medium | Offline Storage | **IMPLEMENTED** (2026-01-26) |
| P2 | Offline Storage Health Monitoring (#67) | Medium | Medium | Offline Storage |
| P2 | Offline-First Collection Mode (#60) | High | Medium | Offline Storage |
| P3 | Data Deduplication System (#68) | High | Low | Offline Storage |
| P3 | Archive Export Presets (#69) | Low | Low | Offline Storage | **IMPLEMENTED** (2026-01-28) |

### External Analysis Preparation

| Priority | Refinement | Effort | Impact | Category | Status |
|----------|-----------|--------|--------|----------|--------|
| P0 | Analysis-Ready Export Formats (#70) | Medium | Critical | External Analysis | **IMPLEMENTED** (2026-01-04) |
| P0 | Data Dictionary & Schema Documentation (#72) | Low | High | External Analysis | **IMPLEMENTED** (2026-01-03) |
| P1 | Batch Export Automation (#77) | Medium | High | External Analysis | **IMPLEMENTED** (2026-01-03) |
| P1 | Analysis-Ready Data Quality Report (#76) | Medium | High | External Analysis | **IMPLEMENTED** (2026-01-04) |
| P1 | Data Versioning for Analysis (#78) | Medium | High | External Analysis | **IMPLEMENTED** (2026-01-04) |
| P2 | Time Series Alignment Tools (#73) | Medium | Medium | External Analysis |
| P2 | Data Sampling & Subset Creation (#71) | Low | Medium | External Analysis |
| P2 | External Analysis Workspace Setup (#75) | Medium | Medium | External Analysis |
| P2 | Feature Engineering Presets (#74) | High | Medium | External Analysis |
| P3 | Analysis Integration Documentation (#79) | Low | Low | External Analysis |

### Existing Features (Maintained for Future Flexibility)

| Priority | Refinement | Effort | Impact | Status |
|----------|-----------|--------|--------|--------|
| P0 | Real-Time Notification System | Medium | High | **IMPLEMENTED** (2026-01-11) |
| P0 | Data Integrity Alerts Dashboard Widget | Medium | High | **IMPLEMENTED** (2026-01-11) |
| P0 | Auto-Reconnection | Low | High | **IMPLEMENTED** (2026-01-11) |
| P0 | Dark/Light Theme | Medium | High |
| P1 | Symbol Groups & Portfolios | Medium | High |
| P1 | Backfill Progress Visualization | Low | Medium |
| P1 | Storage Analytics Dashboard | Medium | Medium |
| P1 | File Retention Assurance | Medium | High | **IMPLEMENTED** (2026-01-26) |
| P1 | Keyboard Shortcuts | Low | Medium |
| P1 | Notification Center & Incident Timeline | Medium | High | **IMPLEMENTED** (2026-01-26) |
| P1 | Workspace Templates & Session Restore | Low | Medium | **IMPLEMENTED** (2026-01-26) |
| P2 | Provider Health Score Breakdown | Medium | Medium |
| P2 | PowerShell Integration | High | Medium |
| P2 | Offline Cache Mode | Medium | Medium |
| P2 | Guided Setup & Preflight Checks | Low | Medium |
| P2 | Advanced Charting | High | Medium |
| P3 | REST API | High | Low |
| P3 | Localization | High | Low |
| P3 | Multi-User Support | High | Low |

> **Note**: Cloud storage integration features (Azure Blob, AWS S3, GCS) are already implemented and maintained for future use when online/hybrid storage workflows are needed. The current focus is on perfecting the offline archival experience.

---

## Technical Debt Considerations

1. **Service Layer Abstraction**: Consider adding interface abstractions for services to enable unit testing
2. **Async/Await Consistency**: Audit all async operations for proper cancellation token support
3. **Memory Management**: Implement IDisposable patterns for resource-heavy pages
4. **Configuration Validation**: Add JSON schema validation for configuration files
5. **Error Boundary**: Implement global exception handling with user-friendly error dialogs

---

## Next Steps

1. Review refinements with stakeholders
2. Prioritize based on user feedback
3. Create implementation timeline
4. Design mockups for major UI changes
5. Begin P0 implementations

---

*Document Version: 2.4*
*Last Updated: January 28, 2026*
*Focus: Offline Data Storage, Collection & Archival Excellence*

## Recent Implementations (v1.6.0)

### January 28, 2026
- **Archive Export Presets (#69)**: ExportPresetService and ExportPresetsPage for saving, managing, and using export configurations with scheduling and post-export hooks

### January 26, 2026
- **Notification Center & Incident Timeline (#50)**: NotificationCenterPage with history, filtering, incident timeline, and snooze rules
- **Workspace Templates & Session Restore (#51)**: WorkspaceService and WorkspacePage for saving/restoring workspace layouts
- **File Retention Assurance (#23)**: RetentionAssuranceService with guardrails, legal holds, dry-run preview, and audit reports
- **Archive Storage Optimization Advisor (#63)**: StorageOptimizationAdvisorService with duplicate detection, compression analysis, and tiering recommendations

### January 11, 2026
- **Real-Time Notification System (#1)**: In-app notification banner with auto-dismiss and action buttons
- **Data Integrity Alerts Dashboard Widget (#2)**: Expandable panel with severity badges, acknowledgment, and export
- **Auto-Reconnection with Exponential Backoff (#3)**: ConnectionService with configurable retry strategy and visual indicators
- **Scheduled Archive Maintenance (#64)**: ScheduledMaintenanceService with daily/weekly/monthly tasks and dry-run support
- **IntegrityEventsService**: Centralized tracking for sequence gaps, stale data, validation failures, and provider switches

### January 4, 2026
- **Archival-First Storage Pipeline (#55)**: Write-ahead logging with checksums for crash-safe persistence
- **Long-Term Format Preservation (#58)**: Schema versioning, migration support, and JSON Schema export
- **Archival-Optimized Compression Profiles (#66)**: LZ4, ZSTD, Gzip profiles for different storage tiers
- **Analysis-Ready Export Formats (#70)**: Pre-built export profiles for Python, R, Lean, Excel, PostgreSQL
- **Analysis-Ready Data Quality Report (#76)**: Comprehensive quality metrics with outlier detection
- **Data Versioning for Analysis (#78)**: Dataset fingerprinting and version tracking
