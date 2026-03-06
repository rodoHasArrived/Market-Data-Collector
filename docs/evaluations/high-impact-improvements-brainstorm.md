# High-Impact Improvements Brainstorm (Effort Ignored)

**Date:** 2026-03-02  
**Status:** Active — Expanded and In Review  
**Author:** Architecture Review  
**Scope:** Ideas prioritized purely by potential upside to product quality, data value, and long-term competitiveness.  
**Constraint:** Implementation cost and complexity are intentionally ignored.

---

## Executive Summary

This document captures the highest-impact architectural and product improvements for the Market Data Collector platform, ordered by long-term strategic value rather than implementation cost. It serves as a north-star reference for investment decisions, roadmap planning, and architecture decisions.

**Key findings:**

- The repository has strong foundations in streaming ingestion, multi-provider support, storage tiering, and data quality monitoring, but operates primarily as a _collector_ rather than a trustable _system of record_.
- The five highest-impact investments — deterministic replay, multi-provider consensus, unified serving contract, automated self-healing, and explainable diagnostics — form a compounding stack where each layer multiplies the value of the next.
- Ten further improvements across storage intelligence, compliance, feature engineering, and provider routing would collectively shift the platform from infrastructure tooling to a strategic market-data intelligence platform.

**Document structure:**

- Sections 1–6: Categorized idea inventory
- Section 7: Impact-only prioritization
- Section 8: Deep expansion of the top five ideas
- Section 9: Strategic sequencing rationale
- Sections 10–12: Additional ideas and cross-cutting capabilities
- Section 13: Current state gap analysis and integration map _(new)_
- Section 14: Phased implementation roadmap _(new)_
- Section 15: Risk factors and mitigation strategies _(new)_
- Section 16: Open questions for architecture decisions _(new)_

---

## 1) Build a "truth-grade" market data platform (not just a collector)

### 1.1 Deterministic event ledger with full replay semantics
- Upgrade ingestion to an immutable, append-only event ledger where every normalized event has:
  - Canonical payload hash
  - Provider raw payload hash
  - Ingestion timestamp + exchange timestamp
  - Versioned normalization ruleset identifier
- Add one-command replay of any time range with bit-for-bit reproducibility guarantees.
- Outcome: Enables forensic debugging, regulator-grade auditability, and ML reproducibility.

### 1.2 Multi-provider consensus engine
- For overlapping symbols/providers, compute a real-time confidence score per event.
- Detect and annotate provider disagreement (price outliers, missing prints, sequence divergence).
- Persist "consensus truth" and "provider deltas" as separate streams.
- Outcome: Substantially improved data trust and easier provider quality benchmarking.

### 1.3 Time-correctness service
- Introduce a dedicated service for timestamp integrity:
  - Clock skew estimation per provider
  - Exchange session-aware ordering repair
  - Late-event correction windows
- Outcome: Significantly cleaner downstream analytics and fewer false anomaly alerts.

---

## 2) Make data quality a first-class product surface

### 2.1 Data quality control plane
- Add a dedicated control plane and UI for quality state:
  - Completeness, timeliness, monotonicity, and cross-provider consistency SLOs
  - Symbol-level quality heatmaps
  - Provider incident timelines
- Outcome: Moves quality from logs/metrics into decision-ready operational tooling.

### 2.2 Built-in "quality contracts" for each provider and asset class
- Introduce declarative contracts (expected fields, latency ranges, session coverage, condition-code behavior).
- Enforce at runtime with automatic quarantine modes for failing streams.
- Outcome: Prevents silent degradation and protects downstream users from poisoned data.

### 2.3 Automated correction workflows
- Add policy-driven auto-remediation:
  - Trigger targeted backfills on detected gaps
  - Reconcile discrepancies via secondary providers
  - Mark confidence tiers for repaired windows
- Outcome: Turns passive monitoring into active self-healing.

---

## 3) Leap from collector to market data operating system

### 3.1 Strategy-facing feature store
- Generate and persist derived features in near real time:
  - Microstructure features (spread regime, trade intensity, imbalance)
  - Regime labels (volatility/liquidity states)
  - Session-aware aggregates
- Outcome: Makes the platform immediately useful for research, modeling, and live strategy inputs.

