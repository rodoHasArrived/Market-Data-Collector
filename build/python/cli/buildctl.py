#!/usr/bin/env python3
import argparse
import json
import os
import subprocess
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SYS_PATH_ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(SYS_PATH_ROOT))

from adapters.dotnet import DotnetAdapter
from analytics.history import BuildHistory, BuildRecord
from analytics.metrics import MetricsCollector
from analytics.profile import print_profile
from core.events import BuildEventEmitter
from core.fingerprint import BuildFingerprint
from core.graph import DependencyGraph
from core.utils import colorize, ensure_directory, human_duration, iso_timestamp
from diagnostics.doctor import run_doctor
from diagnostics.env_diff import capture_environment, diff_envs
from diagnostics.error_matcher import ErrorMatcher
from diagnostics.preflight import run_preflight
from diagnostics.validate_data import validate_directory


DEFAULT_PROJECT = "src/MarketDataCollector/MarketDataCollector.csproj"
DEFAULT_TEST_PROJECT = "tests/MarketDataCollector.Tests/MarketDataCollector.Tests.csproj"


def run_build(args: argparse.Namespace) -> int:
    output_dir = ROOT / ".build-system"
    logs_dir = output_dir / "logs"
    ensure_directory(logs_dir)
    log_file = logs_dir / f"build-{time.strftime('%Y%m%d-%H%M%S')}.log"
    emitter = BuildEventEmitter(output_dir, args.verbosity)

    if not args.skip_preflight:
        ok, messages = run_preflight(ROOT)
        if not ok and not args.force:
            emitter.emit(
                "preflight",
                args.project,
                "failed",
                error_message="; ".join(messages),
                tags=["preflight"],
            )
            return 2
        if not ok:
            emitter.emit(
                "preflight",
                args.project,
                "warning",
                error_message="; ".join(messages),
                tags=["preflight"],
            )
        else:
            emitter.emit("preflight", args.project, "completed", tags=["preflight"])

    fingerprint = BuildFingerprint(ROOT).write(output_dir / "build-fingerprint.json", args.configuration)
    emitter.emit("fingerprint", args.project, "completed", context={"fingerprint": fingerprint["fingerprint"]})

    adapter = DotnetAdapter(ROOT, log_file, args.verbosity in {"verbose", "debug"})
    phases = [
        ("restore", adapter.restore, [args.project]),
        ("build", adapter.build, [args.project, args.configuration]),
    ]
    if args.with_tests:
        phases.append(("test", adapter.test, [args.test_project]))

    phase_durations: dict[str, int] = {}
    phase_success: dict[str, bool] = {}
    success = True
    start_total = time.time()
    error_output = ""

    for phase, func, parameters in phases:
        emitter.emit(phase, args.project, "started", tags=[phase])
        exit_code, output, duration = func(*parameters)
        duration_ms = int(duration * 1000)
        phase_durations[phase] = duration_ms
        if exit_code == 0:
            emitter.emit(phase, args.project, "completed", duration_ms=duration_ms, tags=[phase])
            phase_success[phase] = True
        else:
            success = False
            error_output = output
            emitter.emit(
                phase,
                args.project,
                "failed",
                duration_ms=duration_ms,
                error_code=f"exit-{exit_code}",
                error_message="Build phase failed",
                tags=[phase],
            )
            phase_success[phase] = False
            break

    total_duration_ms = int((time.time() - start_total) * 1000)
    emitter.emit(
        "build",
        args.project,
        "completed" if success else "failed",
        duration_ms=total_duration_ms,
        context={"log": str(log_file)},
        tags=["summary"],
    )

    metrics = MetricsCollector(output_dir)
    metrics.record("build.duration_ms", total_duration_ms)
    for phase, duration in phase_durations.items():
        metrics.record(f"build.phase.{phase}.duration_ms", duration)
    metrics.record("build.success", 1 if success else 0)
    metrics.write()

    git_sha, git_branch = git_info()
    history = BuildHistory(output_dir / "history.db")
    record = BuildRecord(
        fingerprint=fingerprint["fingerprint"],
        timestamp=iso_timestamp(),
        duration_ms=total_duration_ms,
        success=success,
        git_sha=git_sha,
        git_branch=git_branch,
        warnings_count=0,
        errors_count=0 if success else 1,
        configuration=args.configuration,
    )
    build_id = history.record_build(record)
    for phase, duration in phase_durations.items():
        history.record_phase(build_id, phase, duration, phase_success.get(phase, True))

    anomaly_warning(history, total_duration_ms)

    if not success:
        matcher = ErrorMatcher(ROOT / "build-system" / "knowledge" / "errors")
        matches = matcher.match(error_output)
        matcher.print_matches(matches)
        return 1

    if args.generate_graph:
        graph = DependencyGraph(ROOT, args.project)
        graph.generate(output_dir)

    print(colorize(f"Build completed in {human_duration(total_duration_ms / 1000)}", "0;32"))
    return 0


