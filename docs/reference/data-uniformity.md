# Data Uniformity and Usability Plan

This note expands on the data-quality goals for the collector so downstream users receive a uniform, analysis-ready tape regardless of provider quirks.

## Canonical JSONL schema
* **Single envelope:** Persist `MarketEvent` with fields `{ timestamp, provider, symbol, venue, streamId, sequence, eventType, payload, integrity }` in every row so downstream tools do not need per-provider readers.
* **Typed payloads:** Keep `Trade`, `BboQuote`, `LOBSnapshot`, and `OrderFlowStatistics` payloads stable with explicit version fields to support gradual schema evolution.
* **Nullable fields:** Represent missing provider values explicitly (`null` for sequence/venue/stream) instead of omitting fields; keeps JSON column order consistent for parquet conversion.

## Metadata and identifiers
* **Provider provenance:** Include `provider` and optional `connectorId` on every row to make side-by-side comparisons and reconciliation easier.
* **Symbol mapping registry:** Maintain a mapping table of provider symbols → canonical symbols (ISIN/FIGI where available) and apply it during ingestion; emit both the canonical and raw values when mappings exist.
* **Clock domains:** Persist both provider and collector timestamps when available so analysts can measure latency and clock skew.

## Precision, units, and currencies
* **Decimals for prices:** Store prices as `decimal` in code and stringified decimals in JSON to avoid floating-point drift during parquet/duckdb conversion.
* **Unit documentation:** Standardize on quote-currency prices and size in whole units (not lots); document exceptions per venue in metadata and integrity tags.
* **Currency context:** Emit a `quoteCurrency` field for multi-currency feeds (e.g., crypto vs. equities) and default to `USD` when unknown to make aggregation consistent.

## Validation and integrity tagging
* **Schema validators:** Add JSON schema checks per payload type to reject malformed rows before persistence and to surface reasons in integrity events.
* **Integrity codes:** Standardize machine-readable codes (`SEQ_GAP`, `SEQ_OOO`, `DEPTH_STALE`, `UNKNOWN_SYMBOL`, `CLOCK_DRIFT`) with human-readable messages so filters and dashboards can group issues.
* **Quarantine channel:** Route events with critical integrity failures to a `quarantine` sink while still persisting them with flags for forensic analysis.

## File organization and retention
* **Folder conventions:** Default to `YYYY/MM/DD/<provider>/<symbol>.jsonl` for `ByDate` and `<symbol>/YYYY/MM/DD.jsonl` for `BySymbol`; record the active convention in metadata and the dashboard.
* **Rotation and retention:** Rotate files hourly to bound file sizes; pair with retention policies per provider/symbol so storage stays predictable.
* **Manifest files:** Write a small manifest (`manifest.json`) alongside each partition listing file paths, counts, min/max timestamps, and integrity stats to speed up downstream discovery.

## Observability and quality metrics
* **Uniform counters:** Export Prometheus metrics for row count, dropped rows, integrity events by code, and per-symbol late/early arrival; mirror the same fields into `/status` and the dashboard.
* **Data freshness:** Track `maxTimestamp` per symbol/provider and surface time since last event to highlight stalled feeds.
* **Downstream-readiness score:** Compute a simple score per partition (e.g., `1 - integrityEvents/totalRows`) and expose it in manifests and the UI for quick triage.

## Replay and interoperability
* **Replay filters:** Allow JSONL replays filtered by provider, symbol, time window, and integrity code so analysts can reproduce issues without manual slicing.
* **Columnar exports:** Provide first-class parquet exports with stable column order matching the canonical schema to keep analyst tools fast.
* **Compatibility adapters:** Ship lightweight adapters for common research stacks (pandas/duckdb/polars) that apply the same schema, units, and integrity decoding.

## Governance and evolution
* **Versioned schemas:** Version the canonical schema and payloads; publish changelogs and migration helpers in docs so downstream users can adapt safely.
* **Contract tests:** Add integration tests that validate new providers against the canonical schema, symbol mapping, and integrity codes before deployment.
* **Config-driven rollout:** Allow operators to enable new fields or stricter validators via config flags with safe defaults to avoid breaking existing pipelines.
