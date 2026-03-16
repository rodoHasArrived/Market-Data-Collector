"""Code-defined skills provider for MarketDataCollector.

Creates a :class:`SkillsProvider` that exposes two skills:

1. ``mdc-code-review`` — Code review and architecture compliance, available
   both as a file-based skill (discovered from the ``mdc-code-review/``
   directory) and as a code-defined skill.  When both exist the file-based
   version takes precedence; the code-defined definition acts as a fallback
   and hosts **dynamic resources** and **in-process scripts**.

2. ``ai-docs-maintain`` — AI documentation maintenance (freshness checks,
   drift detection, cross-reference validation, stale doc archiving).
   Purely code-defined with scripts that delegate to
   ``build/scripts/docs/ai-docs-maintenance.py``.

Usage (from a custom agent or MCP server)::

    from .claude.skills.skills_provider import skills_provider

    # Load the skill instructions
    instructions = skills_provider.load_skill("mdc-code-review")

    # Read a static resource
    arch = skills_provider.read_skill_resource("mdc-code-review", "architecture")

    # Read a dynamic resource (re-evaluated on every call)
    stats = skills_provider.read_skill_resource("mdc-code-review", "project-stats")

    # Execute a code-defined script
    result = skills_provider.run_skill_script("mdc-code-review", "validate-skill")

    # AI docs maintenance
    report = skills_provider.run_skill_script("ai-docs-maintain", "run-full")
"""

from __future__ import annotations

import subprocess
import sys
from pathlib import Path
from textwrap import dedent
from typing import Any

from agent_framework import Skill, SkillResource, SkillScript, SkillsProvider

# ---------------------------------------------------------------------------
# Path helpers
# ---------------------------------------------------------------------------

_SKILLS_DIR = Path(__file__).parent
_SKILL_DIR = _SKILLS_DIR / "mdc-code-review"
_REFS_DIR = _SKILL_DIR / "references"
_REPO_ROOT = _SKILLS_DIR.parent.parent


def _read(path: Path) -> str:
    """Read *path* as UTF-8 text; return empty string on any I/O error."""
    try:
        return path.read_text(encoding="utf-8")
    except OSError:
        return ""


# ---------------------------------------------------------------------------
# Skill definition
# ---------------------------------------------------------------------------

mdc_code_review_skill = Skill(
    name="mdc-code-review",
    description=dedent("""\
        Code review and architecture compliance skill for the MarketDataCollector
        project — a .NET 9 / C# 13 market data system with WPF desktop app, F# 8.0
        domain models, real-time streaming pipelines, and tiered JSONL/Parquet
        storage. Use this skill whenever the user asks to review, audit, refactor,
        or improve C# or F# code from MarketDataCollector, or when they share
        .cs/.fs files and want feedback.
        Also trigger on: MVVM compliance, ViewModel extraction, code-behind cleanup,
        real-time performance, hot-path optimization, pipeline throughput, provider
        implementation review, backfill logic, data integrity validation, error
        handling patterns, test code quality, unit test review, ProviderSdk
        compliance, dependency violations, JSON source generator usage, hot config
        reload, or WPF architecture — even without naming the project. If code
        references MarketDataCollector namespaces, BindableBase, EventPipeline,
        IMarketDataClient, IStorageSink, or ProviderSdk types, use this skill.
    """),
    # Full skill instructions — identical to what is in SKILL.md so the
    # code-defined version is self-contained when the file is absent.
    content=_read(_SKILL_DIR / "SKILL.md"),
    resources=[
        # ------------------------------------------------------------------
        # Static reference resources bundled alongside the skill
        # ------------------------------------------------------------------
        SkillResource(
            name="architecture",
            description=(
                "Deep project context: solution layout (all 10 projects), "
                "expanded dependency graph, provider/backfill architecture, "
                "F# interop rules, testing conventions, and ADR quick-reference. "
                "Read when you need more detail than the SKILL.md summary."
            ),
            content=_read(_REFS_DIR / "architecture.md"),
        ),
        SkillResource(
            name="schemas",
            description=(
                "JSON schemas for evals.json, grading.json, benchmark.json, "
                "and timing.json. Read when generating or validating eval artifacts."
            ),
            content=_read(_REFS_DIR / "schemas.md"),
        ),
        SkillResource(
            name="grader",
            description=(
                "Assertions grader instructions for evaluating skill outputs against "
                "the test cases in evals.json. Read when grading eval runs."
            ),
            content=_read(_SKILL_DIR / "agents" / "grader.md"),
        ),
        SkillResource(
            name="evals",
            description=(
                "Eval set with 8 test cases and assertions for testing this skill. "
                "Read when running or inspecting skill evaluations."
            ),
            content=_read(_SKILL_DIR / "evals" / "evals.json"),
        ),
    ],
)

