#!/usr/bin/env bash
# Craft Console Linux Uninstaller
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
chmod +x "$SCRIPT_DIR/CraftConsole"

echo "Uninstalling Craft Console shortcuts..."
"$SCRIPT_DIR/CraftConsole" --uninstall "$@"

echo ""
echo "Craft Console shortcuts removed."
