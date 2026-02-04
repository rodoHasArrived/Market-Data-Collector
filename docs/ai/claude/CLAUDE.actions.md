# CLAUDE.actions.md - GitHub Actions Guide

This guide summarizes how GitHub Actions are organized in the Market Data Collector repo and where to look for CI/CD context.

## Key Locations

- Workflows live in `.github/workflows/`.
- Composite and custom actions live in `.github/actions/`.
- Documentation for CI/CD is in:
  - `docs/development/github-actions-summary.md`
  - `docs/development/github-actions-testing.md`

## Typical Workflow Responsibilities

- **Build/Test**: multi-platform build + test matrix.
- **Quality/Security**: code quality checks, dependency review, CodeQL.
- **Release/Packaging**: desktop app builds, Docker image builds, release automation.
- **Docs Automation**: documentation generation and structure synchronization.

## Common Tasks

- Review workflow definitions and associated reusable workflows in `.github/workflows/`.
- Use the summary and testing docs above for CI/CD intent and local testing tips.
- When updating workflows, verify related documentation and ensure naming consistency.

## Related References

- `docs/development/github-actions-summary.md`
- `docs/development/github-actions-testing.md`
- `.github/workflows/README.md`

