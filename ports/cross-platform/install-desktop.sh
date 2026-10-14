#!/usr/bin/env bash
# Craft Console Linux Desktop Shortcut Installer (No root required)
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
chmod +x "$SCRIPT_DIR/CraftConsole"

echo "Setting up Craft Console desktop entries..."
"$SCRIPT_DIR/CraftConsole" --setup

echo ""
echo "Installation complete!"
echo "• Added 'Craft Console' to application menu (~/.local/share/applications)"
echo "• Added 'EffectCraft (with Console)' to application menu"
echo "• Desktop shortcut placed on ~/Desktop"
