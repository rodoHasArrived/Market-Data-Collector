# L3 Inference & Queue-Aware Execution Backtesting: Implementation Plan

## 1) Objective

Implement a queue-aware execution simulation layer that infers **L3-like queue dynamics** from existing stored data:

- L2 order-book updates/snapshots
- Trade executions with aggressor side
- Derived order-flow statistics
- Integrity/sequence diagnostics

The implementation should enable realistic historical backtests of passive and aggressive execution strategies while clearly labeling outputs as **inferred** (not true per-order L3).

---

## 2) Current-State Summary (Repository Alignment)

The repository already has the required raw ingredients:

- `MarketDepthUpdate` deltas (`Position`, `Operation`, `Side`, `Price`, `Size`, `MarketMaker`, `SequenceNumber`) used to build L2 state.
- `MarketDepthCollector` that validates sequencing/consistency and emits `LOBSnapshot` events.
- `TradeDataCollector` emitting `Trade` and `OrderFlowStatistics`.
- JSONL storage sink and replay tools capable of reading historical events.

This plan layers an inference + simulation engine on top of those capabilities.

---

## 3) Scope

### In Scope

1. Deterministic reconstruction of historical L2 state timeline from stored events.
2. L3 inference model producing probabilistic queue-ahead and fill likelihood estimates.
3. Queue-aware execution simulator for backtests:
   - Passive posting (join/cancel/replace)
   - Aggressive crossing
   - Partial fills
4. Calibration and validation tooling.
5. API/CLI surfaces for running simulations and exporting reports.
6. Documentation and guardrails around inference confidence.

### Out of Scope (Phase 1)

- True exchange-grade L3 replay based on real order IDs/FIFO identity.
- Venue-specific micro-priority rules beyond configurable approximations.
- Live trading auto-routing changes (backtest-first delivery).

---

## 4) High-Level Architecture

Add a new bounded context under `Application` + `Storage/Replay` integration:

1. **Event Reconstruction Layer**
   - Reads historical events in timestamp+sequence order.
   - Produces canonical timeline:
     - book state transitions
     - trade events
     - integrity events

2. **L3 Inference Layer**
   - Converts observed L2+trade transitions into inferred queue mechanics:
     - queue depletion rates
     - cancel intensity
     - refill intensity
     - expected queue-ahead progression

3. **Execution Simulation Layer**
   - Receives strategy child-order intents.
   - Simulates fills against reconstructed L2 state + inferred queue process.
   - Outputs fill tape, slippage metrics, queue diagnostics.

4. **Calibration & Evaluation Layer**
   - Calibrates model parameters by symbol/venue/time regime.
   - Evaluates quality with holdout windows.

5. **Interface Layer**
   - CLI command(s), optional HTTP endpoints, export format for analysis.

---

## 5) Data Model Additions

Create new contracts under `src/MarketDataCollector.Contracts/...`:

1. `InferredQueueState`
   - `Timestamp`, `Symbol`, `Side`, `Price`, `DisplayedSize`
   - `EstimatedQueueAhead`, `EstimatedQueueBehind`
   - `CancelRate`, `RefillRate`, `TradeConsumptionRate`
   - `ConfidenceScore` (0-1)

2. `ExecutionSimulationRequest`
   - Symbol/date range
   - Strategy order intents input source
   - Model profile/parameters
   - Latency assumptions
   - Venue behavior profile

3. `ExecutionSimulationResult`
   - Fill events (timestamp/price/qty/reason)
   - Order lifecycle states
   - Slippage/implementation shortfall
   - Queue trajectory diagnostics
   - Confidence and warnings

4. `InferenceModelConfig`
   - Priors for cancel/refill split
   - Trade-to-depth attribution policy
   - Queue initialization policy
   - Confidence thresholds and fallback policy

Serialization registration updates:
- Extend JSON source generation context for new model types.
- Ensure backward-compatible schema versioning for result artifacts.

---

## 6) Reconstruction Engine Plan

## 6.1 Event Ordering

- Reuse `JsonlReplayer` + existing storage policy parsing.
- Build deterministic merge order using:
  1) event timestamp
  2) source/stream sequence number when present
  3) stable file offset tie-breaker

## 6.2 Book State Timeline

- Re-apply L2 updates to reconstruct step-wise snapshots.
- Where only snapshots are available, treat each as authoritative state replacement.
- Track integrity flags (gap, out-of-order, stale).

## 6.3 Trade Alignment

- Align trades to nearest book state in event-time with configurable tolerance window.
- Produce attribution labels:
  - likely consumed best bid/ask
  - uncertain / ambiguous

Deliverable: `ReconstructedMarketTimeline` internal model.

---

## 7) L3 Inference Model Plan

## 7.1 Core Inference Principle

At each price level, observed Δdisplayed size is decomposed into:

- trade-consumed volume
- cancel volume
- add/refill volume

Given only L2+trades, decomposition is probabilistic.

## 7.2 Baseline Model (Phase 1)

Use a tractable state-space/EM style model:

1. **Observation Equation**
   - From consecutive states and aligned trades.
2. **Latent Components**
   - `cancel_ahead`, `cancel_behind`, `new_ahead`, `new_behind`.
3. **Parameter Estimation**
   - Per symbol/venue/session bucket (e.g., open, mid, close).
4. **Output**
   - Expected queue-ahead progression + variance/confidence.

## 7.3 Heuristic Fallback Model

When data quality is weak:

- Conservative assumptions (slower fills, more adverse queue insertion ahead).
- Deterministic lower-bound and upper-bound fill envelopes.

