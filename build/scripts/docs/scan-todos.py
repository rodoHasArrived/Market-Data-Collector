#!/usr/bin/env python3
"""
TODO Scanner and Documentation Generator

Scans the codebase for TODO/FIXME/HACK/NOTE comments and generates
documentation tracking all items.

Features:
- Supports C#, F#, Python, YAML, Markdown, and other common formats
- Extracts context around TODO comments
- Detects GitHub issue references (e.g., #123, issue #123)
- Categorizes by type, file, and priority
- Generates Markdown and JSON output

Usage:
    python3 scan-todos.py --output docs/status/TODO.md --format markdown
    python3 scan-todos.py --json-output results.json
"""

import argparse
import json
import os
import re
import subprocess
import sys
from dataclasses import dataclass, field, asdict
from datetime import datetime, timezone
from pathlib import Path
from typing import Generator


# Supported file extensions and their comment patterns
COMMENT_PATTERNS = {
    # C-style comments (C#, Java, JavaScript, TypeScript, etc.)
    '.cs': [r'//\s*', r'/\*\s*', r'\*\s*'],
    '.fs': [r'//\s*', r'\(\*\s*'],
    '.fsx': [r'//\s*', r'\(\*\s*'],
    '.js': [r'//\s*', r'/\*\s*', r'\*\s*'],
    '.ts': [r'//\s*', r'/\*\s*', r'\*\s*'],
    '.tsx': [r'//\s*', r'/\*\s*', r'\*\s*'],
    '.jsx': [r'//\s*', r'/\*\s*', r'\*\s*'],
    '.java': [r'//\s*', r'/\*\s*', r'\*\s*'],
    '.go': [r'//\s*', r'/\*\s*', r'\*\s*'],
    '.rs': [r'//\s*', r'/\*\s*', r'\*\s*'],
    '.cpp': [r'//\s*', r'/\*\s*', r'\*\s*'],
    '.c': [r'//\s*', r'/\*\s*', r'\*\s*'],
    '.h': [r'//\s*', r'/\*\s*', r'\*\s*'],
    '.hpp': [r'//\s*', r'/\*\s*', r'\*\s*'],
    # Python-style comments
    '.py': [r'#\s*'],
    '.pyx': [r'#\s*'],
    # Shell/Config comments
    '.sh': [r'#\s*'],
    '.bash': [r'#\s*'],
    '.zsh': [r'#\s*'],
    '.yml': [r'#\s*'],
    '.yaml': [r'#\s*'],
    '.toml': [r'#\s*'],
    # Web/Markup
    '.html': [r'<!--\s*', r'-->\s*'],
    '.xml': [r'<!--\s*', r'-->\s*'],
    '.xaml': [r'<!--\s*', r'-->\s*'],
    '.md': [r'<!--\s*'],
    # SQL
    '.sql': [r'--\s*', r'/\*\s*', r'\*\s*'],
    # PowerShell
    '.ps1': [r'#\s*'],
    '.psm1': [r'#\s*'],
}

# TODO types to scan for
TODO_TYPES = ['TODO', 'FIXME', 'HACK', 'BUG', 'XXX', 'PERF', 'OPTIMIZE', 'REFACTOR']
NOTE_TYPE = 'NOTE'

# Directories to exclude
EXCLUDE_DIRS = {
    '.git', 'node_modules', 'bin', 'obj', 'packages', '.vs', '.vscode',
    '.idea', '__pycache__', 'dist', 'build', 'coverage', 'TestResults',
    '.nuget', 'artifacts', 'publish', 'wwwroot/lib'
}

# Files to exclude
EXCLUDE_FILES = {
    'package-lock.json', 'yarn.lock', '.gitignore', '.dockerignore',
    'todo-automation.yml',  # Exclude the TODO workflow itself to avoid false positives
    'scan-todos.py'  # Exclude this scanner script
}

# Issue reference patterns
ISSUE_PATTERNS = [
    r'#(\d+)',                      # #123
    r'issue\s*#?(\d+)',             # issue #123, issue 123
    r'issues/(\d+)',                # issues/123
    r'github\.com/.+/issues/(\d+)', # full GitHub URL
    r'Track with issue #(\d+)',     # Track with issue #123
]


