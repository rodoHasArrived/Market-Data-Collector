#!/bin/bash
#
# Market Data Collector - Build Doctor
# Comprehensive system health check for build environment
#
# Usage: ./doctor.sh [OPTIONS]
#   --quick     Quick check (skip slow operations)
#   --json      Output results as JSON
#   --fix       Attempt to auto-fix issues where possible
#   --verbose   Show detailed output
#
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
BOLD='\033[1m'
DIM='\033[2m'
RESET='\033[0m'

# Options
QUICK_MODE=false
JSON_OUTPUT=false
FIX_MODE=false
VERBOSE=false

# Counters
PASS_COUNT=0
WARN_COUNT=0
FAIL_COUNT=0

# Results array for JSON output
declare -a RESULTS=()

# Parse arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        --quick) QUICK_MODE=true; shift ;;
        --json) JSON_OUTPUT=true; shift ;;
        --fix) FIX_MODE=true; shift ;;
        --verbose|-v) VERBOSE=true; shift ;;
        --help|-h)
            echo "Usage: $0 [OPTIONS]"
            echo "  --quick     Quick check (skip slow operations)"
            echo "  --json      Output results as JSON"
            echo "  --fix       Attempt to auto-fix issues where possible"
            echo "  --verbose   Show detailed output"
            exit 0
            ;;
        *) echo "Unknown option: $1"; exit 1 ;;
    esac
done

# Output functions
print_header() {
    if [[ "$JSON_OUTPUT" != "true" ]]; then
        echo ""
        echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${RESET}"
        echo -e "${CYAN}  $1${RESET}"
        echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${RESET}"
        echo ""
    fi
}

print_section() {
    if [[ "$JSON_OUTPUT" != "true" ]]; then
        echo ""
        echo -e "${BOLD}$1${RESET}"
        echo -e "${DIM}$(printf '─%.0s' {1..50})${RESET}"
    fi
}

check_pass() {
    local name="$1"
    local detail="$2"
    ((PASS_COUNT++))
    RESULTS+=("{\"name\":\"$name\",\"status\":\"pass\",\"detail\":\"$detail\"}")
    if [[ "$JSON_OUTPUT" != "true" ]]; then
        echo -e "  ${GREEN}✓${RESET} $name ${DIM}$detail${RESET}"
    fi
}

check_warn() {
    local name="$1"
    local detail="$2"
    ((WARN_COUNT++))
    RESULTS+=("{\"name\":\"$name\",\"status\":\"warn\",\"detail\":\"$detail\"}")
    if [[ "$JSON_OUTPUT" != "true" ]]; then
        echo -e "  ${YELLOW}⚠${RESET} $name ${YELLOW}$detail${RESET}"
    fi
}

check_fail() {
    local name="$1"
    local detail="$2"
    local fix="$3"
    ((FAIL_COUNT++))
    RESULTS+=("{\"name\":\"$name\",\"status\":\"fail\",\"detail\":\"$detail\",\"fix\":\"$fix\"}")
    if [[ "$JSON_OUTPUT" != "true" ]]; then
        echo -e "  ${RED}✗${RESET} $name ${RED}$detail${RESET}"
        if [[ -n "$fix" ]]; then
            echo -e "    ${DIM}Fix: $fix${RESET}"
        fi
    fi
}

verbose() {
    if [[ "$VERBOSE" == "true" && "$JSON_OUTPUT" != "true" ]]; then
        echo -e "    ${DIM}$1${RESET}"
    fi
}

# Version comparison
version_gte() {
    # Returns 0 if $1 >= $2
    printf '%s\n%s\n' "$2" "$1" | sort -V -C
}

# Check functions
check_dotnet() {
    print_section ".NET SDK"

    if ! command -v dotnet &> /dev/null; then
        check_fail ".NET SDK" "Not installed" "Install from https://dot.net/download"
        return
    fi

    local version
    version=$(dotnet --version 2>/dev/null || echo "unknown")
    local required="9.0.0"

    if version_gte "$version" "$required"; then
        check_pass ".NET SDK $version" "(required: $required+)"
    else
        check_fail ".NET SDK $version" "(required: $required+)" "dotnet SDK 9.0+ required"
    fi

    # Check for additional SDKs
    if [[ "$VERBOSE" == "true" ]]; then
        verbose "Installed SDKs:"
        dotnet --list-sdks 2>/dev/null | while read -r sdk; do
            verbose "  $sdk"
        done
    fi
}

