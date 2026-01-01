#!/bin/bash
set -e

# Market Data Collector - One-Click Installer (Linux/macOS)
# Usage: curl -fsSL https://raw.githubusercontent.com/rodoHasArrived/Test/main/MarketDataCollector/install.sh | bash

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

# Configuration
GITHUB_REPO="rodoHasArrived/Test"
VERSION="${VERSION:-latest}"
INSTALL_PATH="${INSTALL_PATH:-$HOME/.local/share/MarketDataCollector}"
ADD_TO_PATH="${ADD_TO_PATH:-true}"
CREATE_SHORTCUT="${CREATE_TO_PATH:-true}"

# Detect OS and architecture
detect_platform() {
    local os="$(uname -s)"
    local arch="$(uname -m)"

    case "$os" in
        Linux*)
            case "$arch" in
                x86_64) echo "linux-x64" ;;
                aarch64|arm64) echo "linux-arm64" ;;
                *) echo "unsupported" ;;
            esac
            ;;
        Darwin*)
            case "$arch" in
                x86_64) echo "osx-x64" ;;
                arm64) echo "osx-arm64" ;;
                *) echo "unsupported" ;;
            esac
            ;;
        *)
            echo "unsupported"
            ;;
    esac
}

# Banner
echo -e "${CYAN}"
cat << "EOF"
╔══════════════════════════════════════════════════════════════════════╗
║         Market Data Collector - Linux/macOS Installer               ║
╚══════════════════════════════════════════════════════════════════════╝
EOF
echo -e "${NC}"

# Detect platform
PLATFORM=$(detect_platform)
if [ "$PLATFORM" = "unsupported" ]; then
    echo -e "${RED}✗ Unsupported platform: $(uname -s) $(uname -m)${NC}"
    echo -e "${YELLOW}Please build from source:${NC}"
    echo "  git clone https://github.com/$GITHUB_REPO.git"
    echo "  cd Test/MarketDataCollector"
    echo "  ./publish.sh"
    exit 1
fi

echo -e "${GREEN}✓ Detected platform: $PLATFORM${NC}"

# Set download URL
if [ "$VERSION" = "latest" ]; then
    DOWNLOAD_URL="https://github.com/$GITHUB_REPO/releases/latest/download/MarketDataCollector-${PLATFORM}.tar.gz"
else
    DOWNLOAD_URL="https://github.com/$GITHUB_REPO/releases/download/$VERSION/MarketDataCollector-${PLATFORM}.tar.gz"
fi

# Create temp directory
TEMP_DIR=$(mktemp -d)
ARCHIVE_FILE="$TEMP_DIR/MarketDataCollector.tar.gz"

cleanup() {
    rm -rf "$TEMP_DIR"
}
trap cleanup EXIT

echo ""
echo -e "${YELLOW}[1/7] Downloading Market Data Collector...${NC}"
echo -e "${NC}URL: $DOWNLOAD_URL${NC}"

if command -v wget &> /dev/null; then
    wget -q --show-progress "$DOWNLOAD_URL" -O "$ARCHIVE_FILE" || {
        echo -e "${RED}✗ Download failed${NC}"
        echo -e "${YELLOW}Note: If the release doesn't exist yet, please build from source${NC}"
        exit 1
    }
elif command -v curl &> /dev/null; then
    curl -L --progress-bar "$DOWNLOAD_URL" -o "$ARCHIVE_FILE" || {
        echo -e "${RED}✗ Download failed${NC}"
        echo -e "${YELLOW}Note: If the release doesn't exist yet, please build from source${NC}"
        exit 1
    }
else
    echo -e "${RED}✗ Neither wget nor curl found. Please install one of them.${NC}"
    exit 1
fi

echo -e "${GREEN}✓ Download complete${NC}"

# Extract
echo ""
echo -e "${YELLOW}[2/7] Extracting files...${NC}"

# Backup existing installation
if [ -d "$INSTALL_PATH" ]; then
    BACKUP_PATH="$INSTALL_PATH.backup.$(date +%Y%m%d-%H%M%S)"
    echo -e "${YELLOW}⚠ Installation directory exists. Creating backup...${NC}"
    mv "$INSTALL_PATH" "$BACKUP_PATH"
    echo -e "${NC}  Backup created: $BACKUP_PATH${NC}"
fi

mkdir -p "$INSTALL_PATH"
tar -xzf "$ARCHIVE_FILE" -C "$INSTALL_PATH"
echo -e "${GREEN}✓ Files extracted to: $INSTALL_PATH${NC}"

# Make executable
echo ""
echo -e "${YELLOW}[3/7] Setting permissions...${NC}"
chmod +x "$INSTALL_PATH/MarketDataCollector"
echo -e "${GREEN}✓ Executable permissions set${NC}"

