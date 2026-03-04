# Brainstorm: Next Frontier Improvements

**Date:** 2026-03-03
**Context:** Core platform is 94%+ complete. This brainstorm focuses on *new capabilities and systemic improvements* not yet covered by existing evaluations. The goal is to identify work that would meaningfully expand the project's value rather than polish existing features.

**Scoring:**
- **Impact**: How much this changes what users can do or how reliably the system operates
- **Effort**: T-shirt sizing (S = days, M = 1-2 weeks, L = 3+ weeks)
- **Risk**: Likelihood of destabilizing existing functionality

---

## Area 1: Data Intelligence & Analytics

### 1.1 Cross-Symbol Correlation Engine

**Problem:** The system collects data for many symbols independently but provides no way to analyze relationships between them. Quantitative researchers need correlation matrices, lead-lag analysis, and co-movement detection — they currently must export data and compute these externally.

**Proposal:** Add a `CorrelationService` that computes rolling correlation matrices across collected symbols using streaming updates. Expose via `/api/analytics/correlations` and surface in the desktop dashboard as a heatmap. Use the existing `TechnicalIndicatorService` pattern — compute on buffered data, no new storage required.

**Key deliverables:**
- Rolling Pearson correlation (configurable window: 5m, 1h, 1d)
- Lead-lag detection (which symbol moves first)
- Cluster detection (groups of correlated symbols)
- API endpoint + optional dashboard panel

**Impact:** High — turns the collector from a "data hose" into an analytical platform.
**Effort:** M
**Risk:** Low — purely additive, reads existing stored data.

---

### 1.2 Microstructure Event Annotations

**Problem:** Raw trades and quotes are stored without higher-level annotations. A researcher looking at the data needs to manually identify events like: sweep orders, block trades, halt/resume sequences, NBBO changes, or unusual spread widening events.

**Proposal:** Add an `EventAnnotator` stage in the pipeline (after canonicalization, before storage) that tags events with microstructure annotations as metadata fields. These annotations are non-destructive — they add optional fields to the stored JSONL without altering the core schema.

**Candidate annotations:**
- `sweep`: trade that walks through multiple price levels
- `block`: trade above a configurable size threshold (e.g., 10,000 shares)
- `halt_related`: trade/quote near a known halt/resume boundary
- `spread_spike`: BBO spread exceeds N× its rolling average
- `imbalance_flip`: order book imbalance changes sign

**Impact:** High — significantly increases data value for downstream consumers.
**Effort:** M
**Risk:** Low — pipeline decorator pattern already proven by `CanonicalizingPublisher`.

---

### 1.3 Cost-Per-Query Estimator for Backfill

**Problem:** Users run backfill operations without understanding the API cost implications. A `--backfill-symbols SPY,AAPL,MSFT,GOOGL --backfill-from 2020-01-01` command could consume thousands of API calls against rate-limited providers, and there's no visibility into this until the operation runs.

**Proposal:** Add a `BackfillCostEstimator` that, given a backfill request, estimates:
- Number of API calls required per provider
- Estimated wall-clock time (given rate limits)
- Whether the request will exhaust free-tier quotas
- Recommended provider ordering to minimize cost

Surface this via `--backfill --dry-run` (extend existing dry-run) and via the `/api/backfill/run/preview` endpoint (already exists as a stub).

**Impact:** Medium — prevents wasted API quota and user frustration.
**Effort:** S
**Risk:** Low — extends existing dry-run infrastructure.

---

## Area 2: Resilience & Operational Maturity

### 2.1 Replay-Based Regression Testing

**Problem:** The system has 3,400+ unit tests but no way to validate that pipeline changes produce *identical output* for the same input data. A subtle change to canonicalization or filtering could silently alter stored data, and only careful manual inspection would catch it.

**Proposal:** Build a `RegressionTestHarness` that:
1. Replays a recorded market data fixture through the full pipeline (provider mock → EventPipeline → storage sink)
2. Compares output against a "golden" snapshot (stored as committed test data)
3. Diffs any changes and fails the test with a clear report

Use the existing `JsonlReplayer` and `MemoryMappedJsonlReader` as building blocks. Store golden fixtures in `tests/fixtures/golden/`.

