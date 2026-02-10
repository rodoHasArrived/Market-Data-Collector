# Expanding Documentation Scripts

> Developer guide for adding new documentation automation scripts.

## Architecture

All documentation scripts live in `build/scripts/docs/` and follow a consistent pattern:

```
build/scripts/docs/
  scan-todos.py                 # TODO scanning
  generate-structure-docs.py    # Repo structure generation
  update-claude-md.py           # AI instruction sync
  generate-health-dashboard.py  # Health metrics
  repair-links.py               # Link validation/repair
  validate-examples.py          # Code example validation
  generate-coverage.py          # Doc coverage analysis
  generate-changelog.py         # Changelog from commits
  rules-engine.py               # Custom rules validation
```

## Script Template

New scripts should follow this pattern:

```python
#!/usr/bin/env python3
"""
Brief description of what this script does.

Detailed description with usage examples.

Usage:
    python3 my-new-script.py --output report.md
"""

import argparse
import sys
from datetime import datetime, timezone
from pathlib import Path


# Constants
EXCLUDE_DIRS = {
    '.git', 'node_modules', 'bin', 'obj', '__pycache__',
    '.vs', '.vscode', '.idea', 'packages', 'TestResults',
    'artifacts', 'publish'
}


def parse_args() -> argparse.Namespace:
    """Parse command line arguments."""
    parser = argparse.ArgumentParser(
        description='Description of the script'
    )
    parser.add_argument(
        '--output', '-o',
        type=Path,
        help='Output file path for the report'
    )
    parser.add_argument(
        '--root',
        type=Path,
        default=Path('.'),
        help='Repository root directory (default: current directory)'
    )
    parser.add_argument(
        '--summary',
        action='store_true',
        help='Print summary to stdout (for GITHUB_STEP_SUMMARY)'
    )
    return parser.parse_args()


def should_skip(path: Path) -> bool:
    """Check if a path should be excluded."""
    return any(part in EXCLUDE_DIRS for part in path.parts)


def format_timestamp() -> str:
    """Return a UTC timestamp for generated docs."""
    return datetime.now(timezone.utc).strftime('%Y-%m-%d %H:%M:%S UTC')


def main() -> int:
    """Main entry point."""
    args = parse_args()
    root = args.root.resolve()

    try:
        # Your logic here
        result = "# Report\n\n> Auto-generated\n"

        # Write output
        if args.output:
            args.output.parent.mkdir(parents=True, exist_ok=True)
            args.output.write_text(result, encoding='utf-8')
            print(f"Generated: {args.output}")

        # Print summary for CI
        if args.summary:
            print("Summary line for GITHUB_STEP_SUMMARY")

        return 0

    except Exception as e:
        print(f"Error: {e}", file=sys.stderr)
        return 1


if __name__ == '__main__':
    sys.exit(main())
```

## Conventions

### Required

- Shebang line: `#!/usr/bin/env python3`
- Module docstring with usage examples
- Type hints on all functions
- `argparse` CLI with `--help` support
- `--output` for file output, `--summary` for CI summary
- `--root` for repo root (default: `.`)
- Error handling that doesn't crash on missing files
- Only use stdlib (no pip dependencies)
- Exclude standard directories (`.git`, `bin`, `obj`, etc.)
- Return 0 on success, 1 on error

### Recommended

- `--json-output` for machine-readable output
- Auto-generated notice in markdown output
- Timestamp in output headers
- Graceful handling of encoding errors (`errors='replace'`)
- Logging to stderr, summaries to stdout

## Integrating with the Workflow

### Step 1: Add the script to `build/scripts/docs/`

Follow the template above and test locally:

```bash
python3 build/scripts/docs/your-script.py --help
python3 build/scripts/docs/your-script.py --output /tmp/test.md --summary
```

### Step 2: Add a workflow job

Add a new job to `.github/workflows/documentation.yml`:

```yaml
  your-new-job:
    name: Your New Feature
    needs: [detect-changes]
    if: |
      needs.detect-changes.outputs.any_changed == 'true' &&
      github.event_name != 'issues'
    runs-on: ubuntu-latest
    timeout-minutes: 15
    steps:
      - name: Checkout repository
        uses: actions/checkout@v4

      - name: Set up Python
        uses: actions/setup-python@v6.2.0
        with:
          python-version: ${{ env.PYTHON_VERSION }}

      - name: Run your script
        continue-on-error: true
        run: |
          set -euo pipefail
          python3 build/scripts/docs/your-script.py \
            --output docs/status/your-report.md \
            --summary >> "$GITHUB_STEP_SUMMARY" 2>&1
```

### Step 3: Add to the report job

Add the new job to the `report` job's `needs` list and add a result row.

### Step 4: Update documentation

Update `docs/guides/documentation-automation.md` with your new script's details.

## Testing

### Local testing

```bash
# Test all scripts parse arguments
for script in build/scripts/docs/*.py; do
    python3 "$script" --help > /dev/null 2>&1 && echo "OK: $script" || echo "FAIL: $script"
done

# Test execution
python3 build/scripts/docs/your-script.py --output /tmp/test.md
cat /tmp/test.md
```

### CI testing

Use `workflow_dispatch` with `dry_run=true` to test without committing changes.

---

*This guide is part of the documentation automation system.*
