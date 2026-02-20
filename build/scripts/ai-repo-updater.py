#!/usr/bin/env python3
"""
AI Repository Updater — analysis and improvement toolkit for AI agents.

Provides an AI agent (Claude, Copilot, etc.) with structured commands to
analyse, audit, and improve the Market Data Collector repository.  The script
is designed to be *executed* by an AI that has shell access; it prints
machine‑readable JSON reports that the AI can parse, then act on.

Workflow for an AI agent:
    1. Run `audit`  to get a prioritised improvement plan.
    2. Pick items from the plan and implement fixes.
    3. Run `verify` to confirm nothing is broken.
    4. Run `audit`  again to confirm improvements.

Commands:
    audit              Full repository audit (combines all analysers).
    audit-docs         Documentation quality and coverage analysis.
    audit-code         Code quality analysis (missing patterns, conventions).
    audit-tests        Test coverage gaps and missing test patterns.
    audit-config       Configuration and CI/CD analysis.
    audit-providers    Provider implementation completeness.
    verify             Run build + tests + lint to validate changes.
    report             Generate a markdown improvement report.
    known-errors       List known AI errors to avoid repeating.
    diff-summary       Summarise uncommitted changes for commit message.

Usage:
    python3 build/scripts/ai-repo-updater.py audit
    python3 build/scripts/ai-repo-updater.py audit-docs --json-output audit.json
    python3 build/scripts/ai-repo-updater.py verify
    python3 build/scripts/ai-repo-updater.py report --output docs/generated/improvement-report.md
    python3 build/scripts/ai-repo-updater.py known-errors
    python3 build/scripts/ai-repo-updater.py diff-summary
"""

from __future__ import annotations

import argparse
import json
import os
import re
import subprocess
import sys
from dataclasses import asdict, dataclass, field
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Optional


# ---------------------------------------------------------------------------
# Constants
# ---------------------------------------------------------------------------

REPO_ROOT = Path(__file__).resolve().parents[2]

EXCLUDE_DIRS: frozenset[str] = frozenset({
    ".git", "node_modules", "bin", "obj", "__pycache__", ".vs",
    "TestResults", "artifacts", "publish", ".build-system",
})

SOURCE_EXTENSIONS: frozenset[str] = frozenset({".cs", ".fs", ".fsx"})
DOC_EXTENSIONS: frozenset[str] = frozenset({".md"})
CONFIG_EXTENSIONS: frozenset[str] = frozenset({".json", ".yml", ".yaml", ".xml", ".props", ".csproj", ".fsproj"})

# Architecture conventions from CLAUDE.md
REQUIRED_ASYNC_TOKEN = re.compile(r"async\s+Task", re.IGNORECASE)
CANCELLATION_TOKEN_PARAM = re.compile(r"CancellationToken\s+\w+")
SEALED_CLASS = re.compile(r"\bsealed\s+class\b")
UNSEALED_CLASS = re.compile(r"(?<!\bsealed\s)(?<!\babstract\s)(?<!\bstatic\s)\bclass\s+\w+")
IMPLEMENTS_ADR = re.compile(r"\[ImplementsAdr\(")
STRING_INTERPOLATION_LOG = re.compile(r'_logger\.Log\w+\(\$"')
TASK_RUN_IO = re.compile(r"Task\.Run\(")
BLOCKING_ASYNC = re.compile(r"\.(Result|Wait\(\))")
STRUCTURED_LOG = re.compile(r'_logger\.Log\w+\("[^"]*\{')
HTTP_CLIENT_NEW = re.compile(r"new\s+HttpClient\s*\(")

# Provider interfaces
PROVIDER_INTERFACES = [
    "IMarketDataClient",
    "IHistoricalDataProvider",
    "ISymbolSearchProvider",
]

# Test naming conventions
TEST_SUFFIX = re.compile(r"Tests?\.cs$")
TEST_CLASS = re.compile(r"class\s+(\w+Tests?)\b")


# ---------------------------------------------------------------------------
# Data Models
# ---------------------------------------------------------------------------

