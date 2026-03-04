# Project Status Documentation

This folder contains project status, roadmap, and changelog documentation for the Market Data Collector.

## Documents

| Document | Description |
|----------|-------------|
| [FEATURE_INVENTORY.md](FEATURE_INVENTORY.md) | **New** — Complete inventory of every feature area with implementation status |
| [ROADMAP.md](ROADMAP.md) | Feature backlog, development priorities, sprint plans, and Phases 11–15 |
| [IMPROVEMENTS.md](IMPROVEMENTS.md) | Consolidated improvement tracking (35 items across 7+ themes) |
| [EVALUATIONS_AND_AUDITS.md](EVALUATIONS_AND_AUDITS.md) | Consolidated architecture evaluations, code audits, and assessments |
| [production-status.md](production-status.md) | Architecture assessment and production readiness |
| [CHANGELOG.md](CHANGELOG.md) | Version history and implemented improvements |
| [TODO.md](TODO.md) | Auto-generated TODO tracking from code comments |
| [health-dashboard.md](health-dashboard.md) | Auto-generated documentation health report |

## Quick Links

### Current Status
- **Version:** 1.6.2
- **Status:** Development / Pilot Ready
- **Improvements:** 33/35 completed, 1 partial (G2), 1 open (C3)
- **Last Updated:** 2026-02-26

### Key Metrics
- See [FEATURE_INVENTORY.md](FEATURE_INVENTORY.md) for a per-feature breakdown of what is complete, partial, or pending.
- See [production-status.md](production-status.md) for provider readiness, build-time requirements, and desktop UX parity caveats.
- See [ROADMAP.md](ROADMAP.md) for planned work and backlog tracking (Phases 11–15 for full implementation).
- See [IMPROVEMENTS.md](IMPROVEMENTS.md) for detailed improvement item status across all themes.
- See [EVALUATIONS_AND_AUDITS.md](EVALUATIONS_AND_AUDITS.md) for architecture evaluations, audit results, and desktop assessments.

## How Documents Relate

```
FEATURE_INVENTORY.md    — What exists and its current status (per-feature audit)
    │
ROADMAP.md              — What we're building and when (phases, sprints, objectives)
    │
    ├── IMPROVEMENTS.md — Detailed per-item tracking (35 items, themes A-J)
    │
    ├── EVALUATIONS_AND_AUDITS.md — Why and where (evaluations, audits, assessments)
    │       │
    │       ├── docs/evaluations/*.md    — Full evaluation documents (source detail)
    │       ├── docs/audits/*.md         — Full audit documents (source detail)
    │       └── docs/development/*.md    — Implementation guides
    │
    └── production-status.md — Current readiness assessment
```

## Related Documentation

- [Architecture Overview](../architecture/overview.md)
- [Getting Started Guide](../getting-started/README.md)
- [User Guide](../HELP.md)
- [Desktop Development](../development/wpf-implementation-notes.md)
- [Audits Directory](../audits/README.md)
- [Evaluations Directory](../evaluations/)

---

*For the main project documentation, see the [root README](../../README.md).*