@dataclass
class TodoItem:
    """Represents a TODO item found in the codebase."""
    type: str
    text: str
    file: str
    line: int
    context: str = ""
    issue_refs: list = field(default_factory=list)
    has_issue: bool = False
    priority: str = "normal"
    assignee: str = ""
    age_days: int = 0
    last_modified: str = ""

    def to_dict(self):
        return asdict(self)


@dataclass
class ScanResults:
    """Results of the TODO scan."""
    todos: list
    total_count: int
    by_type: dict
    by_file: dict
    scan_time: str
    include_notes: bool

    def to_dict(self):
        return {
            'todos': [t.to_dict() for t in self.todos],
            'total_count': self.total_count,
            'by_type': self.by_type,
            'by_file': self.by_file,
            'scan_time': self.scan_time,
            'include_notes': self.include_notes
        }


def should_skip_path(path: Path) -> bool:
    """Check if path should be skipped."""
    parts = path.parts
    for part in parts:
        if part in EXCLUDE_DIRS:
            return True
    if path.name in EXCLUDE_FILES:
        return True
    return False


def get_comment_patterns(ext: str) -> list:
    """Get comment patterns for a file extension."""
    return COMMENT_PATTERNS.get(ext, [r'//\s*', r'#\s*'])


def extract_issue_refs(text: str) -> list:
    """Extract GitHub issue references from text."""
    refs = []
    for pattern in ISSUE_PATTERNS:
        matches = re.findall(pattern, text, re.IGNORECASE)
        refs.extend(matches)
    return list(set(refs))


def extract_assignee(text: str) -> str:
    """Extract assignee from TODO comment (e.g., @username)."""
    match = re.search(r'@([a-zA-Z0-9_-]+)', text)
    if match:
        return match.group(1)
    return ""


def get_file_last_modified_date(file_path: Path, root: Path) -> tuple[str, int]:
    """Get last modified date of file using git."""
    try:
        result = subprocess.run(
            ['git', 'log', '-1', '--format=%aI', '--', str(file_path)],
            capture_output=True,
            text=True,
            cwd=str(root),
            timeout=5
        )
        if result.returncode == 0 and result.stdout.strip():
            date_str = result.stdout.strip()
            # Parse ISO date and calculate age
            from datetime import datetime
            date = datetime.fromisoformat(date_str.replace('Z', '+00:00'))
            age_days = (datetime.now(date.astimezone().tzinfo) - date).days
            return date_str[:10], age_days
    except Exception:
        pass
    
    return "", 0


def determine_priority(text: str, todo_type: str) -> str:
    """Determine priority based on content and type."""
    text_lower = text.lower()

    # High priority indicators
    if todo_type in ['FIXME', 'BUG']:
        return 'high'
    if any(word in text_lower for word in ['critical', 'urgent', 'important', 'security', 'breaking']):
        return 'high'

    # Low priority indicators
    if any(word in text_lower for word in ['minor', 'cleanup', 'nice to have', 'eventually', 'someday']):
        return 'low'

    return 'normal'


def extract_context(lines: list, line_num: int, context_lines: int = 2) -> str:
    """Extract context lines around the TODO."""
    start = max(0, line_num - context_lines)
    end = min(len(lines), line_num + context_lines + 1)

    context_parts = []
    for i in range(start, end):
        prefix = ">>> " if i == line_num else "    "
        context_parts.append(f"{prefix}{i + 1}: {lines[i].rstrip()}")

    return "\n".join(context_parts)