**Impact:** High — catches data-altering regressions that unit tests miss.
**Effort:** M
**Risk:** Low — test-only infrastructure, no production code changes.

---

### 2.2 Provider Health Scorecard with Trend Analysis

**Problem:** `ProviderDegradationScorer` computes a point-in-time degradation score, but there's no historical tracking. Operators can't answer: "Has Polygon been getting worse this week?" or "Which provider has the best uptime this month?"

**Proposal:** Add a `ProviderHealthHistory` service that:
- Persists hourly snapshots of provider health metrics (latency p50/p95/p99, error rate, data completeness)
- Computes trend lines (improving/degrading/stable) using simple linear regression over configurable windows
- Surfaces via `/api/providers/health/trends` and a dashboard sparkline per provider
- Optionally emits alerts when a trend crosses a configurable threshold ("Polygon latency increasing 15% week-over-week")

**Impact:** High — moves from reactive to proactive provider management.
**Effort:** M
**Risk:** Low — purely additive, stores its own data.

---

### 2.3 Circuit Breaker Dashboard

**Problem:** The `SharedResiliencePolicies` implement circuit breakers, but their state (open/closed/half-open) isn't visible to operators. When a provider hits a circuit breaker, the only evidence is buried in debug logs.

**Proposal:** Expose circuit breaker state as first-class observable data:
- Add `/api/resilience/circuit-breakers` endpoint listing all breakers with current state, trip count, last trip time, and cooldown remaining
- Surface in the dashboard as colored indicators (green=closed, yellow=half-open, red=open)
- Emit a structured log event on every state transition: `CircuitBreaker {Name} transitioned from {OldState} to {NewState} after {FailureCount} failures`

**Impact:** Medium — critical for operational debugging.
**Effort:** S
**Risk:** Low — read-only visibility into existing Polly policies.

---

## Area 3: Developer & User Experience

### 3.1 Data Catalog with Search & Discovery

**Problem:** Stored data grows over time but there's no unified way to discover what's available. "Do I have AAPL trades from January 2025?" requires manually checking file paths or running CLI commands. The `StorageCatalogService` maintains an index, but it's not searchable or browsable in a user-friendly way.

**Proposal:** Build a `DataCatalog` experience:
- **CLI:** `--catalog search "AAPL trades 2025"` — natural-language-ish search over stored data
- **API:** `/api/catalog/search?q=AAPL&type=trades&from=2025-01-01` — structured search
- **Dashboard:** A dedicated "Data Browser" panel showing timeline bars per symbol (visual representation of data coverage)

The timeline visualization would immediately show gaps, coverage periods, and which providers contributed data for each symbol — like a Gantt chart of data availability.

**Impact:** High — transforms data discoverability.
**Effort:** M
**Risk:** Low — reads existing catalog metadata.

---

### 3.2 Provider Credential Rotation Automation

**Problem:** API keys expire or get rotated, requiring manual config updates and app restarts. The `ConfigWatcher` supports hot-reload but doesn't specifically handle credential rotation.

**Proposal:** Add credential lifecycle management:
- **Expiration tracking:** Parse expiration hints from provider responses (HTTP headers, error codes) and track days-until-expiry
- **Rotation alerts:** Emit warnings N days before suspected expiry: `"Alpaca API key may expire in ~7 days based on token metadata"`
- **Zero-downtime rotation:** Support a `ALPACA__SECRETKEY_NEW` env var pattern — when set, the system tests the new credential, and if valid, atomically switches over and clears the old reference
- **Audit trail:** Log all credential changes with timestamps (never log the credential values themselves)

**Impact:** Medium — reduces operational toil for production deployments.
**Effort:** M
**Risk:** Medium — credential handling requires careful security review.

---

### 3.3 Interactive Backfill Planner

**Problem:** Backfill is a "fire and pray" operation. Users specify parameters and wait, with no visibility into progress until completion. The existing progress display helps, but there's no upfront planning step.

**Proposal:** Add an interactive backfill planner (CLI and web):
1. **Scope visualization:** Show calendar grid of what data already exists vs. what the backfill would fill
2. **Provider routing preview:** Show which provider handles which date range (based on capabilities and priority)
3. **Estimated duration:** Based on historical API response times and rate limits
4. **Incremental execution:** Allow starting a backfill and pausing/resuming it across sessions (checkpoint-based)
5. **Conflict resolution:** When backfilled data overlaps existing data, show a diff and let the user choose (keep existing, overwrite, merge)

