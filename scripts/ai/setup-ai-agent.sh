#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck disable=SC1091
source "$SCRIPT_DIR/common.sh"

INSTALL_DOTNET=0

while [[ $# -gt 0 ]]; do
    case "$1" in
        --install-dotnet)
            INSTALL_DOTNET=1
            shift
            ;;
        *)
            echo "Unknown option: $1" >&2
            exit 2
            ;;
    esac
done

ai::write_env_file
ai::load_env

if [[ "$INSTALL_DOTNET" -eq 1 ]] || ! ai::command_exists dotnet; then
    ai::ensure_dotnet
fi

echo "AI agent environment ready."
echo "Environment file: $AI_ENV_FILE"
echo "To reuse in later steps: source \"$AI_ENV_FILE\""
if ai::command_exists dotnet; then
    echo ".NET SDK: $(dotnet --version)"
fi
