#!/usr/bin/env python3
"""Auto-generate AI assistant prompts from workflow run results.

Analyzes recent workflow run results (failures, patterns, annotations)
and existing prompts to generate new or updated prompt templates.

Usage:
    python3 generate-prompts.py \
        --workflow "test-matrix" \
        --run-id 12345 \
        --output .github/prompts/ \
        --json-output prompt-generation-results.json

Environment:
    GH_TOKEN / GITHUB_TOKEN  - GitHub API token (required for API calls)
    GITHUB_REPOSITORY         - owner/repo (default: auto-detect from git)
"""

from __future__ import annotations

import argparse
import json
import os
import re
import subprocess
import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


# ---------------------------------------------------------------------------
# Constants
# ---------------------------------------------------------------------------

PROMPT_DIR = Path(".github/prompts")
PROMPT_EXT = ".prompt.yml"
MAX_LOG_LINES = 200  # max lines to extract per failed job
WORKFLOW_RESULTS_FILE = "workflow-run-results.json"

# Categories that map workflow failure patterns to prompt topics
FAILURE_CATEGORIES = {
    "build": {
        "patterns": [
            r"error\s+CS\d{4}",
            r"Build FAILED",
            r"error\s+NU\d{4}",
            r"NETSDK\d{4}",
            r"dotnet build.*failed",
        ],
        "prompt_name": "fix-build-errors",
        "description": "Fix build errors identified by CI",
    },
    "test": {
        "patterns": [
            r"Failed!\s+- Failed:",
            r"Test Run Failed",
            r"\[\s*FAIL\s*\]",
            r"Assert\.\w+ failed",
            r"Expected .* but (was|got)",
        ],
        "prompt_name": "fix-test-failures",
        "description": "Fix test failures identified by CI",
    },
    "code-quality": {
        "patterns": [
            r"warning\s+CS\d{4}",
            r"IDE\d{4}",
            r"CA\d{4}",
            r"SA\d{4}",
            r"Style cop",
        ],
        "prompt_name": "fix-code-quality",
        "description": "Address code quality issues from CI analysis",
    },
    "security": {
        "patterns": [
            r"security\s+vulnerabilit",
            r"CVE-\d{4}-\d+",
            r"dependabot",
            r"CodeQL.*alert",
            r"secret.*detected",
        ],
        "prompt_name": "fix-security-issues",
        "description": "Resolve security vulnerabilities found by CI",
    },
    "docker": {
        "patterns": [
            r"docker\s+build.*failed",
            r"Dockerfile.*error",
            r"container.*failed",
            r"image.*push.*failed",
        ],
        "prompt_name": "fix-docker-issues",
        "description": "Fix Docker build and deployment issues",
    },
    "performance": {
        "patterns": [
            r"benchmark.*regress",
            r"performance.*degrad",
            r"slower\s+than\s+baseline",
            r"timeout.*exceeded",
        ],
        "prompt_name": "fix-performance-regression",
        "description": "Address performance regressions detected by benchmarks",
    },
}


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------


def get_gh_token() -> str:
    """Get GitHub token from environment."""
    token = os.environ.get("GH_TOKEN") or os.environ.get("GITHUB_TOKEN", "")
    return token


def get_repo() -> str:
    """Get repository owner/name."""
    repo = os.environ.get("GITHUB_REPOSITORY", "")
    if repo:
        return repo
    try:
        result = subprocess.run(
            ["gh", "repo", "view", "--json", "nameWithOwner", "-q", ".nameWithOwner"],
            capture_output=True,
            text=True,
            check=True,
        )
        return result.stdout.strip()
    except (subprocess.CalledProcessError, FileNotFoundError):
        return ""


def gh_api(endpoint: str) -> dict | list | None:
    """Call GitHub API via gh CLI."""
    try:
        result = subprocess.run(
            ["gh", "api", endpoint, "--paginate"],
            capture_output=True,
            text=True,
            check=True,
        )
        return json.loads(result.stdout)
    except (subprocess.CalledProcessError, FileNotFoundError, json.JSONDecodeError):
        return None