@dataclass
class Finding:
    """A single audit finding."""
    category: str
    severity: str  # critical, warning, info, suggestion
    file: str
    line: int
    message: str
    fix_hint: str = ""

    def to_dict(self) -> dict[str, Any]:
        return asdict(self)


@dataclass
class AuditReport:
    """Aggregated audit report."""
    timestamp: str = ""
    command: str = ""
    findings: list[Finding] = field(default_factory=list)
    summary: dict[str, int] = field(default_factory=dict)
    improvement_plan: list[dict[str, Any]] = field(default_factory=list)

    def add(self, finding: Finding) -> None:
        self.findings.append(finding)

    def finalise(self) -> None:
        self.timestamp = datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")
        self.summary = {
            "total": len(self.findings),
            "critical": sum(1 for f in self.findings if f.severity == "critical"),
            "warning": sum(1 for f in self.findings if f.severity == "warning"),
            "info": sum(1 for f in self.findings if f.severity == "info"),
            "suggestion": sum(1 for f in self.findings if f.severity == "suggestion"),
        }
        self._build_improvement_plan()

    def _build_improvement_plan(self) -> None:
        """Group findings into a prioritised improvement plan."""
        groups: dict[str, list[Finding]] = {}
        for f in self.findings:
            groups.setdefault(f.category, []).append(f)

        severity_order = {"critical": 0, "warning": 1, "info": 2, "suggestion": 3}
        plan: list[dict[str, Any]] = []
        for cat, items in sorted(
            groups.items(),
            key=lambda kv: min(severity_order.get(f.severity, 9) for f in kv[1]),
        ):
            worst = min(items, key=lambda f: severity_order.get(f.severity, 9))
            plan.append({
                "category": cat,
                "count": len(items),
                "worst_severity": worst.severity,
                "example_file": worst.file,
                "example_message": worst.message,
                "fix_hint": worst.fix_hint,
            })
        self.improvement_plan = plan

    def to_dict(self) -> dict[str, Any]:
        return {
            "timestamp": self.timestamp,
            "command": self.command,
            "summary": self.summary,
            "improvement_plan": self.improvement_plan,
            "findings": [f.to_dict() for f in self.findings],
        }


# ---------------------------------------------------------------------------
# File Walking
# ---------------------------------------------------------------------------

def walk_files(root: Path, extensions: frozenset[str] | None = None) -> list[Path]:
    """Walk the repo tree, skipping excluded directories."""
    result: list[Path] = []
    for dirpath, dirnames, filenames in os.walk(root):
        dirnames[:] = [d for d in dirnames if d not in EXCLUDE_DIRS]
        for fname in filenames:
            p = Path(dirpath) / fname
            if extensions is None or p.suffix in extensions:
                result.append(p)
    return result


def read_lines(path: Path) -> list[str]:
    """Read file lines, returning empty list on decode errors."""
    try:
        return path.read_text(encoding="utf-8", errors="replace").splitlines()
    except OSError:
        return []


# ---------------------------------------------------------------------------
# Analysers
# ---------------------------------------------------------------------------

