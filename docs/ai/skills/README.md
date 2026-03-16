# Claude Code Skills

This directory is the navigation index for **Claude Code skill definitions** used in the Market Data
Collector project. Skills live in `.claude/skills/` so that Claude Code can discover and load them
automatically.

---

## Available Skills

### mdc-code-review

**Directory:** [`.claude/skills/mdc-code-review/`](../../../.claude/skills/mdc-code-review/)
**Entry point:** [`.claude/skills/mdc-code-review/SKILL.md`](../../../.claude/skills/mdc-code-review/SKILL.md)
**Registered in:** [`.claude/skills/skills_provider.py`](../../../.claude/skills/skills_provider.py)

Code review and architecture compliance skill for the MarketDataCollector codebase. Applies the
canonical 6-lens framework defined in
[`.github/agents/code-review-agent.md`](../../../.github/agents/code-review-agent.md).

**Trigger conditions** (from system prompt):
- User asks to review, audit, refactor, or improve C#/F# code
- Code references `MarketDataCollector` namespaces, `BindableBase`, `EventPipeline`,
  `IMarketDataClient`, `IStorageSink`, or `ProviderSdk` types
- Tasks involving MVVM compliance, hot-path optimization, provider implementation, or WPF architecture

**Bundled resources:**

| Resource | Purpose |
|----------|---------|
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

### ai-docs-maintain

**Registered in:** [`.claude/skills/skills_provider.py`](../../../.claude/skills/skills_provider.py)
*(code-defined only — no separate directory)*

AI documentation maintenance skill. Delegates to `build/scripts/docs/ai-docs-maintenance.py`.

**Trigger conditions:**
- User asks to check AI doc freshness, detect drift, archive stale docs, or validate cross-references
- Tasks involving "update AI docs", "check doc staleness", or "sync AI instructions"

**Available scripts:**

| Script | Purpose |
|--------|---------|
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
|----------|---------|
| [`docs/ai/agents/README.md`](../agents/README.md) | GitHub agent equivalents (Copilot) |
| [`docs/ai/README.md`](../README.md) | Master AI resource index |
| [`CLAUDE.md`](../../../CLAUDE.md) | Root project context |

---

*Last Updated: 2026-03-16*
