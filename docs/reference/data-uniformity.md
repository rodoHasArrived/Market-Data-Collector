# Data Uniformity and Usability Plan

> **Related:** [Deterministic Canonicalization Design](../architecture/deterministic-canonicalization.md) — detailed design for cross-provider symbol resolution, condition code mapping, and venue normalization.

This note expands on the data-quality goals for the collector so downstream users receive a uniform, analysis-ready tape regardless of provider quirks.

## Implementation status summary

| Feature | Status | Key class |
|---------|--------|-----------|
| Canonical envelope fields | ✅ Done | `MarketEvent` (`CanonicalSymbol`, `CanonicalVenue`, `CanonicalizationVersion`) |
| Symbol mapping in ingestion | ✅ Done | `CanonicalSymbolRegistry` + `CanonicalizingPublisher` |
| Condition code normalization | ✅ Done | `ConditionCodeMapper` + `CanonicalTradeCondition` enum |
| Venue normalization (ISO MIC) | ✅ Done | `VenueMicMapper` with `config/venue-mapping.json` |
| Clock skew estimation | ✅ Done | `ClockSkewEstimator` (EWMA per provider) |
| Schema validation | ✅ Done | `EventSchemaValidator` (contract validation at ingestion) |
| Manifest files | ✅ Done | `PackageManifest` with per-file metadata |
| Downstream-readiness score | ✅ Done | `DataQualityScoringService` (multi-dimension, A–F grades) |
| Quarantine channel | ⚠️ Partial | Naming convention recognized; auto-routing not yet wired |
| Replay filtering API | ⚠️ Partial | `JsonlReplayer` present; filter parameters not yet exposed |

## Current implementation snapshot (2026-02-25)
* **Envelope fields:** `MarketEvent` now includes `CanonicalSymbol`, `CanonicalVenue`, `CanonicalizationVersion`, `ExchangeTimestamp`, `ReceivedAtUtc`, and `ReceivedAtMonotonic` alongside the original `Symbol`, `Source`, `Timestamp`, `Type`, `Payload`, `Sequence`, `SchemaVersion`, and `Tier`.
* **Schema evolution:** Payload records are versioned via `schemaVersion` at the event level; per-payload version fields remain a future enhancement.
* **Symbol mapping:** `CanonicalSymbolRegistry` resolves symbols by alias, ISIN, FIGI, SEDOL, CUSIP, or provider ticker and is wired into the `CanonicalizingPublisher` decorator so ingestion emits both raw and canonical symbols.
* **Retention and manifests:** Retention policies are active. `PackageManifest` writes per-file metadata (path, event count, checksum, timestamp range, quality score) alongside data exports. `DataQualityScoringService` computes composite readiness scores exposed in the status dashboard.

## Canonical JSONL schema
* **Single envelope:** Persist `MarketEvent` with consistent envelope fields for every row so downstream tools do not need per-provider readers.
* **Typed payloads:** Keep `Trade`, `BboQuote`, `LOBSnapshot`, and `OrderFlowStatistics` payloads stable with explicit version fields to support gradual schema evolution.
* **Nullable fields:** Represent missing provider values explicitly (`null` for sequence/venue/stream) instead of omitting fields; keeps JSON column order consistent for parquet conversion.