def fetch_workflow_runs(
    repo: str, workflow: str, count: int = 5
) -> list[dict[str, Any]]:
    """Fetch recent workflow runs."""
    endpoint = f"repos/{repo}/actions/workflows/{workflow}/runs?per_page={count}"
    data = gh_api(endpoint)
    if isinstance(data, dict):
        return data.get("workflow_runs", [])
    return []


def fetch_run_jobs(repo: str, run_id: int) -> list[dict[str, Any]]:
    """Fetch jobs for a workflow run."""
    endpoint = f"repos/{repo}/actions/runs/{run_id}/jobs"
    data = gh_api(endpoint)
    if isinstance(data, dict):
        return data.get("jobs", [])
    return []


def fetch_job_logs(repo: str, job_id: int) -> str:
    """Fetch logs for a specific job (truncated)."""
    try:
        result = subprocess.run(
            ["gh", "api", f"repos/{repo}/actions/jobs/{job_id}/logs"],
            capture_output=True,
            text=True,
            check=False,
        )
        if result.returncode == 0:
            lines = result.stdout.splitlines()
            # Return last N lines for analysis
            return "\n".join(lines[-MAX_LOG_LINES:])
    except FileNotFoundError:
        pass
    return ""


def fetch_run_annotations(repo: str, run_id: int) -> list[dict[str, Any]]:
    """Fetch annotations (errors/warnings) for a workflow run."""
    # Annotations come from check runs; try to fetch via check-suite
    endpoint = f"repos/{repo}/actions/runs/{run_id}/annotations"
    try:
        result = subprocess.run(
            ["gh", "api", endpoint, "--paginate"],
            capture_output=True,
            text=True,
            check=False,
        )
        if result.returncode == 0:
            return json.loads(result.stdout)
    except (FileNotFoundError, json.JSONDecodeError):
        pass
    return []


# ---------------------------------------------------------------------------
# Analysis
# ---------------------------------------------------------------------------


def classify_failures(
    logs: str, annotations: list[dict[str, Any]]
) -> dict[str, list[str]]:
    """Classify failure patterns from logs and annotations."""
    findings: dict[str, list[str]] = {}

    combined_text = logs
    for ann in annotations:
        msg = ann.get("message", "")
        combined_text += f"\n{msg}"

    for category, info in FAILURE_CATEGORIES.items():
        matches = []
        for pattern in info["patterns"]:
            for match in re.finditer(pattern, combined_text, re.IGNORECASE):
                # Get surrounding context
                start = max(0, combined_text.rfind("\n", 0, match.start()) + 1)
                end = combined_text.find("\n", match.end())
                if end == -1:
                    end = len(combined_text)
                context_line = combined_text[start:end].strip()
                if context_line and context_line not in matches:
                    matches.append(context_line)
        if matches:
            findings[category] = matches[:10]  # cap at 10 per category

    return findings


def extract_failed_tests(logs: str) -> list[str]:
    """Extract failed test names from log output."""
    failed = []
    patterns = [
        r"\[\s*FAIL\s*\]\s*(.+)",
        r"Failed\s+(\S+\.\S+)",
        r"X\s+(\S+\.\S+)",
    ]
    for pattern in patterns:
        for match in re.finditer(pattern, logs):
            name = match.group(1).strip()
            if name and name not in failed:
                failed.append(name)
    return failed[:20]


def extract_error_codes(logs: str) -> list[str]:
    """Extract unique compiler/analyzer error codes."""
    codes = set()
    for match in re.finditer(
        r"(?:error|warning)\s+(CS\d{4}|NU\d{4}|CA\d{4}|SA\d{4}|IDE\d{4}|NETSDK\d{4})",
        logs,
    ):
        codes.add(match.group(1))
    return sorted(codes)


# ---------------------------------------------------------------------------
# Prompt reading
# ---------------------------------------------------------------------------


def read_existing_prompts(prompt_dir: Path) -> list[dict[str, Any]]:
    """Read all existing prompt files and extract metadata."""
    prompts = []
    if not prompt_dir.exists():
        return prompts

    for f in sorted(prompt_dir.glob(f"*{PROMPT_EXT}")):
        content = f.read_text(encoding="utf-8")
        # Simple YAML extraction (name and description)
        name_match = re.search(r"^name:\s*(.+)$", content, re.MULTILINE)
        desc_match = re.search(r"^description:\s*(.+)$", content, re.MULTILINE)
        prompts.append(
            {
                "file": f.name,
                "stem": f.stem.replace(".prompt", ""),
                "name": name_match.group(1).strip() if name_match else f.stem,
                "description": desc_match.group(1).strip() if desc_match else "",
                "size": len(content),
            }
        )
    return prompts


