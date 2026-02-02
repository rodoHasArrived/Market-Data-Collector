#!/usr/bin/env python3
"""
Generate structure documentation for the Market Data Collector repository.

This script analyzes the repository structure and generates documentation files
that can be used to keep CLAUDE.md, README.md, and other docs up to date.

Usage:
    python3 generate-structure-docs.py --output docs/generated/repository-structure.md
    python3 generate-structure-docs.py --providers-only --output docs/generated/provider-registry.md
    python3 generate-structure-docs.py --workflows-only --output docs/generated/workflows-overview.md
"""

import argparse
import json
import os
import re
import sys
from datetime import datetime
from pathlib import Path
from typing import Any


# Directories to exclude from structure generation
EXCLUDE_DIRS = {
    '.git', 'node_modules', 'bin', 'obj', '.vs', '.vscode',
    '__pycache__', '.idea', 'packages', 'TestResults',
    'publish', 'artifacts', '.nuget'
}

# File patterns to exclude
EXCLUDE_PATTERNS = {
    '*.pyc', '*.pyo', '*.dll', '*.exe', '*.pdb',
    '.DS_Store', 'Thumbs.db', '*.user', '*.suo'
}

# Important directories to highlight
IMPORTANT_DIRS = {
    'src': 'Source code',
    'docs': 'Documentation',
    'tests': 'Test projects',
    'scripts': 'Automation scripts',
    'tools': 'Development tools',
    '.github': 'GitHub configuration',
    'config': 'Configuration files',
    'deploy': 'Deployment configurations',
    'benchmarks': 'Performance benchmarks',
    'build-system': 'Build tooling'
}


def parse_args() -> argparse.Namespace:
    """Parse command line arguments."""
    parser = argparse.ArgumentParser(
        description='Generate repository structure documentation'
    )
    parser.add_argument(
        '--output', '-o',
        type=str,
        default='docs/generated/repository-structure.md',
        help='Output file path'
    )
    parser.add_argument(
        '--format', '-f',
        choices=['markdown', 'json', 'tree'],
        default='markdown',
        help='Output format'
    )
    parser.add_argument(
        '--providers-only',
        action='store_true',
        help='Only generate provider registry documentation'
    )
    parser.add_argument(
        '--workflows-only',
        action='store_true',
        help='Only generate workflows documentation'
    )
    parser.add_argument(
        '--extract-attributes',
        action='store_true',
        help='Extract DataSource and ImplementsAdr attributes from code'
    )
    parser.add_argument(
        '--max-depth',
        type=int,
        default=4,
        help='Maximum directory depth to traverse'
    )
    parser.add_argument(
        '--root',
        type=str,
        default='.',
        help='Repository root directory'
    )
    return parser.parse_args()


def should_exclude(path: Path) -> bool:
    """Check if a path should be excluded from processing."""
    if path.name in EXCLUDE_DIRS:
        return True
    for pattern in EXCLUDE_PATTERNS:
        if path.match(pattern):
            return True
    return False


def get_directory_tree(root: Path, max_depth: int = 4, current_depth: int = 0) -> dict[str, Any]:
    """Build a tree structure of the repository."""
    tree: dict[str, Any] = {
        'name': root.name or str(root),
        'type': 'directory',
        'children': [],
        'description': IMPORTANT_DIRS.get(root.name, '')
    }

    if current_depth >= max_depth:
        tree['truncated'] = True
        return tree

    try:
        items = sorted(root.iterdir(), key=lambda x: (x.is_file(), x.name.lower()))
    except PermissionError:
        return tree

    for item in items:
        if should_exclude(item):
            continue

        if item.is_dir():
            child = get_directory_tree(item, max_depth, current_depth + 1)
            tree['children'].append(child)
        elif item.is_file():
            tree['children'].append({
                'name': item.name,
                'type': 'file',
                'extension': item.suffix
            })

    return tree


def tree_to_markdown(tree: dict[str, Any], indent: int = 0, is_last: bool = True, prefix: str = '') -> str:
    """Convert tree structure to markdown format."""
    lines = []
    name = tree['name']
    description = tree.get('description', '')

    # Build the line prefix for tree-style output
    if indent == 0:
        line = f"{name}/"
    else:
        connector = '└── ' if is_last else '├── '
        if tree['type'] == 'directory':
            line = f"{prefix}{connector}{name}/"
        else:
            line = f"{prefix}{connector}{name}"

    # Add description for important directories
    if description and tree['type'] == 'directory':
        line += f"  # {description}"

    lines.append(line)

    # Process children
    children = tree.get('children', [])
    for i, child in enumerate(children):
        is_last_child = (i == len(children) - 1)
        if indent == 0:
            child_prefix = ''
        else:
            child_prefix = prefix + ('    ' if is_last else '│   ')

        lines.append(tree_to_markdown(child, indent + 1, is_last_child, child_prefix))

    if tree.get('truncated'):
        connector = '    ' if is_last else '│   '
        lines.append(f"{prefix}{connector}...")

    return '\n'.join(lines)


