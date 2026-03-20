# Claude Code Skills

This directory is the navigation index for **Claude Code skill definitions** used in the Market Data
Collector / Meridian project. Skills live in `.claude/skills/` so that Claude Code can discover and load them
automatically. The current skill set is tuned for Meridian's four-pillar scope: data collection, backtesting, execution, and strategy operations.

---

## Available Skills

### mdc-blueprint

**Directory:** [`.claude/skills/mdc-blueprint/`](../../../.claude/skills/mdc-blueprint/)
**Entry point:** [`.claude/skills/mdc-blueprint/SKILL.md`](../../../.claude/skills/mdc-blueprint/SKILL.md)
**Registered in:** [`.claude/skills/skills_provider.py`](../../../.claude/skills/skills_provider.py)
**Copilot equivalent:** [`.github/agents/mdc-blueprint-agent.md`](../../../.github/agents/mdc-blueprint-agent.md)
**Claude agent:** [`.claude/agents/mdc-blueprint.md`](../../../.claude/agents/mdc-blueprint.md)

Blueprint Mode skill for the Meridian project. Translates a single prioritised idea
into a complete, code-ready technical design document — interfaces, component designs, data flows,
XAML sketches, test plans, and implementation checklists — grounded in MDC's actual stack
(C# 13, F# 8, .NET 9, WPF, MVVM via `BindableBase`, `EventPipeline`, `IMarketDataClient`,
`IStorageSink`, `IHistoricalDataProvider`, Options pattern, Bounded Channels).

**Trigger conditions** (from system prompt):

- User says "blueprint", "design document", "technical spec", "design the", "architect the"
- User asks "what interfaces do we need for" or "spike plan for"
- A Roadmap/Brainstorm output needs to be turned into something a developer can implement
- User says "blueprint mode" or provides an idea card from the Brainstorm pipeline stage

**Depth modes:**

| Mode | When to use | Sections produced |
|------|-------------|-------------------|
| `full` | Default — complete feature blueprint | Steps 1–9 |
| `spike` | Riskiest unknowns first; internal design deferred | Steps 1–3 + spike plan |
| `interface-only` | Contracts need alignment before internals | Steps 1–3 only |

**Bundled resources:**

| Resource | Purpose |
| ---------- | ------- |
| `references/blueprint-patterns.md` | Copy-ready section templates for all 9 blueprint steps |
| `references/pipeline-position.md` | Diagrams showing blueprint's position in the Brainstorm → Code Review pipeline |

---

### mdc-code-review

**Directory:** [`.claude/skills/mdc-code-review/`](../../../.claude/skills/mdc-code-review/)
**Entry point:** [`.claude/skills/mdc-code-review/SKILL.md`](../../../.claude/skills/mdc-code-review/SKILL.md)
**Registered in:** [`.claude/skills/skills_provider.py`](../../../.claude/skills/skills_provider.py)
**Copilot equivalent:** [`.github/agents/code-review-agent.md`](../../../.github/agents/code-review-agent.md)

Code review and architecture compliance skill for the Meridian codebase. Applies the
canonical 7-lens framework defined in
[`.github/agents/code-review-agent.md`](../../../.github/agents/code-review-agent.md).

**Trigger conditions** (from system prompt):

- User asks to review, audit, refactor, or improve C#/F# code
- Code references `Meridian` namespaces, `BindableBase`, `EventPipeline`,
  `IMarketDataClient`, `IStorageSink`, or `ProviderSdk` types
- Tasks involving MVVM compliance, hot-path optimization, provider implementation, or WPF architecture

**Bundled resources:**

| Resource | Purpose |
| ---------- | ------- |
| `references/architecture.md` | Deep solution layout, dependency graph, F# interop rules |
| `references/schemas.md` | JSON schemas for eval artifacts |
| `agents/grader.md` | Assertions grader for skill evaluation |
| `evals/evals.json` | 8 test cases with assertions |
| `eval-viewer/viewer.html` + `generate_review.py` | Interactive eval results viewer |
| `scripts/run_eval.py` | Run the skill evaluation suite |
| `scripts/aggregate_benchmark.py` | Aggregate grading results |
| `scripts/package_skill.py` | Package skill into `.skill` file |

