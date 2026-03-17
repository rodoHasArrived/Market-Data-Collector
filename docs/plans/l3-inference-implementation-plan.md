# L3 Inference & Queue-Aware Execution Backtesting: Implementation Plan

**Version:** 1.1
**Last Updated:** 2026-03-17
**Audience:** Quantitative researchers, execution analysts, core contributors

---

## 1) Objective

Implement a queue-aware execution simulation layer that infers **L3-like queue dynamics** from existing stored data:

- L2 order-book updates/snapshots
- Trade executions with aggressor side
- Derived order-flow statistics
- Integrity/sequence diagnostics

The implementation should enable realistic historical backtests of passive and aggressive execution strategies while clearly labeling outputs as **inferred** (not true per-order L3).

---

## 2) End-User Scenarios & Value

This section describes the concrete workflows that a quantitative researcher, algorithmic trader, or execution analyst can perform once this feature is implemented.

### 2.1 Who Is This For?

| User Type | Primary Use Case |
|-----------|-----------------|
| **Execution researcher** | Evaluate whether a passive limit-order strategy fills faster or slower than a market order across a historical date range |
| **Quant developer** | Integrate fill-tape output with a QuantConnect/Lean strategy backtest to get execution-realistic P&L |
| **Trading desk analyst** | Benchmark execution quality of live fills against simulated baseline fills on the same day |
| **Data scientist** | Analyse slippage distributions and queue-ahead estimation accuracy across market regimes |

### 2.2 Primary User Workflows

#### Workflow A — Simulate execution for a single symbol

A researcher wants to understand how a passive AAPL limit-order strategy would have performed in January 2026.