## 7.4 Confidence Scoring

Compute confidence from:

- sequence continuity quality
- trade-book alignment rate
- residual error of observation fit
- market regime stability

Expose confidence with every simulated fill batch.

---

## 8) Execution Simulator Plan

## 8.1 Simulator Inputs

- `ReconstructedMarketTimeline`
- `InferenceModelConfig`
- Strategy-generated parent/child order intents
- Latency model (decision-to-exchange, exchange-to-observation)

## 8.2 Order Types (Phase 1)

- Limit (post-only / regular)
- Market
- Cancel/replace

## 8.3 Queue Position Mechanics

For passive orders:

1. On entry, estimate queue-ahead at price level.
2. Advance queue-ahead as inferred consumption/cancellation occurs.
3. Fill when queue-ahead crosses zero and contra flow exists.
4. Support partial fills and remainders.

For aggressive orders:

- Sweep displayed book levels with configurable slippage/latency penalties.

## 8.4 Latency & Staleness Handling

- Inject configurable latency distributions.
- On low confidence/integrity breaks, apply conservative execution degradation or skip period.

## 8.5 Output Artifacts

- Fill tape (`.jsonl` + optional parquet)
- Order lifecycle log
- Summary metrics report

---

## 9) API / CLI Surface Plan

## 9.1 CLI

Add command group examples:

- `mdc simulate-execution --symbol AAPL --from 2026-01-01 --to 2026-01-31 --model baseline`
- `mdc calibrate-queue-model --symbol AAPL --lookback 30d`

## 9.2 HTTP Endpoints (optional Phase 1.5)

- `POST /api/sim/execution/run`
- `GET /api/sim/execution/{id}`
- `POST /api/sim/execution/calibrate`

## 9.3 UI Integration (later)

- Results viewer with confidence overlays and queue trajectory charts.

---

## 10) Validation & Testing Strategy

## 10.1 Unit Tests

- Event merge/ordering determinism
- Depth replay invariants
- Queue progression math
- Confidence score bounds and monotonic behavior

## 10.2 Property-Based Tests

- Non-negative queue sizes
- Conservation constraints in decomposition
- Fill quantity never exceeds available/inferred executable quantity

## 10.3 Scenario/Golden Tests

- Synthetic streams where latent queue truth is known
- Regression fixtures for high-volatility sessions

## 10.4 Performance Tests

- Throughput target for replay + simulation on daily multi-symbol datasets
- Memory bounds under long sessions

## 10.5 Acceptance Metrics

- Calibration residuals below threshold
- Stable slippage distributions across reruns
- Deterministic replay/sim outputs with same seed/config

---

## 11) Rollout Plan

## Phase 0 — Foundations (1 sprint)

- New contracts/configs
- Reconstruction timeline service
- Test fixtures and synthetic data generator for queue truth cases

## Phase 1 — Baseline Inference + Simulator (2 sprints)

- Baseline probabilistic model
- Queue-aware fill engine
- CLI run command + result export

## Phase 2 — Calibration & Confidence (1 sprint)

- Calibration command
- Confidence scoring and fallback policy
- Evaluation reports

## Phase 3 — Integration Hardening (1 sprint)

- API integration
- docs + runbooks
- performance optimization

---

## 12) Concrete Repository Work Breakdown

1. **Contracts**
   - Add new DTOs/records for simulation request/result/config.
   - Add enum types for policy knobs.

2. **Application**
   - `Simulation/Reconstruction/*`
   - `Simulation/Inference/*`
   - `Simulation/Execution/*`
   - `Simulation/Calibration/*`

3. **Storage/Replay**
   - Extend replay readers to expose stable offsets and merged iterators.

4. **Commands/Endpoints**
   - Add CLI commands in command modules.
   - Optional endpoint mapping in shared UI endpoints project.

5. **Serialization**
   - Register new serializable types in source-generated JSON context.

6. **Tests**
   - New test project folders under existing test suites:
     - `Simulation/ReconstructionTests`
     - `Simulation/InferenceTests`
     - `Simulation/ExecutionTests`

7. **Docs**
   - Architecture doc for inference assumptions.
   - User guide for running backtest simulations and interpreting confidence.

---

## 13) Risks & Mitigations

1. **Risk: Overstated realism**
   - Mitigation: strict labeling as inferred; confidence + conservative fallback.

2. **Risk: Data quality gaps reduce usefulness**
   - Mitigation: integrity-aware period filtering and quality thresholds.

3. **Risk: Model complexity/performance tradeoff**
   - Mitigation: modular baseline heuristic model first; advanced model behind feature flag.

4. **Risk: Venue behavior differences**
   - Mitigation: venue profiles with default generic assumptions.

---

## 14) Definition of Done (Phase 1)

- Deterministic L2 timeline reconstruction from archived data.
- Queue-aware simulation produces fill tapes for limit/market orders.
- Baseline inference parameters calibratable per symbol bucket.
- Confidence score present and surfaced in outputs.
- Test coverage for correctness invariants and deterministic behavior.
- End-user documentation for setup, run, and interpretation.

---

## 15) Recommended First PR Sequence

1. PR1: Contracts + JSON context registration + scaffolding tests.
2. PR2: Reconstruction engine + deterministic replay tests.
3. PR3: Baseline inference model + unit/property tests.
4. PR4: Execution simulator + CLI command + golden tests.
5. PR5: Calibration + confidence scoring + docs.

This keeps risk isolated and reviewable while producing usable intermediate milestones.