### 3.2 Query engine + semantic API
- Add a high-level query layer for ticks, bars, order book snapshots, and derived features.
- Support time-travel queries and composable filters via HTTP + SDK.
- Outcome: Dramatically better user experience than file-level retrieval.

### 3.3 Unified historical + real-time serving layer
- Serve the exact same schema/contract for backtest and live subscriptions.
- Include deterministic stitching around session boundaries and corporate actions.
- Outcome: Reduces train/serve skew and improves strategy deployment reliability.

---

## 4) Extreme reliability and resilience posture

### 4.1 Active-active regional architecture
- Run independent collectors across regions/providers with quorum validation.
- Automatic failover based on freshness + quality, not just connection status.
- Outcome: Meaningfully higher availability for mission-critical ingestion.

### 4.2 Chaos and fault-injection framework
- Native simulation suite for network jitter, provider throttling, malformed payloads, and burst storms.
- Continuous resilience scoring in CI and staging.
- Outcome: Surfaces systemic fragility before production incidents.

### 4.3 End-to-end exactly-once delivery (where feasible)
- Strengthen idempotency, sequence guarantees, and dedup semantics across WAL, queues, and sinks.
- Outcome: Minimizes duplicate or missing records under failure/restart conditions.

---

## 5) Transform usability for operators and developers

### 5.1 "Explain my system" diagnostics assistant
- Add a built-in diagnostics mode that answers:
  - Why a symbol is missing
  - Why a provider is degraded
  - Why freshness/SLO alerts are firing
- Output should include root-cause graph + suggested remediation steps.
- Outcome: Faster MTTR and lower dependence on expert maintainers.

### 5.2 Declarative orchestration DSL
- Replace scattered config complexity with declarative intent:
  - Desired providers, symbols, quality guarantees, and failover policy
- Compiler validates and materializes executable runtime plans.
- Outcome: Safer config changes and easier large-scale operations.

### 5.3 Full digital-twin simulation mode
- Run the entire platform against synthetic but realistic market regimes (normal, illiquid, flash-crash).
- Validate behavior, alerts, and storage outcomes before live deployment.
- Outcome: Confident releases with quantified risk reduction.

---

## 6) Competitive differentiation opportunities

### 6.1 Provider scorecards and procurement analytics
- Continuously rate providers on latency, completeness, error rates, and recovery behavior.
- Track cost-versus-quality over time and surface replacement recommendations.
- Outcome: Converts technical telemetry into business-level decision support.

### 6.2 Corporate actions + symbol identity graph
- Build a canonical identity graph across symbol changes, splits, mergers, venue aliases, and provider symbol variants.
- Outcome: Better longitudinal correctness and easier multi-year analytics.

### 6.3 Verifiable data lineage and provenance exports
- Export signed lineage bundles proving event origin, transformation chain, and storage integrity.
- Outcome: Higher trust for external consumers and regulated workflows.

---

## 7) Suggested prioritization if "impact-only" is the rule

1. **Deterministic event ledger + replay semantics**  
2. **Multi-provider consensus and quality control plane**  
3. **Unified historical/live serving contract**  
4. **Automated correction/self-healing workflows**  
5. **Diagnostics assistant with root-cause explanations**

These five together would most strongly increase trust, operability, and strategic value of the repository's output.

---

## 8) Deep expansion of the highest-impact five ideas

### 8.1 Deterministic event ledger + replay semantics (highest leverage)

**Why this is likely #1:** It upgrades the repository from "best-effort ingestion" to "provable system of record." Nearly every other improvement (quality scoring, anomaly triage, model reproducibility, audits) gets stronger when replay is deterministic.

**Expanded capability set:**
- **Immutable dual-layer storage:** Persist both raw provider frames and canonicalized events, each with stable identifiers and cryptographic hashes.
- **Transformation lineage:** Attach rule-version IDs for every canonicalization step so historical outputs can be traced to exact logic.
- **Replay contracts:** Support deterministic rehydration by date/symbol/provider with strict ordering guarantees and deterministic handling of late/out-of-order events.
- **Diff tooling:** Provide side-by-side comparison between original run and replayed run at event, aggregate, and quality-metric levels.

**What success looks like:**
- Any historical output can be regenerated bit-for-bit from ledger + ruleset.
- "Why did this number change?" becomes a deterministic query, not a forensic investigation.
- Backtesting and production analytics share a common auditable origin.

