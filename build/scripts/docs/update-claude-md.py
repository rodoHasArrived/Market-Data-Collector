#!/usr/bin/env python3
"""
Update CLAUDE.md repository structure section automatically.

This script reads the generated repository structure documentation and
updates the corresponding section in CLAUDE.md while preserving all
other content.

Usage:
    python3 update-claude-md.py --claude-md CLAUDE.md --structure-source docs/generated/repository-structure.md
"""

import argparse
import re
import sys
from datetime import datetime
from pathlib import Path


def parse_args() -> argparse.Namespace:
    """Parse command line arguments."""
    parser = argparse.ArgumentParser(
        description='Update CLAUDE.md repository structure section'
    )
    parser.add_argument(
        '--claude-md',
        type=str,
        default='CLAUDE.md',
        help='Path to CLAUDE.md file'
    )
    parser.add_argument(
        '--structure-source',
        type=str,
        default='docs/generated/repository-structure.md',
        help='Path to generated structure documentation'
    )
    parser.add_argument(
        '--dry-run',
        action='store_true',
        help='Print changes without writing to file'
    )
    parser.add_argument(
        '--section-marker',
        type=str,
        default='## Repository Structure',
        help='Section header to update'
    )
    return parser.parse_args()


def extract_structure_tree(structure_content: str) -> str:
    """Extract the directory tree from structure documentation."""
    # Find the code block containing the directory tree
    tree_pattern = re.compile(
        r'## Directory Layout\s*\n+```\n(.*?)```',
        re.DOTALL
    )
    match = tree_pattern.search(structure_content)
    if match:
        return match.group(1).strip()
    return ''


def find_section_bounds(content: str, section_header: str) -> tuple[int, int]:
    """Find the start and end positions of a markdown section."""
    # Find the section header
    header_pattern = re.compile(
        rf'^{re.escape(section_header)}\s*$',
        re.MULTILINE
    )
    header_match = header_pattern.search(content)

    if not header_match:
        return -1, -1

    start = header_match.start()

    # Find the next section header (## or higher level)
    next_section_pattern = re.compile(r'^##? [A-Z]', re.MULTILINE)
    next_match = next_section_pattern.search(content, header_match.end() + 1)

    if next_match:
        end = next_match.start()
    else:
        # Section goes to end of file
        end = len(content)

    return start, end


def generate_updated_section(tree_content: str) -> str:
    """Generate the updated Repository Structure section."""
    return f"""## Repository Structure

```
{tree_content}
```

"""


def update_claude_md(
    claude_path: Path,
    structure_path: Path,
    section_marker: str,
    dry_run: bool = False
) -> bool:
    """Update the CLAUDE.md file with new structure content."""
    # Read files
    try:
        claude_content = claude_path.read_text(encoding='utf-8')
    except FileNotFoundError:
        print(f"Error: {claude_path} not found", file=sys.stderr)
        return False

    try:
        structure_content = structure_path.read_text(encoding='utf-8')
    except FileNotFoundError:
        print(f"Warning: {structure_path} not found, skipping update")
        return True

    # Extract the tree from generated docs
    tree_content = extract_structure_tree(structure_content)
    if not tree_content:
        print("Warning: Could not extract directory tree from structure docs")
        return True

    # Find the section to replace
    start, end = find_section_bounds(claude_content, section_marker)
    if start == -1:
        print(f"Warning: Section '{section_marker}' not found in CLAUDE.md")
        return True

    # Generate new section
    new_section = generate_updated_section(tree_content)

    # Replace the section
    updated_content = claude_content[:start] + new_section + claude_content[end:]

    # Update the "Last Updated" date if present
    date_pattern = re.compile(r'\*Last Updated: \d{4}-\d{2}-\d{2}\*')
    if date_pattern.search(updated_content):
        today = datetime.now().strftime('%Y-%m-%d')
        updated_content = date_pattern.sub(f'*Last Updated: {today}*', updated_content)

    if dry_run:
        print("=== DRY RUN - Would update CLAUDE.md with: ===")
        print(new_section[:500] + "..." if len(new_section) > 500 else new_section)
        return True

    # Check if content actually changed
    if updated_content == claude_content:
        print("No changes needed to CLAUDE.md")
        return True

    # Write updated content
    claude_path.write_text(updated_content, encoding='utf-8')
    print(f"Updated {claude_path}")
    return True


def main() -> int:
    """Main entry point."""
    args = parse_args()

    claude_path = Path(args.claude_md)
    structure_path = Path(args.structure_source)

    success = update_claude_md(
        claude_path,
        structure_path,
        args.section_marker,
        args.dry_run
    )

    return 0 if success else 1


if __name__ == '__main__':
    sys.exit(main())