## Metadata and identifiers
* **Provider provenance** ✅ — `MarketEvent.Source` carries the provider name on every row; `CanonicalizingPublisher` preserves both raw and canonical values.
* **Symbol mapping registry** ✅ — `CanonicalSymbolRegistry` resolves provider symbols → canonical identifiers (ISIN/FIGI/alias). The [Deterministic Canonicalization](../architecture/deterministic-canonicalization.md) design describes how this populates `CanonicalSymbol` on the `MarketEvent` envelope while preserving the raw `Symbol`.
* **Clock domains** ✅ — `ExchangeTimestamp`, `ReceivedAtUtc`, and `ReceivedAtMonotonic` fields on `MarketEvent` are populated. `ClockSkewEstimator` tracks per-provider drift using EWMA. A `ClockQuality` enum to qualify timestamp trustworthiness remains a future enhancement.
* **Condition code normalization** ✅ — `ConditionCodeMapper` maps provider-specific codes to `CanonicalTradeCondition` using `config/condition-codes.json`. See [condition code mapping](../architecture/deterministic-canonicalization.md#c-condition-code-mapping).
* **Venue normalization** ✅ — `VenueMicMapper` normalizes venue identifiers to ISO 10383 MIC codes using `config/venue-mapping.json`. See [venue normalization](../architecture/deterministic-canonicalization.md#d-venue-normalization).

## Precision, units, and currencies
* **Decimals for prices:** Store prices as `decimal` in code and stringified decimals in JSON to avoid floating-point drift during parquet/duckdb conversion.
* **Unit documentation:** Standardize on quote-currency prices and size in whole units (not lots); document exceptions per venue in metadata and integrity tags.
* **Currency context:** Emit a `quoteCurrency` field for multi-currency feeds (e.g., crypto vs. equities) and default to `USD` when unknown to make aggregation consistent.

## Validation and integrity tagging
* **Schema validators** ✅ — `EventSchemaValidator` enforces contract validation (timestamp, symbol, type, schema version, payload presence) at ingestion before events reach the pipeline.
* **Integrity codes** ✅ — Machine-readable codes (`SEQ_GAP`, `SEQ_OOO`, `DEPTH_STALE`, `UNKNOWN_SYMBOL`, `CLOCK_DRIFT`) are emitted as `IntegrityEvent` payloads and tracked in dashboards.
* **Quarantine channel** ⚠️ Partial — The storage layer recognizes `_quarantine/` directory naming convention, but automatic routing of critically-failed events to a quarantine sink is not yet wired.

## File organization and retention
* **Folder conventions:** Default to `YYYY/MM/DD/<provider>/<symbol>.jsonl` for `ByDate` and `<symbol>/YYYY/MM/DD.jsonl` for `BySymbol`; record the active convention in metadata and the dashboard.
* **Rotation and retention:** Rotate files hourly to bound file sizes; pair with retention policies per provider/symbol so storage stays predictable.
* **Manifest files:** Write a small manifest (`manifest.json`) alongside each partition listing file paths, counts, min/max timestamps, and integrity stats to speed up downstream discovery.

## Observability and quality metrics
* **Uniform counters** ✅ — `PrometheusMetrics` exports row counts, dropped rows, integrity events by code, and per-symbol arrival metrics. Mirrored in `/api/status` and the web dashboard.
* **Data freshness** ✅ — `DataFreshnessSlaMonitor` tracks max timestamp per symbol/provider and surfaces time-since-last-event for stalled feed detection.
* **Downstream-readiness score** ✅ — `DataQualityScoringService` computes a multi-dimension composite score (completeness, sequence integrity, freshness, format quality, source reliability) with A–F grades. Exposed in manifests via `PackageManifest.Quality` and in the status UI.

## Replay and interoperability
* **Replay filters** ⚠️ Partial — `JsonlReplayer` and `MemoryMappedJsonlReader` support JSONL replay with configurable chunk/batch sizes. Provider/symbol/time-window filter parameters are not yet exposed in the public API.
* **Columnar exports** ✅ — `ParquetStorageSink` and `AnalysisExportService` produce Parquet exports with stable column order matching the canonical schema. Arrow and XLSX export formats are also supported.
* **Compatibility adapters** — Portable data packages include SQL loader scripts and import helpers. Dedicated pandas/polars/duckdb adapters are a future enhancement.

## Governance and evolution
* **Versioned schemas** ✅ — `SchemaVersionManager` tracks schema versions and coordinates migrations. `MarketEvent.SchemaVersion` tags every event.
* **Contract tests** ✅ — `ContractVerificationService` validates provider implementations against canonical contracts. Integration endpoint tests in `tests/MarketDataCollector.Tests/Integration/` verify schema compliance.
* **Config-driven rollout** ✅ — Canonicalization is enabled via `CanonicalizationConfig` with `PilotSymbols` list and `EnableDualWrite` flag. Operators can gradually roll out new normalization rules without disrupting existing pipelines.
