# Market Data Collector Documentation

**Version:** 1.6.1
**Last Updated:** 2026-01-27

Welcome to the Market Data Collector documentation. This guide will help you find the information you need.

---

## Quick Links

| I want to... | Go to... |
|--------------|----------|
| Get started quickly | [Getting Started](guides/getting-started.md) |
| Configure the application | [Configuration Guide](guides/configuration.md) |
| Troubleshoot an issue | [Troubleshooting](guides/troubleshooting.md) |
| Understand the architecture | [Architecture Overview](architecture/overview.md) |
| Check production readiness | [Production Status](status/production-status.md) |

---

## Documentation Structure

### [guides/](guides/) - User Guides

Step-by-step guides for using the system.

| Document | Description |
|----------|-------------|
| [getting-started.md](guides/getting-started.md) | Quick start guide for new users |
| [configuration.md](guides/configuration.md) | Complete configuration reference |
| [troubleshooting.md](guides/troubleshooting.md) | Common issues and solutions |
| [operator-runbook.md](guides/operator-runbook.md) | Operations guide for production |
| [provider-implementation.md](guides/provider-implementation.md) | Guide for adding new providers |
| [portable-data-packager.md](guides/portable-data-packager.md) | Creating portable data archives |
| [msix-packaging.md](guides/msix-packaging.md) | MSIX packaging and signing for Windows Desktop |
| [github-actions-summary.md](guides/github-actions-summary.md) | CI/CD workflow documentation |

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

### [reference/](reference/) - Reference Material

Additional reference documentation.

| Document | Description |
|----------|-------------|
| [open-source-references.md](reference/open-source-references.md) | Related open source projects |
| [data-uniformity.md](reference/data-uniformity.md) | Data consistency guidelines |
| [design-review-memo.md](reference/design-review-memo.md) | Design review notes |

---

### [ai-assistants/](ai-assistants/) - AI Assistant Guides

Specialized guides for AI coding assistants working with this codebase.

| Document | Description |
|----------|-------------|
| [CLAUDE.providers.md](ai-assistants/CLAUDE.providers.md) | Provider implementation guide |
| [CLAUDE.storage.md](ai-assistants/CLAUDE.storage.md) | Storage system guide |
| [CLAUDE.fsharp.md](ai-assistants/CLAUDE.fsharp.md) | F# domain library guide |
| [CLAUDE.testing.md](ai-assistants/CLAUDE.testing.md) | Testing guide |

---

### [analysis/](analysis/) - Code Analysis

| Document | Description |
|----------|-------------|
| [DUPLICATE_CODE_ANALYSIS.md](analysis/DUPLICATE_CODE_ANALYSIS.md) | Duplicate code analysis report |

---

### Other Directories

| Directory | Description |
|-----------|-------------|
| [api/](api/) | API documentation index |
| [adr/](adr/) | Architecture Decision Records |
| [changelogs/](changelogs/) | Version change summaries |
| [diagrams/](diagrams/) | Architecture diagrams (images) |
| [uml/](uml/) | UML diagrams (PlantUML) |
| [docfx/](docfx/) | DocFX documentation generator config |

---

## Documentation by Role

### For New Users
1. [Getting Started](guides/getting-started.md)
2. [Configuration Guide](guides/configuration.md)
3. [Troubleshooting](guides/troubleshooting.md)

### For Operators
1. [Operator Runbook](guides/operator-runbook.md)
2. [Production Status](status/production-status.md)
3. [Configuration Guide](guides/configuration.md)

### For Developers
1. [Architecture Overview](architecture/overview.md)
2. [Provider Management](architecture/provider-management.md)
3. [Roadmap](status/ROADMAP.md)

### For Quant Developers
1. [Lean Integration](integrations/lean-integration.md)
2. [F# Integration](integrations/fsharp-integration.md)
3. [Data Sources](providers/data-sources.md)

---

## Getting Help

- Check [Troubleshooting](guides/troubleshooting.md) for common issues
- Review [Configuration](guides/configuration.md) for setup questions
- See [Production Status](status/production-status.md) for known issues
