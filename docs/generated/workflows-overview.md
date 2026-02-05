# GitHub Workflows Overview

> Auto-generated on 2026-02-05 08:56:49 UTC

This document provides an overview of all GitHub Actions workflows in the repository.

## Available Workflows

| Workflow | File | Triggers |
|----------|------|----------|
| Benchmark Performance | `benchmark.yml` | push, PR, manual |
| Build Observability | `build-observability.yml` | push, PR, manual |
| Build and Release | `dotnet-desktop.yml` | push, PR, manual |
| Cache Management | `cache-management.yml` | manual, scheduled |
| Code Quality | `code-quality.yml` | push, PR, manual |
| Dependency Review | `dependency-review.yml` | PR, manual |
| Desktop App Build | `desktop-app.yml` | push, PR, manual |
| Docker | `docker.yml` | push, PR, manual |
| Documentation & Workflow Automation | `docs-comprehensive.yml` | push, PR, manual, scheduled |
| Labeling | `labeling.yml` | PR, manual |
| Mark Stale Issues and PRs | `stale.yml` | manual, scheduled |
| Nightly Testing | `nightly.yml` | manual, scheduled |
| Pull Request Checks | `pr-checks.yml` | PR, manual |
| Release Management | `release.yml` | manual |
| Reusable .NET Build | `reusable-dotnet-build.yml` | unknown |
| Scheduled Maintenance | `scheduled-maintenance.yml` | manual, scheduled |
| Security | `security.yml` | PR, manual, scheduled |
| TODO Automation | `todo-automation.yml` | push, manual, scheduled |
| Test Matrix | `test-matrix.yml` | push, PR, manual |
| Validate Workflows | `validate-workflows.yml` | PR, manual |
| WPF Commands | `wpf-commands.yml` | manual |
| WPF Desktop Build | `wpf-desktop.yml` | push, PR, manual |

## Workflow Categories

### CI/CD Workflows
- **Build & Test**: Main build pipeline, test matrix
- **Code Quality**: Linting, static analysis
- **Security**: Dependency scanning, vulnerability checks

### Documentation Workflows
- **Documentation**: Validation, generation, deployment
- **Docs Structure Sync**: Auto-update structure documentation

### Release Workflows
- **Docker Build**: Container image builds
- **Publishing**: Release artifacts

### Maintenance Workflows
- **Scheduled Maintenance**: Cleanup, dependency updates
- **Stale Management**: Issue/PR lifecycle

## Workflow Count

- **Total workflows:** 22

---

*This file is auto-generated. Do not edit manually.*
