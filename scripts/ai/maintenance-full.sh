#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck disable=SC1091
source "$SCRIPT_DIR/common.sh"

ai::ensure_dotnet
ai::status_init "full" "${1:-general}" "Review failing checks and repair the environment or code before merge."
overall_status="passed"
next_action="No further action required."

ai::run_step known-errors warning python3 build/scripts/ai-repo-updater.py known-errors || overall_status="warning"
ai::run_step diff-summary warning python3 build/scripts/ai-repo-updater.py diff-summary || overall_status="warning"
ai::run_step doctor warning make doctor-ci || overall_status="warning"

if ! ai::run_step dotnet-restore failed dotnet restore Meridian.sln /p:EnableWindowsTargeting=true --verbosity minimal; then
    ai::status_finalize "failed" "dotnet restore failed; review .ai/logs/dotnet-restore.log and repair the SDK or NuGet state."
    exit 1
fi

if ! ai::run_step dotnet-build failed dotnet build Meridian.sln -c Release --no-restore /p:EnableWindowsTargeting=true --verbosity minimal; then
    ai::status_finalize "failed" "dotnet build failed; review .ai/logs/dotnet-build.log and repair the reported compilation errors."
    exit 1
fi

if ! ai::run_step dotnet-test-core failed dotnet test tests/Meridian.Tests/Meridian.Tests.csproj -c Release --no-build /p:EnableWindowsTargeting=true --verbosity minimal --filter "Category!=Integration"; then
    ai::status_finalize "failed" "Core test execution failed; review .ai/logs/dotnet-test-core.log before merging."
    exit 1
fi

ai::run_step dotnet-test-fsharp warning dotnet test tests/Meridian.FSharp.Tests/Meridian.FSharp.Tests.fsproj -c Release --no-build /p:EnableWindowsTargeting=true --verbosity minimal || overall_status="warning"
ai::run_step ai-verify warning make ai-verify || overall_status="warning"

if [[ "$overall_status" == "warning" ]]; then
    next_action="Review warnings in .ai/logs and address any remaining doctor, F#, or ai-verify issues."
fi

ai::status_finalize "$overall_status" "$next_action"
echo "Wrote $AI_STATUS_FILE"