**North-star metrics:**
- Replay determinism pass rate (target: ~100%)
- Percentage of events with complete lineage metadata
- Mean time to root-cause historical discrepancies

### 8.2 Multi-provider consensus + quality control plane

**Why this is likely #2:** It directly upgrades trust in outputs by moving from single-source assumptions to evidence-based data confidence.

**Expanded capability set:**
- **Consensus scoring engine:** Compute confidence per event/window using agreement across providers, recency, and known provider reliability history.
- **Disagreement taxonomy:** Classify mismatches (price outlier, missing trade, sequence divergence, stale stream, venue mismatch).
- **Control plane views:** Real-time quality heatmaps by symbol/provider/session; incident timelines; quality trend decomposition.
- **Action coupling:** Quality states trigger policies (warn-only, partial quarantine, failover, targeted backfill).

**What success looks like:**
- Consumers can choose "best-effort," "high-confidence," or "strict-consensus" data modes.
- Provider incidents are measurable and comparable instead of anecdotal.
- On-call teams get quality-aware operational guidance in real time.

**North-star metrics:**
- High-confidence coverage ratio by symbol/time window
- Time-to-detect and time-to-mitigate provider degradation
- False-positive/false-negative rates for quality incidents

### 8.3 Unified historical/live serving contract

**Why this is likely #3:** It eliminates one of the biggest practical failure modes in quant/data systems: train-serve skew between research and production.

**Expanded capability set:**
- **Single canonical schema:** One event contract for both replayed history and live stream delivery.
- **Deterministic stitch logic:** Consistent behavior across session boundaries, halts, reopenings, and corporate action transitions.
- **Query parity:** Same query semantics (filters, windows, ordering) for live and historical APIs.
- **Compatibility guarantees:** Versioned contract evolution with migration/deprecation policy.

**What success looks like:**
- Strategies validated on historical data behave consistently when switched to live feeds.
- Integration burden drops because consumers integrate once.
- Regression testing can compare live and replayed windows with minimal glue code.

**North-star metrics:**
- Live-vs-historical parity score on sampled windows
- Number of consumer adapters required across modes (target: 1)
- Production incidents attributable to schema/semantic skew

### 8.4 Automated correction and self-healing workflows

**Why this is likely #4:** It turns the platform from monitoring-heavy to outcome-oriented by actively repairing data quality issues.

**Expanded capability set:**
- **Policy engine:** Declarative rules for when and how to remediate gaps, drift, latency spikes, and provider outages.
- **Targeted remediation primitives:** Symbol/time-window scoped backfill, provider substitution, reconciliation merge, confidence relabeling.
- **Closed-loop verification:** Post-remediation checks prove the issue is resolved and record quality deltas.
- **Safety controls:** Budget caps, blast-radius limits, and escalation thresholds to prevent over-correction.

**What success looks like:**
- Most quality incidents are corrected automatically before consumers notice.
- Manual interventions are reserved for edge cases and policy tuning.
- Historical quality steadily improves over time through continuous repair.

**North-star metrics:**
- Auto-remediation success rate
- Percentage of incidents resolved without human intervention
- Time from detection to restored quality SLO

### 8.5 Explainable diagnostics assistant

**Why this is likely #5:** It compounds every technical investment by making system behavior understandable to operators and consumers, dramatically reducing MTTR and organizational dependency on experts.

**Expanded capability set:**
- **Root-cause graph generation:** Correlate provider state, queue pressure, reconnect events, schema validation failures, and quality anomalies.
- **Question-oriented diagnostics:** First-class commands like "why is symbol X stale?" or "why did confidence drop at 09:47?"
- **Remediation playbooks:** Contextual recommendations with confidence levels and expected blast radius.
- **Incident memory:** Capture resolved incidents into reusable diagnostics patterns.

**What success looks like:**
- New operators can resolve non-trivial incidents without tribal knowledge.
- Incident reports become structured and comparable over time.
- Operational learning accumulates rather than resetting each outage.

**North-star metrics:**
- MTTR reduction for top incident classes
- Percent of incidents resolved via assistant-guided workflow
- Repeat incident rate after playbook adoption

---

## 9) Strategic sequencing when optimizing for impact only

If the only criterion is eventual upside (not effort), sequence these as:

1. **Deterministic ledger + replay** (foundation of trust and reproducibility)  
2. **Consensus + control plane** (foundation of confidence and quality governance)  
3. **Unified historical/live contract** (foundation of consumer reliability)  
4. **Automated self-healing** (foundation of resilient outcomes)  
5. **Explainable diagnostics** (foundation of scalable operations)

This order builds compounding value: each layer increases the return of the next.

---

## 10) Additional high-impact improvements and expanded functionality

### 10.1 Real-time market state intelligence layer

**Purpose:** Move beyond event capture into continuously computed market-state awareness.

**Expanded functionality:**
- **State machine per symbol:** Explicit states for normal, opening auction, halt, reopen, volatility interruption, and close transition.
- **Regime transitions:** Detect liquidity and volatility regime shifts in real time and tag downstream events with regime context.
- **Cross-symbol contagion detection:** Identify correlated stress propagation (sector/ETF constituents/index members).
- **Actionable outputs:** Emit state-change webhooks and machine-readable incident annotations.

**Impact:** Consumers gain context-rich, decision-grade feeds rather than raw stream data alone.

### 10.2 Unified order-book and trade microstructure reconstruction

**Purpose:** Elevate analytical value by reconstructing high-fidelity market microstructure from heterogeneous provider streams.

**Expanded functionality:**
- **Canonical L1/L2/L3 model adapters:** Normalize book depth updates and trades from each provider into a common temporal model.
- **Gap-aware reconstruction:** Flag confidence degradation when snapshots/increments are missing, and attempt deterministic rebuild.
- **Derived microstructure metrics:** Queue imbalance, spread elasticity, order-flow toxicity proxies, and short-horizon impact estimates.
- **Research export modes:** Deterministic snapshots for simulation, replay, and model training.

**Impact:** Enables advanced execution research and real-time alpha diagnostics.

### 10.3 Policy-driven compliance and governance framework

**Purpose:** Make governance verifiable and automatic, not ad hoc.

**Expanded functionality:**
- **Data retention policy engine:** Instrument-level and jurisdiction-aware retention/expiry controls.
- **Usage governance:** Tag datasets by allowed usage class (internal analytics, external redistribution, restricted).
- **Audit evidence bundles:** One-click export of lineage, access history, transform versions, and policy evaluations.
- **Continuous compliance checks:** Raise incidents when configuration/runtime behavior drifts from declared policy.

**Impact:** Reduces legal and operational risk while increasing institutional trust.

### 10.4 Intelligent storage tiering and query acceleration fabric

**Purpose:** Turn storage into a strategic capability for both latency-sensitive and deep historical workflows.

**Expanded functionality:**
- **Adaptive tiering:** Hot/warm/cold movement based on symbol activity, access frequency, and query patterns.
- **Columnar acceleration indexes:** Time/symbol/provider/venue composite indexes tuned for common research and operations queries.
- **Materialized semantic views:** Precompute common datasets (NBBO-like views, session aggregates, anomaly windows).
- **Cost-aware planner:** Route queries to optimal data tier and format with transparent performance/cost diagnostics.

**Impact:** Faster insights, better operator experience, and improved economics at scale.

### 10.5 Risk-aware ingestion and adaptive rate orchestration

**Purpose:** Keep ingestion quality stable under provider limits, market bursts, and infrastructure pressure.

**Expanded functionality:**
- **Adaptive subscription prioritization:** Dynamically prioritize symbols/feeds based on configured business criticality and data confidence.
- **Backpressure intelligence:** Predict imminent overload and preemptively reshape intake before data loss.
- **Provider budget optimization:** Allocate request/websocket budgets across providers to maximize confidence-weighted coverage.
- **Graceful degradation profiles:** Explicit modes (critical-only, essential, full) with deterministic transitions.

**Impact:** Higher resilience during volatile sessions and better preservation of high-value data.

### 10.6 Self-optimizing provider routing and failover planner

**Purpose:** Make provider selection a continuous optimization problem rather than static configuration.

**Expanded functionality:**
- **Real-time routing scores:** Compute live provider suitability per symbol using latency, error rates, freshness, and disagreement risk.
- **Predictive failover:** Use trend signals to preemptively reroute before hard failure.
- **Blended sourcing:** Combine providers by use case (e.g., one for speed, another for correction confidence).
- **Outcome tracking:** Compare routing decisions with downstream quality outcomes to continuously improve policies.