def scan_file(file_path: Path, include_notes: bool, root: Path) -> Generator[TodoItem, None, None]:
    """Scan a single file for TODO items."""
    ext = file_path.suffix.lower()
    comment_patterns = get_comment_patterns(ext)

    types_to_find = TODO_TYPES + ([NOTE_TYPE] if include_notes else [])
    
    # Get file modification info once for all TODOs in this file
    last_modified, age_days = get_file_last_modified_date(file_path, root)

    # Build regex pattern
    types_pattern = '|'.join(types_to_find)

    try:
        content = file_path.read_text(encoding='utf-8', errors='replace')
        lines = content.split('\n')
    except Exception as e:
        print(f"Warning: Could not read {file_path}: {e}", file=sys.stderr)
        return

    for line_num, line in enumerate(lines):
        # Skip lines that look like they're inside strings (echo, print statements)
        # This avoids false positives from shell scripts printing "TODO" in output
        stripped = line.strip()
        if any(pattern in stripped.lower() for pattern in ['echo "', "echo '", 'print(', 'console.log']):
            if '"' in stripped or "'" in stripped:
                # Likely a string containing TODO, not a TODO comment
                continue

        for comment_prefix in comment_patterns:
            # Match TODO pattern after comment prefix at the start of comment
            # The comment prefix should be followed immediately by the TODO type
            pattern = rf'^\s*{comment_prefix}({types_pattern})[\s:]+(.+?)$'
            match = re.search(pattern, line, re.IGNORECASE)

            if match:
                todo_type = match.group(1).upper()
                text = match.group(2).strip()

                # Check for multiline continuation
                full_text = text
                next_line = line_num + 1
                while next_line < len(lines):
                    next_content = lines[next_line].strip()
                    # Check if next line is a continuation (starts with comment but not a new TODO)
                    is_continuation = False
                    for cp in comment_patterns:
                        cont_match = re.match(rf'{cp}(?!({types_pattern})[\s:])', next_content)
                        if cont_match:
                            # Extract text after comment prefix
                            cont_text = re.sub(rf'^{cp}', '', next_content).strip()
                            if cont_text and not cont_text.startswith(tuple(types_to_find)):
                                full_text += " " + cont_text
                                is_continuation = True
                                break
                    if not is_continuation:
                        break
                    next_line += 1

                # Extract issue references and assignee
                issue_refs = extract_issue_refs(full_text)
                assignee = extract_assignee(full_text)

                # Get context
                context = extract_context(lines, line_num)

                # Get relative path
                try:
                    rel_path = file_path.relative_to(Path.cwd())
                except ValueError:
                    rel_path = file_path

                yield TodoItem(
                    type=todo_type,
                    text=full_text,
                    file=str(rel_path),
                    line=line_num + 1,
                    context=context,
                    issue_refs=issue_refs,
                    has_issue=len(issue_refs) > 0,
                    priority=determine_priority(full_text, todo_type),
                    assignee=assignee,
                    age_days=age_days,
                    last_modified=last_modified
                )
                break  # Don't match same line with multiple patterns


def scan_directory(root: Path, include_notes: bool) -> ScanResults:
    """Scan directory recursively for TODO items."""
    todos = []

    for ext in COMMENT_PATTERNS.keys():
        for file_path in root.rglob(f'*{ext}'):
            if should_skip_path(file_path):
                continue

            for todo in scan_file(file_path, include_notes, root):
                todos.append(todo)

    # Calculate statistics
    by_type = {}
    by_file = {}

    for todo in todos:
        by_type[todo.type] = by_type.get(todo.type, 0) + 1

        # Group by top-level directory
        parts = Path(todo.file).parts
        top_dir = parts[0] if parts else 'root'
        by_file[top_dir] = by_file.get(top_dir, 0) + 1

    return ScanResults(
        todos=todos,
        total_count=len(todos),
        by_type=by_type,
        by_file=by_file,
        scan_time=datetime.now(timezone.utc).isoformat(),
        include_notes=include_notes
    )