def audit_code(root: Path, report: AuditReport) -> None:
    """Analyse C#/F# source files for convention violations."""
    src_dir = root / "src"
    if not src_dir.exists():
        return

    for path in walk_files(src_dir, SOURCE_EXTENSIONS):
        rel = str(path.relative_to(root))
        lines = read_lines(path)
        text = "\n".join(lines)

        # Skip generated files
        if ".g.cs" in rel or "GlobalUsings.cs" in rel:
            continue

        # --- Check: async methods missing CancellationToken ---
        for i, line in enumerate(lines, 1):
            if REQUIRED_ASYNC_TOKEN.search(line) and not CANCELLATION_TOKEN_PARAM.search(line):
                # Skip interface declarations and very short lines
                stripped = line.strip()
                if stripped.startswith("//") or stripped.startswith("*"):
                    continue
                report.add(Finding(
                    category="missing-cancellation-token",
                    severity="warning",
                    file=rel, line=i,
                    message="Async method appears to lack CancellationToken parameter.",
                    fix_hint="Add 'CancellationToken ct = default' parameter.",
                ))

        # --- Check: string interpolation in logging ---
        for i, line in enumerate(lines, 1):
            if STRING_INTERPOLATION_LOG.search(line):
                report.add(Finding(
                    category="interpolated-logging",
                    severity="warning",
                    file=rel, line=i,
                    message="Logger call uses string interpolation instead of structured parameters.",
                    fix_hint='Use _logger.LogXxx("Message {Param}", param) instead of $"...".',
                ))

        # --- Check: new HttpClient() ---
        for i, line in enumerate(lines, 1):
            if HTTP_CLIENT_NEW.search(line):
                report.add(Finding(
                    category="raw-httpclient",
                    severity="warning",
                    file=rel, line=i,
                    message="Direct HttpClient instantiation detected; use IHttpClientFactory.",
                    fix_hint="Inject IHttpClientFactory and call CreateClient().",
                ))

        # --- Check: blocking async (.Result / .Wait()) ---
        for i, line in enumerate(lines, 1):
            if BLOCKING_ASYNC.search(line) and "Task" in line:
                stripped = line.strip()
                if stripped.startswith("//") or stripped.startswith("*"):
                    continue
                report.add(Finding(
                    category="blocking-async",
                    severity="critical",
                    file=rel, line=i,
                    message="Blocking async code (.Result or .Wait()) can cause deadlocks.",
                    fix_hint="Use 'await' instead of .Result or .Wait().",
                ))

        # --- Check: Task.Run for likely I/O ---
        for i, line in enumerate(lines, 1):
            if TASK_RUN_IO.search(line):
                # Heuristic: if the lambda mentions Async, it's probably I/O
                if "Async" in line or "Http" in line or "Stream" in line:
                    report.add(Finding(
                        category="taskrun-io",
                        severity="warning",
                        file=rel, line=i,
                        message="Task.Run used for what appears to be I/O-bound work.",
                        fix_hint="Use async/await directly for I/O operations.",
                    ))

        # --- Check: unsealed classes (info-level) ---
        if path.suffix == ".cs":
            for i, line in enumerate(lines, 1):
                if UNSEALED_CLASS.search(line) and "abstract" not in line and "static" not in line:
                    stripped = line.strip()
                    if stripped.startswith("//"):
                        continue
                    # Only flag public classes in src (not tests)
                    if "public" in stripped and "/Tests/" not in rel:
                        report.add(Finding(
                            category="unsealed-class",
                            severity="info",
                            file=rel, line=i,
                            message="Public class is not sealed. Consider sealing if not designed for inheritance.",
                            fix_hint="Add 'sealed' modifier unless this class is a base class.",
                        ))