**Impact:** Better data quality and uptime with less manual operations tuning.

### 10.7 Data product packaging and contract marketplace

**Purpose:** Convert internal data streams into reusable, clearly contracted products.

**Expanded functionality:**
- **Versioned data product definitions:** Declare schemas, SLOs, coverage windows, and confidence guarantees.
- **Consumer-specific packaging:** Generate tailored feeds (research, monitoring, strategy, external client) from shared canonical assets.
- **Backward-compatibility enforcement:** CI gates and migration tooling for contract-safe evolution.
- **Entitlement-aware serving:** Runtime policy checks tied to roles, use cases, and contractual obligations.

**Impact:** Scales adoption across teams and external users without losing consistency or control.

---

## 11) Expanded cross-cutting capabilities to amplify all ideas

### 11.1 Universal confidence model
- Assign confidence not just to events, but to aggregates, derived features, and API responses.
- Provide confidence decomposition (source reliability, timestamp quality, reconstruction certainty, correction history).
- Let consumers set minimum confidence thresholds as part of query/stream contracts.

### 11.2 Scenario-aware simulation and shadow production
- Run proposed configuration/routing/policy changes in shadow mode against live traffic.
- Compute counterfactual quality and reliability outcomes before rollout.
- Persist scenario performance to build institutional decision memory.

### 11.3 Intelligent anomaly triage pipeline
- Cluster anomalies by root-cause signatures and suppress duplicate alerts.
- Estimate business impact (symbols affected, duration, confidence drop magnitude).
- Auto-generate incident narratives with timeline, likely cause, and remediation candidates.

### 11.4 Human-in-the-loop control workflows
- Support operator approvals for high-blast-radius automated actions.
- Present expected impact simulations before execution.
- Capture post-action outcomes to improve future policy recommendations.

### 11.5 System-wide explainability and provenance APIs
- Add explainability endpoints for all major outputs: "why this value," "which sources," "which transforms," "which corrections."
- Include machine-readable provenance trees for audit and model-governance pipelines.
- Enable exportable evidence packets for stakeholders beyond engineering.

---

## 12) Additional impact-only priorities (if scope expands)

After the original top five, the next highest-upside additions are:

6. **Self-optimizing provider routing and predictive failover**  
7. **Real-time market state intelligence layer**  
8. **Unified order-book microstructure reconstruction**  
9. **Intelligent storage tiering + semantic query acceleration**  
10. **Policy-driven compliance and provenance evidence framework**

Together, these additions would further shift the repository from a robust collector into a strategic market-data intelligence platform.

---

## 13) Current state gap analysis and integration map

This section maps each major proposal to what already exists in the codebase and what net-new work is required. References point to the primary implementation files.

### 13.1 Deterministic event ledger (idea 1.1 / 8.1)

| Dimension | Current state | Gap |
| --- | --- | --- |
| Immutable append-only storage | `WriteAheadLog` provides durability; `JsonlStorageSink` + `ParquetStorageSink` write events. Sinks are not treated as an immutable ledger. | Need cryptographic hashing per event and a sealed ledger API that forbids mutation. |
| Canonical payload hash | Not computed at ingestion. | Add hash computation in `EventCanonicalizer` / `CanonicalizingPublisher`. |
| Normalization ruleset versioning | `ConditionCodeMapper` and `VenueMicMapper` have no version identifier exposed downstream. | Tag each event with the rule version IDs used during canonicalization. |
| Deterministic replay | `JsonlReplayer` replays files but does not guarantee ordering across providers or bit-for-bit reproducibility. | Add a replay contract enforcing sequence ordering and deterministic late-event handling. |

**Existing leverage points:** `WriteAheadLog`, `JsonlReplayer`, `EventCanonicalizer`, `CanonicalizingPublisher`, `MarketEvent` (extend with `CanonicalHash` and `RulesetVersion` fields).

---

### 13.2 Multi-provider consensus engine (idea 1.2 / 8.2)