def run_build_graph(args: argparse.Namespace) -> int:
    output_dir = ROOT / ".build-system"
    graph = DependencyGraph(ROOT, args.project)
    data = graph.generate(output_dir)
    if "error" in data:
        print(colorize("Dependency graph generation failed", "0;31"))
        print(data.get("details", ""))
        return 1
    print(colorize("Dependency graph generated", "0;32"))
    print(f"JSON: {output_dir / 'dependency-graph.json'}")
    print(f"DOT:  {output_dir / 'dependency-graph.dot'}")
    return 0


def run_fingerprint(args: argparse.Namespace) -> int:
    output_dir = ROOT / ".build-system"
    fingerprint = BuildFingerprint(ROOT).write(output_dir / "build-fingerprint.json", args.configuration)
    print(json.dumps(fingerprint, indent=2))
    return 0


def run_doctor_cmd(args: argparse.Namespace) -> int:
    return run_doctor(ROOT, args.quick, args.json)


def run_collect_debug(args: argparse.Namespace) -> int:
    output_dir = ROOT / ".build-system"
    ensure_directory(output_dir)
    env_path = capture_environment(ROOT, "current")
    DependencyGraph(ROOT, args.project).generate(output_dir)
    BuildFingerprint(ROOT).write(output_dir / "build-fingerprint.json", args.configuration)
    bundle_name = f"debug-bundle-{time.strftime('%Y%m%d-%H%M%S')}.zip"
    bundle_path = output_dir / bundle_name
    import zipfile

    with zipfile.ZipFile(bundle_path, "w") as archive:
        for path in output_dir.glob("**/*"):
            if path.is_file() and path != bundle_path:
                archive.write(path, path.relative_to(output_dir))
        archive.write(env_path, env_path.relative_to(output_dir))
    print(colorize(f"Debug bundle created: {bundle_path}", "0;32"))
    return 0


def run_env_capture(args: argparse.Namespace) -> int:
    path = capture_environment(ROOT, args.name)
    print(colorize(f"Environment snapshot saved: {path}", "0;32"))
    return 0


def run_env_diff(args: argparse.Namespace) -> int:
    env_dir = ROOT / ".build-system" / "envs"
    left_path = env_dir / f"{args.left}.json"
    right_path = env_dir / f"{args.right}.json"
    if not left_path.exists() or not right_path.exists():
        print(colorize("One or both environment snapshots are missing. Use env-capture first.", "0;31"))
        return 1
    left = json.loads(left_path.read_text(encoding="utf-8"))
    right = json.loads(right_path.read_text(encoding="utf-8"))
    diff = diff_envs(left, right)
    print(json.dumps(diff, indent=2))
    return 0


def run_profile(args: argparse.Namespace) -> int:
    history = BuildHistory(ROOT / ".build-system" / "history.db")
    recent = history.recent_builds(limit=1)
    if not recent:
        print("No build history found.")
        return 1
    phase_data = {}
    with sqlite_conn(history.db_path) as conn:
        rows = conn.execute(
            "SELECT phase, duration_ms FROM build_phases ORDER BY rowid DESC LIMIT 10"
        ).fetchall()
    for phase, duration in rows:
        phase_data[phase] = duration
    print_profile(phase_data)
    return 0


def run_metrics(args: argparse.Namespace) -> int:
    metrics_path = ROOT / ".build-system" / "metrics.json"
    if metrics_path.exists():
        print(metrics_path.read_text(encoding="utf-8"))
        return 0
    print(colorize("No metrics found. Run make build first.", "1;33"))
    return 1


def run_history(args: argparse.Namespace) -> int:
    history = BuildHistory(ROOT / ".build-system" / "history.db")
    for timestamp, duration, success in history.recent_builds(limit=10):
        status = "SUCCESS" if success else "FAIL"
        print(f"{timestamp} {duration}ms {status}")
    return 0


def run_impact(args: argparse.Namespace) -> int:
    file_path = Path(args.file)
    if not file_path.is_absolute():
        file_path = ROOT / file_path
    project = find_project(file_path)
    if not project:
        print(colorize("No project found for file.", "1;33"))
        return 1
    print(colorize("Impact Analysis", "0;36"))
    print(f"File: {file_path}")
    print(f"Project: {project}")
    print("Suggested build scope:")
    print(f"  - dotnet build {project}")
    if "Tests" in DEFAULT_TEST_PROJECT:
        print(f"  - dotnet test {DEFAULT_TEST_PROJECT}")
    return 0


def run_bisect(args: argparse.Namespace) -> int:
    command = args.command or f"python3 {ROOT / 'build-system' / 'cli' / 'buildctl.py'} build"
    subprocess.run(["git", "bisect", "start", args.bad, args.good], cwd=str(ROOT), check=False)
    result = subprocess.run(["git", "bisect", "run", "bash", "-c", command], cwd=str(ROOT), check=False)
    subprocess.run(["git", "bisect", "reset"], cwd=str(ROOT), check=False)
    return result.returncode