def audit_docs(root: Path, report: AuditReport) -> None:
    """Analyse documentation quality and coverage."""
    docs_dir = root / "docs"
    if not docs_dir.exists():
        return

    doc_files = walk_files(docs_dir, DOC_EXTENSIONS)

    # --- Check: broken internal links ---
    for path in doc_files:
        rel = str(path.relative_to(root))
        lines = read_lines(path)
        in_code_block = False
        for i, line in enumerate(lines, 1):
            # Track fenced code blocks to skip example links
            stripped = line.strip()
            if stripped.startswith("```"):
                in_code_block = not in_code_block
                continue
            if in_code_block:
                continue
            # Find markdown links [text](path)
            for m in re.finditer(r'\[([^\]]+)\]\(([^)]+)\)', line):
                link_target = m.group(2)
                # Skip external URLs, anchors, and placeholder links
                if link_target.startswith(("http://", "https://", "#", "mailto:")):
                    continue
                if link_target in ("link", "url", "path", "file"):
                    continue
                # Resolve relative path
                target_path = (path.parent / link_target.split("#")[0]).resolve()
                if not target_path.exists():
                    report.add(Finding(
                        category="broken-doc-link",
                        severity="warning",
                        file=rel, line=i,
                        message=f"Broken internal link: {link_target}",
                        fix_hint="Update the link target or remove the dead link.",
                    ))

    # --- Check: empty or stub documentation ---
    for path in doc_files:
        rel = str(path.relative_to(root))
        lines = read_lines(path)
        content_lines = [l for l in lines if l.strip() and not l.strip().startswith("#")]
        if len(content_lines) < 3:
            report.add(Finding(
                category="stub-documentation",
                severity="info",
                file=rel, line=1,
                message="Documentation file appears to be a stub (fewer than 3 content lines).",
                fix_hint="Add meaningful content or remove the stub file.",
            ))

    # --- Check: outdated timestamps ---
    cutoff_year = datetime.now(timezone.utc).year
    for path in doc_files:
        rel = str(path.relative_to(root))
        lines = read_lines(path)
        for i, line in enumerate(lines, 1):
            m = re.search(r"Last Updated:\s*(\d{4})-(\d{2})-(\d{2})", line)
            if m:
                year = int(m.group(1))
                if year < cutoff_year - 1:
                    report.add(Finding(
                        category="outdated-doc-timestamp",
                        severity="info",
                        file=rel, line=i,
                        message=f"Documentation last updated in {year}, may be stale.",
                        fix_hint="Review and update the document, then update the timestamp.",
                    ))

    # --- Check: ADR files missing required sections ---
    adr_dir = docs_dir / "adr"
    if adr_dir.exists():
        required_sections = {"Status", "Context", "Decision", "Consequences"}
        for path in walk_files(adr_dir, DOC_EXTENSIONS):
            if path.name.startswith("_") or path.name == "README.md":
                continue
            rel = str(path.relative_to(root))
            text = path.read_text(encoding="utf-8", errors="replace")
            headings = set(re.findall(r"^##\s+(.+)", text, re.MULTILINE))
            missing = required_sections - headings
            if missing:
                report.add(Finding(
                    category="adr-missing-sections",
                    severity="warning",
                    file=rel, line=1,
                    message=f"ADR missing required sections: {', '.join(sorted(missing))}.",
                    fix_hint="Add the missing sections following the ADR template.",
                ))


def audit_tests(root: Path, report: AuditReport) -> None:
    """Analyse test coverage gaps."""
    src_dir = root / "src"
    test_dir = root / "tests"
    if not src_dir.exists() or not test_dir.exists():
        return

    # Collect source classes (non-test, non-generated)
    source_classes: dict[str, str] = {}  # class_name -> file_path
    for path in walk_files(src_dir, frozenset({".cs"})):
        rel = str(path.relative_to(root))
        if ".g.cs" in rel or "GlobalUsings" in rel:
            continue
        lines = read_lines(path)
        for line in lines:
            m = re.search(r"class\s+(\w+)", line)
            if m and "Test" not in m.group(1):
                source_classes[m.group(1)] = rel

    # Collect test classes
    test_classes: set[str] = set()
    for path in walk_files(test_dir, frozenset({".cs", ".fs"})):
        lines = read_lines(path)
        for line in lines:
            m = re.search(r"class\s+(\w+Tests?)\b", line)
            if m:
                # Derive the class being tested
                tested = m.group(1).replace("Tests", "").replace("Test", "")
                test_classes.add(tested)

    # Find important classes without tests
    important_patterns = [
        "Service", "Provider", "Client", "Handler", "Manager",
        "Collector", "Monitor", "Pipeline", "Sink", "Writer",
    ]
    for cls_name, file_path in source_classes.items():
        if any(pat in cls_name for pat in important_patterns):
            if cls_name not in test_classes:
                report.add(Finding(
                    category="missing-test",
                    severity="suggestion",
                    file=file_path, line=1,
                    message=f"Class '{cls_name}' has no corresponding test class.",
                    fix_hint=f"Create '{cls_name}Tests.cs' in the appropriate test directory.",
                ))