**Prerequisites:** L2 depth data and trade ticks for AAPL must already be collected and stored (see [Section 3](#prerequisites--data-requirements)).

**Steps:**
1. Calibrate the queue model for AAPL using the prior month's data:
   ```bash
   dotnet run --project src/MarketDataCollector -- \
     --calibrate-queue-model \
     --symbols AAPL \
     --calibrate-from 2025-12-01 \
     --calibrate-to 2025-12-31
   ```
2. Run the execution simulation for January 2026:
   ```bash
   dotnet run --project src/MarketDataCollector -- \
     --simulate-execution \
     --symbols AAPL \
     --sim-from 2026-01-01 \
     --sim-to 2026-01-31 \
     --sim-model baseline \
     --sim-orders ./my-orders.jsonl \
     --sim-output ./results/aapl-jan2026
   ```
3. Inspect results:
   - `./results/aapl-jan2026/fill-tape.jsonl` — tick-by-tick fill events
   - `./results/aapl-jan2026/summary.json` — slippage, fill rate, confidence summary
   - `./results/aapl-jan2026/queue-diagnostics.jsonl` — per-order queue trajectory

#### Workflow B — Multi-symbol batch simulation for a strategy backtest

A quant developer runs a batch simulation across a portfolio of symbols to feed a QuantConnect strategy.

```bash
dotnet run --project src/MarketDataCollector -- \
  --simulate-execution \
  --symbols SPY,QQQ,IWM,AAPL,MSFT \
  --sim-from 2026-01-01 \
  --sim-to 2026-03-01 \
  --sim-model baseline \
  --sim-orders ./strategy-orders.jsonl \
  --sim-output ./results/portfolio-q1-2026 \
  --sim-export-format parquet \
  --sim-latency-us 250
```

Output is exported as Parquet files compatible with the existing QuantConnect Lean integration (see `docs/integrations/lean-integration.md`).

#### Workflow C — Compare passive vs. aggressive strategies

Run two simulations with different order intent files and compare summary statistics to determine which strategy had lower implementation shortfall.

```bash
# Passive strategy
dotnet run --project src/MarketDataCollector -- \
  --simulate-execution --symbols AAPL \
  --sim-from 2026-01-02 --sim-to 2026-01-31 \
  --sim-orders ./passive-orders.jsonl \
  --sim-output ./results/passive

# Aggressive strategy
dotnet run --project src/MarketDataCollector -- \
  --simulate-execution --symbols AAPL \
  --sim-from 2026-01-02 --sim-to 2026-01-31 \
  --sim-orders ./aggressive-orders.jsonl \
  --sim-output ./results/aggressive
```

#### Workflow D — Check data quality before simulating

Before running a simulation, check whether your stored data has sufficient quality for reliable inference:

```bash
dotnet run --project src/MarketDataCollector -- \
  --simulate-execution --dry-run \
  --symbols AAPL \
  --sim-from 2026-01-01 \
  --sim-to 2026-01-31
```

The dry-run reports per-day data coverage, sequence gap rates, and an estimated confidence floor before any computation is done.

#### Workflow E — Strategy parameter sensitivity analysis

A quant developer wants to find the optimal limit price aggression level for AAPL passive orders without manually creating multiple order-intent files. The `--sim-grid-search` mode parameterises a template order over a configurable grid and runs all variants in a single reconstruction pass.

```bash
dotnet run --project src/MarketDataCollector -- \
  --simulate-execution \
  --symbols AAPL \
  --sim-from 2026-01-01 \
  --sim-to 2026-01-31 \
  --sim-orders ./template-orders.jsonl \
  --sim-grid-search \
  --sim-grid-param "limitOffsetTicks=-2,-1,0,1,2" \
  --sim-grid-param "cancelAfterMinutes=5,10,20" \
  --sim-output ./results/aapl-grid
```

The reconstruction timeline is built **once** and reused across all 15 parameter combinations. Output includes the standard per-run artifacts plus a `sensitivity-report.json` ranking each combination by fill rate, slippage, and implementation shortfall:

```json
// sensitivity-report.json (excerpt)
{
  "gridDimensions": {"limitOffsetTicks": [-2,-1,0,1,2], "cancelAfterMinutes": [5,10,20]},
  "ranked": [
    {"limitOffsetTicks": -1, "cancelAfterMinutes": 10, "fillRate": 0.87, "avgSlippageBps": 0.01, "rank": 1},
    {"limitOffsetTicks":  0, "cancelAfterMinutes": 10, "fillRate": 0.83, "avgSlippageBps": 0.02, "rank": 2}
  ],
  "isInferred": true
}
```

> **CLI flag reference:** `--sim-grid-search` activates grid mode; `--sim-grid-param` accepts `"paramName=v1,v2,..."` and may be repeated; `--sim-grid-max N` sets a safety limit (default: 50 combinations) requiring explicit override above that count.

### 2.3 Key User-Facing Outputs

| Output | Format | Purpose |
|--------|--------|---------|
| Fill tape | `.jsonl` / `.parquet` | Each simulated fill event with timestamp, price, qty, reason |
| Order lifecycle log | `.jsonl` | Open, partial fill, full fill, cancelled states per order |
| Summary report | `.json` | Aggregate slippage, fill rate, confidence score, IS decomposition, warnings |
| Queue diagnostics | `.jsonl` | Per-order queue-ahead trajectory over time |
| Calibration report | `.json` | Model parameters and calibration quality per symbol/session |
| Sensitivity report | `.json` | Ranked parameter grid results (fill rate, IS) when `--sim-grid-search` is used |
| Comparison report | `.json` | Statistical A/B comparison of two simulation runs (bootstrap CIs, p-value) |

---

## 3) Prerequisites & Data Requirements

Before running a simulation, the relevant historical L2 depth and trade data **must be present in local storage**. Use `--backfill` or real-time collection to populate it.

### 3.1 Required Data Types

| Data Type | CLI to Collect | Minimum Recommended History |
|-----------|---------------|----------------------------|
| L2 depth updates (`MarketDepthUpdate`) | `--depth-levels 10` during collection | ≥ 10 trading days for calibration |
| Trade ticks (`Trade`) | enabled by default | ≥ 10 trading days for calibration |
| Order-flow statistics | computed automatically from the above | — |

> **Note:** Providers that supply L2 depth data include Interactive Brokers, Polygon, NYSE, and StockSharp. Providers that supply only daily OHLCV bars (e.g. Stooq, Yahoo Finance) are **not sufficient** for queue inference — depth tick data is required.

### 3.2 Minimum Data Quality Thresholds

The inference engine evaluates data quality before processing. Simulations covering periods that fall below these thresholds are marked as low-confidence or skipped (configurable):

| Metric | Recommended Minimum | Behavior if Below Threshold |
|--------|--------------------|-----------------------------|
| Sequence gap rate | < 0.5% of depth events | Falls back to heuristic model |
| Trade-book alignment rate | > 85% of trades matched to a depth state | Widens fill-time uncertainty |
| L2 snapshot coverage | At least one full-book snapshot per session | Required; period skipped if absent |
| Book state transitions per minute | > 10 | Marks period as thin/illiquid |

### 3.3 Verify Data Before Simulating

```bash
# Check what L2 + trade data you have stored for AAPL
dotnet run --project src/MarketDataCollector -- \
  --symbol-status AAPL

# Run dry-run to see quality assessment without computing fills
dotnet run --project src/MarketDataCollector -- \
  --simulate-execution --dry-run \
  --symbols AAPL \
  --sim-from 2026-01-01 \
  --sim-to 2026-01-31
```

Expected dry-run output:
```
L3 Simulation Dry-Run: AAPL 2026-01-01 → 2026-01-31
────────────────────────────────────────────────────
Trading days found      : 21 / 21
Days with L2 depth data : 21 (100.0%)
Days with trade data    : 21 (100.0%)
Avg sequence gap rate   : 0.12%  [GOOD]
Avg trade alignment     : 93.4%  [GOOD]
Estimated confidence    : 0.88   [HIGH]
Heuristic fallback days : 0

Ready to simulate. Run without --dry-run to proceed.
```

---

## 4) Current-State Summary (Repository Alignment)

The repository already has the required raw ingredients:

- `MarketDepthUpdate` deltas (`Position`, `Operation`, `Side`, `Price`, `Size`, `MarketMaker`, `SequenceNumber`) used to build L2 state.
- `MarketDepthCollector` that validates sequencing/consistency and emits `LOBSnapshot` events.
- `TradeDataCollector` emitting `Trade` and `OrderFlowStatistics`.
- JSONL storage sink and replay tools capable of reading historical events.

This plan layers an inference + simulation engine on top of those capabilities.

---

## 5) Scope

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

## 6) High-Level Architecture

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

## 7) Data Model Additions

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

## 8) Reconstruction Engine Plan

## 8.1 Event Ordering

- Reuse `JsonlReplayer` + existing storage policy parsing.
- Build deterministic merge order using:
  1) event timestamp
  2) source/stream sequence number when present
  3) stable file offset tie-breaker

