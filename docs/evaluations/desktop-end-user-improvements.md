# Desktop End-User Improvement Opportunities

## Scope
This assessment focuses on high-value improvements for the Windows desktop experience (WPF app), prioritizing user trust, task completion speed, and operational reliability.

## Current UX Gaps Observed

1. **Simulated data in core workflows**
   - Dashboard metrics and activity feed are seeded with hard-coded sample entries and random values.
   - Symbol management starts from demo symbols rather than persisted user subscriptions.
   - Backfill view shows sample status and simulated progress instead of real job execution state.

2. **State and workflow continuity is weak**
   - Filters/search selections are ephemeral and reset on reload/navigation in several pages.
   - No obvious autosave/draft-recovery behavior for in-progress user operations.

3. **Limited decision support for recovery actions**
   - Users get notification banners for actions (validate, repair, pause/cancel), but limited root-cause context and guided remediation sequencing.

4. **Control-plane discoverability needs refinement**
   - The product surface is broad (many pages/features), which is powerful but can increase cognitive load for first-time users.

## High-Value Improvements (Prioritized)

### P0 — Connect all primary screens to live backend state
**Why users care:** Trust collapses if dashboards and operation pages look "demo-like" during live collection.

- Replace seeded/random dashboard values with streaming or polling data from real endpoints.
- Load symbols from persisted configuration/API, including save/update/delete round-trips.
- Convert backfill start/pause/cancel/status into true server-side jobs with resumable IDs and durable progress.
- Add explicit stale-data indicators when backend is unreachable.

**Expected user impact:** immediate increase in confidence; fewer false assumptions while making trading/research decisions.

### P0 — Job reliability UX: resumability + failure transparency
**Why users care:** Long backfills and collection sessions fail in real life; users need safe recovery.

- Add resumable backfill sessions with checkpoints and "resume from last successful bar" options.
- Show per-symbol failure reasons, retry counts, and next retry time.
- Provide one-click "retry failed only" and "export failed symbols" actions.
- Preserve job history with durations, provider used, and output count.

**Expected user impact:** less manual triage, faster recovery after provider/network disruptions.

### P1 — First-run onboarding and role-based presets
**Why users care:** Time-to-first-value determines adoption.

- Extend setup wizard with task-based presets (e.g., "US equities intraday", "options chain snapshots", "research backfill").
- Add guided provider diagnostics before users reach operational pages.
- Generate an initial watchlist/symbol template based on selected goal.

**Expected user impact:** users can run a valid collection in minutes instead of manually configuring many controls.

### P1 — Persistent workspace state and keyboard-first productivity
**Why users care:** Frequent users revisit the same filters/views repeatedly.

- Persist filters, sort order, selected symbols, and pane layout per page.
- Introduce command palette (Ctrl+K) for page/action navigation.
- Expand keyboard coverage for bulk actions and data triage workflows.

**Expected user impact:** lower repetitive effort and faster operator throughput.

### P1 — Alerting model that distinguishes noise vs action
**Why users care:** Too many warnings without prioritization reduces response quality.

- Group alerts by severity + business impact (data loss risk, delayed freshness, provider outage).
- Add alert suppression windows and deduplication for recurring transient failures.
- Offer playbooks inside the UI: "What happened", "What to do now", "What happens if ignored".

**Expected user impact:** reduced alert fatigue and better incident response.

### P2 — Data quality explainability and repair confidence
**Why users care:** End users need to trust repaired datasets.

- Add pre/post repair diff summaries (gaps fixed, bars replaced, source provenance).
- Show quality scores by symbol/timeframe with drill-down to raw anomalies.
- Support side-by-side provider comparison before accepting repair output.

**Expected user impact:** greater confidence in downstream backtests and analytics.

### P2 — Packaging/export workflow hardening
**Why users care:** Researchers regularly move data to notebooks/backtest systems.

- Save reusable export presets with validation previews (schema + sample rows).
- Add destination checks (disk space, write permissions, path health) before execution.
- Produce machine-readable export manifest and post-export verification report.

**Expected user impact:** fewer failed exports and easier reproducibility.

## Suggested 90-Day Delivery Plan

1. **Month 1 (Trust foundation):** P0 live-data wiring for Dashboard, Symbols, Backfill.
2. **Month 2 (Reliability):** resumable jobs, failure transparency, job history.
3. **Month 3 (Adoption/Productivity):** onboarding presets + persistent workspace state + command palette.

## Success Metrics (End-User Focus)

- Time to complete first successful collection session.
- Backfill recovery time after interruption.
- Percentage of alerts resolved without leaving the app.
- Daily active power users and median actions per session.
- User-reported confidence in data correctness.
