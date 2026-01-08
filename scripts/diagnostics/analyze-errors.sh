#!/bin/bash
#
# Market Data Collector - Error Analyzer
# Analyzes build output and matches against known error patterns
#
# Usage: ./analyze-errors.sh [LOG_FILE]
#   LOG_FILE    Optional build log file to analyze (default: reads from stdin)
#
# Examples:
#   dotnet build 2>&1 | ./analyze-errors.sh
#   ./analyze-errors.sh build-output.log
#
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
KNOWN_ERRORS_FILE="$SCRIPT_DIR/known-errors.json"

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
DIM='\033[2m'
BOLD='\033[1m'
RESET='\033[0m'

# Check for jq
if ! command -v jq &> /dev/null; then
    echo -e "${RED}Error: jq is required but not installed.${RESET}"
    echo "Install with: apt-get install jq (Linux) or brew install jq (macOS)"
    exit 1
fi

# Check for known-errors.json
if [[ ! -f "$KNOWN_ERRORS_FILE" ]]; then
    echo -e "${RED}Error: known-errors.json not found at $KNOWN_ERRORS_FILE${RESET}"
    exit 1
fi

# Read input
if [[ $# -gt 0 && -f "$1" ]]; then
    BUILD_OUTPUT=$(cat "$1")
else
    BUILD_OUTPUT=$(cat)
fi

# Parse known errors
PATTERNS=$(jq -r '.patterns[] | @base64' "$KNOWN_ERRORS_FILE")

print_header() {
    echo ""
    echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${RESET}"
    echo -e "${CYAN}  Error Analysis Report${RESET}"
    echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${RESET}"
    echo ""
}

MATCH_COUNT=0
MATCHED_IDS=()

analyze() {
    for pattern_b64 in $PATTERNS; do
        pattern_json=$(echo "$pattern_b64" | base64 -d)

        id=$(echo "$pattern_json" | jq -r '.id')
        regex=$(echo "$pattern_json" | jq -r '.regex')
        title=$(echo "$pattern_json" | jq -r '.title')
        severity=$(echo "$pattern_json" | jq -r '.severity')
        description=$(echo "$pattern_json" | jq -r '.description // empty')
        solution=$(echo "$pattern_json" | jq -r '.solution // empty')
        commands=$(echo "$pattern_json" | jq -r '.commands // [] | .[]')
        docs=$(echo "$pattern_json" | jq -r '.docs // empty')

        # Check if pattern matches
        if echo "$BUILD_OUTPUT" | grep -qE "$regex" 2>/dev/null; then
            ((MATCH_COUNT++))
            MATCHED_IDS+=("$id")

            # Color based on severity
            case "$severity" in
                error)   SEVERITY_COLOR=$RED ;;
                warning) SEVERITY_COLOR=$YELLOW ;;
                *)       SEVERITY_COLOR=$BLUE ;;
            esac

            echo -e "${SEVERITY_COLOR}[$severity]${RESET} ${BOLD}$title${RESET} ${DIM}($id)${RESET}"
            echo ""

            if [[ -n "$description" ]]; then
                echo -e "  ${DIM}$description${RESET}"
                echo ""
            fi

            if [[ -n "$solution" ]]; then
                echo -e "  ${GREEN}Solution:${RESET} $solution"
            fi

            if [[ -n "$commands" ]]; then
                echo ""
                echo -e "  ${BLUE}Suggested commands:${RESET}"
                echo "$commands" | while read -r cmd; do
                    echo -e "    ${DIM}\$${RESET} $cmd"
                done
            fi

            if [[ -n "$docs" ]]; then
                echo ""
                echo -e "  ${DIM}Documentation: $docs${RESET}"
            fi

            echo ""
            echo -e "${DIM}$(printf '─%.0s' {1..60})${RESET}"
            echo ""
        fi
    done
}

print_summary() {
    echo ""
    echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${RESET}"

    if [[ $MATCH_COUNT -eq 0 ]]; then
        echo -e "  ${GREEN}No known error patterns detected.${RESET}"
        echo ""
        echo -e "  ${DIM}If you're still having issues, run:${RESET}"
        echo -e "    ${DIM}\$${RESET} make collect-debug"
        echo -e "  ${DIM}to create a debug bundle for issue reporting.${RESET}"
    else
        echo -e "  ${YELLOW}Found $MATCH_COUNT known issue(s)${RESET}"
        echo ""
        echo -e "  ${DIM}Issue IDs: ${MATCHED_IDS[*]}${RESET}"
    fi

    echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${RESET}"
    echo ""
}

# Main
print_header
analyze
print_summary

exit 0