| Dimension | Current state | Gap |
| --- | --- | --- |
| Multi-provider ingestion | `FailoverAwareMarketDataClient` and `CompositeHistoricalDataProvider` support multiple providers. | These route/fallback; they do not fan-out and compare outputs from multiple providers simultaneously. |
| Disagreement detection | `CrossProviderComparisonService` exists in the data quality layer. | Needs to operate in real time, not just as a batch quality metric. |
| Confidence scoring | `DataQualityMonitoringService` tracks quality metrics but does not produce per-event confidence scores. | Extend to emit confidence annotations on the `MarketEvent` payload. |
| Consensus stream persistence | Not implemented. | Requires a new "consensus truth" sink alongside existing symbol sinks. |

**Existing leverage points:** `DataQualityMonitoringService`, `CrossProviderComparisonService`, `FailoverAwareMarketDataClient`, `EventPipeline`.

---

### 13.3 Unified historical/live serving contract (idea 3.3 / 8.3)

| Dimension | Current state | Gap |
| --- | --- | --- |
| Live streaming | `IMarketDataClient` provides WebSocket-based live streams. | Contract (event types, field set) differs from historical output. |
| Historical serving | `HistoricalDataQueryService` + `JsonlReplayer` provide file-based replay. | No HTTP query layer with time-travel and composable filters. |
| Schema parity | `MarketEvent` is used for live events; `HistoricalBar` for historical. | Needs a single canonical event contract spanning both modes. |
| Session-boundary stitching | Not implemented. | Corporate actions and session transitions need deterministic handling in the serving layer. |

**Existing leverage points:** `IMarketDataClient`, `HistoricalDataQueryService`, `JsonlReplayer`, `MarketEvent`, `HistoricalBar`, `UiApiRoutes` (for new query endpoints).

---

### 13.4 Automated correction and self-healing (idea 2.3 / 8.4)

| Dimension | Current state | Gap |
| --- | --- | --- |
| Gap detection | `GapAnalyzer` in `DataQuality/` detects gaps. | Detection does not automatically trigger remediation. |
| Targeted backfill | `GapBackfillService` can backfill specific windows. | Not connected to quality alerts; no policy-driven auto-trigger. |
| Remediation policies | Not implemented. | Need a declarative policy engine specifying when to auto-remediate, with budget and blast-radius caps. |
| Post-remediation verification | Not implemented. | After backfill, quality score for the repaired window should be re-evaluated and recorded. |

**Existing leverage points:** `GapAnalyzer`, `GapBackfillService`, `HistoricalBackfillService`, `DataQualityMonitoringService`, `BackfillScheduleManager`.

---

### 13.5 Explainable diagnostics assistant (idea 5.1 / 8.5)

| Dimension | Current state | Gap |
| --- | --- | --- |
| Diagnostic data collection | `DiagnosticBundleService` collects system state snapshots. | Outputs raw state rather than question-oriented root-cause analysis. |
| Error correlation | `ErrorTracker` and `ErrorRingBuffer` capture errors. | No causality graph linking provider state, queue pressure, and quality events. |
| Operator-facing diagnostics | `DetailedHealthCheck`, `StatusSnapshot`, `StatusWriter`. | Rich data exists but is not surfaced through a "why" query interface. |
| Remediation playbooks | Not implemented. | Need structured playbook storage tied to incident pattern matching. |

**Existing leverage points:** `DiagnosticBundleService`, `ErrorTracker`, `DetailedHealthCheck`, `PreflightChecker`, `SystemHealthChecker`, `DiagnosticsCommands` CLI.

---

### 13.6 Real-time market state intelligence (idea 10.1)

| Dimension | Current state | Gap |
| --- | --- | --- |
| Market state tracking | `TradingCalendar` provides session/holiday awareness. | No per-symbol state machine for halt/reopen/auction transitions. |
| Regime detection | `ProviderDegradationScorer` scores providers. | No volatility/liquidity regime detection for market data itself. |
| Cross-symbol contagion | Not implemented. | Requires symbol-group models (ETF/constituent, sector) with correlation tracking. |

**Existing leverage points:** `TradingCalendar`, `SpreadMonitor`, `ProviderDegradationScorer`, `MarketDepthCollector`.

---

### 13.7 Policy-driven compliance and governance (idea 10.3)

| Dimension | Current state | Gap |
| --- | --- | --- |
| Data retention | `LifecyclePolicyEngine` enforces retention and tier migration. | No jurisdiction-aware or instrument-level policy distinctions. |
| Lineage tracking | `DataLineageService` tracks file-level lineage. | No signed, exportable evidence bundles for audit. |
| Usage governance | Not implemented. | No usage-class tagging or entitlement-aware serving. |

