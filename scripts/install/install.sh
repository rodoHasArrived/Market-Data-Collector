#!/bin/bash
# =============================================================================
# Market Data Collector - Installation Script
# =============================================================================
#
# This script automates the installation and setup of Market Data Collector.
#
# Usage:
#   ./install.sh              # Interactive installation
#   ./install.sh --docker     # Docker-based installation
#   ./install.sh --native     # Native .NET installation
#   ./install.sh --help       # Show help
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
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"

# Print colored output
print_info() { echo -e "${BLUE}[INFO]${NC} $1"; }
print_success() { echo -e "${GREEN}[SUCCESS]${NC} $1"; }
print_warning() { echo -e "${YELLOW}[WARNING]${NC} $1"; }
print_error() { echo -e "${RED}[ERROR]${NC} $1"; }

# Header
print_header() {
    echo ""
    echo "╔══════════════════════════════════════════════════════════════════════╗"
    echo "║           Market Data Collector - Installation Script                ║"
    echo "║                         Version 1.1.0                                ║"
    echo "╚══════════════════════════════════════════════════════════════════════╝"
    echo ""
}

# Help message
show_help() {
    print_header
    echo "Usage: $0 [OPTIONS]"
    echo ""
    echo "Options:"
    echo "  --docker     Install using Docker (recommended for production)"
    echo "  --native     Install using native .NET SDK"
    echo "  --check      Check prerequisites only"
    echo "  --uninstall  Remove Docker containers and images"
    echo "  --help       Show this help message"
    echo ""
    echo "Examples:"
    echo "  $0                    # Interactive installation"
    echo "  $0 --docker           # Quick Docker installation"
    echo "  $0 --native           # Native .NET installation"
    echo ""
}

# Check if command exists
command_exists() {
    command -v "$1" >/dev/null 2>&1
}

