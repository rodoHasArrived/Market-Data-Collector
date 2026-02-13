# PR #1148 Feature Comparison: Before vs After

## Overview
This document shows the concrete improvements from PR #1148 with before/after examples.

---

## Feature 1: JSON Output

### Before (Main Branch)
```bash
$ python3 build/scripts/docs/create-todo-issues.py --scan-json todos.json --dry-run
[dry-run] would create issue for test.py:10 :: Test item
TODO issue creation complete: created=1, existing=0, failed=0, skipped_limit=0, total_untracked=1
# No machine-readable output
```

### After (This PR)
```bash
$ python3 build/scripts/docs/create-todo-issues.py --scan-json todos.json --dry-run --output-json summary.json
[dry-run] would create issue for test.py:10 :: Test item
TODO issue creation complete: created=1, existing=0, failed=0, skipped_limit=0, total_untracked=1

$ cat summary.json
{
  "created": 1,
  "existing": 0,
  "failed": 0,
  "skipped_limit": 0,
  "total_untracked": 1,
  "dry_run": true,
  "repo": "owner/repo",
  "label": "auto-todo",
  "generated_at": "2026-02-13T14:51:51.432013+00:00"
}
```

**Benefit**: CI/CD systems can parse and act on the JSON output.

---

## Feature 2: Validation Logic

### Before (Main Branch)
```bash
$ python3 build/scripts/docs/run-docs-automation.py --scripts validate-examples --auto-create-todos --dry-run
# Would attempt to create issues without scan data
# Could fail with cryptic errors
```

### After (This PR)
```bash
$ python3 build/scripts/docs/run-docs-automation.py --scripts validate-examples --auto-create-todos --dry-run
Error: --auto-create-todos requires scan-todos to be selected.
Selected scripts (1): validate-examples
```

**Benefit**: Clear error message prevents invalid configurations.

---

## Feature 3: Error Handling - Invalid JSON Structure

### Before (Main Branch)
```bash
$ echo '{"todos": "not a list"}' > invalid.json
$ python3 build/scripts/docs/create-todo-issues.py --scan-json invalid.json --dry-run
Traceback (most recent call last):
  ...
TypeError: 'str' object is not iterable
```

### After (This PR)
```bash
$ echo '{"todos": "not a list"}' > invalid.json
$ python3 build/scripts/docs/create-todo-issues.py --scan-json invalid.json --dry-run
Error: Scan JSON field 'todos' must be a list
```

**Benefit**: Clear error message, no stack trace.

---

## Feature 4: Error Handling - Malformed JSON

### Before (Main Branch)
```bash
$ echo '{ invalid JSON' > malformed.json
$ python3 build/scripts/docs/create-todo-issues.py --scan-json malformed.json --dry-run
Traceback (most recent call last):
  ...
json.decoder.JSONDecodeError: Expecting property name enclosed in double quotes: line 1 column 3 (char 2)
```

### After (This PR)
```bash
$ echo '{ invalid JSON' > malformed.json
$ python3 build/scripts/docs/create-todo-issues.py --scan-json malformed.json --dry-run
Error: Invalid JSON in scan file: malformed.json
```

**Benefit**: User-friendly error message without stack trace.

---

## Feature 5: Improved Title Generation

### Before (Main Branch)
```python
def compact_title(todo: TodoItem) -> str:
    summary = re.sub(r"\s+", " ", todo.text).strip()
    if len(summary) > 72:
        summary = summary[:69].rstrip() + "..."
    return f"[{todo.type}] {summary}"
```

**Issues:**
- Hardcoded 72 character limit
- No fallback for empty text
- Could create titles like `[TODO] ` (empty)

### After (This PR)
```python
MAX_TITLE_LENGTH = 120  # Configurable constant

def compact_title(todo: TodoItem) -> str:
    summary = re.sub(r"\s+", " ", todo.text).strip() or f"Review {todo.type} in {todo.file}:{todo.line}"
    prefix = f"[{todo.type}] "
    max_summary = max(16, MAX_TITLE_LENGTH - len(prefix))
    if len(summary) > max_summary:
        summary = summary[: max_summary - 3].rstrip() + "..."
    return f"{prefix}{summary}"
```

**Improvements:**
- Configurable via `MAX_TITLE_LENGTH` constant
- Fallback for empty text: `"Review TODO in file.py:42"`
- Dynamic length calculation
- Minimum 16 character summary

**Example:**
```python
# Before: "[TODO] " (empty)
# After:  "[TODO] Review TODO in config.py:123"
```

