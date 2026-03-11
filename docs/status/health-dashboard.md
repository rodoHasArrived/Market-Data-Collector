# Documentation Health Dashboard

> Auto-generated documentation health report. Do not edit manually.
> Last updated: 2026-03-11T12:51:02.072896+00:00

## Overall Health Score

```
  [###########################---] 90/100
  Rating: Excellent
```

## Summary

| Metric | Value |
|--------|-------|
| Total documentation files | 175 |
| Total lines | 72,891 |
| Average file size (lines) | 416.5 |
| Orphaned files | 40 |
| Files without headings | 1 |
| Stale files (>90 days) | 0 |
| TODO/FIXME markers | 155 |
| **Health score** | **90/100** |

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

- `.claude/skills/mdc-code-review/SKILL.md`
- `.claude/skills/mdc-code-review/agents/grader.md`
- `.claude/skills/mdc-code-review/references/architecture.md`
- `.claude/skills/mdc-code-review/references/schemas.md`
- `.github/CS0101_FIX_SUMMARY.md`
- `.github/PULL_REQUEST_TEMPLATE.md`
- `.github/QUICKSTART.md`
- `.github/TEST_MATRIX_FIX_SUMMARY.md`
- `.github/WORKFLOW_IMPROVEMENTS.md`
- `.github/instructions/docs.instructions.md`
- `.github/instructions/dotnet-tests.instructions.md`
- `.github/pull_request_template_desktop.md`
- `.github/workflows/AI_SYNC_FIX_SUMMARY.md`
- `.github/workflows/SKIPPED_JOBS_EXPLAINED.md`
- `.github/workflows/TESTING_AI_SYNC.md`
- `benchmarks/BOTTLENECK_REPORT.md`
- `docs/architecture/crystallized-storage-format.md`
- `docs/archived/2026-02_PR_SUMMARY.md`
- `docs/archived/2026-02_UI_IMPROVEMENTS_SUMMARY.md`
- `docs/archived/2026-02_VISUAL_CODE_EXAMPLES.md`
- ... and 20 more

## Trend

<!-- Trend data will be appended by CI when historical snapshots are available. -->

| Date | Score | Files | Orphans | Stale |
|------|-------|-------|---------|-------|
| 2026-03-11 | 90 | 175 | 40 | 0 |

---

*This file is auto-generated. Do not edit manually.*