**Impact:** High — transforms backfill from "advanced CLI operation" to "guided workflow."
**Effort:** L
**Risk:** Low — extends existing backfill infrastructure without changing core logic.

---

## Area 4: Data Integrity & Governance

### 4.1 Data Lineage Visualization

**Problem:** `DataLineageService` tracks provenance metadata but there's no way to visualize the lineage. When debugging a data quality issue, operators need to trace: "This AAPL trade at 14:32:05 came from Alpaca, was canonicalized at 14:32:05.003, passed bad-tick filter, and was stored at 14:32:05.012."

**Proposal:** Add a lineage trace capability:
- **CLI:** `--trace-event <event-id>` — show the full processing chain for a specific event
- **API:** `/api/lineage/trace/{eventId}` — structured lineage response
- **Dashboard:** Clickable events that expand to show pipeline stages with timing

Store lightweight trace data (timestamps + stage names) as an additional metadata field during pipeline processing. Use sampling (e.g., 1% of events) to keep overhead minimal.

**Impact:** Medium — essential for debugging production data quality issues.
**Effort:** M
**Risk:** Low — uses existing lineage service as foundation.

---

### 4.2 Automated Data Retention Compliance Reports

**Problem:** Organizations using this tool for regulated trading need proof that data retention policies are being followed. Currently there's no automated way to generate a compliance report saying "all data older than X has been archived/purged per policy Y."

**Proposal:** Add a `RetentionComplianceReporter`:
- Scan all stored data against configured retention policies
- Generate a report showing: policy name, affected symbols, data ranges, action taken (archived/purged/retained), exceptions
- Output formats: JSON (machine-readable), Markdown (human-readable), CSV (auditor-friendly)
- **CLI:** `--retention-report --output compliance-2026-Q1.md`
- **Scheduled:** Run monthly via `OperationalScheduler` and store reports in a `reports/` directory

**Impact:** Medium — critical for regulated environments, nice-to-have otherwise.
**Effort:** S
**Risk:** Low — read-only analysis of existing storage.

---

### 4.3 Schema Evolution & Migration Toolkit

**Problem:** As the event schema evolves (new fields, renamed fields, type changes), historical data becomes incompatible. The `SchemaVersionManager` tracks versions but there's no automated migration path. Old JSONL files with schema v2 can't be seamlessly read by code expecting schema v5.

**Proposal:** Build a schema migration toolkit:
- **Upcasters:** Register `ISchemaUpcaster` implementations (interface already exists) that transform old events to current schema
- **Migration CLI:** `--migrate-schema --from v2 --to v5 --symbols AAPL` — rewrite stored data with schema upgrades
- **Lazy migration:** When reading old data (replay, export), apply upcasters on-the-fly without rewriting files
- **Compatibility matrix:** `/api/schema/compatibility` — show which stored data files are on which schema version

**Impact:** High — prevents "data rot" as the project evolves.
**Effort:** M
**Risk:** Medium — rewriting stored data requires careful validation and backup.

---

## Area 5: Ecosystem & Integration

### 5.1 Webhook & Notification Framework

**Problem:** `ConnectionStatusWebhook` and `DailySummaryWebhook` exist but are hardcoded patterns. Users can't define custom alerts like "notify me on Slack when AAPL spread exceeds 5 cents" or "email me when backfill completes."

**Proposal:** Add a general-purpose notification framework:
- **Rule engine:** Define conditions as JSON rules: `{ "event": "spread_exceeded", "symbol": "AAPL", "threshold": 0.05, "action": "webhook" }`
- **Channels:** Webhook (generic), Slack (formatted), Email (SMTP), Desktop notification (WPF toast)
- **Templates:** Customizable message templates with variable substitution
- **Endpoint:** `/api/notifications/rules` — CRUD for notification rules
- **CLI:** `--add-alert "AAPL spread > 0.05" --notify slack`

**Impact:** High — transforms passive data collection into active monitoring.
**Effort:** L
**Risk:** Low — additive system, no changes to core pipeline.

---

