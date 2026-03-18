# AI Assistant Resources

This document is the **master index** for all AI assistant guidance in the Market Data Collector
repository. It maps every AI-related resource, explains the purpose hierarchy, and provides a
recommended reading order by task type.

---

## Quick Start: Which File Do I Read?

| Task | Start Here | Deep Dive |
|------|-----------|-----------|
| **Any task** | [`CLAUDE.md`](../../CLAUDE.md) (root) | This file for the full resource map |
| **Before any change** | [`ai-known-errors.md`](ai-known-errors.md) | Prevention checklists |
| **Code review** | [`agents/README.md`](agents/README.md) | [`skills/README.md`](skills/README.md) |
| **Provider implementation** | [`claude/CLAUDE.providers.md`](claude/CLAUDE.providers.md) | [`docs/development/provider-implementation.md`](../development/provider-implementation.md) |
| **Storage changes** | [`claude/CLAUDE.storage.md`](claude/CLAUDE.storage.md) | [`docs/architecture/storage-design.md`](../architecture/storage-design.md) |
| **F# domain models** | [`claude/CLAUDE.fsharp.md`](claude/CLAUDE.fsharp.md) | [`docs/integrations/fsharp-integration.md`](../integrations/fsharp-integration.md) |
| **Testing** | [`claude/CLAUDE.testing.md`](claude/CLAUDE.testing.md) | [`instructions/README.md`](instructions/README.md) |
| **CI/CD & workflows** | [`claude/CLAUDE.actions.md`](claude/CLAUDE.actions.md) | [`.github/workflows/README.md`](../../.github/workflows/README.md) |
| **WPF / MVVM** | [`instructions/README.md`](instructions/README.md) | [`agents/README.md`](agents/README.md) (Lens 1) |
| **C# conventions** | [`instructions/README.md`](instructions/README.md) | [`CLAUDE.md`](../../CLAUDE.md) § Critical Rules |
| **Documentation edits** | [`instructions/README.md`](instructions/README.md) | [`agents/README.md`](agents/README.md) |
| **Repository audit** | [`claude/CLAUDE.repo-updater.md`](claude/CLAUDE.repo-updater.md) | `make ai-audit` |
| **Copilot setup** | [`copilot/instructions.md`](copilot/instructions.md) | [`prompts/README.md`](prompts/README.md) |
| **AI doc sync** | [`copilot/ai-sync-workflow.md`](copilot/ai-sync-workflow.md) | `.github/workflows/documentation.yml` |

---

## Directory Structure

```
docs/ai/
├── README.md                        ← you are here — master navigation index
├── ai-known-errors.md               ← mandatory pre-check: recurring AI mistake registry
│
├── claude/                          ← Claude Code specialized guides
│   ├── CLAUDE.actions.md            ← GitHub Actions and CI/CD context
│   ├── CLAUDE.fsharp.md             ← F# domain models and C#/F# interop
│   ├── CLAUDE.providers.md          ← Data provider implementation patterns
│   ├── CLAUDE.repo-updater.md       ← ai-repo-updater.py audit tool guide
│   ├── CLAUDE.storage.md            ← Storage architecture, WAL, sinks, export
│   └── CLAUDE.testing.md            ← Testing framework, patterns, organization
│
├── copilot/                         ← GitHub Copilot resources
│   ├── instructions.md              ← Extended Copilot guidance
│   └── ai-sync-workflow.md          ← AI instructions sync workflow notes
│
├── agents/                          ← Navigation index for .github/agents/
│   └── README.md                    ← Agent definitions overview
│
├── skills/                          ← Navigation index for .claude/skills/
│   └── README.md                    ← Claude Code skills overview
│
├── instructions/                    ← Navigation index for .github/instructions/
│   └── README.md                    ← Path-specific Copilot instruction rules
│
└── prompts/                         ← Navigation index for .github/prompts/
    └── README.md                    ← Prompt template catalogue
```