def generate_markdown(results: ScanResults) -> str:
    """Generate Markdown documentation from scan results."""
    lines = []

    lines.append("# TODO Tracking")
    lines.append("")
    lines.append("> Auto-generated TODO documentation. Do not edit manually.")
    lines.append(f"> Last updated: {results.scan_time}")
    lines.append("")

    # Summary
    lines.append("## Summary")
    lines.append("")
    lines.append(f"| Metric | Count |")
    lines.append("|--------|-------|")
    lines.append(f"| **Total Items** | {results.total_count} |")
    lines.append(f"| **Linked to Issues** | {sum(1 for t in results.todos if t.has_issue)} |")
    lines.append(f"| **Untracked** | {sum(1 for t in results.todos if not t.has_issue)} |")
    lines.append("")

    # By Type
    if results.by_type:
        lines.append("### By Type")
        lines.append("")
        lines.append("| Type | Count | Description |")
        lines.append("|------|-------|-------------|")

        type_descriptions = {
            'TODO': 'General tasks to complete',
            'FIXME': 'Known bugs or issues to fix',
            'HACK': 'Temporary workarounds needing proper solutions',
            'BUG': 'Identified bugs',
            'XXX': 'Areas needing attention',
            'PERF': 'Performance improvements needed',
            'OPTIMIZE': 'Optimization opportunities',
            'REFACTOR': 'Code needing restructuring',
            'NOTE': 'Important notes and documentation'
        }

        for todo_type, count in sorted(results.by_type.items(), key=lambda x: -x[1]):
            desc = type_descriptions.get(todo_type, '')
            lines.append(f"| `{todo_type}` | {count} | {desc} |")
        lines.append("")

    # By Directory
    if results.by_file:
        lines.append("### By Directory")
        lines.append("")
        lines.append("| Directory | Count |")
        lines.append("|-----------|-------|")
        for dir_name, count in sorted(results.by_file.items(), key=lambda x: -x[1]):
            lines.append(f"| `{dir_name}/` | {count} |")
        lines.append("")

    # Priority sections
    high_priority = [t for t in results.todos if t.priority == 'high']
    if high_priority:
        lines.append("## High Priority")
        lines.append("")
        lines.append("Items requiring immediate attention:")
        lines.append("")
        for todo in high_priority:
            issue_link = f" (#{todo.issue_refs[0]})" if todo.issue_refs else ""
            assignee_str = f" @{todo.assignee}" if todo.assignee else ""
            age_str = f" ({todo.age_days}d old)" if todo.age_days > 0 else ""
            lines.append(f"- **[{todo.type}]** `{todo.file}:{todo.line}`{issue_link}{assignee_str}{age_str}")
            lines.append(f"  - {todo.text}")
        lines.append("")
    
    # Stale TODOs (older than 90 days)
    stale_todos = [t for t in results.todos if t.age_days > 90]
    if stale_todos:
        lines.append("## Stale Items (>90 days)")
        lines.append("")
        lines.append("These items have been in the codebase for over 90 days:")
        lines.append("")
        for todo in sorted(stale_todos, key=lambda x: -x.age_days)[:20]:
            issue_link = f" (#{todo.issue_refs[0]})" if todo.issue_refs else ""
            assignee_str = f" @{todo.assignee}" if todo.assignee else ""
            lines.append(f"- **[{todo.type}]** `{todo.file}:{todo.line}` - {todo.age_days} days old{issue_link}{assignee_str}")
            lines.append(f"  - {todo.text[:100]}{'...' if len(todo.text) > 100 else ''}")
        if len(stale_todos) > 20:
            lines.append(f"- ... and {len(stale_todos) - 20} more stale items")
        lines.append("")
    
    # Unassigned TODOs
    unassigned = [t for t in results.todos if not t.assignee and not t.has_issue]
    if unassigned and len(unassigned) > 10:
        lines.append("## Unassigned & Untracked")
        lines.append("")
        lines.append(f"{len(unassigned)} items have no assignee and no issue tracking:")
        lines.append("")
        lines.append("Consider assigning ownership or creating tracking issues for these items.")
        lines.append("")

    # Detailed listing by type
    lines.append("## All Items")
    lines.append("")

    # Group by type
    todos_by_type = {}
    for todo in results.todos:
        if todo.type not in todos_by_type:
            todos_by_type[todo.type] = []
        todos_by_type[todo.type].append(todo)

    for todo_type in ['FIXME', 'BUG', 'TODO', 'HACK', 'PERF', 'OPTIMIZE', 'REFACTOR', 'XXX', 'NOTE']:
        if todo_type not in todos_by_type:
            continue

        items = todos_by_type[todo_type]
        lines.append(f"### {todo_type} ({len(items)})")
        lines.append("")

        for todo in sorted(items, key=lambda x: (not x.has_issue, x.file)):
            # Status indicator
            status = "[x]" if todo.has_issue else "[ ]"
            issue_link = f" [#{todo.issue_refs[0]}]" if todo.issue_refs else ""

            lines.append(f"- {status} `{todo.file}:{todo.line}`{issue_link}")
            lines.append(f"  > {todo.text}")
            lines.append("")

    # Footer
    lines.append("---")
    lines.append("")
    lines.append("## Contributing")
    lines.append("")
    lines.append("When adding TODO comments, please follow these guidelines:")
    lines.append("")
    lines.append("1. **Link to GitHub Issues**: Use `// TODO: Track with issue #123` format")
    lines.append("2. **Be Descriptive**: Explain what needs to be done and why")
    lines.append("3. **Use Correct Type**:")
    lines.append("   - `TODO` - General tasks")
    lines.append("   - `FIXME` - Bugs that need fixing")
    lines.append("   - `HACK` - Temporary workarounds")
    lines.append("   - `NOTE` - Important information")
    lines.append("")
    lines.append("Example:")
    lines.append("```csharp")
    lines.append("// TODO: Track with issue #123 - Implement retry logic for transient failures")
    lines.append("// This is needed because the API occasionally returns 503 errors during peak load.")
    lines.append("```")
    lines.append("")

    return "\n".join(lines)


