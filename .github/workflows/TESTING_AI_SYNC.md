# Testing AI Instructions Sync Workflow

This document explains how to test the AI Instructions Sync workflow fixes.

## Problem Fixed

The workflow was failing with:
```
##[error]GitHub Actions is not permitted to create or approve pull requests.
```

This occurred because the repository setting "Allow GitHub Actions to create and approve pull requests" was not enabled.

## Solution Implemented

The workflow now:
1. Attempts to create a PR when `create_pr` is set to `true`
2. If PR creation fails, gracefully falls back to direct commit
3. Provides clear messaging about what happened
4. No longer fails the workflow due to repository settings

## Testing Scenarios

### Scenario 1: Direct Commit (Default)
This should work without any special repository settings.

**To Test:**
1. Go to Actions > AI Instructions Sync
2. Click "Run workflow"
3. Leave `create_pr` unchecked (default)
4. Click "Run workflow"

**Expected Result:**
- Workflow completes successfully
- Changes are committed directly to main branch
- Summary shows "Create PR mode: no"

### Scenario 2: PR Creation (Without Repository Setting)
This tests the fallback mechanism.

**To Test:**
1. Ensure "Allow GitHub Actions to create and approve pull requests" is DISABLED:
   - Go to Settings > Actions > General
   - Look for "Workflow permissions" section
   - Ensure "Allow GitHub Actions to create and approve pull requests" is unchecked
2. Go to Actions > AI Instructions Sync
3. Click "Run workflow"
4. Check `create_pr` checkbox
5. Click "Run workflow"

**Expected Result:**
- Workflow completes successfully (no failure)
- PR creation step shows as "continued on error"
- Fallback step executes
- Changes are committed directly to main branch
- Summary shows:
  - "Create PR mode: yes"
  - "PR Creation: ⚠️ Failed (fell back to direct commit)"
  - Message: "Enable PR creation in Settings > Actions > General"
- Warning messages in logs explain the situation

### Scenario 3: PR Creation (With Repository Setting)
This tests the ideal happy path.

**To Test:**
1. Enable "Allow GitHub Actions to create and approve pull requests":
   - Go to Settings > Actions > General
   - Look for "Workflow permissions" section
   - Check "Allow GitHub Actions to create and approve pull requests"
   - Click "Save"
2. Go to Actions > AI Instructions Sync
3. Click "Run workflow"
4. Check `create_pr` checkbox
5. Click "Run workflow"

**Expected Result:**
- Workflow completes successfully
- PR is created with title "docs: sync AI instruction repository structure"
- Branch `automation/ai-instructions-sync` is created
- Summary shows:
  - "Create PR mode: yes"
  - "PR Creation: ✅ Success"
- Fallback step does not execute

### Scenario 4: Dry Run
This tests the dry-run functionality.

**To Test:**
1. Go to Actions > AI Instructions Sync
2. Click "Run workflow"
3. Check `dry_run` checkbox
4. Click "Run workflow"

**Expected Result:**
- Workflow completes successfully
- No commits or PRs are created
- Diff is displayed in logs
- Summary shows "Dry run: yes"

## Verification Checklist

After running the tests above, verify:

- [ ] Workflow never fails due to PR creation permissions
- [ ] Direct commit path works without special settings
- [ ] Fallback mechanism activates when PR creation fails
- [ ] PR creation works when repository setting is enabled
- [ ] Dry run shows changes without committing
- [ ] Summary step displays appropriate status messages
- [ ] Warning/notice messages appear in fallback scenario
- [ ] Repository structure files get updated correctly in all scenarios

## Manual Verification

After running a test, check the files were updated:

```bash
# View recent commits
git log --oneline -5

# Check if files were updated
git log --oneline -1 -- CLAUDE.md docs/ai/copilot/instructions.md .github/agents/documentation-agent.md

# View the repository structure section in CLAUDE.md
grep -A 50 "## Repository Structure" CLAUDE.md
```

## Notes

- The workflow is scheduled to run weekly on Mondays at 3 AM UTC
- Manual runs can be triggered at any time via workflow_dispatch
- The `[skip ci]` tag in commit messages prevents triggering other workflows
- Repository settings require admin access to modify

## Troubleshooting

### Issue: Workflow still fails
- Verify the workflow permissions are set to "Read and write permissions"
- Check that the GITHUB_TOKEN has not been restricted at organization level
- Ensure the workflow file syntax is valid

### Issue: Files not updated
- Check that the Python scripts in `build/scripts/docs/` are executable
- Verify required files exist (listed in "Validate AI sync prerequisites" step)
- Look for errors in the "Generate repository structure reference" step

### Issue: PR creation fails even with setting enabled
- Wait a few minutes after enabling the setting (cache refresh)
- Check organization-level settings if the repository belongs to an organization
- Verify the token has `contents: write` and `pull-requests: write` permissions
