#!/bin/bash
#
# Market Data Collector - Debug Bundle Collector
# Gathers all diagnostic information into a shareable bundle
#
# Usage: ./collect-debug.sh [OPTIONS]
#   --output DIR    Output directory (default: ./debug-bundle-TIMESTAMP)
#   --no-logs       Skip log file collection
#   --no-config     Skip configuration files (for privacy)
#   --include-data  Include sample data files (warning: may be large)
#   --verbose       Show detailed progress
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
DIM='\033[2m'
RESET='\033[0m'

# Options
TIMESTAMP=$(date +%Y%m%d_%H%M%S)
OUTPUT_DIR=""
INCLUDE_LOGS=true
INCLUDE_CONFIG=true
INCLUDE_DATA=false
VERBOSE=false

# Parse arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        --output) OUTPUT_DIR="$2"; shift 2 ;;
        --no-logs) INCLUDE_LOGS=false; shift ;;
        --no-config) INCLUDE_CONFIG=false; shift ;;
        --include-data) INCLUDE_DATA=true; shift ;;
        --verbose|-v) VERBOSE=true; shift ;;
        --help|-h)
            echo "Usage: $0 [OPTIONS]"
            echo "  --output DIR    Output directory (default: ./debug-bundle-TIMESTAMP)"
            echo "  --no-logs       Skip log file collection"
            echo "  --no-config     Skip configuration files (for privacy)"
            echo "  --include-data  Include sample data files (warning: may be large)"
            echo "  --verbose       Show detailed progress"
            exit 0
            ;;
        *) echo "Unknown option: $1"; exit 1 ;;
    esac
done

# Set default output directory
if [[ -z "$OUTPUT_DIR" ]]; then
    OUTPUT_DIR="$PROJECT_ROOT/debug-bundle-$TIMESTAMP"
fi

# Output functions
print_header() {
    echo ""
    echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${RESET}"
    echo -e "${CYAN}  $1${RESET}"
    echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${RESET}"
    echo ""
}

print_step() {
    echo -e "${BLUE}▸${RESET} $1"
}

print_success() {
    echo -e "  ${GREEN}✓${RESET} $1"
}

print_skip() {
    echo -e "  ${DIM}○ $1 (skipped)${RESET}"
}

verbose() {
    if [[ "$VERBOSE" == "true" ]]; then
        echo -e "  ${DIM}$1${RESET}"
    fi
}

# Sanitize sensitive data
sanitize_file() {
    local file="$1"
    if [[ -f "$file" ]]; then
        # Replace common secret patterns
        sed -i.bak \
            -e 's/\("*[Kk]ey[Ii]d"*\s*[=:]\s*\)"[^"]*"/\1"***REDACTED***"/g' \
            -e 's/\("*[Ss]ecret[Kk]ey"*\s*[=:]\s*\)"[^"]*"/\1"***REDACTED***"/g' \
            -e 's/\("*[Aa]pi[Kk]ey"*\s*[=:]\s*\)"[^"]*"/\1"***REDACTED***"/g' \
            -e 's/\("*[Pp]assword"*\s*[=:]\s*\)"[^"]*"/\1"***REDACTED***"/g' \
            -e 's/\("*[Cc]onnection[Ss]tring"*\s*[=:]\s*\)"[^"]*"/\1"***REDACTED***"/g' \
            "$file"
        rm -f "$file.bak"
    fi
}