check_docker() {
    print_section "Docker"

    if ! command -v docker &> /dev/null; then
        check_warn "Docker" "Not installed (optional for native builds)"
        return
    fi

    local version
    version=$(docker --version 2>/dev/null | grep -oE '[0-9]+\.[0-9]+\.[0-9]+' | head -1 || echo "unknown")

    if docker info &> /dev/null; then
        check_pass "Docker $version" "(daemon running)"
    else
        check_warn "Docker $version" "(daemon not running)"
    fi

    # Check Docker Compose
    if command -v docker-compose &> /dev/null; then
        local compose_version
        compose_version=$(docker-compose --version 2>/dev/null | grep -oE '[0-9]+\.[0-9]+\.[0-9]+' | head -1 || echo "unknown")
        check_pass "Docker Compose $compose_version" ""
    elif docker compose version &> /dev/null; then
        local compose_version
        compose_version=$(docker compose version 2>/dev/null | grep -oE '[0-9]+\.[0-9]+\.[0-9]+' | head -1 || echo "unknown")
        check_pass "Docker Compose (plugin) $compose_version" ""
    else
        check_warn "Docker Compose" "Not installed"
    fi
}

check_git() {
    print_section "Git"

    if ! command -v git &> /dev/null; then
        check_fail "Git" "Not installed" "Install git from https://git-scm.com"
        return
    fi

    local version
    version=$(git --version 2>/dev/null | grep -oE '[0-9]+\.[0-9]+\.[0-9]+' | head -1 || echo "unknown")
    check_pass "Git $version" ""

    # Check if in a git repo
    if git -C "$PROJECT_ROOT" rev-parse --git-dir &> /dev/null; then
        local branch
        branch=$(git -C "$PROJECT_ROOT" rev-parse --abbrev-ref HEAD 2>/dev/null || echo "unknown")
        local status
        status=$(git -C "$PROJECT_ROOT" status --porcelain 2>/dev/null | wc -l | tr -d ' ')
        if [[ "$status" == "0" ]]; then
            check_pass "Repository" "branch: $branch (clean)"
        else
            check_warn "Repository" "branch: $branch ($status uncommitted changes)"
        fi
    fi
}

check_nuget() {
    print_section "NuGet Configuration"

    local sources
    sources=$(dotnet nuget list source 2>/dev/null || echo "")
    local source_count
    source_count=$(echo "$sources" | grep -c "^\s*[0-9]" || echo "0")

    if [[ "$source_count" -gt 0 ]]; then
        check_pass "NuGet sources" "$source_count configured"
        if [[ "$VERBOSE" == "true" ]]; then
            echo "$sources" | grep -E "^\s*[0-9]" | while read -r line; do
                verbose "$line"
            done
        fi
    else
        check_warn "NuGet sources" "No sources configured"
    fi

    # Check nuget.org connectivity (skip in quick mode)
    if [[ "$QUICK_MODE" != "true" ]]; then
        if curl -s --max-time 5 https://api.nuget.org/v3/index.json > /dev/null 2>&1; then
            check_pass "nuget.org" "Reachable"
        else
            check_warn "nuget.org" "Not reachable (may affect restore)"
        fi
    fi
}

check_disk_space() {
    print_section "Disk Space"

    local available_kb
    if [[ "$OSTYPE" == "darwin"* ]]; then
        available_kb=$(df -k "$PROJECT_ROOT" | tail -1 | awk '{print $4}')
    else
        available_kb=$(df -k "$PROJECT_ROOT" | tail -1 | awk '{print $4}')
    fi

    local available_gb=$((available_kb / 1024 / 1024))

    if [[ $available_gb -ge 20 ]]; then
        check_pass "Disk space" "${available_gb}GB available"
    elif [[ $available_gb -ge 5 ]]; then
        check_warn "Disk space" "${available_gb}GB available (recommend 20GB+)"
    else
        check_fail "Disk space" "${available_gb}GB available" "Free up disk space (need 5GB+ minimum)"
    fi
}

check_project_structure() {
    print_section "Project Structure"

    # Check solution file
    if [[ -f "$PROJECT_ROOT/MarketDataCollector.sln" ]]; then
        check_pass "Solution file" "MarketDataCollector.sln"
    else
        check_fail "Solution file" "Not found" "Ensure you're in the project root"
    fi

    # Check main project
    if [[ -f "$PROJECT_ROOT/src/MarketDataCollector/MarketDataCollector.csproj" ]]; then
        check_pass "Main project" "src/MarketDataCollector/MarketDataCollector.csproj"
    else
        check_fail "Main project" "Not found" ""
    fi

    # Check Directory.Build.props
    if [[ -f "$PROJECT_ROOT/Directory.Build.props" ]]; then
        if grep -q "EnableWindowsTargeting" "$PROJECT_ROOT/Directory.Build.props"; then
            check_pass "Directory.Build.props" "EnableWindowsTargeting configured"
        else
            check_warn "Directory.Build.props" "EnableWindowsTargeting not set"
        fi
    else
        check_warn "Directory.Build.props" "Not found (may cause cross-platform issues)"
    fi

    # Check config
    if [[ -f "$PROJECT_ROOT/config/appsettings.json" ]]; then
        check_pass "Configuration" "config/appsettings.json"
    else
        if [[ -f "$PROJECT_ROOT/config/appsettings.sample.json" ]]; then
            check_warn "Configuration" "appsettings.json not found (template available)"
            if [[ "$FIX_MODE" == "true" ]]; then
                cp "$PROJECT_ROOT/config/appsettings.sample.json" "$PROJECT_ROOT/config/appsettings.json"
                check_pass "Configuration" "Created from template"
            fi
        else
            check_warn "Configuration" "No config files found"
        fi
    fi
}