### 5.2 Data Export to Cloud Storage

**Problem:** All data is stored locally. Users who want to feed data into cloud-based analytics (S3 + Athena, GCS + BigQuery, Azure Blob + Synapse) must manually copy files.

**Proposal:** Add cloud storage sink support:
- Implement `IStorageSink` for S3, GCS, and Azure Blob
- Use the existing `CompositeSink` to write simultaneously to local + cloud
- Support sync mode (near-real-time upload) and batch mode (periodic upload of completed files)
- Config: `"Storage": { "CloudSync": { "Provider": "S3", "Bucket": "my-market-data", "Prefix": "live/", "Mode": "batch" } }`

**Impact:** High — enables cloud-native analytics workflows.
**Effort:** L
**Risk:** Medium — cloud SDK dependencies, network failure handling.

---

### 5.3 QuantConnect Lean Tight Integration

**Problem:** Lean integration exists (`Integrations/Lean/`) but the data flow is one-directional and manual. Users must export data, convert formats, and configure Lean separately.

**Proposal:** Create a bidirectional Lean bridge:
- **Auto-export:** Continuously export collected data in Lean-compatible format to a configurable directory
- **Backtest trigger:** `/api/lean/backtest` — kick off a Lean backtest using collected data with results streamed back
- **Result ingestion:** Import Lean backtest results (trades, equity curve) and display alongside collected market data in the dashboard
- **Symbol mapping:** Automatic mapping between collector symbol format and Lean's `SecurityIdentifier`

**Impact:** Medium — high value for the quant research use case specifically.
**Effort:** L
**Risk:** Medium — depends on Lean Engine availability and version compatibility.

---

## Area 6: Performance & Scale

### 6.1 Tiered Memory Buffer with Spill-to-Disk

**Problem:** The `EventPipeline` uses bounded channels with a fixed capacity. Under burst load (market open, news events), the pipeline applies backpressure and may drop events. The current approach is "size the buffer large enough" which wastes memory during quiet periods.

**Proposal:** Implement a tiered buffer:
1. **Hot tier:** In-memory bounded channel (existing, fast, ~10K events)
2. **Warm tier:** Memory-mapped file buffer (~100K events, disk-backed but fast)
3. **Spill policy:** When the hot tier is 80% full, start spilling to warm tier; drain warm tier during quiet periods

This extends the existing `EventPipelinePolicy` presets — add a `BurstTolerant` preset that enables the warm tier.

**Impact:** Medium — prevents data loss during burst periods without wasting memory.
**Effort:** M
**Risk:** Medium — changes to the critical path require careful benchmarking.

---

### 6.2 Parallel Backfill Orchestration

**Problem:** Backfill runs sequentially — one symbol at a time, one provider at a time. A 50-symbol backfill across 5 years takes hours even when rate limits would allow parallelism.

**Proposal:** Add parallel backfill with intelligent rate-limit-aware scheduling:
- **Symbol parallelism:** Process N symbols concurrently (configurable, default 3)
- **Provider parallelism:** Use multiple providers simultaneously when one hits rate limits
- **Adaptive throttling:** Monitor 429 responses and dynamically adjust concurrency
- **Priority queue:** Process most-requested symbols first, allow user-defined priority

**Impact:** High — dramatically reduces backfill time for multi-symbol operations.
**Effort:** M
**Risk:** Medium — must respect rate limits carefully to avoid bans.

---

## Summary: Top 5 Recommendations

Ranked by impact-to-effort ratio:

| Rank | Proposal | Impact | Effort | Quick Win? |
|------|----------|--------|--------|------------|
| 1 | 2.3 Circuit Breaker Dashboard | Medium | S | Yes |
| 2 | 1.3 Cost-Per-Query Estimator | Medium | S | Yes |
| 3 | 4.2 Retention Compliance Reports | Medium | S | Yes |
| 4 | 2.1 Replay-Based Regression Testing | High | M | No |
| 5 | 1.2 Microstructure Event Annotations | High | M | No |

**Biggest game-changers** (higher effort but transformative):
- 5.1 Webhook & Notification Framework — turns passive into active
- 3.1 Data Catalog with Search — transforms discoverability
- 6.2 Parallel Backfill — unlocks practical large-scale historical data collection
