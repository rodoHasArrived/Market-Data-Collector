#!/bin/bash
# MarketDataCollector - Cross-platform Single Executable Build Script
# Usage: ./publish.sh [platform] [project]
# Examples:
#   ./publish.sh                    # Build all platforms, all projects
#   ./publish.sh linux-x64          # Build only Linux x64
#   ./publish.sh win-x64 collector  # Build only Windows x64, collector only
#   ./publish.sh all ui             # Build all platforms, UI only

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

# Configuration
VERSION="${VERSION:-1.0.0}"
CONFIGURATION="${CONFIGURATION:-Release}"
OUTPUT_DIR="${OUTPUT_DIR:-./dist}"

# Supported platforms
PLATFORMS=("win-x64" "linux-x64" "osx-x64" "osx-arm64")

# Projects
COLLECTOR_PROJECT="src/MarketDataCollector/MarketDataCollector.csproj"
UI_PROJECT="src/MarketDataCollector.Ui/MarketDataCollector.Ui.csproj"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

log_info() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

log_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

log_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

show_help() {
    echo "MarketDataCollector - Single Executable Build Script"
    echo ""
    echo "Usage: $0 [platform] [project]"
    echo ""
    echo "Platforms:"
    echo "  all        Build for all platforms (default)"
    echo "  win-x64    Windows x64"
    echo "  linux-x64  Linux x64"
    echo "  osx-x64    macOS x64 (Intel)"
    echo "  osx-arm64  macOS ARM64 (Apple Silicon)"
    echo ""
    echo "Projects:"
    echo "  all        Build both projects (default)"
    echo "  collector  Build only MarketDataCollector"
    echo "  ui         Build only MarketDataCollector.Ui"
    echo ""
    echo "Environment Variables:"
    echo "  VERSION        Version number (default: 1.0.0)"
    echo "  CONFIGURATION  Build configuration (default: Release)"
    echo "  OUTPUT_DIR     Output directory (default: ./dist)"
    echo ""
    echo "Examples:"
    echo "  $0                         # Build all platforms, all projects"
    echo "  $0 linux-x64               # Build Linux x64 only"
    echo "  $0 win-x64 collector       # Build Windows collector only"
    echo "  VERSION=2.0.0 $0           # Build with custom version"
}

publish_project() {
    local project="$1"
    local rid="$2"
    local project_name="$3"
    local output_subdir="$4"

    local output_path="$OUTPUT_DIR/$rid/$output_subdir"

    log_info "Publishing $project_name for $rid..."

    dotnet publish "$project" \
        -c "$CONFIGURATION" \
        -r "$rid" \
        -o "$output_path" \
        -p:Version="$VERSION" \
        --self-contained true \
        -p:PublishSingleFile=true \
        -p:PublishTrimmed=true \
        -p:EnableCompressionInSingleFile=true \
        -p:IncludeNativeLibrariesForSelfExtract=true

    # Copy configuration file
    if [ -f "appsettings.json" ]; then
        cp appsettings.json "$output_path/"
    fi

    log_success "Published $project_name for $rid -> $output_path"
}

create_package() {
    local rid="$1"
    local output_path="$OUTPUT_DIR/$rid"
    local package_name="MarketDataCollector-$VERSION-$rid"

    if [ -d "$output_path" ]; then
        log_info "Creating package for $rid..."

        # Create tarball for Unix, zip for Windows
        if [[ "$rid" == win-* ]]; then
            if command -v zip &> /dev/null; then
                (cd "$OUTPUT_DIR" && zip -r "${package_name}.zip" "$rid")
                log_success "Created $OUTPUT_DIR/${package_name}.zip"
            fi
        else
            (cd "$OUTPUT_DIR" && tar -czvf "${package_name}.tar.gz" "$rid")
            log_success "Created $OUTPUT_DIR/${package_name}.tar.gz"
        fi
    fi
}

# Parse arguments
TARGET_PLATFORM="${1:-all}"
TARGET_PROJECT="${2:-all}"

if [ "$TARGET_PLATFORM" == "--help" ] || [ "$TARGET_PLATFORM" == "-h" ]; then
    show_help
    exit 0
fi

# Validate platform
if [ "$TARGET_PLATFORM" != "all" ]; then
    valid_platform=false
    for p in "${PLATFORMS[@]}"; do
        if [ "$TARGET_PLATFORM" == "$p" ]; then
            valid_platform=true
            break
        fi
    done
    if [ "$valid_platform" = false ]; then
        log_error "Invalid platform: $TARGET_PLATFORM"
        show_help
        exit 1
    fi
    PLATFORMS=("$TARGET_PLATFORM")
fi

# Clean output directory
log_info "Cleaning output directory: $OUTPUT_DIR"
rm -rf "$OUTPUT_DIR"
mkdir -p "$OUTPUT_DIR"

# Build projects
log_info "Building MarketDataCollector v$VERSION ($CONFIGURATION)"
log_info "Target platforms: ${PLATFORMS[*]}"
log_info "Target projects: $TARGET_PROJECT"
echo ""

for rid in "${PLATFORMS[@]}"; do
    log_info "=== Building for $rid ==="

    if [ "$TARGET_PROJECT" == "all" ] || [ "$TARGET_PROJECT" == "collector" ]; then
        publish_project "$COLLECTOR_PROJECT" "$rid" "MarketDataCollector" "collector"
    fi

    if [ "$TARGET_PROJECT" == "all" ] || [ "$TARGET_PROJECT" == "ui" ]; then
        publish_project "$UI_PROJECT" "$rid" "MarketDataCollector.Ui" "ui"
    fi

    # Create package
    create_package "$rid"

    echo ""
done

# Summary
log_success "=== Build Complete ==="
echo ""
log_info "Output directory: $OUTPUT_DIR"
echo ""

# List outputs
if command -v tree &> /dev/null; then
    tree -L 3 "$OUTPUT_DIR"
else
    find "$OUTPUT_DIR" -type f -name "MarketDataCollector*" | head -20
fi

echo ""
log_info "To run the collector:"
if [[ "$OSTYPE" == "linux-gnu"* ]]; then
    echo "  $OUTPUT_DIR/linux-x64/collector/MarketDataCollector"
elif [[ "$OSTYPE" == "darwin"* ]]; then
    echo "  $OUTPUT_DIR/osx-x64/collector/MarketDataCollector"
else
    echo "  $OUTPUT_DIR/win-x64/collector/MarketDataCollector.exe"
fi
