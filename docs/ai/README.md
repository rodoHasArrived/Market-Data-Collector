# AI Assistant Resources

This document is the **master index** for all AI assistant guidance in the Market Data Collector repository. It maps every AI-related resource, explains the purpose hierarchy, and provides a recommended reading order by task type.

## Quick Start: Which File Do I Read?

| Task | Start Here | Deep Dive |
|------|-----------|-----------|
| **Any task** | [`CLAUDE.md`](../../CLAUDE.md) (root) | This file for resource map |
| **Before any change** | [`ai-known-errors.md`](ai-known-errors.md) | Prevention checklists |
| **Code review** | [`.github/agents/code-review-agent.md`](../../.github/agents/code-review-agent.md) | [`.claude/skills/mdc-code-review/SKILL.md`](../../.claude/skills/mdc-code-review/SKILL.md) |
| **Provider implementation** | [`CLAUDE.providers.md`](claude/CLAUDE.providers.md) | [`docs/development/provider-implementation.md`](../development/provider-implementation.md) |
| **Storage changes** | [`CLAUDE.storage.md`](claude/CLAUDE.storage.md) | [`docs/architecture/storage-design.md`](../architecture/storage-design.md) |
| **F# domain models** | [`CLAUDE.fsharp.md`](claude/CLAUDE.fsharp.md) | [`docs/integrations/fsharp-integration.md`](../integrations/fsharp-integration.md) |
| **Testing** | [`CLAUDE.testing.md`](claude/CLAUDE.testing.md) | [`.github/instructions/dotnet-tests.instructions.md`](../../.github/instructions/dotnet-tests.instructions.md) |
| **CI/CD & workflows** | [`CLAUDE.actions.md`](claude/CLAUDE.actions.md) | [`.github/workflows/README.md`](../../.github/workflows/README.md) |
| **WPF / MVVM** | [`.github/instructions/wpf.instructions.md`](../../.github/instructions/wpf.instructions.md) | [`.github/agents/code-review-agent.md`](../../.github/agents/code-review-agent.md) (Lens 1) |
| **C# conventions** | [`.github/instructions/csharp.instructions.md`](../../.github/instructions/csharp.instructions.md) | [`CLAUDE.md`](../../CLAUDE.md) § Critical Rules |
| **Documentation edits** | [`.github/instructions/docs.instructions.md`](../../.github/instructions/docs.instructions.md) | [`.github/agents/documentation-agent.md`](../../.github/agents/documentation-agent.md) |
| **Repository audit** | [`CLAUDE.repo-updater.md`](claude/CLAUDE.repo-updater.md) | `make ai-audit` |

---

## Resource Hierarchy

The AI guidance system has four tiers, from broadest to most specialized:

### Tier 1: Root Context (read always)

| File | Purpose | Used By |
|------|---------|---------|
| [`CLAUDE.md`](../../CLAUDE.md) | Master project context — architecture, commands, providers, conventions, API reference | Claude, Copilot, all agents |
| [`ai-known-errors.md`](ai-known-errors.md) | Canonical registry of recurring AI mistakes with prevention checklists (18 entries) | All agents (mandatory pre-check) |

### Tier 2: Specialized Guides (read for specific domains)

Located in `docs/ai/claude/`:

| File | Domain | Lines | Canonical For |
|------|--------|-------|---------------|
| [`CLAUDE.providers.md`](claude/CLAUDE.providers.md) | Data providers | ~790 | Provider implementation patterns, interfaces, file locations |
| [`CLAUDE.storage.md`](claude/CLAUDE.storage.md) | Storage system | ~750 | WAL, sinks, archival, export, packaging, maintenance |
| [`CLAUDE.fsharp.md`](claude/CLAUDE.fsharp.md) | F# domain | ~860 | Discriminated unions, validation, C#/F# interop |
| [`CLAUDE.testing.md`](claude/CLAUDE.testing.md) | Testing | ~825 | Test framework, organization, patterns, coverage |
| [`CLAUDE.actions.md`](claude/CLAUDE.actions.md) | CI/CD | ~112 | GitHub Actions workflows, reusable builds, troubleshooting |
| [`CLAUDE.repo-updater.md`](claude/CLAUDE.repo-updater.md) | Audit tooling | ~195 | `ai-repo-updater.py` workflow and commands |

