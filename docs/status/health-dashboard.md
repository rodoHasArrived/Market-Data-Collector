# Documentation Health Dashboard

> Auto-generated documentation health report. Do not edit manually.
> Last updated: 2026-03-16T08:12:08.914076+00:00

## Overall Health Score

```
  [###########################---] 90/100
  Rating: Excellent
```

## Summary

| Metric | Value |
|--------|-------|
| Total documentation files | 184 |
| Total lines | 76,137 |
| Average file size (lines) | 413.8 |
| Orphaned files | 41 |
| Files without headings | 1 |
| Stale files (>90 days) | 0 |
| TODO/FIXME markers | 160 |
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
- `.github/PULL_REQUEST_TEMPLATE.md`
- `docs/archived/CS0101_FIX_SUMMARY.md`
- `docs/archived/QUICKSTART_2026-01-08.md`
- `docs/archived/TEST_MATRIX_FIX_SUMMARY.md`
- `docs/archived/WORKFLOW_IMPROVEMENTS_2026-01-08.md`
- `.github/agents/code-review-agent.md`
- `.github/instructions/csharp.instructions.md`
- `.github/instructions/docs.instructions.md`
- `.github/instructions/dotnet-tests.instructions.md`
- `.github/instructions/wpf.instructions.md`
- `.github/pull_request_template_desktop.md`
- `.github/workflows/SKIPPED_JOBS_EXPLAINED.md`
- `docs/ai/copilot/ai-sync-workflow.md`
- `benchmarks/BOTTLENECK_REPORT.md`
- `docs/archived/2026-02_PR_SUMMARY.md`
- ... and 21 more

## Trend

<!-- Trend data will be appended by CI when historical snapshots are available. -->

| Date | Score | Files | Orphans | Stale |
|------|-------|-------|---------|-------|
| 2026-03-16 | 90 | 184 | 41 | 0 |

---

*This file is auto-generated. Do not edit manually.*
