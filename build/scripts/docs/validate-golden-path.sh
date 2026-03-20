#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$repo_root"

readonly unix_installer="build/scripts/install/install.sh"
readonly windows_installer="build/scripts/install/install.ps1"

readonly required_reference_files=(
  "README.md"
  "docs/HELP.md"
)

fail() {
  echo "ERROR: $*" >&2
  exit 1
}

check_file_exists() {
  local path="$1"
  [[ -f "$path" ]] || fail "Expected file not found: $path"
}

check_executable() {
  local path="$1"
  [[ -x "$path" ]] || fail "Expected executable bit on: $path"
}

check_contains() {
  local file="$1"
  local text="$2"
  grep -Fq "$text" "$file" || fail "Missing expected reference '$text' in $file"
}

check_absent() {
  local file="$1"
  local text="$2"
  if grep -Fq "$text" "$file"; then
    fail "Found stale reference '$text' in $file"
  fi
}

check_tree_absent() {
  local root="$1"
  local text="$2"
  if rg -n -F --glob '*.md' "$text" "$root" >/dev/null; then
    fail "Found stale reference '$text' under $root"
  fi
}

check_file_exists "$unix_installer"
check_file_exists "$windows_installer"
check_executable "$unix_installer"

for file in "${required_reference_files[@]}"; do
  check_contains "$file" "./$unix_installer"
  check_contains "$file" ".\\build\\scripts\\install\\install.ps1"
  check_absent "$file" "./scripts/install/install.sh"
  check_absent "$file" ".\\scripts\\install\\install.ps1"
done

check_contains "Makefile" "./$unix_installer"
check_tree_absent "." "./scripts/install/install.sh"
check_tree_absent "." ".\\scripts\\install\\install.ps1"

echo "Golden Path installer references are valid."