**Dynamic resources** (refreshed on every read):

- `project-stats` — Live source/test file counts from the filesystem
- `git-context` — Current branch, last relevant commit, changed files

---

### mdc-brainstorm

**Directory:** [`.claude/skills/mdc-brainstorm/`](../../../.claude/skills/mdc-brainstorm/)
**Entry point:** [`.claude/skills/mdc-brainstorm/SKILL.md`](../../../.claude/skills/mdc-brainstorm/SKILL.md)
**Copilot equivalent:** [`.github/agents/mdc-brainstorm-agent.md`](../../../.github/agents/mdc-brainstorm-agent.md)

Brainstorming, ideation, and creative feature exploration skill for the Meridian project.

**Trigger conditions** (from system prompt):

- User wants to generate new ideas, features, or improvements for Meridian
- User asks "what could we add", "how could we improve", "what features should we build"
- Tasks involving architecture brainstorms, user growth strategy, or technical debt ideation
- User describes a pain point or domain problem and wants ideas for solving it

**Bundled resources:**

| Resource | Purpose |
| ---------- | ------- |
| `references/idea-dimensions.md` | Idea evaluation dimensions and scoring framework |
| `references/competitive-landscape.md` | Competitive analysis and differentiation context |

---

### mdc-provider-builder

**Directory:** [`.claude/skills/mdc-provider-builder/`](../../../.claude/skills/mdc-provider-builder/)
**Entry point:** [`.claude/skills/mdc-provider-builder/SKILL.md`](../../../.claude/skills/mdc-provider-builder/SKILL.md)
**Copilot equivalent:** [`.github/agents/mdc-provider-builder-agent.md`](../../../.github/agents/mdc-provider-builder-agent.md)

Step-by-step guided skill for building new data provider adapters for Meridian.
Covers all three provider types (`IMarketDataClient`, `IHistoricalDataProvider`,
`ISymbolSearchProvider`) with a 12-step build process, compliance checklist, and known AI
error table.

**Trigger conditions:**

- User asks to add a new data provider, exchange, or data source
- Tasks involving `IMarketDataClient`, `IHistoricalDataProvider`, `DataSourceAttribute`, or `ProviderSdk`
- Implementing rate limiting, WebSocket reconnection, or DI registration for a provider
- Code review identifies missing `[ImplementsAdr]` attribute or `WaitAsync()` error

**Bundled resources:**

| Resource | Purpose |
| ---------- | ------- |
| `references/provider-patterns.md` | 7 copy-ready patterns: historical skeleton, streaming skeleton, options, DI module, JsonContext diff, test scaffolds, appsettings template |

---

### mdc-test-writer

**Directory:** [`.claude/skills/mdc-test-writer/`](../../../.claude/skills/mdc-test-writer/)
**Entry point:** [`.claude/skills/mdc-test-writer/SKILL.md`](../../../.claude/skills/mdc-test-writer/SKILL.md)
**Copilot equivalent:** [`.github/agents/mdc-test-writer-agent.md`](../../../.github/agents/mdc-test-writer-agent.md)

Test generation skill for any Meridian component. Produces idiomatic xUnit +
FluentAssertions tests with correct async patterns, isolation, naming conventions, and mock
setup across the repo's six test projects, including backtesting and MCP server coverage.

**Trigger conditions:**

- User asks to write, add, or expand tests for any MDC component
- Code review (mdc-code-review Lens 4) identified test quality issues or gaps
- New provider, service, or storage component needs a test scaffold
- Tasks involving `async void`, missing `CancellationToken`, or `Task.Delay` in tests

**Bundled resources:**

| Resource | Purpose |
| ---------- | ------- |
| `references/test-patterns.md` | 8 named patterns (A–H) with full compilable scaffolding for providers, sinks, pipelines, WPF services, F# interop, and endpoint integration tests |