def generate_structure_markdown(root: Path, max_depth: int = 4) -> str:
    """Generate repository structure in markdown format."""
    tree = get_directory_tree(root, max_depth)

    content = f"""# Repository Structure

> Auto-generated on {datetime.now().strftime('%Y-%m-%d %H:%M:%S UTC')}

This document provides an overview of the Market Data Collector repository structure.

## Directory Layout

```
{tree_to_markdown(tree)}
```

## Key Directories

| Directory | Purpose |
|-----------|---------|
"""
    for dir_name, desc in sorted(IMPORTANT_DIRS.items()):
        content += f"| `{dir_name}/` | {desc} |\n"

    content += """
## Source Code Organization

### Core Application (`src/MarketDataCollector/`)

| Directory | Purpose |
|-----------|---------|
| `Domain/` | Business logic, collectors, events, models |
| `Infrastructure/` | Provider implementations, clients |
| `Storage/` | Data persistence, sinks, archival |
| `Application/` | Startup, configuration, HTTP endpoints |
| `Messaging/` | MassTransit message publishers |
| `Integrations/` | External system integrations |

### Microservices (`src/Microservices/`)

| Service | Port | Purpose |
|---------|------|---------|
| Gateway | 5000 | API Gateway and routing |
| TradeIngestion | 5001 | Trade data processing |
| QuoteIngestion | 5002 | Quote data processing |
| OrderBookIngestion | 5003 | Order book processing |
| HistoricalDataIngestion | 5004 | Historical backfill |
| DataValidation | 5005 | Data validation |

---

*This file is auto-generated. Do not edit manually.*
"""
    return content


def extract_providers(root: Path) -> list[dict[str, str]]:
    """Extract provider information from source code."""
    providers = []
    providers_dir = root / 'src' / 'MarketDataCollector' / 'Infrastructure' / 'Providers'

    if not providers_dir.exists():
        return providers

    # Pattern to match DataSource attribute
    datasource_pattern = re.compile(
        r'\[DataSource\s*\(\s*"([^"]+)"(?:\s*,\s*"([^"]*)")?(?:\s*,\s*DataSourceType\.(\w+))?(?:\s*,\s*DataSourceCategory\.(\w+))?\s*\)\]',
        re.MULTILINE
    )

    # Pattern to match class definitions implementing interfaces
    class_pattern = re.compile(
        r'public\s+(?:sealed\s+)?class\s+(\w+)\s*:\s*([^{]+)',
        re.MULTILINE
    )

    for cs_file in providers_dir.rglob('*.cs'):
        try:
            content = cs_file.read_text(encoding='utf-8')
        except Exception:
            continue

        # Find DataSource attributes
        for match in datasource_pattern.finditer(content):
            provider_id = match.group(1)
            display_name = match.group(2) or provider_id.replace('-', ' ').title()
            source_type = match.group(3) or 'Unknown'
            category = match.group(4) or 'Unknown'

            # Find the class name
            class_match = class_pattern.search(content[match.end():match.end() + 500])
            class_name = class_match.group(1) if class_match else 'Unknown'

            providers.append({
                'id': provider_id,
                'name': display_name,
                'type': source_type,
                'category': category,
                'class': class_name,
                'file': str(cs_file.relative_to(root))
            })

    return providers


def generate_provider_registry(root: Path, extract_attrs: bool = False) -> str:
    """Generate provider registry documentation."""
    providers = extract_providers(root) if extract_attrs else []

    # Static provider information (fallback)
    static_providers = [
        {'id': 'alpaca', 'name': 'Alpaca Markets', 'type': 'Streaming', 'category': 'RealTime'},
        {'id': 'interactive-brokers', 'name': 'Interactive Brokers', 'type': 'Streaming', 'category': 'RealTime'},
        {'id': 'polygon', 'name': 'Polygon.io', 'type': 'Streaming', 'category': 'RealTime'},
        {'id': 'nyse', 'name': 'NYSE', 'type': 'Streaming', 'category': 'RealTime'},
        {'id': 'stocksharp', 'name': 'StockSharp', 'type': 'Streaming', 'category': 'RealTime'},
        {'id': 'yahoo-finance', 'name': 'Yahoo Finance', 'type': 'Historical', 'category': 'Backfill'},
        {'id': 'stooq', 'name': 'Stooq', 'type': 'Historical', 'category': 'Backfill'},
        {'id': 'tiingo', 'name': 'Tiingo', 'type': 'Historical', 'category': 'Backfill'},
        {'id': 'alpha-vantage', 'name': 'Alpha Vantage', 'type': 'Historical', 'category': 'Backfill'},
        {'id': 'finnhub', 'name': 'Finnhub', 'type': 'Historical', 'category': 'Backfill'},
        {'id': 'nasdaq-data-link', 'name': 'Nasdaq Data Link', 'type': 'Historical', 'category': 'Backfill'},
    ]

    # Use extracted providers if available, otherwise use static list
    all_providers = providers if providers else static_providers

    content = f"""# Provider Registry

> Auto-generated on {datetime.now().strftime('%Y-%m-%d %H:%M:%S UTC')}

This document lists all data providers available in the Market Data Collector.

## Real-Time Streaming Providers

| Provider | ID | Type | Status |
|----------|-----|------|--------|
"""
    for p in all_providers:
        if p.get('category') == 'RealTime' or p.get('type') == 'Streaming':
            content += f"| {p['name']} | `{p['id']}` | {p.get('type', 'Streaming')} | ✅ Active |\n"

    content += """
## Historical Data Providers (Backfill)

| Provider | ID | Free Tier | Rate Limits |
|----------|-----|-----------|-------------|
"""
    for p in all_providers:
        if p.get('category') == 'Backfill' or p.get('type') == 'Historical':
            content += f"| {p['name']} | `{p['id']}` | Yes | Varies |\n"

    content += """
## Provider Configuration

Providers are configured via environment variables or `appsettings.json`:

```bash
# Real-time providers
export ALPACA__KEYID=your-key-id
export ALPACA__SECRETKEY=your-secret-key
export POLYGON__APIKEY=your-api-key

# Historical providers
export TIINGO__TOKEN=your-token
export ALPHAVANTAGE__APIKEY=your-key
```

## Adding a New Provider

1. Create provider class in `src/MarketDataCollector/Infrastructure/Providers/{Name}/`
2. Implement `IMarketDataClient` (streaming) or `IHistoricalDataProvider` (backfill)
3. Add `[DataSource]` attribute with provider metadata
4. Add `[ImplementsAdr]` attributes for ADR compliance
5. Register in DI container
6. Add configuration section
7. Write tests

---

*This file is auto-generated. Do not edit manually.*
"""
    return content