def audit_config(root: Path, report: AuditReport) -> None:
    """Analyse configuration and CI/CD files."""
    # --- Check: workflows with potential issues ---
    workflows_dir = root / ".github" / "workflows"
    if workflows_dir.exists():
        for path in walk_files(workflows_dir, frozenset({".yml", ".yaml"})):
            rel = str(path.relative_to(root))
            lines = read_lines(path)
            for i, line in enumerate(lines, 1):
                # Check for hardcoded secrets
                if re.search(r"(password|secret|token|key)\s*[:=]\s*['\"][^${}]", line, re.IGNORECASE):
                    stripped = line.strip()
                    if not stripped.startswith("#"):
                        report.add(Finding(
                            category="hardcoded-secret-workflow",
                            severity="critical",
                            file=rel, line=i,
                            message="Possible hardcoded secret in workflow file.",
                            fix_hint="Use GitHub Secrets (${{ secrets.NAME }}) instead.",
                        ))
                # Check for deprecated actions
                if "actions/checkout@v2" in line or "actions/setup-dotnet@v2" in line:
                    report.add(Finding(
                        category="deprecated-action",
                        severity="info",
                        file=rel, line=i,
                        message="Using an older version of a GitHub Action.",
                        fix_hint="Update to the latest major version (v4).",
                    ))

    # --- Check: Directory.Packages.props consistency ---
    packages_props = root / "Directory.Packages.props"
    if packages_props.exists():
        # Find any .csproj with Version= on PackageReference
        for path in walk_files(root / "src", frozenset({".csproj"})):
            rel = str(path.relative_to(root))
            lines = read_lines(path)
            for i, line in enumerate(lines, 1):
                if "PackageReference" in line and 'Version="' in line:
                    report.add(Finding(
                        category="cpm-version-override",
                        severity="critical",
                        file=rel, line=i,
                        message="PackageReference has Version attribute — violates Central Package Management.",
                        fix_hint="Remove Version= from PackageReference; define version in Directory.Packages.props.",
                    ))


def audit_providers(root: Path, report: AuditReport) -> None:
    """Check provider implementations for completeness."""
    providers_dir = root / "src" / "MarketDataCollector.Infrastructure" / "Providers"
    if not providers_dir.exists():
        return

    for path in walk_files(providers_dir, frozenset({".cs"})):
        rel = str(path.relative_to(root))
        text = path.read_text(encoding="utf-8", errors="replace")

        # Check for ImplementsAdr attribute
        implements_interface = any(iface in text for iface in PROVIDER_INTERFACES)
        has_adr_attr = IMPLEMENTS_ADR.search(text) is not None
        if implements_interface and not has_adr_attr:
            report.add(Finding(
                category="missing-implements-adr",
                severity="warning",
                file=rel, line=1,
                message="Provider implements a core interface but lacks [ImplementsAdr] attribute.",
                fix_hint='Add [ImplementsAdr("ADR-001", "reason")] to the class.',
            ))

        # Check for DataSource attribute on client classes
        if "MarketDataClient" in path.stem or "DataSource" in path.stem:
            if "[DataSource(" not in text and "class" in text:
                report.add(Finding(
                    category="missing-datasource-attr",
                    severity="info",
                    file=rel, line=1,
                    message="Provider class may be missing [DataSource] attribute.",
                    fix_hint='Add [DataSource("provider-name")] for automatic discovery.',
                ))


# ---------------------------------------------------------------------------
# Verification
# ---------------------------------------------------------------------------

def run_verify(root: Path) -> dict[str, Any]:
    """Run build + tests + lint and return results."""
    results: dict[str, Any] = {"timestamp": datetime.now(timezone.utc).isoformat()}
    steps = [
        ("build", ["dotnet", "build", "-c", "Release", "--nologo", "-v", "quiet"]),
        ("test", ["dotnet", "test", "tests/MarketDataCollector.Tests", "-c", "Release",
                   "--nologo", "-v", "quiet", "--no-build"]),
        ("lint", ["dotnet", "format", "--verify-no-changes", "--no-restore"]),
    ]
    overall_ok = True
    for name, cmd in steps:
        try:
            proc = subprocess.run(
                cmd, cwd=root, capture_output=True, text=True, timeout=300
            )
            ok = proc.returncode == 0
            results[name] = {
                "ok": ok,
                "return_code": proc.returncode,
                "stderr_tail": proc.stderr[-500:] if proc.stderr else "",
                "stdout_tail": proc.stdout[-500:] if proc.stdout else "",
            }
            if not ok:
                overall_ok = False
        except subprocess.TimeoutExpired:
            results[name] = {"ok": False, "error": "timeout"}
            overall_ok = False
        except FileNotFoundError:
            results[name] = {"ok": False, "error": "command not found"}
            overall_ok = False

    results["overall_ok"] = overall_ok
    return results