check_dependencies() {
    print_section "Dependencies"

    if [[ "$QUICK_MODE" == "true" ]]; then
        verbose "Skipping dependency check in quick mode"
        return
    fi

    # Check if restore is needed
    local needs_restore=false
    if [[ ! -d "$PROJECT_ROOT/src/MarketDataCollector/obj" ]]; then
        needs_restore=true
    fi

    if [[ "$needs_restore" == "true" ]]; then
        check_warn "NuGet packages" "Not restored (run 'dotnet restore')"
        if [[ "$FIX_MODE" == "true" ]]; then
            echo -e "    ${DIM}Running dotnet restore...${RESET}"
            if dotnet restore "$PROJECT_ROOT" -v q 2>/dev/null; then
                check_pass "NuGet packages" "Restored successfully"
            else
                check_fail "NuGet packages" "Restore failed" "Check network and NuGet sources"
            fi
        fi
    else
        check_pass "NuGet packages" "Restored"
    fi

    # Check for outdated packages (if dotnet-outdated is installed)
    if command -v dotnet-outdated &> /dev/null; then
        verbose "Checking for outdated packages..."
        # This would need implementation
    fi
}

check_environment_variables() {
    print_section "Environment Variables"

    local missing_creds=0
    local found_creds=0

    # Check for common credential env vars
    local cred_vars=("ALPACA__KEYID" "ALPACA__SECRETKEY" "NYSE__APIKEY" "TIINGO__APIKEY")

    for var in "${cred_vars[@]}"; do
        if [[ -n "${!var}" ]]; then
            ((found_creds++))
            verbose "$var: configured"
        fi
    done

    if [[ $found_creds -gt 0 ]]; then
        check_pass "API credentials" "$found_creds provider(s) configured"
    else
        check_warn "API credentials" "No provider credentials found in environment"
    fi

    # Check for common development env vars
    if [[ -n "$DOTNET_ENVIRONMENT" ]]; then
        check_pass "DOTNET_ENVIRONMENT" "$DOTNET_ENVIRONMENT"
    fi
}

check_ports() {
    print_section "Network Ports"

    if [[ "$QUICK_MODE" == "true" ]]; then
        verbose "Skipping port check in quick mode"
        return
    fi

    # Check common ports
    declare -A ports=(
        ["8080"]="Web Dashboard"
        ["5000"]="API Gateway"
        ["7497"]="TWS Gateway"
        ["9090"]="Prometheus"
        ["3000"]="Grafana"
    )

    for port in "${!ports[@]}"; do
        local desc="${ports[$port]}"
        if command -v lsof &> /dev/null; then
            if lsof -i ":$port" &> /dev/null; then
                check_warn "Port $port ($desc)" "In use"
            else
                verbose "Port $port ($desc): Available"
            fi
        elif command -v ss &> /dev/null; then
            if ss -tuln | grep -q ":$port "; then
                check_warn "Port $port ($desc)" "In use"
            else
                verbose "Port $port ($desc): Available"
            fi
        fi
    done
}

print_summary() {
    if [[ "$JSON_OUTPUT" == "true" ]]; then
        echo "{"
        echo "  \"timestamp\": \"$(date -u +%Y-%m-%dT%H:%M:%SZ)\","
        echo "  \"passed\": $PASS_COUNT,"
        echo "  \"warnings\": $WARN_COUNT,"
        echo "  \"failures\": $FAIL_COUNT,"
        echo "  \"results\": ["
        local first=true
        for result in "${RESULTS[@]}"; do
            if [[ "$first" == "true" ]]; then
                first=false
            else
                echo ","
            fi
            echo -n "    $result"
        done
        echo ""
        echo "  ]"
        echo "}"
    else
        echo ""
        echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${RESET}"
        echo -e "  ${GREEN}$PASS_COUNT passed${RESET}, ${YELLOW}$WARN_COUNT warnings${RESET}, ${RED}$FAIL_COUNT failures${RESET}"
        echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${RESET}"
        echo ""

        if [[ $FAIL_COUNT -gt 0 ]]; then
            echo -e "${RED}Build environment has critical issues. Please fix failures before building.${RESET}"
            exit 1
        elif [[ $WARN_COUNT -gt 0 ]]; then
            echo -e "${YELLOW}Build environment ready with some warnings.${RESET}"
        else
            echo -e "${GREEN}Build environment is healthy!${RESET}"
        fi
    fi
}

# Main execution
main() {
    print_header "MarketDataCollector Build Doctor"

    check_dotnet
    check_docker
    check_git
    check_nuget
    check_disk_space
    check_project_structure
    check_dependencies
    check_environment_variables
    check_ports

    print_summary
}

main "$@"