**Functional files** that must stay in their tool-required locations:

```
.github/
├── agents/                          ← GitHub Copilot agent definitions (auto-discovered)
│   ├── adr-generator.agent.md
│   ├── code-review-agent.md
│   ├── documentation-agent.md
│   ├── mdc-blueprint-agent.md
│   ├── mdc-brainstorm-agent.md
│   ├── mdc-provider-builder-agent.md
│   └── mdc-test-writer-agent.md
├── copilot-instructions.md          ← Repository-wide Copilot instructions
├── instructions/                    ← Path-specific Copilot rules (auto-applied)
│   ├── csharp.instructions.md
│   ├── wpf.instructions.md
│   ├── dotnet-tests.instructions.md
│   └── docs.instructions.md
└── prompts/                         ← Copilot Chat prompt templates (auto-discovered)
    └── *.prompt.yml

.claude/
├── agents/                          ← Claude Code agent definitions (auto-discovered)
│   ├── mdc-blueprint.md
│   ├── mdc-cleanup.md
│   └── mdc-docs.md
├── settings.local.json              ← Claude Code permissions
└── skills/                          ← Claude Code skill definitions (auto-discovered)
    ├── _shared/
    │   └── project-context.md
    ├── mdc-blueprint/
    │   ├── SKILL.md
    │   └── references/
    ├── mdc-brainstorm/
    │   ├── SKILL.md
    │   └── references/
    ├── mdc-code-review/
    │   ├── SKILL.md
    │   ├── references/
    │   ├── evals/
    │   ├── eval-viewer/
    │   ├── scripts/
    │   └── agents/
    ├── mdc-provider-builder/
    │   ├── SKILL.md
    │   └── references/
    ├── mdc-test-writer/
    │   ├── SKILL.md
    │   └── references/
    └── skills_provider.py

CLAUDE.md                            ← Root AI context (always read first)
build/scripts/ai-repo-updater.py     ← Repository audit tool
build/scripts/docs/ai-docs-maintenance.py  ← AI doc health checker
build/scripts/docs/update-claude-md.py     ← CLAUDE.md updater
```

---

## Resource Hierarchy

The AI guidance system has six tiers, from broadest to most specialized:

### Tier 1: Root Context (read always)

| File | Purpose | Used By |
|------|---------|---------|
| [`CLAUDE.md`](../../CLAUDE.md) | Master project context — architecture, commands, providers, conventions, API reference | Claude, Copilot, all agents |
| [`ai-known-errors.md`](ai-known-errors.md) | Canonical registry of recurring AI mistakes with prevention checklists | All agents (mandatory pre-check) |

### Tier 2: Specialized Guides (read for specific domains)

Located in `docs/ai/claude/`:

| File | Domain | Canonical For |
|------|--------|---------------|
| [`CLAUDE.providers.md`](claude/CLAUDE.providers.md) | Data providers | Provider implementation patterns, interfaces, file locations |
| [`CLAUDE.storage.md`](claude/CLAUDE.storage.md) | Storage system | WAL, sinks, archival, export, packaging, maintenance |
| [`CLAUDE.fsharp.md`](claude/CLAUDE.fsharp.md) | F# domain | Discriminated unions, validation, C#/F# interop |
| [`CLAUDE.testing.md`](claude/CLAUDE.testing.md) | Testing | Test framework, organization, patterns, coverage |
| [`CLAUDE.actions.md`](claude/CLAUDE.actions.md) | CI/CD | GitHub Actions workflows, reusable builds, troubleshooting |
| [`CLAUDE.repo-updater.md`](claude/CLAUDE.repo-updater.md) | Audit tooling | `ai-repo-updater.py` workflow and commands |

### Tier 3: Agent & Skill Definitions (read for agent tasks)

See [`agents/README.md`](agents/README.md) and [`skills/README.md`](skills/README.md) for full details.