# ---------------------------------------------------------------------------
# Diff Summary
# ---------------------------------------------------------------------------

def generate_diff_summary(root: Path) -> dict[str, Any]:
    """Summarise uncommitted changes for commit message drafting."""
    result: dict[str, Any] = {}
    try:
        status = subprocess.run(
            ["git", "status", "--porcelain"],
            cwd=root, capture_output=True, text=True, timeout=30
        )
        lines = status.stdout.strip().splitlines()
        added = [l[3:] for l in lines if l.startswith("A ") or l.startswith("?? ")]
        modified = [l[3:] for l in lines if l.startswith(" M") or l.startswith("M ")]
        deleted = [l[3:] for l in lines if l.startswith(" D") or l.startswith("D ")]
        result["files_added"] = added
        result["files_modified"] = modified
        result["files_deleted"] = deleted
        result["total_changed"] = len(added) + len(modified) + len(deleted)
    except (subprocess.TimeoutExpired, FileNotFoundError):
        result["error"] = "git not available"

    try:
        diff_stat = subprocess.run(
            ["git", "diff", "--stat"],
            cwd=root, capture_output=True, text=True, timeout=30
        )
        result["diff_stat"] = diff_stat.stdout.strip()
    except (subprocess.TimeoutExpired, FileNotFoundError):
        pass

    return result


# ---------------------------------------------------------------------------
# Known Errors
# ---------------------------------------------------------------------------

def load_known_errors(root: Path) -> list[dict[str, str]]:
    """Parse ai-known-errors.md into a list of entries."""
    path = root / "docs" / "ai" / "ai-known-errors.md"
    if not path.exists():
        return []

    text = path.read_text(encoding="utf-8", errors="replace")
    entries: list[dict[str, str]] = []
    current: dict[str, str] = {}

    for line in text.splitlines():
        if line.startswith("### AI-"):
            if current:
                entries.append(current)
            current = {"id": line.lstrip("# ").strip()}
        elif line.startswith("- **") and current:
            m = re.match(r"- \*\*(\w[\w\s]*)\*\*:\s*(.*)", line)
            if m:
                key = m.group(1).strip().lower().replace(" ", "_")
                current[key] = m.group(2).strip()

    if current:
        entries.append(current)
    return entries


# ---------------------------------------------------------------------------
# Report Generation
# ---------------------------------------------------------------------------

def generate_markdown_report(report: AuditReport) -> str:
    """Generate a markdown improvement report."""
    lines: list[str] = [
        "# Repository Improvement Report",
        "",
        f"*Generated: {report.timestamp}*",
        f"*Command: `{report.command}`*",
        "",
        "## Summary",
        "",
        f"| Severity | Count |",
        f"|----------|-------|",
    ]
    for sev in ("critical", "warning", "info", "suggestion"):
        lines.append(f"| {sev.capitalize()} | {report.summary.get(sev, 0)} |")
    lines.append(f"| **Total** | **{report.summary.get('total', 0)}** |")
    lines.append("")

    if report.improvement_plan:
        lines.append("## Improvement Plan (Priority Order)")
        lines.append("")
        for idx, item in enumerate(report.improvement_plan, 1):
            severity_badge = {"critical": "!!!", "warning": "!!", "info": "!", "suggestion": "~"}.get(
                item["worst_severity"], "?"
            )
            lines.append(f"### {idx}. [{severity_badge}] {item['category']} ({item['count']} findings)")
            lines.append(f"- **Example**: `{item['example_file']}`")
            lines.append(f"- **Issue**: {item['example_message']}")
            if item.get("fix_hint"):
                lines.append(f"- **Fix**: {item['fix_hint']}")
            lines.append("")

    if report.findings:
        lines.append("## Detailed Findings")
        lines.append("")
        by_severity: dict[str, list[Finding]] = {}
        for f in report.findings:
            by_severity.setdefault(f.severity, []).append(f)

        for sev in ("critical", "warning", "info", "suggestion"):
            items = by_severity.get(sev, [])
            if not items:
                continue
            lines.append(f"### {sev.capitalize()} ({len(items)})")
            lines.append("")
            for f in items[:50]:  # Cap per severity to keep report manageable
                lines.append(f"- `{f.file}:{f.line}` — {f.message}")
            if len(items) > 50:
                lines.append(f"- *... and {len(items) - 50} more*")
            lines.append("")

    lines.append("---")
    lines.append(f"*Report generated by ai-repo-updater.py*")
    return "\n".join(lines)


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------