# ---------------------------------------------------------------------------
# AI Documentation Maintenance skill
# ---------------------------------------------------------------------------

ai_docs_maintain_skill = Skill(
    name="ai-docs-maintain",
    description=dedent("""\
        AI documentation maintenance skill for the MarketDataCollector project.
        Use this skill when the user asks to check AI doc freshness, detect drift
        between documentation and code, archive stale docs, validate cross-references,
        or generate a sync report for AI-related files. Also trigger when asked to
        "update AI docs", "check doc staleness", "archive deprecated docs", or
        "sync AI instructions".
    """),
    content=dedent("""\
        # AI Documentation Maintenance

        This skill maintains the health of AI assistant documentation in the
        MarketDataCollector repository.

        ## Available Commands

        Run via the `ai-docs-maintenance.py` script:

        ```bash
        # Check staleness of all AI docs
        python3 build/scripts/docs/ai-docs-maintenance.py freshness

        # Detect drift between docs and code reality
        python3 build/scripts/docs/ai-docs-maintenance.py drift

        # Preview stale docs for archiving
        python3 build/scripts/docs/ai-docs-maintenance.py archive-stale

        # Validate cross-references between AI docs
        python3 build/scripts/docs/ai-docs-maintenance.py validate-refs

        # Generate a full sync report (markdown)
        python3 build/scripts/docs/ai-docs-maintenance.py sync-report --output docs/generated/ai-docs-sync-report.md

        # Run all checks
        python3 build/scripts/docs/ai-docs-maintenance.py full --json-output /tmp/ai-docs-report.json
        ```

        Or via Makefile targets:

        ```bash
        make ai-docs-freshness      # Check AI doc freshness
        make ai-docs-drift          # Detect doc/code drift
        make ai-docs-sync-report    # Generate sync report
        make ai-docs-archive        # Preview archive candidates
        make ai-docs-archive-execute # Actually archive stale docs
        make ai-audit-ai-docs       # Integrated audit via ai-repo-updater
        ```

        ## Workflow

        1. Run `freshness` to find stale docs (>60 days warning, >120 days critical)
        2. Run `drift` to find where docs diverge from code (provider counts, workflow counts, file counts)
        3. Fix stale/drifted docs by updating content and timestamps
        4. Run `archive-stale` to identify deprecated content for archiving
        5. Run `validate-refs` to check for broken cross-references
        6. Generate a `sync-report` for human review

        ## Key Files

        - Script: `build/scripts/docs/ai-docs-maintenance.py`
        - Integrated auditor: `build/scripts/ai-repo-updater.py` (command: `audit-ai-docs`)
        - Master AI index: `docs/ai/README.md`
        - Root context: `CLAUDE.md`
    """),
    resources=[],
)


_AI_DOCS_SCRIPT = _REPO_ROOT / "build" / "scripts" / "docs" / "ai-docs-maintenance.py"