| File | Used By | Purpose |
|------|---------|---------|
| [`.github/agents/adr-generator.agent.md`](../../.github/agents/adr-generator.agent.md) | Copilot | ADR creation agent with structured formatting |
| [`.github/agents/code-review-agent.md`](../../.github/agents/code-review-agent.md) | Copilot, Claude skill | **Canonical** 7-lens code review framework |
| [`.github/agents/documentation-agent.md`](../../.github/agents/documentation-agent.md) | Copilot | Documentation maintenance agent |
| [`.github/agents/mdc-blueprint-agent.md`](../../.github/agents/mdc-blueprint-agent.md) | Copilot, Claude agent | Technical design (blueprint) agent |
| [`.github/agents/mdc-brainstorm-agent.md`](../../.github/agents/mdc-brainstorm-agent.md) | Copilot | Brainstorming & ideation agent |
| [`.github/agents/mdc-provider-builder-agent.md`](../../.github/agents/mdc-provider-builder-agent.md) | Copilot | Provider implementation agent |
| [`.github/agents/mdc-test-writer-agent.md`](../../.github/agents/mdc-test-writer-agent.md) | Copilot | Test generation agent |
| [`.claude/agents/mdc-blueprint.md`](../../.claude/agents/mdc-blueprint.md) | Claude Code | Blueprint mode agent (wraps `mdc-blueprint-agent.md`) |
| [`.claude/agents/mdc-cleanup.md`](../../.claude/agents/mdc-cleanup.md) | Claude Code | Code cleanup and anti-pattern correction agent |
| [`.claude/agents/mdc-docs.md`](../../.claude/agents/mdc-docs.md) | Claude Code | Documentation maintenance agent |
| [`.claude/skills/mdc-blueprint/SKILL.md`](../../.claude/skills/mdc-blueprint/SKILL.md) | Claude Code | Blueprint mode skill |
| [`.claude/skills/mdc-brainstorm/SKILL.md`](../../.claude/skills/mdc-brainstorm/SKILL.md) | Claude Code | Brainstorming & ideation skill |
| [`.claude/skills/mdc-code-review/SKILL.md`](../../.claude/skills/mdc-code-review/SKILL.md) | Claude Code | Code review skill (wraps `code-review-agent.md`) |
| [`.claude/skills/mdc-code-review/references/architecture.md`](../../.claude/skills/mdc-code-review/references/architecture.md) | Claude Code | Deep project context, dependency graph |
| [`.claude/skills/mdc-provider-builder/SKILL.md`](../../.claude/skills/mdc-provider-builder/SKILL.md) | Claude Code | Provider implementation skill |
| [`.claude/skills/mdc-test-writer/SKILL.md`](../../.claude/skills/mdc-test-writer/SKILL.md) | Claude Code | Test generation skill |
| [`.claude/skills/skills_provider.py`](../../.claude/skills/skills_provider.py) | Claude Code | Skill registration and dynamic resources |

### Tier 4: Path-Specific Instructions (auto-applied by Copilot)

See [`instructions/README.md`](instructions/README.md) for full rule details.

| File | Applies To | Rules |
|------|-----------|-------|
| [`.github/instructions/csharp.instructions.md`](../../.github/instructions/csharp.instructions.md) | `src/**/*.cs` | 10 C# conventions |
| [`.github/instructions/wpf.instructions.md`](../../.github/instructions/wpf.instructions.md) | `src/MarketDataCollector.Wpf/**` | 10 WPF/MVVM conventions |
| [`.github/instructions/dotnet-tests.instructions.md`](../../.github/instructions/dotnet-tests.instructions.md) | `tests/**/*.cs` | 6 test conventions |
| [`.github/instructions/docs.instructions.md`](../../.github/instructions/docs.instructions.md) | `**/*.md` | 5 documentation conventions |

### Tier 5: Repository-Wide Instructions & Prompts

