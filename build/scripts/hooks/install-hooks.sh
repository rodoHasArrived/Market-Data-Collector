#!/bin/sh
# Installs git hooks from build/scripts/hooks/ into .git/hooks/
# Run from the repository root: ./build/scripts/hooks/install-hooks.sh

set -e

REPO_ROOT="$(git rev-parse --show-toplevel)"
HOOKS_SRC="$REPO_ROOT/build/scripts/hooks"
HOOKS_DST="$REPO_ROOT/.git/hooks"

for hook in "$HOOKS_SRC"/pre-commit "$HOOKS_SRC"/commit-msg; do
    if [ -f "$hook" ]; then
        name="$(basename "$hook")"
        cp "$hook" "$HOOKS_DST/$name"
        chmod +x "$HOOKS_DST/$name"
        echo "Installed $name hook."
    fi
done

echo "Git hooks installed successfully."