# Collection functions
collect_system_info() {
    print_step "Collecting system information..."

    local info_file="$OUTPUT_DIR/system-info.txt"

    {
        echo "=== System Information ==="
        echo "Collected: $(date -u +%Y-%m-%dT%H:%M:%SZ)"
        echo ""

        echo "--- Operating System ---"
        if [[ -f /etc/os-release ]]; then
            cat /etc/os-release
        elif [[ "$OSTYPE" == "darwin"* ]]; then
            sw_vers
        else
            uname -a
        fi
        echo ""

        echo "--- Kernel ---"
        uname -a
        echo ""

        echo "--- Memory ---"
        if command -v free &> /dev/null; then
            free -h
        elif [[ "$OSTYPE" == "darwin"* ]]; then
            vm_stat
        fi
        echo ""

        echo "--- Disk Space ---"
        df -h "$PROJECT_ROOT"
        echo ""

        echo "--- CPU Info ---"
        if [[ -f /proc/cpuinfo ]]; then
            grep -E "^(model name|processor)" /proc/cpuinfo | head -4
        elif [[ "$OSTYPE" == "darwin"* ]]; then
            sysctl -n machdep.cpu.brand_string
            sysctl -n hw.ncpu
        fi
        echo ""

        echo "--- Environment Variables (filtered) ---"
        env | grep -E "^(DOTNET|NUGET|PATH|HOME|USER|SHELL)" | sort
        echo ""

    } > "$info_file"

    print_success "System info collected"
}

collect_dotnet_info() {
    print_step "Collecting .NET SDK information..."

    local dotnet_file="$OUTPUT_DIR/dotnet-info.txt"

    {
        echo "=== .NET SDK Information ==="
        echo ""

        if command -v dotnet &> /dev/null; then
            echo "--- SDK Version ---"
            dotnet --version
            echo ""

            echo "--- Installed SDKs ---"
            dotnet --list-sdks
            echo ""

            echo "--- Installed Runtimes ---"
            dotnet --list-runtimes
            echo ""

            echo "--- NuGet Sources ---"
            dotnet nuget list source 2>/dev/null || echo "Unable to list NuGet sources"
            echo ""

            echo "--- Workloads ---"
            dotnet workload list 2>/dev/null || echo "Unable to list workloads"
            echo ""
        else
            echo "dotnet CLI not found"
        fi

    } > "$dotnet_file"

    print_success ".NET SDK info collected"
}

collect_docker_info() {
    print_step "Collecting Docker information..."

    local docker_file="$OUTPUT_DIR/docker-info.txt"

    {
        echo "=== Docker Information ==="
        echo ""

        if command -v docker &> /dev/null; then
            echo "--- Docker Version ---"
            docker version 2>/dev/null || echo "Docker daemon not running"
            echo ""

            echo "--- Docker Info ---"
            docker info 2>/dev/null || echo "Unable to get Docker info"
            echo ""

            echo "--- Docker Images ---"
            docker images --format "table {{.Repository}}\t{{.Tag}}\t{{.Size}}" 2>/dev/null | head -20
            echo ""

            echo "--- Docker Containers ---"
            docker ps -a --format "table {{.Names}}\t{{.Status}}\t{{.Image}}" 2>/dev/null | head -20
            echo ""
        else
            echo "Docker not installed"
        fi

    } > "$docker_file"

    print_success "Docker info collected"
}

collect_git_info() {
    print_step "Collecting Git information..."

    local git_file="$OUTPUT_DIR/git-info.txt"

    {
        echo "=== Git Information ==="
        echo ""

        if command -v git &> /dev/null && git -C "$PROJECT_ROOT" rev-parse --git-dir &> /dev/null; then
            echo "--- Current Branch ---"
            git -C "$PROJECT_ROOT" rev-parse --abbrev-ref HEAD
            echo ""

            echo "--- Current Commit ---"
            git -C "$PROJECT_ROOT" log -1 --format="%H%n%s%n%an <%ae>%n%ai"
            echo ""

            echo "--- Recent Commits ---"
            git -C "$PROJECT_ROOT" log --oneline -20
            echo ""

            echo "--- Status ---"
            git -C "$PROJECT_ROOT" status --short
            echo ""

            echo "--- Remotes ---"
            git -C "$PROJECT_ROOT" remote -v
            echo ""

            echo "--- Branches ---"
            git -C "$PROJECT_ROOT" branch -a
            echo ""
        else
            echo "Not a Git repository"
        fi

    } > "$git_file"

    print_success "Git info collected"
}