def run_analyze_errors(args: argparse.Namespace) -> int:
    matcher = ErrorMatcher(ROOT / "build-system" / "knowledge" / "errors")
    if args.log and Path(args.log).exists():
        output = Path(args.log).read_text(encoding="utf-8")
    else:
        output = sys.stdin.read()
    matches = matcher.match(output)
    matcher.print_matches(matches)
    return 0


def run_validate_data(args: argparse.Namespace) -> int:
    data_dir = Path(args.directory)
    if not data_dir.is_absolute():
        data_dir = ROOT / data_dir
    return validate_directory(data_dir)


def git_info() -> tuple[str, str]:
    sha = run_git(["rev-parse", "HEAD"])
    branch = run_git(["rev-parse", "--abbrev-ref", "HEAD"])
    return sha, branch


def run_git(args: list[str]) -> str:
    result = subprocess.run(["git", *args], cwd=str(ROOT), capture_output=True, text=True, check=False)
    return result.stdout.strip() or "unknown"


def anomaly_warning(history: BuildHistory, duration_ms: int) -> None:
    avg = history.average_duration()
    if avg and duration_ms > avg * 1.5:
        print(colorize("Warning: build duration exceeds historical average.", "1;33"))


def find_project(file_path: Path) -> str | None:
    current = file_path
    while current != current.parent:
        projects = list(current.glob("*.csproj"))
        if projects:
            return str(projects[0])
        current = current.parent
    return None


def sqlite_conn(path: Path):
    import sqlite3

    return sqlite3.connect(path)


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="Build Observability CLI")
    sub = parser.add_subparsers(dest="command")

    build = sub.add_parser("build", help="Run build with observability")
    build.add_argument("--project", default=DEFAULT_PROJECT)
    build.add_argument("--test-project", default=DEFAULT_TEST_PROJECT)
    build.add_argument("--configuration", default="Release")
    build.add_argument("--verbosity", default=os.getenv("BUILD_VERBOSITY", "normal"))
    build.add_argument("--skip-preflight", action="store_true")
    build.add_argument("--force", action="store_true")
    build.add_argument("--with-tests", action="store_true")
    build.add_argument("--generate-graph", action="store_true")

    fingerprint = sub.add_parser("fingerprint", help="Generate build fingerprint")
    fingerprint.add_argument("--configuration", default="Release")

    doctor = sub.add_parser("doctor", help="Run build environment doctor")
    doctor.add_argument("--quick", action="store_true")
    doctor.add_argument("--json", action="store_true")

    graph = sub.add_parser("build-graph", help="Generate dependency graph")
    graph.add_argument("--project", default=DEFAULT_PROJECT)

    env_capture = sub.add_parser("env-capture", help="Capture environment snapshot")
    env_capture.add_argument("name")

    env_diff = sub.add_parser("env-diff", help="Diff two environment snapshots")
    env_diff.add_argument("left")
    env_diff.add_argument("right")

    debug = sub.add_parser("collect-debug", help="Collect debug bundle")
    debug.add_argument("--project", default=DEFAULT_PROJECT)
    debug.add_argument("--configuration", default="Release")

    profile = sub.add_parser("build-profile", help="Show build profile")

    metrics = sub.add_parser("metrics", help="Show metrics summary")

    history = sub.add_parser("history", help="Show build history summary")

    impact = sub.add_parser("impact", help="Analyze impact of file change")
    impact.add_argument("--file", required=True)

    bisect = sub.add_parser("bisect", help="Run build bisect")
    bisect.add_argument("--good", required=True)
    bisect.add_argument("--bad", required=True)
    bisect.add_argument("--command")

    analyze = sub.add_parser("analyze-errors", help="Analyze build output for known errors")
    analyze.add_argument("--log")

    validate = sub.add_parser("validate-data", help="Validate JSONL data files")
    validate.add_argument("--directory", default="data")

    return parser


def main() -> int:
    parser = build_parser()
    args = parser.parse_args()
    if args.command == "build":
        return run_build(args)
    if args.command == "fingerprint":
        return run_fingerprint(args)
    if args.command == "doctor":
        return run_doctor_cmd(args)
    if args.command == "build-graph":
        return run_build_graph(args)
    if args.command == "collect-debug":
        return run_collect_debug(args)
    if args.command == "env-capture":
        return run_env_capture(args)
    if args.command == "env-diff":
        return run_env_diff(args)
    if args.command == "build-profile":
        return run_profile(args)
    if args.command == "metrics":
        return run_metrics(args)
    if args.command == "history":
        return run_history(args)
    if args.command == "impact":
        return run_impact(args)
    if args.command == "bisect":
        return run_bisect(args)
    if args.command == "analyze-errors":
        return run_analyze_errors(args)
    if args.command == "validate-data":
        return run_validate_data(args)
    parser.print_help()
    return 1


if __name__ == "__main__":
    raise SystemExit(main())