# ---------------------------------------------------------------------------
# Prompt generation
# ---------------------------------------------------------------------------


def generate_prompt_yaml(
    name: str,
    description: str,
    category: str,
    findings: list[str],
    failed_tests: list[str],
    error_codes: list[str],
    workflow_name: str,
    run_url: str,
) -> str:
    """Generate a .prompt.yml file content for a failure category."""

    # Build the error context section
    error_context_lines = []
    if error_codes:
        error_context_lines.append("      ## Error Codes Found")
        for code in error_codes[:15]:
            error_context_lines.append(f"      - `{code}`")
        error_context_lines.append("")

    if findings:
        error_context_lines.append("      ## Failure Patterns")
        for finding in findings[:10]:
            # Sanitize for YAML
            safe = finding.replace("`", "'").replace('"', "'")
            if len(safe) > 200:
                safe = safe[:200] + "..."
            error_context_lines.append(f"      - `{safe}`")
        error_context_lines.append("")

    if failed_tests:
        error_context_lines.append("      ## Failed Tests")
        for test in failed_tests[:10]:
            error_context_lines.append(f"      - `{test}`")
        error_context_lines.append("")

    error_context = "\n".join(error_context_lines)

    # Map category to expert context
    category_context = {
        "build": """\
      You are a senior .NET developer troubleshooting build failures in a .NET 9.0 C#/F# project.

      ## Project Build Context
      - .NET 9.0 SDK with C# 13 and F# 8.0
      - Central Package Management (Directory.Packages.props) - never add Version to PackageReference
      - Cross-platform builds require EnableWindowsTargeting=true
      - Solution has 14 projects including WPF and UWP desktop apps
      - Build command: `dotnet build -c Release`

      ## Common Build Error Patterns
      - NU1008: Version on PackageReference with Central Package Management
      - NETSDK1100: Missing EnableWindowsTargeting
      - CS0246/CS0234: Missing using directive or assembly reference
      - CS8600-CS8604: Nullable reference type warnings treated as errors""",
        "test": """\
      You are a senior .NET developer fixing test failures in a market data collection system.

      ## Test Framework
      - xUnit for test framework
      - FluentAssertions for assertions
      - Moq / NSubstitute for mocking
      - Tests in tests/MarketDataCollector.Tests/ and tests/MarketDataCollector.FSharp.Tests/

      ## Test Patterns
      - Arrange-Act-Assert structure
      - Mock external dependencies (providers, storage)
      - Test edge cases: null inputs, empty collections, boundary values
      - Run tests: `dotnet test tests/MarketDataCollector.Tests`""",
        "code-quality": """\
      You are a senior .NET developer addressing code quality issues in a market data system.

      ## Quality Standards
      - Nullable reference types enabled
      - Roslyn analyzers active (CA, IDE, SA rules)
      - Structured logging with Serilog (no string interpolation)
      - Async/await for all I/O operations with CancellationToken
      - Classes sealed unless designed for inheritance""",
        "security": """\
      You are a security engineer reviewing vulnerabilities in a .NET market data system.

      ## Security Requirements
      - Never log sensitive data (API keys, credentials)
      - Use environment variables for all secrets
      - Validate all external inputs
      - Sanitize file paths to prevent traversal
      - Keep dependencies updated (Dependabot enabled)""",
        "docker": """\
      You are a DevOps engineer fixing Docker issues for a .NET market data system.

      ## Docker Context
      - Dockerfile at deploy/docker/Dockerfile
      - docker-compose at deploy/docker/docker-compose.yml
      - Multi-stage build with .NET 9.0 SDK and runtime images
      - Build: `docker build -f deploy/docker/Dockerfile .`""",
        "performance": """\
      You are a performance engineer addressing regressions in a high-throughput data pipeline.

      ## Performance Context
      - BenchmarkDotNet benchmarks in benchmarks/MarketDataCollector.Benchmarks/
      - System.Threading.Channels for producer-consumer patterns
      - Avoid allocations in hot paths
      - Use Span<T> and Memory<T> for buffer operations
      - Run benchmarks: `dotnet run --project benchmarks/MarketDataCollector.Benchmarks -c Release`""",
    }

    system_content = category_context.get(category, category_context["build"])

    now = datetime.now(timezone.utc).strftime("%Y-%m-%d")

    yaml = f"""name: {name}
description: {description}
# Auto-generated prompt from workflow results - {workflow_name}
# Generated: {now}
# Source: {run_url}
# Model-agnostic prompt - works with any capable LLM
messages:
  - role: system
    content: |
{system_content}

{error_context}
      ## Resolution Approach
      1. Read the error messages and stack traces carefully
      2. Identify the root cause (not just symptoms)
      3. Check if this is a known pattern in docs/ai/ai-known-errors.md
      4. Apply the fix following project conventions
      5. Verify the fix resolves the issue without introducing new problems
      6. Add tests if the failure was not covered

  - role: user
    content: |
      The CI workflow `{workflow_name}` has reported failures that need attention.

      Error details:
      ```
      {{{{error_details}}}}
      ```

      Affected files:
      {{{{affected_files}}}}

      Please provide:
      1. Root cause analysis of each failure
      2. Specific code fixes with file paths and line numbers
      3. Verification steps to confirm the fix
      4. Any preventive measures to avoid recurrence
"""
    return yaml


