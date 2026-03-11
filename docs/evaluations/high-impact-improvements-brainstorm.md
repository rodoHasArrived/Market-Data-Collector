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

This section maps each major proposal to what already exists in the codebase and what net-new work is required. References point to the primary implementation files. Each subsection closes with a concrete "Suggested first implementation step" to provide an actionable entry point.

### 13.1 Deterministic event ledger (idea 1.1 / 8.1)

| Dimension | Current state | Gap |
| --- | --- | --- |
| Immutable append-only storage | `WriteAheadLog` provides durability; `JsonlStorageSink` + `ParquetStorageSink` write events. Sinks are not treated as an immutable ledger. | Need cryptographic hashing per event and a sealed ledger API that forbids mutation. |
| Canonical payload hash | Not computed at ingestion. | Add hash computation in `EventCanonicalizer` / `CanonicalizingPublisher`. |
| Provider raw-frame hash | Not stored. | Capture and persist the hash of the provider's raw payload alongside the canonical event so the transformation can be audited. |
| Normalization ruleset versioning | `ConditionCodeMapper` and `VenueMicMapper` expose a `Version` loaded from their JSON mapping files, but this is not propagated into `MarketEvent` or other downstream outputs. | Propagate the active mapper versions into `MarketEvent` (for example via a `RulesetVersion` field) during canonicalization, and bump versions on any mapper change. |
| Deterministic replay | `JsonlReplayer` replays files but does not guarantee ordering across providers or bit-for-bit reproducibility. | Add a replay contract enforcing sequence ordering and deterministic late-event handling. |
| Diff tooling | No mechanism to compare two replay runs for the same window. | Add a ledger diff command to `DiagnosticsCommands` that emits per-event discrepancies between runs. |

**Existing leverage points:** `WriteAheadLog`, `JsonlReplayer`, `EventCanonicalizer`, `CanonicalizingPublisher`, `MarketEvent` (extend with `CanonicalHash` and `RulesetVersion` fields).

**Suggested first implementation step:** Add `CanonicalHash` (SHA-256 of the serialized canonical payload) and `RulesetVersion` (a static string incremented by convention) to the `MarketEvent` record in `MarketDataCollector.Domain/Events/MarketEvent.cs`. Wire the hash computation into `CanonicalizingPublisher.PublishAsync`. This alone unlocks dedup, audit, and replay-diff capabilities without restructuring storage.

---

### 13.2 Multi-provider consensus engine (idea 1.2 / 8.2)

| Dimension | Current state | Gap |
| --- | --- | --- |
| Multi-provider fan-out | `FailoverAwareMarketDataClient` routes to one provider at a time. `CompositeHistoricalDataProvider` uses priority-ordered fallback. | Neither fans out to multiple providers simultaneously to compare outputs. A new `ConsensusAwareMarketDataClient` wrapper is needed. |
| Real-time disagreement detection | `CrossProviderComparisonService` operates as a batch quality metric, not a streaming comparator. | Refactor or extend to evaluate inbound events from multiple provider channels concurrently within a configurable time window (e.g., 50 ms). |
| Confidence scoring | `DataQualityMonitoringService` tracks aggregate quality metrics per provider. Per-event confidence scores are not produced. | Extend `MarketEvent` with an optional `ConfidenceScore` field and a confidence calculation step in the consensus pipeline. |
| Disagreement taxonomy | Provider mismatches are currently not classified. | Add structured disagreement types: `PriceOutlier`, `MissingPrint`, `SequenceDivergence`, `StaleFeed`, `VenueMismatch`. |
| Consensus stream persistence | Not implemented. | Add a `ConsensusSink` that writes "consensus truth" and "provider delta" streams alongside existing symbol sinks. |

**Existing leverage points:** `DataQualityMonitoringService`, `CrossProviderComparisonService`, `FailoverAwareMarketDataClient`, `EventPipeline`, `PipelinePolicyConstants` (for bounded channel configuration).

**Suggested first implementation step:** Create a `ConsensusWindowBuffer` that groups inbound `MarketEvent` objects from multiple providers by `(Symbol, ExchangeTimestamp ± 50ms)`. For each complete group, emit the event with a `ConfidenceScore` (0–100) computed as `round(100 * agreementCount / totalProviders)`. Start with trade events only; extend to quotes once stable. The existing `EventPipeline` bounded channel can fan events to both the consensus buffer and the storage sinks.

---

### 13.3 Unified historical/live serving contract (idea 3.3 / 8.3)

| Dimension | Current state | Gap |
| --- | --- | --- |
| Live streaming contract | `IMarketDataClient` emits `MarketEvent` objects via `IMarketEventPublisher`. Fields and semantics are live-streaming-specific. | The live event structure lacks fields needed for historical context (e.g., adjustment factor, backfill source, replay mode flag). |
| Historical serving contract | `HistoricalDataQueryService` + `JsonlReplayer` provide file-based replay. `HistoricalBar` is the primary type. | No HTTP query layer with time-travel and composable filters; no streaming delivery from historical data. |
| Schema parity | `MarketEvent` (live) and `HistoricalBar` (historical) are entirely separate types. | Define a `CanonicalMarketEvent` that is a strict superset of both, preserving backward compatibility through `[JsonIgnore(Condition = WhenWritingDefault)]`. |
| Session-boundary stitching | Corporate action adjustments and session transitions are not handled by the serving layer. | Add a `StitchingLayer` that processes `TradingCalendar` events and corporate action records to produce seamless cross-session streams. |
| Query parity | No shared query API; historical queries use file paths, live queries use WebSocket subscriptions. | Design a `UnifiedDataQuery` API endpoint that dispatches to `JsonlReplayer` (historical) or `EventPipeline` (live) based on the requested time range. |

**Existing leverage points:** `IMarketDataClient`, `HistoricalDataQueryService`, `JsonlReplayer`, `MarketEvent`, `HistoricalBar`, `TradingCalendar`, `UiApiRoutes`.

**Suggested first implementation step:** Create a `CanonicalMarketEvent` record that embeds the existing `MarketEventPayload` union and adds `DataMode` (`Live` | `Replay` | `Backfill`), `AdjustmentFactor`, and `BackfillSource` fields. Add a static factory on `MarketEvent` to produce a `CanonicalMarketEvent`. This unifies the type system without breaking existing producers or consumers.

---

### 13.4 Automated correction and self-healing (idea 2.3 / 8.4)

