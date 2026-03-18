# Documentation Health Dashboard

> Auto-generated documentation health report. Do not edit manually.
> Last updated: 2026-03-18T03:07:23.609429+00:00

## Overall Health Score

```
  [############################--] 92/100
  Rating: Excellent
```

## Summary

| Metric | Value |
|--------|-------|
| Total documentation files | 215 |
| Total lines | 84,625 |
| Average file size (lines) | 393.6 |
| Orphaned files | 37 |
| Files without headings | 1 |
| Stale files (>90 days) | 0 |
| TODO/FIXME markers | 178 |
| **Health score** | **92/100** |

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

- `.claude/skills/mdc-brainstorm/references/competitive-landscape.md`
- `.claude/skills/mdc-brainstorm/references/idea-dimensions.md`
- `.claude/skills/mdc-code-review/agents/grader.md`
- `.claude/skills/mdc-code-review/references/schemas.md`
- `.github/PULL_REQUEST_TEMPLATE.md`
- `.github/pull_request_template_desktop.md`
- `.github/workflows/SKIPPED_JOBS_EXPLAINED.md`
- `benchmarks/BOTTLENECK_REPORT.md`
- `docs/archived/2026-02_PR_SUMMARY.md`
- `docs/archived/2026-02_UI_IMPROVEMENTS_SUMMARY.md`
- `docs/archived/2026-02_VISUAL_CODE_EXAMPLES.md`
- `docs/archived/ARTIFACT_ACTIONS_DOWNGRADE.md`
- `docs/archived/CHANGES_SUMMARY.md`
- `docs/archived/CONFIG_CONSOLIDATION_REPORT.md`
- `docs/archived/CS0101_FIX_SUMMARY.md`
- `docs/archived/DUPLICATE_CODE_ANALYSIS.md`
- `docs/archived/IMPROVEMENTS_2026-02.md`
- `docs/archived/QUICKSTART_2026-01-08.md`
- `docs/archived/REDESIGN_IMPROVEMENTS.md`
- `docs/archived/REPOSITORY_REORGANIZATION_PLAN.md`
- ... and 17 more

## Trend

<!-- Trend data will be appended by CI when historical snapshots are available. -->

| Date | Score | Files | Orphans | Stale |
|------|-------|-------|---------|-------|
| 2026-03-18 | 92 | 215 | 37 | 0 |

---

*This file is auto-generated. Do not edit manually.*