| File | Purpose |
|------|---------|
| [`.github/copilot-instructions.md`](../../.github/copilot-instructions.md) | Copilot coding-agent instructions (execution flow, quality bar, build commands) |
| [`copilot/instructions.md`](copilot/instructions.md) | Extended Copilot guide — project structure, conventions, architecture patterns |
| [`.github/workflows/copilot-setup-steps.yml`](../../.github/workflows/copilot-setup-steps.yml) | Environment bootstrap for Copilot agents |

### Tier 6: Prompt Templates

See [`prompts/README.md`](prompts/README.md) for full catalogue.

Located in [`.github/prompts/`](../../.github/prompts/) — 16 YAML templates for Copilot Chat:

| Category | Prompts |
|----------|---------|
| **Development** | `add-data-provider`, `add-export-format`, `provider-implementation-guide`, `write-unit-tests` |
| **Understanding** | `explain-architecture`, `project-context`, `troubleshoot-issue` |
| **Fixing** | `fix-build-errors`, `fix-test-failures`, `fix-code-quality` |
| **DevOps** | `configure-deployment`, `optimize-performance` |
| **Desktop** | `wpf-debug-improve`, `code-review` |
| **CI-Generated** | `workflow-results-code-quality`, `workflow-results-test-matrix` |

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

See [`claude/CLAUDE.repo-updater.md`](claude/CLAUDE.repo-updater.md) for the full workflow guide.

---

## AI Documentation Maintenance Automation

The `ai-docs-maintenance.py` script keeps AI documentation current as the codebase evolves.
It detects stale content, validates cross-references, identifies drift between docs and code,
and archives deprecated content.

**Script:** `build/scripts/docs/ai-docs-maintenance.py`

| Command | Purpose | Output |
|---------|---------|--------|
| `freshness` | Check staleness (60-day warning, 120-day critical) | JSON |
| `drift` | Detect where docs diverge from code | JSON |
| `validate-refs` | Check for broken internal links | JSON |
| `archive-stale` | Find deprecated docs to archive | JSON |
| `sync-report` | Generate a full markdown sync report | Markdown |
| `full` | Run all checks combined | JSON |

```bash
make ai-docs-freshness       # Check AI doc freshness
make ai-docs-drift           # Detect doc/code drift
make ai-docs-sync-report     # Generate sync report
make ai-docs-archive         # Preview archive candidates
make ai-docs-archive-execute # Actually archive stale docs
```

The **AI Documentation Health Check** job in `documentation.yml` runs automatically on every push
to `main` touching `docs/`, `src/`, or `.github/` files, plus weekly (Monday 03:00 UTC).

The `ai-docs-maintain` skill (registered in `.claude/skills/skills_provider.py`) exposes these
checks as Claude Code scripts. See [`skills/README.md`](skills/README.md) for details.

---

## Maintenance Notes

### Canonical Sources (edit these first)

| Topic | Canonical Source | References It |
|-------|-----------------|--------------|
| Code review lenses | `.github/agents/code-review-agent.md` | `.claude/skills/mdc-code-review/SKILL.md` |
| Project overview & conventions | `CLAUDE.md` | `docs/ai/copilot/instructions.md`, `.github/copilot-instructions.md` |
| Provider implementation | `docs/ai/claude/CLAUDE.providers.md` | `.github/prompts/add-data-provider.prompt.yml` |
| Error prevention | `docs/ai/ai-known-errors.md` | All agent/instruction files |
| Build/test commands | `CLAUDE.md` § Quick Commands | `.github/copilot-instructions.md`, `docs/ai/copilot/instructions.md` |

### Adding a New AI Resource

1. Determine the correct tier (root context, specialized guide, agent/skill, path-specific, or prompt)
2. Create the file in the appropriate directory (functional files go to `.github/` or `.claude/`)
3. Add a navigation entry to the relevant `docs/ai/*/README.md`
4. Add an entry to this master README
5. Cross-reference from related files
6. Add a "Last Updated" footer

---

*Last Updated: 2026-03-17*