def _run_ai_docs_cmd(command: str, extra_args: list[str] | None = None,
                     timeout: int = 30) -> str:
    """Run an ai-docs-maintenance.py command and return output.

    Exit codes: 0 = clean, 1 = findings exist (still returns valid JSON),
    2 = script error.
    """
    cmd = [sys.executable, str(_AI_DOCS_SCRIPT), command]
    if extra_args:
        cmd.extend(extra_args)
    try:
        result = subprocess.run(
            cmd, capture_output=True, text=True,
            cwd=str(_REPO_ROOT), timeout=timeout,
        )
        # Exit code 0 (clean) and 1 (findings) both produce valid output
        if result.returncode <= 1:
            return result.stdout.strip() or result.stderr.strip()
        # Exit code 2 = script error
        return f"Error (exit {result.returncode}): {result.stderr.strip()}"
    except (subprocess.SubprocessError, OSError) as exc:
        return f"Error: {exc}"


@ai_docs_maintain_skill.resource(
    name="doc-health-summary",
    description=(
        "Live AI documentation health summary: stale doc count, drift items, "
        "and broken references. Refreshed on every read."
    ),
)
def doc_health_summary() -> Any:
    """Return current AI doc health status from the maintenance script."""
    import json as _json
    raw = _run_ai_docs_cmd("full", timeout=60)
    try:
        data = _json.loads(raw)
        s = data.get("summary", {})
        lines = [
            f"Stale docs    : {s.get('stale_docs', 0)}",
            f"Critical      : {s.get('critical', 0)}",
            f"Warnings      : {s.get('warning', 0)}",
            f"Drift items   : {s.get('drift_items', 0)}",
            f"Info          : {s.get('info', 0)}",
        ]
        return "\n".join(lines)
    except (_json.JSONDecodeError, KeyError):
        return raw


@ai_docs_maintain_skill.script(
    name="run-freshness",
    description="Check staleness of all AI documentation files. Returns JSON report.",
)
def run_freshness_script() -> str:
    """Execute ai-docs-maintenance.py freshness check."""
    return _run_ai_docs_cmd("freshness")


@ai_docs_maintain_skill.script(
    name="run-drift",
    description="Detect where AI documentation diverges from code reality. Returns JSON report.",
)
def run_drift_script() -> str:
    """Execute ai-docs-maintenance.py drift check."""
    return _run_ai_docs_cmd("drift")


@ai_docs_maintain_skill.script(
    name="run-full",
    description="Run all AI doc maintenance checks (freshness, drift, refs, archive). Returns JSON.",
)
def run_full_script() -> str:
    """Execute ai-docs-maintenance.py full check."""
    return _run_ai_docs_cmd("full", timeout=60)


@ai_docs_maintain_skill.script(
    name="run-archive",
    description=(
        "Find deprecated docs that should be archived. "
        "Set execute=True to actually move files (default: dry-run preview only)."
    ),
)
def run_archive_script(execute: bool = False) -> str:
    """Execute ai-docs-maintenance.py archive-stale with optional execution."""
    extra = ["--execute"] if execute else []
    return _run_ai_docs_cmd("archive-stale", extra_args=extra)


# ---------------------------------------------------------------------------
# Dynamic resources — re-evaluated on every read
# ---------------------------------------------------------------------------


@mdc_code_review_skill.resource(
    name="project-stats",
    description=(
        "Live project statistics: source file counts by language and test file "
        "count, derived from the current filesystem state. Refreshed on every read."
    ),
)
def project_stats() -> Any:
    """Return current source-file and test-file statistics for the repository."""
    repo_root = _REPO_ROOT

    def _count(directory: str, pattern: str) -> int:
        try:
            result = subprocess.run(
                [
                    "find",
                    directory,
                    "-name",
                    pattern,
                    "-not",
                    "-path",
                    "*/obj/*",
                    "-not",
                    "-path",
                    "*/bin/*",
                ],
                capture_output=True,
                text=True,
                timeout=10,
            )
            return len(result.stdout.strip().splitlines())
        except (subprocess.SubprocessError, subprocess.TimeoutExpired, OSError):
            return 0

    src = str(repo_root / "src")
    tests = str(repo_root / "tests")
    cs_src = _count(src, "*.cs")
    fs_src = _count(src, "*.fs")
    cs_tests = _count(tests, "*.cs")

    return dedent(f"""\
        Repository root : {repo_root}
        Source files (src/):
          C# (.cs) : {cs_src}
          F# (.fs) : {fs_src}
        Test files (tests/):
          C# (.cs) : {cs_tests}
    """)


