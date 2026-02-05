# Market Data Collector Documentation

**Version:** 1.6.1 | **Last Updated:** 2026-02-04 | **Status:** Production Ready

Welcome to the Market Data Collector documentation. This guide helps you find the information you need.

## What This Project Does

Market Data Collector is a complete solution for **building your own market data archive**. It connects to financial data providers, captures market data in real-time, and stores everything locally so you have full ownership and offline access.

**Core benefits:**
- **Data independence** — Switch providers without losing your archive or rewriting code
- **Cost control** — Use free-tier APIs strategically, pay only for premium data you need
- **Reliability** — Automatic reconnection, failover between providers, and data integrity checks
- **Flexibility** — Collect exactly the symbols and data types you need, store them how you want

---

## Quick Links

| I want to... | Go to... |
|--------------|----------|
| Get started quickly | [Getting Started](getting-started/README.md) |
| Read the full user guide | [HELP.md](HELP.md) |
| Understand the architecture | [Architecture Overview](architecture/overview.md) |
| Check production readiness | [Production Status](status/production-status.md) |
| Set up a data provider | [Provider Documentation](providers/) |

---

## Documentation Structure

### [HELP.md](HELP.md) - Complete User Guide

The authoritative guide covering all features, configuration, providers, troubleshooting, and FAQ.

---

### [getting-started/](getting-started/) - Quick Start

A brief quick-start index pointing to key sections in the user guide.

---

### [architecture/](architecture/) - System Architecture

Technical documentation about system design.

| Document | Description |
|----------|-------------|
| [overview.md](architecture/overview.md) | High-level architecture overview |
| [c4-diagrams.md](architecture/c4-diagrams.md) | C4 model visualizations |
| [domains.md](architecture/domains.md) | Domain model and event contracts |
| [provider-management.md](architecture/provider-management.md) | Provider abstraction layer design |
| [storage-design.md](architecture/storage-design.md) | Storage organization and policies |
| [crystallized-storage-format.md](architecture/crystallized-storage-format.md) | Crystallized storage format specification |
| [consolidation.md](architecture/consolidation.md) | UI layer consolidation guide |
| [why-this-architecture.md](architecture/why-this-architecture.md) | Design decisions and rationale |

---

### [providers/](providers/) - Data Provider Documentation

Documentation for market data providers.

| Document | Description |
|----------|-------------|
| [data-sources.md](providers/data-sources.md) | Available data sources with status |
| [backfill-guide.md](providers/backfill-guide.md) | Historical data backfill guide |
| [provider-comparison.md](providers/provider-comparison.md) | Provider feature comparison |
| [alpaca-setup.md](providers/alpaca-setup.md) | Alpaca Markets setup guide |
| [interactive-brokers-setup.md](providers/interactive-brokers-setup.md) | IB TWS/Gateway configuration |
| [interactive-brokers-free-equity-reference.md](providers/interactive-brokers-free-equity-reference.md) | IB API technical reference |

---

### [development/](development/) - Development Guides

Guides for developers contributing to or extending the project.

| Document | Description |
|----------|-------------|
| [central-package-management.md](development/central-package-management.md) | Central Package Management (CPM) guide |
| [provider-implementation.md](development/provider-implementation.md) | Guide for adding new providers |
| [github-actions-summary.md](development/github-actions-summary.md) | CI/CD workflow documentation |
| [github-actions-testing.md](development/github-actions-testing.md) | Testing GitHub Actions workflows |
| [project-context.md](development/project-context.md) | Project context and background |
| [uwp-to-wpf-migration.md](development/uwp-to-wpf-migration.md) | WPF desktop app migration guide |
| [wpf-implementation-notes.md](development/wpf-implementation-notes.md) | WPF implementation details |
| [desktop-app-xaml-compiler-errors.md](development/desktop-app-xaml-compiler-errors.md) | Desktop app troubleshooting |

---

### [operations/](operations/) - Operations Guides

Guides for running the system in production.

| Document | Description |
|----------|-------------|
| [operator-runbook.md](operations/operator-runbook.md) | Operations guide for production |
| [portable-data-packager.md](operations/portable-data-packager.md) | Creating portable data archives |
| [msix-packaging.md](operations/msix-packaging.md) | MSIX packaging and signing for Windows Desktop |

---

### [integrations/](integrations/) - External Integrations

Documentation for integrations with external tools.

| Document | Description |
|----------|-------------|
| [lean-integration.md](integrations/lean-integration.md) | QuantConnect Lean Engine integration |
| [fsharp-integration.md](integrations/fsharp-integration.md) | F# domain library guide |
| [language-strategy.md](integrations/language-strategy.md) | Polyglot architecture strategy |

---