**Existing leverage points:** `LifecyclePolicyEngine`, `DataLineageService`, `PortableDataPackager`, `MetadataTagService`.

---

## 14) Phased implementation roadmap

This roadmap groups proposals into three phases ordered by strategic dependency. Each phase builds the foundation that makes the next phase higher-leverage.

### Phase 1 — Foundation of trust (highest-leverage prerequisites)

**Goal:** Establish the platform as a reliable system of record before layering intelligence on top.

| ID | Proposal | Prerequisite for |
| --- | --- | --- |
| 1.1 / 8.1 | Deterministic event ledger + immutable storage | All quality scoring, audit, replay |
| 1.3 | Time-correctness service | Reliable sequence ordering in ledger |
| 2.2 | Quality contracts per provider | Consensus scoring; self-healing policies |
| 4.3 | End-to-end exactly-once delivery | Ledger integrity; reliable self-healing |

**Expected outcomes:** Any historical output is reproducible. Provider incidents are precisely measurable. Downstream trust in all data products increases.

---

### Phase 2 — Intelligence and quality governance

**Goal:** Add consensus, quality control, and automated remediation on top of the trustable foundation.

| ID | Proposal | Prerequisite for |
| --- | --- | --- |
| 1.2 / 8.2 | Multi-provider consensus engine | Quality control plane; self-healing |
| 2.1 | Data quality control plane | Self-healing policy triggers |
| 2.3 / 8.4 | Automated correction and self-healing | Continuous quality improvement |
| 3.3 / 8.3 | Unified historical/live serving contract | Strategy-facing feature store; consumers |
| 10.6 | Self-optimizing provider routing | Better consensus inputs and failover |

**Expected outcomes:** Most quality incidents self-heal. Strategy consumers integrate once against a stable contract. Routing decisions are evidence-based.

---

### Phase 3 — Operating system and strategic differentiation

**Goal:** Transform the platform from infrastructure into a strategic market-data intelligence surface.

| ID | Proposal | Depends on |
| --- | --- | --- |
| 3.1 | Strategy-facing feature store | Unified serving contract |
| 3.2 | Query engine + semantic API | Unified contract; Phase 1 ledger |
| 5.1 / 8.5 | Explainable diagnostics assistant | Rich telemetry from Phases 1 and 2 |
| 5.2 | Declarative orchestration DSL | Quality contracts; routing policies |
| 10.1 | Real-time market state intelligence | Time-correctness service |
| 10.2 | Unified order-book microstructure reconstruction | Ledger; unified contract |
| 10.3 | Policy-driven compliance and governance | Lineage + ledger from Phase 1 |
| 10.4 | Intelligent storage tiering and query acceleration | Feature store; semantic API |
| 10.7 | Data product packaging and contract marketplace | All prior phases |
| 6.1 | Provider scorecards and procurement analytics | Consensus + routing from Phase 2 |
| 6.3 | Verifiable data lineage and provenance exports | Compliance framework |

**Expected outcomes:** Platform becomes a strategic market-data intelligence system usable by research, strategy, operations, and external consumers.

---

## 15) Risk factors and mitigation strategies

### 15.1 Immutable ledger introduces storage cost growth

**Risk:** Persisting both raw and canonicalized events, plus hashes and lineage metadata, significantly increases storage volume per event.

**Mitigation:**

- Apply the existing tiered storage model: hot ledger in JSONL/Parquet, cold ledger in ZSTD-compressed archival.
- Implement ledger compaction for resolved periods where raw frames are no longer needed for debugging.
- Use the existing `CompressionProfileManager` and `LifecyclePolicyEngine` to manage ledger growth under policy.

---

### 15.2 Consensus engine creates latency overhead on the hot path

**Risk:** Fanning out to multiple providers and computing confidence scores adds latency on the ingestion critical path.

**Mitigation:**

- Run consensus scoring asynchronously off the main `EventPipeline` using a bounded channel (following `PipelinePolicyConstants` presets).
- Provide a configuration option to disable real-time consensus scoring and fall back to batch scoring during high-load sessions.
- Use the existing `BackpressureAlertService` to monitor queue depth and degrade gracefully.

---

### 15.3 Unified serving contract risks breaking existing downstream consumers

