#!/bin/bash
#
# Generate PNG assets from SVG source files for Market Data Collector UWP app.
# Requires: inkscape or rsvg-convert (librsvg)
#
# Usage: ./generate-assets.sh [inkscape|rsvg]
#

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SOURCE_DIR="$SCRIPT_DIR/Source"
TOOL="${1:-inkscape}"

# Colors for output
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

echo -e "${CYAN}Generating PNG assets from SVG sources...${NC}"
echo -e "${YELLOW}Using tool: $TOOL${NC}"
echo ""

convert_svg() {
    local svg_path="$1"
    local png_path="$2"
    local width="$3"
    local height="$4"

    if [ "$TOOL" = "inkscape" ]; then
        inkscape "$svg_path" --export-width="$width" --export-height="$height" --export-filename="$png_path" 2>/dev/null
    else
        rsvg-convert -w "$width" -h "$height" "$svg_path" > "$png_path"
    fi

    if [ $? -eq 0 ]; then
        echo -e "${GREEN}Created: $png_path${NC}"
    else
        echo "Warning: Failed to convert $svg_path"
    fi
}

# Asset definitions
declare -A ASSETS
ASSETS["Square44x44Logo"]="44 44 100,125,150,200,400"
ASSETS["Square150x150Logo"]="150 150 100,125,150,200,400"
ASSETS["LargeTile"]="310 310 100,125,150,200,400"
ASSETS["Wide310x150Logo"]="310 150 100,125,150,200,400"
ASSETS["SmallTile"]="71 71 100,125,150,200,400"
ASSETS["StoreLogo"]="50 50 100,125,150,200,400"
ASSETS["SplashScreen"]="620 300 100,125,150,200,400"
ASSETS["BadgeLogo"]="24 24 100"
ASSETS["AppIcon"]="256 256 100"

# Generate scaled assets
for asset_name in "${!ASSETS[@]}"; do
    IFS=' ' read -r base_width base_height scales <<< "${ASSETS[$asset_name]}"
    svg_path="$SOURCE_DIR/$asset_name.svg"

    if [ ! -f "$svg_path" ]; then
        echo "Warning: Source not found: $svg_path"
        continue
    fi

    IFS=',' read -ra scale_array <<< "$scales"
    for scale in "${scale_array[@]}"; do
        width=$((base_width * scale / 100))
        height=$((base_height * scale / 100))

        if [ "$scale" -eq 100 ] && [ "${#scale_array[@]}" -eq 1 ]; then
            png_path="$SCRIPT_DIR/$asset_name.png"
        else
            png_path="$SCRIPT_DIR/$asset_name.scale-$scale.png"
        fi

        convert_svg "$svg_path" "$png_path" "$width" "$height"
    done

    # Generate base .png without scale suffix for key assets
    if [[ "$asset_name" =~ ^(Square44x44Logo|Square150x150Logo|StoreLogo)$ ]]; then
        png_path="$SCRIPT_DIR/$asset_name.png"
        convert_svg "$svg_path" "$png_path" "$base_width" "$base_height"
    fi
done

# Generate target size icons
echo ""
echo -e "${CYAN}Generating target size icons...${NC}"
TARGET_SIZES=(16 24 32 48 256)
target_svg="$SOURCE_DIR/Square44x44Logo.svg"
unplated_svg="$SOURCE_DIR/Square44x44Logo_altform-unplated.svg"

if [ -f "$target_svg" ]; then
    for size in "${TARGET_SIZES[@]}"; do
        png_path="$SCRIPT_DIR/Square44x44Logo.targetsize-$size.png"
        convert_svg "$target_svg" "$png_path" "$size" "$size"

        if [ -f "$unplated_svg" ]; then
            png_path="$SCRIPT_DIR/Square44x44Logo.targetsize-${size}_altform-unplated.png"
            convert_svg "$unplated_svg" "$png_path" "$size" "$size"
        fi
    done
fi

# Generate contrast assets
echo ""
echo -e "${CYAN}Generating high contrast assets...${NC}"
for svg in "$SOURCE_DIR"/*_contrast-*.svg; do
    [ -f "$svg" ] || continue
    base_name=$(basename "$svg" .svg)

    if [[ "$base_name" =~ Square44x44 ]]; then
        convert_svg "$svg" "$SCRIPT_DIR/$base_name.png" 44 44
    elif [[ "$base_name" =~ AppIcon ]]; then
        convert_svg "$svg" "$SCRIPT_DIR/$base_name.png" 256 256
    fi
done

echo ""
echo -e "${GREEN}Asset generation complete!${NC}"