def generate_workflow_summary_prompt(
    workflow_name: str,
    run_url: str,
    all_findings: dict[str, list[str]],
    all_failed_tests: list[str],
    all_error_codes: list[str],
    run_conclusion: str,
) -> str:
    """Generate a summary prompt for overall workflow results."""

    now = datetime.now(timezone.utc).strftime("%Y-%m-%d")

    findings_section = []
    for cat, items in all_findings.items():
        findings_section.append(f"      ### {cat.replace('-', ' ').title()}")
        for item in items[:5]:
            safe = item.replace("`", "'").replace('"', "'")
            if len(safe) > 150:
                safe = safe[:150] + "..."
            findings_section.append(f"      - `{safe}`")
        findings_section.append("")

    findings_text = "\n".join(findings_section) if findings_section else "      No specific patterns detected."

    test_section = ""
    if all_failed_tests:
        test_lines = "\n".join(f"      - `{t}`" for t in all_failed_tests[:15])
        test_section = f"""
      ## Failed Tests
{test_lines}
"""

    code_section = ""
    if all_error_codes:
        code_lines = "\n".join(f"      - `{c}`" for c in all_error_codes[:15])
        code_section = f"""
      ## Error Codes
{code_lines}
"""

    yaml = f"""name: Workflow Results - {workflow_name}
description: Address issues found in the {workflow_name} workflow run
# Auto-generated prompt from workflow run analysis
# Generated: {now}
# Source: {run_url}
# Run conclusion: {run_conclusion}
# Model-agnostic prompt - works with any capable LLM
messages:
  - role: system
    content: |
      You are a senior .NET developer analyzing CI/CD workflow results for the Market Data Collector project.
      The `{workflow_name}` workflow has completed with status: **{run_conclusion}**.

      ## Project Context
      Market Data Collector is a .NET 9.0 C#/F# market data collection system.
      - Build: `dotnet build -c Release`
      - Test: `dotnet test tests/MarketDataCollector.Tests`
      - 14 main projects, 105 test files

      ## Findings from Workflow Run
{findings_text}
{test_section}{code_section}
      ## Resolution Guidelines
      1. Prioritize failures by severity (errors > warnings)
      2. Check docs/ai/ai-known-errors.md for recurring patterns
      3. Fix root causes, not symptoms
      4. Follow project conventions (CLAUDE.md)
      5. Run verification commands before considering resolved

  - role: user
    content: |
      The `{workflow_name}` CI workflow needs attention. Analyze the results and help fix the issues.

      Workflow run: {run_url}

      Additional context:
      {{{{additional_context}}}}

      Please provide:
      1. Summary of all issues found
      2. Prioritized fix plan
      3. Code changes needed (with file paths)
      4. Verification steps
"""
    return yaml


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Auto-generate AI prompts from workflow run results"
    )
    parser.add_argument(
        "--workflow",
        required=True,
        help="Workflow file name (e.g., test-matrix.yml) or workflow name",
    )
    parser.add_argument(
        "--run-id",
        type=int,
        default=0,
        help="Specific workflow run ID (0 = use most recent failed run)",
    )
    parser.add_argument(
        "--output",
        default=str(PROMPT_DIR),
        help="Output directory for generated prompts",
    )
    parser.add_argument(
        "--json-output",
        default="prompt-generation-results.json",
        help="JSON output file with generation results",
    )
    parser.add_argument(
        "--dry-run",
        action="store_true",
        help="Show what would be generated without writing files",
    )
    parser.add_argument(
        "--summary",
        action="store_true",
        help="Print summary to stdout (for GITHUB_STEP_SUMMARY)",
    )
    args = parser.parse_args()

    output_dir = Path(args.output)
    output_dir.mkdir(parents=True, exist_ok=True)

    repo = get_repo()
    if not repo:
        print("Could not determine repository. Set GITHUB_REPOSITORY.", file=sys.stderr)
        return 1

    # Ensure workflow name ends with .yml
    workflow = args.workflow
    if not workflow.endswith(".yml") and not workflow.endswith(".yaml"):
        workflow = f"{workflow}.yml"

    print(f"Analyzing workflow: {workflow} in {repo}")

    # --- Fetch workflow run ---
    run_id = args.run_id
    run_url = ""
    run_conclusion = "unknown"

    if run_id == 0:
        # Find most recent completed run (prefer failed)
        runs = fetch_workflow_runs(repo, workflow, count=10)
        if not runs:
            print(f"No runs found for workflow {workflow}")
            # Still generate a template prompt based on workflow name
            run_url = f"https://github.com/{repo}/actions/workflows/{workflow}"
        else:
            # Prefer failed/failure runs, fall back to most recent
            failed_runs = [r for r in runs if r.get("conclusion") in ("failure", "timed_out")]
            target_run = failed_runs[0] if failed_runs else runs[0]
            run_id = target_run["id"]
            run_url = target_run.get("html_url", "")
            run_conclusion = target_run.get("conclusion", "unknown")
            print(f"Using run #{run_id} (conclusion: {run_conclusion})")
    else:
        run_url = f"https://github.com/{repo}/actions/runs/{run_id}"

    # --- Collect data ---
    all_logs = ""
    all_annotations: list[dict[str, Any]] = []
    failed_job_names: list[str] = []

    if run_id:
        jobs = fetch_run_jobs(repo, run_id)
        failed_jobs = [j for j in jobs if j.get("conclusion") == "failure"]

        if not failed_jobs:
            # All jobs passed - use all jobs for context
            failed_jobs = jobs

        for job in failed_jobs:
            job_name = job.get("name", "unknown")
            job_id = job.get("id", 0)
            conclusion = job.get("conclusion", "unknown")

            if conclusion == "failure":
                failed_job_names.append(job_name)

            if job_id:
                log = fetch_job_logs(repo, job_id)
                if log:
                    all_logs += f"\n--- Job: {job_name} ---\n{log}\n"

        all_annotations = fetch_run_annotations(repo, run_id)

    # --- Analyze ---
    findings = classify_failures(all_logs, all_annotations)
    failed_tests = extract_failed_tests(all_logs)
    error_codes = extract_error_codes(all_logs)

    existing_prompts = read_existing_prompts(output_dir)
    existing_stems = {p["stem"] for p in existing_prompts}

    # --- Generate prompts ---
    generated: list[dict[str, Any]] = []

    # Generate category-specific prompts for each finding type
    for category, category_findings in findings.items():
        info = FAILURE_CATEGORIES[category]
        prompt_name = info["prompt_name"]

        # Extract category-specific error codes
        cat_codes = []
        if category == "build":
            cat_codes = [c for c in error_codes if c.startswith(("CS", "NU", "NETSDK"))]
        elif category == "code-quality":
            cat_codes = [c for c in error_codes if c.startswith(("CA", "SA", "IDE"))]
        else:
            cat_codes = error_codes

        cat_tests = failed_tests if category == "test" else []

        prompt_content = generate_prompt_yaml(
            name=info["description"].title(),
            description=info["description"],
            category=category,
            findings=category_findings,
            failed_tests=cat_tests,
            error_codes=cat_codes,
            workflow_name=workflow.replace(".yml", ""),
            run_url=run_url,
        )

        filename = f"{prompt_name}{PROMPT_EXT}"
        filepath = output_dir / filename

        action = "update" if prompt_name in existing_stems else "create"

        if not args.dry_run:
            filepath.write_text(prompt_content, encoding="utf-8")
            print(f"  [{action.upper()}] {filepath}")
        else:
            print(f"  [DRY-RUN {action.upper()}] {filepath}")

        generated.append(
            {
                "file": filename,
                "category": category,
                "action": action,
                "findings_count": len(category_findings),
                "error_codes": cat_codes,
                "failed_tests": cat_tests,
            }
        )

    # Always generate a workflow-level summary prompt
    workflow_stem = workflow.replace(".yml", "").replace(".yaml", "")
    summary_name = f"workflow-results-{workflow_stem}"
    summary_filename = f"{summary_name}{PROMPT_EXT}"
    summary_path = output_dir / summary_filename

    summary_content = generate_workflow_summary_prompt(
        workflow_name=workflow_stem,
        run_url=run_url,
        all_findings=findings,
        all_failed_tests=failed_tests,
        all_error_codes=error_codes,
        run_conclusion=run_conclusion,
    )

    action = "update" if summary_name in existing_stems else "create"
    if not args.dry_run:
        summary_path.write_text(summary_content, encoding="utf-8")
        print(f"  [{action.upper()}] {summary_path}")
    else:
        print(f"  [DRY-RUN {action.upper()}] {summary_path}")

    generated.append(
        {
            "file": summary_filename,
            "category": "summary",
            "action": action,
            "findings_count": sum(len(v) for v in findings.values()),
            "error_codes": error_codes,
            "failed_tests": failed_tests,
        }
    )

    # --- JSON output ---
    results = {
        "generated_at": datetime.now(timezone.utc).isoformat(),
        "workflow": workflow,
        "run_id": run_id,
        "run_url": run_url,
        "run_conclusion": run_conclusion,
        "repository": repo,
        "failed_jobs": failed_job_names,
        "findings_by_category": {k: len(v) for k, v in findings.items()},
        "total_error_codes": len(error_codes),
        "total_failed_tests": len(failed_tests),
        "existing_prompts": len(existing_prompts),
        "generated_prompts": generated,
        "total_generated": len(generated),
    }

    json_path = Path(args.json_output)
    if not args.dry_run:
        json_path.write_text(
            json.dumps(results, indent=2, default=str), encoding="utf-8"
        )
        print(f"\nResults written to {json_path}")

    # --- Summary ---
    if args.summary:
        print(f"\n## Prompt Generation Results")
        print(f"")
        print(f"**Workflow:** `{workflow}`")
        print(f"**Run:** #{run_id} ({run_conclusion})")
        print(f"**Repository:** {repo}")
        print(f"")
        print(f"| Metric | Value |")
        print(f"|--------|-------|")
        print(f"| Failure categories found | {len(findings)} |")
        print(f"| Error codes extracted | {len(error_codes)} |")
        print(f"| Failed tests found | {len(failed_tests)} |")
        print(f"| Prompts generated/updated | {len(generated)} |")
        print(f"| Existing prompts | {len(existing_prompts)} |")
        print(f"")

        if generated:
            print(f"### Generated Prompts")
            print(f"")
            print(f"| File | Category | Action | Findings |")
            print(f"|------|----------|--------|----------|")
            for g in generated:
                print(
                    f"| `{g['file']}` | {g['category']} | {g['action']} | {g['findings_count']} |"
                )

        if failed_job_names:
            print(f"")
            print(f"### Failed Jobs")
            print(f"")
            for name in failed_job_names:
                print(f"- {name}")

    print(f"\nDone. Generated {len(generated)} prompts.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