## 8.2 Book State Timeline

- Re-apply L2 updates to reconstruct step-wise snapshots.
- Where only snapshots are available, treat each as authoritative state replacement.
- Track integrity flags (gap, out-of-order, stale).

## 8.3 Trade Alignment

- Align trades to nearest book state in event-time with configurable tolerance window.
- Produce attribution labels:
  - likely consumed best bid/ask
  - uncertain / ambiguous

Deliverable: `ReconstructedMarketTimeline` internal model.

---

## 9) L3 Inference Model Plan

## 9.1 Core Inference Principle

At each price level, observed Δdisplayed size is decomposed into:

- trade-consumed volume
- cancel volume
- add/refill volume

Given only L2+trades, decomposition is probabilistic.

## 9.2 Baseline Model (Phase 1)

Use a tractable state-space/EM style model:

1. **Observation Equation**
   - From consecutive states and aligned trades.
2. **Latent Components**
   - `cancel_ahead`, `cancel_behind`, `new_ahead`, `new_behind`.
3. **Parameter Estimation**
   - Per symbol/venue/session bucket (e.g., open, mid, close).
4. **Output**
   - Expected queue-ahead progression + variance/confidence.

## 9.3 Iceberg / Hidden-Order Detection (Phase 1.5)

When a price level's displayed size repeatedly drops to near-zero and immediately refills to a nearly identical quantity, this is a strong iceberg fingerprint. Treating these replenishments as ordinary new-order arrivals inflates cancel rate estimates and causes passive fills to be modelled as arriving faster than they will in reality.