collect_project_info() {
    print_step "Collecting project information..."

    local project_file="$OUTPUT_DIR/project-info.txt"

    {
        echo "=== Project Information ==="
        echo ""

        echo "--- Solution File ---"
        if [[ -f "$PROJECT_ROOT/MarketDataCollector.sln" ]]; then
            head -50 "$PROJECT_ROOT/MarketDataCollector.sln"
        else
            echo "Solution file not found"
        fi
        echo ""

        echo "--- Directory.Build.props ---"
        if [[ -f "$PROJECT_ROOT/Directory.Build.props" ]]; then
            cat "$PROJECT_ROOT/Directory.Build.props"
        else
            echo "File not found"
        fi
        echo ""

        echo "--- Project Files ---"
        find "$PROJECT_ROOT/src" -name "*.csproj" -o -name "*.fsproj" 2>/dev/null | head -20
        echo ""

        echo "--- Directory Structure ---"
        find "$PROJECT_ROOT" -maxdepth 2 -type d 2>/dev/null | grep -v -E "(node_modules|\.git|bin|obj)" | head -50
        echo ""

    } > "$project_file"

    print_success "Project info collected"
}

collect_dependency_info() {
    print_step "Collecting dependency information..."

    local deps_file="$OUTPUT_DIR/dependencies.txt"

    {
        echo "=== Dependency Information ==="
        echo ""

        echo "--- Package References (Main Project) ---"
        local csproj="$PROJECT_ROOT/src/MarketDataCollector/MarketDataCollector.csproj"
        if [[ -f "$csproj" ]]; then
            grep -E "<PackageReference" "$csproj" | sed 's/^[[:space:]]*//' || echo "No package references found"
        else
            echo "Main project file not found"
        fi
        echo ""

        echo "--- Dependency Graph (if available) ---"
        if [[ -d "$PROJECT_ROOT/src/MarketDataCollector/obj" ]]; then
            local assets="$PROJECT_ROOT/src/MarketDataCollector/obj/project.assets.json"
            if [[ -f "$assets" ]]; then
                echo "project.assets.json exists (size: $(du -h "$assets" | cut -f1))"
                echo "Top-level packages:"
                grep -o '"[^"]*": {' "$assets" | head -20 | sed 's/": {//'
            else
                echo "project.assets.json not found (run dotnet restore)"
            fi
        else
            echo "obj folder not found (run dotnet restore)"
        fi
        echo ""

    } > "$deps_file"

    print_success "Dependency info collected"
}

collect_config_files() {
    if [[ "$INCLUDE_CONFIG" != "true" ]]; then
        print_skip "Configuration files"
        return
    fi

    print_step "Collecting configuration files..."

    local config_dir="$OUTPUT_DIR/config"
    mkdir -p "$config_dir"

    # Copy and sanitize config files
    if [[ -f "$PROJECT_ROOT/config/appsettings.json" ]]; then
        cp "$PROJECT_ROOT/config/appsettings.json" "$config_dir/"
        sanitize_file "$config_dir/appsettings.json"
        verbose "Copied and sanitized appsettings.json"
    fi

    if [[ -f "$PROJECT_ROOT/config/appsettings.sample.json" ]]; then
        cp "$PROJECT_ROOT/config/appsettings.sample.json" "$config_dir/"
        verbose "Copied appsettings.sample.json"
    fi

    # Copy Docker configs
    if [[ -f "$PROJECT_ROOT/deploy/docker/docker-compose.yml" ]]; then
        cp "$PROJECT_ROOT/deploy/docker/docker-compose.yml" "$config_dir/"
        verbose "Copied docker-compose.yml"
    fi

    print_success "Configuration files collected (sanitized)"
}