| Dimension | Current state | Gap |
| --- | --- | --- |
| Gap detection | `GapAnalyzer` in `DataQuality/` detects gaps against the expected event cadence. | Gap alerts are raised but do not automatically trigger remediation. |
| Targeted backfill | `GapBackfillService` can backfill specific symbol/window pairs. | `GapBackfillService` is not connected to `GapAnalyzer` alerts; the link must be made explicit. |
| Provider substitution | Not implemented. | When the primary provider is the cause of the gap, the system should automatically reroute via `CompositeHistoricalDataProvider` fallback chain. |
| Remediation policies | Not implemented. | Need a declarative policy engine (YAML or C# fluent builder) specifying trigger thresholds, which remediation to apply, and budget/blast-radius caps. |
| Post-remediation verification | Not implemented. | After backfill, `DataQualityMonitoringService` should re-score the repaired window and publish a "quality restored" event to the audit trail. |
| Safety controls | Not implemented. | Hard caps on max backfill requests per hour, max symbols per remediation cycle, and max API cost per day should be enforced. |

**Existing leverage points:** `GapAnalyzer`, `GapBackfillService`, `HistoricalBackfillService`, `DataQualityMonitoringService`, `BackfillScheduleManager`, `OperationalScheduler`.

**Suggested first implementation step:** Wire `GapAnalyzer`'s gap-detected event directly to `GapBackfillService.RequestBackfillAsync` using a new `AutoRemediationOrchestrator` class. Start with a simple hard-coded policy: if gap duration > 5 minutes and the symbol is in the "critical" tier, trigger backfill. Gate the trigger with a leaky-bucket rate limiter (max 10 backfills per hour per provider) to prevent runaway remediation. Log every trigger decision to a new `RemediationAuditLog`.

---

### 13.5 Explainable diagnostics assistant (idea 5.1 / 8.5)

| Dimension | Current state | Gap |
| --- | --- | --- |
| Diagnostic data collection | `DiagnosticBundleService` collects system state snapshots on demand. | Output is a raw state dump, not a structured root-cause analysis. |
| Error correlation | `ErrorTracker` and `ErrorRingBuffer` capture errors with timestamps. | No causality graph linking provider state, queue pressure, reconnection events, and quality anomalies by time window. |
| Operator-facing diagnostics | `DetailedHealthCheck`, `StatusSnapshot`, `StatusWriter` expose rich operational data. | Data is available but not surfaced through a queryable "why is X happening?" interface. |
| Question-oriented queries | `DiagnosticsCommands` supports `--quick-check` and connectivity tests. | No first-class commands like `--why-stale SPY` or `--explain-alert 42` that correlate multiple data sources. |
| Remediation playbooks | Not implemented. | Need a structured playbook corpus (JSON/YAML) mapping incident patterns to recommended remediation steps, stored alongside config. |
| Incident memory | Not implemented. | Resolved incidents should be serialized with cause + resolution so future occurrences match faster. |

**Existing leverage points:** `DiagnosticBundleService`, `ErrorTracker`, `ErrorRingBuffer`, `DetailedHealthCheck`, `PreflightChecker`, `SystemHealthChecker`, `DiagnosticsCommands`, `ConnectionHealthMonitor`.

**Suggested first implementation step:** Add a `--diagnose-symbol <SYMBOL>` CLI command to `DiagnosticsCommands` that aggregates: (1) last 10 errors from `ErrorRingBuffer` for the symbol's provider, (2) connection health from `ConnectionHealthMonitor`, (3) gap status from `GapAnalyzer`, and (4) quality score from `DataQualityMonitoringService`. Output a ranked list of candidate causes with a recommended next action for each. This is the minimum-viable diagnostics assistant.

---

### 13.6 Real-time market state intelligence (idea 10.1)

| Dimension | Current state | Gap |
| --- | --- | --- |
| Market state tracking | `TradingCalendar` provides session/holiday awareness at the exchange level. | No per-symbol state machine for halt, reopen, opening auction, closing auction, and volatility interruption transitions. |
| Halt/reopen handling | Not implemented. | When a symbol enters a halt, the system currently processes (or drops) incoming events without annotating them as halt-period events. |
| Regime detection | `ProviderDegradationScorer` scores providers on connectivity. | No volatility or liquidity regime detection for market data itself (spread regime, order-flow toxicity, volume imbalance). |
| Cross-symbol contagion | Not implemented. | ETF basket stress, index constituent correlation, and sector contagion are untracked. |
| Actionable state outputs | Not implemented. | State-change webhooks and downstream event annotations are not emitted when market state transitions occur. |

**Existing leverage points:** `TradingCalendar`, `SpreadMonitor`, `ProviderDegradationScorer`, `MarketDepthCollector`, `ConnectionStatusWebhook` (extend for market state transitions).

**Suggested first implementation step:** Build a `SymbolMarketStateTracker` that subscribes to `MarketDepthUpdate` and `MarketTradeUpdate` events and maintains a per-symbol enum state: `PreMarket`, `OpeningAuction`, `Normal`, `HaltPending`, `Halted`, `ReopeningAuction`, `ClosingAuction`, `Closed`. Use condition codes from `ConditionCodeMapper` to drive transitions. Annotate every `MarketEvent` with the current state value. This alone enables downstream consumers to correctly interpret event context.

---

### 13.7 Policy-driven compliance and governance (idea 10.3)

| Dimension | Current state | Gap |
| --- | --- | --- |
| Data retention | `LifecyclePolicyEngine` enforces age-based retention and tier migration. | Policies are uniform — no jurisdiction-aware, instrument-type-level, or usage-class distinctions. |
| Lineage tracking | `DataLineageService` tracks file-level lineage (source, destination, timestamps). | Lineage is internal-only; there is no signed, exportable evidence bundle for external audit. |
| Usage governance | Not implemented. | No usage-class tagging (`internal-analytics`, `external-redistribution`, `restricted`) or entitlement-aware serving. |
| Continuous compliance | Not implemented. | No automated check that configuration or runtime behavior continues to satisfy declared retention/governance policies after changes. |
| Audit export | `PortableDataPackager` creates data bundles. | Bundles do not include signed lineage chains or policy evaluation proofs. |

**Existing leverage points:** `LifecyclePolicyEngine`, `DataLineageService`, `PortableDataPackager`, `MetadataTagService`, `StorageChecksumService` (extend for signature).

**Suggested first implementation step:** Extend `MetadataTagService` to support a `UsageClass` tag (enum: `Internal` | `ExternalClient` | `Restricted`) on any symbol or dataset. Add a startup check in `PreflightChecker` that validates that all symbols with `Restricted` usage class have matching retention policies in `LifecyclePolicyEngine`. Log a warning — or fail startup with `--strict-schemas` — if any restricted dataset lacks a policy. This is a lightweight governance foundation that requires no external dependencies.

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

Each risk includes an estimated impact level, the specific mitigation approach, and the existing code or patterns that make the mitigation achievable.

### 15.1 Immutable ledger introduces storage cost growth

**Risk level:** High for raw-frame storage; moderate for hashes/metadata alone.

**Estimated impact:** A dual-hash (SHA-256 of raw + canonical) per event adds approximately 100–150 bytes per event. At 100,000 events per second, that is roughly 10 MB/s of additional metadata before compression. At ZSTD-19 compression, lineage metadata typically compresses at 10:1 on structured JSON, reducing the incremental cost to ~1 MB/s. Raw frame storage is potentially 3–10× the canonical size depending on provider verbosity.

**Mitigation:**

- Apply the existing tiered storage model: maintain a raw-frame ledger only for a configurable forensic window (default: 7 days); automatically purge raw frames after the window via `LifecyclePolicyEngine`.
- Persist hashes and lineage metadata in a separate sidecar file (e.g., `{symbol}_ledger_meta.jsonl`) so the hot path for event storage is not disrupted.
- Use the existing `CompressionProfileManager` `Archive` profile (ZSTD-19) for ledger metadata; apply `RealTime` profile (LZ4) for the hot window.
- Benchmark storage overhead before and after by extending `EventPipelineBenchmarks` with a hash-computation benchmark.

---

### 15.2 Consensus engine creates latency overhead on the hot path

**Risk level:** Moderate. Single-provider ingestion latency is the baseline; multi-provider fan-out adds coordination overhead.

**Estimated impact:** A 50 ms consensus window means events cannot be forwarded until the window expires or all expected providers have arrived. At normal market hours this adds roughly 50 ms median latency and up to 100 ms at the 99th percentile. For strategies sensitive to single-millisecond latency, this is unacceptable in live-trading mode but acceptable for research and monitoring consumers.

**Mitigation:**

- Run consensus scoring asynchronously on a secondary channel off the main `EventPipeline`, following `PipelinePolicyConstants` bounded channel configuration. Live consumers receive the unconsensed event immediately; the consensus-annotated copy follows when the window closes.
- Make consensus participation opt-in per consumer: subscribers to `EventPipeline` can choose `ConsensusMode.Immediate` (no delay) or `ConsensusMode.Confirmed` (waits for the window).
- Provide a configuration option to disable real-time consensus entirely and fall back to `CrossProviderComparisonService` batch scoring during high-load sessions, controlled via `AppConfig`.
- Use the existing `BackpressureAlertService` to monitor the secondary consensus channel depth; degrade to single-provider passthrough if the channel exceeds 80% capacity.

---

### 15.3 Unified serving contract risks breaking existing downstream consumers

**Risk level:** High if approached as a replacement; low if approached as an additive superset.

**Estimated impact:** Any existing code importing `HistoricalBar` or calling `IMarketDataClient` event handlers would require migration. The `UiApiClient` and all WPF/Web consumer services would need testing against the new contract. Without a compatibility layer, this could break all 18 integration endpoint test files simultaneously.

**Mitigation:**

- Follow ADR-006 (polymorphic payload pattern) and ADR-014 (JSON source generators) for schema evolution: add fields with `[JsonIgnore(Condition = WhenWritingDefault)]` so old consumers can ignore them.
- Version the contract from day one using a `SchemaVersion` integer field in the response; bump only on breaking changes.
- Maintain the existing `HistoricalBar` and `MarketEvent` types as unchanged and introduce `CanonicalMarketEvent` as a new type that consumers can opt into. Remove legacy types only after a published deprecation schedule.
- Use the existing `SchemaVersionManager` and `SchemaValidationService` to enforce compatibility gates in CI; add a test in `ResponseSchemaSnapshotTests` for the new contract to prevent silent regressions.

---

### 15.4 Automated self-healing can cause over-correction

**Risk level:** High if deployed without budget controls; moderate with rate limiting and human approval thresholds.

**Estimated impact:** Without limits, a quality event storm (e.g., 100 symbols simultaneously flagged by `GapAnalyzer`) could trigger 100 concurrent backfill requests, exhausting provider API budgets, triggering rate-limit exceptions, and causing a wider outage than the original gap. Providers like Alpaca limit to 200 requests/minute; a storm of 100 backfills with 5 paginated requests each would breach the limit instantly.

**Mitigation:**

- Implement a leaky-bucket rate limiter in `AutoRemediationOrchestrator` (max 10 backfill triggers/hour per provider, configurable).
- Require a human-approval step through the existing `StatusHttpServer` API for any remediation exceeding a configurable blast-radius threshold (e.g., > 5 symbols or > 1 hour of window).
- Track every automated action in a `RemediationAuditLog` (append-only JSONL file) reviewed during weekly operational reviews.
- Use the existing `BackfillStatusStore` to detect and skip symbols already mid-backfill, preventing duplicated remediation requests.
- Test the rate limiter and blast-radius cap in a dedicated test class in `Application/Services/` before enabling auto-remediation in production.

---

### 15.5 Explainable diagnostics risks exposing sensitive operational data

**Risk level:** Moderate. The risk is specific to multi-tenant or externally-accessible deployments; single-user local deployments have lower exposure.

**Estimated impact:** A root-cause graph that leaks provider API endpoint URLs, internal service topology, or rate-limit state could expose competitive intelligence or facilitate targeted denial-of-service attempts against the deployment.

**Mitigation:**

- Route all diagnostic output through the existing `SensitiveValueMasker` before serialization; validate that all `DiagnosticBundleService` outputs are masker-filtered in unit tests.
- Require the existing API key middleware (`ApiKeyMiddleware`) for all new diagnostics endpoints; add to `NegativePathEndpointTests` to confirm unauthenticated requests return 401.
- Introduce two diagnostic depth levels controlled by a config flag: `FullDiagnostics` (internal operators only, served on a local-only port) and `SanitizedDiagnostics` (external-safe, no topology or credential metadata).
- Store playbook files on disk without secrets embedded; reference credentials by environment variable name, not value.

---

### 15.6 Declarative DSL complexity exceeds its usability benefit

**Risk level:** Low in the short term (DSL is a Phase 3 idea); high if pursued prematurely as a configuration replacement.

**Estimated impact:** A custom DSL requires a parser, type-checker, documentation, and editor support to become genuinely usable. Without these, operators prefer editing YAML or C# directly. Historical examples in the quantitative domain (e.g., QuantLib YAML configs, custom backtest DSLs) show adoption rates below 30% without IDE tooling.

**Mitigation:**

- Validate the declarative direction first through a strongly-typed configuration schema (using C# records + JSON schema generation) rather than a custom language; this can be done in a single PR.
- Use the existing `ConfigurationPipeline` and `ValidatedConfig` as a foundation — each field already has validation logic that can be exposed as a schema.
- Prototype the DSL as a thin YAML layer over `AppConfig` records using `System.Text.Json` source generators; avoid a custom parser unless the YAML layer proves insufficient after operator feedback.
- Adopt the full DSL approach only after user research documents that configuration complexity is a top-3 operator pain point.

---

## 16) Open questions for architecture decisions

These questions do not yet have agreed answers and should be resolved before detailed design begins on each area. For each question, one or more candidate solutions are listed to seed discussion. These are not final decisions — they are starting proposals.

---

### 16.1 Ledger identity model

**Context:** The ledger needs a stable, globally unique identity for each event to support dedup, replay, and audit workflows. The identity model chosen here affects storage layout, indexing strategy, and dedup logic throughout the system.

**Questions and candidate solutions:**

1. **Should each event have a globally unique ID assigned at ingestion, or should the canonical hash serve as the primary identifier?**
   - _Solution A (hash-as-ID):_ Use a deterministic hash (SHA-256 of `Symbol + ExchangeTimestamp + ProviderRawPayloadHash`) as the event ID. Pro: content-addressable, inherently dedup-safe. Con: requires re-hashing raw payload at ingestion, and hash collisions (though astronomically rare) must be handled.
   - _Solution B (UUID + hash):_ Assign a UUID at ingestion for identity; separately compute and store the canonical hash for integrity. Pro: decouples identity from content, easier to generate. Con: dedup requires explicit hash comparison rather than ID collision detection.
   - **Recommendation:** Solution B. Use `Guid.NewGuid()` for the event ID; store `CanonicalHash` and `ProviderHash` as separate fields. Dedup on `(Symbol, ExchangeTimestamp, ProviderHash)` to detect exact-duplicate raw events.

2. **How should late or amended events reference and supersede earlier records without breaking immutability?**
   - _Solution A (supersession records):_ Emit a new `AmendedEvent` record containing the original event ID and the corrected payload. The ledger remains append-only; readers must apply the amendment chain to get the final value.
   - _Solution B (correction streams):_ Write corrections to a separate `_corrections` stream file alongside the main event file. Readers optionally merge corrections when building a view.
   - **Recommendation:** Solution A. Amendments as explicit records match the ADR-006 polymorphic payload pattern and keep all data in a single scan path.

3. **What is the retention policy for raw provider frames once the forensic window elapses?**
   - _Solution A (auto-purge):_ `LifecyclePolicyEngine` automatically purges raw frames after a configurable forensic window (default: 7 days). Only canonical events and hashes are retained long-term.
   - _Solution B (tiered archival):_ Compress raw frames into cold storage (ZSTD-19) rather than deleting; retain indefinitely.
   - **Recommendation:** Solution A with a configurable override. Default to 7-day purge with an opt-in "never delete" mode for regulated deployments. Pair with storage quota enforcement via `QuotaEnforcementService`.

4. **What hash algorithm should be used for event fingerprints — SHA-256 or a faster alternative like BLAKE3 or xxHash?**
   - _Solution A (SHA-256):_ Industry standard, widely understood, accepted for regulatory purposes. ~300 MB/s throughput on modern hardware.
   - _Solution B (BLAKE3):_ 3–5× faster than SHA-256, cryptographically secure, designed for streaming. Less established in financial data contexts.
   - _Solution C (xxHash for dedup only):_ Use xxHash128 for real-time dedup (speed-critical) and SHA-256 for the immutable ledger fingerprint (security-critical). Store both.
   - **Recommendation:** Solution C. Update `PersistentDedupLedger.HashIdentity` (and its keying) to use xxHash128 for the hot-path dedup check, while continuing to compute and store SHA-256 as the immutable ledger integrity field. This keeps dedup responsibilities within `PersistentDedupLedger` while making the speed-vs-security tradeoff explicit.

5. **How should an event's identity survive re-canonicalization when normalization rules are updated (e.g., `ConditionCodeMapper` version bump)?**
   - _Solution A (stable raw ID):_ The raw event UUID never changes. Re-canonicalization creates a new canonical record pointing to the original via `OriginalEventId`. The `RulesetVersion` field distinguishes the two views.
   - _Solution B (version-scoped hash):_ The canonical hash includes the ruleset version, making each re-canonicalization produce a distinct fingerprint. Old and new versions coexist in the ledger.
   - **Recommendation:** Solution A. The raw UUID is the stable identity anchor; the canonical record is a derived view. This makes "what changed after a rule update?" a straightforward ledger query.

---

### 16.2 Consensus engine scope and semantics

**Context:** The consensus engine must balance coverage (operating on all event types) against complexity and latency. Scope decisions here determine how many downstream consumers benefit and how much ingestion overhead is introduced.

**Questions and candidate solutions:**

1. **Should the consensus engine operate on all event types, or start with a subset?**
   - _Solution A (trades-first):_ Begin with trade events only — they have the smallest set of comparable fields (price, size, exchange timestamp) and the highest business value for accuracy.
   - _Solution B (quotes-first):_ Begin with BBO quotes — they are emitted more frequently and regime-quality issues show up earlier.
   - _Solution C (all types from day one):_ Full coverage but higher implementation complexity and more edge cases.
   - **Recommendation:** Solution A. Trades-first provides the fastest path to production value with the least ambiguity. Extend to quotes in the next iteration.

2. **What constitutes "agreement" between providers — exact price match, within-tick tolerance, or time-windowed correlation?**
   - _Solution A (exact match):_ Two events agree if all key fields are identical. Too strict for practical use (venue microstructure introduces sub-cent differences).
   - _Solution B (configurable tolerance):_ Agreement is defined by a configurable tolerance: ±$0.01 for equities, ±1 pip for forex, ±0.5% for crypto. Time tolerance: ±50 ms for trades.
   - **Recommendation:** Solution B. Expose tolerances as `ConsensusConfig` fields nested under the existing `AppConfig`; provide sensible defaults per asset class.

3. **What is the minimum provider overlap (N of M) required to compute a consensus score vs. mark as single-source?**
   - _Solution A (2-of-N required):_ Only compute a consensus score when at least 2 providers have emitted a comparable event within the time window. Single-source events are labeled `ConfidenceTier.SingleSource`.
   - _Solution B (1-of-N with weight):_ Always emit a confidence score; single-source events get a base confidence of 0.5. Score increases linearly with additional providers.
   - **Recommendation:** Solution B. Always provide a confidence signal to consumers. Use 0.5 as the single-source floor; score approaches 1.0 as more providers agree.

4. **Should consensus state be part of the `MarketEvent` schema (inline) or a separate stream consumers opt into?**
   - _Solution A (inline):_ Add `ConfidenceScore` and `ConsensusProviderCount` directly to `MarketEvent`. Always present; defaults to 1.0/1 for systems without consensus.
   - _Solution B (parallel stream):_ Consensus metadata is emitted as a separate `ConsensusAnnotation` event on a different channel. Consumers subscribe only when they need it.
   - **Recommendation:** Solution A for the score and tier; Solution B for the full disagreement detail. Inline a lightweight `byte ConfidenceScore` (0–100) and `byte ConsensusProviderCount` on `MarketEvent`; route the full disagreement record to a separate channel.

5. **How should the consensus engine behave during extended hours, pre-market, and closed sessions when provider coverage is thinner?**
   - _Solution A (strict mode only during regular hours):_ Apply consensus scoring only during exchange regular trading hours as defined by `TradingCalendar`. Outside those hours, pass events through with `ConfidenceTier.ExtendedHours`.
   - _Solution B (session-aware thresholds):_ Maintain separate consensus thresholds for each session type (regular, extended, closed) stored in `ConsensusConfig`.
   - **Recommendation:** Solution B. Different sessions have materially different provider coverage and latency profiles. Session-aware thresholds prevent regular-hours policies from producing false alerts in pre-market.

---

### 16.3 Unified contract versioning and migration

**Context:** The unified contract is a long-lived, multi-consumer interface. Versioning decisions made early are difficult and costly to reverse once downstream consumers exist.

**Questions and candidate solutions:**

1. **What is the schema evolution policy — forward-compatible only, or bidirectional with a full migration layer?**
   - _Solution A (forward-only):_ New fields can be added freely; removing or renaming fields requires a major version bump and a migration period. Implemented via `[JsonIgnore(Condition = WhenWritingDefault)]` on new fields.
   - _Solution B (bidirectional):_ Consumers register against a specific version; the serving layer adapts output to the requested version. More powerful but requires maintaining transformation logic per version pair.
   - **Recommendation:** Solution A for the first two major versions. Adopt bidirectional migration only if multiple long-lived consumer versions coexist in production simultaneously.

2. **How long should the transition period be between contract versions before the old one is removed?**
   - _Solution A (fixed calendar period):_ 90-day deprecation window for minor breaking changes; 6-month window for major breaking changes.
   - _Solution B (usage-driven):_ Deprecate when telemetry shows zero active consumers on the old version for 30 consecutive days.
   - **Recommendation:** Solution B with a hard cap of 12 months. Usage-driven deprecation avoids removing versions that silent or infrequent consumers depend on.

3. **How should corporate action adjustments (splits, dividends) be represented in the unified contract?**
   - _Solution A (event annotations):_ Add an `AdjustmentEvent` record type in the polymorphic `MarketEventPayload` union, emitted whenever a corporate action affects price/size of historical data.
   - _Solution B (adjusted price fields):_ Store both raw and adjusted prices inline on every bar/quote event. Consumers select the field they need.
   - _Solution C (separate adjustment stream):_ Corporate actions are maintained as a separate reference dataset; consumers apply adjustments at query time.
   - **Recommendation:** Solution C for storage integrity; Solution B for convenience in analytical export formats. The canonical ledger stores unadjusted prices; the `AnalysisExportService` applies adjustments at export time using a reference dataset.

4. **Should the unified contract be expressed as a protobuf schema (for binary efficiency) or remain JSON-based?**
   - _Solution A (protobuf):_ ~5–10× smaller payloads, faster serialization, strongly typed IDL. Requires protobuf toolchain in the CI/CD pipeline and generates C# classes from `.proto` files.
   - _Solution B (JSON + source generators):_ Consistent with ADR-014; no new toolchain; human-readable on-disk format. Already used for all existing event serialization.
   - _Solution C (Apache Arrow IPC for analytics, JSON for streaming):_ Arrow for bulk historical export (already used via `Apache.Arrow` dependency); JSON for live streaming and API responses.
   - **Recommendation:** Solution C. Keep JSON for live streaming and REST APIs (tooling compatibility); use Arrow IPC for bulk historical delivery (already supported by `AnalysisExportService`). Evaluate protobuf only if JSON throughput proves to be a bottleneck in production profiling.

5. **Who approves schema changes — any engineer, a designated data contracts owner, or a schema registry with an approval workflow?**
   - _Solution A (any engineer):_ Low friction; high risk of accidental breaking changes.
   - _Solution B (code owner group):_ Add a `CODEOWNERS` rule mapping `MarketDataCollector.Contracts/` to a `@data-contracts` team. PRs touching contract types require approval from that group.
   - _Solution C (schema registry):_ External service stores and validates contract versions; changes require passing compatibility checks.
   - **Recommendation:** Solution B immediately; evaluate Solution C if the team grows beyond 5 engineers who touch contracts regularly.

---

### 16.4 Self-healing policy engine design

**Context:** The policy engine determines when automated remediation fires, what action to take, and what safeguards prevent it from making things worse. Getting the design right is critical for operational confidence.

**Questions and candidate solutions:**

1. **Should the first version be rule-based (deterministic, testable) or ML-based (adaptive but opaque)?**
   - _Solution A (rule-based YAML):_ Policies are declarative YAML rules: trigger conditions, actions, and budget caps. Fully deterministic and unit-testable.
   - _Solution B (ML-based):_ Train a model on historical incident/resolution pairs to predict the optimal remediation action. Higher upside in complex scenarios but requires a training dataset that doesn't exist yet.
   - **Recommendation:** Solution A. There is no training data for an ML approach yet. Design the `IRemediationPolicy` interface to support ML-backed implementations later as a drop-in replacement.

2. **Should the same policy framework cover gap backfill, provider substitution, and quarantine, or should these be separate engines?**
   - _Solution A (unified interface):_ Single `IRemediationPolicy` interface with three implementations: `GapBackfillPolicy`, `ProviderSubstitutionPolicy`, `QuarantinePolicy`. Common scheduling and budget-tracking infrastructure.
   - _Solution B (separate engines):_ Each action type has its own service with independent configuration and limits. Simpler per-action; harder to reason about interactions.
   - **Recommendation:** Solution A. A shared interface with a registry pattern (similar to `DataSourceRegistry`) allows policies to be discovered, tested, and budgeted consistently.

3. **Who owns policy authorship — operators at runtime (via API), or developers at build time (via config files promoted through environments)?**
   - _Solution A (config-file-first):_ Policies are YAML files in a dedicated `config/remediation-policies/` directory, promoted through dev → staging → production via Git. Auditable by default.
   - _Solution B (runtime API):_ Policies can be created and updated via API without deploying code. Lower friction; higher risk of untested policies in production.
   - **Recommendation:** Solution A for initial rollout (auditable, testable). Add a runtime override API only for emergency overrides, with every change logged to the `RemediationAuditLog`.

4. **How should conflicting policies be resolved (e.g., "backfill gap for SPY" vs. "pause all Alpaca requests due to API budget")?**
   - _Solution A (priority ordering):_ Each policy has a numeric priority; the highest-priority applicable policy wins. Budget-pause policies get priority 0 (always win).
   - _Solution B (policy arbitrator):_ A `RemediationArbiter` service evaluates all active policies for a given trigger and returns the set of non-conflicting actions, blocking conflicting ones.
   - **Recommendation:** Solution B. Pure priority ordering can lead to unexpected outcomes when new policies are added. An explicit arbitration step makes conflicts visible and testable.

5. **What is the correct blast-radius threshold above which human approval is required before automated action fires?**
   - _Current state:_ No threshold exists; all remediation would be fully automated.
   - _Solution A (symbol count):_ Require approval when > 5 symbols would be affected in a single remediation cycle.
   - _Solution B (estimated API cost):_ Require approval when the estimated backfill cost exceeds a configurable API request budget (e.g., > 500 API calls).
   - _Solution C (combined):_ Both conditions must be satisfied to skip approval; either condition alone triggers approval.
   - **Recommendation:** Solution C. Both dimensions matter; a single high-cost symbol may warrant more caution than five low-cost ones.

---

### 16.5 Diagnostics assistant interface and scope

**Context:** The diagnostics assistant must be useful to operators without a deep understanding of the codebase, while also providing depth for expert investigators. Interface and data model decisions here determine how widely it can be adopted.

**Questions and candidate solutions:**

1. **Should the diagnostics assistant be CLI-only, API-only, or surfaced in both WPF and web UIs?**
   - _Solution A (CLI-first):_ Add `--diagnose-symbol`, `--explain-alert`, and `--incident-report` commands to the existing `DiagnosticsCommands` class. Low effort; high utility for operators who already use the CLI.
   - _Solution B (API + web UI):_ Expose a `/api/diagnostics/explain?symbol=SPY` endpoint returning structured JSON; render in the existing web dashboard. Accessible to all consumers.
   - **Recommendation:** Solution A first (1–2 weeks), Solution B second (4–6 weeks). The CLI validates the underlying data model before committing to UI layout.

2. **How should the assistant prioritize among multiple simultaneous potential root causes?**
   - _Solution A (recency bias):_ Always present the most recently triggered error as the primary cause.
   - _Solution B (scored ranking):_ Score each candidate cause by `confidence × estimated_impact × recency_decay`. Present the top 3 with expandable detail.
   - **Recommendation:** Solution B. Recency alone is misleading for cascading failures where the original cause is older than the symptoms. Use a logarithmic recency decay so events 30 minutes old are still considered but weighted lower.

3. **Should the assistant proactively alert operators (push model) or only respond to queries (pull model)?**
   - _Solution A (pull only):_ Query on demand via CLI or API. Simpler; no false-positive alert fatigue.
   - _Solution B (push + pull):_ Actively emit a structured root-cause event whenever `DataQualityMonitoringService` raises a quality alert, delivered via the existing `ConnectionStatusWebhook`.
   - **Recommendation:** Solution B. Pair push alerts (via webhook) with pull queries (via CLI/API). The webhook payload should include a pre-computed root cause summary and a link to the full diagnostic endpoint.

4. **How should the incident memory / playbook corpus be stored, versioned, and shared across deployments?**
   - _Solution A (local YAML files):_ Playbooks are YAML files in `config/playbooks/`, committed to source control, version-controlled with the codebase.
   - _Solution B (shared remote registry):_ Playbooks are pulled from a remote HTTP endpoint at startup, allowing shared updates across deployments without code deploys.
   - **Recommendation:** Solution A initially. A local YAML corpus is simple, testable, and auditable. Add a "fetch community playbooks" command later once the format is stable.

5. **Is an LLM-backed interpretation layer in scope, or should all root-cause logic be rule-based and deterministic?**
   - _Solution A (rule-based only):_ Pattern-match incident signatures against a known-error catalog (similar to `docs/ai/ai-known-errors.md`). Deterministic, fast, fully offline.
   - _Solution B (LLM-assisted):_ Pass the structured incident bundle to an LLM API to generate a natural-language narrative and suggested remediation. Higher quality explanations; requires network access and API keys.
   - **Recommendation:** Solution A as the default; Solution B as an optional plugin requiring explicit opt-in and an external API key. The rule-based layer must work without any external dependency.

---

### 16.6 Provider routing and blended sourcing

**Context:** Today's routing is static (configured priority order) or reactive (failover on connection loss). Dynamic routing based on real-time quality signals requires a fundamentally different architecture.

**Questions and candidate solutions:**

1. **What is the unit of routing — per symbol, per feed type (trades/quotes/depth), or per time window?**
   - _Solution A (per symbol):_ Route each symbol to the best available provider independently. Fine-grained; may produce different providers for different symbols on the same feed.
   - _Solution B (per feed type + provider):_ Route the entire trades feed to Provider A and depth to Provider B for all symbols simultaneously. Coarser but simpler to reason about.
   - **Recommendation:** Solution A for research/quality mode; Solution B for production streaming to minimize connection count. Expose both modes via `AppConfig.RoutingStrategy`.

2. **How should routing interact with the existing `FailoverAwareMarketDataClient` — replace it, extend it, or sit alongside it?**
   - _Solution A (replace):_ Routing logic subsumes failover; `FailoverAwareMarketDataClient` is deprecated once routing is stable.
   - _Solution B (extend):_ `FailoverAwareMarketDataClient` becomes the reactive/emergency layer; dynamic routing sits above it as an optimization layer.
   - **Recommendation:** Solution B. Failover handles hard failures (connection loss, 401); dynamic routing handles soft degradation (quality drop, latency increase). Separating concerns keeps each simpler.

3. **Should routing scores be influenced by downstream consumer feedback (e.g., strategy performance attribution)?**
   - _Solution A (telemetry-only):_ Scores derived from provider telemetry alone (latency, error rate, completeness). No consumer feedback loop.
   - _Solution B (feedback loop):_ Consumers can tag bad data (e.g., "this trade caused a spurious fill") and those tags feed back to the routing scorer.
   - **Recommendation:** Solution A for the initial implementation. A feedback loop is high value but introduces a complex coupling between data infrastructure and strategy logic. Design the scoring API with a `feedback_weight` parameter set to 0.0 by default, ready to be activated later.

4. **Should routing history be persisted for post-hoc analysis, or is it ephemeral?**
   - _Solution A (ephemeral):_ Routing decisions are in-memory only; not persisted.
   - _Solution B (audit trail):_ Every routing decision is appended to a structured `routing_audit.jsonl` file alongside the existing WAL.
   - **Recommendation:** Solution B. Routing history is essential for understanding why quality issues occurred during specific windows. Append routing change events using the same `WriteAheadLog` infrastructure.

5. **What should happen when all providers for a symbol drop below the minimum acceptable quality threshold simultaneously?**
   - _Solution A (emit degraded events):_ Continue serving events with `ConfidenceTier.BestEffort` and alert.
   - _Solution B (halt and queue):_ Emit a `SymbolSuspendedEvent`, halt the stream, raise an alert via `ConnectionStatusWebhook`, and queue the symbol for manual review. Resume only after quality exceeds the threshold or an operator override is received via API.
   - **Recommendation:** Solution B for critical-tier symbols; Solution A for non-critical. Add a `CriticalityTier` enum to `SymbolConfig` to distinguish behavior per symbol.

---

### 16.7 Data product packaging and contract marketplace

**Context:** The data product layer is the external-facing surface of the platform. Decisions here determine how safely and scalably the internal data assets can be shared.

**Questions and candidate solutions:**

1. **Should data products be defined in code (C# classes), configuration files (YAML/JSON), or a metadata database?**
   - _Solution A (C# manifests):_ Strong typing, compile-time validation, directly testable. Requires a code deploy to create or modify a product.
   - _Solution B (JSON/YAML manifests in source control):_ Human-readable, no-code-deploy required for metadata changes, auditable via Git. Use `PortableDataPackager`'s existing `PackageManifest` as a template.
   - _Solution C (metadata database):_ Runtime CRUD; supports large catalogs. Requires a new service and storage dependency.
   - **Recommendation:** Solution B. JSON manifests committed to `config/data-products/` can be read by the existing `PortableDataPackager` infrastructure. Add a `DataProductManifest` record in `MarketDataCollector.Contracts` as the schema.

2. **What is the access control model for data products — shared namespace, team-partitioned, or per-consumer entitlement graphs?**
   - _Solution A (shared namespace):_ All products are visible to all consumers; access control is coarse-grained (read all or nothing).
   - _Solution B (API key + usage-class tags):_ Products are tagged with `UsageClass` (from Section 13.7); `ApiKeyMiddleware` enforces that the caller's key has permission for the product's usage class.
   - **Recommendation:** Solution B. The existing `ApiKeyMiddleware` provides the enforcement point; extend it with a simple role lookup against a `config/api-keys.json` file mapping key to allowed usage classes.

3. **How should breaking changes to a data product be communicated to consumers?**
   - _Solution A (version bump only):_ Increment `SchemaVersion` in the manifest; consumers are responsible for monitoring.
   - _Solution B (changelog + deprecation notice):_ Each product version includes a `ChangeLog` section in its manifest and an `DeprecatedAfter` date. The serving layer returns a `Deprecation` HTTP header for requests against deprecated versions.
   - **Recommendation:** Solution B. The `DeprecationHeader` pattern is standard HTTP practice and requires minimal implementation effort. Add `DeprecatedAfter` to `PackageManifest`.

4. **Should the first implementation target internal research consumers only, or should external redistribution be a day-one goal?**
   - _Solution A (internal first):_ Simpler access model; no redistribution licensing concerns. Validate the data product abstraction internally before exposing externally.
   - _Solution B (external from day one):_ Requires redistribution policy, entitlement checks, and usage audit from the start.
   - **Recommendation:** Solution A. Build the product abstraction and access control against internal consumers first; add a `DistributionScope` flag (`Internal` | `Licensed` | `Public`) to the manifest for future use.

5. **What is the minimum viable "data product" definition?**
   - _Option A (schema + SLO only):_ A product is a declared schema and quality guarantee. Consumers query for data; the platform retrieves it dynamically.
   - _Option B (schema + SLO + packaged snapshot):_ A product is a versioned, pre-packaged data snapshot with embedded metadata. Consumers download or stream the package.
   - _Option C (schema + SLO + transform + serving logic):_ Full data product with embedded computation; the platform reproduces the product on demand.
   - **Recommendation:** Option B for the first release using the existing `PortableDataPackager`. Option C is appropriate only once a query engine (idea 3.2) is operational.

---

### 16.8 WAL and ledger coexistence

**Context:** The existing `WriteAheadLog` provides crash-safety for event ingestion. The proposed immutable ledger provides audit, dedup, and replay guarantees. These are related but not identical concerns and must be designed to coexist without duplicating storage or logic.

**Questions and candidate solutions:**

1. **Should the WAL serve as the ledger, or should the ledger supersede the WAL?**
   - _Solution A (WAL is the ledger):_ Extend `WriteAheadLog` with hash computation and immutability guarantees. One component handles both crash-safety and audit.
   - _Solution B (WAL feeds ledger):_ The WAL continues to handle durability (survive crashes). A separate `LedgerPromotion` step reads committed WAL entries and writes them to an immutable ledger after successful canonicalization.
   - **Recommendation:** Solution B. The WAL is optimized for write throughput and recovery; the ledger is optimized for query, audit, and integrity. Conflating the two would compromise both. The promotion step mirrors the WAL→canonical-sink flow that already exists.

2. **When should WAL entries be promoted to the ledger — immediately after write, or after a configurable durability checkpoint?**
   - _Solution A (immediate promotion):_ WAL entry → ledger write happen in the same transaction. Higher latency; simpler consistency model.
   - _Solution B (checkpoint-based):_ WAL entries are batched and promoted to the ledger every N seconds or N events. Lower latency on the hot path; requires reconciliation on restart.
   - **Recommendation:** Solution B with a checkpoint interval of 1 second (configurable). This matches the existing WAL flush behavior and avoids blocking the ingestion hot path.

3. **How should the ledger handle WAL entries that failed canonicalization (e.g., malformed payloads)?**
   - _Solution A (skip):_ Failed entries are discarded; only successfully canonicalized events enter the ledger.
   - _Solution B (error ledger):_ Failed entries are written to a separate `_ledger_errors.jsonl` file with the error reason and raw payload hash, enabling forensic investigation.
   - **Recommendation:** Solution B. The error ledger is essential for diagnosing canonicalization failures and provider payload changes. Use `AtomicFileWriter` for the error file.

---

### 16.9 Testing and validation strategy

**Context:** The new components (consensus engine, self-healing, unified contract, diagnostics assistant) require testing strategies beyond simple unit tests. Determinism and edge-case coverage are critical given the financial data context.

**Questions and candidate solutions:**

1. **How should consensus scoring correctness be validated?**
   - _Solution A (unit tests with fixed inputs):_ Pre-define provider event sets and verify the expected confidence score. Fast; does not cover emergent disagreement patterns.
   - _Solution B (property-based tests):_ Use a property-based testing library (e.g., FsCheck, which is compatible with the existing F# test suite) to generate random provider agreement/disagreement scenarios and verify that confidence scores satisfy invariants (monotonically increasing with agreement count; ≤ 1.0; ≥ 0.0).
   - **Recommendation:** Solution B. Property-based tests for the scoring invariants catch edge cases that fixed inputs miss. Add a new `ConsensusEngineTests.fs` in `MarketDataCollector.FSharp.Tests` where the pure scoring function lives.

2. **How should self-healing policy regression be tested to ensure a rule change does not trigger unexpected remediation?**
   - _Solution A (snapshot tests):_ Record policy evaluation outputs for a fixed set of inputs; fail the test if the output changes.
   - _Solution B (scenario tests):_ Define named remediation scenarios (e.g., "5-minute gap in SPY, Alpaca rate limit reached, 3 other symbols affected") and assert that the policy engine produces the expected action set.
   - **Recommendation:** Solution B. Scenario tests are more readable and maintain better intent documentation. Use `SampleDataGenerator` to produce the input events; assert the action set via the `RemediationAuditLog` output.

3. **How should replay determinism be validated — can the exact same output be guaranteed across runs and deployments?**
   - _Solution A (hash comparison):_ Replay a fixed window, hash the output, and compare against a committed golden hash. Fails if any serialization or ordering change occurs.
   - _Solution B (field-level diff):_ Replay the window twice (possibly on different machines/runs) and compare field-by-field, emitting the diff. Allows tolerance for benign differences (e.g., wall-clock ingestion timestamps).
   - **Recommendation:** Solution B. Golden hashes are too brittle (any library version bump can change serialization and break the test). Field-level diff with a configurable ignore list for non-deterministic fields is more maintainable.

4. **What load and latency benchmarks should gate promotion of consensus scoring to production?**
   - _Proposal:_ Add a `ConsensusEngineBenchmarks` class to `MarketDataCollector.Benchmarks` measuring: (a) throughput (events/second through the consensus window buffer at 1, 2, and 3 providers), (b) median and P99 latency added by the consensus step, and (c) memory overhead of the in-flight window buffers.
   - _Acceptance criteria:_ P99 added latency < 100 ms; throughput degradation < 10% vs. single-provider baseline; memory overhead < 50 MB at 1,000 concurrent symbols.

---

### 16.10 Performance targets and observability instrumentation

**Context:** The platform currently lacks explicit performance budgets for the new components. Without defined targets, it is impossible to determine whether an implementation meets production requirements.

**Questions and candidate solutions:**

1. **What are the performance budgets for the immutable ledger hash computation step?**
   - _Proposal:_ Hash computation (xxHash128 for dedup + SHA-256 for integrity) on a 512-byte canonical event should complete in < 5 µs on target hardware. This is well within the margins of typical ingestion pipelines. Add a benchmark in `EventPipelineBenchmarks` to validate this.
   - _Monitoring:_ Expose `ledger.hash_compute_duration_ns` as a Prometheus gauge via `PrometheusMetrics`.

2. **What end-to-end latency target should the unified serving contract meet for historical time-travel queries?**
   - _Proposal:_ For a time-range query covering 1 hour of data for a single symbol, the P95 response latency target should be ≤ 500 ms from the `HistoricalEndpoints` handler. For a full trading day, ≤ 2 s. These targets should be measured and tracked in `HistoricalEndpointTests`.
   - _Monitoring:_ Add `query.historical.duration_ms` histogram to `PrometheusMetrics`.

3. **How should the quality of new observability instrumentation be enforced?**
   - _Solution A (convention):_ Document that new services must emit at least one counter, one gauge, and one histogram via `PrometheusMetrics`. Reviews enforce this by convention.
   - _Solution B (test assertion):_ Add a test in `MetricsEndpointTests` that queries `/api/metrics` after exercising each new component and asserts that the expected metric names are present.
   - **Recommendation:** Solution B. Convention-only enforcement degrades over time; an automated assertion in the test suite enforces it permanently.

4. **What are the targets for automated self-healing mean-time-to-remediation (MTTR)?**
   - _Proposal:_ Define SLOs in `docs/operations/service-level-objectives.md`:
     - Detect gap → trigger backfill: ≤ 5 minutes.
     - Backfill complete + quality score restored: ≤ 30 minutes for windows ≤ 4 hours; ≤ 2 hours for windows ≤ 1 day.
     - Audit log entry written: within 60 seconds of remediation completion.
   - These SLOs should be tracked via `DataFreshnessSlaMonitor` extended for remediation events.