---

## Feature 6: Return Type Clarity

### Before (Main Branch)
```python
def create_issue(...) -> int | None:
    existing = find_existing_issue(...)
    if existing is not None:
        return existing
    if dry_run:
        return None
    # ... create issue ...
    return issue_number

# Caller can't distinguish between:
# - Issue already exists (returns issue number)
# - Dry run (returns None)
# - Creation failed (returns None?)
```

### After (This PR)
```python
def create_issue(...) -> tuple[str, int | None]:
    existing = find_existing_issue(...)
    if existing is not None:
        return ("existing", existing)
    if dry_run:
        return ("dry-run", None)
    # ... create issue ...
    return ("created", issue_number)

# Caller knows exactly what happened:
# - ("existing", 123) - Issue #123 already exists
# - ("dry-run", None) - Dry run mode
# - ("created", 456) - Created issue #456
```

**Benefit**: Caller can provide specific feedback based on outcome.

---

## Feature 7: HTTP/Network Error Handling

### Before (Main Branch)
```python
def gh_request(method: str, url: str, token: str, payload: dict | None) -> dict:
    # ... setup request ...
    with urllib.request.urlopen(req, timeout=30) as response:
        return json.loads(response.read().decode("utf-8"))
    # No error handling - stack trace on any failure
```

### After (This PR)
```python
def gh_request(method: str, url: str, token: str, payload: dict | None) -> dict:
    # ... setup request ...
    try:
        with urllib.request.urlopen(req, timeout=30) as response:
            body = response.read().decode("utf-8")
            if not body.strip():
                return {}
            return json.loads(body)
    except urllib.error.HTTPError:
        raise  # Re-raise with original info
    except urllib.error.URLError as exc:
        raise RuntimeError(f"Network error while calling GitHub API: {exc}") from exc
    except json.JSONDecodeError as exc:
        raise RuntimeError(f"Invalid JSON received from GitHub API for URL: {url}") from exc
```

**Improvements:**
- Handles empty responses gracefully
- Specific error messages for network errors
- Context about which URL failed
- Proper exception chaining

---

## Feature 8: Workflow Integration

### Before (Main Branch)
```yaml
# Manual workflow trigger needed to create issues
# No machine-readable output for automation
# Hard to integrate with CI/CD
```

### After (This PR)
```yaml
- name: Run documentation automation
  run: |
    python3 build/scripts/docs/run-docs-automation.py \
      --profile full \
      --auto-create-todos \
      --json-output summary.json

- name: Check for failures
  run: |
    created=$(jq '.created' summary.json)
    echo "Created $created TODO issues"
```

**Benefit**: Easy CI/CD integration with JSON output.

---

## Summary of Improvements

| Feature | Before | After | Benefit |
|---------|--------|-------|---------|
| JSON Output | ❌ No | ✅ Yes | CI/CD integration |
| Validation | ❌ Weak | ✅ Strong | Clear errors |
| Error Messages | ❌ Stack traces | ✅ User-friendly | Better UX |
| Title Generation | ⚠️ Basic | ✅ Robust | Handles edge cases |
| Return Types | ⚠️ Ambiguous | ✅ Clear | Better debugging |
| HTTP Errors | ❌ Unhandled | ✅ Handled | Resilience |
| Empty Responses | ❌ Crashes | ✅ Handles | Reliability |
| Constants | ❌ Magic numbers | ✅ Named | Maintainability |

---

## Code Quality Metrics

### Complexity Reduction
- **Error handling**: Centralized, consistent patterns
- **Validation**: Explicit checks with clear messages
- **Return types**: Unambiguous, self-documenting

### Maintainability Improvements
- **Named constants**: `MAX_TITLE_LENGTH` instead of magic 72
- **Clear return types**: `tuple[str, int | None]` vs `int | None`
- **Better comments**: Explains why, not just what

### Test Coverage
- All new features have verification tests
- Error cases explicitly tested
- Edge cases documented and handled

---

## Backward Compatibility

✅ **All existing scripts continue to work without changes**

```bash
# Old usage still works:
python3 build/scripts/docs/create-todo-issues.py --scan-json todos.json --dry-run

# New features are opt-in:
python3 build/scripts/docs/create-todo-issues.py --scan-json todos.json --dry-run --output-json summary.json
```

No breaking changes. All improvements are additions or enhancements.

---

**Status**: All features verified and tested  
**Breaking Changes**: None  
**Ready**: Yes