### Tier 3: Agent & Skill Definitions (read for agent tasks)

| File | Used By | Purpose |
|------|---------|---------|
| [`.github/agents/code-review-agent.md`](../../.github/agents/code-review-agent.md) | GitHub Copilot agents, Claude skill | **Canonical** 6-lens code review framework (MVVM, performance, errors, tests, providers, cross-cutting) |
| [`.github/agents/documentation-agent.md`](../../.github/agents/documentation-agent.md) | GitHub Copilot agents | Documentation maintenance and quality agent |
| [`.claude/skills/mdc-code-review/SKILL.md`](../../.claude/skills/mdc-code-review/SKILL.md) | Claude Code | Code review skill with evals and grading (references `code-review-agent.md` for review lenses) |
| [`.claude/skills/mdc-code-review/references/architecture.md`](../../.claude/skills/mdc-code-review/references/architecture.md) | Claude Code | Deep project context: solution layout, dependency graph, abstractions |
| [`.claude/skills/mdc-code-review/references/schemas.md`](../../.claude/skills/mdc-code-review/references/schemas.md) | Claude Code | JSON schemas for eval artifacts |
| [`.claude/skills/mdc-code-review/agents/grader.md`](../../.claude/skills/mdc-code-review/agents/grader.md) | Claude Code | Assertions grader for skill evaluation |

### Tier 4: Path-Specific Instructions (applied automatically by Copilot)

These files are auto-loaded by GitHub Copilot when editing files matching their `applyTo` pattern:

| File | Applies To | Rules |
|------|-----------|-------|
| [`.github/instructions/csharp.instructions.md`](../../.github/instructions/csharp.instructions.md) | `src/**/*.cs` | 10 rules: sealed classes, CancellationToken, structured logging, ADR-014, etc. |
| [`.github/instructions/wpf.instructions.md`](../../.github/instructions/wpf.instructions.md) | `src/MarketDataCollector.Wpf/**` | 10 rules: thin code-behind, BindableBase, dependency rules, no WinRT |
| [`.github/instructions/dotnet-tests.instructions.md`](../../.github/instructions/dotnet-tests.instructions.md) | `tests/**/*.cs` | 6 rules: determinism, AAA, naming, test utilities |
| [`.github/instructions/docs.instructions.md`](../../.github/instructions/docs.instructions.md) | `**/*.md` | 5 rules: task-oriented language, scannable headings, no duplication |

---

## Copilot-Specific Resources

| File | Purpose |
|------|---------|
| [`.github/copilot-instructions.md`](../../.github/copilot-instructions.md) | Repository-wide Copilot coding-agent instructions (execution flow, quality bar, build commands) |
| [`copilot/instructions.md`](copilot/instructions.md) | Extended Copilot guide — project structure, conventions, architecture patterns |
| [`.github/workflows/copilot-setup-steps.yml`](../../.github/workflows/copilot-setup-steps.yml) | Environment bootstrap workflow for Copilot agents |
| [`.github/prompts/`](../../.github/prompts/) | 16 reusable prompt templates for Copilot Chat (see [prompts README](../../.github/prompts/README.md)) |

---

## Prompt Templates

Located in `.github/prompts/`, these YAML templates provide structured prompts for common tasks:

| Category | Prompts |
|----------|---------|
| **Development** | `add-data-provider`, `add-export-format`, `provider-implementation-guide`, `write-unit-tests` |
| **Understanding** | `explain-architecture`, `project-context`, `troubleshoot-issue` |
| **Fixing** | `fix-build-errors`, `fix-test-failures`, `fix-code-quality` |
| **DevOps** | `configure-deployment`, `optimize-performance` |
| **Desktop** | `wpf-debug-improve`, `code-review` |
| **CI-Generated** | `workflow-results-code-quality`, `workflow-results-test-matrix` |

See [`.github/prompts/README.md`](../../.github/prompts/README.md) for usage instructions.

---

## AI Error Memory Workflow

The repository has an automated error memory system to prevent recurring AI mistakes:

