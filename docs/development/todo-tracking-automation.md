# TODO Tracking Automation

## Overview

The Documentation Automation workflow automatically creates and maintains a consolidated **TODO Tracking Issue** that provides visibility into all TODO/FIXME/HACK/NOTE comments in the codebase.

## Features

### Automatic Issue Creation

- **Single Tracking Issue**: Creates one consolidated issue instead of spamming individual issues
- **Smart Updates**: Updates existing issue when it already exists (uses `todo-tracking` label)
- **Scheduled Runs**: Automatically runs on push to main and weekly schedule
- **Manual Control**: Can be triggered or disabled via workflow dispatch

### Issue Content

The tracking issue includes:

1. **Summary Metrics**
   - Total TODO items
   - Number tracked (linked to issues)
   - Number untracked

2. **Breakdown Tables**
   - By type (TODO, FIXME, HACK, NOTE, etc.)
   - By directory (shows which parts of codebase have most TODOs)

3. **High Priority Items**
   - Highlights FIXME and critical TODOs
   - Shows file path and line number
   - Includes full description

4. **Sample of Untracked Items**
   - Shows first 15 untracked items
   - Helps identify what needs to be linked to issues

5. **AI Triage Recommendations**
   - When available, includes AI-generated recommendations
   - Suggests priority, labels, and batching strategies

6. **Links**
   - Link to full TODO.md documentation
   - Link to workflow run for traceability

## How It Works

### Workflow Job Flow

```
scan-todos (runs TODO scan)
    ↓
create-todo-tracking-issue (new job)
    ↓
    1. Download scan results
    2. Check for existing tracking issue (label: todo-tracking)
    3. Build issue content with metrics
    4. Create or update issue
    5. Add summary to workflow output
```

### Conditions

The job runs when:
- TODO scan completes successfully
- Total TODO count > 0
- Event is push or schedule (NOT pull requests)
- Input `create_tracking_issue` is not explicitly false

### Preventing Duplicates

- Uses dedicated label: `todo-tracking`
- Searches for existing open issue with this label
- Updates existing issue instead of creating new one
- Only one tracking issue exists at a time

## Usage

### Automatic Operation

The tracking issue is automatically created/updated:
- On every push to main branch (if TODOs are scanned)
- Weekly via scheduled run (Monday 3 AM UTC)

No manual intervention needed!

### Manual Trigger

You can manually trigger the workflow:

```bash
gh workflow run documentation.yml \
  --ref main \
  -f scan_todos=true \
  -f create_tracking_issue=true
```

### Disabling

To disable automatic issue creation:

```bash
gh workflow run documentation.yml \
  --ref main \
  -f create_tracking_issue=false
```

## Labels

The tracking issue uses these labels:
- `todo-tracking` - Primary label for finding the issue
- `documentation` - Indicates it's auto-generated documentation
- `automation` - Indicates it's created by automation

## Linking TODOs to Issues

To mark a TODO as tracked, add an issue reference:

```csharp
// TODO: Track with issue #123 - Implement retry logic
// This is needed because the API occasionally returns 503 errors.
```

The scan will recognize these patterns:
- `#123`
- `issue #123`
- `Track with issue #123`
- `issues/123`
- Full GitHub URLs

## Benefits

### Before This Feature
- TODOs only documented in TODO.md file
- No visibility in GitHub Issues
- Manual effort needed to track progress
- Easy to lose track of unlinked TODOs

### After This Feature
- ✅ Single consolidated tracking issue
- ✅ Automatic updates keep it current
- ✅ High-priority items highlighted
- ✅ Easy to see what needs attention
- ✅ Integrated with GitHub Issues workflow
- ✅ No spam or duplicate issues

## Example Issue Content

```markdown
# TODO Tracking Report

**Last Updated:** 2026-02-12 08:30 UTC
**Scan Time:** 2026-02-12T01:40:52.316231+00:00

## Summary

- **Total TODOs:** 16
- **Tracked (linked to issues):** 0
- **Untracked:** 16

### By Type

| Type | Count |
|------|-------|
| `NOTE` | 12 |
| `TODO` | 4 |

### By Directory

| Directory | Count |
|-----------|-------|
| `tests/` | 9 |
| `src/` | 7 |

## 🔴 High Priority Untracked Items

1. **[FIXME]** `src/Services/Validation.cs:45`
   - Fix critical data validation bug

## 📋 Untracked Items (showing 15 of 16)

- **[TODO]** `src/Services/OrderBook.cs:37` — Implement once LiveDataService supports...
- **[TODO]** `src/Services/Portfolio.cs:223` — Implement once WatchlistService supports...
...

## 📚 Full Documentation

For complete details, see [docs/status/TODO.md](https://github.com/rodoHasArrived/Market-Data-Collector/blob/main/docs/status/TODO.md)

## 🤖 About This Issue

This issue is automatically created and updated by the Documentation Automation workflow.
```

## Troubleshooting

### Issue Not Created

Check:
1. Are there any TODOs in the codebase? (total_count > 0)
2. Did the scan-todos job complete successfully?
3. Is `create_tracking_issue` input set to false?
4. Are you running on a pull request? (disabled for PRs)

### Issue Not Updated

Check:
1. Does the existing issue have the `todo-tracking` label?
2. Is the existing issue still open?
3. Check workflow logs for error messages

### Permission Errors

The workflow needs:
- `issues: write` permission
- `contents: read` permission

These are already configured in the workflow permissions.

## Future Enhancements

Potential improvements:
- Close/reopen based on TODO count
- Add milestones based on priority
- Link to related issues automatically
- Create sub-tasks for high-priority items
- Notify assignees of high-priority TODOs

## Related Files

- **Workflow**: `.github/workflows/documentation.yml` (job: `create-todo-tracking-issue`)
- **Scanner**: `build/scripts/docs/scan-todos.py`
- **Output**: `docs/status/TODO.md`
- **Scan Results**: `todo-scan-results.json` (workflow artifact)

## See Also

- [TODO Documentation](../status/TODO.md) - Full TODO tracking documentation
- [Documentation Automation Workflow](../../.github/workflows/documentation.yml) - The workflow file
- [GitHub Actions Documentation](https://docs.github.com/en/actions)
