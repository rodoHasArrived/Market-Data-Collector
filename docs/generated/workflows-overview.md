# GitHub Workflows Overview

> Auto-generated on 2026-02-09 03:59:40 UTC

This document provides an overview of all GitHub Actions workflows in the repository.

## Available Workflows

| Workflow | File | Triggers |
|----------|------|----------|
| Benchmark Performance | `benchmark.yml` | push, PR, manual |
| Build Observability | `build-observability.yml` | push, PR, manual |
| Build and Release | `dotnet-desktop.yml` | push, PR, manual |
| Code Quality | `code-quality.yml` | push, PR, manual |
| Desktop Builds | `desktop-builds.yml` | push, PR, manual |
| Docker | `docker.yml` | push, PR, manual |
| Documentation Automation | `documentation.yml` | push, PR, manual, scheduled |
| Labeling | `labeling.yml` | PR, manual |
| Mark Stale Issues and PRs | `stale.yml` | manual, scheduled |
| Nightly Testing | `nightly.yml` | manual, scheduled |
| Pull Request Checks | `pr-checks.yml` | PR, manual |
| Release Management | `release.yml` | manual |
| Reusable .NET Build | `reusable-dotnet-build.yml` | unknown |
| Scheduled Maintenance | `scheduled-maintenance.yml` | manual, scheduled |
| Security | `security.yml` | PR, manual, scheduled |
| Test Matrix | `test-matrix.yml` | push, PR, manual |
| Validate Workflows | `validate-workflows.yml` | PR, manual |

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

- **Total workflows:** 17

---

*This file is auto-generated. Do not edit manually.*