**Risk:** Introducing a new canonical event schema may break existing integrations built against `HistoricalBar` or the current live event structures.

**Mitigation:**

- Follow ADR-006 (polymorphic payload pattern) and ADR-014 (JSON source generators) for schema evolution.
- Version the contract from day one and maintain at least one prior version during migration windows.
- Use the existing `SchemaVersionManager` and `SchemaValidationService` to enforce compatibility gates.

---

### 15.4 Automated self-healing can cause over-correction

**Risk:** Aggressive automated backfill or provider substitution under noisy quality signals can amplify problems rather than resolve them.

**Mitigation:**

- Implement hard budget caps (max symbols per remediation cycle, max backfill window size, max API cost per hour) enforced in the policy engine.
- Require a human-approval step (section 11.4) for any remediation exceeding a configurable blast-radius threshold.
- Track every automated action in an audit trail, reviewing false-positive rates weekly during rollout.

---

### 15.5 Explainable diagnostics risks exposing sensitive operational data

**Risk:** Root-cause graphs and incident narratives may surface API key fragments, internal system topologies, or provider rate-limit details in logs or API responses.

**Mitigation:**

- Route all diagnostic output through the existing `SensitiveValueMasker` before serialization.
- Require the existing API key middleware (`ApiKeyMiddleware`) for all diagnostics endpoints.
- Separate internal diagnostic depth (full) from external-facing responses (sanitized).

---

### 15.6 Declarative DSL complexity exceeds its usability benefit

**Risk:** Building a purpose-specific orchestration language is a significant investment that may not be adopted if the learning curve is high.

**Mitigation:**

- Validate the DSL concept first through a structured YAML/JSON configuration schema rather than a custom language.
- Use the existing `ConfigurationPipeline` and `ValidatedConfig` as a foundation for incremental declarative enhancements.
- Adopt the DSL approach only after demonstrating that the configuration complexity it targets is a documented pain point for operators.

---

## 16) Open questions for architecture decisions

These questions do not yet have agreed answers and should be resolved before detailed design begins on each area.

### 16.1 Ledger identity model

- Should each event have a globally unique ID assigned at ingestion, or should the canonical hash serve as the primary identifier?
- How should late/amended events reference and supersede earlier records without breaking immutability?
- What is the retention policy for raw provider frames once a configurable "forensic window" elapses?

### 16.2 Consensus engine scope

- Should the consensus engine operate on trades/quotes only, or also on aggregated bars and derived features?
- What is the minimum provider overlap (N of M) required before a consensus score is computed vs. marked as single-source?
- Should consensus state be part of the `MarketEvent` schema (inline) or a separate stream consumers opt into?

### 16.3 Unified contract versioning

- What is the schema evolution policy — forward-compatible only, or bidirectional with a migration layer?
- How should corporate action adjustments (splits, dividends) be represented — as event annotations, separate adjustment records, or as adjusted price fields?
- Should the unified contract be expressed as a protobuf schema (for binary efficiency) or kept as JSON (for tooling compatibility)?

### 16.4 Self-healing policy engine design

- Should policies be expressed as code (C# DSL), structured configuration (YAML), or rule-table formats?
- Who owns policy authorship — operators at runtime, or developers at build time with promotion through environments?
- How should conflicting policies (e.g., "backfill gap" vs. "pause due to API budget") be resolved?

### 16.5 Diagnostics assistant interface

- Should the diagnostics assistant be CLI-only, API-only, or accessible through the WPF and web UIs?
- How should the incident memory / playbook corpus be stored, versioned, and shared across deployments?
- Is an LLM-backed interpretation layer in scope, or should all root-cause logic be rule-based and deterministic?

### 16.6 Provider routing and blended sourcing

- What is the unit of routing — per symbol, per feed type, or per time window?
- How should routing decisions interact with the existing `FailoverAwareMarketDataClient` — replace it, extend it, or sit alongside it?
- Should routing scores be influenced by downstream consumer feedback (e.g., strategy performance attribution)?

### 16.7 Data product marketplace

- What is the access control model for data products — shared namespace, team namespaces, or per-consumer entitlement graphs?
- Should the first implementation target internal research consumers only, or should external redistribution be an explicit design goal from the start?
- What is the minimum viable "data product" definition: schema + SLO + coverage window, or does it also require embedded transform and serving logic?
