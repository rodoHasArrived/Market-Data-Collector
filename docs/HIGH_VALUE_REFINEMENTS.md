# Deterministic Canonicalization Across Providers (Suggestion 4)

This document focuses exclusively on suggestion 4 and provides an execution-ready plan for deterministic canonicalization across data providers.

## 4) Deterministic Canonicalization Across Providers

**Refinement**
Strengthen canonical normalization rules for timestamps, condition codes, venue IDs, and symbol mapping so equivalent events from different providers become structurally comparable.

**Expanded design direction**
- Define a **canonical event contract** per event family (`trade`, `quote`, `book_update`) with required/optional fields, valid ranges, and precision constraints.
- Standardize **time semantics** with explicit fields for `source_event_time`, `provider_receive_time`, and `collector_persist_time` (all UTC, nanosecond-capable where available).
- Introduce a deterministic **symbol identity layer** (`symbol`, `asset_class`, `venue`, `instrument_id`) to disambiguate ticker collisions and corporate action renames.
- Normalize **condition/qualifier codes** using a provider->canonical lookup table and preserve raw provider flags for auditability.
- Apply canonicalization as a versioned pipeline stage (`canonicalization_version`) so backtests can pin to a known transformation behavior.

**Why this matters**
- Better multi-provider reconciliation.
- Cleaner model training datasets.
- Easier provider substitution without downstream breakage.
- Fewer hidden data drift issues after provider API changes.
- Improves reproducibility when research outputs must be audited.

**Suggested scope**
- Publish canonical schema contract + versioning.
- Add reconciliation diagnostics (mismatch reason taxonomy).
- Add a "provider parity" dashboard view.
- Build an automated parity test suite with golden fixtures from at least 3 providers.
- Add a schema evolution policy (backward/forward compatibility rules + migration notes).

**Rollout plan**
1. **Phase 1: Contract + mapping inventory**
   - Document current provider field differences and map to canonical names/types.
   - Identify non-lossy transforms vs lossy transforms requiring explicit flags.
2. **Phase 2: Dual-write validation mode**
   - Persist both raw and canonical events for a subset of symbols.
   - Compare parity metrics before switching downstream consumers.
3. **Phase 3: Default canonical read path**
   - Serve canonical events by default while retaining raw passthrough for debugging.
   - Track mismatch rates and rollback thresholds.

**Acceptance criteria**
- >= 99.5% of equivalent cross-provider events map to the same canonical identity tuple.
- < 0.1% unresolved mapping rate for condition codes in liquid US equities universe.
- Canonicalization pipeline adds < 5% median ingest latency overhead.
- All canonical schema changes include version bump + migration note.

**Implementation blueprint (crystallized)**

**A. Pipeline components**
1. **Provider adapters** emit a raw envelope with immutable provider payload + metadata (`provider`, `stream`, `ingest_seq`, checksum).
2. **Canonicalization engine** applies deterministic mapping rules keyed by (`provider`, `event_type`, `schema_version`).
3. **Parity evaluator** compares canonical outputs from overlapping provider windows and emits mismatch diagnostics.
4. **Schema registry** tracks canonical contracts, version history, deprecation windows, and migration notes.
5. **Observability layer** publishes canonicalization success/failure counters, latency, and mismatch taxonomies.

**B. Canonical mapping rules (minimum contract)**
- **Identity fields**: `instrument_id`, `symbol`, `venue`, `asset_class`, `event_type`.
- **Time fields**: `source_event_time`, `provider_receive_time`, `collector_persist_time`, `clock_quality`.
- **Price/size normalization**: decimal precision harmonized per asset class with explicit `precision_source`.
- **Condition semantics**: `trade_condition[]` / `quote_condition[]` canonical enums + `raw_conditions[]` passthrough.
- **Traceability**: `source_provider`, `source_message_id`, `canonicalization_version`, `transform_flags[]`.

**C. Failure handling policy**
- **Hard-fail** records missing required identity/time fields -> route to dead-letter stream.
- **Soft-fail** records with non-critical unmapped qualifiers -> persist with warning flags.
- **Degraded mode** if unresolved mapping rate breaches threshold for 5+ minutes -> auto-fallback to raw-read for affected feeds.

**D. Test strategy**
- **Golden fixtures**: curated raw payloads for each provider/event type including edge cases (halts, crossed markets, odd lots, corporate actions).
- **Property tests**: idempotency (`canonicalize(canonicalize(x)) == canonicalize(x)`) and deterministic output for same input.
- **Drift canaries**: nightly parity checks on top-volume symbols; alert when mismatch class spikes beyond baseline.
- **Backward-compat tests**: previous canonical versions replayed against current engine for migration validation.

**E. Operational metrics to expose**
- `canonicalization_records_total{provider,event_type,status}`
- `canonicalization_latency_ms_bucket{provider,event_type}`
- `canonicalization_unresolved_mapping_rate{provider,field}`
- `provider_parity_mismatch_total{symbol,mismatch_class}`
- `canonical_schema_version_in_use{service}`

**F. Deliverables by milestone**
- **M1 (2 weeks)**: canonical schema v1 + mapping table for 2 providers (`trade`, `quote`).
- **M2 (2 weeks)**: dual-write + parity dashboard + golden fixture CI.
- **M3 (2 weeks)**: default canonical read path for pilot symbols + rollback automation.
- **M4 (2 weeks)**: broaden coverage to `book_update`, finalize schema evolution SOP.