collect_logs() {
    if [[ "$INCLUDE_LOGS" != "true" ]]; then
        print_skip "Log files"
        return
    fi

    print_step "Collecting log files..."

    local logs_dir="$OUTPUT_DIR/logs"
    mkdir -p "$logs_dir"

    # Collect diagnostic logs
    if [[ -d "$PROJECT_ROOT/diagnostic-logs" ]]; then
        find "$PROJECT_ROOT/diagnostic-logs" -name "*.log" -mtime -7 -exec cp {} "$logs_dir/" \; 2>/dev/null
        verbose "Copied recent diagnostic logs"
    fi

    # Collect any application logs
    if [[ -d "$PROJECT_ROOT/logs" ]]; then
        find "$PROJECT_ROOT/logs" -name "*.log" -mtime -7 -exec cp {} "$logs_dir/" \; 2>/dev/null
        verbose "Copied recent application logs"
    fi

    # Sanitize all logs
    for log in "$logs_dir"/*.log; do
        if [[ -f "$log" ]]; then
            sanitize_file "$log"
        fi
    done

    local log_count=$(find "$logs_dir" -type f 2>/dev/null | wc -l | tr -d ' ')
    print_success "Collected $log_count log file(s)"
}

collect_build_output() {
    print_step "Running build diagnostics..."

    local build_file="$OUTPUT_DIR/build-output.txt"

    {
        echo "=== Build Diagnostic Output ==="
        echo "Timestamp: $(date -u +%Y-%m-%dT%H:%M:%SZ)"
        echo ""

        echo "--- Restore ---"
        cd "$PROJECT_ROOT"
        dotnet restore -v detailed 2>&1 | tail -100
        echo ""

        echo "--- Build ---"
        dotnet build --no-restore -v minimal 2>&1 | tail -200
        echo ""

    } > "$build_file" 2>&1

    print_success "Build diagnostics collected"
}

run_doctor() {
    print_step "Running environment doctor..."

    local doctor_file="$OUTPUT_DIR/doctor-output.txt"

    if [[ -x "$SCRIPT_DIR/doctor.sh" ]]; then
        "$SCRIPT_DIR/doctor.sh" --verbose > "$doctor_file" 2>&1 || true
        print_success "Doctor diagnostics collected"
    else
        echo "Doctor script not found" > "$doctor_file"
        print_skip "Doctor script not found"
    fi
}

create_bundle() {
    print_step "Creating archive..."

    local archive_name="debug-bundle-$TIMESTAMP.tar.gz"
    local archive_path="$PROJECT_ROOT/$archive_name"

    # Create manifest
    {
        echo "=== Debug Bundle Manifest ==="
        echo "Created: $(date -u +%Y-%m-%dT%H:%M:%SZ)"
        echo "Project: Market Data Collector"
        echo ""
        echo "Contents:"
        find "$OUTPUT_DIR" -type f | sed "s|$OUTPUT_DIR/||" | sort
    } > "$OUTPUT_DIR/MANIFEST.txt"

    # Create archive
    cd "$(dirname "$OUTPUT_DIR")"
    tar -czf "$archive_path" "$(basename "$OUTPUT_DIR")"

    local size=$(du -h "$archive_path" | cut -f1)
    print_success "Created $archive_name ($size)"

    # Clean up directory
    rm -rf "$OUTPUT_DIR"

    echo ""
    echo -e "${GREEN}Debug bundle created: $archive_path${RESET}"
    echo ""
    echo "To share this bundle:"
    echo "  1. Review the contents for any sensitive information"
    echo "  2. Upload to a secure location or attach to your issue report"
    echo ""
}

# Main execution
main() {
    print_header "MarketDataCollector Debug Bundle Collector"

    # Create output directory
    mkdir -p "$OUTPUT_DIR"
    echo "Collecting diagnostics to: $OUTPUT_DIR"
    echo ""

    # Collect all information
    collect_system_info
    collect_dotnet_info
    collect_docker_info
    collect_git_info
    collect_project_info
    collect_dependency_info
    collect_config_files
    collect_logs
    run_doctor
    collect_build_output

    # Create archive
    create_bundle
}

main "$@"