---

### ai-docs-maintain

**Registered in:** [`.claude/skills/skills_provider.py`](../../../.claude/skills/skills_provider.py)
_(code-defined only — no separate directory)_

AI documentation maintenance skill. Delegates to `build/scripts/docs/ai-docs-maintenance.py`.

**Trigger conditions:**

- User asks to check AI doc freshness, detect drift, archive stale docs, or validate cross-references
- Tasks involving "update AI docs", "check doc staleness", or "sync AI instructions"

**Available scripts:**

| Script | Purpose |
| -------- | ------- |
| `run-freshness` | Check staleness of all AI docs (60-day warning, 120-day critical) |
| `run-drift` | Detect divergence between docs and code reality |
| `run-full` | Run all checks (freshness + drift + refs + archive) |
| `run-archive` | Preview (or execute) stale doc archiving |

**Available resource:**

- `doc-health-summary` — Live health summary (stale count, drift items, broken refs)

---

## Skills Provider

The skills provider [`skills_provider.py`](../../../.claude/skills/skills_provider.py) handles:

- File-based skill discovery from `.claude/skills/mdc-code-review/`
- Code-defined skill registration (`mdc-code-review` fallback, `ai-docs-maintain`)
- Dynamic resource evaluation (live project stats, git context)
- In-process script execution (validate-skill, run-eval, aggregate-benchmark)
- File-based script subprocess execution

---

## Architecture Guard Tool

The `mdc-architecture-guard` tool is a standalone Python compliance checker at
[`build/scripts/ai-architecture-check.py`](../../../build/scripts/ai-architecture-check.py).
It is not a skill invoked via the Claude skill system — it is a command-line tool that AI agents
run before submitting a PR to catch architecture violations.

**What it checks:**

| Check ID | Rule |
| -------- | ---- |
| `CPM-001` | No `Version=` on `<PackageReference>` items (NU1008 guard) |
| `DEP-001` – `DEP-006` | Forbidden dependency directions (Ui.Services→Wpf, ProviderSdk→Infrastructure, UWP refs, FSharp→non-Contracts) |
| `ADR-001` / `ADR-005` | Missing `[ImplementsAdr]` and `[DataSource]` on provider classes |
| `CHAN-001` | Raw `Channel.CreateBounded/CreateUnbounded` calls (must use `EventPipelinePolicy`) |
| `SINK-001` | Direct `FileStream` / `File.Write*` in storage sinks (must use `AtomicFileWriter`) |
| `JSON-001` | Reflection-based `JsonSerializer` calls without source-gen context |
| `LOG-001` | String interpolation (`$"..."`) in structured log calls |

**Usage:**

```bash
# Full check (human-readable)
make ai-arch-check

# One-line summary (useful in pre-PR scripts)
make ai-arch-check-summary

# JSON output (for CI or tooling)
make ai-arch-check-json

# Targeted checks
python3 build/scripts/ai-architecture-check.py --src src/ check-cpm
python3 build/scripts/ai-architecture-check.py --src src/ check-adrs
python3 build/scripts/ai-architecture-check.py --src src/ check-channels
```

---

## Running Skill Evaluations

```bash
# Validate skill definition
python3 .claude/skills/mdc-code-review/scripts/quick_validate.py

# Run eval set (8 test cases)
python3 .claude/skills/mdc-code-review/scripts/run_eval.py \
  --eval-set .claude/skills/mdc-code-review/evals/evals.json \
  --skill-path .claude/skills/mdc-code-review

# Launch interactive eval viewer
python3 .claude/skills/mdc-code-review/eval-viewer/generate_review.py
```

---

## Related Resources

| Resource | Purpose |
| ---------- | ------- |
| [`docs/ai/agents/README.md`](../agents/README.md) | GitHub agent equivalents (Copilot) |
| [`docs/ai/README.md`](../README.md) | Master AI resource index |
| [`CLAUDE.md`](../../../CLAUDE.md) | Root project context |

---

_Last Updated: 2026-03-17_