A lightweight iceberg detector watches for sawtooth patterns (peak → depletion → same-size peak) in each level's displayed-size history. When detected:
- `InferredQueueState` gains `IsLikelyIceberg = true` and `EstimatedHiddenReserve` at that level.
- Queue-ahead for passive orders resting at or behind an iceberg level is adjusted upward.
- `queue-diagnostics.jsonl` gains `icebergLevelsDetected` per order.

Gate behind `InferenceModelConfig.EnableIcebergDetection: false` (opt-in) to preserve Phase 1 behaviour. The heuristic requires confirmation from at least two trade-consumption cycles to suppress false positives in illiquid markets.

## 9.4 Regime-Aware Calibration (Phase 2)

Session buckets (open / mid / close) are the Phase 1 calibration segments. Phase 2 extends this to **intraday regime segments** — calm, moderate, stress — by clustering calibration-window observations on bid-ask spread, book imbalance, and 1-minute trade arrival rate. Each calibration session produces N regime-specific parameter sets. During simulation, the active regime is inferred from a rolling 15-minute window and the matching parameter set is loaded dynamically.

CLI: `--calibrate-regimes 3`. The `calibration-report.json` gains a `regimeBreakdown` array. Minimum recommended calibration history increases from 10 to 30 trading days when this option is active.

## 9.5 Heuristic Fallback Model

When data quality is weak:

- Conservative assumptions (slower fills, more adverse queue insertion ahead).
- Deterministic lower-bound and upper-bound fill envelopes.

## 9.6 Confidence Scoring

Compute confidence from:

- sequence continuity quality
- trade-book alignment rate
- residual error of observation fit
- market regime stability

Expose confidence with every simulated fill batch.

---

## 10) Execution Simulator Plan

## 10.1 Simulator Inputs

- `ReconstructedMarketTimeline`
- `InferenceModelConfig`
- Strategy-generated parent/child order intents
- Latency model (decision-to-exchange, exchange-to-observation)

## 10.2 Order Types (Phase 1)

- Limit (post-only / regular)
- Market
- Cancel/replace

## 10.3 Queue Position Mechanics

For passive orders:

1. On entry, estimate queue-ahead at price level.
2. Advance queue-ahead as inferred consumption/cancellation occurs.
3. Fill when queue-ahead crosses zero and contra flow exists.
4. Support partial fills and remainders.

For aggressive orders:

- Sweep displayed book levels with configurable slippage/latency penalties.

## 10.4 Latency & Staleness Handling

- Inject configurable latency distributions.
- On low confidence/integrity breaks, apply conservative execution degradation or skip period.

## 10.5 Output Artifacts

- Fill tape (`.jsonl` + optional parquet)
- Order lifecycle log
- Summary metrics report

## 11) API / CLI Surface Plan

## 11.1 CLI — Full Reference

All simulation commands follow the same pattern as existing commands (`--backfill`, `--package`). They are invoked via:

```bash
dotnet run --project src/MarketDataCollector -- <flags>
```

### Simulation command (`--simulate-execution`)

