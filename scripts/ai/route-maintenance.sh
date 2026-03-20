#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck disable=SC1091
source "$SCRIPT_DIR/common.sh"

BASE_REF=""
HEAD_REF="HEAD"
CLASSIFY_ONLY=0
FILES=()

while [[ $# -gt 0 ]]; do
    case "$1" in
        --base)
            BASE_REF="$2"
            shift 2
            ;;
        --head)
            HEAD_REF="$2"
            shift 2
            ;;
        --file)
            FILES+=("$2")
            shift 2
            ;;
        --classify-only)
            CLASSIFY_ONLY=1
            shift
            ;;
        *)
            echo "Unknown option: $1" >&2
            exit 2
            ;;
    esac
done

if [[ "${#FILES[@]}" -eq 0 ]]; then
    if [[ -n "$BASE_REF" ]]; then
        mapfile -t FILES < <(git diff --name-only "$BASE_REF" "$HEAD_REF")
    else
        mapfile -t FILES < <(git diff --name-only HEAD)
    fi
fi

python3 - "$AI_ROUTE_FILE" "${FILES[@]}" <<'PY'
import json
import sys

files = [f for f in sys.argv[2:] if f]

docs_prefixes = ("docs/", "docs/ai/")
workflow_prefixes = (".github/workflows/", "build/", "scripts/")
code_prefixes = ("src/", "tests/")
ui_prefixes = ("src/Meridian.Wpf/", "src/Meridian.Ui.Services/", "src/Meridian.Ui.Shared/")
ledger_prefixes = ("src/Meridian.Ledger/",)

project_files = {"Directory.Packages.props", "global.json", "Makefile"}

docs_only = bool(files) and all(f.startswith(docs_prefixes) or f == "CLAUDE.md" for f in files)
workflow_changes = any(f.startswith(workflow_prefixes) or f in project_files for f in files)
code_changes = any(f.startswith(code_prefixes) or f.endswith((".csproj", ".fsproj")) or f == "global.json" for f in files)
ui_changes = any(f.startswith(ui_prefixes) for f in files)
ledger_changes = any(f.startswith(ledger_prefixes) for f in files)

mode = "light"
route = ["light-maintenance"]

if code_changes:
    mode = "full"
    route = ["full-maintenance"]

if docs_only:
    route.append("docs-drift")
if workflow_changes:
    route.append("workflow-validation")
if ui_changes:
    route.append("wpf-smoke")
if ledger_changes:
    route.append("ledger-targeted")

data = {
    "mode": mode,
    "docs_only": docs_only,
    "workflow_changes": workflow_changes,
    "code_changes": code_changes,
    "ui_changes": ui_changes,
    "ledger_changes": ledger_changes,
    "changed_files": files,
    "routes": route,
}

with open(sys.argv[1], "w", encoding="utf-8") as handle:
    json.dump(data, handle, indent=2)
    handle.write("\n")

print(json.dumps(data))
PY

if [[ "$CLASSIFY_ONLY" -eq 1 ]]; then
    exit 0
fi

mode="$(python3 - "$AI_ROUTE_FILE" <<'PY'
import json
import sys
print(json.loads(open(sys.argv[1], encoding="utf-8").read())["mode"])
PY
)"

if [[ "$mode" == "full" ]]; then
    "$SCRIPT_DIR/maintenance-full.sh" routed
else
    "$SCRIPT_DIR/maintenance-light.sh" routed
fi

route_has() {
    python3 - "$AI_ROUTE_FILE" "$1" <<'PY'
import json
import sys
route = json.loads(open(sys.argv[1], encoding="utf-8").read())
print("1" if sys.argv[2] in route["routes"] else "0")
PY
}

ai::load_env

if [[ "$(route_has docs-drift)" == "1" ]]; then
    python3 build/scripts/docs/ai-docs-maintenance.py sync-report --output .ai/docs-sync-report.md >"$AI_LOG_DIR/docs-sync-report.log" 2>&1 || true
fi

if [[ "$(route_has workflow-validation)" == "1" ]]; then
    python3 build/scripts/ai-repo-updater.py audit-config --summary >"$AI_LOG_DIR/workflow-validation.log" 2>&1 || true
fi

if [[ "$(route_has wpf-smoke)" == "1" ]]; then
    make test-desktop-services >"$AI_LOG_DIR/wpf-smoke.log" 2>&1 || true
fi

if [[ "$(route_has ledger-targeted)" == "1" ]]; then
    dotnet test tests/Meridian.Backtesting.Tests/Meridian.Backtesting.Tests.csproj -c Release --filter "FullyQualifiedName~Ledger" >"$AI_LOG_DIR/ledger-targeted.log" 2>&1 || true
fi