def main():
    parser = argparse.ArgumentParser(
        description='Scan codebase for TODO comments and generate documentation'
    )
    parser.add_argument(
        '--root', '-r',
        type=Path,
        default=Path.cwd(),
        help='Root directory to scan (default: current directory)'
    )
    parser.add_argument(
        '--output', '-o',
        type=Path,
        help='Output file for Markdown documentation'
    )
    parser.add_argument(
        '--json-output', '-j',
        type=Path,
        help='Output file for JSON results'
    )
    parser.add_argument(
        '--format', '-f',
        choices=['markdown', 'json', 'both'],
        default='both',
        help='Output format (default: both)'
    )
    parser.add_argument(
        '--include-notes',
        type=lambda x: x.lower() in ('true', '1', 'yes'),
        default=True,
        help='Include NOTE comments (default: true)'
    )
    parser.add_argument(
        '--verbose', '-v',
        action='store_true',
        help='Verbose output'
    )

    args = parser.parse_args()

    if args.verbose:
        print(f"Scanning {args.root} for TODO comments...")
        print(f"Include NOTEs: {args.include_notes}")

    # Scan the codebase
    results = scan_directory(args.root, args.include_notes)

    if args.verbose:
        print(f"Found {results.total_count} TODO items")
        for todo_type, count in results.by_type.items():
            print(f"  - {todo_type}: {count}")

    # Generate Markdown output
    if args.output or args.format in ['markdown', 'both']:
        md_content = generate_markdown(results)

        output_path = args.output or Path('docs/status/TODO.md')
        output_path.parent.mkdir(parents=True, exist_ok=True)
        output_path.write_text(md_content, encoding='utf-8')

        if args.verbose:
            print(f"Markdown written to {output_path}")

    # Generate JSON output
    if args.json_output or args.format in ['json', 'both']:
        json_path = args.json_output or Path('todo-scan-results.json')
        json_path.write_text(
            json.dumps(results.to_dict(), indent=2),
            encoding='utf-8'
        )

        if args.verbose:
            print(f"JSON written to {json_path}")

    # Print summary to stdout
    print(f"TODO Scan Complete: {results.total_count} items found")
    print(f"  - Linked to issues: {sum(1 for t in results.todos if t.has_issue)}")
    print(f"  - Untracked: {sum(1 for t in results.todos if not t.has_issue)}")

    return 0


if __name__ == '__main__':
    sys.exit(main())
