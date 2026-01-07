#!/bin/bash
# =============================================================================
# Market Data Collector - Build Diagnostics Script
# =============================================================================
#
# This script helps diagnose build and restore issues by running dotnet
# commands with diagnostic logging enabled.
#
# Usage:
#   ./scripts/diagnose-build.sh              # Run all diagnostics
#   ./scripts/diagnose-build.sh restore      # Diagnose restore only
#   ./scripts/diagnose-build.sh build        # Diagnose build only
#   ./scripts/diagnose-build.sh clean        # Clean and diagnose
#
# =============================================================================

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Script directory
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
LOG_DIR="$PROJECT_ROOT/diagnostic-logs"

# Print colored output
print_info() { echo -e "${BLUE}[INFO]${NC} $1"; }
print_success() { echo -e "${GREEN}[SUCCESS]${NC} $1"; }
print_warning() { echo -e "${YELLOW}[WARNING]${NC} $1"; }
print_error() { echo -e "${RED}[ERROR]${NC} $1"; }

# Header
print_header() {
    echo ""
    echo "╔══════════════════════════════════════════════════════════════════════╗"
    echo "║         Market Data Collector - Build Diagnostics                    ║"
    echo "╚══════════════════════════════════════════════════════════════════════╝"
    echo ""
}

# Create log directory
setup_log_dir() {
    mkdir -p "$LOG_DIR"
    print_info "Diagnostic logs will be saved to: $LOG_DIR"
}

# Check .NET SDK
check_dotnet() {
    print_info "Checking .NET SDK..."
    if ! command -v dotnet >/dev/null 2>&1; then
        print_error ".NET SDK not found. Please install .NET SDK 8.0 or later."
        exit 1
    fi
    
    local dotnet_version=$(dotnet --version)
    print_success ".NET SDK version: $dotnet_version"
    
    # List installed SDKs
    print_info "Installed .NET SDKs:"
    dotnet --list-sdks | sed 's/^/  /'
    echo ""
}

# Check NuGet sources
check_nuget_sources() {
    print_info "Checking NuGet sources..."
    dotnet nuget list source
    echo ""
}

# Run dotnet restore with diagnostics
diagnose_restore() {
    print_info "Running dotnet restore with diagnostic logging..."
    local timestamp=$(date +%Y%m%d_%H%M%S)
    local log_file="$LOG_DIR/restore-diagnostic-$timestamp.log"
    
    cd "$PROJECT_ROOT"
    
    # Run restore with diagnostic verbosity
    if dotnet restore /p:EnableWindowsTargeting=true -v diag > "$log_file" 2>&1; then
        print_success "Restore completed successfully"
    else
        print_error "Restore failed! Check log file: $log_file"
        
        # Show last 50 lines of log
        print_info "Last 50 lines of diagnostic log:"
        tail -n 50 "$log_file" | sed 's/^/  /'
        return 1
    fi
    
    # Check for warnings in the log
    local warning_count=0
    if [ -f "$log_file" ]; then
        warning_count=$(grep -ci "warning" "$log_file" || echo "0")
    fi
    if [ "$warning_count" -gt 0 ]; then
        print_warning "Found $warning_count warning(s) in restore output"
        print_info "To view warnings: grep -i warning $log_file"
    fi
    
    print_success "Diagnostic log saved to: $log_file"
    echo ""
}

# Run dotnet build with diagnostics
diagnose_build() {
    print_info "Running dotnet build with diagnostic logging..."
    local timestamp=$(date +%Y%m%d_%H%M%S)
    local log_file="$LOG_DIR/build-diagnostic-$timestamp.log"
    
    cd "$PROJECT_ROOT"
    
    # Run build with diagnostic verbosity
    if dotnet build -c Release /p:EnableWindowsTargeting=true -v diag > "$log_file" 2>&1; then
        print_success "Build completed successfully"
    else
        print_error "Build failed! Check log file: $log_file"
        
        # Show last 50 lines of log
        print_info "Last 50 lines of diagnostic log:"
        tail -n 50 "$log_file" | sed 's/^/  /'
        return 1
    fi
    
    # Check for warnings in the log
    local warning_count=0
    if [ -f "$log_file" ]; then
        warning_count=$(grep -ci "warning" "$log_file" || echo "0")
    fi
    if [ "$warning_count" -gt 0 ]; then
        print_warning "Found $warning_count warning(s) in build output"
        print_info "To view warnings: grep -i warning $log_file"
    fi
    
    print_success "Diagnostic log saved to: $log_file"
    echo ""
}

# Clean solution
clean_solution() {
    print_info "Cleaning solution..."
    cd "$PROJECT_ROOT"
    
    if dotnet clean; then
        print_success "Clean completed successfully"
    else
        print_warning "Clean encountered issues"
    fi
    
    # Also clear NuGet cache if requested
    read -p "Do you want to clear NuGet cache? [y/N]: " -n 1 -r
    echo
    if [[ $REPLY =~ ^[Yy]$ ]]; then
        print_info "Clearing NuGet cache..."
        dotnet nuget locals all --clear
        print_success "NuGet cache cleared"
    fi
    echo ""
}

# Show diagnostic log summary
show_log_summary() {
    print_info "Diagnostic log summary:"
    echo ""
    
    if [ -d "$LOG_DIR" ] && [ "$(ls -A $LOG_DIR 2>/dev/null)" ]; then
        ls -lh "$LOG_DIR" | tail -n +2 | awk '{print "  " $9 " (" $5 ")"}'
        echo ""
        print_info "To view a log file: cat $LOG_DIR/<filename>"
        print_info "To search for errors: grep -i error $LOG_DIR/<filename>"
        print_info "To search for warnings: grep -i warning $LOG_DIR/<filename>"
    else
        print_info "No diagnostic logs found"
    fi
    echo ""
}

# Main execution
main() {
    print_header
    
    setup_log_dir
    check_dotnet
    
    case "${1:-all}" in
        restore)
            check_nuget_sources
            diagnose_restore
            ;;
        build)
            diagnose_build
            ;;
        clean)
            clean_solution
            diagnose_restore
            diagnose_build
            ;;
        all|"")
            check_nuget_sources
            diagnose_restore
            local restore_result=$?
            if [ $restore_result -eq 0 ]; then
                diagnose_build
            else
                print_error "Skipping build due to restore failure"
            fi
            ;;
        *)
            print_error "Unknown option: $1"
            echo "Usage: $0 [restore|build|clean|all]"
            exit 1
            ;;
    esac
    
    show_log_summary
    
    echo "╔══════════════════════════════════════════════════════════════════════╗"
    echo "║                    Diagnostics Complete                              ║"
    echo "╚══════════════════════════════════════════════════════════════════════╝"
}

main "$@"
