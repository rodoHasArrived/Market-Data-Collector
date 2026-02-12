# Ingestion Orchestration Evaluation

## Market Data Collector — Scheduler & Backfill Control Assessment

**Date:** 2026-02-12  
**Status:** Evaluation Complete  
**Author:** Architecture Review

---

## Executive Summary

This document evaluates ingestion orchestration capabilities for the Market Data Collector, with emphasis on scheduling, backfill execution, resumability, idempotency, and operational controls.

**Key Finding:** The current architecture has strong building blocks (provider abstraction, background services, and storage durability patterns), but orchestration maturity is uneven. The highest-value next step is a unified job model that treats realtime collection and historical backfills as first-class managed workloads.

---

## A. Evaluation Scope

The assessment focused on:

1. Job lifecycle management (create/start/pause/resume/cancel).
2. Scheduler policy quality (interval, market-session-aware, and event-triggered).
3. Failure handling and retry behavior.
4. Backfill checkpointing and partial progress recovery.
5. Multi-provider coordination and deduplication safeguards.
6. Operator ergonomics and auditability.

---

## B. Current-State Assessment

### Strengths

| Area | Observation | Impact |
|------|-------------|--------|
| Modularity | Providers and storage sinks are cleanly abstracted | Enables scheduler-independent execution strategy |
| Durability primitives | WAL + tiered storage reduce data-loss risk | Supports long-running ingestion jobs |
| Monitoring baseline | Existing health and quality monitoring can be reused for orchestration signals | Reduces implementation cost for job telemetry |
| Desktop workflow hooks | UI already exposes backfill and status concepts | Good foundation for operator control-plane |

### Gaps

| Gap | Risk | Priority |
|-----|------|----------|
| No unified job contract across realtime/backfill flows | Inconsistent behaviors and user expectations | P0 |
| Limited checkpoint semantics exposed to users | Manual reruns after partial failures | P0 |
| Retry policy lacks workload-level intent (e.g., stale data tolerance) | Over-retry, provider throttling, wasted compute | P1 |
| Missing explicit idempotency strategy for repeated backfills | Duplicate records or unnecessary rewrites | P1 |
| Weak operator timeline/audit view | Harder post-incident analysis | P1 |

---

## C. Target Capability Model

### Capability 1: Unified Ingestion Job State Machine

Adopt a shared model for all collection work:

`Draft → Queued → Running → Paused → Completed | Failed | Cancelled`

Required metadata:
- JobId, workload type (`realtime`, `historical`), symbols, provider, timeframe.
- Checkpoint token (symbol/date cursor, last durable offset).
- Retry envelope (attempt count, next retry time, policy class).
- SLA expectations (freshness target, completion deadline).

### Capability 2: Policy-Driven Scheduler

Support multiple policy classes:
- **Cron/time-based** (e.g., nightly refresh).
- **Session-aware** (market open/close windows per venue).
- **Signal-triggered** (repair triggered by quality anomalies).

### Capability 3: Deterministic Resumability

Define resume behavior by workload type:
- Realtime: restart from latest stream + gap-fill window.
- Backfill: resume from last committed bar cursor.

Resume must be explicit, observable, and safe by default.

### Capability 4: Idempotent Writes by Design

Implement consistent dedupe keys and merge semantics:
- Key shape: `(provider, symbol, timestamp, event_type, sequence)`.
- Storage sinks should upsert or drop duplicates predictably.
- Backfill reruns should be marked as reconciliation runs in metadata.

---

## D. 90-Day Implementation Roadmap

### Month 1 (P0) — Job Foundation

- Introduce `IngestionJob` model and persisted state transitions.
- Normalize start/pause/resume/cancel APIs across workloads.
- Persist checkpoints at symbol and batch boundaries.

### Month 2 (P1) — Scheduler Policies + Retry Classes

- Add scheduler policy engine (cron + session-aware).
- Introduce retry classes (`fast`, `conservative`, `provider-safe`).
- Implement jittered retries and provider throttling guards.

### Month 3 (P1/P2) — UX + Operability

- Add job timeline and event trail in desktop UI.
- Add "retry failed symbols only" and "resume from checkpoint" actions.
- Emit orchestration KPIs to dashboards and runbooks.

---

## E. Success Metrics

Track the following KPIs:

- **Backfill completion reliability:** % jobs completed without full restart.
- **Mean recovery time:** interruption-to-resume duration.
- **Duplicate write rate:** duplicates detected per million events.
- **Operator efficiency:** mean clicks/time to recover failed job.
- **Provider safety:** throttling incidents per week.

---

## Recommendation

Prioritize a unified ingestion job contract and checkpoint strategy before adding more provider-specific orchestration features. This sequencing delivers immediate reliability gains and reduces operational complexity across both desktop and headless workflows.
