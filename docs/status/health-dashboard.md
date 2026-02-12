# Documentation Health Dashboard

> Auto-generated documentation health report. Do not edit manually.
> Last updated: 2026-02-12T12:50:10.090154+00:00

## Overall Health Score

```
  [##########################----] 86/100
  Rating: Good
```

## Summary

| Metric | Value |
|--------|-------|
| Total documentation files | 137 |
| Total lines | 55,011 |
| Average file size (lines) | 401.5 |
| Orphaned files | 51 |
| Files without headings | 1 |
| Stale files (>90 days) | 0 |
| TODO/FIXME markers | 88 |
| **Health score** | **86/100** |

### Score Breakdown

| Component | Weight | Description |
|-----------|--------|-------------|
| Orphan ratio | 30 pts | Fewer orphaned files is better |
| Heading coverage | 25 pts | All files should have at least one heading |
| Freshness | 20 pts | Files updated within the last 90 days |
| TODO density | 15 pts | Lower density of TODO/FIXME markers |
| Average size | 10 pts | Files averaging at least 20 lines |

## Top Priorities for Improvement

### Files Without Headings

These files lack a Markdown heading, making them harder to navigate:

- `.github/PULL_REQUEST_TEMPLATE.md`

### Orphaned Documentation

These files are not linked from any other Markdown file in the repository:

- `.github/PULL_REQUEST_TEMPLATE.md`
- `.github/QUICKSTART.md`
- `.github/WORKFLOW_IMPROVEMENTS.md`
- `.github/pull_request_template_desktop.md`
- `.github/workflows/AI_SYNC_FIX_SUMMARY.md`
- `.github/workflows/SKIPPED_JOBS_EXPLAINED.md`
- `.github/workflows/TESTING_AI_SYNC.md`
- `CONFIG_CONSOLIDATION_REPORT.md`
- `docs/architecture/crystallized-storage-format.md`
- `docs/archived/2026-02_PR_SUMMARY.md`
- `docs/archived/2026-02_UI_IMPROVEMENTS_SUMMARY.md`
- `docs/archived/2026-02_VISUAL_CODE_EXAMPLES.md`
- `docs/archived/ARTIFACT_ACTIONS_DOWNGRADE.md`
- `docs/archived/CHANGES_SUMMARY.md`
- `docs/archived/DUPLICATE_CODE_ANALYSIS.md`
- `docs/archived/REDESIGN_IMPROVEMENTS.md`
- `docs/archived/REPOSITORY_REORGANIZATION_PLAN.md`
- `docs/archived/desktop-ui-alternatives-evaluation.md`
- `docs/audits/CLEANUP_OPPORTUNITIES.md`
- `docs/audits/CLEANUP_SUMMARY.md`
- ... and 31 more

## Trend

<!-- Trend data will be appended by CI when historical snapshots are available. -->

| Date | Score | Files | Orphans | Stale |
|------|-------|-------|---------|-------|
| 2026-02-12 | 86 | 137 | 51 | 0 |

---

*This file is auto-generated. Do not edit manually.*