| Flag | Type | Required | Description |
|------|------|----------|-------------|
| `--simulate-execution` | bool | Yes | Enable simulation mode |
| `--symbols` | list | Yes | Comma-separated symbol list, e.g. `AAPL,MSFT` |
| `--sim-from` | date | Yes | Start date (inclusive), `YYYY-MM-DD` |
| `--sim-to` | date | Yes | End date (inclusive), `YYYY-MM-DD` |
| `--sim-orders` | path | Yes | Path to JSONL file of order intents (see format below) |
| `--sim-output` | path | No | Directory for result artifacts (default: `./sim-output/<timestamp>`) |
| `--sim-model` | string | No | Inference model profile: `baseline` (default), `heuristic` |
| `--sim-latency-us` | int | No | Round-trip latency assumption in microseconds (default: `500`) |
| `--sim-export-format` | string | No | Output format: `jsonl` (default), `parquet`, `both` |
| `--sim-confidence-min` | float | No | Skip periods where confidence falls below threshold (default: `0.3`) |
| `--sim-venue-profile` | string | No | Venue behavior profile: `generic` (default), `nasdaq`, `nyse` |
| `--dry-run` | bool | No | Preview data quality and coverage without computing fills |

### Calibration command (`--calibrate-queue-model`)

| Flag | Type | Required | Description |
|------|------|----------|-------------|
| `--calibrate-queue-model` | bool | Yes | Enable calibration mode |
| `--symbols` | list | Yes | Symbols to calibrate |
| `--calibrate-from` | date | Yes | Start of calibration window |
| `--calibrate-to` | date | Yes | End of calibration window |
| `--calibrate-output` | path | No | Directory to write calibration parameters (default: `./data/calibration/`) |
| `--calibrate-session` | string | No | Session buckets: `all` (default), `open`, `mid`, `close` |

### Order intent file format (input)

The `--sim-orders` input is a JSONL file where each line is one order intent:

```jsonc
// One record per line
{
  "orderId": "order-001",
  "symbol": "AAPL",
  "side": "Buy",          // "Buy" or "Sell"
  "orderType": "Limit",   // "Limit", "Market", "LimitPostOnly"
  "quantity": 100,
  "limitPrice": 185.50,   // omit for Market orders
  "submitAt": "2026-01-02T09:35:00Z",
  "cancelAt": "2026-01-02T09:45:00Z"  // optional cancel deadline
}
```

### Step-by-step: first simulation walkthrough

```bash
# Step 1 — Ensure you have L2 + trade data collected (e.g. via Polygon or IB)
dotnet run --project src/MarketDataCollector -- \
  --symbol-status AAPL

# Step 2 — Validate data quality for the simulation period
dotnet run --project src/MarketDataCollector -- \
  --simulate-execution --dry-run \
  --symbols AAPL \
  --sim-from 2026-01-01 \
  --sim-to 2026-01-31

# Step 3 — Calibrate the model using the prior month
dotnet run --project src/MarketDataCollector -- \
  --calibrate-queue-model \
  --symbols AAPL \
  --calibrate-from 2025-12-01 \
  --calibrate-to 2025-12-31

# Step 4 — Run the simulation
dotnet run --project src/MarketDataCollector -- \
  --simulate-execution \
  --symbols AAPL \
  --sim-from 2026-01-01 \
  --sim-to 2026-01-31 \
  --sim-orders ./my-orders.jsonl \
  --sim-output ./results/aapl-jan

# Step 5 — Review results
cat ./results/aapl-jan/summary.json
```

### Expected terminal output during a simulation