# Check prerequisites
check_prerequisites() {
    print_info "Checking prerequisites..."

    local missing=()

    # Check Docker
    if command_exists docker; then
        print_success "Docker: $(docker --version)"
    else
        print_warning "Docker: Not installed"
        missing+=("docker")
    fi

    # Check Docker Compose
    if command_exists docker && docker compose version >/dev/null 2>&1; then
        print_success "Docker Compose: $(docker compose version --short 2>/dev/null || echo 'available')"
    else
        print_warning "Docker Compose: Not available"
        missing+=("docker-compose")
    fi

    # Check .NET SDK
    if command_exists dotnet; then
        local dotnet_version=$(dotnet --version)
        print_success ".NET SDK: $dotnet_version"

        # Check if version is 8.0+
        if [[ "$dotnet_version" < "8.0" ]]; then
            print_warning ".NET SDK version 8.0+ recommended (found: $dotnet_version)"
        fi
    else
        print_warning ".NET SDK: Not installed"
        missing+=("dotnet")
    fi

    # Check Git
    if command_exists git; then
        print_success "Git: $(git --version)"
    else
        print_warning "Git: Not installed"
        missing+=("git")
    fi

    # Check curl
    if command_exists curl; then
        print_success "curl: available"
    else
        print_warning "curl: Not installed"
        missing+=("curl")
    fi

    echo ""

    if [ ${#missing[@]} -eq 0 ]; then
        print_success "All prerequisites are installed!"
        return 0
    else
        print_warning "Missing prerequisites: ${missing[*]}"
        return 1
    fi
}

# Install prerequisites suggestions
suggest_prerequisites() {
    echo ""
    print_info "Installation suggestions for your platform:"
    echo ""

    if [[ "$OSTYPE" == "linux-gnu"* ]]; then
        echo "Ubuntu/Debian:"
        echo "  sudo apt update"
        echo "  sudo apt install -y docker.io docker-compose curl git"
        echo "  # For .NET SDK:"
        echo "  wget https://dot.net/v1/dotnet-install.sh"
        echo "  chmod +x dotnet-install.sh"
        echo "  ./dotnet-install.sh --channel 8.0"
        echo ""
        echo "Fedora/RHEL:"
        echo "  sudo dnf install -y docker docker-compose curl git"
        echo "  sudo dnf install -y dotnet-sdk-8.0"
    elif [[ "$OSTYPE" == "darwin"* ]]; then
        echo "macOS (using Homebrew):"
        echo "  brew install --cask docker"
        echo "  brew install git curl"
        echo "  brew install --cask dotnet-sdk"
    fi
    echo ""
}

# Setup configuration
setup_config() {
    print_info "Setting up configuration..."

    if [ ! -f "$PROJECT_ROOT/config/appsettings.json" ]; then
        if [ -f "$PROJECT_ROOT/config/appsettings.sample.json" ]; then
            cp "$PROJECT_ROOT/config/appsettings.sample.json" "$PROJECT_ROOT/config/appsettings.json"
            print_success "Created appsettings.json from template"
            print_warning "Remember to edit config/appsettings.json with your API credentials"
        else
            print_error "config/appsettings.sample.json not found"
            return 1
        fi
    else
        print_info "appsettings.json already exists, skipping..."
    fi

    # Create data directory
    mkdir -p "$PROJECT_ROOT/data"
    mkdir -p "$PROJECT_ROOT/logs"
    print_success "Created data and logs directories"
}

# Docker installation
install_docker() {
    print_info "Installing with Docker..."

    # Build image
    print_info "Building Docker image..."
    cd "$PROJECT_ROOT"
    docker build -t marketdatacollector:latest .

    if [ $? -eq 0 ]; then
        print_success "Docker image built successfully"
    else
        print_error "Failed to build Docker image"
        return 1
    fi

    # Setup config
    setup_config

    # Start container
    print_info "Starting container..."
    docker compose up -d

    if [ $? -eq 0 ]; then
        print_success "Container started successfully"
        echo ""
        echo "╔══════════════════════════════════════════════════════════════════════╗"
        echo "║                    Installation Complete!                            ║"
        echo "╠══════════════════════════════════════════════════════════════════════╣"
        echo "║  Dashboard:   http://localhost:8080                                  ║"
        echo "║  Metrics:     http://localhost:8080/metrics                          ║"
        echo "║  Status:      http://localhost:8080/status                           ║"
        echo "║  Health:      http://localhost:8080/health                           ║"
        echo "╠══════════════════════════════════════════════════════════════════════╣"
        echo "║  View logs:   docker compose logs -f                                 ║"
        echo "║  Stop:        docker compose down                                    ║"
        echo "║  Restart:     docker compose restart                                 ║"
        echo "╚══════════════════════════════════════════════════════════════════════╝"
    else
        print_error "Failed to start container"
        return 1
    fi
}

# Native .NET installation
install_native() {
    print_info "Installing with native .NET..."

    # Check .NET SDK
    if ! command_exists dotnet; then
        print_error ".NET SDK is required for native installation"
        suggest_prerequisites
        return 1
    fi

    # Restore and build
    print_info "Restoring dependencies..."
    cd "$PROJECT_ROOT"
    dotnet restore src/MarketDataCollector/MarketDataCollector.csproj

    print_info "Building project..."
    dotnet build src/MarketDataCollector/MarketDataCollector.csproj -c Release

    if [ $? -eq 0 ]; then
        print_success "Build completed successfully"
    else
        print_error "Build failed"
        return 1
    fi

    # Setup config
    setup_config

    # Run tests
    print_info "Running self-tests..."
    dotnet run --project src/MarketDataCollector/MarketDataCollector.csproj --configuration Release -- --selftest

    echo ""
    echo "╔══════════════════════════════════════════════════════════════════════╗"
    echo "║                    Installation Complete!                            ║"
    echo "╠══════════════════════════════════════════════════════════════════════╣"
    echo "║  Start dashboard:                                                    ║"
    echo "║    dotnet run --project src/MarketDataCollector/MarketDataCollector.csproj -- --ui                           ║"
    echo "║                                                                      ║"
    echo "║  Or use the publish script for a standalone executable:              ║"
    echo "║    ./publish.sh linux-x64                                            ║"
    echo "║    ./publish/linux-x64/MarketDataCollector --ui                      ║"
    echo "╚══════════════════════════════════════════════════════════════════════╝"
}

# Uninstall Docker installation
uninstall_docker() {
    print_info "Uninstalling Docker containers and images..."

    cd "$PROJECT_ROOT"

    # Stop containers
    if docker compose ps -q 2>/dev/null | grep -q .; then
        print_info "Stopping containers..."
        docker compose down
    fi

    # Remove image
    if docker images marketdatacollector:latest -q | grep -q .; then
        print_info "Removing Docker image..."
        docker rmi marketdatacollector:latest
    fi

    print_success "Uninstallation complete"
    print_warning "Data directory (./data) was preserved. Remove manually if needed."
}

# Interactive installation
interactive_install() {
    print_header

    check_prerequisites
    echo ""

    echo "Choose installation method:"
    echo "  1) Docker (recommended for production)"
    echo "  2) Native .NET SDK"
    echo "  3) Check prerequisites only"
    echo "  4) Exit"
    echo ""
    read -p "Enter choice [1-4]: " choice

    case $choice in
        1)
            install_docker
            ;;
        2)
            install_native
            ;;
        3)
            check_prerequisites
            suggest_prerequisites
            ;;
        4)
            echo "Exiting..."
            exit 0
            ;;
        *)
            print_error "Invalid choice"
            exit 1
            ;;
    esac
}

# Main
main() {
    case "${1:-}" in
        --docker)
            print_header
            check_prerequisites || true
            install_docker
            ;;
        --native)
            print_header
            check_prerequisites || true
            install_native
            ;;
        --check)
            print_header
            check_prerequisites
            suggest_prerequisites
            ;;
        --uninstall)
            print_header
            uninstall_docker
            ;;
        --help|-h)
            show_help
            ;;
        "")
            interactive_install
            ;;
        *)
            print_error "Unknown option: $1"
            show_help
            exit 1
            ;;
    esac
}

main "$@"