# Set up configuration
echo ""
echo -e "${YELLOW}[4/7] Setting up configuration...${NC}"
CONFIG_PATH="$INSTALL_PATH/appsettings.json"
SAMPLE_CONFIG="$INSTALL_PATH/appsettings.sample.json"

if [ ! -f "$CONFIG_PATH" ]; then
    if [ -f "$SAMPLE_CONFIG" ]; then
        cp "$SAMPLE_CONFIG" "$CONFIG_PATH"
        echo -e "${GREEN}✓ Configuration file created from sample${NC}"
    else
        # Create minimal config
        cat > "$CONFIG_PATH" << 'EOL'
{
  "DataRoot": "data",
  "Compress": false,
  "DataSource": "Alpaca",
  "Symbols": []
}
EOL
        echo -e "${GREEN}✓ Default configuration file created${NC}"
    fi
else
    echo -e "${GREEN}✓ Existing configuration preserved${NC}"
fi

# Create data directory
echo ""
echo -e "${YELLOW}[5/7] Creating data directory...${NC}"
mkdir -p "$INSTALL_PATH/data"
echo -e "${GREEN}✓ Data directory created: $INSTALL_PATH/data${NC}"

# Add to PATH
echo ""
if [ "$ADD_TO_PATH" = "true" ]; then
    echo -e "${YELLOW}[6/7] Adding to PATH...${NC}"

    # Determine shell config file
    if [ -n "$BASH_VERSION" ]; then
        SHELL_CONFIG="$HOME/.bashrc"
    elif [ -n "$ZSH_VERSION" ]; then
        SHELL_CONFIG="$HOME/.zshrc"
    else
        SHELL_CONFIG="$HOME/.profile"
    fi

    # Add to PATH if not already there
    if ! grep -q "MarketDataCollector" "$SHELL_CONFIG" 2>/dev/null; then
        echo "" >> "$SHELL_CONFIG"
        echo "# Market Data Collector" >> "$SHELL_CONFIG"
        echo "export PATH=\"\$PATH:$INSTALL_PATH\"" >> "$SHELL_CONFIG"
        echo -e "${GREEN}✓ Added to PATH in $SHELL_CONFIG${NC}"
        echo -e "${NC}  (Run 'source $SHELL_CONFIG' or restart your terminal)${NC}"

        # Add to current session
        export PATH="$PATH:$INSTALL_PATH"
    else
        echo -e "${GREEN}✓ Already in PATH${NC}"
    fi
else
    echo -e "${NC}[6/7] Skipping PATH setup${NC}"
fi

# Create desktop shortcut
echo ""
if [ "$CREATE_SHORTCUT" = "true" ]; then
    echo -e "${YELLOW}[7/7] Creating application launcher...${NC}"

    # Linux desktop entry
    if [ "$(uname -s)" = "Linux" ] && [ -d "$HOME/.local/share/applications" ]; then
        DESKTOP_FILE="$HOME/.local/share/applications/marketdatacollector.desktop"
        cat > "$DESKTOP_FILE" << EOL
[Desktop Entry]
Version=1.0
Type=Application
Name=Market Data Collector
Comment=Real-time market data collection
Exec=$INSTALL_PATH/MarketDataCollector --ui
Icon=$INSTALL_PATH/icon.png
Terminal=true
Categories=Development;Finance;
EOL
        chmod +x "$DESKTOP_FILE"
        echo -e "${GREEN}✓ Desktop launcher created${NC}"
    fi

    # macOS app bundle (simplified)
    if [ "$(uname -s)" = "Darwin" ]; then
        # Create shell script launcher
        LAUNCHER="$HOME/Desktop/MarketDataCollector.command"
        cat > "$LAUNCHER" << EOL
#!/bin/bash
cd "$INSTALL_PATH"
./MarketDataCollector --ui
EOL
        chmod +x "$LAUNCHER"
        echo -e "${GREEN}✓ Launcher created on Desktop${NC}"
    fi
else
    echo -e "${NC}[7/7] Skipping shortcut creation${NC}"
fi

# Success message
echo ""
echo -e "${CYAN}"
cat << EOF
╔══════════════════════════════════════════════════════════════════════╗
║                    Installation Complete! ✓                          ║
╚══════════════════════════════════════════════════════════════════════╝

Installation Directory: $INSTALL_PATH

Quick Start:
  1. Open a new terminal (to pick up PATH changes)
  2. Run: MarketDataCollector --ui
  3. Open browser to: http://localhost:8080

Or run from the installation directory:
  cd $INSTALL_PATH
  ./MarketDataCollector --ui

Documentation:
  - User Guide: $INSTALL_PATH/HELP.md
  - Configuration: $INSTALL_PATH/appsettings.json
  - Getting Started: $INSTALL_PATH/docs/GETTING_STARTED.md

Next Steps:
  1. Configure your data provider (IB or Alpaca)
  2. Add symbols to track
  3. Start collecting data!

For help: MarketDataCollector --help

EOF
echo -e "${NC}"