@mdc_code_review_skill.resource(
    name="git-context",
    description=(
        "Current git branch, the latest commit touching src/ or tests/, and the "
        "list of files changed in that commit. Refreshed on every read."
    ),
)
def git_context() -> Any:
    """Return current git context: branch, last relevant commit, changed files."""
    repo_root = _REPO_ROOT

    def _git(*args: str) -> str:
        try:
            result = subprocess.run(
                ["git", *args],
                capture_output=True,
                text=True,
                cwd=str(repo_root),
                timeout=10,
            )
            return result.stdout.strip()
        except (subprocess.SubprocessError, subprocess.TimeoutExpired, OSError):
            return "(unavailable)"

    branch = _git("rev-parse", "--abbrev-ref", "HEAD")
    last_commit = _git(
        "log", "-1", "--pretty=format:%h %s (%cr)", "--", "src/", "tests/"
    )
    raw_changed = _git("diff", "--name-only", "HEAD~1", "HEAD", "--", "src/", "tests/")

    lines = [
        f"Branch      : {branch}",
        f"Last commit : {last_commit}",
    ]
    if raw_changed:
        changed_files = raw_changed.splitlines()
        lines.append(f"Changed files in last commit ({len(changed_files)}):")
        for f in changed_files[:20]:
            lines.append(f"  {f}")
        if len(changed_files) > 20:
            lines.append(f"  … and {len(changed_files) - 20} more")

    return "\n".join(lines)


# ---------------------------------------------------------------------------
# Code-defined scripts — run in-process
# ---------------------------------------------------------------------------


@mdc_code_review_skill.script(
    name="validate-skill",
    description=(
        "Validate the mdc-code-review SKILL.md frontmatter and directory "
        "structure. Returns 'OK' on success or a failure message."
    ),
)
def validate_skill_script() -> str:
    """Validate the skill definition via quick_validate.py."""
    # Import the validation helper from the skill's scripts package.
    # We add the mdc-code-review directory to sys.path so that the
    # ``from scripts.quick_validate import …`` import inside package_skill.py
    # keeps working too.
    _scripts_parent = str(_SKILL_DIR)
    if _scripts_parent not in sys.path:
        sys.path.insert(0, _scripts_parent)

    try:
        from scripts.quick_validate import validate_skill  # type: ignore[import]

        valid, message = validate_skill(str(_SKILL_DIR))
        return message
    except ImportError:
        # Fallback: minimal structural check without PyYAML.
        skill_md = _SKILL_DIR / "SKILL.md"
        if not skill_md.exists():
            return "FAIL: SKILL.md not found"
        content = skill_md.read_text(encoding="utf-8")
        if not content.startswith("---"):
            return "FAIL: SKILL.md is missing YAML frontmatter"
        return "OK: SKILL.md exists and starts with frontmatter"


@mdc_code_review_skill.script(
    name="run-eval",
    description=(
        "Run the trigger-evaluation suite (evals/evals.json) to measure how "
        "reliably the skill description causes Claude to invoke this skill. "
        "Accepts optional ``description`` (override the description under test) "
        "and ``runs_per_query`` (default 3)."
    ),
)
def run_eval_script(description: str = "", runs_per_query: int = 3) -> str:
    """Execute run_eval.py for the bundled eval set."""
    evals_file = _SKILL_DIR / "evals" / "evals.json"

    cmd = [
        sys.executable,
        "-m",
        "scripts.run_eval",
        "--eval-set",
        str(evals_file),
        "--skill-path",
        str(_SKILL_DIR),
        "--runs-per-query",
        str(runs_per_query),
        "--verbose",
    ]
    if description:
        cmd.extend(["--description", description])

    try:
        result = subprocess.run(
            cmd,
            capture_output=True,
            text=True,
            cwd=str(_SKILL_DIR),  # scripts/ is a package relative to mdc-code-review/
            timeout=300,
        )
        output = result.stdout.strip()
        if result.returncode != 0 and not output:
            return f"Error (exit {result.returncode}): {result.stderr.strip()}"
        return output
    except subprocess.TimeoutExpired:
        return "Error: eval timed out after 300 s"
    except Exception as exc:
        return f"Error: {exc}"