```
L3 Queue-Aware Execution Simulation
════════════════════════════════════════════════════════════
Symbol(s)  : AAPL
Period     : 2026-01-01 → 2026-01-31 (21 trading days)
Model      : baseline
Orders     : 47 order intents loaded from ./my-orders.jsonl
Output     : ./results/aapl-jan
════════════════════════════════════════════════════════════

[14:02:01] Reconstructing event timeline...  done (2.3s, 4,812,091 events)
[14:02:03] Running inference model (AAPL)...  done (1.1s)
[14:02:04] Simulating order fills...          done (0.4s)
[14:02:04] Exporting results...               done

════════════════════════════════════════════════════════════
SIMULATION COMPLETE — AAPL 2026-01-01 → 2026-01-31
════════════════════════════════════════════════════════════
Orders submitted            : 47
  Full fills                : 39  (83.0%)
  Partial fills             : 5   (10.6%)
  Not filled (expired)      : 3   (6.4%)
Avg time to fill (passive)  : 4.2 min
Avg slippage (limit orders) : 0.02 bps (implementation shortfall)
Overall confidence score    : 0.86  [HIGH]
Low-confidence periods      : 0 days skipped
════════════════════════════════════════════════════════════
Results written to: ./results/aapl-jan/
  fill-tape.jsonl          (47.3 KB)
  order-lifecycle.jsonl    (12.1 KB)
  summary.json             (2.8 KB)
  queue-diagnostics.jsonl  (103.7 KB)
```

## 11.2 Output File Formats

### `fill-tape.jsonl` — one fill event per line

```jsonc
{
  "fillId": "fill-001-1",
  "orderId": "order-001",
  "symbol": "AAPL",
  "fillTimestamp": "2026-01-02T09:41:23.456Z",
  "fillPrice": 185.48,
  "fillQty": 60,
  "remainingQty": 40,
  "reason": "QueueDepleted",           // "QueueDepleted", "AggressiveCross", "PartialCancel"
  "inferredQueueAheadAtFill": 12,
  "confidenceScore": 0.91,
  "isInferred": true                   // always true — outputs are never true L3
}
```

### `summary.json` — aggregate statistics

```jsonc
{
  "symbol": "AAPL",
  "simFrom": "2026-01-01",
  "simTo": "2026-01-31",
  "model": "baseline",
  "ordersSubmitted": 47,
  "ordersFilled": 39,
  "ordersPartiallyFilled": 5,
  "ordersExpired": 3,
  "fillRate": 0.830,
  "avgTimeToFillMinutes": 4.2,
  "avgSlippageBps": 0.02,
  "implementationShortfallBps": 0.04,
  "slippageDecomposition": {              // IS split into four standard components
    "timingCostBps": 0.01,               // price drift while waiting (arrival → fill)
    "spreadCostBps": 0.01,               // half-spread paid on aggressive fills
    "opportunityCostBps": 0.02,          // missed alpha from unfilled / expired orders
    "marketImpactBps": 0.00,             // non-zero only when EnableMarketImpact is true
    "decompositionMethod": "Approximate" // "Approximate" | "Almgren-Chriss" (Phase 2)
  },
  "overallConfidenceScore": 0.86,
  "confidenceGrade": "HIGH",             // "HIGH" (≥0.7), "MEDIUM" (0.4–0.7), "LOW" (<0.4)
  "lowConfidenceDaysSkipped": 0,
  "heuristicFallbackDays": 0,
  "warnings": [],
  "isInferred": true
}
```

## 11.3 Interpreting Results

### Confidence score guide

| Score Range | Grade | Meaning | Recommended Action |
|-------------|-------|---------|-------------------|
| 0.70 – 1.00 | HIGH | Strong sequence continuity, good trade alignment | Use results with confidence |
| 0.40 – 0.69 | MEDIUM | Some gaps or alignment issues; heuristic fallback may apply | Review `warnings` field; treat slippage estimates as approximate |
| 0.00 – 0.39 | LOW | Poor data quality or significant gaps | Do not rely on fill timing; consider re-collecting data |

### Common warnings and what to do

