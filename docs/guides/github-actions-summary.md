# GitHub Actions Workflows - Summary

This document provides a quick reference for all GitHub Actions workflows in the Market Data Collector repository.

## New Workflows Added (2026-01-08)

### 1. CodeQL Security Analysis
- **File:** `.github/workflows/codeql-analysis.yml`
- **Purpose:** Automated security vulnerability detection
- **Runs:** On push, PRs, and weekly (Mondays)
- **Benefits:** Early detection of security issues, compliance

### 2. Dependency Review
- **File:** `.github/workflows/dependency-review.yml`
- **Purpose:** Review dependencies in PRs for vulnerabilities
- **Runs:** On pull requests
- **Benefits:** Prevents vulnerable dependencies from being merged

### 3. Docker Build and Push
- **File:** `.github/workflows/docker-build.yml`
- **Purpose:** Automated Docker image builds
- **Runs:** On push to main, PRs, and tags
- **Registry:** GitHub Container Registry (ghcr.io)
- **Benefits:** Consistent container images, automated deployment

### 4. Benchmark Performance
- **File:** `.github/workflows/benchmark.yml`
- **Purpose:** Track performance metrics over time
- **Runs:** When source or benchmark code changes
- **Benefits:** Detect performance regressions early

### 5. Code Quality
- **File:** `.github/workflows/code-quality.yml`
- **Purpose:** Enforce code quality standards
- **Checks:** Code formatting, markdown linting, link validation
- **Runs:** On push and PRs
- **Benefits:** Consistent code style, valid documentation

### 6. Stale Issue Management
- **File:** `.github/workflows/stale.yml`
- **Purpose:** Automatic lifecycle management
- **Runs:** Daily
- **Benefits:** Keeps issue tracker clean and organized

### 7. Label Management
- **File:** `.github/workflows/label-management.yml`
- **Purpose:** Auto-label issues and PRs
- **Runs:** When issues/PRs are created or updated
- **Benefits:** Better organization, easier filtering

## Configuration Files

- **`.github/labeler.yml`** - Label definitions and path patterns
- **`.github/markdown-link-check-config.json`** - Link checker settings

## Quick Commands

```bash
# View all workflows
ls -la .github/workflows/

# Validate YAML syntax
yamllint .github/workflows/*.yml

# Test locally with act
act pull_request -W .github/workflows/code-quality.yml

# Check workflow status
gh run list --limit 10
```

## Workflow Status Badges

The following badges have been added to README.md:

```markdown
[![Build and Release](https://github.com/rodoHasArrived/Market-Data-Collector/actions/workflows/dotnet-desktop.yml/badge.svg)](...)
[![CodeQL](https://github.com/rodoHasArrived/Market-Data-Collector/actions/workflows/codeql-analysis.yml/badge.svg)](...)
[![Docker Build](https://github.com/rodoHasArrived/Market-Data-Collector/actions/workflows/docker-build.yml/badge.svg)](...)
[![Code Quality](https://github.com/rodoHasArrived/Market-Data-Collector/actions/workflows/code-quality.yml/badge.svg)](...)
```

## Impact

These workflows provide:
- ✅ Automated security scanning
- ✅ Dependency vulnerability checks
- ✅ Consistent Docker images
- ✅ Performance regression detection
- ✅ Code quality enforcement
- ✅ Better issue/PR organization
- ✅ Reduced manual maintenance

## Next Steps

1. Monitor first runs of each workflow
2. Adjust thresholds and settings as needed
3. Consider adding:
   - SonarCloud integration for deeper code analysis
   - Release notes automation
   - Automated changelog generation
   - Integration testing workflow
   - Load testing workflow

## Resources

- [GitHub Actions Documentation](https://docs.github.com/en/actions)
- [CodeQL for C#](https://codeql.github.com/docs/codeql-language-guides/codeql-for-csharp/)
- [Docker Build Action](https://github.com/docker/build-push-action)
- [BenchmarkDotNet](https://benchmarkdotnet.org/)

---

**Created:** 2026-01-08  
**Total Workflows:** 8 (1 existing + 7 new)  
**Total Configuration Files:** 2  
**Documentation Files:** 2
