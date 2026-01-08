# GitHub Actions Workflows

This directory contains automated CI/CD workflows for the Market Data Collector project. These workflows ensure code quality, security, and automate common tasks.

## Overview of Workflows

### 1. Build and Release (`dotnet-desktop.yml`)
**Triggers:** Push to `main`, Pull requests, Git tags starting with `v*`, Manual dispatch

The main build and release pipeline that:
- Builds and tests the .NET solution
- Publishes cross-platform binaries (Linux, Windows, macOS x64/ARM64)
- Creates GitHub releases with downloadable artifacts

**Status:** ✅ Existing (Production)

### 2. CodeQL Security Analysis (`codeql-analysis.yml`)
**Triggers:** Push to `main`, Pull requests, Weekly schedule (Mondays at 6:00 UTC)

Automated security vulnerability scanning using GitHub's CodeQL:
- Scans C# codebase for security vulnerabilities
- Runs security and quality queries
- Reports findings in the Security tab
- Scheduled weekly scans to catch new vulnerabilities

**Key Features:**
- Deep semantic code analysis
- Detection of common vulnerabilities (SQL injection, XSS, etc.)
- Integration with GitHub Security Advisory Database
- Automatic PR comments for new vulnerabilities

**Status:** 🆕 New

### 3. Dependency Review (`dependency-review.yml`)
**Triggers:** Pull requests to `main`

Reviews dependency changes in pull requests:
- Scans for known security vulnerabilities in dependencies
- Checks for denied licenses (GPL-2.0, GPL-3.0)
- Fails on moderate or higher severity vulnerabilities
- Adds detailed comments to PRs with findings

**Status:** 🆕 New

### 4. Docker Build and Push (`docker-build.yml`)
**Triggers:** Push to `main`, Pull requests, Git tags, Manual dispatch

Automated Docker image builds and publishing:
- Builds optimized Docker images using multi-stage builds
- Pushes to GitHub Container Registry (ghcr.io)
- Creates multiple tags (latest, branch, version, SHA)
- Uses layer caching for faster builds
- Generates build provenance attestations

**Image Tags Generated:**
- `latest` - Latest main branch build
- `main` - Main branch builds
- `v1.5.0` - Version tags (from git tags)
- `main-abc1234` - Branch + commit SHA

**Registry:** `ghcr.io/rodoHasArrived/market-data-collector`

**Status:** 🆕 New

### 5. Benchmark Performance (`benchmark.yml`)
**Triggers:** Push to `main`, Pull requests (when benchmarks or source code changes), Manual dispatch

Runs performance benchmarks using BenchmarkDotNet:
- Executes all benchmark suites
- Tracks performance trends over time
- Uploads results as artifacts (30-day retention)
- Comments on PRs with benchmark results
- Alerts on performance regressions (>150% threshold)

**Benchmarks Include:**
- Event pipeline throughput
- Order book operations
- JSON serialization performance
- Technical indicators calculation

**Status:** 🆕 New

### 6. Code Quality (`code-quality.yml`)
**Triggers:** Push to `main`, Pull requests, Manual dispatch

Multi-stage code quality checks:

**Lint and Format:**
- Verifies code formatting with `dotnet format`
- Runs static code analysis
- Checks for code style violations

**Markdown Lint:**
- Validates all Markdown files
- Ensures consistent documentation style
- Checks for common formatting issues

**Link Checker:**
- Validates all links in documentation
- Prevents broken documentation links
- Configurable timeout and retry logic

**Status:** 🆕 New

### 7. Stale Issue Management (`stale.yml`)
**Triggers:** Daily at midnight UTC, Manual dispatch

Automated issue and PR lifecycle management:

**Issues:**
- Marked stale after 60 days of inactivity
- Closed 7 days after being marked stale
- Exempt labels: `pinned`, `security`, `bug`, `enhancement`

**Pull Requests:**
- Marked stale after 30 days of inactivity
- Closed 14 days after being marked stale
- Exempt labels: `pinned`, `security`, `work-in-progress`

**Status:** 🆕 New

### 8. Build Observability (`build-observability.yml`)
**Triggers:** Push to `main`, Pull requests, Manual dispatch

Captures build observability artifacts during CI runs:
- Runs the build doctor and observability-enabled build
- Generates dependency graphs, fingerprints, and metrics
- Collects a minimal debug bundle
- Uploads `.build-system/` artifacts for troubleshooting

**Status:** 🆕 New

### 9. Label Management (`label-management.yml`)
**Triggers:** Issues opened/edited, Pull requests opened/edited/synchronized, Manual dispatch

Automated labeling for better issue/PR organization:

**Auto-labels by file path:**
- `documentation` - Changes to docs/ or .md files
- `tests` - Changes to test files
- `ci/cd` - Changes to workflow files
- `performance` - Changes to benchmarks
- `docker` - Changes to Docker files
- `provider` - Changes to data provider code
- `storage` - Changes to storage implementations
- `ui` - Changes to UI projects
- `microservices` - Changes to microservices

**Auto-labels by PR size:**
- `size/XS` - < 10 lines changed
- `size/S` - 10-49 lines changed
- `size/M` - 50-199 lines changed
- `size/L` - 200-499 lines changed
- `size/XL` - 500+ lines changed

**Status:** 🆕 New

## Configuration Files

### `.github/labeler.yml`
Configuration for the GitHub labeler action, defining path patterns for automatic label application.

### `.github/markdown-link-check-config.json`
Configuration for the markdown link checker:
- Ignores localhost URLs
- 20-second timeout per link
- Retries on 429 (rate limit) errors
- Configurable retry count and delays

## Workflow Permissions

All workflows use minimal required permissions following the principle of least privilege:

- **Read access:** Most workflows only read repository contents
- **Write access:** Granted only when needed (e.g., Docker push, creating releases)
- **Security events:** Required for CodeQL analysis
- **Packages:** Required for Docker image publishing

## Best Practices

### Running Workflows Locally

Use [act](https://github.com/nektos/act) to test workflows locally:

```bash
# Install act
brew install act  # macOS
# or
curl https://raw.githubusercontent.com/nektos/act/master/install.sh | sudo bash

# Run a workflow
act pull_request -W .github/workflows/code-quality.yml
```

### Workflow Secrets

Required secrets (configured in repository settings):
- `GITHUB_TOKEN` - Automatically provided by GitHub Actions
- No additional secrets required for current workflows

### Monitoring Workflows

- **Actions Tab:** View all workflow runs
- **Security Tab:** CodeQL findings and dependency alerts
- **Packages Tab:** Published Docker images
- **Pull Requests:** Automatic comments from workflows

## Troubleshooting

### Workflow Failures

**CodeQL Analysis Fails:**
- Ensure .NET SDK 9.0 is available
- Check for build errors in the "Build" step
- Review CodeQL logs for specific errors

**Docker Build Fails:**
- Verify Dockerfile exists at `deploy/docker/Dockerfile`
- Check Docker build context in the workflow
- Ensure base images are accessible

**Benchmark Failures:**
- Benchmarks may fail on underpowered runners
- Check for out-of-memory errors
- Review BenchmarkDotNet logs in artifacts

**Link Checker Failures:**
- External links may be temporarily unavailable
- Check for broken internal documentation links
- Review excluded patterns in config

### Disabling Workflows

To temporarily disable a workflow, add to the top of the file:

```yaml
on:
  workflow_dispatch:  # Only manual trigger
```

Or comment out the trigger section entirely.

## Maintenance

### Updating Actions

Keep actions up to date by reviewing Dependabot PRs or manually updating:

```yaml
# Update from v3 to v4
- uses: actions/checkout@v4  # was v3
- uses: actions/setup-dotnet@v4  # was v3
```

### Adding New Workflows

1. Create a new `.yml` file in `.github/workflows/`
2. Define triggers, jobs, and steps
3. Test locally with `act` if possible
4. Submit a PR and review workflow execution
5. Update this README with the new workflow details

## Resources

- [GitHub Actions Documentation](https://docs.github.com/en/actions)
- [Workflow Syntax](https://docs.github.com/en/actions/reference/workflow-syntax-for-github-actions)
- [CodeQL Documentation](https://codeql.github.com/docs/)
- [Docker Build Action](https://github.com/docker/build-push-action)
- [BenchmarkDotNet](https://benchmarkdotnet.org/)

## Summary

| Workflow | Purpose | Frequency | Status |
|----------|---------|-----------|--------|
| Build and Release | Build, test, and release | On push/PR/tags | ✅ Existing |
| CodeQL Analysis | Security scanning | On push/PR + Weekly | 🆕 New |
| Dependency Review | Dependency security | On PR | 🆕 New |
| Docker Build | Container images | On push/PR/tags | 🆕 New |
| Benchmark | Performance testing | On code changes | 🆕 New |
| Code Quality | Linting and formatting | On push/PR | 🆕 New |
| Stale Management | Issue/PR lifecycle | Daily | 🆕 New |
| Build Observability | Build observability artifacts | On push/PR | 🆕 New |
| Label Management | Auto-labeling | On issue/PR activity | 🆕 New |

**Total:** 9 workflows (1 existing + 8 new)