1. **Registry**: [`ai-known-errors.md`](ai-known-errors.md) — canonical log of AI-caused errors with symptoms, root causes, and prevention checklists
2. **Automated intake**: GitHub Issues labeled `ai-known-error` trigger the `AI Known Errors Intake` job in [`.github/workflows/documentation.yml`](../../.github/workflows/documentation.yml), which opens a PR to record the issue
3. **Issue format**: Include headings `Area`, `Symptoms`, `Root cause`, `Prevention checklist`, and `Verification commands` for best automation quality

**Required workflow for all AI agents:**
1. Before making changes: scan `ai-known-errors.md` and apply listed prevention checks
2. After fixing an AI-caused bug: add a new entry to the registry
3. Before opening a PR: confirm the change does not repeat any known pattern

---

## AI Audit Tooling

The `ai-repo-updater.py` script provides structured, machine-readable repository health analysis:

```bash
make ai-audit            # Full audit (code, docs, tests, config, providers, AI docs)
make ai-audit-code       # C#/F# convention violations only
make ai-audit-docs       # Documentation quality only
make ai-audit-tests      # Test coverage gaps only
make ai-audit-ai-docs    # AI documentation freshness and drift
make ai-verify           # Build + test + lint validation
make ai-report           # Generate improvement report
```

See [`CLAUDE.repo-updater.md`](claude/CLAUDE.repo-updater.md) for the full workflow guide.

---

## AI Documentation Maintenance Automation

The `ai-docs-maintenance.py` script keeps AI documentation current as the codebase evolves. It detects stale content, validates cross-references, identifies drift between docs and code, and archives deprecated content.

**Script:** `build/scripts/docs/ai-docs-maintenance.py`

### Commands

| Command | Purpose | Output |
|---------|---------|--------|
| `freshness` | Check staleness of all AI docs (60-day warning, 120-day critical) | JSON |
| `drift` | Detect where docs diverge from code (provider counts, workflow counts, file counts) | JSON |
| `validate-refs` | Check for broken internal links in AI docs | JSON |
| `archive-stale` | Find deprecated docs to archive (use `--execute` to move files) | JSON |
| `sync-report` | Generate a full markdown sync report | Markdown |
| `full` | Run all checks combined | JSON |

### Makefile Targets

```bash
make ai-docs-freshness       # Check AI doc freshness
make ai-docs-drift           # Detect doc/code drift
make ai-docs-sync-report     # Generate sync report (docs/generated/ai-docs-sync-report.md)
make ai-docs-archive         # Preview archive candidates
make ai-docs-archive-execute # Actually archive stale docs (moves files)
```

### CI Integration

The **AI Documentation Health Check** job in [`.github/workflows/documentation.yml`](../../.github/workflows/documentation.yml) runs automatically on:
- Every push to `main` that touches `docs/`, `src/`, or `.github/` files
- Weekly schedule (Monday 03:00 UTC)
- Manual workflow dispatch

It produces a sync report artifact and publishes a health summary to the GitHub Actions step summary.

### Claude Skill

The `ai-docs-maintain` skill (registered in `.claude/skills/skills_provider.py`) exposes freshness, drift, and full checks as code-defined scripts for Claude agents.

---

## Maintenance Notes

### Canonical Sources (edit these first)

When updating AI guidance, edit the **canonical source** and add cross-references elsewhere:

| Topic | Canonical Source | References It |
|-------|-----------------|--------------|
| Code review lenses | `.github/agents/code-review-agent.md` | `.claude/skills/mdc-code-review/SKILL.md` |
| Project overview & conventions | `CLAUDE.md` | `docs/ai/copilot/instructions.md`, `.github/copilot-instructions.md` |
| Provider implementation | `docs/ai/claude/CLAUDE.providers.md` | `.github/prompts/add-data-provider.prompt.yml` |
| Error prevention | `docs/ai/ai-known-errors.md` | All agent/instruction files |
| Build/test commands | `CLAUDE.md` § Quick Commands | `.github/copilot-instructions.md`, `docs/ai/copilot/instructions.md` |

### Adding a New AI Resource

1. Determine the correct tier (root context, specialized guide, agent/skill, or path-specific)
2. Create the file in the appropriate directory
3. Add an entry to this README
4. Cross-reference from related files
5. Add a "Last Updated" footer

---

*Last Updated: 2026-03-16*
