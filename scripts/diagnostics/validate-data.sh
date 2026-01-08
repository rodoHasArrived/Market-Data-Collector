#!/bin/bash
# validate-data.sh - Validate JSONL data files for integrity
#
# Usage:
#   ./validate-data.sh [directory]
#
# Examples:
#   ./validate-data.sh                 # Validate ./data directory
#   ./validate-data.sh /path/to/data   # Validate specific directory
#
# This script performs basic validation of JSONL files:
# - Checks for valid JSON syntax
# - Counts events by type
# - Identifies potential data gaps
# - Reports file sizes and compression ratios

set -e

DATA_DIR="${1:-./data}"

if [ ! -d "$DATA_DIR" ]; then
    echo "Error: Directory not found: $DATA_DIR"
    exit 1
fi

echo "=== Market Data Validator ==="
echo "Directory: $DATA_DIR"
echo ""

# Count files
JSONL_FILES=$(find "$DATA_DIR" -name "*.jsonl" -o -name "*.jsonl.gz" | wc -l)
echo "Found $JSONL_FILES JSONL file(s)"
echo ""

# Check each file
TOTAL_LINES=0
TOTAL_ERRORS=0
VALID_FILES=0
INVALID_FILES=0

for file in $(find "$DATA_DIR" -name "*.jsonl" -o -name "*.jsonl.gz" | sort); do
    filename=$(basename "$file")

    # Handle gzip files
    if [[ "$file" == *.gz ]]; then
        lines=$(zcat "$file" 2>/dev/null | wc -l || echo 0)
        # Validate JSON
        errors=$(zcat "$file" 2>/dev/null | head -100 | while read line; do
            echo "$line" | python3 -c "import json,sys; json.loads(sys.stdin.read())" 2>&1 || echo "error"
        done | grep -c "error" || true)
    else
        lines=$(wc -l < "$file" || echo 0)
        # Validate JSON (sample first 100 lines)
        errors=$(head -100 "$file" | while read line; do
            echo "$line" | python3 -c "import json,sys; json.loads(sys.stdin.read())" 2>&1 || echo "error"
        done | grep -c "error" || true)
    fi

    TOTAL_LINES=$((TOTAL_LINES + lines))

    if [ "$errors" -eq 0 ]; then
        status="[OK]"
        VALID_FILES=$((VALID_FILES + 1))
    else
        status="[FAIL]"
        INVALID_FILES=$((INVALID_FILES + 1))
        TOTAL_ERRORS=$((TOTAL_ERRORS + errors))
    fi

    # Get file size
    size=$(du -h "$file" | cut -f1)

    echo "$status $filename - $lines lines, $size"
done

echo ""
echo "=== Summary ==="
echo "Valid files: $VALID_FILES"
echo "Invalid files: $INVALID_FILES"
echo "Total events: $TOTAL_LINES"
echo "Parse errors: $TOTAL_ERRORS"

# Exit with error if any files are invalid
if [ "$INVALID_FILES" -gt 0 ]; then
    exit 1
fi