| Warning | Cause | Action |
|---------|-------|--------|
| `"SequenceGapRate > 1% on 3 days"` | Depth data has gaps (provider dropped packets) | Re-collect affected days or use `--sim-confidence-min 0.4` to skip them |
| `"TradeAlignmentRate < 80%"` | Trade timestamps don't align with depth states | Try a different provider for the period; or increase trade alignment tolerance |
| `"HeuristicFallbackApplied on 2026-01-15"` | Model confidence too low for that day; used conservative bounds | Fill times on those days have wider uncertainty; mark in analysis |
| `"LimitPriceOutsideBookRange"` | Order limit price was never inside the observed NBBO | Order would not have been competitive; check order intent parameters |
| `"NoCalibratedParametersForSymbol"` | Calibration was not run for this symbol | Run `--calibrate-queue-model` first |

## 11.4 Configuration Reference (`appsettings.json`)

Add the following section to `config/appsettings.json` to set defaults that CLI flags can override:

```jsonc
{
  "Simulation": {
    "DefaultModel": "baseline",         // "baseline" | "heuristic"
    "DefaultLatencyMicroseconds": 500,
    "DefaultVenueProfile": "generic",   // "generic" | "nasdaq" | "nyse"
    "DefaultExportFormat": "jsonl",     // "jsonl" | "parquet" | "both"
    "MinConfidenceThreshold": 0.30,     // periods below this are skipped
    "OutputDirectory": "./sim-output",
    "InferenceModelConfig": {
      "CancelRefillPriorAlpha": 0.5,    // prior weight for cancel vs. refill split (0–1)
      "TradeAttributionPolicy": "BestBid", // "BestBid" | "Uncertain" | "Conservative"
      "QueueInitializationPolicy": "Observed", // "Observed" | "Conservative" | "Zero"
      "ConfidenceThresholds": {
        "MinSequenceContinuity": 0.995, // flag if gap rate > 0.5%
        "MinTradeAlignment": 0.85,
        "MinBooksPerMinute": 10
      },
      "FallbackPolicy": "Heuristic"     // "Heuristic" | "Skip" | "Error"
    }
  }
}
```

### InferenceModelConfig field reference

| Field | Default | Description |
|-------|---------|-------------|
| `CancelRefillPriorAlpha` | `0.5` | Prior probability that a displayed-size decrease is due to cancellation (vs. trade consumption). Set closer to 1.0 for markets with high cancel rates; closer to 0.0 for markets with low cancel rates. |
| `TradeAttributionPolicy` | `"BestBid"` | How ambiguous trades are attributed to book levels. `BestBid` is most common. Use `Conservative` when alignment rate is low. |
| `QueueInitializationPolicy` | `"Observed"` | How queue-ahead is initialised when a new order enters. `Observed` uses the current displayed size; `Conservative` assumes worst-case (full queue); `Zero` assumes no queue (aggressive). |
| `FallbackPolicy` | `"Heuristic"` | What to do when confidence is below threshold: `Heuristic` uses conservative bounds, `Skip` excludes the period from fills, `Error` fails the simulation. |