@mdc_code_review_skill.script(
    name="aggregate-benchmark",
    description=(
        "Aggregate grading results from a workspace directory into "
        "benchmark.json and benchmark.md summary files. "
        "Requires ``workspace`` (path to the benchmark directory) and an "
        "optional ``skill_name`` override (default: mdc-code-review)."
    ),
)
def aggregate_benchmark_script(workspace: str, skill_name: str = "mdc-code-review") -> str:
    """Execute aggregate_benchmark.py for a given workspace directory."""
    cmd = [
        sys.executable,
        "-m",
        "scripts.aggregate_benchmark",
        workspace,
        "--skill-name",
        skill_name,
    ]

    try:
        result = subprocess.run(
            cmd,
            capture_output=True,
            text=True,
            cwd=str(_SKILL_DIR),
            timeout=60,
        )
        output = result.stdout.strip()
        if result.returncode != 0 and not output:
            return f"Error (exit {result.returncode}): {result.stderr.strip()}"
        return output or "Done."
    except subprocess.TimeoutExpired:
        return "Error: aggregate-benchmark timed out after 60 s"
    except Exception as exc:
        return f"Error: {exc}"


# ---------------------------------------------------------------------------
# Script runner for file-based scripts (subprocess execution)
# ---------------------------------------------------------------------------


def _skill_script_runner(
    skill: Skill, script: SkillScript, args: dict[str, Any] | None = None
) -> str:
    """Execute a file-based skill script as a Python subprocess.

    The runner is intentionally minimal: it validates that the script file
    exists, builds a ``python <script_path> [--key value …]`` command from
    the *args* dict, and captures stdout.  It does **not** use a shell, so
    no shell expansion or injection is possible.

    Parameters
    ----------
    skill:
        The resolved :class:`Skill` whose ``path`` attribute points to the
        skill directory on disk.
    script:
        The :class:`SkillScript` being executed; its ``path`` attribute is
        relative to the skill directory.
    args:
        Optional mapping of argument name → value.  Each entry is appended
        as ``--<name> <value>`` (``None`` values are skipped).
    """
    if skill.path is None:
        raise ValueError(
            f"Skill '{skill.name}' has no filesystem path — "
            "cannot execute a file-based script"
        )

    script_path = Path(skill.path) / script.path
    if not script_path.exists():
        raise FileNotFoundError(f"Script not found: {script_path}")

    cmd: list[str] = [sys.executable, str(script_path)]
    if args:
        for key, value in args.items():
            if value is not None:
                cmd.extend([f"--{key}", str(value)])

    result = subprocess.run(
        cmd,
        capture_output=True,
        text=True,
        cwd=str(_REPO_ROOT),
        timeout=300,
    )

    if result.returncode != 0:
        raise RuntimeError(
            f"Script exited with code {result.returncode}:\n{result.stderr.strip()}"
        )

    return result.stdout.strip()


# ---------------------------------------------------------------------------
# Skills provider
# ---------------------------------------------------------------------------

skills_provider = SkillsProvider(
    # Discover file-based skills from the mdc-code-review/ directory.
    # File-based skills take precedence over code-defined ones with the
    # same name, so the Skill instance above serves as a self-contained
    # fallback and as the host for dynamic resources and in-process scripts.
    skill_paths=_SKILLS_DIR,
    # Register the code-defined skill.
    skills=[mdc_code_review_skill, ai_docs_maintain_skill],
    # Provide a runner for any file-based .py scripts that may be added to
    # skill directories in the future.
    script_runner=_skill_script_runner,
)