def extract_workflows(root: Path) -> list[dict[str, str]]:
    """Extract workflow information from GitHub Actions."""
    workflows = []
    workflows_dir = root / '.github' / 'workflows'

    if not workflows_dir.exists():
        return workflows

    name_pattern = re.compile(r'^name:\s*["\']?([^"\'#\n]+)["\']?\s*$', re.MULTILINE)
    trigger_pattern = re.compile(r'^on:\s*$', re.MULTILINE)

    for yml_file in workflows_dir.glob('*.yml'):
        try:
            content = yml_file.read_text(encoding='utf-8')
        except Exception:
            continue

        name_match = name_pattern.search(content)
        name = name_match.group(1).strip() if name_match else yml_file.stem

        # Extract triggers
        triggers = []
        if 'push:' in content:
            triggers.append('push')
        if 'pull_request:' in content:
            triggers.append('PR')
        if 'workflow_dispatch:' in content:
            triggers.append('manual')
        if 'schedule:' in content:
            triggers.append('scheduled')

        workflows.append({
            'file': yml_file.name,
            'name': name,
            'triggers': ', '.join(triggers) if triggers else 'unknown'
        })

    return sorted(workflows, key=lambda x: x['name'])


def generate_workflows_overview(root: Path) -> str:
    """Generate workflows overview documentation."""
    workflows = extract_workflows(root)

    content = f"""# GitHub Workflows Overview

> Auto-generated on {datetime.now().strftime('%Y-%m-%d %H:%M:%S UTC')}

This document provides an overview of all GitHub Actions workflows in the repository.

## Available Workflows

| Workflow | File | Triggers |
|----------|------|----------|
"""
    for wf in workflows:
        content += f"| {wf['name']} | `{wf['file']}` | {wf['triggers']} |\n"

    content += f"""
## Workflow Categories

### CI/CD Workflows
- **Build & Test**: Main build pipeline, test matrix
- **Code Quality**: Linting, static analysis
- **Security**: Dependency scanning, vulnerability checks

### Documentation Workflows
- **Documentation**: Validation, generation, deployment
- **Docs Structure Sync**: Auto-update structure documentation

### Release Workflows
- **Docker Build**: Container image builds
- **Publishing**: Release artifacts

### Maintenance Workflows
- **Scheduled Maintenance**: Cleanup, dependency updates
- **Stale Management**: Issue/PR lifecycle

## Workflow Count

- **Total workflows:** {len(workflows)}

---

*This file is auto-generated. Do not edit manually.*
"""
    return content


def ensure_output_dir(output_path: Path) -> None:
    """Ensure the output directory exists."""
    output_path.parent.mkdir(parents=True, exist_ok=True)


def main() -> int:
    """Main entry point."""
    args = parse_args()
    root = Path(args.root).resolve()
    output = Path(args.output)

    ensure_output_dir(output)

    try:
        if args.providers_only:
            content = generate_provider_registry(root, args.extract_attributes)
        elif args.workflows_only:
            content = generate_workflows_overview(root)
        elif args.format == 'json':
            tree = get_directory_tree(root, args.max_depth)
            content = json.dumps(tree, indent=2)
        else:
            content = generate_structure_markdown(root, args.max_depth)

        output.write_text(content, encoding='utf-8')
        print(f"Generated: {output}")
        return 0

    except Exception as e:
        print(f"Error: {e}", file=sys.stderr)
        return 1


if __name__ == '__main__':
    sys.exit(main())