## 11.5 HTTP Endpoints (optional Phase 1.5)

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/api/sim/execution/run` | Start a simulation job asynchronously; returns job ID |
| `GET` | `/api/sim/execution/{id}` | Poll simulation status and retrieve results when complete |
| `GET` | `/api/sim/execution/{id}/summary` | Get summary report for a completed simulation |
| `POST` | `/api/sim/execution/calibrate` | Start a calibration job for a symbol/period |
| `GET` | `/api/sim/execution/calibrate/{id}` | Poll calibration status |

Request body for `POST /api/sim/execution/run`:

```jsonc
{
  "symbols": ["AAPL"],
  "from": "2026-01-01",
  "to": "2026-01-31",
  "model": "baseline",
  "orders": [ /* array of order intent objects */ ],
  "latencyMicroseconds": 500,
  "exportFormat": "jsonl"
}
```

## 11.6 UI Integration (later)

- Dedicated **Simulation** page in the WPF desktop app showing:
  - Wizard to set symbol, date range, and order intent source
  - Real-time progress during long simulations
  - Summary card with fill rate, slippage, and confidence badge
  - Queue trajectory chart per order with confidence overlay
  - Link to open results directory or export to Parquet

---

## 12) Validation & Testing Strategy

## 12.1 Unit Tests

- Event merge/ordering determinism
- Depth replay invariants
- Queue progression math
- Confidence score bounds and monotonic behavior

## 12.2 Property-Based Tests

- Non-negative queue sizes
- Conservation constraints in decomposition
- Fill quantity never exceeds available/inferred executable quantity

## 12.3 Scenario/Golden Tests

- Synthetic streams where latent queue truth is known
- Regression fixtures for high-volatility sessions

## 12.4 Performance Tests

- Throughput target for replay + simulation on daily multi-symbol datasets
- Memory bounds under long sessions

## 12.5 Acceptance Metrics

- Calibration residuals below threshold
- Stable slippage distributions across reruns
- Deterministic replay/sim outputs with same seed/config

---

## 13) Rollout Plan

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

## 14) Concrete Repository Work Breakdown

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

## 15) Risks & Mitigations

1. **Risk: Overstated realism**
   - Mitigation: strict labeling as inferred; confidence + conservative fallback.

2. **Risk: Data quality gaps reduce usefulness**
   - Mitigation: integrity-aware period filtering and quality thresholds.

3. **Risk: Model complexity/performance tradeoff**
   - Mitigation: modular baseline heuristic model first; advanced model behind feature flag.

4. **Risk: Venue behavior differences**
   - Mitigation: venue profiles with default generic assumptions.

## 16) Definition of Done (Phase 1)

### Developer-facing criteria

- Deterministic L2 timeline reconstruction from archived data.
- Queue-aware simulation produces fill tapes for limit/market orders.
- Baseline inference parameters calibratable per symbol bucket.
- Confidence score present and surfaced in outputs.
- Test coverage for correctness invariants and deterministic behavior.

### User-facing acceptance criteria

- **Dry-run works end-to-end** — `--simulate-execution --dry-run` completes within 5 seconds for a 30-day single-symbol window and prints coverage, gap rate, alignment rate, and estimated confidence to the console.
- **Calibration is self-contained** — `--calibrate-queue-model` completes without error for any symbol that has ≥ 10 days of L2 + trade data and writes calibration parameters to disk.
- **Simulation produces all result files** — `--simulate-execution` writes `fill-tape.jsonl`, `order-lifecycle.jsonl`, `summary.json`, and `queue-diagnostics.jsonl` to the output directory.
- **Summary is human-readable** — `summary.json` contains `confidenceGrade` (`HIGH`/`MEDIUM`/`LOW`), `fillRate`, `avgSlippageBps`, and `warnings` in plain-English format.
- **Every result is labeled as inferred** — `isInferred: true` is present in every fill-tape record and summary.
- **Low-data periods degrade gracefully** — periods with confidence below `MinConfidenceThreshold` are skipped (not errored) and reported in the warnings list.
- **Parquet export works** — `--sim-export-format parquet` produces files loadable by the existing QuantConnect Lean integration.
- **Existing CLI commands are unaffected** — `--backfill`, `--package`, `--symbol-status`, and all other existing commands continue to work correctly after simulation code is added.
- **End-user documentation is complete** — a user guide exists at `docs/operations/l3-simulation-guide.md` covering setup, prerequisites check, calibration, simulation, and result interpretation before Phase 1 merges.

---

## 17) Recommended First PR Sequence

1. PR1: Contracts + JSON context registration + scaffolding tests.
2. PR2: Reconstruction engine + deterministic replay tests.
3. PR3: Baseline inference model + unit/property tests.
4. PR4: Execution simulator + CLI command + golden tests.
5. PR5: Calibration + confidence scoring + docs.

This keeps risk isolated and reviewable while producing usable intermediate milestones.
