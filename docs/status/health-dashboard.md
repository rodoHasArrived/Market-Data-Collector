# Documentation Health Dashboard

> Auto-generated documentation health report. Do not edit manually.
> Last updated: 2026-04-03T08:12:34.484376+00:00

## Overall Health Score

```
  [###########################---] 90/100
  Rating: Excellent
```

## Summary

| Metric | Value |
|--------|-------|
| Total documentation files | 233 |
| Total lines | 93,330 |
| Average file size (lines) | 400.6 |
| Orphaned files | 55 |
| Files without headings | 2 |
| Stale files (>90 days) | 0 |
| TODO/FIXME markers | 184 |
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
- `.github/agents/cleanup-specialist.agent.md`

### Orphaned Documentation

These files are not linked from any other Markdown file in the repository:

- `.claude/agents/meridian-blueprint.md`
- `.claude/agents/meridian-cleanup.md`
- `.claude/agents/meridian-docs.md`
- `.claude/skills/meridian-blueprint/references/blueprint-patterns.md`
- `.claude/skills/meridian-blueprint/references/pipeline-position.md`
- `.claude/skills/meridian-brainstorm/SKILL.md`
- `.claude/skills/meridian-brainstorm/references/competitive-landscape.md`
- `.claude/skills/meridian-brainstorm/references/idea-dimensions.md`
- `.claude/skills/meridian-code-review/agents/grader.md`
- `.claude/skills/meridian-code-review/references/architecture.md`
- `.claude/skills/meridian-code-review/references/schemas.md`
- `.claude/skills/meridian-provider-builder/SKILL.md`
- `.claude/skills/meridian-test-writer/SKILL.md`
- `.github/PULL_REQUEST_TEMPLATE.md`
- `.github/agents/cleanup-specialist.agent.md`
- `.github/pull_request_template_desktop.md`
- `.github/workflows/SKIPPED_JOBS_EXPLAINED.md`
- `AUDIT_REPORT.md`
- `AUDIT_REPORT_2026_03_20.md`
- `benchmarks/BOTTLENECK_REPORT.md`
- ... and 35 more

## Trend

<!-- Trend data will be appended by CI when historical snapshots are available. -->

| Date | Score | Files | Orphans | Stale |
|------|-------|-------|---------|-------|
| 2026-04-03 | 90 | 233 | 55 | 0 |

---

*This file is auto-generated. Do not edit manually.*
