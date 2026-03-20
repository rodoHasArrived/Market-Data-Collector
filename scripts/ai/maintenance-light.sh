#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck disable=SC1091
source "$SCRIPT_DIR/common.sh"

ai::load_env
ai::status_init "light" "${1:-general}" "Run full maintenance for source or test changes."

overall_status="passed"
next_action="No further action required."

ai::run_step known-errors warning python3 build/scripts/ai-repo-updater.py known-errors || overall_status="warning"
ai::run_step diff-summary warning python3 build/scripts/ai-repo-updater.py diff-summary || overall_status="warning"

if [[ -f package-lock.json ]] && { [[ ! -d node_modules ]] || git diff --name-only -- package-lock.json package.json | grep -q .; }; then
    ai::run_step npm-ci warning npm ci || overall_status="warning"
fi

ai::run_step ai-audit-docs warning make ai-audit-docs || overall_status="warning"
ai::run_step ai-docs-drift warning make ai-docs-drift || overall_status="warning"
ai::run_step ai-docs-refs warning python3 build/scripts/docs/ai-docs-maintenance.py validate-refs || overall_status="warning"

if [[ "$overall_status" == "warning" ]]; then
    next_action="Review warnings in .ai/logs and run full maintenance for code, project, or workflow changes."
fi

ai::status_finalize "$overall_status" "$next_action"
echo "Wrote $AI_STATUS_FILE"
