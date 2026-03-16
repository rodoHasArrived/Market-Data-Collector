# AI Agent Definitions

This directory is the navigation index for AI **agent definitions** used in the Market Data Collector
project. Agent files are kept in `.github/agents/` so that GitHub Copilot and other GitHub-integrated
tools can discover and apply them automatically.

---

## Available Agents

### Code Review Agent

**File:** [`.github/agents/code-review-agent.md`](../../../.github/agents/code-review-agent.md)
**Used by:** GitHub Copilot agents, Claude Code (`mdc-code-review` skill)

Defines the canonical **6-lens code review framework** for the MarketDataCollector codebase:

| Lens | Focus Area | Key Checks |
|------|-----------|------------|
| 1 | MVVM Architecture Compliance | Thin code-behind, BindableBase, dependency boundaries |
| 2 | Real-Time Performance | Blocking calls, hot-path allocations, channel policies, ADR-014 |
| 3 | Error Handling & Resilience | Exception hierarchy, provider resilience, shutdown paths |
| 4 | Test Code Quality | Naming, AAA structure, async patterns, isolation |
| 5 | Provider Implementation Compliance | Interface completeness, rate limiting, reconnection |
| 6 | Cross-Cutting Concerns | Dependency rules, C#/F# interop, benchmark conventions |

The Claude Code equivalent is the [`mdc-code-review`](../skills/README.md) skill.

---

### Documentation Agent

**File:** [`.github/agents/documentation-agent.md`](../../../.github/agents/documentation-agent.md)
**Used by:** GitHub Copilot agents

Handles documentation maintenance and quality tasks:

- Keeping `CLAUDE.md`, `docs/ai/`, and other AI resources current
- Documentation quality checks (broken links, stale content, formatting)
- Automated intake for the `ai-known-errors.md` registry
- Generating and updating architecture docs, changelogs, and API references

---

## Agent Discovery

GitHub Copilot discovers agent definition files from `.github/agents/`. When assigning work to
Copilot agents, referencing these files in the issue or prompt body improves output quality.

**Claude Code** uses the corresponding skill definitions in [`.claude/skills/`](../skills/README.md).

---

## Related Resources

| Resource | Purpose |
|----------|---------|
| [`docs/ai/skills/README.md`](../skills/README.md) | Claude Code skill equivalents |
| [`docs/ai/README.md`](../README.md) | Master AI resource index |
| [`docs/ai/ai-known-errors.md`](../ai-known-errors.md) | Error prevention registry |
| [`docs/ai/instructions/README.md`](../instructions/README.md) | Path-specific Copilot instructions |

---

*Last Updated: 2026-03-16*