### [status/](status/) - Status & Planning

Project status and future planning.

| Document | Description |
|----------|-------------|
| [production-status.md](status/production-status.md) | Production readiness assessment |
| [ROADMAP.md](status/ROADMAP.md) | Feature roadmap and backlog |
| [CHANGELOG.md](status/CHANGELOG.md) | Version changelog and history |

---

### [evaluations/](evaluations/) - Technology Evaluations

Evaluation documents for technology and architecture decisions.

| Document | Description |
|----------|-------------|
| [data-quality-monitoring-evaluation.md](evaluations/data-quality-monitoring-evaluation.md) | Data quality monitoring design |
| [historical-data-providers-evaluation.md](evaluations/historical-data-providers-evaluation.md) | Historical provider comparison |
| [realtime-streaming-architecture-evaluation.md](evaluations/realtime-streaming-architecture-evaluation.md) | Streaming architecture design |
| [storage-architecture-evaluation.md](evaluations/storage-architecture-evaluation.md) | Storage architecture design |

---

### [reference/](reference/) - Reference Material

Additional reference documentation.

| Document | Description |
|----------|-------------|
| [data-dictionary.md](reference/data-dictionary.md) | Data dictionary and field definitions |
| [data-uniformity.md](reference/data-uniformity.md) | Data consistency guidelines |
| [design-review-memo.md](reference/design-review-memo.md) | Design review notes |
| [open-source-references.md](reference/open-source-references.md) | Related open source projects |
| [sandcastle.md](reference/sandcastle.md) | Sandcastle documentation reference |
| [DUPLICATE_CODE_ANALYSIS.md](reference/DUPLICATE_CODE_ANALYSIS.md) | Duplicate code analysis report |

---

### [ai/](ai/) - AI Assistant Guides

Specialized guides for AI coding assistants working with this codebase.

| Document | Description |
|----------|-------------|
| [claude/CLAUDE.providers.md](ai/claude/CLAUDE.providers.md) | Provider implementation guide |
| [claude/CLAUDE.storage.md](ai/claude/CLAUDE.storage.md) | Storage system guide |
| [claude/CLAUDE.fsharp.md](ai/claude/CLAUDE.fsharp.md) | F# domain library guide |
| [claude/CLAUDE.testing.md](ai/claude/CLAUDE.testing.md) | Testing guide |
| [claude/CLAUDE.actions.md](ai/claude/CLAUDE.actions.md) | GitHub Actions guide |
| [copilot/instructions.md](ai/copilot/instructions.md) | Copilot instructions |

---

### [adr/](adr/) - Architecture Decision Records

Documented architectural decisions with context and rationale.

| ADR | Title |
|-----|-------|
| [001](adr/001-provider-abstraction.md) | Provider Abstraction |
| [002](adr/002-tiered-storage-architecture.md) | Tiered Storage Architecture |
| [003](adr/003-microservices-decomposition.md) | Microservices Decomposition |
| [004](adr/004-async-streaming-patterns.md) | Async Streaming Patterns |
| [005](adr/005-attribute-based-discovery.md) | Attribute-Based Discovery |
| [010](adr/010-httpclient-factory.md) | HttpClient Factory |
| [011](adr/011-centralized-configuration-and-credentials.md) | Centralized Configuration |
| [012](adr/012-monitoring-and-alerting-pipeline.md) | Monitoring & Alerting Pipeline |

---

### Other Directories

| Directory | Description |
|-----------|-------------|
| [api/](api/) | API documentation index |
| [diagrams/](diagrams/) | Architecture diagrams (images) |
| [uml/](uml/) | UML diagrams (PlantUML) |
| [docfx/](docfx/) | DocFX documentation generator config |
| [archived/](archived/) | Historical/superseded documentation |

---

## Documentation by Role

### For New Users
1. [Getting Started](getting-started/README.md)
2. [User Guide (HELP.md)](HELP.md)

### For Operators
1. [Operator Runbook](operations/operator-runbook.md)
2. [Production Status](status/production-status.md)
3. [User Guide - Configuration](HELP.md#configuration)

### For Developers
1. [Architecture Overview](architecture/overview.md)
2. [Provider Implementation](development/provider-implementation.md)
3. [Roadmap](status/ROADMAP.md)

### For Quant Developers
1. [Lean Integration](integrations/lean-integration.md)
2. [F# Integration](integrations/fsharp-integration.md)
3. [Data Sources](providers/data-sources.md)

---

## Getting Help

- Check [HELP.md](HELP.md) for the complete user guide with troubleshooting and FAQ
- Review [Production Status](status/production-status.md) for known issues
- See [Architecture Decision Records](adr/) for design rationale

---

*Last Updated: 2026-02-04*