def run_audit(root: Path, command: str) -> AuditReport:
    """Run the requested audit command."""
    report = AuditReport(command=command)

    auditors = {
        "audit": [audit_code, audit_docs, audit_tests, audit_config, audit_providers],
        "audit-docs": [audit_docs],
        "audit-code": [audit_code],
        "audit-tests": [audit_tests],
        "audit-config": [audit_config],
        "audit-providers": [audit_providers],
    }

    for fn in auditors.get(command, []):
        fn(root, report)

    report.finalise()
    return report


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(
        description="AI Repository Updater — audit and improve the codebase.",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog=__doc__,
    )
    parser.add_argument(
        "command",
        choices=[
            "audit", "audit-docs", "audit-code", "audit-tests",
            "audit-config", "audit-providers",
            "verify", "report", "known-errors", "diff-summary",
        ],
        help="Command to run.",
    )
    parser.add_argument("--root", "-r", type=Path, default=REPO_ROOT,
                        help="Repository root (auto-detected).")
    parser.add_argument("--output", "-o", type=Path,
                        help="Write markdown report to this path.")
    parser.add_argument("--json-output", "-j", type=Path,
                        help="Write JSON results to this path.")
    parser.add_argument("--summary", "-s", action="store_true",
                        help="Print a short summary to stdout.")
    args = parser.parse_args(argv)

    if args.command in (
        "audit", "audit-docs", "audit-code", "audit-tests",
        "audit-config", "audit-providers", "report",
    ):
        cmd = args.command if args.command != "report" else "audit"
        report = run_audit(args.root, cmd)
        result = report.to_dict()

        if args.json_output:
            args.json_output.parent.mkdir(parents=True, exist_ok=True)
            args.json_output.write_text(json.dumps(result, indent=2) + "\n")

        if args.output or args.command == "report":
            md = generate_markdown_report(report)
            out_path = args.output or Path("docs/generated/improvement-report.md")
            out_path.parent.mkdir(parents=True, exist_ok=True)
            out_path.write_text(md + "\n")
            if args.summary:
                print(f"Report written to {out_path}")

        if args.summary or (not args.output and not args.json_output and args.command != "report"):
            print(json.dumps(result, indent=2))

        return 0

    elif args.command == "verify":
        result = run_verify(args.root)
        if args.json_output:
            args.json_output.parent.mkdir(parents=True, exist_ok=True)
            args.json_output.write_text(json.dumps(result, indent=2) + "\n")
        print(json.dumps(result, indent=2))
        return 0 if result.get("overall_ok") else 1

    elif args.command == "known-errors":
        entries = load_known_errors(args.root)
        if args.json_output:
            args.json_output.parent.mkdir(parents=True, exist_ok=True)
            args.json_output.write_text(json.dumps(entries, indent=2) + "\n")
        print(json.dumps(entries, indent=2))
        return 0

    elif args.command == "diff-summary":
        result = generate_diff_summary(args.root)
        if args.json_output:
            args.json_output.parent.mkdir(parents=True, exist_ok=True)
            args.json_output.write_text(json.dumps(result, indent=2) + "\n")
        print(json.dumps(result, indent=2))
        return 0

    return 1


if __name__ == "__main__":
    sys.exit(main())
