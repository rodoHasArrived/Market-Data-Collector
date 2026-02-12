# Market Data Collector Documentation

**Version:** 1.6.1  
**Last Updated:** 2026-02-12

Welcome to the Market Data Collector documentation. This guide will help you find the information you need, whether you're a developer, operator, or user.

---

## 🚀 Quick Start

- **New Users** → [Getting Started Guide](getting-started/README.md)
- **Developers** → [Repository Organization Guide](development/repository-organization-guide.md)
- **Operators** → [Operator Runbook](operations/operator-runbook.md)
- **Contributors** → See [Contributing](#contributing) section below

---

## 📖 Documentation by Audience

### For Users

Getting the system running and using its features.

- [Getting Started](getting-started/README.md) - Quick start guide
- [Help & FAQ](HELP.md) - Comprehensive user and operator guide
- [Provider Setup Guides](providers/) - Setup instructions for each data provider
  - [Alpaca Setup](providers/alpaca-setup.md)
  - [Interactive Brokers Setup](providers/interactive-brokers-setup.md)
- [Provider Comparison](providers/provider-comparison.md) - Compare features and costs
- [Backfill Guide](providers/backfill-guide.md) - Historical data backfill procedures
- [Data Dictionary](reference/data-dictionary.md) - Explanation of data fields

### For Developers

Building, extending, and testing the system.

- [Repository Organization Guide](development/repository-organization-guide.md) - **START HERE**
- [Repository Cleanup Action Plan](development/repository-cleanup-action-plan.md) - Cleanup procedures
- [Provider Implementation Guide](development/provider-implementation.md) - Add new providers
- [Desktop Development Guide](development/wpf-implementation-notes.md) - WPF/UWP development
- [Build Observability](development/build-observability.md) - Build metrics
- [GitHub Actions Summary](development/github-actions-summary.md) - CI/CD overview

### For Operators

Deploying, monitoring, and maintaining the system.

- [Operator Runbook](operations/operator-runbook.md) - Day-to-day operations
- [Portable Data Packager](operations/portable-data-packager.md) - Data packages
- [MSIX Packaging](operations/msix-packaging.md) - Desktop app packaging

### For Architecture & Design

Understanding system design.

- [Architecture Overview](architecture/overview.md) - High-level system architecture
- [Layer Boundaries](architecture/layer-boundaries.md) - Project dependency rules
- [Storage Design](architecture/storage-design.md) - Storage architecture
- [ADRs](adr/) - Architecture Decision Records

---

## 📊 Project Status & Planning

- [**Project Roadmap**](status/ROADMAP.md) - **Primary planning document**
- [**Improvements Tracker**](status/IMPROVEMENTS.md) - **Consolidated improvement tracking**
- [TODO Tracking](status/TODO.md) - Auto-scanned TODO comments
- [Changelog](status/CHANGELOG.md) - Version history
- [Production Status](status/production-status.md) - Production readiness

---

## 🔍 Reference Documentation

- [API Reference](reference/api-reference.md) - HTTP API endpoints
- [Data Dictionary](reference/data-dictionary.md) - Data model definitions
- [Generated Documentation](generated/) - Auto-generated docs
- [Diagrams](diagrams/) - System diagrams (DOT, PNG, SVG)
- [UML Diagrams](uml/) - UML diagrams

---

## 🔧 Integration & Evaluations

- [QuantConnect Lean Integration](integrations/lean-integration.md) - Backtesting
- [F# Integration](integrations/fsharp-integration.md) - F# domain logic
- [Technology Evaluations](evaluations/) - Provider and architecture evaluations
- [Code Audits](audits/) - Code quality audits

---

## 🤖 AI Assistant Guides

- [Claude Instructions](ai/claude/) - Claude-specific guides
- [GitHub Copilot Instructions](ai/copilot/instructions.md) - Copilot config
- [AI Known Errors](ai/ai-known-errors.md) - Common AI mistakes

---

## 📦 Archived Documentation

Historical documentation superseded by newer guides.

- [Archived Documentation Index](archived/INDEX.md) - Complete list with context

---

## 🚧 Contributing

### How to Contribute

1. **Read the guides:**
   - [Repository Organization Guide](development/repository-organization-guide.md)
   - [Repository Cleanup Action Plan](development/repository-cleanup-action-plan.md)

2. **Follow conventions:**
   - File naming: PascalCase for C# files
   - Project structure: See organization guide
   - Testing: `dotnet test` must pass

3. **Submit PR:**
   - Small, focused changes
   - Clear commit messages
   - Reference issues

### Documentation Standards

See [Documentation Contribution Guide](development/documentation-contribution-guide.md) for complete standards.

Quick rules:
- **Audience:** State who the doc is for (Users, Developers, Operators)
- **Date:** Include "Last Updated" date in front matter
- **Format:** Use GitHub Flavored Markdown
- **Examples:** Include runnable code examples
- **Links:** Use relative paths for internal links
- **Naming:** Use kebab-case (lowercase with hyphens)

---

## 📞 Getting Help

1. **Search documentation** - Use GitHub search or Ctrl+F
2. **Check FAQ** - See [HELP.md](HELP.md)
3. **Ask the community** - Open a Discussion
4. **Report bugs** - Open an Issue

---

## Directory Structure

```
docs/
├── adr/                    # Architecture Decision Records
├── ai/                     # AI assistant instructions
├── architecture/           # Architecture documentation
├── archived/               # Historical/superseded docs
├── audits/                 # Code audits
├── development/            # Developer guides
├── diagrams/               # Generated diagrams
├── evaluations/            # Technology evaluations
├── generated/              # Auto-generated docs
├── getting-started/        # User onboarding
├── integrations/           # Integration guides
├── operations/             # Operational guides
├── providers/              # Provider-specific docs
├── reference/              # API and data references
├── status/                 # Project status tracking
├── uml/                    # UML diagrams
└── README.md              # This file
```

---

*Documentation maintained by core team. Last update: 2026-02-12*
